using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using FluentAssertions;
using NSubstitute;
using Oproto.FluentDynamoDb.Requests;
using Oproto.FluentDynamoDb.Requests.Extensions;

namespace Oproto.FluentDynamoDb.UnitTests.Requests.Extensions;

public class WithClientExtensionsTests
{
    private readonly IAmazonDynamoDB _originalClient = Substitute.For<IAmazonDynamoDB>();
    private readonly IAmazonDynamoDB _scopedClient = Substitute.For<IAmazonDynamoDB>();

    [Fact]
    public void GetItemRequestBuilder_WithClient_ShouldPreserveConfiguration()
    {
        // Arrange
        var originalBuilder = new GetItemRequestBuilder(_originalClient)
            .ForTable("TestTable")
            .WithKey("pk", "test-key")
            .WithProjection("#name, #email")
            .WithAttribute("#name", "name")
            .WithAttribute("#email", "email")
            .UsingConsistentRead();

        // Act
        var newBuilder = originalBuilder.WithClient(_scopedClient);

        // Assert
        newBuilder.Should().NotBeSameAs(originalBuilder);
        
        var newRequest = newBuilder.ToGetItemRequest();
        newRequest.TableName.Should().Be("TestTable");
        newRequest.Key.Should().ContainKey("pk");
        newRequest.Key["pk"].S.Should().Be("test-key");
        newRequest.ProjectionExpression.Should().Be("#name, #email");
        newRequest.ExpressionAttributeNames.Should().ContainKeys("#name", "#email");
        newRequest.ExpressionAttributeNames["#name"].Should().Be("name");
        newRequest.ExpressionAttributeNames["#email"].Should().Be("email");
        newRequest.ConsistentRead.Should().BeTrue();
    }

    [Fact]
    public void QueryRequestBuilder_WithClient_ShouldPreserveConfiguration()
    {
        // Arrange
        var originalBuilder = new QueryRequestBuilder(_originalClient)
            .ForTable("TestTable")
            .Where("pk = :pk AND begins_with(sk, :prefix)")
            .WithFilter("#status = :status")
            .WithValue(":pk", "test-partition")
            .WithValue(":prefix", "ORDER#")
            .WithValue(":status", "ACTIVE")
            .WithAttribute("#status", "status")
            .Take(10)
            .UsingIndex("GSI1")
            .OrderDescending();

        // Act
        var newBuilder = originalBuilder.WithClient(_scopedClient);

        // Assert
        newBuilder.Should().NotBeSameAs(originalBuilder);
        
        var newRequest = newBuilder.ToQueryRequest();
        newRequest.TableName.Should().Be("TestTable");
        newRequest.KeyConditionExpression.Should().Be("pk = :pk AND begins_with(sk, :prefix)");
        newRequest.FilterExpression.Should().Be("#status = :status");
        newRequest.ExpressionAttributeValues.Should().ContainKeys(":pk", ":prefix", ":status");
        newRequest.ExpressionAttributeValues[":pk"].S.Should().Be("test-partition");
        newRequest.ExpressionAttributeValues[":prefix"].S.Should().Be("ORDER#");
        newRequest.ExpressionAttributeValues[":status"].S.Should().Be("ACTIVE");
        newRequest.ExpressionAttributeNames.Should().ContainKey("#status");
        newRequest.ExpressionAttributeNames["#status"].Should().Be("status");
        newRequest.Limit.Should().Be(10);
        newRequest.IndexName.Should().Be("GSI1");
        newRequest.ScanIndexForward.Should().BeFalse();
    }

