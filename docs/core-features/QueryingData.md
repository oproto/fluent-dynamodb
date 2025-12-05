---
title: "Querying Data"
category: "core-features"
order: 3
keywords: ["query", "scan", "filter", "pagination", "GSI", "key condition", "expression formatting"]
related: ["BasicOperations.md", "ExpressionFormatting.md", "EntityDefinition.md", "../advanced-topics/GlobalSecondaryIndexes.md"]
---

[Documentation](../README.md) > [Core Features](README.md) > Querying Data

# Querying Data

[Previous: Basic Operations](BasicOperations.md) | [Next: Expression Formatting](ExpressionFormatting.md)

---

This guide covers querying and scanning data in DynamoDB using Oproto.FluentDynamoDb with expression formatting. Query operations are the most efficient way to retrieve multiple items when you know the partition key.

> **Table Operation Patterns**: Examples in this guide use a manual table class (inheriting from `DynamoDbTableBase`) for clarity. For source-generated tables:
> - **Single-entity tables**: Use table-level operations like `usersTable.Query()`, `usersTable.Get()`, etc.
> - **Multi-entity tables**: Use entity accessor operations like `ordersTable.Orders.Query()`, `ordersTable.OrderLines.Get()`, etc.
> - See [Single-Entity Tables](../getting-started/SingleEntityTables.md) and [Multi-Entity Tables](../advanced-topics/MultiEntityTables.md) for details.

## Query vs Scan

**Query Operations:**
- ✅ Efficient - only reads items with matching partition key
- ✅ Fast - uses table/index structure
- ✅ Cost-effective - consumes capacity only for matching items
- ✅ Preferred for most use cases

**Scan Operations:**
- ⚠️ Expensive - reads every item in the table
- ⚠️ Slow - examines all items sequentially
- ⚠️ High cost - consumes capacity for all items examined
- ⚠️ Use only when necessary (analytics, migrations)

**Rule of Thumb:** Always use Query when you know the partition key. Only use Scan when you truly need to examine every item.

## Three API Styles

FluentDynamoDb supports three approaches for writing queries. Choose based on your needs:

### 1. Lambda Expressions (PREFERRED)

Use C# lambda expressions for compile-time type safety and IntelliSense support:

```csharp
// PREFERRED: Type-safe with lambda expressions
await table.Query
    .Where<User>(x => x.UserId == userId && x.SortKey.StartsWith("ORDER#"))
    .WithFilter<User>(x => x.Status == "ACTIVE" && x.Age >= 18)
    .ToListAsync();
```

**Advantages:**
- ✓ Compile-time type checking
- ✓ IntelliSense support
- ✓ Refactoring safety
- ✓ Automatic parameter generation

**See:** [LINQ Expressions Guide](LinqExpressions.md) for complete documentation.

### 2. Format Strings (ALTERNATIVE)

Use String.Format-style syntax for concise queries:

```csharp
// ALTERNATIVE: Format string - concise with placeholders
await table.Query
    .Where($"{UserFields.UserId} = {{0}} AND begins_with({UserFields.SortKey}, {{1}})", 
           UserKeys.Pk(userId), "ORDER#")
    .WithFilter($"{UserFields.Status} = {{0}} AND {UserFields.Age} >= {{1}}", 
                "ACTIVE", 18)
    .ToListAsync();
```

**Advantages:**
- ✓ Concise syntax
- ✓ Automatic parameter generation
- ✓ Supports all DynamoDB features

**See:** [Expression Formatting Guide](ExpressionFormatting.md) for complete documentation.

### 3. Manual WithValue (EXPLICIT CONTROL)

Use explicit parameter binding for maximum control:

```csharp
// EXPLICIT CONTROL: Manual - for complex scenarios
await table.Query
    .Where("#pk = :pk AND begins_with(#sk, :prefix)")
    .WithAttribute("#pk", "pk")
    .WithAttribute("#sk", "sk")
    .WithValue(":pk", UserKeys.Pk(userId))
    .WithValue(":prefix", "ORDER#")
    .WithFilter("#status = :status AND #age >= :age")
    .WithAttribute("#status", "status")
    .WithAttribute("#age", "age")
    .WithValue(":status", "ACTIVE")
    .WithValue(":age", 18)
    .ToListAsync();
```

**Advantages:**
- ✓ Maximum control
- ✓ Explicit parameter management
- ✓ Good for dynamic queries

