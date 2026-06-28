using Oproto.FluentDynamoDb.Attributes;

namespace Oproto.FluentDynamoDb.UnitTests.Attributes;

[Trait("Feature", "named-blob-providers")]
public class BlobStorageAttributeTests
{
    [Fact]
    public void ProviderDefaultsToNull()
    {
        // Act
        var attribute = new BlobStorageAttribute();

        // Assert
        attribute.Provider.Should().BeNull();
    }

    [Fact]
    public void LazyLoadDefaultsToFalse()
    {
        // Act
        var attribute = new BlobStorageAttribute();

        // Assert
        attribute.LazyLoad.Should().BeFalse();
    }

    [Fact]
    public void ProviderCanBeSetToNonEmptyString()
    {
        // Act
        var attribute = new BlobStorageAttribute { Provider = "documents" };

        // Assert
        attribute.Provider.Should().Be("documents");
    }
}
