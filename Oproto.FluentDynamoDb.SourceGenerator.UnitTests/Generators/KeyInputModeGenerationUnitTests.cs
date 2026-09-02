using Oproto.FluentDynamoDb.SourceGenerator.Generators;
using Oproto.FluentDynamoDb.SourceGenerator.Models;
using SourceGenAccessModifier = Oproto.FluentDynamoDb.SourceGenerator.Models.AccessModifier;

namespace Oproto.FluentDynamoDb.SourceGenerator.UnitTests.Generators;

/// <summary>
/// Unit tests for KeyInputMode generation on string key with prefix.
/// Verifies that the source generator correctly adds or omits the
/// KeyInputMode mode = KeyInputMode.Default parameter on accessor methods
/// based on entity key configuration.
///
/// **Validates: Requirements 4.1, 4.7, 13.6**
/// </summary>
[Trait("Category", "Unit")]
public class KeyInputModeGenerationUnitTests
{
    /// <summary>
    /// Verifies that a string PK with prefix (no computed key) produces
    /// a Get accessor with KeyInputMode mode = KeyInputMode.Default parameter.
    /// </summary>
    [Fact]
    public void StringPkWithPrefix_NoComputed_GeneratesKeyInputModeParameter()
    {
        // Arrange
        var entity = CreateStringKeyWithPrefixEntity(
            pkPrefix: "ORDER", skPrefix: null, hasSk: false);

        // Act
        var generatedCode = GenerateCode(entity);

        // Assert
        generatedCode.Should().Contain("KeyInputMode mode = KeyInputMode.Default",
            "string PK with prefix and no typed overload should have KeyInputMode parameter");
    }

    /// <summary>
    /// Verifies that both PK and SK with prefixes produce the KeyInputMode parameter.
    /// </summary>
    [Fact]
    public void StringPkAndSkWithPrefixes_NoComputed_GeneratesKeyInputModeParameter()
    {
        // Arrange
        var entity = CreateStringKeyWithPrefixEntity(
            pkPrefix: "ORDER", skPrefix: "LINE", hasSk: true);

        // Act
        var generatedCode = GenerateCode(entity);

        // Assert
        generatedCode.Should().Contain("KeyInputMode mode = KeyInputMode.Default",
            "string PK+SK with prefixes and no typed overload should have KeyInputMode parameter");
    }

    /// <summary>
    /// Verifies that only SK with prefix (PK no prefix) still produces KeyInputMode parameter.
    /// </summary>
    [Fact]
    public void StringSkWithPrefix_PkNoPrefix_GeneratesKeyInputModeParameter()
    {
        // Arrange
        var entity = CreateStringKeyWithPrefixEntity(
            pkPrefix: null, skPrefix: "META", hasSk: true);

        // Act
        var generatedCode = GenerateCode(entity);

        // Assert
        generatedCode.Should().Contain("KeyInputMode mode = KeyInputMode.Default",
            "string SK with prefix (PK no prefix) should have KeyInputMode parameter");
    }

    /// <summary>
    /// Verifies that KeyInputMode parameter appears on all CRUD methods (Get, Delete, Update, ConditionCheck).
    /// </summary>
    [Fact]
    public void StringPkWithPrefix_KeyInputModeOnAllCrudMethods()
    {
        // Arrange
        var entity = CreateStringKeyWithPrefixEntity(
            pkPrefix: "CUSTOMER", skPrefix: null, hasSk: false);

        // Act
        var generatedCode = GenerateCode(entity);

        // Assert — verify all CRUD methods have the KeyInputMode parameter
        // The parameter should appear multiple times (Get, Delete, Update, ConditionCheck + table-level)
        var occurrences = generatedCode.Split("KeyInputMode mode = KeyInputMode.Default").Length - 1;
        occurrences.Should().BeGreaterThanOrEqualTo(4,
            "KeyInputMode parameter should appear on at least Get, Delete, Update, and ConditionCheck accessor methods");
    }

