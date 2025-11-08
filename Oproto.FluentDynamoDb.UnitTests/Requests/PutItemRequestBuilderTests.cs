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
public class PutItemRequestBuilderTests
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
        var builder = new PutItemRequestBuilder<TestEntity>(Substitute.For<IAmazonDynamoDB>());
        builder.ForTable("TestTable");
        var req = builder.ToPutItemRequest();
        req.Should().NotBeNull();
        req.TableName.Should().Be("TestTable");
    }

    #region Attributes

    [Fact]
    public void UsingExpressionAttributeNamesSuccess()
    {
        var builder = new PutItemRequestBuilder<TestEntity>(Substitute.For<IAmazonDynamoDB>());
        builder.WithAttributes(new Dictionary<string, string>() { { "#pk", "pk" } });
        var req = builder.ToPutItemRequest();
        req.Should().NotBeNull();
        req.ExpressionAttributeNames.Should().NotBeNull();
        req.ExpressionAttributeNames.Should().HaveCount(1);
        req.ExpressionAttributeNames["#pk"].Should().Be("pk");
    }

    [Fact]
    public void UsingExpressionAttributeNamesUsingLambdaSuccess()
    {
        var builder = new PutItemRequestBuilder<TestEntity>(Substitute.For<IAmazonDynamoDB>());
        builder.WithAttributes((attributes) => attributes.Add("#pk", "pk"));
        var req = builder.ToPutItemRequest();
        req.Should().NotBeNull();
        req.ExpressionAttributeNames.Should().NotBeNull();
        req.ExpressionAttributeNames.Should().HaveCount(1);
        req.ExpressionAttributeNames["#pk"].Should().Be("pk");
    }

    [Fact]
    public void UsingExpressionAttributeNameSuccess()
    {
        var builder = new PutItemRequestBuilder<TestEntity>(Substitute.For<IAmazonDynamoDB>());
        builder.WithAttribute("#pk", "pk");
        var req = builder.ToPutItemRequest();
        req.Should().NotBeNull();
        req.ExpressionAttributeNames.Should().NotBeNull();
        req.ExpressionAttributeNames.Should().HaveCount(1);
        req.ExpressionAttributeNames["#pk"].Should().Be("pk");
    }

    [Fact]
    public void UsingExpressionAttributeValuesSuccess()
    {
        var builder = new PutItemRequestBuilder<TestEntity>(Substitute.For<IAmazonDynamoDB>());
        builder.WithValues(new Dictionary<string, AttributeValue>() { { ":pk", new AttributeValue { S = "1" } } });
        var req = builder.ToPutItemRequest();
        req.Should().NotBeNull();
        req.ExpressionAttributeValues.Should().NotBeNull();
        req.ExpressionAttributeValues.Should().HaveCount(1);
        req.ExpressionAttributeValues[":pk"].S.Should().Be("1");

    }

    [Fact]
    public void UsingExpressionAttributeValuesLambdaSuccess()
    {
        var builder = new PutItemRequestBuilder<TestEntity>(Substitute.For<IAmazonDynamoDB>());
        builder.WithValues((attributes) => attributes.Add(":pk", new AttributeValue { S = "1" }));
        var req = builder.ToPutItemRequest();
        req.Should().NotBeNull();
        req.ExpressionAttributeValues.Should().NotBeNull();
        req.ExpressionAttributeValues.Should().HaveCount(1);
        req.ExpressionAttributeValues[":pk"].S.Should().Be("1");

    }

    [Fact]
    public void UsingExpressionAttributeStringValueSuccess()
    {
        var builder = new PutItemRequestBuilder<TestEntity>(Substitute.For<IAmazonDynamoDB>());
        builder.WithValue(":pk", "1");
        var req = builder.ToPutItemRequest();
        req.Should().NotBeNull();
        req.ExpressionAttributeValues.Should().NotBeNull();
        req.ExpressionAttributeValues.Should().HaveCount(1);
        req.ExpressionAttributeValues[":pk"].S.Should().Be("1");
    }

    [Fact]
    public void UsingExpressionAttributeBooleanValueSuccess()
    {
        var builder = new PutItemRequestBuilder<TestEntity>(Substitute.For<IAmazonDynamoDB>());
        builder.WithValue(":pk", true);
        var req = builder.ToPutItemRequest();
        req.Should().NotBeNull();
        req.ExpressionAttributeValues.Should().NotBeNull();
        req.ExpressionAttributeValues.Should().HaveCount(1);
        req.ExpressionAttributeValues[":pk"].BOOL.Should().BeTrue();
    }

    #endregion Attributes

    [Fact]
    public void WithItemSuccess()
    {
        var builder = new PutItemRequestBuilder<TestEntity>(Substitute.For<IAmazonDynamoDB>());
        builder.WithItem(new Dictionary<string, AttributeValue>() { { "pk", new AttributeValue() { S = "1" } } });
        var req = builder.ToPutItemRequest();
        req.Should().NotBeNull();
        req.Item.Should().NotBeNull();
        req.Item.Should().HaveCount(1);
        req.Item["pk"].S.Should().Be("1");
    }

    [Fact]
    public void WithItemLambdaSuccess()
    {
        var builder = new PutItemRequestBuilder<TestEntity>(Substitute.For<IAmazonDynamoDB>());
        builder.WithItem(new { Pk = "1" }, (item) =>
        {
            return new Dictionary<string, AttributeValue>() { { "pk", new AttributeValue() { S = item.Pk } } };
        });
        var req = builder.ToPutItemRequest();
        req.Should().NotBeNull();
        req.Item.Should().NotBeNull();
        req.Item.Should().HaveCount(1);
        req.Item["pk"].S.Should().Be("1");
    }

    [Fact]
    public void WhereSuccess()
    {
        var builder = new PutItemRequestBuilder<TestEntity>(Substitute.For<IAmazonDynamoDB>());
        builder.Where("#pk = :pk");
        var req = builder.ToPutItemRequest();
        req.Should().NotBeNull();
        req.ConditionExpression.Should().Be("#pk = :pk");
    }

    #region ConsumedCapacity

    [Fact]
    public void ReturnConsumedCapacitySuccess()
    {
        var builder = new PutItemRequestBuilder<TestEntity>(Substitute.For<IAmazonDynamoDB>());
        builder.ReturnConsumedCapacity(ReturnConsumedCapacity.TOTAL);
        var req = builder.ToPutItemRequest();
        req.Should().NotBeNull();
        req.ReturnConsumedCapacity.Should().Be(ReturnConsumedCapacity.TOTAL);
    }

    [Fact]
    public void ReturnTotalConsumedCapacitySuccess()
    {
        var builder = new PutItemRequestBuilder<TestEntity>(Substitute.For<IAmazonDynamoDB>());
        builder.ReturnTotalConsumedCapacity();
        var req = builder.ToPutItemRequest();
        req.Should().NotBeNull();
        req.ReturnConsumedCapacity.Should().Be(ReturnConsumedCapacity.TOTAL);
    }

    [Fact]
    public void ReturnItemCollectionMetricsSuccess()
    {
        var builder = new PutItemRequestBuilder<TestEntity>(Substitute.For<IAmazonDynamoDB>());
        builder.ReturnItemCollectionMetrics();
        var req = builder.ToPutItemRequest();
        req.Should().NotBeNull();
        req.ReturnItemCollectionMetrics.Should().Be(ReturnItemCollectionMetrics.SIZE);
    }

    #endregion ConsumedCapacity

    #region ReturnValues

    [Fact]
    public void ReturnValuesNoneSuccess()
    {
        var builder = new PutItemRequestBuilder<TestEntity>(Substitute.For<IAmazonDynamoDB>());
        builder.ReturnNone();
        var req = builder.ToPutItemRequest();
        req.Should().NotBeNull();
        req.ReturnValues.Should().Be(ReturnValue.NONE);
    }

    [Fact]
    public void ReturnAllNewValuesSuccess()
    {
        var builder = new PutItemRequestBuilder<TestEntity>(Substitute.For<IAmazonDynamoDB>());
        builder.ReturnAllNewValues();
        var req = builder.ToPutItemRequest();
        req.Should().NotBeNull();
        req.ReturnValues.Should().Be(ReturnValue.ALL_NEW);
    }

    [Fact]
    public void ReturnAllOldValuesSuccess()
    {
        var builder = new PutItemRequestBuilder<TestEntity>(Substitute.For<IAmazonDynamoDB>());
        builder.ReturnAllOldValues();
        var req = builder.ToPutItemRequest();
        req.Should().NotBeNull();
        req.ReturnValues.Should().Be(ReturnValue.ALL_OLD);
    }

    [Fact]
    public void ReturnUpdatedNewValuesSuccess()
    {
        var builder = new PutItemRequestBuilder<TestEntity>(Substitute.For<IAmazonDynamoDB>());
        builder.ReturnUpdatedNewValues();
        var req = builder.ToPutItemRequest();
        req.Should().NotBeNull();
        req.ReturnValues.Should().Be(ReturnValue.UPDATED_NEW);
    }

    [Fact]
    public void ReturnUpdatedOldValuesSuccess()
    {
        var builder = new PutItemRequestBuilder<TestEntity>(Substitute.For<IAmazonDynamoDB>());
        builder.ReturnUpdatedOldValues();
        var req = builder.ToPutItemRequest();
        req.Should().NotBeNull();
        req.ReturnValues.Should().Be(ReturnValue.UPDATED_OLD);
    }

    [Fact]
    public void ReturnOldValuesOnConditionCheckFailureSuccess()
    {
        var builder = new PutItemRequestBuilder<TestEntity>(Substitute.For<IAmazonDynamoDB>());
        builder.ReturnOldValuesOnConditionCheckFailure();
        var req = builder.ToPutItemRequest();
        req.Should().NotBeNull();
        req.ReturnValuesOnConditionCheckFailure.Should().Be(ReturnValuesOnConditionCheckFailure.ALL_OLD);
    }

    [Fact]
    public void ReturnNoValuesOnConditionCheckFailureSuccess()
    {
        var builder = new PutItemRequestBuilder<TestEntity>(Substitute.For<IAmazonDynamoDB>());
        var req = builder.ToPutItemRequest();
        req.Should().NotBeNull();
        req.ReturnValuesOnConditionCheckFailure.Should().BeNull();
    }

    #endregion ReturnValues

    #region PutAsync Tests

    [Fact]
    public async Task PutAsync_Success_CompletesWithoutError()
    {
        // Arrange
        var mockClient = Substitute.For<IAmazonDynamoDB>();
        var builder = new PutItemRequestBuilder<TestEntity>(mockClient);
        var entity = new TestEntity { Id = "test-id", Name = "test-name" };
        
        builder.ForTable("TestTable").WithItem(entity, e => TestEntity.ToDynamoDb(e));

        var mockResponse = new PutItemResponse
        {
            ConsumedCapacity = new ConsumedCapacity { CapacityUnits = 1.0 }
        };

        mockClient.PutItemAsync(Arg.Any<PutItemRequest>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(mockResponse));

        // Act
        await builder.PutAsync<TestEntity>();

        // Assert
        await mockClient.Received(1).PutItemAsync(Arg.Any<PutItemRequest>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task PutAsync_WithCancellationToken_PassesTokenToClient()
    {
        // Arrange
        var mockClient = Substitute.For<IAmazonDynamoDB>();
        var builder = new PutItemRequestBuilder<TestEntity>(mockClient);
        var entity = new TestEntity { Id = "test-id", Name = "test-name" };
        var cancellationToken = new CancellationToken();
        
        builder.ForTable("TestTable").WithItem(entity, e => TestEntity.ToDynamoDb(e));

        var mockResponse = new PutItemResponse();

        mockClient.PutItemAsync(Arg.Any<PutItemRequest>(), cancellationToken)
            .Returns(Task.FromResult(mockResponse));

        // Act
        await builder.PutAsync<TestEntity>(cancellationToken);

        // Assert
        await mockClient.Received(1).PutItemAsync(Arg.Any<PutItemRequest>(), cancellationToken);
    }

    #endregion PutAsync Tests

    #region Context Population Tests

    [Fact]
    public async Task PutAsync_PopulatesContext_WithResponseMetadata()
    {
        // Arrange
        var mockClient = Substitute.For<IAmazonDynamoDB>();
        var builder = new PutItemRequestBuilder<TestEntity>(mockClient);
        var entity = new TestEntity { Id = "test-id", Name = "test-name" };
        
        builder.ForTable("TestTable").WithItem(entity, e => TestEntity.ToDynamoDb(e));

        var mockResponse = new PutItemResponse
        {
            ConsumedCapacity = new ConsumedCapacity
            {
                TableName = "TestTable",
                CapacityUnits = 2.5
            },
            ItemCollectionMetrics = new ItemCollectionMetrics
            {
                ItemCollectionKey = new Dictionary<string, AttributeValue>
                {
                    ["pk"] = new AttributeValue { S = "test-id" }
                }
            },
            ResponseMetadata = new ResponseMetadata
            {
                RequestId = "test-request-id"
            }
        };

        mockClient.PutItemAsync(Arg.Any<PutItemRequest>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(mockResponse));

        // Act - Call helper that returns context from within async flow
        var context = await ExecutePutAndGetContextAsync(builder);

        // Assert
        context.Should().NotBeNull();
        context!.OperationType.Should().Be("PutItem");
        context.TableName.Should().Be("TestTable");
        context.ConsumedCapacity.Should().NotBeNull();
        context.ConsumedCapacity!.CapacityUnits.Should().Be(2.5);
        context.ItemCollectionMetrics.Should().NotBeNull();
        context.ResponseMetadata.Should().NotBeNull();
        context.ResponseMetadata!.RequestId.Should().Be("test-request-id");
    }

    [Fact]
    public async Task PutAsync_WithReturnAllOld_PopulatesPreOperationValues()
    {
        // Arrange
        var mockClient = Substitute.For<IAmazonDynamoDB>();
        var builder = new PutItemRequestBuilder<TestEntity>(mockClient);
        var entity = new TestEntity { Id = "test-id", Name = "new-name" };
        
        builder.ForTable("TestTable")
            .WithItem(entity, e => TestEntity.ToDynamoDb(e))
            .ReturnAllOldValues();

        var oldAttributes = new Dictionary<string, AttributeValue>
        {
            ["pk"] = new AttributeValue { S = "test-id" },
            ["name"] = new AttributeValue { S = "old-name" }
        };

        var mockResponse = new PutItemResponse
        {
            Attributes = oldAttributes,
            ConsumedCapacity = new ConsumedCapacity { CapacityUnits = 1.0 }
        };

        mockClient.PutItemAsync(Arg.Any<PutItemRequest>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(mockResponse));

        // Act - Call helper that returns context from within async flow
        var context = await ExecutePutAndGetContextAsync(builder);

        // Assert
        context.Should().NotBeNull();
        context!.PreOperationValues.Should().NotBeNull();
        context.PreOperationValues.Should().BeSameAs(oldAttributes);
        context.PreOperationValues!["pk"].S.Should().Be("test-id");
        context.PreOperationValues["name"].S.Should().Be("old-name");
    }

    [Fact]
    public async Task PutAsync_WithoutReturnValues_PopulatesNullPreOperationValues()
    {
        // Arrange
        var mockClient = Substitute.For<IAmazonDynamoDB>();
        var builder = new PutItemRequestBuilder<TestEntity>(mockClient);
        var entity = new TestEntity { Id = "test-id", Name = "test-name" };
        
        builder.ForTable("TestTable").WithItem(entity, e => TestEntity.ToDynamoDb(e));

        var mockResponse = new PutItemResponse
        {
            Attributes = null,
            ConsumedCapacity = new ConsumedCapacity { CapacityUnits = 1.0 }
        };

        mockClient.PutItemAsync(Arg.Any<PutItemRequest>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(mockResponse));

        // Act - Call helper that returns context from within async flow
        var context = await ExecutePutAndGetContextAsync(builder);

        // Assert
        context.Should().NotBeNull();
        context!.PreOperationValues.Should().BeNull();
    }

    [Fact]
    public async Task PutAsync_Exception_DoesNotPopulateContext()
    {
        // Arrange
        var mockClient = Substitute.For<IAmazonDynamoDB>();
        var builder = new PutItemRequestBuilder<TestEntity>(mockClient);
        var entity = new TestEntity { Id = "test-id", Name = "test-name" };
        
        builder.ForTable("TestTable").WithItem(entity, e => TestEntity.ToDynamoDb(e));

        // Clear any existing context
        DynamoDbOperationContext.Clear();

        var originalException = new Exception("Test exception");
        mockClient.PutItemAsync(Arg.Any<PutItemRequest>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException<PutItemResponse>(originalException));

        // Act & Assert
        var exception = await Assert.ThrowsAsync<DynamoDbMappingException>(() => builder.PutAsync<TestEntity>());

        // Verify inner exception is the original exception type
        exception.InnerException.Should().BeSameAs(originalException);

        // Context should remain null
        DynamoDbOperationContext.Current.Should().BeNull();
    }

    #endregion Context Population Tests

    #region Helper Methods

    /// <summary>
    /// Helper method to execute PutAsync and return the context from within the async flow.
    /// This is necessary because xUnit's synchronization context prevents AsyncLocal values
    /// from flowing back to the test method after await.
    /// </summary>
    private static async Task<OperationContextData?> ExecutePutAndGetContextAsync<T>(PutItemRequestBuilder<T> builder)
        where T : class
    {
        var tcs = new TaskCompletionSource<OperationContextData?>();
        void Handler(OperationContextData? ctx) => tcs.TrySetResult(ctx);
        DynamoDbOperationContextDiagnostics.ContextAssigned += Handler;

        try
        {
            await builder.PutAsync<T>();
            return await tcs.Task.ConfigureAwait(false);
        }
        finally
        {
            DynamoDbOperationContextDiagnostics.ContextAssigned -= Handler;
        }
    }

    #endregion Helper Methods
}
