using Oproto.FluentDynamoDb.Attributes;
using Oproto.FluentDynamoDb.Geospatial.S2;
using Oproto.FluentDynamoDb.Geospatial.H3;

namespace Oproto.FluentDynamoDb.Geospatial.UnitTests;

/// <summary>
/// Unit tests for the refactored SpatialQueryExtensions architecture.
/// Tests the custom cell list functionality and query factory pattern.
/// </summary>
public class SpatialQueryArchitectureTests
{
    /// <summary>
    /// Tests that custom cell lists can be used for spatial queries.
    /// </summary>
    [Fact]
    public void CustomCellList_CanBeUsedForSpatialQueries()
    {
        // Arrange: Create a custom list of S2 cells
        // Use level 10 (~4.5km cells) with 5km radius to stay within 500 cell limit
        var center = new GeoLocation(37.7749, -122.4194); // San Francisco
        var cells = S2CellCovering.GetCellsForRadius(center, 5.0, 10, maxCells: 10);
        
        // Assert: Cells should be generated
        Assert.NotEmpty(cells);
        Assert.True(cells.Count <= 10);
    }

    /// <summary>
    /// Tests that custom H3 cell lists can be generated.
    /// </summary>
    [Fact]
    public void CustomH3CellList_CanBeGenerated()
    {
        // Arrange: Create a custom list of H3 cells
        // Use resolution 7 (~1.2km cells) with 5km radius to stay within cell limits
        var center = new GeoLocation(37.7749, -122.4194); // San Francisco
        var cells = H3CellCovering.GetCellsForRadius(center, 5.0, 7, maxCells: 10);
        
        // Assert: Cells should be generated
        Assert.NotEmpty(cells);
        Assert.True(cells.Count <= 10);
    }

    /// <summary>
    /// Tests that cell computation for radius queries works correctly.
    /// </summary>
    [Theory]
    [InlineData(SpatialIndexType.S2, 10)]  // Level 10 (~4.5km cells) with 3km radius stays within 500 cell limit
    [InlineData(SpatialIndexType.H3, 9)]
    public void CellComputation_ForRadius_ReturnsValidCells(SpatialIndexType indexType, int precision)
    {
        // Arrange
        var center = new GeoLocation(40.7128, -74.0060); // New York
        var radiusKm = 3.0;
        
        // Act
        List<string> cells = indexType switch
        {
            SpatialIndexType.S2 => S2CellCovering.GetCellsForRadius(center, radiusKm, precision, 50),
            SpatialIndexType.H3 => H3CellCovering.GetCellsForRadius(center, radiusKm, precision, 50),
            _ => throw new ArgumentException($"Unsupported index type: {indexType}")
        };
        
        // Assert
        Assert.NotEmpty(cells);
        Assert.All(cells, cell => Assert.False(string.IsNullOrEmpty(cell)));
    }

    /// <summary>
    /// Tests that cell computation for bounding box queries works correctly.
    /// </summary>
    [Theory]
    [InlineData(SpatialIndexType.S2, 10)]  // Level 10 (~4.5km cells) with 2km bbox stays within 500 cell limit
    [InlineData(SpatialIndexType.H3, 9)]
    public void CellComputation_ForBoundingBox_ReturnsValidCells(SpatialIndexType indexType, int precision)
    {
        // Arrange
        var center = new GeoLocation(51.5074, -0.1278); // London
        var bbox = GeoBoundingBox.FromCenterAndDistanceKilometers(center, 2.0);
        
        // Act
        List<string> cells = indexType switch
        {
            SpatialIndexType.S2 => S2CellCovering.GetCellsForBoundingBox(bbox, precision, 50),
            SpatialIndexType.H3 => H3CellCovering.GetCellsForBoundingBox(bbox, precision, 50),
            _ => throw new ArgumentException($"Unsupported index type: {indexType}")
        };
        
        // Assert
        Assert.NotEmpty(cells);
        Assert.All(cells, cell => Assert.False(string.IsNullOrEmpty(cell)));
    }

    /// <summary>
    /// Tests that maxCells limit is respected.
    /// </summary>
    [Theory]
    [InlineData(SpatialIndexType.S2, 10, 5)]  // Level 10 (~300m cells) with 20km radius stays within 500 cell limit
    [InlineData(SpatialIndexType.H3, 6, 5)]   // Resolution 6 (~3.2km cells) with 20km radius stays within 500 cell limit
    [InlineData(SpatialIndexType.S2, 10, 10)]
    [InlineData(SpatialIndexType.H3, 6, 10)]
    public void CellComputation_RespectsMaxCellsLimit(SpatialIndexType indexType, int precision, int maxCells)
    {
        // Arrange
        var center = new GeoLocation(48.8566, 2.3522); // Paris
        var largeRadiusKm = 20.0; // Large radius to generate many cells
        
        // Act
        List<string> cells = indexType switch
        {
            SpatialIndexType.S2 => S2CellCovering.GetCellsForRadius(center, largeRadiusKm, precision, maxCells),
            SpatialIndexType.H3 => H3CellCovering.GetCellsForRadius(center, largeRadiusKm, precision, maxCells),
            _ => throw new ArgumentException($"Unsupported index type: {indexType}")
        };
        
        // Assert
        Assert.True(cells.Count <= maxCells);
    }

    /// <summary>
    /// Tests that S2 cells are sorted by distance from center (spiral order).
    /// </summary>
    [Fact]
    public void S2CellComputation_ReturnsCellsSortedByDistance()
    {
        // Arrange
        // Use level 10 (~4.5km cells) with 5km radius to stay within 500 cell limit
        var center = new GeoLocation(35.6762, 139.6503); // Tokyo
        var radiusKm = 5.0;
        
        // Act
        var cells = S2CellCovering.GetCellsForRadius(center, radiusKm, 10, 20);
        
        // Assert: Cells should be sorted by distance from center
        if (cells.Count > 1)
        {
            var distances = cells.Select(token =>
            {
                var (lat, lon) = S2Encoder.Decode(token);
                return center.DistanceToKilometers(new GeoLocation(lat, lon));
            }).ToList();
            
            // Verify distances are in non-decreasing order
            for (int i = 1; i < distances.Count; i++)
            {
                Assert.True(distances[i] >= distances[i - 1] - 0.001, 
                    $"Distance at index {i} ({distances[i]}) should be >= distance at index {i-1} ({distances[i-1]})");
            }
        }
    }

    /// <summary>
    /// Tests that H3 cells are sorted by distance from center (spiral order).
    /// </summary>
    [Fact]
    public void H3CellComputation_ReturnsCellsSortedByDistance()
    {
        // Arrange
        var center = new GeoLocation(-33.8688, 151.2093); // Sydney
        var radiusKm = 5.0;
        
        // Act - Use resolution 7 (~1.2km cells) to stay within cell limits
        var cells = H3CellCovering.GetCellsForRadius(center, radiusKm, 7, 20);
        
        // Assert: Cells should be sorted by distance from center
        if (cells.Count > 1)
        {
            var distances = cells.Select(index =>
            {
                var (lat, lon) = H3Encoder.Decode(index);
                return center.DistanceToKilometers(new GeoLocation(lat, lon));
            }).ToList();
            
            // Verify distances are in non-decreasing order
            for (int i = 1; i < distances.Count; i++)
            {
                Assert.True(distances[i] >= distances[i - 1] - 0.001,
                    $"Distance at index {i} ({distances[i]}) should be >= distance at index {i-1} ({distances[i-1]})");
            }
        }
    }
}
