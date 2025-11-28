using H3Lib;
using H3Lib.Extensions;
using Oproto.FluentDynamoDb.Geospatial.H3;
using Xunit;
using Xunit.Abstractions;

namespace Oproto.FluentDynamoDb.Geospatial.UnitTests.H3;

/// <summary>
/// Test to analyze H3 index structure.
/// </summary>
public class H3IndexAnalysisTest2
{
    private readonly ITestOutputHelper _output;

    public H3IndexAnalysisTest2(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public void Analyze_ArabianSea_Indices()
    {
        _output.WriteLine("=== H3 Index Analysis ===");
        
        // Our index
        var ourIndexStr = "8a62a9323c2ffff";
        var ourIndex = Convert.ToUInt64(ourIndexStr, 16);
        
        // Reference index
        var refIndexStr = "8a630ecdd40ffff";
        var refIndex = Convert.ToUInt64(refIndexStr, 16);
        
        _output.WriteLine($"\nOur Index: {ourIndexStr}");
        AnalyzeIndex(ourIndex);
        
        _output.WriteLine($"\nReference Index: {refIndexStr}");
        AnalyzeIndex(refIndex);
        
        // Decode both with our decoder
        _output.WriteLine($"\nOur decode of our index:");
        var (ourLat, ourLon) = H3Encoder.Decode(ourIndexStr);
        _output.WriteLine($"  ({ourLat:F6}, {ourLon:F6})");
        
        _output.WriteLine($"\nOur decode of reference index:");
        var (refLat, refLon) = H3Encoder.Decode(refIndexStr);
        _output.WriteLine($"  ({refLat:F6}, {refLon:F6})");
    }

    private void AnalyzeIndex(ulong index)
    {
        var mode = (int)((index >> 59) & 0xF);
        var reserved = (int)((index >> 56) & 0x7);
        var resolution = (int)((index >> 52) & 0xF);
        var baseCell = (int)((index >> 45) & 0x7F);
        
        _output.WriteLine($"  Mode: {mode}");
        _output.WriteLine($"  Reserved: {reserved}");
        _output.WriteLine($"  Resolution: {resolution}");
        _output.WriteLine($"  Base Cell: {baseCell}");
        
        // Pentagon base cells
        var pentagonBaseCells = new HashSet<int> { 4, 14, 24, 38, 49, 58, 63, 72, 83, 97, 107, 117 };
        _output.WriteLine($"  Is Pentagon: {pentagonBaseCells.Contains(baseCell)}");
        
        _output.WriteLine($"  Digits:");
        for (int r = 1; r <= resolution; r++)
        {
            var digitOffset = (15 - r) * 3;
            var digit = (int)((index >> digitOffset) & 0x7);
            _output.WriteLine($"    Res {r}: {digit}");
        }
    }
}
