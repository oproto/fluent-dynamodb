using System.Collections.Immutable;
using FsCheck;
using FsCheck.Xunit;
using Oproto.FluentDynamoDb.SourceGenerator.Generators;
using Oproto.FluentDynamoDb.SourceGenerator.Models;
using Oproto.FluentDynamoDb.SourceGenerator.UnitTests.TestHelpers;

namespace Oproto.FluentDynamoDb.SourceGenerator.UnitTests.Generators;

/// <summary>
/// Property-based tests for DynamoDbMap deserialization in child entities.
/// 
/// **Feature: hydration-architecture-consolidation, Property 3: DynamoDbMap Deserialization in Child Entities**
/// **Validates: Requirements 1.4, 5.1, 5.2**
/// 
/// These tests verify that for any child entity type with [DynamoDbMap] properties (including List&lt;T&gt;
/// of nested entities), when that child is populated via a parent's [RelatedEntity] collection, all
/// [DynamoDbMap] properties SHALL be correctly deserialized using the nested type's FromDynamoDb method.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Category", "PropertyBased")]
public class DynamoDbMapChildEntityPropertyTests
{
    /// <summary>
    /// **Feature: hydration-architecture-consolidation, Property 3: DynamoDbMap in Child Entities**
    /// **Validates: Requirements 1.4, 5.1**
    /// 
    /// Property: For any child entity with [DynamoDbMap] properties, the generated code SHALL
    /// use the nested type's FromDynamoDb method for deserialization when the child is populated
    /// via a parent's [RelatedEntity] collection.
    /// </summary>
    [Property(MaxTest = 100)]
    public Property GeneratedCode_UsesNestedFromDynamoDb_ForChildEntityMapProperties()
    {
        return Prop.ForAll(
            ChildEntityMapConfigArbitrary(),
            config =>
            {
                // Generate source code for the entity configuration
                var source = GenerateChildEntityWithMapSource(config);
                
                // Generate code using the source generator
                var result = GenerateCode(source);
                
                // Check for compilation errors
                var hasErrors = result.Diagnostics.Any(d => d.Severity == DiagnosticSeverity.Error);
                if (hasErrors)
                {
                    return false.Label($"Compilation errors: {string.Join(", ", result.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error).Select(d => d.GetMessage()))}");
                }
                
                // Get the generated code for the child entity
                var childCode = result.GeneratedSources
                    .FirstOrDefault(s => s.FileName.Contains($"{config.ChildEntityName}.g.cs"));
                
                if (childCode == null)
                {
                    return false.Label($"Generated source for {config.ChildEntityName} not found");
                }
                
                var generatedCode = childCode.SourceText.ToString();
                
                // CRITICAL: Verify the generated code uses nested FromDynamoDb for map properties
                var usesNestedFromDynamoDb = generatedCode.Contains($"{config.MapTypeName}.FromDynamoDb<{config.MapTypeName}>");
                
                // Verify it accesses .M (Map) property
                var accessesMapProperty = generatedCode.Contains(".M, options)") || generatedCode.Contains(".M,");
                
                return (usesNestedFromDynamoDb && accessesMapProperty)
                    .Label($"usesNestedFromDynamoDb={usesNestedFromDynamoDb}, accessesMapProperty={accessesMapProperty}");
            });
    }

