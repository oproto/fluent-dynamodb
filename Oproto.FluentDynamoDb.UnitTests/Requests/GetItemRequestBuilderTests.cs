using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using Amazon.Runtime;
using AwesomeAssertions;
using NSubstitute;
using Oproto.FluentDynamoDb.Logging;
using Oproto.FluentDynamoDb.Requests;
using Oproto.FluentDynamoDb.Requests.Extensions;
using Oproto.FluentDynamoDb.Storage;

namespace Oproto.FluentDynamoDb.UnitTests.Requests;

[Collection("OperationContext")]
public class GetItemRequestBuilderTests
{
    private class TestEntity : IDynamoDbEntity
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;

        public static Dictionary<string, AttributeValue> ToDynamoDb<TSelf>(TSelf entity, IDynamoDbLogger? logger = null) where TSelf : IDynamoDbEntity
        {
            var testEntity = entity as TestEntity;
            return new Dictionary<string, AttributeValue>
            {
                ["pk"] = new AttributeValue { S = testEntity?.Id ?? string.Empty },
                ["name"] = new AttributeValue { S = testEntity?.Name ?? string.Empty }
            };
        }

        public static TSelf FromDynamoDb<TSelf>(Dictionary<string, AttributeValue> item, IDynamoDbLogger? logger = null) where TSelf : IDynamoDbEntity
        {
            var entity = new TestEntity
            {
                Id = item.TryGetValue("pk", out var pk) ? pk.S : string.Empty,
                Name = item.TryGetValue("name", out var name) ? name.S : string.Empty
            };
            return (TSelf)(object)entity;
        }

        public static TSelf FromDynamoDb<TSelf>(IList<Dictionary<string, AttributeValue>> items, IDynamoDbLogger? logger = null) where TSelf : IDynamoDbEntity
        {
            return FromDynamoDb<TSelf>(items.First(), logger);
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
        var builder = new GetItemRequestBuilder<TestEntity>(Substitute.For<IAmazonDynamoDB>());
        builder.ForTable("TestTable");
        var req = builder.ToGetItemRequest();
        req.Should().NotBeNull();
        req.TableName.Should().Be("TestTable");
    }

    [Fact]
    public void WithKeyPkStringValueSuccess()
    {
        var builder = new GetItemRequestBuilder<TestEntity>(Substitute.For<IAmazonDynamoDB>());
        builder.WithKey("pk", "1");
        var req = builder.ToGetItemRequest();
        req.Should().NotBeNull();
        req.Key.Should().NotBeNull();
        req.Key.Should().ContainKey("pk");
        req.Key.Keys.Should().HaveCount(1);
        req.Key["pk"].S.Should().Be("1");
    }

    [Fact]
    public void WithKeyPkSkStringValueSuccess()
    {
        var builder = new GetItemRequestBuilder<TestEntity>(Substitute.For<IAmazonDynamoDB>());
        builder.WithKey("pk", "1", "sk", "abcd");
        var req = builder.ToGetItemRequest();
        req.Should().NotBeNull();
        req.Key.Should().NotBeNull();
        req.Key.Should().ContainKey("pk");
        req.Key.Should().ContainKey("sk");
        req.Key.Keys.Should().HaveCount(2);
        req.Key["pk"].S.Should().Be("1");
        req.Key["sk"].S.Should().Be("abcd");
    }

    [Fact]
    public void WithKeyPkSkAttributeValueSuccess()
    {
        var builder = new GetItemRequestBuilder<TestEntity>(Substitute.For<IAmazonDynamoDB>());
        builder.WithKey("pk", new AttributeValue() { S = "1" }, "sk", new AttributeValue() { S = "abcd" });
        var req = builder.ToGetItemRequest();
        req.Should().NotBeNull();
        req.Key.Should().NotBeNull();
        req.Key.Should().ContainKey("pk");
        req.Key.Should().ContainKey("sk");
        req.Key.Keys.Should().HaveCount(2);
        req.Key["pk"].S.Should().Be("1");
        req.Key["sk"].S.Should().Be("abcd");
    }

