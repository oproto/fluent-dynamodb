---
title: "Multi-Entity Tables"
category: "advanced-topics"
order: 2
keywords: ["multi-entity", "single-table design", "entity accessors", "default entity", "table consolidation"]
related: ["../getting-started/SingleEntityTables.md", "CompositeEntities.md", "../core-features/EntityDefinition.md"]
---

[Documentation](../README.md) > [Advanced Topics](README.md) > Multi-Entity Tables

# Multi-Entity Tables

[Previous: Composite Entities](CompositeEntities.md) | [Next: Global Secondary Indexes](GlobalSecondaryIndexes.md)

---

This guide covers single-table design patterns where multiple entity types share the same DynamoDB table. This is an advanced pattern that provides significant benefits for complex applications.

## Overview

In a multi-entity table design, multiple entity types coexist in a single DynamoDB table, differentiated by their partition and sort key patterns. This is a powerful pattern for:

- **Single-table design**: Minimize table count and optimize for access patterns
- **Related entities**: Store related data together for efficient queries
- **Cost optimization**: Reduce provisioned capacity costs
- **Transaction support**: Coordinate operations across entity types in a single transaction

**Key Benefits:**
- Efficient queries across related entities
- Reduced table management overhead
- Better support for complex access patterns
- Atomic transactions across entity types

**When to Use:**
- Multiple entities share access patterns
- Entities have hierarchical relationships
- You need to query related entities together
- Cost optimization is important


## Basic Multi-Entity Table

The simplest multi-entity table with a default entity:

```csharp
using Oproto.FluentDynamoDb.Attributes;

// Default entity - used for table-level operations
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

// Secondary entity - shares the same table
[DynamoDbTable("ecommerce")]
public partial class OrderLine
{
    [PartitionKey]
    [DynamoDbAttribute("pk")]
    public string CustomerId { get; set; } = string.Empty;
    
    [SortKey]
    [DynamoDbAttribute("sk")]
    [Computed("ORDER#{OrderId}#LINE#{LineNumber}")]
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


**What Gets Generated:**

The source generator creates a single table class named `EcommerceTable` with entity-specific accessors:

```csharp
// Generated: EcommerceTable.g.cs
public partial class EcommerceTable : DynamoDbTableBase
{
    public EcommerceTable(IAmazonDynamoDB client, string tableName) 
        : base(client, tableName)
    {
        Orders = new OrderAccessor(this);
        OrderLines = new OrderLineAccessor(this);
    }
    
    // Table-level operations use the default entity (Order)
    public GetItemRequestBuilder<Order> Get()
    {
        return Orders.Get();
    }
    
    public QueryRequestBuilder<Order> Query()
    {
        return Orders.Query();
    }
    
    public PutItemRequestBuilder<Order> Put(Order item)
    {
        return Orders.Put(item);
    }
    
    // ... other table-level operations for Order
    
    // Entity-specific accessors
    public OrderAccessor Orders { get; }
    public OrderLineAccessor OrderLines { get; }
    
    // Transaction and batch operations (table level only)
    public TransactWriteItemsRequestBuilder TransactWrite()
    {
        return new TransactWriteItemsRequestBuilder(Client);
    }
    
    public BatchWriteItemBuilder BatchWrite()
    {
        return new BatchWriteItemBuilder(Client);
    }
    
    // Nested accessor classes
    public class OrderAccessor
    {
        private readonly EcommerceTable _table;
        
        internal OrderAccessor(EcommerceTable table)
        {
            _table = table;
        }
        
        public GetItemRequestBuilder<Order> Get()
        {
            return new GetItemRequestBuilder<Order>(_table.Client, _table.TableName, OrderMetadata.Instance);
        }
        
        public QueryRequestBuilder<Order> Query()
        {
            return new QueryRequestBuilder<Order>(_table.Client, _table.TableName, OrderMetadata.Instance);
        }
        
        public PutItemRequestBuilder<Order> Put(Order item)
        {
            return new PutItemRequestBuilder<Order>(_table.Client, _table.TableName, OrderMetadata.Instance, item);
        }
        
        // ... other operations
    }
    
    public class OrderLineAccessor
    {
        private readonly EcommerceTable _table;
        