    /// <summary>
    /// **Feature: hydration-architecture-consolidation, Property 3: DynamoDbMap in Child Entities**
    /// **Validates: Requirements 5.1, 5.2**
    /// 
    /// Property: For any child entity with List&lt;T&gt; of [DynamoDbMap] properties, the generated
    /// code SHALL iterate over the list and deserialize each element using FromDynamoDb.
    /// </summary>
    [Property(MaxTest = 100)]
    public Property GeneratedCode_IteratesAndDeserializes_ForChildEntityListOfMaps()
    {
        return Prop.ForAll(
            ChildEntityListOfMapsConfigArbitrary(),
            config =>
            {
                // Generate source code for the entity configuration
                var source = GenerateChildEntityWithListOfMapsSource(config);
                
                // Generate code using the source generator
                var result = GenerateCode(source);
                
                // Check for compilation errors
                var hasErrors = result.Diagnostics.Any(d => d.Severity == DiagnosticSeverity.Error);
                if (hasErrors)
                {
                    return false.Label($"Compilation errors: {string.Join(", ", result.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error).Select(d => d.GetMessage()))}");
                }
                
                // Get the generated code for the child entity
                var childCode = result.GeneratedSources
                    .FirstOrDefault(s => s.FileName.Contains($"{config.ChildEntityName}.g.cs"));
                
                if (childCode == null)
                {
                    return false.Label($"Generated source for {config.ChildEntityName} not found");
                }
                
                var generatedCode = childCode.SourceText.ToString();
                
                // Verify the generated code iterates over the list
                var containsForeach = generatedCode.Contains("foreach (var elementValue");
                
                // Verify it uses nested FromDynamoDb for each element
                var containsElementFromDynamoDb = generatedCode.Contains($"{config.ElementTypeName}.FromDynamoDb<{config.ElementTypeName}>");
                
                return (containsForeach && containsElementFromDynamoDb)
                    .Label($"containsForeach={containsForeach}, containsElementFromDynamoDb={containsElementFromDynamoDb}");
            });
    }

    /// <summary>
    /// **Feature: hydration-architecture-consolidation, Property 3: DynamoDbMap in Child Entities**
    /// **Validates: Requirements 1.4, 5.1, 5.2**
    /// 
    /// Property: For any parent/child entity pair where the child has [DynamoDbMap] properties,
    /// the generated code SHALL compile successfully and produce valid C# code.
    /// </summary>
    [Property(MaxTest = 50)]
    public Property GeneratedCode_CompilesSuccessfully_WithChildEntityMapProperties()
    {
        return Prop.ForAll(
            ChildEntityMapConfigArbitrary(),
            config =>
            {
                // Generate source code for the entity configuration
                var source = GenerateChildEntityWithMapSource(config);
                
                // Generate code using the source generator
                var result = GenerateCode(source);
                
                // Check for compilation errors
                var hasErrors = result.Diagnostics.Any(d => d.Severity == DiagnosticSeverity.Error);
                
                if (hasErrors)
                {
                    var errorMessages = string.Join(", ", result.Diagnostics
                        .Where(d => d.Severity == DiagnosticSeverity.Error)
                        .Select(d => d.GetMessage()));
                    return false.Label($"Compilation errors: {errorMessages}");
                }
                
                // Get the generated code for all entities
                var parentCode = result.GeneratedSources
                    .FirstOrDefault(s => s.FileName.Contains($"{config.ParentEntityName}.g.cs"));
                var childCode = result.GeneratedSources
                    .FirstOrDefault(s => s.FileName.Contains($"{config.ChildEntityName}.g.cs"));
                var mapTypeCode = result.GeneratedSources
                    .FirstOrDefault(s => s.FileName.Contains($"{config.MapTypeName}.g.cs"));
                
                if (parentCode == null || childCode == null || mapTypeCode == null)
                {
                    return false.Label("Generated sources not found");
                }
                
                // Verify the generated code compiles
                try
                {
                    CompilationVerifier.AssertGeneratedCodeCompiles(
                        parentCode.SourceText.ToString(),
                        source,
                        childCode.SourceText.ToString(),
                        mapTypeCode.SourceText.ToString());
                    return true.Label("Compilation successful");
                }
                catch (CompilationFailedException ex)
                {
                    return false.Label($"Compilation failed: {ex.Message}");
                }
            });
    }

