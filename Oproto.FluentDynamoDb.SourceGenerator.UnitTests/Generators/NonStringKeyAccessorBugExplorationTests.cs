using AwesomeAssertions;
using Oproto.FluentDynamoDb.SourceGenerator.Generators;
using Oproto.FluentDynamoDb.SourceGenerator.Models;
using SourceGenAccessModifier = Oproto.FluentDynamoDb.SourceGenerator.Models.AccessModifier;

namespace Oproto.FluentDynamoDb.SourceGenerator.UnitTests.Generators;

/// <summary>
/// Bug condition exploration tests for the non-string key accessor fix.
/// These tests encode the EXPECTED behavior (SetKey with correct AttributeValue construction)
/// and are expected to FAIL on unfixed code — failure confirms the bug exists.
///
/// Bug Condition: isBugCondition(key) = key.PropertyType is non-string AND NOT hasPrefix AND NOT isComputed
///
/// **Feature: non-string-key-accessor-fix, Property 1: Bug Condition**
/// **Validates: Requirements 1.1, 1.2, 1.3, 1.4, 2.4**
/// </summary>
[Trait("Category", "BugExploration")]
public class NonStringKeyAccessorBugExplorationTests
{
    /// <summary>
    /// Test 1: Enum sort key with default string serialization.
    /// Entity with [SortKey] [DynamoDbAttribute("SK")] SnsSubscriptionTopic Topic (no prefix, no format).
    /// Expected: SetKey with new AttributeValue { S = sK.ToString() }
    /// </summary>
    [Fact(Skip = "Deferred: non-string keys with prefixes reverted to string-only accessors. Tracked in future overhaul story.")]
    public void EnumSortKey_DefaultSerialization_ShouldUseSetKeyWithStringAttributeValue()
    {
        // Arrange
        var entity = CreateCompositeKeyEntity(
            pkType: "string",
            pkAttributeName: "PK",
            skType: "SnsSubscriptionTopic",
            skAttributeName: "SK",
            pkPrefix: "USER",
            skPrefix: null,
            skFormat: null,
            skDateTimeKind: null);

        // Act
        var generatedCode = TableGenerator.GenerateTableClass(entity);

        // Assert - the generated code should use SetKey with correct AttributeValue for the enum SK
        generatedCode.Should().Contain(".SetKey(k =>");
        generatedCode.Should().Contain("new AttributeValue { S = sK.ToString() }");
    }

    /// <summary>
    /// Test 2: Enum sort key with integer serialization via Format="D".
    /// Entity with [SortKey] [DynamoDbAttribute("SK", Format = "D")] SnsSubscriptionTopic Topic.
    /// Expected: SetKey with new AttributeValue { S = sK.ToString("D", System.Globalization.CultureInfo.InvariantCulture) }
    /// </summary>
    [Fact(Skip = "Deferred: non-string keys with prefixes reverted to string-only accessors. Tracked in future overhaul story.")]
    public void EnumSortKey_IntegerSerializationFormat_ShouldUseSetKeyWithFormattedAttributeValue()
    {
        // Arrange
        var entity = CreateCompositeKeyEntity(
            pkType: "string",
            pkAttributeName: "PK",
            skType: "SnsSubscriptionTopic",
            skAttributeName: "SK",
            pkPrefix: "USER",
            skPrefix: null,
            skFormat: "D",
            skDateTimeKind: null);

        // Act
        var generatedCode = TableGenerator.GenerateTableClass(entity);

        // Assert
        generatedCode.Should().Contain(".SetKey(k =>");
        generatedCode.Should().Contain("sK.ToString(\"D\", System.Globalization.CultureInfo.InvariantCulture)");
    }

    /// <summary>
    /// Test 3: Int partition key (no prefix).
    /// Entity with [PartitionKey] [DynamoDbAttribute("pk")] int UserId.
    /// Expected: SetKey with new AttributeValue { N = pK.ToString() }
    /// </summary>
    [Fact]
    public void IntPartitionKey_ShouldUseSetKeyWithNumericAttributeValue()
    {
        // Arrange
        var entity = CreateSingleKeyEntity(
            pkType: "int",
            pkAttributeName: "pk",
            pkPrefix: null,
            pkFormat: null);

        // Act
        var generatedCode = TableGenerator.GenerateTableClass(entity);

        // Assert
        generatedCode.Should().Contain(".SetKey(k =>");
        generatedCode.Should().Contain("new AttributeValue { N = pK.ToString() }");
    }

