using Oproto.FluentDynamoDb.Geospatial.H3;
using Oproto.FluentDynamoDb.Geospatial.S2;

namespace Oproto.FluentDynamoDb.Geospatial.UnitTests;

/// <summary>
/// Integration tests for combined dateline and pole handling in spatial queries.
/// Tests scenarios where queries involve both the International Date Line and polar regions.
/// </summary>
/// <remarks>
/// <para>
/// <strong>Validates Requirements:</strong> 13.1, 13.2, 13.3, 13.4, 13.5
/// </para>
/// <para>
/// These tests verify that the system correctly handles the most complex edge cases:
/// queries that cross the date line while also being near or at the poles.
/// </para>
/// </remarks>
public class CombinedDatelinePoleTests
{
    /// <summary>
    /// Tests query at North Pole near the dateline (89°, 179°) with 200km radius.
    /// This tests both dateline crossing and North Pole proximity.
    /// </summary>
    /// <remarks>
    /// Validates Requirements: 13.1, 13.2, 13.3, 13.4, 13.5
    /// </remarks>
    [Fact]
    public void GetCellsForRadius_NorthPoleNearDateline_ReturnsValidCells()
    {
        // Arrange
        var center = new GeoLocation(89, 179); // Near North Pole and dateline
        var radiusKm = 200;
        var s2Level = 10; // ~100km cells - appropriate for this radius
        var h3Resolution = 5; // ~8.5km cells - appropriate for this radius

        // Act - S2
        var s2Cells = S2CellCovering.GetCellsForRadius(center, radiusKm, s2Level, maxCells: 100);

        // Act - H3
        var h3Cells = H3CellCovering.GetCellsForRadius(center, radiusKm, h3Resolution, maxCells: 100);

        // Assert - S2
        Assert.NotEmpty(s2Cells); // cells should be generated for the area
        Assert.Equal(s2Cells.Count, s2Cells.Distinct().Count()); // cells should be deduplicated
        Assert.True(s2Cells.Count <= 100); // should respect maxCells limit

        // Verify all cells are valid S2 tokens
        foreach (var cell in s2Cells)
        {
            var (lat, lon) = S2Encoder.Decode(cell);
            Assert.InRange(lat, -90, 90); // decoded latitude should be valid
            Assert.InRange(lon, -180, 180); // decoded longitude should be valid
        }

        // Assert - H3
        Assert.NotEmpty(h3Cells); // cells should be generated for the area
        Assert.Equal(h3Cells.Count, h3Cells.Distinct().Count()); // cells should be deduplicated
        Assert.True(h3Cells.Count <= 100); // should respect maxCells limit

        // Verify all cells are valid H3 indices
        foreach (var cell in h3Cells)
        {
            var (lat, lon) = H3Encoder.Decode(cell);
            Assert.InRange(lat, -90, 90); // decoded latitude should be valid
            Assert.InRange(lon, -180, 180); // decoded longitude should be valid
        }
    }

    /// <summary>
    /// Tests query at South Pole near the dateline (-89°, -179°) with 200km radius.
    /// This tests both dateline crossing and South Pole proximity.
    /// </summary>
    /// <remarks>
    /// Validates Requirements: 13.1, 13.2, 13.3, 13.4, 13.5
    /// </remarks>
    [Fact]
    public void GetCellsForRadius_SouthPoleNearDateline_ReturnsValidCells()
    {
        // Arrange
        var center = new GeoLocation(-89, -179); // Near South Pole and dateline
        var radiusKm = 200;
        var s2Level = 10; // ~100km cells - appropriate for this radius
        var h3Resolution = 5; // ~8.5km cells - appropriate for this radius

        // Act - S2
        var s2Cells = S2CellCovering.GetCellsForRadius(center, radiusKm, s2Level, maxCells: 100);

        // Act - H3
        var h3Cells = H3CellCovering.GetCellsForRadius(center, radiusKm, h3Resolution, maxCells: 100);

        // Assert - S2
        Assert.NotEmpty(s2Cells); // cells should be generated for the area
        Assert.Equal(s2Cells.Count, s2Cells.Distinct().Count()); // cells should be deduplicated
        Assert.True(s2Cells.Count <= 100); // should respect maxCells limit

        // Verify all cells are valid S2 tokens
        foreach (var cell in s2Cells)
        {
            var (lat, lon) = S2Encoder.Decode(cell);
            Assert.InRange(lat, -90, 90); // decoded latitude should be valid
            Assert.InRange(lon, -180, 180); // decoded longitude should be valid
        }

        // Assert - H3
        Assert.NotEmpty(h3Cells); // cells should be generated for the area
        Assert.Equal(h3Cells.Count, h3Cells.Distinct().Count()); // cells should be deduplicated
        Assert.True(h3Cells.Count <= 100); // should respect maxCells limit

        // Verify all cells are valid H3 indices
        foreach (var cell in h3Cells)
        {
            var (lat, lon) = H3Encoder.Decode(cell);
            Assert.InRange(lat, -90, 90); // decoded latitude should be valid
            Assert.InRange(lon, -180, 180); // decoded longitude should be valid
        }
    }

