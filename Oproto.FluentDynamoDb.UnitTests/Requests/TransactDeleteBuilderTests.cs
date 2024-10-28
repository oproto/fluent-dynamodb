using Amazon.DynamoDBv2.Model;
using FluentAssertions;
using Oproto.FluentDynamoDb.Requests;

namespace Oproto.FluentDynamoDb.UnitTests.Requests;

public class TransactDeleteBuilderTests
{
     [Fact]
    public void ForTableSuccess()
    {
        var builder = new TransactDeleteBuilder("TestTable");
        var req = builder.ToWriteItem();
        req.Should().NotBeNull();
        req.Delete.Should().NotBeNull();
        req.Delete.TableName.Should().Be("TestTable");
    }
    
    #region Keys
    
    [Fact]
    public void WithKeyPkStringValueSuccess()
    {
        var builder = new TransactDeleteBuilder("TestTable");
        builder.WithKey("pk", "1");
        var req = builder.ToWriteItem();
        req.Should().NotBeNull();
        req.Delete.Should().NotBeNull();
        req.Delete.Key.Should().NotBeNull();
        req.Delete.Key.Should().ContainKey("pk");
        req.Delete.Key.Keys.Should().HaveCount(1);
        req.Delete.Key["pk"].S.Should().Be("1");
    }
    
    [Fact]
    public void WithKeyPkSkStringValueSuccess()
    {
        var builder = new TransactDeleteBuilder("TestTable");
        builder.WithKey("pk", "1", "sk", "abcd");
        var req = builder.ToWriteItem();
        req.Should().NotBeNull();
        req.Delete.Should().NotBeNull();
        req.Delete.Key.Should().NotBeNull();
        req.Delete.Key.Should().ContainKey("pk");
        req.Delete.Key.Should().ContainKey("sk");
        req.Delete.Key.Keys.Should().HaveCount(2);
        req.Delete.Key["pk"].S.Should().Be("1");
        req.Delete.Key["sk"].S.Should().Be("abcd");
    }
    
    [Fact]
    public void WithKeyPkSkAttributeValueSuccess()
    {
        var builder = new TransactDeleteBuilder("TestTable");
        builder.WithKey("pk", new AttributeValue() { S = "1" }, "sk", new AttributeValue() { S = "abcd" });
        var req = builder.ToWriteItem();
        req.Should().NotBeNull();
        req.Delete.Should().NotBeNull();
        req.Delete.Key.Should().NotBeNull();
        req.Delete.Key.Should().ContainKey("pk");
        req.Delete.Key.Should().ContainKey("sk");
        req.Delete.Key.Keys.Should().HaveCount(2);
        req.Delete.Key["pk"].S.Should().Be("1");
        req.Delete.Key["sk"].S.Should().Be("abcd");
    }
    
    #endregion Keys
    
    #region Attributes
    
    [Fact]
    public void UsingExpressionAttributeNamesSuccess()
    {
        var builder = new TransactDeleteBuilder("TestTable");
        builder.UsingExpressionAttributeNames(new Dictionary<string, string>() { { "#pk", "pk" } });
        var req = builder.ToWriteItem();
        req.Should().NotBeNull();
        req.Delete.Should().NotBeNull();
        req.Delete.ExpressionAttributeNames.Should().NotBeNull();
        req.Delete.ExpressionAttributeNames.Should().HaveCount(1);
        req.Delete.ExpressionAttributeNames["#pk"].Should().Be("pk");
    }
    
    [Fact]
    public void UsingExpressionAttributeNamesUsingLambdaSuccess()
    {
        var builder = new TransactDeleteBuilder("TestTable");
        builder.UsingExpressionAttributeNames((attributes) => attributes.Add("#pk", "pk"));
        var req = builder.ToWriteItem();
        req.Should().NotBeNull();
        req.Delete.Should().NotBeNull();
        req.Delete.ExpressionAttributeNames.Should().NotBeNull();
        req.Delete.ExpressionAttributeNames.Should().HaveCount(1);
        req.Delete.ExpressionAttributeNames["#pk"].Should().Be("pk");
    }

