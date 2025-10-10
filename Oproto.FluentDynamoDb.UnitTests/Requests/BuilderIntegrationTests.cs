using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using FluentAssertions;
using NSubstitute;
using Oproto.FluentDynamoDb.Requests;
using Oproto.FluentDynamoDb.Requests.Extensions;

namespace Oproto.FluentDynamoDb.UnitTests.Requests;

/// <summary>
/// Integration tests for request builders working with extension methods.
/// Tests verify that all builders work correctly with extension methods,
/// mixed usage of old and new parameter styles, and backward compatibility.
/// </summary>
public class BuilderIntegrationTests
{
    private readonly IAmazonDynamoDB _mockClient = Substitute.For<IAmazonDynamoDB>();

    #region QueryRequestBuilder Integration Tests

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
    public void QueryBuilder_WithComplexFormatStrings_ShouldHandleMultipleFormats()
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

    #endregion

    #region GetItemRequestBuilder Integration Tests

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
    public void GetItemBuilder_WithKeyExtensions_ShouldSupportAllOverloads()
    {
        // Test single key with string
        var builder1 = new GetItemRequestBuilder(_mockClient);
        var request1 = builder1
            .ForTable("TestTable")
            .WithKey("id", "123")
            .ToGetItemRequest();

        request1.Key.Should().ContainKey("id");
        request1.Key["id"].S.Should().Be("123");

        // Test composite key with strings
        var builder2 = new GetItemRequestBuilder(_mockClient);
        var request2 = builder2
            .ForTable("TestTable")
            .WithKey("pk", "USER#123", "sk", "profile")
            .ToGetItemRequest();

        request2.Key.Should().ContainKey("pk");
        request2.Key["pk"].S.Should().Be("USER#123");
        request2.Key.Should().ContainKey("sk");
        request2.Key["sk"].S.Should().Be("profile");

        // Test with AttributeValue objects
        var builder3 = new GetItemRequestBuilder(_mockClient);
        var request3 = builder3
            .ForTable("TestTable")
            .WithKey("pk", new AttributeValue { S = "USER#123" }, "sk", new AttributeValue { N = "456" })
            .ToGetItemRequest();

        request3.Key.Should().ContainKey("pk");
        request3.Key["pk"].S.Should().Be("USER#123");
        request3.Key.Should().ContainKey("sk");
        request3.Key["sk"].N.Should().Be("456");
    }

    #endregion

    #region PutItemRequestBuilder Integration Tests

