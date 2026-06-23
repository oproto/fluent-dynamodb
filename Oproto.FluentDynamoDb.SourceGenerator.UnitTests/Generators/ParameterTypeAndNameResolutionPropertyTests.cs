using AwesomeAssertions;
using FsCheck;
using FsCheck.Xunit;
using Oproto.FluentDynamoDb.SourceGenerator.Generators;
using Oproto.FluentDynamoDb.SourceGenerator.Models;
using Xunit;
using SourceGenAccessModifier = Oproto.FluentDynamoDb.SourceGenerator.Models.AccessModifier;

namespace Oproto.FluentDynamoDb.SourceGenerator.UnitTests.Generators;

/// <summary>
/// Property-based tests for parameter type and name resolution.
///
/// **Feature: computed-key-accessor-overloads, Property 4: Parameter type and name resolution**
/// **Validates: Requirements 2.1, 2.2, 2.4, 2.5**
/// </summary>
public class ParameterTypeAndNameResolutionPropertyTests
{
    private static readonly string[] SupportedTypes =
    {
        "int", "long", "decimal", "DateTime", "DateOnly", "Guid", "string"
    };

    /// <summary>
    /// Property 4: For any source property referenced by a computed key's SourceProperties array,
    /// the generated convenience overload parameter SHALL have a type matching the source property's
    /// PropertyType (including nullability) and a name that is the camelCase transformation of the
    /// source property's PropertyName (first character lowercased, remaining unchanged).
    /// </summary>
    [Property(MaxTest = 100)]
    public Property ResolvedParameters_MatchSourcePropertyType_AndCamelCaseName()
    {
        var entityGen = CreateVariousTypeEntityGenerator();

        return Prop.ForAll(entityGen, entity =>
        {
            var pk = entity.PartitionKeyProperty;

            // Resolve parameters for the computed PK
            var resolvedParams = OverloadParameterResolver.ResolveParameters(entity, pk!);
            if (resolvedParams == null)
                return false.Label("ResolveParameters returned null unexpectedly");

            // For each source property, verify type and name match
            for (int i = 0; i < pk!.ComputedKey!.SourceProperties.Length; i++)
            {
                var sourcePropName = pk.ComputedKey.SourceProperties[i];
                var sourceProp = entity.Properties.First(p => p.PropertyName == sourcePropName);
                var resolvedParam = resolvedParams[i];

                // Type must match
                if (resolvedParam.Type != sourceProp.PropertyType)
                    return false.Label(
                        $"Type mismatch for '{sourcePropName}': expected '{sourceProp.PropertyType}', got '{resolvedParam.Type}'");

                // Nullability must match
                if (resolvedParam.IsNullable != sourceProp.IsNullable)
                    return false.Label(
                        $"Nullability mismatch for '{sourcePropName}': expected IsNullable={sourceProp.IsNullable}, got {resolvedParam.IsNullable}");

                // Name must be camelCase of property name
                var expectedName = OverloadParameterResolver.ToCamelCase(sourceProp.PropertyName);
                if (resolvedParam.Name != expectedName)
                    return false.Label(
                        $"Name mismatch for '{sourcePropName}': expected '{expectedName}', got '{resolvedParam.Name}'");
            }

            return true.Label("All parameters match source property types and names");
        });
    }

    /// <summary>
    /// Property 4 (continued): Nullable source properties produce nullable parameters.
    /// </summary>
    [Property(MaxTest = 100)]
    public Property NullableSourceProperties_ProduceNullableParameters()
    {
        var entityGen = CreateNullablePropertyEntityGenerator();

        return Prop.ForAll(entityGen, entity =>
        {
            var pk = entity.PartitionKeyProperty;
            var resolvedParams = OverloadParameterResolver.ResolveParameters(entity, pk!);
            if (resolvedParams == null)
                return false.Label("ResolveParameters returned null unexpectedly");

            // Verify each nullable source property produces a nullable parameter
            for (int i = 0; i < pk!.ComputedKey!.SourceProperties.Length; i++)
            {
                var sourcePropName = pk.ComputedKey.SourceProperties[i];
                var sourceProp = entity.Properties.First(p => p.PropertyName == sourcePropName);
                var resolvedParam = resolvedParams[i];

                if (sourceProp.IsNullable && !resolvedParam.IsNullable)
                    return false.Label(
                        $"Source property '{sourcePropName}' is nullable but parameter is not nullable");

                if (!sourceProp.IsNullable && resolvedParam.IsNullable)
                    return false.Label(
                        $"Source property '{sourcePropName}' is not nullable but parameter is nullable");
            }

            return true.Label("Nullability correctly propagated");
        });
    }

