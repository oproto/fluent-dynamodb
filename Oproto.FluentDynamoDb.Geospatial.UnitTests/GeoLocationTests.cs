namespace Oproto.FluentDynamoDb.Geospatial.UnitTests;

public class GeoLocationTests
{
    [Fact]
    public void Constructor_WithValidCoordinates_CreatesLocation()
    {
        // Arrange & Act
        var location = new GeoLocation(37.7749, -122.4194);

        // Assert
        location.Latitude.Should().Be(37.7749);
        location.Longitude.Should().Be(-122.4194);
    }

    [Theory]
    [InlineData(-90, 0)]    // South Pole
    [InlineData(90, 0)]     // North Pole
    [InlineData(0, -180)]   // Date line west
    [InlineData(0, 180)]    // Date line east
    [InlineData(0, 0)]      // Null Island
    public void Constructor_WithBoundaryCoordinates_CreatesLocation(double lat, double lon)
    {
        // Arrange & Act
        var location = new GeoLocation(lat, lon);

        // Assert
        location.Latitude.Should().Be(lat);
        location.Longitude.Should().Be(lon);
    }

    [Theory]
    [InlineData(-91, 0, "latitude")]
    [InlineData(91, 0, "latitude")]
    [InlineData(-100, 0, "latitude")]
    [InlineData(100, 0, "latitude")]
    public void Constructor_WithInvalidLatitude_ThrowsArgumentOutOfRangeException(
        double lat, double lon, string expectedParamName)
    {
        // Arrange & Act
        var exception = Assert.Throws<ArgumentOutOfRangeException>(() => new GeoLocation(lat, lon));

        // Assert
        exception.ParamName.Should().Be(expectedParamName);
        exception.Message.Should().Contain("Latitude must be between -90 and 90 degrees");
    }

    [Theory]
    [InlineData(0, -181, "longitude")]
    [InlineData(0, 181, "longitude")]
    [InlineData(0, -200, "longitude")]
    [InlineData(0, 200, "longitude")]
    public void Constructor_WithInvalidLongitude_ThrowsArgumentOutOfRangeException(
        double lat, double lon, string expectedParamName)
    {
        // Arrange & Act
        var exception = Assert.Throws<ArgumentOutOfRangeException>(() => new GeoLocation(lat, lon));

        // Assert
        exception.ParamName.Should().Be(expectedParamName);
        exception.Message.Should().Contain("Longitude must be between -180 and 180 degrees");
    }

    [Fact]
    public void IsValid_WithValidLocation_ReturnsTrue()
    {
        // Arrange
        var location = new GeoLocation(37.7749, -122.4194);

        // Act
        var isValid = location.IsValid();

        // Assert
        isValid.Should().BeTrue();
    }

    [Fact]
    public void DistanceToMeters_BetweenKnownLocations_ReturnsAccurateDistance()
    {
        // Arrange - San Francisco to New York
        var sanFrancisco = new GeoLocation(37.7749, -122.4194);
        var newYork = new GeoLocation(40.7128, -74.0060);

        // Act
        var distance = sanFrancisco.DistanceToMeters(newYork);

        // Assert - Expected distance is approximately 4,130 km = 4,130,000 meters
        // Allow 0.5% tolerance
        distance.Should().BeInRange(4_100_000, 4_150_000);
    }

    [Fact]
    public void DistanceToKilometers_BetweenKnownLocations_ReturnsAccurateDistance()
    {
        // Arrange - London to Paris
        var london = new GeoLocation(51.5074, -0.1278);
        var paris = new GeoLocation(48.8566, 2.3522);

        // Act
        var distance = london.DistanceToKilometers(paris);

        // Assert - Expected distance is approximately 344 km
        // Allow 0.5% tolerance
        distance.Should().BeInRange(342, 346);
    }

    [Fact]
    public void DistanceToMiles_BetweenKnownLocations_ReturnsAccurateDistance()
    {
        // Arrange - Tokyo to Sydney
        var tokyo = new GeoLocation(35.6762, 139.6503);
        var sydney = new GeoLocation(-33.8688, 151.2093);

        // Act
        var distance = tokyo.DistanceToMiles(sydney);

        // Assert - Expected distance is approximately 4,863 miles
        // Allow 0.5% tolerance
        distance.Should().BeInRange(4_840, 4_890);
    }

