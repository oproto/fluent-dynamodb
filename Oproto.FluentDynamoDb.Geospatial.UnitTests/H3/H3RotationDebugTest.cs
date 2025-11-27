using System.Reflection;
using Oproto.FluentDynamoDb.Geospatial.H3;
using Xunit;
using Xunit.Abstractions;

namespace Oproto.FluentDynamoDb.Geospatial.UnitTests.H3;

/// <summary>
/// Test to debug rotation logic for pentagon base cells.
/// </summary>
public class H3RotationDebugTest
{
    private readonly ITestOutputHelper _output;

    public H3RotationDebugTest(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public void Debug_Rotation_ForArabianSea()
    {
        var lat = 14.947707264183059;
        var lon = 58.099686036545755;
        var resolution = 2;

        _output.WriteLine($"=== Rotation Debug for Arabian Sea ===");
        _output.WriteLine($"Original: ({lat:F6}, {lon:F6}), Resolution: {resolution}");

        // Encode without rotation to see the raw digits
        var h3Index = H3Encoder.Encode(lat, lon, resolution);
        var indexValue = Convert.ToUInt64(h3Index, 16);
        var baseCell = (int)((indexValue >> 45) & 0x7F);
        
        _output.WriteLine($"\nEncoded H3 Index: {h3Index}");
        _output.WriteLine($"Base Cell: {baseCell}");
        
        // Extract digits
        _output.WriteLine("Digits:");
        for (int r = 1; r <= resolution; r++)
        {
            var digitOffset = (15 - r) * 3;
            var digit = (int)((indexValue >> digitOffset) & 0x7);
            _output.WriteLine($"  Res {r}: digit={digit}");
        }

        // Check what the H3 reference library produces for the same point
        // (We can't call the reference library directly, but we can compare with known values)
        
        // Let's trace through the encoding manually
        var encoderType = typeof(H3Encoder);
        
        // Get GeoToHex2d result
        var geoToHex2dMethod = encoderType.GetMethod("GeoToHex2d",
            BindingFlags.NonPublic | BindingFlags.Static);
        if (geoToHex2dMethod != null)
        {
            var result = geoToHex2dMethod.Invoke(null, new object[] { lat, lon, resolution });
            _output.WriteLine($"\nGeoToHex2d: {result}");
        }

        // Decode and check
        var (decodedLat, decodedLon) = H3Encoder.Decode(h3Index);
        _output.WriteLine($"\nDecoded: ({decodedLat:F6}, {decodedLon:F6})");
        _output.WriteLine($"Error: lat={Math.Abs(decodedLat - lat):F6}°, lon={Math.Abs(decodedLon - lon):F6}°");
    }

    [Fact]
    public void Debug_ManualDigitTraversal()
    {
        // Manually trace through digit traversal for the Arabian Sea point
        _output.WriteLine("=== Manual Digit Traversal ===");
        
        // Base cell 49 data (from H3 reference)
        // Home face: 14
        // Home IJK: (2, 0, 0)
        // Is pentagon: true
        
        var homeFace = 14;
        var homeI = 2;
        var homeJ = 0;
        var homeK = 0;
        
        _output.WriteLine($"Base cell 49: home face={homeFace}, IJK=({homeI},{homeJ},{homeK})");
        
        // Digits from the encoded index: [2, 5]
        var digits = new int[] { 2, 5 };
        
        // Traverse digits manually
        var i = homeI;
        var j = homeJ;
        var k = homeK;
        
        for (int r = 1; r <= digits.Length; r++)
        {
            var digit = digits[r - 1];
            
            // Apply down-aperture based on resolution class
            if (r % 2 == 1) // Class III (odd resolution)
            {
                // DownAp7 (rotate CCW)
                _output.WriteLine($"  Res {r}: DownAp7 (Class III)");
                (i, j, k) = DownAp7(i, j, k);
            }
            else // Class II (even resolution)
            {
                // DownAp7r (rotate CW)
                _output.WriteLine($"  Res {r}: DownAp7r (Class II)");
                (i, j, k) = DownAp7r(i, j, k);
            }
            
            _output.WriteLine($"    After down-aperture: IJK=({i},{j},{k})");
            
            // Apply neighbor offset
            (i, j, k) = Neighbor(i, j, k, digit);
            _output.WriteLine($"    After neighbor (digit={digit}): IJK=({i},{j},{k}), sum={i+j+k}");
        }
        
        _output.WriteLine($"\nFinal IJK: ({i},{j},{k}), sum={i+j+k}");
        
        // Check if overage should trigger
        // MaxDimByCIIRes[2] = 14
        var maxDim = 14;
        _output.WriteLine($"maxDim={maxDim}, sum > maxDim: {i+j+k > maxDim}");
    }

    // Helper methods for manual traversal
    private static (int i, int j, int k) DownAp7(int i, int j, int k)
    {
        // From H3 reference: _downAp7
        // iVec = {3, 0, 1}, jVec = {1, 3, 0}, kVec = {0, 1, 3}
        var newI = 3 * i + 1 * j + 0 * k;
        var newJ = 0 * i + 3 * j + 1 * k;
        var newK = 1 * i + 0 * j + 3 * k;
        return Normalize(newI, newJ, newK);
    }

    private static (int i, int j, int k) DownAp7r(int i, int j, int k)
    {
        // From H3 reference: _downAp7r
        // iVec = {3, 1, 0}, jVec = {0, 3, 1}, kVec = {1, 0, 3}
        var newI = 3 * i + 0 * j + 1 * k;
        var newJ = 1 * i + 3 * j + 0 * k;
        var newK = 0 * i + 1 * j + 3 * k;
        return Normalize(newI, newJ, newK);
    }

    private static (int i, int j, int k) Neighbor(int i, int j, int k, int digit)
    {
        // From H3 reference: _neighbor
        // Digit offsets in IJK
        var offsets = new (int di, int dj, int dk)[]
        {
            (0, 0, 0),   // 0: center
            (0, 0, 1),   // 1: k-axis
            (0, 1, 0),   // 2: j-axis
            (0, 1, 1),   // 3: jk-axis
            (1, 0, 0),   // 4: i-axis
            (1, 0, 1),   // 5: ik-axis
            (1, 1, 0),   // 6: ij-axis
        };
        
        var (di, dj, dk) = offsets[digit];
        return Normalize(i + di, j + dj, k + dk);
    }

    private static (int i, int j, int k) Normalize(int i, int j, int k)
    {
        // Normalize IJK coordinates so that at most one is negative
        // and the minimum is 0
        var min = Math.Min(i, Math.Min(j, k));
        return (i - min, j - min, k - min);
    }
}
