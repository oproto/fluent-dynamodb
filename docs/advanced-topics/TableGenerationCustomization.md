---
title: "Table Generation Customization"
category: "advanced-topics"
order: 3
keywords: ["customization", "entity accessors", "visibility modifiers", "partial classes", "code generation"]
related: ["MultiEntityTables.md", "../getting-started/SingleEntityTables.md", "../core-features/EntityDefinition.md"]
---

[Documentation](../README.md) > [Advanced Topics](README.md) > Table Generation Customization

# Table Generation Customization

[Previous: Multi-Entity Tables](MultiEntityTables.md) | [Next: Composite Entities](CompositeEntities.md)

---

This guide covers advanced customization options for controlling how table classes and entity accessors are generated. Use these features to create clean public APIs while hiding implementation details.

## Overview

The source generator provides fine-grained control over:

- **Entity accessor properties** - Customize names, visibility, and whether they're generated
- **Operation methods** - Control which operations are generated and their visibility
- **Partial classes** - Extend generated code with custom public methods

**Key Benefits:**
- Hide implementation details from consumers
- Create custom public APIs with validation and business logic
- Control what's exposed in your library's public surface
- Maintain clean separation between generated and custom code

## Customization Attributes

### GenerateEntityProperty Attribute

Controls how entity accessor properties are generated:

```csharp
[AttributeUsage(AttributeTargets.Class)]
public class GenerateEntityPropertyAttribute : Attribute
{
    // Custom name for the accessor property
    public string? Name { get; set; }
    
    // Whether to generate the accessor property
    public bool Generate { get; set; } = true;
    
    // Visibility modifier for the accessor property
    public AccessModifier Modifier { get; set; } = AccessModifier.Public;
}
```

### GenerateAccessors Attribute

Controls which operation methods are generated and their visibility:

```csharp
[AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
public class GenerateAccessorsAttribute : Attribute
{
    // Which operations to configure
    public TableOperation Operations { get; set; } = TableOperation.All;
    
    // Whether to generate the operations
    public bool Generate { get; set; } = true;
    
    // Visibility modifier for the operations
    public AccessModifier Modifier { get; set; } = AccessModifier.Public;
}
```

### AccessModifier Enum

Defines visibility levels:

```csharp
public enum AccessModifier
{
    Public,      // Accessible everywhere
    Internal,    // Accessible within assembly
    Protected,   // Accessible in derived classes
    Private      // Accessible only within class
}
```

### TableOperation Enum

Defines DynamoDB operations (flags enum):

```csharp
[Flags]
public enum TableOperation
{
    Get = 1,
    Query = 2,
    Scan = 4,
    Put = 8,
    Delete = 16,
    Update = 32,
    All = Get | Query | Scan | Put | Delete | Update
}
```

## Custom Entity Accessor Names

### Default Naming

By default, entity accessor properties are named by pluralizing the entity class name:

```csharp
[DynamoDbTable("ecommerce", IsDefault = true)]
public partial class Order { }

[DynamoDbTable("ecommerce")]
public partial class OrderLine { }

// Generated:
// public OrderAccessor Orders { get; }
// public OrderLineAccessor OrderLines { get; }
```

### Custom Names

Use the `Name` property to specify custom accessor names:

```csharp
[DynamoDbTable("ecommerce", IsDefault = true)]
[GenerateEntityProperty(Name = "CustomerOrders")]
public partial class Order
{
    [PartitionKey]
    [DynamoDbAttribute("pk")]
    public string CustomerId { get; set; } = string.Empty;
    
    [SortKey]
    [DynamoDbAttribute("sk")]
    [Computed("ORDER#{OrderId}")]
    public string OrderId { get; set; } = string.Empty;
    
    [DynamoDbAttribute("total")]
    public decimal Total { get; set; }
}

[DynamoDbTable("ecommerce")]
[GenerateEntityProperty(Name = "Lines")]
public partial class OrderLine
{
    [PartitionKey]
    [DynamoDbAttribute("pk")]
    public string CustomerId { get; set; } = string.Empty;
    
    [SortKey]
    [DynamoDbAttribute("sk")]
    [Computed("ORDER#{OrderId}#LINE#{LineNumber:D3}")]
    public string OrderId { get; set; } = string.Empty;
    
    public int LineNumber { get; set; }
    
    [DynamoDbAttribute("productId")]
    public string ProductId { get; set; } = string.Empty;
}
```

**Generated code:**

```csharp
public partial class EcommerceTable : DynamoDbTableBase
{
    public EcommerceTable(IAmazonDynamoDB client) 
        : base(client, "ecommerce")
    {
        CustomerOrders = new OrderAccessor(this);
        Lines = new OrderLineAccessor(this);
    }
    
    // Custom names
    public OrderAccessor CustomerOrders { get; }
    public OrderLineAccessor Lines { get; }
}
```

**Usage:**

```csharp
var table = new EcommerceTable(client, "ecommerce");

// Use custom names
var orders = await table.CustomerOrders.Query()
    .Where($"{OrderFields.CustomerId} = :pk", new { pk = "customer123" })
    .ExecuteAsync();

var lines = await table.Lines.Query()
    .Where($"{OrderLineFields.CustomerId} = :pk", new { pk = "customer123" })
    .ExecuteAsync();
```

