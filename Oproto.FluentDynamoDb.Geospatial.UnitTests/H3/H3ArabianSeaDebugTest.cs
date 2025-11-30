using System.Reflection;
using Oproto.FluentDynamoDb.Geospatial.H3;
using Xunit;
using Xunit.Abstractions;

namespace Oproto.FluentDynamoDb.Geospatial.UnitTests.H3;

public class H3ArabianSeaDebugTest
{
    private readonly ITestOutputHelper _output;

    public H3ArabianSeaDebugTest(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public void Debug_ArabianSea_EncodeDecode()
    {
        var lat = 14.947707264183059;
        var lon = 58.099686036545755;
        var resolution = 10;

        _output.WriteLine($"Original: ({lat:F6}, {lon:F6})");
        _output.WriteLine($"Resolution: {resolution}");

        // Encode
        var h3Index = H3Encoder.Encode(lat, lon, resolution);
        _output.WriteLine($"H3 Index: {h3Index}");

        // Decode
        var (decodedLat, decodedLon) = H3Encoder.Decode(h3Index);
        _output.WriteLine($"Decoded: ({decodedLat:F6}, {decodedLon:F6})");

        var latError = Math.Abs(decodedLat - lat);
        var lonError = Math.Abs(decodedLon - lon);
        _output.WriteLine($"Error: lat={latError:F6}°, lon={lonError:F6}°");

        // Use reflection to call internal methods for debugging
        var encoderType = typeof(H3Encoder);
        var assembly = encoderType.Assembly;
        
        // Get GeoToClosestFace to see which face is selected
        var geoToClosestFaceMethod = encoderType.GetMethod("GeoToClosestFace", 
            BindingFlags.NonPublic | BindingFlags.Static);
        if (geoToClosestFaceMethod != null)
        {
            var result = geoToClosestFaceMethod.Invoke(null, new object[] { lat, lon });
            _output.WriteLine($"GeoToClosestFace result: {result}");
        }

        // Get ParseH3IndexWithFace to see what's decoded
        var stringToH3IndexMethod = encoderType.GetMethod("StringToH3Index",
            BindingFlags.NonPublic | BindingFlags.Static);
        var parseMethod = encoderType.GetMethod("ParseH3IndexWithFace",
            BindingFlags.NonPublic | BindingFlags.Static);
        
        if (stringToH3IndexMethod != null && parseMethod != null)
        {
            var index = stringToH3IndexMethod.Invoke(null, new object[] { h3Index });
            _output.WriteLine($"H3 Index (ulong): {index}");
            
            var parseResult = parseMethod.Invoke(null, new object?[] { index });
            _output.WriteLine($"ParseH3IndexWithFace result: {parseResult}");
        }

        // Try different resolutions to see if the issue is resolution-specific
        _output.WriteLine("\nTrying different resolutions:");
        for (int res = 0; res <= 10; res++)
        {
            var idx = H3Encoder.Encode(lat, lon, res);
            var (dLat, dLon) = H3Encoder.Decode(idx);
            var err = Math.Sqrt((dLat - lat) * (dLat - lat) + (dLon - lon) * (dLon - lon));
            _output.WriteLine($"  Res {res,2}: idx={idx}, decoded=({dLat:F4}, {dLon:F4}), error={err:F4}°");
        }
    }

    [Fact]
    public void Debug_KnownGoodLocation_EncodeDecode()
    {
        // Test with a location that should work (e.g., San Francisco)
        var lat = 37.7749;
        var lon = -122.4194;
        var resolution = 10;

        _output.WriteLine($"San Francisco: ({lat:F6}, {lon:F6})");

        var h3Index = H3Encoder.Encode(lat, lon, resolution);
        _output.WriteLine($"H3 Index: {h3Index}");

        var (decodedLat, decodedLon) = H3Encoder.Decode(h3Index);
        _output.WriteLine($"Decoded: ({decodedLat:F6}, {decodedLon:F6})");

        var latError = Math.Abs(decodedLat - lat);
        var lonError = Math.Abs(decodedLon - lon);
        _output.WriteLine($"Error: lat={latError:F6}°, lon={lonError:F6}°");

        // The error should be small (less than 1 degree for any resolution)
        Assert.True(latError < 1.0, $"Latitude error {latError}° is too large");
        Assert.True(lonError < 1.0, $"Longitude error {lonError}° is too large");
    }
}
