---
title: "Composite Entities"
category: "advanced-topics"
order: 1
keywords: ["composite", "multi-item", "related entities", "collections", "relationships", "sort key patterns"]
related: ["GlobalSecondaryIndexes.md", "../core-features/EntityDefinition.md", "../core-features/QueryingData.md"]
---

[Documentation](../README.md) > [Advanced Topics](README.md) > Composite Entities

# Composite Entities

[Next: Global Secondary Indexes](GlobalSecondaryIndexes.md)

---

Composite entities are DynamoDB entities that span multiple items in a table, allowing you to model complex relationships and collections efficiently. This pattern is essential for single-table design and enables powerful query patterns.

## Concept and Use Cases

### What Are Composite Entities?

A composite entity is a C# object that represents data stored across multiple DynamoDB items sharing the same partition key but with different sort keys. This pattern allows you to:

- Store collections as separate items (one-to-many relationships)
- Model hierarchical data structures
- Implement efficient query patterns
- Maintain data consistency within a partition

### Common Use Cases

**1. Order with Line Items**
```
PK: ORDER#123          SK: METADATA        → Order header
PK: ORDER#123          SK: ITEM#001        → Line item 1
PK: ORDER#123          SK: ITEM#002        → Line item 2
PK: ORDER#123          SK: ITEM#003        → Line item 3
```

**2. Customer with Addresses**
```
PK: CUSTOMER#456       SK: PROFILE         → Customer profile
PK: CUSTOMER#456       SK: ADDRESS#HOME    → Home address
PK: CUSTOMER#456       SK: ADDRESS#WORK    → Work address
```

**3. Transaction with Audit Trail**
```
PK: TXN#789            SK: SUMMARY         → Transaction summary
PK: TXN#789            SK: AUDIT#001       → Audit entry 1
PK: TXN#789            SK: AUDIT#002       → Audit entry 2
```


## Multi-Item Entities (Collections)

Multi-item entities store collections as separate DynamoDB items, where each collection element becomes its own item with a unique sort key.

### Defining Multi-Item Entities

```csharp
using Oproto.FluentDynamoDb.Attributes;

[DynamoDbTable("orders")]
public partial class Order
{
    // Partition key - groups all related items
    [PartitionKey]
    [Computed(nameof(OrderId), Format = "ORDER#{0}")]
    [DynamoDbAttribute("pk")]
    public string OrderId { get; set; } = string.Empty;
    
    // Sort key - differentiates item types
    [SortKey]
    [DynamoDbAttribute("sk")]
    public string SortKey { get; set; } = "METADATA";
    
    // Order header data
    [DynamoDbAttribute("customerId")]
    public string CustomerId { get; set; } = string.Empty;
    
    [DynamoDbAttribute("orderDate")]
    public DateTime OrderDate { get; set; }
    
    [DynamoDbAttribute("status")]
    public string Status { get; set; } = "pending";
    
    // Collection stored as separate items
    public List<OrderItem> Items { get; set; } = new();
}

public class OrderItem
{
    public string ProductId { get; set; } = string.Empty;
    public string ProductName { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public decimal Price { get; set; }
    public decimal Subtotal => Quantity * Price;
}
```

### Storing Multi-Item Entities

When storing an order with items, each item becomes a separate DynamoDB item:

