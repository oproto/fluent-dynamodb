using Amazon.DynamoDBv2.Model;
using Oproto.FluentDynamoDb.Attributes;
using Oproto.FluentDynamoDb.IntegrationTests.Infrastructure;
using Oproto.FluentDynamoDb.Storage;

namespace Oproto.FluentDynamoDb.IntegrationTests.RealWorld;

/// <summary>
/// Integration tests for the new transaction and batch API redesign.
/// Tests the static entry points (DynamoDbTransactions and DynamoDbBatch) with request builder reuse.
/// </summary>
[Collection("DynamoDB Local")]
[Trait("Category", "Integration")]
[Trait("Feature", "Transactions")]
[Trait("Feature", "Batch")]
public class TransactionBatchApiIntegrationTests : IntegrationTestBase
{
    private TransactionApiTestTable _table = null!;
    private string _secondTableName = null!;
    private TransactionApiSecondTable _secondTable = null!;

    public TransactionBatchApiIntegrationTests(DynamoDbLocalFixture fixture) : base(fixture)
    {
    }

    public override async Task InitializeAsync()
    {
        await base.InitializeAsync();
        
        // Create first table
        await CreateTableAsync<TransactionApiUserEntity>();
        _table = new TransactionApiTestTable(DynamoDb, TableName);
        
        // Create second table for multi-table tests
        _secondTableName = $"test_second_{Guid.NewGuid():N}";
        await CreateSecondTableAsync();
        _secondTable = new TransactionApiSecondTable(DynamoDb, _secondTableName);
    }

    private async Task CreateSecondTableAsync()
    {
        var request = new CreateTableRequest
        {
            TableName = _secondTableName,
            KeySchema = new List<KeySchemaElement>
            {
                new KeySchemaElement { AttributeName = "pk", KeyType = KeyType.HASH },
                new KeySchemaElement { AttributeName = "sk", KeyType = KeyType.RANGE }
            },
            AttributeDefinitions = new List<AttributeDefinition>
            {
                new AttributeDefinition { AttributeName = "pk", AttributeType = ScalarAttributeType.S },
                new AttributeDefinition { AttributeName = "sk", AttributeType = ScalarAttributeType.S }
            },
            BillingMode = BillingMode.PAY_PER_REQUEST
        };
        
        await DynamoDb.CreateTableAsync(request);
        await WaitForTableActiveAsync(_secondTableName);
    }

    #region Task 23.1: Transaction Write End-to-End Tests

    [Fact]
    public async Task TransactionWrite_WithMultipleOperations_ExecutesAtomically()
    {
        // Arrange
        var user = new TransactionApiUserEntity
        {
            UserId = "USER#001",
            SortKey = "PROFILE",
            Name = "John Doe",
            Email = "john@example.com",
            Status = "active"
        };

        var order = new TransactionApiOrderEntity
        {
            OrderId = "ORDER#001",
            SortKey = "METADATA",
            UserId = "USER#001",
            Total = 100.50m,
            Status = "pending"
        };

        var product = new TransactionApiProductEntity
        {
            ProductId = "PRODUCT#001",
            SortKey = "INFO",
            Name = "Test Product",
            Price = 50.25m,
            Stock = 10
        };

        // Act - Execute transaction with Put operations
        await DynamoDbTransactions.Write
            .Add(_table.Users.Put(user))
            .Add(_table.Orders.Put(order))
            .Add(_table.Products.Put(product))
            .ExecuteAsync();
        
        // Update the product in a separate transaction
        await DynamoDbTransactions.Write
            .Add(_table.Products.Update("PRODUCT#001", "INFO")
                .Set("SET #stock = :stock")
                .WithAttribute("#stock", "stock")
                .WithValue(":stock", 9))
            .ExecuteAsync();

        // Assert - All operations should be applied
        var savedUser = await _table.Users.Get("USER#001", "PROFILE").GetItemAsync();
        var savedOrder = await _table.Orders.Get("ORDER#001", "METADATA").GetItemAsync();
        var savedProduct = await _table.Products.Get("PRODUCT#001", "INFO").GetItemAsync();

        savedUser.Should().NotBeNull();
        savedUser!.Name.Should().Be("John Doe");
        
        savedOrder.Should().NotBeNull();
        savedOrder!.Total.Should().Be(100.50m);
        
        savedProduct.Should().NotBeNull();
        savedProduct!.Stock.Should().Be(9);
    }

