using Oproto.FluentDynamoDb.Geospatial.H3;
using Xunit;
using Xunit.Abstractions;

namespace Oproto.FluentDynamoDb.Geospatial.UnitTests.H3;

public class H3Resolution0Test
{
    private readonly ITestOutputHelper _output;

    public H3Resolution0Test(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public void Resolution0RoundTrip()
    {
        // Test at resolution 0 (base cells) - simpler case
        var lat = 45.0;
        var lon = -122.0;
        var res = 0;

        _output.WriteLine($"Input: lat={lat}, lon={lon}, res={res}");

        // Encode
        var h3Index = H3Encoder.Encode(lat, lon, res);
        _output.WriteLine($"H3 Index: {h3Index}");

        // Decode
        var (decodedLat, decodedLon) = H3Encoder.Decode(h3Index);
        _output.WriteLine($"Decoded: lat={decodedLat}, lon={decodedLon}");

        // Re-encode
        var h3Index2 = H3Encoder.Encode(decodedLat, decodedLon, res);
        _output.WriteLine($"Re-encoded: {h3Index2}");

        // Check
        _output.WriteLine($"Match: {h3Index == h3Index2}");
        
        Assert.Equal(h3Index, h3Index2);
    }
    
    [Fact]
    public void Resolution1RoundTrip()
    {
        // Test at resolution 1
        var lat = 45.0;
        var lon = -122.0;
        var res = 1;

        _output.WriteLine($"Input: lat={lat}, lon={lon}, res={res}");

        // Encode
        var h3Index = H3Encoder.Encode(lat, lon, res);
        _output.WriteLine($"H3 Index: {h3Index}");

        // Decode
        var (decodedLat, decodedLon) = H3Encoder.Decode(h3Index);
        _output.WriteLine($"Decoded: lat={decodedLat}, lon={decodedLon}");

        // Re-encode
        var h3Index2 = H3Encoder.Encode(decodedLat, decodedLon, res);
        _output.WriteLine($"Re-encoded: {h3Index2}");

        // Check
        _output.WriteLine($"Match: {h3Index == h3Index2}");
        
        Assert.Equal(h3Index, h3Index2);
    }
}
