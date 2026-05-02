---
title: "Global Secondary Indexes"
category: "advanced-topics"
order: 2
keywords: ["GSI", "global secondary index", "query", "access patterns", "projection"]
related: ["CompositeEntities.md", "../core-features/EntityDefinition.md", "../core-features/QueryingData.md"]
---

[Documentation](../README.md) > [Advanced Topics](README.md) > Global Secondary Indexes

# Global Secondary Indexes

[Previous: Composite Entities](CompositeEntities.md) | [Next: STS Integration](STSIntegration.md)

---

Global Secondary Indexes (GSIs) enable alternative query patterns on your DynamoDB tables. This guide covers GSI configuration, generated code, and best practices for using GSIs with Oproto.FluentDynamoDb.

## GSI Attribute Configuration

### Basic GSI Definition

Define a GSI using the `[GsiPartitionKey]` and `[GsiSortKey]` attributes:

```csharp
using Oproto.FluentDynamoDb.Attributes;

[DynamoDbTable("users")]
public partial class User
{
    // Primary table keys
    [PartitionKey]
    [DynamoDbAttribute("pk")]
    public string UserId { get; set; } = string.Empty;
    
    // GSI partition key
    [GsiPartitionKey("EmailIndex")]
    [DynamoDbAttribute("email")]
    public string Email { get; set; } = string.Empty;
    
    [DynamoDbAttribute("name")]
    public string Name { get; set; } = string.Empty;
}
```

**Generated GSI Constants:**
```csharp
public static class UserIndexes
{
    public const string EmailIndex = "EmailIndex";
}
```

### GSI with Sort Key

Add a sort key to your GSI for range queries:

```csharp
[DynamoDbTable("orders")]
public partial class Order
{
    [PartitionKey]
    [DynamoDbAttribute("pk")]
    public string OrderId { get; set; } = string.Empty;
    
    // GSI partition key
    [GsiPartitionKey("StatusIndex")]
    [DynamoDbAttribute("status")]
    public string Status { get; set; } = string.Empty;
    
    // GSI sort key
    [GsiSortKey("StatusIndex")]
    [DynamoDbAttribute("createdAt")]
    public DateTime CreatedAt { get; set; }
    
    [DynamoDbAttribute("customerId")]
    public string CustomerId { get; set; } = string.Empty;
    
    [DynamoDbAttribute("total")]
    public decimal Total { get; set; }
}
```


### Multiple GSIs

Define multiple GSIs on the same entity for different access patterns:

```csharp
[DynamoDbTable("products")]
public partial class Product
{
    [PartitionKey]
    [DynamoDbAttribute("pk")]
    public string ProductId { get; set; } = string.Empty;
    
    // GSI 1: Query by category
    [GsiPartitionKey("CategoryIndex")]
    [DynamoDbAttribute("category")]
    public string Category { get; set; } = string.Empty;
    
    [GsiSortKey("CategoryIndex")]
    [DynamoDbAttribute("price")]
    public decimal Price { get; set; }
    
    // GSI 2: Query by vendor
    [GsiPartitionKey("VendorIndex")]
    [DynamoDbAttribute("vendorId")]
    public string VendorId { get; set; } = string.Empty;
    
    [GsiSortKey("VendorIndex")]
    [DynamoDbAttribute("createdAt")]
    public DateTime CreatedAt { get; set; }
    
    // GSI 3: Query by status
    [GsiPartitionKey("StatusIndex")]
    [DynamoDbAttribute("status")]
    public string Status { get; set; } = "active";
    
    [DynamoDbAttribute("name")]
    public string Name { get; set; } = string.Empty;
}
```

**Generated Code:**
```csharp
public static class ProductIndexes
{
    public const string CategoryIndex = "CategoryIndex";
    public const string VendorIndex = "VendorIndex";
    public const string StatusIndex = "StatusIndex";
}
```

### GSI with ProjectionType

The `ProjectionType` property on `[GsiPartitionKey]` specifies the DynamoDB projection type for the index. This is metadata that affects schema validation and table creation:

```csharp
[DynamoDbTable("orders")]
public partial class Order
{
    [PartitionKey]
    [DynamoDbAttribute("pk")]
    public string Pk { get; set; } = string.Empty;
    
    [SortKey]
    [DynamoDbAttribute("sk")]
    public string Sk { get; set; } = string.Empty;
    
    // GSI with ALL projection (default)
    [GsiPartitionKey("StatusIndex")]
    [DynamoDbAttribute("status")]
    public string Status { get; set; } = string.Empty;
    
    // GSI with KEYS_ONLY projection - auto-generates projection record
    [GsiPartitionKey("CategoryIndex", ProjectionType = ProjectionType.KeysOnly)]
    [DynamoDbAttribute("category")]
    public string Category { get; set; } = string.Empty;
}
```

**ProjectionType Values:**

| Value | Description | Generated Code |
|-------|-------------|----------------|
| `All` (default) | All attributes projected | Single-entity: `DynamoDbIndex<TEntity>`; Multi-entity: `DynamoDbIndex` |
| `KeysOnly` | Only key attributes | Auto-generates `{IndexName}KeysProjection` record |
| `Include` | Keys plus specified attributes | Use with `[UseProjection]` for custom projection |

**Important:** The `ProjectionType` property is metadata only - it does not affect query behavior. It serves these purposes:
1. **Documentation**: Reflects the actual DynamoDB index configuration
2. **Schema Validation**: Enables validation that the index is configured as expected
3. **Keys Only Auto-Generation**: Triggers generation of a keys-only projection record
4. **Table Creation**: Used by `TableCreator` to configure the DynamoDB index projection

### GSI with Computed Keys

Combine GSIs with computed keys for advanced patterns:

```csharp
[DynamoDbTable("transactions")]
public partial class Transaction
{
    [PartitionKey]
    [DynamoDbAttribute("pk")]
    public string TransactionId { get; set; } = string.Empty;
    
    // Source properties
    public string TenantId { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    
    // GSI partition key: "TENANT#tenant123#STATUS#pending"
    [GsiPartitionKey("TenantStatusIndex")]
    [Computed(nameof(TenantId), nameof(Status), Format = "TENANT#{0}#STATUS#{1}")]
    [DynamoDbAttribute("gsi1pk")]
    public string TenantStatusKey { get; set; } = string.Empty;
    
    // GSI sort key: ISO 8601 timestamp
    [GsiSortKey("TenantStatusIndex")]
    [Computed(nameof(CreatedAt), Format = "{0:o}")]
    [DynamoDbAttribute("gsi1sk")]
    public string CreatedAtKey { get; set; } = string.Empty;
}
```

**Use Case:** Query all pending transactions for a tenant, sorted by creation date.

## Generated GSI Field Constants

The source generator creates field constants for GSI attributes:

```csharp
// Generated: OrderFields.g.cs
public static class OrderFields
{
    // Main table fields
    public const string OrderId = "pk";
    public const string Status = "status";
    public const string CreatedAt = "createdAt";
    public const string CustomerId = "customerId";
    public const string Total = "total";
    
    // GSI-specific nested class
    public static class StatusIndex
    {
        public const string Status = "status";
        public const string CreatedAt = "createdAt";
    }
}
```

**Usage:**
```csharp
// Use main table fields with entity accessor
var order = await table.Orders.GetAsync("order123");

// Use GSI with index accessor (preferred)
var orders = await table.StatusIndex.Query<Order>()
    .Where(x => x.Status == "pending")
    .ToListAsync();
```


## Generated GSI Key Builders

The source generator creates key builder methods for GSI keys:

```csharp
// Generated: OrderKeys.g.cs
public static class OrderKeys
{
    // Main table keys
    public static string Pk(string orderId) => orderId;
    
    // GSI key builders (nested class)
    public static class StatusIndex
    {
        public static string Pk(string status) => status;
        public static string Sk(DateTime createdAt) => createdAt.ToString("o");
    }
}
```

**Usage:**
```csharp
// Build GSI partition key
var statusKey = OrderKeys.StatusIndex.Pk("pending");  // Returns "pending"

// Build GSI sort key
var dateKey = OrderKeys.StatusIndex.Sk(DateTime.UtcNow);  // Returns ISO 8601 timestamp

// Use in query with index accessor
var orders = await table.StatusIndex.Query<Order>()
    .Where(x => x.Status == "pending")
    .ToListAsync();
```

### Computed GSI Keys

For computed GSI keys, the generator creates appropriate builder methods:

```csharp
[DynamoDbTable("events")]
public partial class Event
{
    [PartitionKey]
    [DynamoDbAttribute("pk")]
    public string EventId { get; set; } = string.Empty;
    
    public string TenantId { get; set; } = string.Empty;
    public string EventType { get; set; } = string.Empty;
    
    // Computed GSI key
    [GsiPartitionKey("TenantTypeIndex")]
    [Computed(nameof(TenantId), nameof(EventType), Format = "TENANT#{0}#TYPE#{1}")]
    [DynamoDbAttribute("gsi1pk")]
    public string TenantTypeKey { get; set; } = string.Empty;
}
```

**Generated:**
```csharp
public static class EventKeys
{
    public static string Pk(string eventId) => eventId;
    
    public static class TenantTypeIndex
    {
        public static string Pk(string tenantId, string eventType) 
            => $"TENANT#{tenantId}#TYPE#{eventType}";
    }
}
```

**Usage:**
```csharp
// Build computed GSI key
var gsiKey = EventKeys.TenantTypeIndex.Pk("tenant123", "LOGIN");
// Returns: "TENANT#tenant123#TYPE#LOGIN"

// Use in query with index accessor
var events = await table.TenantTypeIndex.Query<Event>()
    .Where(x => x.TenantTypeKey == EventKeys.TenantTypeIndex.Pk("tenant123", "LOGIN"))
    .ToListAsync();
```

## Querying GSIs with Expression Formatting

### Basic GSI Query

Query a GSI using expression formatting:

```csharp
// Query orders by status using index accessor (preferred)
var orders = await table.StatusIndex.Query<Order>()
    .Where(x => x.Status == "pending")
    .ToListAsync();

foreach (var order in orders)
{
    Console.WriteLine($"Order {order.OrderId}: ${order.Total}");
}
```

### GSI Query with Sort Key Range

Query with sort key conditions:

```csharp
// Query pending orders created in the last 7 days using index accessor
var sevenDaysAgo = DateTime.UtcNow.AddDays(-7);

var orders = await table.StatusIndex.Query<Order>()
    .Where(x => x.Status == "pending" && x.CreatedAt > sevenDaysAgo)
    .ToListAsync();
```

### GSI Query with Filter Expression

Add filter expressions for additional filtering:

```csharp
// Query pending orders over $100 using index accessor with filter
var orders = await table.StatusIndex.Query<Order>()
    .Where(x => x.Status == "pending")
    .WithFilter(x => x.Total > 100.00m)
    .ToListAsync();
```

**Note:** Filter expressions are applied after the query, so they don't reduce read capacity consumption.

### GSI Query with Pagination

Paginate through large result sets:

```csharp
var allOrders = new List<Order>();
Dictionary<string, AttributeValue>? lastEvaluatedKey = null;

do
{
    var query = table.StatusIndex.Query<Order>()
        .Where(x => x.Status == "pending")
        .Take(100);
    
    if (lastEvaluatedKey != null)
    {
        query = query.WithExclusiveStartKey(lastEvaluatedKey);
    }
    
    var orders = await query.ToListAsync();
    allOrders.AddRange(orders);
    
    // Access pagination key via builder.Response
    lastEvaluatedKey = query.Response?.LastEvaluatedKey;
    
} while (lastEvaluatedKey != null);

Console.WriteLine($"Found {allOrders.Count} pending orders");
```


## Projection Considerations

### Projection Types

DynamoDB GSIs support three projection types:

1. **KEYS_ONLY** - Only key attributes
2. **INCLUDE** - Keys plus specified attributes
3. **ALL** - All attributes (default)

**Note:** Projection type is configured in your DynamoDB table definition, not in the entity class.

### Automatic Entity Projections for Single-Entity Tables

For single-entity tables (tables with only one entity type), the source generator automatically uses the entity type as the default projection for indexes. This enables non-generic `Query()` methods:

```csharp
// Single-entity table
[DynamoDbTable("orders")]
public partial class Order
{
    [PartitionKey]
    [DynamoDbAttribute("pk")]
    public string Pk { get; set; } = string.Empty;
    
    [SortKey]
    [DynamoDbAttribute("sk")]
    public string Sk { get; set; } = string.Empty;
    
    [GsiPartitionKey("StatusIndex")]
    [DynamoDbAttribute("status")]
    public string Status { get; set; } = string.Empty;
}

// Generated: DynamoDbIndex<Order> StatusIndex
// Non-generic Query() returns Order entities
var orders = await table.StatusIndex.Query(x => x.Status == "pending").ToListAsync();
```

