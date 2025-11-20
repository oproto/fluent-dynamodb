using Oproto.FluentDynamoDb.Geospatial.H3;
using Xunit;
using Xunit.Abstractions;

namespace Oproto.FluentDynamoDb.Geospatial.UnitTests.H3;

public class H3FailingCaseTest
{
    private readonly ITestOutputHelper _output;

    public H3FailingCaseTest(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public void FailingCase_86_28_Minus180_Res4()
    {
        // The exact failing case from the property test
        var lat = 86.28009407999305;
        var lon = -180.0;
        var res = 4;

        _output.WriteLine($"Input: lat={lat}, lon={lon}, res={res}");

        // Encode
        var h3Index = H3Encoder.Encode(lat, lon, res);
        _output.WriteLine($"H3 Index: {h3Index}");

        // Decode
        var (decodedLat, decodedLon) = H3Encoder.Decode(h3Index);
        _output.WriteLine($"Decoded: lat={decodedLat}, lon={decodedLon}");
        _output.WriteLine($"Error: lat_diff={Math.Abs(lat - decodedLat)}, lon_diff={Math.Abs(lon - decodedLon)}");

        // Re-encode
        var h3Index2 = H3Encoder.Encode(decodedLat, decodedLon, res);
        _output.WriteLine($"Re-encoded: {h3Index2}");

        // Check
        _output.WriteLine($"Match: {h3Index == h3Index2}");
        
        // The issue: the decoded center is way off from the input
        // This suggests the cell we encoded to is not the cell we decoded from
        
        // Let's also try encoding a nearby point
        var nearbyLat = 85.0;
        var nearbyLon = -180.0;
        var nearbyIndex = H3Encoder.Encode(nearbyLat, nearbyLon, res);
        _output.WriteLine($"Nearby (85, -180): {nearbyIndex}");
        
        var (nearbyDecLat, nearbyDecLon) = H3Encoder.Decode(nearbyIndex);
        var nearbyIndex2 = H3Encoder.Encode(nearbyDecLat, nearbyDecLon, res);
        _output.WriteLine($"Nearby round-trip: {nearbyIndex} -> {nearbyIndex2}, match={nearbyIndex == nearbyIndex2}");
    }
    
    [Theory]
    [InlineData(80.0, -180.0, 4)]
    [InlineData(85.0, -180.0, 4)]
    [InlineData(86.0, -180.0, 4)]
    [InlineData(86.28009407999305, -180.0, 4)]
    [InlineData(87.0, -180.0, 4)]
    [InlineData(89.0, -180.0, 4)]
    public void HighLatitude_Minus180_Res4_RoundTrip(double lat, double lon, int res)
    {
        _output.WriteLine($"Testing: lat={lat}, lon={lon}, res={res}");

        var h3Index = H3Encoder.Encode(lat, lon, res);
        var (decodedLat, decodedLon) = H3Encoder.Decode(h3Index);
        var h3Index2 = H3Encoder.Encode(decodedLat, decodedLon, res);

        _output.WriteLine($"  {h3Index} -> ({decodedLat:F2}, {decodedLon:F2}) -> {h3Index2}");
        _output.WriteLine($"  Match: {h3Index == h3Index2}");
        
        Assert.Equal(h3Index, h3Index2);
    }
}
