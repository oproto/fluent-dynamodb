using FsCheck;
using FsCheck.Xunit;
using Oproto.FluentDynamoDb.SourceGenerator.Generators;
using Oproto.FluentDynamoDb.SourceGenerator.Models;
using System.Text.RegularExpressions;
using SourceGenAccessModifier = Oproto.FluentDynamoDb.SourceGenerator.Models.AccessModifier;

namespace Oproto.FluentDynamoDb.SourceGenerator.UnitTests.Generators;

/// <summary>
/// Property-based tests for CRUD method consistency of typed parameter convenience overloads.
/// 
/// **Feature: computed-key-accessor-overloads, Property 2: Consistency across CRUD methods**
/// **Validates: Requirements 1.4**
/// </summary>
public class ComputedOverloadCrudConsistencyPropertyTests
{
    private static readonly string[] SupportedTypes = { "int", "string", "long", "DateTime", "Guid", "decimal", "DateOnly" };

    /// <summary>
    /// Property: For any entity that qualifies for a typed parameter convenience overload,
    /// the generated Get, Delete, Update, and ConditionCheck methods SHALL each contain
    /// a typed overload with an identical parameter signature (same parameter names, types,
    /// and positional order).
    /// </summary>
    [Property(MaxTest = 100)]
    public Property TypedOverloads_HaveConsistentSignatures_AcrossAllCrudMethods()
    {
        var entityGen = CreateEligibleEntityGenerator();

        return Prop.ForAll(entityGen, entity =>
        {
            // Act: generate the full table class code
            var generatedCode = TableGenerator.GenerateTableClass(entity);

            // Extract typed overload parameter signatures for each CRUD method
            var getSignature = ExtractTypedOverloadParams(generatedCode, "GetItemRequestBuilder", entity.ClassName);
            var deleteSignature = ExtractTypedOverloadParams(generatedCode, "DeleteItemRequestBuilder", entity.ClassName);
            var updateSignature = ExtractTypedOverloadParams(generatedCode, "UpdateItemRequestBuilder", entity.ClassName);
            var conditionCheckSignature = ExtractTypedOverloadParams(generatedCode, "ConditionCheckBuilder", entity.ClassName);

            // All four must be present (typed overloads generated for all CRUD methods)
            var allPresent = getSignature != null && deleteSignature != null
                && updateSignature != null && conditionCheckSignature != null;

            if (!allPresent)
                return false.Label($"Missing typed overload(s): Get={getSignature != null}, Delete={deleteSignature != null}, Update={updateSignature != null}, ConditionCheck={conditionCheckSignature != null}");

            // All four signatures must be identical
            var consistent = getSignature == deleteSignature
                && getSignature == updateSignature
                && getSignature == conditionCheckSignature;

            return consistent.Label(
                $"Signatures should be identical:\n  Get:            {getSignature}\n  Delete:         {deleteSignature}\n  Update:         {updateSignature}\n  ConditionCheck: {conditionCheckSignature}");
        });
    }

    /// <summary>
    /// Property: For any entity with a computed PK (≥2 sources) and a simple SK,
    /// all CRUD typed overloads have the same parameter signature.
    /// </summary>
    [Property(MaxTest = 100)]
    public Property ComputedPkWithSimpleSk_AllCrudMethodsHaveIdenticalSignatures()
    {
        var entityGen = CreateComputedPkSimpleSkEntityGenerator();

        return Prop.ForAll(entityGen, entity =>
        {
            var generatedCode = TableGenerator.GenerateTableClass(entity);

            var getSignature = ExtractTypedOverloadParams(generatedCode, "GetItemRequestBuilder", entity.ClassName);
            var deleteSignature = ExtractTypedOverloadParams(generatedCode, "DeleteItemRequestBuilder", entity.ClassName);
            var updateSignature = ExtractTypedOverloadParams(generatedCode, "UpdateItemRequestBuilder", entity.ClassName);
            var conditionCheckSignature = ExtractTypedOverloadParams(generatedCode, "ConditionCheckBuilder", entity.ClassName);

            var allPresent = getSignature != null && deleteSignature != null
                && updateSignature != null && conditionCheckSignature != null;

            if (!allPresent)
                return false.Label($"Missing typed overload(s)");

            var consistent = getSignature == deleteSignature
                && getSignature == updateSignature
                && getSignature == conditionCheckSignature;

            return consistent.Label($"Signatures differ across CRUD methods");
        });
    }

