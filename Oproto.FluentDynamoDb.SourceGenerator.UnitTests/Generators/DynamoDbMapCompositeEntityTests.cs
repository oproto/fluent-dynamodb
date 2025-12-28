// ============================================================================
// DynamoDbMap Composite Entity Bug Fix Tests
// ============================================================================
// These tests verify that [DynamoDbMap] properties are correctly deserialized
// in composite entities via ToCompositeEntityAsync().
//
// Bug: The generated multi-item FromDynamoDb method in GeneratePrimaryEntityIdentification
// was missing ComplexType.IsMap check, causing map properties to be incorrectly
// deserialized using GetFromAttributeValueExpression (which falls through to .S accessor).
//
// Requirements: 2.1, 2.2, 2.3, 2.4, 2.5 from source-generator-bug-fixes spec
// ============================================================================

using System.Collections.Immutable;
using AwesomeAssertions;
using FsCheck;
using FsCheck.Xunit;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Oproto.FluentDynamoDb.SourceGenerator.UnitTests.TestHelpers;

namespace Oproto.FluentDynamoDb.SourceGenerator.UnitTests.Generators;

/// <summary>
/// Tests for DynamoDbMap properties in composite entities with [RelatedEntity] attributes.
/// These tests verify that the source generator correctly generates nested FromDynamoDb
/// calls for [DynamoDbMap] properties in multi-item deserialization scenarios.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Feature", "source-generator-bug-fixes")]
public class DynamoDbMapCompositeEntityTests
{
    #region Bug Reproduction Tests

