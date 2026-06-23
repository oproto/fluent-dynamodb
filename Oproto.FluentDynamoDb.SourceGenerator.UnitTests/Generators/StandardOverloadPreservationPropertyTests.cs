using FsCheck;
using FsCheck.Xunit;
using Oproto.FluentDynamoDb.SourceGenerator.Generators;
using Oproto.FluentDynamoDb.SourceGenerator.Models;
using SourceGenAccessModifier = Oproto.FluentDynamoDb.SourceGenerator.Models.AccessModifier;

namespace Oproto.FluentDynamoDb.SourceGenerator.UnitTests.Generators;

/// <summary>
/// Property-based tests for standard overload preservation (backward compatibility).
///
/// **Feature: computed-key-accessor-overloads, Property 9: Standard overload preservation (backward compatibility)**
///
/// For any entity that previously generated (string) or (string, string) accessor overloads,
/// those overloads SHALL remain present with identical parameter names, types, return types,
/// and method bodies after this feature is applied — regardless of whether typed overloads
/// or KeyInputMode parameters are also generated.
///
/// **Validates: Requirements 11.1, 11.5**
/// </summary>
[Trait("Category", "PropertyTest")]
public class StandardOverloadPreservationPropertyTests
{
    private static readonly string[] CrudMethods = { "Get", "Update", "Delete", "ConditionCheck" };

    /// <summary>
    /// Determines the expected standard overload parameter signature for an entity.
    /// The parameter names depend on whether NeedsSetKeyApproach is triggered (non-string key without prefix/computed).
    /// For string keys (with or without prefix/computed), names are camelCase of attribute names.
    /// </summary>
    private static string GetExpectedStandardSignature(EntityModel entity, string method)
    {
        var pk = entity.PartitionKeyProperty!;
        var sk = entity.SortKeyProperty;

        // GetKeyParameterType: returns "string" if hasPrefix or isComputed, else GetCSharpType(PropertyType)
        var pkType = GetExpectedKeyType(pk);
        var skType = sk != null ? GetExpectedKeyType(sk) : null;

        // NeedsSetKeyApproach: returns true when type is NOT string AND no prefix AND not computed
        var pkNeedsSetKey = NeedsSetKeyApproach(pk);
        var skNeedsSetKey = sk != null && NeedsSetKeyApproach(sk);
        var useSetKey = pkNeedsSetKey || skNeedsSetKey;

        var pkParamName = useSetKey ? "pK" : ToCamelCase(pk.AttributeName);
        var skParamName = sk != null ? (useSetKey ? "sK" : ToCamelCase(sk.AttributeName)) : null;

        if (sk == null)
            return $"{method}({pkType} {pkParamName})";
        else
            return $"{method}({pkType} {pkParamName}, {skType} {skParamName})";
    }

    private static string GetExpectedKeyType(PropertyModel key)
    {
        var hasPrefix = !string.IsNullOrEmpty(key.KeyFormat?.Prefix);
        var isComputed = key.IsComputed;
        if (hasPrefix || isComputed)
            return "string";
        return key.PropertyType;
    }

    private static bool NeedsSetKeyApproach(PropertyModel key)
    {
        var isStringType = key.PropertyType is "string" or "String" or "System.String";
        var hasPrefix = !string.IsNullOrEmpty(key.KeyFormat?.Prefix);
        var isComputed = key.IsComputed;
        return !isStringType && !hasPrefix && !isComputed;
    }

    private static string ToCamelCase(string name)
    {
        if (string.IsNullOrEmpty(name)) return name;
        return char.ToLowerInvariant(name[0]) + name.Substring(1);
    }

    /// <summary>
    /// Property 9: Entities with computed PK + simple SK always retain the standard
    /// string-based accessor overloads in generated code.
    /// </summary>
    [Property(MaxTest = 100)]
    public Property ComputedPkSimpleSk_RetainsStandardStringOverloads()
    {
        var entityGen = CreateComputedPkSimpleSkGenerator();

        return Prop.ForAll(entityGen, entity =>
        {
            var generatedCode = TableGenerator.GenerateTableClass(entity);

            var missingMethods = new List<string>();
            foreach (var method in CrudMethods)
            {
                var expected = GetExpectedStandardSignature(entity, method);
                if (!generatedCode.Contains(expected))
                {
                    missingMethods.Add(expected);
                }
            }

            return (missingMethods.Count == 0)
                .Label($"Entity '{entity.ClassName}' missing standard overloads: [{string.Join(", ", missingMethods)}]");
        });
    }