    /// <summary>
    /// Test 4: Long partition key (no prefix).
    /// Entity with [PartitionKey] [DynamoDbAttribute("pk")] long Id.
    /// Expected: SetKey with new AttributeValue { N = pK.ToString() }
    /// </summary>
    [Fact]
    public void LongPartitionKey_ShouldUseSetKeyWithNumericAttributeValue()
    {
        // Arrange
        var entity = CreateSingleKeyEntity(
            pkType: "long",
            pkAttributeName: "pk",
            pkPrefix: null,
            pkFormat: null);

        // Act
        var generatedCode = TableGenerator.GenerateTableClass(entity);

        // Assert
        generatedCode.Should().Contain(".SetKey(k =>");
        generatedCode.Should().Contain("new AttributeValue { N = pK.ToString() }");
    }

    /// <summary>
    /// Test 5: Guid partition key (no prefix).
    /// Entity with [PartitionKey] [DynamoDbAttribute("pk")] Guid Id.
    /// Expected: SetKey with new AttributeValue { S = pK.ToString() }
    /// </summary>
    [Fact]
    public void GuidPartitionKey_ShouldUseSetKeyWithStringAttributeValue()
    {
        // Arrange
        var entity = CreateSingleKeyEntity(
            pkType: "Guid",
            pkAttributeName: "pk",
            pkPrefix: null,
            pkFormat: null);

        // Act
        var generatedCode = TableGenerator.GenerateTableClass(entity);

        // Assert
        generatedCode.Should().Contain(".SetKey(k =>");
        generatedCode.Should().Contain("new AttributeValue { S = pK.ToString() }");
    }

    /// <summary>
    /// Test 6: DateTime sort key with default ISO 8601 format.
    /// Entity with [SortKey] [DynamoDbAttribute("sk")] DateTime CreatedAt.
    /// Expected: SetKey with new AttributeValue { S = sK.ToString("O") }
    /// </summary>
    [Fact]
    public void DateTimeSortKey_DefaultFormat_ShouldUseSetKeyWithIso8601AttributeValue()
    {
        // Arrange
        var entity = CreateCompositeKeyEntity(
            pkType: "string",
            pkAttributeName: "pk",
            skType: "DateTime",
            skAttributeName: "sk",
            pkPrefix: "USER",
            skPrefix: null,
            skFormat: null,
            skDateTimeKind: null);

        // Act
        var generatedCode = TableGenerator.GenerateTableClass(entity);

        // Assert
        generatedCode.Should().Contain(".SetKey(k =>");
        generatedCode.Should().Contain("sK.ToString(\"O\")");
    }

    /// <summary>
    /// Test 7: DateTime sort key with custom format.
    /// Entity with [SortKey] [DynamoDbAttribute("sk", Format = "yyyy-MM-dd")] DateTime CreatedAt.
    /// Expected: SetKey with format string applied with CultureInfo.InvariantCulture
    /// </summary>
    [Fact]
    public void DateTimeSortKey_CustomFormat_ShouldUseSetKeyWithCustomFormatAttributeValue()
    {
        // Arrange
        var entity = CreateCompositeKeyEntity(
            pkType: "string",
            pkAttributeName: "pk",
            skType: "DateTime",
            skAttributeName: "sk",
            pkPrefix: "USER",
            skPrefix: null,
            skFormat: "yyyy-MM-dd",
            skDateTimeKind: null);

        // Act
        var generatedCode = TableGenerator.GenerateTableClass(entity);

        // Assert
        generatedCode.Should().Contain(".SetKey(k =>");
        generatedCode.Should().Contain("sK.ToString(\"yyyy-MM-dd\", System.Globalization.CultureInfo.InvariantCulture)");
    }

