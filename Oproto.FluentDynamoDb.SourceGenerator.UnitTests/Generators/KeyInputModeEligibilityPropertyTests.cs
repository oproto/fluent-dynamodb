using FsCheck;
using FsCheck.Xunit;
using Oproto.FluentDynamoDb.SourceGenerator.Generators;
using Oproto.FluentDynamoDb.SourceGenerator.Models;
using SourceGenAccessModifier = Oproto.FluentDynamoDb.SourceGenerator.Models.AccessModifier;

namespace Oproto.FluentDynamoDb.SourceGenerator.UnitTests.Generators;

/// <summary>
/// Property-based tests for KeyInputMode eligibility determination.
///
/// **Feature: computed-key-accessor-overloads, Property 7: KeyInputMode eligibility**
///
/// For any EntityModel, the generated standard accessor methods SHALL include an optional
/// KeyInputMode mode = KeyInputMode.Default parameter if and only if:
/// (a) at least one key is of type string with a non-null/non-empty KeyFormat.Prefix, AND
/// (b) no non-ambiguous typed parameter convenience overload is being generated for that entity.
///
/// **Validates: Requirements 4.1, 4.2, 4.7, 6.1, 6.3, 7.1, 7.3, 10.1, 10.2, 11.6**
/// </summary>
[Trait("Category", "PropertyTest")]
public class KeyInputModeEligibilityPropertyTests
{
    private static readonly string[] NonStringTypes = { "int", "long", "Guid", "DateTime", "decimal" };

    /// <summary>
    /// Positive case: Entity with string PK + prefix, no computed key → QualifiesForKeyInputMode = true
    /// </summary>
    [Property(MaxTest = 100)]
    public Property StringPkWithPrefix_NoComputed_QualifiesForKeyInputMode()
    {
        var entityGen = CreateStringPkWithPrefixNoComputedGenerator();

        return Prop.ForAll(entityGen, entity =>
        {
            var qualifies = ComputedOverloadEligibility.QualifiesForKeyInputMode(entity);

            return qualifies.Label(
                $"Entity '{entity.ClassName}' with string PK + prefix should qualify for KeyInputMode. " +
                $"PK: type={entity.PartitionKeyProperty?.PropertyType}, " +
                $"prefix={entity.PartitionKeyProperty?.KeyFormat?.Prefix}, " +
                $"isComputed={entity.PartitionKeyProperty?.IsComputed}");
        });
    }

    /// <summary>
    /// Negative case: Entity with string PK, NO prefix → QualifiesForKeyInputMode = false
    /// </summary>
    [Property(MaxTest = 100)]
    public Property StringPkWithoutPrefix_DoesNotQualifyForKeyInputMode()
    {
        var entityGen = CreateStringPkWithoutPrefixGenerator();

        return Prop.ForAll(entityGen, entity =>
        {
            var qualifies = ComputedOverloadEligibility.QualifiesForKeyInputMode(entity);

            return (!qualifies).Label(
                $"Entity '{entity.ClassName}' with string PK but no prefix should NOT qualify for KeyInputMode. " +
                $"PK: type={entity.PartitionKeyProperty?.PropertyType}, " +
                $"prefix={entity.PartitionKeyProperty?.KeyFormat?.Prefix}, " +
                $"SK: type={entity.SortKeyProperty?.PropertyType}, " +
                $"skPrefix={entity.SortKeyProperty?.KeyFormat?.Prefix}");
        });
    }

    /// <summary>
    /// Negative case: Entity with non-string PK (int, Guid), with prefix → QualifiesForKeyInputMode = false
    /// (Non-string keys never contribute to KeyInputMode eligibility)
    /// </summary>
    [Property(MaxTest = 100)]
    public Property NonStringPk_NoStringSkWithPrefix_DoesNotQualifyForKeyInputMode()
    {
        var entityGen = CreateNonStringPkNoPrefixedStringSkGenerator();

        return Prop.ForAll(entityGen, entity =>
        {
            var qualifies = ComputedOverloadEligibility.QualifiesForKeyInputMode(entity);

            return (!qualifies).Label(
                $"Entity '{entity.ClassName}' with non-string PK should NOT qualify for KeyInputMode. " +
                $"PK: type={entity.PartitionKeyProperty?.PropertyType}, " +
                $"SK: type={entity.SortKeyProperty?.PropertyType}, " +
                $"skPrefix={entity.SortKeyProperty?.KeyFormat?.Prefix}");
        });
    }