    /// <summary>
    /// Property 9: Entities with computed SK only (simple PK) always retain the standard
    /// string-based accessor overloads in generated code.
    /// </summary>
    [Property(MaxTest = 100)]
    public Property SimplePkComputedSk_RetainsStandardStringOverloads()
    {
        var entityGen = CreateSimplePkComputedSkGenerator();

        return Prop.ForAll(entityGen, entity =>
        {
            var generatedCode = TableGenerator.GenerateTableClass(entity);

            var missingMethods = new List<string>();
            foreach (var method in CrudMethods)
            {
                var expected = GetExpectedStandardSignature(entity, method);
                if (!generatedCode.Contains(expected))
                {
                    missingMethods.Add(expected);
                }
            }

            return (missingMethods.Count == 0)
                .Label($"Entity '{entity.ClassName}' missing standard overloads: [{string.Join(", ", missingMethods)}]");
        });
    }

    /// <summary>
    /// Property 9: Entities with both keys computed always retain the standard
    /// string-based accessor overloads in generated code.
    /// </summary>
    [Property(MaxTest = 100)]
    public Property BothKeysComputed_RetainsStandardStringOverloads()
    {
        var entityGen = CreateBothKeysComputedGenerator();

        return Prop.ForAll(entityGen, entity =>
        {
            var generatedCode = TableGenerator.GenerateTableClass(entity);

            var missingMethods = new List<string>();
            foreach (var method in CrudMethods)
            {
                var expected = GetExpectedStandardSignature(entity, method);
                if (!generatedCode.Contains(expected))
                {
                    missingMethods.Add(expected);
                }
            }

            return (missingMethods.Count == 0)
                .Label($"Entity '{entity.ClassName}' missing standard overloads: [{string.Join(", ", missingMethods)}]");
        });
    }

    /// <summary>
    /// Property 9: Entities with computed PK and no SK always retain the standard
    /// string-based accessor overloads in generated code.
    /// </summary>
    [Property(MaxTest = 100)]
    public Property ComputedPkNoSk_RetainsStandardStringOverloads()
    {
        var entityGen = CreateComputedPkNoSkGenerator();

        return Prop.ForAll(entityGen, entity =>
        {
            var generatedCode = TableGenerator.GenerateTableClass(entity);

            var missingMethods = new List<string>();
            foreach (var method in CrudMethods)
            {
                var expected = GetExpectedStandardSignature(entity, method);
                if (!generatedCode.Contains(expected))
                {
                    missingMethods.Add(expected);
                }
            }

            return (missingMethods.Count == 0)
                .Label($"Entity '{entity.ClassName}' missing standard overloads: [{string.Join(", ", missingMethods)}]");
        });
    }

    /// <summary>
    /// Property 9: Entities with string key + prefix (no computed key, KeyInputMode eligible)
    /// always retain the standard string-based accessor overloads.
    /// The KeyInputMode parameter is appended but the base string key parameters are preserved.
    /// </summary>
    [Property(MaxTest = 100)]
    public Property StringKeyWithPrefix_RetainsStandardStringOverloads()
    {
        var entityGen = CreateStringKeyWithPrefixGenerator();

        return Prop.ForAll(entityGen, entity =>
        {
            var generatedCode = TableGenerator.GenerateTableClass(entity);
            var hasSk = entity.SortKeyProperty != null;

            // Standard overloads should still have the method with string key parameters.
            // The KeyInputMode parameter is optional with a default value, so we check that
            // the method starts with the correct string key parameters.
            var missingMethods = new List<string>();
            foreach (var method in CrudMethods)
            {
                var pk = entity.PartitionKeyProperty!;
                var pkParamName = ToCamelCase(pk.AttributeName);
                var pkPattern = $"{method}(string {pkParamName}";
                if (!generatedCode.Contains(pkPattern))
                {
                    missingMethods.Add($"{method}(string {pkParamName}...)");
                }
            }

            return (missingMethods.Count == 0)
                .Label($"Entity '{entity.ClassName}' (hasSK={hasSk}) missing standard string overloads: [{string.Join(", ", missingMethods)}]");
        });
    }

