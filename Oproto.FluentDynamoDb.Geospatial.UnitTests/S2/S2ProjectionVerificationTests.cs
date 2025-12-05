using Oproto.FluentDynamoDb.Geospatial.S2;

namespace Oproto.FluentDynamoDb.Geospatial.UnitTests.S2;

/// <summary>
/// Verification tests for S2 face UV projection formulas against reference implementations.
/// Reference: s2-geometry-library-csharp/S2Geometry/S2Projections.cs
/// Python reference: https://docs.s2cell.aliddell.com/en/stable/annotated_source.html
/// </summary>
public class S2ProjectionVerificationTests
{
    /// <summary>
    /// Reference implementation of FaceUvToXyz from s2-geometry-library-csharp.
    /// This is the canonical implementation we're verifying against.
    /// </summary>
    private static (double x, double y, double z) ReferenceFaceUvToXyz(int face, double u, double v)
    {
        return face switch
        {
            0 => (1, u, v),
            1 => (-u, 1, v),
            2 => (-u, -v, 1),
            3 => (-1, -v, -u),
            4 => (v, -1, -u),
            5 => (v, u, -1),
            _ => throw new ArgumentException($"Invalid face: {face}")
        };
    }

    /// <summary>
    /// Reference implementation of ValidFaceXyzToUv from s2-geometry-library-csharp.
    /// This is the canonical implementation we're verifying against.
    /// </summary>
    private static (double u, double v) ReferenceValidFaceXyzToUv(int face, double x, double y, double z)
    {
        return face switch
        {
            0 => (y / x, z / x),
            1 => (-x / y, z / y),
            2 => (-x / z, -y / z),
            3 => (z / x, y / x),
            4 => (z / y, -x / y),
            5 => (-y / z, -x / z),
            _ => throw new ArgumentException($"Invalid face: {face}")
        };
    }

