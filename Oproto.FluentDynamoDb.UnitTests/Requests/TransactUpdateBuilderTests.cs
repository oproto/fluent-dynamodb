using Amazon.DynamoDBv2.DocumentModel;
using Amazon.DynamoDBv2.Model;
using AwesomeAssertions;
using Oproto.FluentDynamoDb.Requests;
using Oproto.FluentDynamoDb.Requests.Extensions;

namespace Oproto.FluentDynamoDb.UnitTests.Requests;

public class TransactUpdateBuilderTests
{
    [Fact]
    public void ForTableSuccess()
    {
        var builder = new TransactUpdateBuilder("TestTable");
        var req = builder.ToWriteItem();
        req.Should().NotBeNull();
        req.Update.Should().NotBeNull();
        req.Update.TableName.Should().Be("TestTable");
    }

    #region Keys

    [Fact]
    public void WithKeyPkStringValueSuccess()
    {
        var builder = new TransactUpdateBuilder("TestTable");
        builder.WithKey("pk", "1");
        var req = builder.ToWriteItem();
        req.Should().NotBeNull();
        req.Update.Should().NotBeNull();
        req.Update.Key.Should().NotBeNull();
        req.Update.Key.Should().ContainKey("pk");
        req.Update.Key.Keys.Should().HaveCount(1);
        req.Update.Key["pk"].S.Should().Be("1");
    }

    [Fact]
    public void WithKeyPkSkStringValueSuccess()
    {
        var builder = new TransactUpdateBuilder("TestTable");
        builder.WithKey("pk", "1", "sk", "abcd");
        var req = builder.ToWriteItem();
        req.Should().NotBeNull();
        req.Update.Should().NotBeNull();
        req.Update.Key.Should().NotBeNull();
        req.Update.Key.Should().ContainKey("pk");
        req.Update.Key.Should().ContainKey("sk");
        req.Update.Key.Keys.Should().HaveCount(2);
        req.Update.Key["pk"].S.Should().Be("1");
        req.Update.Key["sk"].S.Should().Be("abcd");
    }

    [Fact]
    public void WithKeyPkSkAttributeValueSuccess()
    {
        var builder = new TransactUpdateBuilder("TestTable");
        builder.WithKey("pk", new AttributeValue() { S = "1" }, "sk", new AttributeValue() { S = "abcd" });
        var req = builder.ToWriteItem();
        req.Should().NotBeNull();
        req.Update.Should().NotBeNull();
        req.Update.Key.Should().NotBeNull();
        req.Update.Key.Should().ContainKey("pk");
        req.Update.Key.Should().ContainKey("sk");
        req.Update.Key.Keys.Should().HaveCount(2);
        req.Update.Key["pk"].S.Should().Be("1");
        req.Update.Key["sk"].S.Should().Be("abcd");
    }

    #endregion Keys

    #region Attributes

    [Fact]
    public void UsingExpressionAttributeNamesSuccess()
    {
        var builder = new TransactUpdateBuilder("TestTable");
        builder.WithAttributes(new Dictionary<string, string>() { { "#pk", "pk" } });
        var req = builder.ToWriteItem();
        req.Should().NotBeNull();
        req.Update.Should().NotBeNull();
        req.Update.ExpressionAttributeNames.Should().NotBeNull();
        req.Update.ExpressionAttributeNames.Should().HaveCount(1);
        req.Update.ExpressionAttributeNames["#pk"].Should().Be("pk");
    }

    [Fact]
    public void UsingExpressionAttributeNamesUsingLambdaSuccess()
    {
        var builder = new TransactUpdateBuilder("TestTable");
        builder.WithAttributes((attributes) => attributes.Add("#pk", "pk"));
        var req = builder.ToWriteItem();
        req.Should().NotBeNull();
        req.Update.Should().NotBeNull();
        req.Update.ExpressionAttributeNames.Should().NotBeNull();
        req.Update.ExpressionAttributeNames.Should().HaveCount(1);
        req.Update.ExpressionAttributeNames["#pk"].Should().Be("pk");
    }

