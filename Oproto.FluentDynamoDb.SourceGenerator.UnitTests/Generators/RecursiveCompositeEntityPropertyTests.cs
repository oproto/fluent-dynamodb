using FsCheck;
using FsCheck.Xunit;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Oproto.FluentDynamoDb.SourceGenerator.Analysis;
using Oproto.FluentDynamoDb.SourceGenerator.Generators;
using Oproto.FluentDynamoDb.SourceGenerator.Models;

namespace Oproto.FluentDynamoDb.SourceGenerator.UnitTests.Generators;

/// <summary>
/// Property-based tests for recursive composite entity assembly.
/// 
/// **Feature: hydration-architecture-consolidation, Property 4: Recursive Composite Entity Assembly**
/// **Validates: Requirements 7.1, 7.2, 7.3, 7.4, 7.5**
/// 
/// These tests verify that for any multi-level entity hierarchy (e.g., Location → OperatingHours → SpecialOverrides)
/// where each level has [RelatedEntity] attributes, calling ToCompositeEntityAsync() with items for all levels
/// SHALL recursively populate related collections at every level.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Category", "PropertyBased")]
public class RecursiveCompositeEntityPropertyTests
{
    /// <summary>
    /// **Feature: hydration-architecture-consolidation, Property 4: Recursive Composite Entity Assembly**
    /// **Validates: Requirements 7.1, 7.2**
    /// 
    /// Property: For any entity with [RelatedEntity] where the child entity also has [RelatedEntity],
    /// the EntityAnalyzer SHALL detect the nested relationships and set ChildEntityHasRelationships to true.
    /// </summary>
    [Property(MaxTest = 50)]
    public Property EntityAnalyzer_DetectsNestedRelationships_InChildEntities()
    {
        // Generate random hierarchy depth (2 to 4 levels)
        var depthArb = Gen.Choose(2, 4).ToArbitrary();
        
        return Prop.ForAll(depthArb, depth =>
        {
            // Generate source code with nested relationships
            var source = GenerateNestedEntityHierarchy(depth);
            
            // Parse and analyze
            var (classDecl, semanticModel) = ParseSource(source, "Level0Entity");
            var analyzer = new EntityAnalyzer();
            var result = analyzer.AnalyzeEntity(classDecl, semanticModel);
            
            // Property: The entity should have relationships detected
            if (result == null || result.Relationships.Length == 0)
                return false;
            
            // For depth > 1, the first relationship should have ChildEntityHasRelationships = true
            // because Level1Entity has its own [RelatedEntity] pointing to Level2Entity
            var firstRelationship = result.Relationships[0];
            
            // If depth is 2, Level1Entity has a relationship to Level2Entity (which has no children)
            // If depth is 3+, Level1Entity has a relationship to Level2Entity (which has children)
            if (depth > 2)
            {
                return firstRelationship.ChildEntityHasRelationships;
            }
            
            // For depth == 2, Level1Entity has no nested relationships
            return !firstRelationship.ChildEntityHasRelationships;
        });
    }

    /// <summary>
    /// **Feature: hydration-architecture-consolidation, Property 4: Recursive Composite Entity Assembly**
    /// **Validates: Requirements 7.1, 7.3**
    /// 
    /// Property: For any entity with nested [RelatedEntity] relationships, the generated code
    /// SHALL include the ExtractSortKeyPrefix helper method for grouping items during recursive assembly.
    /// </summary>
    [Property(MaxTest = 30)]
    public Property GeneratedCode_IncludesExtractSortKeyPrefixHelper_ForNestedRelationships()
    {
        // Generate random hierarchy depth (3 to 4 levels to ensure nested relationships)
        var depthArb = Gen.Choose(3, 4).ToArbitrary();
        
        return Prop.ForAll(depthArb, depth =>
        {
            // Create an entity model with nested relationships
            var entity = CreateEntityModelWithNestedRelationships(depth);
            
            // Generate the entity implementation
            var result = MapperGenerator.GenerateEntityImplementation(entity);
            
            // Property: The generated code should include the ExtractSortKeyPrefix helper
            var containsHelper = result.Contains("private static string ExtractSortKeyPrefix");
            
            return containsHelper;
        });
    }

