using Oproto.FluentDynamoDb.Geospatial.GeoHash;

namespace Oproto.FluentDynamoDb.Geospatial.UnitTests.GeoHash;

public class GeoHashCellTests
{
    [Fact]
    public void Constructor_WithValidGeoHash_CreatesCell()
    {
        // Arrange
        var hash = "9q8yy9";

        // Act
        var cell = new GeoHashCell(hash);

        // Assert
        cell.Hash.Should().Be(hash);
        cell.Precision.Should().Be(6);
        cell.Bounds.Southwest.Latitude.Should().BeLessThan(cell.Bounds.Northeast.Latitude);
        cell.Bounds.Southwest.Longitude.Should().BeLessThan(cell.Bounds.Northeast.Longitude);
    }

    [Fact]
    public void Constructor_WithLocation_CreatesCell()
    {
        // Arrange
        var location = new GeoLocation(37.7749, -122.4194);
        var precision = 6;

        // Act
        var cell = new GeoHashCell(location, precision);

        // Assert
        cell.Hash.Should().NotBeNullOrEmpty();
        cell.Precision.Should().Be(precision);
        cell.Hash.Length.Should().Be(precision);
    }

    [Fact]
    public void Constructor_WithNullGeoHash_ThrowsArgumentException()
    {
        // Act & Assert
        var exception = Assert.Throws<ArgumentException>(() => new GeoHashCell(null!));
        exception.ParamName.Should().Be("hash");
        exception.Message.Should().Contain("GeoHash string cannot be null or empty");
    }

    [Fact]
    public void Constructor_WithEmptyGeoHash_ThrowsArgumentException()
    {
        // Act & Assert
        var exception = Assert.Throws<ArgumentException>(() => new GeoHashCell(string.Empty));
        exception.ParamName.Should().Be("hash");
        exception.Message.Should().Contain("GeoHash string cannot be null or empty");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(13)]
    [InlineData(100)]
    public void Constructor_WithInvalidPrecision_ThrowsArgumentOutOfRangeException(int precision)
    {
        // Arrange
        var location = new GeoLocation(37.7749, -122.4194);

        // Act & Assert
        var exception = Assert.Throws<ArgumentOutOfRangeException>(
            () => new GeoHashCell(location, precision));
        exception.ParamName.Should().Be("precision");
        exception.Message.Should().Contain("Precision must be between 1 and 12");
    }

    [Fact]
    public void Precision_ReturnsCorrectValue()
    {
        // Arrange
        var hash = "9q8yy9r";

        // Act
        var cell = new GeoHashCell(hash);

        // Assert
        cell.Precision.Should().Be(7);
    }

    [Fact]
    public void Bounds_ContainsCenterPoint()
    {
        // Arrange
        var location = new GeoLocation(37.7749, -122.4194);
        var cell = new GeoHashCell(location, 6);

        // Act
        var bounds = cell.Bounds;

        // Assert
        bounds.Contains(location).Should().BeTrue();
    }

    [Fact]
    public void GetNeighbors_ReturnsEightNeighbors()
    {
        // Arrange
        var cell = new GeoHashCell("9q8yy9");

        // Act
        var neighbors = cell.GetNeighbors();

        // Assert
        neighbors.Length.Should().Be(8);
        foreach (var n in neighbors) n.Precision.Should().Be(cell.Precision);
    }

    [Fact]
    public void GetNeighbors_AllNeighborsAreDifferent()
    {
        // Arrange
        var cell = new GeoHashCell("9q8yy9");

        // Act
        var neighbors = cell.GetNeighbors();

        // Assert
        var hashes = neighbors.Select(n => n.Hash).ToArray();
        hashes.Distinct().Count().Should().Be(8);
        foreach (var h in hashes) h.Should().NotBe(cell.Hash);
    }

    [Fact]
    public void GetNeighbors_NeighborsAreAdjacent()
    {
        // Arrange
        var cell = new GeoHashCell("9q8yy9");
        var cellCenter = cell.Bounds.Center;

        // Act
        var neighbors = cell.GetNeighbors();

        // Assert - All neighbors should be close to the original cell
        foreach (var neighbor in neighbors)
        {
            var neighborCenter = neighbor.Bounds.Center;
            var distance = cellCenter.DistanceToMeters(neighborCenter);
            
            // Neighbors should be within reasonable distance (precision 6 is ~0.61km cells)
            distance.Should().BeLessThan(2000); // Less than 2km
        }
    }

    [Fact]
    public void GetParent_ReturnsParentWithLowerPrecision()
    {
        // Arrange
        var cell = new GeoHashCell("9q8yy9");

        // Act
        var parent = cell.GetParent();

        // Assert
        parent.Precision.Should().Be(cell.Precision - 1);
        parent.Hash.Should().Be("9q8yy");
        cell.Hash.Should().StartWith(parent.Hash);
    }

