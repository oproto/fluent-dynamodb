using FsCheck;
using FsCheck.Xunit;
using Microsoft.CodeAnalysis;
using Oproto.FluentDynamoDb.SourceGenerator.Analysis;
using Oproto.FluentDynamoDb.SourceGenerator.Models;
using System.Text.RegularExpressions;

namespace Oproto.FluentDynamoDb.SourceGenerator.UnitTests.Analysis;

/// <summary>
/// Property-based tests for index configuration conflict detection.
/// 
/// **Feature: multi-entity-index-consolidation**
/// Tests Properties 1, 2, and 3 for configuration conflict detection.
/// </summary>
public class IndexConfigurationConflictPropertyTests
{
    /// <summary>
    /// **Property 1: Conflicting partition key diagnostic emission**
    /// **Validates: Requirements 2.1, 2.4**
    /// 
    /// Property: For any set of entities sharing a table where multiple entities define 
    /// the same DynamoDB index with different partition keys, the source generator SHALL 
    /// emit exactly one FDDB053 diagnostic containing both entity names and conflicting property names.
    /// </summary>
    [Property(MaxTest = 100)]
    public Property ConflictingPartitionKeys_ShouldEmitFDDB053Diagnostic()
    {
        return Prop.ForAll(
            Arb.From<NonEmptyString>(),
            Arb.From<NonEmptyString>(),
            Arb.From<NonEmptyString>(),
            (indexName, pk1, pk2) =>
            {
                // Arrange - sanitize inputs
                var cleanIndexName = SanitizeToDynamoDbIndexName(indexName.Get);
                var cleanPk1 = SanitizeToPropertyName(pk1.Get);
                var cleanPk2 = SanitizeToPropertyName(pk2.Get);
                
                // Ensure partition keys are different for conflict
                if (cleanPk1 == cleanPk2)
                {
                    cleanPk2 = cleanPk2 + "Alt";
                }
                
                // Create two entities with conflicting partition keys
                var entity1 = CreateEntityWithIndex(cleanIndexName, cleanPk1, null, IndexType.GlobalSecondaryIndex, "Entity1");
                var entity2 = CreateEntityWithIndex(cleanIndexName, cleanPk2, null, IndexType.GlobalSecondaryIndex, "Entity2");
                var entities = new List<EntityModel> { entity1, entity2 };
                
                // Act
                var aggregator = new IndexAggregator();
                var aggregatedIndexes = aggregator.AggregateIndexes(entities);
                
                // Assert
                var conflictingIndex = aggregatedIndexes.FirstOrDefault(ai => ai.DynamoDbIndexName == cleanIndexName);
                var hasFddb053 = aggregator.Diagnostics.Any(d => d.Id == "FDDB053");
                var diagnosticContainsInfo = aggregator.Diagnostics
                    .Where(d => d.Id == "FDDB053")
                    .Any(d => 
                        d.GetMessage().Contains(cleanPk1) && 
                        d.GetMessage().Contains(cleanPk2) &&
                        d.GetMessage().Contains("Entity1") &&
                        d.GetMessage().Contains("Entity2"));
                
                return conflictingIndex != null &&
                       conflictingIndex.HasConfigurationConflict &&
                       hasFddb053 &&
                       diagnosticContainsInfo;
            });
    }

    /// <summary>
    /// Property: For any set of entities sharing a table where all entities define 
    /// the same DynamoDB index with the same partition key, no FDDB053 diagnostic should be emitted.
    /// </summary>
    [Property(MaxTest = 100)]
    public Property SamePartitionKeys_ShouldNotEmitFDDB053Diagnostic()
    {
        return Prop.ForAll(
            Arb.From<NonEmptyString>(),
            Arb.From<NonEmptyString>(),
            (indexName, pk) =>
            {
                // Arrange - sanitize inputs
                var cleanIndexName = SanitizeToDynamoDbIndexName(indexName.Get);
                var cleanPk = SanitizeToPropertyName(pk.Get);
                
                // Create two entities with the same partition key
                var entity1 = CreateEntityWithIndex(cleanIndexName, cleanPk, null, IndexType.GlobalSecondaryIndex, "Entity1");
                var entity2 = CreateEntityWithIndex(cleanIndexName, cleanPk, null, IndexType.GlobalSecondaryIndex, "Entity2");
                var entities = new List<EntityModel> { entity1, entity2 };
                
                // Act
                var aggregator = new IndexAggregator();
                var aggregatedIndexes = aggregator.AggregateIndexes(entities);
                
                // Assert - no FDDB053 diagnostic should be emitted
                var hasFddb053 = aggregator.Diagnostics.Any(d => d.Id == "FDDB053");
                var conflictingIndex = aggregatedIndexes.FirstOrDefault(ai => ai.DynamoDbIndexName == cleanIndexName);
                
                // Check that there's no partition key conflict (there might be other conflicts)
                var hasPkConflict = conflictingIndex?.ConfigurationConflictDetails.Any(d => d.StartsWith("PK:")) ?? false;
                
                return !hasFddb053 && !hasPkConflict;
            });
    }

