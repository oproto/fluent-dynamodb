using Oproto.FluentDynamoDb.Geospatial.GeoHash;

namespace Oproto.FluentDynamoDb.Geospatial.UnitTests.GeoHash;

public class GeoHashBoundingBoxExtensionsTests
{
    [Fact]
    public void GetGeoHashRange_WithDefaultPrecision_ReturnsRangeWithPrecision6()
    {
        // Arrange
        var southwest = new GeoLocation(37.7, -122.5);
        var northeast = new GeoLocation(37.9, -122.3);
        var bbox = new GeoBoundingBox(southwest, northeast);

        // Act
        var (minHash, maxHash) = bbox.GetGeoHashRange();

        // Assert
        minHash.Length.Should().Be(6);
        maxHash.Length.Should().Be(6);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(5)]
    [InlineData(6)]
    [InlineData(7)]
    [InlineData(10)]
    [InlineData(12)]
    public void GetGeoHashRange_WithSpecifiedPrecision_ReturnsCorrectLength(int precision)
    {
        // Arrange
        var southwest = new GeoLocation(37.7, -122.5);
        var northeast = new GeoLocation(37.9, -122.3);
        var bbox = new GeoBoundingBox(southwest, northeast);

        // Act
        var (minHash, maxHash) = bbox.GetGeoHashRange(precision);

        // Assert
        minHash.Length.Should().Be(precision);
        maxHash.Length.Should().Be(precision);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(13)]
    [InlineData(100)]
    public void GetGeoHashRange_WithInvalidPrecision_ThrowsArgumentOutOfRangeException(int precision)
    {
        // Arrange
        var southwest = new GeoLocation(37.7, -122.5);
        var northeast = new GeoLocation(37.9, -122.3);
        var bbox = new GeoBoundingBox(southwest, northeast);

        // Act & Assert
        var exception = Assert.Throws<ArgumentOutOfRangeException>(
            () => bbox.GetGeoHashRange(precision));
        exception.ParamName.Should().Be("precision");
        exception.Message.Should().Contain("Precision must be between 1 and 12");
    }

    [Fact]
    public void GetGeoHashRange_MinHashIsLexicographicallyLessThanMaxHash()
    {
        // Arrange
        var southwest = new GeoLocation(37.7, -122.5);
        var northeast = new GeoLocation(37.9, -122.3);
        var bbox = new GeoBoundingBox(southwest, northeast);

        // Act
        var (minHash, maxHash) = bbox.GetGeoHashRange(6);

        // Assert
        string.Compare(minHash, maxHash, StringComparison.Ordinal).Should().BeLessThan(0);
    }

    [Fact]
    public void GetGeoHashRange_ForSmallBoundingBox_ReturnsValidRange()
    {
        // Arrange - Small box in San Francisco
        var center = new GeoLocation(37.7749, -122.4194);
        var bbox = GeoBoundingBox.FromCenterAndDistanceKilometers(center, 1);

        // Act
        var (minHash, maxHash) = bbox.GetGeoHashRange(7);

        // Assert
        minHash.Should().NotBeNullOrEmpty();
        maxHash.Should().NotBeNullOrEmpty();
        minHash.Length.Should().Be(7);
        maxHash.Length.Should().Be(7);
    }

    [Fact]
    public void GetGeoHashRange_ForLargeBoundingBox_ReturnsValidRange()
    {
        // Arrange - Large box covering multiple cities
        var southwest = new GeoLocation(37.0, -123.0);
        var northeast = new GeoLocation(38.0, -121.0);
        var bbox = new GeoBoundingBox(southwest, northeast);

        // Act
        var (minHash, maxHash) = bbox.GetGeoHashRange(5);

        // Assert
        minHash.Should().NotBeNullOrEmpty();
        maxHash.Should().NotBeNullOrEmpty();
        string.Compare(minHash, maxHash, StringComparison.Ordinal).Should().BeLessThan(0);
    }

    [Fact]
    public void GetGeoHashRange_SouthwestCornerHashMatchesMinHash()
    {
        // Arrange
        var southwest = new GeoLocation(37.7, -122.5);
        var northeast = new GeoLocation(37.9, -122.3);
        var bbox = new GeoBoundingBox(southwest, northeast);

        // Act
        var (minHash, maxHash) = bbox.GetGeoHashRange(6);
        var swHash = southwest.ToGeoHash(6);

        // Assert
        minHash.Should().Be(swHash);
    }

    [Fact]
    public void GetGeoHashRange_NortheastCornerHashMatchesMaxHash()
    {
        // Arrange
        var southwest = new GeoLocation(37.7, -122.5);
        var northeast = new GeoLocation(37.9, -122.3);
        var bbox = new GeoBoundingBox(southwest, northeast);

        // Act
        var (minHash, maxHash) = bbox.GetGeoHashRange(6);
        var neHash = northeast.ToGeoHash(6);

        // Assert
        maxHash.Should().Be(neHash);
    }

