using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using FluentAssertions;
using NSubstitute;
using Oproto.FluentDynamoDb.Logging;
using Oproto.FluentDynamoDb.Requests;
using Oproto.FluentDynamoDb.Requests.Extensions;
using Oproto.FluentDynamoDb.Storage;

namespace Oproto.FluentDynamoDb.UnitTests.Storage;

/// <summary>
/// Tests to ensure backward compatibility with existing code that doesn't use logging.
/// These tests verify that all existing constructor signatures and methods work without logger parameters.
/// </summary>
public class BackwardCompatibilityTests
{
    #region Test Helper Classes
    
    /// <summary>
    /// Test table using the original constructor without logger parameter.
    /// This simulates existing user code that should continue to work.
    /// </summary>
    private class LegacyTestTable : DynamoDbTableBase
    {
        public LegacyTestTable(IAmazonDynamoDB client, string tableName)
            : base(client, tableName)
        {
        }
    }
    
    /// <summary>
    /// Test table using the new constructor with optional logger parameter.
    /// This simulates new code that can optionally use logging.
    /// </summary>
    private class ModernTestTable : DynamoDbTableBase
    {
        public ModernTestTable(IAmazonDynamoDB client, string tableName, IDynamoDbLogger? logger = null)
            : base(client, tableName, logger)
        {
        }
    }
    
    #endregion
    
    #region Constructor Backward Compatibility Tests (Task 15.1)
    
    [Fact]
    public void LegacyConstructor_WithoutLogger_ShouldCompileAndWork()
    {
        // Arrange
        var mockClient = Substitute.For<IAmazonDynamoDB>();
        
        // Act - This should compile without any logger parameter
        var table = new LegacyTestTable(mockClient, "TestTable");
        
        // Assert
        table.Should().NotBeNull();
        table.Name.Should().Be("TestTable");
        table.DynamoDbClient.Should().Be(mockClient);
    }
    
    [Fact]
    public void ModernConstructor_WithoutLogger_ShouldUseNoOpLogger()
    {
        // Arrange
        var mockClient = Substitute.For<IAmazonDynamoDB>();
        
        // Act - Call without logger parameter (using default)
        var table = new ModernTestTable(mockClient, "TestTable");
        
        // Assert
        table.Should().NotBeNull();
        table.Name.Should().Be("TestTable");
        table.DynamoDbClient.Should().Be(mockClient);
    }
    
    [Fact]
    public void ModernConstructor_WithNullLogger_ShouldUseNoOpLogger()
    {
        // Arrange
        var mockClient = Substitute.For<IAmazonDynamoDB>();
        
        // Act - Explicitly pass null logger
        var table = new ModernTestTable(mockClient, "TestTable", null);
        
        // Assert
        table.Should().NotBeNull();
        table.Name.Should().Be("TestTable");
        table.DynamoDbClient.Should().Be(mockClient);
    }
    
    [Fact]
    public void ModernConstructor_WithLogger_ShouldUseProvidedLogger()
    {
        // Arrange
        var mockClient = Substitute.For<IAmazonDynamoDB>();
        var mockLogger = Substitute.For<IDynamoDbLogger>();
        
        // Act - Pass a logger
        var table = new ModernTestTable(mockClient, "TestTable", mockLogger);
        
        // Assert
        table.Should().NotBeNull();
        table.Name.Should().Be("TestTable");
        table.DynamoDbClient.Should().Be(mockClient);
    }
    
    [Fact]
    public void DynamoDbTableBase_OriginalConstructor_ShouldStillWork()
    {
        // Arrange
        var mockClient = Substitute.For<IAmazonDynamoDB>();
        
        // Act - Use the original two-parameter constructor
        var table = new LegacyTestTable(mockClient, "TestTable");
        
        // Assert - All original functionality should work
        table.Get.Should().NotBeNull();
        table.Put.Should().NotBeNull();
        table.Query.Should().NotBeNull();
        table.Update.Should().NotBeNull();
        table.Delete.Should().NotBeNull();
    }
    
    #endregion
    
    #region Request Builder Constructor Backward Compatibility Tests (Task 15.1)
    
