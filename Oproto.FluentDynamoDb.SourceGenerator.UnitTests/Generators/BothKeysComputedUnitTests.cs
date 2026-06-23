using Oproto.FluentDynamoDb.SourceGenerator.Generators;
using Oproto.FluentDynamoDb.SourceGenerator.Models;
using SourceGenAccessModifier = Oproto.FluentDynamoDb.SourceGenerator.Models.AccessModifier;

namespace Oproto.FluentDynamoDb.SourceGenerator.UnitTests.Generators;

/// <summary>
/// Unit tests for source generator output verification when both PK and SK are computed.
/// Verifies that a single typed overload is generated with all PK source params followed
/// by all SK source params, correct types, camelCase names, delegation to Keys.Build methods,
/// and that the standard string overload remains present.
///
/// Requirements: 1.3, 13.3
/// </summary>
[Trait("Category", "Unit")]
public class BothKeysComputedUnitTests
{
    /// <summary>
    /// Verifies that when both PK and SK are computed, a single typed overload exists
    /// with all PK source property parameters first, then all SK source property parameters.
    /// </summary>
    [Fact]
    public void BothKeysComputed_GeneratesSingleTypedOverload_WithAllPkThenAllSkParams()
    {
        // Arrange
        var entity = CreateBothKeysComputedEntity(
            pkSourceProps: new[] { ("Year", "int"), ("Month", "int"), ("Day", "int") },
            skSourceProps: new[] { ("Category", "string"), ("ItemId", "long") });

        // Act
        var generatedCode = GenerateCode(entity);

        // Assert — typed overload has PK params first, then SK params
        generatedCode.Should().Contain("Get(int year, int month, int day, string category, long itemId)");
    }

    /// <summary>
    /// Verifies that all parameters in the typed overload have correct camelCase names.
    /// </summary>
    [Fact]
    public void BothKeysComputed_TypedOverloadParameters_HaveCamelCaseNames()
    {
        // Arrange
        var entity = CreateBothKeysComputedEntity(
            pkSourceProps: new[] { ("TenantId", "int"), ("Region", "long") },
            skSourceProps: new[] { ("OrderDate", "DateTime"), ("SequenceNum", "long") });

        // Act
        var generatedCode = GenerateCode(entity);

        // Assert — parameter names are camelCase versions of property names
        generatedCode.Should().Contain("tenantId");
        generatedCode.Should().Contain("region");
        generatedCode.Should().Contain("orderDate");
        generatedCode.Should().Contain("sequenceNum");
    }

    /// <summary>
    /// Verifies that all parameters in the typed overload have correct types matching
    /// their source property types.
    /// </summary>
    [Fact]
    public void BothKeysComputed_TypedOverloadParameters_HaveCorrectTypes()
    {
        // Arrange
        var entity = CreateBothKeysComputedEntity(
            pkSourceProps: new[] { ("Year", "int"), ("Region", "long") },
            skSourceProps: new[] { ("Timestamp", "DateTime"), ("Priority", "Guid") });

        // Act
        var generatedCode = GenerateCode(entity);

        // Assert — typed overload has correct parameter types
        generatedCode.Should().Contain("Get(int year, long region, DateTime timestamp, Guid priority)");
    }

    /// <summary>
    /// Verifies that the generated method body calls Entity.Keys.BuildPk(...) for the PK
    /// and Entity.Keys.BuildSk(...) for the SK independently.
    /// </summary>
    [Fact]
    public void BothKeysComputed_MethodBody_CallsBuildPkAndBuildSkIndependently()
    {
        // Arrange
        var entity = CreateBothKeysComputedEntity(
            entityName: "Event",
            pkSourceProps: new[] { ("Year", "int"), ("Month", "int") },
            skSourceProps: new[] { ("Category", "long"), ("ItemId", "Guid") });

        // Act
        var generatedCode = GenerateCode(entity);

        // Assert — method body calls both Build methods independently
        generatedCode.Should().Contain("Event.Keys.BuildPk(year, month)");
        generatedCode.Should().Contain("Event.Keys.BuildSk(category, itemId)");
    }