    [Fact]
    public void UsingExpressionAttributeValuesSuccess()
    {
        var builder = new TransactDeleteBuilder("TestTable");
        builder.WithValues(new Dictionary<string, AttributeValue>() { { ":pk", new AttributeValue { S = "1" } } });
        var req = builder.ToWriteItem();
        req.Should().NotBeNull();
        req.Delete.Should().NotBeNull();
        req.Delete.ExpressionAttributeValues.Should().NotBeNull();
        req.Delete.ExpressionAttributeValues.Should().HaveCount(1);
        req.Delete.ExpressionAttributeValues[":pk"].S.Should().Be("1");

    }
    
    [Fact]
    public void UsingExpressionAttributeValuesLambdaSuccess()
    {
        var builder = new TransactDeleteBuilder("TestTable");
        builder.WithValues((attributes) => attributes.Add(":pk", new AttributeValue { S = "1" }));
        var req = builder.ToWriteItem();
        req.Should().NotBeNull();
        req.Delete.Should().NotBeNull();
        req.Delete.ExpressionAttributeValues.Should().NotBeNull();
        req.Delete.ExpressionAttributeValues.Should().HaveCount(1);
        req.Delete.ExpressionAttributeValues[":pk"].S.Should().Be("1");

    }

    [Fact]
    public void UsingExpressionAttributeStringValueSuccess()
    {
        var builder = new TransactDeleteBuilder("TestTable");
        builder.WithValue(":pk", "1");
        var req = builder.ToWriteItem();
        req.Should().NotBeNull();
        req.Delete.Should().NotBeNull();
        req.Delete.ExpressionAttributeValues.Should().NotBeNull();
        req.Delete.ExpressionAttributeValues.Should().HaveCount(1);
        req.Delete.ExpressionAttributeValues[":pk"].S.Should().Be("1");
    }
    
    [Fact]
    public void UsingExpressionAttributeBooleanValueSuccess()
    {
        var builder = new TransactDeleteBuilder("TestTable");
        builder.WithValue(":pk", true);
        var req = builder.ToWriteItem();
        req.Should().NotBeNull();
        req.Delete.Should().NotBeNull();
        req.Delete.ExpressionAttributeValues.Should().NotBeNull();
        req.Delete.ExpressionAttributeValues.Should().HaveCount(1);
        req.Delete.ExpressionAttributeValues[":pk"].BOOL.Should().BeTrue();
    }

    #endregion Attributes
    
    [Fact]
    public void WhereSuccess()
    {
        var builder = new TransactDeleteBuilder("TestTable");
        builder.Where("#pk = :pk");
        var req = builder.ToWriteItem();
        req.Should().NotBeNull();
        req.Delete.Should().NotBeNull();
        req.Delete.ConditionExpression.Should().Be("#pk = :pk");
    }
    
    [Fact]
    public void ReturnOldValuesOnConditionCheckFailureSuccess()
    {
        var builder = new TransactDeleteBuilder("TestTable");
        builder.ReturnOldValuesOnConditionCheckFailure();
        var req = builder.ToWriteItem();
        req.Should().NotBeNull();
        req.Delete.Should().NotBeNull();
        req.Delete.ReturnValuesOnConditionCheckFailure.Should().Be(Amazon.DynamoDBv2.ReturnValuesOnConditionCheckFailure.ALL_OLD);
    }
    
    [Fact]
    public void ReturnNoValuesOnConditionCheckFailureSuccess()
    {
        var builder = new TransactDeleteBuilder("TestTable");
        var req = builder.ToWriteItem();
        req.Should().NotBeNull();
        req.Delete.Should().NotBeNull();
        req.Delete.ReturnValuesOnConditionCheckFailure.Should().BeNull();
    }
}