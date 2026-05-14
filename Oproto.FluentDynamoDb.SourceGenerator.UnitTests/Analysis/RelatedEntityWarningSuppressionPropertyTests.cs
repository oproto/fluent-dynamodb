using FsCheck;
using FsCheck.Xunit;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Oproto.FluentDynamoDb.SourceGenerator.Analysis;

namespace Oproto.FluentDynamoDb.SourceGenerator.UnitTests.Analysis;

/// <summary>
/// Property-based tests for RelatedEntity warning suppression in EntityAnalyzer.
/// 
/// **Feature: source-generator-bug-fixes, Property 3: RelatedEntity Warning Suppression**
/// **Validates: Requirements 4.1, 4.3**
/// 
/// These tests verify that for any property with [RelatedEntity] attribute,
/// the source generator does NOT emit DYNDB023 performance warning.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Category", "PropertyBased")]
public class RelatedEntityWarningSuppressionPropertyTests
{
    /// <summary>
    /// **Feature: source-generator-bug-fixes, Property 3: RelatedEntity Warning Suppression**
    /// **Validates: Requirements 4.1, 4.3**
    /// 
    /// Property: For any property with [RelatedEntity] attribute, the source generator
    /// SHALL NOT emit DYNDB023 performance warning, regardless of the collection's element type.
    /// </summary>
    [Property(MaxTest = 100)]
    public Property RelatedEntityProperty_ShouldNotEmitDYNDB023Warning()
    {
        // Generate random number of related entities (1 to 5)
        var relatedEntityCountArb = Gen.Choose(1, 5).ToArbitrary();
        
        return Prop.ForAll(relatedEntityCountArb, relatedEntityCount =>
        {
            // Generate source code with the specified number of related entities
            var source = GenerateEntityWithRelatedEntities(relatedEntityCount);
            
            // Parse and analyze
            var (classDecl, semanticModel) = ParseSource(source);
            var analyzer = new EntityAnalyzer();
            var result = analyzer.AnalyzeEntity(classDecl, semanticModel);
            
            // Property: No DYNDB023 warnings should be emitted for RelatedEntity properties
            // DYNDB023 is the performance warning for complex collection types
            var dyndb023Warnings = analyzer.Diagnostics
                .Where(d => d.Id == "DYNDB023")
                .ToList();
            
            // There should be no DYNDB023 warnings for entities with only RelatedEntity collections
            return dyndb023Warnings.Count == 0;
        });
    }

    /// <summary>
    /// **Feature: source-generator-bug-fixes, Property 4: Non-RelatedEntity Warning Preservation**
    /// **Validates: Requirements 4.4**
    /// 
    /// Property: For any complex collection property WITHOUT [RelatedEntity] attribute,
    /// the source generator SHALL continue to emit DYNDB023 performance warning.
    /// </summary>
    [Property(MaxTest = 100)]
    public Property NonRelatedEntityComplexCollection_ShouldEmitDYNDB023Warning()
    {
        // Generate random number of non-related complex collections (1 to 3)
        var collectionCountArb = Gen.Choose(1, 3).ToArbitrary();
        
        return Prop.ForAll(collectionCountArb, collectionCount =>
        {
            // Generate source code with complex collections that don't have RelatedEntity
            var source = GenerateEntityWithNonRelatedComplexCollections(collectionCount);
            
            // Parse and analyze
            var (classDecl, semanticModel) = ParseSource(source);
            var analyzer = new EntityAnalyzer();
            var result = analyzer.AnalyzeEntity(classDecl, semanticModel);
            
            // Property: DYNDB023 warnings should be emitted for non-RelatedEntity complex collections
            var dyndb023Warnings = analyzer.Diagnostics
                .Where(d => d.Id == "DYNDB023")
                .ToList();
            
            // There should be at least one DYNDB023 warning for complex collections without RelatedEntity
            return dyndb023Warnings.Count >= 1;
        });
    }

