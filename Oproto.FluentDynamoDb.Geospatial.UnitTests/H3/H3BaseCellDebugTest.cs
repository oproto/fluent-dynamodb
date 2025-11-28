using Oproto.FluentDynamoDb.Geospatial.H3;
using Xunit;
using Xunit.Abstractions;

namespace Oproto.FluentDynamoDb.Geospatial.UnitTests.H3;

public class H3BaseCellDebugTest
{
    private readonly ITestOutputHelper _output;

    public H3BaseCellDebugTest(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public void Debug_FailingLocations_CheckBaseCells()
    {
        var locations = new (double lat, double lon, string name)[]
        {
            (14.9477, 58.0997, "Arabian Sea (failing)"),
            (0.0, 0.0, "Gulf of Guinea"),
            (15.0, 60.0, "Arabian Sea 2"),
            (10.0, 55.0, "Gulf of Aden"),
            (37.7749, -122.4194, "San Francisco (working)"),
            (20.0, 65.0, "Arabian Sea 3 (working)"),
        };

        _output.WriteLine("Base cell analysis:");
        foreach (var (lat, lon, name) in locations)
        {
            // Encode at resolution 0 to see the base cell
            var h3Index0 = H3Encoder.Encode(lat, lon, 0);
            
            // Parse the base cell from the index
            var indexValue = Convert.ToUInt64(h3Index0, 16);
            var baseCell = (int)((indexValue >> 45) & 0x7F);
            
            // Check if it's a pentagon
            var pentagonBaseCells = new HashSet<int> { 4, 14, 24, 38, 49, 58, 63, 72, 83, 97, 107, 117 };
            var isPentagon = pentagonBaseCells.Contains(baseCell);
            
            var h3Index5 = H3Encoder.Encode(lat, lon, 5);
            var (decodedLat, decodedLon) = H3Encoder.Decode(h3Index5);
            var error = Math.Sqrt((decodedLat - lat) * (decodedLat - lat) + (decodedLon - lon) * (decodedLon - lon));
            
            _output.WriteLine($"  {name}:");
            _output.WriteLine($"    Base cell: {baseCell} (pentagon: {isPentagon})");
            _output.WriteLine($"    H3 (res 0): {h3Index0}");
            _output.WriteLine($"    Error: {error:F4}°");
        }
    }
}