```csharp
var order = new Order
{
    OrderId = "order123",
    CustomerId = "customer456",
    OrderDate = DateTime.UtcNow,
    Status = "pending",
    Items = new List<OrderItem>
    {
        new() { ProductId = "prod1", ProductName = "Widget", Quantity = 2, Price = 10.00m },
        new() { ProductId = "prod2", ProductName = "Gadget", Quantity = 1, Price = 25.00m }
    }
};

// Store order header
await table.Put
    .WithItem(new Dictionary<string, AttributeValue>
    {
        [OrderFields.OrderId] = new AttributeValue { S = OrderKeys.Pk(order.OrderId) },
        [OrderFields.SortKey] = new AttributeValue { S = "METADATA" },
        [OrderFields.CustomerId] = new AttributeValue { S = order.CustomerId },
        [OrderFields.OrderDate] = new AttributeValue { S = order.OrderDate.ToString("o") },
        [OrderFields.Status] = new AttributeValue { S = order.Status }
    })
    .PutAsync();

// Store each item separately
foreach (var (item, index) in order.Items.Select((item, i) => (item, i)))
{
    await table.Put
        .WithItem(new Dictionary<string, AttributeValue>
        {
            [OrderFields.OrderId] = new AttributeValue { S = OrderKeys.Pk(order.OrderId) },
            [OrderFields.SortKey] = new AttributeValue { S = $"ITEM#{index:D3}" },
            ["productId"] = new AttributeValue { S = item.ProductId },
            ["productName"] = new AttributeValue { S = item.ProductName },
            ["quantity"] = new AttributeValue { N = item.Quantity.ToString() },
            ["price"] = new AttributeValue { N = item.Price.ToString() }
        })
        .PutAsync();
}
```


### Querying Multi-Item Entities

Query all items for an order using the partition key:

```csharp
// Query all items for the order
var response = await table.Query
    .Where($"{OrderFields.OrderId} = {{0}}", OrderKeys.Pk("order123"))
    .ToListAsync();

// Group items by sort key pattern
var orderHeader = response.Items
    .FirstOrDefault(item => item[OrderFields.SortKey].S == "METADATA");

var orderItems = response.Items
    .Where(item => item[OrderFields.SortKey].S.StartsWith("ITEM#"))
    .Select(item => new OrderItem
    {
        ProductId = item["productId"].S,
        ProductName = item["productName"].S,
        Quantity = int.Parse(item["quantity"].N),
        Price = decimal.Parse(item["price"].N)
    })
    .ToList();

// Reconstruct the composite entity
var order = new Order
{
    OrderId = "order123",
    CustomerId = orderHeader?["customerId"]?.S ?? string.Empty,
    OrderDate = DateTime.Parse(orderHeader?["orderDate"]?.S ?? DateTime.UtcNow.ToString("o")),
    Status = orderHeader?["status"]?.S ?? "unknown",
    Items = orderItems
};
```

## Related Entities with [RelatedEntity] Attribute

The `[RelatedEntity]` attribute enables automatic population of related data based on sort key patterns. This is a more declarative approach than manual grouping.

### Single Related Entity

Use `[RelatedEntity]` for one-to-one relationships:

```csharp
[DynamoDbTable("transactions")]
public partial class Transaction
{
    [PartitionKey]
    [Computed(nameof(TransactionId), Format = "TXN#{0}")]
    [DynamoDbAttribute("pk")]
    public string TransactionId { get; set; } = string.Empty;
    
    [SortKey]
    [DynamoDbAttribute("sk")]
    public string SortKey { get; set; } = "SUMMARY";
    
    [DynamoDbAttribute("amount")]
    public decimal Amount { get; set; }
    
    [DynamoDbAttribute("description")]
    public string Description { get; set; } = string.Empty;
    
    // Automatically populated from item with SK = "SUMMARY"
    [RelatedEntity("summary")]
    public TransactionSummary? Summary { get; set; }
}

public class TransactionSummary
{
    public decimal TotalAmount { get; set; }
    public int ItemCount { get; set; }
    public DateTime LastUpdated { get; set; }
}
```

**How It Works:**
1. Query returns multiple items with the same partition key
2. Source generator identifies items matching the sort key pattern
3. Related entity is automatically populated from matching items

### Collection Related Entities

Use `[RelatedEntity]` with wildcard patterns for one-to-many relationships:

```csharp
[DynamoDbTable("transactions")]
public partial class TransactionWithAudit
{
    [PartitionKey]
    [Computed(nameof(TransactionId), Format = "TXN#{0}")]
    [DynamoDbAttribute("pk")]
    public string TransactionId { get; set; } = string.Empty;
    
    [SortKey]
    [DynamoDbAttribute("sk")]
    public string SortKey { get; set; } = "SUMMARY";
    
    [DynamoDbAttribute("amount")]
    public decimal Amount { get; set; }
    
    // Automatically populated from items with SK starting with "audit#"
    [RelatedEntity("audit#*")]
    public List<AuditEntry>? AuditEntries { get; set; }
}

public class AuditEntry
{
    public string Action { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
    public string UserId { get; set; } = string.Empty;
    public string Details { get; set; } = string.Empty;
}
```