    [Theory]
    [InlineData(0, 0.0, 0.0)]
    [InlineData(0, 0.5, 0.5)]
    [InlineData(0, -0.5, -0.5)]
    [InlineData(0, 1.0, 1.0)]
    [InlineData(0, -1.0, -1.0)]
    [InlineData(1, 0.0, 0.0)]
    [InlineData(1, 0.5, 0.5)]
    [InlineData(1, -0.5, -0.5)]
    [InlineData(2, 0.0, 0.0)]
    [InlineData(2, 0.5, 0.5)]
    [InlineData(3, 0.0, 0.0)]
    [InlineData(3, 0.5, 0.5)]
    [InlineData(4, 0.0, 0.0)]
    [InlineData(4, 0.5, 0.5)]
    [InlineData(5, 0.0, 0.0)]
    [InlineData(5, 0.5, 0.5)]
    [InlineData(5, -0.5, -0.5)]
    public void FaceUvToXyz_MatchesReferenceImplementation(int face, double u, double v)
    {
        // Arrange
        var (refX, refY, refZ) = ReferenceFaceUvToXyz(face, u, v);
        
        // Normalize reference result (reference returns non-normalized vector)
        var refLength = Math.Sqrt(refX * refX + refY * refY + refZ * refZ);
        refX /= refLength;
        refY /= refLength;
        refZ /= refLength;

        // Act - Use reflection to access private method
        var method = typeof(S2Encoder).GetMethod("FaceUVToXYZ", 
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
        var result = method!.Invoke(null, new object[] { face, u, v });
        var (actualX, actualY, actualZ) = ((double, double, double))result!;

        // Assert
        actualX.Should().BeApproximately(refX, 1e-10, $"X coordinate mismatch for face {face}, u={u}, v={v}");
        actualY.Should().BeApproximately(refY, 1e-10, $"Y coordinate mismatch for face {face}, u={u}, v={v}");
        actualZ.Should().BeApproximately(refZ, 1e-10, $"Z coordinate mismatch for face {face}, u={u}, v={v}");
    }

    [Theory]
    [InlineData(0, 1.0, 0.0, 0.0)]
    [InlineData(0, 1.0, 0.5, 0.5)]
    [InlineData(0, 1.0, -0.5, -0.5)]
    [InlineData(1, 0.0, 1.0, 0.0)]
    [InlineData(1, 0.5, 1.0, 0.5)]
    [InlineData(2, 0.0, 0.0, 1.0)]
    [InlineData(2, -0.5, -0.5, 1.0)]
    [InlineData(3, -1.0, 0.0, 0.0)]
    [InlineData(3, -1.0, 0.5, 0.5)]
    [InlineData(4, 0.0, -1.0, 0.0)]
    [InlineData(4, 0.5, -1.0, 0.5)]
    [InlineData(5, 0.0, 0.0, -1.0)]
    [InlineData(5, 0.5, 0.5, -1.0)]
    public void XyzToFaceUv_MatchesReferenceImplementation(int expectedFace, double x, double y, double z)
    {
        // Arrange
        var (refU, refV) = ReferenceValidFaceXyzToUv(expectedFace, x, y, z);

        // Act - Use reflection to access private method
        var method = typeof(S2Encoder).GetMethod("XYZToFaceUV",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
        var result = method!.Invoke(null, new object[] { expectedFace, x, y, z });
        var (actualU, actualV) = ((double, double))result!;

        // Assert
        actualU.Should().BeApproximately(refU, 1e-10, $"U coordinate mismatch for face {expectedFace}");
        actualV.Should().BeApproximately(refV, 1e-10, $"V coordinate mismatch for face {expectedFace}");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    public void FaceUvToXyz_RoundTrip_PreservesCoordinates(int face)
    {
        // Test round-trip conversion for various UV coordinates
        var testPoints = new[]
        {
            (0.0, 0.0),
            (0.5, 0.5),
            (-0.5, -0.5),
            (0.7, -0.3),
            (-0.8, 0.6)
        };

        foreach (var (u, v) in testPoints)
        {
            // Convert UV to XYZ
            var faceUvToXyzMethod = typeof(S2Encoder).GetMethod("FaceUVToXYZ",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
            var xyzResult = faceUvToXyzMethod!.Invoke(null, new object[] { face, u, v });
            var (x, y, z) = ((double, double, double))xyzResult!;

            // Convert XYZ back to UV
            var xyzToFaceUvMethod = typeof(S2Encoder).GetMethod("XYZToFaceUV",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
            var uvResult = xyzToFaceUvMethod!.Invoke(null, new object[] { face, x, y, z });
            var (actualU, actualV) = ((double, double))uvResult!;

            // Assert round-trip preserves coordinates
            actualU.Should().BeApproximately(u, 1e-10, $"U coordinate not preserved for face {face}, original u={u}, v={v}");
            actualV.Should().BeApproximately(v, 1e-10, $"V coordinate not preserved for face {face}, original u={u}, v={v}");
        }
    }

    [Fact]
    public void FaceUvToXyz_AllFaces_ProduceUnitVectors()
    {
        // Verify that FaceUvToXyz produces unit vectors (normalized)
        for (var face = 0; face < 6; face++)
        {
            var testPoints = new[]
            {
                (0.0, 0.0),
                (0.5, 0.5),
                (-0.5, -0.5),
                (1.0, 0.0),
                (0.0, 1.0)
            };

            foreach (var (u, v) in testPoints)
            {
                var method = typeof(S2Encoder).GetMethod("FaceUVToXYZ",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
                var result = method!.Invoke(null, new object[] { face, u, v });
                var (x, y, z) = ((double, double, double))result!;

                var length = Math.Sqrt(x * x + y * y + z * z);
                length.Should().BeApproximately(1.0, 1e-10, 
                    $"Vector not normalized for face {face}, u={u}, v={v}");
            }
        }
    }

    [Fact]
    public void FaceUvToXyz_FaceCenters_MatchExpectedNormals()
    {
        // Face centers (u=0, v=0) should point in the direction of the face normal
        var expectedNormals = new[]
        {
            (1.0, 0.0, 0.0),   // Face 0: +X
            (0.0, 1.0, 0.0),   // Face 1: +Y
            (0.0, 0.0, 1.0),   // Face 2: +Z
            (-1.0, 0.0, 0.0),  // Face 3: -X
            (0.0, -1.0, 0.0),  // Face 4: -Y
            (0.0, 0.0, -1.0)   // Face 5: -Z
        };

        for (var face = 0; face < 6; face++)
        {
            var method = typeof(S2Encoder).GetMethod("FaceUVToXYZ",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
            var result = method!.Invoke(null, new object[] { face, 0.0, 0.0 });
            var (x, y, z) = ((double, double, double))result!;

            var (expectedX, expectedY, expectedZ) = expectedNormals[face];
            x.Should().BeApproximately(expectedX, 1e-10, $"X normal mismatch for face {face}");
            y.Should().BeApproximately(expectedY, 1e-10, $"Y normal mismatch for face {face}");
            z.Should().BeApproximately(expectedZ, 1e-10, $"Z normal mismatch for face {face}");
        }
    }

    [Theory]
    [InlineData(0, 1, 1)]   // Face 0 corner at (1, 1) should match Face 1 corner at (-1, -1)
    [InlineData(1, 2, 1)]   // Face 1 corner should match Face 2 corner
    [InlineData(2, 3, 1)]   // Face 2 corner should match Face 3 corner
    [InlineData(3, 4, 1)]   // Face 3 corner should match Face 4 corner
    [InlineData(4, 5, 1)]   // Face 4 corner should match Face 5 corner
    [InlineData(5, 0, 1)]   // Face 5 corner should match Face 0 corner
    public void FaceUvToXyz_AdjacentFaceCorners_Match(int face1, int face2, double sign)
    {
        // Based on reference test: adjacent faces should have matching corners
        // The sign depends on whether axes are swapped (SwapMask)
        var swapMask = 1;
        var actualSign = ((face1 & swapMask) != 0) ? -1 : 1;

        var method = typeof(S2Encoder).GetMethod("FaceUVToXYZ",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
        
        var result1 = method!.Invoke(null, new object[] { face1, actualSign * 1.0, -actualSign * 1.0 });
        var (x1, y1, z1) = ((double, double, double))result1!;

        var result2 = method!.Invoke(null, new object[] { face2, -1.0, -1.0 });
        var (x2, y2, z2) = ((double, double, double))result2!;

        // Adjacent face corners should be very close (within floating point precision)
        x1.Should().BeApproximately(x2, 1e-10, $"X mismatch between face {face1} and {face2}");
        y1.Should().BeApproximately(y2, 1e-10, $"Y mismatch between face {face1} and {face2}");
        z1.Should().BeApproximately(z2, 1e-10, $"Z mismatch between face {face1} and {face2}");
    }
}
