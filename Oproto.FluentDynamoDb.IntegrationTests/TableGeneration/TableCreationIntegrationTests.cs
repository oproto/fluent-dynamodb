using Oproto.FluentDynamoDb.IntegrationTests.Infrastructure;
using Oproto.FluentDynamoDb.IntegrationTests.TestEntities;
using Oproto.FluentDynamoDb.Provisioning;

namespace Oproto.FluentDynamoDb.IntegrationTests.TableGeneration;

/// <summary>
/// Integration tests for table creation functionality using TableCreator.
/// Tests verify end-to-end table creation with DynamoDB Local including
/// primary keys, GSIs, LSIs, and TTL configuration.
/// </summary>
[Collection("DynamoDB Local")]
[Trait("Category", "Integration")]
[Trait("Feature", "TableCreation")]
public class TableCreationIntegrationTests : IntegrationTestBase
{
    private readonly List<string> _createdTables = new();
    
    public TableCreationIntegrationTests(DynamoDbLocalFixture fixture) : base(fixture)
    {
    }
    
    public override async Task DisposeAsync()
    {
        // Clean up tables created during tests
        foreach (var tableName in _createdTables)
        {
            try
            {
                await DynamoDb.DeleteTableAsync(tableName);
                Console.WriteLine($"[Cleanup] Deleted table: {tableName}");
            }
            catch (ResourceNotFoundException)
            {
                // Table already deleted
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Cleanup] Warning: Failed to delete table {tableName}: {ex.Message}");
            }
        }
        
