using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using Amazon.Runtime;
using AwesomeAssertions;
using NSubstitute;

using Oproto.FluentDynamoDb.Context;
using Oproto.FluentDynamoDb.Entities;
using Oproto.FluentDynamoDb.Mapping;
using Oproto.FluentDynamoDb.Metadata;
using Oproto.FluentDynamoDb.Requests;
using Oproto.FluentDynamoDb.Requests.Extensions;

namespace Oproto.FluentDynamoDb.UnitTests.Requests;

[Collection("OperationContext")]
public class ScanRequestBuilderTests
{
    private class TestEntity : IDynamoDbEntity
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;

        public static Dictionary<string, AttributeValue> ToDynamoDb<TSelf>(TSelf entity, FluentDynamoDbOptions? options = null) where TSelf : IDynamoDbEntity
        {
            var testEntity = entity as TestEntity;
            return new Dictionary<string, AttributeValue>
            {
                ["pk"] = new AttributeValue { S = testEntity?.Id ?? string.Empty },
                ["name"] = new AttributeValue { S = testEntity?.Name ?? string.Empty }
            };
        }

        public static TSelf FromDynamoDb<TSelf>(Dictionary<string, AttributeValue> item, FluentDynamoDbOptions? options = null) where TSelf : IDynamoDbEntity
        {
            var entity = new TestEntity
            {
                Id = item.TryGetValue("pk", out var pk) ? pk.S : string.Empty,
                Name = item.TryGetValue("name", out var name) ? name.S : string.Empty
            };
            return (TSelf)(object)entity;
        }

        public static TSelf FromDynamoDb<TSelf>(IList<Dictionary<string, AttributeValue>> items, FluentDynamoDbOptions? options = null) where TSelf : IDynamoDbEntity
        {
            return FromDynamoDb<TSelf>(items.First(), options);
        }

        public static string GetPartitionKey(Dictionary<string, AttributeValue> item)
        {
            return item.TryGetValue("pk", out var pk) ? pk.S : string.Empty;
        }

        public static bool MatchesEntity(Dictionary<string, AttributeValue> item)
        {
            return item.ContainsKey("pk");
        }

