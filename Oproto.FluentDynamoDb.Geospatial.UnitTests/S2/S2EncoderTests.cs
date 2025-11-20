using Oproto.FluentDynamoDb.Geospatial.S2;

namespace Oproto.FluentDynamoDb.Geospatial.UnitTests.S2;

/// <summary>
/// Unit tests for S2Encoder
/// </summary>
public class S2EncoderTests
{
    [Fact]
    public void Encode_SimpleLocation_ProducesToken()
    {
        // Arrange - A simple location
        var lat = 0.0;
        var lon = 0.0;
        var level = 10;

        // Act
        var token = S2Encoder.Encode(lat, lon, level);

        // Assert
        token.Should().NotBeNullOrEmpty();
        token.Length.Should().BeInRange(1, 16, "S2 tokens are variable-length with trailing zeros removed");
        token.Should().MatchRegex("^[0-9a-f]+$");
    }

    [Fact]
    public void EncodeAndDecode_NullIsland_ReturnsApproximateLocation()
    {
        // Arrange - Null Island (0, 0)
        var lat = 0.0;
        var lon = 0.0;
        var level = 10;

        // Act
        var token = S2Encoder.Encode(lat, lon, level);
        var (decodedLat, decodedLon) = S2Encoder.Decode(token);

        // Assert - Should be close to original (within cell size for level 10)
        // Level 10 has cells of about 100km, so ~1 degree tolerance
        Math.Abs(decodedLat - lat).Should().BeLessThan(2.0);
        Math.Abs(decodedLon - lon).Should().BeLessThan(2.0);
    }

    [Fact]
    public void EncodeAndDecode_Level0_BoundsContainOriginalPoint()
    {
        // Arrange - Level 0 case that's failing in property test
        var lat = 16.107682022255336;
        var lon = 171.1200295352824;
        var level = 0;

        // Act
        var token = S2Encoder.Encode(lat, lon, level);
        var (decodedLat, decodedLon) = S2Encoder.Decode(token);
        var (minLat, maxLat, minLon, maxLon) = S2Encoder.DecodeBounds(token);

        Console.WriteLine($"Original: lat={lat}, lon={lon}, level={level}");
        Console.WriteLine($"Token: {token}");
        Console.WriteLine($"Decoded: ({decodedLat}, {decodedLon})");
        Console.WriteLine($"Bounds: Lat[{minLat}, {maxLat}], Lon[{minLon}, {maxLon}]");

        // At level 0, there are only 6 cells (one per cube face)
        // The original point MUST be within the decoded bounds
        lat.Should().BeInRange(minLat, maxLat, "original latitude should be within decoded bounds");
        
        // For longitude, handle wrapping
        var lonInRange = (lon >= minLon && lon <= maxLon) ||
                        (minLon > maxLon && (lon >= minLon || lon <= maxLon));
        lonInRange.Should().BeTrue($"original longitude {lon} should be within decoded bounds [{minLon}, {maxLon}]");
    }

    [Fact]
    public void EncodeAndDecode_FailingCase_ReturnsApproximateLocation()
    {
        // Arrange - The failing case from property test
        var lat = 23.37641274385079;
        var lon = -86.23297555298419;
        var level = 26;

        // Act
        var token = S2Encoder.Encode(lat, lon, level);
        var (decodedLat, decodedLon) = S2Encoder.Decode(token);

        // Debug output
        Console.WriteLine($"Original: ({lat}, {lon})");
        Console.WriteLine($"Token: {token}");
        Console.WriteLine($"Decoded: ({decodedLat}, {decodedLon})");
        Console.WriteLine($"Diff: ({Math.Abs(decodedLat - lat)}, {Math.Abs(decodedLon - lon)})");

        // Assert - Level 26 should be very precise (~1 meter)
        Math.Abs(decodedLat - lat).Should().BeLessThan(0.001);
        Math.Abs(decodedLon - lon).Should().BeLessThan(0.001);
    }

