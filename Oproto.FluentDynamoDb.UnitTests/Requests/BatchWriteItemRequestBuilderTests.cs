using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using FluentAssertions;
using NSubstitute;
using Oproto.FluentDynamoDb.Requests;
using Oproto.FluentDynamoDb.Requests.Extensions;

namespace Oproto.FluentDynamoDb.UnitTests.Requests;

public class BatchWriteItemRequestBuilderTests
{
    private readonly IAmazonDynamoDB _mockClient;

    public BatchWriteItemRequestBuilderTests()
    {
        _mockClient = Substitute.For<IAmazonDynamoDB>();
    }

    [Fact]
    public void ConstructorSuccess()
    {
        var builder = new BatchWriteItemRequestBuilder(_mockClient);
        var request = builder.ToBatchWriteItemRequest();

        request.Should().NotBeNull();
        request.RequestItems.Should().NotBeNull();
        request.RequestItems.Should().BeEmpty();
    }

    [Fact]
    public void WriteToTableSingleTablePutSuccess()
    {
        var builder = new BatchWriteItemRequestBuilder(_mockClient);
        var item = new Dictionary<string, AttributeValue>
        {
            { "pk", new AttributeValue { S = "1" } },
            { "name", new AttributeValue { S = "Test Item" } }
        };

        builder.WriteToTable("TestTable", b => b.PutItem(item));

        var request = builder.ToBatchWriteItemRequest();

        request.Should().NotBeNull();
        request.RequestItems.Should().HaveCount(1);
        request.RequestItems.Should().ContainKey("TestTable");
        request.RequestItems["TestTable"].Should().HaveCount(1);
        request.RequestItems["TestTable"][0].PutRequest.Should().NotBeNull();
        request.RequestItems["TestTable"][0].PutRequest.Item.Should().HaveCount(2);
        request.RequestItems["TestTable"][0].PutRequest.Item["pk"].S.Should().Be("1");
        request.RequestItems["TestTable"][0].PutRequest.Item["name"].S.Should().Be("Test Item");
    }

    [Fact]
    public void WriteToTableSingleTableDeleteSuccess()
    {
        var builder = new BatchWriteItemRequestBuilder(_mockClient);
        var key = new Dictionary<string, AttributeValue>
        {
            { "pk", new AttributeValue { S = "1" } }
        };

        builder.WriteToTable("TestTable", b => b.DeleteItem(key));

        var request = builder.ToBatchWriteItemRequest();

        request.Should().NotBeNull();
        request.RequestItems.Should().HaveCount(1);
        request.RequestItems.Should().ContainKey("TestTable");
        request.RequestItems["TestTable"].Should().HaveCount(1);
        request.RequestItems["TestTable"][0].DeleteRequest.Should().NotBeNull();
        request.RequestItems["TestTable"][0].DeleteRequest.Key.Should().HaveCount(1);
        request.RequestItems["TestTable"][0].DeleteRequest.Key["pk"].S.Should().Be("1");
    }

    [Fact]
    public void WriteToTableMultipleTablesSuccess()
    {
        var builder = new BatchWriteItemRequestBuilder(_mockClient);
        var item1 = new Dictionary<string, AttributeValue> { { "pk", new AttributeValue { S = "1" } } };
        var key2 = new Dictionary<string, AttributeValue> { { "id", new AttributeValue { S = "2" } } };

        builder.WriteToTable("Table1", b => b.PutItem(item1))
               .WriteToTable("Table2", b => b.DeleteItem(key2));

        var request = builder.ToBatchWriteItemRequest();

        request.Should().NotBeNull();
        request.RequestItems.Should().HaveCount(2);
        request.RequestItems.Should().ContainKey("Table1");
        request.RequestItems.Should().ContainKey("Table2");
        request.RequestItems["Table1"][0].PutRequest.Should().NotBeNull();
        request.RequestItems["Table2"][0].DeleteRequest.Should().NotBeNull();
    }