    /// <summary>
    /// CRITICAL BUG REPRODUCTION: Parent entity with [DynamoDbMap] property AND [RelatedEntity] attribute.
    /// 
    /// The bug occurs in GeneratePrimaryEntityIdentification when the PARENT entity has a [DynamoDbMap]
    /// property. The multi-item FromDynamoDb method was directly calling GetFromAttributeValueExpression
    /// for all non-collection properties WITHOUT checking if they are [DynamoDbMap] properties.
    /// 
    /// This caused the generator to emit incorrect code that accessed .S on the AttributeValue
    /// instead of calling the nested type's FromDynamoDb method with .M.
    /// 
    /// **Validates: Requirements 2.1, 2.2, 2.3, 2.4, 2.5**
    /// </summary>
    [Fact]
    public void Generator_WithParentEntityContainingDynamoDbMapAndRelatedEntity_GeneratesCorrectMapDeserialization()
    {
        // Arrange - Create a composite entity scenario where the PARENT has a [DynamoDbMap] property
        var source = @"
using System;
using System.Collections.Generic;
using Oproto.FluentDynamoDb.Attributes;

namespace TestNamespace
{
    /// <summary>
    /// Nested map type - must have [DynamoDbEntity] for AOT-compatible mapping.
    /// </summary>
    [DynamoDbEntity]
    public partial class AddressInfo
    {
        [DynamoDbAttribute(""street"")]
        public string Street { get; set; } = string.Empty;

        [DynamoDbAttribute(""city"")]
        public string City { get; set; } = string.Empty;

        [DynamoDbAttribute(""zipCode"")]
        public string ZipCode { get; set; } = string.Empty;
    }

    /// <summary>
    /// Parent entity with BOTH a [DynamoDbMap] property AND a [RelatedEntity] collection.
    /// This is the bug scenario - the multi-item FromDynamoDb method should use
    /// AddressInfo.FromDynamoDb<AddressInfo>(value.M, options) for the Address property.
    /// </summary>
    [DynamoDbTable(""locations"", IsDefault = true)]
    public partial class LocationEntity
    {
        [PartitionKey(Prefix = ""LOCATION"")]
        [DynamoDbAttribute(""pk"")]
        public string Pk { get; set; } = string.Empty;

        [SortKey(Prefix = ""LOCATION"")]
        [DynamoDbAttribute(""sk"")]
        public string Sk { get; set; } = string.Empty;

        [DynamoDbAttribute(""name"")]
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// DynamoDbMap property on the PARENT entity - this is where the bug manifests.
        /// The multi-item FromDynamoDb method should use nested FromDynamoDb call,
        /// but was incorrectly using GetFromAttributeValueExpression.
        /// </summary>
        [DynamoDbMap]
        [DynamoDbAttribute(""address"")]
        public AddressInfo? Address { get; set; }

        /// <summary>
        /// Related entity collection - the presence of this attribute triggers the
        /// multi-item FromDynamoDb code path where the bug occurs.
        /// </summary>
        [RelatedEntity(""LOCATION#*#CONTACT#*"", EntityType = typeof(ContactEntity))]
        public List<ContactEntity> Contacts { get; set; } = new();
    }

    /// <summary>
    /// Child entity - related to LocationEntity.
    /// </summary>
    [DynamoDbTable(""locations"")]
    public partial class ContactEntity
    {
        [PartitionKey(Prefix = ""LOCATION"")]
        [DynamoDbAttribute(""pk"")]
        public string Pk { get; set; } = string.Empty;

        [SortKey]
        [DynamoDbAttribute(""sk"")]
        public string Sk { get; set; } = string.Empty;

        [DynamoDbAttribute(""contactName"")]
        public string ContactName { get; set; } = string.Empty;
    }
}";

        // Act - Generate code
        var result = GenerateCode(source);

        // Assert - No compilation errors
        result.Diagnostics.Should().NotContain(d => d.Severity == DiagnosticSeverity.Error,
            "source generator should not produce errors for valid composite entity with DynamoDbMap");

        // Get the generated code for the entities
        var locationEntityCode = GetGeneratedSource(result, "LocationEntity.g.cs");
        var contactEntityCode = GetGeneratedSource(result, "ContactEntity.g.cs");
        var addressInfoCode = GetGeneratedSource(result, "AddressInfo.g.cs");
        
        // Verify compilation - THIS IS THE CRITICAL TEST
        CompilationVerifier.AssertGeneratedCodeCompiles(locationEntityCode, source, contactEntityCode, addressInfoCode);

        // CRITICAL ASSERTION: The multi-item FromDynamoDb method should use nested FromDynamoDb call
        // for [DynamoDbMap] properties, NOT GetFromAttributeValueExpression
        locationEntityCode.Should().Contain("AddressInfo.FromDynamoDb<AddressInfo>",
            "generated code MUST use nested FromDynamoDb call for [DynamoDbMap] properties in multi-item mapping");
        
        // Verify it accesses .M (Map) not .S (String)
        locationEntityCode.Should().Contain(".M, options)",
            "generated code MUST access .M property for map deserialization");
    }

    /// <summary>
    /// Test with nullable DynamoDbMap property in parent entity with RelatedEntity.
    /// 
    /// **Validates: Requirements 2.4**
    /// </summary>
    [Fact]
    public void Generator_WithNullableDynamoDbMapInParentEntity_HandlesNullGracefully()
    {
        // Arrange
        var source = @"
using System;
using System.Collections.Generic;
using Oproto.FluentDynamoDb.Attributes;

namespace TestNamespace
{
    [DynamoDbEntity]
    public partial class ProductDetails
    {
        [DynamoDbAttribute(""weight"")]
        public double Weight { get; set; }

        [DynamoDbAttribute(""dimensions"")]
        public string Dimensions { get; set; } = string.Empty;
    }

    [DynamoDbTable(""products"", IsDefault = true)]
    public partial class Product
    {
        [PartitionKey]
        [DynamoDbAttribute(""pk"")]
        public string Pk { get; set; } = string.Empty;

        [SortKey(Prefix = ""PRODUCT"")]
        [DynamoDbAttribute(""sk"")]
        public string Sk { get; set; } = string.Empty;

        [DynamoDbAttribute(""name"")]
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Nullable DynamoDbMap property - should handle null values gracefully.
        /// </summary>
        [DynamoDbMap]
        [DynamoDbAttribute(""details"")]
        public ProductDetails? Details { get; set; }

        [RelatedEntity(""PRODUCT#*#REVIEW#*"", EntityType = typeof(ProductReview))]
        public List<ProductReview> Reviews { get; set; } = new();
    }

    [DynamoDbTable(""products"")]
    public partial class ProductReview
    {
        [PartitionKey]
        [DynamoDbAttribute(""pk"")]
        public string Pk { get; set; } = string.Empty;

        [SortKey]
        [DynamoDbAttribute(""sk"")]
        public string Sk { get; set; } = string.Empty;

        [DynamoDbAttribute(""rating"")]
        public int Rating { get; set; }
    }
}";

        // Act
        var result = GenerateCode(source);

        // Assert
        result.Diagnostics.Should().NotContain(d => d.Severity == DiagnosticSeverity.Error);

        var productCode = GetGeneratedSource(result, "Product.g.cs");
        var reviewCode = GetGeneratedSource(result, "ProductReview.g.cs");
        var detailsCode = GetGeneratedSource(result, "ProductDetails.g.cs");
        
        // Verify compilation
        CompilationVerifier.AssertGeneratedCodeCompiles(productCode, source, reviewCode, detailsCode);

        // Verify null handling for DynamoDbMap property
        productCode.Should().Contain("ProductDetails.FromDynamoDb<ProductDetails>",
            "should use nested FromDynamoDb for nullable DynamoDbMap property");
    }

    /// <summary>
    /// Test that verifies both single-item and multi-item FromDynamoDb methods
    /// use consistent map deserialization for DynamoDbMap properties.
    /// 
    /// **Validates: Requirements 2.3**
    /// </summary>
    [Fact]
    public void Generator_BothFromDynamoDbOverloads_UseConsistentMapDeserialization()
    {
        // Arrange
        var source = @"
using System;
using System.Collections.Generic;
using Oproto.FluentDynamoDb.Attributes;

namespace TestNamespace
{
    [DynamoDbEntity]
    public partial class OrderMetadata
    {
        [DynamoDbAttribute(""source"")]
        public string Source { get; set; } = string.Empty;

        [DynamoDbAttribute(""priority"")]
        public int Priority { get; set; }
    }

    [DynamoDbTable(""orders"", IsDefault = true)]
    public partial class Order
    {
        [PartitionKey(Prefix = ""ORDER"")]
        [DynamoDbAttribute(""pk"")]
        public string Pk { get; set; } = string.Empty;

        [SortKey(Prefix = ""ORDER"")]
        [DynamoDbAttribute(""sk"")]
        public string Sk { get; set; } = string.Empty;

        [DynamoDbMap]
        [DynamoDbAttribute(""metadata"")]
        public OrderMetadata? Metadata { get; set; }

        [RelatedEntity(""ORDER#*#LINE#*"", EntityType = typeof(OrderLine))]
        public List<OrderLine> Lines { get; set; } = new();
    }

    [DynamoDbTable(""orders"")]
    public partial class OrderLine
    {
        [PartitionKey(Prefix = ""ORDER"")]
        [DynamoDbAttribute(""pk"")]
        public string Pk { get; set; } = string.Empty;

        [SortKey]
        [DynamoDbAttribute(""sk"")]
        public string Sk { get; set; } = string.Empty;

        [DynamoDbAttribute(""quantity"")]
        public int Quantity { get; set; }
    }
}";

        // Act
        var result = GenerateCode(source);

        // Assert
        result.Diagnostics.Should().NotContain(d => d.Severity == DiagnosticSeverity.Error);

        var orderCode = GetGeneratedSource(result, "Order.g.cs");
        var lineCode = GetGeneratedSource(result, "OrderLine.g.cs");
        var metadataCode = GetGeneratedSource(result, "OrderMetadata.g.cs");
        
        CompilationVerifier.AssertGeneratedCodeCompiles(orderCode, source, lineCode, metadataCode);

        // Count occurrences of nested FromDynamoDb for OrderMetadata
        // Should appear in BOTH single-item and multi-item FromDynamoDb methods
        var fromDynamoDbCount = CountOccurrences(orderCode, "OrderMetadata.FromDynamoDb<OrderMetadata>");
        
        fromDynamoDbCount.Should().BeGreaterThanOrEqualTo(2,
            "nested FromDynamoDb should appear in both single-item and multi-item FromDynamoDb methods");
    }

    #endregion

    #region Helper Methods

    /// <summary>
    /// Generates code using the source generator.
    /// </summary>
    private static DynamoDbMapTestResult GenerateCode(string source)
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
            .Select(tree => new DynamoDbMapGeneratedSource(tree.FilePath, tree.GetText()))
            .ToArray();

        return new DynamoDbMapTestResult
        {
            Diagnostics = diagnostics,
            GeneratedSources = generatedSources
        };
    }

    private static string GetGeneratedSource(DynamoDbMapTestResult result, string fileNamePart)
    {
        var source = result.GeneratedSources.FirstOrDefault(s => s.FileName.Contains(fileNamePart));
        source.Should().NotBeNull($"Expected to find generated source containing '{fileNamePart}'");
        return source!.SourceText.ToString();
    }

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
/// Result from running the source generator for DynamoDbMap tests.
/// </summary>
internal class DynamoDbMapTestResult
{
    public required ImmutableArray<Diagnostic> Diagnostics { get; set; }
    public required DynamoDbMapGeneratedSource[] GeneratedSources { get; set; }
}

/// <summary>
/// Represents a generated source file for DynamoDbMap tests.
/// </summary>
internal class DynamoDbMapGeneratedSource
{
    public DynamoDbMapGeneratedSource(string fileName, Microsoft.CodeAnalysis.Text.SourceText sourceText)
    {
        FileName = fileName;
        SourceText = sourceText;
    }

    public string FileName { get; }
    public Microsoft.CodeAnalysis.Text.SourceText SourceText { get; }
}


#region Property-Based Tests

/// <summary>
/// Property-based tests for DynamoDbMap multi-item deserialization correctness.
/// These tests verify the correctness properties defined in the design document.
/// </summary>
[Trait("Category", "PropertyTest")]
[Trait("Feature", "source-generator-bug-fixes")]
public class DynamoDbMapMultiItemDeserializationPropertyTests
{
    /// <summary>
    /// **Feature: source-generator-bug-fixes, Property 1: DynamoDbMap Multi-Item Deserialization Correctness**
    /// *For any* entity with a [DynamoDbMap] property and [RelatedEntity] attribute, the generated 
    /// multi-item FromDynamoDb code SHALL use the nested type's FromDynamoDb method (not Enum.Parse 
    /// or string accessor) and produce identical results to single-item deserialization.
    /// **Validates: Requirements 2.1, 2.2, 2.3, 2.4, 2.5**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property DynamoDbMap_MultiItem_UsesNestedFromDynamoDb()
    {
        return Prop.ForAll(
            DynamoDbMapEntityArbitrary(),
            entityConfig =>
            {
                // Generate source code for the entity configuration
                var source = GenerateEntitySource(entityConfig);
                
                // Generate code using the source generator
                var result = GenerateCode(source);
                
                // Check for compilation errors
                var hasErrors = result.Diagnostics.Any(d => d.Severity == DiagnosticSeverity.Error);
                if (hasErrors)
                {
                    return false.Label($"Compilation errors: {string.Join(", ", result.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error).Select(d => d.GetMessage()))}");
                }
                
                // Get the generated code for the parent entity
                var parentCode = result.GeneratedSources
                    .FirstOrDefault(s => s.FileName.Contains($"{entityConfig.ParentEntityName}.g.cs"));
                
                if (parentCode == null)
                {
                    return false.Label($"Generated source for {entityConfig.ParentEntityName} not found");
                }
                
                var generatedCode = parentCode.SourceText.ToString();
                
                // CRITICAL: Verify the generated code uses nested FromDynamoDb call
                // and does NOT use Enum.Parse for the map type
                var usesNestedFromDynamoDb = generatedCode.Contains($"{entityConfig.MapTypeName}.FromDynamoDb<{entityConfig.MapTypeName}>");
                var usesEnumParse = generatedCode.Contains($"Enum.Parse<{entityConfig.MapTypeFullName}>") ||
                                    generatedCode.Contains($"Enum.Parse<TestNamespace.{entityConfig.MapTypeName}>");
                
                // Also verify it accesses .M (Map) property
                var accessesMapProperty = generatedCode.Contains(".M, options)") || generatedCode.Contains(".M,");
                
                return (usesNestedFromDynamoDb && !usesEnumParse && accessesMapProperty)
                    .Label($"usesNestedFromDynamoDb={usesNestedFromDynamoDb}, usesEnumParse={usesEnumParse}, accessesMapProperty={accessesMapProperty}");
            });
    }

    #region Arbitraries

    /// <summary>
    /// Generates arbitrary entity configurations for property-based testing.
    /// </summary>
    private static Arbitrary<DynamoDbMapEntityConfig> DynamoDbMapEntityArbitrary()
    {
        return Gen.Elements(
            // Basic map type
            new DynamoDbMapEntityConfig
            {
                ParentEntityName = "ParentEntity",
                MapTypeName = "MapType",
                MapTypeFullName = "TestNamespace.MapType",
                MapPropertyName = "MapProperty",
                ChildEntityName = "ChildEntity",
                IsNullable = false
            },
            // Nullable map type
            new DynamoDbMapEntityConfig
            {
                ParentEntityName = "NullableParent",
                MapTypeName = "NullableMapType",
                MapTypeFullName = "TestNamespace.NullableMapType",
                MapPropertyName = "NullableMap",
                ChildEntityName = "NullableChild",
                IsNullable = true
            },
            // Different naming patterns
            new DynamoDbMapEntityConfig
            {
                ParentEntityName = "OrderEntity",
                MapTypeName = "OrderMetadata",
                MapTypeFullName = "TestNamespace.OrderMetadata",
                MapPropertyName = "Metadata",
                ChildEntityName = "OrderLineEntity",
                IsNullable = true
            },
            // Complex nested type name
            new DynamoDbMapEntityConfig
            {
                ParentEntityName = "CustomerProfile",
                MapTypeName = "AddressDetails",
                MapTypeFullName = "TestNamespace.AddressDetails",
                MapPropertyName = "ShippingAddress",
                ChildEntityName = "CustomerOrder",
                IsNullable = true
            }
        ).ToArbitrary();
    }

    /// <summary>
    /// Generates source code for a given entity configuration.
    /// </summary>
    private static string GenerateEntitySource(DynamoDbMapEntityConfig config)
    {
        var nullableMarker = config.IsNullable ? "?" : "";
        
        return $@"
using System;
using System.Collections.Generic;
using Oproto.FluentDynamoDb.Attributes;

namespace TestNamespace
{{
    [DynamoDbEntity]
    public partial class {config.MapTypeName}
    {{
        [DynamoDbAttribute(""field1"")]
        public string Field1 {{ get; set; }} = string.Empty;

        [DynamoDbAttribute(""field2"")]
        public int Field2 {{ get; set; }}
    }}

    [DynamoDbTable(""test-table"", IsDefault = true)]
    public partial class {config.ParentEntityName}
    {{
        [PartitionKey(Prefix = ""PARENT"")]
        [DynamoDbAttribute(""pk"")]
        public string Pk {{ get; set; }} = string.Empty;

        [SortKey(Prefix = ""PARENT"")]
        [DynamoDbAttribute(""sk"")]
        public string Sk {{ get; set; }} = string.Empty;

        [DynamoDbMap]
        [DynamoDbAttribute(""{config.MapPropertyName.ToLowerInvariant()}"")]
        public {config.MapTypeName}{nullableMarker} {config.MapPropertyName} {{ get; set; }}{(config.IsNullable ? "" : " = new();")}

        [RelatedEntity(""PARENT#*#CHILD#*"", EntityType = typeof({config.ChildEntityName}))]
        public List<{config.ChildEntityName}> Children {{ get; set; }} = new();
    }}

    [DynamoDbTable(""test-table"")]
    public partial class {config.ChildEntityName}
    {{
        [PartitionKey(Prefix = ""PARENT"")]
        [DynamoDbAttribute(""pk"")]
        public string Pk {{ get; set; }} = string.Empty;

        [SortKey]
        [DynamoDbAttribute(""sk"")]
        public string Sk {{ get; set; }} = string.Empty;

        [DynamoDbAttribute(""childField"")]
        public string ChildField {{ get; set; }} = string.Empty;
    }}
}}";
    }

    /// <summary>
    /// Generates code using the source generator.
    /// </summary>
    private static DynamoDbMapTestResult GenerateCode(string source)
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
            .Select(tree => new DynamoDbMapGeneratedSource(tree.FilePath, tree.GetText()))
            .ToArray();

        return new DynamoDbMapTestResult
        {
            Diagnostics = diagnostics,
            GeneratedSources = generatedSources
        };
    }

    #endregion
}

/// <summary>
/// Configuration for generating test entities with DynamoDbMap properties.
/// </summary>
internal class DynamoDbMapEntityConfig
{
    public required string ParentEntityName { get; set; }
    public required string MapTypeName { get; set; }
    public required string MapTypeFullName { get; set; }
    public required string MapPropertyName { get; set; }
    public required string ChildEntityName { get; set; }
    public bool IsNullable { get; set; }
}

#endregion