    [Fact]
    public void EncodeAndDecode_PropertyTestFailingCase_ReturnsApproximateLocation()
    {
        // Arrange - The failing case from property test: lat=-61.235, lon=29.705, level=29
        var lat = -61.235349126186996;
        var lon = 29.70507413238329;
        var level = 29;

        // Act
        var token = S2Encoder.Encode(lat, lon, level);
        var (decodedLat, decodedLon) = S2Encoder.Decode(token);

        // Debug output
        Console.WriteLine($"Original: ({lat}, {lon})");
        Console.WriteLine($"Token: {token}");
        Console.WriteLine($"Decoded: ({decodedLat}, {decodedLon})");
        Console.WriteLine($"Lat Diff: {Math.Abs(decodedLat - lat)}");
        Console.WriteLine($"Lon Diff: {Math.Abs(decodedLon - lon)}");

        // Assert - Level 29 should be extremely precise (~0.5 meters)
        Math.Abs(decodedLat - lat).Should().BeLessThan(0.001);
        Math.Abs(decodedLon - lon).Should().BeLessThan(0.001);
    }

    [Theory]
    [InlineData(0.0, 0.0, 10)]
    [InlineData(37.7749, -122.4194, 10)]  // San Francisco
    [InlineData(51.5074, -0.1278, 10)]    // London
    [InlineData(-33.8688, 151.2093, 10)]  // Sydney
    public void EncodeAndDecode_KnownLocations_ReturnsApproximateLocation(
        double lat, double lon, int level)
    {
        // Act
        var token = S2Encoder.Encode(lat, lon, level);
        var (decodedLat, decodedLon) = S2Encoder.Decode(token);

        // Assert - Level 10 has cells of about 100km, so ~1 degree tolerance
        Math.Abs(decodedLat - lat).Should().BeLessThan(2.0);
        Math.Abs(decodedLon - lon).Should().BeLessThan(2.0);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(10)]
    [InlineData(20)]
    [InlineData(30)]
    public void Encode_WithValidLevel_ReturnsToken(int level)
    {
        // Arrange
        var lat = 37.7749;
        var lon = -122.4194;

        // Act
        var token = S2Encoder.Encode(lat, lon, level);

        // Assert
        token.Should().NotBeNullOrEmpty();
        token.Length.Should().BeInRange(1, 16, "S2 tokens are variable-length with trailing zeros removed");
        token.Should().MatchRegex("^[0-9a-f]+$");
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(31)]
    [InlineData(100)]
    public void Encode_WithInvalidLevel_ThrowsArgumentOutOfRangeException(int level)
    {
        // Arrange
        var lat = 37.7749;
        var lon = -122.4194;

        // Act & Assert
        var exception = Assert.Throws<ArgumentOutOfRangeException>(
            () => S2Encoder.Encode(lat, lon, level));
        exception.ParamName.Should().Be("level");
        exception.Message.Should().Contain("S2 level must be between 0 and 30");
    }

    [Fact]
    public void Decode_WithNullToken_ThrowsArgumentException()
    {
        // Act & Assert
        var exception = Assert.Throws<ArgumentException>(() => S2Encoder.Decode(null!));
        exception.ParamName.Should().Be("s2Token");
        exception.Message.Should().Contain("S2 token cannot be null or empty");
    }

    [Fact]
    public void Decode_WithEmptyToken_ThrowsArgumentException()
    {
        // Act & Assert
        var exception = Assert.Throws<ArgumentException>(() => S2Encoder.Decode(string.Empty));
        exception.ParamName.Should().Be("s2Token");
        exception.Message.Should().Contain("S2 token cannot be null or empty");
    }

    [Fact]
    public void Decode_WithInvalidToken_ThrowsArgumentException()
    {
        // Act & Assert
        var exception = Assert.Throws<ArgumentException>(() => S2Encoder.Decode("invalid"));
        exception.ParamName.Should().Be("s2Token");
        exception.Message.Should().Contain("Invalid S2 token");
    }

    [Fact]
    public void DecodeBounds_ReturnsValidBoundingBox()
    {
        // Arrange
        var lat = 37.7749;
        var lon = -122.4194;
        var level = 10;
        var token = S2Encoder.Encode(lat, lon, level);

        // Act
        var (minLat, maxLat, minLon, maxLon) = S2Encoder.DecodeBounds(token);

        // Assert
        minLat.Should().BeLessThan(maxLat);
        minLon.Should().BeLessThan(maxLon);
        minLat.Should().BeInRange(-90, 90);
        maxLat.Should().BeInRange(-90, 90);
        minLon.Should().BeInRange(-180, 180);
        maxLon.Should().BeInRange(-180, 180);
    }