    [Fact]
    public async Task TransactionWrite_WithConditionFailure_RollsBackAllOperations()
    {
        // Arrange
        var existingUser = new TransactionApiUserEntity
        {
            UserId = "USER#002",
            SortKey = "PROFILE",
            Name = "Existing User",
            Email = "existing@example.com",
            Status = "active"
        };

        await _table.Users.Put(existingUser).PutAsync();

        var newOrder = new TransactionApiOrderEntity
        {
            OrderId = "ORDER#002",
            SortKey = "METADATA",
            UserId = "USER#002",
            Total = 200.00m,
            Status = "pending"
        };

        // Act & Assert - Transaction should fail due to condition check
        var exception = await Assert.ThrowsAsync<TransactionCanceledException>(async () =>
        {
            await DynamoDbTransactions.Write
                .Add(_table.Orders.Put(newOrder))
                .Add(_table.Users.ConditionCheck("USER#002", "PROFILE")
                    .Where("attribute_not_exists(pk)")) // This will fail
                .ExecuteAsync();
        });

        exception.Should().NotBeNull();

        // Verify rollback - order should not exist
        var order = await _table.Orders.Get("ORDER#002", "METADATA").GetItemAsync();
        order.Should().BeNull();
    }

    [Fact]
    public async Task TransactionWrite_WithMultipleUpdates_WorksCorrectly()
    {
        // Arrange
        var user1 = new TransactionApiUserEntity
        {
            UserId = "USER#ENC1",
            SortKey = "PROFILE",
            Name = "User 1",
            Email = "user1@example.com",
            Status = "active"
        };

        var user2 = new TransactionApiUserEntity
        {
            UserId = "USER#ENC2",
            SortKey = "PROFILE",
            Name = "User 2",
            Email = "user2@example.com",
            Status = "active"
        };

        await _table.Users.Put(user1).PutAsync();
        await _table.Users.Put(user2).PutAsync();

        // Act - Update multiple users in a transaction
        await DynamoDbTransactions.Write
            .Add(_table.Users.Update("USER#ENC1", "PROFILE")
                .Set("SET #status = :status")
                .WithAttribute("#status", "status")
                .WithValue(":status", "verified"))
            .Add(_table.Users.Update("USER#ENC2", "PROFILE")
                .Set("SET #status = :status")
                .WithAttribute("#status", "status")
                .WithValue(":status", "verified"))
            .ExecuteAsync();

        // Assert - Both users should be updated
        var updated1 = await _table.Users.Get("USER#ENC1", "PROFILE").GetItemAsync();
        var updated2 = await _table.Users.Get("USER#ENC2", "PROFILE").GetItemAsync();

        updated1.Should().NotBeNull();
        updated1!.Status.Should().Be("verified");
        updated2.Should().NotBeNull();
        updated2!.Status.Should().Be("verified");
    }

    [Fact]
    public async Task TransactionWrite_WithSourceGeneratedMethods_WorksCorrectly()
    {
        // Arrange
        var user = new TransactionApiUserEntity
        {
            UserId = "USER#003",
            SortKey = "PROFILE",
            Name = "Generated User",
            Email = "generated@example.com",
            Status = "active"
        };

        // Act - Use source-generated methods
        await DynamoDbTransactions.Write
            .Add(_table.Users.Put(user))
            .ExecuteAsync();
        
        // Update in a separate transaction
        await DynamoDbTransactions.Write
            .Add(_table.Users.Update("USER#003", "PROFILE")
                .Set("SET #status = :status")
                .WithAttribute("#status", "status")
                .WithValue(":status", "verified"))
            .ExecuteAsync();

        // Assert
        var savedUser = await _table.Users.Get("USER#003", "PROFILE").GetItemAsync();
        savedUser.Should().NotBeNull();
        savedUser!.Status.Should().Be("verified");
    }

