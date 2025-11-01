using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using FluentAssertions;
using NSubstitute;
using Oproto.FluentDynamoDb.Logging;
using Oproto.FluentDynamoDb.Requests;
using Oproto.FluentDynamoDb.Requests.Extensions;
using Oproto.FluentDynamoDb.Storage;

namespace Oproto.FluentDynamoDb.UnitTests.Requests;

public class BatchGetItemRequestBuilderTests
{
    private class TestEntity : IDynamoDbEntity
    {
        public string Id { get; set; } = string.Empty;

        public static Dictionary<string, AttributeValue> ToDynamoDb<TSelf>(TSelf entity, IDynamoDbLogger? logger = null) where TSelf : IDynamoDbEntity
        {
            var testEntity = entity as TestEntity;
            return new Dictionary<string, AttributeValue>
            {
                ["pk"] = new AttributeValue { S = testEntity?.Id ?? string.Empty }
            };
        }

        public static TSelf FromDynamoDb<TSelf>(Dictionary<string, AttributeValue> item, IDynamoDbLogger? logger = null) where TSelf : IDynamoDbEntity
        {
            var entity = new TestEntity
            {
                Id = item.TryGetValue("pk", out var pk) ? pk.S : string.Empty
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
            return new EntityMetadata { TableName = "test-table" };
        }
    }

    private readonly IAmazonDynamoDB _mockClient;

    public BatchGetItemRequestBuilderTests()
    {
        _mockClient = Substitute.For<IAmazonDynamoDB>();
    }

    [Fact]
    public void ConstructorSuccess()
    {
        var builder = new BatchGetItemRequestBuilder(_mockClient);
        var request = builder.ToBatchGetItemRequest();

        request.Should().NotBeNull();
        request.RequestItems.Should().NotBeNull();
        request.RequestItems.Should().BeEmpty();
    }

    [Fact]
    public void GetFromTableSingleTableSuccess()
    {
        var builder = new BatchGetItemRequestBuilder(_mockClient);
        builder.GetFromTable("TestTable", b => b.WithKey("pk", "1"));

        var request = builder.ToBatchGetItemRequest();

        request.Should().NotBeNull();
        request.RequestItems.Should().HaveCount(1);
        request.RequestItems.Should().ContainKey("TestTable");
        request.RequestItems["TestTable"].Keys.Should().HaveCount(1);
        request.RequestItems["TestTable"].Keys[0]["pk"].S.Should().Be("1");
    }

    [Fact]
    public void GetFromTableMultipleTablesSuccess()
    {
        var builder = new BatchGetItemRequestBuilder(_mockClient);
        builder.GetFromTable("Table1", b => b.WithKey("pk", "1"))
               .GetFromTable("Table2", b => b.WithKey("id", "2"));

        var request = builder.ToBatchGetItemRequest();

        request.Should().NotBeNull();
        request.RequestItems.Should().HaveCount(2);
        request.RequestItems.Should().ContainKey("Table1");
        request.RequestItems.Should().ContainKey("Table2");
        request.RequestItems["Table1"].Keys.Should().HaveCount(1);
        request.RequestItems["Table1"].Keys[0]["pk"].S.Should().Be("1");
        request.RequestItems["Table2"].Keys.Should().HaveCount(1);
        request.RequestItems["Table2"].Keys[0]["id"].S.Should().Be("2");
    }

    [Fact]
    public void GetFromTableMultipleKeysPerTableSuccess()
    {
        var builder = new BatchGetItemRequestBuilder(_mockClient);
        builder.GetFromTable("TestTable", b =>
            b.WithKey("pk", "1")
             .WithKey("pk", "2")
             .WithKey("pk", "3"));

        var request = builder.ToBatchGetItemRequest();

        request.Should().NotBeNull();
        request.RequestItems.Should().HaveCount(1);
        request.RequestItems["TestTable"].Keys.Should().HaveCount(3);
        request.RequestItems["TestTable"].Keys[0]["pk"].S.Should().Be("1");
        request.RequestItems["TestTable"].Keys[1]["pk"].S.Should().Be("2");
        request.RequestItems["TestTable"].Keys[2]["pk"].S.Should().Be("3");
    }

    [Fact]
    public void GetFromTableWithProjectionSuccess()
    {
        var builder = new BatchGetItemRequestBuilder(_mockClient);
        builder.GetFromTable("TestTable", b =>
            b.WithKey("pk", "1")
             .WithProjection("description, price"));

        var request = builder.ToBatchGetItemRequest();

        request.Should().NotBeNull();
        request.RequestItems["TestTable"].ProjectionExpression.Should().Be("description, price");
    }

