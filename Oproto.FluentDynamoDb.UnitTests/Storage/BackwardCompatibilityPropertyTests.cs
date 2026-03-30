using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using AwesomeAssertions;
using FsCheck;
using FsCheck.Xunit;
using NSubstitute;
using Oproto.FluentDynamoDb.Entities;
using Oproto.FluentDynamoDb.Metadata;
using Oproto.FluentDynamoDb.Requests;
using Oproto.FluentDynamoDb.Requests.Extensions;
using Oproto.FluentDynamoDb.Storage;
using System.Text.RegularExpressions;

namespace Oproto.FluentDynamoDb.UnitTests.Storage;

/// <summary>
/// Property-based tests for backward compatibility preservation.
/// 
/// **Feature: projection-interface-enhancement, Property 6: Backward compatibility preservation**
/// **Validates: Requirements 3.5, 6.2**
/// </summary>
public class BackwardCompatibilityPropertyTests
{
    /// <summary>
    /// Property 6: For any existing entity type implementing IDynamoDbEntity,
    /// it SHALL continue to work with QueryRequestBuilder without modification
    /// due to interface inheritance (IDynamoDbEntity inherits from IReadOnlyEntity).
    /// **Validates: Requirements 3.5, 6.2**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property ExistingEntityTypes_ShouldWorkWithQueryRequestBuilder()
    {
        return Prop.ForAll(
            Arb.From<NonEmptyString>(),
            Arb.From<NonEmptyString>(),
            (tableName, keyValue) =>
            {
                // Arrange
                var cleanTableName = SanitizeName(tableName.Get);
                var cleanKeyValue = SanitizeValue(keyValue.Get);
                var mockClient = Substitute.For<IAmazonDynamoDB>();

                // Act - Create QueryRequestBuilder with existing entity type (implements IDynamoDbEntity)
                var builder = new QueryRequestBuilder<BackwardCompatibleEntity>(mockClient)
                    .ForTable(cleanTableName)
                    .SetConditionExpression("pk = :pk")
                    .WithValue(":pk", cleanKeyValue);

                var request = builder.ToQueryRequest();

                // Assert - Builder should work correctly with existing entity types
                return request.TableName == cleanTableName &&
                       request.KeyConditionExpression == "pk = :pk" &&
                       request.ExpressionAttributeValues.ContainsKey(":pk");
            });
    }

    /// <summary>
    /// Property 6: For any existing entity type, the QueryRequestBuilder constraint
    /// (IReadOnlyEntity) SHALL be satisfied through interface inheritance.
    /// **Validates: Requirements 3.5, 6.2**
    /// </summary>
    [Fact]
    public void ExistingEntityType_ShouldSatisfyIReadOnlyEntityConstraint()
    {
        // Arrange & Act
        var implementsIReadOnlyEntity = typeof(IReadOnlyEntity).IsAssignableFrom(typeof(BackwardCompatibleEntity));
        var implementsIDynamoDbEntity = typeof(IDynamoDbEntity).IsAssignableFrom(typeof(BackwardCompatibleEntity));

        // Assert - IDynamoDbEntity inherits from IReadOnlyEntity, so both should be true
        implementsIDynamoDbEntity.Should().BeTrue("BackwardCompatibleEntity implements IDynamoDbEntity");
        implementsIReadOnlyEntity.Should().BeTrue("IDynamoDbEntity inherits from IReadOnlyEntity");
    }

