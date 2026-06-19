using AwesomeAssertions;
using Oproto.FluentDynamoDb.SourceGenerator.Generators;
using Oproto.FluentDynamoDb.SourceGenerator.Models;
using SourceGenAccessModifier = Oproto.FluentDynamoDb.SourceGenerator.Models.AccessModifier;

namespace Oproto.FluentDynamoDb.SourceGenerator.UnitTests.Generators;

/// <summary>
/// Preservation property tests for the non-string key accessor fix.
/// These tests verify that existing correct behavior is preserved for entities
/// where all keys are string-typed, have a prefix, or are computed.
/// All tests must PASS on unfixed code to establish the preservation baseline.
///
/// **Feature: non-string-key-accessor-fix, Property 2: Preservation**
/// **Validates: Requirements 3.1, 3.2, 3.3, 3.4, 3.5**
/// </summary>
[Trait("Category", "Preservation")]
public class NonStringKeyAccessorPreservationTests
{
    /// <summary>
    /// Test 1: String PK, no prefix, not computed.
    /// Entity with [PartitionKey] string Id — verify .WithKey("id", id) with string parameter.
    /// Preservation: string keys without prefix continue using .WithKey().
    /// </summary>
    [Fact]
    public void StringPartitionKey_NoPrefix_NotComputed_ShouldUseWithKey()
    {
        // Arrange
        var entity = CreateSingleKeyEntity(
            pkType: "string",
            pkAttributeName: "id",
            pkPrefix: null);

        // Act
        var generatedCode = TableGenerator.GenerateTableClass(entity);

        // Assert — should use .WithKey() with string parameter
        generatedCode.Should().Contain(".WithKey(\"id\", id)");
        generatedCode.Should().NotContain(".SetKey(");
    }

    /// <summary>
    /// Test 2: String PK with prefix.
    /// Entity with [PartitionKey(Prefix = "USER")] string Pk — verify string parameter type and .WithKey("pk", pk).
    /// Preservation: prefixed string keys continue using .WithKey().
    /// </summary>
    [Fact]
    public void StringPartitionKey_WithPrefix_ShouldUseWithKey()
    {
        // Arrange
        var entity = CreateSingleKeyEntity(
            pkType: "string",
            pkAttributeName: "pk",
            pkPrefix: "USER");

        // Act
        var generatedCode = TableGenerator.GenerateTableClass(entity);

        // Assert — should use .WithKey() with string parameter
        generatedCode.Should().Contain(".WithKey(\"pk\", pk)");
        generatedCode.Should().NotContain(".SetKey(");
        // Parameter type should be string
        generatedCode.Should().Contain("Get(string pk)");
    }

    /// <summary>
    /// Test 3: String SK with prefix.
    /// Entity with [SortKey(Prefix = "ORDER")] string Sk — verify string parameter type and composite .WithKey().
    /// Preservation: prefixed string sort keys continue using .WithKey().
    /// </summary>
    [Fact]
    public void StringSortKey_WithPrefix_ShouldUseWithKey()
    {
        // Arrange
        var entity = CreateCompositeKeyEntity(
            pkType: "string",
            pkAttributeName: "pk",
            skType: "string",
            skAttributeName: "sk",
            pkPrefix: "USER",
            skPrefix: "ORDER");

        // Act
        var generatedCode = TableGenerator.GenerateTableClass(entity);

        // Assert — should use .WithKey() with string parameters
        generatedCode.Should().Contain(".WithKey(\"pk\", pk, \"sk\", sk)");
        generatedCode.Should().NotContain(".SetKey(");
        // Parameter types should be string
        generatedCode.Should().Contain("Get(string pk, string sk)");
    }

    /// <summary>
    /// Test 4: Computed PK.
    /// Entity with computed key (IsComputed == true) — verify string parameter type and .WithKey().
    /// Preservation: computed keys always have string parameter and use .WithKey().
    /// </summary>
    [Fact]
    public void ComputedPartitionKey_ShouldUseWithKey()
    {
        // Arrange
        var entity = CreateSingleKeyEntity(
            pkType: "string",
            pkAttributeName: "pk",
            pkPrefix: null,
            isComputed: true);

        // Act
        var generatedCode = TableGenerator.GenerateTableClass(entity);

        // Assert — should use .WithKey() with string parameter
        generatedCode.Should().Contain(".WithKey(\"pk\", pk)");
        generatedCode.Should().NotContain(".SetKey(");
        // Parameter type should be string
        generatedCode.Should().Contain("Get(string pk)");
    }