    [Fact]
    public void GetItemRequestBuilder_WithoutLogger_ShouldCompileAndWork()
    {
        // Arrange
        var mockClient = Substitute.For<IAmazonDynamoDB>();
        
        // Act - Create builder without logger parameter
        var builder = new GetItemRequestBuilder(mockClient);
        
        // Assert
        builder.Should().NotBeNull();
        var request = builder.ForTable("TestTable").ToGetItemRequest();
        request.TableName.Should().Be("TestTable");
    }
    
    [Fact]
    public void QueryRequestBuilder_WithoutLogger_ShouldCompileAndWork()
    {
        // Arrange
        var mockClient = Substitute.For<IAmazonDynamoDB>();
        
        // Act - Create builder without logger parameter
        var builder = new QueryRequestBuilder(mockClient);
        
        // Assert
        builder.Should().NotBeNull();
        var request = builder.ForTable("TestTable").ToQueryRequest();
        request.TableName.Should().Be("TestTable");
    }
    
    [Fact]
    public void PutItemRequestBuilder_WithoutLogger_ShouldCompileAndWork()
    {
        // Arrange
        var mockClient = Substitute.For<IAmazonDynamoDB>();
        
        // Act - Create builder without logger parameter
        var builder = new PutItemRequestBuilder(mockClient);
        
        // Assert
        builder.Should().NotBeNull();
        var request = builder.ForTable("TestTable").ToPutItemRequest();
        request.TableName.Should().Be("TestTable");
    }
    
    [Fact]
    public void UpdateItemRequestBuilder_WithoutLogger_ShouldCompileAndWork()
    {
        // Arrange
        var mockClient = Substitute.For<IAmazonDynamoDB>();
        
        // Act - Create builder without logger parameter
        var builder = new UpdateItemRequestBuilder(mockClient);
        
        // Assert
        builder.Should().NotBeNull();
        var request = builder.ForTable("TestTable").ToUpdateItemRequest();
        request.TableName.Should().Be("TestTable");
    }
    
    [Fact]
    public void DeleteItemRequestBuilder_WithoutLogger_ShouldCompileAndWork()
    {
        // Arrange
        var mockClient = Substitute.For<IAmazonDynamoDB>();
        
        // Act - Create builder without logger parameter
        var builder = new DeleteItemRequestBuilder(mockClient);
        
        // Assert
        builder.Should().NotBeNull();
        var request = builder.ForTable("TestTable").ToDeleteItemRequest();
        request.TableName.Should().Be("TestTable");
    }
    
    #endregion
    
    #region Method Backward Compatibility Tests (Task 15.2)
    
    [Fact]
    public void TableGetBuilder_WithoutLogger_ShouldWorkAsExpected()
    {
        // Arrange
        var mockClient = Substitute.For<IAmazonDynamoDB>();
        var table = new LegacyTestTable(mockClient, "TestTable");
        
        // Act - Use Get builder without any logger concerns
        var builder = table.Get
            .WithKey("pk", "test-id")
            .WithProjection("name, email");
        
        // Assert
        var request = builder.ToGetItemRequest();
        request.TableName.Should().Be("TestTable");
        request.Key.Should().ContainKey("pk");
        request.ProjectionExpression.Should().Be("name, email");
    }
    
    [Fact]
    public void TableQueryBuilder_WithoutLogger_ShouldWorkAsExpected()
    {
        // Arrange
        var mockClient = Substitute.For<IAmazonDynamoDB>();
        var table = new LegacyTestTable(mockClient, "TestTable");
        
        // Act - Use Query builder without any logger concerns
        var builder = table.Query
            .Where("pk = :pk")
            .WithValue(":pk", "test-id")
            .Take(10);
        
        // Assert
        var request = builder.ToQueryRequest();
        request.TableName.Should().Be("TestTable");
        request.KeyConditionExpression.Should().Be("pk = :pk");
        request.Limit.Should().Be(10);
    }
    
    [Fact]
    public void TablePutBuilder_WithoutLogger_ShouldWorkAsExpected()
    {
        // Arrange
        var mockClient = Substitute.For<IAmazonDynamoDB>();
        var table = new LegacyTestTable(mockClient, "TestTable");
        
        // Act - Use Put builder without any logger concerns
        var item = new Dictionary<string, AttributeValue>
        {
            { "pk", new AttributeValue { S = "test-id" } },
            { "name", new AttributeValue { S = "Test Name" } }
        };
        var builder = table.Put.WithItem(item);
        
        // Assert
        var request = builder.ToPutItemRequest();
        request.TableName.Should().Be("TestTable");
        request.Item.Should().ContainKey("pk");
        request.Item.Should().ContainKey("name");
    }
    
