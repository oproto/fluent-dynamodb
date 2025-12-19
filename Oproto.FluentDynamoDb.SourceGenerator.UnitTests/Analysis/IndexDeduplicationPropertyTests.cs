using FsCheck;
using FsCheck.Xunit;
using Oproto.FluentDynamoDb.SourceGenerator.Analysis;
using Oproto.FluentDynamoDb.SourceGenerator.Models;
using System.Text.RegularExpressions;
using Xunit;

namespace Oproto.FluentDynamoDb.SourceGenerator.UnitTests.Analysis;

/// <summary>
/// Property-based tests for index deduplication across entities.
/// 
/// **Feature: enhanced-index-table-generation, Property 11: Index deduplication across entities**
/// **Validates: Requirements 5.1, 5.3**
/// </summary>
public class IndexDeduplicationPropertyTests
{
    /// <summary>
    /// Property: For any set of entities sharing a table that define the same DynamoDB index name 
    /// with compatible configurations, the generated table class SHALL contain exactly one index 
    /// property for that index.
    /// </summary>
    [Property(MaxTest = 100)]
    public Property SameIndexName_ShouldProduceExactlyOneAggregatedIndex()
    {
        return Prop.ForAll(
            Arb.From<NonEmptyString>(),
            Arb.From<PositiveInt>(),
            (indexName, entityCount) =>
            {
                // Arrange - sanitize inputs
                var cleanIndexName = SanitizeToDynamoDbIndexName(indexName.Get);
                var numEntities = Math.Min(entityCount.Get, 10); // Cap at 10 entities
                
                // Create multiple entities with the same index name (no custom name - compatible)
                var entities = Enumerable.Range(1, numEntities)
                    .Select(i => CreateEntityWithIndex(cleanIndexName, null, $"Entity{i}"))
                    .ToList();
                
                // Act
                var aggregator = new IndexAggregator();
                var aggregatedIndexes = aggregator.AggregateIndexes(entities);
                
                // Assert - should produce exactly one aggregated index for this index name
                var indexesWithName = aggregatedIndexes.Where(ai => ai.DynamoDbIndexName == cleanIndexName).ToList();
                
                return indexesWithName.Count == 1;
            });
    }

    /// <summary>
    /// Property: For any set of entities sharing a table that define the same DynamoDB index name,
    /// the aggregated index should reference all entities that define it.
    /// </summary>
    [Property(MaxTest = 100)]
    public Property SameIndexName_ShouldReferenceAllEntities()
    {
        return Prop.ForAll(
            Arb.From<NonEmptyString>(),
            Arb.From<PositiveInt>(),
            (indexName, entityCount) =>
            {
                // Arrange - sanitize inputs
                var cleanIndexName = SanitizeToDynamoDbIndexName(indexName.Get);
                var numEntities = Math.Min(entityCount.Get, 10); // Cap at 10 entities
                
                // Create multiple entities with the same index name
                var entities = Enumerable.Range(1, numEntities)
                    .Select(i => CreateEntityWithIndex(cleanIndexName, null, $"Entity{i}"))
                    .ToList();
                
                // Act
                var aggregator = new IndexAggregator();
                var aggregatedIndexes = aggregator.AggregateIndexes(entities);
                
                // Assert - the aggregated index should reference all entities
                var aggregatedIndex = aggregatedIndexes.FirstOrDefault(ai => ai.DynamoDbIndexName == cleanIndexName);
                
                return aggregatedIndex != null && 
                       aggregatedIndex.ReferencingEntities.Count == numEntities;
            });
    }

    /// <summary>
    /// Property: For any set of entities with different index names, each index should be 
    /// aggregated separately.
    /// </summary>
    [Property(MaxTest = 100)]
    public Property DifferentIndexNames_ShouldProduceSeparateAggregatedIndexes()
    {
        return Prop.ForAll(
            Arb.From<NonEmptyString>(),
            Arb.From<NonEmptyString>(),
            (indexName1, indexName2) =>
            {
                // Arrange - sanitize inputs
                var cleanIndexName1 = SanitizeToDynamoDbIndexName(indexName1.Get);
                var cleanIndexName2 = SanitizeToDynamoDbIndexName(indexName2.Get);
                
                // Ensure names are different
                if (cleanIndexName1.Equals(cleanIndexName2, StringComparison.OrdinalIgnoreCase))
                {
                    cleanIndexName2 = cleanIndexName2 + "Alt";
                }
                
                // Create entities with different index names
                var entity1 = CreateEntityWithIndex(cleanIndexName1, null, "Entity1");
                var entity2 = CreateEntityWithIndex(cleanIndexName2, null, "Entity2");
                var entities = new List<EntityModel> { entity1, entity2 };
                
                // Act
                var aggregator = new IndexAggregator();
                var aggregatedIndexes = aggregator.AggregateIndexes(entities);
                
                // Assert - should produce two separate aggregated indexes
                var index1 = aggregatedIndexes.FirstOrDefault(ai => 
                    ai.DynamoDbIndexName.Equals(cleanIndexName1, StringComparison.OrdinalIgnoreCase));
                var index2 = aggregatedIndexes.FirstOrDefault(ai => 
                    ai.DynamoDbIndexName.Equals(cleanIndexName2, StringComparison.OrdinalIgnoreCase));
                
                return index1 != null && 
                       index2 != null && 
                       index1 != index2 &&
                       aggregatedIndexes.Count >= 2;
            });
    }