**When to Use Each Approach:**
- **Lambda expressions:** New code, type safety important, known properties
- **Format strings:** Balance of conciseness and flexibility
- **Manual parameters:** Dynamic queries, complex scenarios, existing code

See [Manual Patterns](../advanced-topics/ManualPatterns.md) for more details on the manual approach.

## Basic Query Operations

### Query by Partition Key

The simplest query retrieves all items with a specific partition key:

```csharp
[DynamoDbTable("users")]
public partial class User
{
    [PartitionKey]
    [DynamoDbAttribute("pk")]
    public string UserId { get; set; } = string.Empty;
    
    [DynamoDbAttribute("name")]
    public string Name { get; set; } = string.Empty;
}

// 1. PREFERRED: Lambda expression - type-safe with IntelliSense
var response = await table.Query
    .Where<User>(x => x.UserId == "user123")
    .ToListAsync();

// 2. ALTERNATIVE: Format string - concise with placeholders
var response = await table.Query
    .Where($"{UserFields.UserId} = {{0}}", UserKeys.Pk("user123"))
    .ToListAsync();

// 3. EXPLICIT CONTROL: Manual - for complex scenarios
var response = await table.Query
    .Where("#pk = :pk")
    .WithAttribute("#pk", "pk")
    .WithValue(":pk", UserKeys.Pk("user123"))
    .ToListAsync();

// Process results
foreach (var item in response.Items)
{
    var user = UserMapper.FromAttributeMap(item);
    Console.WriteLine($"User: {user.Name}");
}
```

### Query with Sort Key Condition

Use sort key conditions to filter items within a partition:

```csharp
[DynamoDbTable("orders")]
public partial class Order
{
    [PartitionKey]
    [DynamoDbAttribute("pk")]
    public string CustomerId { get; set; } = string.Empty;
    
    [SortKey]
    [DynamoDbAttribute("sk")]
    public string OrderId { get; set; } = string.Empty;
    
    [DynamoDbAttribute("status")]
    public string Status { get; set; } = string.Empty;
}

// 1. PREFERRED: Lambda expression - type-safe with IntelliSense
var response = await table.Query
    .Where<Order>(x => x.CustomerId == customerId && x.OrderId > "ORDER#2024-01-01")
    .ToListAsync();

// 2. ALTERNATIVE: Format string - concise with placeholders
var response = await table.Query
    .Where($"{OrderFields.CustomerId} = {{0}} AND {OrderFields.OrderId} > {{1}}", 
           OrderKeys.Pk("customer123"),
           OrderKeys.Sk("ORDER#2024-01-01"))
    .ToListAsync();

// 3. EXPLICIT CONTROL: Manual - for complex scenarios
var response = await table.Query
    .Where("#pk = :pk AND #sk > :sk")
    .WithAttribute("#pk", "pk")
    .WithAttribute("#sk", "sk")
    .WithValue(":pk", OrderKeys.Pk("customer123"))
    .WithValue(":sk", OrderKeys.Sk("ORDER#2024-01-01"))
    .ToListAsync();
```

## Key Condition Expressions

Key condition expressions define which items to retrieve based on partition and sort keys.

### Partition Key (Required)

Every query must specify an equality condition on the partition key:

```csharp
// ✅ Valid - partition key equality
.Where($"{UserFields.UserId} = {{0}}", UserKeys.Pk("user123"))

// ❌ Invalid - partition key must use equality
.Where($"{UserFields.UserId} > {{0}}", UserKeys.Pk("user123"))
```

### Sort Key Operators

Sort keys support various comparison operators:

#### Equality

```csharp
// Exact match on sort key
.Where($"{OrderFields.CustomerId} = {{0}} AND {OrderFields.OrderId} = {{1}}", 
       OrderKeys.Pk("customer123"),
       OrderKeys.Sk("ORDER#001"))
```

#### Comparison Operators

```csharp
// Greater than
.Where($"{OrderFields.CustomerId} = {{0}} AND {OrderFields.OrderId} > {{1}}", 
       OrderKeys.Pk("customer123"),
       OrderKeys.Sk("ORDER#2024-01-01"))

// Less than
.Where($"{OrderFields.CustomerId} = {{0}} AND {OrderFields.OrderId} < {{1}}", 
       OrderKeys.Pk("customer123"),
       OrderKeys.Sk("ORDER#2024-12-31"))

// Greater than or equal
.Where($"{OrderFields.CustomerId} = {{0}} AND {OrderFields.OrderId} >= {{1}}", 
       OrderKeys.Pk("customer123"),
       OrderKeys.Sk("ORDER#2024-01-01"))

// Less than or equal
.Where($"{OrderFields.CustomerId} = {{0}} AND {OrderFields.OrderId} <= {{1}}", 
       OrderKeys.Pk("customer123"),
       OrderKeys.Sk("ORDER#2024-12-31"))
```

