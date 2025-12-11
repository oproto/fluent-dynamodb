using AwesomeAssertions;
using Oproto.FluentDynamoDb.SourceGenerator.Generators;
using Oproto.FluentDynamoDb.SourceGenerator.Models;
using Oproto.FluentDynamoDb.SourceGenerator.UnitTests.TestHelpers;

namespace Oproto.FluentDynamoDb.SourceGenerator.UnitTests.Generators;

/// <summary>
/// Unit tests for HydratorGenerator.
/// Tests that IAsyncEntityHydrator implementations are correctly generated for entities with blob storage properties.
/// </summary>
[Trait("Category", "Unit")]
public class HydratorGeneratorTests
{
    [Fact]
    public void RequiresHydrator_WithBlobStorageProperty_ReturnsTrue()
    {
        // Arrange
        var entity = CreateEntityWithBlobStorage();

        // Act
        var result = HydratorGenerator.RequiresHydrator(entity);

        // Assert
        result.Should().BeTrue("entity has a blob storage property");
    }

    [Fact]
    public void RequiresHydrator_WithoutBlobStorageProperty_ReturnsFalse()
    {
        // Arrange
        var entity = CreateBasicEntity();

        // Act
        var result = HydratorGenerator.RequiresHydrator(entity);

        // Assert
        result.Should().BeFalse("entity has no blob storage properties");
    }

    [Fact]
    public void GenerateHydrator_WithBlobStorageProperty_GeneratesHydratorClass()
    {
        // Arrange
        var entity = CreateEntityWithBlobStorage();

        // Act
        var result = HydratorGenerator.GenerateHydrator(entity);

        // Assert
        result.Should().NotBeNull("entity has blob storage property");
        result.Should().Contain("public sealed class TestEntityHydrator : IAsyncEntityHydrator<TestEntity>",
            "should generate hydrator class implementing IAsyncEntityHydrator");
        result.Should().Contain("public static readonly TestEntityHydrator Instance = new();",
            "should generate singleton instance");
    }

    [Fact]
    public void GenerateHydrator_WithBlobStorageProperty_GeneratesHydrateAsyncMethods()
    {
        // Arrange
        var entity = CreateEntityWithBlobStorage();

        // Act
        var result = HydratorGenerator.GenerateHydrator(entity);

        // Assert
        result.Should().NotBeNull();
        
        // Check single item HydrateAsync
        result.Should().Contain("public async Task<TestEntity> HydrateAsync(",
            "should generate HydrateAsync method");
        result.Should().Contain("Dictionary<string, AttributeValue> item,",
            "should accept single item parameter");
        result.Should().Contain("IBlobStorageProvider blobProvider,",
            "should accept blob provider parameter");
        result.Should().Contain("CancellationToken cancellationToken = default)",
            "should accept cancellation token parameter");
        
        // Check multi-item HydrateAsync
        result.Should().Contain("IList<Dictionary<string, AttributeValue>> items,",
            "should generate multi-item HydrateAsync overload");
    }

    [Fact]
    public void GenerateHydrator_WithBlobStorageProperty_GeneratesRegistrationExtension()
    {
        // Arrange
        var entity = CreateEntityWithBlobStorage();

        // Act
        var result = HydratorGenerator.GenerateHydrator(entity);

        // Assert
        result.Should().NotBeNull();
        result.Should().Contain("public static class TestEntityHydratorExtensions",
            "should generate extension class for registration");
        result.Should().Contain("public static IEntityHydratorRegistry RegisterTestEntityHydrator(this IEntityHydratorRegistry registry)",
            "should generate registration extension method");
        result.Should().Contain("registry.Register(TestEntityHydrator.Instance);",
            "should register the singleton hydrator instance");
    }

    [Fact]
    public void GenerateHydrator_WithoutBlobStorageProperty_ReturnsNull()
    {
        // Arrange
        var entity = CreateBasicEntity();

        // Act
        var result = HydratorGenerator.GenerateHydrator(entity);

        // Assert
        result.Should().BeNull("entity has no blob storage properties");
    }

