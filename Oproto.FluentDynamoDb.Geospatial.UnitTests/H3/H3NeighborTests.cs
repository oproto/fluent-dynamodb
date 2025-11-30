using Oproto.FluentDynamoDb.Geospatial.H3;

namespace Oproto.FluentDynamoDb.Geospatial.UnitTests.H3;

/// <summary>
/// Tests for H3 neighbor calculation functionality.
/// </summary>
public class H3NeighborTests
{
    [Fact]
    public void GetNeighbors_HexagonCell_ReturnsSixNeighbors()
    {
        // Arrange: Use a non-pentagon base cell (base cell 0)
        // Resolution 1 cell in base cell 0
        var h3Index = "811fbffffffffff";  // Base cell 0, resolution 1
        
        // Act
        var neighbors = H3Encoder.GetNeighbors(h3Index);
        
        // Assert
        Assert.NotNull(neighbors);
        Assert.Equal(6, neighbors.Length);  // hexagon cells should have 6 neighbors
        Assert.Equal(neighbors.Length, neighbors.Distinct().Count());  // all neighbors should be distinct
    }
    
    [Fact]
    public void GetNeighbors_PentagonCell_ReturnsFiveNeighbors()
    {
        // Arrange: Use a pentagon base cell (base cell 4)
        // Resolution 1 cell in base cell 4 (pentagon)
        var h3Index = "8109bffffffffff";  // Base cell 4, resolution 1, digit 2
        
        // Act
        var neighbors = H3Encoder.GetNeighbors(h3Index);
        
        // Assert
        Assert.NotNull(neighbors);
        Assert.Equal(5, neighbors.Length);  // pentagon cells should have 5 neighbors (one direction is deleted)
        Assert.Equal(neighbors.Length, neighbors.Distinct().Count());  // all neighbors should be distinct
    }
    
    [Fact]
    public void GetNeighbors_AllNeighborsAreValid()
    {
        // Arrange
        var h3Index = "811fbffffffffff";  // Base cell 0, resolution 1
        
        // Act
        var neighbors = H3Encoder.GetNeighbors(h3Index);
        
        // Assert
        foreach (var neighbor in neighbors)
        {
            // Each neighbor should be a valid H3 index that can be decoded
            var (lat, lon) = H3Encoder.Decode(neighbor);
            Assert.InRange(lat, -90, 90);
            Assert.InRange(lon, -180, 180);
        }
    }
    
    [Fact]
    public void GetNeighbors_AllNeighborsAtSameResolution()
    {
        // Arrange
        var h3Index = "821fbffffffffff";  // Resolution 2, base cell 15 (hexagon)
        var originalResolution = GetResolution(h3Index);
        
        // Act
        var neighbors = H3Encoder.GetNeighbors(h3Index);
        
        // Assert
        foreach (var neighbor in neighbors)
        {
            var neighborResolution = GetResolution(neighbor);
            Assert.Equal(originalResolution, neighborResolution);  // all neighbors should be at the same resolution
        }
    }
    
    [Theory]
    [InlineData("821fbffffffffff", 2)]  // Resolution 2, base cell 15 (hexagon)
    [InlineData("89283082803ffff", 9)]  // Resolution 9, San Francisco (the failing case we found)
    [InlineData("8c283082803ffff", 12)]  // Resolution 12, San Francisco
    [InlineData("8f283082803ffff", 15)]  // Resolution 15, San Francisco
    public void GetNeighbors_NeighborsAreAdjacent(string h3Index, int resolution)
    {
        // Arrange
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
        
        // Assert
        foreach (var neighbor in neighbors)
        {
            var (neighborLat, neighborLon) = H3Encoder.Decode(neighbor);
            var neighborLocation = new GeoLocation(neighborLat, neighborLon);
            
            // Calculate actual geographic distance using Haversine formula
            var distanceKm = centerLocation.DistanceToKilometers(neighborLocation);
            
            // Neighbors should be adjacent (within 2 * cellSize)
            Assert.True(
                distanceKm <= maxDistanceKm,
                $"Neighbor at ({neighborLat:F6}, {neighborLon:F6}) is {distanceKm:F3} km away from center " +
                $"at ({centerLat:F6}, {centerLon:F6}), which exceeds the maximum expected distance of " +
                $"{maxDistanceKm:F3} km for resolution {resolution} (cell size: {cellSizeKm:F3} km). " +
                $"This indicates the neighbor is NOT adjacent to the original cell.");
        }
    }
    
