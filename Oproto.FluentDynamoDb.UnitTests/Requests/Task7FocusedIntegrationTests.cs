using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using FluentAssertions;
using NSubstitute;
using Oproto.FluentDynamoDb.Requests;
using Oproto.FluentDynamoDb.Requests.Extensions;

namespace Oproto.FluentDynamoDb.UnitTests.Requests;

/// <summary>
/// Focused integration tests for Task 7: Create integration tests for builder functionality.
/// These tests specifically address the task requirements:
/// - Test all request builders work correctly with extension methods
/// - Verify mixed usage of old parameter style and new format strings
/// - Test complex expressions with multiple parameters and formats
/// - Validate backward compatibility with existing usage patterns
/// Requirements: 2.1, 2.2, 2.3, 3.4
/// </summary>
public class Task7FocusedIntegrationTests
{
    private readonly IAmazonDynamoDB _mockClient = Substitute.For<IAmazonDynamoDB>();

    #region Test all request builders work correctly with extension methods

    [Fact]
    public void QueryRequestBuilder_WithExtensionMethods_ShouldWorkCorrectly()
    {
        // Arrange & Act
        var builder = new QueryRequestBuilder(_mockClient);
        var request = builder
            .ForTable("TestTable")
            .Where("pk = :pk")
            .WithValue(":pk", "USER#123")
            .WithAttribute("#status", "status")
            .WithFilter("#status = :status")
            .WithValue(":status", "ACTIVE")
            .Take(10)
            .UsingConsistentRead()
            .ToQueryRequest();

        // Assert - Verify extension methods work correctly
        request.Should().NotBeNull();
        request.TableName.Should().Be("TestTable");
        request.KeyConditionExpression.Should().Be("pk = :pk");
        request.FilterExpression.Should().Be("#status = :status");
        request.ExpressionAttributeValues.Should().ContainKey(":pk");
        request.ExpressionAttributeValues[":pk"].S.Should().Be("USER#123");
        request.ExpressionAttributeValues.Should().ContainKey(":status");
        request.ExpressionAttributeValues[":status"].S.Should().Be("ACTIVE");
        request.ExpressionAttributeNames.Should().ContainKey("#status");
        request.ExpressionAttributeNames["#status"].Should().Be("status");
        request.Limit.Should().Be(10);
        request.ConsistentRead.Should().BeTrue();
    }

    [Fact]
    public void GetItemRequestBuilder_WithExtensionMethods_ShouldWorkCorrectly()
    {
        // Arrange & Act
        var builder = new GetItemRequestBuilder(_mockClient);
        var request = builder
            .ForTable("TestTable")
            .WithKey("pk", "USER#123", "sk", "profile")
            .WithAttribute("#name", "name")
            .WithAttribute("#email", "email")
            .WithProjection("#name, #email, createdAt")
            .UsingConsistentRead()
            .ToGetItemRequest();

        // Assert - Verify extension methods work correctly
        request.Should().NotBeNull();
        request.TableName.Should().Be("TestTable");
        request.Key.Should().ContainKey("pk");
        request.Key["pk"].S.Should().Be("USER#123");
        request.Key.Should().ContainKey("sk");
        request.Key["sk"].S.Should().Be("profile");
        request.ExpressionAttributeNames.Should().ContainKey("#name");
        request.ExpressionAttributeNames["#name"].Should().Be("name");
        request.ExpressionAttributeNames.Should().ContainKey("#email");
        request.ExpressionAttributeNames["#email"].Should().Be("email");
        request.ProjectionExpression.Should().Be("#name, #email, createdAt");
        request.ConsistentRead.Should().BeTrue();
    }

