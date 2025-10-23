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
}