    /// <summary>
    /// Property: For any set of entities with compatible index configurations (same or no custom name),
    /// no conflict diagnostic should be emitted.
    /// </summary>
    [Property(MaxTest = 100)]
    public Property CompatibleIndexConfigurations_ShouldNotEmitConflictDiagnostic()
    {
        return Prop.ForAll(
            Arb.From<NonEmptyString>(),
            Arb.From<NonEmptyString>(),
            Arb.From<PositiveInt>(),
            (indexName, customName, entityCount) =>
            {
                // Arrange - sanitize inputs
                var cleanIndexName = SanitizeToDynamoDbIndexName(indexName.Get);
                var cleanCustomName = SanitizeToPropertyName(customName.Get);
                var numEntities = Math.Min(entityCount.Get, 5);
                
                // Create entities with the same custom name (compatible)
                var entities = Enumerable.Range(1, numEntities)
                    .Select(i => CreateEntityWithIndex(cleanIndexName, cleanCustomName, $"Entity{i}"))
                    .ToList();
                
                // Act
                var aggregator = new IndexAggregator();
                var aggregatedIndexes = aggregator.AggregateIndexes(entities);
                
                // Assert - no FDDB050 conflict diagnostic should be emitted
                var hasFddb050 = aggregator.Diagnostics.Any(d => d.Id == "FDDB050");
                var aggregatedIndex = aggregatedIndexes.FirstOrDefault(ai => ai.DynamoDbIndexName == cleanIndexName);
                
                return !hasFddb050 && 
                       aggregatedIndex != null && 
                       !aggregatedIndex.HasConflict;
            });
    }

    /// <summary>
    /// Property: For any set of entities where one specifies a custom name and others don't,
    /// the custom name should be used for all.
    /// </summary>
    [Property(MaxTest = 100)]
    public Property MixedCustomNameSpecification_ShouldUseSpecifiedName()
    {
        return Prop.ForAll(
            Arb.From<NonEmptyString>(),
            Arb.From<NonEmptyString>(),
            (indexName, customName) =>
            {
                // Arrange - sanitize inputs
                var cleanIndexName = SanitizeToDynamoDbIndexName(indexName.Get);
                var cleanCustomName = SanitizeToPropertyName(customName.Get);
                
                // Create one entity with custom name, others without
                var entity1 = CreateEntityWithIndex(cleanIndexName, cleanCustomName, "Entity1");
                var entity2 = CreateEntityWithIndex(cleanIndexName, null, "Entity2");
                var entity3 = CreateEntityWithIndex(cleanIndexName, null, "Entity3");
                var entities = new List<EntityModel> { entity1, entity2, entity3 };
                
                // Act
                var aggregator = new IndexAggregator();
                var aggregatedIndexes = aggregator.AggregateIndexes(entities);
                
                // Assert - the custom name should be used
                var aggregatedIndex = aggregatedIndexes.FirstOrDefault(ai => ai.DynamoDbIndexName == cleanIndexName);
                
                return aggregatedIndex != null && 
                       aggregatedIndex.CustomPropertyName == cleanCustomName &&
                       aggregatedIndex.ResolvedPropertyName == cleanCustomName &&
                       !aggregatedIndex.HasConflict;
            });
    }

    /// <summary>
    /// Property: The resolved property name should be applied to all entity indexes after aggregation.
    /// </summary>
    [Property(MaxTest = 100)]
    public Property ApplyResolvedNames_ShouldUpdateAllEntityIndexes()
    {
        return Prop.ForAll(
            Arb.From<NonEmptyString>(),
            Arb.From<NonEmptyString>(),
            (indexName, customName) =>
            {
                // Arrange - sanitize inputs
                var cleanIndexName = SanitizeToDynamoDbIndexName(indexName.Get);
                var cleanCustomName = SanitizeToPropertyName(customName.Get);
                
                // Create entities - one with custom name, one without
                var entity1 = CreateEntityWithIndex(cleanIndexName, cleanCustomName, "Entity1");
                var entity2 = CreateEntityWithIndex(cleanIndexName, null, "Entity2");
                var entities = new List<EntityModel> { entity1, entity2 };
                
                // Act
                var aggregator = new IndexAggregator();
                var aggregatedIndexes = aggregator.AggregateIndexes(entities);
                IndexAggregator.ApplyResolvedNames(entities, aggregatedIndexes);
                
                // Assert - all entity indexes should have the resolved name
                var allIndexesHaveResolvedName = entities.All(e =>
                    e.Indexes.Where(i => i.IndexName == cleanIndexName)
                        .All(i => i.ResolvedPropertyName == cleanCustomName));
                
                return allIndexesHaveResolvedName;
            });
    }

