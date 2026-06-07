using AwesomeAssertions;
using Oproto.FluentDynamoDb.SourceGenerator.Generators;
using Oproto.FluentDynamoDb.SourceGenerator.Models;

namespace Oproto.FluentDynamoDb.SourceGenerator.UnitTests.Generators;

/// <summary>
/// Regression tests verifying that generated table constructors auto-register hydrators
/// for entities with encrypted or blob storage properties.
///
/// Bug: Users had to manually call DefaultEntityHydratorRegistry.Instance.Register{Entity}Hydrator()
/// before using Put/Get operations on encrypted entities. Without this, PutAsync deferred
/// serialization correctly but then failed to resolve because no hydrator was registered.
///
/// Fix: Generated table constructors now auto-register hydrators for all entities that require them.
/// </summary>
[Trait("Category", "Regression")]
[Trait("Category", "Encryption")]
public class HydratorAutoRegistrationTests
{
    /// <summary>
    /// Regression: Single-entity table with encrypted properties must auto-register
    /// the hydrator in the generated constructor.
    /// </summary>
    [Fact]
    public void SingleEntityTable_WithEncryptedProperty_AutoRegistersHydrator()
    {
        // Arrange
        var entity = CreateEncryptionOnlyEntity();

        // Act
        var result = TableGenerator.GenerateTableClass(entity);

        // Assert
        result.Should().Contain(
            "DefaultEntityHydratorRegistry.Instance.RegisterSecureEntityHydrator();",
            "Generated constructor must auto-register the hydrator for encrypted entities");
    }

    /// <summary>
    /// Regression: Single-entity table with blob storage properties must auto-register
    /// the hydrator in the generated constructor.
    /// </summary>
    [Fact]
    public void SingleEntityTable_WithBlobStorageProperty_AutoRegistersHydrator()
    {
        // Arrange
        var entity = CreateBlobStorageEntity();

        // Act
        var result = TableGenerator.GenerateTableClass(entity);

        // Assert
        result.Should().Contain(
            "DefaultEntityHydratorRegistry.Instance.RegisterBlobEntityHydrator();",
            "Generated constructor must auto-register the hydrator for blob storage entities");
    }

    /// <summary>
    /// Preservation: Single-entity table without encrypted or blob properties must NOT
    /// register any hydrator (no unnecessary overhead).
    /// </summary>
    [Fact]
    public void SingleEntityTable_WithoutAsyncProperties_DoesNotRegisterHydrator()
    {
        // Arrange
        var entity = CreatePlainEntity();

        // Act
        var result = TableGenerator.GenerateTableClass(entity);

        // Assert
        result.Should().NotContain(
            "RegisterPlainEntityHydrator",
            "Generated constructor must not register a hydrator for plain entities");
        result.Should().NotContain(
            "DefaultEntityHydratorRegistry.Instance.Register",
            "No hydrator registration should appear for plain entities");
    }

    /// <summary>
    /// Regression: Multi-entity table with one encrypted entity must auto-register
    /// only that entity's hydrator.
    /// </summary>
    [Fact]
    public void MultiEntityTable_WithOneEncryptedEntity_AutoRegistersOnlyThatHydrator()
    {
        // Arrange
        var entities = new List<EntityModel>
        {
            CreatePlainEntityForMultiTable("Order"),
            CreateEncryptionOnlyEntityForMultiTable("SecurePayment")
        };

        // Act
        var result = TableGenerator.GenerateTableClass("shared-table", entities);

        // Assert
        result.Should().Contain(
            "DefaultEntityHydratorRegistry.Instance.RegisterSecurePaymentHydrator();",
            "Must auto-register hydrator for the encrypted entity");
        result.Should().NotContain(
            "RegisterOrderHydrator",
            "Must not register hydrator for the plain entity");
    }

    /// <summary>
    /// Regression: Multi-entity table with multiple encrypted entities registers all hydrators.
    /// </summary>
    [Fact]
    public void MultiEntityTable_WithMultipleEncryptedEntities_RegistersAllHydrators()
    {
        // Arrange
        var entities = new List<EntityModel>
        {
            CreateEncryptionOnlyEntityForMultiTable("SecureUser"),
            CreateEncryptionOnlyEntityForMultiTable("SecurePayment")
        };

        // Act
        var result = TableGenerator.GenerateTableClass("shared-table", entities);

        // Assert
        result.Should().Contain("RegisterSecureUserHydrator();");
        result.Should().Contain("RegisterSecurePaymentHydrator();");
    }

    #region Entity Factories

    private static EntityModel CreateEncryptionOnlyEntity() => new()
    {
        ClassName = "SecureEntity",
        Namespace = "TestNamespace",
        TableName = "secure-table",
        IsDefault = true,
        Properties = new[]
        {
            new PropertyModel
            {
                PropertyName = "Pk",
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
                PropertyName = "Secret",
                AttributeName = "secret",
                PropertyType = "string",
                Security = new SecurityInfo { IsEncrypted = true }
            }
        }
    };

    private static EntityModel CreateBlobStorageEntity() => new()
    {
        ClassName = "BlobEntity",
        Namespace = "TestNamespace",
        TableName = "blob-table",
        IsDefault = true,
        Properties = new[]
        {
            new PropertyModel
            {
                PropertyName = "Pk",
                AttributeName = "pk",
                PropertyType = "string",
                IsPartitionKey = true
            },
            new PropertyModel
            {
                PropertyName = "Document",
                AttributeName = "doc_ref",
                PropertyType = "BlobData<byte[]>",
                ComplexType = new ComplexTypeInfo
                {
                    IsBlobStorage = true,
                    BlobDataInnerType = "byte[]"
                }
            }
        }
    };

    private static EntityModel CreatePlainEntity() => new()
    {
        ClassName = "PlainEntity",
        Namespace = "TestNamespace",
        TableName = "plain-table",
        IsDefault = true,
        Properties = new[]
        {
            new PropertyModel
            {
                PropertyName = "Pk",
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

    private static EntityModel CreatePlainEntityForMultiTable(string className) => new()
    {
        ClassName = className,
        Namespace = "TestNamespace",
        TableName = "shared-table",
        IsDefault = className == "Order",
        EntityPropertyConfig = new EntityPropertyConfig { Generate = true },
        Properties = new[]
        {
            new PropertyModel
            {
                PropertyName = "Pk",
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

    private static EntityModel CreateEncryptionOnlyEntityForMultiTable(string className) => new()
    {
        ClassName = className,
        Namespace = "TestNamespace",
        TableName = "shared-table",
        IsDefault = false,
        EntityPropertyConfig = new EntityPropertyConfig { Generate = true },
        Properties = new[]
        {
            new PropertyModel
            {
                PropertyName = "Pk",
                AttributeName = "pk",
                PropertyType = "string",
                IsPartitionKey = true
            },
            new PropertyModel
            {
                PropertyName = "Secret",
                AttributeName = "secret",
                PropertyType = "string",
                Security = new SecurityInfo { IsEncrypted = true }
            }
        }
    };

    #endregion
}