    /// <summary>
    /// **Feature: hydration-architecture-consolidation, Property 4: Recursive Composite Entity Assembly**
    /// **Validates: Requirements 7.2, 7.3**
    /// 
    /// Property: For any entity with nested [RelatedEntity] relationships, the generated code
    /// SHALL use the multi-item FromDynamoDb overload for recursive assembly.
    /// </summary>
    [Property(MaxTest = 30)]
    public Property GeneratedCode_UsesMultiItemFromDynamoDb_ForRecursiveAssembly()
    {
        // Generate random hierarchy depth (3 to 4 levels)
        var depthArb = Gen.Choose(3, 4).ToArbitrary();
        
        return Prop.ForAll(depthArb, depth =>
        {
            // Create an entity model with nested relationships
            var entity = CreateEntityModelWithNestedRelationships(depth);
            
            // Generate the entity implementation
            var result = MapperGenerator.GenerateEntityImplementation(entity);
            
            // Property: The generated code should use multi-item FromDynamoDb for recursive assembly
            // This is indicated by passing a list of items to FromDynamoDb
            var containsRecursiveAssembly = result.Contains("FromDynamoDb<Level1Entity>(childItems, options)") ||
                                            result.Contains("FromDynamoDb<Level1Entity>(level1Items, options)");
            
            return containsRecursiveAssembly;
        });
    }

    /// <summary>
    /// **Feature: hydration-architecture-consolidation, Property 4: Recursive Composite Entity Assembly**
    /// **Validates: Requirements 7.4, 7.5**
    /// 
    /// Property: For any entity hierarchy with arbitrary depth (3+ levels), the generated code
    /// SHALL support recursive assembly at all levels without explicit depth limits.
    /// </summary>
    [Property(MaxTest = 20)]
    public Property GeneratedCode_SupportsArbitraryNestingDepth()
    {
        // Generate random hierarchy depth (3 to 5 levels)
        var depthArb = Gen.Choose(3, 5).ToArbitrary();
        
        return Prop.ForAll(depthArb, depth =>
        {
            // Generate source code with nested relationships
            var source = GenerateNestedEntityHierarchy(depth);
            
            // Parse and analyze each level
            var allLevelsHaveCorrectRelationships = true;
            
            for (int level = 0; level < depth - 1; level++)
            {
                var entityName = $"Level{level}Entity";
                var (classDecl, semanticModel) = ParseSource(source, entityName);
                var analyzer = new EntityAnalyzer();
                var result = analyzer.AnalyzeEntity(classDecl, semanticModel);
                
                if (result == null || result.Relationships.Length == 0)
                {
                    allLevelsHaveCorrectRelationships = false;
                    break;
                }
                
                // Each level (except the last) should have relationships
                var hasRelationship = result.Relationships.Length > 0;
                
                // For levels before the second-to-last, child should have relationships
                if (level < depth - 2)
                {
                    var childHasRelationships = result.Relationships[0].ChildEntityHasRelationships;
                    allLevelsHaveCorrectRelationships &= hasRelationship && childHasRelationships;
                }
                else
                {
                    // Second-to-last level's child (last level) has no relationships
                    allLevelsHaveCorrectRelationships &= hasRelationship;
                }
            }
            
            return allLevelsHaveCorrectRelationships;
        });
    }

    /// <summary>
    /// **Feature: hydration-architecture-consolidation, Property 4: Recursive Composite Entity Assembly**
    /// **Validates: Requirements 7.1, 7.2**
    /// 
    /// Property: For any entity with nested relationships, the generated code SHALL include
    /// item grouping logic using Dictionary to collect items for each child entity.
    /// </summary>
    [Property(MaxTest = 30)]
    public Property GeneratedCode_IncludesItemGroupingLogic_ForNestedRelationships()
    {
        // Generate random hierarchy depth (3 to 4 levels)
        var depthArb = Gen.Choose(3, 4).ToArbitrary();
        
        return Prop.ForAll(depthArb, depth =>
        {
            // Create an entity model with nested relationships
            var entity = CreateEntityModelWithNestedRelationships(depth);
            
            // Generate the entity implementation
            var result = MapperGenerator.GenerateEntityImplementation(entity);
            
            // Property: The generated code should include Dictionary for grouping items
            var containsItemGroups = result.Contains("ItemGroups = new Dictionary<string, List<Dictionary<string, AttributeValue>>>");
            
            return containsItemGroups;
        });
    }