    [Fact]
    public void UsingExpressionAttributeNamesSuccess()
    {
        var builder = new GetItemRequestBuilder<TestEntity>(Substitute.For<IAmazonDynamoDB>());
        builder.WithAttributes(new Dictionary<string, string>() { { "#pk", "pk" } });
        var req = builder.ToGetItemRequest();
        req.Should().NotBeNull();
        req.ExpressionAttributeNames.Should().NotBeNull();
        req.ExpressionAttributeNames.Should().HaveCount(1);
        req.ExpressionAttributeNames["#pk"].Should().Be("pk");
    }

    [Fact]
    public void UsingExpressionAttributeNamesUsingLambdaSuccess()
    {
        var builder = new GetItemRequestBuilder<TestEntity>(Substitute.For<IAmazonDynamoDB>());
        builder.WithAttributes((attributes) => attributes.Add("#pk", "pk"));
        var req = builder.ToGetItemRequest();
        req.Should().NotBeNull();
        req.ExpressionAttributeNames.Should().NotBeNull();
        req.ExpressionAttributeNames.Should().HaveCount(1);
        req.ExpressionAttributeNames["#pk"].Should().Be("pk");
    }

    [Fact]
    public void UsingExpressionAttributeNameSuccess()
    {
        var builder = new GetItemRequestBuilder<TestEntity>(Substitute.For<IAmazonDynamoDB>());
        builder.WithAttribute("#pk", "pk");
        var req = builder.ToGetItemRequest();
        req.Should().NotBeNull();
        req.ExpressionAttributeNames.Should().NotBeNull();
        req.ExpressionAttributeNames.Should().HaveCount(1);
        req.ExpressionAttributeNames["#pk"].Should().Be("pk");
    }

    [Fact]
    public void ProjectionExpressionSuccess()
    {
        var builder = new GetItemRequestBuilder<TestEntity>(Substitute.For<IAmazonDynamoDB>());
        builder.WithProjection("description, price");
        var req = builder.ToGetItemRequest();
        req.Should().NotBeNull();
        req.ProjectionExpression.Should().Be("description, price");
    }

    [Fact]
    public void ReturnConsumedCapacitySuccess()
    {
        var builder = new GetItemRequestBuilder<TestEntity>(Substitute.For<IAmazonDynamoDB>());
        builder.ReturnConsumedCapacity(ReturnConsumedCapacity.TOTAL);
        var req = builder.ToGetItemRequest();
        req.Should().NotBeNull();
        req.ReturnConsumedCapacity.Should().Be(ReturnConsumedCapacity.TOTAL);
    }

    [Fact]
    public void ReturnTotalConsumedCapacitySuccess()
    {
        var builder = new GetItemRequestBuilder<TestEntity>(Substitute.For<IAmazonDynamoDB>());
        builder.ReturnTotalConsumedCapacity();
        var req = builder.ToGetItemRequest();
        req.Should().NotBeNull();
        req.ReturnConsumedCapacity.Should().Be(ReturnConsumedCapacity.TOTAL);
    }

    [Fact]
    public void UsingConsistentReadSuccess()
    {
        var builder = new GetItemRequestBuilder<TestEntity>(Substitute.For<IAmazonDynamoDB>());
        builder.UsingConsistentRead();
        var req = builder.ToGetItemRequest();
        req.Should().NotBeNull();
        req.ConsistentRead.Should().BeTrue();
    }

    #region GetItemAsync Tests

    [Fact]
    public async Task GetItemAsync_Success_ReturnsEntity()
    {
        // Arrange
        var mockClient = Substitute.For<IAmazonDynamoDB>();
        var builder = new GetItemRequestBuilder<TestEntity>(mockClient);
        builder.ForTable("TestTable").WithKey("pk", "test-id");

        var mockResponse = new GetItemResponse
        {
            Item = new Dictionary<string, AttributeValue>
            {
                ["pk"] = new AttributeValue { S = "test-id" },
                ["name"] = new AttributeValue { S = "test-name" }
            },
            ConsumedCapacity = new ConsumedCapacity { CapacityUnits = 1.0 }
        };

        mockClient.GetItemAsync(Arg.Any<GetItemRequest>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(mockResponse));

        // Act
        var result = await builder.GetItemAsync<TestEntity>();

        // Assert
        result.Should().NotBeNull();
        result!.Id.Should().Be("test-id");
        result.Name.Should().Be("test-name");
    }

