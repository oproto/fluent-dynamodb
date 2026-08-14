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
    /// IsComputed == true, QualifiesForTypedOverload SHALL return false.
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
    /// For any EntityModel where neither key is computed,
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
                // Typed overloads would contain calls to Keys.Pk or Keys.Sk
                var hasPkCall = generatedCode.Contains("Keys.Pk(");
                var hasSkCall = generatedCode.Contains("Keys.Sk(");

                return (!qualifies && !hasPkCall && !hasSkCall)
                    .Label($"Entity '{entity.ClassName}' (hasSK={hasSk}) should not have typed overloads. " +
                           $"qualifies={qualifies}, hasPk={hasPkCall}, hasSk={hasSkCall}.");
            });
    }

    /// <summary>
    /// Property 1: Bug Condition — Single Source Computed Key Typed Overload Eligibility
    ///
    /// For any EntityModel where at least one key is computed with exactly one non-string
    /// source property, QualifiesForTypedOverload SHALL return true,
    /// GetTypedOverloadParameters SHALL resolve the source property to its declared type
    /// (not fallback "string"), and WouldBeAmbiguous SHALL return false (since a non-string
    /// typed overload signature differs from the standard string overload).
    ///
    /// **Validates: Requirements 1.1, 2.1, 2.3**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property SingleSourceNonStringComputedKey_QualifiesForTypedOverload()
    {
        var entityGen = CreateSingleSourceNonStringComputedEntityGenerator();

        return Prop.ForAll(
            entityGen.ToArbitrary(),
            entity =>
            {
                // Assert 1: QualifiesForTypedOverload should return true
                var qualifies = ComputedOverloadEligibility.QualifiesForTypedOverload(entity);

                // Assert 2: GetTypedOverloadParameters should resolve source property to its declared type
                var typedParams = OverloadParameterResolver.GetTypedOverloadParameters(entity);
                var standardParams = OverloadParameterResolver.GetStandardOverloadParameters(entity);

                // Find the computed key's source property type
                var pk = entity.PartitionKeyProperty;
                var sk = entity.SortKeyProperty;
                var computedKey = (pk?.IsComputed == true && pk.ComputedKey!.SourceProperties.Length == 1) ? pk : sk;
                var sourcePropName = computedKey!.ComputedKey!.SourceProperties[0];
                var sourceProp = entity.Properties.First(p => p.PropertyName == sourcePropName);
                var expectedType = sourceProp.PropertyType;

                // The typed params should contain the declared type, not "string" fallback
                var hasResolvedType = typedParams != null &&
                    typedParams.Any(p => p.Type == expectedType && p.Type != "string");

                // Assert 3: WouldBeAmbiguous should return false (non-string != string)
                var isAmbiguous = ComputedOverloadEligibility.WouldBeAmbiguous(entity);

                return (qualifies && hasResolvedType && !isAmbiguous)
                    .Label($"Entity '{entity.ClassName}' with single {expectedType} source on " +
                           $"{(computedKey == pk ? "PK" : "SK")} — " +
                           $"QualifiesForTypedOverload={qualifies} (expected true), " +
                           $"hasResolvedType={hasResolvedType} (expected true, typedParams={FormatParams(typedParams)}), " +
                           $"WouldBeAmbiguous={isAmbiguous} (expected false).");
            });
    }

    private static string FormatParams(List<OverloadParameterResolver.ParameterInfo>? parms)
    {
        if (parms == null) return "null";
        return string.Join(", ", parms.Select(p => $"{p.Type} {p.Name}"));
    }

    #region Preservation Property Tests

    /// <summary>
    /// Property 2 (Preservation): Multi-source computed key entities qualify for typed overloads.
    ///
    /// For all entities where NOT isBugCondition(X) and the entity has 2+ source computed keys,
    /// QualifiesForTypedOverload SHALL return true.
    ///
    /// **Validates: Requirements 3.1**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property MultiSourceComputedEntities_QualifyForTypedOverloads()
    {
        var entityGen = CreateMultiSourceComputedEntityGenerator();

        return Prop.ForAll(
            entityGen.ToArbitrary(),
            entity =>
            {
                var qualifies = ComputedOverloadEligibility.QualifiesForTypedOverload(entity);

                return qualifies
                    .Label($"Entity '{entity.ClassName}' with 2+ source computed key should qualify. " +
                           $"PK: isComputed={entity.PartitionKeyProperty?.IsComputed}, " +
                           $"sourceProps={entity.PartitionKeyProperty?.ComputedKey?.SourceProperties.Length ?? 0}. " +
                           $"SK: isComputed={entity.SortKeyProperty?.IsComputed}, " +
                           $"sourceProps={entity.SortKeyProperty?.ComputedKey?.SourceProperties.Length ?? 0}.");
            });
    }

    /// <summary>
    /// Property 2 (Preservation): WouldBeAmbiguous correctly evaluates multi-source all-string entities.
    ///
    /// For all entities with 2+ source computed keys where all sources are string,
    /// WouldBeAmbiguous SHALL return a consistent result based on type comparison.
    /// With 2+ sources, the typed param count differs from standard, so WouldBeAmbiguous returns false
    /// and the typed overload IS generated (it has more parameters, making it unambiguous).
    ///
    /// **Validates: Requirements 3.2**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property MultiSourceAllStringEntities_WouldBeAmbiguous_ConsistentResult()
    {
        var entityGen = CreateMultiSourceAllStringEntityGenerator();

        return Prop.ForAll(
            entityGen.ToArbitrary(),
            entity =>
            {
                // Must qualify first
                var qualifies = ComputedOverloadEligibility.QualifiesForTypedOverload(entity);
                if (!qualifies)
                    return true.Label("Does not qualify — skipping (unexpected)");

                var ambiguous = ComputedOverloadEligibility.WouldBeAmbiguous(entity);

                // With 2+ sources, typed params always have more params than standard
                // (e.g. typed: (str1, str2, sK) vs standard: (pK, sK))
                // So WouldBeAmbiguous should return false because counts differ
                var typedParams = OverloadParameterResolver.GetTypedOverloadParameters(entity);
                var standardParams = OverloadParameterResolver.GetStandardOverloadParameters(entity);
                var expectedAmbiguous = typedParams != null
                    && typedParams.Count == standardParams.Count
                    && typedParams.Zip(standardParams, (t, s) => t.Type == s.Type).All(x => x);

                return (ambiguous == expectedAmbiguous)
                    .Label($"Entity '{entity.ClassName}' WouldBeAmbiguous mismatch. " +
                           $"Expected={expectedAmbiguous}, Got={ambiguous}. " +
                           $"TypedParams={typedParams?.Count ?? -1}, StandardParams={standardParams.Count}.");
            });
    }

    /// <summary>
    /// Property 2 (Preservation): Non-computed entities do not qualify for typed overloads.
    ///
    /// For all entities with NO computed keys at all,
    /// QualifiesForTypedOverload SHALL return false.
    ///
    /// **Validates: Requirements 3.3**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property NonComputedEntities_DoNotQualify_Preservation()
    {
        var entityGen = CreatePureNonComputedEntityGenerator();

        return Prop.ForAll(
            entityGen.ToArbitrary(),
            entity =>
            {
                var qualifies = ComputedOverloadEligibility.QualifiesForTypedOverload(entity);

                return (!qualifies)
                    .Label($"Entity '{entity.ClassName}' with no computed keys should NOT qualify. " +
                           $"PK: isComputed={entity.PartitionKeyProperty?.IsComputed}. " +
                           $"SK: isComputed={entity.SortKeyProperty?.IsComputed}.");
            });
    }

    /// <summary>
    /// Property 2 (Preservation): GetTypedOverloadParameters resolves all source properties for 2+ source keys.
    ///
    /// For all entities with 2+ source computed keys,
    /// GetTypedOverloadParameters SHALL resolve all source properties to their declared types.
    ///
    /// **Validates: Requirements 3.1**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property MultiSourceEntities_GetTypedOverloadParameters_ResolvesCorrectly()
    {
        var entityGen = CreateMultiSourceComputedEntityGenerator();

        return Prop.ForAll(
            entityGen.ToArbitrary(),
            entity =>
            {
                var typedParams = OverloadParameterResolver.GetTypedOverloadParameters(entity);

                if (typedParams == null)
                    return false.Label("GetTypedOverloadParameters returned null for multi-source entity");

                // Count expected parameters: resolved source props for computed keys, "pK"/"sK" for non-computed
                var pk = entity.PartitionKeyProperty;
                var sk = entity.SortKeyProperty;
                int expectedCount = 0;

                if (pk?.IsComputed == true && pk.ComputedKey!.SourceProperties.Length >= 2)
                    expectedCount += pk.ComputedKey.SourceProperties.Length;
                else if (pk != null)
                    expectedCount += 1;

                if (sk?.IsComputed == true && sk.ComputedKey!.SourceProperties.Length >= 2)
                    expectedCount += sk.ComputedKey.SourceProperties.Length;
                else if (sk != null)
                    expectedCount += 1;

                // Verify each resolved source property has correct type
                var allTypesCorrect = true;
                int paramIdx = 0;

                if (pk?.IsComputed == true && pk.ComputedKey!.SourceProperties.Length >= 2)
                {
                    foreach (var sourcePropName in pk.ComputedKey.SourceProperties)
                    {
                        var prop = entity.Properties.First(p => p.PropertyName == sourcePropName);
                        if (paramIdx < typedParams.Count && typedParams[paramIdx].Type != prop.PropertyType)
                            allTypesCorrect = false;
                        paramIdx++;
                    }
                }
                else if (pk != null)
                {
                    if (paramIdx < typedParams.Count && typedParams[paramIdx].Type != "string")
                        allTypesCorrect = false;
                    paramIdx++;
                }

                if (sk?.IsComputed == true && sk.ComputedKey!.SourceProperties.Length >= 2)
                {
                    foreach (var sourcePropName in sk.ComputedKey.SourceProperties)
                    {
                        var prop = entity.Properties.First(p => p.PropertyName == sourcePropName);
                        if (paramIdx < typedParams.Count && typedParams[paramIdx].Type != prop.PropertyType)
                            allTypesCorrect = false;
                        paramIdx++;
                    }
                }
                else if (sk != null)
                {
                    if (paramIdx < typedParams.Count && typedParams[paramIdx].Type != "string")
                        allTypesCorrect = false;
                    paramIdx++;
                }

                return (typedParams.Count == expectedCount && allTypesCorrect)
                    .Label($"Parameter resolution mismatch. Expected {expectedCount} params, got {typedParams.Count}. " +
                           $"Types correct: {allTypesCorrect}. " +
                           $"Params: [{string.Join(", ", typedParams.Select(p => $"{p.Type} {p.Name}"))}]");
            });
    }

    /// <summary>
    /// Property 2 (Preservation): QualifiesForKeyInputMode evaluates prefix eligibility for non-computed entities.
    ///
    /// For non-computed entities with string key prefixes,
    /// QualifiesForKeyInputMode SHALL return true (prefix evaluation proceeds since no typed overload exists).
    ///
    /// **Validates: Requirements 3.4**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property NonComputedEntitiesWithPrefix_QualifiesForKeyInputMode_ReturnsTrue()
    {
        var entityGen = CreateNonComputedWithPrefixEntityGenerator();

        return Prop.ForAll(
            entityGen.ToArbitrary(),
            entity =>
            {
                var qualifiesForKeyInput = ComputedOverloadEligibility.QualifiesForKeyInputMode(entity);

                // Non-computed entities don't qualify for typed overloads, so they fall through
                // to prefix evaluation. With prefix present, QualifiesForKeyInputMode returns true.
                return qualifiesForKeyInput
                    .Label($"Entity '{entity.ClassName}' with prefix but no typed overload should qualify for KeyInputMode. " +
                           $"QualifiesForTypedOverload={ComputedOverloadEligibility.QualifiesForTypedOverload(entity)}, " +
                           $"PK prefix={entity.PartitionKeyProperty?.KeyFormat?.Prefix}, " +
                           $"SK prefix={entity.SortKeyProperty?.KeyFormat?.Prefix}.");
            });
    }

    /// <summary>
    /// Property 2 (Preservation): QualifiesForKeyInputMode returns false when non-ambiguous typed overload exists.
    ///
    /// For multi-source entities that are NOT ambiguous (have at least one non-string source),
    /// QualifiesForKeyInputMode SHALL return false.
    ///
    /// **Validates: Requirements 3.5**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property NonAmbiguousMultiSourceEntities_QualifiesForKeyInputMode_ReturnsFalse()
    {
        var entityGen = CreateNonAmbiguousMultiSourceEntityGenerator();

        return Prop.ForAll(
            entityGen.ToArbitrary(),
            entity =>
            {
                var qualifiesForKeyInput = ComputedOverloadEligibility.QualifiesForKeyInputMode(entity);

                return (!qualifiesForKeyInput)
                    .Label($"Entity '{entity.ClassName}' with non-ambiguous typed overload should NOT qualify for KeyInputMode. " +
                           $"QualifiesForTypedOverload={ComputedOverloadEligibility.QualifiesForTypedOverload(entity)}, " +
                           $"WouldBeAmbiguous={ComputedOverloadEligibility.WouldBeAmbiguous(entity)}.");
            });
    }

    #endregion

    #region Preservation Generators

    private static readonly string[] NonStringTypes = { "int", "long", "DateTime", "Guid", "decimal", "DateOnly" };

    /// <summary>
    /// Creates a generator for entities with at least one computed key having 2+ source properties.
    /// Ensures at least one non-string source to avoid ambiguity.
    /// NOT isBugCondition: all computed keys have 2+ sources.
    /// </summary>
    private static Gen<EntityModel> CreateMultiSourceComputedEntityGenerator()
    {
        var classNameGen = Gen.Elements("MultiOrder", "MultiUser", "MultiProduct", "MultiEvent", "MultiAccount");
        var tableNameGen = Gen.Elements("multi-orders", "multi-users", "multi-products", "multi-events");
        var sourceCountGen = Gen.Choose(2, 5);
        var hasSortKeyGen = Gen.Elements(true, false);
        // Scenario: 0 = computed PK only, 1 = computed SK only, 2 = both computed
        var scenarioGen = Gen.Choose(0, 2);

        return from className in classNameGen
               from tableName in tableNameGen
               from pkSourceCount in sourceCountGen
               from skSourceCount in sourceCountGen
               from hasSk in hasSortKeyGen
               from scenario in scenarioGen
               let entity = BuildMultiSourceComputedEntity(className, tableName, pkSourceCount, skSourceCount, hasSk, scenario)
               where ComputedOverloadEligibility.QualifiesForTypedOverload(entity)
               select entity;
    }

    /// <summary>
    /// Creates a generator for entities with 2+ source computed keys where ALL sources are string.
    /// These should be ambiguous (typed overload matches standard overload signature).
    /// </summary>
    private static Gen<EntityModel> CreateMultiSourceAllStringEntityGenerator()
    {
        var classNameGen = Gen.Elements("StringOrder", "StringUser", "StringProduct", "StringEvent");
        var tableNameGen = Gen.Elements("string-orders", "string-users", "string-products");
        var sourceCountGen = Gen.Choose(2, 4);
        var hasSortKeyGen = Gen.Elements(true, false);

        return from className in classNameGen
               from tableName in tableNameGen
               from sourceCount in sourceCountGen
               from hasSk in hasSortKeyGen
               let entity = BuildAllStringMultiSourceEntity(className, tableName, sourceCount, hasSk)
               where ComputedOverloadEligibility.QualifiesForTypedOverload(entity)
               select entity;
    }

    /// <summary>
    /// Creates a generator for entities with NO computed keys at all.
    /// These should never qualify for typed overloads.
    /// </summary>
    private static Gen<EntityModel> CreatePureNonComputedEntityGenerator()
    {
        var classNameGen = Gen.Elements("SimpleOrder", "SimpleUser", "SimpleProduct", "SimpleEvent", "SimpleAccount");
        var tableNameGen = Gen.Elements("simple-orders", "simple-users", "simple-products");
        var hasSortKeyGen = Gen.Elements(true, false);
        var hasPrefixGen = Gen.Elements(true, false);

        return from className in classNameGen
               from tableName in tableNameGen
               from hasSk in hasSortKeyGen
               from hasPrefix in hasPrefixGen
               select BuildPureNonComputedEntity(className, tableName, hasSk, hasPrefix);
    }

    /// <summary>
    /// Creates a generator for non-computed entities with string key prefixes.
    /// QualifiesForKeyInputMode should return true for these (prefix is present, no typed overload).
    /// </summary>
    private static Gen<EntityModel> CreateNonComputedWithPrefixEntityGenerator()
    {
        var classNameGen = Gen.Elements("PrefixOrder", "PrefixUser", "PrefixProduct", "PrefixEvent");
        var tableNameGen = Gen.Elements("prefix-orders", "prefix-users", "prefix-products");
        var hasSortKeyGen = Gen.Elements(true, false);

        return from className in classNameGen
               from tableName in tableNameGen
               from hasSk in hasSortKeyGen
               select BuildNonComputedWithPrefixEntity(className, tableName, hasSk);
    }

    /// <summary>
    /// Creates a generator for non-ambiguous multi-source entities (has at least one non-string source).
    /// QualifiesForKeyInputMode should return false for these (typed overload handles it).
    /// </summary>
    private static Gen<EntityModel> CreateNonAmbiguousMultiSourceEntityGenerator()
    {
        var classNameGen = Gen.Elements("TypedOrder", "TypedUser", "TypedProduct", "TypedEvent");
        var tableNameGen = Gen.Elements("typed-orders", "typed-users", "typed-products");
        var sourceCountGen = Gen.Choose(2, 4);

        return from className in classNameGen
               from tableName in tableNameGen
               from sourceCount in sourceCountGen
               let entity = BuildNonAmbiguousMultiSourceEntity(className, tableName, sourceCount)
               where ComputedOverloadEligibility.QualifiesForTypedOverload(entity)
                     && !ComputedOverloadEligibility.WouldBeAmbiguous(entity)
               select entity;
    }

    #endregion

    #region Preservation Entity Builders

    private static EntityModel BuildMultiSourceComputedEntity(
        string className, string tableName, int pkSourceCount, int skSourceCount, bool hasSk, int scenario)
    {
        var properties = new List<PropertyModel>();
        var pkSourceProps = new List<string>();
        var skSourceProps = new List<string>();

        // Determine which key(s) are computed based on scenario
        bool pkIsComputed = scenario == 0 || scenario == 2;
        bool skIsComputed = (scenario == 1 || scenario == 2) && hasSk;

        if (pkIsComputed)
        {
            // PK source properties — first is always int to avoid ambiguity
            for (int i = 0; i < pkSourceCount; i++)
            {
                var propName = $"PkField{i + 1}";
                var propType = i == 0 ? "int" : NonStringTypes[i % NonStringTypes.Length];
                properties.Add(new PropertyModel
                {
                    PropertyName = propName,
                    PropertyType = propType,
                    AttributeName = propName.ToLowerInvariant()
                });
                pkSourceProps.Add(propName);
            }
        }

        // PK property
        var pkProperty = new PropertyModel
        {
            PropertyName = "Pk",
            PropertyType = "string",
            AttributeName = "pk",
            IsPartitionKey = true
        };
        if (pkIsComputed)
        {
            pkProperty.ComputedKey = new ComputedKeyModel
            {
                SourceProperties = pkSourceProps.ToArray(),
                Separator = "#"
            };
        }
        properties.Add(pkProperty);

        if (hasSk)
        {
            if (skIsComputed)
            {
                // SK source properties — first is always long to avoid ambiguity
                for (int i = 0; i < skSourceCount; i++)
                {
                    var propName = $"SkField{i + 1}";
                    var propType = i == 0 ? "long" : NonStringTypes[(i + 2) % NonStringTypes.Length];
                    properties.Add(new PropertyModel
                    {
                        PropertyName = propName,
                        PropertyType = propType,
                        AttributeName = propName.ToLowerInvariant()
                    });
                    skSourceProps.Add(propName);
                }
            }

            var skProperty = new PropertyModel
            {
                PropertyName = "Sk",
                PropertyType = "string",
                AttributeName = "sk",
                IsSortKey = true
            };
            if (skIsComputed)
            {
                skProperty.ComputedKey = new ComputedKeyModel
                {
                    SourceProperties = skSourceProps.ToArray(),
                    Separator = "#"
                };
            }
            properties.Add(skProperty);
        }

        // Add a non-key property
        properties.Add(new PropertyModel
        {
            PropertyName = "Data",
            PropertyType = "string",
            AttributeName = "data"
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

    private static EntityModel BuildAllStringMultiSourceEntity(
        string className, string tableName, int sourceCount, bool hasSk)
    {
        var properties = new List<PropertyModel>();
        var pkSourceProps = new List<string>();

        // All string source properties for PK
        for (int i = 0; i < sourceCount; i++)
        {
            var propName = $"PkStr{i + 1}";
            properties.Add(new PropertyModel
            {
                PropertyName = propName,
                PropertyType = "string",
                AttributeName = propName.ToLowerInvariant()
            });
            pkSourceProps.Add(propName);
        }

        // Computed PK with all-string sources
        properties.Add(new PropertyModel
        {
            PropertyName = "Pk",
            PropertyType = "string",
            AttributeName = "pk",
            IsPartitionKey = true,
            ComputedKey = new ComputedKeyModel
            {
                SourceProperties = pkSourceProps.ToArray(),
                Separator = "#"
            }
        });

        if (hasSk)
        {
            properties.Add(new PropertyModel
            {
                PropertyName = "Sk",
                PropertyType = "string",
                AttributeName = "sk",
                IsSortKey = true
            });
        }

        properties.Add(new PropertyModel
        {
            PropertyName = "Data",
            PropertyType = "string",
            AttributeName = "data"
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

    private static EntityModel BuildPureNonComputedEntity(
        string className, string tableName, bool hasSk, bool hasPrefix)
    {
        var properties = new List<PropertyModel>();

        properties.Add(new PropertyModel
        {
            PropertyName = "Pk",
            PropertyType = "string",
            AttributeName = "pk",
            IsPartitionKey = true,
            KeyFormat = hasPrefix ? new KeyFormatModel { Prefix = "PK_PREFIX" } : null
        });

        if (hasSk)
        {
            properties.Add(new PropertyModel
            {
                PropertyName = "Sk",
                PropertyType = "string",
                AttributeName = "sk",
                IsSortKey = true,
                KeyFormat = hasPrefix ? new KeyFormatModel { Prefix = "SK_PREFIX" } : null
            });
        }

        properties.Add(new PropertyModel
        {
            PropertyName = "Name",
            PropertyType = "string",
            AttributeName = "name"
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

    private static EntityModel BuildNonComputedWithPrefixEntity(
        string className, string tableName, bool hasSk)
    {
        var properties = new List<PropertyModel>();

        // String PK with prefix (makes it eligible for KeyInputMode)
        properties.Add(new PropertyModel
        {
            PropertyName = "Pk",
            PropertyType = "string",
            AttributeName = "pk",
            IsPartitionKey = true,
            KeyFormat = new KeyFormatModel { Prefix = "PREFIX" }
        });

        if (hasSk)
        {
            properties.Add(new PropertyModel
            {
                PropertyName = "Sk",
                PropertyType = "string",
                AttributeName = "sk",
                IsSortKey = true,
                KeyFormat = new KeyFormatModel { Prefix = "SK_PREFIX" }
            });
        }

        properties.Add(new PropertyModel
        {
            PropertyName = "Data",
            PropertyType = "string",
            AttributeName = "data"
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

    private static EntityModel BuildNonAmbiguousMultiSourceEntity(
        string className, string tableName, int sourceCount)
    {
        var properties = new List<PropertyModel>();
        var pkSourceProps = new List<string>();

        // At least one non-string source to make it non-ambiguous
        for (int i = 0; i < sourceCount; i++)
        {
            var propName = $"PkSrc{i + 1}";
            var propType = i == 0 ? "int" : NonStringTypes[i % NonStringTypes.Length];
            properties.Add(new PropertyModel
            {
                PropertyName = propName,
                PropertyType = propType,
                AttributeName = propName.ToLowerInvariant()
            });
            pkSourceProps.Add(propName);
        }

        // Computed PK with non-string sources (non-ambiguous)
        properties.Add(new PropertyModel
        {
            PropertyName = "Pk",
            PropertyType = "string",
            AttributeName = "pk",
            IsPartitionKey = true,
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

        properties.Add(new PropertyModel
        {
            PropertyName = "Data",
            PropertyType = "string",
            AttributeName = "data"
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

    #region Generators

    /// <summary>
    /// Creates a generator for EntityModels satisfying the bug condition:
    /// at least one key is computed with exactly one NON-STRING source property.
    /// Generates entities with single DateTime, int, Guid, long, decimal, or DateOnly sources on PK or SK.
    /// </summary>
    private static Gen<EntityModel> CreateSingleSourceNonStringComputedEntityGenerator()
    {
        var classNameGen = Gen.Elements(
            "OrderEntity", "EventEntity", "MetricEntity", "AuditEntity", "ScheduleEntity", "TrackingEntity");
        var tableNameGen = Gen.Elements(
            "orders-table", "events-table", "metrics-table", "audit-table", "schedule-table");
        var nonStringTypeGen = Gen.Elements("DateTime", "int", "Guid", "long", "decimal", "DateOnly");
        // Whether the single-source computed key is on PK or SK
        var computedOnPkGen = Gen.Elements(true, false);

        return from className in classNameGen
               from tableName in tableNameGen
               from sourceType in nonStringTypeGen
               from computedOnPk in computedOnPkGen
               select BuildSingleSourceNonStringComputedEntity(className, tableName, sourceType, computedOnPk);
    }

    /// <summary>
    /// Creates a generator for EntityModels where NO key is computed (IsComputed == true).
    /// After the fix, ANY entity with IsComputed == true (ComputedKey != null) qualifies
    /// for typed overloads regardless of source count.
    /// So this generator only produces entities with no ComputedKey at all.
    /// </summary>
    private static Gen<EntityModel> CreateNonComputedEntityGenerator()
    {
        var classNameGen = Gen.Elements("SimpleEntity", "OrderEntity", "UserEntity", "ProductEntity", "AccountEntity");
        var tableNameGen = Gen.Elements("simple-table", "orders-table", "users-table", "products-table");
        var keyTypeGen = Gen.Elements("string", "int", "Guid", "long", "DateTime");
        var hasSortKeyGen = Gen.Elements(true, false);
        var hasPrefixGen = Gen.Elements(true, false);

        return from className in classNameGen
               from tableName in tableNameGen
               from pkType in keyTypeGen
               from skType in keyTypeGen
               from hasSk in hasSortKeyGen
               from hasPrefix in hasPrefixGen
               select BuildNonComputedEntity(className, tableName, pkType, skType, hasSk, hasPrefix);
    }

    /// <summary>
    /// Creates a generator for EntityModels with string keys that are NOT computed.
    /// These are suitable for code generation testing (TableGenerator requires well-formed entities).
    /// After the fix, any entity with IsComputed == true qualifies for typed overloads,
    /// so this generator no longer produces entities with single-source computed keys.
    /// </summary>
    private static Gen<EntityModel> CreateNonComputedStringKeyEntityGenerator()
    {
        var classNameGen = Gen.Elements("OrderEntity", "UserEntity", "ProductEntity", "EventEntity");
        var tableNameGen = Gen.Elements("orders-table", "users-table", "products-table", "events-table");
        var hasSortKeyGen = Gen.Elements(true, false);
        var hasPrefixGen = Gen.Elements(true, false);

        return from className in classNameGen
               from tableName in tableNameGen
               from hasSk in hasSortKeyGen
               from hasPkPrefix in hasPrefixGen
               from hasSkPrefix in hasPrefixGen
               select BuildNonComputedStringKeyEntity(className, tableName, hasSk, hasPkPrefix, hasSkPrefix, false);
    }

    #endregion

    #region Entity Builders

    private static EntityModel BuildSingleSourceNonStringComputedEntity(
        string className,
        string tableName,
        string sourceType,
        bool computedOnPk)
    {
        var properties = new List<PropertyModel>();
        var sourcePropertyName = sourceType switch
        {
            "DateTime" => "CreationDateTime",
            "int" => "Year",
            "Guid" => "CorrelationId",
            "long" => "SequenceNumber",
            "decimal" => "Amount",
            "DateOnly" => "EventDate",
            _ => "SourceField"
        };

        if (computedOnPk)
        {
            // PK is computed with single non-string source
            var pkProperty = new PropertyModel
            {
                PropertyName = "Pk",
                PropertyType = "string",
                AttributeName = "pk",
                IsPartitionKey = true,
                ComputedKey = new ComputedKeyModel
                {
                    SourceProperties = new[] { sourcePropertyName },
                    Separator = "#"
                }
            };
            properties.Add(pkProperty);

            // Add a simple string SK
            properties.Add(new PropertyModel
            {
                PropertyName = "Sk",
                PropertyType = "string",
                AttributeName = "sk",
                IsSortKey = true
            });
        }
        else
        {
            // PK is a simple string key
            properties.Add(new PropertyModel
            {
                PropertyName = "Pk",
                PropertyType = "string",
                AttributeName = "pk",
                IsPartitionKey = true
            });

            // SK is computed with single non-string source
            var skProperty = new PropertyModel
            {
                PropertyName = "Sk",
                PropertyType = "string",
                AttributeName = "sk",
                IsSortKey = true,
                ComputedKey = new ComputedKeyModel
                {
                    SourceProperties = new[] { sourcePropertyName },
                    Separator = "#"
                }
            };
            properties.Add(skProperty);
        }

        // Add the source property with the non-string type
        properties.Add(new PropertyModel
        {
            PropertyName = sourcePropertyName,
            PropertyType = sourceType,
            AttributeName = sourcePropertyName.ToLower(),
            IsNullable = false
        });

        // Add a data property
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

    private static EntityModel BuildNonComputedEntity(
        string className,
        string tableName,
        string pkType,
        string skType,
        bool hasSortKey,
        bool hasPrefix)
    {
        var properties = new List<PropertyModel>();

        // No computed key at all — these entities should never qualify for typed overloads
        var pkProperty = new PropertyModel
        {
            PropertyName = "Pk",
            PropertyType = pkType,
            AttributeName = "pk",
            IsPartitionKey = true,
            KeyFormat = hasPrefix ? new KeyFormatModel { Prefix = "PREFIX" } : null
        };

        properties.Add(pkProperty);

        // Optionally add sort key (never computed in non-qualifying scenarios)
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