## Sort Key Pattern Matching

Sort key patterns define how related entities are identified and grouped.

### Exact Match Patterns

Match a specific sort key value:

```csharp
// Matches only items with SK = "summary"
[RelatedEntity("summary")]
public TransactionSummary? Summary { get; set; }

// Matches only items with SK = "PROFILE"
[RelatedEntity("PROFILE")]
public UserProfile? Profile { get; set; }
```

### Wildcard Patterns

Match multiple items using wildcards:

```csharp
// Matches all items with SK starting with "audit#"
// Examples: "audit#001", "audit#002", "audit#abc"
[RelatedEntity("audit#*")]
public List<AuditEntry>? AuditEntries { get; set; }

// Matches all items with SK starting with "ITEM#"
// Examples: "ITEM#001", "ITEM#002", "ITEM#999"
[RelatedEntity("ITEM#*")]
public List<OrderItem>? Items { get; set; }

// Matches all items with SK starting with "ADDRESS#"
// Examples: "ADDRESS#HOME", "ADDRESS#WORK", "ADDRESS#BILLING"
[RelatedEntity("ADDRESS#*")]
public List<Address>? Addresses { get; set; }
```

### Pattern Matching Rules

1. **Exact match**: No wildcard, matches SK exactly
2. **Prefix match**: Ends with `*`, matches SK starting with the prefix
3. **Case sensitive**: Patterns are case-sensitive
4. **Order matters**: Items are returned in sort key order

### Multiple Related Entities

Define multiple related entity patterns on the same entity:

```csharp
[DynamoDbTable("customers")]
public partial class Customer
{
    [PartitionKey]
    [Computed(nameof(CustomerId), Format = "CUSTOMER#{0}")]
    [DynamoDbAttribute("pk")]
    public string CustomerId { get; set; } = string.Empty;
    
    [SortKey]
    [DynamoDbAttribute("sk")]
    public string SortKey { get; set; } = "PROFILE";
    
    [DynamoDbAttribute("name")]
    public string Name { get; set; } = string.Empty;
    
    [DynamoDbAttribute("email")]
    public string Email { get; set; } = string.Empty;
    
    // Multiple related entity patterns
    [RelatedEntity("ADDRESS#*")]
    public List<Address>? Addresses { get; set; }
    
    [RelatedEntity("ORDER#*")]
    public List<OrderSummary>? RecentOrders { get; set; }
    
    [RelatedEntity("PREFERENCE")]
    public CustomerPreferences? Preferences { get; set; }
}
```

**DynamoDB Items:**
```
PK: CUSTOMER#123       SK: PROFILE           → Customer profile
PK: CUSTOMER#123       SK: ADDRESS#HOME      → Home address
PK: CUSTOMER#123       SK: ADDRESS#WORK      → Work address
PK: CUSTOMER#123       SK: ORDER#001         → Recent order 1
PK: CUSTOMER#123       SK: ORDER#002         → Recent order 2
PK: CUSTOMER#123       SK: PREFERENCE        → Preferences
```

## Single vs Collection Relationships

### Single Related Entity (One-to-One)

Use a nullable property for optional one-to-one relationships:

```csharp
// Single related entity - expects 0 or 1 matching item
[RelatedEntity("summary")]
public TransactionSummary? Summary { get; set; }

[RelatedEntity("PROFILE")]
public UserProfile? Profile { get; set; }
```

**Behavior:**
- If no matching item found: Property is `null`
- If one matching item found: Property is populated
- If multiple matching items found: First item is used (warning logged)

### Collection Related Entities (One-to-Many)

Use a `List<T>` for one-to-many relationships:

```csharp
// Collection related entity - expects 0 or more matching items
[RelatedEntity("audit#*")]
public List<AuditEntry>? AuditEntries { get; set; }

[RelatedEntity("ITEM#*")]
public List<OrderItem>? Items { get; set; }
```

