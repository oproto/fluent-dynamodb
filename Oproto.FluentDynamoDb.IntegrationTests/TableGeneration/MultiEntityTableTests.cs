using Oproto.FluentDynamoDb.Attributes;
using Oproto.FluentDynamoDb.IntegrationTests.Infrastructure;
using Oproto.FluentDynamoDb.IntegrationTests.TestEntities;
using Oproto.FluentDynamoDb.Storage;

namespace Oproto.FluentDynamoDb.IntegrationTests.TableGeneration;

/// <summary>
/// Integration tests for multi-entity table generation.
/// Verifies that tables with multiple entities generate correctly and operations work end-to-end.
/// </summary>
[Collection("DynamoDB Local")]
[Trait("Category", "Integration")]
[Trait("Feature", "TableGeneration")]
public class MultiEntityTableTests : IntegrationTestBase
{
    public MultiEntityTableTests(DynamoDbLocalFixture fixture) : base(fixture)
    {
    }

    [Fact]
    public async Task MultiEntity_WithDefault_GeneratesTableClass_WithCorrectName()
    {
        // Arrange
        await CreateTableAsync<MultiEntityOrderTestEntity>();
        var table = new MultiEntityTestTable(DynamoDb, TableName);

        // Assert - Table class should be generated with table name (not entity name)
        table.Should().NotBeNull();
        table.Name.Should().Be(TableName);
        table.DynamoDbClient.Should().Be(DynamoDb);
    }

    [Fact]
    public async Task MultiEntity_WithDefault_GeneratesEntityAccessorProperties()
    {
        // Arrange
        await CreateTableAsync<MultiEntityOrderTestEntity>();
        var table = new MultiEntityTestTable(DynamoDb, TableName);

        // Assert - Entity accessor properties should be generated
        table.Orders.Should().NotBeNull();
        table.OrderLines.Should().NotBeNull();
    }

    [Fact]
    public async Task MultiEntity_EntityAccessorProperty_GetOperation_WorksEndToEnd()
    {
        // Arrange
        await CreateTableAsync<MultiEntityOrderTestEntity>();
        var table = new MultiEntityTestTable(DynamoDb, TableName);
        
        var order = new MultiEntityOrderTestEntity
        {
            Id = "ORDER#123",
            CustomerName = "John Doe",
            TotalAmount = 99.99m
        };

        // Act - Put item using entity accessor
        await table.Orders.Put(order).PutAsync();

        // Act - Get item using entity accessor
        var result = await table.Orders.Get(order.Id)
            .GetItemAsync();

        // Assert
        result.Should().NotBeNull();
        
        result!.Id.Should().Be(order.Id);
        result.CustomerName.Should().Be(order.CustomerName);
        result.TotalAmount.Should().Be(order.TotalAmount);
    }

    [Fact]
    public async Task MultiEntity_EntityAccessorProperty_QueryOperation_WorksEndToEnd()
    {
        // Arrange
        await CreateTableAsync<MultiEntityOrderTestEntity>();
        var table = new MultiEntityTestTable(DynamoDb, TableName);
        
        var orders = new[]
        {
            new MultiEntityOrderTestEntity { Id = "ORDER#1", CustomerName = "Alice", TotalAmount = 50.00m },
            new MultiEntityOrderTestEntity { Id = "ORDER#2", CustomerName = "Bob", TotalAmount = 75.00m },
            new MultiEntityOrderTestEntity { Id = "ORDER#3", CustomerName = "Charlie", TotalAmount = 100.00m }
        };

        foreach (var order in orders)
        {
            await table.Orders.Put(order).PutAsync();
        }

        // Act - Query using entity accessor
        var result = await table.Orders.Query()
            .Where("pk = :pk")
            .WithValue(":pk", "ORDER#1")
            .ToDynamoDbResponseAsync();

        // Assert
        result.Should().NotBeNull();
        result.Items.Should().HaveCount(1);
        
        var retrieved = MultiEntityOrderTestEntity.FromDynamoDb<MultiEntityOrderTestEntity>(result.Items[0]);
        retrieved.Id.Should().Be("ORDER#1");
        retrieved.CustomerName.Should().Be("Alice");
    }