## Disabling Entity Accessor Generation

### Generate = false

Use `Generate = false` to prevent accessor property generation:


```csharp
[DynamoDbTable("ecommerce", IsDefault = true)]
public partial class Order { }

[DynamoDbTable("ecommerce")]
[GenerateEntityProperty(Generate = false)]
public partial class InternalAuditLog
{
    [PartitionKey]
    [DynamoDbAttribute("pk")]
    public string CustomerId { get; set; } = string.Empty;
    
    [SortKey]
    [DynamoDbAttribute("sk")]
    [Computed("AUDIT#{Timestamp:yyyy-MM-ddTHH:mm:ss}")]
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    
    [DynamoDbAttribute("action")]
    public string Action { get; set; } = string.Empty;
    
    [DynamoDbAttribute("details")]
    public string Details { get; set; } = string.Empty;
}
```

**Generated code:**

```csharp
public partial class EcommerceTable : DynamoDbTableBase
{
    public EcommerceTable(IAmazonDynamoDB client) 
        : base(client, "ecommerce")
    {
        Orders = new OrderAccessor(this);
        // No InternalAuditLogs accessor generated
    }
    
    public OrderAccessor Orders { get; }
    // InternalAuditLog accessor is NOT generated
}
```

**Use Case:** Hide internal entities that shouldn't be exposed in the public API. You can still work with these entities through custom methods or by directly using the metadata classes.

### Working with Hidden Entities

Even though the accessor isn't generated, you can still work with the entity:

```csharp
public partial class EcommerceTable
{
    // Custom internal method to work with audit logs
    internal async Task LogActionAsync(string customerId, string action, string details)
    {
        var auditLog = new InternalAuditLog
        {
            CustomerId = customerId,
            Timestamp = DateTime.UtcNow,
            Action = action,
            Details = details
        };
        
        // Use the metadata directly
        var request = new PutItemRequestBuilder<InternalAuditLog>(
            Client, 
            TableName, 
            InternalAuditLogMetadata.Instance, 
            auditLog);
        
        await request.ExecuteAsync();
    }
}
```

## Entity Accessor Visibility Modifiers

### Internal Accessors

Make entity accessors internal to hide them from external assemblies:


```csharp
[DynamoDbTable("ecommerce", IsDefault = true)]
public partial class Order { }

[DynamoDbTable("ecommerce")]
[GenerateEntityProperty(Modifier = AccessModifier.Internal)]
public partial class OrderLine
{
    [PartitionKey]
    [DynamoDbAttribute("pk")]
    public string CustomerId { get; set; } = string.Empty;
    
    [SortKey]
    [DynamoDbAttribute("sk")]
    [Computed("ORDER#{OrderId}#LINE#{LineNumber:D3}")]
    public string OrderId { get; set; } = string.Empty;
    
    public int LineNumber { get; set; }
    
    [DynamoDbAttribute("productId")]
    public string ProductId { get; set; } = string.Empty;
    
    [DynamoDbAttribute("quantity")]
    public int Quantity { get; set; }
    
    [DynamoDbAttribute("price")]
    public decimal Price { get; set; }
}
```

**Generated code:**

```csharp
public partial class EcommerceTable : DynamoDbTableBase
{
    public EcommerceTable(IAmazonDynamoDB client) 
        : base(client, "ecommerce")
    {
        Orders = new OrderAccessor(this);
        OrderLines = new OrderLineAccessor(this);
    }
    
    public OrderAccessor Orders { get; }
    internal OrderLineAccessor OrderLines { get; }  // Internal visibility
}
```

**Use Case:** Hide low-level accessors and expose only custom public methods:

```csharp
public partial class EcommerceTable
{
    // Public method that uses internal accessor
    public async Task<List<OrderLine>> GetOrderLinesAsync(string customerId, string orderId)
    {
        var response = await OrderLines.Query()  // Internal accessor
            .Where($"{OrderLineFields.CustomerId} = :pk AND begins_with({OrderLineFields.OrderId}, :sk)",
                   new { pk = customerId, sk = $"ORDER#{orderId}#LINE#" })
            .ExecuteAsync();
        
        return response.Items;
    }
    
    public async Task AddOrderLineAsync(string customerId, string orderId, OrderLine line)
    {
        // Validation logic
        if (line.Quantity <= 0)
            throw new ArgumentException("Quantity must be positive");
        
        // Use internal accessor
        await OrderLines.Put(line).ExecuteAsync();
    }
}
```

**External usage:**

```csharp
// External assembly can only use public methods
var table = new EcommerceTable(client, "ecommerce");

// ✅ Public method works
var lines = await table.GetOrderLinesAsync("customer123", "order456");

// ❌ Compile error - OrderLines is internal
// var response = await table.OrderLines.Query()...
```

### Protected and Private Accessors

Use `Protected` or `Private` for inheritance scenarios:

```csharp
[DynamoDbTable("ecommerce")]
[GenerateEntityProperty(Modifier = AccessModifier.Protected)]
public partial class OrderMetrics
{
    // Accessible in derived classes only
}
```

## Operation Method Customization

### Selective Operation Generation

Generate only specific operations:


```csharp
[DynamoDbTable("ecommerce", IsDefault = true)]
public partial class Order { }

[DynamoDbTable("ecommerce")]
[GenerateAccessors(Operations = TableOperation.Get | TableOperation.Query)]
public partial class ReadOnlyEntity
{
    [PartitionKey]
    [DynamoDbAttribute("pk")]
    public string Id { get; set; } = string.Empty;
    
    [DynamoDbAttribute("data")]
    public string Data { get; set; } = string.Empty;
}
```

**Generated code:**

```csharp
public class ReadOnlyEntityAccessor
{
    private readonly EcommerceTable _table;
    
    internal ReadOnlyEntityAccessor(EcommerceTable table)
    {
        _table = table;
    }
    
    // Only Get and Query are generated
    public GetItemRequestBuilder<ReadOnlyEntity> Get()
    {
        return new GetItemRequestBuilder<ReadOnlyEntity>(_table.Client, _table.TableName, ReadOnlyEntityMetadata.Instance);
    }
    
    public QueryRequestBuilder<ReadOnlyEntity> Query()
    {
        return new QueryRequestBuilder<ReadOnlyEntity>(_table.Client, _table.TableName, ReadOnlyEntityMetadata.Instance);
    }
    
    // No Put(), Delete(), Update(), or Scan() methods
}
```

**Usage:**

```csharp
var table = new EcommerceTable(client, "ecommerce");

// ✅ Read operations work
var item = await table.ReadOnlyEntities.Get()
    .WithKey(ReadOnlyEntityFields.Id, "id123")
    .ExecuteAsync();

var items = await table.ReadOnlyEntities.Query()
    .Where($"{ReadOnlyEntityFields.Id} = :pk", new { pk = "id123" })
    .ExecuteAsync();

// ❌ Compile error - Put() not generated
// await table.ReadOnlyEntities.Put(item).ExecuteAsync();
```

### Disabling Specific Operations

Use `Generate = false` to disable specific operations:

```csharp
[DynamoDbTable("ecommerce", IsDefault = true)]
public partial class Order { }

[DynamoDbTable("ecommerce")]
[GenerateAccessors(Operations = TableOperation.Delete, Generate = false)]
public partial class ImmutableEntity
{
    [PartitionKey]
    [DynamoDbAttribute("pk")]
    public string Id { get; set; } = string.Empty;
    
    [DynamoDbAttribute("createdAt")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    
    [DynamoDbAttribute("data")]
    public string Data { get; set; } = string.Empty;
}
```

**Generated code:**

```csharp
public class ImmutableEntityAccessor
{
    // All operations except Delete()
    public GetItemRequestBuilder<ImmutableEntity> Get() { }
    public QueryRequestBuilder<ImmutableEntity> Query() { }
    public ScanRequestBuilder<ImmutableEntity> Scan() { }
    public PutItemRequestBuilder<ImmutableEntity> Put(ImmutableEntity item) { }
    public UpdateItemRequestBuilder<ImmutableEntity> Update() { }
    // No Delete() method
}
```

### Operation Visibility Modifiers

Control visibility of specific operations:


```csharp
[DynamoDbTable("ecommerce", IsDefault = true)]
public partial class Order { }

[DynamoDbTable("ecommerce")]
[GenerateAccessors(Operations = TableOperation.All, Modifier = AccessModifier.Internal)]
[GenerateAccessors(Operations = TableOperation.Query, Modifier = AccessModifier.Public)]
public partial class OrderLine
{
    [PartitionKey]
    [DynamoDbAttribute("pk")]
    public string CustomerId { get; set; } = string.Empty;
    
    [SortKey]
    [DynamoDbAttribute("sk")]
    [Computed("ORDER#{OrderId}#LINE#{LineNumber:D3}")]
    public string OrderId { get; set; } = string.Empty;
    
    public int LineNumber { get; set; }
    
    [DynamoDbAttribute("productId")]
    public string ProductId { get; set; } = string.Empty;
}
```

**Generated code:**

```csharp
public class OrderLineAccessor
{
    private readonly EcommerceTable _table;
    
    internal OrderLineAccessor(EcommerceTable table)
    {
        _table = table;
    }
    
    // Internal operations
    internal GetItemRequestBuilder<OrderLine> Get() { }
    internal ScanRequestBuilder<OrderLine> Scan() { }
    internal PutItemRequestBuilder<OrderLine> Put(OrderLine item) { }
    internal DeleteItemRequestBuilder<OrderLine> Delete() { }
    internal UpdateItemRequestBuilder<OrderLine> Update() { }
    
    // Public operation (overrides the All modifier)
    public QueryRequestBuilder<OrderLine> Query() { }
}
```

**Use Case:** Expose only safe query operations publicly while keeping write operations internal:

```csharp
public partial class EcommerceTable
{
    // Public method using public Query operation
    public async Task<List<OrderLine>> GetOrderLinesAsync(string customerId, string orderId)
    {
        var response = await OrderLines.Query()  // Public
            .Where($"{OrderLineFields.CustomerId} = :pk AND begins_with({OrderLineFields.OrderId}, :sk)",
                   new { pk = customerId, sk = $"ORDER#{orderId}#LINE#" })
            .ExecuteAsync();
        
        return response.Items;
    }
    
    // Public method using internal Put operation
    public async Task AddOrderLineAsync(OrderLine line)
    {
        // Validation
        if (line.Quantity <= 0)
            throw new ArgumentException("Quantity must be positive");
        
        if (line.Price < 0)
            throw new ArgumentException("Price cannot be negative");
        
        // Use internal operation
        await OrderLines.Put(line).ExecuteAsync();  // Internal
    }
}
```

### Multiple GenerateAccessors Attributes

Combine multiple `[GenerateAccessors]` attributes for fine-grained control:

```csharp
[DynamoDbTable("ecommerce", IsDefault = true)]
public partial class Order { }

[DynamoDbTable("ecommerce")]
[GenerateAccessors(Operations = TableOperation.All, Modifier = AccessModifier.Internal)]
[GenerateAccessors(Operations = TableOperation.Get | TableOperation.Query, Modifier = AccessModifier.Public)]
[GenerateAccessors(Operations = TableOperation.Delete, Generate = false)]
public partial class Product
{
    [PartitionKey]
    [DynamoDbAttribute("pk")]
    public string ProductId { get; set; } = string.Empty;
    
    [DynamoDbAttribute("name")]
    public string Name { get; set; } = string.Empty;
    
    [DynamoDbAttribute("price")]
    public decimal Price { get; set; }
}
```

**Generated code:**

```csharp
public class ProductAccessor
{
    // Public read operations
    public GetItemRequestBuilder<Product> Get() { }
    public QueryRequestBuilder<Product> Query() { }
    
    // Internal write operations
    internal ScanRequestBuilder<Product> Scan() { }
    internal PutItemRequestBuilder<Product> Put(Product item) { }
    internal UpdateItemRequestBuilder<Product> Update() { }
    
    // Delete() not generated at all
}
```

## Partial Class Pattern for Custom Public Methods

### Basic Pattern

Use partial classes to add custom public methods that call internal generated methods:


**Step 1: Define entities with internal operations**

```csharp
// Entities.cs
[DynamoDbTable("ecommerce", IsDefault = true)]
public partial class Order
{
    [PartitionKey]
    [DynamoDbAttribute("pk")]
    public string CustomerId { get; set; } = string.Empty;
    
    [SortKey]
    [DynamoDbAttribute("sk")]
    [Computed("ORDER#{OrderId}")]
    public string OrderId { get; set; } = string.Empty;
    
    [DynamoDbAttribute("total")]
    public decimal Total { get; set; }
    
    [DynamoDbAttribute("status")]
    public string Status { get; set; } = "pending";
}

[DynamoDbTable("ecommerce")]
[GenerateAccessors(Operations = TableOperation.All, Modifier = AccessModifier.Internal)]
public partial class OrderLine
{
    [PartitionKey]
    [DynamoDbAttribute("pk")]
    public string CustomerId { get; set; } = string.Empty;
    
    [SortKey]
    [DynamoDbAttribute("sk")]
    [Computed("ORDER#{OrderId}#LINE#{LineNumber:D3}")]
    public string OrderId { get; set; } = string.Empty;
    
    public int LineNumber { get; set; }
    
    [DynamoDbAttribute("productId")]
    public string ProductId { get; set; } = string.Empty;
    
    [DynamoDbAttribute("quantity")]
    public int Quantity { get; set; }
    
    [DynamoDbAttribute("price")]
    public decimal Price { get; set; }
}
```

**Step 2: Create custom public methods in a partial class**