**Behavior:**
- If no matching items found: Property is `null` or empty list
- If matching items found: All matching items are added to the list
- Items are ordered by sort key


## Performance Considerations

### Query Efficiency

**✅ Efficient: Single Query for Composite Entity**
```csharp
// One query retrieves all related items
var response = await table.Query<Order>()
    .Where($"{OrderFields.OrderId} = {{0}}", OrderKeys.Pk("order123"))
    .ToListAsync();

// All related entities populated automatically
var order = response.Items.First();
Console.WriteLine($"Order has {order.Items?.Count ?? 0} items");
```

**❌ Inefficient: Multiple Queries**
```csharp
// Avoid: Multiple round trips to DynamoDB
var orderHeader = await table.Get<Order>()
    .WithKey(OrderFields.OrderId, OrderKeys.Pk("order123"))
    .WithKey(OrderFields.SortKey, "METADATA")
    .GetItemAsync();

// Separate query for each item type
var items = await table.Query
    .Where($"{OrderFields.OrderId} = {{0}} AND begins_with({OrderFields.SortKey}, {{1}})", 
           OrderKeys.Pk("order123"), "ITEM#")
    .ToListAsync();
```

### Item Size Limits

DynamoDB has a 400KB item size limit. For composite entities:

**Best Practices:**
1. **Keep individual items small** - Each item (header, line item, audit entry) should be well under 400KB
2. **Use pagination for large collections** - If you have hundreds of line items, consider pagination
3. **Monitor item sizes** - Use CloudWatch metrics to track item sizes

```csharp
// Good: Each item is small
PK: ORDER#123    SK: METADATA     → 5KB order header
PK: ORDER#123    SK: ITEM#001     → 1KB line item
PK: ORDER#123    SK: ITEM#002     → 1KB line item
// ... 100 more items, each 1KB

// Total: 105KB across 102 items (well within limits)
```

### Read Capacity Considerations

Querying composite entities consumes read capacity based on:
- Number of items returned
- Size of items
- Consistency level (eventually consistent vs strongly consistent)

**Example:**
```csharp
// Query returns 10 items totaling 40KB
// Eventually consistent: 5 RCUs (40KB / 8KB, rounded up)
// Strongly consistent: 10 RCUs (40KB / 4KB, rounded up)

var response = await table.Query<Order>()
    .Where($"{OrderFields.OrderId} = {{0}}", OrderKeys.Pk("order123"))
    .UsingConsistentRead()  // Optional: Use strongly consistent reads
    .ToListAsync();
```

### Pagination for Large Collections

For entities with many related items, use pagination:

```csharp
var allItems = new List<OrderItem>();
string? lastEvaluatedKey = null;

do
{
    var response = await table.Query
        .Where($"{OrderFields.OrderId} = {{0}}", OrderKeys.Pk("order123"))
        .Take(100)  // Limit items per page
        .WithExclusiveStartKey(lastEvaluatedKey)
        .ToListAsync();
    
    // Process items
    var pageItems = response.Items
        .Where(item => item[OrderFields.SortKey].S.StartsWith("ITEM#"))
        .Select(item => /* map to OrderItem */)
        .ToList();
    
    allItems.AddRange(pageItems);
    lastEvaluatedKey = response.LastEvaluatedKey;
    
} while (lastEvaluatedKey != null);
```

### Batch Operations

Use batch operations for efficient writes:

```csharp
// Batch write for composite entity
var batchBuilder = new BatchWriteItemRequestBuilder(client);

// Add order header
batchBuilder.Put(table, builder => builder
    .WithItem(/* order header attributes */));

// Add all line items in batch
foreach (var item in order.Items)
{
    batchBuilder.Put(table, builder => builder
        .WithItem(/* line item attributes */));
}

// Execute batch (up to 25 items per batch)
await batchBuilder.ExecuteAsync();
```


## Real-World Examples

### Example 1: E-Commerce Order with Line Items

Complete implementation of an order with line items:

```csharp
using Oproto.FluentDynamoDb.Attributes;

[DynamoDbTable("orders")]
public partial class Order
{
    [PartitionKey]
    [Computed(nameof(OrderId), Format = "ORDER#{0}")]
    [DynamoDbAttribute("pk")]
    public string OrderId { get; set; } = string.Empty;
    
    [SortKey]
    [DynamoDbAttribute("sk")]
    public string SortKey { get; set; } = "METADATA";
    
    // Order header fields
    [DynamoDbAttribute("customerId")]
    public string CustomerId { get; set; } = string.Empty;
    
    [DynamoDbAttribute("orderDate")]
    public DateTime OrderDate { get; set; }
    
    [DynamoDbAttribute("status")]
    public string Status { get; set; } = "pending";
    
    [DynamoDbAttribute("shippingAddress")]
    public string ShippingAddress { get; set; } = string.Empty;
    
    [DynamoDbAttribute("total")]
    public decimal Total { get; set; }
    
    // Related entities
    [RelatedEntity("ITEM#*")]
    public List<OrderItem>? Items { get; set; }
    
    [RelatedEntity("PAYMENT")]
    public PaymentInfo? Payment { get; set; }
}

public class OrderItem
{
    public string ProductId { get; set; } = string.Empty;
    public string ProductName { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal Discount { get; set; }
    public decimal Subtotal => (UnitPrice * Quantity) - Discount;
}

public class PaymentInfo
{
    public string PaymentMethod { get; set; } = string.Empty;
    public string TransactionId { get; set; } = string.Empty;
    public DateTime PaymentDate { get; set; }
    public decimal Amount { get; set; }
}
```

**Usage:**

```csharp
// Create order with items
var order = new Order
{
    OrderId = Guid.NewGuid().ToString(),
    CustomerId = "customer123",
    OrderDate = DateTime.UtcNow,
    Status = "pending",
    ShippingAddress = "123 Main St, City, State 12345",
    Items = new List<OrderItem>
    {
        new() { ProductId = "prod1", ProductName = "Widget", Quantity = 2, UnitPrice = 10.00m, Discount = 0 },
        new() { ProductId = "prod2", ProductName = "Gadget", Quantity = 1, UnitPrice = 25.00m, Discount = 2.50m }
    }
};

order.Total = order.Items.Sum(i => i.Subtotal);

// Store order (header + items)
await StoreOrderAsync(table, order);

// Retrieve complete order
var retrievedOrder = await GetOrderAsync(table, order.OrderId);
Console.WriteLine($"Order {retrievedOrder.OrderId} has {retrievedOrder.Items?.Count} items");
Console.WriteLine($"Total: ${retrievedOrder.Total}");
```

### Example 2: Customer with Addresses and Preferences

Multi-relationship composite entity:

```csharp
[DynamoDbTable("customers")]
public partial class Customer
{
    [PartitionKey]
    [Computed(nameof(CustomerId), Format = "CUSTOMER#{0}")]
    [DynamoDbAttribute("pk")]
    public string CustomerId { get; set; } = string.Empty;
    
    [SortKey]
    [DynamoDbAttribute("sk")]
    public string SortKey { get; set; } = "PROFILE";
    
    // Customer profile fields
    [DynamoDbAttribute("name")]
    public string Name { get; set; } = string.Empty;
    
    [DynamoDbAttribute("email")]
    public string Email { get; set; } = string.Empty;
    
    [DynamoDbAttribute("phone")]
    public string Phone { get; set; } = string.Empty;
    
    [DynamoDbAttribute("createdAt")]
    public DateTime CreatedAt { get; set; }
    
    // Related entities
    [RelatedEntity("ADDRESS#*")]
    public List<Address>? Addresses { get; set; }
    
    [RelatedEntity("PREFERENCES")]
    public CustomerPreferences? Preferences { get; set; }
}

public class Address
{
    public string Type { get; set; } = string.Empty;  // HOME, WORK, BILLING
    public string Street { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
    public string State { get; set; } = string.Empty;
    public string ZipCode { get; set; } = string.Empty;
    public bool IsDefault { get; set; }
}

public class CustomerPreferences
{
    public string Theme { get; set; } = "light";
    public string Language { get; set; } = "en";
    public bool EmailNotifications { get; set; } = true;
    public bool SmsNotifications { get; set; } = false;
}
```

