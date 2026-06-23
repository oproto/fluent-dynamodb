using Oproto.FluentDynamoDb.SourceGenerator.Generators;
using Oproto.FluentDynamoDb.SourceGenerator.Models;
using SourceGenAccessModifier = Oproto.FluentDynamoDb.SourceGenerator.Models.AccessModifier;

namespace Oproto.FluentDynamoDb.SourceGenerator.UnitTests.Generators;

/// <summary>
/// Unit tests for prefix + computed key scenario.
/// Verifies that an entity with a computed PK (≥2 sources) AND a configured prefix
/// generates the typed overload correctly, delegates to Keys.Build{PropertyName}(...),
/// and does NOT add KeyInputMode to the standard overload.
///
/// Requirements: 9.1, 13.4
/// </summary>
[Trait("Category", "Unit")]
public class PrefixComputedKeyScenarioTests
{
    /// <summary>
    /// Entity with computed PK (2 source properties: int TenantId, string UserId)
    /// AND a prefix "ORDER" on the PK. Verify the typed overload is generated correctly.
    /// </summary>
    [Fact]
    public void ComputedPkWithPrefix_GeneratesTypedOverload()
    {
        // Arrange
        var entity = BuildComputedPkWithPrefixEntity(
            "Order", "orders", "ORDER", new[] { ("TenantId", "int"), ("UserId", "string") });

        // Act
        var generatedCode = TableGenerator.GenerateTableClass(entity.TableName, new List<EntityModel> { entity });

        // Assert — typed overload should exist with correct parameter types
        generatedCode.Should().Contain("int tenantId, string userId",
            "typed overload should accept source property parameters with correct types");
    }

    /// <summary>
    /// Entity with computed PK + prefix "ORDER". Verify the typed overload body
    /// calls Entity.Keys.BuildPk(...) which incorporates the prefix.
    /// </summary>
    [Fact]
    public void ComputedPkWithPrefix_TypedOverloadDelegatesToKeysBuildPk()
    {
        // Arrange
        var entity = BuildComputedPkWithPrefixEntity(
            "Order", "orders", "ORDER", new[] { ("TenantId", "int"), ("UserId", "string") });

        // Act
        var generatedCode = TableGenerator.GenerateTableClass(entity.TableName, new List<EntityModel> { entity });

        // Assert — typed overload calls Keys.BuildPk(tenantId, userId)
        generatedCode.Should().Contain("Order.Keys.BuildPk(tenantId, userId)",
            "typed overload should delegate to Entity.Keys.BuildPk(...) which incorporates the prefix");
    }

    /// <summary>
    /// Entity with computed PK + prefix. Verify no KeyInputMode parameter appears
    /// on the standard overload (since typed overload exists and disambiguates).
    /// </summary>
    [Fact]
    public void ComputedPkWithPrefix_NoKeyInputModeOnStandardOverload()
    {
        // Arrange
        var entity = BuildComputedPkWithPrefixEntity(
            "Order", "orders", "ORDER", new[] { ("TenantId", "int"), ("UserId", "string") });

        // Act
        var generatedCode = TableGenerator.GenerateTableClass(entity.TableName, new List<EntityModel> { entity });

        // Assert — standard overload should NOT have KeyInputMode parameter
        generatedCode.Should().NotContain("KeyInputMode mode = KeyInputMode.Default",
            "when typed overload exists, standard overload should not have KeyInputMode parameter");
    }

    /// <summary>
    /// Entity with computed PK + prefix. Verify the standard string overload remains intact.
    /// </summary>
    [Fact]
    public void ComputedPkWithPrefix_StandardStringOverloadRemainsIntact()
    {
        // Arrange
        var entity = BuildComputedPkWithPrefixEntity(
            "Order", "orders", "ORDER", new[] { ("TenantId", "int"), ("UserId", "string") });

        // Act
        var generatedCode = TableGenerator.GenerateTableClass(entity.TableName, new List<EntityModel> { entity });

        // Assert — standard overload with (string pk) should still exist
        generatedCode.Should().Contain("Get(string pk)",
            "standard string overload should remain intact without KeyInputMode");
    }

