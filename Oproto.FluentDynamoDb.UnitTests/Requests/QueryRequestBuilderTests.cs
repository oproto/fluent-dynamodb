using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using FluentAssertions;
using NSubstitute;
using Oproto.FluentDynamoDb.Requests;
using Oproto.FluentDynamoDb.Requests.Extensions;

namespace Oproto.FluentDynamoDb.UnitTests.Requests;

public class QueryRequestBuilderTests
{
    [Fact]
    public void ForTableSuccess()
    {
        var builder = new QueryRequestBuilder(Substitute.For<IAmazonDynamoDB>());
        builder.ForTable("TestTable");
        var req = builder.ToQueryRequest();
        req.Should().NotBeNull();
        req.TableName.Should().Be("TestTable");
    }
    
    [Fact]
    public void UsingIndexSuccess()
    {
        var builder = new QueryRequestBuilder(Substitute.For<IAmazonDynamoDB>());
        builder.UsingIndex("gsi1");
        var req = builder.ToQueryRequest();
        req.Should().NotBeNull();
        req.IndexName.Should().Be("gsi1");
    }
    
    #region Attributes
    
    [Fact]
    public void UsingExpressionAttributeNamesSuccess()
    {
        var builder = new QueryRequestBuilder(Substitute.For<IAmazonDynamoDB>());
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
        var builder = new QueryRequestBuilder(Substitute.For<IAmazonDynamoDB>());
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
        var builder = new QueryRequestBuilder(Substitute.For<IAmazonDynamoDB>());
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
        var builder = new QueryRequestBuilder(Substitute.For<IAmazonDynamoDB>());
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
        var builder = new QueryRequestBuilder(Substitute.For<IAmazonDynamoDB>());
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
        var builder = new QueryRequestBuilder(Substitute.For<IAmazonDynamoDB>());
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
        var builder = new QueryRequestBuilder(Substitute.For<IAmazonDynamoDB>());
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
        var builder = new QueryRequestBuilder(Substitute.For<IAmazonDynamoDB>());
        builder.Where("#pk = :pk");
        var req = builder.ToQueryRequest();
        req.Should().NotBeNull();
        req.KeyConditionExpression.Should().Be("#pk = :pk");
    }
    
    [Fact]
    public void WithFilterSuccess()
    {
        var builder = new QueryRequestBuilder(Substitute.For<IAmazonDynamoDB>());
        builder.WithFilter("#v >= :num");
        var req = builder.ToQueryRequest();
        req.Should().NotBeNull();
        req.FilterExpression.Should().Be("#v >= :num");
    }
    
    [Fact]
    public void ProjectionExpressionSuccess()
    {
        var builder = new QueryRequestBuilder(Substitute.For<IAmazonDynamoDB>());
        builder.WithProjection("description, price");
        var req = builder.ToQueryRequest();
        req.Should().NotBeNull();
        req.ProjectionExpression.Should().Be("description, price");
    }
    
    
    #region ConsumedCapacity
    
    [Fact]
    public void ReturnConsumedCapacitySuccess()
    {
        var builder = new QueryRequestBuilder(Substitute.For<IAmazonDynamoDB>());
        builder.ReturnConsumedCapacity(ReturnConsumedCapacity.TOTAL);
        var req = builder.ToQueryRequest();
        req.Should().NotBeNull();
        req.ReturnConsumedCapacity.Should().Be(ReturnConsumedCapacity.TOTAL);
    }
    
    [Fact]
    public void ReturnTotalConsumedCapacitySuccess()
    {
        var builder = new QueryRequestBuilder(Substitute.For<IAmazonDynamoDB>());
        builder.ReturnTotalConsumedCapacity();
        var req = builder.ToQueryRequest();
        req.Should().NotBeNull();
        req.ReturnConsumedCapacity.Should().Be(ReturnConsumedCapacity.TOTAL);
    }
    
    [Fact]
    public void ReturnIndexConsumedCapacitySuccess()
    {
        var builder = new QueryRequestBuilder(Substitute.For<IAmazonDynamoDB>());
        builder.ReturnIndexConsumedCapacity();
        var req = builder.ToQueryRequest();
        req.Should().NotBeNull();
        req.ReturnConsumedCapacity.Should().Be(ReturnConsumedCapacity.INDEXES);
    }
    
    #endregion ConsumedCapacity
    
    [Fact]
    public void UsingConsistentReadSuccess()
    {
        var builder = new QueryRequestBuilder(Substitute.For<IAmazonDynamoDB>());
        builder.UsingConsistentRead();
        var req = builder.ToQueryRequest();
        req.Should().NotBeNull();
        req.ConsistentRead.Should().BeTrue();
    }
    
    [Fact]
    public void StartAtSuccess()
    {
        var builder = new QueryRequestBuilder(Substitute.For<IAmazonDynamoDB>());
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
        var builder = new QueryRequestBuilder(Substitute.For<IAmazonDynamoDB>());
        builder.Take(10);
        var req = builder.ToQueryRequest();
        req.Should().NotBeNull();
        req.Limit.Should().Be(10);
    }
}