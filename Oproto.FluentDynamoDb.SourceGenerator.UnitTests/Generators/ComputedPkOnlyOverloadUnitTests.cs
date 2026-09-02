using AwesomeAssertions;
using Oproto.FluentDynamoDb.SourceGenerator.Generators;
using Oproto.FluentDynamoDb.SourceGenerator.Models;
using SourceGenAccessModifier = Oproto.FluentDynamoDb.SourceGenerator.Models.AccessModifier;

namespace Oproto.FluentDynamoDb.SourceGenerator.UnitTests.Generators;

/// <summary>
/// Unit tests for computed PK only (no SK) scenario.
/// Verifies generated output contains typed overload with PK source params only.
///
/// **Validates: Requirements 1.7, 13.1**
/// </summary>
[Trait("Category", "Unit")]
public class ComputedPkOnlyOverloadUnitTests
{
    /// <summary>
    /// Verifies that a typed Get overload exists with correct parameter signature
    /// when the entity has a computed PK with 2 source properties and no SK.
    /// </summary>
    [Fact]
    public void ComputedPkNoSk_TwoSourceProps_GeneratesTypedGetOverload()
    {
        // Arrange
        var entity = CreateComputedPkNoSkEntity(
            pkSourceProps: new[] { ("Year", "int"), ("Month", "int") });

        // Act
        var generatedCode = TableGenerator.GenerateTableClass(entity.TableName, new List<EntityModel> { entity });

        // Assert — typed overload with PK source params only
        generatedCode.Should().Contain("Get(int year, int month)");
    }

    /// <summary>
    /// Verifies that a typed Get overload exists with correct parameter signature
    /// when the entity has a computed PK with 3 source properties and no SK.
    /// </summary>
    [Fact]
    public void ComputedPkNoSk_ThreeSourceProps_GeneratesTypedGetOverload()
    {
        // Arrange
        var entity = CreateComputedPkNoSkEntity(
            pkSourceProps: new[] { ("Year", "int"), ("Month", "int"), ("Day", "int") });

        // Act
        var generatedCode = TableGenerator.GenerateTableClass(entity.TableName, new List<EntityModel> { entity });

        // Assert — typed overload with all 3 PK source params
        generatedCode.Should().Contain("Get(int year, int month, int day)");
    }

    /// <summary>
    /// Verifies parameters match PK source property types and camelCase names.
    /// Uses mixed types to verify proper type resolution.
    /// </summary>
    [Fact]
    public void ComputedPkNoSk_MixedTypes_ParameterTypesMatchSourceProperties()
    {
        // Arrange
        var entity = CreateComputedPkNoSkEntity(
            pkSourceProps: new[] { ("TenantId", "int"), ("UserId", "long") });

        // Act
        var generatedCode = TableGenerator.GenerateTableClass(entity.TableName, new List<EntityModel> { entity });

        // Assert — parameters use source property types and camelCase names
        generatedCode.Should().Contain("Get(int tenantId, long userId)");
    }

    /// <summary>
    /// Verifies the typed overload method delegates to Entity.Keys.Pk(...).
    /// </summary>
    [Fact]
    public void ComputedPkNoSk_DelegatesToKeysPk()
    {
        // Arrange
        var entity = CreateComputedPkNoSkEntity(
            pkSourceProps: new[] { ("Year", "int"), ("Month", "int") });

        // Act
        var generatedCode = TableGenerator.GenerateTableClass(entity.TableName, new List<EntityModel> { entity });

        // Assert — method body calls Entity.Keys.Pk(...)
        generatedCode.Should().Contain("TestEntity.Keys.Pk(year, month)");
    }

    /// <summary>
    /// Verifies the standard string overload is also present (backward compatibility).
    /// </summary>
    [Fact]
    public void ComputedPkNoSk_StandardStringOverloadRemainsPresent()
    {
        // Arrange
        var entity = CreateComputedPkNoSkEntity(
            pkSourceProps: new[] { ("Year", "int"), ("Month", "int") });

        // Act
        var generatedCode = TableGenerator.GenerateTableClass(entity.TableName, new List<EntityModel> { entity });

        // Assert — standard string overload still exists (parameter name derived from attribute name "pk")
        generatedCode.Should().Contain("Get(string pk)");
    }

    /// <summary>
    /// Verifies typed overloads are also generated for Delete, Update, and ConditionCheck.
    /// </summary>
    [Fact]
    public void ComputedPkNoSk_TypedOverloadsExistForAllCrudMethods()
    {
        // Arrange
        var entity = CreateComputedPkNoSkEntity(
            pkSourceProps: new[] { ("Year", "int"), ("Month", "int") });

        // Act
        var generatedCode = TableGenerator.GenerateTableClass(entity.TableName, new List<EntityModel> { entity });

        // Assert — all CRUD methods have typed overloads
        generatedCode.Should().Contain("Get(int year, int month)");
        generatedCode.Should().Contain("Delete(int year, int month)");
        generatedCode.Should().Contain("Update(int year, int month)");
        generatedCode.Should().Contain("ConditionCheck(int year, int month)");
    }

    /// <summary>
    /// Verifies that no KeyInputMode parameter is added to the standard overload
    /// when typed overloads are generated (per Requirement 4 AC 2).
    /// </summary>
    [Fact]
    public void ComputedPkNoSk_NoKeyInputModeOnStandardOverload()
    {
        // Arrange
        var entity = CreateComputedPkNoSkEntity(
            pkSourceProps: new[] { ("Year", "int"), ("Month", "int") });

        // Act
        var generatedCode = TableGenerator.GenerateTableClass(entity.TableName, new List<EntityModel> { entity });

        // Assert — no KeyInputMode parameter on standard string overload
        generatedCode.Should().NotContain("KeyInputMode mode = KeyInputMode.Default");
    }

    #region Helper Methods

    private static EntityModel CreateComputedPkNoSkEntity(
        (string Name, string Type)[] pkSourceProps)
    {
        var properties = new List<PropertyModel>();
        var sourcePropertyNames = new List<string>();

        // Add source properties
        foreach (var (name, type) in pkSourceProps)
        {
            properties.Add(new PropertyModel
            {
                PropertyName = name,
                PropertyType = type,
                AttributeName = name.ToLowerInvariant()
            });
            sourcePropertyNames.Add(name);
        }

        // Add computed PK property (no SK)
        properties.Add(new PropertyModel
        {
            PropertyName = "Pk",
            PropertyType = "string",
            AttributeName = "pk",
            IsPartitionKey = true,
            ComputedKey = new ComputedKeyModel
            {
                SourceProperties = sourcePropertyNames.ToArray(),
                Separator = "#"
            }
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
            AccessorConfigs = new List<AccessorConfig>()
        };
    }

    #endregion
}