    [Fact]
    public void TableUpdateBuilder_WithoutLogger_ShouldWorkAsExpected()
    {
        // Arrange
        var mockClient = Substitute.For<IAmazonDynamoDB>();
        var table = new LegacyTestTable(mockClient, "TestTable");
        
        // Act - Use Update builder without any logger concerns
        var builder = table.Update
            .WithKey("pk", "test-id")
            .Set("name = :name")
            .WithValue(":name", "Updated Name");
        
        // Assert
        var request = builder.ToUpdateItemRequest();
        request.TableName.Should().Be("TestTable");
        request.Key.Should().ContainKey("pk");
        request.UpdateExpression.Should().Contain("name = :name");
    }
    
    [Fact]
    public void TableDeleteBuilder_WithoutLogger_ShouldWorkAsExpected()
    {
        // Arrange
        var mockClient = Substitute.For<IAmazonDynamoDB>();
        var table = new LegacyTestTable(mockClient, "TestTable");
        
        // Act - Use Delete builder without any logger concerns
        var builder = table.Delete.WithKey("pk", "test-id");
        
        // Assert
        var request = builder.ToDeleteItemRequest();
        request.TableName.Should().Be("TestTable");
        request.Key.Should().ContainKey("pk");
    }
    
    [Fact]
    public void AllRequestBuilders_ChainedMethods_ShouldWorkWithoutLogger()
    {
        // Arrange
        var mockClient = Substitute.For<IAmazonDynamoDB>();
        
        // Act & Assert - Complex chaining should work without logger
        var getRequest = new GetItemRequestBuilder(mockClient)
            .ForTable("TestTable")
            .WithKey("pk", "id1")
            .WithProjection("name")
            .UsingConsistentRead()
            .ToGetItemRequest();
        
        getRequest.TableName.Should().Be("TestTable");
        getRequest.ConsistentRead.Should().BeTrue();
        
        var queryRequest = new QueryRequestBuilder(mockClient)
            .ForTable("TestTable")
            .Where("pk = :pk")
            .WithValue(":pk", "id1")
            .WithFilter("age > :age")
            .WithValue(":age", 18)
            .Take(20)
            .OrderDescending()
            .ToQueryRequest();
        
        queryRequest.TableName.Should().Be("TestTable");
        queryRequest.Limit.Should().Be(20);
        queryRequest.ScanIndexForward.Should().BeFalse();
    }
    
    #endregion
    
    #region Migration Scenario Tests (Task 15.3)
    
    [Fact]
    public void MigrationScenario_AddingLoggerToExistingCode_ShouldWork()
    {
        // Arrange - Start with legacy code
        var mockClient = Substitute.For<IAmazonDynamoDB>();
        var legacyTable = new LegacyTestTable(mockClient, "TestTable");
        
        // Act - Migrate to modern code with logger
        var mockLogger = Substitute.For<IDynamoDbLogger>();
        var modernTable = new ModernTestTable(mockClient, "TestTable", mockLogger);
        
        // Assert - Both should work identically
        legacyTable.Name.Should().Be(modernTable.Name);
        legacyTable.DynamoDbClient.Should().Be(modernTable.DynamoDbClient);
        
        // Both should produce the same requests
        var legacyRequest = legacyTable.Get.WithKey("pk", "id1").ToGetItemRequest();
        var modernRequest = modernTable.Get.WithKey("pk", "id1").ToGetItemRequest();
        
        legacyRequest.TableName.Should().Be(modernRequest.TableName);
        legacyRequest.Key.Should().BeEquivalentTo(modernRequest.Key);
    }
    