    [Fact]
    public void DistanceToMeters_ToSameLocation_ReturnsZero()
    {
        // Arrange
        var location = new GeoLocation(37.7749, -122.4194);

        // Act
        var distance = location.DistanceToMeters(location);

        // Assert
        distance.Should().Be(0.0);
    }

    [Fact]
    public void DistanceToMeters_AtPoles_CalculatesCorrectly()
    {
        // Arrange
        var northPole = new GeoLocation(90, 0);
        var southPole = new GeoLocation(-90, 0);

        // Act
        var distance = northPole.DistanceToMeters(southPole);

        // Assert - Half the Earth's circumference (approximately 20,000 km)
        distance.Should().BeInRange(19_900_000, 20_100_000);
    }

    [Fact]
    public void DistanceToMeters_AcrossDateLine_CalculatesCorrectly()
    {
        // Arrange - Points on opposite sides of the date line
        var west = new GeoLocation(0, 179);
        var east = new GeoLocation(0, -179);

        // Act
        var distance = west.DistanceToMeters(east);

        // Assert - Should be approximately 222 km (2 degrees at equator)
        distance.Should().BeInRange(220_000, 225_000);
    }

    [Fact]
    public void DistanceToMeters_AcrossPrimeMeridian_CalculatesCorrectly()
    {
        // Arrange - Points on opposite sides of prime meridian
        var west = new GeoLocation(51.5, -1);
        var east = new GeoLocation(51.5, 1);

        // Act
        var distance = west.DistanceToMeters(east);

        // Assert - Should be approximately 139 km (2 degrees at this latitude)
        distance.Should().BeInRange(135_000, 145_000);
    }

    [Fact]
    public void Equals_WithSameCoordinates_ReturnsTrue()
    {
        // Arrange
        var location1 = new GeoLocation(37.7749, -122.4194);
        var location2 = new GeoLocation(37.7749, -122.4194);

        // Act & Assert
        location1.Equals(location2).Should().BeTrue();
        (location1 == location2).Should().BeTrue();
        (location1 != location2).Should().BeFalse();
    }

    [Fact]
    public void Equals_WithDifferentCoordinates_ReturnsFalse()
    {
        // Arrange
        var location1 = new GeoLocation(37.7749, -122.4194);
        var location2 = new GeoLocation(40.7128, -74.0060);

        // Act & Assert
        location1.Equals(location2).Should().BeFalse();
        (location1 == location2).Should().BeFalse();
        (location1 != location2).Should().BeTrue();
    }

    [Fact]
    public void Equals_WithObject_WorksCorrectly()
    {
        // Arrange
        var location1 = new GeoLocation(37.7749, -122.4194);
        object location2 = new GeoLocation(37.7749, -122.4194);
        object differentLocation = new GeoLocation(40.7128, -74.0060);
        object notALocation = "not a location";

        // Act & Assert
        location1.Equals(location2).Should().BeTrue();
        location1.Equals(differentLocation).Should().BeFalse();
        location1.Equals(notALocation).Should().BeFalse();
        location1.Equals(null).Should().BeFalse();
    }

    [Fact]
    public void GetHashCode_WithSameCoordinates_ReturnsSameHashCode()
    {
        // Arrange
        var location1 = new GeoLocation(37.7749, -122.4194);
        var location2 = new GeoLocation(37.7749, -122.4194);

        // Act
        var hash1 = location1.GetHashCode();
        var hash2 = location2.GetHashCode();

        // Assert
        hash1.Should().Be(hash2);
    }

    [Fact]
    public void GetHashCode_WithDifferentCoordinates_ReturnsDifferentHashCode()
    {
        // Arrange
        var location1 = new GeoLocation(37.7749, -122.4194);
        var location2 = new GeoLocation(40.7128, -74.0060);

        // Act
        var hash1 = location1.GetHashCode();
        var hash2 = location2.GetHashCode();

        // Assert
        hash1.Should().NotBe(hash2);
    }

    [Fact]
    public void ToString_ReturnsFormattedString()
    {
        // Arrange
        var location = new GeoLocation(37.7749, -122.4194);

        // Act
        var result = location.ToString();

        // Assert
        result.Should().Be("37.7749,-122.4194");
    }

    [Fact]
    public void ToString_WithNegativeCoordinates_ReturnsFormattedString()
    {
        // Arrange
        var location = new GeoLocation(-33.8688, -151.2093);

        // Act
        var result = location.ToString();

        // Assert
        result.Should().Be("-33.8688,-151.2093");
    }
}