    [Fact]
    public async Task MultiEntity_EntityAccessorProperty_PutOperation_WorksEndToEnd()
    {
        // Arrange
        await CreateTableAsync<MultiEntityOrderTestEntity>();
        var table = new MultiEntityTestTable(DynamoDb, TableName);
        
        var order = new MultiEntityOrderTestEntity
        {
            Id = "ORDER#456",
            CustomerName = "Jane Smith",
            TotalAmount = 150.00m
        };

        // Act - Put using entity accessor
        await table.Orders.Put(order).PutAsync();

        // Assert - Verify item was saved
        var getResult = await table.Orders.Get(order.Id).GetItemAsync();

        getResult.Should().NotBeNull();
        getResult.Should().BeEquivalentTo(order);
    }

    [Fact]
    public async Task MultiEntity_EntityAccessorProperty_DeleteOperation_WorksEndToEnd()
    {
        // Arrange
        await CreateTableAsync<MultiEntityOrderTestEntity>();
        var table = new MultiEntityTestTable(DynamoDb, TableName);
        
        var order = new MultiEntityOrderTestEntity
        {
            Id = "ORDER#789",
            CustomerName = "Delete Me",
            TotalAmount = 25.00m
        };

        await table.Orders.Put(order).PutAsync();

        // Act - Delete using entity accessor
        await table.Orders.Delete(order.Id)
            .DeleteAsync();

        // Assert - Verify item was deleted
        var getResult = await table.Orders.Get(order.Id)
            .GetItemAsync();

        getResult.Should().BeNull();
    }

    [Fact]
    public async Task MultiEntity_EntityAccessorProperty_UpdateOperation_WorksEndToEnd()
    {
        // Arrange
        await CreateTableAsync<MultiEntityOrderTestEntity>();
        var table = new MultiEntityTestTable(DynamoDb, TableName);
        
        var order = new MultiEntityOrderTestEntity
        {
            Id = "ORDER#999",
            CustomerName = "Original Customer",
            TotalAmount = 200.00m
        };

        await table.Orders.Put(order).PutAsync();

        // Act - Update using entity accessor
        await table.Orders.Update(order.Id)
            .Set("SET #name = :name")
            .WithValue(":name", "Updated Customer")
            .WithAttribute("#name", "customer_name")
            .UpdateAsync();

        // Assert - Verify item was updated
        var getResult = await table.Orders.Get(order.Id)
            .GetItemAsync();

        getResult.CustomerName.Should().Be("Updated Customer");
        getResult.TotalAmount.Should().Be(200.00m); // Unchanged
    }

    [Fact]
    public async Task MultiEntity_EntityAccessorProperty_ScanOperation_WorksEndToEnd()
    {
        // Arrange
        await CreateTableAsync<MultiEntityOrderTestEntity>();
        var table = new MultiEntityTestTable(DynamoDb, TableName);
        
        var orders = new[]
        {
            new MultiEntityOrderTestEntity { Id = "ORDER#A", CustomerName = "Customer A", TotalAmount = 10.00m },
            new MultiEntityOrderTestEntity { Id = "ORDER#B", CustomerName = "Customer B", TotalAmount = 20.00m },
            new MultiEntityOrderTestEntity { Id = "ORDER#C", CustomerName = "Customer C", TotalAmount = 30.00m }
        };

        foreach (var order in orders)
        {
            await table.Orders.Put(order).PutAsync();
        }

        // Act - Scan using entity accessor
        var result = await table.Orders.Scan()
            .ToDynamoDbResponseAsync();

        // Assert
        result.Should().NotBeNull();
        result.Items.Should().HaveCount(3);
        
        var retrievedOrders = result.Items
            .Select(item => MultiEntityOrderTestEntity.FromDynamoDb<MultiEntityOrderTestEntity>(item))
            .ToList();
        
        retrievedOrders.Should().HaveCount(3);
        retrievedOrders.Should().Contain(o => o.Id == "ORDER#A");
        retrievedOrders.Should().Contain(o => o.Id == "ORDER#B");
        retrievedOrders.Should().Contain(o => o.Id == "ORDER#C");
    }