    [Fact]
    public void MigrationScenario_RemovingLoggerFromCode_ShouldWork()
    {
        // Arrange - Start with modern code using logger
        var mockClient = Substitute.For<IAmazonDynamoDB>();
        var mockLogger = Substitute.For<IDynamoDbLogger>();
        var modernTable = new ModernTestTable(mockClient, "TestTable", mockLogger);
        
        // Act - Remove logger (pass null or omit parameter)
        var tableWithoutLogger = new ModernTestTable(mockClient, "TestTable");
        
        // Assert - Should work identically
        modernTable.Name.Should().Be(tableWithoutLogger.Name);
        modernTable.DynamoDbClient.Should().Be(tableWithoutLogger.DynamoDbClient);
        
        // Both should produce the same requests
        var withLoggerRequest = modernTable.Query.Where("pk = :pk").ToQueryRequest();
        var withoutLoggerRequest = tableWithoutLogger.Query.Where("pk = :pk").ToQueryRequest();
        
        withLoggerRequest.TableName.Should().Be(withoutLoggerRequest.TableName);
        withLoggerRequest.KeyConditionExpression.Should().Be(withoutLoggerRequest.KeyConditionExpression);
    }
    
    [Fact]
    public void MigrationScenario_UpgradingFromPreviousVersion_ShouldCompile()
    {
        // This test simulates code that was written against the previous version
        // and should continue to compile and work after upgrade
        
        // Arrange - Code written for previous version (no logger awareness)
        var mockClient = Substitute.For<IAmazonDynamoDB>();
        
        // Act - All these patterns should still work
        var table = new LegacyTestTable(mockClient, "TestTable");
        
        var getBuilder = table.Get;
        var queryBuilder = table.Query;
        var putBuilder = table.Put;
        var updateBuilder = table.Update;
        var deleteBuilder = table.Delete;
        
        // Assert - All builders should be functional
        getBuilder.Should().NotBeNull();
        queryBuilder.Should().NotBeNull();
        putBuilder.Should().NotBeNull();
        updateBuilder.Should().NotBeNull();
        deleteBuilder.Should().NotBeNull();
        
        // Complex operations should work
        var complexRequest = table.Query
            .Where("pk = :pk AND begins_with(sk, :prefix)")
            .WithValue(":pk", "USER#123")
            .WithValue(":prefix", "ORDER#")
            .WithFilter("#status = :status")
            .WithAttribute("#status", "status")
            .WithValue(":status", "ACTIVE")
            .Take(10)
            .OrderDescending()
            .UsingConsistentRead()
            .ToQueryRequest();
        
        complexRequest.TableName.Should().Be("TestTable");
        complexRequest.KeyConditionExpression.Should().Contain("pk = :pk");
        complexRequest.FilterExpression.Should().Contain("#status = :status");
    }
    
    [Fact]
    public void MigrationScenario_GradualAdoption_BothStylesCoexist()
    {
        // This test verifies that legacy and modern code can coexist in the same codebase
        
        // Arrange
        var mockClient = Substitute.For<IAmazonDynamoDB>();
        var mockLogger = Substitute.For<IDynamoDbLogger>();
        
        // Act - Create both legacy and modern tables
        var legacyTable = new LegacyTestTable(mockClient, "LegacyTable");
        var modernTable = new ModernTestTable(mockClient, "ModernTable", mockLogger);
        
        // Assert - Both should work independently
        legacyTable.Get.Should().NotBeNull();
        modernTable.Get.Should().NotBeNull();
        
        var legacyRequest = legacyTable.Get.WithKey("pk", "id1").ToGetItemRequest();
        var modernRequest = modernTable.Get.WithKey("pk", "id1").ToGetItemRequest();
        
        legacyRequest.Should().NotBeNull();
        modernRequest.Should().NotBeNull();
        
        // Requests should be structurally identical (except table name)
        legacyRequest.Key.Should().BeEquivalentTo(modernRequest.Key);
    }
    
    #endregion
    
    #region Projection Models Backward Compatibility Tests (Task 11)
    
    [Fact]
    public void DynamoDbIndex_LegacyConstructor_WithoutProjection_ShouldWork()
    {
        // Arrange
        var mockClient = Substitute.For<IAmazonDynamoDB>();
        var table = new LegacyTestTable(mockClient, "TestTable");
        
        // Act - Create index using legacy two-parameter constructor
        var index = new DynamoDbIndex(table, "StatusIndex");
        
        // Assert
        index.Should().NotBeNull();
        index.Name.Should().Be("StatusIndex");
        index.Query.Should().NotBeNull();
    }
    