```csharp
// EcommerceTable.Custom.cs
public partial class EcommerceTable
{
    /// <summary>
    /// Gets all order lines for a specific order with validation.
    /// </summary>
    public async Task<List<OrderLine>> GetOrderLinesAsync(string customerId, string orderId)
    {
        if (string.IsNullOrWhiteSpace(customerId))
            throw new ArgumentException("Customer ID is required", nameof(customerId));
        
        if (string.IsNullOrWhiteSpace(orderId))
            throw new ArgumentException("Order ID is required", nameof(orderId));
        
        var response = await OrderLines.Query()  // Internal accessor
            .Where($"{OrderLineFields.CustomerId} = :pk AND begins_with({OrderLineFields.OrderId}, :sk)",
                   new { pk = customerId, sk = $"ORDER#{orderId}#LINE#" })
            .ExecuteAsync();
        
        return response.Items;
    }
    
    /// <summary>
    /// Adds a new order line with validation and automatic line numbering.
    /// </summary>
    public async Task<OrderLine> AddOrderLineAsync(
        string customerId, 
        string orderId, 
        string productId, 
        int quantity, 
        decimal price)
    {
        // Validation
        if (quantity <= 0)
            throw new ArgumentException("Quantity must be positive", nameof(quantity));
        
        if (price < 0)
            throw new ArgumentException("Price cannot be negative", nameof(price));
        
        // Get existing lines to determine next line number
        var existingLines = await GetOrderLinesAsync(customerId, orderId);
        var nextLineNumber = existingLines.Any() 
            ? existingLines.Max(l => l.LineNumber) + 1 
            : 1;
        
        var line = new OrderLine
        {
            CustomerId = customerId,
            OrderId = orderId,
            LineNumber = nextLineNumber,
            ProductId = productId,
            Quantity = quantity,
            Price = price
        };
        
        await OrderLines.Put(line).ExecuteAsync();  // Internal accessor
        
        return line;
    }
    
    /// <summary>
    /// Updates the quantity of an order line with validation.
    /// </summary>
    public async Task UpdateOrderLineQuantityAsync(
        string customerId, 
        string orderId, 
        int lineNumber, 
        int newQuantity)
    {
        if (newQuantity <= 0)
            throw new ArgumentException("Quantity must be positive", nameof(newQuantity));
        
        await OrderLines.Update()  // Internal accessor
            .WithKey(OrderLineFields.CustomerId, customerId)
            .WithKey(OrderLineFields.OrderId, $"ORDER#{orderId}#LINE#{lineNumber:D3}")
            .Set($"SET {OrderLineFields.Quantity} = :qty", new { qty = newQuantity })
            .ExecuteAsync();
    }
    
    /// <summary>
    /// Removes an order line.
    /// </summary>
    public async Task RemoveOrderLineAsync(string customerId, string orderId, int lineNumber)
    {
        await OrderLines.Delete()  // Internal accessor
            .WithKey(OrderLineFields.CustomerId, customerId)
            .WithKey(OrderLineFields.OrderId, $"ORDER#{orderId}#LINE#{lineNumber:D3}")
            .ExecuteAsync();
    }
}
```

**Step 3: Use the clean public API**

```csharp
var table = new EcommerceTable(client, "ecommerce");

// ✅ Clean public API with validation
var lines = await table.GetOrderLinesAsync("customer123", "order456");

var newLine = await table.AddOrderLineAsync(
    "customer123", 
    "order456", 
    "prod789", 
    quantity: 2, 
    price: 99.99m);

await table.UpdateOrderLineQuantityAsync("customer123", "order456", 1, newQuantity: 3);

await table.RemoveOrderLineAsync("customer123", "order456", 1);

// ❌ Compile error - internal accessor not accessible
// await table.OrderLines.Put(line).ExecuteAsync();
```

### Advanced Pattern: Business Logic Encapsulation

Create a rich domain API with business rules:


```csharp
// EcommerceTable.OrderManagement.cs
public partial class EcommerceTable
{
    /// <summary>
    /// Creates a complete order with lines in a single transaction.
    /// </summary>
    public async Task<Order> CreateOrderAsync(
        string customerId, 
        string orderId, 
        List<(string ProductId, int Quantity, decimal Price)> items)
    {
        if (!items.Any())
            throw new ArgumentException("Order must have at least one item", nameof(items));
        
        // Calculate total
        var total = items.Sum(i => i.Quantity * i.Price);
        
        // Create order
        var order = new Order
        {
            CustomerId = customerId,
            OrderId = orderId,
            Total = total,
            Status = "pending"
        };
        
        // Create order lines
        var lines = items.Select((item, index) => new OrderLine
        {
            CustomerId = customerId,
            OrderId = orderId,
            LineNumber = index + 1,
            ProductId = item.ProductId,
            Quantity = item.Quantity,
            Price = item.Price
        }).ToList();
        
        // Create everything atomically
        var transaction = TransactWrite()
            .AddPut(Orders, order);
        
        foreach (var line in lines)
        {
            transaction.AddPut(OrderLines, line);  // Internal accessor
        }
        
        await transaction.ExecuteAsync();
        
        return order;
    }
    
    /// <summary>
    /// Cancels an order and removes all associated lines.
    /// </summary>
    public async Task CancelOrderAsync(string customerId, string orderId)
    {
        // Get all order lines
        var lines = await GetOrderLinesAsync(customerId, orderId);
        
        // Delete everything atomically
        var transaction = TransactWrite()
            .AddUpdate(Orders, update => update
                .WithKey(OrderFields.CustomerId, customerId)
                .WithKey(OrderFields.OrderId, $"ORDER#{orderId}")
                .Set($"SET {OrderFields.Status} = :status", new { status = "cancelled" }));
        
        foreach (var line in lines)
        {
            transaction.AddDelete(OrderLines, delete => delete  // Internal accessor
                .WithKey(OrderLineFields.CustomerId, customerId)
                .WithKey(OrderLineFields.OrderId, $"ORDER#{orderId}#LINE#{line.LineNumber:D3}"));
        }
        
        await transaction.ExecuteAsync();
    }
    
    /// <summary>
    /// Gets the complete order with all lines.
    /// </summary>
    public async Task<(Order Order, List<OrderLine> Lines)> GetCompleteOrderAsync(
        string customerId, 
        string orderId)
    {
        // Query all items for the order in a single query
        var response = await Query()
            .Where($"{OrderFields.CustomerId} = :pk AND begins_with({OrderFields.OrderId}, :sk)",
                   new { pk = customerId, sk = $"ORDER#{orderId}" })
            .ExecuteAsync();
        
        var order = response.Items.FirstOrDefault();
        if (order == null)
            throw new InvalidOperationException($"Order {orderId} not found");
        
        // Get lines separately (different entity type)
        var lines = await GetOrderLinesAsync(customerId, orderId);
        
        return (order, lines);
    }
    
    /// <summary>
    /// Recalculates and updates the order total based on current lines.
    /// </summary>
    public async Task RecalculateOrderTotalAsync(string customerId, string orderId)
    {
        var lines = await GetOrderLinesAsync(customerId, orderId);
        var newTotal = lines.Sum(l => l.Quantity * l.Price);
        
        await Update()
            .WithKey(OrderFields.CustomerId, customerId)
            .WithKey(OrderFields.OrderId, $"ORDER#{orderId}")
            .Set($"SET {OrderFields.Total} = :total", new { total = newTotal })
            .ExecuteAsync();
    }
}
```