    [Fact]
    public async Task MultiEntity_TableLevelOperations_UseDefaultEntity()
    {
        // Arrange
        await CreateTableAsync<MultiEntityOrderTestEntity>();
        var table = new MultiEntityTestTable(DynamoDb, TableName);
        
        var order = new MultiEntityOrderTestEntity
        {
            Id = "ORDER#DEFAULT",
            CustomerName = "Default Test",
            TotalAmount = 500.00m
        };

        // Act - Use table-level operations (should use default entity type)
        await table.Put<MultiEntityOrderTestEntity>().WithItem(order).PutAsync();
        
        var getResult = await table.Get<MultiEntityOrderTestEntity>()
            .WithKey("pk", order.Id)
            .GetItemAsync();

        // Assert - Table-level operations should work with default entity
        getResult.Should().NotBeNull();
        getResult.Should().BeEquivalentTo(order);
    }

    [Fact]
    public async Task MultiEntity_TableLevelQuery_UsesDefaultEntity()
    {
        // Arrange
        await CreateTableAsync<MultiEntityOrderTestEntity>();
        var table = new MultiEntityTestTable(DynamoDb, TableName);
        
        var orders = new[]
        {
            new MultiEntityOrderTestEntity { Id = "ORDER#X", CustomerName = "X Customer", TotalAmount = 100.00m },
            new MultiEntityOrderTestEntity { Id = "ORDER#Y", CustomerName = "Y Customer", TotalAmount = 200.00m }
        };

        foreach (var order in orders)
        {
            await table.Put<MultiEntityOrderTestEntity>().WithItem(order).PutAsync();
        }

        // Act - Query at table level (should use default entity)
        var result = await table.Query<MultiEntityOrderTestEntity>()
            .Where("pk = :pk")
            .WithValue(":pk", "ORDER#X")
            .ToDynamoDbResponseAsync();

        // Assert
        result.Items.Should().HaveCount(1);
        var retrieved = MultiEntityOrderTestEntity.FromDynamoDb<MultiEntityOrderTestEntity>(result.Items[0]);
        retrieved.Id.Should().Be("ORDER#X");
    }

    [Fact]
    public async Task MultiEntity_DifferentEntityTypes_WorkIndependently()
    {
        // Arrange
        await CreateTableAsync<MultiEntityOrderTestEntity>();
        var table = new MultiEntityTestTable(DynamoDb, TableName);
        
        var order = new MultiEntityOrderTestEntity
        {
            Id = "ORDER#100",
            CustomerName = "Order Customer",
            TotalAmount = 100.00m
        };

        var orderLine = new MultiEntityOrderLineTestEntity
        {
            Id = "ORDERLINE#100#1",
            ProductName = "Product A",
            Quantity = 5
        };

        // Act - Save both entity types
        await table.Orders.Put(order).PutAsync();
        await table.OrderLines.Put(orderLine).PutAsync();

        // Act - Retrieve both entity types
        var orderResult = await table.Orders.Get(order.Id)
            .GetItemAsync();

        var orderLineResult = await table.OrderLines.Get(orderLine.Id)
            .GetItemAsync();

        // Assert - Both entities should be retrievable with correct types
        orderResult.Should().NotBeNull();
        orderResult.Id.Should().Be(order.Id);
        orderResult.CustomerName.Should().Be(order.CustomerName);

        orderLineResult.Should().NotBeNull();
        orderLineResult.Id.Should().Be(orderLine.Id);
        orderLineResult.ProductName.Should().Be(orderLine.ProductName);
    }

