using Oproto.FluentDynamoDb.Geospatial.H3;
using Xunit;
using Xunit.Abstractions;

namespace Oproto.FluentDynamoDb.Geospatial.UnitTests.H3;

/// <summary>
/// Tests to debug and fix the overage handling issues.
/// Focuses on the 4 failing resolution 1 cells.
/// </summary>
public class H3OverageFixTests
{
    private readonly ITestOutputHelper _output;

    public H3OverageFixTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Theory]
    [InlineData("8102bffffffffff", 73.613016162899996, -130.74352242379999, 1, 2)] // Base cell 1, digit 2
    [InlineData("8108bffffffffff", 60.1912180486, 18.2585301192, 4, 2)]             // Base cell 4, digit 2
    [InlineData("81033ffffffffff", 86.889885017599994, -110.58483358700001, 1, 4)]  // Base cell 1, digit 4
    [InlineData("8103bffffffffff", 79.8699090482, -149.99363275089999, 1, 6)]       // Base cell 1, digit 6
    public void Debug_FailingCells_ShowDetails(string h3Index, double expectedLat, double expectedLon, int expectedBaseCell, int expectedDigit)
    {
        _output.WriteLine($"=== Debugging H3: {h3Index} ===");
        _output.WriteLine($"Expected: lat={expectedLat:F10}, lon={expectedLon:F10}");
        _output.WriteLine($"Expected base cell: {expectedBaseCell}, digit: {expectedDigit}");
        _output.WriteLine("");
        
        // Decode the H3 index
        var (actualLat, actualLon) = H3Encoder.Decode(h3Index);
        
        _output.WriteLine($"Actual:   lat={actualLat:F10}, lon={actualLon:F10}");
        _output.WriteLine($"Diff:     lat={Math.Abs(expectedLat - actualLat):F10}, lon={Math.Abs(expectedLon - actualLon):F10}");
        
        var latError = Math.Abs(expectedLat - actualLat);
        var lonError = Math.Abs(expectedLon - actualLon);
        
        _output.WriteLine("");
        _output.WriteLine($"Errors: lat={latError:F2}°, lon={lonError:F2}°");
        
        // These are currently failing with large errors
        // After fix, they should pass
        Assert.InRange(actualLat, expectedLat - 0.0001, expectedLat + 0.0001);
        Assert.InRange(actualLon, expectedLon - 0.0001, expectedLon + 0.0001);
    }
}