    [Fact]
    public void PutItemRequestBuilder_WithExtensionMethods_ShouldWorkCorrectly()
    {
        // Arrange
        var item = new Dictionary<string, AttributeValue>
        {
            ["pk"] = new AttributeValue { S = "USER#123" },
            ["sk"] = new AttributeValue { S = "profile" },
            ["name"] = new AttributeValue { S = "John Doe" },
            ["version"] = new AttributeValue { N = "1" }
        };

        // Act
        var builder = new PutItemRequestBuilder(_mockClient);
        var request = builder
            .ForTable("TestTable")
            .WithItem(item)
            .Where("attribute_not_exists(pk) OR version = :expectedVersion")
            .WithValue(":expectedVersion", 1)
            .WithAttribute("#name", "name")
            .ReturnAllOldValues()
            .ToPutItemRequest();

        // Assert - Verify extension methods work correctly
        request.Should().NotBeNull();
        request.TableName.Should().Be("TestTable");
        request.Item.Should().BeEquivalentTo(item);
        request.ConditionExpression.Should().Be("attribute_not_exists(pk) OR version = :expectedVersion");
        request.ExpressionAttributeValues.Should().ContainKey(":expectedVersion");
        request.ExpressionAttributeValues[":expectedVersion"].N.Should().Be("1");
        request.ExpressionAttributeNames.Should().ContainKey("#name");
        request.ExpressionAttributeNames["#name"].Should().Be("name");
        request.ReturnValues.Should().Be(ReturnValue.ALL_OLD);
    }

    [Fact]
    public void UpdateItemRequestBuilder_WithExtensionMethods_ShouldWorkCorrectly()
    {
        // Arrange & Act
        var builder = new UpdateItemRequestBuilder(_mockClient);
        var request = builder
            .ForTable("TestTable")
            .WithKey("pk", "USER#123", "sk", "profile")
            .Where("version = :currentVersion")
            .WithValue(":currentVersion", 1)
            .WithValue(":newName", "Jane Doe")
            .WithValue(":newVersion", 2)
            .WithAttribute("#name", "name")
            .WithAttribute("#version", "version")
            .ReturnUpdatedNewValues()
            .ToUpdateItemRequest();

        // Assert - Verify extension methods work correctly
        request.Should().NotBeNull();
        request.TableName.Should().Be("TestTable");
        request.Key.Should().ContainKey("pk");
        request.Key["pk"].S.Should().Be("USER#123");
        request.Key.Should().ContainKey("sk");
        request.Key["sk"].S.Should().Be("profile");
        request.ConditionExpression.Should().Be("version = :currentVersion");
        request.ExpressionAttributeValues.Should().ContainKey(":currentVersion");
        request.ExpressionAttributeValues[":currentVersion"].N.Should().Be("1");
        request.ExpressionAttributeValues.Should().ContainKey(":newName");
        request.ExpressionAttributeValues[":newName"].S.Should().Be("Jane Doe");
        request.ExpressionAttributeValues.Should().ContainKey(":newVersion");
        request.ExpressionAttributeValues[":newVersion"].N.Should().Be("2");
        request.ExpressionAttributeNames.Should().ContainKey("#name");
        request.ExpressionAttributeNames["#name"].Should().Be("name");
        request.ExpressionAttributeNames.Should().ContainKey("#version");
        request.ExpressionAttributeNames["#version"].Should().Be("version");
        request.ReturnValues.Should().Be(ReturnValue.UPDATED_NEW);
    }

    [Fact]
    public void DeleteItemRequestBuilder_WithExtensionMethods_ShouldWorkCorrectly()
    {
        // Arrange & Act
        var builder = new DeleteItemRequestBuilder(_mockClient);
        var request = builder
            .ForTable("TestTable")
            .WithKey("pk", "USER#123", "sk", "profile")
            .Where("attribute_exists(pk) AND version = :expectedVersion")
            .WithValue(":expectedVersion", 1)
            .WithAttribute("#status", "status")
            .ReturnAllOldValues()
            .ToDeleteItemRequest();

        // Assert - Verify extension methods work correctly
        request.Should().NotBeNull();
        request.TableName.Should().Be("TestTable");
        request.Key.Should().ContainKey("pk");
        request.Key["pk"].S.Should().Be("USER#123");
        request.Key.Should().ContainKey("sk");
        request.Key["sk"].S.Should().Be("profile");
        request.ConditionExpression.Should().Be("attribute_exists(pk) AND version = :expectedVersion");
        request.ExpressionAttributeValues.Should().ContainKey(":expectedVersion");
        request.ExpressionAttributeValues[":expectedVersion"].N.Should().Be("1");
        request.ExpressionAttributeNames.Should().ContainKey("#status");
        request.ExpressionAttributeNames["#status"].Should().Be("status");
        request.ReturnValues.Should().Be(ReturnValue.ALL_OLD);
    }

