using FsCheck;
using FsCheck.Xunit;
using Microsoft.CodeAnalysis;
using Oproto.FluentDynamoDb.SourceGenerator.Analysis;
using Oproto.FluentDynamoDb.SourceGenerator.Models;
using System.Text.RegularExpressions;

namespace Oproto.FluentDynamoDb.SourceGenerator.UnitTests.Analysis;

/// <summary>
/// Property-based tests for single-name-wins logic in index aggregation.
/// 
/// **Feature: enhanced-index-table-generation, Property 4: Single specified name wins**
/// **Validates: Requirements 1.5**
/// </summary>
public class IndexSingleNameWinsPropertyTests
{
    /// <summary>
    /// Property: For any set of entities sharing a table where exactly one entity 
    /// specifies a Name for an index and others do not, the generated index property 
    /// SHALL use the specified name.
    /// </summary>
    [Property(MaxTest = 100)]
    public Property SingleNameSpecified_ShouldBeUsedForAllEntities()
    {
        return Prop.ForAll(
            Arb.From<NonEmptyString>(),
            Arb.From<NonEmptyString>(),
            Arb.From<PositiveInt>(),
            (indexName, customName, entityCount) =>
            {
                // Arrange - sanitize inputs
                var cleanIndexName = SanitizeToDynamoDbIndexName(indexName.Get);
                var cleanName = SanitizeToPropertyName(customName.Get);
                var count = Math.Min(entityCount.Get, 5) + 1; // 2-6 entities
                
                // Create entities - only the first one has a custom name
                var entities = new List<EntityModel>();
                entities.Add(CreateEntityWithIndex(cleanIndexName, cleanName, "Entity1"));
                
                for (int i = 2; i <= count; i++)
                {
                    entities.Add(CreateEntityWithIndex(cleanIndexName, null, $"Entity{i}"));
                }
                
                // Act
                var aggregator = new IndexAggregator();
                var aggregatedIndexes = aggregator.AggregateIndexes(entities);
                
                // Assert - the custom name should be used
                var aggregatedIndex = aggregatedIndexes.FirstOrDefault(ai => ai.DynamoDbIndexName == cleanIndexName);
                
                return aggregatedIndex != null &&
                       aggregatedIndex.CustomPropertyName == cleanName &&
                       aggregatedIndex.ResolvedPropertyName == cleanName &&
                       !aggregatedIndex.HasConflict;
            });
    }

    /// <summary>
    /// Property: For any set of entities where no entity specifies a Name,
    /// the resolved name should be derived from the DynamoDB index name.
    /// </summary>
    [Property(MaxTest = 100)]
    public Property NoNameSpecified_ShouldUseDerivedName()
    {
        return Prop.ForAll(
            Arb.From<NonEmptyString>(),
            Arb.From<PositiveInt>(),
            (indexName, entityCount) =>
            {
                // Arrange - sanitize inputs
                var cleanIndexName = SanitizeToDynamoDbIndexName(indexName.Get);
                var count = Math.Min(entityCount.Get, 5) + 1; // 2-6 entities
                var expectedDerivedName = IndexAggregator.DerivePropertyName(cleanIndexName);
                
                // Create entities - none have custom names
                var entities = new List<EntityModel>();
                for (int i = 1; i <= count; i++)
                {
                    entities.Add(CreateEntityWithIndex(cleanIndexName, null, $"Entity{i}"));
                }
                
                // Act
                var aggregator = new IndexAggregator();
                var aggregatedIndexes = aggregator.AggregateIndexes(entities);
                
                // Assert - the derived name should be used
                var aggregatedIndex = aggregatedIndexes.FirstOrDefault(ai => ai.DynamoDbIndexName == cleanIndexName);
                
                return aggregatedIndex != null &&
                       aggregatedIndex.CustomPropertyName == null &&
                       aggregatedIndex.ResolvedPropertyName == expectedDerivedName &&
                       !aggregatedIndex.HasConflict;
            });
    }

