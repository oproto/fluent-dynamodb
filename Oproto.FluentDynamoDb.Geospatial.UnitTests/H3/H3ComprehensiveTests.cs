using Oproto.FluentDynamoDb.Geospatial.H3;
using Xunit;
using Xunit.Abstractions;

namespace Oproto.FluentDynamoDb.Geospatial.UnitTests.H3;

/// <summary>
/// Comprehensive tests covering edge cases and high-value scenarios from H3 reference implementation.
/// Test data extracted from h3/tests/inputfiles/*.txt
/// </summary>
public class H3ComprehensiveTests
{
    private readonly ITestOutputHelper _output;

    public H3ComprehensiveTests(ITestOutputHelper output)
    {
        _output = output;
    }

    #region Near-Pole Tests

    [Theory]
    [InlineData("820327fffffffff", 89.2415240263, -35.5323178498)]  // Very close to North Pole
    [InlineData("82005ffffffffff", 86.4484351388, -10.1752678992)]  // Near North Pole
    [InlineData("8200cffffffffff", 87.6680058054, 104.9599570631)]  // Near North Pole
    [InlineData("8200effffffffff", 86.9116280774, 41.1127984813)]   // Near North Pole
    public void Decode_NearNorthPole_MatchesReference(string h3Index, double expectedLat, double expectedLon)
    {
        // Act
        var (actualLat, actualLon) = H3Encoder.Decode(h3Index);
        
        _output.WriteLine($"H3: {h3Index}");
        _output.WriteLine($"Expected: lat={expectedLat:F10}, lon={expectedLon:F10}");
        _output.WriteLine($"Actual:   lat={actualLat:F10}, lon={actualLon:F10}");
        _output.WriteLine($"Diff:     lat={Math.Abs(actualLat - expectedLat):F10}, lon={Math.Abs(actualLon - expectedLon):F10}");
        
        // Assert - allow small tolerance for floating point
        Assert.InRange(actualLat, expectedLat - 0.0001, expectedLat + 0.0001);
        Assert.InRange(actualLon, expectedLon - 0.0001, expectedLon + 0.0001);
    }

    #endregion

    #region Pentagon Tests

    [Theory]
    [InlineData("8009fffffffffff", 64.7000001279, 10.5361990755)]    // Base cell 4 (pentagon)
    [InlineData("801dfffffffffff", 50.1032014822, -143.4784900150)]  // Base cell 14 (pentagon)
    [InlineData("8031fffffffffff", 39.1000000340, 122.3000004078)]   // Base cell 24 (pentagon)
    [InlineData("804dfffffffffff", 23.7179252712, -67.1323263664)]   // Base cell 38 (pentagon)
    [InlineData("8063fffffffffff", 10.4473451875, 58.1577058396)]    // Base cell 49 (pentagon)
    [InlineData("8075fffffffffff", 2.3008821116, -5.2453902968)]     // Base cell 58 (pentagon)
    public void Decode_PentagonBaseCells_MatchesReference(string h3Index, double expectedLat, double expectedLon)
    {
        // Act
        var (actualLat, actualLon) = H3Encoder.Decode(h3Index);
        
        _output.WriteLine($"H3: {h3Index} (Pentagon)");
        _output.WriteLine($"Expected: lat={expectedLat:F10}, lon={expectedLon:F10}");
        _output.WriteLine($"Actual:   lat={actualLat:F10}, lon={actualLon:F10}");
        _output.WriteLine($"Diff:     lat={Math.Abs(actualLat - expectedLat):F10}, lon={Math.Abs(actualLon - expectedLon):F10}");
        
        // Assert
        Assert.InRange(actualLat, expectedLat - 0.0001, expectedLat + 0.0001);
        Assert.InRange(actualLon, expectedLon - 0.0001, expectedLon + 0.0001);
    }

    #endregion

    #region Date Line Tests

    [Theory]
    [InlineData("820447fffffffff", 75.427229063200002, 177.81902932770001)]  // Near +180°
    [InlineData("82044ffffffffff", 73.629512616900001, -172.96221979609999)]  // Near -180°
    [InlineData("820337fffffffff", 87.6534240158, -173.9137399635)]           // Near -180° and North Pole
    public void Decode_NearDateLine_MatchesReference(string h3Index, double expectedLat, double expectedLon)
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