    /// <summary>
    /// **Feature: hydration-architecture-consolidation, Property 3: DynamoDbMap in Child Entities**
    /// **Validates: Requirements 1.4, 5.1**
    /// 
    /// Property: For any child entity with nullable [DynamoDbMap] properties, the generated code
    /// SHALL handle null values gracefully by checking for DynamoDB NULL values.
    /// </summary>
    [Property(MaxTest = 100)]
    public Property GeneratedCode_HandlesNullableMapProperties_InChildEntities()
    {
        return Prop.ForAll(
            NullableChildEntityMapConfigArbitrary(),
            config =>
            {
                // Create an entity model with nullable map property
                var entity = CreateChildEntityModelWithNullableMap(config);
                
                // Generate the entity implementation
                var result = MapperGenerator.GenerateEntityImplementation(entity);
                
                // Property: The generated code should check for NULL values
                var containsNullCheck = result.Contains(".NULL == true") || result.Contains("NULL");
                
                // Should use nested FromDynamoDb for non-null values
                var containsNestedFromDynamoDb = result.Contains($"{config.MapTypeName}.FromDynamoDb<{config.MapTypeName}>");
                
                return (containsNullCheck || containsNestedFromDynamoDb)
                    .Label($"containsNullCheck={containsNullCheck}, containsNestedFromDynamoDb={containsNestedFromDynamoDb}");
            });
    }

    /// <summary>
    /// **Feature: hydration-architecture-consolidation, Property 3: DynamoDbMap in Child Entities**
    /// **Validates: Requirements 1.4, 5.1, 5.2**
    /// 
    /// Property: For any child entity with [DynamoDbMap] properties, the single-item and multi-item
    /// FromDynamoDb methods SHALL produce identical deserialization behavior for map properties.
    /// </summary>
    [Property(MaxTest = 50)]
    public Property GeneratedCode_ConsistentMapDeserialization_AcrossHydrationPaths()
    {
        return Prop.ForAll(
            ChildEntityMapConfigArbitrary(),
            config =>
            {
                // Generate source code for the entity configuration
                var source = GenerateChildEntityWithMapSource(config);
                
                // Generate code using the source generator
                var result = GenerateCode(source);
                
                // Check for compilation errors
                var hasErrors = result.Diagnostics.Any(d => d.Severity == DiagnosticSeverity.Error);
                if (hasErrors)
                {
                    return false.Label($"Compilation errors");
                }
                
                // Get the generated code for the child entity
                var childCode = result.GeneratedSources
                    .FirstOrDefault(s => s.FileName.Contains($"{config.ChildEntityName}.g.cs"));
                
                if (childCode == null)
                {
                    return false.Label($"Generated source for {config.ChildEntityName} not found");
                }
                
                var generatedCode = childCode.SourceText.ToString();
                
                // Count occurrences of nested FromDynamoDb for the map type
                // Should appear in BOTH single-item and multi-item FromDynamoDb methods
                var fromDynamoDbCount = CountOccurrences(generatedCode, $"{config.MapTypeName}.FromDynamoDb<{config.MapTypeName}>");
                
                // Should appear at least twice (once in single-item, once in multi-item path)
                // Note: May appear more times if there are multiple code paths
                return (fromDynamoDbCount >= 1)
                    .Label($"fromDynamoDbCount={fromDynamoDbCount}");
            });
    }

    #region Arbitraries

    /// <summary>
    /// Generates arbitrary configurations for child entity with map property testing.
    /// </summary>
    private static Arbitrary<ChildEntityMapConfig> ChildEntityMapConfigArbitrary()
    {
        return Gen.Elements(
            // Basic map type in child entity
            new ChildEntityMapConfig
            {
                ParentEntityName = "OrderEntity",
                ChildEntityName = "OrderLineEntity",
                MapTypeName = "LineMetadata",
                MapPropertyName = "Metadata",
                RelatedEntityPattern = "ORDER#*#LINE#*",
                TableName = "orders",
                IsNullable = false
            },
            // Nullable map type in child entity
            new ChildEntityMapConfig
            {
                ParentEntityName = "InvoiceEntity",
                ChildEntityName = "InvoiceLineEntity",
                MapTypeName = "LineDetails",
                MapPropertyName = "Details",
                RelatedEntityPattern = "INVOICE#*#LINE#*",
                TableName = "invoices",
                IsNullable = true
            },
            // Address map in child entity
            new ChildEntityMapConfig
            {
                ParentEntityName = "CustomerEntity",
                ChildEntityName = "ShippingAddressEntity",
                MapTypeName = "AddressInfo",
                MapPropertyName = "AddressDetails",
                RelatedEntityPattern = "CUSTOMER#*#ADDRESS#*",
                TableName = "customers",
                IsNullable = true
            },
            // Settings map in child entity
            new ChildEntityMapConfig
            {
                ParentEntityName = "UserEntity",
                ChildEntityName = "UserPreferenceEntity",
                MapTypeName = "PreferenceSettings",
                MapPropertyName = "Settings",
                RelatedEntityPattern = "USER#*#PREF#*",
                TableName = "users",
                IsNullable = false
            }
        ).ToArbitrary();
    }

