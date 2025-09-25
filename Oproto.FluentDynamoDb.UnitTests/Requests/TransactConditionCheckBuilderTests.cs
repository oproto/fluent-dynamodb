using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using FluentAssertions;
using NSubstitute;
using Oproto.FluentDynamoDb.Requests;

namespace Oproto.FluentDynamoDb.UnitTests.Requests;

public class TransactConditionCheckBuilderTests
{
    [Fact]
    public void ConstructorSuccess()
    {
        var builder = new TransactConditionCheckBuilder("TestTable");
        var req = builder.ToWriteItem();
        req.Should().NotBeNull();
        req.ConditionCheck.Should().NotBeNull();
        req.ConditionCheck.TableName.Should().Be("TestTable");
    }
    
    #region Keys
    
    [Fact]
    public void WithKeyPkStringValueSuccess()
    {
        var builder = new TransactConditionCheckBuilder("TestTable");
        builder.WithKey("pk", "1");
        var req = builder.ToWriteItem();
        req.Should().NotBeNull();
        req.ConditionCheck.Should().NotBeNull();
        req.ConditionCheck.Key.Should().NotBeNull();
        req.ConditionCheck.Key.Should().ContainKey("pk");
        req.ConditionCheck.Key.Keys.Should().HaveCount(1);
        req.ConditionCheck.Key["pk"].S.Should().Be("1");
    }
    
    [Fact]
    public void WithKeyPkSkStringValueSuccess()
    {
        var builder = new TransactConditionCheckBuilder("TestTable");
        builder.WithKey("pk", "1", "sk", "abcd");
        var req = builder.ToWriteItem();
        req.Should().NotBeNull();
        req.ConditionCheck.Should().NotBeNull();
        req.ConditionCheck.Key.Should().NotBeNull();
        req.ConditionCheck.Key.Should().ContainKey("pk");
        req.ConditionCheck.Key.Should().ContainKey("sk");
        req.ConditionCheck.Key.Keys.Should().HaveCount(2);
        req.ConditionCheck.Key["pk"].S.Should().Be("1");
        req.ConditionCheck.Key["sk"].S.Should().Be("abcd");
    }
    
    [Fact]
    public void WithKeyPkSkAttributeValueSuccess()
    {
        var builder = new TransactConditionCheckBuilder("TestTable");
        builder.WithKey("pk", new AttributeValue() { S = "1" }, "sk", new AttributeValue() { S = "abcd" });
        var req = builder.ToWriteItem();
        req.Should().NotBeNull();
        req.ConditionCheck.Should().NotBeNull();
        req.ConditionCheck.Key.Should().NotBeNull();
        req.ConditionCheck.Key.Should().ContainKey("pk");
        req.ConditionCheck.Key.Should().ContainKey("sk");
        req.ConditionCheck.Key.Keys.Should().HaveCount(2);
        req.ConditionCheck.Key["pk"].S.Should().Be("1");
        req.ConditionCheck.Key["sk"].S.Should().Be("abcd");
    }
    
    #endregion Keys
    
    #region Attributes
    
    [Fact]
    public void UsingExpressionAttributeNamesSuccess()
    {
        var builder = new TransactConditionCheckBuilder("TestTable");
        builder.WithAttributes(new Dictionary<string, string>() { { "#pk", "pk" } });
        var req = builder.ToWriteItem();
        req.Should().NotBeNull();
        req.ConditionCheck.Should().NotBeNull();
        req.ConditionCheck.ExpressionAttributeNames.Should().NotBeNull();
        req.ConditionCheck.ExpressionAttributeNames.Should().HaveCount(1);
        req.ConditionCheck.ExpressionAttributeNames["#pk"].Should().Be("pk");
    }
    
    [Fact]
    public void UsingExpressionAttributeNamesUsingLambdaSuccess()
    {
        var builder = new TransactConditionCheckBuilder("TestTable");
        builder.WithAttributes((attributes) => attributes.Add("#pk", "pk"));
        var req = builder.ToWriteItem();
        req.Should().NotBeNull();
        req.ConditionCheck.Should().NotBeNull();
        req.ConditionCheck.ExpressionAttributeNames.Should().NotBeNull();
        req.ConditionCheck.ExpressionAttributeNames.Should().HaveCount(1);
        req.ConditionCheck.ExpressionAttributeNames["#pk"].Should().Be("pk");
    }
    
