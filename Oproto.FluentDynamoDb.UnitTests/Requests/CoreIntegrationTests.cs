using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using FluentAssertions;
using NSubstitute;
using Oproto.FluentDynamoDb.Requests;
using Oproto.FluentDynamoDb.Requests.Extensions;

namespace Oproto.FluentDynamoDb.UnitTests.Requests;

/// <summary>
/// Core integration tests for request builders working with extension methods.
/// These tests verify the essential functionality works correctly.
/// </summary>
public class CoreIntegrationTests
{
    private readonly IAmazonDynamoDB _mockClient = Substitute.For<IAmazonDynamoDB>();

    [Fact]
    public void QueryBuilder_WithExtensionMethods_ShouldWorkCorrectly()
    {
        // Arrange & Act
        var builder = new QueryRequestBuilder(_mockClient);
        var request = builder
            .ForTable("TestTable")
            .Where("pk = :pk")
            .WithValue(":pk", "USER#123")
            .WithAttribute("#status", "status")
            .ToQueryRequest();

        // Assert
        request.Should().NotBeNull();
        request.TableName.Should().Be("TestTable");
        request.KeyConditionExpression.Should().Be("pk = :pk");
        request.ExpressionAttributeValues.Should().ContainKey(":pk");
        request.ExpressionAttributeValues[":pk"].S.Should().Be("USER#123");
        request.ExpressionAttributeNames.Should().ContainKey("#status");
        request.ExpressionAttributeNames["#status"].Should().Be("status");
    }

    [Fact]
    public void QueryBuilder_WithFormatStringParameters_ShouldGenerateCorrectRequest()
    {
        // Arrange & Act
        var builder = new QueryRequestBuilder(_mockClient);
        var request = builder
            .ForTable("TestTable")
            .Where("pk = {0} AND begins_with(sk, {1})", "USER#123", "ORDER#")
            .ToQueryRequest();

        // Assert
        request.Should().NotBeNull();
        request.TableName.Should().Be("TestTable");
        request.KeyConditionExpression.Should().Be("pk = :p0 AND begins_with(sk, :p1)");
        request.ExpressionAttributeValues.Should().ContainKey(":p0");
        request.ExpressionAttributeValues[":p0"].S.Should().Be("USER#123");
        request.ExpressionAttributeValues.Should().ContainKey(":p1");
        request.ExpressionAttributeValues[":p1"].S.Should().Be("ORDER#");
    }

    [Fact]
    public void QueryBuilder_WithMixedParameterStyles_ShouldWorkCorrectly()
    {
        // Arrange & Act
        var builder = new QueryRequestBuilder(_mockClient);
        var request = builder
            .ForTable("TestTable")
            .Where("pk = {0} AND sk > :minDate", "USER#123")
            .WithValue(":minDate", "2024-01-01")
            .ToQueryRequest();

        // Assert
        request.Should().NotBeNull();
        request.KeyConditionExpression.Should().Be("pk = :p0 AND sk > :minDate");
        request.ExpressionAttributeValues.Should().ContainKey(":p0");
        request.ExpressionAttributeValues[":p0"].S.Should().Be("USER#123");
        request.ExpressionAttributeValues.Should().ContainKey(":minDate");
        request.ExpressionAttributeValues[":minDate"].S.Should().Be("2024-01-01");
    }

    [Fact]
    public void GetItemBuilder_WithExtensionMethods_ShouldWorkCorrectly()
    {
        // Arrange & Act
        var builder = new GetItemRequestBuilder(_mockClient);
        var request = builder
            .ForTable("TestTable")
            .WithKey("pk", "USER#123", "sk", "profile")
            .WithAttribute("#name", "name")
            .ToGetItemRequest();

        // Assert
        request.Should().NotBeNull();
        request.TableName.Should().Be("TestTable");
        request.Key.Should().ContainKey("pk");
        request.Key["pk"].S.Should().Be("USER#123");
        request.Key.Should().ContainKey("sk");
        request.Key["sk"].S.Should().Be("profile");
        request.ExpressionAttributeNames.Should().ContainKey("#name");
        request.ExpressionAttributeNames["#name"].Should().Be("name");
    }

