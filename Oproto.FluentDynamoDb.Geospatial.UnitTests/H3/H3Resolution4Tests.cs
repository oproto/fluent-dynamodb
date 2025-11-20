using Oproto.FluentDynamoDb.Geospatial.H3;
using Xunit;
using Xunit.Abstractions;

namespace Oproto.FluentDynamoDb.Geospatial.UnitTests.H3;

/// <summary>
/// Tests for resolution 4 cells using data from H3 reference implementation.
/// Test data extracted from h3/tests/inputfiles/res04ic.txt
/// </summary>
public class H3Resolution4Tests
{
    private readonly ITestOutputHelper _output;

    public H3Resolution4Tests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Theory]
    [InlineData("8400001ffffffff", 79.2423985098, 38.0234070080)]
    [InlineData("8400003ffffffff", 79.6335259201, 37.3333326630)]
    [InlineData("8400005ffffffff", 79.1237887051, 40.1207397878)]
    [InlineData("8400007ffffffff", 79.5196463969, 39.5180123326)]
    [InlineData("8400009ffffffff", 78.9598476710, 36.6110921587)]
    [InlineData("840000bffffffff", 79.3463795680, 35.8883132415)]
    [InlineData("840000dffffffff", 78.8508731935, 38.6639206531)]
    [InlineData("8400011ffffffff", 80.1167714103, 34.2693230234)]
    [InlineData("8400013ffffffff", 80.5000760578, 33.3594222442)]
    [InlineData("8400015ffffffff", 80.0240406577, 36.5879394714)]
    public void Decode_Resolution4_NorthernCells_MatchesReference(string h3Index, double expectedLat, double expectedLon)
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
    [InlineData("8400017ffffffff", 80.4136987161, 35.7805834230)]
    [InlineData("8400019ffffffff", 79.8150701577, 32.8533699512)]
    [InlineData("840001bffffffff", 80.1931618274, 31.9177374031)]
    [InlineData("840001dffffffff", 79.7321073674, 35.1098468489)]
    [InlineData("8400021ffffffff", 79.2475621066, 43.7509884749)]
    [InlineData("8400023ffffffff", 79.6513574907, 43.2824307852)]
    [InlineData("8400025ffffffff", 79.0902599417, 45.7910589999)]
    [InlineData("8400027ffffffff", 79.4971210571, 45.4094865624)]
    [InlineData("8400029ffffffff", 78.9909178266, 42.1758443468)]
    [InlineData("840002bffffffff", 79.3908483693, 41.6587210294)]
    public void Decode_Resolution4_HighLatitudeCells_MatchesReference(string h3Index, double expectedLat, double expectedLon)
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
    [InlineData("840002dffffffff", 78.8441972218, 44.1848642993)]
    [InlineData("8400031ffffffff", 80.1909572143, 40.4948806727)]
    [InlineData("8400033ffffffff", 80.5908007826, 39.8371507569)]
    [InlineData("8400035ffffffff", 80.0554607471, 42.7749305902)]
    [InlineData("8400037ffffffff", 80.4597342098, 42.2235131194)]
    [InlineData("8400039ffffffff", 79.9152913538, 38.8668622927)]
    [InlineData("840003bffffffff", 80.3105260423, 38.1614005600)]
    [InlineData("840003dffffffff", 79.7909138133, 41.1000948046)]
    [InlineData("8400041ffffffff", 78.2899792591, 35.9758018082)]
    [InlineData("8400043ffffffff", 78.6726129640, 35.2760351139)]
    public void Decode_Resolution4_MidLatitudeCells_MatchesReference(string h3Index, double expectedLat, double expectedLon)
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
    [InlineData("8400b2bffffffff", 74.1418917286, 73.6856347137)]
    [InlineData("8404203ffffffff", 77.5856034677, 140.1715302621)]
    [InlineData("8408919ffffffff", 58.8253611034, 19.5233583170)]
    [InlineData("842a451ffffffff", 33.8768847948, -64.7186072082)]
    [InlineData("84548b3ffffffff", 10.4405665891, -19.2627813464)]
    public void Decode_Resolution4_VariousLatitudes_MatchesReference(string h3Index, double expectedLat, double expectedLon)
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
    [InlineData("8470001ffffffff", 4.7796854525, -170.3596184978)]
    [InlineData("8470003ffffffff", 4.5349929686, -170.0282203281)]
    [InlineData("8470005ffffffff", 4.6144291359, -170.7126818371)]
    [InlineData("8470007ffffffff", 4.3703245827, -170.3825560637)]
    [InlineData("8470009ffffffff", 5.1901105897, -170.3365937232)]
    [InlineData("847f1a7ffffffff", -5.1948992844, 176.1825990492)]
    public void Decode_Resolution4_EquatorialCells_MatchesReference(string h3Index, double expectedLat, double expectedLon)
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
    [InlineData("84a0001ffffffff", -19.0936806835, -136.3611811711)]
    [InlineData("84a0003ffffffff", -18.8751253558, -136.7293577978)]
    [InlineData("84a0005ffffffff", -18.9013106135, -136.0039026945)]
    [InlineData("84a9609ffffffff", -29.4696295589, -58.3014274642)]
    [InlineData("84d35c9ffffffff", -46.2143258598, -118.3492715960)]
    public void Decode_Resolution4_SouthernHemisphere_MatchesReference(string h3Index, double expectedLat, double expectedLon)
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