**Behavior by Table Type:**

| Table Type | Index Without `[UseProjection]` | Generated Index Type |
|------------|--------------------------------|---------------------|
| Single-entity | Uses entity as default projection | `DynamoDbIndex<TEntity>` |
| Multi-entity | No default projection | `DynamoDbIndex` (generic required) |

### Keys Only Projection Auto-Generation

When `ProjectionType = KeysOnly` is specified on an index, the source generator automatically creates a read-only projection record containing only the key attributes:

```csharp
[DynamoDbTable("orders")]
public partial class Order
{
    [PartitionKey]
    [DynamoDbAttribute("pk")]
    public string Pk { get; set; } = string.Empty;
    
    [SortKey]
    [DynamoDbAttribute("sk")]
    public string Sk { get; set; } = string.Empty;
    
    // Keys Only projection - auto-generates StatusIndexKeysProjection
    [GsiPartitionKey("StatusIndex", ProjectionType = ProjectionType.KeysOnly)]
    [DynamoDbAttribute("gsi1pk")]
    public string Gsi1Pk { get; set; } = string.Empty;
    
    [GsiSortKey("StatusIndex")]
    [DynamoDbAttribute("gsi1sk")]
    public string Gsi1Sk { get; set; } = string.Empty;
}
```

**Generated Keys Only Projection:**

```csharp
// Auto-generated nested record within table class
public sealed record StatusIndexKeysProjection : IReadOnlyEntity<StatusIndexKeysProjection>
{
    // Base table keys
    [DynamoDbAttribute("pk")]
    public string Pk { get; init; } = string.Empty;
    
    [DynamoDbAttribute("sk")]
    public string Sk { get; init; } = string.Empty;
    
    // GSI keys
    [DynamoDbAttribute("gsi1pk")]
    public string Gsi1Pk { get; init; } = string.Empty;
    
    [DynamoDbAttribute("gsi1sk")]
    public string Gsi1Sk { get; init; } = string.Empty;
    
    public static string ProjectionExpression => "pk, sk, gsi1pk, gsi1sk";
    
    // FromDynamoDb method for deserialization
    // GetPartitionKey/GetSortKey return base table keys for entity lookup
}
```

**Usage Pattern:**

```csharp
// Query returns keys-only projection
var keys = await table.StatusIndex.Query(x => x.Gsi1Pk == "STATUS#pending").ToListAsync();

// Use keys to batch-get full entities
var orders = await DynamoDbBatch.Get
    .Add(keys.Select(k => table.Orders.Get(k.Pk, k.Sk)))
    .ExecuteAsync();
```

**Keys Only Projection Contents:**

| Index Type | Included Keys |
|------------|---------------|
| GSI | GSI partition key, GSI sort key (if any), base table partition key, base table sort key |
| LSI | Base table partition key, LSI sort key, base table sort key (if different) |

### Querying with Projections

When using KEYS_ONLY or INCLUDE projections, only projected attributes are returned:

```csharp
// GSI configured with KEYS_ONLY projection
// Only returns: pk, status, createdAt
var orders = await table.StatusIndex.Query<Order>()
    .Where(x => x.Status == "pending")
    .ToListAsync();

// order.OrderId, order.Status, order.CreatedAt are populated
// order.CustomerId, order.Total may be null/default
```

### Fetching Full Items

To get full items when using sparse projections:

```csharp
// Step 1: Query GSI for keys using index accessor
var orders = await table.StatusIndex.Query<Order>()
    .Where(x => x.Status == "pending")
    .ToListAsync();

// Step 2: Batch get full items using static entry point
var batch = DynamoDbBatch.Get;
foreach (var order in orders)
{
    batch.Add(table.Orders.Get(order.OrderId));
}
var fullItems = await batch.ExecuteAsync();
```

**Trade-off:** Two operations vs. larger GSI storage and throughput costs.

### Projection Best Practices

