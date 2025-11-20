using Xunit;
using Xunit.Abstractions;

namespace Oproto.FluentDynamoDb.Geospatial.UnitTests.H3;

public class H3ParseIndexTest
{
    private readonly ITestOutputHelper _output;

    public H3ParseIndexTest(ITestOutputHelper output)
    {
        _output = output;
    }

    [Theory]
    [InlineData("8403935ffffffff")] // Failing case
    [InlineData("840392bffffffff")] // Failing case
    [InlineData("8405689ffffffff")] // Passing case
    [InlineData("84056cbffffffff")] // Passing case
    public void ParseH3Index(string h3IndexStr)
    {
        var index = Convert.ToUInt64(h3IndexStr, 16);

        var mode = (index >> 59) & 0xF;
        var reserved = (index >> 56) & 0x7;
        var resolution = (index >> 52) & 0xF;
        var baseCell = (index >> 45) & 0x7F;

        _output.WriteLine($"H3 Index: {h3IndexStr}");
        _output.WriteLine($"  Mode: {mode}");
        _output.WriteLine($"  Resolution: {resolution}");
        _output.WriteLine($"  Base Cell: {baseCell}");
        
        // Extract digits
        _output.WriteLine("  Digits:");
        for (var r = 1; r <= (int)resolution; r++)
        {
            var digitOffset = (15 - r) * 3;
            var digit = (index >> digitOffset) & 0x7;
            _output.WriteLine($"    Res {r}: {digit}");
        }
    }
}
