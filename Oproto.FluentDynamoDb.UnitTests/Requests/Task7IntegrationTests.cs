using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using FluentAssertions;
using NSubstitute;
using Oproto.FluentDynamoDb.Requests;
using Oproto.FluentDynamoDb.Requests.Extensions;

namespace Oproto.FluentDynamoDb.UnitTests.Requests;

/// <summary>
/// Integration tests for Task 7: Create integration tests for builder functionality
/// These tests specifically address the task requirements:
/// - Test all request builders work correctly with extension methods
/// - Verify mixed usage of old parameter style and new format strings
/// - Test complex expressions with multiple parameters and formats
/// - Validate backward compatibility with existing usage patterns
/// </summary>
public class Task7IntegrationTests
{
    private readonly IAmazonDynamoDB _mockClient = Substitute.For<IAmazonDynamoDB>();

    #region Test all request builders work correctly with extension methods

    [Fact]
    public void AllRequestBuilders_WithExtensionMethods_ShouldWorkCorrectly()
    {
        // Test QueryRequestBuilder with extension methods
        var queryBuilder = new QueryRequestBuilder(_mockClient);
        var queryRequest = queryBuilder
            .ForTable("TestTable")
            .Where("pk = :pk")
            .WithValue(":pk", "USER#123")
            .WithAttribute("#status", "status")
            .ToQueryRequest();

        queryRequest.Should().NotBeNull();
        queryRequest.KeyConditionExpression.Should().Be("pk = :pk");
        queryRequest.ExpressionAttributeValues[":pk"].S.Should().Be("USER#123");
        queryRequest.ExpressionAttributeNames["#status"].Should().Be("status");

        // Test GetItemRequestBuilder with extension methods
        var getBuilder = new GetItemRequestBuilder(_mockClient);
        var getRequest = getBuilder
            .ForTable("TestTable")
            .WithKey("pk", "USER#123", "sk", "profile")
            .WithAttribute("#name", "name")
            .ToGetItemRequest();

        getRequest.Should().NotBeNull();
        getRequest.Key["pk"].S.Should().Be("USER#123");
        getRequest.Key["sk"].S.Should().Be("profile");
        getRequest.ExpressionAttributeNames["#name"].Should().Be("name");

        // Test PutItemRequestBuilder with extension methods
        var putBuilder = new PutItemRequestBuilder(_mockClient);
        var putRequest = putBuilder
            .ForTable("TestTable")
            .WithItem(new Dictionary<string, AttributeValue> { ["id"] = new AttributeValue { S = "123" } })
            .Where("attribute_not_exists(id)")
            .WithValue(":version", 1)
            .ToPutItemRequest();

        putRequest.Should().NotBeNull();
        putRequest.ConditionExpression.Should().Be("attribute_not_exists(id)");
        putRequest.ExpressionAttributeValues[":version"].N.Should().Be("1");

        // Test UpdateItemRequestBuilder with extension methods
        var updateBuilder = new UpdateItemRequestBuilder(_mockClient);
        var updateRequest = updateBuilder
            .ForTable("TestTable")
            .WithKey("id", "123")
            .WithValue(":newName", "Jane Doe")
            .WithAttribute("#name", "name")
            .ToUpdateItemRequest();

        updateRequest.Should().NotBeNull();
        updateRequest.Key["id"].S.Should().Be("123");
        updateRequest.ExpressionAttributeValues[":newName"].S.Should().Be("Jane Doe");
        updateRequest.ExpressionAttributeNames["#name"].Should().Be("name");

        // Test DeleteItemRequestBuilder with extension methods
        var deleteBuilder = new DeleteItemRequestBuilder(_mockClient);
        var deleteRequest = deleteBuilder
            .ForTable("TestTable")
            .WithKey("id", "123")
            .Where("attribute_exists(id)")
            .WithAttribute("#status", "status")
            .ToDeleteItemRequest();

        deleteRequest.Should().NotBeNull();
        deleteRequest.Key["id"].S.Should().Be("123");
        deleteRequest.ConditionExpression.Should().Be("attribute_exists(id)");
        deleteRequest.ExpressionAttributeNames["#status"].Should().Be("status");

        // Test ScanRequestBuilder with extension methods
        var scanBuilder = new ScanRequestBuilder(_mockClient);
        var scanRequest = scanBuilder
            .ForTable("TestTable")
            .WithFilter("#status = :status")
            .WithValue(":status", "ACTIVE")
            .WithAttribute("#status", "status")
            .ToScanRequest();

        scanRequest.Should().NotBeNull();
        scanRequest.FilterExpression.Should().Be("#status = :status");
        scanRequest.ExpressionAttributeValues[":status"].S.Should().Be("ACTIVE");
        scanRequest.ExpressionAttributeNames["#status"].Should().Be("status");
    }