    [Fact]
    public void ScanRequestBuilder_WithExtensionMethods_ShouldWorkCorrectly()
    {
        // Arrange & Act
        var builder = new ScanRequestBuilder(_mockClient);
        var request = builder
            .ForTable("TestTable")
            .WithFilter("#status = :status AND amount > :minAmount")
            .WithValue(":status", "ACTIVE")
            .WithValue(":minAmount", 100.50m)
            .WithAttribute("#status", "status")
            .WithProjection("#status, amount, createdAt")
            .Take(25)
            .ToScanRequest();

        // Assert - Verify extension methods work correctly
        request.Should().NotBeNull();
        request.TableName.Should().Be("TestTable");
        request.FilterExpression.Should().Be("#status = :status AND amount > :minAmount");
        request.ExpressionAttributeValues.Should().ContainKey(":status");
        request.ExpressionAttributeValues[":status"].S.Should().Be("ACTIVE");
        request.ExpressionAttributeValues.Should().ContainKey(":minAmount");
        request.ExpressionAttributeValues[":minAmount"].N.Should().Be("100.5");
        request.ExpressionAttributeNames.Should().ContainKey("#status");
        request.ExpressionAttributeNames["#status"].Should().Be("status");
        request.ProjectionExpression.Should().Be("#status, amount, createdAt");
        request.Limit.Should().Be(25);
    }

    #endregion

    #region Verify mixed usage of old parameter style and new format strings

    [Fact]
    public void QueryRequestBuilder_WithMixedParameterStyles_ShouldWorkCorrectly()
    {
        // Arrange & Act - Mix format strings with traditional parameter style
        var builder = new QueryRequestBuilder(_mockClient);
        var request = builder
            .ForTable("TestTable")
            .Where("pk = {0} AND sk BETWEEN :minDate AND :maxDate", "USER#123")
            .WithValue(":minDate", "2024-01-01")
            .WithValue(":maxDate", "2024-12-31")
            .WithFilter("#status = :status AND amount > :minAmount")
            .WithValue(":status", "ACTIVE")
            .WithAttribute("#status", "status")
            .ToQueryRequest();

        // Assert - Both parameter styles should work together
        request.Should().NotBeNull();
        request.KeyConditionExpression.Should().Be("pk = :p0 AND sk BETWEEN :minDate AND :maxDate");
        request.FilterExpression.Should().Be("#status = :status AND amount > :minAmount");
        
        // Format string parameters
        request.ExpressionAttributeValues.Should().ContainKey(":p0");
        request.ExpressionAttributeValues[":p0"].S.Should().Be("USER#123");
        
        // Traditional parameters
        request.ExpressionAttributeValues.Should().ContainKey(":minDate");
        request.ExpressionAttributeValues[":minDate"].S.Should().Be("2024-01-01");
        request.ExpressionAttributeValues.Should().ContainKey(":maxDate");
        request.ExpressionAttributeValues[":maxDate"].S.Should().Be("2024-12-31");
        request.ExpressionAttributeValues.Should().ContainKey(":status");
        request.ExpressionAttributeValues[":status"].S.Should().Be("ACTIVE");
        request.ExpressionAttributeValues.Should().ContainKey(":minAmount");
        request.ExpressionAttributeValues[":minAmount"].N.Should().Be("100.5");
    }