    private static EntityModel CreateEntityWithIndex(
        string indexName, 
        string partitionKey, 
        string? sortKey, 
        IndexType indexType, 
        string entityName)
    {
        var index = new IndexModel
        {
            IndexName = indexName,
            PartitionKeyProperty = partitionKey,
            PartitionKeyAttribute = partitionKey, // Use same value for attribute name in tests
            SortKeyProperty = sortKey,
            SortKeyAttribute = sortKey, // Use same value for attribute name in tests
            IndexType = indexType,
            ResolvedPropertyName = IndexAggregator.DerivePropertyName(indexName)
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
            sanitized = "Prop" + sanitized;
        }
        return sanitized.Length > 50 ? sanitized.Substring(0, 50) : sanitized;
    }
}


/// <summary>
/// Property-based tests for sort key configuration conflict detection.
/// 
/// **Feature: multi-entity-index-consolidation, Property 2: Conflicting sort key diagnostic emission**
/// **Validates: Requirements 2.2, 2.4**
/// </summary>
public class IndexSortKeyConflictPropertyTests
{
    /// <summary>
    /// **Property 2: Conflicting sort key diagnostic emission**
    /// **Validates: Requirements 2.2, 2.4**
    /// 
    /// Property: For any set of entities sharing a table where multiple entities define 
    /// the same DynamoDB index with different sort keys, the source generator SHALL 
    /// emit exactly one FDDB054 diagnostic containing both entity names and conflicting property names.
    /// </summary>
    [Property(MaxTest = 100)]
    public Property ConflictingSortKeys_ShouldEmitFDDB054Diagnostic()
    {
        return Prop.ForAll(
            Arb.From<NonEmptyString>(),
            Arb.From<NonEmptyString>(),
            Arb.From<NonEmptyString>(),
            (indexName, sk1, sk2) =>
            {
                // Arrange - sanitize inputs
                var cleanIndexName = SanitizeToDynamoDbIndexName(indexName.Get);
                var cleanSk1 = SanitizeToPropertyName(sk1.Get);
                var cleanSk2 = SanitizeToPropertyName(sk2.Get);
                
                // Ensure sort keys are different for conflict
                if (cleanSk1 == cleanSk2)
                {
                    cleanSk2 = cleanSk2 + "Alt";
                }
                
                // Create two entities with conflicting sort keys (same partition key)
                var entity1 = CreateEntityWithIndex(cleanIndexName, "Pk", cleanSk1, IndexType.GlobalSecondaryIndex, "Entity1");
                var entity2 = CreateEntityWithIndex(cleanIndexName, "Pk", cleanSk2, IndexType.GlobalSecondaryIndex, "Entity2");
                var entities = new List<EntityModel> { entity1, entity2 };
                
                // Act
                var aggregator = new IndexAggregator();
                var aggregatedIndexes = aggregator.AggregateIndexes(entities);
                
                // Assert
                var conflictingIndex = aggregatedIndexes.FirstOrDefault(ai => ai.DynamoDbIndexName == cleanIndexName);
                var hasFddb054 = aggregator.Diagnostics.Any(d => d.Id == "FDDB054");
                var diagnosticContainsInfo = aggregator.Diagnostics
                    .Where(d => d.Id == "FDDB054")
                    .Any(d => 
                        d.GetMessage().Contains(cleanSk1) && 
                        d.GetMessage().Contains(cleanSk2) &&
                        d.GetMessage().Contains("Entity1") &&
                        d.GetMessage().Contains("Entity2"));
                
                return conflictingIndex != null &&
                       conflictingIndex.HasConfigurationConflict &&
                       hasFddb054 &&
                       diagnosticContainsInfo;
            });
    }

