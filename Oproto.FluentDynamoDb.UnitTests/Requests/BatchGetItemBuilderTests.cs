using Amazon.DynamoDBv2.Model;
using FluentAssertions;
using Oproto.FluentDynamoDb.Requests;
using Oproto.FluentDynamoDb.Requests.Extensions;

namespace Oproto.FluentDynamoDb.UnitTests.Requests;

public class BatchGetItemBuilderTests
{
    [Fact]
    public void ConstructorSuccess()
    {
        var builder = new BatchGetItemBuilder("TestTable");
        var keysAndAttributes = builder.ToKeysAndAttributes();
        
        keysAndAttributes.Should().NotBeNull();
        keysAndAttributes.Keys.Should().NotBeNull();
        keysAndAttributes.Keys.Should().BeEmpty();
    }

    [Fact]
    public void WithKeyPkStringValueSuccess()
    {
        var builder = new BatchGetItemBuilder("TestTable");
        builder.WithKey("pk", "1");
        var keysAndAttributes = builder.ToKeysAndAttributes();
        
        keysAndAttributes.Should().NotBeNull();
        keysAndAttributes.Keys.Should().HaveCount(1);
        keysAndAttributes.Keys[0].Should().ContainKey("pk");
        keysAndAttributes.Keys[0]["pk"].S.Should().Be("1");
    }

    [Fact]
    public void WithKeyPkSkStringValueSuccess()
    {
        var builder = new BatchGetItemBuilder("TestTable");
        builder.WithKey("pk", "1", "sk", "abcd");
        var keysAndAttributes = builder.ToKeysAndAttributes();
        
        keysAndAttributes.Should().NotBeNull();
        keysAndAttributes.Keys.Should().HaveCount(1);
        keysAndAttributes.Keys[0].Should().ContainKey("pk");
        keysAndAttributes.Keys[0].Should().ContainKey("sk");
        keysAndAttributes.Keys[0].Keys.Should().HaveCount(2);
        keysAndAttributes.Keys[0]["pk"].S.Should().Be("1");
        keysAndAttributes.Keys[0]["sk"].S.Should().Be("abcd");
    }

    [Fact]
    public void WithKeyPkSkAttributeValueSuccess()
    {
        var builder = new BatchGetItemBuilder("TestTable");
        builder.WithKey("pk", new AttributeValue { S = "1" }, "sk", new AttributeValue { S = "abcd" });
        var keysAndAttributes = builder.ToKeysAndAttributes();
        
        keysAndAttributes.Should().NotBeNull();
        keysAndAttributes.Keys.Should().HaveCount(1);
        keysAndAttributes.Keys[0].Should().ContainKey("pk");
        keysAndAttributes.Keys[0].Should().ContainKey("sk");
        keysAndAttributes.Keys[0].Keys.Should().HaveCount(2);
        keysAndAttributes.Keys[0]["pk"].S.Should().Be("1");
        keysAndAttributes.Keys[0]["sk"].S.Should().Be("abcd");
    }

    [Fact]
    public void WithKeyPkAttributeValueOnlySuccess()
    {
        var builder = new BatchGetItemBuilder("TestTable");
        builder.WithKey("pk", new AttributeValue { S = "1" });
        var keysAndAttributes = builder.ToKeysAndAttributes();
        
        keysAndAttributes.Should().NotBeNull();
        keysAndAttributes.Keys.Should().HaveCount(1);
        keysAndAttributes.Keys[0].Should().ContainKey("pk");
        keysAndAttributes.Keys[0].Keys.Should().HaveCount(1);
        keysAndAttributes.Keys[0]["pk"].S.Should().Be("1");
    }

    [Fact]
    public void WithMultipleKeysSuccess()
    {
        var builder = new BatchGetItemBuilder("TestTable");
        builder.WithKey("pk", "1")
               .WithKey("pk", "2")
               .WithKey("pk", "3");
        var keysAndAttributes = builder.ToKeysAndAttributes();
        
        keysAndAttributes.Should().NotBeNull();
        keysAndAttributes.Keys.Should().HaveCount(3);
        keysAndAttributes.Keys[0]["pk"].S.Should().Be("1");
        keysAndAttributes.Keys[1]["pk"].S.Should().Be("2");
        keysAndAttributes.Keys[2]["pk"].S.Should().Be("3");
    }

    [Fact]
    public void WithProjectionSuccess()
    {
        var builder = new BatchGetItemBuilder("TestTable");
        builder.WithProjection("description, price");
        var keysAndAttributes = builder.ToKeysAndAttributes();
        
        keysAndAttributes.Should().NotBeNull();
        keysAndAttributes.ProjectionExpression.Should().Be("description, price");
    }

    [Fact]
    public void UsingConsistentReadSuccess()
    {
        var builder = new BatchGetItemBuilder("TestTable");
        builder.UsingConsistentRead();
        var keysAndAttributes = builder.ToKeysAndAttributes();
        
        keysAndAttributes.Should().NotBeNull();
        keysAndAttributes.ConsistentRead.Should().BeTrue();
    }

