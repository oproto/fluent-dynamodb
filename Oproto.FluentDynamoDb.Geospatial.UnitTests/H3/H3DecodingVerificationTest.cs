using Oproto.FluentDynamoDb.Geospatial.H3;
using Xunit;
using Xunit.Abstractions;

namespace Oproto.FluentDynamoDb.Geospatial.UnitTests.H3;

/// <summary>
/// Verification test to confirm that our decoding matches the H3 reference implementation.
/// </summary>
public class H3DecodingVerificationTest
{
    private readonly ITestOutputHelper _output;

    public H3DecodingVerificationTest(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public void Decode_824b9ffffffffff_MatchesH3Reference()
    {
        // From h3/tests/inputfiles/res02ic.txt:
        // 824b9ffffffffff 19.7638554673 123.2819514071
        const string h3Index = "824b9ffffffffff";
        const double expectedLat = 19.7638554673;
        const double expectedLon = 123.2819514071;
        
        _output.WriteLine($"=== Verifying Decoding Against H3 Reference ===");
        _output.WriteLine($"H3 Index: {h3Index}");
        _output.WriteLine($"Expected (from H3 reference): lat={expectedLat:F10}°, lon={expectedLon:F10}°");
        _output.WriteLine("");
        
        // Decode the index
        var (actualLat, actualLon) = H3Encoder.Decode(h3Index);
        
        _output.WriteLine($"Actual (our implementation):  lat={actualLat:F10}°, lon={actualLon:F10}°");
        _output.WriteLine($"Difference:                   lat={Math.Abs(actualLat - expectedLat):F10}°, lon={Math.Abs(actualLon - expectedLon):F10}°");
        _output.WriteLine("");
        
        // Check if our implementation matches the H3 reference
        var latError = Math.Abs(actualLat - expectedLat);
        var lonError = Math.Abs(actualLon - expectedLon);
        
        if (latError < 0.0001 && lonError < 0.0001)
        {
            _output.WriteLine("✓ SUCCESS: Our implementation matches the H3 reference implementation!");
        }
        else
        {
            _output.WriteLine("✗ FAILURE: Our implementation does NOT match the H3 reference!");
        }
        
        // Assert with tight tolerance since we should match exactly
        Assert.InRange(actualLat, expectedLat - 0.0001, expectedLat + 0.0001);
        Assert.InRange(actualLon, expectedLon - 0.0001, expectedLon + 0.0001);
    }

    [Fact]
    public void Explain_WhyDecodedValueDiffersFromOriginalPoint()
    {
        _output.WriteLine("=== Understanding H3 Encoding/Decoding ===");
        _output.WriteLine("");
        _output.WriteLine("When you encode a point (20°, 123°) at resolution 2:");
        _output.WriteLine("  1. H3 finds which cell contains that point");
        _output.WriteLine("  2. The cell is identified by index 824b9ffffffffff");
        _output.WriteLine("  3. The cell has a CENTER at (19.76°, 123.28°)");
        _output.WriteLine("");
        _output.WriteLine("When you decode the index 824b9ffffffffff:");
        _output.WriteLine("  1. H3 returns the CENTER of the cell");
        _output.WriteLine("  2. The center is at (19.76°, 123.28°)");
        _output.WriteLine("  3. This is NOT the same as the original point!");
        _output.WriteLine("");
        _output.WriteLine("This is EXPECTED BEHAVIOR:");
        _output.WriteLine("  - Resolution 2 cells have ~86km edge length");
        _output.WriteLine("  - The original point (20°, 123°) is somewhere in the cell");
        _output.WriteLine("  - The decoded center (19.76°, 123.28°) is ~27-29km away");
        _output.WriteLine("  - This distance is well within the cell size");
        _output.WriteLine("");
        _output.WriteLine("CONCLUSION: The 'error' is not a bug - it's how H3 works!");
        _output.WriteLine("Our implementation correctly returns the cell center, matching H3 reference.");
    }
}