**✅ Use KEYS_ONLY when:**
- You only need to identify items
- You'll fetch full items in a second operation
- Minimizing GSI storage costs is important

**✅ Use INCLUDE when:**
- You need specific attributes for filtering/display
- You want to avoid a second query
- The included attributes are relatively small

**✅ Use ALL when:**
- You need all attributes in query results
- Storage cost is not a concern
- You want simplest query logic

```csharp
// Example: INCLUDE projection with commonly needed fields
// GSI projects: pk, status, createdAt, customerId, total
// Omits: large description field, metadata

var orders = await table.StatusIndex.Query<Order>()
    .Where(x => x.Status == "pending")
    .ToListAsync();

// All projected fields are available
// No need for second query in most cases
```

## GSI Design Patterns

### Pattern 1: Status-Based Queries

Query items by status with time-based sorting:

```csharp
[DynamoDbTable("tasks")]
public partial class Task
{
    [PartitionKey]
    [DynamoDbAttribute("pk")]
    public string TaskId { get; set; } = string.Empty;
    
    [GsiPartitionKey("StatusIndex")]
    [DynamoDbAttribute("status")]
    public string Status { get; set; } = "pending";
    
    [GsiSortKey("StatusIndex")]
    [DynamoDbAttribute("dueDate")]
    public DateTime DueDate { get; set; }
    
    [DynamoDbAttribute("title")]
    public string Title { get; set; } = string.Empty;
}
```

**Access Pattern:** Get all pending tasks due in the next week

```csharp
var nextWeek = DateTime.UtcNow.AddDays(7);

var tasks = await table.StatusIndex.Query<Task>()
    .Where(x => x.Status == "pending" && x.DueDate < nextWeek)
    .ToListAsync();
```

### Pattern 2: Multi-Tenant Queries

Query items for a specific tenant:

```csharp
[DynamoDbTable("documents")]
public partial class Document
{
    [PartitionKey]
    [DynamoDbAttribute("pk")]
    public string DocumentId { get; set; } = string.Empty;
    
    [GsiPartitionKey("TenantIndex")]
    [Computed(nameof(TenantId), Format = "TENANT#{0}")]
    [DynamoDbAttribute("gsi1pk")]
    public string TenantId { get; set; } = string.Empty;
    
    [GsiSortKey("TenantIndex")]
    [DynamoDbAttribute("createdAt")]
    public DateTime CreatedAt { get; set; }
    
    [DynamoDbAttribute("title")]
    public string Title { get; set; } = string.Empty;
}
```

**Access Pattern:** Get all documents for a tenant, newest first

```csharp
var documents = await table.TenantIndex.Query<Document>()
    .Where(x => x.TenantId == DocumentKeys.TenantIndex.Pk("tenant123"))
    .ScanIndexForward(false)  // Descending order
    .ToListAsync();
```

### Pattern 3: Sparse Indexes

Create GSIs that only index items with specific attributes:

```csharp
[DynamoDbTable("users")]
public partial class User
{
    [PartitionKey]
    [DynamoDbAttribute("pk")]
    public string UserId { get; set; } = string.Empty;
    
    [DynamoDbAttribute("email")]
    public string Email { get; set; } = string.Empty;
    
    // Only users with premium status are indexed
    [GsiPartitionKey("PremiumIndex")]
    [DynamoDbAttribute("premiumStatus")]
    public string? PremiumStatus { get; set; }  // null for non-premium users
    
    [GsiSortKey("PremiumIndex")]
    [DynamoDbAttribute("premiumSince")]
    public DateTime? PremiumSince { get; set; }
}
```

**Access Pattern:** Get all premium users

```csharp
// Only items with premiumStatus != null are in the index
var premiumUsers = await table.PremiumIndex.Query<User>()
    .Where(x => x.PremiumStatus == "active")
    .ToListAsync();
```

**Benefits:**
- Reduced GSI storage costs (only premium users indexed)
- Faster queries (smaller index)
- Automatic filtering (non-premium users excluded)


### Pattern 4: Inverted Index

Create an inverted index for reverse lookups:

```csharp
[DynamoDbTable("relationships")]
public partial class Relationship
{
    // Main table: User -> Followers
    [PartitionKey]
    [Computed(nameof(UserId), Format = "USER#{0}")]
    [DynamoDbAttribute("pk")]
    public string UserId { get; set; } = string.Empty;
    
    [SortKey]
    [Computed(nameof(FollowerId), Format = "FOLLOWER#{0}")]
    [DynamoDbAttribute("sk")]
    public string FollowerId { get; set; } = string.Empty;
    
    // GSI: Inverted index for Follower -> Following
    [GsiPartitionKey("InvertedIndex")]
    [Computed(nameof(FollowerId), Format = "USER#{0}")]
    [DynamoDbAttribute("gsi1pk")]
    public string InvertedPk { get; set; } = string.Empty;
    
    [GsiSortKey("InvertedIndex")]
    [Computed(nameof(UserId), Format = "FOLLOWING#{0}")]
    [DynamoDbAttribute("gsi1sk")]
    public string InvertedSk { get; set; } = string.Empty;
    
    [DynamoDbAttribute("createdAt")]
    public DateTime CreatedAt { get; set; }
}
```

**Access Patterns:**

```csharp
// Pattern 1: Get all followers of a user (main table)
var followers = await table.Query
    .Where(x => x.UserId == RelationshipKeys.Pk("user123"))
    .ToListAsync();

// Pattern 2: Get all users that a user is following (GSI) using index accessor
var following = await table.InvertedIndex.Query<Relationship>()
    .Where(x => x.InvertedPk == RelationshipKeys.InvertedIndex.Pk("user123"))
    .ToListAsync();
```

### Pattern 5: Composite GSI Keys for Filtering

Use composite GSI keys to enable efficient filtering:

```csharp
[DynamoDbTable("products")]
public partial class Product
{
    [PartitionKey]
    [DynamoDbAttribute("pk")]
    public string ProductId { get; set; } = string.Empty;
    
    public string Category { get; set; } = string.Empty;
    public string Status { get; set; } = "active";
    public decimal Price { get; set; }
    
    // Composite GSI key: "CATEGORY#electronics#STATUS#active"
    [GsiPartitionKey("CategoryStatusIndex")]
    [Computed(nameof(Category), nameof(Status), Format = "CATEGORY#{0}#STATUS#{1}")]
    [DynamoDbAttribute("gsi1pk")]
    public string CategoryStatusKey { get; set; } = string.Empty;
    
    [GsiSortKey("CategoryStatusIndex")]
    [DynamoDbAttribute("price")]
    public decimal PriceKey { get; set; }
}
```

**Access Pattern:** Get active products in a category, sorted by price

```csharp
var products = await table.CategoryStatusIndex.Query<Product>()
    .Where(x => x.CategoryStatusKey == ProductKeys.CategoryStatusIndex.Pk("electronics", "active"))
    .ToListAsync();

// Results are automatically sorted by price (GSI sort key)
```

## Performance and Cost Considerations

### Read Capacity

GSI queries consume read capacity from the GSI, not the main table:

```csharp
// Consumes RCUs from StatusIndex
var orders = await table.StatusIndex.Query<Order>()
    .Where(x => x.Status == "pending")
    .ToListAsync();
```

**Capacity Calculation:**
- Eventually consistent: 1 RCU per 8KB
- Strongly consistent: Not supported on GSIs
- Query returns 40KB: 5 RCUs (40KB / 8KB, rounded up)

### Write Capacity

Every write to the main table that affects GSI keys consumes write capacity on both:

```csharp
// Consumes WCUs on:
// 1. Main table
// 2. StatusIndex (status or createdAt changed)
// 3. VendorIndex (vendorId or createdAt changed)
await table.Put
    .WithItem(product)
    .ExecuteAsync();
```

**Best Practice:** Minimize GSI updates by:
- Using sparse indexes (null values not indexed)
- Avoiding frequently updated attributes as GSI keys
- Batching updates when possible

### Storage Costs

GSIs consume additional storage:

```csharp
// Main table item: 10KB
// GSI with ALL projection: Additional 10KB
// GSI with KEYS_ONLY: Additional ~1KB
// Total storage: 10KB + 10KB + 1KB = 21KB
```

**Optimization:**
- Use KEYS_ONLY or INCLUDE projections
- Use sparse indexes to reduce item count
- Remove unnecessary GSIs

### Query Performance

