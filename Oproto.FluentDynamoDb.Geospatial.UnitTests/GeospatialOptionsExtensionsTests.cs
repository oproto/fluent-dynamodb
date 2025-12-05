using Oproto.FluentDynamoDb.Geospatial;

namespace Oproto.FluentDynamoDb.Geospatial.UnitTests;

/// <summary>
/// Unit tests for <see cref="GeospatialOptionsExtensions"/>.
/// </summary>
public class GeospatialOptionsExtensionsTests
{
    [Fact]
    public void AddGeospatial_ReturnsNewOptionsWithGeospatialProvider()
    {
        // Arrange
        var options = new FluentDynamoDbOptions();

        // Act
        var result = options.AddGeospatial();

        // Assert
        result.GeospatialProvider.Should().NotBeNull();
        result.GeospatialProvider.Should().BeOfType<DefaultGeospatialProvider>();
    }

    [Fact]
    public void AddGeospatial_DoesNotModifyOriginalOptions()
    {
        // Arrange
        var options = new FluentDynamoDbOptions();

        // Act
        var result = options.AddGeospatial();

        // Assert
        options.GeospatialProvider.Should().BeNull();
        result.GeospatialProvider.Should().NotBeNull();
    }

    [Fact]
    public void AddGeospatial_PreservesOtherOptions()
    {
        // Arrange
        var options = new FluentDynamoDbOptions();

        // Act
        var result = options.AddGeospatial();

        // Assert
        result.Logger.Should().Be(options.Logger);
        result.BlobStorageProvider.Should().Be(options.BlobStorageProvider);
        result.FieldEncryptor.Should().Be(options.FieldEncryptor);
    }

    [Fact]
    public void AddGeospatial_WithNullOptions_ThrowsArgumentNullException()
    {
        // Arrange
        FluentDynamoDbOptions? options = null;

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => options!.AddGeospatial());
    }

    [Fact]
    public void AddGeospatial_WithCustomProvider_UsesProvidedProvider()
    {
        // Arrange
        var options = new FluentDynamoDbOptions();
        var customProvider = Substitute.For<IGeospatialProvider>();

        // Act
        var result = options.AddGeospatial(customProvider);

        // Assert
        result.GeospatialProvider.Should().BeSameAs(customProvider);
    }

    [Fact]
    public void AddGeospatial_WithCustomProvider_DoesNotModifyOriginalOptions()
    {
        // Arrange
        var options = new FluentDynamoDbOptions();
        var customProvider = Substitute.For<IGeospatialProvider>();

        // Act
        var result = options.AddGeospatial(customProvider);

        // Assert
        options.GeospatialProvider.Should().BeNull();
        result.GeospatialProvider.Should().BeSameAs(customProvider);
    }

    [Fact]
    public void AddGeospatial_WithNullProvider_ThrowsArgumentNullException()
    {
        // Arrange
        var options = new FluentDynamoDbOptions();
        IGeospatialProvider? provider = null;

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => options.AddGeospatial(provider!));
    }

    [Fact]
    public void AddGeospatial_CanBeChainedWithOtherMethods()
    {
        // Arrange
        var options = new FluentDynamoDbOptions();

        // Act
        var result = options
            .AddGeospatial()
            .WithLogger(null);

        // Assert
        result.GeospatialProvider.Should().NotBeNull();
        result.GeospatialProvider.Should().BeOfType<DefaultGeospatialProvider>();
    }

    [Fact]
    public void AddGeospatial_CalledMultipleTimes_LastProviderWins()
    {
        // Arrange
        var options = new FluentDynamoDbOptions();
        var customProvider = Substitute.For<IGeospatialProvider>();

        // Act
        var result = options
            .AddGeospatial()
            .AddGeospatial(customProvider);

        // Assert
        result.GeospatialProvider.Should().BeSameAs(customProvider);
    }
}
