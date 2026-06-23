using FsCheck;
using FsCheck.Xunit;
using Oproto.FluentDynamoDb.SourceGenerator.Generators;
using Oproto.FluentDynamoDb.SourceGenerator.Models;
using SourceGenAccessModifier = Oproto.FluentDynamoDb.SourceGenerator.Models.AccessModifier;

namespace Oproto.FluentDynamoDb.SourceGenerator.UnitTests.Generators;

/// <summary>
/// Property-based tests for delegation to Keys.Build methods with Raw bypass.
///
/// **Feature: computed-key-accessor-overloads, Property 5: Delegation to Keys.Build methods with Raw bypass**
/// **Validates: Requirements 3.1, 3.2, 3.3, 3.4, 5.1, 5.2**
/// </summary>
public class DelegationToKeysBuildPropertyTests
{
    private static readonly string[] NonStringTypes = { "int", "long", "DateTime", "Guid", "decimal", "DateOnly" };

    /// <summary>
    /// Property 5: For any entity with a computed PK and simple SK, the generated overload
    /// SHALL call Entity.Keys.Build{PropertyName}(...) for computed partition keys with parameters
    /// in declaration order, and the composed key value SHALL be passed to the standard overload
    /// without any further prefix transformation (equivalent to KeyInputMode.Raw behavior).
    /// </summary>
    [Property(MaxTest = 100)]
    public Property TypedOverload_DelegatesToKeysBuildPk_ForComputedPkWithSimpleSk()
    {
        var entityGen = CreateComputedPkSimpleSkGenerator();

        return Prop.ForAll(entityGen, entity =>
        {
            var generatedCode = TableGenerator.GenerateTableClass(entity.TableName, new List<EntityModel> { entity });

            var pk = entity.PartitionKeyProperty!;
            var pkSourceParams = OverloadParameterResolver.ResolveParameters(entity, pk)!;
            var pkArgs = string.Join(", ", pkSourceParams.Select(p => p.Name));

            // Verify: generated code contains call to Entity.Keys.Build{PropertyName}(...)
            var expectedBuildCall = $"{entity.ClassName}.Keys.Build{pk.PropertyName}({pkArgs})";
            var hasBuildPkCall = generatedCode.Contains(expectedBuildCall);

            // Verify: the composed key is passed to the standard overload (delegation pattern)
            // The code should contain "return Get(computedPk" (with optional second arg)
            var hasDelegation = generatedCode.Contains("return Get(computedPk")
                || generatedCode.Contains("return Get(computedPk,");

            // Verify: no KeyInputMode parameter on standard overload when typed overload exists
            // (per Requirement 4 AC 2 / 5.1 — typed overloads bypass prefix logic)
            var hasNoKeyInputModeOnStandard = !generatedCode.Contains("KeyInputMode mode = KeyInputMode.Default");

            return (hasBuildPkCall && hasDelegation && hasNoKeyInputModeOnStandard)
                .Label($"BuildPk call: {hasBuildPkCall}, Delegation: {hasDelegation}, No KeyInputMode: {hasNoKeyInputModeOnStandard}. " +
                       $"Expected: {expectedBuildCall}");
        });
    }

    /// <summary>
    /// Property 5 (continued): For any entity with a simple PK and computed SK, the generated
    /// overload SHALL call Entity.Keys.Build{PropertyName}(...) for computed sort keys with
    /// parameters in declaration order.
    /// </summary>
    [Property(MaxTest = 100)]
    public Property TypedOverload_DelegatesToKeysBuildSk_ForSimplePkWithComputedSk()
    {
        var entityGen = CreateSimplePkComputedSkGenerator();

        return Prop.ForAll(entityGen, entity =>
        {
            var generatedCode = TableGenerator.GenerateTableClass(entity.TableName, new List<EntityModel> { entity });

            var sk = entity.SortKeyProperty!;
            var skSourceParams = OverloadParameterResolver.ResolveParameters(entity, sk)!;
            var skArgs = string.Join(", ", skSourceParams.Select(p => p.Name));

            // Verify: generated code contains call to Entity.Keys.Build{PropertyName}(...)
            var expectedBuildCall = $"{entity.ClassName}.Keys.Build{sk.PropertyName}({skArgs})";
            var hasBuildSkCall = generatedCode.Contains(expectedBuildCall);

            // Verify: delegation to standard overload using computed SK
            var hasDelegation = generatedCode.Contains("return Get(pK, computedSk)");

            // Verify: no KeyInputMode parameter on standard overload
            var hasNoKeyInputModeOnStandard = !generatedCode.Contains("KeyInputMode mode = KeyInputMode.Default");

            return (hasBuildSkCall && hasDelegation && hasNoKeyInputModeOnStandard)
                .Label($"BuildSk call: {hasBuildSkCall}, Delegation: {hasDelegation}, No KeyInputMode: {hasNoKeyInputModeOnStandard}. " +
                       $"Expected: {expectedBuildCall}");
        });
    }

