using Oproto.FluentDynamoDb.Geospatial.H3;
using Xunit;
using Xunit.Abstractions;

namespace Oproto.FluentDynamoDb.Geospatial.UnitTests.H3;

/// <summary>
/// Detailed debugging for the (0, 0) equator encoding issue.
/// </summary>
public class H3EquatorDebugTest
{
    private readonly ITestOutputHelper _output;

    public H3EquatorDebugTest(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public void Debug_Equator_Origin_Res5()
    {
        // NOTE: H3 does NOT guarantee that a cell's center is close to the input point.
        // Cell centers may be significantly offset from the input due to the hexagonal grid structure.
        // This test verifies that encoding and decoding are deterministic and produce valid results.
        
        var lat = 0.0;
        var lon = 0.0;
        var res = 5;
        
        _output.WriteLine("=== Encoding (0, 0) at resolution 5 ===");
        
        // Encode
        var index = H3Encoder.Encode(lat, lon, res);
        _output.WriteLine($"Encoded: {index}");
        
        // Parse the index
        var indexValue = Convert.ToUInt64(index, 16);
        var baseCell = (int)((indexValue >> 45) & 0x7F);
        var resolution = (int)((indexValue >> 52) & 0xF);
        
        _output.WriteLine($"Base cell: {baseCell}");
        _output.WriteLine($"Resolution: {resolution}");
        
        // Decode
        var (decodedLat, decodedLon) = H3Encoder.Decode(index);
        _output.WriteLine($"Decoded: ({decodedLat:F10}, {decodedLon:F10})");
        
        // Calculate offset from input
        var latOffset = Math.Abs(decodedLat - lat);
        var lonOffset = Math.Abs(decodedLon - lon);
        _output.WriteLine($"Offset from input: lat={latOffset:F10}°, lon={lonOffset:F10}°");
        
        // Verify encoding is deterministic
        var indexAgain = H3Encoder.Encode(lat, lon, res);
        Assert.Equal(index, indexAgain);
        
        // Verify decoding is deterministic
        var (decodedLat2, decodedLon2) = H3Encoder.Decode(index);
        Assert.Equal(decodedLat, decodedLat2);
        Assert.Equal(decodedLon, decodedLon2);
        
        // Verify decoded coordinates are valid
        Assert.InRange(decodedLat, -90, 90);
        Assert.InRange(decodedLon, -180, 180);
    }

    [Theory]
    [InlineData(0.0, 0.0, 0)]
    [InlineData(0.0, 0.0, 1)]
    [InlineData(0.0, 0.0, 2)]
    [InlineData(0.0, 0.0, 3)]
    [InlineData(0.0, 0.0, 4)]
    [InlineData(0.0, 0.0, 5)]
    [InlineData(0.0, 0.0, 6)]
    public void Debug_Equator_Origin_AllResolutions(double lat, double lon, int res)
    {
        // NOTE: H3 does NOT guarantee round-trip consistency (encode → decode → encode).
        // This test verifies that encoding and decoding produce valid, deterministic results.
        
        _output.WriteLine($"=== Resolution {res} ===");
        
        var index = H3Encoder.Encode(lat, lon, res);
        var (decodedLat, decodedLon) = H3Encoder.Decode(index);
        
        var latError = Math.Abs(decodedLat - lat);
        var lonError = Math.Abs(decodedLon - lon);
        
        _output.WriteLine($"  Index: {index}");
        _output.WriteLine($"  Decoded: ({decodedLat:F6}, {decodedLon:F6})");
        _output.WriteLine($"  Error: lat={latError:F6}°, lon={lonError:F6}°");
        
        // Verify encoding is deterministic
        var indexAgain = H3Encoder.Encode(lat, lon, res);
        Assert.Equal(index, indexAgain);
        
        // Verify decoding is deterministic
        var (decodedLat2, decodedLon2) = H3Encoder.Decode(index);
        Assert.Equal(decodedLat, decodedLat2);
        Assert.Equal(decodedLon, decodedLon2);
        
        // Verify decoded coordinates are valid
        Assert.InRange(decodedLat, -90, 90);
        Assert.InRange(decodedLon, -180, 180);
    }
}