    #endregion

    #region Task 23.2: Transaction Get End-to-End Tests

    [Fact]
    public async Task TransactionGet_WithMultipleItems_RetrievesWithSnapshotIsolation()
    {
        // Arrange
        var user = new TransactionApiUserEntity
        {
            UserId = "USER#004",
            SortKey = "PROFILE",
            Name = "Transaction User",
            Email = "txn@example.com",
            Status = "active"
        };

        var order = new TransactionApiOrderEntity
        {
            OrderId = "ORDER#004",
            SortKey = "METADATA",
            UserId = "USER#004",
            Total = 150.00m,
            Status = "completed"
        };

        await _table.Users.Put(user).PutAsync();
        await _table.Orders.Put(order).PutAsync();

        // Act - Get multiple items atomically
        var response = await DynamoDbTransactions.Get
            .Add(_table.Users.Get("USER#004", "PROFILE"))
            .Add(_table.Orders.Get("ORDER#004", "METADATA"))
            .ExecuteAsync();

        // Assert
        response.Should().NotBeNull();
        response.Count.Should().Be(2);

        var retrievedUser = response.GetItem<TransactionApiUserEntity>(0);
        var retrievedOrder = response.GetItem<TransactionApiOrderEntity>(1);

        retrievedUser.Should().NotBeNull();
        retrievedUser!.Name.Should().Be("Transaction User");
        
        retrievedOrder.Should().NotBeNull();
        retrievedOrder!.Total.Should().Be(150.00m);
    }

    [Fact]
    public async Task TransactionGet_WithExecuteAndMapAsync_ReturnsTypedTuple()
    {
        // Arrange
        var user = new TransactionApiUserEntity
        {
            UserId = "USER#005",
            SortKey = "PROFILE",
            Name = "Mapped User",
            Email = "mapped@example.com",
            Status = "active"
        };

        var order = new TransactionApiOrderEntity
        {
            OrderId = "ORDER#005",
            SortKey = "METADATA",
            UserId = "USER#005",
            Total = 250.00m,
            Status = "shipped"
        };

        await _table.Users.Put(user).PutAsync();
        await _table.Orders.Put(order).PutAsync();

        // Act - Use ExecuteAndMapAsync for automatic deserialization
        var (retrievedUser, retrievedOrder) = await DynamoDbTransactions.Get
            .Add(_table.Users.Get("USER#005", "PROFILE"))
            .Add(_table.Orders.Get("ORDER#005", "METADATA"))
            .ExecuteAndMapAsync<TransactionApiUserEntity, TransactionApiOrderEntity>();

        // Assert
        retrievedUser.Should().NotBeNull();
        retrievedUser!.Name.Should().Be("Mapped User");
        
        retrievedOrder.Should().NotBeNull();
        retrievedOrder!.Status.Should().Be("shipped");
    }

    [Fact]
    public async Task TransactionGet_WithItemsFromDifferentTables_WorksCorrectly()
    {
        // Arrange
        var user = new TransactionApiUserEntity
        {
            UserId = "USER#006",
            SortKey = "PROFILE",
            Name = "Multi Table User",
            Email = "multi@example.com",
            Status = "active"
        };

        var secondItem = new TransactionApiSecondEntity
        {
            Id = "ITEM#001",
            SortKey = "DATA",
            Value = "Second Table Value"
        };

        await _table.Users.Put(user).PutAsync();
        await _secondTable.Items.Put(secondItem).PutAsync();

        // Act - Get items from different tables
        var response = await DynamoDbTransactions.Get
            .Add(_table.Users.Get("USER#006", "PROFILE"))
            .Add(_secondTable.Items.Get("ITEM#001", "DATA"))
            .ExecuteAsync();

        // Assert
        response.Count.Should().Be(2);
        
        var retrievedUser = response.GetItem<TransactionApiUserEntity>(0);
        var retrievedSecond = response.GetItem<TransactionApiSecondEntity>(1);

        retrievedUser.Should().NotBeNull();
        retrievedSecond.Should().NotBeNull();
        retrievedSecond!.Value.Should().Be("Second Table Value");
    }