    [Fact]
    public void WriteToTableMixedOperationsSuccess()
    {
        var builder = new BatchWriteItemRequestBuilder(_mockClient);
        var item = new Dictionary<string, AttributeValue> { { "pk", new AttributeValue { S = "1" } } };
        var key = new Dictionary<string, AttributeValue> { { "pk", new AttributeValue { S = "2" } } };

        builder.WriteToTable("TestTable", b =>
            b.PutItem(item)
             .DeleteItem(key));

        var request = builder.ToBatchWriteItemRequest();

        request.Should().NotBeNull();
        request.RequestItems.Should().HaveCount(1);
        request.RequestItems["TestTable"].Should().HaveCount(2);
        request.RequestItems["TestTable"][0].PutRequest.Should().NotBeNull();
        request.RequestItems["TestTable"][1].DeleteRequest.Should().NotBeNull();
    }

    [Fact]
    public void WriteToTableWithModelMappingSuccess()
    {
        var builder = new BatchWriteItemRequestBuilder(_mockClient);
        var testModel = new { Id = "123", Name = "Test" };

        builder.WriteToTable("TestTable", b =>
            b.PutItem(testModel, model => new Dictionary<string, AttributeValue>
            {
                { "id", new AttributeValue { S = model.Id } },
                { "name", new AttributeValue { S = model.Name } }
            }));

        var request = builder.ToBatchWriteItemRequest();

        request.Should().NotBeNull();
        request.RequestItems["TestTable"][0].PutRequest.Item["id"].S.Should().Be("123");
        request.RequestItems["TestTable"][0].PutRequest.Item["name"].S.Should().Be("Test");
    }

    [Fact]
    public void WriteToTableWithStringKeyDeleteSuccess()
    {
        var builder = new BatchWriteItemRequestBuilder(_mockClient);

        builder.WriteToTable("TestTable", b => b.DeleteItem("pk", "test-key"));

        var request = builder.ToBatchWriteItemRequest();

        request.Should().NotBeNull();
        request.RequestItems["TestTable"][0].DeleteRequest.Key["pk"].S.Should().Be("test-key");
    }

    [Fact]
    public void WriteToTableWithCompositeKeyDeleteSuccess()
    {
        var builder = new BatchWriteItemRequestBuilder(_mockClient);

        builder.WriteToTable("TestTable", b =>
            b.DeleteItem("pk", "partition-key", "sk", "sort-key"));

        var request = builder.ToBatchWriteItemRequest();

        request.Should().NotBeNull();
        request.RequestItems["TestTable"][0].DeleteRequest.Key["pk"].S.Should().Be("partition-key");
        request.RequestItems["TestTable"][0].DeleteRequest.Key["sk"].S.Should().Be("sort-key");
    }

    [Fact]
    public void ReturnConsumedCapacitySuccess()
    {
        var builder = new BatchWriteItemRequestBuilder(_mockClient);
        builder.ReturnConsumedCapacity(ReturnConsumedCapacity.TOTAL);

        var request = builder.ToBatchWriteItemRequest();

        request.Should().NotBeNull();
        request.ReturnConsumedCapacity.Should().Be(ReturnConsumedCapacity.TOTAL);
    }

    [Fact]
    public void ReturnTotalConsumedCapacitySuccess()
    {
        var builder = new BatchWriteItemRequestBuilder(_mockClient);
        builder.ReturnTotalConsumedCapacity();

        var request = builder.ToBatchWriteItemRequest();

        request.Should().NotBeNull();
        request.ReturnConsumedCapacity.Should().Be(ReturnConsumedCapacity.TOTAL);
    }

    [Fact]
    public void ReturnIndexesConsumedCapacitySuccess()
    {
        var builder = new BatchWriteItemRequestBuilder(_mockClient);
        builder.ReturnIndexesConsumedCapacity();

        var request = builder.ToBatchWriteItemRequest();

        request.Should().NotBeNull();
        request.ReturnConsumedCapacity.Should().Be(ReturnConsumedCapacity.INDEXES);
    }