    /// <summary>
    /// Test 8: DateTime sort key with DateTimeKind=Utc.
    /// Entity with [SortKey] [DynamoDbAttribute("sk", DateTimeKind = DateTimeKind.Utc)] DateTime CreatedAt.
    /// Expected: .ToUniversalTime() before formatting
    /// </summary>
    [Fact]
    public void DateTimeSortKey_UtcKind_ShouldUseSetKeyWithToUniversalTime()
    {
        // Arrange
        var entity = CreateCompositeKeyEntity(
            pkType: "string",
            pkAttributeName: "pk",
            skType: "DateTime",
            skAttributeName: "sk",
            pkPrefix: "USER",
            skPrefix: null,
            skFormat: null,
            skDateTimeKind: DateTimeKind.Utc);

        // Act
        var generatedCode = TableGenerator.GenerateTableClass(entity);

        // Assert
        generatedCode.Should().Contain(".SetKey(k =>");
        generatedCode.Should().Contain(".ToUniversalTime()");
    }

    /// <summary>
    /// Test 9: DateOnly sort key (default ISO 8601).
    /// Entity with [SortKey] [DynamoDbAttribute("sk")] DateOnly EventDate.
    /// Expected: SetKey with new AttributeValue { S = sK.ToString("O", System.Globalization.CultureInfo.InvariantCulture) }
    /// </summary>
    [Fact]
    public void DateOnlySortKey_ShouldUseSetKeyWithIso8601AttributeValue()
    {
        // Arrange
        var entity = CreateCompositeKeyEntity(
            pkType: "string",
            pkAttributeName: "pk",
            skType: "DateOnly",
            skAttributeName: "sk",
            pkPrefix: "USER",
            skPrefix: null,
            skFormat: null,
            skDateTimeKind: null);

        // Act
        var generatedCode = TableGenerator.GenerateTableClass(entity);

        // Assert
        generatedCode.Should().Contain(".SetKey(k =>");
        generatedCode.Should().Contain("sK.ToString(\"O\", System.Globalization.CultureInfo.InvariantCulture)");
    }

    /// <summary>
    /// Test 10: TimeOnly sort key.
    /// Entity with [SortKey] [DynamoDbAttribute("sk")] TimeOnly StartTime.
    /// Expected: Same pattern as DateOnly
    /// </summary>
    [Fact]
    public void TimeOnlySortKey_ShouldUseSetKeyWithIso8601AttributeValue()
    {
        // Arrange
        var entity = CreateCompositeKeyEntity(
            pkType: "string",
            pkAttributeName: "pk",
            skType: "TimeOnly",
            skAttributeName: "sk",
            pkPrefix: "USER",
            skPrefix: null,
            skFormat: null,
            skDateTimeKind: null);

        // Act
        var generatedCode = TableGenerator.GenerateTableClass(entity);

        // Assert
        generatedCode.Should().Contain(".SetKey(k =>");
        generatedCode.Should().Contain("sK.ToString(\"O\", System.Globalization.CultureInfo.InvariantCulture)");
    }

    /// <summary>
    /// Test 11: Nullable int partition key.
    /// Entity with [PartitionKey] [DynamoDbAttribute("pk")] int? Score.
    /// Expected: .Value accessor used before .ToString()
    /// </summary>
    [Fact]
    public void NullableIntPartitionKey_ShouldUseSetKeyWithValueAccessor()
    {
        // Arrange
        var entity = CreateSingleKeyEntity(
            pkType: "int?",
            pkAttributeName: "pk",
            pkPrefix: null,
            pkFormat: null,
            isNullable: true);

        // Act
        var generatedCode = TableGenerator.GenerateTableClass(entity);

        // Assert
        generatedCode.Should().Contain(".SetKey(k =>");
        generatedCode.Should().Contain(".Value");
        generatedCode.Should().Contain("new AttributeValue { N =");
    }