    [Fact]
    public void UsingExpressionAttributeNamesSuccess()
    {
        var builder = new BatchGetItemBuilder("TestTable");
        builder.WithAttributes(new Dictionary<string, string> { { "#pk", "pk" } });
        var keysAndAttributes = builder.ToKeysAndAttributes();
        
        keysAndAttributes.Should().NotBeNull();
        keysAndAttributes.ExpressionAttributeNames.Should().NotBeNull();
        keysAndAttributes.ExpressionAttributeNames.Should().HaveCount(1);
        keysAndAttributes.ExpressionAttributeNames["#pk"].Should().Be("pk");
    }

    [Fact]
    public void UsingExpressionAttributeNamesUsingLambdaSuccess()
    {
        var builder = new BatchGetItemBuilder("TestTable");
        builder.WithAttributes(attributes => attributes.Add("#pk", "pk"));
        var keysAndAttributes = builder.ToKeysAndAttributes();
        
        keysAndAttributes.Should().NotBeNull();
        keysAndAttributes.ExpressionAttributeNames.Should().NotBeNull();
        keysAndAttributes.ExpressionAttributeNames.Should().HaveCount(1);
        keysAndAttributes.ExpressionAttributeNames["#pk"].Should().Be("pk");
    }

    [Fact]
    public void UsingExpressionAttributeNameSuccess()
    {
        var builder = new BatchGetItemBuilder("TestTable");
        builder.WithAttribute("#pk", "pk");
        var keysAndAttributes = builder.ToKeysAndAttributes();
        
        keysAndAttributes.Should().NotBeNull();
        keysAndAttributes.ExpressionAttributeNames.Should().NotBeNull();
        keysAndAttributes.ExpressionAttributeNames.Should().HaveCount(1);
        keysAndAttributes.ExpressionAttributeNames["#pk"].Should().Be("pk");
    }

    [Fact]
    public void WithMultipleAttributeNamesSuccess()
    {
        var builder = new BatchGetItemBuilder("TestTable");
        builder.WithAttribute("#pk", "pk")
               .WithAttribute("#sk", "sk")
               .WithAttribute("#desc", "description");
        var keysAndAttributes = builder.ToKeysAndAttributes();
        
        keysAndAttributes.Should().NotBeNull();
        keysAndAttributes.ExpressionAttributeNames.Should().NotBeNull();
        keysAndAttributes.ExpressionAttributeNames.Should().HaveCount(3);
        keysAndAttributes.ExpressionAttributeNames["#pk"].Should().Be("pk");
        keysAndAttributes.ExpressionAttributeNames["#sk"].Should().Be("sk");
        keysAndAttributes.ExpressionAttributeNames["#desc"].Should().Be("description");
    }

    [Fact]
    public void ComplexBuilderWithAllOptionsSuccess()
    {
        var builder = new BatchGetItemBuilder("TestTable");
        builder.WithKey("pk", "1", "sk", "a")
               .WithKey("pk", "2", "sk", "b")
               .WithProjection("#pk, #sk, #desc")
               .UsingConsistentRead()
               .WithAttribute("#pk", "pk")
               .WithAttribute("#sk", "sk")
               .WithAttribute("#desc", "description");
        
        var keysAndAttributes = builder.ToKeysAndAttributes();
        
        keysAndAttributes.Should().NotBeNull();
        keysAndAttributes.Keys.Should().HaveCount(2);
        keysAndAttributes.Keys[0]["pk"].S.Should().Be("1");
        keysAndAttributes.Keys[0]["sk"].S.Should().Be("a");
        keysAndAttributes.Keys[1]["pk"].S.Should().Be("2");
        keysAndAttributes.Keys[1]["sk"].S.Should().Be("b");
        keysAndAttributes.ProjectionExpression.Should().Be("#pk, #sk, #desc");
        keysAndAttributes.ConsistentRead.Should().BeTrue();
        keysAndAttributes.ExpressionAttributeNames.Should().HaveCount(3);
        keysAndAttributes.ExpressionAttributeNames["#pk"].Should().Be("pk");
        keysAndAttributes.ExpressionAttributeNames["#sk"].Should().Be("sk");
        keysAndAttributes.ExpressionAttributeNames["#desc"].Should().Be("description");
    }

    [Fact]
    public void ToKeysAndAttributesWithoutAttributeNamesSuccess()
    {
        var builder = new BatchGetItemBuilder("TestTable");
        builder.WithKey("pk", "1")
               .WithProjection("pk, sk")
               .UsingConsistentRead();
        
        var keysAndAttributes = builder.ToKeysAndAttributes();
        
        keysAndAttributes.Should().NotBeNull();
        keysAndAttributes.Keys.Should().HaveCount(1);
        keysAndAttributes.ProjectionExpression.Should().Be("pk, sk");
        keysAndAttributes.ConsistentRead.Should().BeTrue();
        // ExpressionAttributeNames is not set when no attributes are added
    }
}