    /// <summary>
    /// Entity with computed SK + prefix "META" on SK (PK is simple string).
    /// Verify the typed overload delegates to Keys.BuildSk(...).
    /// </summary>
    [Fact]
    public void ComputedSkWithPrefix_TypedOverloadDelegatesToKeysBuildSk()
    {
        // Arrange
        var entity = BuildComputedSkWithPrefixEntity(
            "Event", "events", "META", new[] { ("Year", "int"), ("Month", "int") });

        // Act
        var generatedCode = TableGenerator.GenerateTableClass(entity.TableName, new List<EntityModel> { entity });

        // Assert — typed overload calls Keys.BuildSk(year, month)
        generatedCode.Should().Contain("Event.Keys.BuildSk(year, month)",
            "typed overload should delegate to Entity.Keys.BuildSk(...) which incorporates the prefix");
    }

    /// <summary>
    /// Entity with computed PK (3 sources) + prefix "CUSTOMER". Verify all CRUD methods
    /// have typed overloads that delegate to Keys.BuildPk.
    /// </summary>
    [Fact]
    public void ComputedPkWithPrefix_AllCrudMethodsDelegateToBuildPk()
    {
        // Arrange
        var entity = BuildComputedPkWithPrefixEntity(
            "Customer", "customers", "CUSTOMER",
            new[] { ("Region", "int"), ("Division", "string"), ("AccountId", "long") });

        // Act
        var generatedCode = TableGenerator.GenerateTableClass(entity.TableName, new List<EntityModel> { entity });

        // Assert — all CRUD methods should contain delegation to Keys.BuildPk
        var expectedBuildCall = "Customer.Keys.BuildPk(region, division, accountId)";
        generatedCode.Should().Contain(expectedBuildCall,
            "all CRUD typed overloads should delegate to Keys.BuildPk(...)");

        // Verify each CRUD method has the delegation (Get, Delete, Update, ConditionCheck)
        generatedCode.Should().Contain("return Get(computedPk)",
            "Get typed overload should delegate to standard overload");
        generatedCode.Should().Contain("return Delete(computedPk)",
            "Delete typed overload should delegate to standard overload");
        generatedCode.Should().Contain("return Update(computedPk)",
            "Update typed overload should delegate to standard overload");
        generatedCode.Should().Contain("return ConditionCheck(computedPk)",
            "ConditionCheck typed overload should delegate to standard overload");
    }

    /// <summary>
    /// Entity with both computed PK (prefix "ORDER") and computed SK (prefix "LINE").
    /// Verify both Keys.BuildPk and Keys.BuildSk are called.
    /// </summary>
    [Fact]
    public void BothKeysComputedWithPrefixes_DelegatesToBothBuildMethods()
    {
        // Arrange
        var entity = BuildBothKeysComputedWithPrefixesEntity(
            "OrderLine", "order-lines",
            pkPrefix: "ORDER", pkSources: new[] { ("TenantId", "int"), ("CustomerId", "string") },
            skPrefix: "LINE", skSources: new[] { ("Year", "int"), ("Sequence", "long") });

        // Act
        var generatedCode = TableGenerator.GenerateTableClass(entity.TableName, new List<EntityModel> { entity });

        // Assert — both Build calls should be present
        generatedCode.Should().Contain("OrderLine.Keys.BuildPk(tenantId, customerId)",
            "should delegate PK construction to Keys.BuildPk(...)");
        generatedCode.Should().Contain("OrderLine.Keys.BuildSk(year, sequence)",
            "should delegate SK construction to Keys.BuildSk(...)");
    }

    /// <summary>
    /// Verifies that an entity qualifying for typed overloads (prefix + computed)
    /// correctly reports eligibility via ComputedOverloadEligibility.
    /// </summary>
    [Fact]
    public void ComputedPkWithPrefix_QualifiesForTypedOverload()
    {
        // Arrange
        var entity = BuildComputedPkWithPrefixEntity(
            "Order", "orders", "ORDER", new[] { ("TenantId", "int"), ("UserId", "string") });

        // Act & Assert
        ComputedOverloadEligibility.QualifiesForTypedOverload(entity).Should().BeTrue(
            "entity with computed PK (≥2 sources) should qualify for typed overload");
        ComputedOverloadEligibility.WouldBeAmbiguous(entity).Should().BeFalse(
            "entity with non-string source properties should not be ambiguous");
        ComputedOverloadEligibility.QualifiesForKeyInputMode(entity).Should().BeFalse(
            "entity with typed overload should NOT qualify for KeyInputMode");
    }

    #region Entity Builders

