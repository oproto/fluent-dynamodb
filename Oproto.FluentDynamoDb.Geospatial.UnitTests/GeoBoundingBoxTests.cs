namespace Oproto.FluentDynamoDb.Geospatial.UnitTests;

public class GeoBoundingBoxTests
{
    [Fact]
    public void Constructor_WithValidCorners_CreatesBoundingBox()
    {
        // Arrange
        var southwest = new GeoLocation(37.7, -122.5);
        var northeast = new GeoLocation(37.8, -122.4);

        // Act
        var bbox = new GeoBoundingBox(southwest, northeast);

        // Assert
        bbox.Southwest.Should().Be(southwest);
        bbox.Northeast.Should().Be(northeast);
    }

    [Fact]
    public void Constructor_WithSouthAboveNorth_ThrowsArgumentException()
    {
        // Arrange
        var southwest = new GeoLocation(37.8, -122.5); // Latitude too high
        var northeast = new GeoLocation(37.7, -122.4);

        // Act & Assert
        var exception = Assert.Throws<ArgumentException>(() => new GeoBoundingBox(southwest, northeast));
        exception.ParamName.Should().Be("southwest");
        exception.Message.Should().Contain("Southwest corner latitude must be less than or equal to northeast corner latitude");
    }

    [Fact]
    public void Constructor_WithDatelineCrossing_AllowsConstruction()
    {
        // Arrange - Box from 170°E to -170°E (crosses dateline)
        var southwest = new GeoLocation(10, 170);
        var northeast = new GeoLocation(20, -170);

        // Act - Should not throw
        var bbox = new GeoBoundingBox(southwest, northeast);

        // Assert
        bbox.Southwest.Should().Be(southwest);
        bbox.Northeast.Should().Be(northeast);
        bbox.CrossesDateLine().Should().BeTrue();
    }

    [Fact]
    public void Center_CalculatesCorrectCenterPoint()
    {
        // Arrange
        var southwest = new GeoLocation(37.7, -122.5);
        var northeast = new GeoLocation(37.9, -122.3);
        var bbox = new GeoBoundingBox(southwest, northeast);

        // Act
        var center = bbox.Center;

        // Assert
        center.Latitude.Should().Be(37.8);
        center.Longitude.Should().Be(-122.4);
    }

    [Fact]
    public void FromCenterAndDistanceMeters_CreatesCorrectBoundingBox()
    {
        // Arrange
        var center = new GeoLocation(37.7749, -122.4194);
        var distanceMeters = 5000.0; // 5 km

        // Act
        var bbox = GeoBoundingBox.FromCenterAndDistanceMeters(center, distanceMeters);

        // Assert - Use approximate comparison for floating point
        bbox.Center.Latitude.Should().BeApproximately(center.Latitude, 0.0001);
        bbox.Center.Longitude.Should().BeApproximately(center.Longitude, 0.0001);
        
        // Verify the bounding box contains points well inside the specified distance
        // 5km distance at ~37.7 latitude: ~0.045 degrees lat, ~0.056 degrees lon
        // Use 90% of that to be safely inside
        var northPoint = new GeoLocation(center.Latitude + 0.040, center.Longitude);
        var southPoint = new GeoLocation(center.Latitude - 0.040, center.Longitude);
        var eastPoint = new GeoLocation(center.Latitude, center.Longitude + 0.050);
        var westPoint = new GeoLocation(center.Latitude, center.Longitude - 0.050);
        
        bbox.Contains(northPoint).Should().BeTrue();
        bbox.Contains(southPoint).Should().BeTrue();
        bbox.Contains(eastPoint).Should().BeTrue();
        bbox.Contains(westPoint).Should().BeTrue();
    }

    [Fact]
    public void FromCenterAndDistanceKilometers_CreatesCorrectBoundingBox()
    {
        // Arrange
        var center = new GeoLocation(51.5074, -0.1278); // London
        var distanceKm = 10.0;

        // Act
        var bbox = GeoBoundingBox.FromCenterAndDistanceKilometers(center, distanceKm);

        // Assert - Use approximate comparison for floating point
        bbox.Center.Latitude.Should().BeApproximately(center.Latitude, 0.0001);
        bbox.Center.Longitude.Should().BeApproximately(center.Longitude, 0.0001);
        
        // Verify the center is within the box
        bbox.Contains(center).Should().BeTrue();
    }

