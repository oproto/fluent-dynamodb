using Oproto.FluentDynamoDb.Geospatial.GeoHash;

namespace Oproto.FluentDynamoDb.Geospatial.UnitTests.GeoHash;

public class GeoHashEncoderTests
{
    // Known test vectors for validation
    private static readonly (double Lat, double Lon, string Hash5, string Hash6, string Hash7)[] KnownVectors =
    {
        (37.7749, -122.4194, "9q8yy", "9q8yy9", "9q8yy9r"),  // San Francisco
        (40.7128, -74.0060, "dr5ru", "dr5ru7", "dr5ru7v"),   // New York
        (51.5074, -0.1278, "gcpvj", "gcpvj0", "gcpvj03"),    // London
        (35.6762, 139.6503, "xn76g", "xn76gk", "xn76gkh"),   // Tokyo
        (-33.8688, 151.2093, "r3gx2", "r3gx2f", "r3gx2f8"),  // Sydney
        (0.0, 0.0, "s0000", "s00000", "s000000"),            // Null Island
    };

    [Theory]
    [InlineData(37.7749, -122.4194, 5, "9q8yy")]
    [InlineData(37.7749, -122.4194, 6, "9q8yyk")]
    [InlineData(37.7749, -122.4194, 7, "9q8yyk8")]
    [InlineData(40.7128, -74.0060, 5, "dr5re")]
    [InlineData(40.7128, -74.0060, 6, "dr5reg")]
    [InlineData(51.5074, -0.1278, 5, "gcpvj")]
    [InlineData(51.5074, -0.1278, 6, "gcpvj0")]
    [InlineData(35.6762, 139.6503, 5, "xn76c")]
    [InlineData(35.6762, 139.6503, 6, "xn76cy")]
    [InlineData(-33.8688, 151.2093, 5, "r3gx2")]
    [InlineData(-33.8688, 151.2093, 6, "r3gx2f")]
    public void Encode_WithKnownTestVectors_ReturnsExpectedGeoHash(
        double lat, double lon, int precision, string expectedHash)
    {
        // Act
        var hash = GeoHashEncoder.Encode(lat, lon, precision);

        // Assert
        hash.Should().Be(expectedHash);
    }

    [Fact]
    public void Encode_NullIsland_ReturnsExpectedGeoHash()
    {
        // Arrange - Null Island (0, 0)
        var lat = 0.0;
        var lon = 0.0;

        // Act
        var hash = GeoHashEncoder.Encode(lat, lon, 6);

        // Assert
        hash.Should().Be("s00000");
    }

    [Fact]
    public void Encode_NorthPole_ReturnsValidGeoHash()
    {
        // Arrange
        var lat = 90.0;
        var lon = 0.0;

        // Act
        var hash = GeoHashEncoder.Encode(lat, lon, 6);

        // Assert
        hash.Should().NotBeNullOrEmpty();
        hash.Length.Should().Be(6);
        // North pole should start with 'u' or higher in base32
        hash[0].Should().BeOneOf('u', 'v', 'w', 'x', 'y', 'z');
    }

    [Fact]
    public void Encode_SouthPole_ReturnsValidGeoHash()
    {
        // Arrange
        var lat = -90.0;
        var lon = 0.0;

        // Act
        var hash = GeoHashEncoder.Encode(lat, lon, 6);

        // Assert
        hash.Should().NotBeNullOrEmpty();
        hash.Length.Should().Be(6);
        // South pole should start with '0' or low values in base32
        hash[0].Should().BeOneOf('0', '1', '2', '3', '4', '5', 'h', 'j', 'n', 'p');
    }

    [Fact]
    public void Encode_DateLineWest_ReturnsValidGeoHash()
    {
        // Arrange
        var lat = 0.0;
        var lon = -180.0;

        // Act
        var hash = GeoHashEncoder.Encode(lat, lon, 6);

        // Assert
        hash.Should().NotBeNullOrEmpty();
        hash.Length.Should().Be(6);
    }

    [Fact]
    public void Encode_DateLineEast_ReturnsValidGeoHash()
    {
        // Arrange
        var lat = 0.0;
        var lon = 180.0;

        // Act
        var hash = GeoHashEncoder.Encode(lat, lon, 6);

        // Assert
        hash.Should().NotBeNullOrEmpty();
        hash.Length.Should().Be(6);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(6)]
    [InlineData(10)]
    [InlineData(12)]
    public void Encode_WithValidPrecision_ReturnsCorrectLength(int precision)
    {
        // Arrange
        var lat = 37.7749;
        var lon = -122.4194;

        // Act
        var hash = GeoHashEncoder.Encode(lat, lon, precision);

        // Assert
        hash.Length.Should().Be(precision);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(13)]
    [InlineData(100)]
    public void Encode_WithInvalidPrecision_ThrowsArgumentOutOfRangeException(int precision)
    {
        // Arrange
        var lat = 37.7749;
        var lon = -122.4194;

        // Act & Assert
        var exception = Assert.Throws<ArgumentOutOfRangeException>(
            () => GeoHashEncoder.Encode(lat, lon, precision));
        exception.ParamName.Should().Be("precision");
        exception.Message.Should().Contain("Precision must be between 1 and 12");
    }