    /// <summary>
    /// Property 6: For any existing entity type, all existing builder methods
    /// SHALL continue to work without modification.
    /// **Validates: Requirements 3.5, 6.2**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property ExistingEntityTypes_AllBuilderMethods_ShouldWork()
    {
        return Prop.ForAll(
            Arb.From<NonEmptyString>(),
            Arb.From<PositiveInt>(),
            (tableName, limit) =>
            {
                // Arrange
                var cleanTableName = SanitizeName(tableName.Get);
                var cleanLimit = Math.Min(limit.Get, 1000);
                var mockClient = Substitute.For<IAmazonDynamoDB>();

                // Act - Use all common builder methods with existing entity type
                var builder = new QueryRequestBuilder<BackwardCompatibleEntity>(mockClient)
                    .ForTable(cleanTableName)
                    .SetConditionExpression("pk = :pk AND begins_with(sk, :prefix)")
                    .WithValue(":pk", "test-pk")
                    .WithValue(":prefix", "PREFIX#")
                    .WithFilter("amount > :amount")
                    .WithValue(":amount", 100)
                    .WithProjection("pk, sk, name, amount")
                    .Take(cleanLimit)
                    .OrderDescending()
                    .UsingConsistentRead();

                var request = builder.ToQueryRequest();

                // Assert - All methods should work correctly
                return request.TableName == cleanTableName &&
                       request.KeyConditionExpression == "pk = :pk AND begins_with(sk, :prefix)" &&
                       request.FilterExpression == "amount > :amount" &&
                       request.ProjectionExpression == "pk, sk, name, amount" &&
                       request.Limit == cleanLimit &&
                       request.ScanIndexForward == false &&
                       request.ConsistentRead == true;
            });
    }

    /// <summary>
    /// Property 6: For any existing entity type used with DynamoDbIndex,
    /// the Query method SHALL continue to work without modification.
    /// **Validates: Requirements 3.5, 6.2**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property ExistingEntityTypes_ShouldWorkWithDynamoDbIndex()
    {
        return Prop.ForAll(
            Arb.From<NonEmptyString>(),
            Arb.From<NonEmptyString>(),
            (tableName, indexName) =>
            {
                // Arrange
                var cleanTableName = SanitizeName(tableName.Get);
                var cleanIndexName = SanitizeName(indexName.Get);
                var mockClient = Substitute.For<IAmazonDynamoDB>();
                var mockTable = Substitute.For<IDynamoDbTable>();
                mockTable.DynamoDbClient.Returns(mockClient);
                mockTable.Name.Returns(cleanTableName);
                mockTable.GetOptions().Returns(new FluentDynamoDbOptions());

                var index = new DynamoDbIndex(mockTable, cleanIndexName);

                // Act - Query with existing entity type
                var builder = index.Query<BackwardCompatibleEntity>()
                    .SetConditionExpression("gsi1pk = :pk")
                    .WithValue(":pk", "test-pk");

                var request = builder.ToQueryRequest();

                // Assert - Index query should work correctly
                return request.TableName == cleanTableName &&
                       request.IndexName == cleanIndexName &&
                       request.KeyConditionExpression == "gsi1pk = :pk";
            });
    }

    /// <summary>
    /// Property 6: For any existing entity type, GetItemRequestBuilder
    /// SHALL continue to work without modification.
    /// **Validates: Requirements 3.5, 6.2**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property ExistingEntityTypes_ShouldWorkWithGetItemRequestBuilder()
    {
        return Prop.ForAll(
            Arb.From<NonEmptyString>(),
            Arb.From<NonEmptyString>(),
            (tableName, keyValue) =>
            {
                // Arrange
                var cleanTableName = SanitizeName(tableName.Get);
                var cleanKeyValue = SanitizeValue(keyValue.Get);
                var mockClient = Substitute.For<IAmazonDynamoDB>();

                // Act - Create GetItemRequestBuilder with existing entity type
                var builder = new GetItemRequestBuilder<BackwardCompatibleEntity>(mockClient)
                    .ForTable(cleanTableName)
                    .WithKey("pk", cleanKeyValue);

                var request = builder.ToGetItemRequest();

                // Assert - Builder should work correctly
                return request.TableName == cleanTableName &&
                       request.Key.ContainsKey("pk") &&
                       request.Key["pk"].S == cleanKeyValue;
            });
    }