    [Fact]
    public void Debug_SanFrancisco_Level0_CheckFace()
    {
        // Arrange - San Francisco
        var lat = 37.7749;
        var lon = -122.4194;
        var level = 0;

        // Act
        var token = S2Encoder.Encode(lat, lon, level);
        var (decodedLat, decodedLon) = S2Encoder.Decode(token);
        var (minLat, maxLat, minLon, maxLon) = S2Encoder.DecodeBounds(token);

        // Debug output
        Console.WriteLine($"Original: lat={lat}, lon={lon}");
        Console.WriteLine($"Token: {token}");
        Console.WriteLine($"Face: {Convert.ToUInt64(token, 16) >> 61}");
        Console.WriteLine($"Decoded: ({decodedLat}, {decodedLon})");
        Console.WriteLine($"Bounds: Lat[{minLat}, {maxLat}], Lon[{minLon}, {maxLon}]");
        
        // At level 0, the 6 faces have specific bounds
        // Face 0 (+X): lat[-45, 45], lon[-45, 45]
        // Face 1 (+Y): lat[-45, 45], lon[45, 135]
        // Face 2 (+Z): lat[35.26, 90], lon[-180, 180]
        // Face 3 (-X): lat[-45, 45], lon[135, -135] (wraps)
        // Face 4 (-Y): lat[-45, 45], lon[-135, -45]
        // Face 5 (-Z): lat[-90, -35.26], lon[-180, 180]
    }

    [Fact]
    public void GetNeighbors_ReturnsEightNeighbors()
    {
        // Arrange
        var lat = 37.7749;
        var lon = -122.4194;
        var level = 10;
        var token = S2Encoder.Encode(lat, lon, level);

        // Act
        var neighbors = S2Encoder.GetNeighbors(token);

        // Assert
        neighbors.Length.Should().Be(8);
        foreach (var n in neighbors)
        {
            n.Should().NotBeNullOrEmpty();
            n.Length.Should().BeInRange(1, 16, "S2 tokens are variable-length with trailing zeros removed");
            n.Should().MatchRegex("^[0-9a-f]+$");
        }
    }

    [Fact]
    public void GetNeighbors_AllNeighborsAreDifferent()
    {
        // Arrange
        var lat = 37.7749;
        var lon = -122.4194;
        var level = 10;
        var token = S2Encoder.Encode(lat, lon, level);

        // Act
        var neighbors = S2Encoder.GetNeighbors(token);

        // Assert
        neighbors.Distinct().Count().Should().Be(8);
        foreach (var n in neighbors)
        {
            n.Should().NotBe(token);
        }
    }

    [Fact]
    public void GetNeighbors_WithNullToken_ThrowsArgumentException()
    {
        // Act & Assert
        var exception = Assert.Throws<ArgumentException>(() => S2Encoder.GetNeighbors(null!));
        exception.ParamName.Should().Be("s2Token");
        exception.Message.Should().Contain("S2 token cannot be null or empty");
    }

    [Theory]
    [InlineData(37.7749, -122.4194, 10)]  // San Francisco, level 10
    [InlineData(37.7749, -122.4194, 15)]  // San Francisco, level 15
    [InlineData(37.7749, -122.4194, 20)]  // San Francisco, level 20
    [InlineData(0.0, 0.0, 10)]            // Equator/Prime Meridian
    [InlineData(51.5074, -0.1278, 10)]    // London
    [InlineData(-33.8688, 151.2093, 10)]  // Sydney
    public void GetNeighbors_AtVariousLevels_AllNeighborsAtSameLevel(
        double lat, double lon, int level)
    {
        // Arrange
        var token = S2Encoder.Encode(lat, lon, level);

        // Act
        var neighbors = S2Encoder.GetNeighbors(token);

        // Assert
        neighbors.Should().HaveCount(8);
        
        // Verify all neighbors are at the same level by checking their bounds
        // Neighbors at the same level should have similar-sized bounds
        var originalBounds = S2Encoder.DecodeBounds(token);
        var originalLatSpan = originalBounds.MaxLat - originalBounds.MinLat;
        
        foreach (var neighborToken in neighbors)
        {
            var neighborBounds = S2Encoder.DecodeBounds(neighborToken);
            var neighborLatSpan = neighborBounds.MaxLat - neighborBounds.MinLat;
            
            // Neighbors at the same level should have similar cell sizes
            // Due to S2's spherical projection, cells can vary slightly in size
            // Use a tolerance of 1% of the cell size
            var tolerance = originalLatSpan * 0.01;
            Math.Abs(neighborLatSpan - originalLatSpan).Should().BeLessThan(tolerance, 
                $"neighbor {neighborToken} should have similar bounds to original cell at level {level}");
        }
    }