    [Fact]
    public void PutItemRequestBuilder_WithMixedParameterStyles_ShouldWorkCorrectly()
    {
        // Arrange
        var item = new Dictionary<string, AttributeValue>
        {
            ["pk"] = new AttributeValue { S = "USER#123" },
            ["version"] = new AttributeValue { N = "1" },
            ["status"] = new AttributeValue { S = "ACTIVE" }
        };

        // Act - Mix format strings with traditional parameters
        var builder = new PutItemRequestBuilder(_mockClient);
        var request = builder
            .ForTable("TestTable")
            .WithItem(item)
            .Where("(attribute_not_exists(pk) OR version = {0}) AND #status <> :oldStatus", 1)
            .WithValue(":oldStatus", "DELETED")
            .WithAttribute("#status", "status")
            .ToPutItemRequest();

        // Assert - Both parameter styles should work together
        request.Should().NotBeNull();
        request.ConditionExpression.Should().Be("(attribute_not_exists(pk) OR version = :p0) AND #status <> :oldStatus");
        
        // Format string parameter
        request.ExpressionAttributeValues.Should().ContainKey(":p0");
        request.ExpressionAttributeValues[":p0"].N.Should().Be("1");
        
        // Traditional parameter
        request.ExpressionAttributeValues.Should().ContainKey(":oldStatus");
        request.ExpressionAttributeValues[":oldStatus"].S.Should().Be("DELETED");
        
        request.ExpressionAttributeNames.Should().ContainKey("#status");
        request.ExpressionAttributeNames["#status"].Should().Be("status");
    }

    [Fact]
    public void UpdateItemRequestBuilder_WithMixedParameterStyles_ShouldWorkCorrectly()
    {
        // Act - Mix format strings with traditional parameters
        var builder = new UpdateItemRequestBuilder(_mockClient);
        var request = builder
            .ForTable("TestTable")
            .WithKey("id", "123")
            .Where("version = {0} AND #status = :currentStatus", 1)
            .WithValue(":currentStatus", "ACTIVE")
            .WithValue(":newVersion", 2)
            .WithAttribute("#status", "status")
            .ToUpdateItemRequest();

        // Assert - Both parameter styles should work together
        request.Should().NotBeNull();
        request.ConditionExpression.Should().Be("version = :p0 AND #status = :currentStatus");
        
        // Format string parameter
        request.ExpressionAttributeValues.Should().ContainKey(":p0");
        request.ExpressionAttributeValues[":p0"].N.Should().Be("1");
        
        // Traditional parameters
        request.ExpressionAttributeValues.Should().ContainKey(":currentStatus");
        request.ExpressionAttributeValues[":currentStatus"].S.Should().Be("ACTIVE");
        request.ExpressionAttributeValues.Should().ContainKey(":newVersion");
        request.ExpressionAttributeValues[":newVersion"].N.Should().Be("2");
    }

    [Fact]
    public void DeleteItemRequestBuilder_WithMixedParameterStyles_ShouldWorkCorrectly()
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

        // Assert - Both parameter styles should work together
        request.Should().NotBeNull();
        request.ConditionExpression.Should().Be("version = :p0 AND #status IN (:status1, :status2)");
        
        // Format string parameter
        request.ExpressionAttributeValues.Should().ContainKey(":p0");
        request.ExpressionAttributeValues[":p0"].N.Should().Be("1");
        