    /// <summary>
    /// Property: When multiple entities specify the same Name, the resolved name 
    /// should still be that name (with a redundancy warning).
    /// </summary>
    [Property(MaxTest = 100)]
    public Property SameNameSpecifiedMultipleTimes_ShouldStillUseThatName()
    {
        return Prop.ForAll(
            Arb.From<NonEmptyString>(),
            Arb.From<NonEmptyString>(),
            Arb.From<PositiveInt>(),
            (indexName, customName, entityCount) =>
            {
                // Arrange - sanitize inputs
                var cleanIndexName = SanitizeToDynamoDbIndexName(indexName.Get);
                var cleanName = SanitizeToPropertyName(customName.Get);
                var count = Math.Min(entityCount.Get, 4) + 2; // 2-6 entities
                
                // Create entities - all have the same custom name
                var entities = new List<EntityModel>();
                for (int i = 1; i <= count; i++)
                {
                    entities.Add(CreateEntityWithIndex(cleanIndexName, cleanName, $"Entity{i}"));
                }
                
                // Act
                var aggregator = new IndexAggregator();
                var aggregatedIndexes = aggregator.AggregateIndexes(entities);
                
                // Assert - the custom name should be used, with redundancy flag
                var aggregatedIndex = aggregatedIndexes.FirstOrDefault(ai => ai.DynamoDbIndexName == cleanIndexName);
                
                return aggregatedIndex != null &&
                       aggregatedIndex.CustomPropertyName == cleanName &&
                       aggregatedIndex.ResolvedPropertyName == cleanName &&
                       !aggregatedIndex.HasConflict &&
                       aggregatedIndex.HasRedundantSpecification;
            });
    }

    /// <summary>
    /// Property: When multiple entities specify the same Name, a FDDB052 warning 
    /// should be emitted.
    /// </summary>
    [Property(MaxTest = 100)]
    public Property SameNameSpecifiedMultipleTimes_ShouldEmitFDDB052Warning()
    {
        return Prop.ForAll(
            Arb.From<NonEmptyString>(),
            Arb.From<NonEmptyString>(),
            (indexName, customName) =>
            {
                // Arrange - sanitize inputs
                var cleanIndexName = SanitizeToDynamoDbIndexName(indexName.Get);
                var cleanName = SanitizeToPropertyName(customName.Get);
                
                // Create two entities with the same custom name
                var entity1 = CreateEntityWithIndex(cleanIndexName, cleanName, "Entity1");
                var entity2 = CreateEntityWithIndex(cleanIndexName, cleanName, "Entity2");
                var entities = new List<EntityModel> { entity1, entity2 };
                
                // Act
                var aggregator = new IndexAggregator();
                aggregator.AggregateIndexes(entities);
                
                // Assert - FDDB052 warning should be emitted
                var hasFddb052 = aggregator.Diagnostics.Any(d => d.Id == "FDDB052");
                var diagnosticContainsName = aggregator.Diagnostics
                    .Where(d => d.Id == "FDDB052")
                    .Any(d => d.GetMessage().Contains(cleanName));
                
                return hasFddb052 && diagnosticContainsName;
            });
    }

    /// <summary>
    /// Property: When only one entity specifies a Name, no FDDB052 warning should be emitted.
    /// </summary>
    [Property(MaxTest = 100)]
    public Property SingleNameSpecified_ShouldNotEmitFDDB052Warning()
    {
        return Prop.ForAll(
            Arb.From<NonEmptyString>(),
            Arb.From<NonEmptyString>(),
            (indexName, customName) =>
            {
                // Arrange - sanitize inputs
                var cleanIndexName = SanitizeToDynamoDbIndexName(indexName.Get);
                var cleanName = SanitizeToPropertyName(customName.Get);
                
                // Create one entity with custom name, one without
                var entity1 = CreateEntityWithIndex(cleanIndexName, cleanName, "Entity1");
                var entity2 = CreateEntityWithIndex(cleanIndexName, null, "Entity2");
                var entities = new List<EntityModel> { entity1, entity2 };
                
                // Act
                var aggregator = new IndexAggregator();
                aggregator.AggregateIndexes(entities);
                
                // Assert - no FDDB052 warning should be emitted
                var hasFddb052 = aggregator.Diagnostics.Any(d => d.Id == "FDDB052");
                
                return !hasFddb052;
            });
    }