    /// <summary>
    /// Property: For any set of entities where one has a sort key and one doesn't,
    /// the source generator SHALL emit FDDB054 diagnostic.
    /// </summary>
    [Property(MaxTest = 100)]
    public Property SortKeyVsNoSortKey_ShouldEmitFDDB054Diagnostic()
    {
        return Prop.ForAll(
            Arb.From<NonEmptyString>(),
            Arb.From<NonEmptyString>(),
            (indexName, sk) =>
            {
                // Arrange - sanitize inputs
                var cleanIndexName = SanitizeToDynamoDbIndexName(indexName.Get);
                var cleanSk = SanitizeToPropertyName(sk.Get);
                
                // Create one entity with sort key, one without
                var entity1 = CreateEntityWithIndex(cleanIndexName, "Pk", cleanSk, IndexType.GlobalSecondaryIndex, "Entity1");
                var entity2 = CreateEntityWithIndex(cleanIndexName, "Pk", null, IndexType.GlobalSecondaryIndex, "Entity2");
                var entities = new List<EntityModel> { entity1, entity2 };
                
                // Act
                var aggregator = new IndexAggregator();
                var aggregatedIndexes = aggregator.AggregateIndexes(entities);
                
                // Assert
                var conflictingIndex = aggregatedIndexes.FirstOrDefault(ai => ai.DynamoDbIndexName == cleanIndexName);
                var hasFddb054 = aggregator.Diagnostics.Any(d => d.Id == "FDDB054");
                
                return conflictingIndex != null &&
                       conflictingIndex.HasConfigurationConflict &&
                       hasFddb054;
            });
    }

    /// <summary>
    /// Property: For any set of entities sharing a table where all entities define 
    /// the same DynamoDB index with the same sort key (or all have no sort key), 
    /// no FDDB054 diagnostic should be emitted.
    /// </summary>
    [Property(MaxTest = 100)]
    public Property SameSortKeys_ShouldNotEmitFDDB054Diagnostic()
    {
        return Prop.ForAll(
            Arb.From<NonEmptyString>(),
            Arb.From<NonEmptyString>(),
            (indexName, sk) =>
            {
                // Arrange - sanitize inputs
                var cleanIndexName = SanitizeToDynamoDbIndexName(indexName.Get);
                var cleanSk = SanitizeToPropertyName(sk.Get);
                
                // Create two entities with the same sort key
                var entity1 = CreateEntityWithIndex(cleanIndexName, "Pk", cleanSk, IndexType.GlobalSecondaryIndex, "Entity1");
                var entity2 = CreateEntityWithIndex(cleanIndexName, "Pk", cleanSk, IndexType.GlobalSecondaryIndex, "Entity2");
                var entities = new List<EntityModel> { entity1, entity2 };
                
                // Act
                var aggregator = new IndexAggregator();
                var aggregatedIndexes = aggregator.AggregateIndexes(entities);
                
                // Assert - no FDDB054 diagnostic should be emitted
                var hasFddb054 = aggregator.Diagnostics.Any(d => d.Id == "FDDB054");
                var conflictingIndex = aggregatedIndexes.FirstOrDefault(ai => ai.DynamoDbIndexName == cleanIndexName);
                
                // Check that there's no sort key conflict
                var hasSkConflict = conflictingIndex?.ConfigurationConflictDetails.Any(d => d.StartsWith("SK:")) ?? false;
                
                return !hasFddb054 && !hasSkConflict;
            });
    }