        // Traditional parameters
        request.ExpressionAttributeValues.Should().ContainKey(":status1");
        request.ExpressionAttributeValues[":status1"].S.Should().Be("ACTIVE");
        request.ExpressionAttributeValues.Should().ContainKey(":status2");
        request.ExpressionAttributeValues[":status2"].S.Should().Be("PENDING");
    }

    #endregion

    #region Test complex expressions with multiple parameters and formats

    [Fact]
    public void QueryRequestBuilder_WithComplexFormatExpressions_ShouldHandleMultipleFormats()
    {
        // Arrange - Complex data types with various formats
        var testDate = new DateTime(2024, 1, 15, 10, 30, 0, DateTimeKind.Utc);
        var testDecimal = 99.99m;

        // Act - Complex expression with multiple format specifiers
        var builder = new QueryRequestBuilder(_mockClient);
        var request = builder
            .ForTable("TestTable")
            .Where("pk = {0} AND sk BETWEEN {1:o} AND {2:o}", "USER#123", testDate.AddDays(-30), testDate)
            .WithFilter("amount >= :amount AND #status = :filterStatus")
            .WithValue(":amount", testDecimal)
            .WithValue(":filterStatus", "ACTIVE")
            .WithAttribute("#status", "status")
            .ToQueryRequest();

        // Assert - Complex expressions should work with multiple formats
        request.Should().NotBeNull();
        request.KeyConditionExpression.Should().Be("pk = :p0 AND sk BETWEEN :p1 AND :p2");
        request.FilterExpression.Should().Be("amount >= :amount AND #status = :filterStatus");
        
        // Verify all parameters are correctly formatted
        request.ExpressionAttributeValues.Should().ContainKey(":p0");
        request.ExpressionAttributeValues[":p0"].S.Should().Be("USER#123");
        
        request.ExpressionAttributeValues.Should().ContainKey(":p1");
        request.ExpressionAttributeValues[":p1"].S.Should().Be("2023-12-16T10:30:00.0000000Z");
        
        request.ExpressionAttributeValues.Should().ContainKey(":p2");
        request.ExpressionAttributeValues[":p2"].S.Should().Be("2024-01-15T10:30:00.0000000Z");
        
        request.ExpressionAttributeValues.Should().ContainKey(":amount");
        request.ExpressionAttributeValues[":amount"].N.Should().Be("99.99");
        
        request.ExpressionAttributeValues.Should().ContainKey(":filterStatus");
        request.ExpressionAttributeValues[":filterStatus"].S.Should().Be("ACTIVE");
    }

    [Fact]
    public void PutItemRequestBuilder_WithComplexFormatExpressions_ShouldHandleAdvancedConditions()
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

        // Assert - Complex conditions should work with multiple parameter types
        request.Should().NotBeNull();
        request.ConditionExpression.Should().Be("(attribute_not_exists(version) OR version = :p0) AND amount <= :p1 AND #status IN (:p2, :p3)");
        
        request.ExpressionAttributeValues.Should().ContainKey(":p0");
        request.ExpressionAttributeValues[":p0"].N.Should().Be("0");
        
        request.ExpressionAttributeValues.Should().ContainKey(":p1");
        request.ExpressionAttributeValues[":p1"].S.Should().Be("1000.00");
        
        request.ExpressionAttributeValues.Should().ContainKey(":p2");
        request.ExpressionAttributeValues[":p2"].S.Should().Be("DRAFT");
        
        request.ExpressionAttributeValues.Should().ContainKey(":p3");
        request.ExpressionAttributeValues[":p3"].S.Should().Be("PENDING");
    }

    [Fact]
    public void ScanRequestBuilder_WithComplexFormatExpressions_ShouldHandleNestedConditions()
    {
        // Arrange
        var startDate = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var endDate = new DateTime(2024, 12, 31, 23, 59, 59, DateTimeKind.Utc);
        var minAmount = 1000.50m;
        var maxAmount = 5000.75m;

        // Act - Complex nested conditions with multiple formats
        var builder = new ScanRequestBuilder(_mockClient);
        var request = builder
            .ForTable("TestTable")
            .WithFilter("(createdAt BETWEEN :startDate AND :endDate) AND (amount BETWEEN :minAmount AND :maxAmount) AND (#status = :status1 OR #status = :status2)")
            .WithValue(":startDate", startDate.ToString("o"))
            .WithValue(":endDate", endDate.ToString("o"))
            .WithValue(":minAmount", minAmount)
            .WithValue(":maxAmount", maxAmount)
            .WithValue(":status1", "ACTIVE")
            .WithValue(":status2", "PENDING")
            .WithAttribute("#status", "status")
            .ToScanRequest();

        // Assert - Complex nested conditions should work
        request.Should().NotBeNull();
        request.FilterExpression.Should().Be("(createdAt BETWEEN :startDate AND :endDate) AND (amount BETWEEN :minAmount AND :maxAmount) AND (#status = :status1 OR #status = :status2)");
        
        request.ExpressionAttributeValues.Should().ContainKey(":startDate");
        request.ExpressionAttributeValues[":startDate"].S.Should().Be("2024-01-01T00:00:00.0000000Z");
        
        request.ExpressionAttributeValues.Should().ContainKey(":endDate");
        request.ExpressionAttributeValues[":endDate"].S.Should().Be("2024-12-31T23:59:59.0000000Z");
        
        request.ExpressionAttributeValues.Should().ContainKey(":minAmount");
        request.ExpressionAttributeValues[":minAmount"].N.Should().Be("1000.5");
        
        request.ExpressionAttributeValues.Should().ContainKey(":maxAmount");
        request.ExpressionAttributeValues[":maxAmount"].N.Should().Be("5000.75");
        
        request.ExpressionAttributeValues.Should().ContainKey(":status1");
        request.ExpressionAttributeValues[":status1"].S.Should().Be("ACTIVE");
        
        request.ExpressionAttributeValues.Should().ContainKey(":status2");
        request.ExpressionAttributeValues[":status2"].S.Should().Be("PENDING");
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
            .WithFilter("#status = :status AND #amount > :minAmount")
            .WithValue(":status", "ACTIVE")
            .WithValue(":minAmount", 100.0m)
            .Take(25)
            .UsingConsistentRead()
            .OrderDescending()
            .ReturnTotalConsumedCapacity()
            .ToQueryRequest();

        // Verify all traditional functionality works exactly as before
        queryRequest.TableName.Should().Be("Users");
        queryRequest.KeyConditionExpression.Should().Be("pk = :pk AND begins_with(sk, :prefix)");
        queryRequest.FilterExpression.Should().Be("#status = :status AND #amount > :minAmount");
        queryRequest.ExpressionAttributeValues[":pk"].S.Should().Be("USER#123");
        queryRequest.ExpressionAttributeValues[":prefix"].S.Should().Be("ORDER#");
        queryRequest.ExpressionAttributeValues[":status"].S.Should().Be("ACTIVE");
        queryRequest.ExpressionAttributeValues[":minAmount"].N.Should().Be("100");
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
    public void Builders_WithInvalidFormatStrings_ShouldThrowAppropriateExceptions()
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

        // Null arguments
        var deleteBuilder = new DeleteItemRequestBuilder(_mockClient);
        var act4 = () => deleteBuilder.Where("pk = {0}", (object[])null!);
        act4.Should().Throw<ArgumentNullException>();

        // Out of range parameter index
        var updateBuilder = new UpdateItemRequestBuilder(_mockClient);
        var act5 = () => updateBuilder.Where("pk = {5}", "value");
        act5.Should().Throw<ArgumentException>()
            .WithMessage("*parameter index 5 but only 1 arguments were provided*");
    }

    [Fact]
    public void Builders_WithNullValues_ShouldHandleGracefully()
    {
        // Test how builders handle null values in format strings
        var builder = new QueryRequestBuilder(_mockClient);
        var request = builder
            .ForTable("TestTable")
            .Where("pk = {0} AND sk = {1}", "USER#123", (string?)null)
            .ToQueryRequest();

        // Null values should be converted to NULL AttributeValue
        request.ExpressionAttributeValues.Should().ContainKey(":p1");
        request.ExpressionAttributeValues[":p1"].NULL.Should().BeTrue();
    }

    [Fact]
    public void Builders_WithVariousDataTypes_ShouldFormatCorrectly()
    {
        // Test various data types with format strings
        var testDate = new DateTime(2024, 1, 15, 10, 30, 0, DateTimeKind.Utc);
        var testGuid = Guid.Parse("12345678-1234-1234-1234-123456789012");
        var testEnum = ReturnValue.ALL_OLD;
        
        var builder = new QueryRequestBuilder(_mockClient);
        var request = builder
            .ForTable("TestTable")
            .Where("pk = {0} AND created = {1:o} AND requestId = {2:D} AND status = {3} AND amount = {4:F2}", 
                "USER#123", testDate, testGuid, testEnum, 99.99m)
            .ToQueryRequest();

        // Verify all data types are formatted correctly
        request.ExpressionAttributeValues[":p0"].S.Should().Be("USER#123");
        request.ExpressionAttributeValues[":p1"].S.Should().Be("2024-01-15T10:30:00.0000000Z");
        request.ExpressionAttributeValues[":p2"].S.Should().Be("12345678-1234-1234-1234-123456789012");
        request.ExpressionAttributeValues[":p3"].S.Should().Be("ALL_OLD");
        request.ExpressionAttributeValues[":p4"].S.Should().Be("99.99");
    }

    #endregion

    #region Transaction and Batch Builder Integration

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