    /// <summary>
    /// Tests the specific failing case from property test at resolution 12.
    /// Location: (-48.77, -64.69) in South America
    /// </summary>
    [Fact]
    public void GetNeighbors_Resolution12_SouthAmerica_NeighborsAreAdjacent()
    {
        // Arrange: The specific location that failed in property tests
        var lat = -48.7690920710396;
        var lon = -64.68905493051608;
        var resolution = 12;
        
        var h3Index = H3Encoder.Encode(lat, lon, resolution);
        var (centerLat, centerLon) = H3Encoder.Decode(h3Index);
        var centerLocation = new GeoLocation(centerLat, centerLon);
        
        // Calculate expected cell size for this resolution
        var cellSizeKm = GetApproximateCellSizeKm(resolution);
        var maxDistanceKm = 2.5 * cellSizeKm;
        
        // Act
        var neighbors = H3Encoder.GetNeighbors(h3Index);
        
        // Assert
        Assert.NotNull(neighbors);
        Assert.Equal(6, neighbors.Length);  // Should have 6 neighbors (hexagon)
        
        foreach (var neighbor in neighbors)
        {
            var (neighborLat, neighborLon) = H3Encoder.Decode(neighbor);
            var neighborLocation = new GeoLocation(neighborLat, neighborLon);
            
            // Calculate actual geographic distance
            var distanceKm = centerLocation.DistanceToKilometers(neighborLocation);
            
            // Neighbors should be adjacent (within expected distance)
            Assert.True(
                distanceKm <= maxDistanceKm,
                $"Neighbor {neighbor} at ({neighborLat:F6}, {neighborLon:F6}) is {distanceKm:F3} km away " +
                $"from center at ({centerLat:F6}, {centerLon:F6}). Expected distance <= {maxDistanceKm:F3} km. " +
                $"This neighbor is NOT adjacent!");
        }
    }
    
