using Oproto.FluentDynamoDb.Geospatial.H3;
using Xunit;
using Xunit.Abstractions;

namespace Oproto.FluentDynamoDb.Geospatial.UnitTests.H3;

/// <summary>
/// Tests for resolution 0 cells using data from H3 reference implementation.
/// Test data extracted from h3/tests/inputfiles/res00ic.txt
/// </summary>
public class H3Resolution0Tests
{
    private readonly ITestOutputHelper _output;

    public H3Resolution0Tests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Theory]
    [InlineData("8001fffffffffff", 79.2423985098, 38.0234070080)]
    [InlineData("8003fffffffffff", 79.2209863563, -107.4292022430)]
    [InlineData("8005fffffffffff", 74.9284343892, 145.3562419228)]
    [InlineData("8007fffffffffff", 69.6634529498, -30.9680446065)]
    [InlineData("8009fffffffffff", 64.7000001279, 10.5361990755)]  // Pentagon
    [InlineData("800bfffffffffff", 64.4365965876, 89.5730685412)]
    [InlineData("800dfffffffffff", 64.4180604989, -158.9174850412)]
    [InlineData("800ffffffffffff", 60.4327952631, -77.2070574856)]
    [InlineData("8011fffffffffff", 60.1852201299, 45.3065275291)]
    [InlineData("8013fffffffffff", 59.0048043016, -119.2906835190)]
    [InlineData("8015fffffffffff", 55.2574646294, 127.0877451493)]
    [InlineData("8017fffffffffff", 55.2504452897, 163.5721725337)]
    [InlineData("8019fffffffffff", 52.6757511246, -11.6016256117)]
    [InlineData("801bfffffffffff", 50.1597568232, -44.6097341949)]
    [InlineData("801dfffffffffff", 50.1032014822, -143.4784900150)]
    [InlineData("801ffffffffffff", 48.7583497236, 18.3030444806)]
    [InlineData("8021fffffffffff", 46.0418943188, 71.5279032991)]
    [InlineData("8023fffffffffff", 45.8046549230, -167.3437104494)]
    [InlineData("8025fffffffffff", 44.9859022908, 101.5006917663)]
    [InlineData("8027fffffffffff", 43.4228149390, -97.4246592613)]
    public void Decode_Resolution0_First20Cells_MatchesReference(string h3Index, double expectedLat, double expectedLon)
    {
        // Act
        var (actualLat, actualLon) = H3Encoder.Decode(h3Index);
        
        _output.WriteLine($"H3: {h3Index}");
        _output.WriteLine($"Expected: lat={expectedLat:F10}, lon={expectedLon:F10}");
        _output.WriteLine($"Actual:   lat={actualLat:F10}, lon={actualLon:F10}");
        _output.WriteLine($"Diff:     lat={Math.Abs(actualLat - expectedLat):F10}, lon={Math.Abs(actualLon - expectedLon):F10}");
        
        // Assert - allow small tolerance for floating point differences
        Assert.InRange(actualLat, expectedLat - 0.0001, expectedLat + 0.0001);
        Assert.InRange(actualLon, expectedLon - 0.0001, expectedLon + 0.0001);
    }

    [Theory]
    [InlineData("8075fffffffffff", 2.3008821116, -5.2453902968)]  // Near equator
    [InlineData("8077fffffffffff", 0.7617301194, 158.5662116893)]  // Near equator
    [InlineData("8079fffffffffff", 0.0193810903, -134.6329629085)]  // Very close to equator
    [InlineData("807bfffffffffff", -0.0193810903, 45.3670370915)]  // Just south of equator
    [InlineData("807dfffffffffff", -0.7617301194, -21.4337883107)]  // Near equator
    [InlineData("807ffffffffffff", -2.3008821116, 174.7546097032)]  // Near equator
    public void Decode_Resolution0_EquatorialCells_MatchesReference(string h3Index, double expectedLat, double expectedLon)
    {
        // Act
        var (actualLat, actualLon) = H3Encoder.Decode(h3Index);
        
        _output.WriteLine($"H3: {h3Index}");
        _output.WriteLine($"Expected: lat={expectedLat:F10}, lon={expectedLon:F10}");
        _output.WriteLine($"Actual:   lat={actualLat:F10}, lon={actualLon:F10}");
        _output.WriteLine($"Diff:     lat={Math.Abs(actualLat - expectedLat):F10}, lon={Math.Abs(actualLon - expectedLon):F10}");
        
        // Assert
        Assert.InRange(actualLat, expectedLat - 0.0001, expectedLat + 0.0001);
        Assert.InRange(actualLon, expectedLon - 0.0001, expectedLon + 0.0001);
    }

    [Theory]
    [InlineData("8097fffffffffff", -13.2331274567, 29.0200594961)]
    [InlineData("80a1fffffffffff", -19.0936806835, -136.3611811711)]
    [InlineData("80b1fffffffffff", -26.2836286531, -117.0457250103)]
    [InlineData("80c1fffffffffff", -34.7171928093, -10.7526601965)]
    [InlineData("80c3fffffffffff", -39.1000000340, -57.6999995922)]
    public void Decode_Resolution0_SouthernHemisphere_MatchesReference(string h3Index, double expectedLat, double expectedLon)
    {
        // Act
        var (actualLat, actualLon) = H3Encoder.Decode(h3Index);
        
        _output.WriteLine($"H3: {h3Index}");
        _output.WriteLine($"Expected: lat={expectedLat:F10}, lon={expectedLon:F10}");
        _output.WriteLine($"Actual:   lat={actualLat:F10}, lon={actualLon:F10}");
        _output.WriteLine($"Diff:     lat={Math.Abs(actualLat - expectedLat):F10}, lon={Math.Abs(actualLon - expectedLon):F10}");
        
        // Assert
        Assert.InRange(actualLat, expectedLat - 0.0001, expectedLat + 0.0001);
        Assert.InRange(actualLon, expectedLon - 0.0001, expectedLon + 0.0001);
    }
}
