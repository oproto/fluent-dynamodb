using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using FluentAssertions;
using NSubstitute;
using Oproto.FluentDynamoDb.Requests;
using Oproto.FluentDynamoDb.Requests.Extensions;

namespace Oproto.FluentDynamoDb.UnitTests.Requests;

public class UpdateItemRequestBuilderTests
{
    [Fact]
    public void ForTableSuccess()
    {
        var builder = new UpdateItemRequestBuilder(Substitute.For<IAmazonDynamoDB>());
        builder.ForTable("TestTable");
        var req = builder.ToUpdateItemRequest();
        req.Should().NotBeNull();
        req.TableName.Should().Be("TestTable");
    }
    
    #region Keys
    
    [Fact]
    public void WithKeyPkStringValueSuccess()
    {
        var builder = new UpdateItemRequestBuilder(Substitute.For<IAmazonDynamoDB>());
        builder.WithKey("pk", "1");
        var req = builder.ToUpdateItemRequest();
        req.Should().NotBeNull();
        req.Key.Should().NotBeNull();
        req.Key.Should().ContainKey("pk");
        req.Key.Keys.Should().HaveCount(1);
        req.Key["pk"].S.Should().Be("1");
    }
    
    [Fact]
    public void WithKeyPkSkStringValueSuccess()
    {
        var builder = new UpdateItemRequestBuilder(Substitute.For<IAmazonDynamoDB>());
        builder.WithKey("pk", "1", "sk", "abcd");
        var req = builder.ToUpdateItemRequest();
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
        var builder = new UpdateItemRequestBuilder(Substitute.For<IAmazonDynamoDB>());
        builder.WithKey("pk", new AttributeValue() { S = "1" }, "sk", new AttributeValue() { S = "abcd" });
        var req = builder.ToUpdateItemRequest();
        req.Should().NotBeNull();
        req.Key.Should().NotBeNull();
        req.Key.Should().ContainKey("pk");
        req.Key.Should().ContainKey("sk");
        req.Key.Keys.Should().HaveCount(2);
        req.Key["pk"].S.Should().Be("1");
        req.Key["sk"].S.Should().Be("abcd");
    }
    
    #endregion Keys
    
    #region Attributes
    
    [Fact]
    public void UsingExpressionAttributeNamesSuccess()
    {
        var builder = new UpdateItemRequestBuilder(Substitute.For<IAmazonDynamoDB>());
        builder.WithAttributes(new Dictionary<string, string>() { { "#pk", "pk" } });
        var req = builder.ToUpdateItemRequest();
        req.Should().NotBeNull();
        req.ExpressionAttributeNames.Should().NotBeNull();
        req.ExpressionAttributeNames.Should().HaveCount(1);
        req.ExpressionAttributeNames["#pk"].Should().Be("pk");
    }
    
    [Fact]
    public void UsingExpressionAttributeNamesUsingLambdaSuccess()
    {
        var builder = new UpdateItemRequestBuilder(Substitute.For<IAmazonDynamoDB>());
        builder.WithAttributes((attributes) => attributes.Add("#pk", "pk"));
        var req = builder.ToUpdateItemRequest();
        req.Should().NotBeNull();
        req.ExpressionAttributeNames.Should().NotBeNull();
        req.ExpressionAttributeNames.Should().HaveCount(1);
        req.ExpressionAttributeNames["#pk"].Should().Be("pk");
    }

    [Fact]
    public void UsingExpressionAttributeNameSuccess()
    {
        var builder = new UpdateItemRequestBuilder(Substitute.For<IAmazonDynamoDB>());
        builder.WithAttribute("#pk", "pk");
        var req = builder.ToUpdateItemRequest();
        req.Should().NotBeNull();
        req.ExpressionAttributeNames.Should().NotBeNull();
        req.ExpressionAttributeNames.Should().HaveCount(1);
        req.ExpressionAttributeNames["#pk"].Should().Be("pk");
    }
    [Fact]
    public void UsingExpressionAttributeValuesSuccess()
    {
        var builder = new UpdateItemRequestBuilder(Substitute.For<IAmazonDynamoDB>());
        builder.WithValues(new Dictionary<string, AttributeValue>() { { ":pk", new AttributeValue { S = "1" } } });
        var req = builder.ToUpdateItemRequest();
        req.Should().NotBeNull();
        req.ExpressionAttributeValues.Should().NotBeNull();
        req.ExpressionAttributeValues.Should().HaveCount(1);
        req.ExpressionAttributeValues[":pk"].S.Should().Be("1");

    }
    
    [Fact]
    public void UsingExpressionAttributeValuesLambdaSuccess()
    {
        var builder = new UpdateItemRequestBuilder(Substitute.For<IAmazonDynamoDB>());
        builder.WithValues((attributes) => attributes.Add(":pk", new AttributeValue { S = "1" }));
        var req = builder.ToUpdateItemRequest();
        req.Should().NotBeNull();
        req.ExpressionAttributeValues.Should().NotBeNull();
        req.ExpressionAttributeValues.Should().HaveCount(1);
        req.ExpressionAttributeValues[":pk"].S.Should().Be("1");

    }

    [Fact]
    public void UsingExpressionAttributeStringValueSuccess()
    {
        var builder = new UpdateItemRequestBuilder(Substitute.For<IAmazonDynamoDB>());
        builder.WithValue(":pk", "1");
        var req = builder.ToUpdateItemRequest();
        req.Should().NotBeNull();
        req.ExpressionAttributeValues.Should().NotBeNull();
        req.ExpressionAttributeValues.Should().HaveCount(1);
        req.ExpressionAttributeValues[":pk"].S.Should().Be("1");
    }
    