    [Theory]
    [InlineData(0.0, 179.9, 10)]    // Near date line (east side)
    [InlineData(0.0, -179.9, 10)]   // Near date line (west side)
    [InlineData(89.9, 0.0, 10)]     // Near North Pole
    [InlineData(-89.9, 0.0, 10)]    // Near South Pole
    public void GetNeighbors_NearFaceBoundaries_AllNeighborsDistinct(
        double lat, double lon, int level)
    {
        // Arrange
        var token = S2Encoder.Encode(lat, lon, level);

        // Act
        var neighbors = S2Encoder.GetNeighbors(token);

        // Assert
        neighbors.Should().HaveCount(8);
        neighbors.Distinct().Should().HaveCount(8, "all neighbors should be distinct");
        
        // Verify none of the neighbors are the original cell
        foreach (var neighbor in neighbors)
        {
            neighbor.Should().NotBe(token, "neighbor should not be the original cell");
        }
    }

    [Fact]
    public void GetNeighbors_Level0Cell_ReturnsNeighbors()
    {
        // Arrange - Level 0 cells are entire cube faces
        // At level 0, cells are so large that some neighbor offsets wrap to the same face
        // This is expected behavior - level 0 cells have 4 edge neighbors (one per edge)
        var lat = 0.0;
        var lon = 0.0;
        var level = 0;
        var token = S2Encoder.Encode(lat, lon, level);

        // Act
        var neighbors = S2Encoder.GetNeighbors(token);

        // Assert
        neighbors.Should().HaveCount(8, "GetNeighbors always returns 8 neighbors");
        
        // At level 0, multiple offsets can lead to the same neighboring face
        // The distinct count will be 4 (the 4 edge neighbors of the cube face)
        var distinctNeighbors = neighbors.Distinct().ToList();
        distinctNeighbors.Should().HaveCountGreaterThanOrEqualTo(4, "level 0 cells have at least 4 distinct neighbors");
        
        // Verify none of the neighbors are the original cell
        foreach (var neighbor in distinctNeighbors)
        {
            neighbor.Should().NotBe(token, "neighbor should not be the original cell");
        }
    }

    [Fact]
    public void GetNeighbors_MaxLevel_AllNeighborsDistinct()
    {
        // Arrange - Level 30 is the maximum level (leaf cells)
        var lat = 37.7749;
        var lon = -122.4194;
        var level = 30;
        var token = S2Encoder.Encode(lat, lon, level);

        // Act
        var neighbors = S2Encoder.GetNeighbors(token);

        // Assert
        neighbors.Should().HaveCount(8);
        neighbors.Distinct().Should().HaveCount(8, "all neighbors should be distinct even at max level");
    }

    // ===== Pole Handling Tests =====
    // At poles (±90° latitude), longitude is mathematically undefined, but S2 cells
    // still have structure. Different longitudes at the pole map to different S2 cells.
    // These tests verify that pole encoding/decoding works correctly.

    [Theory]
    [InlineData(90.0, 0.0, 10)]      // North Pole, longitude 0
    [InlineData(90.0, 45.0, 10)]     // North Pole, longitude 45
    [InlineData(90.0, 90.0, 10)]     // North Pole, longitude 90
    [InlineData(90.0, 180.0, 10)]    // North Pole, longitude 180
    [InlineData(90.0, -90.0, 10)]    // North Pole, longitude -90
    [InlineData(90.0, -180.0, 10)]   // North Pole, longitude -180
    [InlineData(-90.0, 0.0, 10)]     // South Pole, longitude 0
    [InlineData(-90.0, 45.0, 10)]    // South Pole, longitude 45
    [InlineData(-90.0, 90.0, 10)]    // South Pole, longitude 90
    [InlineData(-90.0, 180.0, 10)]   // South Pole, longitude 180
    [InlineData(-90.0, -90.0, 10)]   // South Pole, longitude -90
    [InlineData(-90.0, -180.0, 10)]  // South Pole, longitude -180
    public void EncodeAndDecode_Poles_ReturnsValidCoordinates(
        double lat, double lon, int level)
    {
        // Act
        var token = S2Encoder.Encode(lat, lon, level);
        var (decodedLat, decodedLon) = S2Encoder.Decode(token);

        // Assert
        // Decoded coordinates should be valid
        decodedLat.Should().BeInRange(-90, 90);
        decodedLon.Should().BeInRange(-180, 180);
        
        // Latitude should be close to the pole (within cell size for level 10)
        // Level 10 has cells of about 100km, which is ~1 degree at the poles
        Math.Abs(decodedLat - lat).Should().BeLessThan(5.0, 
            $"Decoded latitude should be close to pole");
    }

