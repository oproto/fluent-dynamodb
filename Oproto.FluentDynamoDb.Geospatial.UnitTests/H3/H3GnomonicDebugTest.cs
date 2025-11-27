using Oproto.FluentDynamoDb.Geospatial.H3;
using Xunit;
using Xunit.Abstractions;

namespace Oproto.FluentDynamoDb.Geospatial.UnitTests.H3;

public class H3GnomonicDebugTest
{
    private readonly ITestOutputHelper _output;

    public H3GnomonicDebugTest(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public void Debug_FaceCenters_DecodeCorrectly()
    {
        // Test that face centers decode correctly
        // Face centers should have minimal error
        
        var faceCentersApprox = new (double lat, double lon, int face)[]
        {
            (46.0, 71.5, 0),
            (75.0, 145.0, 1),
            (60.0, -77.0, 2),
            (34.0, -26.0, 3),
            (28.0, 23.0, 4),
        };

        foreach (var (lat, lon, expectedFace) in faceCentersApprox)
        {
            var h3Index = H3Encoder.Encode(lat, lon, 5);
            var (decodedLat, decodedLon) = H3Encoder.Decode(h3Index);
            var error = Math.Sqrt((decodedLat - lat) * (decodedLat - lat) + (decodedLon - lon) * (decodedLon - lon));
            
            _output.WriteLine($"Face {expectedFace}: ({lat}, {lon}) -> {h3Index} -> ({decodedLat:F4}, {decodedLon:F4}), error={error:F4}°");
        }
    }

    [Fact]
    public void Debug_VariousLocations_CheckErrors()
    {
        // Test various locations around the world
        var locations = new (double lat, double lon, string name)[]
        {
            (37.7749, -122.4194, "San Francisco"),
            (51.5074, -0.1278, "London"),
            (35.6762, 139.6503, "Tokyo"),
            (-33.8688, 151.2093, "Sydney"),
            (14.9477, 58.0997, "Arabian Sea (failing)"),
            (0.0, 0.0, "Gulf of Guinea"),
            (0.0, 90.0, "Indian Ocean"),
            (45.0, 45.0, "Caspian Sea area"),
            (30.0, 60.0, "Iran/Afghanistan"),
            (15.0, 60.0, "Arabian Sea 2"),
            (10.0, 55.0, "Gulf of Aden"),
            (20.0, 65.0, "Arabian Sea 3"),
        };

        _output.WriteLine("Location errors at resolution 5:");
        foreach (var (lat, lon, name) in locations)
        {
            var h3Index = H3Encoder.Encode(lat, lon, 5);
            var (decodedLat, decodedLon) = H3Encoder.Decode(h3Index);
            var error = Math.Sqrt((decodedLat - lat) * (decodedLat - lat) + (decodedLon - lon) * (decodedLon - lon));
            
            var status = error < 1.0 ? "OK" : "FAIL";
            _output.WriteLine($"  {name}: ({lat}, {lon}) -> ({decodedLat:F4}, {decodedLon:F4}), error={error:F4}° [{status}]");
        }
    }
}
