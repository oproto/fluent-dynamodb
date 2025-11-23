using Oproto.FluentDynamoDb.Geospatial.H3;

namespace Oproto.FluentDynamoDb.Geospatial.UnitTests.H3;

/// <summary>
/// Tests for H3 neighbor calculation functionality.
/// Validates Requirements 8.3: GetNeighbors returns correct count and level
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
    
    [Fact]
    public void GetNeighbors_NeighborsAreAdjacent()
    {
        // Arrange
        var h3Index = "821fbffffffffff";  // Resolution 2, base cell 15 (hexagon)
        var (centerLat, centerLon) = H3Encoder.Decode(h3Index);
        
        // Act
        var neighbors = H3Encoder.GetNeighbors(h3Index);
        
        // Assert
        foreach (var neighbor in neighbors)
        {
            var (neighborLat, neighborLon) = H3Encoder.Decode(neighbor);
            
            // Calculate approximate distance (simple Euclidean for small distances)
            var latDiff = neighborLat - centerLat;
            var lonDiff = neighborLon - centerLon;
            var distance = Math.Sqrt(latDiff * latDiff + lonDiff * lonDiff);
            
            // At resolution 2, cells are roughly 1-2 degrees apart
            // Neighbors should be within reasonable distance
            Assert.True(distance < 5.0, "neighbors should be adjacent to the original cell");
        }
    }
    
    [Fact]
    public void GetNeighbors_DifferentResolutions_ReturnsCorrectCount()
    {
        // Test at multiple resolutions to ensure consistency
        var testCases = new[]
        {
            ("811fbffffffffff", 6),  // Resolution 1, hexagon (base cell 15)
            ("821fbffffffffff", 6),  // Resolution 2, hexagon (base cell 15)
            ("831fbffffffffff", 6),  // Resolution 3, hexagon (base cell 15)
            ("8109bffffffffff", 5),  // Resolution 1, pentagon (base cell 4)
            ("821c07fffffffff", 5),  // Resolution 2, pentagon (base cell 14)
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
        // Arrange: Test at a higher resolution with a hexagon cell (base cell 15)
        var h3Index = "891fbffffffffff";  // Resolution 9, base cell 15 (hexagon)
        
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
        var h3Index = "821fbffffffffff";  // Resolution 2, base cell 15 (hexagon)
        
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
