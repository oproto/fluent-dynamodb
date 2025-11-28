using Oproto.FluentDynamoDb.Geospatial.H3;
using Xunit;
using Xunit.Abstractions;

namespace Oproto.FluentDynamoDb.Geospatial.UnitTests.H3;

/// <summary>
/// Tests for resolution 1 cells using data from H3 reference implementation.
/// Test data extracted from h3/tests/inputfiles/res01ic.txt
/// 
/// Resolution 1 cells are the first level of subdivision beyond base cells.
/// Each base cell is divided into 7 resolution 1 cells (aperture-7 hierarchy).
/// This tests the DownAp7/DownAp7r operations and digit path encoding/decoding.
/// </summary>
public class H3Resolution1Tests
{
    private readonly ITestOutputHelper _output;

    public H3Resolution1Tests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Theory]
    [InlineData("81003ffffffffff", 79.2423985098, 38.0234070080)]
    [InlineData("81007ffffffffff", 83.6576608516, -7.3190411494)]
    [InlineData("8100bffffffffff", 77.1530287166, 74.8802719453)]
    [InlineData("8100fffffffffff", 85.1221781325, 76.6384313654)]
    [InlineData("81013ffffffffff", 72.6804915314, 28.6431886669)]
    [InlineData("81017ffffffffff", 76.7930159352, 5.1064179565)]
    [InlineData("8101bffffffffff", 72.5117339545, 52.9068719179)]
    [InlineData("81023ffffffffff", 79.2209863563, -107.4292022430)]
    [InlineData("81027ffffffffff", 74.3965763653, -81.1779110167)]
    [InlineData("8102bffffffffff", 73.6130161629, -130.7435224238)]
    public void Decode_Resolution1_First10Cells_MatchesReference(string h3Index, double expectedLat, double expectedLon)
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
    [InlineData("8102fffffffffff", 71.5466152153, -106.8885790982)]
    [InlineData("81033ffffffffff", 86.8898850176, -110.5848335870)]
    [InlineData("81037ffffffffff", 80.9187885742, -62.4026191870)]
    [InlineData("8103bffffffffff", 79.8699090482, -149.9936327509)]
    [InlineData("81043ffffffffff", 74.9284343892, 145.3562419228)]
    [InlineData("81047ffffffffff", 75.4272290632, 177.8190293277)]
    [InlineData("8104bffffffffff", 66.8114213805, 141.4085042847)]
    [InlineData("8104fffffffffff", 68.6461020944, 162.6003011509)]
    [InlineData("81053ffffffffff", 78.5326304791, 112.4718685761)]
    [InlineData("81057ffffffffff", 82.8268583759, 157.8950188628)]
    public void Decode_Resolution1_Next10Cells_MatchesReference(string h3Index, double expectedLat, double expectedLon)
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
    [InlineData("8105bffffffffff", 70.6579864870, 121.2922149856)]
    [InlineData("81063ffffffffff", 69.6634529498, -30.9680446065)]
    [InlineData("81067ffffffffff", 61.9770501575, -33.0655318324)]
    [InlineData("8106bffffffffff", 73.5549888727, -52.1609388733)]
    [InlineData("8106fffffffffff", 65.7039339401, -48.0992655672)]
    [InlineData("81073ffffffffff", 71.3215642384, -10.7239457859)]
    [InlineData("81077ffffffffff", 64.5054097415, -18.1800804990)]
    [InlineData("8107bffffffffff", 77.1613539876, -26.6084854461)]
    [InlineData("81083ffffffffff", 64.7000001279, 10.5361990755)]
    [InlineData("8108bffffffffff", 60.1912180486, 18.2585301192)]
    public void Decode_Resolution1_VariousLocations_MatchesReference(string h3Index, double expectedLat, double expectedLon)
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