    [Fact]
    public async Task TransactionGet_WithGetItemsRange_RetrievesContiguousItems()
    {
        // Arrange
        var users = new[]
        {
            new TransactionApiUserEntity { UserId = "USER#007", SortKey = "PROFILE", Name = "User 1", Email = "u1@example.com", Status = "active" },
            new TransactionApiUserEntity { UserId = "USER#008", SortKey = "PROFILE", Name = "User 2", Email = "u2@example.com", Status = "active" },
            new TransactionApiUserEntity { UserId = "USER#009", SortKey = "PROFILE", Name = "User 3", Email = "u3@example.com", Status = "active" }
        };

        foreach (var user in users)
        {
            await _table.Users.Put(user).PutAsync();
        }

        // Act
        var response = await DynamoDbTransactions.Get
            .Add(_table.Users.Get("USER#007", "PROFILE"))
            .Add(_table.Users.Get("USER#008", "PROFILE"))
            .Add(_table.Users.Get("USER#009", "PROFILE"))
            .ExecuteAsync();

        var retrievedUsers = response.GetItemsRange<TransactionApiUserEntity>(0, 2);

        // Assert
        retrievedUsers.Should().HaveCount(3);
        retrievedUsers[0]!.Name.Should().Be("User 1");
        retrievedUsers[1]!.Name.Should().Be("User 2");
        retrievedUsers[2]!.Name.Should().Be("User 3");
    }

    #endregion

    #region Task 23.3: Batch Write End-to-End Tests

    [Fact]
    public async Task BatchWrite_WithMultipleTables_GroupsOperationsCorrectly()
    {
        // Arrange
        var users = new[]
        {
            new TransactionApiUserEntity { UserId = "USER#010", SortKey = "PROFILE", Name = "Batch User 1", Email = "b1@example.com", Status = "active" },
            new TransactionApiUserEntity { UserId = "USER#011", SortKey = "PROFILE", Name = "Batch User 2", Email = "b2@example.com", Status = "active" }
        };

        var secondItems = new[]
        {
            new TransactionApiSecondEntity { Id = "ITEM#002", SortKey = "DATA", Value = "Batch Value 1" },
            new TransactionApiSecondEntity { Id = "ITEM#003", SortKey = "DATA", Value = "Batch Value 2" }
        };

        // Act - Batch write to multiple tables
        await DynamoDbBatch.Write
            .Add(_table.Users.Put(users[0]))
            .Add(_table.Users.Put(users[1]))
            .Add(_secondTable.Items.Put(secondItems[0]))
            .Add(_secondTable.Items.Put(secondItems[1]))
            .ExecuteAsync();

        // Assert - All items should be saved
        var user1 = await _table.Users.Get("USER#010", "PROFILE").GetItemAsync();
        var user2 = await _table.Users.Get("USER#011", "PROFILE").GetItemAsync();
        var item1 = await _secondTable.Items.Get("ITEM#002", "DATA").GetItemAsync();
        var item2 = await _secondTable.Items.Get("ITEM#003", "DATA").GetItemAsync();

        user1.Should().NotBeNull();
        user2.Should().NotBeNull();
        item1.Should().NotBeNull();
        item2.Should().NotBeNull();
    }

    [Fact]
    public async Task BatchWrite_WithMixedPutAndDelete_WorksCorrectly()
    {
        // Arrange
        var existingUser = new TransactionApiUserEntity
        {
            UserId = "USER#MIX1",
            SortKey = "PROFILE",
            Name = "Existing User",
            Email = "existing@example.com",
            Status = "active"
        };

        await _table.Users.Put(existingUser).PutAsync();

        var newUser = new TransactionApiUserEntity
        {
            UserId = "USER#MIX2",
            SortKey = "PROFILE",
            Name = "New User",
            Email = "new@example.com",
            Status = "active"
        };

        // Act - Mix put and delete in batch
        await DynamoDbBatch.Write
            .Add(_table.Users.Put(newUser))
            .Add(_table.Users.Delete("USER#MIX1", "PROFILE"))
            .ExecuteAsync();

        // Assert
        var deletedUser = await _table.Users.Get("USER#MIX1", "PROFILE").GetItemAsync();
        var newUserResult = await _table.Users.Get("USER#MIX2", "PROFILE").GetItemAsync();

        deletedUser.Should().BeNull();
        newUserResult.Should().NotBeNull();
        newUserResult!.Name.Should().Be("New User");
    }