    [Fact]
    public async Task MultiEntity_EntityAccessors_AreStronglyTyped()
    {
        // Arrange
        await CreateTableAsync<MultiEntityOrderTestEntity>();
        var table = new MultiEntityTestTable(DynamoDb, TableName);
        
        var order = new MultiEntityOrderTestEntity
        {
            Id = "ORDER#TYPE",
            CustomerName = "Type Test",
            TotalAmount = 75.00m
        };

        // Act - Operations should return strongly-typed builders
        var putBuilder = table.Orders.Put(order);
        var getBuilder = table.Orders.Get("id");
        var queryBuilder = table.Orders.Query();
        var deleteBuilder = table.Orders.Delete("id");
        var updateBuilder = table.Orders.Update("id");
        var scanBuilder = table.Orders.Scan();

        // Assert - Verify builders are of correct types (compile-time check)
        putBuilder.Should().NotBeNull();
        getBuilder.Should().NotBeNull();
        queryBuilder.Should().NotBeNull();
        deleteBuilder.Should().NotBeNull();
        updateBuilder.Should().NotBeNull();
        scanBuilder.Should().NotBeNull();
    }

    [Fact]
    public async Task MultiEntity_MixedOperations_WorkCorrectly()
    {
        // Arrange
        await CreateTableAsync<MultiEntityOrderTestEntity>();
        var table = new MultiEntityTestTable(DynamoDb, TableName);
        
        var order = new MultiEntityOrderTestEntity
        {
            Id = "ORDER#MIX",
            CustomerName = "Mixed Test",
            TotalAmount = 300.00m
        };

        var orderLine = new MultiEntityOrderLineTestEntity
        {
            Id = "ORDERLINE#MIX#1",
            ProductName = "Mixed Product",
            Quantity = 10
        };

        // Act - Mix table-level and entity accessor operations
        await table.Put<MultiEntityOrderTestEntity>().WithItem(order).ToDynamoDbResponseAsync(); // Table-level (default entity)
        await table.OrderLines.Put().WithItem(orderLine).PutAsync(); // Entity accessor

        var orderResult = await table.Get<MultiEntityOrderTestEntity>() // Table-level (default entity)
            .WithKey("pk", order.Id)
            .GetItemAsync();

        var orderLineResult = await table.OrderLines.Get(orderLine.Id).GetItemAsync(); // Entity accessor

        // Assert - Both operations should work correctly
        orderResult.Should().NotBeNull();
        orderLineResult.Should().NotBeNull();
        
        orderResult.Id.Should().Be(order.Id);

        orderLineResult.Id.Should().Be(orderLine.Id);
    }
}

/// <summary>
/// Test entity representing an Order in a multi-entity table.
/// This is the default entity for the table.
/// </summary>
[DynamoDbTable("multi-entity-test", IsDefault = true)]
[GenerateEntityProperty(Name = "Orders")]
[Scannable]
public partial class MultiEntityOrderTestEntity
{
    [PartitionKey]
    [DynamoDbAttribute("pk")]
    public string Id { get; set; } = string.Empty;
    
    [DynamoDbAttribute("customer_name")]
    public string? CustomerName { get; set; }
    
    [DynamoDbAttribute("total_amount")]
    public decimal? TotalAmount { get; set; }
    
    [DynamoDbAttribute("item")]
    public string? Item { get; set; }
}

/// <summary>
/// Test entity representing an OrderLine in a multi-entity table.
/// This is a non-default entity sharing the same table.
/// </summary>
[DynamoDbTable("multi-entity-test")]
[GenerateEntityProperty(Name = "OrderLines")]
[Scannable]
public partial class MultiEntityOrderLineTestEntity
{
    [PartitionKey]
    [DynamoDbAttribute("pk")]
    public string Id { get; set; } = string.Empty;
    
    [DynamoDbAttribute("product_name")]
    public string? ProductName { get; set; }
    
    [DynamoDbAttribute("quantity")]
    public int? Quantity { get; set; }
}
