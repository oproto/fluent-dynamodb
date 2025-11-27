using FsCheck;
using FsCheck.Xunit;
using Oproto.FluentDynamoDb.Geospatial.H3;

namespace Oproto.FluentDynamoDb.Geospatial.UnitTests.H3;

/// <summary>
/// Property-based tests for H3 neighbor calculation functionality.
/// Each test runs 100 iterations with random inputs to verify universal properties.
/// </summary>
public class H3NeighborPropertyTests
{
    /// <summary>
    /// Feature: cell-covering-algorithm-fix, Property 0: H3 GetNeighbors returns adjacent cells
    /// Validates: Requirements 1.1, 1.2, 1.3, 2.1, 2.3
    /// 
    /// For any valid H3 cell index, when calling GetNeighbors(), all returned neighbor cell indices
    /// should decode to locations that are adjacent to the input cell (within 2 * cellSize distance).
    /// </summary>
    [Property(MaxTest = 100, Arbitrary = new[] { typeof(ValidH3CellArbitraries) })]
    public Property GetNeighbors_ReturnsAdjacentCells(ValidH3Cell cell)
    {
        // Arrange
        var h3Index = cell.Index;
        var resolution = cell.Resolution;
        
        // Decode the center cell
        var (centerLat, centerLon) = H3Encoder.Decode(h3Index);
        var centerLocation = new GeoLocation(centerLat, centerLon);
        
        // Calculate expected cell size for this resolution
        var cellSizeKm = GetApproximateCellSizeKm(resolution);
        
        // Neighbors should be within 2.5 * cellSize distance (accounting for hexagonal geometry)
        // The factor of 2.5 accounts for the fact that hexagonal neighbors can be slightly
        // farther than 2 * cellSize due to the hexagonal grid structure
        var maxDistanceKm = 2.5 * cellSizeKm;
        
        // Act
        var neighbors = H3Encoder.GetNeighbors(h3Index);
        
        // Assert: Check neighbor count
        var isPentagon = IsPentagonCell(h3Index);
        var expectedCount = isPentagon ? 5 : 6;
        var correctCount = neighbors.Length == expectedCount;
        
        if (!correctCount)
        {
            return false.ToProperty()
                .Label($"Expected {expectedCount} neighbors for {(isPentagon ? "pentagon" : "hexagon")} " +
                       $"cell at resolution {resolution}, but got {neighbors.Length}");
        }
        
        // Assert: All neighbors should be unique
        var allUnique = neighbors.Length == neighbors.Distinct().Count();
        if (!allUnique)
        {
            return false.ToProperty()
                .Label($"Neighbors contain duplicates for cell {h3Index} at resolution {resolution}");
        }
        
        // Assert: All neighbors should be adjacent (within expected distance)
        var allAdjacent = true;
        var failureMessage = "";
        
        foreach (var neighbor in neighbors)
        {
            var (neighborLat, neighborLon) = H3Encoder.Decode(neighbor);
            var neighborLocation = new GeoLocation(neighborLat, neighborLon);
            
            // Calculate actual geographic distance using Haversine formula
            var distanceKm = centerLocation.DistanceToKilometers(neighborLocation);
            
            if (distanceKm > maxDistanceKm)
            {
                allAdjacent = false;
                failureMessage = $"Neighbor at ({neighborLat:F6}, {neighborLon:F6}) is {distanceKm:F3} km away " +
                                $"from center at ({centerLat:F6}, {centerLon:F6}), which exceeds the maximum " +
                                $"expected distance of {maxDistanceKm:F3} km for resolution {resolution} " +
                                $"(cell size: {cellSizeKm:F3} km). This indicates the neighbor is NOT adjacent " +
                                $"to the original cell.";
                break;
            }
        }
        
        return allAdjacent.ToProperty()
            .Label(string.IsNullOrEmpty(failureMessage)
                ? $"All {neighbors.Length} neighbors are adjacent for cell at resolution {resolution}"
                : failureMessage);
    }
    
    /// <summary>
    /// Gets the approximate cell size in kilometers for a given H3 resolution.
    /// This matches the calculation in H3CellCovering.
    /// </summary>
    private static double GetApproximateCellSizeKm(int resolution)
    {
        // H3 cell edge lengths (approximate):
        // Resolution 0: ~1107 km
        // Resolution 5: ~8.5 km
        // Resolution 9: ~174 m = 0.174 km
        // Resolution 15: ~0.5 m
        
        // Approximate formula based on H3 documentation
        // Edge length ≈ 1107 / (7^(resolution/2))
        return 1107.0 / Math.Pow(7, resolution / 2.0);
    }
    
    /// <summary>
    /// Checks if an H3 cell is a pentagon.
    /// Pentagon cells are at base cells 4, 14, 24, 38, 49, 58, 63, 72, 83, 97, 107, 117.
    /// </summary>
    private static bool IsPentagonCell(string h3Index)
    {
        // Pentagon base cells in H3
        var pentagonBaseCells = new HashSet<int> { 4, 14, 24, 38, 49, 58, 63, 72, 83, 97, 107, 117 };
        
        // Extract base cell from H3 index
        var index = Convert.ToUInt64(h3Index, 16);
        var baseCell = (int)((index >> 45) & 0x7F); // Bits 45-51 contain the base cell
        
        return pentagonBaseCells.Contains(baseCell);
    }
}

/// <summary>
/// Wrapper for valid H3 cell indices with their resolution
/// </summary>
public record ValidH3Cell(string Index, int Resolution);

/// <summary>
/// Custom arbitraries for generating valid H3 cell indices
/// </summary>
public static class ValidH3CellArbitraries
{
    /// <summary>
    /// Generates valid H3 cell indices at various resolutions.
    /// Uses random geographic coordinates and encodes them to H3 cells.
    /// </summary>
    public static Arbitrary<ValidH3Cell> H3Cell()
    {
        return (from lat in ValidGeoArbitraries.Latitude().Generator
                from lon in ValidGeoArbitraries.Longitude().Generator
                from res in ValidGeoArbitraries.H3Resolution().Generator
                select new ValidH3Cell(
                    H3Encoder.Encode(lat.Value, lon.Value, res.Value),
                    res.Value))
            .ToArbitrary();
    }
}