    [Fact]
    public async Task BatchWrite_WithDeleteOperations_DeletesCorrectly()
    {
        // Arrange
        var users = new[]
        {
            new TransactionApiUserEntity { UserId = "USER#012", SortKey = "PROFILE", Name = "To Delete 1", Email = "del1@example.com", Status = "active" },
            new TransactionApiUserEntity { UserId = "USER#013", SortKey = "PROFILE", Name = "To Delete 2", Email = "del2@example.com", Status = "active" }
        };

        await _table.Users.Put(users[0]).PutAsync();
        await _table.Users.Put(users[1]).PutAsync();

        // Act - Batch delete
        await DynamoDbBatch.Write
            .Add(_table.Users.Delete("USER#012", "PROFILE"))
            .Add(_table.Users.Delete("USER#013", "PROFILE"))
            .ExecuteAsync();

        // Assert - Items should be deleted
        var user1 = await _table.Users.Get("USER#012", "PROFILE").GetItemAsync();
        var user2 = await _table.Users.Get("USER#013", "PROFILE").GetItemAsync();

        user1.Should().BeNull();
        user2.Should().BeNull();
    }

    #endregion

    #region Task 23.4: Batch Get End-to-End Tests

    [Fact]
    public async Task BatchGet_WithMultipleTables_RetrievesAllItems()
    {
        // Arrange
        var users = new[]
        {
            new TransactionApiUserEntity { UserId = "USER#014", SortKey = "PROFILE", Name = "Batch Get User 1", Email = "bg1@example.com", Status = "active" },
            new TransactionApiUserEntity { UserId = "USER#015", SortKey = "PROFILE", Name = "Batch Get User 2", Email = "bg2@example.com", Status = "active" }
        };

        var secondItems = new[]
        {
            new TransactionApiSecondEntity { Id = "ITEM#004", SortKey = "DATA", Value = "Batch Get Value 1" },
            new TransactionApiSecondEntity { Id = "ITEM#005", SortKey = "DATA", Value = "Batch Get Value 2" }
        };

        await _table.Users.Put(users[0]).PutAsync();
        await _table.Users.Put(users[1]).PutAsync();
        await _secondTable.Items.Put(secondItems[0]).PutAsync();
        await _secondTable.Items.Put(secondItems[1]).PutAsync();

        // Act
        var response = await DynamoDbBatch.Get
            .Add(_table.Users.Get("USER#014", "PROFILE"))
            .Add(_table.Users.Get("USER#015", "PROFILE"))
            .Add(_secondTable.Items.Get("ITEM#004", "DATA"))
            .Add(_secondTable.Items.Get("ITEM#005", "DATA"))
            .ExecuteAsync();

        // Assert
        response.Count.Should().Be(4);
        
        var user1 = response.GetItem<TransactionApiUserEntity>(0);
        var user2 = response.GetItem<TransactionApiUserEntity>(1);
        var item1 = response.GetItem<TransactionApiSecondEntity>(2);
        var item2 = response.GetItem<TransactionApiSecondEntity>(3);

        user1.Should().NotBeNull();
        user2.Should().NotBeNull();
        item1.Should().NotBeNull();
        item2.Should().NotBeNull();
    }

