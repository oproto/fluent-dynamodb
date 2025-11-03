using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using AwesomeAssertions;
using NSubstitute;
using Oproto.FluentDynamoDb.Requests;
using Oproto.FluentDynamoDb.Requests.Extensions;

namespace Oproto.FluentDynamoDb.UnitTests.Requests;

public class TransactGetItemBuilderTests
{
    [Fact]
    public void ForTableSuccess()
    {
        var builder = new TransactGetItemBuilder("TestTable");
        var req = builder.ToGetItem();
        req.Should().NotBeNull();
        req.Get.TableName.Should().Be("TestTable");
    }

    [Fact]
    public void WithKeyPkStringValueSuccess()
    {
        var builder = new TransactGetItemBuilder("TestTable");
        builder.WithKey("pk", "1");
        var req = builder.ToGetItem();
        req.Should().NotBeNull();
        req.Get.Key.Should().NotBeNull();
        req.Get.Key.Should().ContainKey("pk");
        req.Get.Key.Keys.Should().HaveCount(1);
        req.Get.Key["pk"].S.Should().Be("1");
    }

    [Fact]
    public void WithKeyPkSkStringValueSuccess()
    {
        var builder = new TransactGetItemBuilder("TestTable");
        builder.WithKey("pk", "1", "sk", "abcd");
        var req = builder.ToGetItem();
        req.Should().NotBeNull();
        req.Get.Key.Should().NotBeNull();
        req.Get.Key.Should().ContainKey("pk");
        req.Get.Key.Should().ContainKey("sk");
        req.Get.Key.Keys.Should().HaveCount(2);
        req.Get.Key["pk"].S.Should().Be("1");
        req.Get.Key["sk"].S.Should().Be("abcd");
    }

    [Fact]
    public void WithKeyPkSkAttributeValueSuccess()
    {
        var builder = new TransactGetItemBuilder("TestTable");
        builder.WithKey("pk", new AttributeValue() { S = "1" }, "sk", new AttributeValue() { S = "abcd" });
        var req = builder.ToGetItem();
        req.Should().NotBeNull();
        req.Get.Key.Should().NotBeNull();
        req.Get.Key.Should().ContainKey("pk");
        req.Get.Key.Should().ContainKey("sk");
        req.Get.Key.Keys.Should().HaveCount(2);
        req.Get.Key["pk"].S.Should().Be("1");
        req.Get.Key["sk"].S.Should().Be("abcd");
    }

    [Fact]
    public void UsingExpressionAttributeNamesSuccess()
    {
        var builder = new TransactGetItemBuilder("TestTable");
        builder.WithAttributes(new Dictionary<string, string>() { { "#pk", "pk" } });
        var req = builder.ToGetItem();
        req.Should().NotBeNull();
        req.Get.ExpressionAttributeNames.Should().NotBeNull();
        req.Get.ExpressionAttributeNames.Should().HaveCount(1);
        req.Get.ExpressionAttributeNames["#pk"].Should().Be("pk");
    }

    [Fact]
    public void UsingExpressionAttributeNamesUsingLambdaSuccess()
    {
        var builder = new TransactGetItemBuilder("TestTable");
        builder.WithAttributes((attributes) => attributes.Add("#pk", "pk"));
        var req = builder.ToGetItem();
        req.Should().NotBeNull();
        req.Get.ExpressionAttributeNames.Should().NotBeNull();
        req.Get.ExpressionAttributeNames.Should().HaveCount(1);
        req.Get.ExpressionAttributeNames["#pk"].Should().Be("pk");
    }

    [Fact]
    public void UsingExpressionAttributeNameSuccess()
    {
        var builder = new TransactGetItemBuilder("TestTable");
        builder.WithAttribute("#pk", "pk");
        var req = builder.ToGetItem();
        req.Should().NotBeNull();
        req.Get.ExpressionAttributeNames.Should().NotBeNull();
        req.Get.ExpressionAttributeNames.Should().HaveCount(1);
        req.Get.ExpressionAttributeNames["#pk"].Should().Be("pk");
    }

    [Fact]
    public void ProjectionExpressionSuccess()
    {
        var builder = new TransactGetItemBuilder("TestTable");
        builder.WithProjection("description, price");
        var req = builder.ToGetItem();
        req.Should().NotBeNull();
        req.Get.ProjectionExpression.Should().Be("description, price");
    }
}