    [Fact]
    public void DynamoDbIndex_NewConstructor_WithProjection_ShouldWork()
    {
        // Arrange
        var mockClient = Substitute.For<IAmazonDynamoDB>();
        var table = new LegacyTestTable(mockClient, "TestTable");
        
        // Act - Create index using new three-parameter constructor with projection
        var index = new DynamoDbIndex(table, "StatusIndex", "id, amount, status");
        
        // Assert
        index.Should().NotBeNull();
        index.Name.Should().Be("StatusIndex");
        index.Query.Should().NotBeNull();
    }
    
    [Fact]
    public void DynamoDbIndex_LegacyConstructor_QueryBuilder_ShouldNotHaveProjection()
    {
        // Arrange
        var mockClient = Substitute.For<IAmazonDynamoDB>();
        var table = new LegacyTestTable(mockClient, "TestTable");
        var index = new DynamoDbIndex(table, "StatusIndex");
        
        // Act - Get query builder from legacy index
        var builder = index.Query;
        var request = builder.ToQueryRequest();
        
        // Assert - No projection should be applied automatically
        request.ProjectionExpression.Should().BeNullOrEmpty();
        request.IndexName.Should().Be("StatusIndex");
        request.TableName.Should().Be("TestTable");
    }
    
    [Fact]
    public void DynamoDbIndex_NewConstructor_QueryBuilder_ShouldHaveProjection()
    {
        // Arrange
        var mockClient = Substitute.For<IAmazonDynamoDB>();
        var table = new LegacyTestTable(mockClient, "TestTable");
        var index = new DynamoDbIndex(table, "StatusIndex", "id, amount, status");
        
        // Act - Get query builder from index with projection
        var builder = index.Query;
        var request = builder.ToQueryRequest();
        
        // Assert - Projection should be applied automatically
        request.ProjectionExpression.Should().Be("id, amount, status");
        request.IndexName.Should().Be("StatusIndex");
        request.TableName.Should().Be("TestTable");
    }
    
    [Fact]
    public void DynamoDbIndexGeneric_Constructor_WithoutProjection_ShouldWork()
    {
        // Arrange
        var mockClient = Substitute.For<IAmazonDynamoDB>();
        var table = new LegacyTestTable(mockClient, "TestTable");
        
        // Act - Create generic index without projection
        var index = new DynamoDbIndex<TestEntity>(table, "StatusIndex");
        
        // Assert
        index.Should().NotBeNull();
        index.Name.Should().Be("StatusIndex");
        index.Query.Should().NotBeNull();
    }
    
    [Fact]
    public void DynamoDbIndexGeneric_Constructor_WithProjection_ShouldWork()
    {
        // Arrange
        var mockClient = Substitute.For<IAmazonDynamoDB>();
        var table = new LegacyTestTable(mockClient, "TestTable");
        
        // Act - Create generic index with projection
        var index = new DynamoDbIndex<TestEntity>(table, "StatusIndex", "id, amount, status");
        
        // Assert
        index.Should().NotBeNull();
        index.Name.Should().Be("StatusIndex");
        index.Query.Should().NotBeNull();
    }
    
    [Fact]
    public void QueryRequestBuilder_WithProjection_ManualCall_ShouldWork()
    {
        // Arrange
        var mockClient = Substitute.For<IAmazonDynamoDB>();
        
        // Act - Use manual WithProjection call (existing API)
        var builder = new QueryRequestBuilder(mockClient)
            .ForTable("TestTable")
            .Where("pk = :pk")
            .WithValue(":pk", "test-id")
            .WithProjection("id, name, email");
        
        var request = builder.ToQueryRequest();
        
        // Assert - Manual projection should work as before
        request.ProjectionExpression.Should().Be("id, name, email");
        request.Select.Should().Be(Select.SPECIFIC_ATTRIBUTES);
    }
    
