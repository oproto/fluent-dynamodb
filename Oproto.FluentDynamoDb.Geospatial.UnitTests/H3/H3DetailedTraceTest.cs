using System.Reflection;
using Oproto.FluentDynamoDb.Geospatial.H3;
using Xunit;
using Xunit.Abstractions;

namespace Oproto.FluentDynamoDb.Geospatial.UnitTests.H3;

/// <summary>
/// Detailed trace of the encoding process to debug coordinate transformation.
/// </summary>
public class H3DetailedTraceTest
{
    private readonly ITestOutputHelper _output;

    public H3DetailedTraceTest(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public void Trace_Encoding_Steps_For_20_123()
    {
        var lat = 20.0;
        var lon = 123.0;
        var res = 2;
        
        _output.WriteLine($"=== Encoding (lat={lat}, lon={lon}, res={res}) ===");
        _output.WriteLine($"Expected: 824b9ffffffffff");
        _output.WriteLine("");
        
        // Step 1: Convert to radians
        var latRad = lat * Math.PI / 180.0;
        var lonRad = lon * Math.PI / 180.0;
        _output.WriteLine($"Radians: lat={latRad:F10}, lon={lonRad:F10}");
        
        // Step 2: Convert to XYZ
        var x = Math.Cos(latRad) * Math.Cos(lonRad);
        var y = Math.Cos(latRad) * Math.Sin(lonRad);
        var z = Math.Sin(latRad);
        _output.WriteLine($"XYZ: x={x:F10}, y={y:F10}, z={z:F10}");
        
        // Step 3: Find face (we need to call the encoder to get this)
        // For now, let's manually check which face
        // Face 5 is around (lat=20, lon=123) based on the icosahedron
        
        // Let's just encode and decode to see what we get
        var encoded = H3Encoder.Encode(lat, lon, res);
        _output.WriteLine($"");
        _output.WriteLine($"Our encoding: {encoded}");
        
        var index = Convert.ToUInt64(encoded, 16);
        var bc = (int)((index >> 45) & 0x7F);
        _output.WriteLine($"Base cell: {bc}");
        
        for (int r = 1; r <= res; r++)
        {
            var digitOffset = (15 - r) * 3;
            var digit = (int)((index >> digitOffset) & 0x7);
            _output.WriteLine($"Digit {r}: {digit}");
        }
        
        // Now decode our encoding
        var (decodedLat, decodedLon) = H3Encoder.Decode(encoded);
        _output.WriteLine($"");
        _output.WriteLine($"Our encoding decodes to: lat={decodedLat:F10}, lon={decodedLon:F10}");
        _output.WriteLine($"Error: lat={Math.Abs(lat - decodedLat):F10}, lon={Math.Abs(lon - decodedLon):F10}");
        
        // Now decode the expected encoding
        var expected = "824b9ffffffffff";
        var (expectedLat, expectedLon) = H3Encoder.Decode(expected);
        _output.WriteLine($"");
        _output.WriteLine($"Expected encoding decodes to: lat={expectedLat:F10}, lon={expectedLon:F10}");
        _output.WriteLine($"Expected error from input: lat={Math.Abs(lat - expectedLat):F10}, lon={Math.Abs(lon - expectedLon):F10}");
        
        // Parse expected
        var expectedIndex = Convert.ToUInt64(expected, 16);
        var expectedBc = (int)((expectedIndex >> 45) & 0x7F);
        _output.WriteLine($"");
        _output.WriteLine($"Expected base cell: {expectedBc}");
        
        for (int r = 1; r <= res; r++)
        {
            var digitOffset = (15 - r) * 3;
            var digit = (int)((expectedIndex >> digitOffset) & 0x7);
            _output.WriteLine($"Expected digit {r}: {digit}");
        }
    }
}