    [Theory]
    [InlineData(90.0, 0.0, 0)]       // North Pole, level 0
    [InlineData(90.0, 45.0, 5)]      // North Pole, level 5
    [InlineData(90.0, 90.0, 15)]     // North Pole, level 15
    [InlineData(90.0, 180.0, 20)]    // North Pole, level 20
    [InlineData(90.0, -90.0, 25)]    // North Pole, level 25
    [InlineData(90.0, -180.0, 30)]   // North Pole, level 30
    [InlineData(-90.0, 0.0, 0)]      // South Pole, level 0
    [InlineData(-90.0, 45.0, 5)]     // South Pole, level 5
    [InlineData(-90.0, 90.0, 15)]    // South Pole, level 15
    [InlineData(-90.0, 180.0, 20)]   // South Pole, level 20
    [InlineData(-90.0, -90.0, 25)]   // South Pole, level 25
    [InlineData(-90.0, -180.0, 30)]  // South Pole, level 30
    public void EncodeAndDecode_PolesAtVariousLevels_ReturnsValidCoordinates(
        double lat, double lon, int level)
    {
        // Act
        var token = S2Encoder.Encode(lat, lon, level);
        var (decodedLat, decodedLon) = S2Encoder.Decode(token);

        // Assert
        // Decoded coordinates should be valid
        decodedLat.Should().BeInRange(-90, 90);
        decodedLon.Should().BeInRange(-180, 180);
        
        // Latitude should be close to the pole (within tolerance for the level)
        var latTolerance = level switch
        {
            <= 5 => 20.0,   // Very large cells
            <= 10 => 5.0,   // Large cells
            <= 15 => 1.0,   // Medium cells
            <= 20 => 0.1,   // Small cells
            <= 25 => 0.01,  // Very small cells
            _ => 0.001      // Tiny cells
        };
        
        Math.Abs(decodedLat - lat).Should().BeLessThan(latTolerance, 
            $"Decoded latitude should be close to pole for level {level}");
    }

    [Fact]
    public void DecodeBounds_NorthPole_ReturnsValidBounds()
    {
        // Arrange
        var lat = 90.0;
        var lon = 0.0;
        var level = 10;
        var token = S2Encoder.Encode(lat, lon, level);

        // Act
        var (minLat, maxLat, minLon, maxLon) = S2Encoder.DecodeBounds(token);

        // Assert
        minLat.Should().BeLessThanOrEqualTo(maxLat);
        minLon.Should().BeLessThanOrEqualTo(maxLon);
        minLat.Should().BeInRange(-90, 90);
        maxLat.Should().BeInRange(-90, 90);
        minLon.Should().BeInRange(-180, 180);
        maxLon.Should().BeInRange(-180, 180);
        
        // The original point should be within the bounds
        lat.Should().BeInRange(minLat, maxLat);
    }

    [Fact]
    public void DecodeBounds_SouthPole_ReturnsValidBounds()
    {
        // Arrange
        var lat = -90.0;
        var lon = 0.0;
        var level = 10;
        var token = S2Encoder.Encode(lat, lon, level);

        // Act
        var (minLat, maxLat, minLon, maxLon) = S2Encoder.DecodeBounds(token);

        // Assert
        minLat.Should().BeLessThanOrEqualTo(maxLat);
        minLon.Should().BeLessThanOrEqualTo(maxLon);
        minLat.Should().BeInRange(-90, 90);
        maxLat.Should().BeInRange(-90, 90);
        minLon.Should().BeInRange(-180, 180);
        maxLon.Should().BeInRange(-180, 180);
        
        // The original point should be within the bounds
        lat.Should().BeInRange(minLat, maxLat);
    }

    // ===== Comprehensive Bounds Tests at Various Levels =====
    // Task 14a.8: Add comprehensive unit tests for bounds at levels 0, 5, 10, 15, 20, 30

