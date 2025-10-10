using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using FluentAssertions;
using Oproto.FluentDynamoDb.Requests;
using Oproto.FluentDynamoDb.Requests.Extensions;

namespace Oproto.FluentDynamoDb.UnitTests.Requests;

public class TransactPutBuilderTests
{
    [Fact]
    public void ForTableSuccess()
    {
        var builder = new TransactPutBuilder("TestTable");
        var req = builder.ToWriteItem();
        req.Should().NotBeNull();
        req.Put.Should().NotBeNull();
        req.Put.TableName.Should().Be("TestTable");
    }
    
    #region Attributes
    
    [Fact]
    public void UsingExpressionAttributeNamesSuccess()
    {
        var builder = new TransactPutBuilder("TestTable");
        builder.WithAttributes(new Dictionary<string, string>() { { "#pk", "pk" } });
        var req = builder.ToWriteItem();
        req.Should().NotBeNull();
        req.Put.Should().NotBeNull();
        req.Put.ExpressionAttributeNames.Should().NotBeNull();
        req.Put.ExpressionAttributeNames.Should().HaveCount(1);
        req.Put.ExpressionAttributeNames["#pk"].Should().Be("pk");
    }
    
    [Fact]
    public void UsingExpressionAttributeNamesUsingLambdaSuccess()
    {
        var builder = new TransactPutBuilder("TestTable");
        builder.WithAttributes((attributes) => attributes.Add("#pk", "pk"));
        var req = builder.ToWriteItem();
        req.Should().NotBeNull();
        req.Put.Should().NotBeNull();
        req.Put.ExpressionAttributeNames.Should().NotBeNull();
        req.Put.ExpressionAttributeNames.Should().HaveCount(1);
        req.Put.ExpressionAttributeNames["#pk"].Should().Be("pk");
    }

    [Fact]
    public void UsingExpressionAttributeNameSuccess()
    {
        var builder = new TransactPutBuilder("TestTable");
        builder.WithAttribute("#pk", "pk");
        var req = builder.ToWriteItem();
        req.Should().NotBeNull();
        req.Put.Should().NotBeNull();
        req.Put.ExpressionAttributeNames.Should().NotBeNull();
        req.Put.ExpressionAttributeNames.Should().HaveCount(1);
        req.Put.ExpressionAttributeNames["#pk"].Should().Be("pk");
    }
    [Fact]
    public void UsingExpressionAttributeValuesSuccess()
    {
        var builder = new TransactPutBuilder("TestTable");
        builder.WithValues(new Dictionary<string, AttributeValue>() { { ":pk", new AttributeValue { S = "1" } } });
        var req = builder.ToWriteItem();
        req.Should().NotBeNull();
        req.Put.Should().NotBeNull();
        req.Put.ExpressionAttributeValues.Should().NotBeNull();
        req.Put.ExpressionAttributeValues.Should().HaveCount(1);
        req.Put.ExpressionAttributeValues[":pk"].S.Should().Be("1");

    }
    
    [Fact]
    public void UsingExpressionAttributeValuesLambdaSuccess()
    {
        var builder = new TransactPutBuilder("TestTable");
        builder.WithValues((attributes) => attributes.Add(":pk", new AttributeValue { S = "1" }));
        var req = builder.ToWriteItem();
        req.Should().NotBeNull();
        req.Put.Should().NotBeNull();
        req.Put.ExpressionAttributeValues.Should().NotBeNull();
        req.Put.ExpressionAttributeValues.Should().HaveCount(1);
        req.Put.ExpressionAttributeValues[":pk"].S.Should().Be("1");

    }

    [Fact]
    public void UsingExpressionAttributeStringValueSuccess()
    {
        var builder = new TransactPutBuilder("TestTable");
        builder.WithValue(":pk", "1");
        var req = builder.ToWriteItem();
        req.Should().NotBeNull();
        req.Put.Should().NotBeNull();
        req.Put.ExpressionAttributeValues.Should().NotBeNull();
        req.Put.ExpressionAttributeValues.Should().HaveCount(1);
        req.Put.ExpressionAttributeValues[":pk"].S.Should().Be("1");
    }
    
    [Fact]
    public void UsingExpressionAttributeBooleanValueSuccess()
    {
        var builder = new TransactPutBuilder("TestTable");
        builder.WithValue(":pk", true);
        var req = builder.ToWriteItem();
        req.Should().NotBeNull();
        req.Put.Should().NotBeNull();
        req.Put.ExpressionAttributeValues.Should().NotBeNull();
        req.Put.ExpressionAttributeValues.Should().HaveCount(1);
        req.Put.ExpressionAttributeValues[":pk"].BOOL.Should().BeTrue();
    }

    #endregion Attributes
    
    [Fact]
    public void WithItemSuccess()
    {
        var builder = new TransactPutBuilder("TestTable");
        builder.WithItem(new Dictionary<string, AttributeValue>() { { "pk", new AttributeValue() { S = "1" } } });
        var req = builder.ToWriteItem();
        req.Should().NotBeNull();
        req.Put.Should().NotBeNull();
        req.Put.Item.Should().NotBeNull();
        req.Put.Item.Should().HaveCount(1);
        req.Put.Item["pk"].S.Should().Be("1");
    }
    
    [Fact]
    public void WithItemLambdaSuccess()
    {
        var builder = new TransactPutBuilder("TestTable");
        builder.WithItem( new { Pk = "1"}, (item) =>
        {
            return new Dictionary<string, AttributeValue>() { { "pk", new AttributeValue() { S = item.Pk } } };
        });
        var req = builder.ToWriteItem();
        req.Should().NotBeNull();
        req.Put.Should().NotBeNull();
        req.Put.Item.Should().NotBeNull();
        req.Put.Item.Should().HaveCount(1);
        req.Put.Item["pk"].S.Should().Be("1");
    }
    
    [Fact]
    public void WhereSuccess()
    {
        var builder = new TransactPutBuilder("TestTable");
        builder.Where("#pk = :pk");
        var req = builder.ToWriteItem();
        req.Should().NotBeNull();
        req.Put.Should().NotBeNull();
        req.Put.ConditionExpression.Should().Be("#pk = :pk");
    }
    
    [Fact]
    public void ReturnOldValuesOnConditionCheckFailureSuccess()
    {
        var builder = new TransactPutBuilder("TestTable");
        builder.ReturnOldValuesOnConditionCheckFailure();
        var req = builder.ToWriteItem();
        req.Should().NotBeNull();
        req.Put.Should().NotBeNull();
        req.Put.ReturnValuesOnConditionCheckFailure.Should().Be(ReturnValuesOnConditionCheckFailure.ALL_OLD);
    }
    
    [Fact]
    public void ReturnNoValuesOnConditionCheckFailureSuccess()
    {
        var builder = new TransactPutBuilder("TestTable");
        var req = builder.ToWriteItem();
        req.Should().NotBeNull();
        req.Put.Should().NotBeNull();
        req.Put.ReturnValuesOnConditionCheckFailure.Should().BeNull();
    }
}