    /// <summary>
    /// Property 5 (continued): For any entity with both computed PK and computed SK, the generated
    /// overload SHALL call Entity.Keys.Build{PropertyName}(...) for BOTH keys independently
    /// and pass both returned strings to the standard two-key accessor overload.
    /// </summary>
    [Property(MaxTest = 100)]
    public Property TypedOverload_DelegatesToBothKeysBuild_ForBothKeysComputed()
    {
        var entityGen = CreateBothKeysComputedGenerator();

        return Prop.ForAll(entityGen, entity =>
        {
            var generatedCode = TableGenerator.GenerateTableClass(entity.TableName, new List<EntityModel> { entity });

            var pk = entity.PartitionKeyProperty!;
            var sk = entity.SortKeyProperty!;

            var pkSourceParams = OverloadParameterResolver.ResolveParameters(entity, pk)!;
            var pkArgs = string.Join(", ", pkSourceParams.Select(p => p.Name));
            var expectedBuildPk = $"{entity.ClassName}.Keys.Build{pk.PropertyName}({pkArgs})";

            var skSourceParams = OverloadParameterResolver.ResolveParameters(entity, sk)!;
            var skArgs = string.Join(", ", skSourceParams.Select(p => p.Name));
            var expectedBuildSk = $"{entity.ClassName}.Keys.Build{sk.PropertyName}({skArgs})";

            // Verify: generated code contains calls to both Build methods
            var hasBuildPkCall = generatedCode.Contains(expectedBuildPk);
            var hasBuildSkCall = generatedCode.Contains(expectedBuildSk);

            // Verify: delegation to standard overload using both computed values
            var hasDelegation = generatedCode.Contains("return Get(computedPk, computedSk)");

            // Verify: no KeyInputMode on standard overload
            var hasNoKeyInputModeOnStandard = !generatedCode.Contains("KeyInputMode mode = KeyInputMode.Default");

            return (hasBuildPkCall && hasBuildSkCall && hasDelegation && hasNoKeyInputModeOnStandard)
                .Label($"BuildPk: {hasBuildPkCall}, BuildSk: {hasBuildSkCall}, Delegation: {hasDelegation}, " +
                       $"No KeyInputMode: {hasNoKeyInputModeOnStandard}. " +
                       $"Expected PK: {expectedBuildPk}, Expected SK: {expectedBuildSk}");
        });
    }

    /// <summary>
    /// Property 5 (continued): For any entity with a computed PK and no SK, the generated
    /// overload SHALL call Entity.Keys.Build{PropertyName}(...) and delegate to the standard
    /// overload with the composed key value passed directly (Raw bypass).
    /// </summary>
    [Property(MaxTest = 100)]
    public Property TypedOverload_DelegatesToKeysBuildPk_ForComputedPkNoSk()
    {
        var entityGen = CreateComputedPkNoSkGenerator();

        return Prop.ForAll(entityGen, entity =>
        {
            var generatedCode = TableGenerator.GenerateTableClass(entity.TableName, new List<EntityModel> { entity });

            var pk = entity.PartitionKeyProperty!;
            var pkSourceParams = OverloadParameterResolver.ResolveParameters(entity, pk)!;
            var pkArgs = string.Join(", ", pkSourceParams.Select(p => p.Name));

            // Verify: generated code contains call to Entity.Keys.Build{PropertyName}(...)
            var expectedBuildCall = $"{entity.ClassName}.Keys.Build{pk.PropertyName}({pkArgs})";
            var hasBuildPkCall = generatedCode.Contains(expectedBuildCall);

            // Verify: delegation — "return Get(computedPk)"
            var hasDelegation = generatedCode.Contains("return Get(computedPk)");

            return (hasBuildPkCall && hasDelegation)
                .Label($"BuildPk call: {hasBuildPkCall}, Delegation: {hasDelegation}. " +
                       $"Expected: {expectedBuildCall}");
        });
    }