    #endregion

    #region Verify mixed usage of old parameter style and new format strings

    [Fact]
    public void QueryBuilder_WithMixedParameterStyles_ShouldWorkCorrectly()
    {
        // Test mixing format strings with traditional parameter style
        var builder = new QueryRequestBuilder(_mockClient);
        var request = builder
            .ForTable("TestTable")
            .Where("pk = {0} AND sk BETWEEN :minDate AND :maxDate", "USER#123")
            .WithValue(":minDate", "2024-01-01")
            .WithValue(":maxDate", "2024-12-31")
            .ToQueryRequest();

        // Assert key condition expression uses both styles
        request.KeyConditionExpression.Should().Be("pk = :p0 AND sk BETWEEN :minDate AND :maxDate");
        request.ExpressionAttributeValues.Should().ContainKey(":p0");
        request.ExpressionAttributeValues[":p0"].S.Should().Be("USER#123");
        request.ExpressionAttributeValues.Should().ContainKey(":minDate");
        request.ExpressionAttributeValues[":minDate"].S.Should().Be("2024-01-01");
        request.ExpressionAttributeValues.Should().ContainKey(":maxDate");
        request.ExpressionAttributeValues[":maxDate"].S.Should().Be("2024-12-31");
    }

    [Fact]
    public void PutItemBuilder_WithMixedParameterStyles_ShouldWorkCorrectly()
    {
        // Arrange
        var item = new Dictionary<string, AttributeValue>
        {
            ["id"] = new AttributeValue { S = "123" },
            ["version"] = new AttributeValue { N = "1" }
        };

        // Act - Mix format strings with traditional parameters
        var builder = new PutItemRequestBuilder(_mockClient);
        var request = builder
            .ForTable("TestTable")
            .WithItem(item)
            .Where("(attribute_not_exists(id) OR version = {0}) AND #status <> :oldStatus", 1)
            .WithValue(":oldStatus", "DELETED")
            .WithAttribute("#status", "status")
            .ToPutItemRequest();

        // Assert
        request.ConditionExpression.Should().Be("(attribute_not_exists(id) OR version = :p0) AND #status <> :oldStatus");
        request.ExpressionAttributeValues.Should().ContainKey(":p0");
        request.ExpressionAttributeValues[":p0"].N.Should().Be("1");
        request.ExpressionAttributeValues.Should().ContainKey(":oldStatus");
        request.ExpressionAttributeValues[":oldStatus"].S.Should().Be("DELETED");
        request.ExpressionAttributeNames["#status"].Should().Be("status");
    }

    [Fact]
    public void UpdateItemBuilder_WithMixedParameterStyles_ShouldWorkCorrectly()
    {
        // Act - Mix format strings with traditional parameters in condition
        var builder = new UpdateItemRequestBuilder(_mockClient);
        var request = builder
            .ForTable("TestTable")
            .WithKey("id", "123")
            .Where("version = {0} AND #status = :currentStatus", 1)
            .WithValue(":currentStatus", "ACTIVE")
            .WithValue(":newVersion", 2)
            .WithAttribute("#status", "status")
            .ToUpdateItemRequest();

        // Assert
        request.ConditionExpression.Should().Be("version = :p0 AND #status = :currentStatus");
        request.ExpressionAttributeValues.Should().ContainKey(":p0");
        request.ExpressionAttributeValues[":p0"].N.Should().Be("1");
        request.ExpressionAttributeValues.Should().ContainKey(":currentStatus");
        request.ExpressionAttributeValues[":currentStatus"].S.Should().Be("ACTIVE");
        request.ExpressionAttributeValues.Should().ContainKey(":newVersion");
        request.ExpressionAttributeValues[":newVersion"].N.Should().Be("2");
    }