    [Theory]
    [InlineData("9q8yy", 37.7749, -122.4194)]
    [InlineData("dr5ru", 40.7128, -74.0060)]
    [InlineData("gcpvj", 51.5074, -0.1278)]
    [InlineData("xn76g", 35.6762, 139.6503)]
    [InlineData("r3gx2", -33.8688, 151.2093)]
    public void Decode_WithKnownGeoHash_ReturnsApproximateLocation(
        string geohash, double expectedLat, double expectedLon)
    {
        // Act
        var (lat, lon) = GeoHashEncoder.Decode(geohash);

        // Assert - Precision 5 has ~2.4km accuracy (~0.02 degrees tolerance)
        // Use larger tolerance of 0.1 degrees (~11km) to account for cell center vs original point
        lat.Should().BeApproximately(expectedLat, 0.1, "decoded latitude should be within precision bounds");
        lon.Should().BeApproximately(expectedLon, 0.1, "decoded longitude should be within precision bounds");
    }

    [Fact]
    public void Decode_NullIsland_ReturnsCorrectLocation()
    {
        // Arrange
        var geohash = "s00000";

        // Act
        var (lat, lon) = GeoHashEncoder.Decode(geohash);

        // Assert - Precision 6 has ~0.61km accuracy (~0.006 degrees tolerance)
        lat.Should().BeApproximately(0.0, 0.01, "decoded latitude should be near 0");
        lon.Should().BeApproximately(0.0, 0.01, "decoded longitude should be near 0");
    }

    [Fact]
    public void Decode_WithNullGeoHash_ThrowsArgumentException()
    {
        // Act & Assert
        var exception = Assert.Throws<ArgumentException>(() => GeoHashEncoder.Decode(null!));
        exception.ParamName.Should().Be("geohash");
        exception.Message.Should().Contain("GeoHash string cannot be null or empty");
    }

    [Fact]
    public void Decode_WithEmptyGeoHash_ThrowsArgumentException()
    {
        // Act & Assert
        var exception = Assert.Throws<ArgumentException>(() => GeoHashEncoder.Decode(string.Empty));
        exception.ParamName.Should().Be("geohash");
        exception.Message.Should().Contain("GeoHash string cannot be null or empty");
    }

    [Theory]
    [InlineData("9q8yy!")]  // Invalid character !
    [InlineData("9q8yya")]  // Invalid character a
    [InlineData("9q8yyi")]  // Invalid character i
    [InlineData("9q8yyl")]  // Invalid character l
    [InlineData("9q8yyo")]  // Invalid character o
    [InlineData("INVALID")]  // All invalid characters
    public void Decode_WithInvalidCharacters_ThrowsArgumentException(string invalidHash)
    {
        // Act & Assert
        var exception = Assert.Throws<ArgumentException>(() => GeoHashEncoder.Decode(invalidHash));
        exception.ParamName.Should().Be("geohash");
        exception.Message.Should().Contain("Invalid character");
    }

    [Theory]
    [InlineData(37.7749, -122.4194, 5)]
    [InlineData(37.7749, -122.4194, 6)]
    [InlineData(37.7749, -122.4194, 7)]
    [InlineData(40.7128, -74.0060, 6)]
    [InlineData(51.5074, -0.1278, 6)]
    [InlineData(35.6762, 139.6503, 6)]
    [InlineData(-33.8688, 151.2093, 6)]
    [InlineData(0.0, 0.0, 6)]
    public void EncodeAndDecode_RoundTrip_ReturnsApproximateLocation(
        double originalLat, double originalLon, int precision)
    {
        // Act
        var hash = GeoHashEncoder.Encode(originalLat, originalLon, precision);
        var (decodedLat, decodedLon) = GeoHashEncoder.Decode(hash);

        // Assert - Decoded location should be within the precision bounds
        // Precision 6 has ~0.61km accuracy
        var tolerance = precision switch
        {
            <= 4 => 1.0,    // ~20km accuracy
            5 => 0.1,       // ~2.4km accuracy
            6 => 0.01,      // ~0.61km accuracy
            7 => 0.005,     // ~76m accuracy
            _ => 0.001      // Higher precision
        };

        // Decoded location should be within the precision bounds
        Math.Abs(decodedLat - originalLat).Should().BeLessThan(tolerance);
        Math.Abs(decodedLon - originalLon).Should().BeLessThan(tolerance);
    }