    [Fact]
    public void PutItemRequestBuilder_WithClient_ShouldPreserveConfiguration()
    {
        // Arrange
        var testItem = new Dictionary<string, AttributeValue>
        {
            ["pk"] = new AttributeValue("test-key"),
            ["name"] = new AttributeValue("Test Name")
        };

        var originalBuilder = new PutItemRequestBuilder(_originalClient)
            .ForTable("TestTable")
            .WithItem(testItem)
            .Where("attribute_not_exists(pk)")
            .ReturnAllOldValues();

        // Act
        var newBuilder = originalBuilder.WithClient(_scopedClient);

        // Assert
        newBuilder.Should().NotBeSameAs(originalBuilder);
        
        var newRequest = newBuilder.ToPutItemRequest();
        newRequest.TableName.Should().Be("TestTable");
        newRequest.Item.Should().ContainKeys("pk", "name");
        newRequest.Item["pk"].S.Should().Be("test-key");
        newRequest.Item["name"].S.Should().Be("Test Name");
        newRequest.ConditionExpression.Should().Be("attribute_not_exists(pk)");
        newRequest.ReturnValues.Should().Be(ReturnValue.ALL_OLD);
    }

    [Fact]
    public void UpdateItemRequestBuilder_WithClient_ShouldPreserveConfiguration()
    {
        // Arrange
        var originalBuilder = new UpdateItemRequestBuilder(_originalClient)
            .ForTable("TestTable")
            .WithKey("pk", "test-key")
            .Set("SET #name = :name, #count = #count + :inc")
            .Where("attribute_exists(pk)")
            .WithAttribute("#name", "name")
            .WithAttribute("#count", "count")
            .WithValue(":name", "Updated Name")
            .WithValue(":inc", 1)
            .ReturnUpdatedNewValues();

        // Act
        var newBuilder = originalBuilder.WithClient(_scopedClient);

        // Assert
        newBuilder.Should().NotBeSameAs(originalBuilder);
        
        var newRequest = newBuilder.ToUpdateItemRequest();
        newRequest.TableName.Should().Be("TestTable");
        newRequest.Key.Should().ContainKey("pk");
        newRequest.Key["pk"].S.Should().Be("test-key");
        newRequest.UpdateExpression.Should().Be("SET #name = :name, #count = #count + :inc");
        newRequest.ConditionExpression.Should().Be("attribute_exists(pk)");
        newRequest.ExpressionAttributeNames.Should().ContainKeys("#name", "#count");
        newRequest.ExpressionAttributeNames["#name"].Should().Be("name");
        newRequest.ExpressionAttributeNames["#count"].Should().Be("count");
        newRequest.ExpressionAttributeValues.Should().ContainKeys(":name", ":inc");
        newRequest.ExpressionAttributeValues[":name"].S.Should().Be("Updated Name");
        newRequest.ExpressionAttributeValues[":inc"].N.Should().Be("1");
        newRequest.ReturnValues.Should().Be(ReturnValue.UPDATED_NEW);
    }

    [Fact]
    public void DeleteItemRequestBuilder_WithClient_ShouldPreserveConfiguration()
    {
        // Arrange
        var originalBuilder = new DeleteItemRequestBuilder(_originalClient)
            .ForTable("TestTable")
            .WithKey("pk", "test-key", "sk", "sort-key")
            .Where("attribute_exists(#status)")
            .WithAttribute("#status", "status")
            .ReturnAllOldValues();

        // Act
        var newBuilder = originalBuilder.WithClient(_scopedClient);

        // Assert
        newBuilder.Should().NotBeSameAs(originalBuilder);
        
        var newRequest = newBuilder.ToDeleteItemRequest();
        newRequest.TableName.Should().Be("TestTable");
        newRequest.Key.Should().ContainKeys("pk", "sk");
        newRequest.Key["pk"].S.Should().Be("test-key");
        newRequest.Key["sk"].S.Should().Be("sort-key");
        newRequest.ConditionExpression.Should().Be("attribute_exists(#status)");
        newRequest.ExpressionAttributeNames.Should().ContainKey("#status");
        newRequest.ExpressionAttributeNames["#status"].Should().Be("status");
        newRequest.ReturnValues.Should().Be(ReturnValue.ALL_OLD);
    }

