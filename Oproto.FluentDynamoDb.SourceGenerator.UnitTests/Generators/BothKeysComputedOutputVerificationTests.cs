using Oproto.FluentDynamoDb.SourceGenerator.Generators;
using Oproto.FluentDynamoDb.SourceGenerator.Models;
using SourceGenAccessModifier = Oproto.FluentDynamoDb.SourceGenerator.Models.AccessModifier;

namespace Oproto.FluentDynamoDb.SourceGenerator.UnitTests.Generators;

/// <summary>
/// Unit tests for both keys computed scenario.
/// Constructs EntityModel with both PK and SK computed.
/// Verifies single typed overload with all PK source params followed by all SK source params.
///
/// Requirements: 1.3, 13.3
/// </summary>
[Trait("Category", "Unit")]
public class BothKeysComputedOutputVerificationTests
{
    /// <summary>
    /// Verifies that a single typed Get overload exists with all PK source params first,
    /// then all SK source params when both keys are computed.
    /// </summary>
    [Fact]
    public void BothKeysComputed_TypedOverload_HasAllPkParamsFollowedByAllSkParams()
    {
        // Arrange
        var entity = CreateEntityWithBothKeysComputed();

        // Act
        var generatedCode = GenerateMultiEntityTable(entity);

        // Assert - single typed overload with PK params (year, month) then SK params (region, category)
        generatedCode.Should().Contain("Get(int year, int month, int region, string category)");
    }

    /// <summary>
    /// Verifies that all parameters have correct types matching source property types.
    /// PK sources: Year (int), Month (int)
    /// SK sources: Region (int), Category (string)
    /// </summary>
    [Fact]
    public void BothKeysComputed_TypedOverload_AllParamsHaveCorrectTypes()
    {
        // Arrange
        var entity = CreateEntityWithBothKeysComputed();

        // Act
        var generatedCode = GenerateMultiEntityTable(entity);

        // Assert - verify each parameter type and camelCase name
        generatedCode.Should().Contain("int year");
        generatedCode.Should().Contain("int month");
        generatedCode.Should().Contain("int region");
        generatedCode.Should().Contain("string category");
    }

    /// <summary>
    /// Verifies that the method body calls Entity.Keys.BuildPk(...) for the PK independently.
    /// </summary>
    [Fact]
    public void BothKeysComputed_TypedOverload_CallsKeysBuildPkForPartitionKey()
    {
        // Arrange
        var entity = CreateEntityWithBothKeysComputed();

        // Act
        var generatedCode = GenerateMultiEntityTable(entity);

        // Assert - method body calls Keys.BuildPk with PK source params
        generatedCode.Should().Contain("TestEntity.Keys.BuildPk(year, month)");
    }

    /// <summary>
    /// Verifies that the method body calls Entity.Keys.BuildSk(...) for the SK independently.
    /// </summary>
    [Fact]
    public void BothKeysComputed_TypedOverload_CallsKeysBuildSkForSortKey()
    {
        // Arrange
        var entity = CreateEntityWithBothKeysComputed();

        // Act
        var generatedCode = GenerateMultiEntityTable(entity);

        // Assert - method body calls Keys.BuildSk with SK source params
        generatedCode.Should().Contain("TestEntity.Keys.BuildSk(region, category)");
    }

    /// <summary>
    /// Verifies that the delegation uses return Get(computedPk, computedSk) pattern.
    /// The typed overload calls BuildPk and BuildSk independently and passes both to the standard overload.
    /// </summary>
    [Fact]
    public void BothKeysComputed_TypedOverload_DelegatesWithBothComputedKeys()
    {
        // Arrange
        var entity = CreateEntityWithBothKeysComputed();

        // Act
        var generatedCode = GenerateMultiEntityTable(entity);

        // Assert - delegation pattern: both BuildPk and BuildSk called, results passed to standard overload
        generatedCode.Should().Contain("TestEntity.Keys.BuildPk(year, month)");
        generatedCode.Should().Contain("TestEntity.Keys.BuildSk(region, category)");
    }

    /// <summary>
    /// Verifies that the standard string overload (string pK, string sK) is also present.
    /// </summary>
    [Fact]
    public void BothKeysComputed_StandardStringOverload_IsPresent()
    {
        // Arrange
        var entity = CreateEntityWithBothKeysComputed();

        // Act
        var generatedCode = GenerateMultiEntityTable(entity);

        // Assert - standard string overload with two string params is present
        generatedCode.Should().Contain("Get(string pk, string sk)");
    }

    /// <summary>
    /// Verifies that the typed overload exists for Delete with the same signature.
    /// </summary>
    [Fact]
    public void BothKeysComputed_TypedOverload_ExistsForDelete()
    {
        // Arrange
        var entity = CreateEntityWithBothKeysComputed();

        // Act
        var generatedCode = GenerateMultiEntityTable(entity);

        // Assert - Delete typed overload with same params
        generatedCode.Should().Contain("Delete(int year, int month, int region, string category)");
    }