        internal OrderLineAccessor(EcommerceTable table)
        {
            _table = table;
        }
        
        public GetItemRequestBuilder<OrderLine> Get()
        {
            return new GetItemRequestBuilder<OrderLine>(_table.Client, _table.TableName, OrderLineMetadata.Instance);
        }
        
        public QueryRequestBuilder<OrderLine> Query()
        {
            return new QueryRequestBuilder<OrderLine>(_table.Client, _table.TableName, OrderLineMetadata.Instance);
        }
        
        // ... other operations
    }
}
```


## Using Multi-Entity Tables

### Creating the Table Instance

```csharp
using Amazon.DynamoDBv2;
using Oproto.FluentDynamoDb.Storage;

var client = new AmazonDynamoDBClient();
var ecommerceTable = new EcommerceTable(client, "ecommerce");
```

### Entity Accessor Usage

Access entity-specific operations through the accessor properties:

```csharp
// Create an order using the Orders accessor
var order = new Order
{
    CustomerId = "customer123",
    OrderId = "order456",
    Total = 199.99m,
    Status = "pending"
};

await ecommerceTable.Orders.Put(order)
    .PutAsync();

// Create order lines using the OrderLines accessor
var line1 = new OrderLine
{
    CustomerId = "customer123",
    OrderId = "order456",
    LineNumber = 1,
    ProductId = "prod789",
    Quantity = 2,
    Price = 99.99m
};

await ecommerceTable.OrderLines.Put(line1)
    .PutAsync();

// Get a specific order
var orderResponse = await ecommerceTable.Orders.Get()
    .WithKey(OrderFields.CustomerId, "customer123")
    .WithKey(OrderFields.OrderId, "ORDER#order456")
    .GetItemAsync();

// Query all order lines for an order
var linesResponse = await ecommerceTable.OrderLines.Query()
    .Where($"{OrderLineFields.CustomerId} = :pk AND begins_with({OrderLineFields.OrderId}, :sk)",
           new { pk = "customer123", sk = "ORDER#order456#LINE#" })
    .ToListAsync();

foreach (var line in linesResponse.Items)
{
    Console.WriteLine($"Line {line.LineNumber}: {line.ProductId} x {line.Quantity}");
}
```

**Notice:** Operations are called on entity accessors (`ecommerceTable.Orders.Get()`, `ecommerceTable.OrderLines.Query()`), not directly on the table.


### Table-Level Operations Using Default Entity

Since `Order` is marked as the default entity (`IsDefault = true`), table-level operations are available and use the `Order` type:

```csharp
// These are equivalent:
var order1 = await ecommerceTable.Get()
    .WithKey(OrderFields.CustomerId, "customer123")
    .WithKey(OrderFields.OrderId, "ORDER#order456")
    .GetItemAsync();

var order2 = await ecommerceTable.Orders.Get()
    .WithKey(OrderFields.CustomerId, "customer123")
    .WithKey(OrderFields.OrderId, "ORDER#order456")
    .GetItemAsync();

// Both return GetItemResponse<Order>

// Query all orders for a customer (table-level)
var customerOrders = await ecommerceTable.Query()
    .Where($"{OrderFields.CustomerId} = :pk AND begins_with({OrderFields.OrderId}, :sk)",
           new { pk = "customer123", sk = "ORDER#" })
    .ToListAsync();

// This is equivalent to:
var customerOrders2 = await ecommerceTable.Orders.Query()
    .Where($"{OrderFields.CustomerId} = :pk AND begins_with({OrderFields.OrderId}, :sk)",
           new { pk = "customer123", sk = "ORDER#" })
    .ToListAsync();
```

**Convenience:** Table-level operations provide a shorthand for the default entity, making common operations more concise.


## Default Entity Selection

### IsDefault = true

When multiple entities share a table, you must mark exactly one as the default:

```csharp
// ✅ Correct - Order is the default entity
[DynamoDbTable("ecommerce", IsDefault = true)]
public partial class Order
{
    // ...
}

[DynamoDbTable("ecommerce")]
public partial class OrderLine
{
    // ...
}

