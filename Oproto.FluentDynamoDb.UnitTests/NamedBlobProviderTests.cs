using NSubstitute;
using Oproto.FluentDynamoDb.Providers.BlobStorage;

namespace Oproto.FluentDynamoDb.UnitTests;

[Trait("Feature", "named-blob-providers")]
public class NamedBlobProviderTests
{
    [Fact]
    public void GetBlobProvider_Null_ReturnsDefaultProvider_WhenConfigured()
    {
        // Arrange
        var defaultProvider = Substitute.For<IBlobStorageProvider>();
        var options = new FluentDynamoDbOptions()
            .WithBlobStorage(defaultProvider);

        // Act
        var result = options.GetBlobProvider(null);

        // Assert
        result.Should().BeSameAs(defaultProvider);
    }

    [Fact]
    public void GetBlobProvider_EmptyString_ReturnsDefaultProvider_WhenConfigured()
    {
        // Arrange
        var defaultProvider = Substitute.For<IBlobStorageProvider>();
        var options = new FluentDynamoDbOptions()
            .WithBlobStorage(defaultProvider);

        // Act
        var result = options.GetBlobProvider("");

        // Assert
        result.Should().BeSameAs(defaultProvider);
    }

    [Fact]
    public void GetBlobProvider_Null_ThrowsWhenNoDefault_MessageSuggestsWithBlobStorage()
    {
        // Arrange
        var options = new FluentDynamoDbOptions();

        // Act
        var action = () => options.GetBlobProvider(null);

        // Assert
        action.Should().Throw<InvalidOperationException>()
            .WithMessage("*WithBlobStorage(provider)*");
    }

    [Fact]
    public void GetBlobProvider_MissingName_ThrowsWithNameAndListsAvailableProviders()
    {
        // Arrange
        var options = new FluentDynamoDbOptions()
            .WithBlobStorage("s3", Substitute.For<IBlobStorageProvider>())
            .WithBlobStorage("azure", Substitute.For<IBlobStorageProvider>());

        // Act
        var action = () => options.GetBlobProvider("missing");

        // Assert
        action.Should().Throw<InvalidOperationException>()
            .WithMessage("*'missing'*")
            .WithMessage("*azure*")
            .WithMessage("*s3*");
    }

    [Fact]
    public void GetBlobProvider_MissingName_EmptyRegistry_ThrowsWithNoNamedProvidersMessage()
    {
        // Arrange
        var options = new FluentDynamoDbOptions();

        // Act
        var action = () => options.GetBlobProvider("missing");

        // Assert
        action.Should().Throw<InvalidOperationException>()
            .WithMessage("*'missing'*")
            .WithMessage("*no named providers*");
    }

    [Fact]
    public void WithBlobStorage_NullName_ThrowsArgumentException()
    {
        // Arrange
        var options = new FluentDynamoDbOptions();
        var provider = Substitute.For<IBlobStorageProvider>();

        // Act
        var action = () => options.WithBlobStorage(null!, provider);

        // Assert
        action.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void WithBlobStorage_NullProvider_ThrowsArgumentNullException()
    {
        // Arrange
        var options = new FluentDynamoDbOptions();

        // Act
        var action = () => options.WithBlobStorage("name", null!);

        // Assert
        action.Should().Throw<ArgumentNullException>();
    }
}