    /// <summary>
    /// Property 9: Standard overloads have correct return types (GetItemRequestBuilder,
    /// UpdateItemRequestBuilder, DeleteItemRequestBuilder, ConditionCheckBuilder).
    /// </summary>
    [Property(MaxTest = 100)]
    public Property StandardOverloads_HaveCorrectReturnTypes()
    {
        var entityGen = CreateMixedEntityGenerator();

        return Prop.ForAll(entityGen, entity =>
        {
            var generatedCode = TableGenerator.GenerateTableClass(entity);
            var entityName = entity.ClassName;
            var pk = entity.PartitionKeyProperty!;
            var pkType = GetExpectedKeyType(pk);
            var pkParamName = NeedsSetKeyApproach(pk) ? "pK" : ToCamelCase(pk.AttributeName);

            // Verify return types for standard overloads
            var hasGetReturnType = generatedCode.Contains($"GetItemRequestBuilder<{entityName}> Get({pkType} {pkParamName}");
            var hasUpdateReturnType = generatedCode.Contains($"UpdateItemRequestBuilder<{entityName}> Update({pkType} {pkParamName}");
            var hasDeleteReturnType = generatedCode.Contains($"DeleteItemRequestBuilder<{entityName}> Delete({pkType} {pkParamName}");
            var hasConditionCheckReturnType = generatedCode.Contains($"ConditionCheckBuilder<{entityName}> ConditionCheck({pkType} {pkParamName}");

            return (hasGetReturnType && hasUpdateReturnType && hasDeleteReturnType && hasConditionCheckReturnType)
                .Label($"Entity '{entityName}': Get={hasGetReturnType}, Update={hasUpdateReturnType}, " +
                       $"Delete={hasDeleteReturnType}, ConditionCheck={hasConditionCheckReturnType}");
        });
    }

    #region Generators

    /// <summary>
    /// Creates generator for entities with computed PK (≥2 sources) + simple string SK.
    /// At least one non-string source property to avoid ambiguity.
    /// </summary>
    private static Arbitrary<EntityModel> CreateComputedPkSimpleSkGenerator()
    {
        var gen = from entityName in GenSafeIdentifier()
                  from tableName in GenSafeIdentifier()
                  from pkSourceCount in Gen.Choose(2, 5)
                  from hasPrefix in Gen.Elements(true, false)
                  let entity = BuildComputedPkSimpleSkEntity(entityName, tableName, pkSourceCount, hasPrefix)
                  where ComputedOverloadEligibility.QualifiesForTypedOverload(entity)
                  select entity;

        return Arb.From(gen);
    }

    /// <summary>
    /// Creates generator for entities with simple string PK + computed SK (≥2 sources).
    /// </summary>
    private static Arbitrary<EntityModel> CreateSimplePkComputedSkGenerator()
    {
        var gen = from entityName in GenSafeIdentifier()
                  from tableName in GenSafeIdentifier()
                  from skSourceCount in Gen.Choose(2, 5)
                  from hasPrefix in Gen.Elements(true, false)
                  let entity = BuildSimplePkComputedSkEntity(entityName, tableName, skSourceCount, hasPrefix)
                  where ComputedOverloadEligibility.QualifiesForTypedOverload(entity)
                  select entity;

        return Arb.From(gen);
    }

    /// <summary>
    /// Creates generator for entities with both PK and SK computed.
    /// </summary>
    private static Arbitrary<EntityModel> CreateBothKeysComputedGenerator()
    {
        var gen = from entityName in GenSafeIdentifier()
                  from tableName in GenSafeIdentifier()
                  from pkSourceCount in Gen.Choose(2, 4)
                  from skSourceCount in Gen.Choose(2, 4)
                  let entity = BuildBothComputedEntity(entityName, tableName, pkSourceCount, skSourceCount)
                  where ComputedOverloadEligibility.QualifiesForTypedOverload(entity)
                  select entity;

        return Arb.From(gen);
    }