#### Between

```csharp
// Query orders within a date range
.Where($"{OrderFields.CustomerId} = {{0}} AND {OrderFields.OrderId} BETWEEN {{1}} AND {{2}}", 
       OrderKeys.Pk("customer123"),
       OrderKeys.Sk("ORDER#2024-01-01"),
       OrderKeys.Sk("ORDER#2024-12-31"))
```

#### Begins With

```csharp
// Expression-based (type-safe)
.Where<Order>(x => x.CustomerId == customerId && x.OrderId.StartsWith("ORDER#"))

// Format string approach
.Where($"{OrderFields.CustomerId} = {{0}} AND begins_with({OrderFields.OrderId}, {{1}})", 
       OrderKeys.Pk("customer123"),
       "ORDER#")

// Query orders from a specific month (expression-based)
.Where<Order>(x => x.CustomerId == customerId && x.OrderId.StartsWith("ORDER#2024-03"))

// Query orders from a specific month (format string)
.Where($"{OrderFields.CustomerId} = {{0}} AND begins_with({OrderFields.OrderId}, {{1}})", 
       OrderKeys.Pk("customer123"),
       "ORDER#2024-03")
```

**Use Case:** `begins_with` is perfect for hierarchical data or time-based queries with formatted sort keys.

## Filter Expressions

Filter expressions apply additional filtering after items are retrieved by the key condition. They reduce data transfer but don't reduce consumed capacity.

### Basic Filters

```csharp
// 1. PREFERRED: Lambda expression - type-safe with IntelliSense
var response = await table.Query
    .Where<Order>(x => x.CustomerId == customerId)
    .WithFilter<Order>(x => x.Status == "pending")
    .ToListAsync();

// 2. ALTERNATIVE: Format string - concise with placeholders
var response = await table.Query
    .Where($"{OrderFields.CustomerId} = {{0}}", OrderKeys.Pk("customer123"))
    .WithFilter($"{OrderFields.Status} = {{0}}", "pending")
    .ToListAsync();

// 3. EXPLICIT CONTROL: Manual - for complex scenarios
var response = await table.Query
    .Where("#pk = :pk")
    .WithAttribute("#pk", "pk")
    .WithValue(":pk", OrderKeys.Pk("customer123"))
    .WithFilter("#status = :status")
    .WithAttribute("#status", "status")
    .WithValue(":status", "pending")
    .ToListAsync();
```

### Multiple Filter Conditions

**AND Conditions (all three styles):**

```csharp
// 1. PREFERRED: Lambda expression - type-safe with IntelliSense
var response = await table.Query
    .Where<Order>(x => x.CustomerId == customerId)
    .WithFilter<Order>(x => x.Status == "pending" && x.Total > 100.00m)
    .ToListAsync();

// 2. ALTERNATIVE: Format string - concise with placeholders
var response = await table.Query
    .Where($"{OrderFields.CustomerId} = {{0}}", OrderKeys.Pk("customer123"))
    .WithFilter($"{OrderFields.Status} = {{0}} AND {OrderFields.Total} > {{1}}", 
                "pending", 
                100.00m)
    .ToListAsync();

// 3. EXPLICIT CONTROL: Manual - for complex scenarios
var response = await table.Query
    .Where("#pk = :pk")
    .WithAttribute("#pk", "pk")
    .WithValue(":pk", OrderKeys.Pk("customer123"))
    .WithFilter("#status = :status AND #total > :total")
    .WithAttribute("#status", "status")
    .WithAttribute("#total", "total")
    .WithValue(":status", "pending")
    .WithValue(":total", 100.00m)
    .ToListAsync();
```

**OR Conditions (all three styles):**