    /// <summary>
    /// Negative case: Entity with computed PK (non-ambiguous typed overload) → QualifiesForKeyInputMode = false
    /// When a typed overload exists for the entity, KeyInputMode should NOT be added.
    /// </summary>
    [Property(MaxTest = 100)]
    public Property ComputedPk_WithNonAmbiguousTypedOverload_DoesNotQualifyForKeyInputMode()
    {
        var entityGen = CreateComputedPkWithTypedOverloadGenerator();

        return Prop.ForAll(entityGen, entity =>
        {
            // Verify the entity actually qualifies for typed overload (precondition)
            var qualifiesForTyped = ComputedOverloadEligibility.QualifiesForTypedOverload(entity);
            var wouldBeAmbiguous = ComputedOverloadEligibility.WouldBeAmbiguous(entity);

            if (!qualifiesForTyped || wouldBeAmbiguous)
                return true.Label("Precondition not met — entity doesn't have non-ambiguous typed overload");

            var qualifiesForKeyInputMode = ComputedOverloadEligibility.QualifiesForKeyInputMode(entity);

            return (!qualifiesForKeyInputMode).Label(
                $"Entity '{entity.ClassName}' with non-ambiguous typed overload should NOT qualify for KeyInputMode. " +
                $"qualifiesForTyped={qualifiesForTyped}, wouldBeAmbiguous={wouldBeAmbiguous}");
        });
    }

    /// <summary>
    /// Positive case: Entity with string SK + prefix (PK has no prefix) → QualifiesForKeyInputMode = true
    /// KeyInputMode eligibility is per-entity; if any string key has a prefix, it qualifies.
    /// </summary>
    [Property(MaxTest = 100)]
    public Property StringSkWithPrefix_PkNoPrefixed_QualifiesForKeyInputMode()
    {
        var entityGen = CreateStringSkWithPrefixPkNoPrefixGenerator();

        return Prop.ForAll(entityGen, entity =>
        {
            var qualifies = ComputedOverloadEligibility.QualifiesForKeyInputMode(entity);

            return qualifies.Label(
                $"Entity '{entity.ClassName}' with string SK + prefix (PK no prefix) should qualify for KeyInputMode. " +
                $"PK: type={entity.PartitionKeyProperty?.PropertyType}, prefix={entity.PartitionKeyProperty?.KeyFormat?.Prefix}, " +
                $"SK: type={entity.SortKeyProperty?.PropertyType}, prefix={entity.SortKeyProperty?.KeyFormat?.Prefix}");
        });
    }

    #region Generators

    /// <summary>
    /// Generates entities with string PK + non-empty prefix, no computed key with ≥2 sources.
    /// These should qualify for KeyInputMode.
    /// </summary>
    private static Arbitrary<EntityModel> CreateStringPkWithPrefixNoComputedGenerator()
    {
        var gen = from className in GenSafeIdentifier()
                  from tableName in GenSafeIdentifier()
                  from prefix in GenNonEmptyPrefix()
                  from hasSk in Gen.Elements(true, false)
                  from skPrefix in Gen.Elements<string?>(null, "SK_PREFIX", "SORT")
                  let entity = BuildStringPkWithPrefixEntity(className, tableName, prefix, hasSk, skPrefix)
                  select entity;

        return Arb.From(gen);
    }

    /// <summary>
    /// Generates entities with string PK but NO prefix (and no SK with prefix either).
    /// These should NOT qualify for KeyInputMode.
    /// </summary>
    private static Arbitrary<EntityModel> CreateStringPkWithoutPrefixGenerator()
    {
        var gen = from className in GenSafeIdentifier()
                  from tableName in GenSafeIdentifier()
                  from hasSk in Gen.Elements(true, false)
                  let entity = BuildStringPkNoPrefixEntity(className, tableName, hasSk)
                  select entity;

        return Arb.From(gen);
    }

