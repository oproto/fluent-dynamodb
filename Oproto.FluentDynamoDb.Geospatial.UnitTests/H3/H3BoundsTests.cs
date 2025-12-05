using Oproto.FluentDynamoDb.Geospatial.H3;
using Xunit;

namespace Oproto.FluentDynamoDb.Geospatial.UnitTests.H3;

/// <summary>
/// Tests for H3 bounds decoding functionality.
/// Validates that DecodeBounds correctly calculates the bounding box for H3 cells.
/// </summary>
public class H3BoundsTests
{
    [Fact]
    public void DecodeBounds_Resolution0_ReturnsValidBounds()
    {
        // Arrange - Use a known resolution 0 cell (base cell)
        // Base cell 0 is centered around (79.2°N, 38.0°E) approximately
        var h3Index = "8001fffffffffff";  // Base cell 0
        
        // Act
        var (minLat, maxLat, minLon, maxLon) = H3Encoder.DecodeBounds(h3Index);
        
        // Assert
        Assert.True(minLat < maxLat);
        Assert.True(minLon < maxLon);
        
        // Bounds should be reasonable (not at poles or wrapping around)
        Assert.InRange(minLat, -90, 90);
        Assert.InRange(maxLat, -90, 90);
        Assert.InRange(minLon, -180, 180);
        Assert.InRange(maxLon, -180, 180);
        
        // The bounds should span a reasonable area for resolution 0
        // Resolution 0 cells are very large (thousands of km)
        Assert.True((maxLat - minLat) > 10);  // At least 10 degrees
        Assert.True((maxLon - minLon) > 10);
    }
    
    [Fact]
    public void DecodeBounds_Resolution5_ReturnsValidBounds()
    {
        // Arrange - San Francisco at resolution 5
        var h3Index = "85283473fffffff";
        
        // Act
        var (minLat, maxLat, minLon, maxLon) = H3Encoder.DecodeBounds(h3Index);
        
        // Assert
        Assert.True(minLat < maxLat);
        Assert.True(minLon < maxLon);
        
        // Bounds should be in the San Francisco area
        Assert.InRange(minLat, 35, 40);
        Assert.InRange(maxLat, 35, 40);
        Assert.InRange(minLon, -125, -120);
        Assert.InRange(maxLon, -125, -120);
        
        // Resolution 5 cells are ~20km across, so bounds should be reasonable
        Assert.InRange(maxLat - minLat, 0.1, 5);
        Assert.InRange(maxLon - minLon, 0.1, 5);
    }
    
    [Fact]
    public void DecodeBounds_Resolution9_ReturnsValidBounds()
    {
        // Arrange - San Francisco at resolution 9
        var h3Index = "8928308280fffff";
        
        // Act
        var (minLat, maxLat, minLon, maxLon) = H3Encoder.DecodeBounds(h3Index);
        
        // Assert
        Assert.True(minLat < maxLat);
        Assert.True(minLon < maxLon);
        
        // Bounds should be in the San Francisco area
        Assert.InRange(minLat, 37, 38);
        Assert.InRange(maxLat, 37, 38);
        Assert.InRange(minLon, -123, -122);
        Assert.InRange(maxLon, -123, -122);
        
        // Resolution 9 cells are ~174m across, so bounds should be small
        Assert.InRange(maxLat - minLat, 0.001, 0.01);
        Assert.InRange(maxLon - minLon, 0.001, 0.01);
    }
    
    [Fact]
    public void DecodeBounds_CellCenter_IsWithinBounds()
    {
        // Arrange
        var h3Index = "8928308280fffff";
        
        // Act
        var (centerLat, centerLon) = H3Encoder.Decode(h3Index);
        var (minLat, maxLat, minLon, maxLon) = H3Encoder.DecodeBounds(h3Index);
        
        // Assert - The center point should be within the bounds
        Assert.InRange(centerLat, minLat, maxLat);
        Assert.InRange(centerLon, minLon, maxLon);
    }
    
    [Fact]
    public void DecodeBounds_Pentagon_ReturnsValidBounds()
    {
        // Arrange - Pentagon base cell 4
        var h3Index = "8009fffffffffff";  // Base cell 4 (pentagon)
        
        // Act
        var (minLat, maxLat, minLon, maxLon) = H3Encoder.DecodeBounds(h3Index);
        
        // Assert
        Assert.True(minLat < maxLat);
        Assert.True(minLon < maxLon);
        
        // Bounds should be valid
        Assert.InRange(minLat, -90, 90);
        Assert.InRange(maxLat, -90, 90);
        Assert.InRange(minLon, -180, 180);
        Assert.InRange(maxLon, -180, 180);
        
        // Pentagon should have 5 vertices, so bounds should still be reasonable
        Assert.True((maxLat - minLat) > 10);
    }
    
    [Fact]
    public void DecodeBounds_MultipleResolutions_BoundsGetSmaller()
    {
        // Arrange - Same location at different resolutions
        var res0 = "8001fffffffffff";  // Resolution 0
        var res5 = "85283473fffffff";  // Resolution 5
        var res9 = "8928308280fffff";  // Resolution 9
        
        // Act
        var (minLat0, maxLat0, minLon0, maxLon0) = H3Encoder.DecodeBounds(res0);
        var (minLat5, maxLat5, minLon5, maxLon5) = H3Encoder.DecodeBounds(res5);
        var (minLat9, maxLat9, minLon9, maxLon9) = H3Encoder.DecodeBounds(res9);
        
        var area0 = (maxLat0 - minLat0) * (maxLon0 - minLon0);
        var area5 = (maxLat5 - minLat5) * (maxLon5 - minLon5);
        var area9 = (maxLat9 - minLat9) * (maxLon9 - minLon9);
        
        // Assert - Higher resolution cells should have smaller bounds
        Assert.True(area0 > area5);
        Assert.True(area5 > area9);
    }
    
    [Fact]
    public void DecodeBounds_NullIndex_ThrowsArgumentException()
    {
        // Act & Assert
        Assert.Throws<ArgumentException>(() => H3Encoder.DecodeBounds(null!));
    }
    
    [Fact]
    public void DecodeBounds_EmptyIndex_ThrowsArgumentException()
    {
        // Act & Assert
        Assert.Throws<ArgumentException>(() => H3Encoder.DecodeBounds(string.Empty));
    }
    
    [Fact]
    public void DecodeBounds_InvalidIndex_ThrowsArgumentException()
    {
        // Act & Assert
        Assert.Throws<ArgumentException>(() => H3Encoder.DecodeBounds("invalid"));
    }
}
