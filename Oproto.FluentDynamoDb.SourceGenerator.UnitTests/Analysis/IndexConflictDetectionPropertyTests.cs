using FsCheck;
using FsCheck.Xunit;
using Microsoft.CodeAnalysis;
using Oproto.FluentDynamoDb.SourceGenerator.Analysis;
using Oproto.FluentDynamoDb.SourceGenerator.Models;
using System.Text.RegularExpressions;

namespace Oproto.FluentDynamoDb.SourceGenerator.UnitTests.Analysis;

/// <summary>
/// Property-based tests for index name conflict detection.
/// 
/// **Feature: enhanced-index-table-generation, Property 3: Conflicting name diagnostic emission**
/// **Validates: Requirements 1.4, 5.2**
/// </summary>
public class IndexConflictDetectionPropertyTests
{
    /// <summary>
    /// Property: For any set of entities sharing a table where multiple entities define 
    /// the same DynamoDB index with different Name values, the source generator SHALL 
    /// emit exactly one FDDB050 diagnostic containing both conflicting values.
    /// </summary>
    [Property(MaxTest = 100)]
    public Property ConflictingNames_ShouldEmitFDDB050Diagnostic()
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
                
                // Create two entities with conflicting index names
                var entity1 = CreateEntityWithIndex(cleanIndexName, cleanName1, "Entity1");
                var entity2 = CreateEntityWithIndex(cleanIndexName, cleanName2, "Entity2");
                var entities = new List<EntityModel> { entity1, entity2 };
                
                // Act
                var aggregator = new IndexAggregator();
                var aggregatedIndexes = aggregator.AggregateIndexes(entities);
                
                // Assert
                var conflictingIndex = aggregatedIndexes.FirstOrDefault(ai => ai.DynamoDbIndexName == cleanIndexName);
                var hasFddb050 = aggregator.Diagnostics.Any(d => d.Id == "FDDB050");
                var diagnosticContainsBothNames = aggregator.Diagnostics
                    .Where(d => d.Id == "FDDB050")
                    .Any(d => 
                        d.GetMessage().Contains(cleanName1) && 
                        d.GetMessage().Contains(cleanName2));
                
                return conflictingIndex != null &&
                       conflictingIndex.HasConflict &&
                       hasFddb050 &&
                       diagnosticContainsBothNames;
            });
    }

    /// <summary>
    /// Property: For any set of entities sharing a table where all entities define 
    /// the same DynamoDB index with the same Name value (or no Name), no FDDB050 
    /// diagnostic should be emitted.
    /// </summary>
    [Property(MaxTest = 100)]
    public Property SameNames_ShouldNotEmitFDDB050Diagnostic()
    {
        return Prop.ForAll(
            Arb.From<NonEmptyString>(),
            Arb.From<NonEmptyString>(),
            (indexName, customName) =>
            {
                // Arrange - sanitize inputs
                var cleanIndexName = SanitizeToDynamoDbIndexName(indexName.Get);
                var cleanName = SanitizeToPropertyName(customName.Get);
                
                // Create two entities with the same index name
                var entity1 = CreateEntityWithIndex(cleanIndexName, cleanName, "Entity1");
                var entity2 = CreateEntityWithIndex(cleanIndexName, cleanName, "Entity2");
                var entities = new List<EntityModel> { entity1, entity2 };
                
                // Act
                var aggregator = new IndexAggregator();
                var aggregatedIndexes = aggregator.AggregateIndexes(entities);
                
                // Assert - no FDDB050 diagnostic should be emitted
                var hasFddb050 = aggregator.Diagnostics.Any(d => d.Id == "FDDB050");
                var conflictingIndex = aggregatedIndexes.FirstOrDefault(ai => ai.DynamoDbIndexName == cleanIndexName);
                
                return !hasFddb050 && 
                       conflictingIndex != null && 
                       !conflictingIndex.HasConflict;
            });
    }

    /// <summary>
    /// Property: For any set of entities where only one entity specifies a Name,
    /// no conflict diagnostic should be emitted.
    /// </summary>
    [Property(MaxTest = 100)]
    public Property SingleNameSpecified_ShouldNotEmitConflictDiagnostic()
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
                var aggregatedIndexes = aggregator.AggregateIndexes(entities);
                
                // Assert - no FDDB050 diagnostic should be emitted
                var hasFddb050 = aggregator.Diagnostics.Any(d => d.Id == "FDDB050");
                var aggregatedIndex = aggregatedIndexes.FirstOrDefault(ai => ai.DynamoDbIndexName == cleanIndexName);
                
                return !hasFddb050 && 
                       aggregatedIndex != null && 
                       !aggregatedIndex.HasConflict &&
                       aggregatedIndex.CustomPropertyName == cleanName;
            });
    }

    /// <summary>
    /// Property: For any set of entities where no entity specifies a Name,
    /// no conflict diagnostic should be emitted.
    /// </summary>
    [Property(MaxTest = 100)]
    public Property NoNamesSpecified_ShouldNotEmitConflictDiagnostic()
    {
        return Prop.ForAll(
            Arb.From<NonEmptyString>(),
            indexName =>
            {
                // Arrange - sanitize inputs
                var cleanIndexName = SanitizeToDynamoDbIndexName(indexName.Get);
                
                // Create two entities without custom names
                var entity1 = CreateEntityWithIndex(cleanIndexName, null, "Entity1");
                var entity2 = CreateEntityWithIndex(cleanIndexName, null, "Entity2");
                var entities = new List<EntityModel> { entity1, entity2 };
                
                // Act
                var aggregator = new IndexAggregator();
                var aggregatedIndexes = aggregator.AggregateIndexes(entities);
                
                // Assert - no FDDB050 diagnostic should be emitted
                var hasFddb050 = aggregator.Diagnostics.Any(d => d.Id == "FDDB050");
                var aggregatedIndex = aggregatedIndexes.FirstOrDefault(ai => ai.DynamoDbIndexName == cleanIndexName);
                
                return !hasFddb050 && 
                       aggregatedIndex != null && 
                       !aggregatedIndex.HasConflict &&
                       aggregatedIndex.CustomPropertyName == null;
            });
    }

    /// <summary>
    /// Property: The FDDB050 diagnostic message should contain the index name.
    /// </summary>
    [Property(MaxTest = 100)]
    public Property ConflictDiagnostic_ShouldContainIndexName()
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
                
                // Create two entities with conflicting index names
                var entity1 = CreateEntityWithIndex(cleanIndexName, cleanName1, "Entity1");
                var entity2 = CreateEntityWithIndex(cleanIndexName, cleanName2, "Entity2");
                var entities = new List<EntityModel> { entity1, entity2 };
                
                // Act
                var aggregator = new IndexAggregator();
                aggregator.AggregateIndexes(entities);
                
                // Assert - diagnostic message should contain the index name
                var fddb050Diagnostic = aggregator.Diagnostics.FirstOrDefault(d => d.Id == "FDDB050");
                
                return fddb050Diagnostic != null && 
                       fddb050Diagnostic.GetMessage().Contains(cleanIndexName);
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