    [Fact]
    public async Task BatchGet_WithExecuteAndMapAsync_ReturnsTypedTuple()
    {
        // Arrange
        var user = new TransactionApiUserEntity
        {
            UserId = "USER#016",
            SortKey = "PROFILE",
            Name = "Batch Mapped User",
            Email = "bm@example.com",
            Status = "active"
        };

        var order = new TransactionApiOrderEntity
        {
            OrderId = "ORDER#016",
            SortKey = "METADATA",
            UserId = "USER#016",
            Total = 300.00m,
            Status = "delivered"
        };

        await _table.Users.Put(user).PutAsync();
        await _table.Orders.Put(order).PutAsync();

        // Act
        var (retrievedUser, retrievedOrder) = await DynamoDbBatch.Get
            .Add(_table.Users.Get("USER#016", "PROFILE"))
            .Add(_table.Orders.Get("ORDER#016", "METADATA"))
            .ExecuteAndMapAsync<TransactionApiUserEntity, TransactionApiOrderEntity>();

        // Assert
        retrievedUser.Should().NotBeNull();
        retrievedOrder.Should().NotBeNull();
        retrievedOrder!.Status.Should().Be("delivered");
    }

    [Fact]
    public async Task BatchGet_WithGetItems_RetrievesMultipleIndices()
    {
        // Arrange
        var users = new[]
        {
            new TransactionApiUserEntity { UserId = "USER#017", SortKey = "PROFILE", Name = "Multi Index 1", Email = "mi1@example.com", Status = "active" },
            new TransactionApiUserEntity { UserId = "USER#018", SortKey = "PROFILE", Name = "Multi Index 2", Email = "mi2@example.com", Status = "active" },
            new TransactionApiUserEntity { UserId = "USER#019", SortKey = "PROFILE", Name = "Multi Index 3", Email = "mi3@example.com", Status = "active" }
        };

        foreach (var user in users)
        {
            await _table.Users.Put(user).PutAsync();
        }

        // Act
        var response = await DynamoDbBatch.Get
            .Add(_table.Users.Get("USER#017", "PROFILE"))
            .Add(_table.Users.Get("USER#018", "PROFILE"))
            .Add(_table.Users.Get("USER#019", "PROFILE"))
            .ExecuteAsync();

        var retrievedUsers = response.GetItems<TransactionApiUserEntity>(0, 2); // Get indices 0 and 2

        // Assert
        retrievedUsers.Should().HaveCount(2);
        retrievedUsers[0]!.Name.Should().Be("Multi Index 1");
        retrievedUsers[1]!.Name.Should().Be("Multi Index 3");
    }

    #endregion

    #region Task 23.5: Client Inference Scenarios

    [Fact]
    public async Task TransactionWrite_WithClientInference_UsesFirstBuilderClient()
    {
        // Arrange
        var user = new TransactionApiUserEntity
        {
            UserId = "USER#020",
            SortKey = "PROFILE",
            Name = "Inferred Client User",
            Email = "inferred@example.com",
            Status = "active"
        };

        // Act - Client should be inferred from first builder
        await DynamoDbTransactions.Write
            .Add(_table.Users.Put(user))
            .ExecuteAsync(); // No client parameter

        // Assert
        var savedUser = await _table.Users.Get("USER#020", "PROFILE").GetItemAsync();
        savedUser.Should().NotBeNull();
    }

    [Fact]
    public async Task TransactionWrite_WithExplicitClient_OverridesInference()
    {
        // Arrange
        var user = new TransactionApiUserEntity
        {
            UserId = "USER#021",
            SortKey = "PROFILE",
            Name = "Explicit Client User",
            Email = "explicit@example.com",
            Status = "active"
        };

        // Act - Explicit client via WithClient
        await DynamoDbTransactions.Write
            .Add(_table.Users.Put(user))
            .WithClient(DynamoDb)
            .ExecuteAsync();

        // Assert
        var savedUser = await _table.Users.Get("USER#021", "PROFILE").GetItemAsync();
        savedUser.Should().NotBeNull();
    }

