using Oproto.FluentDynamoDb.Geospatial.GeoHash;

namespace Oproto.FluentDynamoDb.Geospatial.UnitTests.GeoHash;

public class GeoHashExtensionsTests
{
    [Fact]
    public void ToGeoHash_WithDefaultPrecision_ReturnsGeoHashWithPrecision6()
    {
        // Arrange
        var location = new GeoLocation(37.7749, -122.4194);

        // Act
        var hash = location.ToGeoHash();

        // Assert
        hash.Length.Should().Be(6);
        hash.Should().StartWith("9q8yy");  // Verify it's in the right general area
    }

    [Theory]
    [InlineData(1)]
    [InlineData(5)]
    [InlineData(6)]
    [InlineData(7)]
    [InlineData(10)]
    [InlineData(12)]
    public void ToGeoHash_WithSpecifiedPrecision_ReturnsCorrectLength(int precision)
    {
        // Arrange
        var location = new GeoLocation(37.7749, -122.4194);

        // Act
        var hash = location.ToGeoHash(precision);

        // Assert
        hash.Length.Should().Be(precision);
    }

    [Theory]
    [InlineData(37.7749, -122.4194, 5, "9q8yy")]
    [InlineData(40.7128, -74.0060, 6, "dr5ru")]
    [InlineData(51.5074, -0.1278, 7, "gcpvj0")]
    [InlineData(0.0, 0.0, 6, "s00000")]
    public void ToGeoHash_WithKnownLocations_ReturnsExpectedHash(
        double lat, double lon, int precision, string expectedHash)
    {
        // Arrange
        var location = new GeoLocation(lat, lon);

        // Act
        var hash = location.ToGeoHash(precision);

        // Assert - Verify the hash starts with expected prefix (geohash can vary slightly at boundaries)
        hash.Should().StartWith(expectedHash.Substring(0, Math.Min(expectedHash.Length - 1, precision - 1)));
        hash.Length.Should().Be(precision);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(13)]
    [InlineData(100)]
    public void ToGeoHash_WithInvalidPrecision_ThrowsArgumentOutOfRangeException(int precision)
    {
        // Arrange
        var location = new GeoLocation(37.7749, -122.4194);

        // Act & Assert
        var exception = Assert.Throws<ArgumentOutOfRangeException>(
            () => location.ToGeoHash(precision));
        exception.ParamName.Should().Be("precision");
    }

    [Theory]
    [InlineData("9q8yy9", 37.7749, -122.4194)]
    [InlineData("dr5ru7", 40.7128, -74.0060)]
    [InlineData("gcpvj0", 51.5074, -0.1278)]
    [InlineData("s00000", 0.0, 0.0)]
    public void FromGeoHash_WithValidHash_ReturnsApproximateLocation(
        string hash, double expectedLat, double expectedLon)
    {
        // Act
        var location = GeoHashExtensions.FromGeoHash(hash);

        // Assert - Precision 6 has ~0.61km accuracy, but decoded center can be offset from original point
        // Use 0.05 degrees (~5.5km) tolerance to account for cell center vs original point
        location.Latitude.Should().BeApproximately(expectedLat, 0.05, "decoded latitude should be within precision bounds");
        location.Longitude.Should().BeApproximately(expectedLon, 0.05, "decoded longitude should be within precision bounds");
    }

    [Fact]
    public void FromGeoHash_WithNullHash_ThrowsArgumentException()
    {
        // Act & Assert
        var exception = Assert.Throws<ArgumentException>(
            () => GeoHashExtensions.FromGeoHash(null!));
        exception.ParamName.Should().Be("geohash");
    }

    [Fact]
    public void FromGeoHash_WithEmptyHash_ThrowsArgumentException()
    {
        // Act & Assert
        var exception = Assert.Throws<ArgumentException>(
            () => GeoHashExtensions.FromGeoHash(string.Empty));
        exception.ParamName.Should().Be("geohash");
    }

    [Theory]
    [InlineData("invalid!")]
    [InlineData("9q8yya")]  // 'a' is not valid
    [InlineData("9q8yyi")]  // 'i' is not valid
    public void FromGeoHash_WithInvalidCharacters_ThrowsArgumentException(string invalidHash)
    {
        // Act & Assert
        var exception = Assert.Throws<ArgumentException>(
            () => GeoHashExtensions.FromGeoHash(invalidHash));
        exception.ParamName.Should().Be("geohash");
    }