[DynamoDbTable("ecommerce")]
public partial class Payment
{
    // ...
}
```

### Compile-Time Validation

The source generator enforces default entity rules:

```csharp
// ❌ Error FDDB001: No default specified
[DynamoDbTable("ecommerce")]
public partial class Order { }

[DynamoDbTable("ecommerce")]
public partial class OrderLine { }

// Compiler error: Table 'ecommerce' has multiple entities but no default specified. 
// Mark one entity with IsDefault = true
```

```csharp
// ❌ Error FDDB002: Multiple defaults
[DynamoDbTable("ecommerce", IsDefault = true)]
public partial class Order { }

[DynamoDbTable("ecommerce", IsDefault = true)]
public partial class OrderLine { }

// Compiler error: Table 'ecommerce' has multiple entities marked as default. 
// Only one entity can be marked with IsDefault = true
```

### Choosing the Default Entity

Select the entity that:
- Is most frequently accessed
- Represents the primary concept of the table
- Is used in the majority of operations

In an e-commerce table, `Order` is typically the default because most operations revolve around orders.


## Transaction Operations

Transaction and batch operations are always available at the table level and can coordinate operations across multiple entity types:

```csharp
var ecommerceTable = new EcommerceTable(client, "ecommerce");

// Create order and order lines in a single transaction
var order = new Order
{
    CustomerId = "customer123",
    OrderId = "order456",
    Total = 199.98m,
    Status = "pending"
};

var line1 = new OrderLine
{
    CustomerId = "customer123",
    OrderId = "order456",
    LineNumber = 1,
    ProductId = "prod789",
    Quantity = 2,
    Price = 99.99m
};

var line2 = new OrderLine
{
    CustomerId = "customer123",
    OrderId = "order456",
    LineNumber = 2,
    ProductId = "prod101",
    Quantity = 1,
    Price = 99.99m
};

// Transaction across multiple entity types
await ecommerceTable.TransactWrite()
    .AddPut(ecommerceTable.Orders, order)
    .AddPut(ecommerceTable.OrderLines, line1)
    .AddPut(ecommerceTable.OrderLines, line2)
    .CommitAsync();

// All items are created atomically or none are created
```

### Batch Operations

```csharp
// Batch write multiple entity types
await ecommerceTable.BatchWrite()
    .AddPut(order1)
    .AddPut(order2)
    .AddPut(line1)
    .AddPut(line2)
    .AddPut(line3)
    .ExecuteAsync();

// Batch get items of different types
var batchResponse = await ecommerceTable.BatchGet()
    .AddKey(OrderKeys.Pk("customer123"), OrderKeys.Sk("ORDER#order456"))
    .AddKey(OrderLineKeys.Pk("customer123"), OrderLineKeys.Sk("ORDER#order456#LINE#1"))
    .AddKey(OrderLineKeys.Pk("customer123"), OrderLineKeys.Sk("ORDER#order456#LINE#2"))
    .ExecuteAsync();
```

**Important:** Transaction and batch operations are only available at the table level, not on entity accessors. This ensures you can coordinate operations across entity types.


## Complete Example: E-Commerce Application

Here's a complete example showing a multi-entity table for an e-commerce application:

```csharp
// Orders - Default entity
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
    
    [GlobalSecondaryIndex("StatusIndex", IsPartitionKey = true)]
    [DynamoDbAttribute("status")]
    public string StatusIndexPk { get; set; } = string.Empty;
    
    [GlobalSecondaryIndex("StatusIndex", IsSortKey = true)]
    [DynamoDbAttribute("createdAt")]
    public DateTime StatusIndexSk { get; set; }
}

// Order Lines
[DynamoDbTable("ecommerce")]
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

// Payments
[DynamoDbTable("ecommerce")]
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
    
    [DynamoDbAttribute("processedAt")]
    public DateTime? ProcessedAt { get; set; }
}

// Shipments
[DynamoDbTable("ecommerce")]
public partial class Shipment
{
    [PartitionKey]
    [DynamoDbAttribute("pk")]
    public string CustomerId { get; set; } = string.Empty;
    
    [SortKey]
    [DynamoDbAttribute("sk")]
    [Computed("ORDER#{OrderId}#SHIPMENT")]
    public string OrderId { get; set; } = string.Empty;
    
