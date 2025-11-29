using Oproto.FluentDynamoDb.Geospatial;

namespace Oproto.FluentDynamoDb.Geospatial.UnitTests;

/// <summary>
/// Unit tests for <see cref="DefaultGeospatialProvider"/>.
/// </summary>
public class DefaultGeospatialProviderTests
{
    private readonly DefaultGeospatialProvider _provider = new();

    #region CreateBoundingBox from center and distance

    [Fact]
    public void CreateBoundingBox_FromCenterAndDistance_ReturnsValidBoundingBox()
    {
        // Arrange
        var latitude = 37.7749;
        var longitude = -122.4194;
        var distanceMeters = 5000.0;

        // Act
        var result = _provider.CreateBoundingBox(latitude, longitude, distanceMeters);

        // Assert
        result.SouthwestLatitude.Should().BeLessThan(latitude);
        result.NortheastLatitude.Should().BeGreaterThan(latitude);
        result.SouthwestLongitude.Should().BeLessThan(longitude);
        result.NortheastLongitude.Should().BeGreaterThan(longitude);
    }

    [Fact]
    public void CreateBoundingBox_FromCenterAndDistance_CanBeUsedForCellCovering()
    {
        // Arrange
        var latitude = 37.7749;
        var longitude = -122.4194;
        var distanceMeters = 5000.0;

        // Act
        var result = _provider.CreateBoundingBox(latitude, longitude, distanceMeters);
        var cells = _provider.GetS2CellCovering(result, 12, 100);

        // Assert - The bounding box should be usable for cell covering
        cells.Should().NotBeEmpty();
    }

    [Fact]
    public void CreateBoundingBox_FromCenterAndDistance_CenterIsWithinBox()
    {
        // Arrange
        var latitude = 51.5074;
        var longitude = -0.1278;
        var distanceMeters = 10000.0;

        // Act
        var result = _provider.CreateBoundingBox(latitude, longitude, distanceMeters);

        // Assert
        latitude.Should().BeGreaterThanOrEqualTo(result.SouthwestLatitude);
        latitude.Should().BeLessThanOrEqualTo(result.NortheastLatitude);
        longitude.Should().BeGreaterThanOrEqualTo(result.SouthwestLongitude);
        longitude.Should().BeLessThanOrEqualTo(result.NortheastLongitude);
    }

    #endregion

    #region CreateBoundingBox from corners

    [Fact]
    public void CreateBoundingBox_FromCorners_ReturnsValidBoundingBox()
    {
        // Arrange
        var swLat = 37.7;
        var swLon = -122.5;
        var neLat = 37.9;
        var neLon = -122.3;

        // Act
        var result = _provider.CreateBoundingBox(swLat, swLon, neLat, neLon);

        // Assert
        result.SouthwestLatitude.Should().Be(swLat);
        result.SouthwestLongitude.Should().Be(swLon);
        result.NortheastLatitude.Should().Be(neLat);
        result.NortheastLongitude.Should().Be(neLon);
    }

    [Fact]
    public void CreateBoundingBox_FromCorners_CanBeUsedForCellCovering()
    {
        // Arrange
        var swLat = 37.7;
        var swLon = -122.5;
        var neLat = 37.9;
        var neLon = -122.3;

        // Act
        var result = _provider.CreateBoundingBox(swLat, swLon, neLat, neLon);
        var cells = _provider.GetH3CellCovering(result, 7, 100);

        // Assert - The bounding box should be usable for cell covering
        cells.Should().NotBeEmpty();
    }

    #endregion

    #region GetGeoHashRange

    [Fact]
    public void GetGeoHashRange_ReturnsValidRange()
    {
        // Arrange
        var bbox = _provider.CreateBoundingBox(37.7, -122.5, 37.9, -122.3);
        var precision = 6;

        // Act
        var (minHash, maxHash) = _provider.GetGeoHashRange(bbox, precision);

        // Assert
        minHash.Should().NotBeNullOrEmpty();
        maxHash.Should().NotBeNullOrEmpty();
        minHash.Length.Should().Be(precision);
        maxHash.Length.Should().Be(precision);
    }

    [Fact]
    public void GetGeoHashRange_MinHashIsLexicographicallyLessThanOrEqualToMaxHash()
    {
        // Arrange
        var bbox = _provider.CreateBoundingBox(37.7, -122.5, 37.9, -122.3);
        var precision = 6;

        // Act
        var (minHash, maxHash) = _provider.GetGeoHashRange(bbox, precision);

        // Assert
        string.Compare(minHash, maxHash, StringComparison.Ordinal).Should().BeLessThanOrEqualTo(0);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(4)]
    [InlineData(8)]
    [InlineData(12)]
    public void GetGeoHashRange_RespectsRequestedPrecision(int precision)
    {
        // Arrange
        var bbox = _provider.CreateBoundingBox(37.7, -122.5, 37.9, -122.3);

        // Act
        var (minHash, maxHash) = _provider.GetGeoHashRange(bbox, precision);

        // Assert
        minHash.Length.Should().Be(precision);
        maxHash.Length.Should().Be(precision);
    }

    #endregion

    #region GetS2CellCovering

    [Fact]
    public void GetS2CellCovering_ReturnsNonEmptyList()
    {
        // Arrange
        var bbox = _provider.CreateBoundingBox(37.7, -122.5, 37.9, -122.3);
        var level = 12;
        var maxCells = 100;

        // Act
        var cells = _provider.GetS2CellCovering(bbox, level, maxCells);

        // Assert
        cells.Should().NotBeEmpty();
    }