    /// <summary>
    /// Generates entities with non-string PK types (int, Guid, etc.) and no SK with string+prefix.
    /// These should NOT qualify for KeyInputMode.
    /// </summary>
    private static Arbitrary<EntityModel> CreateNonStringPkNoPrefixedStringSkGenerator()
    {
        var gen = from className in GenSafeIdentifier()
                  from tableName in GenSafeIdentifier()
                  from pkType in Gen.Elements(NonStringTypes)
                  from hasSk in Gen.Elements(true, false)
                  from skType in Gen.Elements(NonStringTypes)
                  let entity = BuildNonStringPkEntity(className, tableName, pkType, hasSk, skType)
                  select entity;

        return Arb.From(gen);
    }

    /// <summary>
    /// Generates entities with computed PK (≥2 sources, at least one non-string)
    /// that qualify for typed overloads and are NOT ambiguous.
    /// These should NOT qualify for KeyInputMode since typed overloads handle disambiguation.
    /// </summary>
    private static Arbitrary<EntityModel> CreateComputedPkWithTypedOverloadGenerator()
    {
        var gen = from className in GenSafeIdentifier()
                  from tableName in GenSafeIdentifier()
                  from pkSourceCount in Gen.Choose(2, 4)
                  from prefix in Gen.Elements<string?>("ORDER", "EVENT", null)
                  let entity = BuildComputedPkEntity(className, tableName, pkSourceCount, prefix)
                  where ComputedOverloadEligibility.QualifiesForTypedOverload(entity)
                      && !ComputedOverloadEligibility.WouldBeAmbiguous(entity)
                  select entity;

        return Arb.From(gen);
    }

    /// <summary>
    /// Generates entities with string PK (no prefix) + string SK with prefix.
    /// These should qualify for KeyInputMode due to the SK having a prefix.
    /// </summary>
    private static Arbitrary<EntityModel> CreateStringSkWithPrefixPkNoPrefixGenerator()
    {
        var gen = from className in GenSafeIdentifier()
                  from tableName in GenSafeIdentifier()
                  from skPrefix in GenNonEmptyPrefix()
                  let entity = BuildStringSkWithPrefixEntity(className, tableName, skPrefix)
                  select entity;

        return Arb.From(gen);
    }

    #endregion

    #region Generator Helpers

    private static Gen<string> GenSafeIdentifier()
    {
        return Gen.Elements("Alpha", "Beta", "Gamma", "Delta", "Epsilon", "Zeta", "Eta", "Theta",
                "Order", "Event", "Invoice", "Customer", "Product", "Session", "Record", "Entry");
    }

    private static Gen<string> GenNonEmptyPrefix()
    {
        return Gen.Elements("ORDER", "CUSTOMER", "EVENT", "PRODUCT", "USER", "ITEM", "RECORD");
    }

    #endregion

    #region Entity Builders