    [DynamoDbAttribute("trackingNumber")]
    public string TrackingNumber { get; set; } = string.Empty;
    
    [DynamoDbAttribute("carrier")]
    public string Carrier { get; set; } = string.Empty;
    
    [DynamoDbAttribute("status")]
    public string Status { get; set; } = "preparing";
    
    [DynamoDbAttribute("shippedAt")]
    public DateTime? ShippedAt { get; set; }
    
    [DynamoDbAttribute("deliveredAt")]
    public DateTime? DeliveredAt { get; set; }
}
```


### Usage Example

```csharp
var client = new AmazonDynamoDBClient();
var ecommerceTable = new EcommerceTable(client, "ecommerce");

// Create a complete order with all related entities in a transaction
var order = new Order
{
    CustomerId = "customer123",
    OrderId = "order456",
    Total = 299.97m,
    Status = "pending",
    StatusIndexPk = "pending"
};

var line1 = new OrderLine
{
    CustomerId = "customer123",
    OrderId = "order456",
    LineNumber = 1,
    ProductId = "prod789",
    ProductName = "Widget Pro",
    Quantity = 2,
    Price = 99.99m
};

var line2 = new OrderLine
{
    CustomerId = "customer123",
    OrderId = "order456",
    LineNumber = 2,
    ProductId = "prod101",
    ProductName = "Gadget Plus",
    Quantity = 1,
    Price = 99.99m
};

var payment = new Payment
{
    CustomerId = "customer123",
    OrderId = "order456",
    PaymentId = "pay789",
    Amount = 299.97m,
    Method = "credit_card",
    Status = "pending"
};

// Create everything atomically
await ecommerceTable.TransactWrite()
    .AddPut(ecommerceTable.Orders, order)
    .AddPut(ecommerceTable.OrderLines, line1)
    .AddPut(ecommerceTable.OrderLines, line2)
    .AddPut(ecommerceTable.Payments, payment)
    .CommitAsync();

// Query all items for an order (efficient single query)
var allOrderItems = await ecommerceTable.Query()
    .Where($"{OrderFields.CustomerId} = :pk AND begins_with({OrderFields.OrderId}, :sk)",
           new { pk = "customer123", sk = "ORDER#order456" })
    .ToListAsync();

// Results include Order, OrderLines, Payment, and Shipment (if exists)
// All retrieved in a single query due to shared partition key

// Query just order lines
var orderLines = await ecommerceTable.OrderLines.Query()
    .Where($"{OrderLineFields.CustomerId} = :pk AND begins_with({OrderLineFields.OrderId}, :sk)",
           new { pk = "customer123", sk = "ORDER#order456#LINE#" })
    .ToListAsync();

// Get specific payment
var paymentResponse = await ecommerceTable.Payments.Get()
    .WithKey(PaymentFields.CustomerId, "customer123")
    .WithKey(PaymentFields.OrderId, "ORDER#order456#PAYMENT")
    .GetItemAsync();

// Query orders by status using GSI (table-level operation)
var pendingOrders = await ecommerceTable.Query<Order>()
    .UsingIndex("StatusIndex")
    .Where($"{Order.Fields.StatusIndexPk} = {{0}}", "pending")
    .ToListAsync();

// Update order status and create shipment atomically
await ecommerceTable.TransactWrite()
    .AddUpdate(ecommerceTable.Orders, 
        update => update
            .WithKey(OrderFields.CustomerId, "customer123")
            .WithKey(OrderFields.OrderId, "ORDER#order456")
            .Set($"SET {OrderFields.Status} = :status", new { status = "shipped" }))
    .AddPut(ecommerceTable.Shipments, new Shipment
    {
        CustomerId = "customer123",
        OrderId = "order456",
        TrackingNumber = "TRACK123",
        Carrier = "UPS",
        Status = "in_transit",
        ShippedAt = DateTime.UtcNow
    })
    .CommitAsync();