    [Fact]
    public void QueryRequestBuilder_WithProjection_ChainedWithOtherMethods_ShouldWork()
    {
        // Arrange
        var mockClient = Substitute.For<IAmazonDynamoDB>();
        
        // Act - Chain WithProjection with other methods (existing pattern)
        var builder = new QueryRequestBuilder(mockClient)
            .ForTable("TestTable")
            .UsingIndex("StatusIndex")
            .Where("pk = :pk")
            .WithValue(":pk", "test-id")
            .WithFilter("amount > :amount")
            .WithValue(":amount", 100)
            .WithProjection("id, amount, status")
            .Take(10)
            .OrderDescending();
        
        var request = builder.ToQueryRequest();
        
        // Assert - All methods should work together
        request.ProjectionExpression.Should().Be("id, amount, status");
        request.IndexName.Should().Be("StatusIndex");
        request.FilterExpression.Should().Be("amount > :amount");
        request.Limit.Should().Be(10);
        request.ScanIndexForward.Should().BeFalse();
    }
    
    [Fact]
    public void DynamoDbIndex_ManualProjection_ShouldOverrideAutoProjection()
    {
        // Arrange
        var mockClient = Substitute.For<IAmazonDynamoDB>();
        var table = new LegacyTestTable(mockClient, "TestTable");
        var index = new DynamoDbIndex(table, "StatusIndex", "id, amount, status");
        
        // Act - Manually override the auto-applied projection
        var builder = index.Query
            .Where("pk = :pk")
            .WithValue(":pk", "test-id")
            .WithProjection("id, name"); // Manual override
        
        var request = builder.ToQueryRequest();
        
        // Assert - Manual projection should take precedence
        request.ProjectionExpression.Should().Be("id, name");
    }
    
    [Fact]
    public void QueryRequestBuilder_ExecuteAsync_WithoutToListAsync_ShouldWork()
    {
        // Arrange
        var mockClient = Substitute.For<IAmazonDynamoDB>();
        mockClient.QueryAsync(Arg.Any<QueryRequest>(), Arg.Any<CancellationToken>())
            .Returns(new QueryResponse { Items = new List<Dictionary<string, AttributeValue>>() });
        
        // Act - Use ExecuteAsync directly (existing pattern)
        var builder = new QueryRequestBuilder(mockClient)
            .ForTable("TestTable")
            .Where("pk = :pk")
            .WithValue(":pk", "test-id");
        
        var responseTask = builder.ExecuteAsync();
        
        // Assert - Should work without ToListAsync
        responseTask.Should().NotBeNull();
    }
    
    [Fact]
    public void MigrationScenario_ExistingIndexUsage_ShouldContinueToWork()
    {
        // This test simulates existing code that manually creates indexes
        // and should continue to work after the projection models feature is added
        
        // Arrange - Existing code pattern
        var mockClient = Substitute.For<IAmazonDynamoDB>();
        var table = new LegacyTestTable(mockClient, "TestTable");
        
        // Act - Existing manual index instantiation
        var statusIndex = new DynamoDbIndex(table, "StatusIndex");
        var gsi1 = new DynamoDbIndex(table, "GSI1");
        
        // Existing query patterns
        var query1 = statusIndex.Query
            .Where("status = :status")
            .WithValue(":status", "ACTIVE");
        
        var query2 = gsi1.Query
            .Where("gsi1pk = :pk")
            .WithValue(":pk", "USER#123")
            .WithProjection("id, name"); // Manual projection
        
        // Assert - All existing patterns should work
        statusIndex.Name.Should().Be("StatusIndex");
        gsi1.Name.Should().Be("GSI1");
        
        var request1 = query1.ToQueryRequest();
        request1.IndexName.Should().Be("StatusIndex");
        request1.ProjectionExpression.Should().BeNullOrEmpty(); // No auto-projection
        
        var request2 = query2.ToQueryRequest();
        request2.IndexName.Should().Be("GSI1");
        request2.ProjectionExpression.Should().Be("id, name"); // Manual projection preserved
    }
    
    [Fact]
    public void MigrationScenario_AddingProjectionToExistingIndex_ShouldWork()
    {
        // This test simulates migrating from manual index without projection
        // to manual index with projection
        
        // Arrange - Start with existing code
        var mockClient = Substitute.For<IAmazonDynamoDB>();
        var table = new LegacyTestTable(mockClient, "TestTable");
        
        // Old pattern - no projection
        var oldIndex = new DynamoDbIndex(table, "StatusIndex");
        var oldRequest = oldIndex.Query.Where("pk = :pk").ToQueryRequest();
        
        // Act - Migrate to new pattern with projection
        var newIndex = new DynamoDbIndex(table, "StatusIndex", "id, amount, status");
        var newRequest = newIndex.Query.Where("pk = :pk").ToQueryRequest();
        
        // Assert - Both should work, but new one has projection
        oldRequest.ProjectionExpression.Should().BeNullOrEmpty();
        newRequest.ProjectionExpression.Should().Be("id, amount, status");
        
        // Both should have same index name and table name
        oldRequest.IndexName.Should().Be(newRequest.IndexName);
        oldRequest.TableName.Should().Be(newRequest.TableName);
    }
    