    [Fact]
    public void UsingExpressionAttributeBooleanValueSuccess()
    {
        var builder = new UpdateItemRequestBuilder(Substitute.For<IAmazonDynamoDB>());
        builder.WithValue(":pk", true);
        var req = builder.ToUpdateItemRequest();
        req.Should().NotBeNull();
        req.ExpressionAttributeValues.Should().NotBeNull();
        req.ExpressionAttributeValues.Should().HaveCount(1);
        req.ExpressionAttributeValues[":pk"].BOOL.Should().BeTrue();
    }

    #endregion Attributes
    
    [Fact]
    public void WhereSuccess()
    {
        var builder = new UpdateItemRequestBuilder(Substitute.For<IAmazonDynamoDB>());
        builder.Where("#pk = :pk");
        var req = builder.ToUpdateItemRequest();
        req.Should().NotBeNull();
        req.ConditionExpression.Should().Be("#pk = :pk");
    }
    
    [Fact]
    public void SetSuccess()
    {
        var builder = new UpdateItemRequestBuilder(Substitute.For<IAmazonDynamoDB>());
        builder.Set("SET #pk = :pk");
        var req = builder.ToUpdateItemRequest();
        req.Should().NotBeNull();
        req.UpdateExpression.Should().Be("SET #pk = :pk");
    }
    
    #region ConsumedCapacity
    
    [Fact]
    public void ReturnConsumedCapacitySuccess()
    {
        var builder = new UpdateItemRequestBuilder(Substitute.For<IAmazonDynamoDB>());
        builder.ReturnConsumedCapacity(ReturnConsumedCapacity.TOTAL);
        var req = builder.ToUpdateItemRequest();
        req.Should().NotBeNull();
        req.ReturnConsumedCapacity.Should().Be(ReturnConsumedCapacity.TOTAL);
    }
    
    [Fact]
    public void ReturnTotalConsumedCapacitySuccess()
    {
        var builder = new UpdateItemRequestBuilder(Substitute.For<IAmazonDynamoDB>());
        builder.ReturnTotalConsumedCapacity();
        var req = builder.ToUpdateItemRequest();
        req.Should().NotBeNull();
        req.ReturnConsumedCapacity.Should().Be(ReturnConsumedCapacity.TOTAL);
    }
    
    [Fact]
    public void ReturnItemCollectionMetricsSuccess()
    {
        var builder = new UpdateItemRequestBuilder(Substitute.For<IAmazonDynamoDB>());
        builder.ReturnItemCollectionMetrics();
        var req = builder.ToUpdateItemRequest();
        req.Should().NotBeNull();
        req.ReturnItemCollectionMetrics.Should().Be(ReturnItemCollectionMetrics.SIZE);
    }
    
    #endregion ConsumedCapacity
    
    #region ReturnValues
    
    [Fact]
    public void ReturnValuesNoneSuccess()
    {
        var builder = new UpdateItemRequestBuilder(Substitute.For<IAmazonDynamoDB>());
        builder.ReturnNone();
        var req = builder.ToUpdateItemRequest();
        req.Should().NotBeNull();
        req.ReturnValues.Should().Be(ReturnValue.NONE);
    }
    
    [Fact]
    public void ReturnAllNewValuesSuccess()
    {
        var builder = new UpdateItemRequestBuilder(Substitute.For<IAmazonDynamoDB>());
        builder.ReturnAllNewValues();
        var req = builder.ToUpdateItemRequest();
        req.Should().NotBeNull();
        req.ReturnValues.Should().Be(ReturnValue.ALL_NEW);
    }
    
    [Fact]
    public void ReturnAllOldValuesSuccess()
    {
        var builder = new UpdateItemRequestBuilder(Substitute.For<IAmazonDynamoDB>());
        builder.ReturnAllOldValues();
        var req = builder.ToUpdateItemRequest();
        req.Should().NotBeNull();
        req.ReturnValues.Should().Be(ReturnValue.ALL_OLD);
    }
    
    [Fact]
    public void ReturnUpdatedNewValuesSuccess()
    {
        var builder = new UpdateItemRequestBuilder(Substitute.For<IAmazonDynamoDB>());
        builder.ReturnUpdatedNewValues();
        var req = builder.ToUpdateItemRequest();
        req.Should().NotBeNull();
        req.ReturnValues.Should().Be(ReturnValue.UPDATED_NEW);
    }
    
    [Fact]
    public void ReturnUpdatedOldValuesSuccess()
    {
        var builder = new UpdateItemRequestBuilder(Substitute.For<IAmazonDynamoDB>());
        builder.ReturnUpdatedOldValues();
        var req = builder.ToUpdateItemRequest();
        req.Should().NotBeNull();
        req.ReturnValues.Should().Be(ReturnValue.UPDATED_OLD);
    }
    
    [Fact]
    public void ReturnOldValuesOnConditionCheckFailureSuccess()
    {
        var builder = new UpdateItemRequestBuilder(Substitute.For<IAmazonDynamoDB>());
        builder.ReturnOldValuesOnConditionCheckFailure();
        var req = builder.ToUpdateItemRequest();
        req.Should().NotBeNull();
        req.ReturnValuesOnConditionCheckFailure.Should().Be(ReturnValuesOnConditionCheckFailure.ALL_OLD);
    }
    
    [Fact]
    public void ReturnNoValuesOnConditionCheckFailureSuccess()
    {
        var builder = new UpdateItemRequestBuilder(Substitute.For<IAmazonDynamoDB>());
        var req = builder.ToUpdateItemRequest();
        req.Should().NotBeNull();
        req.ReturnValuesOnConditionCheckFailure.Should().BeNull();
    }
    
    #endregion ReturnValues

}