    /// <summary>
    /// Property 4 (continued): GetTypedOverloadParameters produces correct combined parameter list
    /// with types and names matching source properties for all computed keys.
    /// </summary>
    [Property(MaxTest = 100)]
    public Property GetTypedOverloadParameters_ResolvesAllSourcePropertyTypes_Correctly()
    {
        var entityGen = CreateBothKeysComputedWithVariousTypesGenerator();

        return Prop.ForAll(entityGen, entity =>
        {
            var typedParams = OverloadParameterResolver.GetTypedOverloadParameters(entity);
            if (typedParams == null)
                return false.Label("GetTypedOverloadParameters returned null");

            var pk = entity.PartitionKeyProperty;
            var sk = entity.SortKeyProperty;
            int expectedIndex = 0;

            // Verify PK source properties
            if (pk?.IsComputed == true && pk.ComputedKey!.SourceProperties.Length >= 2)
            {
                foreach (var sourcePropName in pk.ComputedKey.SourceProperties)
                {
                    var sourceProp = entity.Properties.First(p => p.PropertyName == sourcePropName);
                    var param = typedParams[expectedIndex];

                    if (param.Type != sourceProp.PropertyType)
                        return false.Label($"PK param type mismatch at index {expectedIndex}: expected '{sourceProp.PropertyType}', got '{param.Type}'");

                    if (param.IsNullable != sourceProp.IsNullable)
                        return false.Label($"PK param nullability mismatch at index {expectedIndex}");

                    var expectedName = OverloadParameterResolver.ToCamelCase(sourceProp.PropertyName);
                    if (param.Name != expectedName)
                        return false.Label($"PK param name mismatch at index {expectedIndex}: expected '{expectedName}', got '{param.Name}'");

                    expectedIndex++;
                }
            }
            else if (pk != null)
            {
                // Non-computed PK should be "pK" string
                var param = typedParams[expectedIndex];
                if (param.Type != "string" || param.Name != "pK")
                    return false.Label($"Non-computed PK should be 'string pK', got '{param.Type} {param.Name}'");
                expectedIndex++;
            }

            // Verify SK source properties
            if (sk?.IsComputed == true && sk.ComputedKey!.SourceProperties.Length >= 2)
            {
                foreach (var sourcePropName in sk.ComputedKey.SourceProperties)
                {
                    var sourceProp = entity.Properties.First(p => p.PropertyName == sourcePropName);
                    var param = typedParams[expectedIndex];

                    if (param.Type != sourceProp.PropertyType)
                        return false.Label($"SK param type mismatch at index {expectedIndex}: expected '{sourceProp.PropertyType}', got '{param.Type}'");

                    if (param.IsNullable != sourceProp.IsNullable)
                        return false.Label($"SK param nullability mismatch at index {expectedIndex}");

                    var expectedName = OverloadParameterResolver.ToCamelCase(sourceProp.PropertyName);
                    if (param.Name != expectedName)
                        return false.Label($"SK param name mismatch at index {expectedIndex}: expected '{expectedName}', got '{param.Name}'");

                    expectedIndex++;
                }
            }
            else if (sk != null)
            {
                // Non-computed SK should be "sK" string
                var param = typedParams[expectedIndex];
                if (param.Type != "string" || param.Name != "sK")
                    return false.Label($"Non-computed SK should be 'string sK', got '{param.Type} {param.Name}'");
                expectedIndex++;
            }

            if (expectedIndex != typedParams.Count)
                return false.Label($"Parameter count mismatch: expected {expectedIndex}, got {typedParams.Count}");

            return true.Label("All typed overload parameters match source property types and names");
        });
    }