**DynamoDB Structure:**
```
PK: CUSTOMER#123       SK: PROFILE           → Customer profile
PK: CUSTOMER#123       SK: ADDRESS#HOME      → Home address
PK: CUSTOMER#123       SK: ADDRESS#WORK      → Work address
PK: CUSTOMER#123       SK: ADDRESS#BILLING   → Billing address
PK: CUSTOMER#123       SK: PREFERENCES       → Preferences
```

**Usage:**

```csharp
// Query customer with all related data
var response = await table.Query<Customer>()
    .Where($"{CustomerFields.CustomerId} = {{0}}", CustomerKeys.Pk("customer123"))
    .ToListAsync();

var customer = response.Items.First();

// Access related entities
Console.WriteLine($"Customer: {customer.Name}");
Console.WriteLine($"Addresses: {customer.Addresses?.Count ?? 0}");
Console.WriteLine($"Theme: {customer.Preferences?.Theme ?? "default"}");

// Find default address
var defaultAddress = customer.Addresses?.FirstOrDefault(a => a.IsDefault);
if (defaultAddress != null)
{
    Console.WriteLine($"Default: {defaultAddress.Street}, {defaultAddress.City}");
}
```


### Example 3: Transaction with Ledger Entries and Audit Trail

Financial transaction with multiple related collections:

```csharp
[DynamoDbTable("transactions")]
public partial class Transaction
{
    [PartitionKey]
    [Computed(nameof(TenantId), nameof(TransactionId), Format = "TENANT#{0}#TXN#{1}")]
    [DynamoDbAttribute("pk")]
    public string TenantId { get; set; } = string.Empty;
    
    public string TransactionId { get; set; } = string.Empty;
    
    [SortKey]
    [DynamoDbAttribute("sk")]
    public string SortKey { get; set; } = "SUMMARY";
    
    // Transaction summary fields
    [DynamoDbAttribute("description")]
    public string Description { get; set; } = string.Empty;
    
    [DynamoDbAttribute("status")]
    public string Status { get; set; } = "draft";
    
    [DynamoDbAttribute("createdAt")]
    public DateTime CreatedAt { get; set; }
    
    [DynamoDbAttribute("createdBy")]
    public string CreatedBy { get; set; } = string.Empty;
    
    // Related entities
    [RelatedEntity("LEDGER#*")]
    public List<LedgerEntry>? LedgerEntries { get; set; }
    
    [RelatedEntity("AUDIT#*")]
    public List<AuditEntry>? AuditTrail { get; set; }
}

public class LedgerEntry
{
    public string LedgerId { get; set; } = string.Empty;
    public string AccountId { get; set; } = string.Empty;
    public decimal DebitAmount { get; set; }
    public decimal CreditAmount { get; set; }
    public string Description { get; set; } = string.Empty;
}

public class AuditEntry
{
    public string Action { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
    public string UserId { get; set; } = string.Empty;
    public string Details { get; set; } = string.Empty;
    public Dictionary<string, string> Changes { get; set; } = new();
}
```

**DynamoDB Structure:**
```
PK: TENANT#abc#TXN#123    SK: SUMMARY        → Transaction summary
PK: TENANT#abc#TXN#123    SK: LEDGER#001     → Ledger entry 1
PK: TENANT#abc#TXN#123    SK: LEDGER#002     → Ledger entry 2
PK: TENANT#abc#TXN#123    SK: AUDIT#001      → Audit entry 1
PK: TENANT#abc#TXN#123    SK: AUDIT#002      → Audit entry 2
PK: TENANT#abc#TXN#123    SK: AUDIT#003      → Audit entry 3
```

**Usage:**