**Usage:**

```csharp
var table = new EcommerceTable(client, "ecommerce");

// Create order with items
var order = await table.CreateOrderAsync(
    "customer123",
    "order456",
    new List<(string, int, decimal)>
    {
        ("prod789", 2, 99.99m),
        ("prod101", 1, 149.99m)
    });

// Get complete order
var (orderData, lines) = await table.GetCompleteOrderAsync("customer123", "order456");
Console.WriteLine($"Order total: ${orderData.Total}");
Console.WriteLine($"Line count: {lines.Count}");

// Update line quantity and recalculate total
await table.UpdateOrderLineQuantityAsync("customer123", "order456", 1, 3);
await table.RecalculateOrderTotalAsync("customer123", "order456");

// Cancel order
await table.CancelOrderAsync("customer123", "order456");
```

## Complete Example: Library with Clean Public API

Here's a complete example showing how to build a library with a clean public API:

**Step 1: Entity definitions with internal operations**

```csharp
// Entities/Order.cs
namespace MyEcommerce.Data.Entities;

[DynamoDbTable("ecommerce", IsDefault = true)]
public partial class Order
{
    [PartitionKey]
    [DynamoDbAttribute("pk")]
    public string CustomerId { get; set; } = string.Empty;
    
    [SortKey]
    [DynamoDbAttribute("sk")]
    [Computed("ORDER#{OrderId}")]
    public string OrderId { get; set; } = string.Empty;
    
    [DynamoDbAttribute("total")]
    public decimal Total { get; set; }
    
    [DynamoDbAttribute("status")]
    public string Status { get; set; } = "pending";
    
    [DynamoDbAttribute("createdAt")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

// Entities/OrderLine.cs
[DynamoDbTable("ecommerce")]
[GenerateEntityProperty(Modifier = AccessModifier.Internal)]
[GenerateAccessors(Operations = TableOperation.All, Modifier = AccessModifier.Internal)]
public partial class OrderLine
{
    [PartitionKey]
    [DynamoDbAttribute("pk")]
    public string CustomerId { get; set; } = string.Empty;
    
    [SortKey]
    [DynamoDbAttribute("sk")]
    [Computed("ORDER#{OrderId}#LINE#{LineNumber:D3}")]
    public string OrderId { get; set; } = string.Empty;
    
    public int LineNumber { get; set; }
    
    [DynamoDbAttribute("productId")]
    public string ProductId { get; set; } = string.Empty;
    
    [DynamoDbAttribute("productName")]
    public string ProductName { get; set; } = string.Empty;
    
    [DynamoDbAttribute("quantity")]
    public int Quantity { get; set; }
    
    [DynamoDbAttribute("price")]
    public decimal Price { get; set; }
}

// Entities/Payment.cs
[DynamoDbTable("ecommerce")]
[GenerateEntityProperty(Modifier = AccessModifier.Internal)]
[GenerateAccessors(Operations = TableOperation.All, Modifier = AccessModifier.Internal)]
public partial class Payment
{
    [PartitionKey]
    [DynamoDbAttribute("pk")]
    public string CustomerId { get; set; } = string.Empty;
    
    [SortKey]
    [DynamoDbAttribute("sk")]
    [Computed("ORDER#{OrderId}#PAYMENT")]
    public string OrderId { get; set; } = string.Empty;
    
    [DynamoDbAttribute("paymentId")]
    public string PaymentId { get; set; } = string.Empty;
    
    [DynamoDbAttribute("amount")]
    public decimal Amount { get; set; }
    
    [DynamoDbAttribute("method")]
    public string Method { get; set; } = string.Empty;
    
    [DynamoDbAttribute("status")]
    public string Status { get; set; } = "pending";
}
```

**Step 2: Custom public API**