    /// <summary>
    /// ToCamelCase: Verifies camelCase transformation preserves all characters except lowercasing the first.
    /// Specific cases: "Year" → "year", "OrderId" → "orderId", "XValue" → "xValue", "a" → "a"
    /// </summary>
    [Property(MaxTest = 100)]
    public Property ToCamelCase_LowercasesFirstCharacter_PreservesRest()
    {
        var nameGen = GenPropertyName();

        return Prop.ForAll(nameGen, propertyName =>
        {
            var result = OverloadParameterResolver.ToCamelCase(propertyName);

            if (string.IsNullOrEmpty(propertyName))
                return (result == propertyName).Label("Empty/null should pass through unchanged");

            // First character should be lowercased
            var expectedFirst = char.ToLowerInvariant(propertyName[0]);
            if (result[0] != expectedFirst)
                return false.Label($"First char mismatch: input '{propertyName}', expected first char '{expectedFirst}', got '{result[0]}'");

            // Rest should be unchanged
            if (propertyName.Length > 1)
            {
                var expectedRest = propertyName.Substring(1);
                var actualRest = result.Substring(1);
                if (actualRest != expectedRest)
                    return false.Label($"Rest mismatch: input '{propertyName}', expected rest '{expectedRest}', got '{actualRest}'");
            }

            return true.Label($"'{propertyName}' → '{result}'");
        });
    }

    /// <summary>
    /// ToCamelCase: Explicit verification of specific cases from the spec.
    /// </summary>
    [Fact]
    public void ToCamelCase_SpecificCases_MatchExpected()
    {
        // "Year" → "year"
        OverloadParameterResolver.ToCamelCase("Year").Should().Be("year");

        // "OrderId" → "orderId"
        OverloadParameterResolver.ToCamelCase("OrderId").Should().Be("orderId");

        // "XValue" → "xValue"
        OverloadParameterResolver.ToCamelCase("XValue").Should().Be("xValue");

        // "a" → "a"
        OverloadParameterResolver.ToCamelCase("a").Should().Be("a");
    }

    #region Generators

    /// <summary>
    /// Creates generator for entities with computed PK using various property types
    /// including int, long, decimal, DateTime, DateOnly, Guid, and string.
    /// </summary>
    private static Arbitrary<EntityModel> CreateVariousTypeEntityGenerator()
    {
        var gen = from entityName in GenSafeIdentifier()
                  from tableName in GenSafeIdentifier()
                  from sourceCount in Gen.Choose(2, 5)
                  from types in Gen.ArrayOf(sourceCount, Gen.Elements(SupportedTypes))
                  from names in GenUniquePropertyNames(sourceCount)
                  let entity = BuildEntityWithTypedProperties(entityName, tableName, names, types, new bool[sourceCount])
                  where entity.PartitionKeyProperty?.IsComputed == true
                  select entity;

        return Arb.From(gen);
    }

    /// <summary>
    /// Creates generator for entities with nullable source properties.
    /// </summary>
    private static Arbitrary<EntityModel> CreateNullablePropertyEntityGenerator()
    {
        var gen = from entityName in GenSafeIdentifier()
                  from tableName in GenSafeIdentifier()
                  from sourceCount in Gen.Choose(2, 4)
                  from types in Gen.ArrayOf(sourceCount, Gen.Elements(SupportedTypes))
                  from nullabilities in Gen.ArrayOf(sourceCount, Gen.Elements(true, false))
                  from names in GenUniquePropertyNames(sourceCount)
                  // Ensure at least one non-string type to avoid ambiguity when combined with SK
                  where types.Any(t => t != "string")
                  let entity = BuildEntityWithTypedProperties(entityName, tableName, names, types, nullabilities)
                  where entity.PartitionKeyProperty?.IsComputed == true
                  select entity;

        return Arb.From(gen);
    }