    /// <summary>
    /// Property: For any set of entities where both have no sort key,
    /// no FDDB054 diagnostic should be emitted.
    /// </summary>
    [Property(MaxTest = 100)]
    public Property BothNoSortKey_ShouldNotEmitFDDB054Diagnostic()
    {
        return Prop.ForAll(
            Arb.From<NonEmptyString>(),
            indexName =>
            {
                // Arrange - sanitize inputs
                var cleanIndexName = SanitizeToDynamoDbIndexName(indexName.Get);
                
                // Create two entities with no sort key
                var entity1 = CreateEntityWithIndex(cleanIndexName, "Pk", null, IndexType.GlobalSecondaryIndex, "Entity1");
                var entity2 = CreateEntityWithIndex(cleanIndexName, "Pk", null, IndexType.GlobalSecondaryIndex, "Entity2");
                var entities = new List<EntityModel> { entity1, entity2 };
                
                // Act
                var aggregator = new IndexAggregator();
                var aggregatedIndexes = aggregator.AggregateIndexes(entities);
                
                // Assert - no FDDB054 diagnostic should be emitted
                var hasFddb054 = aggregator.Diagnostics.Any(d => d.Id == "FDDB054");
                var conflictingIndex = aggregatedIndexes.FirstOrDefault(ai => ai.DynamoDbIndexName == cleanIndexName);
                
                // Check that there's no sort key conflict
                var hasSkConflict = conflictingIndex?.ConfigurationConflictDetails.Any(d => d.StartsWith("SK:")) ?? false;
                
                return !hasFddb054 && !hasSkConflict;
            });
    }

    private static EntityModel CreateEntityWithIndex(
        string indexName, 
        string partitionKey, 
        string? sortKey, 
        IndexType indexType, 
        string entityName)
    {
        var index = new IndexModel
        {
            IndexName = indexName,
            PartitionKeyProperty = partitionKey,
            PartitionKeyAttribute = partitionKey, // Use same value for attribute name in tests
            SortKeyProperty = sortKey,
            SortKeyAttribute = sortKey, // Use same value for attribute name in tests
            IndexType = indexType,
            ResolvedPropertyName = IndexAggregator.DerivePropertyName(indexName)
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
        var sanitized = Regex.Replace(name, @"[^a-zA-Z0-9_-]", "");
        if (string.IsNullOrEmpty(sanitized))
        {
            sanitized = "gsi1";
        }
        return sanitized.Length > 50 ? sanitized.Substring(0, 50) : sanitized;
    }

    private static string SanitizeToPropertyName(string name)
    {
        var sanitized = Regex.Replace(name, @"[^a-zA-Z0-9_]", "");
        if (string.IsNullOrEmpty(sanitized) || !char.IsLetter(sanitized[0]))
        {
            sanitized = "Prop" + sanitized;
        }
        return sanitized.Length > 50 ? sanitized.Substring(0, 50) : sanitized;
    }
}


/// <summary>
/// Property-based tests for index type configuration conflict detection.
/// 
/// **Feature: multi-entity-index-consolidation, Property 3: Conflicting index type diagnostic emission**
/// **Validates: Requirements 2.3, 2.4**
/// </summary>
public class IndexTypeConflictPropertyTests
{
    /// <summary>
    /// **Property 3: Conflicting index type diagnostic emission**
    /// **Validates: Requirements 2.3, 2.4**
    /// 
    /// Property: For any set of entities sharing a table where multiple entities define 
    /// the same DynamoDB index with different index types (GSI vs LSI), the source generator SHALL 
    /// emit exactly one FDDB055 diagnostic containing both entity names and conflicting types.
    /// </summary>
    [Property(MaxTest = 100)]
    public Property ConflictingIndexTypes_ShouldEmitFDDB055Diagnostic()
    {
        return Prop.ForAll(
            Arb.From<NonEmptyString>(),
            indexName =>
            {
                // Arrange - sanitize inputs
                var cleanIndexName = SanitizeToDynamoDbIndexName(indexName.Get);
                
                // Create two entities with conflicting index types (GSI vs LSI)
                var entity1 = CreateEntityWithIndex(cleanIndexName, "Pk", "Sk", IndexType.GlobalSecondaryIndex, "Entity1");
                var entity2 = CreateEntityWithIndex(cleanIndexName, "Pk", "Sk", IndexType.LocalSecondaryIndex, "Entity2");
                var entities = new List<EntityModel> { entity1, entity2 };
                
                // Act
                var aggregator = new IndexAggregator();
                var aggregatedIndexes = aggregator.AggregateIndexes(entities);
                
                // Assert
                var conflictingIndex = aggregatedIndexes.FirstOrDefault(ai => ai.DynamoDbIndexName == cleanIndexName);
                var hasFddb055 = aggregator.Diagnostics.Any(d => d.Id == "FDDB055");
                var diagnosticContainsInfo = aggregator.Diagnostics
                    .Where(d => d.Id == "FDDB055")
                    .Any(d => 
                        d.GetMessage().Contains("GlobalSecondaryIndex") && 
                        d.GetMessage().Contains("LocalSecondaryIndex") &&
                        d.GetMessage().Contains("Entity1") &&
                        d.GetMessage().Contains("Entity2"));
                
                return conflictingIndex != null &&
                       conflictingIndex.HasConfigurationConflict &&
                       hasFddb055 &&
                       diagnosticContainsInfo;
            });
    }