    [Fact]
    public void DeleteItemBuilder_WithMixedParameterStyles_ShouldWorkCorrectly()
    {
        // Act - Mix format strings with traditional parameters
        var builder = new DeleteItemRequestBuilder(_mockClient);
        var request = builder
            .ForTable("TestTable")
            .WithKey("id", "123")
            .Where("version = {0} AND #status IN (:status1, :status2)", 1)
            .WithValue(":status1", "ACTIVE")
            .WithValue(":status2", "PENDING")
            .WithAttribute("#status", "status")
            .ToDeleteItemRequest();

        // Assert
        request.ConditionExpression.Should().Be("version = :p0 AND #status IN (:status1, :status2)");
        request.ExpressionAttributeValues.Should().ContainKey(":p0");
        request.ExpressionAttributeValues[":p0"].N.Should().Be("1");
        request.ExpressionAttributeValues.Should().ContainKey(":status1");
        request.ExpressionAttributeValues[":status1"].S.Should().Be("ACTIVE");
        request.ExpressionAttributeValues.Should().ContainKey(":status2");
        request.ExpressionAttributeValues[":status2"].S.Should().Be("PENDING");
    }

    #endregion

    #region Test complex expressions with multiple parameters and formats

    [Fact]
    public void QueryBuilder_WithComplexFormatExpressions_ShouldHandleMultipleFormats()
    {
        // Arrange - Complex data types with various formats
        var testDate = new DateTime(2024, 1, 15, 10, 30, 0, DateTimeKind.Utc);

        // Act - Complex expression with multiple format specifiers
        var builder = new QueryRequestBuilder(_mockClient);
        var request = builder
            .ForTable("TestTable")
            .Where("pk = {0} AND sk BETWEEN {1:o} AND {2:o}", "USER#123", testDate.AddDays(-30), testDate)
            .ToQueryRequest();

        // Assert key condition
        request.KeyConditionExpression.Should().Be("pk = :p0 AND sk BETWEEN :p1 AND :p2");
        request.ExpressionAttributeValues[":p0"].S.Should().Be("USER#123");
        request.ExpressionAttributeValues[":p1"].S.Should().Be("2023-12-16T10:30:00.0000000Z");
        request.ExpressionAttributeValues[":p2"].S.Should().Be("2024-01-15T10:30:00.0000000Z");
    }

    [Fact]
    public void PutItemBuilder_WithComplexFormatExpressions_ShouldHandleAdvancedConditions()
    {
        // Arrange
        var item = new Dictionary<string, AttributeValue>
        {
            ["id"] = new AttributeValue { S = "123" },
            ["version"] = new AttributeValue { N = "1" },
            ["amount"] = new AttributeValue { N = "999.99" }
        };
        var currentVersion = 0;
        var maxAmount = 1000.00m;
        var validStatuses = new[] { "DRAFT", "PENDING" };

        // Act - Complex condition with multiple format types
        var builder = new PutItemRequestBuilder(_mockClient);
        var request = builder
            .ForTable("TestTable")
            .WithItem(item)
            .Where("(attribute_not_exists(version) OR version = {0}) AND amount <= {1:F2} AND #status IN ({2}, {3})", 
                currentVersion, maxAmount, validStatuses[0], validStatuses[1])
            .WithAttribute("#status", "status")
            .ToPutItemRequest();

        // Assert
        request.ConditionExpression.Should().Be("(attribute_not_exists(version) OR version = :p0) AND amount <= :p1 AND #status IN (:p2, :p3)");
        request.ExpressionAttributeValues[":p0"].N.Should().Be("0");
        request.ExpressionAttributeValues[":p1"].S.Should().Be("1000.00");
        request.ExpressionAttributeValues[":p2"].S.Should().Be("DRAFT");
        request.ExpressionAttributeValues[":p3"].S.Should().Be("PENDING");
    }