    [Theory]
    [InlineData(0)]
    [InlineData(5)]
    [InlineData(10)]
    [InlineData(15)]
    [InlineData(20)]
    [InlineData(30)]
    public void DecodeBounds_VariousLevels_OriginalPointWithinBounds(int level)
    {
        // Arrange - Test a variety of locations
        var testLocations = new[]
        {
            (lat: 0.0, lon: 0.0),           // Null Island
            (lat: 37.7749, lon: -122.4194), // San Francisco
            (lat: 51.5074, lon: -0.1278),   // London
            (lat: -33.8688, lon: 151.2093), // Sydney
            (lat: 35.6762, lon: 139.6503),  // Tokyo
        };

        foreach (var (lat, lon) in testLocations)
        {
            // Act
            var token = S2Encoder.Encode(lat, lon, level);
            var (minLat, maxLat, minLon, maxLon) = S2Encoder.DecodeBounds(token);

            // Assert
            lat.Should().BeInRange(minLat, maxLat, 
                $"original latitude {lat} should be within decoded bounds at level {level}");
            
            // For longitude, handle potential wrapping at date line
            var lonInRange = (lon >= minLon && lon <= maxLon) ||
                            (minLon > maxLon && (lon >= minLon || lon <= maxLon));
            lonInRange.Should().BeTrue(
                $"original longitude {lon} should be within decoded bounds [{minLon}, {maxLon}] at level {level}");
        }
    }

    [Theory]
    [InlineData(0.0, 0.0, 0)]           // Equator, level 0
    [InlineData(0.0, 0.0, 5)]           // Equator, level 5
    [InlineData(0.0, 0.0, 10)]          // Equator, level 10
    [InlineData(0.0, 0.0, 15)]          // Equator, level 15
    [InlineData(0.0, 0.0, 20)]          // Equator, level 20
    [InlineData(0.0, 0.0, 30)]          // Equator, level 30
    [InlineData(0.0, 90.0, 10)]         // Equator, 90° longitude
    [InlineData(0.0, 180.0, 10)]        // Equator, date line
    [InlineData(0.0, -180.0, 10)]       // Equator, date line (negative)
    public void DecodeBounds_Equator_OriginalPointWithinBounds(double lat, double lon, int level)
    {
        // Act
        var token = S2Encoder.Encode(lat, lon, level);
        var (minLat, maxLat, minLon, maxLon) = S2Encoder.DecodeBounds(token);

        // Assert
        lat.Should().BeInRange(minLat, maxLat, 
            $"original latitude {lat} should be within decoded bounds at level {level}");
        
        // For longitude, handle potential wrapping at date line
        var lonInRange = (lon >= minLon && lon <= maxLon) ||
                        (minLon > maxLon && (lon >= minLon || lon <= maxLon));
        lonInRange.Should().BeTrue(
            $"original longitude {lon} should be within decoded bounds [{minLon}, {maxLon}] at level {level}");
    }

    [Theory]
    [InlineData(0.0, 180.0, 0)]         // Date line, level 0
    [InlineData(0.0, 180.0, 5)]         // Date line, level 5
    [InlineData(0.0, 180.0, 10)]        // Date line, level 10
    [InlineData(0.0, 180.0, 15)]        // Date line, level 15
    [InlineData(0.0, 180.0, 20)]        // Date line, level 20
    [InlineData(0.0, 180.0, 30)]        // Date line, level 30
    [InlineData(45.0, 180.0, 10)]       // Date line, 45° latitude
    [InlineData(-45.0, 180.0, 10)]      // Date line, -45° latitude
    [InlineData(0.0, -180.0, 10)]       // Date line (negative)
    public void DecodeBounds_DateLine_OriginalPointWithinBounds(double lat, double lon, int level)
    {
        // Act
        var token = S2Encoder.Encode(lat, lon, level);
        var (minLat, maxLat, minLon, maxLon) = S2Encoder.DecodeBounds(token);

        // Assert
        lat.Should().BeInRange(minLat, maxLat, 
            $"original latitude {lat} should be within decoded bounds at level {level}");
        
        // For longitude, handle potential wrapping at date line
        var lonInRange = (lon >= minLon && lon <= maxLon) ||
                        (minLon > maxLon && (lon >= minLon || lon <= maxLon));
        lonInRange.Should().BeTrue(
            $"original longitude {lon} should be within decoded bounds [{minLon}, {maxLon}] at level {level}");
    }

