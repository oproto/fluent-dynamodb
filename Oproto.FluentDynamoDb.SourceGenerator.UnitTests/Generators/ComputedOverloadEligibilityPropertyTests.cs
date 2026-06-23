using FsCheck;
using FsCheck.Xunit;
using Oproto.FluentDynamoDb.SourceGenerator.Generators;
using Oproto.FluentDynamoDb.SourceGenerator.Models;
using SourceGenAccessModifier = Oproto.FluentDynamoDb.SourceGenerator.Models.AccessModifier;

namespace Oproto.FluentDynamoDb.SourceGenerator.UnitTests.Generators;

/// <summary>
/// Property-based tests for computed key accessor overload eligibility.
/// These tests verify that the eligibility logic correctly determines when entities
/// qualify (or don't qualify) for typed parameter convenience overloads.
///
/// **Feature: computed-key-accessor-overloads**
/// </summary>
[Trait("Category", "PropertyTest")]
public class ComputedOverloadEligibilityPropertyTests
{
    /// <summary>
    /// Property 3: No overload for non-computed entities
    ///
    /// For any EntityModel where neither the partition key nor the sort key has
    /// IsComputed == true with ComputedKey.SourceProperties.Length >= 2,
    /// QualifiesForTypedOverload SHALL return false.
    ///
    /// **Validates: Requirements 1.5**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property NonComputedEntities_DoNotQualifyForTypedOverloads()
    {
        // Generate entities that do NOT have computed keys with >= 2 source properties
        var entityGen = CreateNonComputedEntityGenerator();

        return Prop.ForAll(
            entityGen.ToArbitrary(),
            entity =>
            {
                var qualifies = ComputedOverloadEligibility.QualifiesForTypedOverload(entity);

                return (!qualifies)
                    .Label($"Entity '{entity.ClassName}' should NOT qualify for typed overload. " +
                           $"PK: type={entity.PartitionKeyProperty?.PropertyType}, " +
                           $"isComputed={entity.PartitionKeyProperty?.IsComputed}, " +
                           $"sourceProps={entity.PartitionKeyProperty?.ComputedKey?.SourceProperties.Length ?? 0}. " +
                           $"SK: type={entity.SortKeyProperty?.PropertyType}, " +
                           $"isComputed={entity.SortKeyProperty?.IsComputed}, " +
                           $"sourceProps={entity.SortKeyProperty?.ComputedKey?.SourceProperties.Length ?? 0}.");
            });
    }

    /// <summary>
    /// Property 3 (extended): Non-computed entities do not get typed overloads in generated code.
    ///
    /// For any EntityModel where neither key is computed with >= 2 source properties,
    /// the generated code SHALL NOT contain any typed parameter convenience overloads
    /// beyond the standard (string) or (string, string) overloads.
    ///
    /// **Validates: Requirements 1.5**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property NonComputedEntities_GeneratedCode_DoesNotContainTypedOverloads()
    {
        // Generate entities with string keys (so they can generate code) that are NOT computed with >= 2 sources
        var entityGen = CreateNonComputedStringKeyEntityGenerator();

        return Prop.ForAll(
            entityGen.ToArbitrary(),
            entity =>
            {
                var generatedCode = TableGenerator.GenerateTableClass(entity);

                // Count the number of Get method declarations
                // Standard overloads use (string pK) or (string pK, string sK)
                // Typed overloads would have different parameter types (int, DateTime, Guid, etc.)
                // or more parameters than the standard overload
                var hasSk = entity.SortKeyProperty != null;

                // The entity should NOT qualify for typed overloads
                var qualifies = ComputedOverloadEligibility.QualifiesForTypedOverload(entity);

                // If the standard get method exists, no additional typed overload should exist
                // Typed overloads would contain calls to Keys.BuildPk or Keys.BuildSk
                var hasBuildPkCall = generatedCode.Contains("Keys.BuildPk(");
                var hasBuildSkCall = generatedCode.Contains("Keys.BuildSk(");

                return (!qualifies && !hasBuildPkCall && !hasBuildSkCall)
                    .Label($"Entity '{entity.ClassName}' (hasSK={hasSk}) should not have typed overloads. " +
                           $"qualifies={qualifies}, hasBuildPk={hasBuildPkCall}, hasBuildSk={hasBuildSkCall}.");
            });
    }

    #region Generators

    /// <summary>
    /// Creates a generator for EntityModels where NO key is computed with >= 2 source properties.
    /// Covers several scenarios:
    /// - No computed keys at all (simple string keys)
    /// - Computed key with only 1 source property (doesn't qualify)
    /// - Non-string keys without computed (int, Guid, enum)
    /// - Mixed: one key is computed with 1 source, other is not computed
    /// </summary>
    private static Gen<EntityModel> CreateNonComputedEntityGenerator()
    {
        var classNameGen = Gen.Elements("SimpleEntity", "OrderEntity", "UserEntity", "ProductEntity", "AccountEntity");
        var tableNameGen = Gen.Elements("simple-table", "orders-table", "users-table", "products-table");
        var keyTypeGen = Gen.Elements("string", "int", "Guid", "long", "DateTime");
        var hasSortKeyGen = Gen.Elements(true, false);
        var hasPrefixGen = Gen.Elements(true, false);

        // Scenario selection: determines what kind of non-qualifying entity to generate
        var scenarioGen = Gen.Choose(0, 4);

        return from className in classNameGen
               from tableName in tableNameGen
               from pkType in keyTypeGen
               from skType in keyTypeGen
               from hasSk in hasSortKeyGen
               from hasPrefix in hasPrefixGen
               from scenario in scenarioGen
               select BuildNonComputedEntity(className, tableName, pkType, skType, hasSk, hasPrefix, scenario);
    }

