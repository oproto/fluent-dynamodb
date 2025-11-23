using Oproto.FluentDynamoDb.Geospatial.S2;
using Oproto.FluentDynamoDb.Geospatial.H3;

namespace Oproto.FluentDynamoDb.Geospatial.UnitTests;

/// <summary>
/// Unit tests for dateline crossing handling in S2 and H3 cell coverings.
/// </summary>
public class DatelineCellCoveringTests
{
    #region S2 Dateline Tests

    [Fact]
    public void S2CellCovering_WithDatelineCrossingBox_ReturnsUniqueCells()
    {
        // Arrange - Box from 170°E to -170°E (crosses dateline)
        var southwest = new GeoLocation(10, 170);
        var northeast = new GeoLocation(20, -170);
        var bbox = new GeoBoundingBox(southwest, northeast);
        var level = 10;

        // Act
        var cells = S2CellCovering.GetCellsForBoundingBox(bbox, level, maxCells: 100);

        // Assert - Should have unique cells (no duplicates)
        cells.Should().OnlyHaveUniqueItems();
        cells.Should().NotBeEmpty();
    }

    [Fact]
    public void S2CellCovering_WithDatelineCrossingBox_CellsAreSortedByDistance()
    {
        // Arrange - Box from 170°E to -170°E (crosses dateline)
        var southwest = new GeoLocation(10, 170);
        var northeast = new GeoLocation(20, -170);
        var bbox = new GeoBoundingBox(southwest, northeast);
        var center = bbox.Center;
        var level = 10;

        // Act
        var cells = S2CellCovering.GetCellsForBoundingBox(bbox, level, maxCells: 100);

        // Assert - Cells should be sorted by distance from center
        var distances = cells.Select(cell =>
        {
            var (lat, lon) = S2Encoder.Decode(cell);
            var location = new GeoLocation(lat, lon);
            return center.DistanceToKilometers(location);
        }).ToList();

        distances.Should().BeInAscendingOrder();
    }

    [Fact]
    public void S2CellCovering_WithDatelineCrossingRadius_ReturnsUniqueCells()
    {
        // Arrange - Center near dateline with radius that crosses it
        var center = new GeoLocation(15, 179);
        var radiusKm = 200; // Large enough to cross dateline
        var level = 10;

        // Act
        var cells = S2CellCovering.GetCellsForRadius(center, radiusKm, level, maxCells: 100);

        // Assert - Should have unique cells (no duplicates)
        cells.Should().OnlyHaveUniqueItems();
        cells.Should().NotBeEmpty();
    }

    [Fact]
    public void S2CellCovering_WithDatelineCrossingRadius_CellsAreSortedByDistance()
    {
        // Arrange - Center near dateline with radius that crosses it
        var center = new GeoLocation(15, 179);
        var radiusKm = 200; // Large enough to cross dateline
        var level = 10;

        // Act
        var cells = S2CellCovering.GetCellsForRadius(center, radiusKm, level, maxCells: 100);

        // Assert - Cells should be sorted by distance from center
        var distances = cells.Select(cell =>
        {
            var (lat, lon) = S2Encoder.Decode(cell);
            var location = new GeoLocation(lat, lon);
            return center.DistanceToKilometers(location);
        }).ToList();

        distances.Should().BeInAscendingOrder();
    }

    [Theory]
    [InlineData(170, -170)]  // 170°E to -170°E
    [InlineData(179, -179)]  // 179°E to -179°E
    [InlineData(175, -175)]  // 175°E to -175°E
    public void S2CellCovering_WithVariousDatelineCrossings_ProducesValidCells(double swLon, double neLon)
    {
        // Arrange
        var southwest = new GeoLocation(10, swLon);
        var northeast = new GeoLocation(20, neLon);
        var bbox = new GeoBoundingBox(southwest, northeast);
        var level = 10;

        // Act
        var cells = S2CellCovering.GetCellsForBoundingBox(bbox, level, maxCells: 100);

        // Assert
        cells.Should().NotBeEmpty();
        cells.Should().OnlyHaveUniqueItems();

        // All cells should be valid S2 tokens
        foreach (var cell in cells)
        {
            // Should be able to decode without throwing
            var (lat, lon) = S2Encoder.Decode(cell);
            lat.Should().BeInRange(-90, 90);
            lon.Should().BeInRange(-180, 180);
        }
    }

