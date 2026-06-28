using AwesomeAssertions;
using Oproto.FluentDynamoDb.SourceGenerator.Generators;
using Oproto.FluentDynamoDb.SourceGenerator.Models;

namespace Oproto.FluentDynamoDb.SourceGenerator.UnitTests.Generators;

/// <summary>
/// Integration tests verifying the source generator emits the correct GetBlobProvider calls
/// for entities with blob storage properties using named providers.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Feature", "named-blob-providers")]
public class NamedBlobProviderGenerationTests
{
    [Fact]
    public void GenerateEntityImplementation_BlobStorageWithNoProvider_EmitsGetBlobProviderNull()
    {
        // Arrange
        var entity = CreateEntityWithDefaultBlobProvider();

        // Act
        var result = MapperGenerator.GenerateEntityImplementation(entity);

        // Assert
        result.Should().Contain("options.GetBlobProvider(null)",
            "default blob storage (no Provider set) should resolve via GetBlobProvider(null)");
        result.Should().Contain("var blobProvider_Content = options.GetBlobProvider(null)",
            "should declare per-property blob provider variable for Content");
    }

    [Fact]
    public void GenerateEntityImplementation_BlobStorageWithNamedProvider_EmitsGetBlobProviderWithName()
    {
        // Arrange
        var entity = CreateEntityWithNamedBlobProvider("docs");

        // Act
        var result = MapperGenerator.GenerateEntityImplementation(entity);

        // Assert
        result.Should().Contain("options.GetBlobProvider(\"docs\")",
            "named blob storage provider should resolve via GetBlobProvider(\"docs\")");
        result.Should().Contain("var blobProvider_Document = options.GetBlobProvider(\"docs\")",
            "should declare per-property blob provider variable for Document with named provider");
    }

    [Fact]
    public void GenerateEntityImplementation_MultipleBlobPropertiesWithDifferentProviders_EmitsPerPropertyResolution()
    {
        // Arrange
        var entity = new EntityModel
        {
            ClassName = "MultiProviderEntity",
            Namespace = "TestNamespace",
            TableName = "multi-provider-table",
            Properties = new[]
            {
                new PropertyModel
                {
                    PropertyName = "Id",
                    AttributeName = "pk",
                    PropertyType = "string",
                    IsPartitionKey = true
                },
                new PropertyModel
                {
                    PropertyName = "Images",
                    AttributeName = "images_ref",
                    PropertyType = "BlobData<byte[]>",
                    ComplexType = new ComplexTypeInfo
                    {
                        IsBlobStorage = true,
                        BlobDataInnerType = "byte[]",
                        BlobStorageProviderName = "images"
                    }
                },
                new PropertyModel
                {
                    PropertyName = "Contracts",
                    AttributeName = "contracts_ref",
                    PropertyType = "BlobData<byte[]>",
                    ComplexType = new ComplexTypeInfo
                    {
                        IsBlobStorage = true,
                        BlobDataInnerType = "byte[]",
                        BlobStorageProviderName = "documents"
                    }
                }
            }
        };

        // Act
        var result = MapperGenerator.GenerateEntityImplementation(entity);

        // Assert
        result.Should().Contain("var blobProvider_Images = options.GetBlobProvider(\"images\")",
            "Images property should resolve via named provider 'images'");
        result.Should().Contain("var blobProvider_Contracts = options.GetBlobProvider(\"documents\")",
            "Contracts property should resolve via named provider 'documents'");
    }

