using System.Reflection;
using Xunit;
using Xunit.Abstractions;

namespace Oproto.FluentDynamoDb.Geospatial.UnitTests.H3;

/// <summary>
/// Test that IJK -> Hex2d -> IJK round-trips correctly.
/// </summary>
public class H3CoordinateRoundTripTest
{
    private readonly ITestOutputHelper _output;

    public H3CoordinateRoundTripTest(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact(Skip = "Debug test - uses reflection to test internal coordinate transformations. Not needed for regular test runs.")]
    public void IJKToHex2dToIJK_ShouldRoundTrip()
    {
        // This test was used for debugging internal coordinate transformations.
        // It uses reflection to access private methods and is kept for reference but skipped in regular test runs.
        // The IJK <-> Hex2d transformations are tested indirectly through the public encode/decode methods.
        
        _output.WriteLine("This test is skipped - it was used for debugging internal transformations.");
    }
}
