using Oproto.FluentDynamoDb.Attributes;
using Oproto.FluentDynamoDb.IntegrationTests.Infrastructure;

namespace Oproto.FluentDynamoDb.IntegrationTests.TableGeneration;

/// <summary>
/// Integration tests for transaction and batch operations in table generation.
/// Verifies that TransactWrite, TransactGet, BatchWrite, and BatchGet work correctly
/// with multiple entity types in a single table.
/// </summary>
[Collection("DynamoDB Local")]
[Trait("Category", "Integration")]
[Trait("Feature", "TableGeneration")]
[Trait("Feature", "Transactions")]
public class TransactionOperationTests : IntegrationTestBase
{
    public TransactionOperationTests(DynamoDbLocalFixture fixture) : base(fixture)
    {
    }

    [Fact]
    public async Task TransactWrite_WithMultipleEntityTypes_WorksEndToEnd()
    {
        // Arrange
        await CreateTableAsync<TransactionOrderEntity>();
        var table = new TransactionTestTable(DynamoDb, TableName);
        
        var order = new TransactionOrderEntity
        {
            Id = "ORDER#TXN1",
            CustomerName = "Transaction Customer",
            TotalAmount = 250.00m
        };

        var orderLine = new TransactionOrderLineEntity
        {
            Id = "ORDERLINE#TXN1#1",
            ProductName = "Transaction Product",
            Quantity = 3
        };

        // Act - Use TransactWrite to save both entities atomically
        await DynamoDbTransactions.Write
            .Add(table.Orders.Put(order))
            .Add(table.OrderLines.Put(orderLine))
            .ExecuteAsync();

        // Assert - Both entities should be saved
        var orderResult = await table.Orders.Get(order.Id)
            .GetItemAsync();

        var orderLineResult = await table.OrderLines.Get(orderLine.Id)
            .GetItemAsync();

        orderResult.Should().NotBeNull();
        orderLineResult.Should().NotBeNull();
        
        orderResult.Id.Should().Be(order.Id);
        orderResult.CustomerName.Should().Be(order.CustomerName);

        orderLineResult.Id.Should().Be(orderLine.Id);
        orderLineResult.ProductName.Should().Be(orderLine.ProductName);
    }

    [Fact]
    public async Task TransactWrite_WithMixedOperations_WorksEndToEnd()
    {
        // Arrange
        await CreateTableAsync<TransactionOrderEntity>();
        var table = new TransactionTestTable(DynamoDb, TableName);
        
        var existingOrder = new TransactionOrderEntity
        {
            Id = "ORDER#EXISTING",
            CustomerName = "Existing Customer",
            TotalAmount = 100.00m
        };

        await table.Orders.Put(existingOrder).PutAsync();

        var newOrder = new TransactionOrderEntity
        {
            Id = "ORDER#NEW",
            CustomerName = "New Customer",
            TotalAmount = 200.00m
        };

        var newOrderLine = new TransactionOrderLineEntity
        {
            Id = "ORDERLINE#NEW#1",
            ProductName = "New Product",
            Quantity = 5
        };

        // Act - Use TransactWrite with Put, Update, and Delete operations
        await DynamoDbTransactions.Write
            .Add(table.Orders.Put(newOrder))
            .Add(table.OrderLines.Put(newOrderLine))
            .Add(table.Orders.Update(existingOrder.Id)
                .Set("SET #name = :name")
                .WithValue(":name", "Updated Customer")
                .WithAttribute("#name", "customer_name"))
            .ExecuteAsync();

        // Assert - All operations should be applied
        var newOrderResult = await table.Orders.Get(newOrder.Id)
            .GetItemAsync();

        var newOrderLineResult = await table.OrderLines.Get(newOrderLine.Id)
            .GetItemAsync();

        var updatedOrderResult = await table.Orders.Get(existingOrder.Id)
            .GetItemAsync();

        newOrderResult.Should().NotBeNull();
        newOrderLineResult.Should().NotBeNull();
        updatedOrderResult.Should().NotBeNull();
        
        updatedOrderResult.CustomerName.Should().Be("Updated Customer");
    }