    /// <summary>
    /// Property: For any set of entities sharing a table where all entities define 
    /// the same DynamoDB index with the same index type (all GSI or all LSI), 
    /// no FDDB055 diagnostic should be emitted.
    /// </summary>
    [Property(MaxTest = 100)]
    public Property SameIndexType_GSI_ShouldNotEmitFDDB055Diagnostic()
    {
        return Prop.ForAll(
            Arb.From<NonEmptyString>(),
            indexName =>
            {
                // Arrange - sanitize inputs
                var cleanIndexName = SanitizeToDynamoDbIndexName(indexName.Get);
                
                // Create two entities with the same index type (both GSI)
                var entity1 = CreateEntityWithIndex(cleanIndexName, "Pk", "Sk", IndexType.GlobalSecondaryIndex, "Entity1");
                var entity2 = CreateEntityWithIndex(cleanIndexName, "Pk", "Sk", IndexType.GlobalSecondaryIndex, "Entity2");
                var entities = new List<EntityModel> { entity1, entity2 };
                
                // Act
                var aggregator = new IndexAggregator();
                var aggregatedIndexes = aggregator.AggregateIndexes(entities);
                
                // Assert - no FDDB055 diagnostic should be emitted
                var hasFddb055 = aggregator.Diagnostics.Any(d => d.Id == "FDDB055");
                var conflictingIndex = aggregatedIndexes.FirstOrDefault(ai => ai.DynamoDbIndexName == cleanIndexName);
                
                // Check that there's no type conflict
                var hasTypeConflict = conflictingIndex?.ConfigurationConflictDetails.Any(d => d.StartsWith("TYPE:")) ?? false;
                
                return !hasFddb055 && !hasTypeConflict;
            });
    }

    /// <summary>
    /// Property: For any set of entities sharing a table where all entities define 
    /// the same DynamoDB index with the same index type (all LSI), 
    /// no FDDB055 diagnostic should be emitted.
    /// </summary>
    [Property(MaxTest = 100)]
    public Property SameIndexType_LSI_ShouldNotEmitFDDB055Diagnostic()
    {
        return Prop.ForAll(
            Arb.From<NonEmptyString>(),
            indexName =>
            {
                // Arrange - sanitize inputs
                var cleanIndexName = SanitizeToDynamoDbIndexName(indexName.Get);
                
                // Create two entities with the same index type (both LSI)
                var entity1 = CreateEntityWithIndex(cleanIndexName, "Pk", "Sk", IndexType.LocalSecondaryIndex, "Entity1");
                var entity2 = CreateEntityWithIndex(cleanIndexName, "Pk", "Sk", IndexType.LocalSecondaryIndex, "Entity2");
                var entities = new List<EntityModel> { entity1, entity2 };
                
                // Act
                var aggregator = new IndexAggregator();
                var aggregatedIndexes = aggregator.AggregateIndexes(entities);
                
                // Assert - no FDDB055 diagnostic should be emitted
                var hasFddb055 = aggregator.Diagnostics.Any(d => d.Id == "FDDB055");
                var conflictingIndex = aggregatedIndexes.FirstOrDefault(ai => ai.DynamoDbIndexName == cleanIndexName);
                
                // Check that there's no type conflict
                var hasTypeConflict = conflictingIndex?.ConfigurationConflictDetails.Any(d => d.StartsWith("TYPE:")) ?? false;
                
                return !hasFddb055 && !hasTypeConflict;
            });
    }