    /// <summary>
    /// Creates generator for entities with computed PK (≥2 sources) and NO sort key.
    /// </summary>
    private static Arbitrary<EntityModel> CreateComputedPkNoSkGenerator()
    {
        var gen = from entityName in GenSafeIdentifier()
                  from tableName in GenSafeIdentifier()
                  from pkSourceCount in Gen.Choose(2, 5)
                  let entity = BuildComputedPkNoSkEntity(entityName, tableName, pkSourceCount)
                  where ComputedOverloadEligibility.QualifiesForTypedOverload(entity)
                  select entity;

        return Arb.From(gen);
    }

    /// <summary>
    /// Creates generator for entities with string key + prefix (no computed key).
    /// These qualify for KeyInputMode but should still have standard overloads.
    /// </summary>
    private static Arbitrary<EntityModel> CreateStringKeyWithPrefixGenerator()
    {
        var gen = from entityName in GenSafeIdentifier()
                  from tableName in GenSafeIdentifier()
                  from hasSk in Gen.Elements(true, false)
                  from prefix in GenNonEmptyPrefix()
                  let entity = BuildStringKeyWithPrefixEntity(entityName, tableName, prefix, hasSk)
                  where ComputedOverloadEligibility.QualifiesForKeyInputMode(entity)
                  select entity;

        return Arb.From(gen);
    }

    /// <summary>
    /// Creates a generator covering all entity configurations:
    /// computed PK, computed SK, both, with prefix, without prefix.
    /// All should have standard string overloads present.
    /// </summary>
    private static Arbitrary<EntityModel> CreateMixedEntityGenerator()
    {
        var gen = Gen.OneOf(
            // Computed PK + simple SK
            from entityName in GenSafeIdentifier()
            from tableName in GenSafeIdentifier()
            from pkSourceCount in Gen.Choose(2, 4)
            let entity = BuildComputedPkSimpleSkEntity(entityName, tableName, pkSourceCount, false)
            where ComputedOverloadEligibility.QualifiesForTypedOverload(entity)
            select entity,

            // Simple PK + computed SK
            from entityName in GenSafeIdentifier()
            from tableName in GenSafeIdentifier()
            from skSourceCount in Gen.Choose(2, 4)
            let entity = BuildSimplePkComputedSkEntity(entityName, tableName, skSourceCount, false)
            where ComputedOverloadEligibility.QualifiesForTypedOverload(entity)
            select entity,

            // String key with prefix (KeyInputMode eligible)
            from entityName in GenSafeIdentifier()
            from tableName in GenSafeIdentifier()
            from prefix in GenNonEmptyPrefix()
            let entity = BuildStringKeyWithPrefixEntity(entityName, tableName, prefix, true)
            select entity,

            // Computed PK no SK
            from entityName in GenSafeIdentifier()
            from tableName in GenSafeIdentifier()
            from pkSourceCount in Gen.Choose(2, 4)
            let entity = BuildComputedPkNoSkEntity(entityName, tableName, pkSourceCount)
            where ComputedOverloadEligibility.QualifiesForTypedOverload(entity)
            select entity
        );

        return Arb.From(gen);
    }

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

    private static readonly string[] SupportedTypes = { "int", "string", "long", "DateTime", "Guid", "decimal", "DateOnly" };