    /// <summary>
    /// Creates generator for entities with both keys computed, using various types.
    /// </summary>
    private static Arbitrary<EntityModel> CreateBothKeysComputedWithVariousTypesGenerator()
    {
        var gen = from entityName in GenSafeIdentifier()
                  from tableName in GenSafeIdentifier()
                  from pkSourceCount in Gen.Choose(2, 3)
                  from skSourceCount in Gen.Choose(2, 3)
                  from pkTypes in Gen.ArrayOf(pkSourceCount, Gen.Elements(SupportedTypes))
                  from skTypes in Gen.ArrayOf(skSourceCount, Gen.Elements(SupportedTypes))
                  from pkNames in GenUniquePropertyNames(pkSourceCount, "Pk")
                  from skNames in GenUniquePropertyNames(skSourceCount, "Sk")
                  // Ensure at least one non-string type for non-ambiguity
                  where pkTypes.Concat(skTypes).Any(t => t != "string")
                  let entity = BuildBothComputedEntityWithTypes(entityName, tableName, pkNames, pkTypes, skNames, skTypes)
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

    private static Gen<string[]> GenUniquePropertyNames(int count, string prefix = "Prop")
    {
        // Generate unique property names using PascalCase names
        var allNames = new[]
        {
            "Year", "Month", "Day", "OrderId", "CustomerId", "TenantId",
            "Region", "Status", "Priority", "Amount", "Quantity", "Price",
            "XValue", "Category", "Timestamp", "Duration", "Score", "Level",
            "AccountId", "ProductId", "SessionId", "UserId", "GroupId", "ItemId"
        };

        return Gen.Shuffle(allNames).Select(shuffled =>
            shuffled.Take(count).Select(n => $"{prefix}{n}").ToArray());
    }

    private static Arbitrary<string> GenPropertyName()
    {
        var names = new[]
        {
            "Year", "OrderId", "XValue", "a", "CustomerId", "TenantId",
            "Region", "Status", "Id", "Amount", "Pk", "Sk",
            "FirstName", "LastName", "ABC", "MyProperty", "X", "Ab"
        };

        return Arb.From(Gen.Elements(names));
    }

    #endregion

    #region Entity Builders

    private static EntityModel BuildEntityWithTypedProperties(
        string entityName, string tableName,
        string[] propertyNames, string[] types, bool[] nullabilities)
    {
        var properties = new List<PropertyModel>();
        var pkSourceProps = new List<string>();

        // Build source properties with specific types and nullabilities
        for (int i = 0; i < propertyNames.Length; i++)
        {
            properties.Add(new PropertyModel
            {
                PropertyName = propertyNames[i],
                PropertyType = types[i],
                AttributeName = propertyNames[i].ToLowerInvariant(),
                IsNullable = nullabilities[i]
            });
            pkSourceProps.Add(propertyNames[i]);
        }

        // Computed PK referencing the source properties
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

    private static EntityModel BuildBothComputedEntityWithTypes(
        string entityName, string tableName,
        string[] pkNames, string[] pkTypes,
        string[] skNames, string[] skTypes)
    {
        var properties = new List<PropertyModel>();
        var pkSourceProps = new List<string>();
        var skSourceProps = new List<string>();

        // PK source properties
        for (int i = 0; i < pkNames.Length; i++)
        {
            properties.Add(new PropertyModel
            {
                PropertyName = pkNames[i],
                PropertyType = pkTypes[i],
                AttributeName = pkNames[i].ToLowerInvariant()
            });
            pkSourceProps.Add(pkNames[i]);
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

        // SK source properties
        for (int i = 0; i < skNames.Length; i++)
        {
            properties.Add(new PropertyModel
            {
                PropertyName = skNames[i],
                PropertyType = skTypes[i],
                AttributeName = skNames[i].ToLowerInvariant()
            });
            skSourceProps.Add(skNames[i]);
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