    [Fact]
    public void PutItemBuilder_WithExtensionMethods_ShouldWorkCorrectly()
    {
        // Arrange
        var item = new Dictionary<string, AttributeValue>
        {
            ["id"] = new AttributeValue { S = "123" },
            ["name"] = new AttributeValue { S = "John Doe" },
            ["email"] = new AttributeValue { S = "john@example.com" }
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

    #endregion

    #region UpdateItemRequestBuilder Integration Tests

    [Fact]
    public void UpdateItemBuilder_WithExtensionMethods_ShouldWorkCorrectly()
    {
        // Arrange & Act
        var builder = new UpdateItemRequestBuilder(_mockClient);
        var request = builder
            .ForTable("TestTable")
            .WithKey("id", "123")
            .WithValue(":newName", "Jane Doe")
            .WithValue(":timestamp", DateTime.UtcNow.ToString("o"))
            .WithAttribute("#name", "name")
            .WithAttribute("#updated", "updatedAt")
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
    public void UpdateItemBuilder_WithFormatStringCondition_ShouldGenerateCorrectRequest()
    {
        // Arrange & Act
        var builder = new UpdateItemRequestBuilder(_mockClient);
        var request = builder
            .ForTable("TestTable")
            .WithKey("id", "123")
            .Where("version = {0}", 1)
            .WithValue(":newVersion", 2)
            .ToUpdateItemRequest();

        // Assert
        request.Should().NotBeNull();
        request.ConditionExpression.Should().Be("version = :p0");
        request.ExpressionAttributeValues.Should().ContainKey(":p0");
        request.ExpressionAttributeValues[":p0"].N.Should().Be("1");
        request.ExpressionAttributeValues.Should().ContainKey(":newVersion");
        request.ExpressionAttributeValues[":newVersion"].N.Should().Be("2");
    }

    #endregion

    #region DeleteItemRequestBuilder Integration Tests

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
    public void DeleteItemBuilder_WithFormatStringCondition_ShouldGenerateCorrectRequest()
    {
        // Arrange & Act
        var builder = new DeleteItemRequestBuilder(_mockClient);
        var request = builder
            .ForTable("TestTable")
            .WithKey("id", "123")
            .Where("version = {0} AND #status = {1}", 1, "ACTIVE")
            .WithAttribute("#status", "status")
            .ToDeleteItemRequest();

        // Assert
        request.Should().NotBeNull();
        request.ConditionExpression.Should().Be("version = :p0 AND #status = :p1");
        request.ExpressionAttributeValues.Should().ContainKey(":p0");
        request.ExpressionAttributeValues[":p0"].N.Should().Be("1");
        request.ExpressionAttributeValues.Should().ContainKey(":p1");
        request.ExpressionAttributeValues[":p1"].S.Should().Be("ACTIVE");
    }

    #endregion

    #region ScanRequestBuilder Integration Tests

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
    public void ScanBuilder_WithAttributeExtensions_ShouldWorkCorrectly()
    {
        // Arrange & Act
        var builder = new ScanRequestBuilder(_mockClient);
        var request = builder
            .ForTable("TestTable")
            .WithFilter("#status = :status AND amount > :amount")
            .WithValue(":status", "ACTIVE")
            .WithValue(":amount", 100.50m)
            .WithAttribute("#status", "status")
            .ToScanRequest();

        // Assert
        request.Should().NotBeNull();
        request.FilterExpression.Should().Be("#status = :status AND amount > :amount");
        request.ExpressionAttributeValues.Should().ContainKey(":status");
        request.ExpressionAttributeValues[":status"].S.Should().Be("ACTIVE");
        request.ExpressionAttributeValues.Should().ContainKey(":amount");
        request.ExpressionAttributeValues[":amount"].N.Should().Be("100.5");
    }

    #endregion

    #region Transaction Builder Integration Tests

    [Fact]
    public void TransactWriteBuilder_WithBasicOperations_ShouldWorkCorrectly()
    {
        // Note: Transaction builders require DynamoDbTableBase instances, 
        // so we'll test the basic builder functionality instead
        var builder = new TransactWriteItemsRequestBuilder(_mockClient);
        var request = builder
            .ReturnTotalConsumedCapacity()
            .ToTransactWriteItemsRequest();

        // Assert
        request.Should().NotBeNull();
        request.ReturnConsumedCapacity.Should().Be(Amazon.DynamoDBv2.ReturnConsumedCapacity.TOTAL);
        request.TransactItems.Should().BeEmpty(); // No items added yet
    }

    [Fact]
    public void TransactGetBuilder_WithBasicOperations_ShouldWorkCorrectly()
    {
        // Note: Transaction builders require DynamoDbTableBase instances,
        // so we'll test the basic builder functionality instead
        var builder = new TransactGetItemsRequestBuilder(_mockClient);
        var request = builder
            .ReturnConsumedCapacity(ReturnConsumedCapacity.TOTAL)
            .ToTransactGetItemsRequest();

        // Assert
        request.Should().NotBeNull();
        request.ReturnConsumedCapacity.Should().Be(ReturnConsumedCapacity.TOTAL);
        request.TransactItems.Should().BeEmpty(); // No items added yet
    }

    [Fact]
    public void TransactWriteBuilder_WithClientRequestToken_ShouldWorkCorrectly()
    {
        // Arrange
        var clientToken = Guid.NewGuid().ToString();
        
        // Act
        var builder = new TransactWriteItemsRequestBuilder(_mockClient);
        var request = builder
            .WithClientRequestToken(clientToken)
            .ReturnItemCollectionMetrics()
            .ToTransactWriteItemsRequest();

        // Assert
        request.Should().NotBeNull();
        request.ClientRequestToken.Should().Be(clientToken);
        request.ReturnItemCollectionMetrics.Should().Be(Amazon.DynamoDBv2.ReturnItemCollectionMetrics.SIZE);
    }

    #endregion

    #region Batch Operation Integration Tests

    [Fact]
    public void BatchGetBuilder_WithBasicOperations_ShouldWorkCorrectly()
    {
        // Arrange & Act
        var builder = new BatchGetItemRequestBuilder(_mockClient);
        var request = builder
            .ReturnConsumedCapacity(ReturnConsumedCapacity.TOTAL)
            .ToBatchGetItemRequest();

        // Assert
        request.Should().NotBeNull();
        request.ReturnConsumedCapacity.Should().Be(ReturnConsumedCapacity.TOTAL);
        request.RequestItems.Should().BeEmpty(); // No tables added yet
    }

    [Fact]
    public void BatchWriteBuilder_WithBasicOperations_ShouldWorkCorrectly()
    {
        // Arrange & Act
        var builder = new BatchWriteItemRequestBuilder(_mockClient);
        var request = builder
            .ReturnTotalConsumedCapacity()
            .ReturnItemCollectionMetrics()
            .ToBatchWriteItemRequest();

        // Assert
        request.Should().NotBeNull();
        request.ReturnConsumedCapacity.Should().Be(Amazon.DynamoDBv2.ReturnConsumedCapacity.TOTAL);
        request.ReturnItemCollectionMetrics.Should().Be(Amazon.DynamoDBv2.ReturnItemCollectionMetrics.SIZE);
        request.RequestItems.Should().BeEmpty(); // No tables added yet
    }

    #endregion

    #region Backward Compatibility Tests

    [Fact]
    public void AllBuilders_ExistingMethodCalls_ShouldContinueToWork()
    {
        // This test verifies that all existing method signatures still work
        // exactly as they did before the refactoring
        
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
        
        // Put builder - existing usage pattern
        var putBuilder = new PutItemRequestBuilder(_mockClient);
        var putRequest = putBuilder
            .ForTable("TestTable")
            .WithItem(new Dictionary<string, AttributeValue> { ["id"] = new AttributeValue { S = "123" } })
            .Where("attribute_not_exists(id)")
            .ReturnAllOldValues()
            .ToPutItemRequest();
        
        putRequest.Should().NotBeNull();
        putRequest.ConditionExpression.Should().Be("attribute_not_exists(id)");
        putRequest.ReturnValues.Should().Be(ReturnValue.ALL_OLD);
    }

    [Fact]
    public void AllBuilders_MethodChaining_ShouldReturnCorrectTypes()
    {
        // Verify that method chaining returns the correct builder types
        // This ensures the Self property and extension methods work correctly
        
        var queryBuilder = new QueryRequestBuilder(_mockClient);
        var chainedQuery = queryBuilder
            .ForTable("TestTable")
            .Where("pk = :pk")
            .WithValue(":pk", "test")
            .WithAttribute("#attr", "attribute")
            .Take(10);
        
        chainedQuery.Should().BeOfType<QueryRequestBuilder>();
        
        var getBuilder = new GetItemRequestBuilder(_mockClient);
        var chainedGet = getBuilder
            .ForTable("TestTable")
            .WithKey("id", "123")
            .WithAttribute("#name", "name");
        
        chainedGet.Should().BeOfType<GetItemRequestBuilder>();
        
        var putBuilder = new PutItemRequestBuilder(_mockClient);
        var chainedPut = putBuilder
            .ForTable("TestTable")
            .WithItem(new Dictionary<string, AttributeValue>())
            .Where("condition")
            .WithValue(":val", "test");
        
        chainedPut.Should().BeOfType<PutItemRequestBuilder>();
    }

    #endregion

    #region Edge Cases and Error Handling

    [Fact]
    public void Builders_WithEmptyFormatString_ShouldThrowArgumentException()
    {
        var builder = new QueryRequestBuilder(_mockClient);
        
        var act = () => builder.Where("", "value");
        
        act.Should().Throw<ArgumentException>()
            .WithMessage("Format string cannot be null or empty.*");
    }

    [Fact]
    public void Builders_WithMismatchedParameterCount_ShouldThrowArgumentException()
    {
        var builder = new QueryRequestBuilder(_mockClient);
        
        var act = () => builder.Where("pk = {0} AND sk = {1}", "onlyOneValue");
        
        act.Should().Throw<ArgumentException>()
            .WithMessage("*parameter index 1 but only 1 arguments were provided*");
    }

    [Fact]
    public void Builders_WithInvalidFormatSpecifier_ShouldThrowFormatException()
    {
        var builder = new QueryRequestBuilder(_mockClient);
        
        var act = () => builder.Where("pk = {0:invalidFormat}", "value");
        
        act.Should().Throw<FormatException>();
    }

    [Fact]
    public void Builders_WithNullArguments_ShouldThrowArgumentNullException()
    {
        var builder = new QueryRequestBuilder(_mockClient);
        
        var act = () => builder.Where("pk = {0}", (object[])null!);
        
        act.Should().Throw<ArgumentNullException>();
    }

    #endregion
}