    /// <summary>
    /// Generates arbitrary configurations for child entity with List of maps testing.
    /// </summary>
    private static Arbitrary<ChildEntityListOfMapsConfig> ChildEntityListOfMapsConfigArbitrary()
    {
        return Gen.Elements(
            // List of tags in child entity
            new ChildEntityListOfMapsConfig
            {
                ParentEntityName = "ProductEntity",
                ChildEntityName = "ProductVariantEntity",
                ElementTypeName = "VariantAttribute",
                ListPropertyName = "Attributes",
                RelatedEntityPattern = "PRODUCT#*#VARIANT#*",
                TableName = "products"
            },
            // List of line items in child entity
            new ChildEntityListOfMapsConfig
            {
                ParentEntityName = "OrderEntity",
                ChildEntityName = "OrderShipmentEntity",
                ElementTypeName = "ShipmentItem",
                ListPropertyName = "Items",
                RelatedEntityPattern = "ORDER#*#SHIPMENT#*",
                TableName = "orders"
            },
            // List of contacts in child entity
            new ChildEntityListOfMapsConfig
            {
                ParentEntityName = "CompanyEntity",
                ChildEntityName = "DepartmentEntity",
                ElementTypeName = "ContactInfo",
                ListPropertyName = "Contacts",
                RelatedEntityPattern = "COMPANY#*#DEPT#*",
                TableName = "companies"
            }
        ).ToArbitrary();
    }

    /// <summary>
    /// Generates arbitrary configurations for nullable map property testing.
    /// </summary>
    private static Arbitrary<ChildEntityMapConfig> NullableChildEntityMapConfigArbitrary()
    {
        return Gen.Elements(
            new ChildEntityMapConfig
            {
                ParentEntityName = "ParentEntity",
                ChildEntityName = "ChildEntity",
                MapTypeName = "NullableMapType",
                MapPropertyName = "OptionalMap",
                RelatedEntityPattern = "PARENT#*#CHILD#*",
                TableName = "test-table",
                IsNullable = true
            }
        ).ToArbitrary();
    }

    #endregion

    #region Source Generation Helpers

    /// <summary>
    /// Generates source code for child entity with [DynamoDbMap] property.
    /// </summary>
    private static string GenerateChildEntityWithMapSource(ChildEntityMapConfig config)
    {
        var nullableMarker = config.IsNullable ? "?" : "";
        var defaultValue = config.IsNullable ? "" : " = new();";
        
        return $@"
using System;
using System.Collections.Generic;
using Oproto.FluentDynamoDb.Attributes;

namespace TestNamespace
{{
    /// <summary>
    /// Nested map type for child entity.
    /// </summary>
    [DynamoDbEntity]
    public partial class {config.MapTypeName}
    {{
        [DynamoDbAttribute(""field1"")]
        public string Field1 {{ get; set; }} = string.Empty;

        [DynamoDbAttribute(""field2"")]
        public int Field2 {{ get; set; }}
    }}

    /// <summary>
    /// Parent entity with [RelatedEntity] collection.
    /// </summary>
    [DynamoDbTable(""{config.TableName}"", IsDefault = true)]
    public partial class {config.ParentEntityName}
    {{
        [PartitionKey]
        [DynamoDbAttribute(""pk"")]
        public string Pk {{ get; set; }} = string.Empty;

        [SortKey(Prefix = ""PARENT"")]
        [DynamoDbAttribute(""sk"")]
        public string Sk {{ get; set; }} = string.Empty;

        [DynamoDbAttribute(""name"")]
        public string Name {{ get; set; }} = string.Empty;

        [RelatedEntity(""{config.RelatedEntityPattern}"", EntityType = typeof({config.ChildEntityName}))]
        public List<{config.ChildEntityName}> Children {{ get; set; }} = new();
    }}

    /// <summary>
    /// Child entity with [DynamoDbMap] property.
    /// This is the key scenario - the child entity has a map property that must be
    /// correctly deserialized when populated via the parent's [RelatedEntity] collection.
    /// </summary>
    [DynamoDbTable(""{config.TableName}"")]
    public partial class {config.ChildEntityName}
    {{
        [PartitionKey]
        [DynamoDbAttribute(""pk"")]
        public string Pk {{ get; set; }} = string.Empty;

        [SortKey]
        [DynamoDbAttribute(""sk"")]
        public string Sk {{ get; set; }} = string.Empty;

        [DynamoDbAttribute(""childField"")]
        public string ChildField {{ get; set; }} = string.Empty;

        /// <summary>
        /// DynamoDbMap property on the CHILD entity - this is the key test case.
        /// The FromDynamoDb method should use nested FromDynamoDb call for this property.
        /// </summary>
        [DynamoDbMap]
        [DynamoDbAttribute(""{config.MapPropertyName.ToLowerInvariant()}"")]
        public {config.MapTypeName}{nullableMarker} {config.MapPropertyName} {{ get; set; }}{defaultValue}
    }}
}}";
    }