    /// <summary>
    /// Property: For any entity with both PK and SK computed,
    /// all CRUD typed overloads have the same parameter signature.
    /// </summary>
    [Property(MaxTest = 100)]
    public Property BothKeysComputed_AllCrudMethodsHaveIdenticalSignatures()
    {
        var entityGen = CreateBothKeysComputedEntityGenerator();

        return Prop.ForAll(entityGen, entity =>
        {
            var generatedCode = TableGenerator.GenerateTableClass(entity);

            var getSignature = ExtractTypedOverloadParams(generatedCode, "GetItemRequestBuilder", entity.ClassName);
            var deleteSignature = ExtractTypedOverloadParams(generatedCode, "DeleteItemRequestBuilder", entity.ClassName);
            var updateSignature = ExtractTypedOverloadParams(generatedCode, "UpdateItemRequestBuilder", entity.ClassName);
            var conditionCheckSignature = ExtractTypedOverloadParams(generatedCode, "ConditionCheckBuilder", entity.ClassName);

            var allPresent = getSignature != null && deleteSignature != null
                && updateSignature != null && conditionCheckSignature != null;

            if (!allPresent)
                return false.Label($"Missing typed overload(s)");

            var consistent = getSignature == deleteSignature
                && getSignature == updateSignature
                && getSignature == conditionCheckSignature;

            return consistent.Label($"Signatures differ across CRUD methods");
        });
    }

    #region Helper Methods

    /// <summary>
    /// Extracts the parameter list from a typed overload method signature.
    /// Looks for patterns like: "GetItemRequestBuilder&lt;EntityName&gt; Get(int year, int month, string sK)"
    /// and returns the parameter list portion: "int year, int month, string sK"
    /// </summary>
    private static string? ExtractTypedOverloadParams(string generatedCode, string returnTypePrefix, string entityName)
    {
        // Determine the method name from the return type prefix
        var methodName = returnTypePrefix switch
        {
            "GetItemRequestBuilder" => "Get",
            "DeleteItemRequestBuilder" => "Delete",
            "UpdateItemRequestBuilder" => "Update",
            "ConditionCheckBuilder" => "ConditionCheck",
            _ => throw new ArgumentException($"Unknown return type prefix: {returnTypePrefix}")
        };

        // Pattern: {ReturnType}<{EntityName}> {MethodName}({params})
        // We need to find the TYPED overload (not the standard string overload)
        // The typed overload has at least one non-string parameter OR has more parameters than the standard overload
        var pattern = $@"{Regex.Escape(returnTypePrefix)}<{Regex.Escape(entityName)}>\s+{Regex.Escape(methodName)}\(([^)]+)\)";
        var matches = Regex.Matches(generatedCode, pattern);

        foreach (Match match in matches)
        {
            var paramList = match.Groups[1].Value.Trim();
            // Skip the standard overload (which only has string pK and/or string sK parameters)
            if (IsStandardOverload(paramList))
                continue;

            return NormalizeParamList(paramList);
        }

        return null;
    }

    /// <summary>
    /// Determines if a parameter list represents the standard string overload.
    /// Standard overloads have only "string pK" or "string pK, string sK" patterns.
    /// </summary>
    private static bool IsStandardOverload(string paramList)
    {
        var normalized = paramList.Replace(" ", "");
        return normalized == "stringpK" || normalized == "stringpK,stringsK";
    }

    /// <summary>
    /// Normalizes a parameter list for comparison (trims whitespace between tokens).
    /// </summary>
    private static string NormalizeParamList(string paramList)
    {
        // Normalize whitespace: collapse multiple spaces, trim around commas
        return Regex.Replace(paramList.Trim(), @"\s+", " ");
    }