    /// <summary>
    /// Property 6: For any existing entity type, write operations (Put, Update, Delete)
    /// SHALL continue to work without modification.
    /// **Validates: Requirements 3.5, 6.2**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property ExistingEntityTypes_WriteOperations_ShouldWork()
    {
        return Prop.ForAll(
            Arb.From<NonEmptyString>(),
            Arb.From<NonEmptyString>(),
            (tableName, keyValue) =>
            {
                // Arrange
                var cleanTableName = SanitizeName(tableName.Get);
                var cleanKeyValue = SanitizeValue(keyValue.Get);
                var mockClient = Substitute.For<IAmazonDynamoDB>();

                // Act - Create write builders with existing entity type
                var putBuilder = new PutItemRequestBuilder<BackwardCompatibleEntity>(mockClient)
                    .ForTable(cleanTableName);

                var updateBuilder = new UpdateItemRequestBuilder<BackwardCompatibleEntity>(mockClient)
                    .ForTable(cleanTableName)
                    .WithKey("pk", cleanKeyValue);

                var deleteBuilder = new DeleteItemRequestBuilder<BackwardCompatibleEntity>(mockClient)
                    .ForTable(cleanTableName)
                    .WithKey("pk", cleanKeyValue);

                var putRequest = putBuilder.ToPutItemRequest();
                var updateRequest = updateBuilder.ToUpdateItemRequest();
                var deleteRequest = deleteBuilder.ToDeleteItemRequest();

                // Assert - All write builders should work correctly
                return putRequest.TableName == cleanTableName &&
                       updateRequest.TableName == cleanTableName &&
                       updateRequest.Key.ContainsKey("pk") &&
                       deleteRequest.TableName == cleanTableName &&
                       deleteRequest.Key.ContainsKey("pk");
            });
    }

    #region Helper Methods

    private static string SanitizeName(string name)
    {
        var sanitized = Regex.Replace(name, @"[^a-zA-Z0-9_-]", "");
        if (string.IsNullOrEmpty(sanitized) || !char.IsLetter(sanitized[0]))
        {
            sanitized = "Test" + sanitized;
        }
        return sanitized.Length > 50 ? sanitized.Substring(0, 50) : sanitized;
    }

    private static string SanitizeValue(string value)
    {
        var sanitized = value.Replace("\0", "").Trim();
        return string.IsNullOrEmpty(sanitized) ? "default" : sanitized;
    }

    #endregion
}

/// <summary>
/// Test entity that implements IDynamoDbEntity (existing entity pattern).
/// Used to verify backward compatibility with existing entity types.
/// </summary>
public class BackwardCompatibleEntity : IDynamoDbEntity
{
    public string Pk { get; set; } = string.Empty;
    public string Sk { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public int Amount { get; set; }

    public static Dictionary<string, AttributeValue> ToDynamoDb<TSelf>(TSelf entity, FluentDynamoDbOptions? options = null)
        where TSelf : IDynamoDbEntity
    {
        var e = entity as BackwardCompatibleEntity;
        return new Dictionary<string, AttributeValue>
        {
            ["pk"] = new AttributeValue { S = e?.Pk ?? string.Empty },
            ["sk"] = new AttributeValue { S = e?.Sk ?? string.Empty },
            ["name"] = new AttributeValue { S = e?.Name ?? string.Empty },
            ["amount"] = new AttributeValue { N = (e?.Amount ?? 0).ToString() }
        };
    }

    public static TSelf FromDynamoDb<TSelf>(Dictionary<string, AttributeValue> item, FluentDynamoDbOptions? options = null)
        where TSelf : IReadOnlyEntity
    {
        var entity = new BackwardCompatibleEntity
        {
            Pk = item.TryGetValue("pk", out var pk) ? pk.S ?? string.Empty : string.Empty,
            Sk = item.TryGetValue("sk", out var sk) ? sk.S ?? string.Empty : string.Empty,
            Name = item.TryGetValue("name", out var name) ? name.S ?? string.Empty : string.Empty,
            Amount = item.TryGetValue("amount", out var amount) && int.TryParse(amount.N, out var a) ? a : 0
        };
        return (TSelf)(object)entity;
    }

    public static TSelf FromDynamoDb<TSelf>(IList<Dictionary<string, AttributeValue>> items, FluentDynamoDbOptions? options = null)
        where TSelf : IDynamoDbEntity
    {
        return FromDynamoDb<TSelf>(items.First(), options);
    }

    public static string GetPartitionKey(Dictionary<string, AttributeValue> item)
    {
        return item.TryGetValue("pk", out var pk) ? pk.S ?? string.Empty : string.Empty;
    }

    public static bool MatchesEntity(Dictionary<string, AttributeValue> item)
    {
        return item.ContainsKey("pk");
    }

    public static EntityMetadata GetEntityMetadata()
    {
        return new EntityMetadata
        {
            TableName = "BackwardCompatibleTable",
            PartitionKeyAttributeName = "pk",
            PartitionKeyAttributeType = "S",
            SortKeyAttributeName = "sk",
            SortKeyAttributeType = "S",
            RequiresWriteTransaction = false,
            IsMultiItemEntity = false,
            Properties = Array.Empty<PropertyMetadata>(),
            Indexes = Array.Empty<IndexMetadata>(),
            Relationships = Array.Empty<RelationshipMetadata>()
        };
    }

    public static bool RequiresWriteTransaction => false;
}


/// <summary>
/// Property-based tests for backward compatibility interface preservation.
/// 
/// **Feature: projection-interface-enhancement, Property 11: Backward compatibility interface preservation**
/// **Validates: Requirements 6.1, 6.2, 6.3**
/// </summary>
public class BackwardCompatibilityInterfacePreservationPropertyTests
{
    /// <summary>
    /// Property 11: The existing IProjectionModel&lt;TSelf&gt; interface SHALL remain available and functional.
    /// **Validates: Requirements 6.1, 6.2, 6.3**
    /// </summary>
    [Fact]
    public void IProjectionModel_ShouldRemainAvailableAndFunctional()
    {
        // Arrange & Act
        var interfaceType = typeof(IProjectionModel<>);

        // Assert - Interface should exist and have correct members
        interfaceType.Should().NotBeNull();
        interfaceType.IsInterface.Should().BeTrue();
        interfaceType.IsGenericTypeDefinition.Should().BeTrue();

        // Should have ProjectionExpression property
        var projectionExpressionProperty = interfaceType.GetProperty("ProjectionExpression");
        projectionExpressionProperty.Should().NotBeNull();
        projectionExpressionProperty!.PropertyType.Should().Be(typeof(string));

        // Should have FromDynamoDb method
        var fromDynamoDbMethod = interfaceType.GetMethod("FromDynamoDb");
        fromDynamoDbMethod.Should().NotBeNull();
    }