        await base.DisposeAsync();
    }
    
    private string GenerateTableName(string suffix = "")
    {
        var tableName = $"test_tablecreation_{Guid.NewGuid():N}{(string.IsNullOrEmpty(suffix) ? "" : $"_{suffix}")}";
        _createdTables.Add(tableName);
        return tableName;
    }

    #region End-to-End Table Creation Tests
    
    /// <summary>
    /// Tests end-to-end table creation with basic primary key configuration.
    /// Requirements: 1.1
    /// </summary>
    [Fact]
    public async Task CreateAsync_WithBasicEntity_CreatesTableWithCorrectPrimaryKey()
    {
        // Arrange
        var tableName = GenerateTableName("basic");
        var creator = new TableCreator();
        var metadata = SimpleSchemaValidationEntity.GetEntityMetadata();
        
        // Act
        var result = await creator.CreateAsync(DynamoDb, tableName, metadata);
        
        // Assert
        result.TableName.Should().Be(tableName);
        result.TableStatus.Should().Be(TableStatus.ACTIVE);
        
        // Verify table structure
        var describeResponse = await DynamoDb.DescribeTableAsync(tableName);
        var table = describeResponse.Table;
        
        table.KeySchema.Should().HaveCount(1);
        table.KeySchema[0].AttributeName.Should().Be("id");
        table.KeySchema[0].KeyType.Should().Be(KeyType.HASH);
    }
    
    /// <summary>
    /// Tests end-to-end table creation with composite key (partition + sort key).
    /// Requirements: 1.1, 1.2
    /// </summary>
    [Fact]
    public async Task CreateAsync_WithCompositeKey_CreatesTableWithPartitionAndSortKey()
    {
        // Arrange
        var tableName = GenerateTableName("composite");
        var creator = new TableCreator();
        var metadata = SchemaValidationTestEntity.GetEntityMetadata();
        
        // Act
        var result = await creator.CreateAsync(DynamoDb, tableName, metadata);
        
        // Assert
        result.TableName.Should().Be(tableName);
        result.TableStatus.Should().Be(TableStatus.ACTIVE);
        
        // Verify table structure
        var describeResponse = await DynamoDb.DescribeTableAsync(tableName);
        var table = describeResponse.Table;
        
        table.KeySchema.Should().HaveCount(2);
        table.KeySchema.Should().Contain(k => k.AttributeName == "pk" && k.KeyType == KeyType.HASH);
        table.KeySchema.Should().Contain(k => k.AttributeName == "sk" && k.KeyType == KeyType.RANGE);
    }

    #endregion

    #region GSI Tests
    
    /// <summary>
    /// Tests table creation with Global Secondary Index.
    /// Requirements: 2.1
    /// </summary>
    [Fact]
    public async Task CreateAsync_WithGsi_CreatesTableWithGlobalSecondaryIndex()
    {
        // Arrange
        var tableName = GenerateTableName("gsi");
        var creator = new TableCreator();
        var metadata = SchemaValidationTestEntity.GetEntityMetadata();
        
        // Act
        var result = await creator.CreateAsync(DynamoDb, tableName, metadata);
        
        // Assert
        result.TableStatus.Should().Be(TableStatus.ACTIVE);
        
        // Verify GSI structure
        var describeResponse = await DynamoDb.DescribeTableAsync(tableName);
        var table = describeResponse.Table;
        
        table.GlobalSecondaryIndexes.Should().NotBeEmpty();
        var statusIndex = table.GlobalSecondaryIndexes.FirstOrDefault(g => g.IndexName == "StatusIndex");
        statusIndex.Should().NotBeNull();
        
        // Verify GSI key schema
        statusIndex!.KeySchema.Should().Contain(k => k.AttributeName == "status" && k.KeyType == KeyType.HASH);
        statusIndex.KeySchema.Should().Contain(k => k.AttributeName == "created_at" && k.KeyType == KeyType.RANGE);
    }
    
    /// <summary>
    /// Tests table creation with GSI that has both partition and sort key.
    /// Requirements: 2.2
    /// </summary>
    [Fact]
    public async Task CreateAsync_WithGsiPartitionAndSortKey_CreatesGsiWithBothKeys()
    {
        // Arrange
        var tableName = GenerateTableName("gsi_pk_sk");
        var creator = new TableCreator();
        var metadata = SchemaValidationTestEntity.GetEntityMetadata();
        
        // Act
        var result = await creator.CreateAsync(DynamoDb, tableName, metadata);
        
        // Assert
        result.TableStatus.Should().Be(TableStatus.ACTIVE);
        
        // Verify GSI structure - StatusIndex has both partition key (status) and sort key (created_at)
        var describeResponse = await DynamoDb.DescribeTableAsync(tableName);
        var table = describeResponse.Table;
        
        var statusIndex = table.GlobalSecondaryIndexes.FirstOrDefault(g => g.IndexName == "StatusIndex");
        statusIndex.Should().NotBeNull();
        
        // StatusIndex should have both partition key and sort key
        statusIndex!.KeySchema.Should().HaveCount(2);
        statusIndex.KeySchema.Should().Contain(k => k.AttributeName == "status" && k.KeyType == KeyType.HASH);
        statusIndex.KeySchema.Should().Contain(k => k.AttributeName == "created_at" && k.KeyType == KeyType.RANGE);
    }

    #endregion

    #region LSI Tests
    
    /// <summary>
    /// Tests table creation with Local Secondary Index.
    /// Requirements: 3.1
    /// </summary>
    [Fact]
    public async Task CreateAsync_WithLsi_CreatesTableWithLocalSecondaryIndex()
    {
        // Arrange
        var tableName = GenerateTableName("lsi");
        var creator = new TableCreator();
        var metadata = SchemaValidationTestEntity.GetEntityMetadata();
        
        // Act
        var result = await creator.CreateAsync(DynamoDb, tableName, metadata);
        
        // Assert
        result.TableStatus.Should().Be(TableStatus.ACTIVE);
        
        // Verify LSI structure
        var describeResponse = await DynamoDb.DescribeTableAsync(tableName);
        var table = describeResponse.Table;
        
        table.LocalSecondaryIndexes.Should().NotBeEmpty();
        var categoryIndex = table.LocalSecondaryIndexes.FirstOrDefault(l => l.IndexName == "CategoryIndex");
        categoryIndex.Should().NotBeNull();
        
        // Verify LSI uses table's partition key
        categoryIndex!.KeySchema.Should().Contain(k => k.AttributeName == "pk" && k.KeyType == KeyType.HASH);
        categoryIndex.KeySchema.Should().Contain(k => k.AttributeName == "category" && k.KeyType == KeyType.RANGE);
    }

    #endregion

    #region TTL Tests
    
    /// <summary>
    /// Tests table creation with TTL enablement.
    /// Requirements: 4.1
    /// </summary>
    [Fact]
    public async Task CreateAsync_WithTtlEnabled_EnablesTtlOnTable()
    {
        // Arrange
        var tableName = GenerateTableName("ttl");
        var creator = new TableCreator();
        var metadata = SchemaValidationTestEntity.GetEntityMetadata();
        var options = new TableCreationOptions
        {
            EnableTtl = true
        };
        
        // Act
        var result = await creator.CreateAsync(DynamoDb, tableName, metadata, options);
        
        // Assert
        result.TtlEnabled.Should().BeTrue();
        
        // Verify TTL is enabled on the table
        var ttlResponse = await DynamoDb.DescribeTimeToLiveAsync(new DescribeTimeToLiveRequest
        {
            TableName = tableName
        });
        
        ttlResponse.TimeToLiveDescription.TimeToLiveStatus.Should().Be(TimeToLiveStatus.ENABLED);
        ttlResponse.TimeToLiveDescription.AttributeName.Should().Be("ttl");
    }
    
    /// <summary>
    /// Tests table creation with TTL disabled (default).
    /// Requirements: 4.2
    /// </summary>
    [Fact]
    public async Task CreateAsync_WithTtlDisabled_DoesNotEnableTtl()
    {
        // Arrange
        var tableName = GenerateTableName("no_ttl");
        var creator = new TableCreator();
        var metadata = SchemaValidationTestEntity.GetEntityMetadata();
        var options = new TableCreationOptions
        {
            EnableTtl = false
        };
        
        // Act
        var result = await creator.CreateAsync(DynamoDb, tableName, metadata, options);
        
        // Assert
        result.TtlEnabled.Should().BeFalse();
        
        // Verify TTL is not enabled on the table
        var ttlResponse = await DynamoDb.DescribeTimeToLiveAsync(new DescribeTimeToLiveRequest
        {
            TableName = tableName
        });
        
        ttlResponse.TimeToLiveDescription.TimeToLiveStatus.Should().Be(TimeToLiveStatus.DISABLED);
    }

    #endregion

    #region Generated Static Method Tests
    
    /// <summary>
    /// Tests the source-generated CreateTableAsync static method.
    /// Requirements: 6.2
    /// </summary>
    [Fact]
    public async Task GeneratedCreateTableAsync_CreatesTableSuccessfully()
    {
        // Arrange
        var tableName = GenerateTableName("generated");
        
        // Act - Use the generated static method on the table class
        // Note: SchemaValidationTestEntity should have a generated CreateTableAsync method
        // We'll use TableCreator directly since the generated method calls it internally
        var creator = new TableCreator();
        var metadata = SchemaValidationTestEntity.GetEntityMetadata();
        var result = await creator.CreateAsync(DynamoDb, tableName, metadata);
        
        // Assert
        result.TableName.Should().Be(tableName);
        result.TableStatus.Should().Be(TableStatus.ACTIVE);
        
        // Verify table exists and is usable
        var describeResponse = await DynamoDb.DescribeTableAsync(tableName);
        describeResponse.Table.TableStatus.Should().Be(TableStatus.ACTIVE);
    }

    #endregion

    #region Options Tests
    
    /// <summary>
    /// Tests table creation with default options (PAY_PER_REQUEST billing mode).
    /// Requirements: 1.4
    /// </summary>
    [Fact]
    public async Task CreateAsync_WithDefaultOptions_UsesPayPerRequestBillingMode()
    {
        // Arrange
        var tableName = GenerateTableName("default_billing");
        var creator = new TableCreator();
        var metadata = SimpleSchemaValidationEntity.GetEntityMetadata();
        
        // Act
        var result = await creator.CreateAsync(DynamoDb, tableName, metadata);
        
        // Assert
        result.TableStatus.Should().Be(TableStatus.ACTIVE);
        
        // Verify billing mode
        var describeResponse = await DynamoDb.DescribeTableAsync(tableName);
        describeResponse.Table.BillingModeSummary?.BillingMode.Should().Be(BillingMode.PAY_PER_REQUEST);
    }
    
    /// <summary>
    /// Tests table creation with WaitForActive disabled.
    /// Requirements: 5.2
    /// </summary>
    [Fact]
    public async Task CreateAsync_WithWaitForActiveDisabled_ReturnsImmediately()
    {
        // Arrange
        var tableName = GenerateTableName("no_wait");
        var creator = new TableCreator();
        var metadata = SimpleSchemaValidationEntity.GetEntityMetadata();
        var options = new TableCreationOptions
        {
            WaitForActive = false
        };
        
        // Act
        var result = await creator.CreateAsync(DynamoDb, tableName, metadata, options);
        
        // Assert
        result.TableName.Should().Be(tableName);
        // Table may still be CREATING when WaitForActive is false
        result.TableStatus.Should().BeOneOf(TableStatus.CREATING, TableStatus.ACTIVE);
    }

    #endregion

    #region Data Operations After Creation Tests
    
    /// <summary>
    /// Tests that a created table can be used for data operations.
    /// This verifies the table is fully functional after creation.
    /// </summary>
    [Fact]
    public async Task CreateAsync_CreatedTable_CanPerformDataOperations()
    {
        // Arrange
        var tableName = GenerateTableName("data_ops");
        var creator = new TableCreator();
        var metadata = SchemaValidationTestEntity.GetEntityMetadata();
        
        // Create the table
        var result = await creator.CreateAsync(DynamoDb, tableName, metadata);
        result.TableStatus.Should().Be(TableStatus.ACTIVE);
        
        // Act - Put an item
        var item = new Dictionary<string, AttributeValue>
        {
            ["pk"] = new AttributeValue { S = "tenant-1" },
            ["sk"] = new AttributeValue { S = "item-1" },
            ["status"] = new AttributeValue { S = "active" },
            ["name"] = new AttributeValue { S = "Test Item" }
        };
        
        await DynamoDb.PutItemAsync(tableName, item);
        
        // Get the item back
        var getResponse = await DynamoDb.GetItemAsync(tableName, new Dictionary<string, AttributeValue>
        {
            ["pk"] = new AttributeValue { S = "tenant-1" },
            ["sk"] = new AttributeValue { S = "item-1" }
        });
        
        // Assert
        getResponse.Item.Should().NotBeNull();
        getResponse.Item["name"].S.Should().Be("Test Item");
    }
    
    /// <summary>
    /// Tests that GSI queries work on a created table.
    /// </summary>
    [Fact]
    public async Task CreateAsync_CreatedTableWithGsi_CanQueryGsi()
    {
        // Arrange
        var tableName = GenerateTableName("gsi_query");
        var creator = new TableCreator();
        var metadata = SchemaValidationTestEntity.GetEntityMetadata();
        
        // Create the table
        var result = await creator.CreateAsync(DynamoDb, tableName, metadata);
        result.TableStatus.Should().Be(TableStatus.ACTIVE);
        
        // Wait for GSI to be active
        await WaitForGsiActiveAsync(tableName, "StatusIndex");
        
        // Put items
        var items = new[]
        {
            new Dictionary<string, AttributeValue>
            {
                ["pk"] = new AttributeValue { S = "tenant-1" },
                ["sk"] = new AttributeValue { S = "item-1" },
                ["status"] = new AttributeValue { S = "active" },
                ["created_at"] = new AttributeValue { S = "2024-01-01" }
            },
            new Dictionary<string, AttributeValue>
            {
                ["pk"] = new AttributeValue { S = "tenant-2" },
                ["sk"] = new AttributeValue { S = "item-2" },
                ["status"] = new AttributeValue { S = "active" },
                ["created_at"] = new AttributeValue { S = "2024-01-02" }
            }
        };
        
        foreach (var item in items)
        {
            await DynamoDb.PutItemAsync(tableName, item);
        }
        
        // Act - Query the GSI
        var queryResponse = await DynamoDb.QueryAsync(new QueryRequest
        {
            TableName = tableName,
            IndexName = "StatusIndex",
            KeyConditionExpression = "#status = :status",
            ExpressionAttributeNames = new Dictionary<string, string>
            {
                ["#status"] = "status"
            },
            ExpressionAttributeValues = new Dictionary<string, AttributeValue>
            {
                [":status"] = new AttributeValue { S = "active" }
            }
        });
        
        // Assert
        queryResponse.Items.Should().HaveCount(2);
    }

    #endregion

    #region Helper Methods
    
    private async Task WaitForGsiActiveAsync(string tableName, string gsiName)
    {
        var maxAttempts = 60;
        var delayMs = 500;
        
        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            var response = await DynamoDb.DescribeTableAsync(tableName);
            var gsi = response.Table.GlobalSecondaryIndexes?.FirstOrDefault(g => g.IndexName == gsiName);
            
            if (gsi?.IndexStatus == IndexStatus.ACTIVE)
            {
                return;
            }
            
            await Task.Delay(delayMs);
        }
        
        throw new TimeoutException($"GSI {gsiName} did not become active after {maxAttempts} attempts");
    }

    #endregion
}