    [Fact]
    public void GetS2CellCovering_RespectsMaxCellsLimit()
    {
        // Arrange - Use a smaller bounding box with lower level to avoid exceeding cell limits
        var bbox = _provider.CreateBoundingBox(37.7, -122.5, 37.8, -122.4);
        var level = 12;
        var maxCells = 50;

        // Act
        var cells = _provider.GetS2CellCovering(bbox, level, maxCells);

        // Assert
        cells.Count.Should().BeLessThanOrEqualTo(maxCells);
    }

    [Fact]
    public void GetS2CellCovering_ReturnsValidCellTokens()
    {
        // Arrange
        var bbox = _provider.CreateBoundingBox(37.7, -122.5, 37.9, -122.3);
        var level = 12;
        var maxCells = 100;

        // Act
        var cells = _provider.GetS2CellCovering(bbox, level, maxCells);

        // Assert
        foreach (var cell in cells)
        {
            cell.Should().NotBeNullOrEmpty();
            // S2 cell tokens are hexadecimal strings
            cell.Should().MatchRegex("^[0-9a-f]+$");
        }
    }

    [Fact]
    public void GetS2CellCovering_ReturnsUniqueCells()
    {
        // Arrange
        var bbox = _provider.CreateBoundingBox(37.7, -122.5, 37.9, -122.3);
        var level = 12;
        var maxCells = 100;

        // Act
        var cells = _provider.GetS2CellCovering(bbox, level, maxCells);

        // Assert
        cells.Should().OnlyHaveUniqueItems();
    }

    #endregion

    #region GetH3CellCovering

    [Fact]
    public void GetH3CellCovering_ReturnsNonEmptyList()
    {
        // Arrange
        var bbox = _provider.CreateBoundingBox(37.7, -122.5, 37.9, -122.3);
        var resolution = 7;
        var maxCells = 100;

        // Act
        var cells = _provider.GetH3CellCovering(bbox, resolution, maxCells);

        // Assert
        cells.Should().NotBeEmpty();
    }

    [Fact]
    public void GetH3CellCovering_RespectsMaxCellsLimit()
    {
        // Arrange - Use a smaller bounding box with lower resolution to avoid exceeding cell limits
        var bbox = _provider.CreateBoundingBox(37.7, -122.5, 37.8, -122.4);
        var resolution = 7;
        var maxCells = 50;

        // Act
        var cells = _provider.GetH3CellCovering(bbox, resolution, maxCells);

        // Assert
        cells.Count.Should().BeLessThanOrEqualTo(maxCells);
    }

    [Fact]
    public void GetH3CellCovering_ReturnsValidCellIndices()
    {
        // Arrange
        var bbox = _provider.CreateBoundingBox(37.7, -122.5, 37.9, -122.3);
        var resolution = 7;
        var maxCells = 100;

        // Act
        var cells = _provider.GetH3CellCovering(bbox, resolution, maxCells);

        // Assert
        foreach (var cell in cells)
        {
            cell.Should().NotBeNullOrEmpty();
            // H3 cell indices are hexadecimal strings
            cell.Should().MatchRegex("^[0-9a-f]+$");
        }
    }

    [Fact]
    public void GetH3CellCovering_ReturnsUniqueCells()
    {
        // Arrange
        var bbox = _provider.CreateBoundingBox(37.7, -122.5, 37.9, -122.3);
        var resolution = 7;
        var maxCells = 100;

        // Act
        var cells = _provider.GetH3CellCovering(bbox, resolution, maxCells);

        // Assert
        cells.Should().OnlyHaveUniqueItems();
    }

    #endregion

    #region BoundingBox without NativeBox

    [Fact]
    public void GetGeoHashRange_WithBoundingBoxWithoutNativeBox_StillWorks()
    {
        // Arrange - Create a GeoBoundingBoxResult without NativeBox
        var bbox = new GeoBoundingBoxResult
        {
            SouthwestLatitude = 37.7,
            SouthwestLongitude = -122.5,
            NortheastLatitude = 37.9,
            NortheastLongitude = -122.3
        };
        var precision = 6;

        // Act
        var (minHash, maxHash) = _provider.GetGeoHashRange(bbox, precision);

        // Assert
        minHash.Should().NotBeNullOrEmpty();
        maxHash.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void GetS2CellCovering_WithBoundingBoxWithoutNativeBox_StillWorks()
    {
        // Arrange - Create a GeoBoundingBoxResult without NativeBox
        var bbox = new GeoBoundingBoxResult
        {
            SouthwestLatitude = 37.7,
            SouthwestLongitude = -122.5,
            NortheastLatitude = 37.9,
            NortheastLongitude = -122.3
        };
        var level = 12;
        var maxCells = 100;

        // Act
        var cells = _provider.GetS2CellCovering(bbox, level, maxCells);

        // Assert
        cells.Should().NotBeEmpty();
    }

    [Fact]
    public void GetH3CellCovering_WithBoundingBoxWithoutNativeBox_StillWorks()
    {
        // Arrange - Create a GeoBoundingBoxResult without NativeBox
        var bbox = new GeoBoundingBoxResult
        {
            SouthwestLatitude = 37.7,
            SouthwestLongitude = -122.5,
            NortheastLatitude = 37.9,
            NortheastLongitude = -122.3
        };
        var resolution = 7;
        var maxCells = 100;

        // Act
        var cells = _provider.GetH3CellCovering(bbox, resolution, maxCells);

        // Assert
        cells.Should().NotBeEmpty();
    }

    #endregion
}