    [Fact]
    public void FromCenterAndDistanceMiles_CreatesCorrectBoundingBox()
    {
        // Arrange
        var center = new GeoLocation(40.7128, -74.0060); // New York
        var distanceMiles = 5.0;

        // Act
        var bbox = GeoBoundingBox.FromCenterAndDistanceMiles(center, distanceMiles);

        // Assert
        bbox.Center.Latitude.Should().Be(center.Latitude);
        bbox.Center.Longitude.Should().Be(center.Longitude);
        
        // Verify the center is within the box
        bbox.Contains(center).Should().BeTrue();
    }

    [Fact]
    public void FromCenterAndDistanceMeters_AllUnits_CreateEquivalentBoxes()
    {
        // Arrange
        var center = new GeoLocation(35.6762, 139.6503); // Tokyo
        var distanceMeters = 5000.0;
        var distanceKm = 5.0;
        var distanceMiles = 3.106856; // 5 km in miles

        // Act
        var bboxMeters = GeoBoundingBox.FromCenterAndDistanceMeters(center, distanceMeters);
        var bboxKm = GeoBoundingBox.FromCenterAndDistanceKilometers(center, distanceKm);
        var bboxMiles = GeoBoundingBox.FromCenterAndDistanceMiles(center, distanceMiles);

        // Assert - All three should create approximately the same bounding box (within floating point precision)
        bboxMeters.Southwest.Latitude.Should().BeApproximately(bboxKm.Southwest.Latitude, 0.0000001);
        bboxMeters.Southwest.Longitude.Should().BeApproximately(bboxKm.Southwest.Longitude, 0.0000001);
        bboxMeters.Northeast.Latitude.Should().BeApproximately(bboxKm.Northeast.Latitude, 0.0000001);
        bboxMeters.Northeast.Longitude.Should().BeApproximately(bboxKm.Northeast.Longitude, 0.0000001);
        
        bboxMeters.Southwest.Latitude.Should().BeApproximately(bboxMiles.Southwest.Latitude, 0.0000001);
        bboxMeters.Southwest.Longitude.Should().BeApproximately(bboxMiles.Southwest.Longitude, 0.0000001);
        bboxMeters.Northeast.Latitude.Should().BeApproximately(bboxMiles.Northeast.Latitude, 0.0000001);
        bboxMeters.Northeast.Longitude.Should().BeApproximately(bboxMiles.Northeast.Longitude, 0.0000001);
    }

    [Fact]
    public void FromCenterAndDistanceMeters_AtNorthPole_ClampsToValidRange()
    {
        // Arrange
        var center = new GeoLocation(89, 0);
        var distanceMeters = 200000.0; // 200 km - would exceed north pole

        // Act
        var bbox = GeoBoundingBox.FromCenterAndDistanceMeters(center, distanceMeters);

        // Assert - Should clamp to 90 degrees
        bbox.Northeast.Latitude.Should().Be(90.0);
        bbox.Southwest.Latitude.Should().BeLessThan(89);
    }

    [Fact]
    public void FromCenterAndDistanceMeters_AtSouthPole_ClampsToValidRange()
    {
        // Arrange
        var center = new GeoLocation(-89, 0);
        var distanceMeters = 200000.0; // 200 km - would exceed south pole

        // Act
        var bbox = GeoBoundingBox.FromCenterAndDistanceMeters(center, distanceMeters);

        // Assert - Should clamp to -90 degrees
        bbox.Southwest.Latitude.Should().Be(-90.0);
        bbox.Northeast.Latitude.Should().BeGreaterThan(-89);
    }

    [Fact]
    public void Contains_WithLocationInside_ReturnsTrue()
    {
        // Arrange
        var southwest = new GeoLocation(37.7, -122.5);
        var northeast = new GeoLocation(37.9, -122.3);
        var bbox = new GeoBoundingBox(southwest, northeast);
        var insideLocation = new GeoLocation(37.8, -122.4);

        // Act
        var contains = bbox.Contains(insideLocation);

        // Assert
        contains.Should().BeTrue();
    }

    [Fact]
    public void Contains_WithLocationOutside_ReturnsFalse()
    {
        // Arrange
        var southwest = new GeoLocation(37.7, -122.5);
        var northeast = new GeoLocation(37.9, -122.3);
        var bbox = new GeoBoundingBox(southwest, northeast);
        var outsideLocation = new GeoLocation(38.0, -122.4); // North of box

        // Act
        var contains = bbox.Contains(outsideLocation);

        // Assert
        contains.Should().BeFalse();
    }

