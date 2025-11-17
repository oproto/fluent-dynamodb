using Oproto.FluentDynamoDb.Geospatial.GeoHash;

namespace Oproto.FluentDynamoDb.Geospatial.UnitTests;

/// <summary>
/// Comprehensive tests for error handling across all geospatial components.
/// Verifies that all exceptions are thrown with clear, actionable error messages.
/// </summary>
public class ErrorHandlingTests
{
    #region GeoLocation Error Handling

    [Theory]
    [InlineData(-91)]
    [InlineData(-100)]
    [InlineData(-180)]
    [InlineData(91)]
    [InlineData(100)]
    [InlineData(180)]
    [InlineData(double.MinValue)]
    [InlineData(double.MaxValue)]
    public void GeoLocation_InvalidLatitude_ThrowsArgumentOutOfRangeException(double invalidLat)
    {
        // Act & Assert
        var exception = Assert.Throws<ArgumentOutOfRangeException>(
            () => new GeoLocation(invalidLat, 0));
        
        exception.ParamName.Should().Be("latitude");
        exception.Message.Should().Contain("Latitude must be between -90 and 90 degrees");
        exception.ActualValue.Should().Be(invalidLat);
    }

    [Theory]
    [InlineData(-181)]
    [InlineData(-200)]
    [InlineData(-360)]
    [InlineData(181)]
    [InlineData(200)]
    [InlineData(360)]
    [InlineData(double.MinValue)]
    [InlineData(double.MaxValue)]
    public void GeoLocation_InvalidLongitude_ThrowsArgumentOutOfRangeException(double invalidLon)
    {
        // Act & Assert
        var exception = Assert.Throws<ArgumentOutOfRangeException>(
            () => new GeoLocation(0, invalidLon));
        
        exception.ParamName.Should().Be("longitude");
        exception.Message.Should().Contain("Longitude must be between -180 and 180 degrees");
        exception.ActualValue.Should().Be(invalidLon);
    }

    #endregion

    #region GeoBoundingBox Error Handling

    [Fact]
    public void GeoBoundingBox_SouthAboveNorth_ThrowsArgumentException()
    {
        // Arrange
        var southwest = new GeoLocation(40.0, -122.5);
        var northeast = new GeoLocation(39.0, -122.3);

        // Act & Assert
        var exception = Assert.Throws<ArgumentException>(
            () => new GeoBoundingBox(southwest, northeast));
        
        exception.ParamName.Should().Be("southwest");
        exception.Message.Should().Contain("Southwest corner latitude must be less than or equal to northeast corner latitude");
    }

    [Fact]
    public void GeoBoundingBox_WestEastOfEast_ThrowsArgumentException()
    {
        // Arrange
        var southwest = new GeoLocation(37.7, -122.3);
        var northeast = new GeoLocation(37.9, -122.5);

        // Act & Assert
        var exception = Assert.Throws<ArgumentException>(
            () => new GeoBoundingBox(southwest, northeast));
        
        exception.ParamName.Should().Be("southwest");
        exception.Message.Should().Contain("Southwest corner longitude must be less than or equal to northeast corner longitude");
    }

    #endregion

