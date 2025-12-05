using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using AwesomeAssertions;
using NSubstitute;
using Oproto.FluentDynamoDb.Requests;
using Oproto.FluentDynamoDb.Requests.Extensions;

namespace Oproto.FluentDynamoDb.UnitTests.Requests;

/// <summary>
/// Comprehensive integration tests for all request builders working with extension methods.
/// These tests specifically address task 7 requirements:
/// - Test all request builders work correctly with extension methods
/// - Verify mixed usage of old parameter style and new format strings
/// - Test complex expressions with multiple parameters and formats
/// - Validate backward compatibility with existing usage patterns
/// </summary>
public class ComprehensiveBuilderIntegrationTests
{
    private class TestEntity { }
    private readonly IAmazonDynamoDB _mockClient = Substitute.For<IAmazonDynamoDB>();

    #region All Request Builders with Extension Methods

    [Fact]
    public void AllRequestBuilders_WithExtensionMethods_ShouldWorkCorrectly()
    {
        // Test QueryRequestBuilder
        var queryBuilder = new QueryRequestBuilder<TestEntity>(_mockClient);
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

        // Test GetItemRequestBuilder
        var getBuilder = new GetItemRequestBuilder<TestEntity>(_mockClient);
        var getRequest = getBuilder
            .ForTable("TestTable")
            .WithKey("pk", "USER#123", "sk", "profile")
            .WithAttribute("#name", "name")
            .ToGetItemRequest();

        getRequest.Should().NotBeNull();
        getRequest.Key["pk"].S.Should().Be("USER#123");
        getRequest.Key["sk"].S.Should().Be("profile");
        getRequest.ExpressionAttributeNames["#name"].Should().Be("name");

        // Test PutItemRequestBuilder
        var putBuilder = new PutItemRequestBuilder<TestEntity>(_mockClient);
        var putRequest = putBuilder
            .ForTable("TestTable")
            .WithItem(new Dictionary<string, AttributeValue> { ["id"] = new AttributeValue { S = "123" } })
            .Where("attribute_not_exists(id)")
            .WithValue(":version", 1)
            .ToPutItemRequest();

        putRequest.Should().NotBeNull();
        putRequest.ConditionExpression.Should().Be("attribute_not_exists(id)");
        putRequest.ExpressionAttributeValues[":version"].N.Should().Be("1");

        // Test UpdateItemRequestBuilder
        var updateBuilder = new UpdateItemRequestBuilder<TestEntity>(_mockClient);
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

        // Test DeleteItemRequestBuilder
        var deleteBuilder = new DeleteItemRequestBuilder<TestEntity>(_mockClient);
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

        // Test ScanRequestBuilder
        var scanBuilder = new ScanRequestBuilder<TestEntity>(_mockClient);
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

    #region Mixed Parameter Style Usage Tests

    [Fact]
    public void QueryBuilder_WithMixedParameterStyles_ShouldHandleComplexScenarios()
    {
        // Test mixing format strings with traditional parameter style
        var builder = new QueryRequestBuilder<TestEntity>(_mockClient);
        var request = builder
            .ForTable("TestTable")
            .Where("pk = {0} AND sk BETWEEN :minDate AND :maxDate", "USER#123")
            .WithValue(":minDate", "2024-01-01")
            .WithValue(":maxDate", "2024-12-31")
            .WithFilter("#status = :status AND amount > :minAmount")
            .WithValue(":status", "ACTIVE")
            .WithValue(":minAmount", 100.5m)
            .WithAttribute("#status", "status")
            .ToQueryRequest();

        // Assert key condition expression
        request.KeyConditionExpression.Should().Be("pk = :p0 AND sk BETWEEN :minDate AND :maxDate");
        request.ExpressionAttributeValues.Should().ContainKey(":p0");
        request.ExpressionAttributeValues[":p0"].S.Should().Be("USER#123");
        request.ExpressionAttributeValues.Should().ContainKey(":minDate");
        request.ExpressionAttributeValues[":minDate"].S.Should().Be("2024-01-01");
        request.ExpressionAttributeValues.Should().ContainKey(":maxDate");
        request.ExpressionAttributeValues[":maxDate"].S.Should().Be("2024-12-31");

        // Assert filter expression
        request.FilterExpression.Should().Be("#status = :status AND amount > :minAmount");
        request.ExpressionAttributeValues.Should().ContainKey(":status");
        request.ExpressionAttributeValues[":status"].S.Should().Be("ACTIVE");
        request.ExpressionAttributeValues.Should().ContainKey(":minAmount");
        request.ExpressionAttributeValues[":minAmount"].N.Should().Be("100.5");
    }

    [Fact]
    public void PutItemBuilder_WithMixedParameterStyles_ShouldWorkCorrectly()
    {
        // Arrange
        var item = new Dictionary<string, AttributeValue>
        {
            ["id"] = new AttributeValue { S = "123" },
            ["version"] = new AttributeValue { N = "1" },
            ["status"] = new AttributeValue { S = "ACTIVE" }
        };

        // Act - Mix format strings with traditional parameters
        var builder = new PutItemRequestBuilder<TestEntity>(_mockClient);
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
        var builder = new UpdateItemRequestBuilder<TestEntity>(_mockClient);
        var request = builder
            .ForTable("TestTable")
            .WithKey("id", "123")
            .Where("version = {0} AND #status = :currentStatus", 1)
            .WithValue(":currentStatus", "ACTIVE")
            .WithValue(":newVersion", 2)
            .WithValue(":timestamp", DateTime.UtcNow.ToString("o"))
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
        var builder = new DeleteItemRequestBuilder<TestEntity>(_mockClient);
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

    #region Complex Expressions with Multiple Parameters and Formats

    [Fact]
    public void QueryBuilder_WithComplexFormatExpressions_ShouldHandleMultipleFormats()
    {
        // Arrange - Complex data types with various formats
        var testDate = new DateTime(2024, 1, 15, 10, 30, 0, DateTimeKind.Utc);
        var testDecimal = 99.99m;
        var testEnum = ReturnValue.ALL_OLD;
        var testGuid = Guid.Parse("12345678-1234-1234-1234-123456789012");

        // Act - Complex expression with multiple format specifiers
        var builder = new QueryRequestBuilder<TestEntity>(_mockClient);
        var request = builder
            .ForTable("TestTable")
            .Where("pk = {0} AND sk BETWEEN {1:o} AND {2:o}", "USER#123", testDate.AddDays(-30), testDate)
            .WithFilter("amount >= :amount AND #status = :status AND requestId = :requestId")
            .WithValue(":amount", testDecimal)
            .WithValue(":status", testEnum.ToString())
            .WithValue(":requestId", testGuid.ToString("D"))
            .WithAttribute("#status", "status")
            .ToQueryRequest();

        // Assert key condition
        request.KeyConditionExpression.Should().Be("pk = :p0 AND sk BETWEEN :p1 AND :p2");
        request.ExpressionAttributeValues[":p0"].S.Should().Be("USER#123");
        request.ExpressionAttributeValues[":p1"].S.Should().Be("2023-12-16T10:30:00.0000000Z");
        request.ExpressionAttributeValues[":p2"].S.Should().Be("2024-01-15T10:30:00.0000000Z");

        // Assert filter expression
        request.FilterExpression.Should().Be("amount >= :amount AND #status = :status AND requestId = :requestId");
        request.ExpressionAttributeValues[":amount"].N.Should().Be("99.99");
        request.ExpressionAttributeValues[":status"].S.Should().Be("ALL_OLD");
        request.ExpressionAttributeValues[":requestId"].S.Should().Be("12345678-1234-1234-1234-123456789012");
    }

    [Fact]
    public void ScanBuilder_WithComplexFormatExpressions_ShouldHandleNestedConditions()
    {
        // Arrange
        var startDate = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var endDate = new DateTime(2024, 12, 31, 23, 59, 59, DateTimeKind.Utc);
        var minAmount = 1000.50m;
        var maxAmount = 5000.75m;

        // Act - Complex nested conditions with multiple formats
        var builder = new ScanRequestBuilder<TestEntity>(_mockClient);
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

        // Assert
        request.FilterExpression.Should().Be("(createdAt BETWEEN :startDate AND :endDate) AND (amount BETWEEN :minAmount AND :maxAmount) AND (#status = :status1 OR #status = :status2)");
        request.ExpressionAttributeValues[":startDate"].S.Should().Be("2024-01-01T00:00:00.0000000Z");
        request.ExpressionAttributeValues[":endDate"].S.Should().Be("2024-12-31T23:59:59.0000000Z");
        request.ExpressionAttributeValues[":minAmount"].N.Should().Be("1000.50");
        request.ExpressionAttributeValues[":maxAmount"].N.Should().Be("5000.75");
        request.ExpressionAttributeValues[":status1"].S.Should().Be("ACTIVE");
        request.ExpressionAttributeValues[":status2"].S.Should().Be("PENDING");
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
        var builder = new PutItemRequestBuilder<TestEntity>(_mockClient);
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
        request.ExpressionAttributeValues[":p1"].N.Should().Be("1000.00");
        request.ExpressionAttributeValues[":p2"].S.Should().Be("DRAFT");
        request.ExpressionAttributeValues[":p3"].S.Should().Be("PENDING");
    }

    #endregion

    #region Backward Compatibility Validation

    [Fact]
    public void AllBuilders_ExistingUsagePatterns_ShouldRemainUnchanged()
    {
        // This test validates that all existing usage patterns continue to work
        // exactly as they did before the refactoring

        // Query - Traditional usage pattern
        var queryBuilder = new QueryRequestBuilder<TestEntity>(_mockClient);
        var queryRequest = queryBuilder
            .ForTable("Users")
            .Where("pk = :pk AND begins_with(sk, :prefix)")
            .WithValue(":pk", "USER#123")
            .WithValue(":prefix", "ORDER#")
            .WithAttribute("#status", "status")
            .WithAttribute("#amount", "amount")
            .WithFilter("#status = :status AND #amount > :minAmount")
            .WithValue(":status", "ACTIVE")
            .WithValue(":minAmount", 100m)
            .Take(25)
            .UsingConsistentRead()
            .OrderDescending()
            .ReturnTotalConsumedCapacity()
            .ToQueryRequest();

        // Verify all traditional functionality works
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

        // Get - Traditional usage pattern
        var getBuilder = new GetItemRequestBuilder<TestEntity>(_mockClient);
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

        // Put - Traditional usage pattern
        var putBuilder = new PutItemRequestBuilder<TestEntity>(_mockClient);
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
    public void AllBuilders_MethodChaining_ShouldReturnCorrectBuilderTypes()
    {
        // Verify that method chaining continues to work and returns correct types
        // This ensures the Self property implementation is working correctly

        // Test QueryRequestBuilder chaining
        var queryBuilder = new QueryRequestBuilder<TestEntity>(_mockClient);
        var chainedQuery = queryBuilder
            .ForTable("TestTable")
            .Where("pk = :pk")
            .WithValue(":pk", "test")
            .WithAttribute("#attr", "attribute")
            .Take(10)
            .UsingConsistentRead();

        chainedQuery.Should().BeOfType<QueryRequestBuilder<TestEntity>>();
        chainedQuery.Should().BeSameAs(queryBuilder);

        // Test GetItemRequestBuilder chaining
        var getBuilder = new GetItemRequestBuilder<TestEntity>(_mockClient);
        var chainedGet = getBuilder
            .ForTable("TestTable")
            .WithKey("id", "123")
            .WithAttribute("#name", "name")
            .UsingConsistentRead();

        chainedGet.Should().BeOfType<GetItemRequestBuilder<TestEntity>>();
        chainedGet.Should().BeSameAs(getBuilder);

        // Test PutItemRequestBuilder chaining
        var putBuilder = new PutItemRequestBuilder<TestEntity>(_mockClient);
        var chainedPut = putBuilder
            .ForTable("TestTable")
            .WithItem(new Dictionary<string, AttributeValue>())
            .Where("condition")
            .WithValue(":val", "test")
            .ReturnAllOldValues();

        chainedPut.Should().BeOfType<PutItemRequestBuilder<TestEntity>>();
        chainedPut.Should().BeSameAs(putBuilder);

        // Test UpdateItemRequestBuilder chaining
        var updateBuilder = new UpdateItemRequestBuilder<TestEntity>(_mockClient);
        var chainedUpdate = updateBuilder
            .ForTable("TestTable")
            .WithKey("id", "123")
            .Where("condition")
            .WithValue(":val", "test")
            .ReturnUpdatedNewValues();

        chainedUpdate.Should().BeOfType<UpdateItemRequestBuilder<TestEntity>>();
        chainedUpdate.Should().BeSameAs(updateBuilder);

        // Test DeleteItemRequestBuilder chaining
        var deleteBuilder = new DeleteItemRequestBuilder<TestEntity>(_mockClient);
        var chainedDelete = deleteBuilder
            .ForTable("TestTable")
            .WithKey("id", "123")
            .Where("condition")
            .WithValue(":val", "test")
            .ReturnAllOldValues();

        chainedDelete.Should().BeOfType<DeleteItemRequestBuilder<TestEntity>>();
        chainedDelete.Should().BeSameAs(deleteBuilder);

        // Test ScanRequestBuilder chaining
        var scanBuilder = new ScanRequestBuilder<TestEntity>(_mockClient);
        var chainedScan = scanBuilder
            .ForTable("TestTable")
            .WithFilter("condition")
            .WithValue(":val", "test")
            .WithAttribute("#attr", "attribute")
            .Take(10);

        chainedScan.Should().BeOfType<ScanRequestBuilder<TestEntity>>();
        chainedScan.Should().BeSameAs(scanBuilder);
    }

    #endregion

    #region Advanced Integration Scenarios

    [Fact]
    public void QueryBuilder_WithAdvancedFeatures_ShouldIntegrateCorrectly()
    {
        // Test advanced Query features with extension methods
        var builder = new QueryRequestBuilder<TestEntity>(_mockClient);
        var request = builder
            .ForTable("TestTable")
            .UsingIndex("GSI1")
            .Where("gsi1pk = {0} AND gsi1sk > {1:o}", "CATEGORY#electronics", DateTime.UtcNow.AddDays(-7))
            .WithFilter("price BETWEEN {0} AND {1} AND #brand = {2}", 100m, 500m, "Apple")
            .WithProjection("#name, price, #brand, createdAt")
            .WithAttribute("#name", "name")
            .WithAttribute("#brand", "brand")
            .Take(20)
            .OrderDescending()
            .ReturnIndexConsumedCapacity()
            .ToQueryRequest();

        // Assert all features work together
        request.TableName.Should().Be("TestTable");
        request.IndexName.Should().Be("GSI1");
        request.KeyConditionExpression.Should().Be("gsi1pk = :p0 AND gsi1sk > :p1");
        request.FilterExpression.Should().Be("price BETWEEN :p2 AND :p3 AND #brand = :p4");
        request.ProjectionExpression.Should().Be("#name, price, #brand, createdAt");
        request.Select.Should().Be(Select.SPECIFIC_ATTRIBUTES);
        request.Limit.Should().Be(20);
        request.ScanIndexForward.Should().BeFalse();
        request.ReturnConsumedCapacity.Should().Be(Amazon.DynamoDBv2.ReturnConsumedCapacity.INDEXES);

        // Verify all parameters are correctly set
        request.ExpressionAttributeValues[":p0"].S.Should().Be("CATEGORY#electronics");
        request.ExpressionAttributeValues[":p2"].N.Should().Be("100");
        request.ExpressionAttributeValues[":p3"].N.Should().Be("500");
        request.ExpressionAttributeValues[":p4"].S.Should().Be("Apple");
        request.ExpressionAttributeNames["#name"].Should().Be("name");
        request.ExpressionAttributeNames["#brand"].Should().Be("brand");
    }

    [Fact]
    public void ScanBuilder_WithAdvancedFeatures_ShouldIntegrateCorrectly()
    {
        // Test advanced Scan features with extension methods
        var builder = new ScanRequestBuilder<TestEntity>(_mockClient);
        var request = builder
            .ForTable("TestTable")
            .UsingIndex("GSI1")
            .WithFilter("(#status = :status1 OR #status = :status2) AND amount > :amount AND contains(tags, :tag)")
            .WithValue(":status1", "ACTIVE")
            .WithValue(":status2", "PENDING")
            .WithValue(":amount", 100.5m)
            .WithValue(":tag", "electronics")
            .WithProjection("id, #name, amount, tags, #status")
            .WithAttribute("#name", "name")
            .WithAttribute("#status", "status")
            .Take(50)
            .WithSegment(0, 4) // Parallel scan
            .ReturnTotalConsumedCapacity()
            .ToScanRequest();

        // Assert all features work together
        request.TableName.Should().Be("TestTable");
        request.IndexName.Should().Be("GSI1");
        request.FilterExpression.Should().Be("(#status = :status1 OR #status = :status2) AND amount > :amount AND contains(tags, :tag)");
        request.ProjectionExpression.Should().Be("id, #name, amount, tags, #status");
        request.Select.Should().Be(Select.SPECIFIC_ATTRIBUTES);
        request.Limit.Should().Be(50);
        request.Segment.Should().Be(0);
        request.TotalSegments.Should().Be(4);
        request.ReturnConsumedCapacity.Should().Be(Amazon.DynamoDBv2.ReturnConsumedCapacity.TOTAL);

        // Verify parameters
        request.ExpressionAttributeValues[":status1"].S.Should().Be("ACTIVE");
        request.ExpressionAttributeValues[":status2"].S.Should().Be("PENDING");
        request.ExpressionAttributeValues[":amount"].N.Should().Be("100.5");
        request.ExpressionAttributeValues[":tag"].S.Should().Be("electronics");
    }

    #endregion

    #region Error Handling and Edge Cases

    [Fact]
    public void AllBuilders_WithInvalidFormatStrings_ShouldThrowAppropriateExceptions()
    {
        // Test various error conditions across different builders

        // Empty format string
        var queryBuilder = new QueryRequestBuilder<TestEntity>(_mockClient);
        var act1 = () => queryBuilder.Where("", "value");
        act1.Should().Throw<ArgumentException>()
            .WithMessage("Format string cannot be null or empty.*");

        // Mismatched parameter count
        var putBuilder = new PutItemRequestBuilder<TestEntity>(_mockClient);
        var act2 = () => putBuilder.Where("pk = {0} AND sk = {1}", "onlyOneValue");
        act2.Should().Throw<ArgumentException>()
            .WithMessage("*parameter index 1 but only 1 arguments were provided*");



        // Null arguments
        var deleteBuilder = new DeleteItemRequestBuilder<TestEntity>(_mockClient);
        var act4 = () => deleteBuilder.Where("pk = {0}", (object[])null!);
        act4.Should().Throw<ArgumentNullException>();

        // Out of range parameter index - using QueryRequestBuilder instead since ScanRequestBuilder doesn't support format strings
        var queryBuilder2 = new QueryRequestBuilder<TestEntity>(_mockClient);
        var act5 = () => queryBuilder2.Where("pk = {5}", "value");
        act5.Should().Throw<ArgumentException>()
            .WithMessage("*parameter index 5 but only 1 arguments were provided*");
    }

    [Fact]
    public void AllBuilders_WithNullValues_ShouldHandleGracefully()
    {
        // Test how builders handle null values in format strings
        var builder = new QueryRequestBuilder<TestEntity>(_mockClient);
        var request = builder
            .ForTable("TestTable")
            .Where("pk = {0} AND sk = {1}", "USER#123", (string?)null)
            .ToQueryRequest();

        // Null values should be converted to NULL AttributeValue
        request.ExpressionAttributeValues[":p1"].NULL.Should().BeTrue();
    }

    #endregion
}
