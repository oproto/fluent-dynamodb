using Xunit;
using AwesomeAssertions;
using Google.Common.Geometry;

namespace Oproto.FluentDynamoDb.Geospatial.UnitTests.S2;

/// <summary>
/// Cross-validation tests that compare our S2 implementation against the reference S2Geometry library.
/// These tests provide high confidence that our implementation matches the reference behavior.
/// </summary>
public class S2CrossValidationTests
{
    private static readonly Random Random = new(42); // Fixed seed for reproducibility

    /// <summary>
    /// Generates random coordinates for testing.
    /// </summary>
    private static (double lat, double lon) GenerateRandomCoordinate()
    {
        var lat = (Random.NextDouble() * 180.0) - 90.0;  // -90 to 90
        var lon = (Random.NextDouble() * 360.0) - 180.0; // -180 to 180
        return (lat, lon);
    }

    [Fact]
    public void Encode_100RandomCoordinates_MatchesReferenceImplementation()
    {
        // Arrange & Act & Assert
        for (var i = 0; i < 100; i++)
        {
            var (lat, lon) = GenerateRandomCoordinate();
            var level = Random.Next(0, 31); // 0-30

            // Our implementation
            var ourToken = Geospatial.S2.S2Encoder.Encode(lat, lon, level);

            // Reference implementation
            var point = S2LatLng.FromDegrees(lat, lon).ToPoint();
            var refCellId = S2CellId.FromPoint(point).ParentForLevel(level);
            var refToken = refCellId.ToToken();

            // Compare
            ourToken.Should().Be(refToken, 
                $"Token mismatch for lat={lat}, lon={lon}, level={level}");
        }
    }

    [Theory]
    [InlineData(0)]
    [InlineData(5)]
    [InlineData(10)]
    [InlineData(15)]
    [InlineData(20)]
    [InlineData(25)]
    [InlineData(30)]
    public void Encode_VariousLevels_MatchesReference(int level)
    {
        // Test 50 random coordinates at each level
        for (var i = 0; i < 50; i++)
        {
            var (lat, lon) = GenerateRandomCoordinate();

            var ourToken = Geospatial.S2.S2Encoder.Encode(lat, lon, level);

            var point = S2LatLng.FromDegrees(lat, lon).ToPoint();
            var refCellId = S2CellId.FromPoint(point).ParentForLevel(level);
            var refToken = refCellId.ToToken();

            ourToken.Should().Be(refToken,
                $"Token mismatch at level {level} for lat={lat}, lon={lon}");
        }
    }

    [Fact]
    public void Decode_100RandomTokens_MatchesReferenceWithinTolerance()
    {
        // Arrange & Act & Assert
        for (var i = 0; i < 100; i++)
        {
            var (lat, lon) = GenerateRandomCoordinate();
            var level = Random.Next(5, 31); // 5-30 (avoid level 0 which has large cells)

            // Encode with reference
            var point = S2LatLng.FromDegrees(lat, lon).ToPoint();
            var refCellId = S2CellId.FromPoint(point).ParentForLevel(level);
            var token = refCellId.ToToken();

            // Decode with our implementation
            var (ourLat, ourLon) = Geospatial.S2.S2Encoder.Decode(token);

            // Decode with reference
            var refLatLng = refCellId.ToLatLng();
            var refLat = refLatLng.LatDegrees;
            var refLon = refLatLng.LngDegrees;

            // Compare - should be identical
            Math.Abs(ourLat - refLat).Should().BeLessThan(1e-10,
                $"Latitude mismatch for token {token}");
            Math.Abs(ourLon - refLon).Should().BeLessThan(1e-10,
                $"Longitude mismatch for token {token}");
        }
    }

    [Fact]
    public void DecodeBounds_100RandomCells_MatchesReference()
    {
        for (var i = 0; i < 100; i++)
        {
            var (lat, lon) = GenerateRandomCoordinate();
            var level = Random.Next(1, 31); // 1-30 (level 0 has special hardcoded bounds)

            // Encode
            var point = S2LatLng.FromDegrees(lat, lon).ToPoint();
            var refCellId = S2CellId.FromPoint(point).ParentForLevel(level);
            var token = refCellId.ToToken();

            // Get bounds from our implementation
            var (ourMinLat, ourMaxLat, ourMinLon, ourMaxLon) = 
                Geospatial.S2.S2Encoder.DecodeBounds(token);

            // Get bounds from reference
            var refCell = new S2Cell(refCellId);
            var refBounds = refCell.RectBound;

            // Compare bounds
            var tolerance = 1e-10;
            Math.Abs(ourMinLat - refBounds.LatLo.Degrees).Should().BeLessThan(tolerance,
                $"MinLat mismatch for token {token}");
            Math.Abs(ourMaxLat - refBounds.LatHi.Degrees).Should().BeLessThan(tolerance,
                $"MaxLat mismatch for token {token}");

            // Longitude comparison is trickier due to wrapping
            // For cells that don't wrap around the date line, compare directly
            if (!refBounds.Lng.IsInverted)
            {
                Math.Abs(ourMinLon - refBounds.LngLo.Degrees).Should().BeLessThan(tolerance,
                    $"MinLon mismatch for token {token}");
                Math.Abs(ourMaxLon - refBounds.LngHi.Degrees).Should().BeLessThan(tolerance,
                    $"MaxLon mismatch for token {token}");
            }
        }
    }