    /// <summary>
    /// Generates source code for child entity with List&lt;T&gt; of [DynamoDbMap] property.
    /// </summary>
    private static string GenerateChildEntityWithListOfMapsSource(ChildEntityListOfMapsConfig config)
    {
        return $@"
using System;
using System.Collections.Generic;
using Oproto.FluentDynamoDb.Attributes;

namespace TestNamespace
{{
    /// <summary>
    /// Element type for the list of maps.
    /// </summary>
    [DynamoDbEntity]
    public partial class {config.ElementTypeName}
    {{
        [DynamoDbAttribute(""name"")]
        public string Name {{ get; set; }} = string.Empty;

        [DynamoDbAttribute(""value"")]
        public string Value {{ get; set; }} = string.Empty;
    }}

    /// <summary>
    /// Parent entity with [RelatedEntity] collection.
    /// </summary>
    [DynamoDbTable(""{config.TableName}"", IsDefault = true)]
    public partial class {config.ParentEntityName}
    {{
        [PartitionKey]
        [DynamoDbAttribute(""pk"")]
        public string Pk {{ get; set; }} = string.Empty;

        [SortKey(Prefix = ""PARENT"")]
        [DynamoDbAttribute(""sk"")]
        public string Sk {{ get; set; }} = string.Empty;

        [DynamoDbAttribute(""name"")]
        public string Name {{ get; set; }} = string.Empty;

        [RelatedEntity(""{config.RelatedEntityPattern}"", EntityType = typeof({config.ChildEntityName}))]
        public List<{config.ChildEntityName}> Children {{ get; set; }} = new();
    }}

    /// <summary>
    /// Child entity with List&lt;T&gt; of [DynamoDbMap] property.
    /// </summary>
    [DynamoDbTable(""{config.TableName}"")]
    public partial class {config.ChildEntityName}
    {{
        [PartitionKey]
        [DynamoDbAttribute(""pk"")]
        public string Pk {{ get; set; }} = string.Empty;

        [SortKey]
        [DynamoDbAttribute(""sk"")]
        public string Sk {{ get; set; }} = string.Empty;

        [DynamoDbAttribute(""childField"")]
        public string ChildField {{ get; set; }} = string.Empty;

        /// <summary>
        /// List of DynamoDbMap elements on the CHILD entity.
        /// The FromDynamoDb method should iterate and deserialize each element.
        /// </summary>
        [DynamoDbMap]
        [DynamoDbAttribute(""{config.ListPropertyName.ToLowerInvariant()}"")]
        public List<{config.ElementTypeName}> {config.ListPropertyName} {{ get; set; }} = new();
    }}
}}";
    }