    [Fact]
    public void DecodeBounds_ReturnsValidBoundingBox()
    {
        // Arrange
        var geohash = "9q8yy9";

        // Act
        var (minLat, maxLat, minLon, maxLon) = GeoHashEncoder.DecodeBounds(geohash);

        // Assert
        minLat.Should().BeLessThan(maxLat);
        minLon.Should().BeLessThan(maxLon);
        minLat.Should().BeInRange(-90, 90);
        maxLat.Should().BeInRange(-90, 90);
        minLon.Should().BeInRange(-180, 180);
        maxLon.Should().BeInRange(-180, 180);
    }

    [Fact]
    public void DecodeBounds_CenterPointIsWithinBounds()
    {
        // Arrange
        var geohash = "9q8yy9";

        // Act
        var (minLat, maxLat, minLon, maxLon) = GeoHashEncoder.DecodeBounds(geohash);
        var (centerLat, centerLon) = GeoHashEncoder.Decode(geohash);

        // Assert
        centerLat.Should().BeInRange(minLat, maxLat);
        centerLon.Should().BeInRange(minLon, maxLon);
    }

    [Fact]
    public void DecodeBounds_WithNullGeoHash_ThrowsArgumentException()
    {
        // Act & Assert
        var exception = Assert.Throws<ArgumentException>(() => GeoHashEncoder.DecodeBounds(null!));
        exception.ParamName.Should().Be("geohash");
        exception.Message.Should().Contain("GeoHash string cannot be null or empty");
    }

    [Fact]
    public void DecodeBounds_WithEmptyGeoHash_ThrowsArgumentException()
    {
        // Act & Assert
        var exception = Assert.Throws<ArgumentException>(() => GeoHashEncoder.DecodeBounds(string.Empty));
        exception.ParamName.Should().Be("geohash");
        exception.Message.Should().Contain("GeoHash string cannot be null or empty");
    }

    [Fact]
    public void GetNeighbors_ReturnsEightNeighbors()
    {
        // Arrange
        var geohash = "9q8yy9";

        // Act
        var neighbors = GeoHashEncoder.GetNeighbors(geohash);

        // Assert
        neighbors.Length.Should().Be(8);
        foreach (var n in neighbors) n.Should().NotBeNullOrEmpty();
        foreach (var n in neighbors) n.Length.Should().Be(geohash.Length);
    }

    [Fact]
    public void GetNeighbors_AllNeighborsAreDifferent()
    {
        // Arrange
        var geohash = "9q8yy9";

        // Act
        var neighbors = GeoHashEncoder.GetNeighbors(geohash);

        // Assert
        neighbors.Distinct().Count().Should().Be(8);
        foreach (var n in neighbors)
        {
            n.Should().NotBe(geohash);
        }
    }

    [Fact]
    public void GetNeighbors_WithNullGeoHash_ThrowsArgumentException()
    {
        // Act & Assert
        var exception = Assert.Throws<ArgumentException>(() => GeoHashEncoder.GetNeighbors(null!));
        exception.ParamName.Should().Be("geohash");
        exception.Message.Should().Contain("GeoHash string cannot be null or empty");
    }

    [Fact]
    public void GetNeighbors_WithEmptyGeoHash_ThrowsArgumentException()
    {
        // Act & Assert
        var exception = Assert.Throws<ArgumentException>(() => GeoHashEncoder.GetNeighbors(string.Empty));
        exception.ParamName.Should().Be("geohash");
        exception.Message.Should().Contain("GeoHash string cannot be null or empty");
    }

    [Fact]
    public void Encode_OnlyUsesValidBase32Characters()
    {
        // Arrange
        var validChars = "0123456789bcdefghjkmnpqrstuvwxyz";
        var lat = 37.7749;
        var lon = -122.4194;

        // Act
        var hash = GeoHashEncoder.Encode(lat, lon, 12);

        // Assert
        foreach (var c in hash)
        {
            validChars.Should().Contain(c.ToString());
        }
    }

    [Fact]
    public void Encode_HigherPrecision_IsMoreSpecific()
    {
        // Arrange
        var lat = 37.7749;
        var lon = -122.4194;

        // Act
        var hash5 = GeoHashEncoder.Encode(lat, lon, 5);
        var hash6 = GeoHashEncoder.Encode(lat, lon, 6);
        var hash7 = GeoHashEncoder.Encode(lat, lon, 7);

        // Assert - Higher precision should start with lower precision
        hash6.Should().StartWith(hash5);
        hash7.Should().StartWith(hash6);
    }
}