    [Fact]
    public void ScanRequestBuilder_WithClient_ShouldPreserveConfiguration()
    {
        // Arrange
        var originalBuilder = new ScanRequestBuilder(_originalClient)
            .ForTable("TestTable")
            .WithFilter("#status = :status AND #count > :minCount")
            .WithAttribute("#status", "status")
            .WithAttribute("#count", "count")
            .WithValue(":status", "ACTIVE")
            .WithValue(":minCount", 10)
            .WithProjection("#name, #email")
            .Take(50)
            .UsingIndex("GSI1")
            .WithSegment(0, 4);

        // Act
        var newBuilder = originalBuilder.WithClient(_scopedClient);

        // Assert
        newBuilder.Should().NotBeSameAs(originalBuilder);
        
        var newRequest = newBuilder.ToScanRequest();
        newRequest.TableName.Should().Be("TestTable");
        newRequest.FilterExpression.Should().Be("#status = :status AND #count > :minCount");
        newRequest.ExpressionAttributeNames.Should().ContainKeys("#status", "#count");
        newRequest.ExpressionAttributeNames["#status"].Should().Be("status");
        newRequest.ExpressionAttributeNames["#count"].Should().Be("count");
        newRequest.ExpressionAttributeValues.Should().ContainKeys(":status", ":minCount");
        newRequest.ExpressionAttributeValues[":status"].S.Should().Be("ACTIVE");
        newRequest.ExpressionAttributeValues[":minCount"].N.Should().Be("10");
        newRequest.ProjectionExpression.Should().Be("#name, #email");
        newRequest.Limit.Should().Be(50);
        newRequest.IndexName.Should().Be("GSI1");
        newRequest.Segment.Should().Be(0);
        newRequest.TotalSegments.Should().Be(4);
    }

    [Fact]
    public void BatchGetItemRequestBuilder_WithClient_ShouldPreserveConfiguration()
    {
        // Arrange
        var originalBuilder = new BatchGetItemRequestBuilder(_originalClient)
            .GetFromTable("Users", builder => builder
                .WithKey("id", "user1")
                .WithKey("id", "user2")
                .WithProjection("#name, #email")
                .WithAttribute("#name", "name")
                .WithAttribute("#email", "email"))
            .GetFromTable("Orders", builder => builder
                .WithKey("orderId", "order123")
                .UsingConsistentRead())
            .ReturnConsumedCapacity(ReturnConsumedCapacity.TOTAL);

        // Act
        var newBuilder = originalBuilder.WithClient(_scopedClient);

        // Assert
        newBuilder.Should().NotBeSameAs(originalBuilder);
        
        var newRequest = newBuilder.ToBatchGetItemRequest();
        newRequest.RequestItems.Should().ContainKeys("Users", "Orders");
        newRequest.RequestItems["Users"].Keys.Should().HaveCount(2);
        newRequest.RequestItems["Orders"].Keys.Should().HaveCount(1);
        newRequest.ReturnConsumedCapacity.Should().Be(ReturnConsumedCapacity.TOTAL);
    }

    [Fact]
    public void BatchWriteItemRequestBuilder_WithClient_ShouldPreserveConfiguration()
    {
        // Arrange
        var testItem = new Dictionary<string, AttributeValue>
        {
            ["id"] = new AttributeValue("user1"),
            ["name"] = new AttributeValue("John Doe")
        };

        var originalBuilder = new BatchWriteItemRequestBuilder(_originalClient)
            .WriteToTable("Users", builder => builder
                .PutItem(testItem)
                .DeleteItem("id", "user2"))
            .ReturnTotalConsumedCapacity()
            .ReturnItemCollectionMetrics();

        // Act
        var newBuilder = originalBuilder.WithClient(_scopedClient);

        // Assert
        newBuilder.Should().NotBeSameAs(originalBuilder);
        
        var newRequest = newBuilder.ToBatchWriteItemRequest();
        newRequest.RequestItems.Should().ContainKey("Users");
        newRequest.RequestItems["Users"].Should().HaveCount(2);
        newRequest.ReturnConsumedCapacity.Should().Be(ReturnConsumedCapacity.TOTAL);
        newRequest.ReturnItemCollectionMetrics.Should().Be(ReturnItemCollectionMetrics.SIZE);
    }