    /// <summary>
    /// Verifies that the typed overload delegates using return Get(computedPk, computedSk).
    /// </summary>
    [Fact]
    public void BothKeysComputed_Delegation_UsesReturnGetWithBothComputedValues()
    {
        // Arrange
        var entity = CreateBothKeysComputedEntity(
            pkSourceProps: new[] { ("Year", "int"), ("Month", "int") },
            skSourceProps: new[] { ("Type", "long"), ("Id", "Guid") });

        // Act
        var generatedCode = GenerateCode(entity);

        // Assert — delegation uses both computed values
        generatedCode.Should().Contain("return Get(computedPk, computedSk)");
    }

    /// <summary>
    /// Verifies that the standard string overload (string pk, string sk) is still present
    /// alongside the typed overload.
    /// </summary>
    [Fact]
    public void BothKeysComputed_StandardStringOverload_IsAlsoPresent()
    {
        // Arrange
        var entity = CreateBothKeysComputedEntity(
            pkSourceProps: new[] { ("Year", "int"), ("Month", "int") },
            skSourceProps: new[] { ("Category", "long"), ("ItemId", "Guid") });

        // Act
        var generatedCode = GenerateCode(entity);

        // Assert — standard string overload exists (uses camelCase attribute names: pk, sk)
        generatedCode.Should().Contain("Get(string pk, string sk)");
    }

    /// <summary>
    /// Verifies that no separate PK-only or SK-only typed overloads are generated
    /// when both keys are computed (only the single combined overload exists).
    /// </summary>
    [Fact]
    public void BothKeysComputed_NoSeparatePkOnlyOrSkOnlyOverloads()
    {
        // Arrange
        var entity = CreateBothKeysComputedEntity(
            entityName: "Event",
            pkSourceProps: new[] { ("Year", "int"), ("Month", "int") },
            skSourceProps: new[] { ("Category", "long"), ("ItemId", "Guid") });

        // Act
        var generatedCode = GenerateCode(entity);

        // Assert — the typed overload is the combined one
        var typedOverloadSignature = "Get(int year, int month, long category, Guid itemId)";
        generatedCode.Should().Contain(typedOverloadSignature);

        // Count occurrences of "GetItemRequestBuilder<Event> Get(" to verify
        // only the standard + one typed overload exist at each level (entity accessor + table level)
        var getOverloads = generatedCode.Split("GetItemRequestBuilder<Event> Get(")
            .Length - 1;
        // Should have exactly 4: entity accessor standard + typed, table-level standard + typed
        getOverloads.Should().Be(4);
    }

    /// <summary>
    /// Verifies that the typed overload also generates for Delete, Update, and ConditionCheck methods
    /// with identical parameter signatures.
    /// </summary>
    [Fact]
    public void BothKeysComputed_TypedOverloadGeneratedForAllCrudMethods()
    {
        // Arrange
        var entity = CreateBothKeysComputedEntity(
            entityName: "Event",
            pkSourceProps: new[] { ("Year", "int"), ("Month", "int") },
            skSourceProps: new[] { ("Category", "long"), ("ItemId", "Guid") });

        // Act
        var generatedCode = GenerateCode(entity);

        // Assert — typed overloads for all CRUD methods with same signature
        var expectedParams = "int year, int month, long category, Guid itemId";
        generatedCode.Should().Contain($"Get({expectedParams})");
        generatedCode.Should().Contain($"Delete({expectedParams})");
        generatedCode.Should().Contain($"Update({expectedParams})");
        generatedCode.Should().Contain($"ConditionCheck({expectedParams})");
    }

    /// <summary>
    /// Verifies that when both keys are computed, no KeyInputMode parameter is added
    /// to the standard string overload.
    /// </summary>
    [Fact]
    public void BothKeysComputed_NoKeyInputModeOnStandardOverload()
    {
        // Arrange - entity with both keys computed and prefixes
        var entity = CreateBothKeysComputedEntity(
            pkSourceProps: new[] { ("Year", "int"), ("Month", "int") },
            skSourceProps: new[] { ("Category", "long"), ("ItemId", "Guid") },
            pkPrefix: "EVENT",
            skPrefix: "ITEM");

        // Act
        var generatedCode = GenerateCode(entity);

        // Assert — no KeyInputMode on any overload
        generatedCode.Should().NotContain("KeyInputMode mode = KeyInputMode.Default");
    }