```csharp
// Create transaction with ledger entries
var transaction = new Transaction
{
    TenantId = "tenant123",
    TransactionId = Guid.NewGuid().ToString(),
    Description = "Payment received",
    Status = "draft",
    CreatedAt = DateTime.UtcNow,
    CreatedBy = "user456",
    LedgerEntries = new List<LedgerEntry>
    {
        new() { LedgerId = "ledger1", AccountId = "cash", DebitAmount = 100.00m, CreditAmount = 0 },
        new() { LedgerId = "ledger2", AccountId = "revenue", DebitAmount = 0, CreditAmount = 100.00m }
    }
};

// Store transaction
await StoreTransactionAsync(table, transaction);

// Add audit entry
await AddAuditEntryAsync(table, transaction.TenantId, transaction.TransactionId, new AuditEntry
{
    Action = "CREATED",
    Timestamp = DateTime.UtcNow,
    UserId = "user456",
    Details = "Transaction created"
});

// Retrieve complete transaction with audit trail
var fullTransaction = await GetTransactionAsync(table, transaction.TenantId, transaction.TransactionId);
Console.WriteLine($"Transaction has {fullTransaction.LedgerEntries?.Count} ledger entries");
Console.WriteLine($"Audit trail has {fullTransaction.AuditTrail?.Count} entries");
```

## Best Practices

### 1. Use Consistent Sort Key Prefixes

```csharp
// ✅ Good - consistent prefix pattern
METADATA          → Main entity
ITEM#001          → Collection items
ITEM#002
ADDRESS#HOME      → Related entities
ADDRESS#WORK
AUDIT#001         → Audit trail
AUDIT#002

// ❌ Avoid - inconsistent patterns
MAIN              → Hard to distinguish
item_1            → Inconsistent casing
addr-home         → Different separator
audit001          → No separator
```

### 2. Order Sort Keys for Efficient Queries

```csharp
// ✅ Good - sortable format with zero-padding
ITEM#001
ITEM#002
ITEM#010
ITEM#100

// ❌ Avoid - not sortable
ITEM#1
ITEM#2
ITEM#10
ITEM#100
// Results in: ITEM#1, ITEM#10, ITEM#100, ITEM#2 (wrong order)
```

### 3. Keep Related Entity Types Separate

```csharp
// ✅ Good - clear separation
[RelatedEntity("ITEM#*")]
public List<OrderItem>? Items { get; set; }

[RelatedEntity("PAYMENT#*")]
public List<Payment>? Payments { get; set; }

// ❌ Avoid - overlapping patterns
[RelatedEntity("*")]  // Matches everything
public List<object>? AllRelated { get; set; }
```

### 4. Use Transactions for Consistency

```csharp
// ✅ Good - atomic write of composite entity
var txnBuilder = new TransactWriteItemsRequestBuilder(client);

// Add order header
txnBuilder.Put(table, builder => builder.WithItem(/* order header */));

// Add all items in same transaction
foreach (var item in order.Items)
{
    txnBuilder.Put(table, builder => builder.WithItem(/* item */));
}

await txnBuilder.CommitAsync();
```

### 5. Consider Item Count Limits

DynamoDB queries return up to 1MB of data. For large composite entities:

```csharp
// ✅ Good - paginate large collections
var allItems = new List<OrderItem>();
string? lastKey = null;

do
{
    var response = await table.Query
        .Where($"{OrderFields.OrderId} = {{0}}", OrderKeys.Pk("order123"))
        .Take(100)
        .WithExclusiveStartKey(lastKey)
        .ToListAsync();
    
    // Process page
    allItems.AddRange(/* extract items */);
    lastKey = response.LastEvaluatedKey;
    
} while (lastKey != null);
```

## Next Steps

- **[Global Secondary Indexes](GlobalSecondaryIndexes.md)** - Query composite entities by different attributes
- **[Performance Optimization](PerformanceOptimization.md)** - Optimize composite entity queries
- **[Querying Data](../core-features/QueryingData.md)** - Advanced query patterns
- **[Batch Operations](../core-features/BatchOperations.md)** - Efficient batch writes

---

[Previous: Advanced Topics](README.md) | [Next: Global Secondary Indexes](GlobalSecondaryIndexes.md)

**See Also:**
- [Entity Definition](../core-features/EntityDefinition.md)
- [Expression Formatting](../core-features/ExpressionFormatting.md)
- [Transactions](../core-features/Transactions.md)
- [Attribute Reference](../reference/AttributeReference.md)
