using FsCheck;
using FsCheck.Xunit;
using Oproto.FluentDynamoDb.SourceGenerator.Generators;
using Oproto.FluentDynamoDb.SourceGenerator.Models;
using SourceGenAccessModifier = Oproto.FluentDynamoDb.SourceGenerator.Models.AccessModifier;

namespace Oproto.FluentDynamoDb.SourceGenerator.UnitTests.Generators;

/// <summary>
/// Property-based tests for typed overload generation correctness.
///
/// **Feature: computed-key-accessor-overloads, Property 1: Typed overload generation correctness**
/// **Validates: Requirements 1.1, 1.3, 1.6, 1.7**
/// </summary>
public class ComputedOverloadPropertyTests
{
    private static readonly string[] SupportedTypes = { "int", "string", "long", "DateTime", "Guid", "decimal", "DateOnly" };

    /// <summary>
    /// Property 1: For any EntityModel where at least one key has IsComputed == true
    /// and ComputedKey.SourceProperties.Length >= 2, and the typed overload is not ambiguous
    /// with the existing overload, the generated code SHALL contain a method with parameters
    /// matching each source property in declaration order (PK components first, SK components second),
    /// where computed-key source properties are typed parameters and non-computed key(s) are a single
    /// string parameter.
    /// </summary>
    [Property(MaxTest = 100)]
    public Property TypedOverload_GeneratesCorrectParameters_ForComputedPkWithSimpleSk()
    {
        var entityGen = CreateComputedPkSimpleSkGenerator();

        return Prop.ForAll(entityGen, entity =>
        {
            // Precondition: must qualify and not be ambiguous
            if (!ComputedOverloadEligibility.QualifiesForTypedOverload(entity))
                return true.Label("Does not qualify — skipping");

            if (ComputedOverloadEligibility.WouldBeAmbiguous(entity))
                return true.Label("Would be ambiguous — skipping");

            // Act: Resolve the typed overload parameters
            var typedParams = OverloadParameterResolver.GetTypedOverloadParameters(entity);
            if (typedParams == null)
                return true.Label("Unresolvable — skipping");

            // Also generate the full code
            var generatedCode = TableGenerator.GenerateTableClass(entity.TableName, new List<EntityModel> { entity });

            // Build expected parameter list: PK source props first, then string sK
            var expectedParams = BuildExpectedParams(entity);

            // Verify resolved parameters match expected
            if (!VerifyParamsMatch(typedParams, expectedParams))
                return false.Label($"Parameter mismatch. Expected: ({FormatParams(expectedParams)}), Got: ({FormatTypedParams(typedParams)})");

            // Verify generated code contains the Get method with correct signature
            var paramSignature = FormatParams(expectedParams);
            var hasGetOverload = generatedCode.Contains($"Get({paramSignature})");

            return hasGetOverload.Label($"Generated code missing Get({paramSignature})");
        });
    }

    /// <summary>
    /// Property 1 (continued): For computed SK only entities (simple PK + computed SK),
    /// the generated typed overload has PK string parameter first, then SK source property parameters.
    /// </summary>
    [Property(MaxTest = 100)]
    public Property TypedOverload_GeneratesCorrectParameters_ForSimplePkWithComputedSk()
    {
        var entityGen = CreateSimplePkComputedSkGenerator();

        return Prop.ForAll(entityGen, entity =>
        {
            if (!ComputedOverloadEligibility.QualifiesForTypedOverload(entity))
                return true.Label("Does not qualify — skipping");

            if (ComputedOverloadEligibility.WouldBeAmbiguous(entity))
                return true.Label("Would be ambiguous — skipping");

            var typedParams = OverloadParameterResolver.GetTypedOverloadParameters(entity);
            if (typedParams == null)
                return true.Label("Unresolvable — skipping");

            var generatedCode = TableGenerator.GenerateTableClass(entity.TableName, new List<EntityModel> { entity });
            var expectedParams = BuildExpectedParams(entity);

            if (!VerifyParamsMatch(typedParams, expectedParams))
                return false.Label($"Parameter mismatch. Expected: ({FormatParams(expectedParams)}), Got: ({FormatTypedParams(typedParams)})");

            var paramSignature = FormatParams(expectedParams);
            var hasGetOverload = generatedCode.Contains($"Get({paramSignature})");

            return hasGetOverload.Label($"Generated code missing Get({paramSignature})");
        });
    }

