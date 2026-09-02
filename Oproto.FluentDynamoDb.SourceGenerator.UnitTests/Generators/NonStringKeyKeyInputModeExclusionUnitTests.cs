using Oproto.FluentDynamoDb.SourceGenerator.Generators;
using Oproto.FluentDynamoDb.SourceGenerator.Models;
using SourceGenAccessModifier = Oproto.FluentDynamoDb.SourceGenerator.Models.AccessModifier;

namespace Oproto.FluentDynamoDb.SourceGenerator.UnitTests.Generators;

/// <summary>
/// Unit tests for non-string key KeyInputMode exclusion.
/// Verifies that KeyInputMode is NOT added for non-string keys (int, Guid, enum),
/// and that it IS added when a string key with prefix coexists with a non-string key.
///
/// **Validates: Requirements 10.1, 10.2, 10.3, 10.4, 10.5**
/// </summary>
[Trait("Category", "Unit")]
public class NonStringKeyKeyInputModeExclusionUnitTests
{
    /// <summary>
    /// Verifies that an entity with an int partition key (no prefix) does NOT
    /// get a KeyInputMode parameter on its accessor methods.
    /// Requirement 10.1: Non-string key types are not considered for KeyInputMode eligibility.
    /// </summary>
    [Fact]
    public void IntPartitionKey_NoPrefix_DoesNotGetKeyInputMode()
    {
        // Arrange
        var entity = CreateSingleKeyEntity(pkType: "int", pkPrefix: null);

        // Act
        var generatedCode = GenerateCode(entity);

        // Assert — no KeyInputMode parameter
        generatedCode.Should().NotContain("KeyInputMode mode = KeyInputMode.Default");
    }

    /// <summary>
    /// Verifies that an entity with a Guid partition key (no prefix) does NOT
    /// get a KeyInputMode parameter on its accessor methods.
    /// Requirement 10.1: Non-string key types are not considered for KeyInputMode eligibility.
    /// </summary>
    [Fact]
    public void GuidPartitionKey_NoPrefix_DoesNotGetKeyInputMode()
    {
        // Arrange
        var entity = CreateSingleKeyEntity(pkType: "Guid", pkPrefix: null);

        // Act
        var generatedCode = GenerateCode(entity);

        // Assert — no KeyInputMode parameter
        generatedCode.Should().NotContain("KeyInputMode mode = KeyInputMode.Default");
    }

    /// <summary>
    /// Verifies that an entity with an enum partition key (no prefix) does NOT
    /// get a KeyInputMode parameter on its accessor methods.
    /// Requirement 10.1: Non-string key types are not considered for KeyInputMode eligibility.
    /// </summary>
    [Fact]
    public void EnumPartitionKey_NoPrefix_DoesNotGetKeyInputMode()
    {
        // Arrange
        var entity = CreateSingleKeyEntity(pkType: "MyNamespace.Status", pkPrefix: null, isEnum: true);

        // Act
        var generatedCode = GenerateCode(entity);

        // Assert — no KeyInputMode parameter
        generatedCode.Should().NotContain("KeyInputMode mode = KeyInputMode.Default");
    }

    /// <summary>
    /// Verifies that an entity with a non-string PK that has a configured prefix
    /// still does NOT get a KeyInputMode parameter (prefix on non-string keys is irrelevant).
    /// Requirement 10.1: Non-string key type is not considered regardless of prefix.
    /// </summary>
    [Fact]
    public void IntPartitionKey_WithPrefix_DoesNotGetKeyInputMode()
    {
        // Arrange
        var entity = CreateSingleKeyEntity(pkType: "int", pkPrefix: "ORDER");

        // Act
        var generatedCode = GenerateCode(entity);

        // Assert — no KeyInputMode parameter even though prefix is set
        generatedCode.Should().NotContain("KeyInputMode mode = KeyInputMode.Default");
    }

