using FsCheck;
using FsCheck.Xunit;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Oproto.FluentDynamoDb.SourceGenerator.UnitTests.TestHelpers;
using System.Collections.Immutable;
using System.Text.RegularExpressions;

namespace Oproto.FluentDynamoDb.SourceGenerator.UnitTests.Generators;

/// <summary>
/// Property-based tests for source generator update model property filtering.
/// Tests verify that the UpdateExpressionsGenerator correctly excludes/includes properties
/// based on key, extracted, and computed field classifications.
///
/// Feature: update-model-computed-field-redesign
/// </summary>
[Trait("Category", "Property")]
[Trait("Generator", "UpdateExpressions")]
public class UpdateModelFilteringPropertyTests
{
    // ──────────────────────────────────────────────────────────────────────
    // Property 1: Key Properties Excluded from Update Model
    // ──────────────────────────────────────────────────────────────────────

    /// <summary>
    /// **Feature: update-model-computed-field-redesign, Property 1: Key Properties Excluded from Update Model**
    ///
    /// For any entity model with a partition key and/or sort key property, the generated update model
    /// class SHALL NOT contain properties matching the key property names, AND SHALL contain all
    /// non-key properties that have HasAttributeMapping.
    ///
    /// **Validates: Requirements 1.1, 1.2, 1.3, 1.4**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property KeyProperties_ExcludedFromUpdateModel()
    {
        return Prop.ForAll(
            GenEntityWithKeys().ToArbitrary(),
            config =>
            {
                var source = BuildEntitySource(config);
                var (updateModelCode, _) = RunGenerator(source, "UpdateModel");
                if (updateModelCode == null) return false;

                // Key properties must NOT appear in the update model
                foreach (var keyProp in config.Properties.Where(p => p.IsPartitionKey || p.IsSortKey))
                {
                    if (updateModelCode.Contains($"{keyProp.Name} {{ get; set; }}"))
                        return false;
                }

                // Non-key properties with attribute mapping MUST appear
                foreach (var nonKeyProp in config.Properties.Where(p => !p.IsPartitionKey && !p.IsSortKey && !p.IsExtracted && !p.IsComputed))
                {
                    if (!updateModelCode.Contains($"{nonKeyProp.Name} {{ get; set; }}"))
                        return false;
                }

                return true;
            });
    }

    // ──────────────────────────────────────────────────────────────────────
    // Property 2: Extracted Properties of Keys Excluded from Update Model
    // ──────────────────────────────────────────────────────────────────────

    /// <summary>
    /// **Feature: update-model-computed-field-redesign, Property 2: Extracted Properties of Keys Excluded from Update Model**
    ///
    /// For any entity model with extracted properties whose SourceProperty references a partition key
    /// or sort key property, the generated update model class SHALL NOT contain those extracted properties.
    ///
    /// **Validates: Requirements 2.1, 2.2, 2.3**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property ExtractedPropertiesOfKeys_ExcludedFromUpdateModel()
    {
        return Prop.ForAll(
            GenEntityWithExtractedFromKey().ToArbitrary(),
            config =>
            {
                var source = BuildEntitySource(config);
                var (updateModelCode, _) = RunGenerator(source, "UpdateModel");
                if (updateModelCode == null) return false;

                // Extracted properties whose source is a key must NOT appear
                foreach (var extractedProp in config.Properties.Where(p => p.IsExtracted && p.ExtractedSourceIsKey))
                {
                    if (updateModelCode.Contains($"{extractedProp.Name} {{ get; set; }}"))
                        return false;
                }

                return true;
            });
    }

    // ──────────────────────────────────────────────────────────────────────
    // Property 3: Non-Key Computed Field Inclusion
    // ──────────────────────────────────────────────────────────────────────