    /// <summary>
    /// Property 1 (continued): For both-computed entities, the generated typed overload
    /// has all PK source property parameters first, then all SK source property parameters.
    /// </summary>
    [Property(MaxTest = 100)]
    public Property TypedOverload_GeneratesCorrectParameters_ForBothKeysComputed()
    {
        var entityGen = CreateBothKeysComputedGenerator();

        return Prop.ForAll(entityGen, entity =>
        {
            if (!ComputedOverloadEligibility.QualifiesForTypedOverload(entity))
                return true.Label("Does not qualify — skipping");

            if (ComputedOverloadEligibility.WouldBeAmbiguous(entity))
                return true.Label("Would be ambiguous — skipping");

            var typedParams = OverloadParameterResolver.GetTypedOverloadParameters(entity);
            if (typedParams == null)
                return true.Label("Unresolvable — skipping");

            var generatedCode = TableGenerator.GenerateTableClass(entity.TableName, new List<EntityModel> { entity });
            var expectedParams = BuildExpectedParams(entity);

            if (!VerifyParamsMatch(typedParams, expectedParams))
                return false.Label($"Parameter mismatch. Expected: ({FormatParams(expectedParams)}), Got: ({FormatTypedParams(typedParams)})");

            var paramSignature = FormatParams(expectedParams);
            var hasGetOverload = generatedCode.Contains($"Get({paramSignature})");

            return hasGetOverload.Label($"Generated code missing Get({paramSignature})");
        });
    }

    /// <summary>
    /// Property 1 (continued): For computed PK only (no SK) entities,
    /// the generated typed overload has only the PK source property parameters.
    /// </summary>
    [Property(MaxTest = 100)]
    public Property TypedOverload_GeneratesCorrectParameters_ForComputedPkNoSk()
    {
        var entityGen = CreateComputedPkNoSkGenerator();

        return Prop.ForAll(entityGen, entity =>
        {
            if (!ComputedOverloadEligibility.QualifiesForTypedOverload(entity))
                return true.Label("Does not qualify — skipping");

            if (ComputedOverloadEligibility.WouldBeAmbiguous(entity))
                return true.Label("Would be ambiguous — skipping");

            var typedParams = OverloadParameterResolver.GetTypedOverloadParameters(entity);
            if (typedParams == null)
                return true.Label("Unresolvable — skipping");

            var generatedCode = TableGenerator.GenerateTableClass(entity.TableName, new List<EntityModel> { entity });
            var expectedParams = BuildExpectedParams(entity);

            if (!VerifyParamsMatch(typedParams, expectedParams))
                return false.Label($"Parameter mismatch. Expected: ({FormatParams(expectedParams)}), Got: ({FormatTypedParams(typedParams)})");

            var paramSignature = FormatParams(expectedParams);
            var hasGetOverload = generatedCode.Contains($"Get({paramSignature})");

            return hasGetOverload.Label($"Generated code missing Get({paramSignature})");
        });
    }

    #region Verification Helpers