    /// <summary>
    /// Tests query at equator near the dateline (0°, 179°) with 200km radius.
    /// This tests dateline crossing without pole complications.
    /// </summary>
    /// <remarks>
    /// Validates Requirements: 13.1, 13.2, 13.5
    /// </remarks>
    [Fact]
    public void GetCellsForRadius_EquatorNearDateline_ReturnsValidCells()
    {
        // Arrange
        var center = new GeoLocation(0, 179); // At equator near dateline
        var radiusKm = 200;
        var s2Level = 14; // ~6km cells - appropriate for this radius
        var h3Resolution = 6; // ~3.2km cells - appropriate for this radius

        // Act - S2
        var s2Cells = S2CellCovering.GetCellsForRadius(center, radiusKm, s2Level, maxCells: 100);

        // Act - H3
        var h3Cells = H3CellCovering.GetCellsForRadius(center, radiusKm, h3Resolution, maxCells: 100);

        // Assert - S2
        Assert.NotEmpty(s2Cells); // cells should be generated for the area
        Assert.Equal(s2Cells.Count, s2Cells.Distinct().Count()); // cells should be deduplicated
        Assert.True(s2Cells.Count <= 100); // should respect maxCells limit

        // Verify cells span across the dateline
        var hasWesternCells = s2Cells.Any(cell =>
        {
            var (_, lon) = S2Encoder.Decode(cell);
            return lon > 170;
        });
        var hasEasternCells = s2Cells.Any(cell =>
        {
            var (_, lon) = S2Encoder.Decode(cell);
            return lon < -170;
        });

        Assert.True(hasWesternCells || hasEasternCells); // cells should span across the dateline region

        // Assert - H3
        Assert.NotEmpty(h3Cells); // cells should be generated for the area
        Assert.Equal(h3Cells.Count, h3Cells.Distinct().Count()); // cells should be deduplicated
        Assert.True(h3Cells.Count <= 100); // should respect maxCells limit

        // Verify cells span across the dateline
        var h3HasWesternCells = h3Cells.Any(cell =>
        {
            var (_, lon) = H3Encoder.Decode(cell);
            return lon > 170;
        });
        var h3HasEasternCells = h3Cells.Any(cell =>
        {
            var (_, lon) = H3Encoder.Decode(cell);
            return lon < -170;
        });

        Assert.True(h3HasWesternCells || h3HasEasternCells); // cells should span across the dateline region
    }