    [Fact]
    public void GetParent_ParentBoundsContainChildBounds()
    {
        // Arrange
        var cell = new GeoHashCell("9q8yy9");

        // Act
        var parent = cell.GetParent();

        // Assert
        parent.Bounds.Contains(cell.Bounds.Southwest).Should().BeTrue();
        parent.Bounds.Contains(cell.Bounds.Northeast).Should().BeTrue();
        parent.Bounds.Contains(cell.Bounds.Center).Should().BeTrue();
    }

    [Fact]
    public void GetParent_WithPrecisionOne_ThrowsInvalidOperationException()
    {
        // Arrange
        var cell = new GeoHashCell("9");

        // Act & Assert
        var exception = Assert.Throws<InvalidOperationException>(() => cell.GetParent());
        exception.Message.Should().Contain("Cannot get parent for a GeoHash cell with precision 1");
    }

    [Fact]
    public void GetChildren_ReturnsThirtyTwoChildren()
    {
        // Arrange
        var cell = new GeoHashCell("9q8yy");

        // Act
        var children = cell.GetChildren();

        // Assert
        children.Length.Should().Be(32);
        foreach (var c in children) c.Precision.Should().Be(cell.Precision + 1);
    }

    [Fact]
    public void GetChildren_AllChildrenAreDifferent()
    {
        // Arrange
        var cell = new GeoHashCell("9q8yy");

        // Act
        var children = cell.GetChildren();

        // Assert
        var hashes = children.Select(c => c.Hash).ToArray();
        hashes.Distinct().Count().Should().Be(32);
    }

    [Fact]
    public void GetChildren_AllChildrenStartWithParentHash()
    {
        // Arrange
        var cell = new GeoHashCell("9q8yy");

        // Act
        var children = cell.GetChildren();

        // Assert
        children.Should().AllSatisfy(c => c.Hash.StartsWith(cell.Hash));
    }

    [Fact]
    public void GetChildren_ChildBoundsAreWithinParentBounds()
    {
        // Arrange
        var cell = new GeoHashCell("9q8yy");

        // Act
        var children = cell.GetChildren();

        // Assert
        foreach (var child in children)
        {
            cell.Bounds.Contains(child.Bounds.Southwest).Should().BeTrue();
            cell.Bounds.Contains(child.Bounds.Northeast).Should().BeTrue();
            cell.Bounds.Contains(child.Bounds.Center).Should().BeTrue();
        }
    }

    [Fact]
    public void GetChildren_WithMaxPrecision_ThrowsInvalidOperationException()
    {
        // Arrange
        var cell = new GeoHashCell("9q8yy9r12345");

        // Act & Assert
        var exception = Assert.Throws<InvalidOperationException>(() => cell.GetChildren());
        exception.Message.Should().Contain("Cannot get children for a GeoHash cell with precision 12");
    }

    [Fact]
    public void ToString_ReturnsGeoHashString()
    {
        // Arrange
        var hash = "9q8yy9";
        var cell = new GeoHashCell(hash);

        // Act
        var result = cell.ToString();

        // Assert
        result.Should().Be(hash);
    }

    [Fact]
    public void Constructor_WithDifferentPrecisions_CreatesCorrectCells()
    {
        // Arrange
        var location = new GeoLocation(37.7749, -122.4194);

        // Act
        var cell5 = new GeoHashCell(location, 5);
        var cell6 = new GeoHashCell(location, 6);
        var cell7 = new GeoHashCell(location, 7);

        // Assert
        cell5.Precision.Should().Be(5);
        cell6.Precision.Should().Be(6);
        cell7.Precision.Should().Be(7);
        
        // Higher precision should start with lower precision
        cell6.Hash.Should().StartWith(cell5.Hash);
        cell7.Hash.Should().StartWith(cell6.Hash);
    }

    [Fact]
    public void GetParent_MultipleGenerations_WorksCorrectly()
    {
        // Arrange
        var cell = new GeoHashCell("9q8yy9r");

        // Act
        var parent1 = cell.GetParent();
        var parent2 = parent1.GetParent();
        var parent3 = parent2.GetParent();

        // Assert
        parent1.Hash.Should().Be("9q8yy9");
        parent2.Hash.Should().Be("9q8yy");
        parent3.Hash.Should().Be("9q8y");
        
        parent1.Precision.Should().Be(6);
        parent2.Precision.Should().Be(5);
        parent3.Precision.Should().Be(4);
    }

    [Fact]
    public void GetChildren_ThenGetParent_ReturnsOriginalCell()
    {
        // Arrange
        var cell = new GeoHashCell("9q8yy");

        // Act
        var children = cell.GetChildren();
        var firstChild = children[0];
        var parent = firstChild.GetParent();

        // Assert
        parent.Hash.Should().Be(cell.Hash);
        parent.Precision.Should().Be(cell.Precision);
    }
}