    /// <summary>
    /// Verifies that an entity with a non-string PK and a non-string SK (both with prefixes)
    /// does NOT get a KeyInputMode parameter.
    /// Requirement 10.2: Both keys must be evaluated independently — neither is string so no eligibility.
    /// </summary>
    [Fact]
    public void NonStringPkAndNonStringSk_BothWithPrefix_DoesNotGetKeyInputMode()
    {
        // Arrange
        var entity = CreateCompositeKeyEntity(
            pkType: "int", pkPrefix: "ORDER",
            skType: "Guid", skPrefix: "ITEM");

        // Act
        var generatedCode = GenerateCode(entity);

        // Assert — no KeyInputMode parameter
        generatedCode.Should().NotContain("KeyInputMode mode = KeyInputMode.Default");
    }

    /// <summary>
    /// Verifies that an entity with a non-string PK and a string SK WITH a prefix
    /// DOES get a KeyInputMode parameter.
    /// Requirement 10.3: If an entity has a non-string PK and a string SK with prefix,
    /// the KeyInputMode parameter SHALL be added.
    /// </summary>
    [Fact]
    public void NonStringPk_StringSkWithPrefix_GetsKeyInputMode()
    {
        // Arrange
        var entity = CreateCompositeKeyEntity(
            pkType: "int", pkPrefix: null,
            skType: "string", skPrefix: "ITEM");

        // Act
        var generatedCode = GenerateCode(entity);

        // Assert — KeyInputMode IS present because string SK has prefix
        generatedCode.Should().Contain("KeyInputMode mode = KeyInputMode.Default");
    }

    /// <summary>
    /// Verifies that an entity with a non-string PK and a string SK WITHOUT a prefix
    /// does NOT get a KeyInputMode parameter.
    /// Requirement 10.4: If the string SK has no configured prefix, no KeyInputMode.
    /// </summary>
    [Fact]
    public void NonStringPk_StringSkWithoutPrefix_DoesNotGetKeyInputMode()
    {
        // Arrange
        var entity = CreateCompositeKeyEntity(
            pkType: "int", pkPrefix: null,
            skType: "string", skPrefix: null);

        // Act
        var generatedCode = GenerateCode(entity);

        // Assert — no KeyInputMode because SK has no prefix
        generatedCode.Should().NotContain("KeyInputMode mode = KeyInputMode.Default");
    }

    /// <summary>
    /// Verifies that a Guid PK coexisting with a string SK with prefix gets KeyInputMode.
    /// Requirement 10.3: Non-string PK + string SK with prefix = KeyInputMode eligible.
    /// </summary>
    [Fact]
    public void GuidPk_StringSkWithPrefix_GetsKeyInputMode()
    {
        // Arrange
        var entity = CreateCompositeKeyEntity(
            pkType: "Guid", pkPrefix: null,
            skType: "string", skPrefix: "META");

        // Act
        var generatedCode = GenerateCode(entity);

        // Assert — KeyInputMode IS present
        generatedCode.Should().Contain("KeyInputMode mode = KeyInputMode.Default");
    }

    /// <summary>
    /// Verifies that an enum PK coexisting with a string SK with prefix gets KeyInputMode.
    /// Requirement 10.3: Non-string PK + string SK with prefix = KeyInputMode eligible.
    /// </summary>
    [Fact]
    public void EnumPk_StringSkWithPrefix_GetsKeyInputMode()
    {
        // Arrange
        var entity = CreateCompositeKeyEntity(
            pkType: "MyNamespace.Status", pkPrefix: null,
            skType: "string", skPrefix: "RECORD",
            pkIsEnum: true);

        // Act
        var generatedCode = GenerateCode(entity);

        // Assert — KeyInputMode IS present
        generatedCode.Should().Contain("KeyInputMode mode = KeyInputMode.Default");
    }

    /// <summary>
    /// Verifies that the generated code still uses SetKey for non-string key types
    /// (existing non-string key accessor behavior is preserved).
    /// Requirement 10.5: Non-string keys continue to use SetKey with inline AttributeValue construction.
    /// </summary>
    [Fact]
    public void IntPartitionKey_ContinuesToUseSetKeyPattern()
    {
        // Arrange
        var entity = CreateSingleKeyEntity(pkType: "int", pkPrefix: null);

        // Act
        var generatedCode = GenerateCode(entity);

        // Assert — SetKey pattern for non-string keys
        generatedCode.Should().Contain(".SetKey(k =>");
    }