    [Fact]
    public void ReturnItemCollectionMetricsSuccess()
    {
        var builder = new BatchWriteItemRequestBuilder(_mockClient);
        builder.ReturnItemCollectionMetrics();

        var request = builder.ToBatchWriteItemRequest();

        request.Should().NotBeNull();
        request.ReturnItemCollectionMetrics.Should().Be(ReturnItemCollectionMetrics.SIZE);
    }

    [Fact]
    public void ReturnItemCollectionMetricsWithParameterSuccess()
    {
        var builder = new BatchWriteItemRequestBuilder(_mockClient);
        builder.ReturnItemCollectionMetrics(ReturnItemCollectionMetrics.NONE);

        var request = builder.ToBatchWriteItemRequest();

        request.Should().NotBeNull();
        request.ReturnItemCollectionMetrics.Should().Be(ReturnItemCollectionMetrics.NONE);
    }

    [Fact]
    public void ComplexMultiTableRequestSuccess()
    {
        var builder = new BatchWriteItemRequestBuilder(_mockClient);
        var userItem = new Dictionary<string, AttributeValue>
        {
            { "userId", new AttributeValue { S = "user1" } },
            { "name", new AttributeValue { S = "John Doe" } },
            { "email", new AttributeValue { S = "john@example.com" } }
        };
        var orderKey = new Dictionary<string, AttributeValue>
        {
            { "orderId", new AttributeValue { S = "order1" } },
            { "userId", new AttributeValue { S = "user1" } }
        };

        builder.WriteToTable("Users", b =>
                   b.PutItem(userItem)
                    .DeleteItem("userId", "user2"))
               .WriteToTable("Orders", b =>
                   b.DeleteItem(orderKey)
                    .PutItem(new Dictionary<string, AttributeValue>
                    {
                        { "orderId", new AttributeValue { S = "order2" } },
                        { "userId", new AttributeValue { S = "user1" } },
                        { "total", new AttributeValue { N = "99.99" } }
                    }))
               .ReturnConsumedCapacity(ReturnConsumedCapacity.INDEXES)
               .ReturnItemCollectionMetrics();

        var request = builder.ToBatchWriteItemRequest();

        request.Should().NotBeNull();
        request.RequestItems.Should().HaveCount(2);
        request.ReturnConsumedCapacity.Should().Be(ReturnConsumedCapacity.INDEXES);
        request.ReturnItemCollectionMetrics.Should().Be(ReturnItemCollectionMetrics.SIZE);

        // Verify Users table operations
        request.RequestItems["Users"].Should().HaveCount(2);
        request.RequestItems["Users"][0].PutRequest.Should().NotBeNull();
        request.RequestItems["Users"][0].PutRequest.Item["userId"].S.Should().Be("user1");
        request.RequestItems["Users"][0].PutRequest.Item["name"].S.Should().Be("John Doe");
        request.RequestItems["Users"][1].DeleteRequest.Should().NotBeNull();
        request.RequestItems["Users"][1].DeleteRequest.Key["userId"].S.Should().Be("user2");

        // Verify Orders table operations
        request.RequestItems["Orders"].Should().HaveCount(2);
        request.RequestItems["Orders"][0].DeleteRequest.Should().NotBeNull();
        request.RequestItems["Orders"][0].DeleteRequest.Key["orderId"].S.Should().Be("order1");
        request.RequestItems["Orders"][0].DeleteRequest.Key["userId"].S.Should().Be("user1");
        request.RequestItems["Orders"][1].PutRequest.Should().NotBeNull();
        request.RequestItems["Orders"][1].PutRequest.Item["orderId"].S.Should().Be("order2");
        request.RequestItems["Orders"][1].PutRequest.Item["total"].N.Should().Be("99.99");
    }