```


## Access Pattern Design

Multi-entity tables excel when entities share access patterns. Here's how to design effective access patterns:

### Pattern 1: Hierarchical Data

Store parent and child entities together:

```
PK: customer123
SK: ORDER#order456              → Order
SK: ORDER#order456#LINE#001     → OrderLine
SK: ORDER#order456#LINE#002     → OrderLine
SK: ORDER#order456#PAYMENT      → Payment
SK: ORDER#order456#SHIPMENT     → Shipment
```

**Query:** Get order and all related items in one query:
```csharp
var allItems = await table.Query()
    .Where($"{OrderFields.CustomerId} = :pk AND begins_with({OrderFields.OrderId}, :sk)",
           new { pk = "customer123", sk = "ORDER#order456" })
    .ToListAsync();
```

### Pattern 2: Time-Series Data

Store events with timestamps:

```
PK: customer123
SK: ORDER#order456                      → Order
SK: ORDER#order456#EVENT#2024-01-15     → OrderEvent
SK: ORDER#order456#EVENT#2024-01-16     → OrderEvent
SK: ORDER#order456#EVENT#2024-01-17     → OrderEvent
```

**Query:** Get order and recent events:
```csharp
var recentActivity = await table.Query()
    .Where($"{OrderFields.CustomerId} = :pk AND {OrderFields.OrderId} BETWEEN :start AND :end",
           new { 
               pk = "customer123", 
               start = "ORDER#order456#EVENT#2024-01-15",
               end = "ORDER#order456#EVENT#2024-01-20"
           })
    .ToListAsync();
```

### Pattern 3: Status-Based Queries

Use GSI for status queries across entity types:

```csharp
// Query all pending orders
var pendingOrders = await table.Query<Order>()
    .UsingIndex("StatusIndex")
    .Where($"{Order.Fields.StatusIndexPk} = {{0}}", "pending")
    .ToListAsync();

// Query pending payments
var pendingPayments = await table.Payments.Query()
    .UsingIndex("StatusIndex")
    .Where($"{Payment.Fields.StatusIndexPk} = {{0}}", "pending")
    .ToListAsync();
```


## Customizing Entity Accessors

You can customize how entity accessors are generated using attributes. For complete details and advanced patterns, see [Table Generation Customization](TableGenerationCustomization.md).

### Custom Accessor Names

Use `[GenerateEntityProperty]` to customize the accessor property name:

```csharp
[DynamoDbTable("ecommerce", IsDefault = true)]
public partial class Order
{
    // ...
}

[DynamoDbTable("ecommerce")]
[GenerateEntityProperty(Name = "Lines")]  // Custom name
public partial class OrderLine
{
    // ...
}
```

**Usage:**
```csharp
// Use custom name
var lines = await ecommerceTable.Lines.Query()
    .Where($"{OrderLineFields.CustomerId} = :pk", new { pk = "customer123" })
    .ToListAsync();
```

### Hiding Entity Accessors

Use `Generate = false` to hide entity accessors:

```csharp
[DynamoDbTable("ecommerce")]
[GenerateEntityProperty(Generate = false)]  // No accessor generated
public partial class InternalAuditLog
{
    // ...
}
```

This is useful for internal entities that you don't want exposed in the public API.

### Accessor Visibility

Control accessor visibility with the `Modifier` property:

```csharp
[DynamoDbTable("ecommerce")]
[GenerateEntityProperty(Modifier = AccessModifier.Internal)]
public partial class OrderLine
{
    // ...
}
```

**Generated code:**
```csharp
public partial class EcommerceTable
{
    // Internal accessor - not visible outside assembly
    internal OrderLineAccessor OrderLines { get; }
}
```

This allows you to create custom public methods that call internal generated methods.

**For more customization options, see [Table Generation Customization](TableGenerationCustomization.md).**


## Customizing Operation Methods

Control which operations are generated and their visibility using `[GenerateAccessors]`. For complete details and advanced patterns, see [Table Generation Customization](TableGenerationCustomization.md).

### Selective Operation Generation

Generate only specific operations:

```csharp
[DynamoDbTable("ecommerce")]
[GenerateAccessors(Operations = TableOperation.Get | TableOperation.Query)]
public partial class ReadOnlyEntity
{
    // Only Get() and Query() methods are generated
    // No Put(), Delete(), or Update() methods
}
```

### Disabling Operations

Disable specific operations:

```csharp
[DynamoDbTable("ecommerce")]
[GenerateAccessors(Operations = TableOperation.Delete, Generate = false)]
public partial class ImmutableEntity
{
    // All operations except Delete() are generated
}
```

### Operation Visibility

Control visibility of specific operations:

```csharp
[DynamoDbTable("ecommerce")]
[GenerateAccessors(Operations = TableOperation.All, Modifier = AccessModifier.Internal)]
[GenerateAccessors(Operations = TableOperation.Query, Modifier = AccessModifier.Public)]
public partial class OrderLine
{
    // All operations are internal except Query() which is public
}
```

**Generated code:**
```csharp
public class OrderLineAccessor
{
    internal GetItemRequestBuilder<OrderLine> Get() { }
    public QueryRequestBuilder<OrderLine> Query() { }  // Public
    internal ScanRequestBuilder<OrderLine> Scan() { }
    internal PutItemRequestBuilder<OrderLine> Put(OrderLine item) { }
    internal DeleteItemRequestBuilder<OrderLine> Delete() { }
    internal UpdateItemRequestBuilder<OrderLine> Update() { }
}
```

### Creating Custom Public APIs

Use internal generated methods to create custom public APIs:

```csharp
// Generated accessor with internal operations
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
}