    /// <summary>
    /// Property: ApplyResolvedNames should update all entity indexes with the resolved name.
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
                var cleanName = SanitizeToPropertyName(customName.Get);
                
                // Create entities - only first has custom name
                var entity1 = CreateEntityWithIndex(cleanIndexName, cleanName, "Entity1");
                var entity2 = CreateEntityWithIndex(cleanIndexName, null, "Entity2");
                var entity3 = CreateEntityWithIndex(cleanIndexName, null, "Entity3");
                var entities = new List<EntityModel> { entity1, entity2, entity3 };
                
                // Act
                var aggregator = new IndexAggregator();
                var aggregatedIndexes = aggregator.AggregateIndexes(entities);
                IndexAggregator.ApplyResolvedNames(entities, aggregatedIndexes);
                
                // Assert - all entities should have the same resolved name
                var allHaveSameResolvedName = entities.All(e =>
                    e.Indexes.First(i => i.IndexName == cleanIndexName).ResolvedPropertyName == cleanName);
                
                return allHaveSameResolvedName;
            });
    }

    /// <summary>
    /// Property: HasNoConflicts should return true when there are no conflicts.
    /// </summary>
    [Property(MaxTest = 100)]
    public Property HasNoConflicts_ShouldReturnTrueWhenNoConflicts()
    {
        return Prop.ForAll(
            Arb.From<NonEmptyString>(),
            Arb.From<NonEmptyString>(),
            (indexName, customName) =>
            {
                // Arrange - sanitize inputs
                var cleanIndexName = SanitizeToDynamoDbIndexName(indexName.Get);
                var cleanName = SanitizeToPropertyName(customName.Get);
                
                // Create entities with same or no custom name (no conflict)
                var entity1 = CreateEntityWithIndex(cleanIndexName, cleanName, "Entity1");
                var entity2 = CreateEntityWithIndex(cleanIndexName, null, "Entity2");
                var entities = new List<EntityModel> { entity1, entity2 };
                
                // Act
                var aggregator = new IndexAggregator();
                var aggregatedIndexes = aggregator.AggregateIndexes(entities);
                
                // Assert
                return IndexAggregator.HasNoConflicts(aggregatedIndexes);
            });
    }

    /// <summary>
    /// Property: HasNoConflicts should return false when there are conflicts.
    /// </summary>
    [Property(MaxTest = 100)]
    public Property HasNoConflicts_ShouldReturnFalseWhenConflicts()
    {
        return Prop.ForAll(
            Arb.From<NonEmptyString>(),
            Arb.From<NonEmptyString>(),
            Arb.From<NonEmptyString>(),
            (indexName, customName1, customName2) =>
            {
                // Arrange - sanitize inputs
                var cleanIndexName = SanitizeToDynamoDbIndexName(indexName.Get);
                var cleanName1 = SanitizeToPropertyName(customName1.Get);
                var cleanName2 = SanitizeToPropertyName(customName2.Get);
                
                // Ensure names are different for conflict
                if (cleanName1 == cleanName2)
                {
                    cleanName2 = cleanName2 + "Alt";
                }
                
                // Create entities with different custom names (conflict)
                var entity1 = CreateEntityWithIndex(cleanIndexName, cleanName1, "Entity1");
                var entity2 = CreateEntityWithIndex(cleanIndexName, cleanName2, "Entity2");
                var entities = new List<EntityModel> { entity1, entity2 };
                
                // Act
                var aggregator = new IndexAggregator();
                var aggregatedIndexes = aggregator.AggregateIndexes(entities);
                
                // Assert
                return !IndexAggregator.HasNoConflicts(aggregatedIndexes);
            });
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