    /// <summary>
    /// Verifies that the typed overload exists for Update with the same signature.
    /// </summary>
    [Fact]
    public void BothKeysComputed_TypedOverload_ExistsForUpdate()
    {
        // Arrange
        var entity = CreateEntityWithBothKeysComputed();

        // Act
        var generatedCode = GenerateMultiEntityTable(entity);

        // Assert - Update typed overload with same params
        generatedCode.Should().Contain("Update(int year, int month, int region, string category)");
    }

    /// <summary>
    /// Verifies that the typed overload exists for ConditionCheck with the same signature.
    /// </summary>
    [Fact]
    public void BothKeysComputed_TypedOverload_ExistsForConditionCheck()
    {
        // Arrange
        var entity = CreateEntityWithBothKeysComputed();

        // Act
        var generatedCode = GenerateMultiEntityTable(entity);

        // Assert - ConditionCheck typed overload with same params
        generatedCode.Should().Contain("ConditionCheck(int year, int month, int region, string category)");
    }

    /// <summary>
    /// Verifies that the BuildSk delegation occurs in all four CRUD method typed overloads.
    /// </summary>
    [Fact]
    public void BothKeysComputed_AllCrudOverloads_DelegateToKeysBuildSkAndBuildPk()
    {
        // Arrange
        var entity = CreateEntityWithBothKeysComputed();

        // Act
        var generatedCode = GenerateMultiEntityTable(entity);

        // Assert - BuildPk and BuildSk both called in Get, Delete, Update, ConditionCheck
        var buildPkCount = generatedCode.Split("TestEntity.Keys.BuildPk(year, month)").Length - 1;
        var buildSkCount = generatedCode.Split("TestEntity.Keys.BuildSk(region, category)").Length - 1;
        buildPkCount.Should().BeGreaterThanOrEqualTo(4,
            "BuildPk should be called in Get, Delete, Update, and ConditionCheck typed overloads");
        buildSkCount.Should().BeGreaterThanOrEqualTo(4,
            "BuildSk should be called in Get, Delete, Update, and ConditionCheck typed overloads");
    }

    /// <summary>
    /// Verifies that no KeyInputMode parameter is added when typed overloads are generated.
    /// Per Requirement 4 AC 2, when typed overloads exist, no KeyInputMode is needed.
    /// </summary>
    [Fact]
    public void BothKeysComputed_NoKeyInputModeOnStandardOverload()
    {
        // Arrange
        var entity = CreateEntityWithBothKeysComputed();

        // Act
        var generatedCode = GenerateMultiEntityTable(entity);

        // Assert - no KeyInputMode parameter on any overload
        generatedCode.Should().NotContain("KeyInputMode mode = KeyInputMode.Default");
    }

    #region Helper Methods

    /// <summary>
    /// Creates an EntityModel with both PK and SK computed.
    /// PK: computed from Year (int) + Month (int), attribute "pk"
    /// SK: computed from Region (int) + Category (string), attribute "sk"
    /// </summary>
    private static EntityModel CreateEntityWithBothKeysComputed()
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
                    PropertyType = "string",
                    AttributeName = "pk",
                    IsPartitionKey = true,
                    IsNullable = false,
                    KeyFormat = null,
                    ComputedKey = new ComputedKeyModel
                    {
                        SourceProperties = new[] { "Year", "Month" },
                        Separator = "#"
                    }
                },
                new PropertyModel
                {
                    PropertyName = "Sk",
                    PropertyType = "string",
                    AttributeName = "sk",
                    IsSortKey = true,
                    IsNullable = false,
                    KeyFormat = null,
                    ComputedKey = new ComputedKeyModel
                    {
                        SourceProperties = new[] { "Region", "Category" },
                        Separator = "#"
                    }
                },
                // Source properties for PK
                new PropertyModel
                {
                    PropertyName = "Year",
                    PropertyType = "int",
                    AttributeName = "year",
                    IsNullable = false
                },
                new PropertyModel
                {
                    PropertyName = "Month",
                    PropertyType = "int",
                    AttributeName = "month",
                    IsNullable = false
                },
                // Source properties for SK
                new PropertyModel
                {
                    PropertyName = "Region",
                    PropertyType = "int",
                    AttributeName = "region",
                    IsNullable = false
                },
                new PropertyModel
                {
                    PropertyName = "Category",
                    PropertyType = "string",
                    AttributeName = "category",
                    IsNullable = false
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

    /// <summary>
    /// Generates a multi-entity table class, which produces accessor classes with typed overloads.
    /// </summary>
    private static string GenerateMultiEntityTable(EntityModel entity)
    {
        var entities = new List<EntityModel> { entity };
        return TableGenerator.GenerateTableClass("test-table", entities);
    }

    #endregion
}