**✅ Efficient GSI Queries:**
```csharp
// Good: Specific partition key using index accessor
var orders = await table.StatusIndex.Query<Order>()
    .Where(x => x.Status == "pending")
    .ToListAsync();

// Good: Partition key + sort key range
var recentOrders = await table.StatusIndex.Query<Order>()
    .Where(x => x.Status == "pending" && x.CreatedAt > sevenDaysAgo)
    .ToListAsync();
```

**❌ Inefficient GSI Queries:**
```csharp
// Bad: Scan entire GSI (no partition key)
// Note: Requires [Scannable] attribute on table class
var response = await table.StatusIndex.Scan<Order>()
    .WithFilter(x => x.Total > 100.00m)
    .ToListAsync();

// Bad: Filter expression does heavy lifting
var orders = await table.StatusIndex.Query<Order>()
    .Where(x => x.Status == "pending")
    .WithFilter(x => x.CustomerId == "customer123" && x.Total > 100.00m)
    .ToListAsync();
// Better: Create a GSI with CustomerId as partition key
```

## Best Practices

### 1. Design GSIs for Access Patterns

```csharp
// ✅ Good - GSI matches query pattern
// Access pattern: "Get all pending orders for a customer"
[GsiPartitionKey("CustomerStatusIndex")]
[Computed(nameof(CustomerId), nameof(Status), Format = "{0}#{1}")]
[DynamoDbAttribute("gsi1pk")]
public string CustomerStatusKey { get; set; } = string.Empty;

// Query efficiently using index accessor
var orders = await table.CustomerStatusIndex.Query<Order>()
    .Where(x => x.CustomerStatusKey == OrderKeys.CustomerStatusIndex.Pk("customer123", "pending"))
    .ToListAsync();
```

### 2. Use Sparse Indexes

```csharp
// ✅ Good - only index items that need it
[GsiPartitionKey("ErrorIndex")]
[DynamoDbAttribute("errorCode")]
public string? ErrorCode { get; set; }  // null for successful items

// Only failed items are indexed
// Reduces storage and improves query performance
```

### 3. Choose Appropriate Projections

```csharp
// ✅ Good - KEYS_ONLY for lookup, then batch get using index accessor
var orders = await table.StatusIndex.Query<Order>()
    .Where(x => x.Status == "pending")
    .ToListAsync();

// Batch get full items
var fullOrders = await BatchGetFullItems(orders.Select(o => o.OrderId));

// ✅ Good - INCLUDE for common fields
// GSI includes: pk, status, createdAt, customerId, total
// Omits: large description, metadata
```

### 4. Avoid Hot Partitions

```csharp
// ❌ Avoid - all items have same GSI partition key
[GsiPartitionKey("TypeIndex")]
[DynamoDbAttribute("type")]
public string Type { get; set; } = "ORDER";  // Same for all orders

// ✅ Better - distribute across multiple partitions
[GsiPartitionKey("StatusDateIndex")]
[Computed(nameof(Status), nameof(CreatedDate), Format = "{0}#{1:yyyy-MM-dd}")]
[DynamoDbAttribute("gsi1pk")]
public string StatusDateKey { get; set; } = string.Empty;
// Distributes items across dates
```

### 5. Monitor GSI Performance

```csharp
// Monitor consumed capacity using index accessor
var response = await table.StatusIndex.Query<Order>()
    .Where(x => x.Status == "pending")
    .WithReturnConsumedCapacity(ReturnConsumedCapacity.TOTAL)
    .ToResponseAsync();

Console.WriteLine($"Consumed capacity: {response.ConsumedCapacity?.CapacityUnits} RCUs");
```

## Next Steps

- **[Composite Entities](CompositeEntities.md)** - Combine GSIs with multi-item entities
- **[Performance Optimization](PerformanceOptimization.md)** - Optimize GSI queries
- **[Querying Data](../core-features/QueryingData.md)** - Advanced query patterns
- **[Entity Definition](../core-features/EntityDefinition.md)** - GSI attribute configuration

---

[Previous: Composite Entities](CompositeEntities.md) | [Next: STS Integration](STSIntegration.md)

**See Also:**
- [Expression Formatting](../core-features/ExpressionFormatting.md)
- [Attribute Reference](../reference/AttributeReference.md)
- [Troubleshooting](../reference/Troubleshooting.md)