```csharp
// 1. PREFERRED: Lambda expression - type-safe with IntelliSense
var response = await table.Query
    .Where<Order>(x => x.CustomerId == customerId)
    .WithFilter<Order>(x => x.Status == "pending" || x.Status == "processing")
    .ToListAsync();

// 2. ALTERNATIVE: Format string - concise with placeholders
var response = await table.Query
    .Where($"{OrderFields.CustomerId} = {{0}}", OrderKeys.Pk("customer123"))
    .WithFilter($"{OrderFields.Status} = {{0}} OR {OrderFields.Status} = {{1}}", 
                "pending", 
                "processing")
    .ToListAsync();

// 3. EXPLICIT CONTROL: Manual - for complex scenarios
var response = await table.Query
    .Where("#pk = :pk")
    .WithAttribute("#pk", "pk")
    .WithValue(":pk", OrderKeys.Pk("customer123"))
    .WithFilter("#status = :status1 OR #status = :status2")
    .WithAttribute("#status", "status")
    .WithValue(":status1", "pending")
    .WithValue(":status2", "processing")
    .ToListAsync();
```

### Filter Functions

DynamoDB provides several functions for filter expressions:

#### attribute_exists / attribute_not_exists

```csharp
// Expression-based: Only return items with a discount attribute
.WithFilter<Order>(x => x.Discount.AttributeExists())

// Format string: Only return items with a discount attribute
.WithFilter($"attribute_exists({OrderFields.Discount})")

// Expression-based: Only return items without a cancellation reason
.WithFilter<Order>(x => x.CancellationReason.AttributeNotExists())

// Format string: Only return items without a cancellation reason
.WithFilter($"attribute_not_exists({OrderFields.CancellationReason})")
```

#### attribute_type

```csharp
// Filter by attribute type
.WithFilter($"attribute_type({OrderFields.Metadata}, {{0}})", "M")  // Map
.WithFilter($"attribute_type({OrderFields.Tags}, {{0}})", "L")      // List
.WithFilter($"attribute_type({OrderFields.Count}, {{0}})", "N")     // Number
```

#### begins_with

```csharp
// Filter items where email starts with specific domain
.WithFilter($"begins_with({UserFields.Email}, {{0}})", "admin@")
```

#### contains

```csharp
// Expression-based: Filter items where tags contain a specific value
.WithFilter<Order>(x => x.Tags.Contains("priority"))

// Format string: Filter items where tags contain a specific value
.WithFilter($"contains({OrderFields.Tags}, {{0}})", "priority")

// Expression-based: Filter items where description contains text
.WithFilter<Product>(x => x.Description.Contains("premium"))

// Format string: Filter items where description contains text
.WithFilter($"contains({ProductFields.Description}, {{0}})", "premium")
```

#### size

```csharp
// Expression-based: Filter items where list has specific size
.WithFilter<Order>(x => x.Items.Size() > 5)

// Format string: Filter items where list has specific size
.WithFilter($"size({OrderFields.Items}) > {{0}}", 5)

// Expression-based: Filter items where string length is within range
.WithFilter<User>(x => x.Name.Size().Between(3, 50))

// Format string: Filter items where string length is within range
.WithFilter($"size({UserFields.Name}) BETWEEN {{0}} AND {{1}}", 3, 50)
```

### Filter Expression Best Practices

```csharp
// ✅ Good - use key condition for primary filtering (expression-based)
.Where<Order>(x => x.CustomerId == customerId && x.OrderId > "ORDER#2024-01-01")
.WithFilter<Order>(x => x.Status == "pending")

// ✅ Good - use key condition for primary filtering (format string)
.Where($"{OrderFields.CustomerId} = {{0}} AND {OrderFields.OrderId} > {{1}}", 
       OrderKeys.Pk("customer123"),
       OrderKeys.Sk("ORDER#2024-01-01"))
.WithFilter($"{OrderFields.Status} = {{0}}", "pending")

// ❌ Avoid - using filter for what should be a key condition
.Where<Order>(x => x.CustomerId == customerId)
.WithFilter<Order>(x => x.OrderId > "ORDER#2024-01-01")  // Should be in key condition
```

**Important:** Filter expressions are applied after items are read, so you still consume read capacity for filtered-out items. Design your key schema to minimize filtering.

## Pagination

DynamoDB limits query responses to 1MB of data. Use pagination to retrieve all results.

### Basic Pagination

```csharp
var allOrders = new List<Order>();
Dictionary<string, AttributeValue>? lastKey = null;

do
{
    var query = table.Query
        .Where($"{OrderFields.CustomerId} = {{0}}", OrderKeys.Pk("customer123"));
    
    // Add pagination token if we have one
    if (lastKey != null)
    {
        query = query.StartAt(lastKey);
    }
    
    var response = await query.ToListAsync();
    
    // Process this page of results
    foreach (var item in response.Items)
    {
        allOrders.Add(OrderMapper.FromAttributeMap(item));
    }
    
    // Get the key for the next page
    lastKey = response.LastEvaluatedKey;
    
} while (lastKey != null && lastKey.Count > 0);

Console.WriteLine($"Retrieved {allOrders.Count} total orders");
```