    [Fact]
    public void GenerateEntityImplementation_MixedDefaultAndNamedProviders_EmitsCorrectRouting()
    {
        // Arrange
        var entity = new EntityModel
        {
            ClassName = "MixedProviderEntity",
            Namespace = "TestNamespace",
            TableName = "mixed-provider-table",
            Properties = new[]
            {
                new PropertyModel
                {
                    PropertyName = "Id",
                    AttributeName = "pk",
                    PropertyType = "string",
                    IsPartitionKey = true
                },
                new PropertyModel
                {
                    PropertyName = "Avatar",
                    AttributeName = "avatar_ref",
                    PropertyType = "BlobData<byte[]>",
                    ComplexType = new ComplexTypeInfo
                    {
                        IsBlobStorage = true,
                        BlobDataInnerType = "byte[]",
                        BlobStorageProviderName = null // default provider
                    }
                },
                new PropertyModel
                {
                    PropertyName = "Resume",
                    AttributeName = "resume_ref",
                    PropertyType = "BlobData<byte[]>",
                    ComplexType = new ComplexTypeInfo
                    {
                        IsBlobStorage = true,
                        BlobDataInnerType = "byte[]",
                        BlobStorageProviderName = "docs" // named provider
                    }
                }
            }
        };

        // Act
        var result = MapperGenerator.GenerateEntityImplementation(entity);

        // Assert
        result.Should().Contain("var blobProvider_Avatar = options.GetBlobProvider(null)",
            "Avatar with no provider should use default via GetBlobProvider(null)");
        result.Should().Contain("var blobProvider_Resume = options.GetBlobProvider(\"docs\")",
            "Resume with named provider should use GetBlobProvider(\"docs\")");
    }

    [Fact]
    public void GenerateEntityImplementation_ExistingEntityWithoutProviderProperty_CompilesWithoutChanges()
    {
        // Arrange - entity using blob storage exactly as it was before the named providers feature
        var entity = new EntityModel
        {
            ClassName = "LegacyEntity",
            Namespace = "TestNamespace",
            TableName = "legacy-table",
            Properties = new[]
            {
                new PropertyModel
                {
                    PropertyName = "Id",
                    AttributeName = "pk",
                    PropertyType = "string",
                    IsPartitionKey = true
                },
                new PropertyModel
                {
                    PropertyName = "Attachment",
                    AttributeName = "attachment_ref",
                    PropertyType = "BlobData<byte[]>",
                    ComplexType = new ComplexTypeInfo
                    {
                        IsBlobStorage = true,
                        BlobDataInnerType = "byte[]"
                        // BlobStorageProviderName is null by default - backwards compatible
                    }
                }
            }
        };

        // Act
        var result = MapperGenerator.GenerateEntityImplementation(entity);

        // Assert - backwards compatibility: existing entities compile and use default provider
        result.Should().NotBeNull("entity with blob storage should generate code");
        result.Should().Contain("options.GetBlobProvider(null)",
            "legacy entity without Provider should fall back to GetBlobProvider(null) for default provider");
        result.Should().Contain("var blobProvider_Attachment = options.GetBlobProvider(null)",
            "should generate per-property provider variable even for default provider");
        // Ensure it does NOT reference any named provider
        result.Should().NotContain("GetBlobProvider(\"",
            "legacy entity should not reference any named provider");
    }

    #region Helper Methods

    private static EntityModel CreateEntityWithDefaultBlobProvider()
    {
        return new EntityModel
        {
            ClassName = "TestEntity",
            Namespace = "TestNamespace",
            TableName = "test-table",
            Properties = new[]
            {
                new PropertyModel
                {
                    PropertyName = "Id",
                    AttributeName = "pk",
                    PropertyType = "string",
                    IsPartitionKey = true
                },
                new PropertyModel
                {
                    PropertyName = "Content",
                    AttributeName = "content_ref",
                    PropertyType = "BlobData<byte[]>",
                    ComplexType = new ComplexTypeInfo
                    {
                        IsBlobStorage = true,
                        BlobDataInnerType = "byte[]",
                        BlobStorageProviderName = null
                    }
                }
            }
        };
    }

    private static EntityModel CreateEntityWithNamedBlobProvider(string providerName)
    {
        return new EntityModel
        {
            ClassName = "TestEntity",
            Namespace = "TestNamespace",
            TableName = "test-table",
            Properties = new[]
            {
                new PropertyModel
                {
                    PropertyName = "Id",
                    AttributeName = "pk",
                    PropertyType = "string",
                    IsPartitionKey = true
                },
                new PropertyModel
                {
                    PropertyName = "Document",
                    AttributeName = "document_ref",
                    PropertyType = "BlobData<byte[]>",
                    ComplexType = new ComplexTypeInfo
                    {
                        IsBlobStorage = true,
                        BlobDataInnerType = "byte[]",
                        BlobStorageProviderName = providerName
                    }
                }
            }
        };
    }

    #endregion
}