    [Fact]
    public void WriteToTableMultipleCallsSameTableSuccess()
    {
        var builder = new BatchWriteItemRequestBuilder(_mockClient);
        var item1 = new Dictionary<string, AttributeValue> { { "pk", new AttributeValue { S = "1" } } };
        var item2 = new Dictionary<string, AttributeValue> { { "pk", new AttributeValue { S = "2" } } };

        builder.WriteToTable("TestTable", b => b.PutItem(item1))
               .WriteToTable("TestTable", b => b.PutItem(item2));

        var request = builder.ToBatchWriteItemRequest();

        request.Should().NotBeNull();
        request.RequestItems.Should().HaveCount(1);
        request.RequestItems["TestTable"].Should().HaveCount(2);
        request.RequestItems["TestTable"][0].PutRequest.Item["pk"].S.Should().Be("1");
        request.RequestItems["TestTable"][1].PutRequest.Item["pk"].S.Should().Be("2");
    }

    [Fact]
    public async Task ToDynamoDbResponseAsync_CallsClientSuccess()
    {
        var expectedResponse = new BatchWriteItemResponse();
        _mockClient.BatchWriteItemAsync(Arg.Any<BatchWriteItemRequest>(), Arg.Any<CancellationToken>())
                  .Returns(Task.FromResult(expectedResponse));

        var builder = new BatchWriteItemRequestBuilder(_mockClient);
        var item = new Dictionary<string, AttributeValue> { { "pk", new AttributeValue { S = "1" } } };
        builder.WriteToTable("TestTable", b => b.PutItem(item));

        var response = await builder.ToDynamoDbResponseAsync();

        response.Should().NotBeNull();
        await _mockClient.Received(1).BatchWriteItemAsync(Arg.Any<BatchWriteItemRequest>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ToDynamoDbResponseAsync_WithCancellationToken_CallsClientSuccess()
    {
        var expectedResponse = new BatchWriteItemResponse();
        var cancellationToken = new CancellationToken();
        _mockClient.BatchWriteItemAsync(Arg.Any<BatchWriteItemRequest>(), cancellationToken)
                  .Returns(Task.FromResult(expectedResponse));

        var builder = new BatchWriteItemRequestBuilder(_mockClient);
        var item = new Dictionary<string, AttributeValue> { { "pk", new AttributeValue { S = "1" } } };
        builder.WriteToTable("TestTable", b => b.PutItem(item));

        var response = await builder.ToDynamoDbResponseAsync(cancellationToken);

        response.Should().NotBeNull();
        await _mockClient.Received(1).BatchWriteItemAsync(Arg.Any<BatchWriteItemRequest>(), cancellationToken);
    }

    [Fact]
    public void ToBatchWriteItemRequestReturnsCorrectRequestSuccess()
    {
        var builder = new BatchWriteItemRequestBuilder(_mockClient);
        var item = new Dictionary<string, AttributeValue> { { "pk", new AttributeValue { S = "1" } } };
        builder.WriteToTable("TestTable", b => b.PutItem(item))
               .ReturnConsumedCapacity(ReturnConsumedCapacity.TOTAL);

        var request1 = builder.ToBatchWriteItemRequest();
        var request2 = builder.ToBatchWriteItemRequest();

        request1.Should().BeSameAs(request2);
        request1.RequestItems.Should().HaveCount(1);
        request1.ReturnConsumedCapacity.Should().Be(ReturnConsumedCapacity.TOTAL);
    }

    [Fact]
    public void EmptyBuilderProducesValidRequestSuccess()
    {
        var builder = new BatchWriteItemRequestBuilder(_mockClient);
        var request = builder.ToBatchWriteItemRequest();

        request.Should().NotBeNull();
        request.RequestItems.Should().NotBeNull();
        request.RequestItems.Should().BeEmpty();
        request.ReturnConsumedCapacity.Should().BeNull();
        request.ReturnItemCollectionMetrics.Should().BeNull();
    }

    [Fact]
    public void WriteToTableWithEmptyBuilderActionSuccess()
    {
        var builder = new BatchWriteItemRequestBuilder(_mockClient);
        builder.WriteToTable("TestTable", b => { });

        var request = builder.ToBatchWriteItemRequest();

        request.Should().NotBeNull();
        request.RequestItems.Should().BeEmpty();
    }
}