    [Fact]
    public async Task TransactionWrite_WithExecuteAsyncClient_HasHighestPrecedence()
    {
        // Arrange
        var user = new TransactionApiUserEntity
        {
            UserId = "USER#022",
            SortKey = "PROFILE",
            Name = "ExecuteAsync Client User",
            Email = "execute@example.com",
            Status = "active"
        };

        // Act - Client parameter has highest precedence
        await DynamoDbTransactions.Write
            .Add(_table.Users.Put(user))
            .ExecuteAsync(DynamoDb);

        // Assert
        var savedUser = await _table.Users.Get("USER#022", "PROFILE").GetItemAsync();
        savedUser.Should().NotBeNull();
    }

    [Fact]
    public async Task BatchWrite_WithClientInference_WorksCorrectly()
    {
        // Arrange
        var users = new[]
        {
            new TransactionApiUserEntity { UserId = "USER#023", SortKey = "PROFILE", Name = "Batch Inferred 1", Email = "bi1@example.com", Status = "active" },
            new TransactionApiUserEntity { UserId = "USER#024", SortKey = "PROFILE", Name = "Batch Inferred 2", Email = "bi2@example.com", Status = "active" }
        };

        // Act - Client inferred from builders
        await DynamoDbBatch.Write
            .Add(_table.Users.Put(users[0]))
            .Add(_table.Users.Put(users[1]))
            .ExecuteAsync();

        // Assert
        var user1 = await _table.Users.Get("USER#023", "PROFILE").GetItemAsync();
        var user2 = await _table.Users.Get("USER#024", "PROFILE").GetItemAsync();

        user1.Should().NotBeNull();
        user2.Should().NotBeNull();
    }

    #endregion
}

#region Test Entities

[DynamoDbEntity]
[DynamoDbTable("transaction-api-test", IsDefault = true)]
[GenerateEntityProperty(Name = "Users")]
public partial class TransactionApiUserEntity
{
    [PartitionKey]
    [DynamoDbAttribute("pk")]
    public string UserId { get; set; } = string.Empty;
    
    [SortKey]
    [DynamoDbAttribute("sk")]
    public string SortKey { get; set; } = string.Empty;
    
    [DynamoDbAttribute("name")]
    public string? Name { get; set; }
    
    [DynamoDbAttribute("email")]
    public string? Email { get; set; }
    
    [DynamoDbAttribute("status")]
    public string? Status { get; set; }
}

[DynamoDbEntity]
[DynamoDbTable("transaction-api-test")]
[GenerateEntityProperty(Name = "Orders")]
public partial class TransactionApiOrderEntity
{
    [PartitionKey]
    [DynamoDbAttribute("pk")]
    public string OrderId { get; set; } = string.Empty;
    
    [SortKey]
    [DynamoDbAttribute("sk")]
    public string SortKey { get; set; } = string.Empty;
    
    [DynamoDbAttribute("user_id")]
    public string? UserId { get; set; }
    
    [DynamoDbAttribute("total")]
    public decimal? Total { get; set; }
    
    [DynamoDbAttribute("status")]
    public string? Status { get; set; }
}

[DynamoDbEntity]
[DynamoDbTable("transaction-api-test")]
[GenerateEntityProperty(Name = "Products")]
public partial class TransactionApiProductEntity
{
    [PartitionKey]
    [DynamoDbAttribute("pk")]
    public string ProductId { get; set; } = string.Empty;
    
    [SortKey]
    [DynamoDbAttribute("sk")]
    public string SortKey { get; set; } = string.Empty;
    
    [DynamoDbAttribute("name")]
    public string? Name { get; set; }
    
    [DynamoDbAttribute("price")]
    public decimal? Price { get; set; }
    
    [DynamoDbAttribute("stock")]
    public int? Stock { get; set; }
}

[DynamoDbEntity]
[DynamoDbTable("transaction-api-second")]
[GenerateEntityProperty(Name = "Items")]
public partial class TransactionApiSecondEntity
{
    [PartitionKey]
    [DynamoDbAttribute("pk")]
    public string Id { get; set; } = string.Empty;
    
    [SortKey]
    [DynamoDbAttribute("sk")]
    public string SortKey { get; set; } = string.Empty;
    
    [DynamoDbAttribute("value")]
    public string? Value { get; set; }
}

#endregion