    [Fact]
    public void PutItemBuilder_WithExtensionMethods_ShouldWorkCorrectly()
    {
        // Arrange
        var item = new Dictionary<string, AttributeValue>
        {
            ["id"] = new AttributeValue { S = "123" },
            ["name"] = new AttributeValue { S = "John Doe" }
        };

        // Act
        var builder = new PutItemRequestBuilder(_mockClient);
        var request = builder
            .ForTable("TestTable")
            .WithItem(item)
            .Where("attribute_not_exists(id)")
            .WithAttribute("#name", "name")
            .ToPutItemRequest();

        // Assert
        request.Should().NotBeNull();
        request.TableName.Should().Be("TestTable");
        request.Item.Should().BeEquivalentTo(item);
        request.ConditionExpression.Should().Be("attribute_not_exists(id)");
        request.ExpressionAttributeNames.Should().ContainKey("#name");
        request.ExpressionAttributeNames["#name"].Should().Be("name");
    }

    [Fact]
    public void PutItemBuilder_WithFormatStringCondition_ShouldGenerateCorrectRequest()
    {
        // Arrange
        var item = new Dictionary<string, AttributeValue>
        {
            ["id"] = new AttributeValue { S = "123" },
            ["version"] = new AttributeValue { N = "1" }
        };

        // Act
        var builder = new PutItemRequestBuilder(_mockClient);
        var request = builder
            .ForTable("TestTable")
            .WithItem(item)
            .Where("attribute_not_exists(id) OR version = {0}", 1)
            .ToPutItemRequest();

        // Assert
        request.Should().NotBeNull();
        request.ConditionExpression.Should().Be("attribute_not_exists(id) OR version = :p0");
        request.ExpressionAttributeValues.Should().ContainKey(":p0");
        request.ExpressionAttributeValues[":p0"].N.Should().Be("1");
    }

    [Fact]
    public void UpdateItemBuilder_WithExtensionMethods_ShouldWorkCorrectly()
    {
        // Arrange & Act
        var builder = new UpdateItemRequestBuilder(_mockClient);
        var request = builder
            .ForTable("TestTable")
            .WithKey("id", "123")
            .WithValue(":newName", "Jane Doe")
            .WithAttribute("#name", "name")
            .ToUpdateItemRequest();

        // Assert
        request.Should().NotBeNull();
        request.TableName.Should().Be("TestTable");
        request.Key.Should().ContainKey("id");
        request.Key["id"].S.Should().Be("123");
        request.ExpressionAttributeValues.Should().ContainKey(":newName");
        request.ExpressionAttributeValues[":newName"].S.Should().Be("Jane Doe");
        request.ExpressionAttributeNames.Should().ContainKey("#name");
        request.ExpressionAttributeNames["#name"].Should().Be("name");
    }

    [Fact]
    public void DeleteItemBuilder_WithExtensionMethods_ShouldWorkCorrectly()
    {
        // Arrange & Act
        var builder = new DeleteItemRequestBuilder(_mockClient);
        var request = builder
            .ForTable("TestTable")
            .WithKey("id", "123")
            .Where("attribute_exists(id)")
            .WithAttribute("#status", "status")
            .ToDeleteItemRequest();

        // Assert
        request.Should().NotBeNull();
        request.TableName.Should().Be("TestTable");
        request.Key.Should().ContainKey("id");
        request.Key["id"].S.Should().Be("123");
        request.ConditionExpression.Should().Be("attribute_exists(id)");
        request.ExpressionAttributeNames.Should().ContainKey("#status");
        request.ExpressionAttributeNames["#status"].Should().Be("status");
    }