### Pagination with Limit

Control how many items are evaluated per request:

```csharp
var response = await table.Query
    .Where($"{OrderFields.CustomerId} = {{0}}", OrderKeys.Pk("customer123"))
    .Take(25)  // Evaluate up to 25 items per request
    .ToListAsync();

// Check if there are more results
if (response.LastEvaluatedKey != null && response.LastEvaluatedKey.Count > 0)
{
    Console.WriteLine("More results available");
}
```

**Note:** `Take(n)` limits items *evaluated*, not items *returned*. If you have a filter expression, you may get fewer than n items back.

### Page-Based Pagination Pattern

```csharp
public async Task<(List<Order> Orders, string? NextPageToken)> GetOrdersPage(
    string customerId,
    string? pageToken = null,
    int pageSize = 25)
{
    var query = table.Query
        .Where($"{OrderFields.CustomerId} = {{0}}", OrderKeys.Pk(customerId))
        .Take(pageSize);
    
    // Decode and apply page token
    if (!string.IsNullOrEmpty(pageToken))
    {
        var lastKey = DecodePageToken(pageToken);
        query = query.StartAt(lastKey);
    }
    
    var response = await query.ToListAsync();
    
    var orders = response.Items
        .Select(OrderMapper.FromAttributeMap)
        .ToList();
    
    // Encode next page token
    string? nextToken = null;
    if (response.LastEvaluatedKey != null && response.LastEvaluatedKey.Count > 0)
    {
        nextToken = EncodePageToken(response.LastEvaluatedKey);
    }
    
    return (orders, nextToken);
}

private string EncodePageToken(Dictionary<string, AttributeValue> lastKey)
{
    var json = System.Text.Json.JsonSerializer.Serialize(lastKey);
    return Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(json));
}

private Dictionary<string, AttributeValue> DecodePageToken(string token)
{
    var json = System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(token));
    return System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, AttributeValue>>(json)!;
}
```

## Query Ordering

Control the sort order of query results using the sort key.

### Ascending Order (Default)

```csharp
// Returns items in ascending order by sort key
var response = await table.Query
    .Where($"{OrderFields.CustomerId} = {{0}}", OrderKeys.Pk("customer123"))
    .OrderAscending()  // Optional - this is the default
    .ToListAsync();
```

### Descending Order

```csharp
// Returns items in descending order by sort key
// Useful for getting most recent items first
var response = await table.Query
    .Where($"{OrderFields.CustomerId} = {{0}}", OrderKeys.Pk("customer123"))
    .OrderDescending()
    .ToListAsync();
```

**Use Case:** When your sort key represents timestamps, use `OrderDescending()` to get the most recent items first.

### Limit with Ordering

```csharp
// Get the 10 most recent orders
var response = await table.Query
    .Where($"{OrderFields.CustomerId} = {{0}}", OrderKeys.Pk("customer123"))
    .OrderDescending()
    .Take(10)
    .ToListAsync();
```

## Projection Expressions

Retrieve only specific attributes to reduce data transfer and improve performance.

### Basic Projection

```csharp
// Get only specific fields
var response = await table.Query
    .Where($"{OrderFields.CustomerId} = {{0}}", OrderKeys.Pk("customer123"))
    .WithProjection($"{OrderFields.OrderId}, {OrderFields.Total}, {OrderFields.Status}")
    .ToListAsync();

// Note: Other properties will have default values
foreach (var item in response.Items)
{
    var order = OrderMapper.FromAttributeMap(item);
    Console.WriteLine($"Order {order.OrderId}: ${order.Total} - {order.Status}");
    // order.Items will be empty/default
}
```

### Projection with Nested Attributes

```csharp
// Project nested attributes
.WithProjection($"{OrderFields.OrderId}, {OrderFields.Customer}.{CustomerFields.Name}, {OrderFields.Customer}.{CustomerFields.Email}")
```

### Projection Benefits

- **Reduced Network Transfer:** Only requested attributes are sent
- **Lower Costs:** Smaller response size means less data transfer
- **Improved Performance:** Faster response times for large items

**Trade-off:** You still consume the same read capacity (based on item size), but you save on network transfer.