    /// <summary>
    /// Tests the specific failing case from property test at resolution 15.
    /// Location: (-35.42, -148.47) in Pacific Ocean
    /// </summary>
    [Fact]
    public void GetNeighbors_Resolution15_PacificOcean_NeighborsAreAdjacent()
    {
        // Arrange: The specific location that failed in property tests
        var lat = -35.42443666882222;
        var lon = -148.47138881986947;
        var resolution = 15;
        
        var h3Index = H3Encoder.Encode(lat, lon, resolution);
        var (centerLat, centerLon) = H3Encoder.Decode(h3Index);
        var centerLocation = new GeoLocation(centerLat, centerLon);
        
        // Calculate expected cell size for this resolution
        var cellSizeKm = GetApproximateCellSizeKm(resolution);
        var maxDistanceKm = 2.5 * cellSizeKm;
        
        // Act
        var neighbors = H3Encoder.GetNeighbors(h3Index);
        
        // Assert
        Assert.NotNull(neighbors);
        Assert.Equal(6, neighbors.Length);  // Should have 6 neighbors (hexagon)
        
        foreach (var neighbor in neighbors)
        {
            var (neighborLat, neighborLon) = H3Encoder.Decode(neighbor);
            var neighborLocation = new GeoLocation(neighborLat, neighborLon);
            
            // Calculate actual geographic distance
            var distanceKm = centerLocation.DistanceToKilometers(neighborLocation);
            
            // Neighbors should be adjacent (within expected distance)
            Assert.True(
                distanceKm <= maxDistanceKm,
                $"Neighbor {neighbor} at ({neighborLat:F6}, {neighborLon:F6}) is {distanceKm:F3} km away " +
                $"from center at ({centerLat:F6}, {centerLon:F6}). Expected distance <= {maxDistanceKm:F3} km. " +
                $"This neighbor is NOT adjacent!");
        }
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
    
    [Fact]
    public void GetNeighbors_DifferentResolutions_ReturnsCorrectCount()
    {
        // Test at multiple resolutions to ensure consistency
        // Use valid H3 indices generated by encoding actual coordinates
        // Note: The old test data (821fbffffffffff, 831fbffffffffff) had INVALID_DIGIT (7) 
        // at their resolution level, making them invalid H3 cells.
        
        // Generate valid cells at different resolutions
        var lat = 47.984658;  // Location in Europe (base cell 15 area)
        var lon = 6.917933;
        
        var testCases = new[]
        {
            (H3Encoder.Encode(lat, lon, 1), 6),  // Resolution 1, hexagon
            (H3Encoder.Encode(lat, lon, 2), 6),  // Resolution 2, hexagon
            (H3Encoder.Encode(lat, lon, 3), 6),  // Resolution 3, hexagon
            (H3Encoder.Encode(59.604145, 4.836943, 1), 5),  // Resolution 1, pentagon (base cell 4 area)
            (H3Encoder.Encode(50.103201, -143.478490, 2), 5),  // Resolution 2, pentagon (base cell 14 area)
        };
        
        foreach (var (h3Index, expectedCount) in testCases)
        {
            // Act
            var neighbors = H3Encoder.GetNeighbors(h3Index);
            
            // Assert
            Assert.Equal(expectedCount, neighbors.Length);
        }
    }
    
    [Fact]
    public void GetNeighbors_NullIndex_ThrowsArgumentException()
    {
        // Act & Assert
        Assert.Throws<ArgumentException>(() => H3Encoder.GetNeighbors(null!));
    }
    
    [Fact]
    public void GetNeighbors_EmptyIndex_ThrowsArgumentException()
    {
        // Act & Assert
        Assert.Throws<ArgumentException>(() => H3Encoder.GetNeighbors(string.Empty));
    }
    
    [Fact]
    public void GetNeighbors_Resolution0_ReturnsNeighbors()
    {
        // Arrange: Base cell 0 at resolution 0
        var h3Index = "8001fffffffffff";  // Base cell 0, resolution 0
        
        // Act
        var neighbors = H3Encoder.GetNeighbors(h3Index);
        
        // Assert
        Assert.NotNull(neighbors);
        Assert.Equal(6, neighbors.Length);  // resolution 0 hexagon should have 6 neighbors
    }
    
    [Fact]
    public void GetNeighbors_HighResolution_ReturnsNeighbors()
    {
        // Arrange: Test at a higher resolution with a hexagon cell
        // Generate a valid resolution 9 cell by encoding actual coordinates
        var h3Index = H3Encoder.Encode(47.984658, 6.917933, 9);  // Resolution 9, hexagon
        
        // Act
        var neighbors = H3Encoder.GetNeighbors(h3Index);
        
        // Assert
        Assert.NotNull(neighbors);
        Assert.Equal(6, neighbors.Length);  // high resolution hexagon should have 6 neighbors
        Assert.Equal(neighbors.Length, neighbors.Distinct().Count());  // all unique
    }
    
    [Fact]
    public void GetNeighbors_CellNearFaceBoundary_HandlesOverage()
    {
        // Arrange: Use a hexagon cell that's likely near a face boundary
        // This tests the AdjustOverageClassII functionality
        // Generate a valid resolution 2 cell near a face boundary
        var h3Index = H3Encoder.Encode(47.984658, 6.917933, 2);  // Resolution 2, hexagon
        
        // Act
        var neighbors = H3Encoder.GetNeighbors(h3Index);
        
        // Assert
        Assert.NotNull(neighbors);
        Assert.Equal(6, neighbors.Length);
        
        // All neighbors should be valid and decodable
        foreach (var neighbor in neighbors)
        {
            var (lat, lon) = H3Encoder.Decode(neighbor);
            Assert.InRange(lat, -90, 90);
            Assert.InRange(lon, -180, 180);
        }
    }
    
    /// <summary>
    /// Tests the specific failing case discovered during performance testing.
    /// Cell 89283082803ffff is in San Francisco at resolution 9.
    /// This test verifies that all neighbors are actually near San Francisco,
    /// not thousands of kilometers away.
    /// </summary>
    [Fact]
    public void GetNeighbors_SanFranciscoCell_AllNeighborsNearby()
    {
        // Arrange: The specific cell that was failing in performance tests
        var h3Index = "89283082803ffff";  // San Francisco, resolution 9
        var (centerLat, centerLon) = H3Encoder.Decode(h3Index);
        var centerLocation = new GeoLocation(centerLat, centerLon);
        
        // Verify the center is actually in San Francisco
        Assert.InRange(centerLat, 37.0, 38.0);  // San Francisco latitude range
        Assert.InRange(centerLon, -123.0, -122.0);  // San Francisco longitude range
        
        // Resolution 9 cells are approximately 174 meters
        var cellSizeKm = GetApproximateCellSizeKm(9);
        var maxDistanceKm = 2.5 * cellSizeKm;  // Neighbors should be within 2.5 * cellSize
        
        // Act
        var neighbors = H3Encoder.GetNeighbors(h3Index);
        
        // Assert
        Assert.NotNull(neighbors);
        Assert.Equal(6, neighbors.Length);  // Should have 6 neighbors (hexagon)
        
        // Verify ALL neighbors are near San Francisco, not thousands of km away
        foreach (var neighbor in neighbors)
        {
            var (neighborLat, neighborLon) = H3Encoder.Decode(neighbor);
            var neighborLocation = new GeoLocation(neighborLat, neighborLon);
            
            // Calculate actual geographic distance
            var distanceKm = centerLocation.DistanceToKilometers(neighborLocation);
            
            // Verify neighbor is in San Francisco area (not in Canada or elsewhere)
            Assert.InRange(neighborLat, 37.0, 38.0);
            Assert.InRange(neighborLon, -123.0, -122.0);
            
            // Verify neighbor is adjacent (within expected distance)
            Assert.True(
                distanceKm <= maxDistanceKm,
                $"Neighbor {neighbor} at ({neighborLat:F6}, {neighborLon:F6}) is {distanceKm:F3} km away " +
                $"from center at ({centerLat:F6}, {centerLon:F6}). Expected distance <= {maxDistanceKm:F3} km. " +
                $"This neighbor is NOT adjacent - it's thousands of km away!");
            
            // Extra check: no neighbor should be more than 1 km away for resolution 9
            Assert.True(
                distanceKm < 1.0,
                $"Neighbor {neighbor} is {distanceKm:F3} km away, which is way too far for resolution 9 " +
                $"(cell size ~0.174 km). This indicates GetNeighbors() is returning wrong cell indices.");
        }
    }
    
    /// <summary>
    /// Tests neighbor symmetry: if B is a neighbor of A, then A should be a neighbor of B.
    /// Tests with multiple resolutions and locations.
    /// </summary>
    [Theory]
    [InlineData("811fbffffffffff")]  // Resolution 1, base cell 15 (hexagon)
    [InlineData("821fbffffffffff")]  // Resolution 2, base cell 15 (hexagon)
    [InlineData("89283082803ffff")]  // Resolution 9, San Francisco (hexagon)
    [InlineData("8109bffffffffff")]  // Resolution 1, base cell 4 (pentagon)
    [InlineData("891fbffffffffff")]  // Resolution 9, base cell 15 (hexagon)
    public void GetNeighbors_NeighborSymmetry_BidirectionalRelationship(string h3Index)
    {
        // Arrange
        var neighborsOfA = H3Encoder.GetNeighbors(h3Index);
        
        // Act & Assert
        foreach (var neighborB in neighborsOfA)
        {
            // Get neighbors of B
            var neighborsOfB = H3Encoder.GetNeighbors(neighborB);
            
            // If B is a neighbor of A, then A should be a neighbor of B
            Assert.Contains(h3Index, neighborsOfB);
        }
    }
    
    /// <summary>
    /// Helper method to extract resolution from H3 index string.
    /// </summary>
    private static int GetResolution(string h3Index)
    {
        // H3 index format: bits 52-55 contain the resolution
        var index = Convert.ToUInt64(h3Index, 16);
        var resolution = (int)((index >> 52) & 0xF);
        return resolution;
    }
}
