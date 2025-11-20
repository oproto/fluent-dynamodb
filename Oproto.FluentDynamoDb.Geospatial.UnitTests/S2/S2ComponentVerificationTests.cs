using Oproto.FluentDynamoDb.Geospatial.S2;

namespace Oproto.FluentDynamoDb.Geospatial.UnitTests.S2;

/// <summary>
/// Component-level verification tests for S2 encoder based on reference implementation tests.
/// These tests verify individual components of the S2 encoding process.
/// Reference: s2-geometry-library-csharp/S2Geometry.Tests/S2Test.cs
/// </summary>
public class S2ComponentVerificationTests
{
    private static readonly Random Random = new(42); // Fixed seed for reproducible tests

    /// <summary>
    /// Test conversion from latitude/longitude to XYZ coordinates.
    /// </summary>
    [Theory]
    [InlineData(0.0, 0.0)]     // Equator, Prime Meridian
    [InlineData(90.0, 0.0)]    // North Pole
    [InlineData(-90.0, 0.0)]   // South Pole
    [InlineData(45.0, 90.0)]   // 45°N, 90°E
    [InlineData(-45.0, -90.0)] // 45°S, 90°W
    public void LatLngToXYZ_KnownCoordinates_ProducesUnitVectors(double lat, double lon)
    {
        // Act
        var (x, y, z) = S2Encoder.LatLonToXYZ(lat, lon);

        // Assert - Should be unit vector
        var length = Math.Sqrt(x * x + y * y + z * z);
        length.Should().BeApproximately(1.0, 1e-10);
        
        // Verify specific known cases
        if (Math.Abs(lat) < 1e-10 && Math.Abs(lon) < 1e-10)
        {
            // Equator, Prime Meridian should be (1, 0, 0)
            x.Should().BeApproximately(1.0, 1e-10);
            y.Should().BeApproximately(0.0, 1e-10);
            z.Should().BeApproximately(0.0, 1e-10);
        }
        else if (Math.Abs(lat - 90.0) < 1e-10)
        {
            // North Pole should be (0, 0, 1)
            x.Should().BeApproximately(0.0, 1e-10);
            y.Should().BeApproximately(0.0, 1e-10);
            z.Should().BeApproximately(1.0, 1e-10);
        }
        else if (Math.Abs(lat + 90.0) < 1e-10)
        {
            // South Pole should be (0, 0, -1)
            x.Should().BeApproximately(0.0, 1e-10);
            y.Should().BeApproximately(0.0, 1e-10);
            z.Should().BeApproximately(-1.0, 1e-10);
        }
    }

    /// <summary>
    /// Test XYZ to face determination.
    /// </summary>
    [Theory]
    [InlineData(1.0, 0.0, 0.0, 0)]   // +X face
    [InlineData(0.0, 1.0, 0.0, 1)]   // +Y face
    [InlineData(0.0, 0.0, 1.0, 2)]   // +Z face
    [InlineData(-1.0, 0.0, 0.0, 3)]  // -X face
    [InlineData(0.0, -1.0, 0.0, 4)]  // -Y face
    [InlineData(0.0, 0.0, -1.0, 5)]  // -Z face
    public void XYZToFace_FaceCenters_ReturnsCorrectFace(double x, double y, double z, int expectedFace)
    {
        // Act
        var actualFace = S2Encoder.XYZToFace(x, y, z);

        // Assert
        actualFace.Should().Be(expectedFace);
    }

    /// <summary>
    /// Test ST to UV coordinate conversion.
    /// CRITICAL: ST coordinates are in the range [-1, 1], not [0, 1].
    /// </summary>
    [Theory]
    [InlineData(0.0)]      // Center
    [InlineData(-0.5)]     // Quarter towards -1
    [InlineData(0.5)]      // Quarter towards +1
    [InlineData(-1.0)]     // Edge
    [InlineData(1.0)]      // Other edge
    public void STToUV_VariousCoordinates_ProducesValidResults(double s)
    {
        // Act
        var u = S2Encoder.STToUV(s);

        // Assert - UV should be in range [-1, 1]
        u.Should().BeInRange(-1.0, 1.0);
        
        // Test specific known values
        if (Math.Abs(s) < 1e-10)
        {
            u.Should().BeApproximately(0.0, 1e-10); // Center (s=0) should map to u=0
        }
    }