    private static EntityModel CreateEntityWithIndex(
        string indexName, 
        string partitionKey, 
        string? sortKey, 
        IndexType indexType, 
        string entityName)
    {
        var index = new IndexModel
        {
            IndexName = indexName,
            PartitionKeyProperty = partitionKey,
            PartitionKeyAttribute = partitionKey, // Use same value for attribute name in tests
            SortKeyProperty = sortKey,
            SortKeyAttribute = sortKey, // Use same value for attribute name in tests
            IndexType = indexType,
            ResolvedPropertyName = IndexAggregator.DerivePropertyName(indexName)
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
        var sanitized = Regex.Replace(name, @"[^a-zA-Z0-9_-]", "");
        if (string.IsNullOrEmpty(sanitized))
        {
            sanitized = "gsi1";
        }
        return sanitized.Length > 50 ? sanitized.Substring(0, 50) : sanitized;
    }
}


/// <summary>
/// Tests that validate the distinction between C# property names and DynamoDB attribute names
/// in index configuration conflict detection.
/// 
/// **Feature: multi-entity-index-consolidation**
/// **Validates: Different C# property names with same DynamoDB attribute should NOT conflict**
/// </summary>
public class IndexAttributeVsPropertyNameTests
{
    /// <summary>
    /// Validates that two entities with different C# property names but the same DynamoDB attribute name
    /// for the partition key do NOT trigger a conflict diagnostic.
    /// 
    /// This is the key scenario: Entity1 has property "Gsi1Pk" mapped to attribute "gsi1pk",
    /// Entity2 has property "OAuthUserId" also mapped to attribute "gsi1pk".
    /// Both should be valid since they reference the same DynamoDB attribute.
    /// </summary>
    [Fact]
    public void DifferentPropertyNames_SameAttributeName_ShouldNotConflict()
    {
        // Arrange - simulate the user's scenario
        var index1 = new IndexModel
        {
            IndexName = "gsi1",
            PartitionKeyProperty = "Gsi1Pk",           // C# property name on TenantEntity
            PartitionKeyAttribute = "gsi1pk",          // DynamoDB attribute name
            IndexType = IndexType.GlobalSecondaryIndex,
            ResolvedPropertyName = "Gsi1"
        };
        
        var index2 = new IndexModel
        {
            IndexName = "gsi1",
            PartitionKeyProperty = "OAuthUserId",      // Different C# property name on TenantUserEntity
            PartitionKeyAttribute = "gsi1pk",          // Same DynamoDB attribute name
            IndexType = IndexType.GlobalSecondaryIndex,
            ResolvedPropertyName = "Gsi1"
        };
        
        var entity1 = new EntityModel
        {
            ClassName = "TenantEntity",
            Namespace = "Test",
            TableName = "TestTable",
            Indexes = new[] { index1 }
        };
        
        var entity2 = new EntityModel
        {
            ClassName = "TenantUserEntity",
            Namespace = "Test",
            TableName = "TestTable",
            Indexes = new[] { index2 }
        };
        
        var entities = new List<EntityModel> { entity1, entity2 };
        
        // Act
        var aggregator = new IndexAggregator();
        var aggregatedIndexes = aggregator.AggregateIndexes(entities);
        
        // Assert - no FDDB053 diagnostic should be emitted
        var hasFddb053 = aggregator.Diagnostics.Any(d => d.Id == "FDDB053");
        var aggregatedIndex = aggregatedIndexes.FirstOrDefault(ai => ai.DynamoDbIndexName == "gsi1");
        var hasPkConflict = aggregatedIndex?.ConfigurationConflictDetails.Any(d => d.StartsWith("PK:")) ?? false;
        
        Assert.False(hasFddb053, "Should not emit FDDB053 when attribute names match");
        Assert.False(hasPkConflict, "Should not detect partition key conflict when attribute names match");
        Assert.NotNull(aggregatedIndex);
        Assert.False(aggregatedIndex.HasConfigurationConflict, "Should not have configuration conflict");
    }