    [Fact]
    public async Task TransactGet_WithMultipleEntityTypes_WorksEndToEnd()
    {
        // Arrange
        await CreateTableAsync<TransactionOrderEntity>();
        var table = new TransactionTestTable(DynamoDb, TableName);
        
        var order = new TransactionOrderEntity
        {
            Id = "ORDER#TGET1",
            CustomerName = "TransactGet Customer",
            TotalAmount = 300.00m
        };

        var orderLine = new TransactionOrderLineEntity
        {
            Id = "ORDERLINE#TGET1#1",
            ProductName = "TransactGet Product",
            Quantity = 7
        };

        await table.Orders.Put(order).PutAsync();
        await table.OrderLines.Put(orderLine).PutAsync();

        // Act - Use TransactGet to retrieve both entities atomically
        var result = await DynamoDbTransactions.Get
            .Add(table.Orders.Get(order.Id))
            .Add(table.OrderLines.Get(orderLine.Id))
            .ExecuteAsync();

        // Assert - Both entities should be retrieved
        result.Should().NotBeNull();
        result.Count.Should().Be(2);
        
        var retrievedOrder = result.GetItem<TransactionOrderEntity>(0);
        retrievedOrder.Id.Should().Be(order.Id);
        retrievedOrder.CustomerName.Should().Be(order.CustomerName);

        var retrievedOrderLine = result.GetItem<TransactionOrderLineEntity>(1);
        retrievedOrderLine.Id.Should().Be(orderLine.Id);
        retrievedOrderLine.ProductName.Should().Be(orderLine.ProductName);
    }

    [Fact]
    public async Task BatchWrite_WithMultipleEntityTypes_WorksEndToEnd()
    {
        // Arrange
        await CreateTableAsync<TransactionOrderEntity>();
        var table = new TransactionTestTable(DynamoDb, TableName);
        
        var orders = new[]
        {
            new TransactionOrderEntity { Id = "ORDER#BATCH1", CustomerName = "Batch Customer 1", TotalAmount = 50.00m },
            new TransactionOrderEntity { Id = "ORDER#BATCH2", CustomerName = "Batch Customer 2", TotalAmount = 75.00m }
        };

        var orderLines = new[]
        {
            new TransactionOrderLineEntity { Id = "ORDERLINE#BATCH1#1", ProductName = "Batch Product 1", Quantity = 2 },
            new TransactionOrderLineEntity { Id = "ORDERLINE#BATCH2#1", ProductName = "Batch Product 2", Quantity = 4 }
        };

        // Act - Use BatchWrite to save multiple entities of different types
        await DynamoDbBatch.Write
            .Add(table.Orders.Put(orders[0]))
            .Add(table.Orders.Put(orders[1]))
            .Add(table.OrderLines.Put(orderLines[0]))
            .Add(table.OrderLines.Put(orderLines[1]))
            .ExecuteAsync();

        // Assert - All entities should be saved
        var order1Result = await table.Orders.Get(orders[0].Id).ToDynamoDbResponseAsync();
        var order2Result = await table.Orders.Get(orders[1].Id).ToDynamoDbResponseAsync();
        var orderLine1Result = await table.OrderLines.Get(orderLines[0].Id).ToDynamoDbResponseAsync();
        var orderLine2Result = await table.OrderLines.Get(orderLines[1].Id).ToDynamoDbResponseAsync();

        order1Result.Item.Should().NotBeNull();
        order2Result.Item.Should().NotBeNull();
        orderLine1Result.Item.Should().NotBeNull();
        orderLine2Result.Item.Should().NotBeNull();
        
        var retrievedOrder1 = TransactionOrderEntity.FromDynamoDb<TransactionOrderEntity>(order1Result.Item);
        retrievedOrder1.CustomerName.Should().Be("Batch Customer 1");

        var retrievedOrderLine1 = TransactionOrderLineEntity.FromDynamoDb<TransactionOrderLineEntity>(orderLine1Result.Item);
        retrievedOrderLine1.ProductName.Should().Be("Batch Product 1");
    }

    [Fact]
    public async Task BatchGet_WithMultipleEntityTypes_WorksEndToEnd()
    {
        // Arrange
        await CreateTableAsync<TransactionOrderEntity>();
        var table = new TransactionTestTable(DynamoDb, TableName);
        
        var orders = new[]
        {
            new TransactionOrderEntity { Id = "ORDER#BGET1", CustomerName = "BatchGet Customer 1", TotalAmount = 100.00m },
            new TransactionOrderEntity { Id = "ORDER#BGET2", CustomerName = "BatchGet Customer 2", TotalAmount = 150.00m }
        };

        var orderLines = new[]
        {
            new TransactionOrderLineEntity { Id = "ORDERLINE#BGET1#1", ProductName = "BatchGet Product 1", Quantity = 3 },
            new TransactionOrderLineEntity { Id = "ORDERLINE#BGET2#1", ProductName = "BatchGet Product 2", Quantity = 6 }
        };

        await table.Orders.Put(orders[0]).PutAsync();
        await table.Orders.Put(orders[1]).PutAsync();
        await table.OrderLines.Put(orderLines[0]).PutAsync();
        await table.OrderLines.Put(orderLines[1]).PutAsync();

        // Act - Use BatchGet to retrieve multiple entities of different types
        var result = await DynamoDbBatch.Get
            .Add(table.Orders.Get(orders[0].Id))
            .Add(table.Orders.Get(orders[1].Id))
            .Add(table.OrderLines.Get(orderLines[0].Id))
            .Add(table.OrderLines.Get(orderLines[1].Id))
            .ExecuteAsync();

        // Assert - All entities should be retrieved
        result.Should().NotBeNull();
        result.Count.Should().Be(4);
        
        // Get items by index (in the order they were added)
        var retrievedOrder1 = result.GetItem<TransactionOrderEntity>(0);
        retrievedOrder1.CustomerName.Should().Be("BatchGet Customer 1");
        
        var retrievedOrder2 = result.GetItem<TransactionOrderEntity>(1);
        retrievedOrder2.CustomerName.Should().Be("BatchGet Customer 2");
        
        var retrievedOrderLine1 = result.GetItem<TransactionOrderLineEntity>(2);
        retrievedOrderLine1.ProductName.Should().Be("BatchGet Product 1");
        
        var retrievedOrderLine2 = result.GetItem<TransactionOrderLineEntity>(3);
        retrievedOrderLine2.ProductName.Should().Be("BatchGet Product 2");
    }

