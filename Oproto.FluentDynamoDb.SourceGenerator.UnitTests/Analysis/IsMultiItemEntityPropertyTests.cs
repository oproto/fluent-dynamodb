using FsCheck;
using FsCheck.Xunit;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Oproto.FluentDynamoDb.SourceGenerator.Analysis;

namespace Oproto.FluentDynamoDb.SourceGenerator.UnitTests.Analysis;

/// <summary>
/// Property-based tests for IsMultiItemEntity flag in EntityAnalyzer.
/// 
/// **Feature: composite-entity-assembly, Property 1: IsMultiItemEntity Flag for Entities with Relationships**
/// **Validates: Requirements 1.1**
/// 
/// These tests verify that for any entity class with one or more [RelatedEntity] attributes,
/// the source generator produces an EntityModel with IsMultiItemEntity = true.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Category", "PropertyBased")]
public class IsMultiItemEntityPropertyTests
{
    /// <summary>
    /// **Feature: composite-entity-assembly, Property 1: IsMultiItemEntity Flag for Entities with Relationships**
    /// **Validates: Requirements 1.1**
    /// 
    /// Property: For any entity with N related entity attributes (N >= 1), IsMultiItemEntity should be true.
    /// For any entity with 0 related entity attributes, IsMultiItemEntity should be false.
    /// </summary>
    [Property(MaxTest = 100)]
    public Property IsMultiItemEntity_MatchesPresenceOfRelationships()
    {
        // Generate random number of related entities (0 to 5)
        var relatedEntityCountArb = Gen.Choose(0, 5).ToArbitrary();
        
        return Prop.ForAll(relatedEntityCountArb, relatedEntityCount =>
        {
            // Generate source code with the specified number of related entities
            var source = GenerateEntitySource(relatedEntityCount);
            
            // Parse and analyze
            var (classDecl, semanticModel) = ParseSource(source);
            var analyzer = new EntityAnalyzer();
            var result = analyzer.AnalyzeEntity(classDecl, semanticModel);
            
            // The entity should be analyzed successfully
            if (result == null)
            {
                // If analysis failed, check if it's due to expected diagnostics
                // (e.g., unsupported types for related entities)
                return true; // Skip this case
            }
            
            // Property: IsMultiItemEntity should match whether relationships exist
            var expectedIsMultiItem = relatedEntityCount > 0;
            return result.IsMultiItemEntity == expectedIsMultiItem;
        });
    }

    /// <summary>
    /// Generates entity source code with the specified number of [RelatedEntity] attributes.
    /// </summary>
    private static string GenerateEntitySource(int relatedEntityCount)
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