    /// <summary>
    /// Verifies correct generation when PK and SK have different numbers of source properties.
    /// </summary>
    [Fact]
    public void BothKeysComputed_DifferentSourcePropertyCounts_GeneratesCorrectly()
    {
        // Arrange - PK has 3 source props, SK has 2
        var entity = CreateBothKeysComputedEntity(
            entityName: "Booking",
            pkSourceProps: new[] { ("Hotel", "int"), ("Floor", "int"), ("Room", "int") },
            skSourceProps: new[] { ("CheckInDate", "DateTime"), ("GuestId", "Guid") });

        // Act
        var generatedCode = GenerateCode(entity);

        // Assert — correct combined parameter list
        generatedCode.Should().Contain(
            "Get(int hotel, int floor, int room, DateTime checkInDate, Guid guestId)");
        generatedCode.Should().Contain("Booking.Keys.BuildPk(hotel, floor, room)");
        generatedCode.Should().Contain("Booking.Keys.BuildSk(checkInDate, guestId)");
    }

    #region Helper Methods

    /// <summary>
    /// Generates code using the multi-entity overload of TableGenerator which
    /// produces entity accessor classes with typed overloads.
    /// </summary>
    private static string GenerateCode(EntityModel entity)
    {
        return TableGenerator.GenerateTableClass(entity.TableName, new List<EntityModel> { entity });
    }

    private static EntityModel CreateBothKeysComputedEntity(
        (string Name, string Type)[] pkSourceProps,
        (string Name, string Type)[] skSourceProps,
        string entityName = "TestEntity",
        string tableName = "test-table",
        string? pkPrefix = null,
        string? skPrefix = null)
    {
        var properties = new List<PropertyModel>();
        var pkSourceNames = new List<string>();
        var skSourceNames = new List<string>();

        // Add PK source properties
        foreach (var (name, type) in pkSourceProps)
        {
            properties.Add(new PropertyModel
            {
                PropertyName = name,
                PropertyType = type,
                AttributeName = name.ToLowerInvariant()
            });
            pkSourceNames.Add(name);
        }

        // Add computed PK property
        properties.Add(new PropertyModel
        {
            PropertyName = "Pk",
            PropertyType = "string",
            AttributeName = "pk",
            IsPartitionKey = true,
            KeyFormat = pkPrefix != null ? new KeyFormatModel { Prefix = pkPrefix } : null,
            ComputedKey = new ComputedKeyModel
            {
                SourceProperties = pkSourceNames.ToArray(),
                Separator = "#"
            }
        });

        // Add SK source properties
        foreach (var (name, type) in skSourceProps)
        {
            properties.Add(new PropertyModel
            {
                PropertyName = name,
                PropertyType = type,
                AttributeName = name.ToLowerInvariant()
            });
            skSourceNames.Add(name);
        }

        // Add computed SK property
        properties.Add(new PropertyModel
        {
            PropertyName = "Sk",
            PropertyType = "string",
            AttributeName = "sk",
            IsSortKey = true,
            KeyFormat = skPrefix != null ? new KeyFormatModel { Prefix = skPrefix } : null,
            ComputedKey = new ComputedKeyModel
            {
                SourceProperties = skSourceNames.ToArray(),
                Separator = "#"
            }
        });

        return new EntityModel
        {
            ClassName = entityName,
            Namespace = "TestNamespace",
            TableName = tableName,
            Properties = properties.ToArray(),
            Indexes = Array.Empty<IndexModel>(),
            IsScannable = false,
            IsDefault = true,
            EntityPropertyConfig = new EntityPropertyConfig
            {
                Generate = true,
                Modifier = SourceGenAccessModifier.Public
            },
            AccessorConfigs = new List<AccessorConfig>()
        };
    }

    #endregion
}