## Global Secondary Index Queries

Query alternative access patterns using Global Secondary Indexes (GSIs).

### Query GSI by Partition Key

```csharp
[DynamoDbTable("orders")]
public partial class Order
{
    [PartitionKey]
    [DynamoDbAttribute("pk")]
    public string OrderId { get; set; } = string.Empty;
    
    // GSI partition key
    [GlobalSecondaryIndex("StatusIndex", IsPartitionKey = true)]
    [DynamoDbAttribute("status")]
    public string Status { get; set; } = string.Empty;
    
    // GSI sort key
    [GlobalSecondaryIndex("StatusIndex", IsSortKey = true)]
    [DynamoDbAttribute("createdAt")]
    public DateTime CreatedAt { get; set; }
}

// Query all pending orders
var response = await table.Query
    .UsingIndex(OrderIndexes.StatusIndex)
    .Where($"{OrderFields.Status} = {{0}}", "pending")
    .ToListAsync();
```

### Query GSI with Sort Key Condition

```csharp
// Query pending orders created after a specific date
var response = await table.Query
    .UsingIndex(OrderIndexes.StatusIndex)
    .Where($"{OrderFields.Status} = {{0}} AND {OrderFields.CreatedAt} > {{1:o}}", 
           "pending",
           new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc))
    .ToListAsync();
```

### GSI Query Limitations

```csharp
// ❌ Cannot use consistent reads on GSI
var response = await table.Query
    .UsingIndex(OrderIndexes.StatusIndex)
    .Where($"{OrderFields.Status} = {{0}}", "pending")
    .UsingConsistentRead()  // This will throw an exception!
    .ToListAsync();

// ✅ GSI queries are always eventually consistent
var response = await table.Query
    .UsingIndex(OrderIndexes.StatusIndex)
    .Where($"{OrderFields.Status} = {{0}}", "pending")
    .ToListAsync();
```

**Important:** GSIs only support eventually consistent reads. Strongly consistent reads will throw a `ValidationException`.

### GSI Projection Considerations

If your GSI uses a projection (not ALL), only projected attributes are available:

```csharp
// If GSI projects only: status, createdAt, orderId, total
var response = await table.Query
    .UsingIndex(OrderIndexes.StatusIndex)
    .Where($"{OrderFields.Status} = {{0}}", "pending")
    .ToListAsync();

foreach (var item in response.Items)
{
    var order = OrderMapper.FromAttributeMap(item);
    // order.Status, order.CreatedAt, order.OrderId, order.Total are available
    // order.Items, order.ShippingAddress, etc. will be default/empty
}
```

See [Global Secondary Indexes](../advanced-topics/GlobalSecondaryIndexes.md) for advanced GSI patterns.

## Scan Operations

**⚠️ Warning:** Scan operations are expensive and should be used sparingly.

### When to Use Scan

Use Scan only for:
- Data migration or ETL processes
- Analytics on small tables (< 1000 items)
- Admin operations that truly need all items
- One-time data cleanup tasks

### Basic Scan

```csharp
// Scan requires ScannableDynamoDbTable
var scannableTable = new ScannableDynamoDbTable(client, "users");

var response = await scannableTable.Scan
    .ToListAsync();

foreach (var item in response.Items)
{
    var user = UserMapper.FromAttributeMap(item);
    Console.WriteLine($"User: {user.Name}");
}
```

### Scan with Filter

```csharp
// Filter reduces data transfer but NOT consumed capacity
var response = await scannableTable.Scan
    .WithFilter($"{UserFields.Status} = {{0}}", "active")
    .ToListAsync();
```

**Important:** Filters don't reduce the cost of a scan. You still pay for reading every item in the table.

### Scan with Pagination

```csharp
var allUsers = new List<User>();
Dictionary<string, AttributeValue>? lastKey = null;

do
{
    var scan = scannableTable.Scan.Take(100);
    
    if (lastKey != null)
    {
        scan = scan.StartAt(lastKey);
    }
    
    var response = await scan.ToListAsync();
    
    foreach (var item in response.Items)
    {
        allUsers.Add(UserMapper.FromAttributeMap(item));
    }
    
    lastKey = response.LastEvaluatedKey;
    
} while (lastKey != null && lastKey.Count > 0);
```

### Parallel Scan

Improve scan throughput on large tables by scanning in parallel:

```csharp
public async Task<List<User>> ParallelScanAsync(int segments = 4)
{
    var tasks = new List<Task<List<User>>>();
    
    // Create a scan task for each segment
    for (int i = 0; i < segments; i++)
    {
        int segment = i;  // Capture for closure
        tasks.Add(ScanSegmentAsync(segment, segments));
    }
    
    // Wait for all segments to complete
    var results = await Task.WhenAll(tasks);
    
    // Combine results
    return results.SelectMany(r => r).ToList();
}

private async Task<List<User>> ScanSegmentAsync(int segment, int totalSegments)
{
    var users = new List<User>();
    Dictionary<string, AttributeValue>? lastKey = null;
    
    do
    {
        var scan = scannableTable.Scan
            .WithSegment(segment, totalSegments)
            .Take(100);
        
        if (lastKey != null)
        {
            scan = scan.StartAt(lastKey);
        }
        
        var response = await scan.ToListAsync();
        
        foreach (var item in response.Items)
        {
            users.Add(UserMapper.FromAttributeMap(item));
        }
        
        lastKey = response.LastEvaluatedKey;
        
    } while (lastKey != null && lastKey.Count > 0);
    
    return users;
}
```

**Parallel Scan Benefits:**
- Faster completion time for large tables
- Better throughput utilization
- Each segment can run independently

**Parallel Scan Considerations:**
- Consumes more read capacity simultaneously
- May trigger throttling if not careful
- Results are not ordered

### Scan Cost Example

```csharp
// Monitor consumed capacity
var response = await scannableTable.Scan
    .ReturnTotalConsumedCapacity()
    .ToListAsync();

Console.WriteLine($"Items returned: {response.Items.Count}");
Console.WriteLine($"Items scanned: {response.ScannedCount}");
Console.WriteLine($"Capacity consumed: {response.ConsumedCapacity?.CapacityUnits} RCUs");
```

**Example Output:**
```
Items returned: 50
Items scanned: 10000
Capacity consumed: 2500 RCUs
```

This shows why Scan is expensive: you pay for all 10,000 items scanned, even though only 50 matched your filter.

## Query Optimization Tips

### 1. Design Keys for Query Patterns

```csharp
// ✅ Good - can query by customer efficiently
[PartitionKey]
[Computed(nameof(CustomerId), Format = "CUSTOMER#{0}")]
[DynamoDbAttribute("pk")]
public string PartitionKey { get; set; } = string.Empty;

[SortKey]
[Computed(nameof(OrderDate), Format = "ORDER#{0:yyyy-MM-dd}#{1}")]
[DynamoDbAttribute("sk")]
public string SortKey { get; set; } = string.Empty;

// Can query: all orders for customer, orders in date range, etc.
```

### 2. Use Projection Expressions

```csharp
// ✅ Good - only retrieve needed attributes
.WithProjection($"{OrderFields.OrderId}, {OrderFields.Total}")

// ❌ Avoid - retrieves all attributes without projection
.ToListAsync()
```

### 3. Prefer Key Conditions Over Filters

```csharp
// ✅ Good - uses sort key condition
.Where($"{OrderFields.CustomerId} = {{0}} AND {OrderFields.OrderDate} > {{1:o}}", 
       OrderKeys.Pk("customer123"),
       startDate)

// ❌ Avoid - uses filter (less efficient)
.Where($"{OrderFields.CustomerId} = {{0}}", OrderKeys.Pk("customer123"))
.WithFilter($"{OrderFields.OrderDate} > {{0:o}}", startDate)
```

### 4. Use GSIs for Alternative Access Patterns

```csharp
// ✅ Good - GSI designed for this query
.UsingIndex(OrderIndexes.StatusIndex)
.Where($"{OrderFields.Status} = {{0}}", "pending")

// ❌ Avoid - scanning entire table
scannableTable.Scan.WithFilter($"{OrderFields.Status} = {{0}}", "pending")
```

### 5. Implement Pagination

```csharp
// ✅ Good - handles large result sets
do {
    var response = await query.StartAt(lastKey).ToListAsync();
    // Process page
    lastKey = response.LastEvaluatedKey;
} while (lastKey != null);

// ❌ Avoid - may hit 1MB limit
var response = await query.ToListAsync();
```

### 6. Use Consistent Reads Sparingly

```csharp
// ✅ Good - eventually consistent (default)
.ToListAsync()

// ⚠️ Use only when necessary - 2x cost
.UsingConsistentRead().ToListAsync()
```

### 7. Monitor Consumed Capacity