    /// <summary>
    /// Validates that two entities with the same C# property names but different DynamoDB attribute names
    /// for the partition key DO trigger a conflict diagnostic.
    /// </summary>
    [Fact]
    public void SamePropertyNames_DifferentAttributeNames_ShouldConflict()
    {
        // Arrange
        var index1 = new IndexModel
        {
            IndexName = "gsi1",
            PartitionKeyProperty = "Gsi1Pk",           // Same C# property name
            PartitionKeyAttribute = "gsi1pk",          // Different DynamoDB attribute
            IndexType = IndexType.GlobalSecondaryIndex,
            ResolvedPropertyName = "Gsi1"
        };
        
        var index2 = new IndexModel
        {
            IndexName = "gsi1",
            PartitionKeyProperty = "Gsi1Pk",           // Same C# property name
            PartitionKeyAttribute = "different_attr",  // Different DynamoDB attribute
            IndexType = IndexType.GlobalSecondaryIndex,
            ResolvedPropertyName = "Gsi1"
        };
        
        var entity1 = new EntityModel
        {
            ClassName = "Entity1",
            Namespace = "Test",
            TableName = "TestTable",
            Indexes = new[] { index1 }
        };
        
        var entity2 = new EntityModel
        {
            ClassName = "Entity2",
            Namespace = "Test",
            TableName = "TestTable",
            Indexes = new[] { index2 }
        };
        
        var entities = new List<EntityModel> { entity1, entity2 };
        
        // Act
        var aggregator = new IndexAggregator();
        var aggregatedIndexes = aggregator.AggregateIndexes(entities);
        
        // Assert - FDDB053 diagnostic should be emitted
        var hasFddb053 = aggregator.Diagnostics.Any(d => d.Id == "FDDB053");
        var aggregatedIndex = aggregatedIndexes.FirstOrDefault(ai => ai.DynamoDbIndexName == "gsi1");
        
        Assert.True(hasFddb053, "Should emit FDDB053 when attribute names differ");
        Assert.NotNull(aggregatedIndex);
        Assert.True(aggregatedIndex.HasConfigurationConflict, "Should have configuration conflict");
    }

    /// <summary>
    /// Validates that different C# property names with same DynamoDB attribute names
    /// for sort keys do NOT trigger a conflict diagnostic.
    /// </summary>
    [Fact]
    public void DifferentSortKeyPropertyNames_SameAttributeName_ShouldNotConflict()
    {
        // Arrange
        var index1 = new IndexModel
        {
            IndexName = "gsi1",
            PartitionKeyProperty = "Pk",
            PartitionKeyAttribute = "pk",
            SortKeyProperty = "Gsi1Sk",               // Different C# property name
            SortKeyAttribute = "gsi1sk",              // Same DynamoDB attribute
            IndexType = IndexType.GlobalSecondaryIndex,
            ResolvedPropertyName = "Gsi1"
        };
        
        var index2 = new IndexModel
        {
            IndexName = "gsi1",
            PartitionKeyProperty = "Pk",
            PartitionKeyAttribute = "pk",
            SortKeyProperty = "CreatedAt",            // Different C# property name
            SortKeyAttribute = "gsi1sk",              // Same DynamoDB attribute
            IndexType = IndexType.GlobalSecondaryIndex,
            ResolvedPropertyName = "Gsi1"
        };
        
        var entity1 = new EntityModel
        {
            ClassName = "Entity1",
            Namespace = "Test",
            TableName = "TestTable",
            Indexes = new[] { index1 }
        };
        
        var entity2 = new EntityModel
        {
            ClassName = "Entity2",
            Namespace = "Test",
            TableName = "TestTable",
            Indexes = new[] { index2 }
        };
        
        var entities = new List<EntityModel> { entity1, entity2 };
        
        // Act
        var aggregator = new IndexAggregator();
        var aggregatedIndexes = aggregator.AggregateIndexes(entities);
        
        // Assert - no FDDB054 diagnostic should be emitted
        var hasFddb054 = aggregator.Diagnostics.Any(d => d.Id == "FDDB054");
        var aggregatedIndex = aggregatedIndexes.FirstOrDefault(ai => ai.DynamoDbIndexName == "gsi1");
        var hasSkConflict = aggregatedIndex?.ConfigurationConflictDetails.Any(d => d.StartsWith("SK:")) ?? false;
        
        Assert.False(hasFddb054, "Should not emit FDDB054 when sort key attribute names match");
        Assert.False(hasSkConflict, "Should not detect sort key conflict when attribute names match");
    }
}