    /// <summary>
    /// Creates an FsCheck generator for EntityModel instances that qualify for typed overloads.
    /// Generates entities with at least one computed key with ≥2 source properties,
    /// ensuring the overload is non-ambiguous (at least one non-string source property type).
    /// </summary>
    private static Arbitrary<EntityModel> CreateEligibleEntityGenerator()
    {
        var gen = from entityName in GenSafeIdentifier()
                  from tableName in GenSafeIdentifier()
                  from scenario in Gen.Choose(0, 2) // 0=PK computed, 1=SK computed, 2=both
                  from pkSourceCount in Gen.Choose(2, 4)
                  from skSourceCount in Gen.Choose(2, 4)
                  from includeNonStringType in Gen.Constant(true) // ensure non-ambiguous
                  let entity = BuildEligibleEntity(entityName, tableName, scenario, pkSourceCount, skSourceCount, includeNonStringType)
                  where ComputedOverloadEligibility.QualifiesForTypedOverload(entity)
                      && !ComputedOverloadEligibility.WouldBeAmbiguous(entity)
                  select entity;

        return Arb.From(gen);
    }

    /// <summary>
    /// Creates an FsCheck generator specifically for computed PK + simple SK scenarios.
    /// </summary>
    private static Arbitrary<EntityModel> CreateComputedPkSimpleSkEntityGenerator()
    {
        var gen = from entityName in GenSafeIdentifier()
                  from tableName in GenSafeIdentifier()
                  from pkSourceCount in Gen.Choose(2, 4)
                  let entity = BuildComputedPkSimpleSkEntity(entityName, tableName, pkSourceCount)
                  where ComputedOverloadEligibility.QualifiesForTypedOverload(entity)
                      && !ComputedOverloadEligibility.WouldBeAmbiguous(entity)
                  select entity;

        return Arb.From(gen);
    }

    /// <summary>
    /// Creates an FsCheck generator specifically for both-keys-computed scenarios.
    /// </summary>
    private static Arbitrary<EntityModel> CreateBothKeysComputedEntityGenerator()
    {
        var gen = from entityName in GenSafeIdentifier()
                  from tableName in GenSafeIdentifier()
                  from pkSourceCount in Gen.Choose(2, 3)
                  from skSourceCount in Gen.Choose(2, 3)
                  let entity = BuildBothKeysComputedEntity(entityName, tableName, pkSourceCount, skSourceCount)
                  where ComputedOverloadEligibility.QualifiesForTypedOverload(entity)
                      && !ComputedOverloadEligibility.WouldBeAmbiguous(entity)
                  select entity;

        return Arb.From(gen);
    }

    private static Gen<string> GenSafeIdentifier()
    {
        return Gen.Elements("Alpha", "Beta", "Gamma", "Delta", "Epsilon", "Zeta", "Eta", "Theta",
                "Iota", "Kappa", "Lambda", "Mu", "Nu", "Xi", "Omicron", "Pi", "Rho", "Sigma",
                "Tau", "Upsilon", "Phi", "Chi", "Psi", "Omega",
                "Order", "Event", "Invoice", "Customer", "Product", "Session", "Record", "Entry");
    }