    #endregion

    #region Validate backward compatibility with existing usage patterns

    [Fact]
    public void AllBuilders_ExistingUsagePatterns_ShouldRemainUnchanged()
    {
        // This test validates that all existing usage patterns continue to work
        // exactly as they did before the refactoring

        // Query builder - existing usage pattern
        var queryBuilder = new QueryRequestBuilder(_mockClient);
        var queryRequest = queryBuilder
            .ForTable("Users")
            .Where("pk = :pk AND begins_with(sk, :prefix)")
            .WithValue(":pk", "USER#123")
            .WithValue(":prefix", "ORDER#")
            .WithAttribute("#status", "status")
            .WithAttribute("#amount", "amount")
            .Take(25)
            .UsingConsistentRead()
            .OrderDescending()
            .ReturnTotalConsumedCapacity()
            .ToQueryRequest();

        // Verify all traditional functionality works
        queryRequest.TableName.Should().Be("Users");
        queryRequest.KeyConditionExpression.Should().Be("pk = :pk AND begins_with(sk, :prefix)");
        queryRequest.ExpressionAttributeValues[":pk"].S.Should().Be("USER#123");
        queryRequest.ExpressionAttributeValues[":prefix"].S.Should().Be("ORDER#");
        queryRequest.ExpressionAttributeNames["#status"].Should().Be("status");
        queryRequest.ExpressionAttributeNames["#amount"].Should().Be("amount");
        queryRequest.Limit.Should().Be(25);
        queryRequest.ConsistentRead.Should().BeTrue();
        queryRequest.ScanIndexForward.Should().BeFalse();
        queryRequest.ReturnConsumedCapacity.Should().Be(Amazon.DynamoDBv2.ReturnConsumedCapacity.TOTAL);

        // Get builder - existing usage pattern
        var getBuilder = new GetItemRequestBuilder(_mockClient);
        var getRequest = getBuilder
            .ForTable("Users")
            .WithKey("pk", "USER#123", "sk", "profile")
            .WithAttribute("#name", "name")
            .WithAttribute("#email", "email")
            .WithProjection("#name, #email, createdAt")
            .UsingConsistentRead()
            .ReturnTotalConsumedCapacity()
            .ToGetItemRequest();

        getRequest.TableName.Should().Be("Users");
        getRequest.Key["pk"].S.Should().Be("USER#123");
        getRequest.Key["sk"].S.Should().Be("profile");
        getRequest.ExpressionAttributeNames["#name"].Should().Be("name");
        getRequest.ExpressionAttributeNames["#email"].Should().Be("email");
        getRequest.ProjectionExpression.Should().Be("#name, #email, createdAt");
        getRequest.ConsistentRead.Should().BeTrue();
        getRequest.ReturnConsumedCapacity.Should().Be(Amazon.DynamoDBv2.ReturnConsumedCapacity.TOTAL);

        // Put builder - existing usage pattern
        var putBuilder = new PutItemRequestBuilder(_mockClient);
        var putRequest = putBuilder
            .ForTable("Users")
            .WithItem(new Dictionary<string, AttributeValue>
            {
                ["pk"] = new AttributeValue { S = "USER#123" },
                ["sk"] = new AttributeValue { S = "profile" },
                ["name"] = new AttributeValue { S = "John Doe" }
            })
            .Where("attribute_not_exists(pk) OR version = :expectedVersion")
            .WithValue(":expectedVersion", 1)
            .WithAttribute("#name", "name")
            .ReturnAllOldValues()
            .ReturnTotalConsumedCapacity()
            .ToPutItemRequest();

        putRequest.TableName.Should().Be("Users");
        putRequest.ConditionExpression.Should().Be("attribute_not_exists(pk) OR version = :expectedVersion");
        putRequest.ExpressionAttributeValues[":expectedVersion"].N.Should().Be("1");
        putRequest.ExpressionAttributeNames["#name"].Should().Be("name");
        putRequest.ReturnValues.Should().Be(ReturnValue.ALL_OLD);
        putRequest.ReturnConsumedCapacity.Should().Be(Amazon.DynamoDBv2.ReturnConsumedCapacity.TOTAL);
    }