    [Fact]
    public void GetNeighbors_100RandomCells_MatchesReference()
    {
        for (var i = 0; i < 100; i++)
        {
            var (lat, lon) = GenerateRandomCoordinate();
            var level = Random.Next(1, 30); // 1-29 (avoid level 0 and 30 for neighbor tests)

            // Encode
            var point = S2LatLng.FromDegrees(lat, lon).ToPoint();
            var refCellId = S2CellId.FromPoint(point).ParentForLevel(level);
            var token = refCellId.ToToken();

            // Get neighbors from our implementation
            var ourNeighbors = Geospatial.S2.S2Encoder.GetNeighbors(token);

            // Get neighbors from reference (edge neighbors)
            var refNeighbors = refCellId.GetEdgeNeighbors();

            // Our implementation returns 8 neighbors (edge + corner)
            // Reference GetEdgeNeighbors returns 4 neighbors (just edges)
            // So we'll verify that the 4 edge neighbors from reference are in our set
            ourNeighbors.Should().HaveCount(8, "Our implementation returns 8 neighbors");

            foreach (var refNeighbor in refNeighbors)
            {
                var refToken = refNeighbor.ToToken();
                ourNeighbors.Should().Contain(refToken,
                    $"Our neighbors should include reference edge neighbor {refToken} for cell {token}");
            }
        }
    }

    [Fact]
    public void EncodeAndDecode_EdgeCases_MatchesReference()
    {
        var testCases = new[]
        {
            (0.0, 0.0, 10),           // Null Island
            (90.0, 0.0, 10),          // North Pole
            (-90.0, 0.0, 10),         // South Pole
            (0.0, 180.0, 10),         // Date line (east)
            (0.0, -180.0, 10),        // Date line (west)
            (37.7749, -122.4194, 16), // San Francisco
            (51.5074, -0.1278, 16),   // London
            (-33.8688, 151.2093, 16), // Sydney
            (35.6762, 139.6503, 16),  // Tokyo
        };

        foreach (var (lat, lon, level) in testCases)
        {
            // Our implementation
            var ourToken = Geospatial.S2.S2Encoder.Encode(lat, lon, level);
            var (ourDecodedLat, ourDecodedLon) = Geospatial.S2.S2Encoder.Decode(ourToken);

            // Reference implementation
            var point = S2LatLng.FromDegrees(lat, lon).ToPoint();
            var refCellId = S2CellId.FromPoint(point).ParentForLevel(level);
            var refToken = refCellId.ToToken();
            var refLatLng = refCellId.ToLatLng();

            // Compare tokens
            ourToken.Should().Be(refToken,
                $"Token mismatch for lat={lat}, lon={lon}, level={level}");

            // Compare decoded coordinates
            Math.Abs(ourDecodedLat - refLatLng.LatDegrees).Should().BeLessThan(1e-10,
                $"Decoded latitude mismatch for {lat}, {lon}");
            Math.Abs(ourDecodedLon - refLatLng.LngDegrees).Should().BeLessThan(1e-10,
                $"Decoded longitude mismatch for {lat}, {lon}");
        }
    }

    [Fact]
    public void Encode_AllLevels_ProducesSameTokensAsReference()
    {
        // Test a specific location at all levels
        var lat = 37.7749;
        var lon = -122.4194;

        for (var level = 0; level <= 30; level++)
        {
            var ourToken = Geospatial.S2.S2Encoder.Encode(lat, lon, level);

            var point = S2LatLng.FromDegrees(lat, lon).ToPoint();
            var refCellId = S2CellId.FromPoint(point).ParentForLevel(level);
            var refToken = refCellId.ToToken();

            ourToken.Should().Be(refToken,
                $"Token mismatch at level {level}");
        }
    }

    [Fact]
    public void RoundTrip_1000RandomCoordinates_BothImplementationsAgree()
    {
        // This is the ultimate test: encode with reference, decode with ours, and vice versa
        for (var i = 0; i < 1000; i++)
        {
            var (lat, lon) = GenerateRandomCoordinate();
            var level = Random.Next(5, 31); // 5-30

            // Encode with reference
            var point = S2LatLng.FromDegrees(lat, lon).ToPoint();
            var refCellId = S2CellId.FromPoint(point).ParentForLevel(level);
            var token = refCellId.ToToken();

            // Decode with our implementation
            var (ourLat, ourLon) = Geospatial.S2.S2Encoder.Decode(token);

            // Decode with reference
            var refLatLng = refCellId.ToLatLng();

            // Should be identical
            Math.Abs(ourLat - refLatLng.LatDegrees).Should().BeLessThan(1e-10,
                $"Latitude mismatch for iteration {i}");
            Math.Abs(ourLon - refLatLng.LngDegrees).Should().BeLessThan(1e-10,
                $"Longitude mismatch for iteration {i}");

            // Now encode with ours
            var ourToken = Geospatial.S2.S2Encoder.Encode(lat, lon, level);

            // Should produce the same token
            ourToken.Should().Be(token,
                $"Token mismatch for iteration {i}");
        }
    }
}