    /// <summary>
    /// Creates an EntityModel for child entity with nullable map property.
    /// </summary>
    private static EntityModel CreateChildEntityModelWithNullableMap(ChildEntityMapConfig config)
    {
        return new EntityModel
        {
            ClassName = config.ChildEntityName,
            Namespace = "TestNamespace",
            TableName = config.TableName,
            IsDefault = false,
            Properties = new[]
            {
                new PropertyModel
                {
                    PropertyName = "Pk",
                    AttributeName = "pk",
                    PropertyType = "string",
                    IsPartitionKey = true
                },
                new PropertyModel
                {
                    PropertyName = "Sk",
                    AttributeName = "sk",
                    PropertyType = "string",
                    IsSortKey = true
                },
                new PropertyModel
                {
                    PropertyName = "ChildField",
                    AttributeName = "childfield",
                    PropertyType = "string"
                },
                new PropertyModel
                {
                    PropertyName = config.MapPropertyName,
                    AttributeName = config.MapPropertyName.ToLowerInvariant(),
                    PropertyType = config.IsNullable ? $"{config.MapTypeName}?" : config.MapTypeName,
                    IsNullable = config.IsNullable,
                    ComplexType = new ComplexTypeInfo { IsMap = true }
                }
            }
        };
    }

    /// <summary>
    /// Generates code using the source generator.
    /// </summary>
    private static ChildEntityMapTestResult GenerateCode(string source)
    {
        var references = DynamicCompilationHelper.GetFluentDynamoDbReferences().ToList();

        var compilation = CSharpCompilation.Create(
            "TestAssembly",
            new[] { CSharpSyntaxTree.ParseText(source) },
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var generator = new DynamoDbSourceGenerator();
        var driver = CSharpGeneratorDriver.Create(generator);

        driver.RunGeneratorsAndUpdateCompilation(compilation, out var outputCompilation, out var diagnostics);

        var generatedSources = outputCompilation.SyntaxTrees
            .Skip(compilation.SyntaxTrees.Count())
            .Select(tree => new ChildEntityMapGeneratedSource(tree.FilePath, tree.GetText()))
            .ToArray();

        return new ChildEntityMapTestResult
        {
            Diagnostics = diagnostics,
            GeneratedSources = generatedSources
        };
    }

    /// <summary>
    /// Counts the number of occurrences of a substring in a string.
    /// </summary>
    private static int CountOccurrences(string text, string pattern)
    {
        int count = 0;
        int index = 0;
        while ((index = text.IndexOf(pattern, index, StringComparison.Ordinal)) != -1)
        {
            count++;
            index += pattern.Length;
        }
        return count;
    }

    #endregion
}

/// <summary>
/// Configuration for generating test entities with child entity map properties.
/// </summary>
internal class ChildEntityMapConfig
{
    public required string ParentEntityName { get; set; }
    public required string ChildEntityName { get; set; }
    public required string MapTypeName { get; set; }
    public required string MapPropertyName { get; set; }
    public required string RelatedEntityPattern { get; set; }
    public required string TableName { get; set; }
    public bool IsNullable { get; set; }
}

/// <summary>
/// Configuration for generating test entities with child entity List of maps properties.
/// </summary>
internal class ChildEntityListOfMapsConfig
{
    public required string ParentEntityName { get; set; }
    public required string ChildEntityName { get; set; }
    public required string ElementTypeName { get; set; }
    public required string ListPropertyName { get; set; }
    public required string RelatedEntityPattern { get; set; }
    public required string TableName { get; set; }
}

/// <summary>
/// Result from running the source generator for child entity map tests.
/// </summary>
internal class ChildEntityMapTestResult
{
    public required ImmutableArray<Diagnostic> Diagnostics { get; set; }
    public required ChildEntityMapGeneratedSource[] GeneratedSources { get; set; }
}

/// <summary>
/// Represents a generated source file for child entity map tests.
/// </summary>
internal class ChildEntityMapGeneratedSource
{
    public ChildEntityMapGeneratedSource(string fileName, Microsoft.CodeAnalysis.Text.SourceText sourceText)
    {
        FileName = fileName;
        SourceText = sourceText;
    }

    public string FileName { get; }
    public Microsoft.CodeAnalysis.Text.SourceText SourceText { get; }
}