// Custom partial class with public methods
public partial class EcommerceTable
{
    // Custom public method that calls internal generated method
    public async Task<List<OrderLine>> GetOrderLinesAsync(string customerId, string orderId)
    {
        var response = await OrderLines.Query()  // Internal accessor
            .Where($"{OrderLineFields.CustomerId} = :pk AND begins_with({OrderLineFields.OrderId}, :sk)",
                   new { pk = customerId, sk = $"ORDER#{orderId}#LINE#" })
            .ToListAsync();
        
        return response.Items;
    }
    
    // Custom public method with validation
    public async Task AddOrderLineAsync(OrderLine line)
    {
        if (line.Quantity <= 0)
            throw new ArgumentException("Quantity must be positive", nameof(line));
        
        if (string.IsNullOrEmpty(line.ProductId))
            throw new ArgumentException("ProductId is required", nameof(line));
        
        await OrderLines.Put(line)  // Internal accessor
            .PutAsync();
    }
}
```

**Usage:**
```csharp
var table = new EcommerceTable(client, "ecommerce");

// Use custom public API
var lines = await table.GetOrderLinesAsync("customer123", "order456");

// Internal accessor not accessible
// var response = await table.OrderLines.Query(); // Compile error
```

**For complete examples including business logic encapsulation and library design patterns, see [Table Generation Customization](TableGenerationCustomization.md).**


## Best Practices

### 1. Choose the Right Default Entity

Select the entity that:
- Is most frequently accessed
- Represents the primary concept
- Is used in most operations

```csharp
// ✅ Good - Order is the primary entity
[DynamoDbTable("ecommerce", IsDefault = true)]
public partial class Order { }

[DynamoDbTable("ecommerce")]
public partial class OrderLine { }
```

### 2. Use Consistent Key Patterns

Establish clear patterns for sort keys:

```csharp
// ✅ Good - Clear hierarchical pattern
SK: ORDER#order456              → Order
SK: ORDER#order456#LINE#001     → OrderLine
SK: ORDER#order456#PAYMENT      → Payment
SK: ORDER#order456#SHIPMENT     → Shipment

// ❌ Bad - Inconsistent patterns
SK: order456                    → Order
SK: line-001-order456           → OrderLine
SK: payment_order456            → Payment
```

### 3. Design for Query Efficiency

Store related entities together for efficient queries:

```csharp
// ✅ Good - Single query gets order and all lines
var allItems = await table.Query()
    .Where($"pk = :pk AND begins_with(sk, :sk)",
           new { pk = "customer123", sk = "ORDER#order456" })
    .ToListAsync();

// ❌ Bad - Multiple queries needed
var order = await table.Orders.Get()...
var line1 = await table.OrderLines.Get()...
var line2 = await table.OrderLines.Get()...
```

### 4. Use Transactions for Consistency

Ensure related entities are created/updated atomically:

```csharp
// ✅ Good - Atomic creation
await table.TransactWrite()
    .AddPut(table.Orders, order)
    .AddPut(table.OrderLines, line1)
    .AddPut(table.OrderLines, line2)
    .CommitAsync();