    [Fact]
    public async Task TransactWrite_WithDelete_WorksEndToEnd()
    {
        // Arrange
        await CreateTableAsync<TransactionOrderEntity>();
        var table = new TransactionTestTable(DynamoDb, TableName);
        
        var orderToDelete = new TransactionOrderEntity
        {
            Id = "ORDER#DELETE",
            CustomerName = "To Delete",
            TotalAmount = 50.00m
        };

        var orderToKeep = new TransactionOrderEntity
        {
            Id = "ORDER#KEEP",
            CustomerName = "To Keep",
            TotalAmount = 100.00m
        };

        await table.Orders.Put(orderToDelete).PutAsync();
        await table.Orders.Put(orderToKeep).PutAsync();

        // Act - Use TransactWrite to delete one order and update another
        await DynamoDbTransactions.Write
            .Add(table.Orders.Delete(orderToDelete.Id))
            .Add(table.Orders.Update(orderToKeep.Id)
                .Set("SET #name = :name")
                .WithValue(":name", "Updated Keep")
                .WithAttribute("#name", "customer_name"))
            .ExecuteAsync();

        // Assert - One order should be deleted, the other updated
        var deletedResult = await table.Orders.Get(orderToDelete.Id).GetItemAsync();
        var keptResult = await table.Orders.Get(orderToKeep.Id).GetItemAsync();

        deletedResult.Should().BeNull();
        keptResult.Should().NotBeNull();
        
        keptResult.CustomerName.Should().Be("Updated Keep");
    }

    [Fact]
    public async Task BatchWrite_WithDelete_WorksEndToEnd()
    {
        // Arrange
        await CreateTableAsync<TransactionOrderEntity>();
        var table = new TransactionTestTable(DynamoDb, TableName);
        
        var ordersToDelete = new[]
        {
            new TransactionOrderEntity { Id = "ORDER#BDEL1", CustomerName = "Batch Delete 1", TotalAmount = 25.00m },
            new TransactionOrderEntity { Id = "ORDER#BDEL2", CustomerName = "Batch Delete 2", TotalAmount = 35.00m }
        };

        await table.Orders.Put(ordersToDelete[0]).PutAsync();
        await table.Orders.Put(ordersToDelete[1]).PutAsync();

        // Act - Use BatchWrite to delete multiple orders
        await DynamoDbBatch.Write
            .Add(table.Orders.Delete(ordersToDelete[0].Id))
            .Add(table.Orders.Delete(ordersToDelete[1].Id))
            .ExecuteAsync();

        // Assert - Both orders should be deleted
        var result1 = await table.Orders.Get(ordersToDelete[0].Id).GetItemAsync();
        var result2 = await table.Orders.Get(ordersToDelete[1].Id).GetItemAsync();

        result1.Should().BeNull();
        result2.Should().BeNull();
    }

    [Fact]
    public async Task TransactionOperations_AccessibleViaStaticEntryPoints()
    {
        // Arrange
        await CreateTableAsync<TransactionOrderEntity>();

        // Assert - Transaction methods should be accessible via static entry points
        var transactWrite = DynamoDbTransactions.Write;
        var transactGet = DynamoDbTransactions.Get;
        var batchWrite = DynamoDbBatch.Write;
        var batchGet = DynamoDbBatch.Get;

        transactWrite.Should().NotBeNull();
        transactGet.Should().NotBeNull();
        batchWrite.Should().NotBeNull();
        batchGet.Should().NotBeNull();

        // Note: Transaction operations are now accessed via static entry points
        // DynamoDbTransactions and DynamoDbBatch, not via table instances.
    }