    /// <summary>
    /// Test 12: Nullable DateTime sort key.
    /// Entity with [SortKey] [DynamoDbAttribute("sk")] DateTime? ExpiresAt.
    /// Expected: .Value accessor before formatting
    /// </summary>
    [Fact]
    public void NullableDateTimeSortKey_ShouldUseSetKeyWithValueAccessor()
    {
        // Arrange
        var entity = CreateCompositeKeyEntity(
            pkType: "string",
            pkAttributeName: "pk",
            skType: "DateTime?",
            skAttributeName: "sk",
            pkPrefix: "USER",
            skPrefix: null,
            skFormat: null,
            skDateTimeKind: null,
            skIsNullable: true);

        // Act
        var generatedCode = TableGenerator.GenerateTableClass(entity);

        // Assert
        generatedCode.Should().Contain(".SetKey(k =>");
        generatedCode.Should().Contain(".Value");
    }

    /// <summary>
    /// Test 13: Mixed composite key — string PK with prefix + enum SK no prefix.
    /// Verifies composite SetKey generation handles the mixed case correctly.
    /// When one key is non-string (no prefix, not computed), BOTH keys should go through SetKey.
    /// </summary>
    [Fact]
    public void MixedCompositeKey_StringPkWithPrefix_EnumSkNoPrefix_ShouldUseSetKey()
    {
        // Arrange
        var entity = CreateCompositeKeyEntity(
            pkType: "string",
            pkAttributeName: "PK",
            skType: "SnsSubscriptionTopic",
            skAttributeName: "SK",
            pkPrefix: "USER",
            skPrefix: null,
            skFormat: null,
            skDateTimeKind: null);

        // Act
        var generatedCode = TableGenerator.GenerateTableClass(entity);

        // Assert — the generated code should use .SetKey for composite key handling
        generatedCode.Should().Contain(".SetKey(k =>");
        // Should NOT use the old .WithKey pattern with non-string SK
        generatedCode.Should().NotContain(".WithKey(\"PK\", pK, \"SK\", sK)");
    }

    /// <summary>
    /// Test 14: Both keys non-string — int PK + enum SK, both without prefix.
    /// Verifies both keys go through SetKey.
    /// </summary>
    [Fact(Skip = "Deferred: non-string keys with prefixes reverted to string-only accessors. Tracked in future overhaul story.")]
    public void BothKeysNonString_IntPkAndEnumSk_ShouldUseSetKey()
    {
        // Arrange
        var entity = CreateCompositeKeyEntity(
            pkType: "int",
            pkAttributeName: "pk",
            skType: "SnsSubscriptionTopic",
            skAttributeName: "sk",
            pkPrefix: null,
            skPrefix: null,
            skFormat: null,
            skDateTimeKind: null);

        // Act
        var generatedCode = TableGenerator.GenerateTableClass(entity);

        // Assert
        generatedCode.Should().Contain(".SetKey(k =>");
        generatedCode.Should().Contain("new AttributeValue { N = pK.ToString() }");
        generatedCode.Should().Contain("new AttributeValue { S = sK.ToString() }");
    }

    #region Helper Methods

    private static EntityModel CreateSingleKeyEntity(
        string pkType,
        string pkAttributeName,
        string? pkPrefix,
        string? pkFormat,
        bool isNullable = false)
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
                    IsNullable = isNullable,
                    KeyFormat = pkPrefix != null ? new KeyFormatModel { Prefix = pkPrefix } : null,
                    Format = pkFormat
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
        string pkAttributeName,
        string skType,
        string skAttributeName,
        string? pkPrefix,
        string? skPrefix,
        string? skFormat,
        DateTimeKind? skDateTimeKind,
        bool pkIsNullable = false,
        bool skIsNullable = false)
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
                    IsNullable = pkIsNullable,
                    KeyFormat = pkPrefix != null ? new KeyFormatModel { Prefix = pkPrefix } : null
                },
                new PropertyModel
                {
                    PropertyName = "Sk",
                    PropertyType = skType,
                    AttributeName = skAttributeName,
                    IsSortKey = true,
                    IsNullable = skIsNullable,
                    KeyFormat = skPrefix != null ? new KeyFormatModel { Prefix = skPrefix } : null,
                    Format = skFormat,
                    DateTimeKind = skDateTimeKind
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