```csharp
var response = await table.Query
    .Where($"{OrderFields.CustomerId} = {{0}}", OrderKeys.Pk("customer123"))
    .ReturnTotalConsumedCapacity()
    .ToListAsync();

Console.WriteLine($"Consumed: {response.ConsumedCapacity?.CapacityUnits} RCUs");
```

## Complete Query Example

Here's a comprehensive example combining multiple techniques:

```csharp
public async Task<(List<Order> Orders, string? NextPageToken)> GetCustomerOrders(
    string customerId,
    DateTime? startDate = null,
    DateTime? endDate = null,
    string? status = null,
    string? pageToken = null,
    int pageSize = 25)
{
    // Build key condition
    var keyCondition = $"{OrderFields.CustomerId} = {{0}}";
    var parameters = new List<object> { OrderKeys.Pk(customerId) };
    
    // Add date range to key condition if provided
    if (startDate.HasValue && endDate.HasValue)
    {
        keyCondition += $" AND {OrderFields.OrderDate} BETWEEN {{1:o}} AND {{2:o}}";
        parameters.Add(startDate.Value);
        parameters.Add(endDate.Value);
    }
    else if (startDate.HasValue)
    {
        keyCondition += $" AND {OrderFields.OrderDate} >= {{1:o}}";
        parameters.Add(startDate.Value);
    }
    
    // Build query
    var query = table.Query
        .Where(keyCondition, parameters.ToArray())
        .OrderDescending()  // Most recent first
        .Take(pageSize)
        .WithProjection($"{OrderFields.OrderId}, {OrderFields.Total}, {OrderFields.Status}, {OrderFields.OrderDate}")
        .ReturnTotalConsumedCapacity();
    
    // Add status filter if provided
    if (!string.IsNullOrEmpty(status))
    {
        query = query.WithFilter($"{OrderFields.Status} = {{0}}", status);
    }
    
    // Add pagination token
    if (!string.IsNullOrEmpty(pageToken))
    {
        var lastKey = DecodePageToken(pageToken);
        query = query.StartAt(lastKey);
    }
    
    // Execute query
    var response = await query.ToListAsync();
    
    // Log capacity consumption
    Console.WriteLine($"Query consumed {response.ConsumedCapacity?.CapacityUnits} RCUs");
    
    // Map results
    var orders = response.Items
        .Select(OrderMapper.FromAttributeMap)
        .ToList();
    
    // Encode next page token
    string? nextToken = null;
    if (response.LastEvaluatedKey != null && response.LastEvaluatedKey.Count > 0)
    {
        nextToken = EncodePageToken(response.LastEvaluatedKey);
    }
    
    return (orders, nextToken);
}
```

## Manual Patterns

While **lambda expressions are preferred** and **format strings are a good alternative**, you can use manual parameter binding for complex or dynamic scenarios:

```csharp
// Manual parameter approach - use when you need explicit control
await table.Query
    .Where("#pk = :pk AND #date > :date")
    .WithAttribute("#pk", "pk")
    .WithAttribute("#date", "orderDate")
    .WithValue(":pk", OrderKeys.Pk("customer123"))
    .WithValue(":date", startDate)
    .WithFilter("#status = :status")
    .WithAttribute("#status", "status")
    .WithValue(":status", "pending")
    .ToListAsync();
```

> **Recommendation**: Use lambda expressions (preferred) or format strings (alternative) for most queries. Reserve manual patterns for dynamic queries, complex scenarios, or legacy code migration.

See [Manual Patterns](../advanced-topics/ManualPatterns.md) for more details on lower-level approaches.

## Next Steps

- **[LINQ Expressions](LinqExpressions.md)** - Type-safe lambda expressions
- **[Expression Formatting](ExpressionFormatting.md)** - Complete format specifier reference
- **[Batch Operations](BatchOperations.md)** - Batch get operations
- **[Global Secondary Indexes](../advanced-topics/GlobalSecondaryIndexes.md)** - Advanced GSI patterns
- **[Performance Optimization](../advanced-topics/PerformanceOptimization.md)** - Query optimization strategies

---

[Previous: Basic Operations](BasicOperations.md) | [Next: Expression Formatting](ExpressionFormatting.md)

**See Also:**
- [Entity Definition](EntityDefinition.md)
- [Composite Entities](../advanced-topics/CompositeEntities.md)
- [Error Handling](../reference/ErrorHandling.md)
- [Troubleshooting](../reference/Troubleshooting.md)