    /// <summary>
    /// Test UV to ST coordinate conversion (inverse of STToUV).
    /// CRITICAL: ST coordinates are in the range [-1, 1], not [0, 1].
    /// </summary>
    [Theory]
    [InlineData(0.0)]      // Center
    [InlineData(0.5)]      // Half way to edge
    [InlineData(-0.5)]     // Other direction
    [InlineData(0.9)]      // Near edge
    [InlineData(-0.9)]     // Near other edge
    public void UVToST_RoundTrip_PreservesCoordinates(double uv)
    {
        // Act - Convert UV to ST and back
        var st = S2Encoder.UVToST(uv);
        var uvBack = S2Encoder.STToUV(st);

        // Assert - Round trip should preserve coordinate
        uvBack.Should().BeApproximately(uv, 1e-10);
        
        // CRITICAL FIX: ST should be in range [-1, 1], not [0, 1]
        st.Should().BeInRange(-1.0, 1.0);
    }

    /// <summary>
    /// Test that each face center maps correctly through the full pipeline.
    /// Based on testCellIdFromPoint from reference.
    /// </summary>
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    public void FaceUVToXYZ_FaceCenters_ProducesCorrectPoints(int face)
    {
        // Arrange - Get face center in UV coordinates (0, 0)
        
        // Act
        var (x, y, z) = S2Encoder.FaceUVToXYZ(face, 0.0, 0.0);
        
        // Assert - Should be unit vector
        var length = Math.Sqrt(x * x + y * y + z * z);
        length.Should().BeApproximately(1.0, 1e-10);
        
        // One component should be ±1, others should be 0
        var absX = Math.Abs(x);
        var absY = Math.Abs(y);
        var absZ = Math.Abs(z);
        var maxComponent = Math.Max(absX, Math.Max(absY, absZ));
        maxComponent.Should().BeApproximately(1.0, 1e-10);
        
        // Verify the correct component is 1 for each face
        switch (face)
        {
            case 0: // +X
                x.Should().BeApproximately(1.0, 1e-10);
                break;
            case 1: // +Y
                y.Should().BeApproximately(1.0, 1e-10);
                break;
            case 2: // +Z
                z.Should().BeApproximately(1.0, 1e-10);
                break;
            case 3: // -X
                x.Should().BeApproximately(-1.0, 1e-10);
                break;
            case 4: // -Y
                y.Should().BeApproximately(-1.0, 1e-10);
                break;
            case 5: // -Z
                z.Should().BeApproximately(-1.0, 1e-10);
                break;
        }
    }

    /// <summary>
    /// Test XYZ to FaceUV conversion round trip.
    /// </summary>
    [Theory]
    [InlineData(0, 0.0, 0.0)]
    [InlineData(1, 0.5, -0.5)]
    [InlineData(2, -0.3, 0.7)]
    [InlineData(3, 0.1, 0.1)]
    [InlineData(4, -0.8, 0.2)]
    [InlineData(5, 0.6, -0.4)]
    public void XYZToFaceUV_RoundTrip_PreservesCoordinates(int face, double u, double v)
    {
        // Act - Convert to XYZ and back
        var (x, y, z) = S2Encoder.FaceUVToXYZ(face, u, v);
        
        // Determine which face the XYZ point is on
        var actualFace = S2Encoder.XYZToFace(x, y, z);
        
        // Convert XYZ back to UV coordinates on that face
        var (actualU, actualV) = S2Encoder.XYZToFaceUV(actualFace, x, y, z);
        
        // Assert
        actualFace.Should().Be(face);
        actualU.Should().BeApproximately(u, 1e-10);
        actualV.Should().BeApproximately(v, 1e-10);
    }