    /// <summary>
    /// **Feature: update-model-computed-field-redesign, Property 3: Non-Key Computed Field Inclusion**
    ///
    /// For any entity model with a computed field that is NOT a partition key or sort key, the generated
    /// update model class SHALL contain a nullable property for the computed field itself and for each
    /// of its source properties.
    ///
    /// **Validates: Requirements 3.1, 3.2, 3.3**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property NonKeyComputedField_IncludedInUpdateModel()
    {
        return Prop.ForAll(
            GenEntityWithNonKeyComputed().ToArbitrary(),
            config =>
            {
                var source = BuildEntitySource(config);
                var (updateModelCode, _) = RunGenerator(source, "UpdateModel");
                if (updateModelCode == null) return false;

                // The non-key computed field itself must appear
                var computedProp = config.Properties.First(p => p.IsComputed && !p.IsPartitionKey && !p.IsSortKey);
                if (!updateModelCode.Contains($"{computedProp.Name} {{ get; set; }}"))
                    return false;

                // Each source property of the non-key computed field must appear
                foreach (var sourceName in computedProp.ComputedSourceProperties!)
                {
                    var sourceProp = config.Properties.FirstOrDefault(p => p.Name == sourceName);
                    if (sourceProp != null && !updateModelCode.Contains($"{sourceName} {{ get; set; }}"))
                        return false;
                }

                return true;
            });
    }

    // ──────────────────────────────────────────────────────────────────────
    // Property 4: Update Model Property Deduplication
    // ──────────────────────────────────────────────────────────────────────

    /// <summary>
    /// **Feature: update-model-computed-field-redesign, Property 4: Update Model Property Deduplication**
    ///
    /// When a property is both a source property and an extracted property of the same non-key
    /// computed field, it appears exactly once in the generated update model.
    ///
    /// **Validates: Requirements 3.4**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property PropertyDeduplication_AppearsExactlyOnce()
    {
        return Prop.ForAll(
            GenEntityWithDeduplicationScenario().ToArbitrary(),
            config =>
            {
                var source = BuildEntitySource(config);
                var (updateModelCode, _) = RunGenerator(source, "UpdateModel");
                if (updateModelCode == null) return false;

                // The shared property (source AND has attribute mapping) should appear exactly once
                var sharedProp = config.Properties.First(p => p.IsSharedSourceAndMapped);
                var pattern = $"public .+\\? {sharedProp.Name} {{ get; set; }}";
                var matches = Regex.Matches(updateModelCode, pattern);

                return matches.Count == 1;
            });
    }

    // ──────────────────────────────────────────────────────────────────────
    // Property 5: Key-Based Computed Field Cascade Exclusion
    // ──────────────────────────────────────────────────────────────────────

    /// <summary>
    /// **Feature: update-model-computed-field-redesign, Property 5: Key-Based Computed Field Cascade Exclusion**
    ///
    /// For any entity model with a computed field that IS a partition key or sort key, the generated
    /// update model class SHALL NOT contain the computed field, its source properties, or any extracted
    /// properties targeting that computed field.
    ///
    /// **Validates: Requirements 3.5**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property KeyBasedComputedField_CascadeExclusion()
    {
        return Prop.ForAll(
            GenEntityWithKeyComputed().ToArbitrary(),
            config =>
            {
                var source = BuildEntitySource(config);
                var (updateModelCode, _) = RunGenerator(source, "UpdateModel");
                if (updateModelCode == null) return false;

                // The key-based computed field must NOT appear
                var keyComputedProp = config.Properties.First(p => p.IsComputed && (p.IsPartitionKey || p.IsSortKey));
                if (updateModelCode.Contains($"{keyComputedProp.Name} {{ get; set; }}"))
                    return false;

                // Its source properties must NOT appear
                foreach (var sourceName in keyComputedProp.ComputedSourceProperties!)
                {
                    if (updateModelCode.Contains($"{sourceName} {{ get; set; }}"))
                        return false;
                }

                // Extracted properties targeting the key-based computed field must NOT appear
                foreach (var extractedProp in config.Properties.Where(p => p.IsExtracted && p.ExtractedSource == keyComputedProp.Name))
                {
                    if (updateModelCode.Contains($"{extractedProp.Name} {{ get; set; }}"))
                        return false;
                }

                return true;
            });
    }

    // ──────────────────────────────────────────────────────────────────────
    // Property 12: Nullable Type Generation Convention
    // ──────────────────────────────────────────────────────────────────────