    /// <summary>
    /// Property 5 (continued): Parameters SHALL be passed in declaration order to the Build method.
    /// Verifies that the parameter order matches the SourceProperties declaration order.
    /// </summary>
    [Property(MaxTest = 100)]
    public Property TypedOverload_PassesParametersInDeclarationOrder()
    {
        var entityGen = CreateComputedPkSimpleSkGenerator();

        return Prop.ForAll(entityGen, entity =>
        {
            var generatedCode = TableGenerator.GenerateTableClass(entity.TableName, new List<EntityModel> { entity });

            var pk = entity.PartitionKeyProperty!;
            var sourceProps = pk.ComputedKey!.SourceProperties;

            // Build expected arg order from source properties (camelCase of each source property name)
            var expectedArgOrder = sourceProps
                .Select(sp => OverloadParameterResolver.ToCamelCase(sp))
                .ToList();

            // The generated Build call should have args in this exact order
            var expectedBuildCall = $"{entity.ClassName}.Keys.Build{pk.PropertyName}({string.Join(", ", expectedArgOrder)})";
            var hasCorrectOrder = generatedCode.Contains(expectedBuildCall);

            return hasCorrectOrder
                .Label($"Declaration order mismatch. Expected: {expectedBuildCall}");
        });
    }

    /// <summary>
    /// Property 5 (continued): Verifies that the delegation pattern is consistent across
    /// Get, Delete, Update, and ConditionCheck methods — all use the same Keys.Build call.
    /// </summary>
    [Property(MaxTest = 100)]
    public Property TypedOverload_DelegationConsistent_AcrossAllCrudMethods()
    {
        var entityGen = CreateComputedPkSimpleSkGenerator();

        return Prop.ForAll(entityGen, entity =>
        {
            var generatedCode = TableGenerator.GenerateTableClass(entity.TableName, new List<EntityModel> { entity });

            var pk = entity.PartitionKeyProperty!;
            var pkSourceParams = OverloadParameterResolver.ResolveParameters(entity, pk)!;
            var pkArgs = string.Join(", ", pkSourceParams.Select(p => p.Name));
            var expectedBuildCall = $"{entity.ClassName}.Keys.Build{pk.PropertyName}({pkArgs})";

            // Count occurrences of the Build call — should appear at least once per CRUD method
            // (Get, Delete, Update, ConditionCheck = 4 minimum for entity accessor level)
            var buildCallCount = CountOccurrences(generatedCode, expectedBuildCall);

            // Also check for delegation patterns in all four methods
            var hasGetDelegation = generatedCode.Contains("return Get(computedPk");
            var hasDeleteDelegation = generatedCode.Contains("return Delete(computedPk");
            var hasUpdateDelegation = generatedCode.Contains("return Update(computedPk");
            var hasConditionCheckDelegation = generatedCode.Contains("return ConditionCheck(computedPk");

            // Should have at least 4 Build calls (one per entity accessor CRUD method)
            // Could have more if table-level overloads also generate (they delegate differently)
            var hasSufficientBuildCalls = buildCallCount >= 4;

            return (hasSufficientBuildCalls && hasGetDelegation && hasDeleteDelegation
                    && hasUpdateDelegation && hasConditionCheckDelegation)
                .Label($"Build call count: {buildCallCount} (need >= 4), " +
                       $"Get: {hasGetDelegation}, Delete: {hasDeleteDelegation}, " +
                       $"Update: {hasUpdateDelegation}, ConditionCheck: {hasConditionCheckDelegation}");
        });
    }

    #region Helpers

    private static int CountOccurrences(string source, string pattern)
    {
        int count = 0;
        int index = 0;
        while ((index = source.IndexOf(pattern, index, StringComparison.Ordinal)) != -1)
        {
            count++;
            index += pattern.Length;
        }
        return count;
    }

    #endregion

    #region Generators

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
        return NonStringTypes[index % NonStringTypes.Length];
    }

    #endregion
}