    private static EntityModel BuildComputedPkSimpleSkEntity(string entityName, string tableName, int pkSourceCount, bool hasPrefix)
    {
        var properties = new List<PropertyModel>();
        var pkSourceProps = new List<string>();

        // PK source properties — first is always int to avoid ambiguity
        for (int i = 0; i < pkSourceCount; i++)
        {
            var propName = $"PkPart{i + 1}";
            var propType = i == 0 ? "int" : PickType(i);
            properties.Add(new PropertyModel
            {
                PropertyName = propName,
                PropertyType = propType,
                AttributeName = propName.ToLowerInvariant()
            });
            pkSourceProps.Add(propName);
        }

        // Computed PK
        properties.Add(new PropertyModel
        {
            PropertyName = "Pk",
            PropertyType = "string",
            AttributeName = "pk",
            IsPartitionKey = true,
            KeyFormat = hasPrefix ? new KeyFormatModel { Prefix = "PREFIX" } : null,
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

        return CreateEntity(entityName, tableName, properties.ToArray());
    }

    private static EntityModel BuildSimplePkComputedSkEntity(string entityName, string tableName, int skSourceCount, bool hasPrefix)
    {
        var properties = new List<PropertyModel>();
        var skSourceProps = new List<string>();

        // Simple string PK
        properties.Add(new PropertyModel
        {
            PropertyName = "Pk",
            PropertyType = "string",
            AttributeName = "pk",
            IsPartitionKey = true,
            KeyFormat = hasPrefix ? new KeyFormatModel { Prefix = "PK_PREFIX" } : null
        });

        // SK source properties — first is always long to avoid ambiguity
        for (int i = 0; i < skSourceCount; i++)
        {
            var propName = $"SkPart{i + 1}";
            var propType = i == 0 ? "long" : PickType(i + 3);
            properties.Add(new PropertyModel
            {
                PropertyName = propName,
                PropertyType = propType,
                AttributeName = propName.ToLowerInvariant()
            });
            skSourceProps.Add(propName);
        }

        // Computed SK
        properties.Add(new PropertyModel
        {
            PropertyName = "Sk",
            PropertyType = "string",
            AttributeName = "sk",
            IsSortKey = true,
            ComputedKey = new ComputedKeyModel
            {
                SourceProperties = skSourceProps.ToArray(),
                Separator = "#"
            }
        });

        return CreateEntity(entityName, tableName, properties.ToArray());
    }

    private static EntityModel BuildBothComputedEntity(string entityName, string tableName,
        int pkSourceCount, int skSourceCount)
    {
        var properties = new List<PropertyModel>();
        var pkSourceProps = new List<string>();
        var skSourceProps = new List<string>();

        // PK source properties — first is always int
        for (int i = 0; i < pkSourceCount; i++)
        {
            var propName = $"PkPart{i + 1}";
            var propType = i == 0 ? "int" : PickType(i);
            properties.Add(new PropertyModel
            {
                PropertyName = propName,
                PropertyType = propType,
                AttributeName = propName.ToLowerInvariant()
            });
            pkSourceProps.Add(propName);
        }

        // Computed PK
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

        // SK source properties — first is always long, unique names
        for (int i = 0; i < skSourceCount; i++)
        {
            var propName = $"SkPart{i + 1}";
            var propType = i == 0 ? "long" : PickType(i + pkSourceCount + 2);
            properties.Add(new PropertyModel
            {
                PropertyName = propName,
                PropertyType = propType,
                AttributeName = propName.ToLowerInvariant()
            });
            skSourceProps.Add(propName);
        }

        // Computed SK
        properties.Add(new PropertyModel
        {
            PropertyName = "Sk",
            PropertyType = "string",
            AttributeName = "sk",
            IsSortKey = true,
            ComputedKey = new ComputedKeyModel
            {
                SourceProperties = skSourceProps.ToArray(),
                Separator = "#"
            }
        });

        return CreateEntity(entityName, tableName, properties.ToArray());
    }

    private static EntityModel BuildComputedPkNoSkEntity(string entityName, string tableName, int pkSourceCount)
    {
        var properties = new List<PropertyModel>();
        var pkSourceProps = new List<string>();

        // PK source properties — first is always int to avoid ambiguity
        for (int i = 0; i < pkSourceCount; i++)
        {
            var propName = $"PkPart{i + 1}";
            var propType = i == 0 ? "int" : PickType(i);
            properties.Add(new PropertyModel
            {
                PropertyName = propName,
                PropertyType = propType,
                AttributeName = propName.ToLowerInvariant()
            });
            pkSourceProps.Add(propName);
        }

        // Computed PK (no SK)
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

        return CreateEntity(entityName, tableName, properties.ToArray());
    }

    private static EntityModel BuildStringKeyWithPrefixEntity(string entityName, string tableName, string prefix, bool hasSk)
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
                IsSortKey = true
            });
        }

        properties.Add(new PropertyModel
        {
            PropertyName = "Data",
            PropertyType = "string",
            AttributeName = "data"
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
            AccessorConfigs = new List<AccessorConfig>()
        };
    }

    private static string PickType(int index)
    {
        return SupportedTypes[index % SupportedTypes.Length];
    }

    #endregion
}