    /// <summary>
    /// Test that random coordinates round trip correctly through the full encoding/decoding pipeline.
    /// </summary>
    [Fact]
    public void FullPipeline_RandomCoordinates_RoundTripsWithinTolerance()
    {
        // Arrange
        const int iterations = 100;
        const double tolerance = 0.1; // 0.1 degree tolerance
        
        for (int i = 0; i < iterations; i++)
        {
            var lat = Random.NextDouble() * 180.0 - 90.0;  // -90 to 90
            var lon = Random.NextDouble() * 360.0 - 180.0; // -180 to 180
            
            // Act - Encode and decode
            var cellId = S2Encoder.Encode(lat, lon, 30);
            var (decodedLat, decodedLon) = S2Encoder.Decode(cellId);
            
            // Assert - Should be within tolerance
            var latError = Math.Abs(decodedLat - lat);
            var lonError = Math.Abs(decodedLon - lon);
            
            latError.Should().BeLessThan(tolerance, 
                $"Latitude error too large for ({lat}, {lon}): got ({decodedLat}, {decodedLon})");
            lonError.Should().BeLessThan(tolerance,
                $"Longitude error too large for ({lat}, {lon}): got ({decodedLat}, {decodedLon})");
        }
    }

    /// <summary>
    /// Test that IJToSTWithCorrection produces ST coordinates in the correct [-1, 1] range.
    /// This is a critical fix for task 14a.6b.
    /// Reference: s2-geometry-library-csharp/S2Geometry/S2CellId.cs line 1074-1075
    /// </summary>
    [Theory]
    [InlineData(0, 0, 30)]           // Corner at (0,0)
    [InlineData(536870912, 536870912, 30)]  // Center (MaxSize/2, MaxSize/2)
    [InlineData(1073741823, 1073741823, 30)] // Max corner (MaxSize-1, MaxSize-1)
    [InlineData(0, 0, 0)]            // Level 0 corner
    [InlineData(1, 1, 1)]            // Level 1 cell
    public void IJToSTWithCorrection_VariousIJCoordinates_ProducesSTInCorrectRange(int i, int j, int level)
    {
        // Arrange - Create a dummy cell ID for the correction calculation
        // The cell ID format is: [3 face bits][61 position bits]
        // For this test, we'll use face 0 and encode the IJ coordinates
        var cellId = S2Encoder.FaceIJToCellId(0, i, j, level);
        
        // Act - Call IJToSTWithCorrection
        var (s, t) = S2Encoder.IJToSTWithCorrection(i, j, level, cellId);
        
        // Assert - CRITICAL: ST coordinates must be in the range [-1, 1], not [0, 1]
        s.Should().BeInRange(-1.0, 1.0, 
            $"S coordinate must be in [-1, 1] range for i={i}, j={j}, level={level}");
        t.Should().BeInRange(-1.0, 1.0,
            $"T coordinate must be in [-1, 1] range for i={i}, j={j}, level={level}");
        
        // Additional validation: center of the cube face should be near (0, 0) in ST coordinates
        if (i == 536870912 && j == 536870912 && level == 30)
        {
            s.Should().BeApproximately(0.0, 0.01, "Center should be near s=0");
            t.Should().BeApproximately(0.0, 0.01, "Center should be near t=0");
        }
        
        // Corners should be near -1 or +1
        if (i == 0 && j == 0 && level == 30)
        {
            s.Should().BeApproximately(-1.0, 0.01, "Corner (0,0) should be near s=-1");
            t.Should().BeApproximately(-1.0, 0.01, "Corner (0,0) should be near t=-1");
        }
        
        if (i == 1073741823 && j == 1073741823 && level == 30)
        {
            s.Should().BeApproximately(1.0, 0.01, "Corner (max,max) should be near s=+1");
            t.Should().BeApproximately(1.0, 0.01, "Corner (max,max) should be near t=+1");
        }
    }
}