    [Fact]
    public async Task GetItemAsync_ItemNotFound_ReturnsNull()
    {
        // Arrange
        var mockClient = Substitute.For<IAmazonDynamoDB>();
        var builder = new GetItemRequestBuilder<TestEntity>(mockClient);
        builder.ForTable("TestTable").WithKey("pk", "non-existent");

        var mockResponse = new GetItemResponse
        {
            Item = null
        };

        mockClient.GetItemAsync(Arg.Any<GetItemRequest>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(mockResponse));

        // Act
        var result = await builder.GetItemAsync<TestEntity>();

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task GetItemAsync_WithCancellationToken_PassesTokenToClient()
    {
        // Arrange
        var mockClient = Substitute.For<IAmazonDynamoDB>();
        var builder = new GetItemRequestBuilder<TestEntity>(mockClient);
        builder.ForTable("TestTable").WithKey("pk", "test-id");
        var cancellationToken = new CancellationToken();

        var mockResponse = new GetItemResponse
        {
            Item = new Dictionary<string, AttributeValue>
            {
                ["pk"] = new AttributeValue { S = "test-id" }
            }
        };

        mockClient.GetItemAsync(Arg.Any<GetItemRequest>(), cancellationToken)
            .Returns(Task.FromResult(mockResponse));

        // Act
        await builder.GetItemAsync<TestEntity>(cancellationToken);

        // Assert
        await mockClient.Received(1).GetItemAsync(Arg.Any<GetItemRequest>(), cancellationToken);
    }

    #endregion GetItemAsync Tests

    #region Context Population Tests

    [Fact]
    public async Task GetItemAsync_PopulatesContext_WithResponseMetadata()
    {
        // Arrange
        DynamoDbOperationContext.Clear();
        
        var mockClient = Substitute.For<IAmazonDynamoDB>();
        var builder = new GetItemRequestBuilder<TestEntity>(mockClient);
        builder.ForTable("TestTable").WithKey("pk", "test-id");

        var mockResponse = new GetItemResponse
        {
            Item = new Dictionary<string, AttributeValue>
            {
                ["pk"] = new AttributeValue { S = "test-id" },
                ["name"] = new AttributeValue { S = "test-name" }
            },
            ConsumedCapacity = new ConsumedCapacity
            {
                TableName = "TestTable",
                CapacityUnits = 1.5
            },
            ResponseMetadata = new ResponseMetadata
            {
                RequestId = "test-request-id"
            }
        };

        mockClient.GetItemAsync(Arg.Any<GetItemRequest>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(mockResponse));

        // Act
        var context = await ExecuteGetAndCaptureContextAsync(builder);

        // Assert
        context.Should().NotBeNull();
        context!.OperationType.Should().Be("GetItem");
        context.TableName.Should().Be("TestTable");
        context.ConsumedCapacity.Should().NotBeNull();
        context.ConsumedCapacity!.CapacityUnits.Should().Be(1.5);
        context.ResponseMetadata.Should().NotBeNull();
        context.ResponseMetadata!.RequestId.Should().Be("test-request-id");
    }

    [Fact]
    public async Task GetItemAsync_PopulatesContext_WithRawItem()
    {
        // Arrange
        DynamoDbOperationContext.Clear();
        
        var mockClient = Substitute.For<IAmazonDynamoDB>();
        var builder = new GetItemRequestBuilder<TestEntity>(mockClient);
        builder.ForTable("TestTable").WithKey("pk", "test-id");

        var rawItem = new Dictionary<string, AttributeValue>
        {
            ["pk"] = new AttributeValue { S = "test-id" },
            ["name"] = new AttributeValue { S = "test-name" }
        };

        var mockResponse = new GetItemResponse
        {
            Item = rawItem
        };

        mockClient.GetItemAsync(Arg.Any<GetItemRequest>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(mockResponse));

        // Act
        var context = await ExecuteGetAndCaptureContextAsync(builder);

        // Assert
        context.Should().NotBeNull();
        context!.RawItem.Should().NotBeNull();
        context.RawItem.Should().BeSameAs(rawItem);
        context.RawItem!["pk"].S.Should().Be("test-id");
        context.RawItem["name"].S.Should().Be("test-name");
    }

    [Fact]
    public async Task GetItemAsync_ItemNotFound_PopulatesContextWithNullRawItem()
    {
        // Arrange
        DynamoDbOperationContext.Clear();
        
        var mockClient = Substitute.For<IAmazonDynamoDB>();
        var builder = new GetItemRequestBuilder<TestEntity>(mockClient);
        builder.ForTable("TestTable").WithKey("pk", "non-existent");

        var mockResponse = new GetItemResponse
        {
            Item = null
        };

        mockClient.GetItemAsync(Arg.Any<GetItemRequest>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(mockResponse));

        // Act
        var context = await ExecuteGetAndCaptureContextAsync(builder);

        // Assert
        context.Should().NotBeNull();
        context!.RawItem.Should().BeNull();
    }

    [Fact]
    public async Task GetItemAsync_Exception_DoesNotPopulateContext()
    {
        // Arrange
        var mockClient = Substitute.For<IAmazonDynamoDB>();
        var builder = new GetItemRequestBuilder<TestEntity>(mockClient);
        builder.ForTable("TestTable").WithKey("pk", "test-id");

        // Clear any existing context
        DynamoDbOperationContext.Clear();

        var originalException = new Exception("Test exception");
        mockClient.GetItemAsync(Arg.Any<GetItemRequest>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException<GetItemResponse>(originalException));

        // Act & Assert
        var exception = await Assert.ThrowsAsync<DynamoDbMappingException>(() => builder.GetItemAsync<TestEntity>());

        // Verify inner exception is the original exception type
        exception.InnerException.Should().NotBeNull();
        exception.InnerException.Should().BeSameAs(originalException);

        // Context should remain null
        DynamoDbOperationContext.Current.Should().BeNull();
    }

    [Fact]
    public async Task GetItemAsync_SequentialCalls_ReplacesContext()
    {
        // Arrange
        DynamoDbOperationContext.Clear();
        
        var mockClient = Substitute.For<IAmazonDynamoDB>();
        var builder1 = new GetItemRequestBuilder<TestEntity>(mockClient);
        builder1.ForTable("Table1").WithKey("pk", "id1");

        var builder2 = new GetItemRequestBuilder<TestEntity>(mockClient);
        builder2.ForTable("Table2").WithKey("pk", "id2");

        var mockResponse1 = new GetItemResponse
        {
            Item = new Dictionary<string, AttributeValue>
            {
                ["pk"] = new AttributeValue { S = "id1" }
            },
            ConsumedCapacity = new ConsumedCapacity { CapacityUnits = 1.0 }
        };

        var mockResponse2 = new GetItemResponse
        {
            Item = new Dictionary<string, AttributeValue>
            {
                ["pk"] = new AttributeValue { S = "id2" }
            },
            ConsumedCapacity = new ConsumedCapacity { CapacityUnits = 2.0 }
        };

        mockClient.GetItemAsync(Arg.Is<GetItemRequest>(r => r.TableName == "Table1"), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(mockResponse1));

        mockClient.GetItemAsync(Arg.Is<GetItemRequest>(r => r.TableName == "Table2"), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(mockResponse2));

        // Act
        var contextAfterFirst = await ExecuteGetAndCaptureContextAsync(builder1);

        var contextAfterSecond = await ExecuteGetAndCaptureContextAsync(builder2);

        // Assert
        contextAfterFirst.Should().NotBeNull();
        contextAfterFirst!.TableName.Should().Be("Table1");
        contextAfterFirst.ConsumedCapacity!.CapacityUnits.Should().Be(1.0);

        contextAfterSecond.Should().NotBeNull();
        contextAfterSecond!.TableName.Should().Be("Table2");
        contextAfterSecond.ConsumedCapacity!.CapacityUnits.Should().Be(2.0);
    }

    #endregion Context Population Tests

    #region Helper Methods

    private static async Task<OperationContextData?> ExecuteGetAndCaptureContextAsync<T>(
        GetItemRequestBuilder<T> builder,
        CancellationToken cancellationToken = default)
        where T : class, IDynamoDbEntity
    {
        var tcs = new TaskCompletionSource<OperationContextData?>();
        void Handler(OperationContextData? ctx) => tcs.TrySetResult(ctx);
        DynamoDbOperationContextDiagnostics.ContextAssigned += Handler;

        try
        {
            await builder.GetItemAsync<T>(cancellationToken);
            return await tcs.Task.ConfigureAwait(false);
        }
        finally
        {
            DynamoDbOperationContextDiagnostics.ContextAssigned -= Handler;
        }
    }

    #endregion Helper Methods
}