    /// <summary>
    /// Verifies that NO KeyInputMode parameter is generated when there is no prefix
    /// on any string key.
    /// </summary>
    [Fact]
    public void StringPkNoPrefix_NoSk_DoesNotGenerateKeyInputModeParameter()
    {
        // Arrange
        var entity = CreateStringKeyWithPrefixEntity(
            pkPrefix: null, skPrefix: null, hasSk: false);

        // Act
        var generatedCode = GenerateCode(entity);

        // Assert
        generatedCode.Should().NotContain("KeyInputMode mode = KeyInputMode.Default",
            "no prefix on any key means no KeyInputMode parameter");
    }

    /// <summary>
    /// Verifies that NO KeyInputMode parameter is generated when neither PK nor SK has a prefix.
    /// </summary>
    [Fact]
    public void StringPkAndSk_NoPrefixes_DoesNotGenerateKeyInputModeParameter()
    {
        // Arrange
        var entity = CreateStringKeyWithPrefixEntity(
            pkPrefix: null, skPrefix: null, hasSk: true);

        // Act
        var generatedCode = GenerateCode(entity);

        // Assert
        generatedCode.Should().NotContain("KeyInputMode mode = KeyInputMode.Default",
            "no prefix on either key means no KeyInputMode parameter");
    }

    /// <summary>
    /// Verifies that NO KeyInputMode parameter is generated when a typed overload exists
    /// (computed key with ≥2 source properties of non-string types).
    /// </summary>
    [Fact]
    public void ComputedPkWithPrefix_TypedOverloadGenerated_NoKeyInputModeParameter()
    {
        // Arrange — computed PK with prefix AND non-string source properties (non-ambiguous)
        var entity = CreateComputedPkWithPrefixEntity(
            prefix: "EVENT",
            sourceProps: new[] { ("Year", "int"), ("Month", "int") });

        // Act
        var generatedCode = GenerateCode(entity);

        // Assert
        generatedCode.Should().NotContain("KeyInputMode mode = KeyInputMode.Default",
            "when typed overload is generated, no KeyInputMode on standard overload");
        // Verify the typed overload IS generated instead
        generatedCode.Should().Contain("Get(int year, int month)",
            "typed overload should be present instead of KeyInputMode");
    }

    /// <summary>
    /// Verifies that KeyInputMode parameter is positioned after key parameters and before CancellationToken.
    /// </summary>
    [Fact]
    public void StringPkWithPrefix_KeyInputModePositionedAfterKeyParams()
    {
        // Arrange
        var entity = CreateStringKeyWithPrefixEntity(
            pkPrefix: "ORDER", skPrefix: "LINE", hasSk: true);

        // Act
        var generatedCode = GenerateCode(entity);

        // Assert — KeyInputMode appears after key params (string pk, string sk, KeyInputMode mode...)
        generatedCode.Should().Contain("string pk, string sk, KeyInputMode mode = KeyInputMode.Default",
            "KeyInputMode parameter should be positioned after key parameters");
    }

    /// <summary>
    /// Verifies that a single PK key with prefix generates the correct accessor signature:
    /// Get(string pk, KeyInputMode mode = KeyInputMode.Default)
    /// </summary>
    [Fact]
    public void StringPkOnlyWithPrefix_GeneratesCorrectSignature()
    {
        // Arrange
        var entity = CreateStringKeyWithPrefixEntity(
            pkPrefix: "USER", skPrefix: null, hasSk: false);

        // Act
        var generatedCode = GenerateCode(entity);

        // Assert — correct parameter order for PK-only entity
        generatedCode.Should().Contain("string pk, KeyInputMode mode = KeyInputMode.Default",
            "PK-only entity with prefix should have Get(string pk, KeyInputMode mode = KeyInputMode.Default)");
    }

    /// <summary>
    /// Verifies the generated code references KeyPrefixHelper.ApplyKeyPrefix
    /// when the KeyInputMode parameter is present.
    /// </summary>
    [Fact]
    public void StringPkWithPrefix_GeneratedBody_UsesKeyPrefixHelper()
    {
        // Arrange
        var entity = CreateStringKeyWithPrefixEntity(
            pkPrefix: "ORDER", skPrefix: null, hasSk: false);

        // Act
        var generatedCode = GenerateCode(entity);

        // Assert — generated method body uses KeyPrefixHelper
        generatedCode.Should().Contain("KeyPrefixHelper.ApplyKeyPrefix",
            "generated method body should use KeyPrefixHelper.ApplyKeyPrefix for prefix application");
    }