    [Fact]
    public void GetFromTableWithConsistentReadSuccess()
    {
        var builder = new BatchGetItemRequestBuilder(_mockClient);
        builder.GetFromTable("TestTable", b =>
            b.WithKey("pk", "1")
             .UsingConsistentRead());

        var request = builder.ToBatchGetItemRequest();

        request.Should().NotBeNull();
        request.RequestItems["TestTable"].ConsistentRead.Should().BeTrue();
    }

    [Fact]
    public void GetFromTableWithAttributeNamesSuccess()
    {
        var builder = new BatchGetItemRequestBuilder(_mockClient);
        builder.GetFromTable("TestTable", b =>
            b.WithKey("pk", "1")
             .WithAttribute("#pk", "pk")
             .WithAttribute("#desc", "description"));

        var request = builder.ToBatchGetItemRequest();

        request.Should().NotBeNull();
        request.RequestItems["TestTable"].ExpressionAttributeNames.Should().HaveCount(2);
        request.RequestItems["TestTable"].ExpressionAttributeNames["#pk"].Should().Be("pk");
        request.RequestItems["TestTable"].ExpressionAttributeNames["#desc"].Should().Be("description");
    }

    [Fact]
    public void ReturnConsumedCapacitySuccess()
    {
        var builder = new BatchGetItemRequestBuilder(_mockClient);
        builder.ReturnConsumedCapacity(ReturnConsumedCapacity.TOTAL);

        var request = builder.ToBatchGetItemRequest();

        request.Should().NotBeNull();
        request.ReturnConsumedCapacity.Should().Be(ReturnConsumedCapacity.TOTAL);
    }

    [Fact]
    public void ComplexMultiTableRequestSuccess()
    {
        var builder = new BatchGetItemRequestBuilder(_mockClient);
        builder.GetFromTable("Users", b =>
                   b.WithKey("userId", "user1")
                    .WithKey("userId", "user2")
                    .WithProjection("#name, email")
                    .WithAttribute("#name", "name")
                    .UsingConsistentRead())
               .GetFromTable("Orders", b =>
                   b.WithKey("orderId", "order1", "userId", "user1")
                    .WithKey("orderId", "order2", "userId", "user1")
                    .WithProjection("orderId, total, #status")
                    .WithAttribute("#status", "status"))
               .ReturnConsumedCapacity(ReturnConsumedCapacity.INDEXES);

        var request = builder.ToBatchGetItemRequest();

        request.Should().NotBeNull();
        request.RequestItems.Should().HaveCount(2);
        request.ReturnConsumedCapacity.Should().Be(ReturnConsumedCapacity.INDEXES);

        // Verify Users table
        request.RequestItems["Users"].Keys.Should().HaveCount(2);
        request.RequestItems["Users"].Keys[0]["userId"].S.Should().Be("user1");
        request.RequestItems["Users"].Keys[1]["userId"].S.Should().Be("user2");
        request.RequestItems["Users"].ProjectionExpression.Should().Be("#name, email");
        request.RequestItems["Users"].ConsistentRead.Should().BeTrue();
        request.RequestItems["Users"].ExpressionAttributeNames.Should().HaveCount(1);
        request.RequestItems["Users"].ExpressionAttributeNames["#name"].Should().Be("name");

        // Verify Orders table
        request.RequestItems["Orders"].Keys.Should().HaveCount(2);
        request.RequestItems["Orders"].Keys[0]["orderId"].S.Should().Be("order1");
        request.RequestItems["Orders"].Keys[0]["userId"].S.Should().Be("user1");
        request.RequestItems["Orders"].Keys[1]["orderId"].S.Should().Be("order2");
        request.RequestItems["Orders"].Keys[1]["userId"].S.Should().Be("user1");
        request.RequestItems["Orders"].ProjectionExpression.Should().Be("orderId, total, #status");
        request.RequestItems["Orders"].ConsistentRead.Should().BeFalse();
        request.RequestItems["Orders"].ExpressionAttributeNames.Should().HaveCount(1);
        request.RequestItems["Orders"].ExpressionAttributeNames["#status"].Should().Be("status");
    }