    [Fact]
    public void AllBuilders_MethodChaining_ShouldReturnCorrectTypes()
    {
        // Verify that method chaining continues to work and returns correct types
        // This ensures the Self property implementation is working correctly

        // Test QueryRequestBuilder chaining
        var queryBuilder = new QueryRequestBuilder(_mockClient);
        var chainedQuery = queryBuilder
            .ForTable("TestTable")
            .Where("pk = :pk")
            .WithValue(":pk", "test")
            .WithAttribute("#attr", "attribute")
            .Take(10)
            .UsingConsistentRead();

        chainedQuery.Should().BeOfType<QueryRequestBuilder>();
        chainedQuery.Should().BeSameAs(queryBuilder);

        // Test GetItemRequestBuilder chaining
        var getBuilder = new GetItemRequestBuilder(_mockClient);
        var chainedGet = getBuilder
            .ForTable("TestTable")
            .WithKey("id", "123")
            .WithAttribute("#name", "name")
            .UsingConsistentRead();

        chainedGet.Should().BeOfType<GetItemRequestBuilder>();
        chainedGet.Should().BeSameAs(getBuilder);

        // Test PutItemRequestBuilder chaining
        var putBuilder = new PutItemRequestBuilder(_mockClient);
        var chainedPut = putBuilder
            .ForTable("TestTable")
            .WithItem(new Dictionary<string, AttributeValue>())
            .Where("condition")
            .WithValue(":val", "test")
            .ReturnAllOldValues();

        chainedPut.Should().BeOfType<PutItemRequestBuilder>();
        chainedPut.Should().BeSameAs(putBuilder);

        // Test UpdateItemRequestBuilder chaining
        var updateBuilder = new UpdateItemRequestBuilder(_mockClient);
        var chainedUpdate = updateBuilder
            .ForTable("TestTable")
            .WithKey("id", "123")
            .Where("condition")
            .WithValue(":val", "test")
            .ReturnUpdatedNewValues();

        chainedUpdate.Should().BeOfType<UpdateItemRequestBuilder>();
        chainedUpdate.Should().BeSameAs(updateBuilder);

        // Test DeleteItemRequestBuilder chaining
        var deleteBuilder = new DeleteItemRequestBuilder(_mockClient);
        var chainedDelete = deleteBuilder
            .ForTable("TestTable")
            .WithKey("id", "123")
            .Where("condition")
            .WithValue(":val", "test")
            .ReturnAllOldValues();

        chainedDelete.Should().BeOfType<DeleteItemRequestBuilder>();
        chainedDelete.Should().BeSameAs(deleteBuilder);

        // Test ScanRequestBuilder chaining
        var scanBuilder = new ScanRequestBuilder(_mockClient);
        var chainedScan = scanBuilder
            .ForTable("TestTable")
            .WithFilter("condition")
            .WithValue(":val", "test")
            .WithAttribute("#attr", "attribute")
            .Take(10);

        chainedScan.Should().BeOfType<ScanRequestBuilder>();
        chainedScan.Should().BeSameAs(scanBuilder);
    }

    #endregion

    #region Error Handling and Edge Cases