    /// <summary>
    /// Builds an entity with string PK that has a prefix, no computed key.
    /// </summary>
    private static EntityModel BuildStringPkWithPrefixEntity(
        string className, string tableName, string prefix, bool hasSk, string? skPrefix)
    {
        var properties = new List<PropertyModel>
        {
            new PropertyModel
            {
                PropertyName = "Pk",
                PropertyType = "string",
                AttributeName = "pk",
                IsPartitionKey = true,
                KeyFormat = new KeyFormatModel { Prefix = prefix }
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

        properties.Add(new PropertyModel
        {
            PropertyName = "Data",
            PropertyType = "string",
            AttributeName = "data"
        });

        return CreateEntity(className, tableName, properties.ToArray());
    }

    /// <summary>
    /// Builds an entity with string PK but no prefix, and no SK with prefix.
    /// </summary>
    private static EntityModel BuildStringPkNoPrefixEntity(string className, string tableName, bool hasSk)
    {
        var properties = new List<PropertyModel>
        {
            new PropertyModel
            {
                PropertyName = "Pk",
                PropertyType = "string",
                AttributeName = "pk",
                IsPartitionKey = true,
                KeyFormat = null // No prefix
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
                KeyFormat = null // No prefix on SK either
            });
        }

        properties.Add(new PropertyModel
        {
            PropertyName = "Name",
            PropertyType = "string",
            AttributeName = "name"
        });

        return CreateEntity(className, tableName, properties.ToArray());
    }

    /// <summary>
    /// Builds an entity with non-string PK type and optionally a non-string SK.
    /// No key should have a string type with prefix, so it should NOT qualify.
    /// </summary>
    private static EntityModel BuildNonStringPkEntity(
        string className, string tableName, string pkType, bool hasSk, string skType)
    {
        var properties = new List<PropertyModel>
        {
            new PropertyModel
            {
                PropertyName = "Pk",
                PropertyType = pkType,
                AttributeName = "pk",
                IsPartitionKey = true,
                KeyFormat = new KeyFormatModel { Prefix = "PREFIX" } // has prefix but non-string type
            }
        };

        if (hasSk)
        {
            properties.Add(new PropertyModel
            {
                PropertyName = "Sk",
                PropertyType = skType,
                AttributeName = "sk",
                IsSortKey = true,
                KeyFormat = new KeyFormatModel { Prefix = "SK_PREFIX" } // has prefix but non-string type
            });
        }

        properties.Add(new PropertyModel
        {
            PropertyName = "Value",
            PropertyType = "string",
            AttributeName = "value"
        });

        return CreateEntity(className, tableName, properties.ToArray());
    }

    /// <summary>
    /// Builds an entity with computed PK (≥2 sources, at least one non-string for non-ambiguity).
    /// This entity qualifies for typed overloads and should NOT qualify for KeyInputMode.
    /// </summary>
    private static EntityModel BuildComputedPkEntity(
        string className, string tableName, int pkSourceCount, string? prefix)
    {
        var properties = new List<PropertyModel>();
        var pkSourceProps = new List<string>();

        // Source properties — first is always int to ensure non-ambiguity
        for (int i = 0; i < pkSourceCount; i++)
        {
            var propName = $"Part{i + 1}";
            var propType = i == 0 ? "int" : (i == 1 ? "string" : NonStringTypes[i % NonStringTypes.Length]);
            properties.Add(new PropertyModel
            {
                PropertyName = propName,
                PropertyType = propType,
                AttributeName = propName.ToLowerInvariant()
            });
            pkSourceProps.Add(propName);
        }

        // Computed PK with optional prefix
        properties.Add(new PropertyModel
        {
            PropertyName = "Pk",
            PropertyType = "string",
            AttributeName = "pk",
            IsPartitionKey = true,
            KeyFormat = !string.IsNullOrEmpty(prefix) ? new KeyFormatModel { Prefix = prefix } : null,
            ComputedKey = new ComputedKeyModel
            {
                SourceProperties = pkSourceProps.ToArray(),
                Separator = "#"
            }
        });

        // Simple string SK
        properties.Add(new PropertyModel
        {
            PropertyName = "Sk",
            PropertyType = "string",
            AttributeName = "sk",
            IsSortKey = true
        });

        return CreateEntity(className, tableName, properties.ToArray());
    }

    /// <summary>
    /// Builds an entity with string PK (no prefix) and string SK with prefix.
    /// Should qualify for KeyInputMode via the SK.
    /// </summary>
    private static EntityModel BuildStringSkWithPrefixEntity(
        string className, string tableName, string skPrefix)
    {
        var properties = new List<PropertyModel>
        {
            new PropertyModel
            {
                PropertyName = "Pk",
                PropertyType = "string",
                AttributeName = "pk",
                IsPartitionKey = true,
                KeyFormat = null // PK has no prefix
            },
            new PropertyModel
            {
                PropertyName = "Sk",
                PropertyType = "string",
                AttributeName = "sk",
                IsSortKey = true,
                KeyFormat = new KeyFormatModel { Prefix = skPrefix }
            },
            new PropertyModel
            {
                PropertyName = "Description",
                PropertyType = "string",
                AttributeName = "description"
            }
        };

        return CreateEntity(className, tableName, properties.ToArray());
    }

    private static EntityModel CreateEntity(string className, string tableName, PropertyModel[] properties)
    {
        return new EntityModel
        {
            ClassName = className,
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