    /// <summary>
    /// Property 11: For any existing projection using IProjectionModel&lt;TSelf&gt;,
    /// it SHALL continue to work without modification.
    /// **Validates: Requirements 6.1, 6.2, 6.3**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property ExistingProjectionModel_ShouldContinueToWork()
    {
        return Prop.ForAll(
            Arb.From<NonEmptyString>(),
            Arb.From<NonEmptyString>(),
            Arb.From<NonEmptyString>(),
            (id, name, status) =>
            {
                // Arrange - Create a DynamoDB item
                var cleanId = SanitizeValue(id.Get);
                var cleanName = SanitizeValue(name.Get);
                var cleanStatus = SanitizeValue(status.Get);

                var item = new Dictionary<string, AttributeValue>
                {
                    ["pk"] = new AttributeValue { S = cleanId },
                    ["name"] = new AttributeValue { S = cleanName },
                    ["status"] = new AttributeValue { S = cleanStatus }
                };

                // Act - Use the existing IProjectionModel interface methods
                var projectionExpression = LegacyProjectionModel.ProjectionExpression;
                var result = LegacyProjectionModel.FromDynamoDb(item);

                // Assert - Interface methods should work correctly
                return !string.IsNullOrEmpty(projectionExpression) &&
                       result.Id == cleanId &&
                       result.Name == cleanName &&
                       result.Status == cleanStatus;
            });
    }

    /// <summary>
    /// Property 11: For any projection type implementing both IProjectionModel and IReadOnlyEntity,
    /// both interfaces SHALL be usable independently.
    /// **Validates: Requirements 6.1, 6.2, 6.3**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property DualInterfaceProjection_BothInterfacesShouldWork()
    {
        return Prop.ForAll(
            Arb.From<NonEmptyString>(),
            Arb.From<NonEmptyString>(),
            (id, name) =>
            {
                // Arrange
                var cleanId = SanitizeValue(id.Get);
                var cleanName = SanitizeValue(name.Get);

                var item = new Dictionary<string, AttributeValue>
                {
                    ["pk"] = new AttributeValue { S = cleanId },
                    ["name"] = new AttributeValue { S = cleanName },
                    ["status"] = new AttributeValue { S = "active" }
                };

                // Act - Use IProjectionModel interface
                var projectionExpression = DualInterfaceProjection.ProjectionExpression;
                var projectionResult = DualInterfaceProjection.FromDynamoDb(item);

                // Act - Use IReadOnlyEntity interface
                var partitionKey = DualInterfaceProjection.GetPartitionKey(item);
                var readOnlyResult = DualInterfaceProjection.FromDynamoDb<DualInterfaceProjection>(item, null);

                // Assert - Both interfaces should work correctly
                return !string.IsNullOrEmpty(projectionExpression) &&
                       projectionResult.Id == cleanId &&
                       projectionResult.Name == cleanName &&
                       partitionKey == cleanId &&
                       readOnlyResult.Id == cleanId &&
                       readOnlyResult.Name == cleanName;
            });
    }