    [Fact]
    public void GetFromTableOverwritesSameTableSuccess()
    {
        var builder = new BatchGetItemRequestBuilder(_mockClient);
        builder.GetFromTable("TestTable", b => b.WithKey("pk", "1"))
               .GetFromTable("TestTable", b => b.WithKey("pk", "2"));

        var request = builder.ToBatchGetItemRequest();

        request.Should().NotBeNull();
        request.RequestItems.Should().HaveCount(1);
        request.RequestItems["TestTable"].Keys.Should().HaveCount(1);
        request.RequestItems["TestTable"].Keys[0]["pk"].S.Should().Be("2");
    }

    [Fact]
    public async Task ToDynamoDbResponseAsync_CallsClientSuccess()
    {
        var expectedResponse = new BatchGetItemResponse
        {
            Responses = new Dictionary<string, List<Dictionary<string, AttributeValue>>>()
        };
        _mockClient.BatchGetItemAsync(Arg.Any<BatchGetItemRequest>(), Arg.Any<CancellationToken>())
                  .Returns(Task.FromResult(expectedResponse));

        var builder = new BatchGetItemRequestBuilder(_mockClient);
        builder.GetFromTable("TestTable", b => b.WithKey("pk", "1"));

        var response = await builder.ToDynamoDbResponseAsync();

        response.Should().NotBeNull();
        await _mockClient.Received(1).BatchGetItemAsync(Arg.Any<BatchGetItemRequest>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ToDynamoDbResponseAsync_WithCancellationToken_CallsClientSuccess()
    {
        var expectedResponse = new BatchGetItemResponse
        {
            Responses = new Dictionary<string, List<Dictionary<string, AttributeValue>>>()
        };
        var cancellationToken = new CancellationToken();
        _mockClient.BatchGetItemAsync(Arg.Any<BatchGetItemRequest>(), cancellationToken)
                  .Returns(Task.FromResult(expectedResponse));

        var builder = new BatchGetItemRequestBuilder(_mockClient);
        builder.GetFromTable("TestTable", b => b.WithKey("pk", "1"));

        var response = await builder.ToDynamoDbResponseAsync(cancellationToken);

        response.Should().NotBeNull();
        await _mockClient.Received(1).BatchGetItemAsync(Arg.Any<BatchGetItemRequest>(), cancellationToken);
    }

    [Fact]
    public void ToBatchGetItemRequestReturnsCorrectRequestSuccess()
    {
        var builder = new BatchGetItemRequestBuilder(_mockClient);
        builder.GetFromTable("TestTable", b =>
                   b.WithKey("pk", "1")
                    .WithProjection("description"))
               .ReturnConsumedCapacity(ReturnConsumedCapacity.TOTAL);

        var request1 = builder.ToBatchGetItemRequest();
        var request2 = builder.ToBatchGetItemRequest();

        request1.Should().BeSameAs(request2);
        request1.RequestItems.Should().HaveCount(1);
        request1.ReturnConsumedCapacity.Should().Be(ReturnConsumedCapacity.TOTAL);
    }

    [Fact]
    public void EmptyBuilderProducesValidRequestSuccess()
    {
        var builder = new BatchGetItemRequestBuilder(_mockClient);
        var request = builder.ToBatchGetItemRequest();

        request.Should().NotBeNull();
        request.RequestItems.Should().NotBeNull();
        request.RequestItems.Should().BeEmpty();
        request.ReturnConsumedCapacity.Should().BeNull();
    }

    [Fact]
    public void GetFromTableWithCompositeKeysAndAttributeValuesSuccess()
    {
        var builder = new BatchGetItemRequestBuilder(_mockClient);
        builder.GetFromTable("TestTable", b =>
            b.WithKey("pk", new AttributeValue { S = "partition1" }, "sk", new AttributeValue { N = "123" })
             .WithKey("pk", new AttributeValue { S = "partition2" }, "sk", new AttributeValue { N = "456" }));

        var request = builder.ToBatchGetItemRequest();

        request.Should().NotBeNull();
        request.RequestItems["TestTable"].Keys.Should().HaveCount(2);
        request.RequestItems["TestTable"].Keys[0]["pk"].S.Should().Be("partition1");
        request.RequestItems["TestTable"].Keys[0]["sk"].N.Should().Be("123");
        request.RequestItems["TestTable"].Keys[1]["pk"].S.Should().Be("partition2");
        request.RequestItems["TestTable"].Keys[1]["sk"].N.Should().Be("456");
    }
}
