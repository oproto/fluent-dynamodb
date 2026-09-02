using Oproto.FluentDynamoDb.SourceGenerator.Generators;
using Oproto.FluentDynamoDb.SourceGenerator.Models;
using SourceGenAccessModifier = Oproto.FluentDynamoDb.SourceGenerator.Models.AccessModifier;

namespace Oproto.FluentDynamoDb.SourceGenerator.UnitTests.Generators;

/// <summary>
/// Unit tests for computed SK only (simple PK) scenario.
/// Constructs EntityModel with simple string PK + computed SK (≥2 sources).
/// Verifies typed overload has PK string + SK source property params.
///
/// Requirements: 1.2, 13.2
/// </summary>
[Trait("Category", "Unit")]
public class ComputedSkOnlyOutputVerificationTests
{
    /// <summary>
    /// Verifies that when an entity has a simple string PK and a computed SK with 2+ source properties,
    /// the generated typed overload has the PK as a string parameter (pK) followed by SK source property params.
    /// </summary>
    [Fact]
    public void ComputedSkOnly_TypedOverload_HasPkStringFollowedBySkSourceParams()
    {
        // Arrange
        var entity = CreateEntityWithSimplePkAndComputedSk();

        // Act
        var generatedCode = GenerateMultiEntityTable(entity);

        // Assert - typed overload exists with string pK as first param + SK source params
        generatedCode.Should().Contain("Get(string pK, int region, string category)");
    }

    /// <summary>
    /// Verifies that the SK source property parameters have correct types and camelCase names.
    /// Source properties: Region (int), Category (string) → region, category
    /// </summary>
    [Fact]
    public void ComputedSkOnly_TypedOverload_SkSourcePropertiesHaveCorrectTypesAndCamelCaseNames()
    {
        // Arrange
        var entity = CreateEntityWithSimplePkAndComputedSk();

        // Act
        var generatedCode = GenerateMultiEntityTable(entity);

        // Assert - parameters have correct camelCase names and types
        // The typed overload for Get should have: string pK, int region, string category
        generatedCode.Should().Contain("int region");
        generatedCode.Should().Contain("string category");
    }

    /// <summary>
    /// Verifies that the typed overload delegates to Entity.Keys.Sk(...) for the computed sort key.
    /// </summary>
    [Fact]
    public void ComputedSkOnly_TypedOverload_DelegatesToKeysSkMethod()
    {
        // Arrange
        var entity = CreateEntityWithSimplePkAndComputedSk();

        // Act
        var generatedCode = GenerateMultiEntityTable(entity);

        // Assert - delegates to Keys.Sk (via unified Pk/Sk API)
        generatedCode.Should().Contain("TestEntity.Keys.Sk(region, category)");
    }

    /// <summary>
    /// Verifies that the standard string overload (string pk, string sk) is still present.
    /// When both keys are string type and don't need SetKey approach, parameter names are
    /// the camelCase of attribute names ("pk", "sk").
    /// </summary>
    [Fact]
    public void ComputedSkOnly_StandardStringOverload_IsStillPresent()
    {
        // Arrange
        var entity = CreateEntityWithSimplePkAndComputedSk();

        // Act
        var generatedCode = GenerateMultiEntityTable(entity);

        // Assert - standard overload with (string pk, string sk) exists in accessor class
        // Uses camelCase of attribute names when keys are both strings
        generatedCode.Should().Contain("Get(string pk, string sk)");
    }

    /// <summary>
    /// Verifies that the typed overload exists for Delete with the same signature as Get.
    /// </summary>
    [Fact]
    public void ComputedSkOnly_TypedOverload_ExistsForDelete()
    {
        // Arrange
        var entity = CreateEntityWithSimplePkAndComputedSk();

        // Act
        var generatedCode = GenerateMultiEntityTable(entity);

        // Assert - Delete typed overload with same params
        generatedCode.Should().Contain("Delete(string pK, int region, string category)");
    }

    /// <summary>
    /// Verifies that the typed overload exists for Update with the same signature as Get.
    /// </summary>
    [Fact]
    public void ComputedSkOnly_TypedOverload_ExistsForUpdate()
    {
        // Arrange
        var entity = CreateEntityWithSimplePkAndComputedSk();

        // Act
        var generatedCode = GenerateMultiEntityTable(entity);

        // Assert - Update typed overload with same params
        generatedCode.Should().Contain("Update(string pK, int region, string category)");
    }

    /// <summary>
    /// Verifies that the typed overload exists for ConditionCheck with the same signature.
    /// </summary>
    [Fact]
    public void ComputedSkOnly_TypedOverload_ExistsForConditionCheck()
    {
        // Arrange
        var entity = CreateEntityWithSimplePkAndComputedSk();

        // Act
        var generatedCode = GenerateMultiEntityTable(entity);

        // Assert - ConditionCheck typed overload with same params
        generatedCode.Should().Contain("ConditionCheck(string pK, int region, string category)");
    }

    /// <summary>
    /// Verifies that the typed overloads delegate to Keys.Sk for all CRUD methods.
    /// </summary>
    [Fact]
    public void ComputedSkOnly_AllCrudOverloads_DelegateToKeysSk()
    {
        // Arrange
        var entity = CreateEntityWithSimplePkAndComputedSk();

        // Act
        var generatedCode = GenerateMultiEntityTable(entity);

        // Assert - count occurrences of Keys.Sk delegation (should appear for Get, Delete, Update, ConditionCheck)
        var skCount = generatedCode.Split("TestEntity.Keys.Sk(region, category)").Length - 1;
        skCount.Should().BeGreaterThanOrEqualTo(4,
            "Sk should be called in Get, Delete, Update, and ConditionCheck typed overloads");
    }

    #region Helper Methods

    /// <summary>
    /// Creates an EntityModel with a simple string PK (no computed) and a computed SK with 2 source properties.
    /// PK: string, attribute "pk"
    /// SK: string (computed from Region + Category), attribute "sk"
    /// Source properties: Region (int), Category (string)
    /// </summary>
    private static EntityModel CreateEntityWithSimplePkAndComputedSk()
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
                    KeyFormat = null // No prefix - simple PK
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
                // Source properties for the computed SK
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
    /// The multi-entity path is what generates typed parameter convenience overloads.
    /// </summary>
    private static string GenerateMultiEntityTable(EntityModel entity)
    {
        var entities = new List<EntityModel> { entity };
        return TableGenerator.GenerateTableClass("test-table", entities);
    }

    #endregion
}