    [Fact]
    public void UsingExpressionAttributeNameSuccess()
    {
        var builder = new TransactUpdateBuilder("TestTable");
        builder.WithAttribute("#pk", "pk");
        var req = builder.ToWriteItem();
        req.Should().NotBeNull();
        req.Update.Should().NotBeNull();
        req.Update.ExpressionAttributeNames.Should().NotBeNull();
        req.Update.ExpressionAttributeNames.Should().HaveCount(1);
        req.Update.ExpressionAttributeNames["#pk"].Should().Be("pk");
    }

    [Fact]
    public void UsingExpressionAttributeValuesSuccess()
    {
        var builder = new TransactUpdateBuilder("TestTable");
        builder.WithValues(new Dictionary<string, AttributeValue>() { { ":pk", new AttributeValue { S = "1" } } });
        var req = builder.ToWriteItem();
        req.Should().NotBeNull();
        req.Update.Should().NotBeNull();
        req.Update.ExpressionAttributeValues.Should().NotBeNull();
        req.Update.ExpressionAttributeValues.Should().HaveCount(1);
        req.Update.ExpressionAttributeValues[":pk"].S.Should().Be("1");

    }

    [Fact]
    public void UsingExpressionAttributeValuesLambdaSuccess()
    {
        var builder = new TransactUpdateBuilder("TestTable");
        builder.WithValues((attributes) => attributes.Add(":pk", new AttributeValue { S = "1" }));
        var req = builder.ToWriteItem();
        req.Should().NotBeNull();
        req.Update.Should().NotBeNull();
        req.Update.ExpressionAttributeValues.Should().NotBeNull();
        req.Update.ExpressionAttributeValues.Should().HaveCount(1);
        req.Update.ExpressionAttributeValues[":pk"].S.Should().Be("1");

    }

    [Fact]
    public void UsingExpressionAttributeStringValueSuccess()
    {
        var builder = new TransactUpdateBuilder("TestTable");
        builder.WithValue(":pk", "1");
        var req = builder.ToWriteItem();
        req.Should().NotBeNull();
        req.Update.Should().NotBeNull();
        req.Update.ExpressionAttributeValues.Should().NotBeNull();
        req.Update.ExpressionAttributeValues.Should().HaveCount(1);
        req.Update.ExpressionAttributeValues[":pk"].S.Should().Be("1");
    }

    [Fact]
    public void UsingExpressionAttributeBooleanValueSuccess()
    {
        var builder = new TransactUpdateBuilder("TestTable");
        builder.WithValue(":pk", true);
        var req = builder.ToWriteItem();
        req.Should().NotBeNull();
        req.Update.Should().NotBeNull();
        req.Update.ExpressionAttributeValues.Should().NotBeNull();
        req.Update.ExpressionAttributeValues.Should().HaveCount(1);
        req.Update.ExpressionAttributeValues[":pk"].BOOL.Should().BeTrue();
    }

    #endregion Attributes

    [Fact]
    public void SetSuccess()
    {
        var builder = new TransactUpdateBuilder("TestTable");
        builder.Set("SET #pk = :pk");
        var req = builder.ToWriteItem();
        req.Should().NotBeNull();
        req.Update.Should().NotBeNull();
        req.Update.UpdateExpression.Should().Be("SET #pk = :pk");
    }

    [Fact]
    public void WhereSuccess()
    {
        var builder = new TransactUpdateBuilder("TestTable");
        builder.Where("#pk = :pk");
        var req = builder.ToWriteItem();
        req.Should().NotBeNull();
        req.Update.Should().NotBeNull();
        req.Update.ConditionExpression.Should().Be("#pk = :pk");
    }

    [Fact]
    public void ReturnOldValuesOnConditionCheckFailureSuccess()
    {
        var builder = new TransactUpdateBuilder("TestTable");
        builder.ReturnOldValuesOnConditionCheckFailure();
        var req = builder.ToWriteItem();
        req.Should().NotBeNull();
        req.Update.Should().NotBeNull();
        req.Update.ReturnValuesOnConditionCheckFailure.Should().Be(Amazon.DynamoDBv2.ReturnValuesOnConditionCheckFailure.ALL_OLD);
    }

    [Fact]
    public void ReturnNoValuesOnConditionCheckFailureSuccess()
    {
        var builder = new TransactUpdateBuilder("TestTable");
        var req = builder.ToWriteItem();
        req.Should().NotBeNull();
        req.Update.Should().NotBeNull();
        req.Update.ReturnValuesOnConditionCheckFailure.Should().BeNull();
    }
}