    /// <summary>
    /// Generates entity source code with the specified number of [RelatedEntity] attributes.
    /// </summary>
    private static string GenerateEntityWithRelatedEntities(int relatedEntityCount)
    {
        var relatedEntityProperties = new System.Text.StringBuilder();
        
        for (int i = 0; i < relatedEntityCount; i++)
        {
            relatedEntityProperties.AppendLine($@"
        [RelatedEntity(""related{i}#*"", EntityType = typeof(RelatedEntity{i}))]
        public List<RelatedEntity{i}>? Related{i} {{ get; set; }}");
        }
        
        var relatedEntityClasses = new System.Text.StringBuilder();
        for (int i = 0; i < relatedEntityCount; i++)
        {
            relatedEntityClasses.AppendLine($@"
    [DynamoDbTable(""test-table"")]
    public partial class RelatedEntity{i}
    {{
        [PartitionKey]
        [DynamoDbAttribute(""pk"")]
        public string Pk {{ get; set; }} = string.Empty;
        
        [SortKey]
        [DynamoDbAttribute(""sk"")]
        public string Sk {{ get; set; }} = string.Empty;
    }}");
        }
        
        return $@"
using Oproto.FluentDynamoDb.Attributes;
using System.Collections.Generic;

namespace TestNamespace
{{
    [DynamoDbTable(""test-table"")]
    public partial class TestEntity
    {{
        [PartitionKey]
        [DynamoDbAttribute(""pk"")]
        public string Id {{ get; set; }} = string.Empty;
        
        [SortKey]
        [DynamoDbAttribute(""sk"")]
        public string SortKey {{ get; set; }} = string.Empty;
{relatedEntityProperties}
    }}
{relatedEntityClasses}
}}";
    }

    /// <summary>
    /// Generates entity source code with complex collections that don't have [RelatedEntity] attribute.
    /// These should trigger DYNDB023 performance warnings.
    /// </summary>
    private static string GenerateEntityWithNonRelatedComplexCollections(int collectionCount)
    {
        var collectionProperties = new System.Text.StringBuilder();
        
        for (int i = 0; i < collectionCount; i++)
        {
            // Use complex types that trigger DYNDB023 warning
            collectionProperties.AppendLine($@"
        [DynamoDbAttribute(""items{i}"")]
        public List<ComplexItem{i}>? Items{i} {{ get; set; }}");
        }
        
        var complexItemClasses = new System.Text.StringBuilder();
        for (int i = 0; i < collectionCount; i++)
        {
            complexItemClasses.AppendLine($@"
    public class ComplexItem{i}
    {{
        public string Name {{ get; set; }} = string.Empty;
        public int Value {{ get; set; }}
    }}");
        }
        
        return $@"
using Oproto.FluentDynamoDb.Attributes;
using System.Collections.Generic;

namespace TestNamespace
{{
    [DynamoDbTable(""test-table"")]
    public partial class TestEntity
    {{
        [PartitionKey]
        [DynamoDbAttribute(""pk"")]
        public string Id {{ get; set; }} = string.Empty;
        
        [SortKey]
        [DynamoDbAttribute(""sk"")]
        public string SortKey {{ get; set; }} = string.Empty;
{collectionProperties}
    }}
{complexItemClasses}
}}";
    }

    /// <summary>
    /// Parses source code and returns the class declaration with semantic model.
    /// </summary>
    private static (ClassDeclarationSyntax ClassDecl, SemanticModel SemanticModel) ParseSource(string source)
    {
        var syntaxTree = CSharpSyntaxTree.ParseText(source);
        var compilation = CSharpCompilation.Create(
            "TestAssembly",
            new[] { syntaxTree },
            TestHelpers.DynamicCompilationHelper.GetFluentDynamoDbReferences(),
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var semanticModel = compilation.GetSemanticModel(syntaxTree);
        
        // Get the first class declaration (TestEntity)
        var classDecl = syntaxTree.GetRoot()
            .DescendantNodes()
            .OfType<ClassDeclarationSyntax>()
            .First(c => c.Identifier.ValueText == "TestEntity");

        return (classDecl, semanticModel);
    }
}