    [Fact]
    public void AllBuilders_WithInvalidFormatStrings_ShouldThrowAppropriateExceptions()
    {
        // Test various error conditions across different builders

        // Empty format string
        var queryBuilder = new QueryRequestBuilder(_mockClient);
        var act1 = () => queryBuilder.Where("", "value");
        act1.Should().Throw<ArgumentException>()
            .WithMessage("Format string cannot be null or empty.*");

        // Mismatched parameter count
        var putBuilder = new PutItemRequestBuilder(_mockClient);
        var act2 = () => putBuilder.Where("pk = {0} AND sk = {1}", "onlyOneValue");
        act2.Should().Throw<ArgumentException>()
            .WithMessage("*parameter index 1 but only 1 arguments were provided*");

        // Invalid format specifier
        var updateBuilder = new UpdateItemRequestBuilder(_mockClient);
        var act3 = () => updateBuilder.Where("pk = {0:invalidFormat}", "value");
        act3.Should().Throw<FormatException>();

        // Null arguments
        var deleteBuilder = new DeleteItemRequestBuilder(_mockClient);
        var act4 = () => deleteBuilder.Where("pk = {0}", (object[])null!);
        act4.Should().Throw<ArgumentNullException>();

        // Out of range parameter index - using Where method instead of WithFilter
        var scanBuilder2 = new QueryRequestBuilder(_mockClient);
        var act5 = () => scanBuilder2.Where("pk = {5}", "value");
        act5.Should().Throw<ArgumentException>()
            .WithMessage("*parameter index 5 but only 1 arguments were provided*");
    }

    [Fact]
    public void AllBuilders_WithNullValues_ShouldHandleGracefully()
    {
        // Test how builders handle null values in format strings
        var builder = new QueryRequestBuilder(_mockClient);
        var request = builder
            .ForTable("TestTable")
            .Where("pk = {0} AND sk = {1}", "USER#123", (string?)null)
            .ToQueryRequest();

        // Null values should be converted to NULL AttributeValue
        request.ExpressionAttributeValues[":p1"].NULL.Should().BeTrue();
    }

    #endregion

    #region Transaction and Batch Builder Basic Tests

    [Fact]
    public void TransactionBuilders_WithBasicOperations_ShouldWorkCorrectly()
    {
        // Test TransactWriteItemsRequestBuilder
        var writeBuilder = new TransactWriteItemsRequestBuilder(_mockClient);
        var writeRequest = writeBuilder
            .ReturnTotalConsumedCapacity()
            .ReturnItemCollectionMetrics()
            .ToTransactWriteItemsRequest();

        writeRequest.Should().NotBeNull();
        writeRequest.ReturnConsumedCapacity.Should().Be(Amazon.DynamoDBv2.ReturnConsumedCapacity.TOTAL);
        writeRequest.ReturnItemCollectionMetrics.Should().Be(Amazon.DynamoDBv2.ReturnItemCollectionMetrics.SIZE);

        // Test TransactGetItemsRequestBuilder
        var getBuilder = new TransactGetItemsRequestBuilder(_mockClient);
        var getRequest = getBuilder
            .ReturnConsumedCapacity(ReturnConsumedCapacity.TOTAL)
            .ToTransactGetItemsRequest();

        getRequest.Should().NotBeNull();
        getRequest.ReturnConsumedCapacity.Should().Be(ReturnConsumedCapacity.TOTAL);
    }

    [Fact]
    public void BatchBuilders_WithBasicOperations_ShouldWorkCorrectly()
    {
        // Test BatchGetItemRequestBuilder
        var batchGetBuilder = new BatchGetItemRequestBuilder(_mockClient);
        var batchGetRequest = batchGetBuilder
            .ReturnConsumedCapacity(ReturnConsumedCapacity.TOTAL)
            .ToBatchGetItemRequest();

        batchGetRequest.Should().NotBeNull();
        batchGetRequest.ReturnConsumedCapacity.Should().Be(ReturnConsumedCapacity.TOTAL);

        // Test BatchWriteItemRequestBuilder
        var batchWriteBuilder = new BatchWriteItemRequestBuilder(_mockClient);
        var batchWriteRequest = batchWriteBuilder
            .ReturnTotalConsumedCapacity()
            .ReturnItemCollectionMetrics()
            .ToBatchWriteItemRequest();

        batchWriteRequest.Should().NotBeNull();
        batchWriteRequest.ReturnConsumedCapacity.Should().Be(Amazon.DynamoDBv2.ReturnConsumedCapacity.TOTAL);
        batchWriteRequest.ReturnItemCollectionMetrics.Should().Be(Amazon.DynamoDBv2.ReturnItemCollectionMetrics.SIZE);
    }

    #endregion
}