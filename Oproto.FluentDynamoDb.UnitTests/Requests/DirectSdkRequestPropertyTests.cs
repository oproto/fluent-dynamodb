using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using FsCheck;
using FsCheck.Xunit;
using NSubstitute;
using Oproto.FluentDynamoDb.Entities;
using Oproto.FluentDynamoDb.Metadata;
using Oproto.FluentDynamoDb.Requests;

namespace Oproto.FluentDynamoDb.UnitTests.Requests;

/// <summary>
/// Property-based tests for direct SDK request passing via builders.
/// Each test runs 100 iterations with random inputs to verify universal properties.
/// 
/// **Feature: v1.0-architecture-improvements, Property 5: Direct SDK request hydration consistency**
/// **Validates: Requirements 4.1, 4.5, 4.6**
/// </summary>
public class DirectSdkRequestPropertyTests
{
    /// <summary>
    /// **Feature: v1.0-architecture-improvements, Property 5: Direct SDK request hydration consistency**
    /// **Validates: Requirement 4.1**
    /// 
    /// For any GetItemRequest passed via WithRequest, the builder should preserve all request properties.
    /// </summary>
    [Property(MaxTest = 100)]
    public Property GetItemRequest_WithRequest_PreservesAllProperties()
    {
        return Prop.ForAll(
            GenerateGetItemRequest(),
            request =>
            {
                // Arrange
                var client = Substitute.For<IAmazonDynamoDB>();
                var builder = new GetItemRequestBuilder<TestEntity>(client);
                
                // Act
                builder.WithRequest(request);
                var result = builder.ToGetItemRequest();
                
                // Assert - all properties should be preserved
                var tableNamePreserved = result.TableName == request.TableName;
                var keyPreserved = result.Key != null && 
                    result.Key.Count == request.Key.Count &&
                    result.Key.All(kv => request.Key.ContainsKey(kv.Key));
                var consistentReadPreserved = result.ConsistentRead == request.ConsistentRead;
                var projectionPreserved = result.ProjectionExpression == request.ProjectionExpression;
                
                return (tableNamePreserved && keyPreserved && consistentReadPreserved && projectionPreserved)
                    .ToProperty()
                    .Label($"GetItemRequest properties should be preserved. " +
                           $"TableName: {tableNamePreserved}, Key: {keyPreserved}, " +
                           $"ConsistentRead: {consistentReadPreserved}, Projection: {projectionPreserved}");
            });
    }

    /// <summary>
    /// **Feature: v1.0-architecture-improvements, Property 5: Direct SDK request hydration consistency**
    /// **Validates: Requirement 4.5**
    /// 
    /// For any QueryRequest passed via WithRequest, the builder should preserve all request properties.
    /// </summary>
    [Property(MaxTest = 100)]
    public Property QueryRequest_WithRequest_PreservesAllProperties()
    {
        return Prop.ForAll(
            GenerateQueryRequest(),
            request =>
            {
                // Arrange
                var client = Substitute.For<IAmazonDynamoDB>();
                var builder = new QueryRequestBuilder<TestEntity>(client);
                
                // Act
                builder.WithRequest(request);
                var result = builder.ToQueryRequest();
                
                // Assert - all properties should be preserved
                var tableNamePreserved = result.TableName == request.TableName;
                var keyConditionPreserved = result.KeyConditionExpression == request.KeyConditionExpression;
                var filterPreserved = result.FilterExpression == request.FilterExpression;
                var limitPreserved = result.Limit == request.Limit;
                var scanForwardPreserved = result.ScanIndexForward == request.ScanIndexForward;
                
                return (tableNamePreserved && keyConditionPreserved && filterPreserved && 
                        limitPreserved && scanForwardPreserved)
                    .ToProperty()
                    .Label($"QueryRequest properties should be preserved. " +
                           $"TableName: {tableNamePreserved}, KeyCondition: {keyConditionPreserved}, " +
                           $"Filter: {filterPreserved}, Limit: {limitPreserved}, ScanForward: {scanForwardPreserved}");
            });
    }

