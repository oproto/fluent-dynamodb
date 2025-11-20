using Oproto.FluentDynamoDb.Geospatial.H3;
using Xunit;
using Xunit.Abstractions;

namespace Oproto.FluentDynamoDb.Geospatial.UnitTests.H3;

public class H3IndexAnalysisTest
{
    private readonly ITestOutputHelper _output;

    public H3IndexAnalysisTest(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public void AnalyzeH3Index()
    {
        var h3Index = "8403935ffffffff";
        var index = Convert.ToUInt64(h3Index, 16);

        _output.WriteLine($"H3 Index: {h3Index}");
        _output.WriteLine($"Binary: {Convert.ToString((long)index, 2).PadLeft(64, '0')}");
        _output.WriteLine("");

        // Parse the index structure
        var mode = (index >> 59) & 0xF;
        var reserved = (index >> 56) & 0x7;
        var resolution = (index >> 52) & 0xF;
        var baseCell = (index >> 45) & 0x7F;

        _output.WriteLine($"Mode: {mode}");
        _output.WriteLine($"Reserved: {reserved}");
        _output.WriteLine($"Resolution: {resolution}");
        _output.WriteLine($"Base Cell: {baseCell}");
        _output.WriteLine("");

        // Extract digits
        _output.WriteLine("Digits (resolution 1-4):");
        for (var r = 1; r <= 4; r++)
        {
            var digitOffset = (15 - r) * 3;
            var digit = (index >> digitOffset) & 0x7;
            _output.WriteLine($"  Resolution {r}: digit {digit}");
        }
    }
}