    /// <summary>
    /// Test 5: Composite string keys (both prefixed).
    /// Entity with string PK + string SK both with prefix — verify .WithKey("PK", pK, "SK", sK).
    /// Preservation: both prefixed string keys continue using composite .WithKey().
    /// </summary>
    [Fact]
    public void CompositeStringKeys_BothPrefixed_ShouldUseWithKey()
    {
        // Arrange
        var entity = CreateCompositeKeyEntity(
            pkType: "string",
            pkAttributeName: "PK",
            skType: "string",
            skAttributeName: "SK",
            pkPrefix: "CUSTOMER",
            skPrefix: "ORDER");

        // Act
        var generatedCode = TableGenerator.GenerateTableClass(entity);

        // Assert — should use .WithKey() with composite string parameters
        generatedCode.Should().Contain(".WithKey(\"PK\", pK, \"SK\", sK)");
        generatedCode.Should().NotContain(".SetKey(");
    }

    /// <summary>
    /// Test 6: Composite string keys (no prefix).
    /// Entity with string PK + string SK, no prefix — verify .WithKey("pk", pk, "sk", sk).
    /// Preservation: unprefixed string keys still use .WithKey() because they are string type.
    /// </summary>
    [Fact]
    public void CompositeStringKeys_NoPrefix_ShouldUseWithKey()
    {
        // Arrange
        var entity = CreateCompositeKeyEntity(
            pkType: "string",
            pkAttributeName: "pk",
            skType: "string",
            skAttributeName: "sk",
            pkPrefix: null,
            skPrefix: null);

        // Act
        var generatedCode = TableGenerator.GenerateTableClass(entity);

        // Assert — should use .WithKey() with composite string parameters
        generatedCode.Should().Contain(".WithKey(\"pk\", pk, \"sk\", sk)");
        generatedCode.Should().NotContain(".SetKey(");
    }

    /// <summary>
    /// Test 7: Non-string key WITH prefix (should still be string parameter).
    /// Entity with [PartitionKey(Prefix = "ID")] int Id — verify parameter type is string and .WithKey() used.
    /// Preservation: prefix forces string parameter type, so .WithKey() is used.
    /// </summary>
    [Fact]
    public void NonStringPartitionKey_WithPrefix_ShouldUseWithKeyAndStringParameter()
    {
        // Arrange
        var entity = CreateSingleKeyEntity(
            pkType: "int",
            pkAttributeName: "pk",
            pkPrefix: "ID");

        // Act
        var generatedCode = TableGenerator.GenerateTableClass(entity);

        // Assert — prefix means parameter type is string and .WithKey() is used
        generatedCode.Should().Contain(".WithKey(\"pk\",");
        generatedCode.Should().NotContain(".SetKey(");
    }

    /// <summary>
    /// Test 8: Non-string key WITH computed (should still be string parameter).
    /// Entity with computed non-string key — verify parameter type is string and .WithKey() used.
    /// Preservation: computed keys always have string parameter regardless of underlying type.
    /// </summary>
    [Fact]
    public void NonStringPartitionKey_WithComputed_ShouldUseWithKeyAndStringParameter()
    {
        // Arrange
        var entity = CreateSingleKeyEntity(
            pkType: "int",
            pkAttributeName: "pk",
            pkPrefix: null,
            isComputed: true);

        // Act
        var generatedCode = TableGenerator.GenerateTableClass(entity);

        // Assert — computed key means parameter type is string and .WithKey() is used
        generatedCode.Should().Contain(".WithKey(\"pk\",");
        generatedCode.Should().NotContain(".SetKey(");
    }

    #region Helper Methods

    private static EntityModel CreateSingleKeyEntity(
        string pkType,
        string pkAttributeName,
        string? pkPrefix,
        bool isComputed = false)
    {
        var pkProperty = new PropertyModel
        {
            PropertyName = "Pk",
            PropertyType = pkType,
            AttributeName = pkAttributeName,
            IsPartitionKey = true,
            KeyFormat = pkPrefix != null ? new KeyFormatModel { Prefix = pkPrefix } : null,
            ComputedKey = isComputed ? new ComputedKeyModel { SourceProperties = new[] { "Field1", "Field2" }, Separator = "#" } : null
        };

        return new EntityModel
        {
            ClassName = "TestEntity",
            Namespace = "TestNamespace",
            TableName = "test-table",
            Properties = new[] { pkProperty },
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
        string pkAttributeName,
        string skType,
        string skAttributeName,
        string? pkPrefix,
        string? skPrefix)
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
                    AttributeName = pkAttributeName,
                    IsPartitionKey = true,
                    KeyFormat = pkPrefix != null ? new KeyFormatModel { Prefix = pkPrefix } : null
                },
                new PropertyModel
                {
                    PropertyName = "Sk",
                    PropertyType = skType,
                    AttributeName = skAttributeName,
                    IsSortKey = true,
                    KeyFormat = skPrefix != null ? new KeyFormatModel { Prefix = skPrefix } : null
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