    /// <summary>
    /// **Feature: update-model-computed-field-redesign, Property 12: Nullable Type Generation Convention**
    ///
    /// For any property included in the generated update model, if the property type is a reference type
    /// it SHALL be generated as T?, and if it is a value type it SHALL be generated as Nullable&lt;T&gt;
    /// (e.g., int?, DateTime?, decimal?).
    ///
    /// **Validates: Requirements 1.5**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property NullableTypeGeneration_FollowsConvention()
    {
        return Prop.ForAll(
            GenEntityWithMixedTypes().ToArbitrary(),
            config =>
            {
                var source = BuildEntitySource(config);
                var (updateModelCode, _) = RunGenerator(source, "UpdateModel");
                if (updateModelCode == null) return false;

                foreach (var prop in config.Properties.Where(p => !p.IsPartitionKey && !p.IsSortKey && !p.IsExtracted && !p.IsComputed))
                {
                    // Verify property appears with correct nullable type
                    var expectedNullableType = GetExpectedNullableType(prop.Type);
                    var pattern = $"public {Regex.Escape(expectedNullableType)} {prop.Name} {{ get; set; }}";
                    if (!Regex.IsMatch(updateModelCode, pattern))
                        return false;
                }

                return true;
            });
    }

    // ──────────────────────────────────────────────────────────────────────
    // Generators
    // ──────────────────────────────────────────────────────────────────────

    private static Gen<EntityConfig> GenEntityWithKeys()
    {
        return Gen.Choose(1, 3).SelectMany(nonKeyCount =>
            Gen.Choose(0, 1).Select(hasSortKey =>
            {
                var props = new List<PropertyConfig>();

                // Always have a partition key
                props.Add(new PropertyConfig("Pk", "string", "pk", isPartitionKey: true));

                // Optionally have a sort key
                if (hasSortKey == 1)
                    props.Add(new PropertyConfig("Sk", "string", "sk", isSortKey: true));

                // Add non-key properties
                for (int i = 0; i < nonKeyCount; i++)
                {
                    props.Add(new PropertyConfig($"Prop{i}", "string", $"prop{i}"));
                }

                return new EntityConfig("TestEntity", props);
            }));
    }

    private static Gen<EntityConfig> GenEntityWithExtractedFromKey()
    {
        return Gen.Choose(1, 3).SelectMany(extractedCount =>
            Gen.Choose(0, 1).Select(extractFromSk =>
            {
                var props = new List<PropertyConfig>();

                // Partition key with computed (so it can be extracted from)
                props.Add(new PropertyConfig("Pk", "string", "pk", isPartitionKey: true,
                    isComputed: true, computedSourceProperties: new[] { "Region", "Id" }));

                // Sort key
                props.Add(new PropertyConfig("Sk", "string", "sk", isSortKey: true));

                // Source properties for computed PK (these are sources of key-computed and will also be excluded)
                props.Add(new PropertyConfig("Region", "string", "region"));
                props.Add(new PropertyConfig("Id", "string", "id"));

                // Extracted properties from the PK (key)
                for (int i = 0; i < extractedCount; i++)
                {
                    props.Add(new PropertyConfig(
                        $"Extracted{i}", "string", isExtracted: true,
                        extractedSource: "Pk", extractedIndex: i,
                        extractedSourceIsKey: true));
                }

                // A regular non-key property that should be included
                props.Add(new PropertyConfig("Data", "string", "data"));

                return new EntityConfig("TestEntity", props);
            }));
    }

    private static Gen<EntityConfig> GenEntityWithNonKeyComputed()
    {
        return Gen.Choose(2, 4).Select(sourceCount =>
        {
            var props = new List<PropertyConfig>();

            // Simple PK and SK
            props.Add(new PropertyConfig("Pk", "string", "pk", isPartitionKey: true));
            props.Add(new PropertyConfig("Sk", "string", "sk", isSortKey: true));

            // Source properties for the non-key computed field
            var sourceNames = new string[sourceCount];
            for (int i = 0; i < sourceCount; i++)
            {
                var name = $"Source{i}";
                sourceNames[i] = name;
                props.Add(new PropertyConfig(name, "string", $"source{i}"));
            }

            // Non-key computed field (GSI partition key, for example)
            props.Add(new PropertyConfig("GsiPk", "string", "gsi1pk",
                isComputed: true, computedSourceProperties: sourceNames));

            // A regular non-key property
            props.Add(new PropertyConfig("Description", "string", "description"));

            return new EntityConfig("TestEntity", props);
        });
    }

