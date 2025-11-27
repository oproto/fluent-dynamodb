using System.Reflection;
using Oproto.FluentDynamoDb.Geospatial.H3;
using Xunit;
using Xunit.Abstractions;

namespace Oproto.FluentDynamoDb.Geospatial.UnitTests.H3;

/// <summary>
/// Debug test to trace through pentagon decoding and understand the bug.
/// </summary>
public class H3PentagonDecodingDebugTest
{
    private readonly ITestOutputHelper _output;

    public H3PentagonDecodingDebugTest(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public void Debug_ArabianSea_TraceDecoding()
    {
        // The failing case: Arabian Sea point
        var lat = 14.947707264183059;
        var lon = 58.099686036545755;
        var resolution = 10;

        _output.WriteLine($"=== Arabian Sea Pentagon Decoding Debug ===");
        _output.WriteLine($"Original: ({lat:F6}, {lon:F6})");
        _output.WriteLine($"Resolution: {resolution}");

        // Encode
        var h3Index = H3Encoder.Encode(lat, lon, resolution);
        _output.WriteLine($"H3 Index: {h3Index}");

        // Parse the index to get base cell info
        var indexValue = Convert.ToUInt64(h3Index, 16);
        var baseCell = (int)((indexValue >> 45) & 0x7F);
        var res = (int)((indexValue >> 52) & 0xF);
        
        _output.WriteLine($"Base cell: {baseCell}");
        _output.WriteLine($"Resolution: {res}");
        
        // Check if pentagon
        var pentagonBaseCells = new HashSet<int> { 4, 14, 24, 38, 49, 58, 63, 72, 83, 97, 107, 117 };
        var isPentagon = pentagonBaseCells.Contains(baseCell);
        _output.WriteLine($"Is pentagon: {isPentagon}");

        // Get base cell data using reflection
        var encoderType = typeof(H3Encoder);
        var baseCellDataTableField = encoderType.GetField("BaseCellDataTable", 
            BindingFlags.NonPublic | BindingFlags.Static);
        
        if (baseCellDataTableField != null)
        {
            var baseCellDataTable = baseCellDataTableField.GetValue(null) as Array;
            if (baseCellDataTable != null)
            {
                var baseCellData = baseCellDataTable.GetValue(baseCell);
                _output.WriteLine($"Base cell data: {baseCellData}");
                
                // Get the Face property
                var faceProperty = baseCellData?.GetType().GetProperty("Face");
                if (faceProperty != null)
                {
                    var homeFace = faceProperty.GetValue(baseCellData);
                    _output.WriteLine($"Home face: {homeFace}");
                }
            }
        }

        // Decode
        var (decodedLat, decodedLon) = H3Encoder.Decode(h3Index);
        _output.WriteLine($"Decoded: ({decodedLat:F6}, {decodedLon:F6})");

        var latError = Math.Abs(decodedLat - lat);
        var lonError = Math.Abs(decodedLon - lon);
        _output.WriteLine($"Error: lat={latError:F6}°, lon={lonError:F6}°");

        // The error should be small
        Assert.True(latError < 1.0, $"Latitude error {latError}° is too large for pentagon base cell");
    }

    [Fact]
    public void Debug_AllPentagonBaseCells_Resolution0()
    {
        // Test all 12 pentagon base cells at resolution 0
        var pentagonBaseCells = new int[] { 4, 14, 24, 38, 49, 58, 63, 72, 83, 97, 107, 117 };
        
        _output.WriteLine("=== Pentagon Base Cell Centers at Resolution 0 ===");
        
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
            
            var h3String = index.ToString("x");
            
            try
            {
                var (lat, lon) = H3Encoder.Decode(h3String);
                _output.WriteLine($"Base cell {bc,3}: ({lat:F4}, {lon:F4}) - index: {h3String}");
            }
            catch (Exception ex)
            {
                _output.WriteLine($"Base cell {bc,3}: ERROR - {ex.Message}");
            }
        }
    }

    [Fact]
    public void Debug_BaseCell49_MultipleResolutions()
    {
        // Test base cell 49 at multiple resolutions
        _output.WriteLine("=== Base Cell 49 at Multiple Resolutions ===");
        
        // Find a point that encodes to base cell 49
        // Base cell 49 is a pentagon with home face 14
        // CW offset faces: 0, 9
        
        // The Arabian Sea point encodes to base cell 49
        var lat = 14.947707264183059;
        var lon = 58.099686036545755;
        
        _output.WriteLine($"Test point: ({lat:F6}, {lon:F6})");
        
        for (int res = 0; res <= 10; res++)
        {
            var h3Index = H3Encoder.Encode(lat, lon, res);
            var (decodedLat, decodedLon) = H3Encoder.Decode(h3Index);
            var error = Math.Sqrt((decodedLat - lat) * (decodedLat - lat) + (decodedLon - lon) * (decodedLon - lon));
            
            // Parse base cell
            var indexValue = Convert.ToUInt64(h3Index, 16);
            var baseCell = (int)((indexValue >> 45) & 0x7F);
            
            var status = error < 1.0 ? "OK" : "FAIL";
            _output.WriteLine($"Res {res,2}: bc={baseCell,3}, idx={h3Index}, decoded=({decodedLat:F4}, {decodedLon:F4}), error={error:F4}° [{status}]");
        }
    }

    [Fact]
    public void Debug_HexagonVsPentagon_Comparison()
    {
        // Compare a hexagon base cell location with a pentagon base cell location
        _output.WriteLine("=== Hexagon vs Pentagon Base Cell Comparison ===");
        
        // San Francisco - encodes to hexagon base cell
        var sfLat = 37.7749;
        var sfLon = -122.4194;
        
        // Arabian Sea - encodes to pentagon base cell 49
        var asLat = 14.947707264183059;
        var asLon = 58.099686036545755;
        
        for (int res = 0; res <= 10; res++)
        {
            // San Francisco
            var sfIndex = H3Encoder.Encode(sfLat, sfLon, res);
            var (sfDecodedLat, sfDecodedLon) = H3Encoder.Decode(sfIndex);
            var sfError = Math.Sqrt((sfDecodedLat - sfLat) * (sfDecodedLat - sfLat) + (sfDecodedLon - sfLon) * (sfDecodedLon - sfLon));
            var sfBaseCell = (int)((Convert.ToUInt64(sfIndex, 16) >> 45) & 0x7F);
            
            // Arabian Sea
            var asIndex = H3Encoder.Encode(asLat, asLon, res);
            var (asDecodedLat, asDecodedLon) = H3Encoder.Decode(asIndex);
            var asError = Math.Sqrt((asDecodedLat - asLat) * (asDecodedLat - asLat) + (asDecodedLon - asLon) * (asDecodedLon - asLon));
            var asBaseCell = (int)((Convert.ToUInt64(asIndex, 16) >> 45) & 0x7F);
            
            _output.WriteLine($"Res {res,2}: SF(bc={sfBaseCell,3}, err={sfError:F4}°) vs AS(bc={asBaseCell,3}, err={asError:F4}°)");
        }
    }
}