    [Fact]
    public void Contains_WithLocationOnBoundary_ReturnsTrue()
    {
        // Arrange
        var southwest = new GeoLocation(37.7, -122.5);
        var northeast = new GeoLocation(37.9, -122.3);
        var bbox = new GeoBoundingBox(southwest, northeast);
        var boundaryLocation = new GeoLocation(37.7, -122.4); // On south edge

        // Act
        var contains = bbox.Contains(boundaryLocation);

        // Assert
        contains.Should().BeTrue();
    }

    [Fact]
    public void Contains_WithCornerLocations_ReturnsTrue()
    {
        // Arrange
        var southwest = new GeoLocation(37.7, -122.5);
        var northeast = new GeoLocation(37.9, -122.3);
        var bbox = new GeoBoundingBox(southwest, northeast);

        // Act & Assert
        bbox.Contains(southwest).Should().BeTrue();
        bbox.Contains(northeast).Should().BeTrue();
        bbox.Contains(new GeoLocation(37.7, -122.3)).Should().BeTrue(); // Southeast
        bbox.Contains(new GeoLocation(37.9, -122.5)).Should().BeTrue(); // Northwest
    }

    [Fact]
    public void Contains_WithLocationWestOfBox_ReturnsFalse()
    {
        // Arrange
        var southwest = new GeoLocation(37.7, -122.5);
        var northeast = new GeoLocation(37.9, -122.3);
        var bbox = new GeoBoundingBox(southwest, northeast);
        var westLocation = new GeoLocation(37.8, -122.6);

        // Act
        var contains = bbox.Contains(westLocation);

        // Assert
        contains.Should().BeFalse();
    }

    [Fact]
    public void Contains_WithLocationEastOfBox_ReturnsFalse()
    {
        // Arrange
        var southwest = new GeoLocation(37.7, -122.5);
        var northeast = new GeoLocation(37.9, -122.3);
        var bbox = new GeoBoundingBox(southwest, northeast);
        var eastLocation = new GeoLocation(37.8, -122.2);

        // Act
        var contains = bbox.Contains(eastLocation);

        // Assert
        contains.Should().BeFalse();
    }

    [Fact]
    public void ToString_ReturnsFormattedString()
    {
        // Arrange
        var southwest = new GeoLocation(37.7, -122.5);
        var northeast = new GeoLocation(37.9, -122.3);
        var bbox = new GeoBoundingBox(southwest, northeast);

        // Act
        var result = bbox.ToString();

        // Assert
        result.Should().Be("SW: 37.7,-122.5, NE: 37.9,-122.3");
    }

    #region Dateline Crossing Tests

    [Fact]
    public void CrossesDateLine_WithNormalBox_ReturnsFalse()
    {
        // Arrange - Normal box that doesn't cross dateline
        var southwest = new GeoLocation(10, -122.5);
        var northeast = new GeoLocation(20, -122.3);
        var bbox = new GeoBoundingBox(southwest, northeast);

        // Act
        var crosses = bbox.CrossesDateLine();

        // Assert
        crosses.Should().BeFalse();
    }

    [Fact]
    public void CrossesDateLine_WithDatelineCrossingBox_ReturnsTrue()
    {
        // Arrange - Box from 170°E to -170°E (crosses dateline)
        var southwest = new GeoLocation(10, 170);
        var northeast = new GeoLocation(20, -170);
        var bbox = new GeoBoundingBox(southwest, northeast);

        // Act
        var crosses = bbox.CrossesDateLine();

        // Assert
        crosses.Should().BeTrue();
    }

    [Theory]
    [InlineData(170, -170)]  // 170°E to -170°E
    [InlineData(179, -179)]  // 179°E to -179°E
    [InlineData(175, -175)]  // 175°E to -175°E
    [InlineData(160, -160)]  // 160°E to -160°E
    public void CrossesDateLine_WithVariousDatelineCrossings_ReturnsTrue(double swLon, double neLon)
    {
        // Arrange
        var southwest = new GeoLocation(10, swLon);
        var northeast = new GeoLocation(20, neLon);
        var bbox = new GeoBoundingBox(southwest, northeast);

        // Act
        var crosses = bbox.CrossesDateLine();

        // Assert
        crosses.Should().BeTrue();
    }

