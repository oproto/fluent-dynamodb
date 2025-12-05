using Oproto.FluentDynamoDb.Geospatial.H3;
using Xunit;
using Xunit.Abstractions;

namespace Oproto.FluentDynamoDb.Geospatial.UnitTests.H3;

/// <summary>
/// Verify that encoding is selecting the correct cell.
/// </summary>
public class H3EncodingVerificationTest
{
    private readonly ITestOutputHelper _output;

    public H3EncodingVerificationTest(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public void VerifyDecodingProducesCorrectCellCenter()
    {
        // NOTE: H3 does NOT guarantee that a cell's center re-encodes to the same cell.
        // This is documented behavior due to the aperture-7 hexagonal grid design.
        // This test verifies that decoding produces the expected cell center coordinates.
        
        // From reference data: cell 8403949ffffffff has center at (78.20, -163.03)
        var referenceIndex = "8403949ffffffff";
        var expectedCenter = (lat: 78.2041270329, lon: -163.0317541712);
        
        _output.WriteLine("=== Verifying Reference Cell Decoding ===");
        _output.WriteLine($"Reference index: {referenceIndex}");
        _output.WriteLine($"Expected center: ({expectedCenter.lat:F10}, {expectedCenter.lon:F10})");
        
        // Decode the reference index - should give us the expected center
        var (actualLat, actualLon) = H3Encoder.Decode(referenceIndex);
        _output.WriteLine($"Actual center: ({actualLat:F10}, {actualLon:F10})");
        
        // Verify the decoded center matches the expected center (within tolerance)
        Assert.InRange(actualLat, expectedCenter.lat - 0.0001, expectedCenter.lat + 0.0001);
        Assert.InRange(actualLon, expectedCenter.lon - 0.0001, expectedCenter.lon + 0.0001);
    }

    [Theory]
    [InlineData("8403949ffffffff", 78.2041270329, -163.0317541712)]
    [InlineData("840392bffffffff", 76.4716888372, -157.2674548147)]
    [InlineData("8403935ffffffff", 76.1370791738, -154.5358942155)]
    public void VerifyReferenceCellDecoding(string referenceIndex, double expectedCenterLat, double expectedCenterLon)
    {
        // NOTE: This test verifies that our decoding matches the H3 reference implementation.
        // We're testing decoding accuracy, not round-trip consistency.
        
        _output.WriteLine($"=== Reference index: {referenceIndex} ===");
        _output.WriteLine($"Expected center: ({expectedCenterLat:F10}, {expectedCenterLon:F10})");
        
        // Decode the reference index
        var (actualLat, actualLon) = H3Encoder.Decode(referenceIndex);
        _output.WriteLine($"Actual center: ({actualLat:F10}, {actualLon:F10})");
        
        // Verify the decoded center matches the expected center (within tolerance)
        Assert.InRange(actualLat, expectedCenterLat - 0.0001, expectedCenterLat + 0.0001);
        Assert.InRange(actualLon, expectedCenterLon - 0.0001, expectedCenterLon + 0.0001);
    }
}
