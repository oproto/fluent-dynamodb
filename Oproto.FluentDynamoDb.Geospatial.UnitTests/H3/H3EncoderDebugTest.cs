using Oproto.FluentDynamoDb.Geospatial.H3;
using Xunit;
using Xunit.Abstractions;

namespace Oproto.FluentDynamoDb.Geospatial.UnitTests.H3;

public class H3EncoderDebugTest
{
    private readonly ITestOutputHelper _output;

    public H3EncoderDebugTest(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public void DebugRoundTripIssue()
    {
        // Test case from property test failure
        var lat = 86.28009407999305;
        var lon = -180.0;
        var res = 4;

        _output.WriteLine($"Input: lat={lat}, lon={lon}, res={res}");

        // First encode
        var h3Index1 = H3Encoder.Encode(lat, lon, res);
        _output.WriteLine($"First encode: {h3Index1}");

        // Decode
        var (decodedLat, decodedLon) = H3Encoder.Decode(h3Index1);
        _output.WriteLine($"Decoded: lat={decodedLat}, lon={decodedLon}");

        // Second encode
        var h3Index2 = H3Encoder.Encode(decodedLat, decodedLon, res);
        _output.WriteLine($"Second encode: {h3Index2}");

        // Check if they match
        _output.WriteLine($"Match: {h3Index1 == h3Index2}");
        
        if (h3Index1 != h3Index2)
        {
            _output.WriteLine($"MISMATCH: {h3Index1} != {h3Index2}");
            
            // Decode both to see the difference
            var (lat1, lon1) = H3Encoder.Decode(h3Index1);
            var (lat2, lon2) = H3Encoder.Decode(h3Index2);
            
            _output.WriteLine($"Cell 1 center: ({lat1}, {lon1})");
            _output.WriteLine($"Cell 2 center: ({lat2}, {lon2})");
            _output.WriteLine($"Distance: lat_diff={Math.Abs(lat1 - lat2)}, lon_diff={Math.Abs(lon1 - lon2)}");
        }
    }
}