    /// <summary>
    /// Verifies the generated code calls KeyInputModeResolver.Resolve
    /// when the KeyInputMode parameter is present.
    /// </summary>
    [Fact]
    public void StringPkWithPrefix_GeneratedBody_UsesKeyInputModeResolver()
    {
        // Arrange
        var entity = CreateStringKeyWithPrefixEntity(
            pkPrefix: "ORDER", skPrefix: null, hasSk: false);

        // Act
        var generatedCode = GenerateCode(entity);

        // Assert — generated method body calls KeyInputModeResolver.Resolve
        generatedCode.Should().Contain("KeyInputModeResolver.Resolve",
            "generated method body should call KeyInputModeResolver.Resolve to resolve the effective mode");
    }

    #region Helper Methods

    private static string GenerateCode(EntityModel entity)
    {
        return TableGenerator.GenerateTableClass(entity.TableName, new List<EntityModel> { entity });
    }

    /// <summary>
    /// Creates an entity with string key(s) and optional prefix(es), NO computed key.
    /// </summary>
    private static EntityModel CreateStringKeyWithPrefixEntity(
        string? pkPrefix, string? skPrefix, bool hasSk)
    {
        var properties = new List<PropertyModel>
        {
            new PropertyModel
            {
                PropertyName = "Pk",
                PropertyType = "string",
                AttributeName = "pk",
                IsPartitionKey = true,
                KeyFormat = !string.IsNullOrEmpty(pkPrefix) ? new KeyFormatModel { Prefix = pkPrefix } : null
            }
        };

        if (hasSk)
        {
            properties.Add(new PropertyModel
            {
                PropertyName = "Sk",
                PropertyType = "string",
                AttributeName = "sk",
                IsSortKey = true,
                KeyFormat = !string.IsNullOrEmpty(skPrefix) ? new KeyFormatModel { Prefix = skPrefix } : null
            });
        }

        // Add a non-key property
        properties.Add(new PropertyModel
        {
            PropertyName = "Name",
            PropertyType = "string",
            AttributeName = "name"
        });

        return new EntityModel
        {
            ClassName = "TestEntity",
            Namespace = "TestNamespace",
            TableName = "test-table",
            Properties = properties.ToArray(),
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

    /// <summary>
    /// Creates an entity with a computed PK (≥2 source properties) and a prefix.
    /// This entity should qualify for typed overloads, and therefore NOT get KeyInputMode.
    /// </summary>
    private static EntityModel CreateComputedPkWithPrefixEntity(
        string prefix, (string Name, string Type)[] sourceProps)
    {
        var properties = new List<PropertyModel>();
        var pkSourceNames = new List<string>();

        // Add source properties for computed PK
        foreach (var (name, type) in sourceProps)
        {
            properties.Add(new PropertyModel
            {
                PropertyName = name,
                PropertyType = type,
                AttributeName = name.ToLowerInvariant()
            });
            pkSourceNames.Add(name);
        }

        // Add computed PK property with prefix
        properties.Add(new PropertyModel
        {
            PropertyName = "Pk",
            PropertyType = "string",
            AttributeName = "pk",
            IsPartitionKey = true,
            KeyFormat = new KeyFormatModel { Prefix = prefix },
            ComputedKey = new ComputedKeyModel
            {
                SourceProperties = pkSourceNames.ToArray(),
                Separator = "#"
            }
        });

        // Add a non-key property
        properties.Add(new PropertyModel
        {
            PropertyName = "Data",
            PropertyType = "string",
            AttributeName = "data"
        });

        return new EntityModel
        {
            ClassName = "TestEntity",
            Namespace = "TestNamespace",
            TableName = "test-table",
            Properties = properties.ToArray(),
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