    [Fact]
    public void SplitAtDateLine_WithDatelineCrossingBox_ProducesCorrectWesternBox()
    {
        // Arrange - Box from (10°, 170°) to (20°, -170°)
        var southwest = new GeoLocation(10, 170);
        var northeast = new GeoLocation(20, -170);
        var bbox = new GeoBoundingBox(southwest, northeast);

        // Act
        var (western, eastern) = bbox.SplitAtDateLine();

        // Assert - Western box should go from 170° to 180°
        western.Southwest.Latitude.Should().Be(10);
        western.Southwest.Longitude.Should().Be(170);
        western.Northeast.Latitude.Should().Be(20);
        western.Northeast.Longitude.Should().Be(180);
    }

    [Fact]
    public void SplitAtDateLine_WithDatelineCrossingBox_ProducesCorrectEasternBox()
    {
        // Arrange - Box from (10°, 170°) to (20°, -170°)
        var southwest = new GeoLocation(10, 170);
        var northeast = new GeoLocation(20, -170);
        var bbox = new GeoBoundingBox(southwest, northeast);

        // Act
        var (western, eastern) = bbox.SplitAtDateLine();

        // Assert - Eastern box should go from -180° to -170°
        eastern.Southwest.Latitude.Should().Be(10);
        eastern.Southwest.Longitude.Should().Be(-180);
        eastern.Northeast.Latitude.Should().Be(20);
        eastern.Northeast.Longitude.Should().Be(-170);
    }

    [Fact]
    public void SplitAtDateLine_WithDatelineCrossingBox_SplitBoxesCoverSameArea()
    {
        // Arrange - Box from (10°, 170°) to (20°, -170°)
        var southwest = new GeoLocation(10, 170);
        var northeast = new GeoLocation(20, -170);
        var bbox = new GeoBoundingBox(southwest, northeast);

        // Act
        var (western, eastern) = bbox.SplitAtDateLine();

        // Assert - Points in the original area should be in one of the split boxes
        var pointInWestern = new GeoLocation(15, 175);  // Between 170° and 180°
        var pointInEastern = new GeoLocation(15, -175); // Between -180° and -170°

        western.Contains(pointInWestern).Should().BeTrue();
        eastern.Contains(pointInEastern).Should().BeTrue();

        // Points outside the original area should not be in either box
        var pointOutside = new GeoLocation(15, 0);
        western.Contains(pointOutside).Should().BeFalse();
        eastern.Contains(pointOutside).Should().BeFalse();
    }

    [Fact]
    public void SplitAtDateLine_WithNonCrossingBox_ThrowsInvalidOperationException()
    {
        // Arrange - Normal box that doesn't cross dateline
        var southwest = new GeoLocation(10, -122.5);
        var northeast = new GeoLocation(20, -122.3);
        var bbox = new GeoBoundingBox(southwest, northeast);

        // Act & Assert
        var exception = Assert.Throws<InvalidOperationException>(() => bbox.SplitAtDateLine());
        exception.Message.Should().Contain("does not cross the date line");
        exception.Message.Should().Contain("Check CrossesDateLine()");
    }

    [Theory]
    [InlineData(170, -170, 10, 20)]  // Standard dateline crossing
    [InlineData(179.5, -179.5, 0, 10)]  // Very close to dateline
    [InlineData(160, -150, -10, 10)]  // Wider crossing
    public void SplitAtDateLine_WithVariousCrossings_ProducesValidBoxes(
        double swLon, double neLon, double swLat, double neLat)
    {
        // Arrange
        var southwest = new GeoLocation(swLat, swLon);
        var northeast = new GeoLocation(neLat, neLon);
        var bbox = new GeoBoundingBox(southwest, northeast);

        // Act
        var (western, eastern) = bbox.SplitAtDateLine();

        // Assert - Both boxes should be valid (not cross dateline themselves)
        western.CrossesDateLine().Should().BeFalse();
        eastern.CrossesDateLine().Should().BeFalse();

        // Western box should end at 180°
        western.Northeast.Longitude.Should().Be(180);

        // Eastern box should start at -180°
        eastern.Southwest.Longitude.Should().Be(-180);

        // Both boxes should preserve the latitude range
        western.Southwest.Latitude.Should().Be(swLat);
        western.Northeast.Latitude.Should().Be(neLat);
        eastern.Southwest.Latitude.Should().Be(swLat);
        eastern.Northeast.Latitude.Should().Be(neLat);
    }

    #endregion
}