    #region GeoHashEncoder Error Handling

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-10)]
    [InlineData(13)]
    [InlineData(20)]
    [InlineData(100)]
    [InlineData(int.MinValue)]
    [InlineData(int.MaxValue)]
    public void GeoHashEncoder_Encode_InvalidPrecision_ThrowsArgumentOutOfRangeException(int invalidPrecision)
    {
        // Act & Assert
        var exception = Assert.Throws<ArgumentOutOfRangeException>(
            () => GeoHashEncoder.Encode(37.7749, -122.4194, invalidPrecision));
        
        exception.ParamName.Should().Be("precision");
        exception.Message.Should().Contain("Precision must be between 1 and 12");
        exception.ActualValue.Should().Be(invalidPrecision);
    }

    [Fact]
    public void GeoHashEncoder_Decode_NullGeoHash_ThrowsArgumentException()
    {
        // Act & Assert
        var exception = Assert.Throws<ArgumentException>(
            () => GeoHashEncoder.Decode(null!));
        
        exception.ParamName.Should().Be("geohash");
        exception.Message.Should().Contain("GeoHash string cannot be null or empty");
    }

    [Fact]
    public void GeoHashEncoder_Decode_EmptyGeoHash_ThrowsArgumentException()
    {
        // Act & Assert
        var exception = Assert.Throws<ArgumentException>(
            () => GeoHashEncoder.Decode(string.Empty));
        
        exception.ParamName.Should().Be("geohash");
        exception.Message.Should().Contain("GeoHash string cannot be null or empty");
    }

    [Theory]
    [InlineData("9q8yy!", '!')]
    [InlineData("9q8yya", 'a')]
    [InlineData("9q8yyi", 'i')]
    [InlineData("9q8yyl", 'l')]
    [InlineData("9q8yyo", 'o')]
    [InlineData("9q8yyA", 'A')]
    [InlineData("9q8yyZ", 'Z')]
    [InlineData("9q8yy@", '@')]
    [InlineData("9q8yy#", '#')]
    [InlineData("9q8yy ", ' ')]
    public void GeoHashEncoder_Decode_InvalidCharacter_ThrowsArgumentException(
        string invalidHash, char invalidChar)
    {
        // Act & Assert
        var exception = Assert.Throws<ArgumentException>(
            () => GeoHashEncoder.Decode(invalidHash));
        
        exception.ParamName.Should().Be("geohash");
        exception.Message.Should().Contain($"Invalid character '{invalidChar}'");
        exception.Message.Should().Contain("Valid characters are: 0123456789bcdefghjkmnpqrstuvwxyz");
    }

    [Fact]
    public void GeoHashEncoder_DecodeBounds_NullGeoHash_ThrowsArgumentException()
    {
        // Act & Assert
        var exception = Assert.Throws<ArgumentException>(
            () => GeoHashEncoder.DecodeBounds(null!));
        
        exception.ParamName.Should().Be("geohash");
        exception.Message.Should().Contain("GeoHash string cannot be null or empty");
    }

    [Fact]
    public void GeoHashEncoder_DecodeBounds_EmptyGeoHash_ThrowsArgumentException()
    {
        // Act & Assert
        var exception = Assert.Throws<ArgumentException>(
            () => GeoHashEncoder.DecodeBounds(string.Empty));
        
        exception.ParamName.Should().Be("geohash");
        exception.Message.Should().Contain("GeoHash string cannot be null or empty");
    }

    [Fact]
    public void GeoHashEncoder_GetNeighbors_NullGeoHash_ThrowsArgumentException()
    {
        // Act & Assert
        var exception = Assert.Throws<ArgumentException>(
            () => GeoHashEncoder.GetNeighbors(null!));
        
        exception.ParamName.Should().Be("geohash");
        exception.Message.Should().Contain("GeoHash string cannot be null or empty");
    }

    [Fact]
    public void GeoHashEncoder_GetNeighbors_EmptyGeoHash_ThrowsArgumentException()
    {
        // Act & Assert
        var exception = Assert.Throws<ArgumentException>(
            () => GeoHashEncoder.GetNeighbors(string.Empty));
        
        exception.ParamName.Should().Be("geohash");
        exception.Message.Should().Contain("GeoHash string cannot be null or empty");
    }

    #endregion

    #region GeoHashCell Error Handling

    [Fact]
    public void GeoHashCell_Constructor_NullHash_ThrowsArgumentException()
    {
        // Act & Assert
        var exception = Assert.Throws<ArgumentException>(
            () => new GeoHashCell(null!));
        
        exception.ParamName.Should().Be("hash");
        exception.Message.Should().Contain("GeoHash string cannot be null or empty");
    }

    [Fact]
    public void GeoHashCell_Constructor_EmptyHash_ThrowsArgumentException()
    {
        // Act & Assert
        var exception = Assert.Throws<ArgumentException>(
            () => new GeoHashCell(string.Empty));
        
        exception.ParamName.Should().Be("hash");
        exception.Message.Should().Contain("GeoHash string cannot be null or empty");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(13)]
    [InlineData(100)]
    public void GeoHashCell_Constructor_InvalidPrecision_ThrowsArgumentOutOfRangeException(
        int invalidPrecision)
    {
        // Arrange
        var location = new GeoLocation(37.7749, -122.4194);

        // Act & Assert
        var exception = Assert.Throws<ArgumentOutOfRangeException>(
            () => new GeoHashCell(location, invalidPrecision));
        
        exception.ParamName.Should().Be("precision");
        exception.Message.Should().Contain("Precision must be between 1 and 12");
        exception.ActualValue.Should().Be(invalidPrecision);
    }

    [Fact]
    public void GeoHashCell_GetParent_PrecisionOne_ThrowsInvalidOperationException()
    {
        // Arrange
        var cell = new GeoHashCell("9");

        // Act & Assert
        var exception = Assert.Throws<InvalidOperationException>(
            () => cell.GetParent());
        
        exception.Message.Should().Contain("Cannot get parent for a GeoHash cell with precision 1");
    }

    [Fact]
    public void GeoHashCell_GetChildren_MaxPrecision_ThrowsInvalidOperationException()
    {
        // Arrange
        var cell = new GeoHashCell("9q8yy9r12345");

        // Act & Assert
        var exception = Assert.Throws<InvalidOperationException>(
            () => cell.GetChildren());
        
        exception.Message.Should().Contain("Cannot get children for a GeoHash cell with precision 12");
    }

    #endregion

    #region Extension Methods Error Handling

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(13)]
    [InlineData(100)]
    public void ToGeoHash_InvalidPrecision_ThrowsArgumentOutOfRangeException(int invalidPrecision)
    {
        // Arrange
        var location = new GeoLocation(37.7749, -122.4194);

        // Act & Assert
        var exception = Assert.Throws<ArgumentOutOfRangeException>(
            () => location.ToGeoHash(invalidPrecision));
        
        exception.ParamName.Should().Be("precision");
    }

    [Fact]
    public void FromGeoHash_NullHash_ThrowsArgumentException()
    {
        // Act & Assert
        var exception = Assert.Throws<ArgumentException>(
            () => GeoHashExtensions.FromGeoHash(null!));
        
        exception.ParamName.Should().Be("geohash");
    }

    [Fact]
    public void FromGeoHash_EmptyHash_ThrowsArgumentException()
    {
        // Act & Assert
        var exception = Assert.Throws<ArgumentException>(
            () => GeoHashExtensions.FromGeoHash(string.Empty));
        
        exception.ParamName.Should().Be("geohash");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(13)]
    [InlineData(100)]
    public void ToGeoHashCell_InvalidPrecision_ThrowsArgumentOutOfRangeException(int invalidPrecision)
    {
        // Arrange
        var location = new GeoLocation(37.7749, -122.4194);

        // Act & Assert
        var exception = Assert.Throws<ArgumentOutOfRangeException>(
            () => location.ToGeoHashCell(invalidPrecision));
        
        exception.ParamName.Should().Be("precision");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(13)]
    [InlineData(100)]
    public void GetGeoHashRange_InvalidPrecision_ThrowsArgumentOutOfRangeException(int invalidPrecision)
    {
        // Arrange
        var southwest = new GeoLocation(37.7, -122.5);
        var northeast = new GeoLocation(37.9, -122.3);
        var bbox = new GeoBoundingBox(southwest, northeast);

        // Act & Assert
        var exception = Assert.Throws<ArgumentOutOfRangeException>(
            () => bbox.GetGeoHashRange(invalidPrecision));
        
        exception.ParamName.Should().Be("precision");
        exception.Message.Should().Contain("Precision must be between 1 and 12");
        exception.ActualValue.Should().Be(invalidPrecision);
    }

    #endregion

    #region Error Message Quality Tests

    [Fact]
    public void AllExceptions_HaveNonEmptyMessages()
    {
        // This test verifies that all exceptions have meaningful messages
        var exceptions = new List<Exception>
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new GeoLocation(100, 0)),
            Assert.Throws<ArgumentOutOfRangeException>(() => new GeoLocation(0, 200)),
            Assert.Throws<ArgumentException>(() => new GeoBoundingBox(
                new GeoLocation(40, -122), new GeoLocation(39, -122))),
            Assert.Throws<ArgumentOutOfRangeException>(() => GeoHashEncoder.Encode(0, 0, 0)),
            Assert.Throws<ArgumentException>(() => GeoHashEncoder.Decode(null!)),
            Assert.Throws<ArgumentException>(() => new GeoHashCell(null!)),
            Assert.Throws<InvalidOperationException>(() => new GeoHashCell("9").GetParent())
        };

        // Assert
        foreach (var exception in exceptions)
        {
            exception.Message.Should().NotBeNullOrEmpty();
            exception.Message.Length.Should().BeGreaterThan(10); // Meaningful message
        }
    }

    [Fact]
    public void AllArgumentExceptions_HaveParameterNames()
    {
        // This test verifies that all ArgumentException-derived exceptions specify parameter names
        var exceptions = new List<ArgumentException>
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new GeoLocation(100, 0)),
            Assert.Throws<ArgumentOutOfRangeException>(() => new GeoLocation(0, 200)),
            Assert.Throws<ArgumentException>(() => new GeoBoundingBox(
                new GeoLocation(40, -122), new GeoLocation(39, -122))),
            Assert.Throws<ArgumentOutOfRangeException>(() => GeoHashEncoder.Encode(0, 0, 0)),
            Assert.Throws<ArgumentException>(() => GeoHashEncoder.Decode(null!)),
            Assert.Throws<ArgumentException>(() => new GeoHashCell(null!))
        };

        // Assert
        foreach (var exception in exceptions)
        {
            exception.ParamName.Should().NotBeNullOrEmpty();
        }
    }

    [Fact]
    public void AllArgumentOutOfRangeExceptions_HaveActualValues()
    {
        // This test verifies that ArgumentOutOfRangeException includes actual values
        var exceptions = new List<ArgumentOutOfRangeException>
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new GeoLocation(100, 0)),
            Assert.Throws<ArgumentOutOfRangeException>(() => new GeoLocation(0, 200)),
            Assert.Throws<ArgumentOutOfRangeException>(() => GeoHashEncoder.Encode(0, 0, 0)),
            Assert.Throws<ArgumentOutOfRangeException>(() => new GeoHashCell(
                new GeoLocation(0, 0), 0))
        };

        // Assert
        foreach (var exception in exceptions)
        {
            exception.ActualValue.Should().NotBeNull();
        }
    }

    #endregion
}