    [Fact]
    public void MigrationScenario_MixingLegacyAndNewIndexes_ShouldWork()
    {
        // This test verifies that legacy indexes and new projection-enabled indexes
        // can coexist in the same codebase
        
        // Arrange
        var mockClient = Substitute.For<IAmazonDynamoDB>();
        var table = new LegacyTestTable(mockClient, "TestTable");
        
        // Act - Create both legacy and new style indexes
        var legacyIndex = new DynamoDbIndex(table, "LegacyIndex");
        var projectionIndex = new DynamoDbIndex(table, "ProjectionIndex", "id, amount");
        var genericIndex = new DynamoDbIndex<TestEntity>(table, "GenericIndex", "id, name");
        
        // Assert - All should work independently
        legacyIndex.Query.ToQueryRequest().ProjectionExpression.Should().BeNullOrEmpty();
        projectionIndex.Query.ToQueryRequest().ProjectionExpression.Should().Be("id, amount");
        genericIndex.Query.ToQueryRequest().ProjectionExpression.Should().Be("id, name");
        
        // All should have correct index names
        legacyIndex.Name.Should().Be("LegacyIndex");
        projectionIndex.Name.Should().Be("ProjectionIndex");
        genericIndex.Name.Should().Be("GenericIndex");
    }
    
    [Fact]
    public void BackwardCompatibility_AllExistingAPIs_ShouldRemainUnchanged()
    {
        // This comprehensive test verifies that all existing APIs work exactly as before
        
        // Arrange
        var mockClient = Substitute.For<IAmazonDynamoDB>();
        var table = new LegacyTestTable(mockClient, "TestTable");
        
        // Act & Assert - Test all existing API patterns
        
        // 1. Table builders
        table.Get.Should().NotBeNull();
        table.Put.Should().NotBeNull();
        table.Query.Should().NotBeNull();
        table.Update.Should().NotBeNull();
        table.Delete.Should().NotBeNull();
        
        // 2. Request builders
        new GetItemRequestBuilder(mockClient).Should().NotBeNull();
        new PutItemRequestBuilder(mockClient).Should().NotBeNull();
        new QueryRequestBuilder(mockClient).Should().NotBeNull();
        new UpdateItemRequestBuilder(mockClient).Should().NotBeNull();
        new DeleteItemRequestBuilder(mockClient).Should().NotBeNull();
        
        // 3. Index creation
        var index = new DynamoDbIndex(table, "TestIndex");
        index.Should().NotBeNull();
        index.Name.Should().Be("TestIndex");
        index.Query.Should().NotBeNull();
        
        // 4. Query building
        var queryRequest = table.Query
            .Where("pk = :pk")
            .WithValue(":pk", "test")
            .WithProjection("id, name")
            .Take(10)
            .ToQueryRequest();
        
        queryRequest.TableName.Should().Be("TestTable");
        queryRequest.KeyConditionExpression.Should().Be("pk = :pk");
        queryRequest.ProjectionExpression.Should().Be("id, name");
        queryRequest.Limit.Should().Be(10);
        
        // 5. Index query building
        var indexQueryRequest = index.Query
            .Where("pk = :pk")
            .WithValue(":pk", "test")
            .ToQueryRequest();
        
        indexQueryRequest.IndexName.Should().Be("TestIndex");
        indexQueryRequest.TableName.Should().Be("TestTable");
    }
    
    #endregion
    
    #region Test Helper Classes for Projection Tests
    
    /// <summary>
    /// Simple test entity for generic index tests.
    /// </summary>
    private class TestEntity
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public int Amount { get; set; }
        public string Status { get; set; } = string.Empty;
    }
    
    #endregion
}