    /// <summary>
    /// **Feature: v1.0-architecture-improvements, Property 5: Direct SDK request hydration consistency**
    /// **Validates: Requirement 4.6**
    /// 
    /// For any ScanRequest passed via WithRequest, the builder should preserve all request properties.
    /// </summary>
    [Property(MaxTest = 100)]
    public Property ScanRequest_WithRequest_PreservesAllProperties()
    {
        return Prop.ForAll(
            GenerateScanRequest(),
            request =>
            {
                // Arrange
                var client = Substitute.For<IAmazonDynamoDB>();
                var builder = new ScanRequestBuilder<TestEntity>(client);
                
                // Act
                builder.WithRequest(request);
                var result = builder.ToScanRequest();
                
                // Assert - all properties should be preserved
                var tableNamePreserved = result.TableName == request.TableName;
                var filterPreserved = result.FilterExpression == request.FilterExpression;
                var limitPreserved = result.Limit == request.Limit;
                var segmentPreserved = result.Segment == request.Segment;
                var totalSegmentsPreserved = result.TotalSegments == request.TotalSegments;
                
                return (tableNamePreserved && filterPreserved && limitPreserved && 
                        segmentPreserved && totalSegmentsPreserved)
                    .ToProperty()
                    .Label($"ScanRequest properties should be preserved. " +
                           $"TableName: {tableNamePreserved}, Filter: {filterPreserved}, " +
                           $"Limit: {limitPreserved}, Segment: {segmentPreserved}, TotalSegments: {totalSegmentsPreserved}");
            });
    }

    /// <summary>
    /// **Feature: v1.0-architecture-improvements, Property 5: Direct SDK request hydration consistency**
    /// **Validates: Requirements 4.1, 4.5, 4.6**
    /// 
    /// For any SDK request passed via WithRequest, the builder should allow additional fluent configuration.
    /// </summary>
    [Property(MaxTest = 100)]
    public Property WithRequest_AllowsAdditionalConfiguration()
    {
        return Prop.ForAll(
            GenerateGetItemRequest(),
            request =>
            {
                // Arrange
                var client = Substitute.For<IAmazonDynamoDB>();
                var builder = new GetItemRequestBuilder<TestEntity>(client);
                
                // Act - pass request then add additional configuration
                builder.WithRequest(request);
                builder.UsingConsistentRead();
                builder.ReturnTotalConsumedCapacity();
                var result = builder.ToGetItemRequest();
                
                // Assert - original properties preserved and new ones added
                var tableNamePreserved = result.TableName == request.TableName;
                var consistentReadEnabled = result.ConsistentRead == true;
                var capacityEnabled = result.ReturnConsumedCapacity == ReturnConsumedCapacity.TOTAL;
                
                return (tableNamePreserved && consistentReadEnabled && capacityEnabled)
                    .ToProperty()
                    .Label($"WithRequest should allow additional configuration. " +
                           $"TableName: {tableNamePreserved}, ConsistentRead: {consistentReadEnabled}, " +
                           $"Capacity: {capacityEnabled}");
            });
    }