    private static Gen<EntityConfig> GenEntityWithDeduplicationScenario()
    {
        return Gen.Constant(0).Select(_ =>
        {
            var props = new List<PropertyConfig>();

            // Simple PK and SK
            props.Add(new PropertyConfig("Pk", "string", "pk", isPartitionKey: true));
            props.Add(new PropertyConfig("Sk", "string", "sk", isSortKey: true));

            // A property that is both a source of a non-key computed field AND has its own DynamoDb attribute
            // This tests deduplication: it would be included by the main loop (HasAttributeMapping)
            // and again by the source property inclusion loop
            props.Add(new PropertyConfig("Department", "string", "department", isSharedSourceAndMapped: true));
            props.Add(new PropertyConfig("Category", "string", "category"));

            // Non-key computed field that uses Department as a source
            props.Add(new PropertyConfig("GsiPk", "string", "gsi1pk",
                isComputed: true, computedSourceProperties: new[] { "Department", "Category" }));

            return new EntityConfig("TestEntity", props);
        });
    }

    private static Gen<EntityConfig> GenEntityWithKeyComputed()
    {
        return Gen.Choose(2, 3).SelectMany(sourceCount =>
            Gen.Choose(0, 2).Select(extractedCount =>
            {
                var props = new List<PropertyConfig>();

                // Computed partition key
                var sourceNames = new string[sourceCount];
                for (int i = 0; i < sourceCount; i++)
                {
                    var name = $"KeySource{i}";
                    sourceNames[i] = name;
                    props.Add(new PropertyConfig(name, "string", $"keySource{i}"));
                }

                props.Add(new PropertyConfig("Pk", "string", "pk", isPartitionKey: true,
                    isComputed: true, computedSourceProperties: sourceNames));

                // Sort key (simple)
                props.Add(new PropertyConfig("Sk", "string", "sk", isSortKey: true));

                // Extracted properties from the computed PK
                for (int i = 0; i < extractedCount && i < sourceCount; i++)
                {
                    props.Add(new PropertyConfig(
                        $"PkPart{i}", "string", isExtracted: true,
                        extractedSource: "Pk", extractedIndex: i,
                        extractedSourceIsKey: true));
                }

                // A regular property that should still appear
                props.Add(new PropertyConfig("Name", "string", "name"));

                return new EntityConfig("TestEntity", props);
            }));
    }

    private static Gen<EntityConfig> GenEntityWithMixedTypes()
    {
        // Generate an entity with various property types to test nullable convention
        var typeChoices = new[]
        {
            ("string", true),   // reference type
            ("int", false),     // value type
            ("decimal", false), // value type
            ("bool", false),    // value type
            ("System.DateTime", false), // value type
            ("System.Guid", false),     // value type
            ("long", false),    // value type
            ("double", false),  // value type
        };

        return Gen.Choose(2, 5).Select(propCount =>
        {
            var props = new List<PropertyConfig>();
            props.Add(new PropertyConfig("Pk", "string", "pk", isPartitionKey: true));

            for (int i = 0; i < propCount && i < typeChoices.Length; i++)
            {
                var (type, _) = typeChoices[i];
                props.Add(new PropertyConfig($"Field{i}", type, $"field{i}"));
            }

            return new EntityConfig("TestEntity", props);
        });
    }

    // ──────────────────────────────────────────────────────────────────────
    // Source Generation Helpers
    // ──────────────────────────────────────────────────────────────────────

    private static string BuildEntitySource(EntityConfig config)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("using System;");
        sb.AppendLine("using Oproto.FluentDynamoDb.Attributes;");
        sb.AppendLine();
        sb.AppendLine("namespace TestNamespace");
        sb.AppendLine("{");
        sb.AppendLine($"    [DynamoDbTable(\"test-table\")]");
        sb.AppendLine($"    public partial class {config.ClassName}");
        sb.AppendLine("    {");