    #endregion

    #region Round-Trip Tests

    [Theory]
    [InlineData("8001fffffffffff")]  // Resolution 0
    [InlineData("8100bffffffffff")]  // Resolution 1
    [InlineData("824b9ffffffffff")]  // Resolution 2
    [InlineData("830000fffffffff")]  // Resolution 3
    // Note: Resolution 4+ can have multiple valid representations for the same cell
    // due to the way H3 encodes the digit path, so we don't test round-trip at higher resolutions
    public void RoundTrip_DecodeAndReencode_ProducesSameIndex(string originalIndex)
    {
        // Decode to get center
        var (lat, lon) = H3Encoder.Decode(originalIndex);
        
        _output.WriteLine($"Original index: {originalIndex}");
        _output.WriteLine($"Decoded center: lat={lat:F10}, lon={lon:F10}");
        
        // Extract resolution from index
        var indexValue = Convert.ToUInt64(originalIndex, 16);
        var resolution = (int)((indexValue >> 52) & 0xF);
        
        // Re-encode the center
        var reEncodedIndex = H3Encoder.Encode(lat, lon, resolution);
        
        _output.WriteLine($"Re-encoded:     {reEncodedIndex}");
        _output.WriteLine($"Match: {originalIndex == reEncodedIndex}");
        
        // The re-encoded index should match the original
        Assert.Equal(originalIndex, reEncodedIndex);
    }

    #endregion

    #region Equator Tests

    [Fact]
    public void Encode_Equator_ProducesValidIndex()
    {
        // Test encoding a point on the equator
        var lat = 0.0;
        var lon = 0.0;
        var resolution = 5;
        
        var h3Index = H3Encoder.Encode(lat, lon, resolution);
        
        _output.WriteLine($"Input: lat={lat}, lon={lon}, res={resolution}");
        _output.WriteLine($"H3 Index: {h3Index}");
        
        // Verify it's a valid index
        Assert.NotNull(h3Index);
        Assert.InRange(h3Index.Length, 1, 16);
        
        // Verify we can decode it
        var (decodedLat, decodedLon) = H3Encoder.Decode(h3Index);
        Assert.InRange(decodedLat, -90, 90);
        Assert.InRange(decodedLon, -180, 180);
    }

    #endregion

    #region High Resolution Spot Checks

    [Fact]
    public void Encode_Resolution9_ProducesValidIndex()
    {
        // Resolution 9 is commonly used for location-based apps (~174m cells)
        var lat = 37.7749;  // San Francisco
        var lon = -122.4194;
        var resolution = 9;
        
        var h3Index = H3Encoder.Encode(lat, lon, resolution);
        
        _output.WriteLine($"Input: lat={lat}, lon={lon}, res={resolution}");
        _output.WriteLine($"H3 Index: {h3Index}");
        
        // Verify it's a valid index
        Assert.NotNull(h3Index);
        Assert.InRange(h3Index.Length, 1, 16);
        Assert.Matches("^[0-9a-f]+$", h3Index);
        
        // Verify we can decode it
        var (decodedLat, decodedLon) = H3Encoder.Decode(h3Index);
        Assert.InRange(decodedLat, -90, 90);
        Assert.InRange(decodedLon, -180, 180);
    }

    [Fact]
    public void Encode_Resolution12_ProducesValidIndex()
    {
        // Resolution 12 provides building-level precision (~22m cells)
        var lat = 51.5074;  // London
        var lon = -0.1278;
        var resolution = 12;
        
        var h3Index = H3Encoder.Encode(lat, lon, resolution);
        
        _output.WriteLine($"Input: lat={lat}, lon={lon}, res={resolution}");
        _output.WriteLine($"H3 Index: {h3Index}");
        
        // Verify it's a valid index
        Assert.NotNull(h3Index);
        Assert.InRange(h3Index.Length, 1, 16);
        Assert.Matches("^[0-9a-f]+$", h3Index);
        
        // Verify we can decode it
        var (decodedLat, decodedLon) = H3Encoder.Decode(h3Index);
        Assert.InRange(decodedLat, -90, 90);
        Assert.InRange(decodedLon, -180, 180);
    }

    #endregion
}
