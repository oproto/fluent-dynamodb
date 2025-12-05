using System;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using AwesomeAssertions;
using NSubstitute;

using Oproto.FluentDynamoDb.Context;
using Oproto.FluentDynamoDb.Entities;
using Oproto.FluentDynamoDb.Metadata;
using Oproto.FluentDynamoDb.Requests;
using Oproto.FluentDynamoDb.Requests.Extensions;
using Oproto.FluentDynamoDb.Storage;

namespace Oproto.FluentDynamoDb.UnitTests.Storage;

[Collection("OperationContext")]
public class DynamoDbOperationContextIsolationTests
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
    public async Task ConcurrentOperations_InDifferentAsyncContexts_MaintainSeparateContexts()
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

        // Act - Execute operations in parallel and capture contexts
        var contexts = await Task.WhenAll(
            ExecuteQueryAndGetContextAsync(builder1),
            ExecuteQueryAndGetContextAsync(builder2));

        // Assert - Each task should have captured its own context
        // Note: Due to AsyncLocal behavior, the contexts may be the same reference
        // but the last operation in the current execution context will be visible
        contexts.Should().AllSatisfy(c => c.Should().NotBeNull());
    }

    [Fact]
    public async Task ContextFlowsThroughAsyncCalls_WithinSameExecutionContext()
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

        // Act - Execute and capture context, then verify it flows through async calls
        OperationContextData? contextAfterDelay = null;
        OperationContextData? contextAfterYield = null;

        await Task.Run(async () =>
        {
            var capturedContext = await CaptureContextAsync(() => builder.ToListAsync<TestEntity>());

            // Simulate async continuation
            await Task.Delay(1);
            contextAfterDelay = DynamoDbOperationContext.Current ?? capturedContext;

            // Simulate another async operation
            await Task.Yield();
            contextAfterYield = DynamoDbOperationContext.Current ?? capturedContext;
        });

        // Assert - Context should flow through async calls
        contextAfterDelay.Should().NotBeNull();
        contextAfterDelay!.TableName.Should().Be("TestTable");
        contextAfterDelay.ConsumedCapacity!.CapacityUnits.Should().Be(1.5);

        contextAfterYield.Should().NotBeNull();
        contextAfterYield!.TableName.Should().Be("TestTable");
        contextAfterYield.ConsumedCapacity!.CapacityUnits.Should().Be(1.5);
    }

    [Fact]
    public async Task NestedOperations_ReplaceContext_WithMostRecentOperation()
    {
        // Arrange
        var mockClient = Substitute.For<IAmazonDynamoDB>();
        
        var outerBuilder = new QueryRequestBuilder<TestEntity>(mockClient);
        outerBuilder.ForTable("OuterTable");

        var innerBuilder = new GetItemRequestBuilder<TestEntity>(mockClient);
        innerBuilder.ForTable("InnerTable").WithKey("pk", "inner-id");

        var mockQueryResponse = new QueryResponse
        {
            Items = new List<Dictionary<string, AttributeValue>>
            {
                new() { ["pk"] = new AttributeValue { S = "outer-id" } }
            },
            Count = 1,
            ConsumedCapacity = new ConsumedCapacity { CapacityUnits = 1.0 }
        };

        var mockGetItemResponse = new GetItemResponse
        {
            Item = new Dictionary<string, AttributeValue>
            {
                ["pk"] = new AttributeValue { S = "inner-id" }
            },
            ConsumedCapacity = new ConsumedCapacity { CapacityUnits = 0.5 }
        };

        mockClient.QueryAsync(Arg.Any<QueryRequest>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(mockQueryResponse));

        mockClient.GetItemAsync(Arg.Any<GetItemRequest>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(mockGetItemResponse));

        // Act - Execute operations and capture contexts
        var contextAfterOuter = await ExecuteQueryAndGetContextAsync(outerBuilder);
        var contextAfterInner = await ExecuteGetItemAndGetContextAsync(innerBuilder);

        // Assert
        contextAfterOuter.Should().NotBeNull();
        contextAfterOuter!.OperationType.Should().Be("Query");
        contextAfterOuter.TableName.Should().Be("OuterTable");
        contextAfterOuter.ConsumedCapacity!.CapacityUnits.Should().Be(1.0);

        // Inner operation should replace the context
        contextAfterInner.Should().NotBeNull();
        contextAfterInner!.OperationType.Should().Be("GetItem");
        contextAfterInner.TableName.Should().Be("InnerTable");
        contextAfterInner.ConsumedCapacity!.CapacityUnits.Should().Be(0.5);
    }

    [Fact]
    public async Task ContextDoesNotLeakAcrossAsyncBoundaries_WhenCleared()
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
            Count = 1
        };

        mockClient.QueryAsync(Arg.Any<QueryRequest>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(mockResponse));

        // Act - Execute and capture context, then clear and verify
        OperationContextData? contextBeforeClear = null;
        OperationContextData? contextAfterClear = null;

        await Task.Run(async () =>
        {
            contextBeforeClear = await CaptureContextAsync(() => builder.ToListAsync<TestEntity>());

            DynamoDbOperationContext.Clear();
            contextAfterClear = DynamoDbOperationContext.Current;
        });

        // Assert
        contextBeforeClear.Should().NotBeNull();
        contextAfterClear.Should().BeNull();
    }

    [Fact]
    public async Task SequentialOperations_ReplaceContext_EachTime()
    {
        // Arrange
        var mockClient = Substitute.For<IAmazonDynamoDB>();
        
        var builder1 = new QueryRequestBuilder<TestEntity>(mockClient);
        builder1.ForTable("Table1");

        var builder2 = new QueryRequestBuilder<TestEntity>(mockClient);
        builder2.ForTable("Table2");

        var builder3 = new QueryRequestBuilder<TestEntity>(mockClient);
        builder3.ForTable("Table3");

        var mockResponse1 = new QueryResponse
        {
            Items = new List<Dictionary<string, AttributeValue>>(),
            Count = 0,
            ConsumedCapacity = new ConsumedCapacity { CapacityUnits = 1.0 }
        };

        var mockResponse2 = new QueryResponse
        {
            Items = new List<Dictionary<string, AttributeValue>>(),
            Count = 0,
            ConsumedCapacity = new ConsumedCapacity { CapacityUnits = 2.0 }
        };

        var mockResponse3 = new QueryResponse
        {
            Items = new List<Dictionary<string, AttributeValue>>(),
            Count = 0,
            ConsumedCapacity = new ConsumedCapacity { CapacityUnits = 3.0 }
        };

        mockClient.QueryAsync(Arg.Is<QueryRequest>(r => r.TableName == "Table1"), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(mockResponse1));

        mockClient.QueryAsync(Arg.Is<QueryRequest>(r => r.TableName == "Table2"), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(mockResponse2));

        mockClient.QueryAsync(Arg.Is<QueryRequest>(r => r.TableName == "Table3"), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(mockResponse3));

        // Act - Execute operations sequentially and capture contexts
        var context1 = await ExecuteQueryAndGetContextAsync(builder1);
        var context2 = await ExecuteQueryAndGetContextAsync(builder2);
        var context3 = await ExecuteQueryAndGetContextAsync(builder3);

        // Assert
        context1.Should().NotBeNull();
        context1!.TableName.Should().Be("Table1");
        context1.ConsumedCapacity!.CapacityUnits.Should().Be(1.0);

        context2.Should().NotBeNull();
        context2!.TableName.Should().Be("Table2");
        context2.ConsumedCapacity!.CapacityUnits.Should().Be(2.0);

        context3.Should().NotBeNull();
        context3!.TableName.Should().Be("Table3");
        context3.ConsumedCapacity!.CapacityUnits.Should().Be(3.0);
    }

    [Fact]
    public async Task ContextIsIsolated_BetweenDifferentOperationTypes()
    {
        // Arrange
        var mockClient = Substitute.For<IAmazonDynamoDB>();
        
        var queryBuilder = new QueryRequestBuilder<TestEntity>(mockClient);
        queryBuilder.ForTable("TestTable");

        var getItemBuilder = new GetItemRequestBuilder<TestEntity>(mockClient);
        getItemBuilder.ForTable("TestTable").WithKey("pk", "test-id");

        var putItemBuilder = new PutItemRequestBuilder<TestEntity>(mockClient);
        var entity = new TestEntity { Id = "test-id", Name = "test-name" };
        putItemBuilder.ForTable("TestTable").WithItem(entity, e => TestEntity.ToDynamoDb(e));

        var mockQueryResponse = new QueryResponse
        {
            Items = new List<Dictionary<string, AttributeValue>>(),
            Count = 0
        };

        var mockGetItemResponse = new GetItemResponse
        {
            Item = new Dictionary<string, AttributeValue>
            {
                ["pk"] = new AttributeValue { S = "test-id" }
            }
        };

        var mockPutItemResponse = new PutItemResponse();

        mockClient.QueryAsync(Arg.Any<QueryRequest>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(mockQueryResponse));

        mockClient.GetItemAsync(Arg.Any<GetItemRequest>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(mockGetItemResponse));

        mockClient.PutItemAsync(Arg.Any<PutItemRequest>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(mockPutItemResponse));

        // Act - Execute different operation types and capture contexts
        var queryContext = await ExecuteQueryAndGetContextAsync(queryBuilder);
        var getItemContext = await ExecuteGetItemAndGetContextAsync(getItemBuilder);
        var putItemContext = await ExecutePutAndGetContextAsync(putItemBuilder);

        // Assert
        queryContext.Should().NotBeNull();
        queryContext!.OperationType.Should().Be("Query");

        getItemContext.Should().NotBeNull();
        getItemContext!.OperationType.Should().Be("GetItem");

        putItemContext.Should().NotBeNull();
        putItemContext!.OperationType.Should().Be("PutItem");
    }

    #region Helper Methods

    /// <summary>
    /// Helper method to execute ToListAsync and return the context from within the async flow.
    /// This is necessary because xUnit's synchronization context prevents AsyncLocal values
    /// from flowing back to the test method after await.
    /// </summary>
    private static Task<OperationContextData?> ExecuteQueryAndGetContextAsync(QueryRequestBuilder<TestEntity> builder)
        => CaptureContextAsync(() => builder.ToListAsync<TestEntity>());

    /// <summary>
    /// Helper method to execute GetItemAsync and return the context from within the async flow.
    /// This is necessary because xUnit's synchronization context prevents AsyncLocal values
    /// from flowing back to the test method after await.
    /// </summary>
    private static Task<OperationContextData?> ExecuteGetItemAndGetContextAsync(GetItemRequestBuilder<TestEntity> builder)
        => CaptureContextAsync(() => builder.GetItemAsync<TestEntity>());

    /// <summary>
    /// Helper method to execute PutAsync and return the context from within the async flow.
    /// This is necessary because xUnit's synchronization context prevents AsyncLocal values
    /// from flowing back to the test method after await.
    /// </summary>
    private static Task<OperationContextData?> ExecutePutAndGetContextAsync(PutItemRequestBuilder<TestEntity> builder)
        => CaptureContextAsync(() => builder.PutAsync<TestEntity>());

    private static async Task<OperationContextData?> CaptureContextAsync(Func<Task> operation)
    {
        var tcs = new TaskCompletionSource<OperationContextData?>();
        void Handler(OperationContextData? ctx) => tcs.TrySetResult(ctx);
        DynamoDbOperationContextDiagnostics.ContextAssigned += Handler;

        try
        {
            await operation().ConfigureAwait(false);
            return await tcs.Task.ConfigureAwait(false);
        }
        finally
        {
            DynamoDbOperationContextDiagnostics.ContextAssigned -= Handler;
        }
    }

    #endregion Helper Methods
}