    /// <summary>
    /// Property 11: The existing projection extension methods SHALL continue to work
    /// with the new interface implementation.
    /// **Validates: Requirements 6.1, 6.2, 6.3**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property ExistingProjectionExtensionMethods_ShouldContinueToWork()
    {
        return Prop.ForAll(
            Arb.From<NonEmptyString>(),
            (tableName) =>
            {
                // Arrange
                var cleanTableName = SanitizeName(tableName.Get);
                var mockClient = Substitute.For<IAmazonDynamoDB>();

                // Act - Create QueryRequestBuilder with projection type
                var builder = new QueryRequestBuilder<DualInterfaceProjection>(mockClient)
                    .ForTable(cleanTableName)
                    .SetConditionExpression("pk = :pk");

                // Apply projection manually (simulating what ToListAsync does)
                builder = builder.WithProjection(DualInterfaceProjection.ProjectionExpression);
                var request = builder.ToQueryRequest();

                // Assert - Projection should be applied correctly
                return request.ProjectionExpression == DualInterfaceProjection.ProjectionExpression &&
                       request.Select == Select.SPECIFIC_ATTRIBUTES;
            });
    }

    /// <summary>
    /// Property 11: The ProjectionExpression property SHALL remain available on projection types.
    /// **Validates: Requirements 6.1, 6.2, 6.3**
    /// </summary>
    [Fact]
    public void ProjectionExpression_ShouldRemainAvailableOnProjectionTypes()
    {
        // Arrange & Act
        var legacyExpression = LegacyProjectionModel.ProjectionExpression;
        var dualExpression = DualInterfaceProjection.ProjectionExpression;

        // Assert - Both projection types should have ProjectionExpression
        legacyExpression.Should().NotBeNullOrEmpty();
        dualExpression.Should().NotBeNullOrEmpty();
        legacyExpression.Should().Contain(","); // Should have multiple attributes
        dualExpression.Should().Contain(",");
    }

    /// <summary>
    /// Property 11: For any projection type, the FromDynamoDb method from IProjectionModel
    /// SHALL be callable via the interface.
    /// **Validates: Requirements 6.1, 6.2, 6.3**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property FromDynamoDb_ShouldBeCallableViaIProjectionModelInterface()
    {
        return Prop.ForAll(
            Arb.From<NonEmptyString>(),
            (id) =>
            {
                // Arrange
                var cleanId = SanitizeValue(id.Get);
                var item = new Dictionary<string, AttributeValue>
                {
                    ["pk"] = new AttributeValue { S = cleanId },
                    ["name"] = new AttributeValue { S = "test" },
                    ["status"] = new AttributeValue { S = "active" }
                };

                // Act - Call via interface static method
                var result = HydrateViaIProjectionModel<LegacyProjectionModel>(item);

                // Assert
                return result.Id == cleanId;
            });
    }

    /// <summary>
    /// Property 11: Interface inheritance should be preserved - IDynamoDbEntity inherits from IReadOnlyEntity.
    /// **Validates: Requirements 6.1, 6.2, 6.3**
    /// </summary>
    [Fact]
    public void InterfaceInheritance_ShouldBePreserved()
    {
        // Assert - IDynamoDbEntity should inherit from IReadOnlyEntity
        typeof(IReadOnlyEntity).IsAssignableFrom(typeof(IDynamoDbEntity)).Should().BeTrue(
            "IDynamoDbEntity should inherit from IReadOnlyEntity");

        // Assert - IReadOnlyEntity should inherit from IEntityMetadataProvider
        typeof(IEntityMetadataProvider).IsAssignableFrom(typeof(IReadOnlyEntity)).Should().BeTrue(
            "IReadOnlyEntity should inherit from IEntityMetadataProvider");

        // Assert - IProjectionModel should NOT inherit from IReadOnlyEntity (they are separate)
        typeof(IReadOnlyEntity).IsAssignableFrom(typeof(IProjectionModel<>)).Should().BeFalse(
            "IProjectionModel should NOT inherit from IReadOnlyEntity - they are separate interfaces");
    }