    [Fact]
    public void UsingExpressionAttributeNameSuccess()
    {
        var builder = new TransactConditionCheckBuilder("TestTable");
        builder.WithAttribute("#pk", "pk");
        var req = builder.ToWriteItem();
        req.Should().NotBeNull();
        req.ConditionCheck.Should().NotBeNull();
        req.ConditionCheck.ExpressionAttributeNames.Should().NotBeNull();
        req.ConditionCheck.ExpressionAttributeNames.Should().HaveCount(1);
        req.ConditionCheck.ExpressionAttributeNames["#pk"].Should().Be("pk");
    }

    [Fact]
    public void UsingExpressionAttributeValuesSuccess()
    {
        var builder = new TransactConditionCheckBuilder("TestTable");
        builder.WithValues(new Dictionary<string, AttributeValue>() { { ":pk", new AttributeValue { S = "1" } } });
        var req = builder.ToWriteItem();
        req.Should().NotBeNull();
        req.ConditionCheck.Should().NotBeNull();
        req.ConditionCheck.ExpressionAttributeValues.Should().NotBeNull();
        req.ConditionCheck.ExpressionAttributeValues.Should().HaveCount(1);
        req.ConditionCheck.ExpressionAttributeValues[":pk"].S.Should().Be("1");

    }
    
    [Fact]
    public void UsingExpressionAttributeValuesLambdaSuccess()
    {
        var builder = new TransactConditionCheckBuilder("TestTable");
        builder.WithValues((attributes) => attributes.Add(":pk", new AttributeValue { S = "1" }));
        var req = builder.ToWriteItem();
        req.Should().NotBeNull();
        req.ConditionCheck.Should().NotBeNull();
        req.ConditionCheck.ExpressionAttributeValues.Should().NotBeNull();
        req.ConditionCheck.ExpressionAttributeValues.Should().HaveCount(1);
        req.ConditionCheck.ExpressionAttributeValues[":pk"].S.Should().Be("1");

    }

    [Fact]
    public void UsingExpressionAttributeStringValueSuccess()
    {
        var builder = new TransactConditionCheckBuilder("TestTable");
        builder.WithValue(":pk", "1");
        var req = builder.ToWriteItem();
        req.Should().NotBeNull();
        req.ConditionCheck.Should().NotBeNull();
        req.ConditionCheck.ExpressionAttributeValues.Should().NotBeNull();
        req.ConditionCheck.ExpressionAttributeValues.Should().HaveCount(1);
        req.ConditionCheck.ExpressionAttributeValues[":pk"].S.Should().Be("1");
    }
    
    [Fact]
    public void UsingExpressionAttributeBooleanValueSuccess()
    {
        var builder = new TransactConditionCheckBuilder("TestTable");
        builder.WithValue(":pk", true);
        var req = builder.ToWriteItem();
        req.Should().NotBeNull();
        req.ConditionCheck.Should().NotBeNull();
        req.ConditionCheck.ExpressionAttributeValues.Should().NotBeNull();
        req.ConditionCheck.ExpressionAttributeValues.Should().HaveCount(1);
        req.ConditionCheck.ExpressionAttributeValues[":pk"].BOOL.Should().BeTrue();
    }

    #endregion Attributes
    
    [Fact]
    public void WhereSuccess()
    {
        var builder = new TransactConditionCheckBuilder("TestTable");
        builder.Where("#pk = :pk");
        var req = builder.ToWriteItem();
        req.Should().NotBeNull();
        req.ConditionCheck.Should().NotBeNull();
        req.ConditionCheck.ConditionExpression.Should().Be("#pk = :pk");
    }
    
    [Fact]
    public void ReturnOldValuesOnConditionCheckFailureSuccess()
    {
        var builder = new TransactConditionCheckBuilder("TestTable");
        builder.ReturnOldValuesOnConditionCheckFailure();
        var req = builder.ToWriteItem();
        req.Should().NotBeNull();
        req.ConditionCheck.Should().NotBeNull();
        req.ConditionCheck.ReturnValuesOnConditionCheckFailure.Should().Be(ReturnValuesOnConditionCheckFailure.ALL_OLD);
    }
    
    [Fact]
    public void ReturnNoValuesOnConditionCheckFailureSuccess()
    {
        var builder = new TransactConditionCheckBuilder("TestTable");
        var req = builder.ToWriteItem();
        req.Should().NotBeNull();
        req.ConditionCheck.Should().NotBeNull();
        req.ConditionCheck.ReturnValuesOnConditionCheckFailure.Should().BeNull();
    }
}