    /// <summary>
    /// Property: Index deduplication should be case-insensitive for DynamoDB index names.
    /// </summary>
    [Property(MaxTest = 100)]
    public Property IndexDeduplication_ShouldBeCaseInsensitive()
    {
        return Prop.ForAll(
            Arb.From<NonEmptyString>(),
            indexName =>
            {
                // Arrange - sanitize inputs
                var cleanIndexName = SanitizeToDynamoDbIndexName(indexName.Get);
                var upperCaseName = cleanIndexName.ToUpperInvariant();
                var lowerCaseName = cleanIndexName.ToLowerInvariant();
                
                // Create entities with different case variations
                var entity1 = CreateEntityWithIndex(upperCaseName, null, "Entity1");
                var entity2 = CreateEntityWithIndex(lowerCaseName, null, "Entity2");
                var entities = new List<EntityModel> { entity1, entity2 };
                
                // Act
                var aggregator = new IndexAggregator();
                var aggregatedIndexes = aggregator.AggregateIndexes(entities);
                
                // Assert - should produce exactly one aggregated index (case-insensitive)
                // Note: The aggregator uses StringComparer.OrdinalIgnoreCase
                var matchingIndexes = aggregatedIndexes.Where(ai => 
                    ai.DynamoDbIndexName.Equals(cleanIndexName, StringComparison.OrdinalIgnoreCase)).ToList();
                
                return matchingIndexes.Count == 1 && 
                       matchingIndexes[0].ReferencingEntities.Count == 2;
            });
    }

    /// <summary>
    /// Verifies that empty entity list produces empty aggregated indexes.
    /// </summary>
    [Fact]
    public void EmptyEntityList_ShouldProduceEmptyAggregatedIndexes()
    {
        // Arrange
        var entities = new List<EntityModel>();
        
        // Act
        var aggregator = new IndexAggregator();
        var aggregatedIndexes = aggregator.AggregateIndexes(entities);
        
        // Assert
        Assert.Empty(aggregatedIndexes);
        Assert.Empty(aggregator.Diagnostics);
    }

    /// <summary>
    /// Verifies that null entity list produces empty aggregated indexes.
    /// </summary>
    [Fact]
    public void NullEntityList_ShouldProduceEmptyAggregatedIndexes()
    {
        // Act
        var aggregator = new IndexAggregator();
        var aggregatedIndexes = aggregator.AggregateIndexes(null!);
        
        // Assert
        Assert.Empty(aggregatedIndexes);
        Assert.Empty(aggregator.Diagnostics);
    }

    /// <summary>
    /// Verifies that entity without indexes produces empty aggregated indexes.
    /// </summary>
    [Fact]
    public void EntityWithoutIndexes_ShouldProduceEmptyAggregatedIndexes()
    {
        // Arrange
        var entity = new EntityModel
        {
            ClassName = "TestEntity",
            Namespace = "Test",
            TableName = "TestTable",
            Indexes = Array.Empty<IndexModel>()
        };
        var entities = new List<EntityModel> { entity };
        
        // Act
        var aggregator = new IndexAggregator();
        var aggregatedIndexes = aggregator.AggregateIndexes(entities);
        
        // Assert
        Assert.Empty(aggregatedIndexes);
    }

    private static EntityModel CreateEntityWithIndex(string indexName, string? customName, string entityName)
    {
        var index = new IndexModel
        {
            IndexName = indexName,
            CustomName = customName,
            ResolvedPropertyName = customName ?? IndexAggregator.DerivePropertyName(indexName),
            IndexType = IndexType.GlobalSecondaryIndex,
            PartitionKeyProperty = "Pk"
        };

        return new EntityModel
        {
            ClassName = entityName,
            Namespace = "Test",
            TableName = "TestTable",
            Indexes = new[] { index }
        };
    }

    private static string SanitizeToDynamoDbIndexName(string name)
    {
        // DynamoDB index names: 3-255 characters, alphanumeric, hyphens, underscores
        var sanitized = Regex.Replace(name, @"[^a-zA-Z0-9_-]", "");
        if (string.IsNullOrEmpty(sanitized))
        {
            sanitized = "gsi1";
        }
        return sanitized.Length > 50 ? sanitized.Substring(0, 50) : sanitized;
    }

    private static string SanitizeToPropertyName(string name)
    {
        // C# property names: start with letter or underscore, alphanumeric and underscores
        var sanitized = Regex.Replace(name, @"[^a-zA-Z0-9_]", "");
        if (string.IsNullOrEmpty(sanitized) || !char.IsLetter(sanitized[0]))
        {
            sanitized = "Index" + sanitized;
        }
        return sanitized.Length > 50 ? sanitized.Substring(0, 50) : sanitized;
    }
}
