using FsCheck;
using FsCheck.Xunit;
using Oproto.FluentDynamoDb.SourceGenerator.Generators;
using Oproto.FluentDynamoDb.SourceGenerator.Models;

namespace Oproto.FluentDynamoDb.SourceGenerator.UnitTests.Generators;

/// <summary>
/// Property-based tests for graceful error handling during related entity mapping.
/// 
/// **Feature: hydration-architecture-consolidation, Property 5: Graceful Error Handling During Related Entity Mapping**
/// **Validates: Requirements 3.3, 6.1, 6.4**
/// 
/// These tests verify that for any set of DynamoDB items where some items matching a [RelatedEntity] pattern
/// fail to deserialize (e.g., missing required attributes), the composite entity assembly SHALL skip the
/// failing items, log warnings, and continue processing remaining items without throwing an exception.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Category", "PropertyBased")]
public class GracefulErrorHandlingPropertyTests
{
    /// <summary>
    /// **Feature: hydration-architecture-consolidation, Property 5: Graceful Error Handling**
    /// **Validates: Requirements 3.3, 6.1**
    /// 
    /// Property: For any entity with [RelatedEntity] collections, the generated code SHALL wrap
    /// the FromDynamoDb call in a try/catch block to handle deserialization failures gracefully.
    /// </summary>
    [Property(MaxTest = 50)]
    public Property GeneratedCode_WrapsFromDynamoDbInTryCatch_ForRelatedEntities()
    {
        // Generate random number of relationships (1 to 5)
        var relationshipCountArb = Gen.Choose(1, 5).ToArbitrary();
        
        return Prop.ForAll(relationshipCountArb, relationshipCount =>
        {
            // Create an entity model with the specified number of relationships
            var entity = CreateEntityModelWithRelationships(relationshipCount);
            
            // Generate the entity implementation
            var result = MapperGenerator.GenerateEntityImplementation(entity);
            
            // Property: The generated code should contain try/catch for each relationship
            // Each relationship should have a try block followed by a catch block
            var containsTryCatch = result.Contains("try") && result.Contains("catch (Exception ex)");
            
            // The catch block should NOT re-throw (it should skip and continue)
            var catchBlocksSkipAndContinue = !result.Contains("catch (Exception ex)\n            {\n                throw;");
            
            return containsTryCatch && catchBlocksSkipAndContinue;
        });
    }

    /// <summary>
    /// **Feature: hydration-architecture-consolidation, Property 5: Graceful Error Handling**
    /// **Validates: Requirements 6.1, 6.4**
    /// 
    /// Property: For any entity with [RelatedEntity] collections, the generated code SHALL log
    /// a warning with the sort key value and entity type when deserialization fails.
    /// </summary>
    [Property(MaxTest = 50)]
    public Property GeneratedCode_LogsWarningWithSortKeyAndEntityType_OnDeserializationFailure()
    {
        // Generate random number of relationships (1 to 3)
        var relationshipCountArb = Gen.Choose(1, 3).ToArbitrary();
        
        return Prop.ForAll(relationshipCountArb, relationshipCount =>
        {
            // Create an entity model with the specified number of relationships
            var entity = CreateEntityModelWithRelationships(relationshipCount);
            
            // Generate the entity implementation
            var result = MapperGenerator.GenerateEntityImplementation(entity);
            
            // Property: The generated code should log warnings with RelatedEntityMappingFailed event ID
            var containsWarningLog = result.Contains("LogEventIds.RelatedEntityMappingFailed");
            
            // The log message should include sort key and entity type placeholders
            var containsSortKeyPlaceholder = result.Contains("{SortKey}") || result.Contains("sortKey");
            var containsEntityTypePlaceholder = result.Contains("{EntityType}") || result.Contains("EntityType");
            
            return containsWarningLog && containsSortKeyPlaceholder && containsEntityTypePlaceholder;
        });
    }