    [Fact]
    public void GenerateHydrator_WithBlobStorageProperty_DelegatesToFromDynamoDbAsync()
    {
        // Arrange
        var entity = CreateEntityWithBlobStorage();

        // Act
        var result = HydratorGenerator.GenerateHydrator(entity);

        // Assert
        result.Should().NotBeNull();
        result.Should().Contain("return await TestEntity.FromDynamoDbAsync<TestEntity>(",
            "should delegate to the generated FromDynamoDbAsync method on the entity");
    }

    [Fact]
    public void GenerateHydrator_WithBlobStorageProperty_IncludesNullChecks()
    {
        // Arrange
        var entity = CreateEntityWithBlobStorage();

        // Act
        var result = HydratorGenerator.GenerateHydrator(entity);

        // Assert
        result.Should().NotBeNull();
        result.Should().Contain("ArgumentNullException.ThrowIfNull(item);",
            "should validate item parameter is not null");
        result.Should().Contain("ArgumentNullException.ThrowIfNull(blobProvider);",
            "should validate blobProvider parameter is not null");
        result.Should().Contain("ArgumentNullException.ThrowIfNull(items);",
            "should validate items parameter is not null");
        result.Should().Contain("if (items.Count == 0)",
            "should check for empty items collection");
    }

    [Fact]
    public void GenerateHydrator_WithBlobStorageProperty_GeneratesCorrectNamespace()
    {
        // Arrange
        var entity = CreateEntityWithBlobStorage();
        entity.Namespace = "MyApp.Entities";

        // Act
        var result = HydratorGenerator.GenerateHydrator(entity);

        // Assert
        result.Should().NotBeNull();
        result.Should().Contain("namespace MyApp.Entities",
            "should use the entity's namespace");
    }

    [Fact]
    public void GenerateHydrator_WithMultipleBlobStorageProperties_GeneratesHydrator()
    {
        // Arrange
        var entity = new EntityModel
        {
            ClassName = "DocumentEntity",
            Namespace = "TestNamespace",
            TableName = "documents",
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
                        BlobDataInnerType = "byte[]"
                    }
                },
                new PropertyModel
                {
                    PropertyName = "Thumbnail",
                    AttributeName = "thumbnail_ref",
                    PropertyType = "BlobData<byte[]>",
                    ComplexType = new ComplexTypeInfo
                    {
                        IsBlobStorage = true,
                        BlobDataInnerType = "byte[]"
                    }
                }
            }
        };

        // Act
        var result = HydratorGenerator.GenerateHydrator(entity);

        // Assert
        result.Should().NotBeNull("entity has multiple blob storage properties");
        result.Should().Contain("public sealed class DocumentEntityHydrator : IAsyncEntityHydrator<DocumentEntity>",
            "should generate hydrator for entity with multiple blob storage properties");
    }

    /// <summary>
    /// Creates a basic entity without blob storage properties for testing.
    /// </summary>
    private static EntityModel CreateBasicEntity()
    {
        return new EntityModel
        {
            ClassName = "BasicEntity",
            Namespace = "TestNamespace",
            TableName = "basic-table",
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
                    PropertyName = "Name",
                    AttributeName = "name",
                    PropertyType = "string"
                }
            }
        };
    }

    /// <summary>
    /// Creates an entity with a blob storage property for testing.
    /// </summary>
    private static EntityModel CreateEntityWithBlobStorage()
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
                    PropertyName = "Name",
                    AttributeName = "name",
                    PropertyType = "string"
                },
                new PropertyModel
                {
                    PropertyName = "LargeData",
                    AttributeName = "large_data_ref",
                    PropertyType = "BlobData<byte[]>",
                    ComplexType = new ComplexTypeInfo
                    {
                        IsBlobStorage = true,
                        BlobDataInnerType = "byte[]"
                    }
                }
            }
        };
    }
}
