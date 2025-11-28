using Oproto.FluentDynamoDb.Geospatial.H3;
using Xunit;
using Xunit.Abstractions;

namespace Oproto.FluentDynamoDb.Geospatial.UnitTests.H3;

public class H3PentagonCenterTest
{
    private readonly ITestOutputHelper _output;

    public H3PentagonCenterTest(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public void Debug_PentagonBaseCellCenters()
    {
        // Pentagon base cells
        var pentagonBaseCells = new int[] { 4, 14, 24, 38, 49, 58, 63, 72, 83, 97, 107, 117 };
        
        _output.WriteLine("Pentagon base cell centers:");
        foreach (var bc in pentagonBaseCells)
        {
            // Create an H3 index for this base cell at resolution 0
            // H3 index format: mode(4) | reserved(3) | resolution(4) | baseCell(7) | digits(45)
            ulong index = 0x8000000000000000UL; // Mode 1
            index |= ((ulong)bc << 45); // Base cell
            // All digits are 7 (0x7) for resolution 0
            for (int r = 1; r <= 15; r++)
            {
                int digitOffset = (15 - r) * 3;
                index |= (0x7UL << digitOffset);
            }
            
            var h3String = index.ToString("x15");
            
            try
            {
                var (lat, lon) = H3Encoder.Decode(h3String);
                _output.WriteLine($"  Base cell {bc}: ({lat:F4}, {lon:F4})");
            }
            catch (Exception ex)
            {
                _output.WriteLine($"  Base cell {bc}: ERROR - {ex.Message}");
            }
        }
    }

    [Fact]
    public void Debug_BaseCell49_DetailedAnalysis()
    {
        _output.WriteLine("Base cell 49 detailed analysis:");
        
        // Base cell 49 info from BaseCellDataTable:
        // Face: 14, IJK: (2, 0, 0), Pentagon: true, CW offset faces: 0, 9
        _output.WriteLine("  Home face: 14");
        _output.WriteLine("  IJK on home face: (2, 0, 0)");
        _output.WriteLine("  CW offset faces: 0, 9");
        
        // Decode base cell 49 at resolution 0
        ulong index = 0x8063fffffffffffUL; // Base cell 49, resolution 0
        var h3String = "8063fffffffffff";
        
        var (lat, lon) = H3Encoder.Decode(h3String);
        _output.WriteLine($"  Decoded center: ({lat:F4}, {lon:F4})");
        
        // Check which face this location is closest to
        // Face 0 center: ~(46°, 71°)
        // Face 14 center: ~(-24°, 72°)
        var distToFace0 = Math.Sqrt((lat - 46) * (lat - 46) + (lon - 71) * (lon - 71));
        var distToFace14 = Math.Sqrt((lat - (-24)) * (lat - (-24)) + (lon - 72) * (lon - 72));
        _output.WriteLine($"  Distance to face 0 center: {distToFace0:F2}°");
        _output.WriteLine($"  Distance to face 14 center: {distToFace14:F2}°");
        
        // The failing point
        var failLat = 14.9477;
        var failLon = 58.0997;
        _output.WriteLine($"\n  Failing point: ({failLat}, {failLon})");
        _output.WriteLine($"  Distance from decoded center: {Math.Sqrt((lat - failLat) * (lat - failLat) + (lon - failLon) * (lon - failLon)):F2}°");
    }
}