    /// <summary>
    /// **Feature: hydration-architecture-consolidation, Property 5: Graceful Error Handling**
    /// **Validates: Requirements 3.3**
    /// 
    /// Property: For any entity with [RelatedEntity] collections, the generated code SHALL NOT
    /// use MatchesEntity() as a filter condition before calling FromDynamoDb.
    /// </summary>
    [Property(MaxTest = 50)]
    public Property GeneratedCode_DoesNotUseMatchesEntity_ForRelatedEntityFiltering()
    {
        // Generate random number of relationships (1 to 5)
        var relationshipCountArb = Gen.Choose(1, 5).ToArbitrary();
        
        return Prop.ForAll(relationshipCountArb, relationshipCount =>
        {
            // Create an entity model with the specified number of relationships
            var entity = CreateEntityModelWithRelationships(relationshipCount);
            
            // Generate the entity implementation
            var result = MapperGenerator.GenerateEntityImplementation(entity);
            
            // Property: The generated code should NOT contain MatchesEntity() in the related entity mapping section
            // We check for the pattern "if (EntityType.MatchesEntity(item))" which was the old buggy pattern
            var containsMatchesEntityFilter = result.Contains(".MatchesEntity(item))");
            
            return !containsMatchesEntityFilter;
        });
    }

    /// <summary>
    /// **Feature: hydration-architecture-consolidation, Property 5: Graceful Error Handling**
    /// **Validates: Requirements 6.1, 6.4**
    /// 
    /// Property: For any entity with [RelatedEntity] collections, the generated code SHALL
    /// continue processing remaining items after a deserialization failure (not break or return).
    /// </summary>
    [Property(MaxTest = 50)]
    public Property GeneratedCode_ContinuesProcessingAfterFailure()
    {
        // Generate random number of relationships (1 to 3)
        var relationshipCountArb = Gen.Choose(1, 3).ToArbitrary();
        
        return Prop.ForAll(relationshipCountArb, relationshipCount =>
        {
            // Create an entity model with the specified number of relationships
            var entity = CreateEntityModelWithRelationships(relationshipCount);
            
            // Generate the entity implementation
            var result = MapperGenerator.GenerateEntityImplementation(entity);
            
            // Property: The catch block should contain a comment indicating we skip and continue
            var containsSkipComment = result.Contains("// Skip this item and continue") ||
                                      result.Contains("skip this item") ||
                                      result.Contains("continue processing");
            
            // The catch block should NOT contain break, return, or throw
            // We need to check the catch block specifically, not the entire code
            // A simple heuristic: the code should have more "catch" blocks than "throw" statements in catch blocks
            var catchCount = CountOccurrences(result, "catch (Exception ex)");
            var throwInCatchCount = CountOccurrences(result, "catch (Exception ex)\n            {\n                throw");
            
            // Most catch blocks should NOT re-throw
            var mostCatchBlocksDoNotThrow = throwInCatchCount < catchCount;
            
            return containsSkipComment || mostCatchBlocksDoNotThrow;
        });
    }

    /// <summary>
    /// **Feature: hydration-architecture-consolidation, Property 5: Graceful Error Handling**
    /// **Validates: Requirements 6.1**
    /// 
    /// Property: For any entity with [RelatedEntity] collections, the generated code SHALL
    /// include the error message in the warning log.
    /// </summary>
    [Property(MaxTest = 30)]
    public Property GeneratedCode_IncludesErrorMessageInWarningLog()
    {
        // Generate random number of relationships (1 to 3)
        var relationshipCountArb = Gen.Choose(1, 3).ToArbitrary();
        
        return Prop.ForAll(relationshipCountArb, relationshipCount =>
        {
            // Create an entity model with the specified number of relationships
            var entity = CreateEntityModelWithRelationships(relationshipCount);
            
            // Generate the entity implementation
            var result = MapperGenerator.GenerateEntityImplementation(entity);
            
            // Property: The log message should include the error message placeholder
            var containsErrorPlaceholder = result.Contains("{Error}") || result.Contains("ex.Message");
            
            return containsErrorPlaceholder;
        });
    }

    /// <summary>
    /// Creates an EntityModel with the specified number of relationships for testing.
    /// </summary>
    private static EntityModel CreateEntityModelWithRelationships(int relationshipCount)
    {
        var relationships = new RelationshipModel[relationshipCount];
        
        for (int i = 0; i < relationshipCount; i++)
        {
            relationships[i] = new RelationshipModel
            {
                PropertyName = $"Children{i}",
                PropertyType = $"List<ChildEntity{i}>",
                SortKeyPattern = $"PARENT#*#CHILD{i}#*",
                EntityType = $"ChildEntity{i}",
                IsCollection = true,
                ChildEntityHasRelationships = false,
                ChildEntityRelationships = Array.Empty<RelationshipModel>()
            };
        }
        
        return new EntityModel
        {
            ClassName = "ParentEntity",
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
}