    private static List<(string Type, string Name)> BuildExpectedParams(EntityModel entity)
    {
        var expectedParams = new List<(string Type, string Name)>();
        var pk = entity.PartitionKeyProperty;
        var sk = entity.SortKeyProperty;

        bool pkComputed = pk?.IsComputed == true && pk.ComputedKey!.SourceProperties.Length >= 2;
        bool skComputed = sk?.IsComputed == true && sk.ComputedKey!.SourceProperties.Length >= 2;

        if (pkComputed)
        {
            // PK source properties in declaration order
            foreach (var sourcePropName in pk!.ComputedKey!.SourceProperties)
            {
                var prop = entity.Properties.First(p => p.PropertyName == sourcePropName);
                expectedParams.Add((
                    prop.PropertyType + (prop.IsNullable ? "?" : ""),
                    OverloadParameterResolver.ToCamelCase(prop.PropertyName)));
            }
        }
        else if (pk != null)
        {
            // Non-computed PK → single string parameter "pK"
            expectedParams.Add(("string", "pK"));
        }

        if (skComputed)
        {
            // SK source properties in declaration order
            foreach (var sourcePropName in sk!.ComputedKey!.SourceProperties)
            {
                var prop = entity.Properties.First(p => p.PropertyName == sourcePropName);
                expectedParams.Add((
                    prop.PropertyType + (prop.IsNullable ? "?" : ""),
                    OverloadParameterResolver.ToCamelCase(prop.PropertyName)));
            }
        }
        else if (sk != null)
        {
            // Non-computed SK → single string parameter "sK"
            expectedParams.Add(("string", "sK"));
        }

        return expectedParams;
    }

    private static bool VerifyParamsMatch(
        List<OverloadParameterResolver.ParameterInfo> actual,
        List<(string Type, string Name)> expected)
    {
        if (actual.Count != expected.Count)
            return false;

        for (int i = 0; i < actual.Count; i++)
        {
            var actualFullType = actual[i].Type + (actual[i].IsNullable ? "?" : "");
            if (actualFullType != expected[i].Type || actual[i].Name != expected[i].Name)
                return false;
        }

        return true;
    }

    private static string FormatParams(List<(string Type, string Name)> parameters)
    {
        return string.Join(", ", parameters.Select(p => $"{p.Type} {p.Name}"));
    }

    private static string FormatTypedParams(List<OverloadParameterResolver.ParameterInfo> parameters)
    {
        return string.Join(", ", parameters.Select(p => $"{p.Type}{(p.IsNullable ? "?" : "")} {p.Name}"));
    }

    #endregion

    #region Generators

    /// <summary>
    /// Creates generator for entities with computed PK (≥2 sources) + simple string SK.
    /// Ensures at least one non-string source property to avoid ambiguity.
    /// </summary>
    private static Arbitrary<EntityModel> CreateComputedPkSimpleSkGenerator()
    {
        var gen = from entityName in GenSafeIdentifier()
                  from tableName in GenSafeIdentifier()
                  from pkSourceCount in Gen.Choose(2, 5)
                  let entity = BuildComputedPkSimpleSkEntity(entityName, tableName, pkSourceCount)
                  where ComputedOverloadEligibility.QualifiesForTypedOverload(entity)
                      && !ComputedOverloadEligibility.WouldBeAmbiguous(entity)
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
                  let entity = BuildSimplePkComputedSkEntity(entityName, tableName, skSourceCount)
                  where ComputedOverloadEligibility.QualifiesForTypedOverload(entity)
                      && !ComputedOverloadEligibility.WouldBeAmbiguous(entity)
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
                      && !ComputedOverloadEligibility.WouldBeAmbiguous(entity)
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
                      && !ComputedOverloadEligibility.WouldBeAmbiguous(entity)
                  select entity;

        return Arb.From(gen);
    }

    private static Gen<string> GenSafeIdentifier()
    {
        return Gen.Elements("Alpha", "Beta", "Gamma", "Delta", "Epsilon", "Zeta", "Eta", "Theta",
                "Order", "Event", "Invoice", "Customer", "Product", "Session", "Record", "Entry");
    }

    #endregion

    #region Entity Builders

    private static EntityModel BuildComputedPkSimpleSkEntity(string entityName, string tableName, int pkSourceCount)
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

    private static EntityModel BuildSimplePkComputedSkEntity(string entityName, string tableName, int skSourceCount)
    {
        var properties = new List<PropertyModel>();
        var skSourceProps = new List<string>();

        // Simple string PK
        properties.Add(new PropertyModel
        {
            PropertyName = "Pk",
            PropertyType = "string",
            AttributeName = "pk",
            IsPartitionKey = true
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