    [Theory]
    [InlineData(90.0, 0.0, 0)]          // North Pole, level 0
    [InlineData(90.0, 0.0, 5)]          // North Pole, level 5
    [InlineData(90.0, 0.0, 10)]         // North Pole, level 10
    [InlineData(90.0, 0.0, 15)]         // North Pole, level 15
    [InlineData(90.0, 0.0, 20)]         // North Pole, level 20
    [InlineData(90.0, 0.0, 30)]         // North Pole, level 30
    [InlineData(-90.0, 0.0, 0)]         // South Pole, level 0
    [InlineData(-90.0, 0.0, 5)]         // South Pole, level 5
    [InlineData(-90.0, 0.0, 10)]        // South Pole, level 10
    [InlineData(-90.0, 0.0, 15)]        // South Pole, level 15
    [InlineData(-90.0, 0.0, 20)]        // South Pole, level 20
    [InlineData(-90.0, 0.0, 30)]        // South Pole, level 30
    public void DecodeBounds_Poles_OriginalPointWithinBounds(double lat, double lon, int level)
    {
        // Act
        var token = S2Encoder.Encode(lat, lon, level);
        var (minLat, maxLat, minLon, maxLon) = S2Encoder.DecodeBounds(token);

        // Assert
        lat.Should().BeInRange(minLat, maxLat, 
            $"original latitude {lat} should be within decoded bounds at level {level}");
        
        // For longitude at poles, the bounds may span the entire longitude range
        // So we just verify the bounds are valid
        minLat.Should().BeInRange(-90, 90);
        maxLat.Should().BeInRange(-90, 90);
        minLon.Should().BeInRange(-180, 180);
        maxLon.Should().BeInRange(-180, 180);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(5)]
    [InlineData(10)]
    [InlineData(15)]
    [InlineData(20)]
    [InlineData(30)]
    public void DecodeBounds_FaceBoundaries_OriginalPointWithinBounds(int level)
    {
        // Test points near face boundaries (where cube faces meet)
        // These are at ±45° latitude and longitude multiples of 90°
        var faceBoundaryLocations = new[]
        {
            (lat: 45.0, lon: 0.0),
            (lat: 45.0, lon: 90.0),
            (lat: 45.0, lon: 180.0),
            (lat: 45.0, lon: -90.0),
            (lat: -45.0, lon: 0.0),
            (lat: -45.0, lon: 90.0),
            (lat: -45.0, lon: 180.0),
            (lat: -45.0, lon: -90.0),
        };

        foreach (var (lat, lon) in faceBoundaryLocations)
        {
            // Act
            var token = S2Encoder.Encode(lat, lon, level);
            var (minLat, maxLat, minLon, maxLon) = S2Encoder.DecodeBounds(token);

            // Assert
            lat.Should().BeInRange(minLat, maxLat, 
                $"original latitude {lat} should be within decoded bounds at level {level}");
            
            // For longitude, handle potential wrapping at date line
            var lonInRange = (lon >= minLon && lon <= maxLon) ||
                            (minLon > maxLon && (lon >= minLon || lon <= maxLon));
            lonInRange.Should().BeTrue(
                $"original longitude {lon} should be within decoded bounds [{minLon}, {maxLon}] at level {level}");
        }
    }

    [Theory]
    [InlineData(0)]
    [InlineData(5)]
    [InlineData(10)]
    [InlineData(15)]
    [InlineData(20)]
    [InlineData(30)]
    public void DecodeBounds_AllLevels_BoundsAreValid(int level)
    {
        // Arrange
        var lat = 37.7749;
        var lon = -122.4194;
        var token = S2Encoder.Encode(lat, lon, level);

        // Act
        var (minLat, maxLat, minLon, maxLon) = S2Encoder.DecodeBounds(token);

        // Assert - Bounds should be valid
        minLat.Should().BeLessThanOrEqualTo(maxLat, "minLat should be <= maxLat");
        minLat.Should().BeInRange(-90, 90, "minLat should be valid");
        maxLat.Should().BeInRange(-90, 90, "maxLat should be valid");
        minLon.Should().BeInRange(-180, 180, "minLon should be valid");
        maxLon.Should().BeInRange(-180, 180, "maxLon should be valid");
        
        // Original point should be within bounds
        lat.Should().BeInRange(minLat, maxLat, "original point should be within bounds");
    }
}