    [Fact]
    public void GetGeoHashRange_WithDifferentPrecisions_MaintainsRelationship()
    {
        // Arrange
        var southwest = new GeoLocation(37.7, -122.5);
        var northeast = new GeoLocation(37.9, -122.3);
        var bbox = new GeoBoundingBox(southwest, northeast);

        // Act
        var (minHash5, maxHash5) = bbox.GetGeoHashRange(5);
        var (minHash6, maxHash6) = bbox.GetGeoHashRange(6);
        var (minHash7, maxHash7) = bbox.GetGeoHashRange(7);

        // Assert - Higher precision should start with lower precision
        minHash6.Should().StartWith(minHash5);
        minHash7.Should().StartWith(minHash6);
        maxHash6.Should().StartWith(maxHash5);
        maxHash7.Should().StartWith(maxHash6);
    }

    [Fact]
    public void GetGeoHashRange_ForBoundingBoxCrossingPrimeMeridian_ReturnsValidRange()
    {
        // Arrange - Box crossing prime meridian
        var southwest = new GeoLocation(51.0, -1.0);
        var northeast = new GeoLocation(52.0, 1.0);
        var bbox = new GeoBoundingBox(southwest, northeast);

        // Act
        var (minHash, maxHash) = bbox.GetGeoHashRange(6);

        // Assert
        minHash.Should().NotBeNullOrEmpty();
        maxHash.Should().NotBeNullOrEmpty();
        minHash.Length.Should().Be(6);
        maxHash.Length.Should().Be(6);
    }

    [Fact]
    public void GetGeoHashRange_ForBoundingBoxAtEquator_ReturnsValidRange()
    {
        // Arrange - Box at equator
        var southwest = new GeoLocation(-1.0, -1.0);
        var northeast = new GeoLocation(1.0, 1.0);
        var bbox = new GeoBoundingBox(southwest, northeast);

        // Act
        var (minHash, maxHash) = bbox.GetGeoHashRange(6);

        // Assert
        minHash.Should().NotBeNullOrEmpty();
        maxHash.Should().NotBeNullOrEmpty();
        string.Compare(minHash, maxHash, StringComparison.Ordinal).Should().BeLessThan(0);
    }

    [Fact]
    public void GetGeoHashRange_ForBoundingBoxNearPole_ReturnsValidRange()
    {
        // Arrange - Box near north pole
        var southwest = new GeoLocation(85.0, -10.0);
        var northeast = new GeoLocation(89.0, 10.0);
        var bbox = new GeoBoundingBox(southwest, northeast);

        // Act
        var (minHash, maxHash) = bbox.GetGeoHashRange(5);

        // Assert
        minHash.Should().NotBeNullOrEmpty();
        maxHash.Should().NotBeNullOrEmpty();
        minHash.Length.Should().Be(5);
        maxHash.Length.Should().Be(5);
    }

    [Fact]
    public void GetGeoHashRange_UsedInBetweenQuery_ProducesValidRange()
    {
        // Arrange - Simulate a typical query scenario
        var center = new GeoLocation(37.7749, -122.4194);
        var bbox = GeoBoundingBox.FromCenterAndDistanceKilometers(center, 5);

        // Act
        var (minHash, maxHash) = bbox.GetGeoHashRange(7);

        // Assert - Verify this would work in a BETWEEN query
        minHash.Should().NotBeNullOrEmpty();
        maxHash.Should().NotBeNullOrEmpty();
        string.Compare(minHash, maxHash, StringComparison.Ordinal).Should().BeLessThan(0);
        
        // Verify center location's hash is within range
        var centerHash = center.ToGeoHash(7);
        string.Compare(centerHash, minHash, StringComparison.Ordinal).Should().BeGreaterThanOrEqualTo(0);
        string.Compare(centerHash, maxHash, StringComparison.Ordinal).Should().BeLessThanOrEqualTo(0);
    }

    [Fact]
    public void GetGeoHashRange_WithVerySmallBoundingBox_ReturnsValidRange()
    {
        // Arrange - Very small box (100 meters)
        var center = new GeoLocation(37.7749, -122.4194);
        var bbox = GeoBoundingBox.FromCenterAndDistanceMeters(center, 100);

        // Act
        var (minHash, maxHash) = bbox.GetGeoHashRange(9);

        // Assert
        minHash.Should().NotBeNullOrEmpty();
        maxHash.Should().NotBeNullOrEmpty();
        minHash.Length.Should().Be(9);
        maxHash.Length.Should().Be(9);
    }
}