    /// <summary>
    /// Generates source code for a nested entity hierarchy with the specified depth.
    /// </summary>
    private static string GenerateNestedEntityHierarchy(int depth)
    {
        var entityClasses = new System.Text.StringBuilder();
        
        for (int level = 0; level < depth; level++)
        {
            var entityName = $"Level{level}Entity";
            var hasRelatedEntity = level < depth - 1;
            var childEntityName = hasRelatedEntity ? $"Level{level + 1}Entity" : null;
            
            var relatedEntityProperty = hasRelatedEntity
                ? $@"
        [RelatedEntity(""LEVEL{level}#*#LEVEL{level + 1}#*"", EntityType = typeof({childEntityName}))]
        public List<{childEntityName}>? Children {{ get; set; }}"
                : "";
            
            entityClasses.AppendLine($@"
    [DynamoDbTable(""test-table""{(level == 0 ? ", IsDefault = true" : "")})]
    public partial class {entityName}
    {{
        [PartitionKey]
        [DynamoDbAttribute(""pk"")]
        public string Pk {{ get; set; }} = string.Empty;
        
        [SortKey]
        [DynamoDbAttribute(""sk"")]
        public string Sk {{ get; set; }} = string.Empty;
        
        [DynamoDbAttribute(""name"")]
        public string Name {{ get; set; }} = string.Empty;
{relatedEntityProperty}
    }}");
        }
        
        return $@"
using Oproto.FluentDynamoDb.Attributes;
using System.Collections.Generic;

namespace TestNamespace
{{
{entityClasses}
}}";
    }

    /// <summary>
    /// Creates an EntityModel with nested relationships for testing code generation.
    /// </summary>
    private static EntityModel CreateEntityModelWithNestedRelationships(int depth)
    {
        // Create child relationships recursively
        RelationshipModel[] CreateChildRelationships(int currentLevel, int maxDepth)
        {
            if (currentLevel >= maxDepth - 1)
                return Array.Empty<RelationshipModel>();
            
            var childRelationships = CreateChildRelationships(currentLevel + 1, maxDepth);
            
            return new[]
            {
                new RelationshipModel
                {
                    PropertyName = "Children",
                    PropertyType = $"List<Level{currentLevel + 1}Entity>",
                    SortKeyPattern = $"LEVEL{currentLevel}#*#LEVEL{currentLevel + 1}#*",
                    EntityType = $"Level{currentLevel + 1}Entity",
                    IsCollection = true,
                    ChildEntityHasRelationships = childRelationships.Length > 0,
                    ChildEntityRelationships = childRelationships
                }
            };
        }
        
        var relationships = CreateChildRelationships(0, depth);
        
        return new EntityModel
        {
            ClassName = "Level0Entity",
            Namespace = "TestNamespace",
            TableName = "test-table",
            IsDefault = true,
            IsMultiItemEntity = true,
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
                    PropertyName = "Name",
                    AttributeName = "name",
                    PropertyType = "string"
                }
            },
            Relationships = relationships
        };
    }

    /// <summary>
    /// Parses source code and returns the specified class declaration with semantic model.
    /// </summary>
    private static (ClassDeclarationSyntax ClassDecl, SemanticModel SemanticModel) ParseSource(string source, string className)
    {
        var syntaxTree = CSharpSyntaxTree.ParseText(source);
        var compilation = CSharpCompilation.Create(
            "TestAssembly",
            new[] { syntaxTree },
            TestHelpers.DynamicCompilationHelper.GetFluentDynamoDbReferences(),
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var semanticModel = compilation.GetSemanticModel(syntaxTree);
        
        // Get the specified class declaration
        var classDecl = syntaxTree.GetRoot()
            .DescendantNodes()
            .OfType<ClassDeclarationSyntax>()
            .First(c => c.Identifier.ValueText == className);

        return (classDecl, semanticModel);
    }
}