```csharp
// EcommerceTable.Public.cs
namespace MyEcommerce.Data;

public partial class EcommerceTable
{
    // Public DTOs
    public class OrderLineDto
    {
        public int LineNumber { get; set; }
        public string ProductId { get; set; } = string.Empty;
        public string ProductName { get; set; } = string.Empty;
        public int Quantity { get; set; }
        public decimal Price { get; set; }
        public decimal LineTotal => Quantity * Price;
    }
    
    public class CompleteOrderDto
    {
        public string OrderId { get; set; } = string.Empty;
        public string CustomerId { get; set; } = string.Empty;
        public decimal Total { get; set; }
        public string Status { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public List<OrderLineDto> Lines { get; set; } = new();
        public PaymentInfo? Payment { get; set; }
    }
    
    public class PaymentInfo
    {
        public string PaymentId { get; set; } = string.Empty;
        public decimal Amount { get; set; }
        public string Method { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
    }
    
    // Public methods
    public async Task<string> CreateOrderAsync(
        string customerId,
        List<OrderLineDto> items)
    {
        var orderId = Guid.NewGuid().ToString();
        var total = items.Sum(i => i.LineTotal);
        
        var order = new Order
        {
            CustomerId = customerId,
            OrderId = orderId,
            Total = total,
            Status = "pending",
            CreatedAt = DateTime.UtcNow
        };
        
        var transaction = TransactWrite().AddPut(Orders, order);
        
        foreach (var (item, index) in items.Select((i, idx) => (i, idx)))
        {
            var line = new OrderLine
            {
                CustomerId = customerId,
                OrderId = orderId,
                LineNumber = index + 1,
                ProductId = item.ProductId,
                ProductName = item.ProductName,
                Quantity = item.Quantity,
                Price = item.Price
            };
            transaction.AddPut(OrderLines, line);
        }
        
        await transaction.ExecuteAsync();
        return orderId;
    }
    
    public async Task<CompleteOrderDto?> GetOrderAsync(string customerId, string orderId)
    {
        var orderResponse = await Get()
            .WithKey(OrderFields.CustomerId, customerId)
            .WithKey(OrderFields.OrderId, $"ORDER#{orderId}")
            .ExecuteAsync();
        
        if (orderResponse.Item == null)
            return null;
        
        var linesResponse = await OrderLines.Query()
            .Where($"{OrderLineFields.CustomerId} = :pk AND begins_with({OrderLineFields.OrderId}, :sk)",
                   new { pk = customerId, sk = $"ORDER#{orderId}#LINE#" })
            .ExecuteAsync();
        
        var paymentResponse = await Payments.Get()
            .WithKey(PaymentFields.CustomerId, customerId)
            .WithKey(PaymentFields.OrderId, $"ORDER#{orderId}#PAYMENT")
            .ExecuteAsync();
        
        return new CompleteOrderDto
        {
            OrderId = orderResponse.Item.OrderId,
            CustomerId = orderResponse.Item.CustomerId,
            Total = orderResponse.Item.Total,
            Status = orderResponse.Item.Status,
            CreatedAt = orderResponse.Item.CreatedAt,
            Lines = linesResponse.Items.Select(l => new OrderLineDto
            {
                LineNumber = l.LineNumber,
                ProductId = l.ProductId,
                ProductName = l.ProductName,
                Quantity = l.Quantity,
                Price = l.Price
            }).ToList(),
            Payment = paymentResponse.Item != null ? new PaymentInfo
            {
                PaymentId = paymentResponse.Item.PaymentId,
                Amount = paymentResponse.Item.Amount,
                Method = paymentResponse.Item.Method,
                Status = paymentResponse.Item.Status
            } : null
        };
    }
    
    public async Task ProcessPaymentAsync(
        string customerId,
        string orderId,
        string paymentMethod,
        decimal amount)
    {
        var paymentId = Guid.NewGuid().ToString();
        
        var payment = new Payment
        {
            CustomerId = customerId,
            OrderId = orderId,
            PaymentId = paymentId,
            Amount = amount,
            Method = paymentMethod,
            Status = "completed"
        };
        
        await TransactWrite()
            .AddPut(Payments, payment)
            .AddUpdate(Orders, update => update
                .WithKey(OrderFields.CustomerId, customerId)
                .WithKey(OrderFields.OrderId, $"ORDER#{orderId}")
                .Set($"SET {OrderFields.Status} = :status", new { status = "paid" }))
            .ExecuteAsync();
    }
}
```

**Step 3: Clean external usage**

```csharp
// Consumer code
var client = new AmazonDynamoDBClient();
var ecommerce = new EcommerceTable(client, "ecommerce");

// Create order
var orderId = await ecommerce.CreateOrderAsync(
    "customer123",
    new List<EcommerceTable.OrderLineDto>
    {
        new() { ProductId = "prod1", ProductName = "Widget", Quantity = 2, Price = 99.99m },
        new() { ProductId = "prod2", ProductName = "Gadget", Quantity = 1, Price = 149.99m }
    });

// Get order
var order = await ecommerce.GetOrderAsync("customer123", orderId);
if (order != null)
{
    Console.WriteLine($"Order {order.OrderId}: ${order.Total}");
    Console.WriteLine($"Status: {order.Status}");
    Console.WriteLine($"Lines: {order.Lines.Count}");
}

// Process payment
await ecommerce.ProcessPaymentAsync("customer123", orderId, "credit_card", order!.Total);

// Internal accessors are not accessible
// ❌ Compile error
// await ecommerce.OrderLines.Put(line).ExecuteAsync();
// await ecommerce.Payments.Query()...
```