    #region Helper Methods

    private static T HydrateViaIProjectionModel<T>(Dictionary<string, AttributeValue> item)
        where T : IProjectionModel<T>
    {
        return T.FromDynamoDb(item);
    }

    private static string SanitizeName(string name)
    {
        var sanitized = Regex.Replace(name, @"[^a-zA-Z0-9_-]", "");
        if (string.IsNullOrEmpty(sanitized) || !char.IsLetter(sanitized[0]))
        {
            sanitized = "Test" + sanitized;
        }
        return sanitized.Length > 50 ? sanitized.Substring(0, 50) : sanitized;
    }

    private static string SanitizeValue(string value)
    {
        var sanitized = value.Replace("\0", "").Trim();
        return string.IsNullOrEmpty(sanitized) ? "default" : sanitized;
    }

    #endregion
}

/// <summary>
/// Legacy projection model that implements only IProjectionModel (existing pattern).
/// Used to verify backward compatibility with existing projection code.
/// </summary>
public class LegacyProjectionModel : IProjectionModel<LegacyProjectionModel>
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;

    public static string ProjectionExpression => "pk, name, status";

    public static LegacyProjectionModel FromDynamoDb(Dictionary<string, AttributeValue> item)
    {
        return new LegacyProjectionModel
        {
            Id = item.TryGetValue("pk", out var pk) ? pk.S ?? string.Empty : string.Empty,
            Name = item.TryGetValue("name", out var name) ? name.S ?? string.Empty : string.Empty,
            Status = item.TryGetValue("status", out var status) ? status.S ?? string.Empty : string.Empty
        };
    }
}

/// <summary>
/// Projection model that implements both IProjectionModel and IReadOnlyEntity (new pattern).
/// Used to verify that both interfaces can be implemented together.
/// </summary>
public class DualInterfaceProjection : IProjectionModel<DualInterfaceProjection>, IReadOnlyEntity
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;

    // IProjectionModel<TSelf> implementation
    public static string ProjectionExpression => "pk, name, status";

    public static DualInterfaceProjection FromDynamoDb(Dictionary<string, AttributeValue> item)
    {
        return new DualInterfaceProjection
        {
            Id = item.TryGetValue("pk", out var pk) ? pk.S ?? string.Empty : string.Empty,
            Name = item.TryGetValue("name", out var name) ? name.S ?? string.Empty : string.Empty,
            Status = item.TryGetValue("status", out var status) ? status.S ?? string.Empty : string.Empty
        };
    }

    // IReadOnlyEntity implementation
    public static TSelf FromDynamoDb<TSelf>(Dictionary<string, AttributeValue> item, FluentDynamoDbOptions? options = null)
        where TSelf : IReadOnlyEntity
    {
        if (typeof(TSelf) != typeof(DualInterfaceProjection))
        {
            throw new ArgumentException($"Type parameter must be DualInterfaceProjection, but was {typeof(TSelf).Name}", nameof(TSelf));
        }
        return (TSelf)(object)FromDynamoDb(item);
    }

    public static string GetPartitionKey(Dictionary<string, AttributeValue> item)
    {
        return item.TryGetValue("pk", out var pk) ? pk.S ?? string.Empty : string.Empty;
    }

    public static EntityMetadata GetEntityMetadata()
    {
        return new EntityMetadata
        {
            TableName = "DualInterfaceTable",
            PartitionKeyAttributeName = "pk",
            PartitionKeyAttributeType = "S",
            SortKeyAttributeName = null,
            SortKeyAttributeType = null,
            RequiresWriteTransaction = false,
            IsMultiItemEntity = false,
            Properties = Array.Empty<PropertyMetadata>(),
            Indexes = Array.Empty<IndexMetadata>(),
            Relationships = Array.Empty<RelationshipMetadata>()
        };
    }
}