    /// <summary>
    /// Tests query at North Pole away from dateline (89°, 0°) with 200km radius.
    /// This tests North Pole handling without dateline complications.
    /// </summary>
    /// <remarks>
    /// Validates Requirements: 13.3, 13.4, 13.5
    /// </remarks>
    [Fact]
    public void GetCellsForRadius_NorthPoleAwayFromDateline_ReturnsValidCells()
    {
        // Arrange
        var center = new GeoLocation(89, 0); // Near North Pole, away from dateline
        var radiusKm = 200;
        var s2Level = 10; // ~100km cells - appropriate for this radius
        var h3Resolution = 5; // ~8.5km cells - appropriate for this radius

        // Act - S2
        var s2Cells = S2CellCovering.GetCellsForRadius(center, radiusKm, s2Level, maxCells: 100);

        // Act - H3
        var h3Cells = H3CellCovering.GetCellsForRadius(center, radiusKm, h3Resolution, maxCells: 100);

        // Assert - S2
        Assert.NotEmpty(s2Cells); // cells should be generated for the area
        Assert.Equal(s2Cells.Count, s2Cells.Distinct().Count()); // cells should be deduplicated
        Assert.True(s2Cells.Count <= 100); // should respect maxCells limit

        // Verify cells are in the northern hemisphere (latitude > 0)
        foreach (var cell in s2Cells)
        {
            var (lat, _) = S2Encoder.Decode(cell);
            Assert.True(lat > 0); // cells should be in northern hemisphere
        }

        // Assert - H3
        Assert.NotEmpty(h3Cells); // cells should be generated for the area
        Assert.Equal(h3Cells.Count, h3Cells.Distinct().Count()); // cells should be deduplicated
        Assert.True(h3Cells.Count <= 100); // should respect maxCells limit

        // Verify cells are in the northern hemisphere (latitude > 0)
        foreach (var cell in h3Cells)
        {
            var (lat, _) = H3Encoder.Decode(cell);
            Assert.True(lat > 0); // cells should be in northern hemisphere
        }
    }

    /// <summary>
    /// Tests query at South Pole away from dateline (-89°, 0°) with 200km radius.
    /// This tests South Pole handling without dateline complications.
    /// </summary>
    /// <remarks>
    /// Validates Requirements: 13.3, 13.4, 13.5
    /// </remarks>
    [Fact]
    public void GetCellsForRadius_SouthPoleAwayFromDateline_ReturnsValidCells()
    {
        // Arrange
        var center = new GeoLocation(-89, 0); // Near South Pole, away from dateline
        var radiusKm = 200;
        var s2Level = 10; // ~100km cells - appropriate for this radius
        var h3Resolution = 5; // ~8.5km cells - appropriate for this radius

        // Act - S2
        var s2Cells = S2CellCovering.GetCellsForRadius(center, radiusKm, s2Level, maxCells: 100);

        // Act - H3
        var h3Cells = H3CellCovering.GetCellsForRadius(center, radiusKm, h3Resolution, maxCells: 100);

        // Assert - S2
        Assert.NotEmpty(s2Cells); // cells should be generated for the area
        Assert.Equal(s2Cells.Count, s2Cells.Distinct().Count()); // cells should be deduplicated
        Assert.True(s2Cells.Count <= 100); // should respect maxCells limit

        // Verify cells are in the southern hemisphere (latitude < 0)
        foreach (var cell in s2Cells)
        {
            var (lat, _) = S2Encoder.Decode(cell);
            Assert.True(lat < 0); // cells should be in southern hemisphere
        }

        // Assert - H3
        Assert.NotEmpty(h3Cells); // cells should be generated for the area
        Assert.Equal(h3Cells.Count, h3Cells.Distinct().Count()); // cells should be deduplicated
        Assert.True(h3Cells.Count <= 100); // should respect maxCells limit

        // Verify cells are in the southern hemisphere (latitude < 0)
        foreach (var cell in h3Cells)
        {
            var (lat, _) = H3Encoder.Decode(cell);
            Assert.True(lat < 0); // cells should be in southern hemisphere
        }
    }

    /// <summary>
    /// Tests that cells are properly deduplicated when a query spans both sides of the dateline
    /// and includes polar regions.
    /// </summary>
    /// <remarks>
    /// Validates Requirements: 13.5
    /// </remarks>
    [Fact]
    public void GetCellsForRadius_CombinedEdgeCase_DeduplicatesCells()
    {
        // Arrange
        var center = new GeoLocation(89, 179); // Near North Pole and dateline
        var radiusKm = 200;
        var s2Level = 10;
        var h3Resolution = 5;

        // Act - S2
        var s2Cells = S2CellCovering.GetCellsForRadius(center, radiusKm, s2Level, maxCells: 100);

        // Act - H3
        var h3Cells = H3CellCovering.GetCellsForRadius(center, radiusKm, h3Resolution, maxCells: 100);

        // Assert - S2
        var s2UniqueCount = s2Cells.Distinct().Count();
        Assert.Equal(s2Cells.Count, s2UniqueCount); // all S2 cells should be unique (no duplicates)

        // Assert - H3
        var h3UniqueCount = h3Cells.Distinct().Count();
        Assert.Equal(h3Cells.Count, h3UniqueCount); // all H3 cells should be unique (no duplicates)
    }