// ❌ Bad - Non-atomic, can leave partial data
await table.Orders.Put(order).PutAsync();
await table.OrderLines.Put(line1).PutAsync();
await table.OrderLines.Put(line2).PutAsync();
```

### 5. Leverage GSIs for Alternative Access Patterns

Use GSIs for queries that don't fit the primary key pattern:

```csharp
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
    
    // GSI for status-based queries
    [GlobalSecondaryIndex("StatusIndex", IsPartitionKey = true)]
    [DynamoDbAttribute("status")]
    public string Status { get; set; } = string.Empty;
    
    [GlobalSecondaryIndex("StatusIndex", IsSortKey = true)]
    [DynamoDbAttribute("createdAt")]
    public DateTime CreatedAt { get; set; }
}
```

### 6. Use Internal Accessors for Encapsulation

Hide implementation details with internal accessors:

```csharp
[DynamoDbTable("ecommerce")]
[GenerateAccessors(Operations = TableOperation.All, Modifier = AccessModifier.Internal)]
public partial class OrderLine { }

// Create custom public API
public partial class EcommerceTable
{
    public async Task<List<OrderLine>> GetOrderLinesAsync(string customerId, string orderId)
    {
        // Implementation uses internal accessor
        var response = await OrderLines.Query()...
        return response.Items;
    }
}
```


## Migration from Single-Entity Tables

If you're migrating from single-entity tables to multi-entity tables:

### Step 1: Update Entity Definitions

Add `IsDefault = true` to the primary entity:

```csharp
// Before
[DynamoDbTable("orders")]
public partial class Order { }

[DynamoDbTable("order_lines")]
public partial class OrderLine { }

// After
[DynamoDbTable("ecommerce", IsDefault = true)]
public partial class Order { }

[DynamoDbTable("ecommerce")]
public partial class OrderLine { }
```

### Step 2: Update Key Patterns

Ensure sort keys differentiate entity types:

```csharp
[DynamoDbTable("ecommerce", IsDefault = true)]
public partial class Order
{
    [SortKey]
    [DynamoDbAttribute("sk")]
    [Computed("ORDER#{OrderId}")]  // Add prefix
    public string OrderId { get; set; } = string.Empty;
}

[DynamoDbTable("ecommerce")]
public partial class OrderLine
{
    [SortKey]
    [DynamoDbAttribute("sk")]
    [Computed("ORDER#{OrderId}#LINE#{LineNumber:D3}")]  // Hierarchical pattern
    public string OrderId { get; set; } = string.Empty;
    public int LineNumber { get; set; }
}
```

### Step 3: Update Code to Use Entity Accessors

```csharp
// Before (separate tables)
var ordersTable = new OrdersTable(client, "orders");
var orderLinesTable = new OrderLinesTable(client, "order-lines");

await ordersTable.Put(order).PutAsync();
await orderLinesTable.Put(line).PutAsync();

// After (multi-entity table)
var ecommerceTable = new EcommerceTable(client, "ecommerce");

await ecommerceTable.Orders.Put(order).PutAsync();
await ecommerceTable.OrderLines.Put(line).PutAsync();

// Or use table-level operations for default entity
await ecommerceTable.Put(order).PutAsync();
await ecommerceTable.OrderLines.Put(line).PutAsync();
```

### Step 4: Migrate Data

Use DynamoDB's data migration tools or write a migration script:

```csharp
// Migration script example
var sourceOrdersTable = new OrdersTable(client, "orders");
var sourceOrderLinesTable = new OrderLinesTable(client, "order-lines");
var targetTable = new EcommerceTable(client, "ecommerce");

// Scan source tables
var orders = await sourceOrdersTable.Scan().ToListAsync();
var orderLines = await sourceOrderLinesTable.Scan().ToListAsync();

// Write to target table with new key patterns
foreach (var order in orders.Items)
{
    await targetTable.Orders.Put(order).PutAsync();
}

