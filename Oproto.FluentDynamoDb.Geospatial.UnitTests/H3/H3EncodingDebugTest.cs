using System.Reflection;
using Oproto.FluentDynamoDb.Geospatial.H3;
using Xunit;
using Xunit.Abstractions;

namespace Oproto.FluentDynamoDb.Geospatial.UnitTests.H3;

public class H3EncodingDebugTest
{
    private readonly ITestOutputHelper _output;

    public H3EncodingDebugTest(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public void Debug_Encoding_TraceBaseCellSelection()
    {
        var lat = 14.947707264183059;
        var lon = 58.099686036545755;
        var resolution = 0; // Start with resolution 0 to see base cell selection

        _output.WriteLine($"Original: ({lat:F6}, {lon:F6})");

        var encoderType = typeof(H3Encoder);
        
        // Get GeoToHex2d
        var geoToHex2dMethod = encoderType.GetMethod("GeoToHex2d",
            BindingFlags.NonPublic | BindingFlags.Static);
        
        if (geoToHex2dMethod != null)
        {
            var result = geoToHex2dMethod.Invoke(null, new object[] { lat, lon, resolution });
            _output.WriteLine($"GeoToHex2d(res=0): {result}");
        }

        // Encode at resolution 0
        var h3Index0 = H3Encoder.Encode(lat, lon, 0);
        _output.WriteLine($"H3 Index (res=0): {h3Index0}");
        
        var (decoded0Lat, decoded0Lon) = H3Encoder.Decode(h3Index0);
        _output.WriteLine($"Decoded (res=0): ({decoded0Lat:F6}, {decoded0Lon:F6})");
        _output.WriteLine($"Error (res=0): {Math.Sqrt((decoded0Lat-lat)*(decoded0Lat-lat) + (decoded0Lon-lon)*(decoded0Lon-lon)):F4}°");

        // The base cell for this location
        // Base cell 49 is a pentagon at face 14
        // Let's check what the expected base cell should be for face 0
        
        _output.WriteLine("\nBase cell 49 info:");
        _output.WriteLine("  - Pentagon: true");
        _output.WriteLine("  - Home face: 14");
        _output.WriteLine("  - IJK on home face: (2, 0, 0)");
        _output.WriteLine("  - CW offset faces: 0, 9");
        
        // Check if the point is actually closer to face 0 or face 14
        // Face 0 center: ~(46°, 71°) 
        // Face 14 center: ~(-24°, 72°)
        _output.WriteLine("\nFace center distances:");
        _output.WriteLine($"  Face 0 center: ~(46°, 71°)");
        _output.WriteLine($"  Face 14 center: ~(-24°, 72°)");
        _output.WriteLine($"  Point: ({lat:F2}°, {lon:F2}°)");
        
        var distToFace0 = Math.Sqrt((lat - 46) * (lat - 46) + (lon - 71) * (lon - 71));
        var distToFace14 = Math.Sqrt((lat - (-24)) * (lat - (-24)) + (lon - 72) * (lon - 72));
        _output.WriteLine($"  Approx dist to face 0: {distToFace0:F2}°");
        _output.WriteLine($"  Approx dist to face 14: {distToFace14:F2}°");
    }
}