    [Fact]
    public void TransactWriteItemsRequestBuilder_WithClient_ShouldPreserveConfiguration()
    {
        // Arrange
        var testItem = new Dictionary<string, AttributeValue>
        {
            ["id"] = new AttributeValue("user1"),
            ["name"] = new AttributeValue("John Doe")
        };

        var originalBuilder = new TransactWriteItemsRequestBuilder(_originalClient)
            .WithClientRequestToken("test-token-123")
            .ReturnTotalConsumedCapacity()
            .ReturnItemCollectionMetrics();

        // Act
        var newBuilder = originalBuilder.WithClient(_scopedClient);

        // Assert
        newBuilder.Should().NotBeSameAs(originalBuilder);
        
        var newRequest = newBuilder.ToTransactWriteItemsRequest();
        newRequest.ClientRequestToken.Should().Be("test-token-123");
        newRequest.ReturnConsumedCapacity.Should().Be(ReturnConsumedCapacity.TOTAL);
        newRequest.ReturnItemCollectionMetrics.Should().Be(ReturnItemCollectionMetrics.SIZE);
    }

    [Fact]
    public void TransactGetItemsRequestBuilder_WithClient_ShouldPreserveConfiguration()
    {
        // Arrange
        var originalBuilder = new TransactGetItemsRequestBuilder(_originalClient)
            .ReturnConsumedCapacity(ReturnConsumedCapacity.INDEXES);

        // Act
        var newBuilder = originalBuilder.WithClient(_scopedClient);

        // Assert
        newBuilder.Should().NotBeSameAs(originalBuilder);
        
        var newRequest = newBuilder.ToTransactGetItemsRequest();
        newRequest.ReturnConsumedCapacity.Should().Be(ReturnConsumedCapacity.INDEXES);
    }

    [Fact]
    public void WithClient_EmptyBuilder_ShouldCreateNewBuilderWithClient()
    {
        // Arrange
        var originalBuilder = new GetItemRequestBuilder(_originalClient);

        // Act
        var newBuilder = originalBuilder.WithClient(_scopedClient);

        // Assert
        newBuilder.Should().NotBeSameAs(originalBuilder);
        
        var newRequest = newBuilder.ToGetItemRequest();
        newRequest.Should().NotBeNull();
        newRequest.ExpressionAttributeNames.Should().BeEmpty();
    }

    [Fact]
    public async Task WithClient_ExecuteAsync_ShouldUseScopedClient()
    {
        // Arrange
        var expectedResponse = new GetItemResponse
        {
            Item = new Dictionary<string, AttributeValue>
            {
                ["pk"] = new AttributeValue("test-key"),
                ["name"] = new AttributeValue("Test Name")
            }
        };

        _scopedClient.GetItemAsync(Arg.Any<GetItemRequest>(), Arg.Any<CancellationToken>())
            .Returns(expectedResponse);

        var builder = new GetItemRequestBuilder(_originalClient)
            .ForTable("TestTable")
            .WithKey("pk", "test-key")
            .WithClient(_scopedClient);

        // Act
        var response = await builder.ExecuteAsync();

        // Assert
        response.Should().BeSameAs(expectedResponse);
        await _scopedClient.Received(1).GetItemAsync(Arg.Any<GetItemRequest>(), Arg.Any<CancellationToken>());
        await _originalClient.DidNotReceive().GetItemAsync(Arg.Any<GetItemRequest>(), Arg.Any<CancellationToken>());
    }
}