foreach (var line in orderLines.Items)
{
    await targetTable.OrderLines.Put(line).PutAsync();
}
```


## When to Use Multi-Entity Tables

### Use Multi-Entity Tables When:

✅ **Multiple entities share access patterns**
- Order and OrderLines are always queried together
- Customer and CustomerAddresses need to be retrieved in one query

✅ **Entities have hierarchical relationships**
- Parent-child relationships (Order → OrderLines)
- Aggregation relationships (Invoice → LineItems)

✅ **You need efficient related entity queries**
- Get order and all related items in a single query
- Retrieve user profile with all associated data

✅ **Cost optimization is important**
- Reduce table count to minimize provisioned capacity costs
- Consolidate related data for better throughput utilization

✅ **Atomic transactions across entity types**
- Create order, lines, and payment atomically
- Update multiple related entities consistently

### Use Single-Entity Tables When:

❌ **Entities have completely independent access patterns**
- Users and Products are never queried together
- Separate microservices own different entities

❌ **Entities have different scaling characteristics**
- One entity has high write volume, another has high read volume
- Different entities need different capacity settings

❌ **Simplicity is more important than optimization**
- Small application with few entities
- Team is new to DynamoDB patterns

❌ **Entities belong to different bounded contexts**
- Clear domain boundaries between entities
- Different teams own different entities

### Comparison

| Aspect | Single-Entity Tables | Multi-Entity Tables |
|--------|---------------------|---------------------|
| **Complexity** | Simple | More complex |
| **Query Efficiency** | Multiple queries needed | Single query for related data |
| **Table Count** | One per entity | One per logical grouping |
| **Cost** | Higher (more tables) | Lower (fewer tables) |
| **Transactions** | Across tables | Within table |
| **Access Patterns** | Independent | Shared |
| **Learning Curve** | Easy | Moderate |
| **Best For** | Simple apps, microservices | Complex apps, single-table design |


## Summary

Multi-entity tables enable powerful single-table design patterns:

1. **Table Consolidation** - Multiple entities share one DynamoDB table
2. **Default Entity** - Mark one entity with `IsDefault = true` for table-level operations
3. **Entity Accessors** - Access entity-specific operations via `table.EntityName.Operation()`
4. **Table-Level Operations** - Convenient shortcuts using the default entity type
5. **Transactions** - Coordinate operations across entity types atomically
6. **Customization** - Control accessor generation and visibility with attributes

**Key Attributes:**
- `[DynamoDbTable(TableName, IsDefault = true)]` - Mark default entity
- `[GenerateEntityProperty(Name, Generate, Modifier)]` - Customize accessor properties
- `[GenerateAccessors(Operations, Generate, Modifier)]` - Customize operation methods

**Generated Structure:**
```csharp
public partial class MyAppTable : DynamoDbTableBase
{
    // Table-level operations (default entity)
    public GetItemRequestBuilder<Order> Get() { }
    public QueryRequestBuilder<Order> Query() { }
    
    // Entity accessors
    public OrderAccessor Orders { get; }
    public OrderLineAccessor OrderLines { get; }
    
    // Transactions (table level only)
    public TransactWriteItemsRequestBuilder TransactWrite() { }
}
```

For simpler scenarios with one entity per table, see [Single-Entity Tables](../getting-started/SingleEntityTables.md).

## Next Steps

- **[Table Generation Customization](TableGenerationCustomization.md)** - Advanced customization patterns
- **[Composite Entities](CompositeEntities.md)** - Multi-item entities and related entities
- **[Global Secondary Indexes](GlobalSecondaryIndexes.md)** - Alternative access patterns
- **[Transactions](../core-features/Transactions.md)** - Atomic operations across entities
- **[Entity Definition](../core-features/EntityDefinition.md)** - Complete entity configuration guide

---

[Previous: Composite Entities](CompositeEntities.md) | [Next: Table Generation Customization](TableGenerationCustomization.md)

**See Also:**
- [Single-Entity Tables](../getting-started/SingleEntityTables.md)
- [Basic Operations](../core-features/BasicOperations.md)
- [Querying Data](../core-features/QueryingData.md)
- [Attribute Reference](../reference/AttributeReference.md)
