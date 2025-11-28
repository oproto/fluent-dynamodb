using Oproto.FluentDynamoDb.Geospatial.H3;
using Xunit;
using Xunit.Abstractions;

namespace Oproto.FluentDynamoDb.Geospatial.UnitTests.H3;

/// <summary>
/// Tests for resolution 2 cells using data from H3 reference implementation.
/// Test data extracted from h3/tests/inputfiles/res02ic.txt
/// 
/// Resolution 2 cells are the second level of subdivision.
/// Each resolution 1 cell is divided into 7 resolution 2 cells.
/// This tests deeper digit path encoding/decoding and coordinate transformations.
/// </summary>
public class H3Resolution2Tests
{
    private readonly ITestOutputHelper _output;

    public H3Resolution2Tests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Theory]
    [InlineData("820007fffffffff", 79.2423985098, 38.0234070080)]
    [InlineData("82000ffffffffff", 81.9574923092, 31.7572277199)]
    [InlineData("820017fffffffff", 78.1281546999, 51.7045382695)]
    [InlineData("82001ffffffffff", 81.0292761569, 50.5046347695)]
    [InlineData("820027fffffffff", 77.1842902821, 29.5986973277)]
    [InlineData("82002ffffffffff", 79.6496815778, 22.5999265922)]
    [InlineData("820037fffffffff", 76.5027267490, 41.7072308696)]
    [InlineData("820047fffffffff", 83.6576608516, -7.3190411494)]
    [InlineData("82004ffffffffff", 84.3625365404, -34.7989745100)]
    [InlineData("820057fffffffff", 84.5162817685, 19.2822604953)]
    public void Decode_Resolution2_First10Cells_MatchesReference(string h3Index, double expectedLat, double expectedLon)
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
    [InlineData("82005ffffffffff", 86.4484351388, -10.1752678992)]
    [InlineData("820067fffffffff", 80.9535937898, -6.2314325543)]
    [InlineData("82006ffffffffff", 81.9053693563, -24.4977003646)]
    [InlineData("820077fffffffff", 81.9059462462, 11.3605729795)]
    [InlineData("820087fffffffff", 77.1530287166, 74.8802719453)]
    [InlineData("82008ffffffffff", 79.8901758817, 80.6766062479)]
    [InlineData("820097fffffffff", 74.6860920109, 81.9649737683)]
    [InlineData("82009ffffffffff", 77.2375775604, 88.1527830407)]
    [InlineData("8200a7fffffffff", 76.4563343281, 62.7163221774)]
    [InlineData("8200affffffffff", 79.3346004007, 64.7990427262)]
    public void Decode_Resolution2_Next10Cells_MatchesReference(string h3Index, double expectedLat, double expectedLon)
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
    [InlineData("8200b7fffffffff", 74.3948972811, 71.1837505291)]
    [InlineData("8200c7fffffffff", 85.1221781325, 76.6384313654)]
    [InlineData("8200cffffffffff", 87.6680058054, 104.9599570631)]
    [InlineData("8200d7fffffffff", 82.4947715901, 90.7451499493)]
    [InlineData("8200dffffffffff", 84.6700864913, 110.2651922618)]
    [InlineData("8200e7fffffffff", 83.9697365456, 48.1162642826)]
    [InlineData("8200effffffffff", 86.9116280774, 41.1127984813)]
    [InlineData("8200f7fffffffff", 82.2435948903, 68.4841594070)]
    [InlineData("820107fffffffff", 72.6804915314, 28.6431886669)]
    [InlineData("82010ffffffffff", 75.0237200109, 23.7627754542)]
    public void Decode_Resolution2_HighLatitudes_MatchesReference(string h3Index, double expectedLat, double expectedLon)
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
    [InlineData("820207fffffffff", 79.2209863563, -107.4292022430)]
    [InlineData("82020ffffffffff", 77.0836853085, -98.7524973334)]
    [InlineData("820217fffffffff", 78.1057755976, -121.0820276086)]
    [InlineData("82021ffffffffff", 76.4303216371, -111.1535624155)]
    [InlineData("820227fffffffff", 81.9369326156, -101.1912772380)]
    [InlineData("82022ffffffffff", 79.6312602984, -91.7472470040)]
    [InlineData("820237fffffffff", 81.0069314160, -119.8873014978)]
    [InlineData("820247fffffffff", 74.3965763653, -81.1779110167)]
    [InlineData("82024ffffffffff", 71.5580129815, -78.0608125275)]
    [InlineData("820257fffffffff", 74.6629015861, -92.4279683600)]
    public void Decode_Resolution2_NegativeLongitudes_MatchesReference(string h3Index, double expectedLat, double expectedLon)
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
    [InlineData("820407fffffffff", 74.9284343892, 145.3562419228)]
    [InlineData("82040ffffffffff", 74.6132498066, 157.2060842795)]
    [InlineData("820417fffffffff", 72.1576671504, 140.2485137444)]
    [InlineData("82041ffffffffff", 72.1556735212, 150.4500847434)]
    [InlineData("820427fffffffff", 77.5387839221, 138.1126146806)]
    [InlineData("82042ffffffffff", 77.5359531972, 152.6172470057)]
    [InlineData("820437fffffffff", 74.6178550933, 133.5028872633)]
    [InlineData("820447fffffffff", 75.4272290632, 177.8190293277)]
    [InlineData("82044ffffffffff", 73.6295126169, -172.9622197961)]
    [InlineData("820457fffffffff", 73.7155120727, 168.1178909314)]
    public void Decode_Resolution2_PacificRegion_MatchesReference(string h3Index, double expectedLat, double expectedLon)
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