    /// <summary>
    /// Verifies that a long PK without prefix does NOT get a KeyInputMode parameter.
    /// Requirement 10.1: long is a non-string type.
    /// </summary>
    [Fact]
    public void LongPartitionKey_NoPrefix_DoesNotGetKeyInputMode()
    {
        // Arrange
        var entity = CreateSingleKeyEntity(pkType: "long", pkPrefix: null);

        // Act
        var generatedCode = GenerateCode(entity);

        // Assert — no KeyInputMode parameter
        generatedCode.Should().NotContain("KeyInputMode mode = KeyInputMode.Default");
    }

    /// <summary>
    /// Verifies that a string PK with prefix + non-string SK (no prefix) gets KeyInputMode
    /// (PK is the eligible key, SK is non-string and irrelevant).
    /// Requirement 10.2: Each key is evaluated independently.
    /// </summary>
    [Fact]
    public void StringPkWithPrefix_NonStringSk_GetsKeyInputMode()
    {
        // Arrange
        var entity = CreateCompositeKeyEntity(
            pkType: "string", pkPrefix: "CUSTOMER",
            skType: "int", skPrefix: null);

        // Act
        var generatedCode = GenerateCode(entity);

        // Assert — KeyInputMode IS present because string PK has prefix
        generatedCode.Should().Contain("KeyInputMode mode = KeyInputMode.Default");
    }

    #region Helper Methods

    /// <summary>
    /// Generates code using the multi-entity overload of TableGenerator which
    /// produces entity accessor classes with typed overloads and KeyInputMode.
    /// </summary>
    private static string GenerateCode(EntityModel entity)
    {
        return TableGenerator.GenerateTableClass(entity.TableName, new List<EntityModel> { entity });
    }

    private static EntityModel CreateSingleKeyEntity(
        string pkType,
        string? pkPrefix,
        bool isEnum = false)
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
                    PropertyName = "Pk",
                    PropertyType = pkType,
                    AttributeName = "pk",
                    IsPartitionKey = true,
                    IsEnum = isEnum,
                    KeyFormat = pkPrefix != null ? new KeyFormatModel { Prefix = pkPrefix } : null
                },
                new PropertyModel
                {
                    PropertyName = "Data",
                    PropertyType = "string",
                    AttributeName = "data"
                }
            },
            Indexes = Array.Empty<IndexModel>(),
            IsScannable = false,
            IsDefault = true,
            EntityPropertyConfig = new EntityPropertyConfig
            {
                Generate = true,
                Modifier = SourceGenAccessModifier.Public
            },
            AccessorConfigs = new List<AccessorConfig>
            {
                new AccessorConfig
                {
                    Operations = TableOperation.Get | TableOperation.Update | TableOperation.Delete,
                    Modifier = SourceGenAccessModifier.Public
                }
            }
        };
    }

    private static EntityModel CreateCompositeKeyEntity(
        string pkType,
        string? pkPrefix,
        string skType,
        string? skPrefix,
        bool pkIsEnum = false,
        bool skIsEnum = false)
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
                    PropertyName = "Pk",
                    PropertyType = pkType,
                    AttributeName = "pk",
                    IsPartitionKey = true,
                    IsEnum = pkIsEnum,
                    KeyFormat = pkPrefix != null ? new KeyFormatModel { Prefix = pkPrefix } : null
                },
                new PropertyModel
                {
                    PropertyName = "Sk",
                    PropertyType = skType,
                    AttributeName = "sk",
                    IsSortKey = true,
                    IsEnum = skIsEnum,
                    KeyFormat = skPrefix != null ? new KeyFormatModel { Prefix = skPrefix } : null
                },
                new PropertyModel
                {
                    PropertyName = "Data",
                    PropertyType = "string",
                    AttributeName = "data"
                }
            },
            Indexes = Array.Empty<IndexModel>(),
            IsScannable = false,
            IsDefault = true,
            EntityPropertyConfig = new EntityPropertyConfig
            {
                Generate = true,
                Modifier = SourceGenAccessModifier.Public
            },
            AccessorConfigs = new List<AccessorConfig>
            {
                new AccessorConfig
                {
                    Operations = TableOperation.Get | TableOperation.Update | TableOperation.Delete,
                    Modifier = SourceGenAccessModifier.Public
                }
            }
        };
    }

    #endregion
}