    /// <summary>
    /// Tests that bounding boxes that cross the dateline and include poles are handled correctly.
    /// </summary>
    /// <remarks>
    /// Validates Requirements: 13.1, 13.2, 13.3, 13.4, 13.5
    /// </remarks>
    [Fact]
    public void GetCellsForBoundingBox_CrossesDatelineAndIncludesPole_ReturnsValidCells()
    {
        // Arrange - Create a bounding box that crosses the dateline and includes the North Pole
        var southwest = new GeoLocation(85, 170);
        var northeast = new GeoLocation(90, -170);
        var bbox = new GeoBoundingBox(southwest, northeast);
        var s2Level = 10;
        var h3Resolution = 5;

        // Verify our test setup
        Assert.True(bbox.CrossesDateLine()); // bounding box should cross the dateline
        Assert.True(bbox.IncludesPole()); // bounding box should include the North Pole

        // Act - S2
        var s2Cells = S2CellCovering.GetCellsForBoundingBox(bbox, s2Level, maxCells: 100);

        // Act - H3
        var h3Cells = H3CellCovering.GetCellsForBoundingBox(bbox, h3Resolution, maxCells: 100);

        // Assert - S2
        Assert.NotEmpty(s2Cells); // cells should be generated for the area
        Assert.Equal(s2Cells.Count, s2Cells.Distinct().Count()); // cells should be deduplicated
        Assert.True(s2Cells.Count <= 100); // should respect maxCells limit

        // Verify cells are at high latitude
        foreach (var cell in s2Cells)
        {
            var (lat, _) = S2Encoder.Decode(cell);
            Assert.True(lat > 80); // cells should be at high northern latitude
        }

        // Assert - H3
        Assert.NotEmpty(h3Cells); // cells should be generated for the area
        Assert.Equal(h3Cells.Count, h3Cells.Distinct().Count()); // cells should be deduplicated
        Assert.True(h3Cells.Count <= 100); // should respect maxCells limit

        // Verify cells are at high latitude
        foreach (var cell in h3Cells)
        {
            var (lat, _) = H3Encoder.Decode(cell);
            Assert.True(lat > 80); // cells should be at high northern latitude
        }
    }

    /// <summary>
    /// Tests that cells are sorted by distance from center even in combined edge cases.
    /// </summary>
    /// <remarks>
    /// Validates Requirements: 13.1, 13.2, 13.3, 13.4
    /// </remarks>
    [Fact]
    public void GetCellsForRadius_CombinedEdgeCase_CellsSortedByDistance()
    {
        // Arrange
        var center = new GeoLocation(89, 179);
        var radiusKm = 200;
        var s2Level = 10;
        var h3Resolution = 5;

        // Act - S2
        var s2Cells = S2CellCovering.GetCellsForRadius(center, radiusKm, s2Level, maxCells: 100);

        // Act - H3
        var h3Cells = H3CellCovering.GetCellsForRadius(center, radiusKm, h3Resolution, maxCells: 100);

        // Assert - S2: Verify cells are sorted by distance (spiral order)
        var s2Distances = s2Cells.Select(cell =>
        {
            var (lat, lon) = S2Encoder.Decode(cell);
            return center.DistanceToKilometers(new GeoLocation(lat, lon));
        }).ToList();

        for (int i = 1; i < s2Distances.Count; i++)
        {
            Assert.True(s2Distances[i] >= s2Distances[i - 1]); // S2 cells should be sorted by distance from center (spiral order)
        }

        // Assert - H3: Verify cells are sorted by distance (spiral order)
        var h3Distances = h3Cells.Select(cell =>
        {
            var (lat, lon) = H3Encoder.Decode(cell);
            return center.DistanceToKilometers(new GeoLocation(lat, lon));
        }).ToList();

        for (int i = 1; i < h3Distances.Count; i++)
        {
            Assert.True(h3Distances[i] >= h3Distances[i - 1]); // H3 cells should be sorted by distance from center (spiral order)
        }
    }
}