        foreach (var prop in config.Properties)
        {
            // Key attributes
            if (prop.IsPartitionKey)
                sb.AppendLine("        [PartitionKey]");
            if (prop.IsSortKey)
                sb.AppendLine("        [SortKey]");

            // Computed attribute
            if (prop.IsComputed && prop.ComputedSourceProperties != null)
            {
                var sources = string.Join(", ", prop.ComputedSourceProperties.Select(s => $"\"{s}\""));
                sb.AppendLine($"        [Computed({sources})]");
            }

            // Extracted attribute
            if (prop.IsExtracted)
            {
                sb.AppendLine($"        [Extracted(\"{prop.ExtractedSource}\", {prop.ExtractedIndex})]");
            }

            // DynamoDbAttribute (only if has an attribute name)
            if (!string.IsNullOrEmpty(prop.AttributeName))
            {
                sb.AppendLine($"        [DynamoDbAttribute(\"{prop.AttributeName}\")]");
            }

            sb.AppendLine($"        public {prop.Type} {prop.Name} {{ get; set; }}{GetDefaultValue(prop.Type)}");
            sb.AppendLine();
        }

        sb.AppendLine("    }");
        sb.AppendLine("}");

        return sb.ToString();
    }

    private static string GetDefaultValue(string type)
    {
        return type switch
        {
            "string" => " = string.Empty;",
            "int" or "long" or "decimal" or "double" or "float" or "short" or "byte" => "",
            "bool" => "",
            _ when type.StartsWith("System.") => "",
            _ => ""
        };
    }

    private static (string? Code, ImmutableArray<Diagnostic> Diagnostics) RunGenerator(string source, string fileNameContains)
    {
        var compilation = CSharpCompilation.Create(
            "TestAssembly",
            new[] { CSharpSyntaxTree.ParseText(source) },
            DynamicCompilationHelper.GetFluentDynamoDbReferences(),
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var generator = new DynamoDbSourceGenerator();
        var driver = CSharpGeneratorDriver.Create(generator);

        driver.RunGeneratorsAndUpdateCompilation(compilation, out var outputCompilation, out var diagnostics);

        var generatedTree = outputCompilation.SyntaxTrees
            .Skip(compilation.SyntaxTrees.Count())
            .FirstOrDefault(t => t.FilePath.Contains(fileNameContains));

        return (generatedTree?.GetText().ToString(), diagnostics);
    }

    private static string GetExpectedNullableType(string type)
    {
        // Both reference and value types get ? suffix in the update model
        return type switch
        {
            "string" => "string?",
            "int" => "int?",
            "long" => "long?",
            "decimal" => "decimal?",
            "double" => "double?",
            "float" => "float?",
            "short" => "short?",
            "byte" => "byte?",
            "bool" => "bool?",
            "System.DateTime" => "System.DateTime?",
            "System.Guid" => "System.Guid?",
            "System.DateTimeOffset" => "System.DateTimeOffset?",
            _ => type + "?"
        };
    }

    // ──────────────────────────────────────────────────────────────────────
    // Config Models
    // ──────────────────────────────────────────────────────────────────────

    private class EntityConfig
    {
        public string ClassName { get; }
        public List<PropertyConfig> Properties { get; }

        public EntityConfig(string className, List<PropertyConfig> properties)
        {
            ClassName = className;
            Properties = properties;
        }
    }

    private class PropertyConfig
    {
        public string Name { get; set; } = string.Empty;
        public string Type { get; set; } = "string";
        public string? AttributeName { get; set; }
        public bool IsPartitionKey { get; set; }
        public bool IsSortKey { get; set; }
        public bool IsComputed { get; set; }
        public string[]? ComputedSourceProperties { get; set; }
        public bool IsExtracted { get; set; }
        public string? ExtractedSource { get; set; }
        public int ExtractedIndex { get; set; }
        public bool ExtractedSourceIsKey { get; set; }
        public bool IsSharedSourceAndMapped { get; set; }

        public PropertyConfig(string name, string type, string? attributeName = null,
            bool isPartitionKey = false, bool isSortKey = false,
            bool isComputed = false, string[]? computedSourceProperties = null,
            bool isExtracted = false, string? extractedSource = null,
            int extractedIndex = 0, bool extractedSourceIsKey = false,
            bool isSharedSourceAndMapped = false)
        {
            Name = name;
            Type = type;
            AttributeName = attributeName;
            IsPartitionKey = isPartitionKey;
            IsSortKey = isSortKey;
            IsComputed = isComputed;
            ComputedSourceProperties = computedSourceProperties;
            IsExtracted = isExtracted;
            ExtractedSource = extractedSource;
            ExtractedIndex = extractedIndex;
            ExtractedSourceIsKey = extractedSourceIsKey;
            IsSharedSourceAndMapped = isSharedSourceAndMapped;
        }
    }
}