## Best Practices

### 1. Use Internal Accessors for Implementation Details

Hide low-level DynamoDB operations:

```csharp
// ✅ Good - Internal accessor, public methods
[DynamoDbTable("app")]
[GenerateAccessors(Operations = TableOperation.All, Modifier = AccessModifier.Internal)]
public partial class InternalEntity { }

public partial class AppTable
{
    public async Task<Result> DoSomethingAsync()
    {
        // Use internal accessor
        await InternalEntities.Put(item).ExecuteAsync();
    }
}

// ❌ Bad - Public accessor exposes implementation
[DynamoDbTable("app")]
public partial class InternalEntity { }
// Consumers can call table.InternalEntities.Put() directly
```

### 2. Validate in Public Methods

Add validation and business logic in custom methods:

```csharp
public partial class EcommerceTable
{
    public async Task AddOrderLineAsync(OrderLine line)
    {
        // ✅ Good - Validation before database operation
        if (line.Quantity <= 0)
            throw new ArgumentException("Quantity must be positive");
        
        if (line.Price < 0)
            throw new ArgumentException("Price cannot be negative");
        
        await OrderLines.Put(line).ExecuteAsync();
    }
}
```

### 3. Use DTOs for Public APIs

Don't expose entity classes directly:

```csharp
// ✅ Good - Public DTO
public class OrderDto
{
    public string OrderId { get; set; }
    public decimal Total { get; set; }
    public List<OrderLineDto> Lines { get; set; }
}

public async Task<OrderDto> GetOrderAsync(string id)
{
    var entity = await Orders.Get()...
    return MapToDto(entity);
}

// ❌ Bad - Exposing entity class
public async Task<Order> GetOrderAsync(string id)
{
    return await Orders.Get()...
}
```

### 4. Group Related Operations

Organize custom methods by feature:

```csharp
// EcommerceTable.OrderManagement.cs
public partial class EcommerceTable
{
    public async Task<string> CreateOrderAsync(...) { }
    public async Task CancelOrderAsync(...) { }
    public async Task GetOrderAsync(...) { }
}

// EcommerceTable.PaymentManagement.cs
public partial class EcommerceTable
{
    public async Task ProcessPaymentAsync(...) { }
    public async Task RefundPaymentAsync(...) { }
}
```

### 5. Document Public Methods

Add XML documentation to public methods:

```csharp
/// <summary>
/// Creates a new order with the specified items.
/// </summary>
/// <param name="customerId">The customer ID.</param>
/// <param name="items">The order items.</param>
/// <returns>The created order ID.</returns>
/// <exception cref="ArgumentException">Thrown when items list is empty.</exception>
public async Task<string> CreateOrderAsync(string customerId, List<OrderLineDto> items)
{
    // Implementation
}
```

## Summary

Table generation customization provides powerful control over generated code:

1. **Entity Accessor Names** - Use `[GenerateEntityProperty(Name = "...")]` for custom names
2. **Disable Accessors** - Use `Generate = false` to hide entity accessors
3. **Accessor Visibility** - Use `Modifier` to control accessor visibility (Public, Internal, Protected, Private)
4. **Selective Operations** - Use `[GenerateAccessors(Operations = ...)]` to generate only specific operations
5. **Operation Visibility** - Use `Modifier` on operations for fine-grained control
6. **Partial Classes** - Extend generated code with custom public methods

**Key Pattern:**
```csharp
// Internal generated operations
[GenerateAccessors(Operations = TableOperation.All, Modifier = AccessModifier.Internal)]
public partial class Entity { }

// Custom public API
public partial class MyTable
{
    public async Task<Result> PublicMethodAsync()
    {
        // Use internal accessor
        await Entities.Put(item).ExecuteAsync();
    }
}
```

This approach provides:
- Clean public APIs with validation and business logic
- Hidden implementation details
- Type-safe operations
- Maintainable code structure

## Next Steps

- **[Multi-Entity Tables](MultiEntityTables.md)** - Learn about multi-entity table patterns
- **[Single-Entity Tables](../getting-started/SingleEntityTables.md)** - Understand single-entity basics
- **[Entity Definition](../core-features/EntityDefinition.md)** - Complete entity configuration
- **[Attribute Reference](../reference/AttributeReference.md)** - All available attributes

---

[Previous: Multi-Entity Tables](MultiEntityTables.md) | [Next: Composite Entities](CompositeEntities.md)

**See Also:**
- [Basic Operations](../core-features/BasicOperations.md)
- [Transactions](../core-features/Transactions.md)
- [Source Generator Guide](../SourceGeneratorGuide.md)