    [Fact]
    public void ScanBuilder_WithExtensionMethods_ShouldWorkCorrectly()
    {
        // Arrange & Act
        var builder = new ScanRequestBuilder(_mockClient);
        var request = builder
            .ForTable("TestTable")
            .WithFilter("#status = :status")
            .WithValue(":status", "ACTIVE")
            .WithAttribute("#status", "status")
            .ToScanRequest();

        // Assert
        request.Should().NotBeNull();
        request.TableName.Should().Be("TestTable");
        request.FilterExpression.Should().Be("#status = :status");
        request.ExpressionAttributeValues.Should().ContainKey(":status");
        request.ExpressionAttributeValues[":status"].S.Should().Be("ACTIVE");
        request.ExpressionAttributeNames.Should().ContainKey("#status");
        request.ExpressionAttributeNames["#status"].Should().Be("status");
    }

    [Fact]
    public void AllBuilders_BackwardCompatibility_ShouldWork()
    {
        // Test that existing method signatures still work exactly as before
        
        // Query builder - existing usage pattern
        var queryBuilder = new QueryRequestBuilder(_mockClient);
        var queryRequest = queryBuilder
            .ForTable("TestTable")
            .Where("pk = :pk")
            .WithValue(":pk", "USER#123")
            .WithAttribute("#status", "status")
            .Take(10)
            .UsingConsistentRead()
            .ToQueryRequest();
        
        queryRequest.Should().NotBeNull();
        queryRequest.KeyConditionExpression.Should().Be("pk = :pk");
        queryRequest.ExpressionAttributeValues[":pk"].S.Should().Be("USER#123");
        
        // Get builder - existing usage pattern
        var getBuilder = new GetItemRequestBuilder(_mockClient);
        var getRequest = getBuilder
            .ForTable("TestTable")
            .WithKey("pk", "USER#123", "sk", "profile")
            .WithAttribute("#name", "name")
            .UsingConsistentRead()
            .ToGetItemRequest();
        
        getRequest.Should().NotBeNull();
        getRequest.Key["pk"].S.Should().Be("USER#123");
        getRequest.Key["sk"].S.Should().Be("profile");
    }

    [Fact]
    public void Builders_WithComplexFormatStrings_ShouldHandleMultipleFormats()
    {
        // Arrange
        var testDate = new DateTime(2024, 1, 15, 10, 30, 0, DateTimeKind.Utc);
        var testDecimal = 99.99m;
        
        // Act
        var builder = new QueryRequestBuilder(_mockClient);
        var request = builder
            .ForTable("TestTable")
            .Where("pk = {0} AND created > {1:o} AND amount <= {2:F2}", "USER#123", testDate, testDecimal)
            .ToQueryRequest();

        // Assert
        request.Should().NotBeNull();
        request.KeyConditionExpression.Should().Be("pk = :p0 AND created > :p1 AND amount <= :p2");
        request.ExpressionAttributeValues.Should().ContainKey(":p0");
        request.ExpressionAttributeValues[":p0"].S.Should().Be("USER#123");
        request.ExpressionAttributeValues.Should().ContainKey(":p1");
        request.ExpressionAttributeValues[":p1"].S.Should().Be("2024-01-15T10:30:00.0000000Z");
        request.ExpressionAttributeValues.Should().ContainKey(":p2");
        request.ExpressionAttributeValues[":p2"].S.Should().Be("99.99");
    }

    [Fact]
    public void Builders_ErrorHandling_ShouldThrowAppropriateExceptions()
    {
        var builder = new QueryRequestBuilder(_mockClient);
        
        // Test empty format string
        var act1 = () => builder.Where("", "value");
        act1.Should().Throw<ArgumentException>()
            .WithMessage("Format string cannot be null or empty.*");
        
        // Test mismatched parameter count
        var act2 = () => builder.Where("pk = {0} AND sk = {1}", "onlyOneValue");
        act2.Should().Throw<ArgumentException>()
            .WithMessage("*parameter index 1 but only 1 arguments were provided*");
        
        // Test null arguments
        var act3 = () => builder.Where("pk = {0}", (object[])null!);
        act3.Should().Throw<ArgumentNullException>();
    }
}