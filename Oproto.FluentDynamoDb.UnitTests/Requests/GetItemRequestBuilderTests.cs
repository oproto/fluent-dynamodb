using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using FluentAssertions;
using NSubstitute;
using Oproto.FluentDynamoDb.Requests;
using Oproto.FluentDynamoDb.Requests.Extensions;

namespace Oproto.FluentDynamoDb.UnitTests.Requests;

public class GetItemRequestBuilderTests
{
    [Fact]
    public void ForTableSuccess()
    {
        var builder = new GetItemRequestBuilder(Substitute.For<IAmazonDynamoDB>());
        builder.ForTable("TestTable");
        var req = builder.ToGetItemRequest();
        req.Should().NotBeNull();
        req.TableName.Should().Be("TestTable");
    }
    
    [Fact]
    public void WithKeyPkStringValueSuccess()
    {
        var builder = new GetItemRequestBuilder(Substitute.For<IAmazonDynamoDB>());
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
        var builder = new GetItemRequestBuilder(Substitute.For<IAmazonDynamoDB>());
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
        var builder = new GetItemRequestBuilder(Substitute.For<IAmazonDynamoDB>());
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
        var builder = new GetItemRequestBuilder(Substitute.For<IAmazonDynamoDB>());
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
        var builder = new GetItemRequestBuilder(Substitute.For<IAmazonDynamoDB>());
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
        var builder = new GetItemRequestBuilder(Substitute.For<IAmazonDynamoDB>());
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
        var builder = new GetItemRequestBuilder(Substitute.For<IAmazonDynamoDB>());
        builder.WithProjection("description, price");
        var req = builder.ToGetItemRequest();
        req.Should().NotBeNull();
        req.ProjectionExpression.Should().Be("description, price");
    }
    
    [Fact]
    public void ReturnConsumedCapacitySuccess()
    {
        var builder = new GetItemRequestBuilder(Substitute.For<IAmazonDynamoDB>());
        builder.ReturnConsumedCapacity(ReturnConsumedCapacity.TOTAL);
        var req = builder.ToGetItemRequest();
        req.Should().NotBeNull();
        req.ReturnConsumedCapacity.Should().Be(ReturnConsumedCapacity.TOTAL);
    }
    
    [Fact]
    public void ReturnTotalConsumedCapacitySuccess()
    {
        var builder = new GetItemRequestBuilder(Substitute.For<IAmazonDynamoDB>());
        builder.ReturnTotalConsumedCapacity();
        var req = builder.ToGetItemRequest();
        req.Should().NotBeNull();
        req.ReturnConsumedCapacity.Should().Be(ReturnConsumedCapacity.TOTAL);
    }
    
    [Fact]
    public void UsingConsistentReadSuccess()
    {
        var builder = new GetItemRequestBuilder(Substitute.For<IAmazonDynamoDB>());
        builder.UsingConsistentRead();
        var req = builder.ToGetItemRequest();
        req.Should().NotBeNull();
        req.ConsistentRead.Should().BeTrue();
    }
}