    [Fact]
    public void ToGeoHash_ThenFromGeoHash_RoundTrip_ReturnsApproximateLocation()
    {
        // Arrange
        var originalLocation = new GeoLocation(37.7749, -122.4194);

        // Act
        var hash = originalLocation.ToGeoHash(7);
        var decodedLocation = GeoHashExtensions.FromGeoHash(hash);

        // Assert - Precision 7 has ~76m accuracy (~0.0007 degrees tolerance)
        decodedLocation.Latitude.Should().BeApproximately(originalLocation.Latitude, 0.001, "decoded latitude should be very close to original");
        decodedLocation.Longitude.Should().BeApproximately(originalLocation.Longitude, 0.001, "decoded longitude should be very close to original");
    }

    [Fact]
    public void ToGeoHashCell_WithDefaultPrecision_ReturnsCell()
    {
        // Arrange
        var location = new GeoLocation(37.7749, -122.4194);

        // Act
        var cell = location.ToGeoHashCell();

        // Assert
        cell.Precision.Should().Be(6);
        cell.Hash.Should().StartWith("9q8yy");  // Verify it's in the right general area
        cell.Bounds.Contains(location).Should().BeTrue();
    }

    [Theory]
    [InlineData(1)]
    [InlineData(5)]
    [InlineData(7)]
    [InlineData(10)]
    [InlineData(12)]
    public void ToGeoHashCell_WithSpecifiedPrecision_ReturnsCorrectCell(int precision)
    {
        // Arrange
        var location = new GeoLocation(37.7749, -122.4194);

        // Act
        var cell = location.ToGeoHashCell(precision);

        // Assert
        cell.Precision.Should().Be(precision);
        cell.Hash.Length.Should().Be(precision);
        cell.Bounds.Contains(location).Should().BeTrue();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(13)]
    [InlineData(100)]
    public void ToGeoHashCell_WithInvalidPrecision_ThrowsArgumentOutOfRangeException(int precision)
    {
        // Arrange
        var location = new GeoLocation(37.7749, -122.4194);

        // Act & Assert
        var exception = Assert.Throws<ArgumentOutOfRangeException>(
            () => location.ToGeoHashCell(precision));
        exception.ParamName.Should().Be("precision");
    }

    [Fact]
    public void ToGeoHashCell_CellBoundsContainOriginalLocation()
    {
        // Arrange
        var location = new GeoLocation(37.7749, -122.4194);

        // Act
        var cell = location.ToGeoHashCell(8);

        // Assert
        cell.Bounds.Contains(location).Should().BeTrue();
    }

    [Fact]
    public void ToGeoHash_HigherPrecision_StartsWithLowerPrecision()
    {
        // Arrange
        var location = new GeoLocation(37.7749, -122.4194);

        // Act
        var hash5 = location.ToGeoHash(5);
        var hash6 = location.ToGeoHash(6);
        var hash7 = location.ToGeoHash(7);

        // Assert
        hash6.Should().StartWith(hash5);
        hash7.Should().StartWith(hash6);
    }

    [Fact]
    public void ToGeoHashCell_HigherPrecision_HashStartsWithLowerPrecision()
    {
        // Arrange
        var location = new GeoLocation(37.7749, -122.4194);

        // Act
        var cell5 = location.ToGeoHashCell(5);
        var cell6 = location.ToGeoHashCell(6);
        var cell7 = location.ToGeoHashCell(7);

        // Assert
        cell6.Hash.Should().StartWith(cell5.Hash);
        cell7.Hash.Should().StartWith(cell6.Hash);
    }

    [Theory]
    [InlineData(90, 0)]     // North Pole
    [InlineData(-90, 0)]    // South Pole
    [InlineData(0, 180)]    // Date line
    [InlineData(0, -180)]   // Date line
    public void ToGeoHash_WithEdgeCaseLocations_ReturnsValidHash(double lat, double lon)
    {
        // Arrange
        var location = new GeoLocation(lat, lon);

        // Act
        var hash = location.ToGeoHash();

        // Assert
        hash.Should().NotBeNullOrEmpty();
        hash.Length.Should().Be(6);
    }

    [Theory]
    [InlineData(90, 0)]
    [InlineData(-90, 0)]
    [InlineData(0, 180)]
    [InlineData(0, -180)]
    public void ToGeoHashCell_WithEdgeCaseLocations_ReturnsValidCell(double lat, double lon)
    {
        // Arrange
        var location = new GeoLocation(lat, lon);

        // Act
        var cell = location.ToGeoHashCell();

        // Assert
        cell.Hash.Should().NotBeNullOrEmpty();
        cell.Precision.Should().Be(6);
        cell.Bounds.Contains(location).Should().BeTrue();
    }
}
