using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using Amazon.Runtime;
using AwesomeAssertions;
using NSubstitute;

using Oproto.FluentDynamoDb.Requests;
using Oproto.FluentDynamoDb.Requests.Extensions;
using Oproto.FluentDynamoDb.Storage;

namespace Oproto.FluentDynamoDb.UnitTests.Requests;

[Trait("Category", "Unit")]
[Collection("OperationContext")]
public class QueryRequestBuilderTests
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
        var builder = new QueryRequestBuilder<TestEntity>(Substitute.For<IAmazonDynamoDB>());
        builder.ForTable("TestTable");
        var req = builder.ToQueryRequest();
        req.Should().NotBeNull();
        req.TableName.Should().Be("TestTable");
    }

    [Fact]
    public void UsingIndexSuccess()
    {
        var builder = new QueryRequestBuilder<TestEntity>(Substitute.For<IAmazonDynamoDB>());
        builder.UsingIndex("gsi1");
        var req = builder.ToQueryRequest();
        req.Should().NotBeNull();
        req.IndexName.Should().Be("gsi1");
    }

    #region Attributes

    [Fact]
    public void UsingExpressionAttributeNamesSuccess()
    {
        var builder = new QueryRequestBuilder<TestEntity>(Substitute.For<IAmazonDynamoDB>());
        builder.WithAttributes(new Dictionary<string, string>() { { "#pk", "pk" } });
        var req = builder.ToQueryRequest();
        req.Should().NotBeNull();
        req.ExpressionAttributeNames.Should().NotBeNull();
        req.ExpressionAttributeNames.Should().HaveCount(1);
        req.ExpressionAttributeNames["#pk"].Should().Be("pk");
    }

    [Fact]
    public void UsingExpressionAttributeNamesUsingLambdaSuccess()
    {
        var builder = new QueryRequestBuilder<TestEntity>(Substitute.For<IAmazonDynamoDB>());
        builder.WithAttributes((attributes) => attributes.Add("#pk", "pk"));
        var req = builder.ToQueryRequest();
        req.Should().NotBeNull();
        req.ExpressionAttributeNames.Should().NotBeNull();
        req.ExpressionAttributeNames.Should().HaveCount(1);
        req.ExpressionAttributeNames["#pk"].Should().Be("pk");
    }

    [Fact]
    public void UsingExpressionAttributeNameSuccess()
    {
        var builder = new QueryRequestBuilder<TestEntity>(Substitute.For<IAmazonDynamoDB>());
        builder.WithAttribute("#pk", "pk");
        var req = builder.ToQueryRequest();
        req.Should().NotBeNull();
        req.ExpressionAttributeNames.Should().NotBeNull();
        req.ExpressionAttributeNames.Should().HaveCount(1);
        req.ExpressionAttributeNames["#pk"].Should().Be("pk");
    }

    [Fact]
    public void UsingExpressionAttributeValuesSuccess()
    {
        var builder = new QueryRequestBuilder<TestEntity>(Substitute.For<IAmazonDynamoDB>());
        builder.WithValues(new Dictionary<string, AttributeValue>() { { ":pk", new AttributeValue { S = "1" } } });
        var req = builder.ToQueryRequest();
        req.Should().NotBeNull();
        req.ExpressionAttributeValues.Should().NotBeNull();
        req.ExpressionAttributeValues.Should().HaveCount(1);
        req.ExpressionAttributeValues[":pk"].S.Should().Be("1");

    }

    [Fact]
    public void UsingExpressionAttributeValuesLambdaSuccess()
    {
        var builder = new QueryRequestBuilder<TestEntity>(Substitute.For<IAmazonDynamoDB>());
        builder.WithValues((attributes) => attributes.Add(":pk", new AttributeValue { S = "1" }));
        var req = builder.ToQueryRequest();
        req.Should().NotBeNull();
        req.ExpressionAttributeValues.Should().NotBeNull();
        req.ExpressionAttributeValues.Should().HaveCount(1);
        req.ExpressionAttributeValues[":pk"].S.Should().Be("1");

    }

    [Fact]
    public void UsingExpressionAttributeStringValueSuccess()
    {
        var builder = new QueryRequestBuilder<TestEntity>(Substitute.For<IAmazonDynamoDB>());
        builder.WithValue(":pk", "1");
        var req = builder.ToQueryRequest();
        req.Should().NotBeNull();
        req.ExpressionAttributeValues.Should().NotBeNull();
        req.ExpressionAttributeValues.Should().HaveCount(1);
        req.ExpressionAttributeValues[":pk"].S.Should().Be("1");
    }

    [Fact]
    public void UsingExpressionAttributeBooleanValueSuccess()
    {
        var builder = new QueryRequestBuilder<TestEntity>(Substitute.For<IAmazonDynamoDB>());
        builder.WithValue(":pk", true);
        var req = builder.ToQueryRequest();
        req.Should().NotBeNull();
        req.ExpressionAttributeValues.Should().NotBeNull();
        req.ExpressionAttributeValues.Should().HaveCount(1);
        req.ExpressionAttributeValues[":pk"].BOOL.Should().BeTrue();
    }

    #endregion Attributes

    [Fact]
    public void WhereSuccess()
    {
        var builder = new QueryRequestBuilder<TestEntity>(Substitute.For<IAmazonDynamoDB>());
        builder.Where("#pk = :pk");
        var req = builder.ToQueryRequest();
        req.Should().NotBeNull();
        req.KeyConditionExpression.Should().Be("#pk = :pk");
    }

    [Fact]
    public void WithFilterSuccess()
    {
        var builder = new QueryRequestBuilder<TestEntity>(Substitute.For<IAmazonDynamoDB>());
        builder.WithFilter("#v >= :num");
        var req = builder.ToQueryRequest();
        req.Should().NotBeNull();
        req.FilterExpression.Should().Be("#v >= :num");
    }

    [Fact]
    public void ProjectionExpressionSuccess()
    {
        var builder = new QueryRequestBuilder<TestEntity>(Substitute.For<IAmazonDynamoDB>());
        builder.WithProjection("description, price");
        var req = builder.ToQueryRequest();
        req.Should().NotBeNull();
        req.ProjectionExpression.Should().Be("description, price");
    }


    #region ConsumedCapacity

    [Fact]
    public void ReturnConsumedCapacitySuccess()
    {
        var builder = new QueryRequestBuilder<TestEntity>(Substitute.For<IAmazonDynamoDB>());
        builder.ReturnConsumedCapacity(ReturnConsumedCapacity.TOTAL);
        var req = builder.ToQueryRequest();
        req.Should().NotBeNull();
        req.ReturnConsumedCapacity.Should().Be(ReturnConsumedCapacity.TOTAL);
    }

    [Fact]
    public void ReturnTotalConsumedCapacitySuccess()
    {
        var builder = new QueryRequestBuilder<TestEntity>(Substitute.For<IAmazonDynamoDB>());
        builder.ReturnTotalConsumedCapacity();
        var req = builder.ToQueryRequest();
        req.Should().NotBeNull();
        req.ReturnConsumedCapacity.Should().Be(ReturnConsumedCapacity.TOTAL);
    }

    [Fact]
    public void ReturnIndexConsumedCapacitySuccess()
    {
        var builder = new QueryRequestBuilder<TestEntity>(Substitute.For<IAmazonDynamoDB>());
        builder.ReturnIndexConsumedCapacity();
        var req = builder.ToQueryRequest();
        req.Should().NotBeNull();
        req.ReturnConsumedCapacity.Should().Be(ReturnConsumedCapacity.INDEXES);
    }

    #endregion ConsumedCapacity

    [Fact]
    public void UsingConsistentReadSuccess()
    {
        var builder = new QueryRequestBuilder<TestEntity>(Substitute.For<IAmazonDynamoDB>());
        builder.UsingConsistentRead();
        var req = builder.ToQueryRequest();
        req.Should().NotBeNull();
        req.ConsistentRead.Should().BeTrue();
    }

    [Fact]
    public void StartAtSuccess()
    {
        var builder = new QueryRequestBuilder<TestEntity>(Substitute.For<IAmazonDynamoDB>());
        builder.StartAt(new Dictionary<string, AttributeValue>() { { "pk", new AttributeValue { S = "1" } } });
        var req = builder.ToQueryRequest();
        req.Should().NotBeNull();
        req.ExclusiveStartKey.Should().NotBeNull();
        req.ExclusiveStartKey.Should().HaveCount(1);
        req.ExclusiveStartKey["pk"].S.Should().Be("1");
    }

    [Fact]
    public void TakeSuccess()
    {
        var builder = new QueryRequestBuilder<TestEntity>(Substitute.For<IAmazonDynamoDB>());
        builder.Take(10);
        var req = builder.ToQueryRequest();
        req.Should().NotBeNull();
        req.Limit.Should().Be(10);
    }

    #region ToListAsync Tests

    [Fact]
    public async Task ToListAsync_Success_ReturnsEntityList()
    {
        // Arrange
        var mockClient = Substitute.For<IAmazonDynamoDB>();
        var builder = new QueryRequestBuilder<TestEntity>(mockClient);
        builder.ForTable("TestTable").Where("pk = :pk").WithValue(":pk", "test-pk");

        var mockResponse = new QueryResponse
        {
            Items = new List<Dictionary<string, AttributeValue>>
            {
                new() { ["pk"] = new AttributeValue { S = "id1" }, ["name"] = new AttributeValue { S = "name1" } },
                new() { ["pk"] = new AttributeValue { S = "id2" }, ["name"] = new AttributeValue { S = "name2" } }
            },
            Count = 2,
            ScannedCount = 2
        };

        mockClient.QueryAsync(Arg.Any<QueryRequest>(), Arg.Any<CancellationToken>())
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
        var builder = new QueryRequestBuilder<TestEntity>(mockClient);
        builder.ForTable("TestTable").Where("pk = :pk").WithValue(":pk", "non-existent");

        var mockResponse = new QueryResponse
        {
            Items = new List<Dictionary<string, AttributeValue>>(),
            Count = 0,
            ScannedCount = 0
        };

        mockClient.QueryAsync(Arg.Any<QueryRequest>(), Arg.Any<CancellationToken>())
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
        var builder = new QueryRequestBuilder<TestEntity>(mockClient);
        builder.ForTable("TestTable");
        var cancellationToken = new CancellationToken();

        var mockResponse = new QueryResponse
        {
            Items = new List<Dictionary<string, AttributeValue>>()
        };

        mockClient.QueryAsync(Arg.Any<QueryRequest>(), cancellationToken)
            .Returns(Task.FromResult(mockResponse));

        // Act
        await builder.ToListAsync<TestEntity>(cancellationToken);

        // Assert
        await mockClient.Received(1).QueryAsync(Arg.Any<QueryRequest>(), cancellationToken);
    }

    #endregion ToListAsync Tests

    #region Context Population Tests

    [Fact]
    public async Task ToListAsync_PopulatesContext_WithResponseMetadata()
    {
        // Arrange
        var mockClient = Substitute.For<IAmazonDynamoDB>();
        var builder = new QueryRequestBuilder<TestEntity>(mockClient);
        builder.ForTable("TestTable").UsingIndex("gsi1").Where("pk = :pk").WithValue(":pk", "test-pk");

        var mockResponse = new QueryResponse
        {
            Items = new List<Dictionary<string, AttributeValue>>
            {
                new() { ["pk"] = new AttributeValue { S = "id1" } }
            },
            Count = 1,
            ScannedCount = 1,
            ConsumedCapacity = new ConsumedCapacity
            {
                TableName = "TestTable",
                CapacityUnits = 2.5
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

        mockClient.QueryAsync(Arg.Any<QueryRequest>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(mockResponse));

        // Act - Call helper that returns context from within async flow
        var context = await ExecuteQueryAndGetContextAsync(builder);

        // Assert
        context.Should().NotBeNull();
        context!.OperationType.Should().Be("Query");
        context.TableName.Should().Be("TestTable");
        context.IndexName.Should().Be("gsi1");
        context.ItemCount.Should().Be(1);
        context.ScannedCount.Should().Be(1);
        context.ConsumedCapacity.Should().NotBeNull();
        context.ConsumedCapacity!.CapacityUnits.Should().Be(2.5);
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
        var builder = new QueryRequestBuilder<TestEntity>(mockClient);
        builder.ForTable("TestTable");

        var rawItems = new List<Dictionary<string, AttributeValue>>
        {
            new() { ["pk"] = new AttributeValue { S = "id1" }, ["name"] = new AttributeValue { S = "name1" } },
            new() { ["pk"] = new AttributeValue { S = "id2" }, ["name"] = new AttributeValue { S = "name2" } }
        };

        var mockResponse = new QueryResponse
        {
            Items = rawItems,
            Count = 2
        };

        mockClient.QueryAsync(Arg.Any<QueryRequest>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(mockResponse));

        // Act - Call helper that returns context from within async flow
        var context = await ExecuteQueryAndGetContextAsync(builder);

        // Assert
        context.Should().NotBeNull();
        context!.RawItems.Should().NotBeNull();
        context.RawItems.Should().BeSameAs(rawItems);
        context.RawItems.Should().HaveCount(2);
        context.RawItems![0]["pk"].S.Should().Be("id1");
        context.RawItems[1]["pk"].S.Should().Be("id2");
    }

    [Fact]
    public async Task ToListAsync_EmptyResult_PopulatesContextWithEmptyRawItems()
    {
        // Arrange
        var mockClient = Substitute.For<IAmazonDynamoDB>();
        var builder = new QueryRequestBuilder<TestEntity>(mockClient);
        builder.ForTable("TestTable");

        var mockResponse = new QueryResponse
        {
            Items = new List<Dictionary<string, AttributeValue>>(),
            Count = 0
        };

        mockClient.QueryAsync(Arg.Any<QueryRequest>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(mockResponse));

        // Act - Call helper that returns context from within async flow
        var context = await ExecuteQueryAndGetContextAsync(builder);

        // Assert
        context.Should().NotBeNull();
        context!.RawItems.Should().NotBeNull();
        context.RawItems.Should().BeEmpty();
    }

    [Fact]
    public async Task ToListAsync_NoLastEvaluatedKey_PopulatesContextWithNull()
    {
        // Arrange
        var mockClient = Substitute.For<IAmazonDynamoDB>();
        var builder = new QueryRequestBuilder<TestEntity>(mockClient);
        builder.ForTable("TestTable");

        var mockResponse = new QueryResponse
        {
            Items = new List<Dictionary<string, AttributeValue>>
            {
                new() { ["pk"] = new AttributeValue { S = "id1" } }
            },
            Count = 1,
            LastEvaluatedKey = null
        };

        mockClient.QueryAsync(Arg.Any<QueryRequest>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(mockResponse));

        // Act - Call helper that returns context from within async flow
        var context = await ExecuteQueryAndGetContextAsync(builder);

        // Assert
        context.Should().NotBeNull();
        context!.LastEvaluatedKey.Should().BeNull();
    }

    [Fact]
    public async Task ToListAsync_Exception_DoesNotPopulateContext()
    {
        // Arrange
        var mockClient = Substitute.For<IAmazonDynamoDB>();
        var builder = new QueryRequestBuilder<TestEntity>(mockClient);
        builder.ForTable("TestTable");

        // Clear any existing context
        DynamoDbOperationContext.Clear();

        var originalException = new Exception("Test exception");
        mockClient.QueryAsync(Arg.Any<QueryRequest>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException<QueryResponse>(originalException));

        // Act & Assert
        var exception = await Assert.ThrowsAsync<DynamoDbMappingException>(() => builder.ToListAsync<TestEntity>());

        // Verify inner exception is the original exception type
        exception.InnerException.Should().NotBeNull();
        exception.InnerException.Should().BeOfType<Exception>();
        exception.InnerException!.Message.Should().Be("Test exception");

        // Context should remain null
        DynamoDbOperationContext.Current.Should().BeNull();
    }

    #endregion Context Population Tests

    #region Context Isolation Tests

    [Fact]
    public async Task ToListAsync_ConcurrentOperations_MaintainSeparateContexts()
    {
        // Arrange
        var mockClient = Substitute.For<IAmazonDynamoDB>();
        
        var builder1 = new QueryRequestBuilder<TestEntity>(mockClient);
        builder1.ForTable("Table1");

        var builder2 = new QueryRequestBuilder<TestEntity>(mockClient);
        builder2.ForTable("Table2");

        var mockResponse1 = new QueryResponse
        {
            Items = new List<Dictionary<string, AttributeValue>>
            {
                new() { ["pk"] = new AttributeValue { S = "id1" } }
            },
            Count = 1,
            ConsumedCapacity = new ConsumedCapacity { CapacityUnits = 1.0 }
        };

        var mockResponse2 = new QueryResponse
        {
            Items = new List<Dictionary<string, AttributeValue>>
            {
                new() { ["pk"] = new AttributeValue { S = "id2" } }
            },
            Count = 1,
            ConsumedCapacity = new ConsumedCapacity { CapacityUnits = 2.0 }
        };

        mockClient.QueryAsync(Arg.Is<QueryRequest>(r => r.TableName == "Table1"), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(mockResponse1));

        mockClient.QueryAsync(Arg.Is<QueryRequest>(r => r.TableName == "Table2"), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(mockResponse2));

        // Act - Execute operations sequentially (simulating different async contexts)
        var contextAfterFirst = await ExecuteQueryAndGetContextAsync(builder1);
        var contextAfterSecond = await ExecuteQueryAndGetContextAsync(builder2);

        // Assert
        contextAfterFirst.Should().NotBeNull();
        contextAfterFirst!.TableName.Should().Be("Table1");
        contextAfterFirst.ConsumedCapacity!.CapacityUnits.Should().Be(1.0);

        // Second operation replaces the context
        contextAfterSecond.Should().NotBeNull();
        contextAfterSecond!.TableName.Should().Be("Table2");
        contextAfterSecond.ConsumedCapacity!.CapacityUnits.Should().Be(2.0);
    }

    [Fact]
    public async Task ToListAsync_ContextFlowsThroughAsyncCalls()
    {
        // Arrange
        var mockClient = Substitute.For<IAmazonDynamoDB>();
        var builder = new QueryRequestBuilder<TestEntity>(mockClient);
        builder.ForTable("TestTable");

        var mockResponse = new QueryResponse
        {
            Items = new List<Dictionary<string, AttributeValue>>
            {
                new() { ["pk"] = new AttributeValue { S = "id1" } }
            },
            Count = 1,
            ConsumedCapacity = new ConsumedCapacity { CapacityUnits = 1.5 }
        };

        mockClient.QueryAsync(Arg.Any<QueryRequest>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(mockResponse));

        // Act - Call helper that returns context from within async flow
        var context = await ExecuteQueryAndGetContextAsync(builder);

        // Assert - Context should be captured from within the async flow
        context.Should().NotBeNull();
        context!.TableName.Should().Be("TestTable");
        context.ConsumedCapacity!.CapacityUnits.Should().Be(1.5);
    }

    #endregion Context Isolation Tests

    #region Helper Methods

    /// <summary>
    /// Helper method to execute ToListAsync and return the context from within the async flow.
    /// This is necessary because xUnit's synchronization context prevents AsyncLocal values
    /// from flowing back to the test method after await.
    /// </summary>
    private static async Task<OperationContextData?> ExecuteQueryAndGetContextAsync<T>(QueryRequestBuilder<T> builder)
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