    [Fact]
    public async Task TransactWrite_WithConditionCheck_WorksEndToEnd()
    {
        // Arrange
        await CreateTableAsync<TransactionOrderEntity>();
        var table = new TransactionTestTable(DynamoDb, TableName);
        
        var existingOrder = new TransactionOrderEntity
        {
            Id = "ORDER#CONDITION",
            CustomerName = "Condition Customer",
            TotalAmount = 100.00m
        };

        await table.Orders.Put(existingOrder).PutAsync();

        var newOrder = new TransactionOrderEntity
        {
            Id = "ORDER#CONDITIONAL",
            CustomerName = "New Conditional",
            TotalAmount = 200.00m
        };

        // Act - Use TransactWrite with condition check
        await DynamoDbTransactions.Write
            .Add(table.Orders.ConditionCheck(existingOrder.Id)
                .Where("attribute_exists(pk)"))
            .Add(table.Orders.Put(newOrder))
            .ExecuteAsync();

        // Assert - New order should be saved (condition was met)
        var result = await table.Orders.Get(newOrder.Id).ToDynamoDbResponseAsync();
        result.Item.Should().NotBeNull();
        
        var retrieved = TransactionOrderEntity.FromDynamoDb<TransactionOrderEntity>(result.Item);
        retrieved.CustomerName.Should().Be("New Conditional");
    }

    [Fact]
    public async Task TransactionOperations_AcceptAnyEntityType()
    {
        // Arrange
        await CreateTableAsync<TransactionOrderEntity>();
        var table = new TransactionTestTable(DynamoDb, TableName);
        
        var order = new TransactionOrderEntity
        {
            Id = "ORDER#ANYTYPE",
            CustomerName = "Any Type Customer",
            TotalAmount = 150.00m
        };

        var orderLine = new TransactionOrderLineEntity
        {
            Id = "ORDERLINE#ANYTYPE#1",
            ProductName = "Any Type Product",
            Quantity = 8
        };

        var payment = new TransactionPaymentTestEntity
        {
            Id = "PAYMENT#ANYTYPE",
            Amount = 150.00m,
            PaymentMethod = "Credit Card"
        };

        // Act - Transaction operations should accept all entity types registered to the table
        await DynamoDbTransactions.Write
            .Add(table.Orders.Put(order))
            .Add(table.OrderLines.Put(orderLine))
            .Add(table.Payments.Put(payment))
            .ExecuteAsync();

        // Assert - All entities should be saved
        var orderResult = await table.Orders.Get(order.Id).ToDynamoDbResponseAsync();
        var orderLineResult = await table.OrderLines.Get(orderLine.Id).ToDynamoDbResponseAsync();
        var paymentResult = await table.Payments.Get(payment.Id).ToDynamoDbResponseAsync();

        orderResult.Item.Should().NotBeNull();
        orderLineResult.Item.Should().NotBeNull();
        paymentResult.Item.Should().NotBeNull();
    }
}

/// <summary>
/// Test entity representing an Order for transaction tests.
/// This is the default entity for the table.
/// </summary>
[DynamoDbEntity]
[DynamoDbTable("transaction-test", IsDefault = true)]
[GenerateEntityProperty(Name = "Orders")]
public partial class TransactionOrderEntity
{
    [PartitionKey]
    [DynamoDbAttribute("pk")]
    public string Id { get; set; } = string.Empty;
    
    [DynamoDbAttribute("customer_name")]
    public string? CustomerName { get; set; }
    
    [DynamoDbAttribute("total_amount")]
    public decimal? TotalAmount { get; set; }
}

/// <summary>
/// Test entity representing an OrderLine for transaction tests.
/// </summary>
[DynamoDbEntity]
[DynamoDbTable("transaction-test")]
[GenerateEntityProperty(Name = "OrderLines")]
public partial class TransactionOrderLineEntity
{
    [PartitionKey]
    [DynamoDbAttribute("pk")]
    public string Id { get; set; } = string.Empty;
    
    [DynamoDbAttribute("product_name")]
    public string? ProductName { get; set; }
    
    [DynamoDbAttribute("quantity")]
    public int? Quantity { get; set; }
}

/// <summary>
/// Test entity representing a Payment for transaction tests.
/// </summary>
[DynamoDbEntity]
[DynamoDbTable("transaction-test")]
[GenerateEntityProperty(Name = "Payments")]
public partial class TransactionPaymentTestEntity
{
    [PartitionKey]
    [DynamoDbAttribute("pk")]
    public string Id { get; set; } = string.Empty;
    
    [DynamoDbAttribute("amount")]
    public decimal? Amount { get; set; }
    
    [DynamoDbAttribute("payment_method")]
    public string? PaymentMethod { get; set; }
}