    /// <summary>
    /// Creates a generator for EntityModels with string keys that are NOT computed with >= 2 source properties.
    /// These are suitable for code generation testing (TableGenerator requires well-formed entities).
    /// </summary>
    private static Gen<EntityModel> CreateNonComputedStringKeyEntityGenerator()
    {
        var classNameGen = Gen.Elements("OrderEntity", "UserEntity", "ProductEntity", "EventEntity");
        var tableNameGen = Gen.Elements("orders-table", "users-table", "products-table", "events-table");
        var hasSortKeyGen = Gen.Elements(true, false);
        var hasPrefixGen = Gen.Elements(true, false);
        var hasComputedSingleSourceGen = Gen.Elements(true, false);

        return from className in classNameGen
               from tableName in tableNameGen
               from hasSk in hasSortKeyGen
               from hasPkPrefix in hasPrefixGen
               from hasSkPrefix in hasPrefixGen
               from hasSingleSourceComputed in hasComputedSingleSourceGen
               select BuildNonComputedStringKeyEntity(className, tableName, hasSk, hasPkPrefix, hasSkPrefix, hasSingleSourceComputed);
    }

    #endregion

    #region Entity Builders

    private static EntityModel BuildNonComputedEntity(
        string className,
        string tableName,
        string pkType,
        string skType,
        bool hasSortKey,
        bool hasPrefix,
        int scenario)
    {
        var properties = new List<PropertyModel>();

        // Build PK property based on scenario
        var pkProperty = new PropertyModel
        {
            PropertyName = "Pk",
            PropertyType = pkType,
            AttributeName = "pk",
            IsPartitionKey = true,
            KeyFormat = hasPrefix ? new KeyFormatModel { Prefix = "PREFIX" } : null
        };

        switch (scenario)
        {
            case 0:
                // No computed key at all
                break;
            case 1:
                // Computed key with exactly 1 source property (doesn't qualify for typed overload)
                pkProperty.ComputedKey = new ComputedKeyModel
                {
                    SourceProperties = new[] { "Field1" },
                    Separator = "#"
                };
                break;
            case 2:
                // Computed key with 0 source properties (edge case - shouldn't qualify)
                pkProperty.ComputedKey = new ComputedKeyModel
                {
                    SourceProperties = Array.Empty<string>(),
                    Separator = "#"
                };
                break;
            default:
                // No computed key (most common case)
                break;
        }

        properties.Add(pkProperty);

        // Optionally add sort key
        if (hasSortKey)
        {
            var skProperty = new PropertyModel
            {
                PropertyName = "Sk",
                PropertyType = skType,
                AttributeName = "sk",
                IsSortKey = true,
                KeyFormat = hasPrefix ? new KeyFormatModel { Prefix = "SK_PREFIX" } : null
            };

            // For scenario 3, put single-source computed on SK
            if (scenario == 3)
            {
                skProperty.ComputedKey = new ComputedKeyModel
                {
                    SourceProperties = new[] { "OnlyField" },
                    Separator = "#"
                };
            }

            properties.Add(skProperty);
        }

        // Add a non-key property
        properties.Add(new PropertyModel
        {
            PropertyName = "Name",
            PropertyType = "string",
            AttributeName = "name",
            IsNullable = false
        });

        return new EntityModel
        {
            ClassName = className,
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

    private static EntityModel BuildNonComputedStringKeyEntity(
        string className,
        string tableName,
        bool hasSortKey,
        bool hasPkPrefix,
        bool hasSkPrefix,
        bool hasSingleSourceComputed)
    {
        var properties = new List<PropertyModel>();

        var pkProperty = new PropertyModel
        {
            PropertyName = "Pk",
            PropertyType = "string",
            AttributeName = "pk",
            IsPartitionKey = true,
            KeyFormat = hasPkPrefix ? new KeyFormatModel { Prefix = "PK_PREFIX" } : null
        };

        // Optionally add a single-source computed key (still doesn't qualify for typed overload)
        if (hasSingleSourceComputed)
        {
            pkProperty.ComputedKey = new ComputedKeyModel
            {
                SourceProperties = new[] { "SingleField" },
                Separator = "#"
            };
            // Add the source property so the entity model is consistent
            properties.Add(new PropertyModel
            {
                PropertyName = "SingleField",
                PropertyType = "string",
                AttributeName = "singleField",
                IsNullable = false
            });
        }

        properties.Add(pkProperty);

        if (hasSortKey)
        {
            properties.Add(new PropertyModel
            {
                PropertyName = "Sk",
                PropertyType = "string",
                AttributeName = "sk",
                IsSortKey = true,
                KeyFormat = hasSkPrefix ? new KeyFormatModel { Prefix = "SK_PREFIX" } : null
            });
        }

        // Add a non-key property
        properties.Add(new PropertyModel
        {
            PropertyName = "Data",
            PropertyType = "string",
            AttributeName = "data",
            IsNullable = true
        });

        return new EntityModel
        {
            ClassName = className,
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