    private static EntityModel BuildEligibleEntity(string entityName, string tableName, int scenario,
        int pkSourceCount, int skSourceCount, bool includeNonStringType)
    {
        var properties = new List<PropertyModel>();
        var pkSourceProps = new List<string>();
        var skSourceProps = new List<string>();

        // Build PK
        PropertyModel pk;
        if (scenario == 0 || scenario == 2) // PK computed
        {
            pk = new PropertyModel
            {
                PropertyName = "Pk",
                PropertyType = "string",
                AttributeName = "pk",
                IsPartitionKey = true,
                ComputedKey = new ComputedKeyModel { Separator = "#" }
            };

            for (int i = 0; i < pkSourceCount; i++)
            {
                var propName = $"PkSource{i + 1}";
                // Ensure at least one non-string type to avoid ambiguity
                var propType = (includeNonStringType && i == 0) ? "int" : PickType(i);
                properties.Add(new PropertyModel
                {
                    PropertyName = propName,
                    PropertyType = propType,
                    AttributeName = propName.ToLower()
                });
                pkSourceProps.Add(propName);
            }

            pk.ComputedKey!.SourceProperties = pkSourceProps.ToArray();
        }
        else
        {
            pk = new PropertyModel
            {
                PropertyName = "Pk",
                PropertyType = "string",
                AttributeName = "pk",
                IsPartitionKey = true
            };
        }
        properties.Insert(0, pk);

        // Build SK
        if (scenario == 1 || scenario == 2) // SK computed
        {
            var sk = new PropertyModel
            {
                PropertyName = "Sk",
                PropertyType = "string",
                AttributeName = "sk",
                IsSortKey = true,
                ComputedKey = new ComputedKeyModel { Separator = "#" }
            };

            for (int i = 0; i < skSourceCount; i++)
            {
                var propName = $"SkSource{i + 1}";
                var propType = (includeNonStringType && i == 0) ? "long" : PickType(i + pkSourceCount);
                properties.Add(new PropertyModel
                {
                    PropertyName = propName,
                    PropertyType = propType,
                    AttributeName = propName.ToLower()
                });
                skSourceProps.Add(propName);
            }

            sk.ComputedKey!.SourceProperties = skSourceProps.ToArray();
            properties.Insert(1, sk);
        }
        else
        {
            // Add a simple SK when PK is computed (scenario 0)
            var sk = new PropertyModel
            {
                PropertyName = "Sk",
                PropertyType = "string",
                AttributeName = "sk",
                IsSortKey = true
            };
            properties.Insert(1, sk);
        }

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

    private static EntityModel BuildComputedPkSimpleSkEntity(string entityName, string tableName, int pkSourceCount)
    {
        var properties = new List<PropertyModel>();
        var pkSourceProps = new List<string>();

        var pk = new PropertyModel
        {
            PropertyName = "Pk",
            PropertyType = "string",
            AttributeName = "pk",
            IsPartitionKey = true,
            ComputedKey = new ComputedKeyModel { Separator = "#" }
        };

        // First source is always int to avoid ambiguity
        for (int i = 0; i < pkSourceCount; i++)
        {
            var propName = $"Component{i + 1}";
            var propType = i == 0 ? "int" : PickType(i);
            properties.Add(new PropertyModel
            {
                PropertyName = propName,
                PropertyType = propType,
                AttributeName = propName.ToLower()
            });
            pkSourceProps.Add(propName);
        }

        pk.ComputedKey!.SourceProperties = pkSourceProps.ToArray();
        properties.Insert(0, pk);

        // Simple string SK
        properties.Insert(1, new PropertyModel
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

    private static EntityModel BuildBothKeysComputedEntity(string entityName, string tableName,
        int pkSourceCount, int skSourceCount)
    {
        var properties = new List<PropertyModel>();
        var pkSourceProps = new List<string>();
        var skSourceProps = new List<string>();

        var pk = new PropertyModel
        {
            PropertyName = "Pk",
            PropertyType = "string",
            AttributeName = "pk",
            IsPartitionKey = true,
            ComputedKey = new ComputedKeyModel { Separator = "#" }
        };

        // PK source properties - first is always int to avoid ambiguity
        for (int i = 0; i < pkSourceCount; i++)
        {
            var propName = $"PkPart{i + 1}";
            var propType = i == 0 ? "int" : PickType(i);
            properties.Add(new PropertyModel
            {
                PropertyName = propName,
                PropertyType = propType,
                AttributeName = propName.ToLower()
            });
            pkSourceProps.Add(propName);
        }
        pk.ComputedKey!.SourceProperties = pkSourceProps.ToArray();
        properties.Insert(0, pk);

        var sk = new PropertyModel
        {
            PropertyName = "Sk",
            PropertyType = "string",
            AttributeName = "sk",
            IsSortKey = true,
            ComputedKey = new ComputedKeyModel { Separator = "#" }
        };

        // SK source properties
        for (int i = 0; i < skSourceCount; i++)
        {
            var propName = $"SkPart{i + 1}";
            var propType = i == 0 ? "long" : PickType(i + pkSourceCount);
            properties.Add(new PropertyModel
            {
                PropertyName = propName,
                PropertyType = propType,
                AttributeName = propName.ToLower()
            });
            skSourceProps.Add(propName);
        }
        sk.ComputedKey!.SourceProperties = skSourceProps.ToArray();
        properties.Insert(1, sk);

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

    private static string PickType(int index)
    {
        return SupportedTypes[index % SupportedTypes.Length];
    }

    #endregion
}
