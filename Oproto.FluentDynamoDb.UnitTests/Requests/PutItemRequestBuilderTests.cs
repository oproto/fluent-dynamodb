using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using FluentAssertions;
using NSubstitute;
using Oproto.FluentDynamoDb.Requests;

namespace Oproto.FluentDynamoDb.UnitTests.Requests;

public class PutItemRequestBuilderTests
{
    [Fact]
    public void ForTableSuccess()
    {
        var builder = new PutItemRequestBuilder(Substitute.For<IAmazonDynamoDB>());
        builder.ForTable("TestTable");
        var req = builder.ToPutItemRequest();
        req.Should().NotBeNull();
        req.TableName.Should().Be("TestTable");
    }
    
     #region Attributes
    
    [Fact]
    public void UsingExpressionAttributeNamesSuccess()
    {
        var builder = new PutItemRequestBuilder(Substitute.For<IAmazonDynamoDB>());
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
        var builder = new PutItemRequestBuilder(Substitute.For<IAmazonDynamoDB>());
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
        var builder = new PutItemRequestBuilder(Substitute.For<IAmazonDynamoDB>());
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
        var builder = new PutItemRequestBuilder(Substitute.For<IAmazonDynamoDB>());
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
        var builder = new PutItemRequestBuilder(Substitute.For<IAmazonDynamoDB>());
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
        var builder = new PutItemRequestBuilder(Substitute.For<IAmazonDynamoDB>());
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
        var builder = new PutItemRequestBuilder(Substitute.For<IAmazonDynamoDB>());
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
        var builder = new PutItemRequestBuilder(Substitute.For<IAmazonDynamoDB>());
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
        var builder = new PutItemRequestBuilder(Substitute.For<IAmazonDynamoDB>());
        builder.WithItem( new { Pk = "1"}, (item) =>
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
        var builder = new PutItemRequestBuilder(Substitute.For<IAmazonDynamoDB>());
        builder.Where("#pk = :pk");
        var req = builder.ToPutItemRequest();
        req.Should().NotBeNull();
        req.ConditionExpression.Should().Be("#pk = :pk");
    }

    #region ConsumedCapacity
    
    [Fact]
    public void ReturnConsumedCapacitySuccess()
    {
        var builder = new PutItemRequestBuilder(Substitute.For<IAmazonDynamoDB>());
        builder.ReturnConsumedCapacity(ReturnConsumedCapacity.TOTAL);
        var req = builder.ToPutItemRequest();
        req.Should().NotBeNull();
        req.ReturnConsumedCapacity.Should().Be(ReturnConsumedCapacity.TOTAL);
    }
    
    [Fact]
    public void ReturnTotalConsumedCapacitySuccess()
    {
        var builder = new PutItemRequestBuilder(Substitute.For<IAmazonDynamoDB>());
        builder.ReturnTotalConsumedCapacity();
        var req = builder.ToPutItemRequest();
        req.Should().NotBeNull();
        req.ReturnConsumedCapacity.Should().Be(ReturnConsumedCapacity.TOTAL);
    }
    
    [Fact]
    public void ReturnItemCollectionMetricsSuccess()
    {
        var builder = new PutItemRequestBuilder(Substitute.For<IAmazonDynamoDB>());
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
        var builder = new PutItemRequestBuilder(Substitute.For<IAmazonDynamoDB>());
        builder.ReturnNone();
        var req = builder.ToPutItemRequest();
        req.Should().NotBeNull();
        req.ReturnValues.Should().Be(ReturnValue.NONE);
    }
    
    [Fact]
    public void ReturnAllNewValuesSuccess()
    {
        var builder = new PutItemRequestBuilder(Substitute.For<IAmazonDynamoDB>());
        builder.ReturnAllNewValues();
        var req = builder.ToPutItemRequest();
        req.Should().NotBeNull();
        req.ReturnValues.Should().Be(ReturnValue.ALL_NEW);
    }
    
    [Fact]
    public void ReturnAllOldValuesSuccess()
    {
        var builder = new PutItemRequestBuilder(Substitute.For<IAmazonDynamoDB>());
        builder.ReturnAllOldValues();
        var req = builder.ToPutItemRequest();
        req.Should().NotBeNull();
        req.ReturnValues.Should().Be(ReturnValue.ALL_OLD);
    }
    
    [Fact]
    public void ReturnUpdatedNewValuesSuccess()
    {
        var builder = new PutItemRequestBuilder(Substitute.For<IAmazonDynamoDB>());
        builder.ReturnUpdatedNewValues();
        var req = builder.ToPutItemRequest();
        req.Should().NotBeNull();
        req.ReturnValues.Should().Be(ReturnValue.UPDATED_NEW);
    }
    
    [Fact]
    public void ReturnUpdatedOldValuesSuccess()
    {
        var builder = new PutItemRequestBuilder(Substitute.For<IAmazonDynamoDB>());
        builder.ReturnUpdatedOldValues();
        var req = builder.ToPutItemRequest();
        req.Should().NotBeNull();
        req.ReturnValues.Should().Be(ReturnValue.UPDATED_OLD);
    }
    
    [Fact]
    public void ReturnOldValuesOnConditionCheckFailureSuccess()
    {
        var builder = new PutItemRequestBuilder(Substitute.For<IAmazonDynamoDB>());
        builder.ReturnOldValuesOnConditionCheckFailure();
        var req = builder.ToPutItemRequest();
        req.Should().NotBeNull();
        req.ReturnValuesOnConditionCheckFailure.Should().Be(ReturnValuesOnConditionCheckFailure.ALL_OLD);
    }
    
    [Fact]
    public void ReturnNoValuesOnConditionCheckFailureSuccess()
    {
        var builder = new PutItemRequestBuilder(Substitute.For<IAmazonDynamoDB>());
        var req = builder.ToPutItemRequest();
        req.Should().NotBeNull();
        req.ReturnValuesOnConditionCheckFailure.Should().BeNull();
    }
    
    #endregion ReturnValues
}