    private static EntityModel BuildComputedPkWithPrefixEntity(
        string entityName, string tableName, string pkPrefix,
        (string Name, string Type)[] sourceProperties)
    {
        var properties = new List<PropertyModel>();
        var pkSourceProps = new List<string>();

        // Source properties for the computed PK
        foreach (var (name, type) in sourceProperties)
        {
            properties.Add(new PropertyModel
            {
                PropertyName = name,
                PropertyType = type,
                AttributeName = name.ToLowerInvariant()
            });
            pkSourceProps.Add(name);
        }

        // Computed PK with prefix
        properties.Add(new PropertyModel
        {
            PropertyName = "Pk",
            PropertyType = "string",
            AttributeName = "pk",
            IsPartitionKey = true,
            KeyFormat = new KeyFormatModel { Prefix = pkPrefix },
            ComputedKey = new ComputedKeyModel
            {
                SourceProperties = pkSourceProps.ToArray(),
                Separator = "#"
            }
        });

        return CreateEntity(entityName, tableName, properties.ToArray());
    }

    private static EntityModel BuildComputedSkWithPrefixEntity(
        string entityName, string tableName, string skPrefix,
        (string Name, string Type)[] sourceProperties)
    {
        var properties = new List<PropertyModel>();
        var skSourceProps = new List<string>();

        // Simple string PK (no prefix, no computed)
        properties.Add(new PropertyModel
        {
            PropertyName = "Pk",
            PropertyType = "string",
            AttributeName = "pk",
            IsPartitionKey = true
        });

        // Source properties for the computed SK
        foreach (var (name, type) in sourceProperties)
        {
            properties.Add(new PropertyModel
            {
                PropertyName = name,
                PropertyType = type,
                AttributeName = name.ToLowerInvariant()
            });
            skSourceProps.Add(name);
        }

        // Computed SK with prefix
        properties.Add(new PropertyModel
        {
            PropertyName = "Sk",
            PropertyType = "string",
            AttributeName = "sk",
            IsSortKey = true,
            KeyFormat = new KeyFormatModel { Prefix = skPrefix },
            ComputedKey = new ComputedKeyModel
            {
                SourceProperties = skSourceProps.ToArray(),
                Separator = "#"
            }
        });

        return CreateEntity(entityName, tableName, properties.ToArray());
    }

    private static EntityModel BuildBothKeysComputedWithPrefixesEntity(
        string entityName, string tableName,
        string pkPrefix, (string Name, string Type)[] pkSources,
        string skPrefix, (string Name, string Type)[] skSources)
    {
        var properties = new List<PropertyModel>();
        var pkSourceProps = new List<string>();
        var skSourceProps = new List<string>();

        // PK source properties
        foreach (var (name, type) in pkSources)
        {
            properties.Add(new PropertyModel
            {
                PropertyName = name,
                PropertyType = type,
                AttributeName = name.ToLowerInvariant()
            });
            pkSourceProps.Add(name);
        }

        // Computed PK with prefix
        properties.Add(new PropertyModel
        {
            PropertyName = "Pk",
            PropertyType = "string",
            AttributeName = "pk",
            IsPartitionKey = true,
            KeyFormat = new KeyFormatModel { Prefix = pkPrefix },
            ComputedKey = new ComputedKeyModel
            {
                SourceProperties = pkSourceProps.ToArray(),
                Separator = "#"
            }
        });

        // SK source properties
        foreach (var (name, type) in skSources)
        {
            properties.Add(new PropertyModel
            {
                PropertyName = name,
                PropertyType = type,
                AttributeName = name.ToLowerInvariant()
            });
            skSourceProps.Add(name);
        }

        // Computed SK with prefix
        properties.Add(new PropertyModel
        {
            PropertyName = "Sk",
            PropertyType = "string",
            AttributeName = "sk",
            IsSortKey = true,
            KeyFormat = new KeyFormatModel { Prefix = skPrefix },
            ComputedKey = new ComputedKeyModel
            {
                SourceProperties = skSourceProps.ToArray(),
                Separator = "#"
            }
        });

        return CreateEntity(entityName, tableName, properties.ToArray());
    }

    private static EntityModel CreateEntity(string entityName, string tableName, PropertyModel[] properties)
    {
        return new EntityModel
        {
            ClassName = entityName,
            Namespace = "TestNamespace",
            TableName = tableName,
            Properties = properties,
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