        public static EntityMetadata GetEntityMetadata()
        {
            return new EntityMetadata
            {
                TableName = "test-table",
                Properties = Array.Empty<PropertyMetadata>(),
                Indexes = Array.Empty<IndexMetadata>(),
                Relationships = Array.Empty<RelationshipMetadata>()
            };
        }
    }
    [Fact]
    public void ForTableSuccess()
    {
        var builder = new ScanRequestBuilder<TestEntity>(Substitute.For<IAmazonDynamoDB>());
        builder.ForTable("TestTable");
        var req = builder.ToScanRequest();
        req.Should().NotBeNull();
        req.TableName.Should().Be("TestTable");
    }

    #region Filter Expression Tests

    [Fact]
    public void WithFilterSuccess()
    {
        var builder = new ScanRequestBuilder<TestEntity>(Substitute.For<IAmazonDynamoDB>());
        builder.WithFilter("#status = :status");
        var req = builder.ToScanRequest();
        req.Should().NotBeNull();
        req.FilterExpression.Should().Be("#status = :status");
    }

    [Fact]
    public void WithFilterComplexExpressionSuccess()
    {
        var builder = new ScanRequestBuilder<TestEntity>(Substitute.For<IAmazonDynamoDB>());
        builder.WithFilter("#status = :status AND #price > :minPrice");
        var req = builder.ToScanRequest();
        req.Should().NotBeNull();
        req.FilterExpression.Should().Be("#status = :status AND #price > :minPrice");
    }

    #endregion Filter Expression Tests

    #region Projection Expression Tests

    [Fact]
    public void WithProjectionSuccess()
    {
        var builder = new ScanRequestBuilder<TestEntity>(Substitute.For<IAmazonDynamoDB>());
        builder.WithProjection("#pk, #sk, #status");
        var req = builder.ToScanRequest();
        req.Should().NotBeNull();
        req.ProjectionExpression.Should().Be("#pk, #sk, #status");
        req.Select.Should().Be(Select.SPECIFIC_ATTRIBUTES);
    }

    [Fact]
    public void WithProjectionSingleAttributeSuccess()
    {
        var builder = new ScanRequestBuilder<TestEntity>(Substitute.For<IAmazonDynamoDB>());
        builder.WithProjection("#pk");
        var req = builder.ToScanRequest();
        req.Should().NotBeNull();
        req.ProjectionExpression.Should().Be("#pk");
        req.Select.Should().Be(Select.SPECIFIC_ATTRIBUTES);
    }

    #endregion Projection Expression Tests

    #region Index Tests

    [Fact]
    public void UsingIndexSuccess()
    {
        var builder = new ScanRequestBuilder<TestEntity>(Substitute.For<IAmazonDynamoDB>());
        builder.UsingIndex("gsi1");
        var req = builder.ToScanRequest();
        req.Should().NotBeNull();
        req.IndexName.Should().Be("gsi1");
    }

    [Fact]
    public void UsingIndexWithFilterSuccess()
    {
        var builder = new ScanRequestBuilder<TestEntity>(Substitute.For<IAmazonDynamoDB>());
        builder.UsingIndex("gsi1").WithFilter("#status = :status");
        var req = builder.ToScanRequest();
        req.Should().NotBeNull();
        req.IndexName.Should().Be("gsi1");
        req.FilterExpression.Should().Be("#status = :status");
    }

    #endregion Index Tests

    #region Limit and Pagination Tests

    [Fact]
    public void TakeSuccess()
    {
        var builder = new ScanRequestBuilder<TestEntity>(Substitute.For<IAmazonDynamoDB>());
        builder.Take(10);
        var req = builder.ToScanRequest();
        req.Should().NotBeNull();
        req.Limit.Should().Be(10);
    }

    [Fact]
    public void StartAtSuccess()
    {
        var builder = new ScanRequestBuilder<TestEntity>(Substitute.For<IAmazonDynamoDB>());
        var startKey = new Dictionary<string, AttributeValue>
        {
            { "pk", new AttributeValue { S = "test-pk" } },
            { "sk", new AttributeValue { S = "test-sk" } }
        };
        builder.StartAt(startKey);
        var req = builder.ToScanRequest();
        req.Should().NotBeNull();
        req.ExclusiveStartKey.Should().BeEquivalentTo(startKey);
    }

    #endregion Limit and Pagination Tests

    #region Parallel Scan Tests

    [Fact]
    public void WithSegmentSuccess()
    {
        var builder = new ScanRequestBuilder<TestEntity>(Substitute.For<IAmazonDynamoDB>());
        builder.WithSegment(0, 4);
        var req = builder.ToScanRequest();
        req.Should().NotBeNull();
        req.Segment.Should().Be(0);
        req.TotalSegments.Should().Be(4);
    }

    [Fact]
    public void WithSegmentMultipleSegmentsSuccess()
    {
        var builder = new ScanRequestBuilder<TestEntity>(Substitute.For<IAmazonDynamoDB>());
        builder.WithSegment(2, 8);
        var req = builder.ToScanRequest();
        req.Should().NotBeNull();
        req.Segment.Should().Be(2);
        req.TotalSegments.Should().Be(8);
    }

    [Fact]
    public void WithSegmentAndFilterSuccess()
    {
        var builder = new ScanRequestBuilder<TestEntity>(Substitute.For<IAmazonDynamoDB>());
        builder.WithSegment(1, 4).WithFilter("#status = :status");
        var req = builder.ToScanRequest();
        req.Should().NotBeNull();
        req.Segment.Should().Be(1);
        req.TotalSegments.Should().Be(4);
        req.FilterExpression.Should().Be("#status = :status");
    }

    #endregion Parallel Scan Tests

    #region Consistency Tests

    [Fact]
    public void UsingConsistentReadSuccess()
    {
        var builder = new ScanRequestBuilder<TestEntity>(Substitute.For<IAmazonDynamoDB>());
        builder.UsingConsistentRead();
        var req = builder.ToScanRequest();
        req.Should().NotBeNull();
        req.ConsistentRead.Should().BeTrue();
    }

    [Fact]
    public void DefaultConsistentReadSuccess()
    {
        var builder = new ScanRequestBuilder<TestEntity>(Substitute.For<IAmazonDynamoDB>());
        var req = builder.ToScanRequest();
        req.Should().NotBeNull();
        req.ConsistentRead.Should().BeFalse();
    }

    #endregion Consistency Tests

    #region Count Tests

    [Fact]
    public void CountSuccess()
    {
        var builder = new ScanRequestBuilder<TestEntity>(Substitute.For<IAmazonDynamoDB>());
        builder.Count();
        var req = builder.ToScanRequest();
        req.Should().NotBeNull();
        req.Select.Should().Be(Select.COUNT);
    }

    [Fact]
    public void CountOverridesProjectionSuccess()
    {
        var builder = new ScanRequestBuilder<TestEntity>(Substitute.For<IAmazonDynamoDB>());
        builder.WithProjection("#pk, #sk").Count();
        var req = builder.ToScanRequest();
        req.Should().NotBeNull();
        req.Select.Should().Be(Select.COUNT);
        req.ProjectionExpression.Should().Be("#pk, #sk"); // Projection expression remains but Select is COUNT
    }

    #endregion Count Tests

    #region Consumed Capacity Tests

    [Fact]
    public void ReturnTotalConsumedCapacitySuccess()
    {
        var builder = new ScanRequestBuilder<TestEntity>(Substitute.For<IAmazonDynamoDB>());
        builder.ReturnTotalConsumedCapacity();
        var req = builder.ToScanRequest();
        req.Should().NotBeNull();
        req.ReturnConsumedCapacity.Should().Be(ReturnConsumedCapacity.TOTAL);
    }

    [Fact]
    public void ReturnIndexConsumedCapacitySuccess()
    {
        var builder = new ScanRequestBuilder<TestEntity>(Substitute.For<IAmazonDynamoDB>());
        builder.ReturnIndexConsumedCapacity();
        var req = builder.ToScanRequest();
        req.Should().NotBeNull();
        req.ReturnConsumedCapacity.Should().Be(ReturnConsumedCapacity.INDEXES);
    }

    [Fact]
    public void ReturnConsumedCapacitySuccess()
    {
        var builder = new ScanRequestBuilder<TestEntity>(Substitute.For<IAmazonDynamoDB>());
        builder.ReturnConsumedCapacity(ReturnConsumedCapacity.NONE);
        var req = builder.ToScanRequest();
        req.Should().NotBeNull();
        req.ReturnConsumedCapacity.Should().Be(ReturnConsumedCapacity.NONE);
    }

    [Fact]
    public void DefaultConsumedCapacitySuccess()
    {
        var builder = new ScanRequestBuilder<TestEntity>(Substitute.For<IAmazonDynamoDB>());
        var req = builder.ToScanRequest();
        req.Should().NotBeNull();
        req.ReturnConsumedCapacity.Should().BeNull();
    }

    #endregion Consumed Capacity Tests

    #region Attribute Names Tests

    [Fact]
    public void WithAttributesSuccess()
    {
        var builder = new ScanRequestBuilder<TestEntity>(Substitute.For<IAmazonDynamoDB>());
        builder.WithAttributes(new Dictionary<string, string> { { "#pk", "pk" }, { "#status", "status" } });
        var req = builder.ToScanRequest();
        req.Should().NotBeNull();
        req.ExpressionAttributeNames.Should().NotBeNull();
        req.ExpressionAttributeNames.Should().HaveCount(2);
        req.ExpressionAttributeNames["#pk"].Should().Be("pk");
        req.ExpressionAttributeNames["#status"].Should().Be("status");
    }

    [Fact]
    public void WithAttributesUsingLambdaSuccess()
    {
        var builder = new ScanRequestBuilder<TestEntity>(Substitute.For<IAmazonDynamoDB>());
        builder.WithAttributes(attributes =>
        {
            attributes.Add("#pk", "pk");
            attributes.Add("#status", "status");
        });
        var req = builder.ToScanRequest();
        req.Should().NotBeNull();
        req.ExpressionAttributeNames.Should().NotBeNull();
        req.ExpressionAttributeNames.Should().HaveCount(2);
        req.ExpressionAttributeNames["#pk"].Should().Be("pk");
        req.ExpressionAttributeNames["#status"].Should().Be("status");
    }

    [Fact]
    public void WithAttributeSuccess()
    {
        var builder = new ScanRequestBuilder<TestEntity>(Substitute.For<IAmazonDynamoDB>());
        builder.WithAttribute("#pk", "pk");
        var req = builder.ToScanRequest();
        req.Should().NotBeNull();
        req.ExpressionAttributeNames.Should().NotBeNull();
        req.ExpressionAttributeNames.Should().HaveCount(1);
        req.ExpressionAttributeNames["#pk"].Should().Be("pk");
    }

    #endregion Attribute Names Tests

    #region Attribute Values Tests

    [Fact]
    public void WithValuesSuccess()
    {
        var builder = new ScanRequestBuilder<TestEntity>(Substitute.For<IAmazonDynamoDB>());
        builder.WithValues(new Dictionary<string, AttributeValue>
        {
            { ":pk", new AttributeValue { S = "1" } },
            { ":status", new AttributeValue { S = "active" } }
        });
        var req = builder.ToScanRequest();
        req.Should().NotBeNull();
        req.ExpressionAttributeValues.Should().NotBeNull();
        req.ExpressionAttributeValues.Should().HaveCount(2);
        req.ExpressionAttributeValues[":pk"].S.Should().Be("1");
        req.ExpressionAttributeValues[":status"].S.Should().Be("active");
    }

    [Fact]
    public void WithValuesLambdaSuccess()
    {
        var builder = new ScanRequestBuilder<TestEntity>(Substitute.For<IAmazonDynamoDB>());
        builder.WithValues(attributes =>
        {
            attributes.Add(":pk", new AttributeValue { S = "1" });
            attributes.Add(":status", new AttributeValue { S = "active" });
        });
        var req = builder.ToScanRequest();
        req.Should().NotBeNull();
        req.ExpressionAttributeValues.Should().NotBeNull();
        req.ExpressionAttributeValues.Should().HaveCount(2);
        req.ExpressionAttributeValues[":pk"].S.Should().Be("1");
        req.ExpressionAttributeValues[":status"].S.Should().Be("active");
    }

    [Fact]
    public void WithValueStringSuccess()
    {
        var builder = new ScanRequestBuilder<TestEntity>(Substitute.For<IAmazonDynamoDB>());
        builder.WithValue(":status", "active");
        var req = builder.ToScanRequest();
        req.Should().NotBeNull();
        req.ExpressionAttributeValues.Should().NotBeNull();
        req.ExpressionAttributeValues.Should().HaveCount(1);
        req.ExpressionAttributeValues[":status"].S.Should().Be("active");
    }

    [Fact]
    public void WithValueStringNullSuccess()
    {
        var builder = new ScanRequestBuilder<TestEntity>(Substitute.For<IAmazonDynamoDB>());
        builder.WithValue(":status", (string?)null);
        var req = builder.ToScanRequest();
        req.Should().NotBeNull();
        req.ExpressionAttributeValues.Should().NotBeNull();
        req.ExpressionAttributeValues.Should().HaveCount(0); // null string values are not added when conditionalUse is true
    }

    [Fact]
    public void WithValueBooleanSuccess()
    {
        var builder = new ScanRequestBuilder<TestEntity>(Substitute.For<IAmazonDynamoDB>());
        builder.WithValue(":active", true);
        var req = builder.ToScanRequest();
        req.Should().NotBeNull();
        req.ExpressionAttributeValues.Should().NotBeNull();
        req.ExpressionAttributeValues.Should().HaveCount(1);
        req.ExpressionAttributeValues[":active"].BOOL.Should().BeTrue();
    }

    [Fact]
    public void WithValueBooleanNullSuccess()
    {
        var builder = new ScanRequestBuilder<TestEntity>(Substitute.For<IAmazonDynamoDB>());
        builder.WithValue(":active", (bool?)null);
        var req = builder.ToScanRequest();
        req.Should().NotBeNull();
        req.ExpressionAttributeValues.Should().NotBeNull();
        req.ExpressionAttributeValues.Should().HaveCount(1);
        req.ExpressionAttributeValues[":active"].BOOL.Should().BeNull(); // null bool becomes null in SDK v4
        req.ExpressionAttributeValues[":active"].IsBOOLSet.Should().BeFalse(); // IsBOOLSet indicates it was null
    }

    [Fact]
    public void WithValueDecimalSuccess()
    {
        var builder = new ScanRequestBuilder<TestEntity>(Substitute.For<IAmazonDynamoDB>());
        builder.WithValue(":price", 99.99m);
        var req = builder.ToScanRequest();
        req.Should().NotBeNull();
        req.ExpressionAttributeValues.Should().NotBeNull();
        req.ExpressionAttributeValues.Should().HaveCount(1);
        req.ExpressionAttributeValues[":price"].N.Should().Be("99.99");
    }

    [Fact]
    public void WithValueDecimalNullSuccess()
    {
        var builder = new ScanRequestBuilder<TestEntity>(Substitute.For<IAmazonDynamoDB>());
        builder.WithValue(":price", (decimal?)null);
        var req = builder.ToScanRequest();
        req.Should().NotBeNull();
        req.ExpressionAttributeValues.Should().NotBeNull();
        req.ExpressionAttributeValues.Should().HaveCount(1);
        req.ExpressionAttributeValues[":price"].N.Should().Be(""); // null decimal becomes empty string
    }

    [Fact]
    public void WithValueStringDictionarySuccess()
    {
        var builder = new ScanRequestBuilder<TestEntity>(Substitute.For<IAmazonDynamoDB>());
        var mapValue = new Dictionary<string, string> { { "key1", "value1" }, { "key2", "value2" } };
        builder.WithValue(":map", mapValue);
        var req = builder.ToScanRequest();
        req.Should().NotBeNull();
        req.ExpressionAttributeValues.Should().NotBeNull();
        req.ExpressionAttributeValues.Should().HaveCount(1);
        req.ExpressionAttributeValues[":map"].M.Should().HaveCount(2);
        req.ExpressionAttributeValues[":map"].M["key1"].S.Should().Be("value1");
        req.ExpressionAttributeValues[":map"].M["key2"].S.Should().Be("value2");
    }

    [Fact]
    public void WithValueAttributeValueDictionarySuccess()
    {
        var builder = new ScanRequestBuilder<TestEntity>(Substitute.For<IAmazonDynamoDB>());
        var mapValue = new Dictionary<string, AttributeValue>
        {
            { "key1", new AttributeValue { S = "value1" } },
            { "key2", new AttributeValue { N = "42" } }
        };
        builder.WithValue(":map", mapValue);
        var req = builder.ToScanRequest();
        req.Should().NotBeNull();
        req.ExpressionAttributeValues.Should().NotBeNull();
        req.ExpressionAttributeValues.Should().HaveCount(1);
        req.ExpressionAttributeValues[":map"].M.Should().HaveCount(2);
        req.ExpressionAttributeValues[":map"].M["key1"].S.Should().Be("value1");
        req.ExpressionAttributeValues[":map"].M["key2"].N.Should().Be("42");
    }

    [Fact]
    public void WithValueConditionalUseFalseSuccess()
    {
        var builder = new ScanRequestBuilder<TestEntity>(Substitute.For<IAmazonDynamoDB>());
        builder.WithValue(":status", "active", conditionalUse: false);
        var req = builder.ToScanRequest();
        req.Should().NotBeNull();
        req.ExpressionAttributeValues.Should().NotBeNull();
        req.ExpressionAttributeValues.Should().HaveCount(0); // conditionalUse: false means nothing is added
    }

    [Fact]
    public void WithValueConditionalUseTrueSuccess()
    {
        var builder = new ScanRequestBuilder<TestEntity>(Substitute.For<IAmazonDynamoDB>());
        builder.WithValue(":status", "active", conditionalUse: true);
        var req = builder.ToScanRequest();
        req.Should().NotBeNull();
        req.ExpressionAttributeValues.Should().NotBeNull();
        req.ExpressionAttributeValues.Should().HaveCount(1);
        req.ExpressionAttributeValues[":status"].S.Should().Be("active");
    }

    #endregion Attribute Values Tests

    #region Request Building and Execution Tests

    [Fact]
    public void ToScanRequestSuccess()
    {
        var builder = new ScanRequestBuilder<TestEntity>(Substitute.For<IAmazonDynamoDB>());
        builder.ForTable("TestTable")
               .WithFilter("#status = :status")
               .WithProjection("#pk, #sk, #status")
               .UsingIndex("gsi1")
               .Take(10)
               .UsingConsistentRead()
               .WithSegment(0, 4)
               .WithAttribute("#status", "status")
               .WithValue(":status", "active")
               .ReturnTotalConsumedCapacity();

        var req = builder.ToScanRequest();

        req.Should().NotBeNull();
        req.TableName.Should().Be("TestTable");
        req.FilterExpression.Should().Be("#status = :status");
        req.ProjectionExpression.Should().Be("#pk, #sk, #status");
        req.Select.Should().Be(Select.SPECIFIC_ATTRIBUTES);
        req.IndexName.Should().Be("gsi1");
        req.Limit.Should().Be(10);
        req.ConsistentRead.Should().BeTrue();
        req.Segment.Should().Be(0);
        req.TotalSegments.Should().Be(4);
        req.ExpressionAttributeNames.Should().HaveCount(1);
        req.ExpressionAttributeNames["#status"].Should().Be("status");
        req.ExpressionAttributeValues.Should().HaveCount(1);
        req.ExpressionAttributeValues[":status"].S.Should().Be("active");
        req.ReturnConsumedCapacity.Should().Be(ReturnConsumedCapacity.TOTAL);
    }

    [Fact]
    public async Task ExecuteAsyncSuccess()
    {
        var mockClient = Substitute.For<IAmazonDynamoDB>();
        var expectedResponse = new ScanResponse
        {
            Items = new List<Dictionary<string, AttributeValue>>()
        };
        mockClient.ScanAsync(Arg.Any<ScanRequest>(), Arg.Any<CancellationToken>())
                  .Returns(Task.FromResult(expectedResponse));

        var builder = new ScanRequestBuilder<TestEntity>(mockClient);
        builder.ForTable("TestTable");

        var result = await builder.ToListAsync<TestEntity>();

        result.Should().NotBeNull();
        await mockClient.Received(1).ScanAsync(Arg.Any<ScanRequest>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsyncWithCancellationTokenSuccess()
    {
        var mockClient = Substitute.For<IAmazonDynamoDB>();
        var expectedResponse = new ScanResponse
        {
            Items = new List<Dictionary<string, AttributeValue>>()
        };
        var cancellationToken = new CancellationToken();

        mockClient.ScanAsync(Arg.Any<ScanRequest>(), cancellationToken)
                  .Returns(Task.FromResult(expectedResponse));

        var builder = new ScanRequestBuilder<TestEntity>(mockClient);
        builder.ForTable("TestTable");

        var result = await builder.ToListAsync<TestEntity>(cancellationToken);

        result.Should().NotBeNull();
        await mockClient.Received(1).ScanAsync(Arg.Any<ScanRequest>(), cancellationToken);
    }

    #endregion Request Building and Execution Tests

    #region Fluent Interface Tests

    [Fact]
    public void FluentInterfaceChainSuccess()
    {
        var builder = new ScanRequestBuilder<TestEntity>(Substitute.For<IAmazonDynamoDB>());

        var result = builder
            .ForTable("TestTable")
            .WithFilter("#status = :status")
            .WithProjection("#pk, #sk")
            .UsingIndex("gsi1")
            .Take(10)
            .UsingConsistentRead()
            .WithSegment(0, 4)
            .WithAttribute("#status", "status")
            .WithValue(":status", "active")
            .ReturnTotalConsumedCapacity();

        result.Should().Be(builder);

        var req = result.ToScanRequest();
        req.Should().NotBeNull();
        req.TableName.Should().Be("TestTable");
    }

    [Fact]
    public void MultipleAttributeAndValueCallsSuccess()
    {
        var builder = new ScanRequestBuilder<TestEntity>(Substitute.For<IAmazonDynamoDB>());

        builder.WithAttribute("#pk", "pk")
               .WithAttribute("#status", "status")
               .WithValue(":pk", "test-key")
               .WithValue(":status", "active")
               .WithValue(":price", 99.99m);

        var req = builder.ToScanRequest();

        req.Should().NotBeNull();
        req.ExpressionAttributeNames.Should().HaveCount(2);
        req.ExpressionAttributeNames["#pk"].Should().Be("pk");
        req.ExpressionAttributeNames["#status"].Should().Be("status");
        req.ExpressionAttributeValues.Should().HaveCount(3);
        req.ExpressionAttributeValues[":pk"].S.Should().Be("test-key");
        req.ExpressionAttributeValues[":status"].S.Should().Be("active");
        req.ExpressionAttributeValues[":price"].N.Should().Be("99.99");
    }

    #endregion Fluent Interface Tests
    
    #region ToListAsync Tests

    [Fact]
    public async Task ToListAsync_Success_ReturnsEntityList()
    {
        // Arrange
        var mockClient = Substitute.For<IAmazonDynamoDB>();
        var builder = new ScanRequestBuilder<TestEntity>(mockClient);
        builder.ForTable("TestTable");

        var mockResponse = new ScanResponse
        {
            Items = new List<Dictionary<string, AttributeValue>>
            {
                new() { ["pk"] = new AttributeValue { S = "id1" }, ["name"] = new AttributeValue { S = "name1" } },
                new() { ["pk"] = new AttributeValue { S = "id2" }, ["name"] = new AttributeValue { S = "name2" } }
            },
            Count = 2,
            ScannedCount = 2
        };

        mockClient.ScanAsync(Arg.Any<ScanRequest>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(mockResponse));

        // Act
        var result = await builder.ToListAsync<TestEntity>();

        // Assert
        result.Should().NotBeNull();
        result.Should().HaveCount(2);
        result[0].Id.Should().Be("id1");
        result[1].Id.Should().Be("id2");
    }

    [Fact]
    public async Task ToListAsync_EmptyResult_ReturnsEmptyList()
    {
        // Arrange
        var mockClient = Substitute.For<IAmazonDynamoDB>();
        var builder = new ScanRequestBuilder<TestEntity>(mockClient);
        builder.ForTable("TestTable");

        var mockResponse = new ScanResponse
        {
            Items = new List<Dictionary<string, AttributeValue>>(),
            Count = 0,
            ScannedCount = 0
        };

        mockClient.ScanAsync(Arg.Any<ScanRequest>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(mockResponse));

        // Act
        var result = await builder.ToListAsync<TestEntity>();

        // Assert
        result.Should().NotBeNull();
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task ToListAsync_WithCancellationToken_PassesTokenToClient()
    {
        // Arrange
        var mockClient = Substitute.For<IAmazonDynamoDB>();
        var builder = new ScanRequestBuilder<TestEntity>(mockClient);
        builder.ForTable("TestTable");
        var cancellationToken = new CancellationToken();

        var mockResponse = new ScanResponse
        {
            Items = new List<Dictionary<string, AttributeValue>>()
        };

        mockClient.ScanAsync(Arg.Any<ScanRequest>(), cancellationToken)
            .Returns(Task.FromResult(mockResponse));

        // Act
        await builder.ToListAsync<TestEntity>(cancellationToken);

        // Assert
        await mockClient.Received(1).ScanAsync(Arg.Any<ScanRequest>(), cancellationToken);
    }

    #endregion ToListAsync Tests

    #region Context Population Tests

    [Fact]
    public async Task ToListAsync_PopulatesContext_WithResponseMetadata()
    {
        // Arrange
        var mockClient = Substitute.For<IAmazonDynamoDB>();
        var builder = new ScanRequestBuilder<TestEntity>(mockClient);
        builder.ForTable("TestTable").UsingIndex("gsi1");

        var mockResponse = new ScanResponse
        {
            Items = new List<Dictionary<string, AttributeValue>>
            {
                new() { ["pk"] = new AttributeValue { S = "id1" } }
            },
            Count = 1,
            ScannedCount = 5,
            ConsumedCapacity = new ConsumedCapacity
            {
                TableName = "TestTable",
                CapacityUnits = 3.5
            },
            LastEvaluatedKey = new Dictionary<string, AttributeValue>
            {
                ["pk"] = new AttributeValue { S = "last-key" }
            },
            ResponseMetadata = new ResponseMetadata
            {
                RequestId = "test-request-id"
            }
        };

        mockClient.ScanAsync(Arg.Any<ScanRequest>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(mockResponse));

        // Act - Call helper that returns context from within async flow
        var context = await ExecuteScanAndGetContextAsync(builder);

        // Assert
        context.Should().NotBeNull();
        context!.OperationType.Should().Be("Scan");
        context.TableName.Should().Be("TestTable");
        context.IndexName.Should().Be("gsi1");
        context.ItemCount.Should().Be(1);
        context.ScannedCount.Should().Be(5);
        context.ConsumedCapacity.Should().NotBeNull();
        context.ConsumedCapacity!.CapacityUnits.Should().Be(3.5);
        context.LastEvaluatedKey.Should().NotBeNull();
        context.LastEvaluatedKey!["pk"].S.Should().Be("last-key");
        context.ResponseMetadata.Should().NotBeNull();
        context.ResponseMetadata!.RequestId.Should().Be("test-request-id");
    }

    [Fact]
    public async Task ToListAsync_PopulatesContext_WithRawItems()
    {
        // Arrange
        var mockClient = Substitute.For<IAmazonDynamoDB>();
        var builder = new ScanRequestBuilder<TestEntity>(mockClient);
        builder.ForTable("TestTable");

        var rawItems = new List<Dictionary<string, AttributeValue>>
        {
            new() { ["pk"] = new AttributeValue { S = "id1" }, ["name"] = new AttributeValue { S = "name1" } },
            new() { ["pk"] = new AttributeValue { S = "id2" }, ["name"] = new AttributeValue { S = "name2" } }
        };

        var mockResponse = new ScanResponse
        {
            Items = rawItems,
            Count = 2,
            ScannedCount = 2
        };

        mockClient.ScanAsync(Arg.Any<ScanRequest>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(mockResponse));

        // Act - Call helper that returns context from within async flow
        var context = await ExecuteScanAndGetContextAsync(builder);

        // Assert
        context.Should().NotBeNull();
        context!.RawItems.Should().NotBeNull();
        context.RawItems.Should().BeSameAs(rawItems);
        context.RawItems.Should().HaveCount(2);
        context.RawItems![0]["pk"].S.Should().Be("id1");
        context.RawItems[1]["pk"].S.Should().Be("id2");
    }

    [Fact]
    public async Task ToListAsync_WithFilter_PopulatesScannedCount()
    {
        // Arrange
        var mockClient = Substitute.For<IAmazonDynamoDB>();
        var builder = new ScanRequestBuilder<TestEntity>(mockClient);
        builder.ForTable("TestTable").WithFilter("#status = :status");

        var mockResponse = new ScanResponse
        {
            Items = new List<Dictionary<string, AttributeValue>>
            {
                new() { ["pk"] = new AttributeValue { S = "id1" } }
            },
            Count = 1,
            ScannedCount = 10 // Scanned 10 items but only 1 matched the filter
        };

        mockClient.ScanAsync(Arg.Any<ScanRequest>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(mockResponse));

        // Act - Call helper that returns context from within async flow
        var context = await ExecuteScanAndGetContextAsync(builder);

        // Assert
        context.Should().NotBeNull();
        context!.ItemCount.Should().Be(1);
        context.ScannedCount.Should().Be(10);
    }

    [Fact]
    public async Task ToListAsync_Exception_DoesNotPopulateContext()
    {
        // Arrange
        var mockClient = Substitute.For<IAmazonDynamoDB>();
        var builder = new ScanRequestBuilder<TestEntity>(mockClient);
        builder.ForTable("TestTable");

        // Clear any existing context
        DynamoDbOperationContext.Clear();

        var originalException = new Exception("Test exception");
        mockClient.ScanAsync(Arg.Any<ScanRequest>(), Arg.Any<CancellationToken>())
                  .Returns(Task.FromException<ScanResponse>(originalException));

        // Act & Assert
        var exception = await Assert.ThrowsAsync<DynamoDbMappingException>(() => builder.ToListAsync<TestEntity>());

        // Verify inner exception is preserved
        exception.InnerException.Should().BeSameAs(originalException);

        // Context should remain null
        DynamoDbOperationContext.Current.Should().BeNull();
    }

    #endregion Context Population Tests

    #region Helper Methods

    private static async Task<OperationContextData?> ExecuteScanAndGetContextAsync<T>(ScanRequestBuilder<T> builder)
        where T : class, IDynamoDbEntity
    {
        var tcs = new TaskCompletionSource<OperationContextData?>();
        void Handler(OperationContextData? ctx) => tcs.TrySetResult(ctx);
        DynamoDbOperationContextDiagnostics.ContextAssigned += Handler;

        try
        {
            await builder.ToListAsync<T>();
            return await tcs.Task.ConfigureAwait(false);
        }
        finally
        {
            DynamoDbOperationContextDiagnostics.ContextAssigned -= Handler;
        }
    }

    #endregion Helper Methods
}
