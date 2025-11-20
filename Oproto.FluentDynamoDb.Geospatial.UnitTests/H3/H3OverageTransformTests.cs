using Xunit;
using Xunit.Abstractions;

namespace Oproto.FluentDynamoDb.Geospatial.UnitTests.H3;

/// <summary>
/// Tests to manually verify the overage transformation math.
/// </summary>
public class H3OverageTransformTests
{
    private readonly ITestOutputHelper _output;

    public H3OverageTransformTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public void ManualTransform_Face2ToFace7_JKQuadrant()
    {
        // For 8102bffffffffff
        // After DownAp7r for overage check: i=7, j=14, k=7
        // Face 2, JK quadrant -> Face 7, translate=(0,2,2), ccwRot60=3
        
        _output.WriteLine("Starting coordinates: i=7, j=14, k=7 on Face 2");
        _output.WriteLine("Transitioning to Face 7 via JK quadrant");
        _output.WriteLine("");
        
        var i = 7;
        var j = 14;
        var k = 7;
        
        // Apply 3 CCW rotations
        _output.WriteLine("Applying 3 CCW rotations:");
        for (var rot = 0; rot < 3; rot++)
        {
            // IJKRotate60ccw: (i,j,k) -> (k,i,j)
            var temp = i;
            i = k;
            k = j;
            j = temp;
            _output.WriteLine($"  After rotation {rot + 1}: i={i}, j={j}, k={k}");
        }
        
        // Translate by (0,2,2) * unitScale[2] = (0,2,2) * 7
        var transI = 0 * 7;
        var transJ = 2 * 7;
        var transK = 2 * 7;
        
        _output.WriteLine($"Translation vector: ({transI}, {transJ}, {transK})");
        
        i += transI;
        j += transJ;
        k += transK;
        
        _output.WriteLine($"After translation: i={i}, j={j}, k={k}, sum={i + j + k}");
        
        // Normalize
        var min = Math.Min(i, Math.Min(j, k));
        i -= min;
        j -= min;
        k -= min;
        
        _output.WriteLine($"After normalization: i={i}, j={j}, k={k}, sum={i + j + k}");
        
        // Now apply UpAp7r to go back to resolution 1
        // UpAp7r formula: i' = (2i + j) / 7, j' = (3j - i) / 7
        var iOrig = i - k;
        var jOrig = j - k;
        
        var iUp = (int)Math.Round((2.0 * iOrig + jOrig) / 7.0);
        var jUp = (int)Math.Round((3.0 * jOrig - iOrig) / 7.0);
        
        _output.WriteLine($"After UpAp7r: i={iUp}, j={jUp}");
        _output.WriteLine("");
        _output.WriteLine("This should give us the correct coordinates on Face 7");
    }
}