    #endregion

    #region H3 Dateline Tests

    [Fact]
    public void H3CellCovering_WithDatelineCrossingBox_ReturnsUniqueCells()
    {
        // Arrange - Box from 170°E to -170°E (crosses dateline)
        var southwest = new GeoLocation(10, 170);
        var northeast = new GeoLocation(20, -170);
        var bbox = new GeoBoundingBox(southwest, northeast);
        var resolution = 5;

        // Act
        var cells = H3CellCovering.GetCellsForBoundingBox(bbox, resolution, maxCells: 100);

        // Assert - Should have unique cells (no duplicates)
        cells.Should().OnlyHaveUniqueItems();
        cells.Should().NotBeEmpty();
    }

    [Fact]
    public void H3CellCovering_WithDatelineCrossingBox_CellsAreSortedByDistance()
    {
        // Arrange - Box from 170°E to -170°E (crosses dateline)
        var southwest = new GeoLocation(10, 170);
        var northeast = new GeoLocation(20, -170);
        var bbox = new GeoBoundingBox(southwest, northeast);
        var center = bbox.Center;
        var resolution = 5;

        // Act
        var cells = H3CellCovering.GetCellsForBoundingBox(bbox, resolution, maxCells: 100);

        // Assert - Cells should be sorted by distance from center
        var distances = cells.Select(cell =>
        {
            var (lat, lon) = H3Encoder.Decode(cell);
            var location = new GeoLocation(lat, lon);
            return center.DistanceToKilometers(location);
        }).ToList();

        distances.Should().BeInAscendingOrder();
    }

    [Fact]
    public void H3CellCovering_WithDatelineCrossingRadius_ReturnsUniqueCells()
    {
        // Arrange - Center near dateline with radius that crosses it
        var center = new GeoLocation(15, 179);
        var radiusKm = 200; // Large enough to cross dateline
        var resolution = 5;

        // Act
        var cells = H3CellCovering.GetCellsForRadius(center, radiusKm, resolution, maxCells: 100);

        // Assert - Should have unique cells (no duplicates)
        cells.Should().OnlyHaveUniqueItems();
        cells.Should().NotBeEmpty();
    }

    [Fact]
    public void H3CellCovering_WithDatelineCrossingRadius_CellsAreSortedByDistance()
    {
        // Arrange - Center near dateline with radius that crosses it
        var center = new GeoLocation(15, 179);
        var radiusKm = 200; // Large enough to cross dateline
        var resolution = 5;

        // Act
        var cells = H3CellCovering.GetCellsForRadius(center, radiusKm, resolution, maxCells: 100);

        // Assert - Cells should be sorted by distance from center
        var distances = cells.Select(cell =>
        {
            var (lat, lon) = H3Encoder.Decode(cell);
            var location = new GeoLocation(lat, lon);
            return center.DistanceToKilometers(location);
        }).ToList();

        distances.Should().BeInAscendingOrder();
    }

    [Theory]
    [InlineData(170, -170)]  // 170°E to -170°E
    [InlineData(179, -179)]  // 179°E to -179°E
    [InlineData(175, -175)]  // 175°E to -175°E
    public void H3CellCovering_WithVariousDatelineCrossings_ProducesValidCells(double swLon, double neLon)
    {
        // Arrange
        var southwest = new GeoLocation(10, swLon);
        var northeast = new GeoLocation(20, neLon);
        var bbox = new GeoBoundingBox(southwest, northeast);
        var resolution = 5;

        // Act
        var cells = H3CellCovering.GetCellsForBoundingBox(bbox, resolution, maxCells: 100);

        // Assert
        cells.Should().NotBeEmpty();
        cells.Should().OnlyHaveUniqueItems();

        // All cells should be valid H3 indices
        foreach (var cell in cells)
        {
            // Should be able to decode without throwing
            var (lat, lon) = H3Encoder.Decode(cell);
            lat.Should().BeInRange(-90, 90);
            lon.Should().BeInRange(-180, 180);
        }
    }

    #endregion
}