    /// <summary>
    /// **Feature: v1.0-architecture-improvements, Property 5: Direct SDK request hydration consistency**
    /// **Validates: Requirements 4.1, 4.5, 4.6**
    /// 
    /// For any null request passed to WithRequest, the builder should throw ArgumentNullException.
    /// </summary>
    [Fact]
    public void WithRequest_NullRequest_ThrowsArgumentNullException()
    {
        // Arrange
        var client = Substitute.For<IAmazonDynamoDB>();
        var getBuilder = new GetItemRequestBuilder<TestEntity>(client);
        var queryBuilder = new QueryRequestBuilder<TestEntity>(client);
        var scanBuilder = new ScanRequestBuilder<TestEntity>(client);
        
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => getBuilder.WithRequest(null!));
        Assert.Throws<ArgumentNullException>(() => queryBuilder.WithRequest(null!));
        Assert.Throws<ArgumentNullException>(() => scanBuilder.WithRequest(null!));
    }

    /// <summary>
    /// **Feature: v1.0-architecture-improvements, Property 5: Direct SDK request hydration consistency**
    /// **Validates: Requirements 4.2, 4.3, 4.4**
    /// 
    /// For any PutItemRequest passed via WithRequest, the builder should preserve all request properties.
    /// </summary>
    [Property(MaxTest = 100)]
    public Property PutItemRequest_WithRequest_PreservesAllProperties()
    {
        return Prop.ForAll(
            GeneratePutItemRequest(),
            request =>
            {
                // Arrange
                var client = Substitute.For<IAmazonDynamoDB>();
                var builder = new PutItemRequestBuilder<TestEntity>(client);
                
                // Act
                builder.WithRequest(request);
                var result = builder.ToPutItemRequest();
                
                // Assert - all properties should be preserved
                var tableNamePreserved = result.TableName == request.TableName;
                var itemPreserved = result.Item != null && 
                    result.Item.Count == request.Item.Count;
                var conditionPreserved = result.ConditionExpression == request.ConditionExpression;
                
                return (tableNamePreserved && itemPreserved && conditionPreserved)
                    .ToProperty()
                    .Label($"PutItemRequest properties should be preserved. " +
                           $"TableName: {tableNamePreserved}, Item: {itemPreserved}, " +
                           $"Condition: {conditionPreserved}");
            });
    }

    /// <summary>
    /// **Feature: v1.0-architecture-improvements, Property 5: Direct SDK request hydration consistency**
    /// **Validates: Requirements 4.3**
    /// 
    /// For any UpdateItemRequest passed via WithRequest, the builder should preserve all request properties.
    /// </summary>
    [Property(MaxTest = 100)]
    public Property UpdateItemRequest_WithRequest_PreservesAllProperties()
    {
        return Prop.ForAll(
            GenerateUpdateItemRequest(),
            request =>
            {
                // Arrange
                var client = Substitute.For<IAmazonDynamoDB>();
                var builder = new UpdateItemRequestBuilder<TestEntity>(client);
                
                // Act
                builder.WithRequest(request);
                var result = builder.ToUpdateItemRequest();
                
                // Assert - all properties should be preserved
                var tableNamePreserved = result.TableName == request.TableName;
                var keyPreserved = result.Key != null && 
                    result.Key.Count == request.Key.Count;
                var updateExpressionPreserved = result.UpdateExpression == request.UpdateExpression;
                var conditionPreserved = result.ConditionExpression == request.ConditionExpression;
                
                return (tableNamePreserved && keyPreserved && updateExpressionPreserved && conditionPreserved)
                    .ToProperty()
                    .Label($"UpdateItemRequest properties should be preserved. " +
                           $"TableName: {tableNamePreserved}, Key: {keyPreserved}, " +
                           $"UpdateExpression: {updateExpressionPreserved}, Condition: {conditionPreserved}");
            });
    }

    /// <summary>
    /// **Feature: v1.0-architecture-improvements, Property 5: Direct SDK request hydration consistency**
    /// **Validates: Requirements 4.4**
    /// 
    /// For any DeleteItemRequest passed via WithRequest, the builder should preserve all request properties.
    /// </summary>
    [Property(MaxTest = 100)]
    public Property DeleteItemRequest_WithRequest_PreservesAllProperties()
    {
        return Prop.ForAll(
            GenerateDeleteItemRequest(),
            request =>
            {
                // Arrange
                var client = Substitute.For<IAmazonDynamoDB>();
                var builder = new DeleteItemRequestBuilder<TestEntity>(client);
                
                // Act
                builder.WithRequest(request);
                var result = builder.ToDeleteItemRequest();
                
                // Assert - all properties should be preserved
                var tableNamePreserved = result.TableName == request.TableName;
                var keyPreserved = result.Key != null && 
                    result.Key.Count == request.Key.Count;
                var conditionPreserved = result.ConditionExpression == request.ConditionExpression;
                
                return (tableNamePreserved && keyPreserved && conditionPreserved)
                    .ToProperty()
                    .Label($"DeleteItemRequest properties should be preserved. " +
                           $"TableName: {tableNamePreserved}, Key: {keyPreserved}, " +
                           $"Condition: {conditionPreserved}");
            });
    }

    #region Test Entity

    /// <summary>
    /// Simple test entity for property tests.
    /// </summary>
    private class TestEntity : IDynamoDbEntity
    {
        public string Pk { get; set; } = string.Empty;
        public string Sk { get; set; } = string.Empty;

        public static Dictionary<string, AttributeValue> ToDynamoDb<TSelf>(TSelf entity, FluentDynamoDbOptions? options = null)
            where TSelf : IDynamoDbEntity
        {
            if (entity is not TestEntity testEntity)
                throw new InvalidOperationException();
            return new Dictionary<string, AttributeValue>
            {
                ["pk"] = new AttributeValue { S = testEntity.Pk },
                ["sk"] = new AttributeValue { S = testEntity.Sk }
            };
        }

        public static TSelf FromDynamoDb<TSelf>(Dictionary<string, AttributeValue> item, FluentDynamoDbOptions? options = null)
            where TSelf : IDynamoDbEntity
        {
            var entity = new TestEntity
            {
                Pk = item.TryGetValue("pk", out var pk) ? pk.S : string.Empty,
                Sk = item.TryGetValue("sk", out var sk) ? sk.S : string.Empty
            };
            return (TSelf)(object)entity;
        }

        public static TSelf FromDynamoDb<TSelf>(IList<Dictionary<string, AttributeValue>> items, FluentDynamoDbOptions? options = null)
            where TSelf : IDynamoDbEntity
        {
            return FromDynamoDb<TSelf>(items[0], options);
        }

        public static string GetPartitionKey(Dictionary<string, AttributeValue> item)
            => item.TryGetValue("pk", out var pk) ? pk.S : string.Empty;

        public static bool MatchesEntity(Dictionary<string, AttributeValue> item) => true;

        public static bool RequiresWriteTransaction => false;

        public static EntityMetadata GetEntityMetadata()
            => new EntityMetadata
            {
                TableName = "Test",
                Properties = Array.Empty<PropertyMetadata>(),
                Indexes = Array.Empty<IndexMetadata>(),
                Relationships = Array.Empty<RelationshipMetadata>()
            };
    }

    #endregion

    #region Generators

    /// <summary>
    /// Generates a random GetItemRequest with valid properties.
    /// </summary>
    private static Arbitrary<GetItemRequest> GenerateGetItemRequest()
    {
        return Arb.From(
            from tableName in Arb.Default.NonEmptyString().Generator
            from pkValue in Arb.Default.NonEmptyString().Generator
            from skValue in Arb.Default.NonEmptyString().Generator
            from consistentRead in Arb.Default.Bool().Generator
            from hasProjection in Arb.Default.Bool().Generator
            let projection = hasProjection ? "pk, sk" : null
            select new GetItemRequest
            {
                TableName = tableName.Get,
                Key = new Dictionary<string, AttributeValue>
                {
                    ["pk"] = new AttributeValue { S = pkValue.Get },
                    ["sk"] = new AttributeValue { S = skValue.Get }
                },
                ConsistentRead = consistentRead,
                ProjectionExpression = projection
            });
    }

    /// <summary>
    /// Generates a random QueryRequest with valid properties.
    /// </summary>
    private static Arbitrary<QueryRequest> GenerateQueryRequest()
    {
        return Arb.From(
            from tableName in Arb.Default.NonEmptyString().Generator
            from pkValue in Arb.Default.NonEmptyString().Generator
            from limit in Gen.Choose(1, 100)
            from scanForward in Arb.Default.Bool().Generator
            from hasFilter in Arb.Default.Bool().Generator
            let filter = hasFilter ? "attribute_exists(#attr)" : null
            select new QueryRequest
            {
                TableName = tableName.Get,
                KeyConditionExpression = "pk = :pk",
                ExpressionAttributeValues = new Dictionary<string, AttributeValue>
                {
                    [":pk"] = new AttributeValue { S = pkValue.Get }
                },
                FilterExpression = filter,
                Limit = limit,
                ScanIndexForward = scanForward
            });
    }

    /// <summary>
    /// Generates a random ScanRequest with valid properties.
    /// </summary>
    private static Arbitrary<ScanRequest> GenerateScanRequest()
    {
        return Arb.From(
            from tableName in Arb.Default.NonEmptyString().Generator
            from limit in Gen.Choose(1, 100)
            from segment in Gen.Choose(0, 3)
            from totalSegments in Gen.Choose(1, 4)
            from hasFilter in Arb.Default.Bool().Generator
            let filter = hasFilter ? "attribute_exists(#attr)" : null
            select new ScanRequest
            {
                TableName = tableName.Get,
                FilterExpression = filter,
                Limit = limit,
                Segment = segment,
                TotalSegments = Math.Max(totalSegments, segment + 1) // Ensure totalSegments > segment
            });
    }

    /// <summary>
    /// Generates a random PutItemRequest with valid properties.
    /// </summary>
    private static Arbitrary<PutItemRequest> GeneratePutItemRequest()
    {
        return Arb.From(
            from tableName in Arb.Default.NonEmptyString().Generator
            from pkValue in Arb.Default.NonEmptyString().Generator
            from skValue in Arb.Default.NonEmptyString().Generator
            from hasCondition in Arb.Default.Bool().Generator
            let condition = hasCondition ? "attribute_not_exists(pk)" : null
            select new PutItemRequest
            {
                TableName = tableName.Get,
                Item = new Dictionary<string, AttributeValue>
                {
                    ["pk"] = new AttributeValue { S = pkValue.Get },
                    ["sk"] = new AttributeValue { S = skValue.Get }
                },
                ConditionExpression = condition
            });
    }

    /// <summary>
    /// Generates a random UpdateItemRequest with valid properties.
    /// </summary>
    private static Arbitrary<UpdateItemRequest> GenerateUpdateItemRequest()
    {
        return Arb.From(
            from tableName in Arb.Default.NonEmptyString().Generator
            from pkValue in Arb.Default.NonEmptyString().Generator
            from skValue in Arb.Default.NonEmptyString().Generator
            from hasCondition in Arb.Default.Bool().Generator
            let condition = hasCondition ? "attribute_exists(pk)" : null
            select new UpdateItemRequest
            {
                TableName = tableName.Get,
                Key = new Dictionary<string, AttributeValue>
                {
                    ["pk"] = new AttributeValue { S = pkValue.Get },
                    ["sk"] = new AttributeValue { S = skValue.Get }
                },
                UpdateExpression = "SET #attr = :val",
                ConditionExpression = condition
            });
    }

    /// <summary>
    /// Generates a random DeleteItemRequest with valid properties.
    /// </summary>
    private static Arbitrary<DeleteItemRequest> GenerateDeleteItemRequest()
    {
        return Arb.From(
            from tableName in Arb.Default.NonEmptyString().Generator
            from pkValue in Arb.Default.NonEmptyString().Generator
            from skValue in Arb.Default.NonEmptyString().Generator
            from hasCondition in Arb.Default.Bool().Generator
            let condition = hasCondition ? "attribute_exists(pk)" : null
            select new DeleteItemRequest
            {
                TableName = tableName.Get,
                Key = new Dictionary<string, AttributeValue>
                {
                    ["pk"] = new AttributeValue { S = pkValue.Get },
                    ["sk"] = new AttributeValue { S = skValue.Get }
                },
                ConditionExpression = condition
            });
    }

    #endregion
}
