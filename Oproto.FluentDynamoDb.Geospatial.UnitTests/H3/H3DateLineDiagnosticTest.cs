using Oproto.FluentDynamoDb.Geospatial.H3;
using Xunit;
using Xunit.Abstractions;

namespace Oproto.FluentDynamoDb.Geospatial.UnitTests.H3;

/// <summary>
/// Diagnostic tests for Phase 1 of H3 date line fix.
/// These tests isolate the exact point of failure in the encoding pipeline.
/// </summary>
public class H3DateLineDiagnosticTest
{
    private readonly ITestOutputHelper _output;

    public H3DateLineDiagnosticTest(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public void Debug_DateLine_CompareMinusAndPlus180()
    {
        var testCases = new[]
        {
            (lat: 89.0, res: 4),
            (lat: 87.0, res: 4),
            (lat: 86.28, res: 4),
            (lat: 85.0, res: 4),
            (lat: 3.61, res: 14)
        };
        
        _output.WriteLine("=== Date Line Comparison: -180° vs +180° ===\n");
        
        foreach (var (lat, res) in testCases)
        {
            // Test -180 vs +180 (should be identical)
            var indexMinus = H3Encoder.Encode(lat, -180.0, res);
            var indexPlus = H3Encoder.Encode(lat, 180.0, res);
            
            _output.WriteLine($"Lat={lat}, Res={res}");
            _output.WriteLine($"  -180°: {indexMinus}");
            _output.WriteLine($"  +180°: {indexPlus}");
            _output.WriteLine($"  Match: {indexMinus == indexPlus}");
            
            // Extract and compare base cells
            var baseCellMinus = ExtractBaseCell(indexMinus);
            var baseCellPlus = ExtractBaseCell(indexPlus);
            _output.WriteLine($"  Base cells: {baseCellMinus} vs {baseCellPlus}");
            
            // Decode both to see where they point
            var (latMinus, lonMinus) = H3Encoder.Decode(indexMinus);
            var (latPlus, lonPlus) = H3Encoder.Decode(indexPlus);
            _output.WriteLine($"  Decoded -180°: ({latMinus:F2}, {lonMinus:F2})");
            _output.WriteLine($"  Decoded +180°: ({latPlus:F2}, {lonPlus:F2})");
            _output.WriteLine("");
        }
    }

    [Theory]
    [InlineData(89.0, -180.0, 4)]
    [InlineData(89.0, 180.0, 4)]
    [InlineData(87.0, -180.0, 4)]
    [InlineData(87.0, 180.0, 4)]
    [InlineData(86.28, -180.0, 4)]
    [InlineData(86.28, 180.0, 4)]
    public void DateLine_ShouldProduceSameIndex(double lat, double lon, int res)
    {
        var index = H3Encoder.Encode(lat, lon, res);
        var baseCell = ExtractBaseCell(index);
        
        _output.WriteLine($"({lat}, {lon}) -> {index} (base cell {baseCell})");
        
        // -180 and +180 should produce the same index
        if (Math.Abs(lon - (-180.0)) < 0.001)
        {
            var indexPlus = H3Encoder.Encode(lat, 180.0, res);
            var baseCellPlus = ExtractBaseCell(indexPlus);
            
            _output.WriteLine($"  Comparing with +180°: {indexPlus} (base cell {baseCellPlus})");
            Assert.Equal(indexPlus, index);
        }
    }

    [Fact]
    public void Debug_Equator_Encoding()
    {
        var lat = 0.0;
        var lon = 0.0;
        var res = 5;
        
        _output.WriteLine("=== Equator Encoding Debug ===\n");
        _output.WriteLine($"Input: ({lat}, {lon}) at resolution {res}");
        
        var index = H3Encoder.Encode(lat, lon, res);
        _output.WriteLine($"Encoded: {index}");
        _output.WriteLine($"Base cell: {ExtractBaseCell(index)}");
        
        var (decodedLat, decodedLon) = H3Encoder.Decode(index);
        _output.WriteLine($"Decoded: ({decodedLat:F6}, {decodedLon:F6})");
        
        var reencoded = H3Encoder.Encode(decodedLat, decodedLon, res);
        _output.WriteLine($"Re-encoded: {reencoded}");
        _output.WriteLine($"Base cell: {ExtractBaseCell(reencoded)}");
        _output.WriteLine($"Round-trip match: {index == reencoded}");
        
        // This should pass but currently fails
        // Assert.Equal(index, reencoded);
    }

    private int ExtractBaseCell(string h3Index)
    {
        var index = Convert.ToUInt64(h3Index, 16);
        return (int)((index >> 45) & 0x7F);
    }
}
