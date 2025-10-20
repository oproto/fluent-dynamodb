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

// Query all items for a user
var response = await table.Query
    .Where($"{UserFields.UserId} = {{0}}", UserKeys.Pk("user123"))
    .ExecuteAsync();

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

// Query orders for a customer with sort key condition
var response = await table.Query
    .Where($"{OrderFields.CustomerId} = {{0}} AND {OrderFields.OrderId} > {{1}}", 
           OrderKeys.Pk("customer123"),
           OrderKeys.Sk("ORDER#2024-01-01"))
    .ExecuteAsync();
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
// Query all orders (assuming sort key format: "ORDER#...")
.Where($"{OrderFields.CustomerId} = {{0}} AND begins_with({OrderFields.OrderId}, {{1}})", 
       OrderKeys.Pk("customer123"),
       "ORDER#")

// Query orders from a specific month
.Where($"{OrderFields.CustomerId} = {{0}} AND begins_with({OrderFields.OrderId}, {{1}})", 
       OrderKeys.Pk("customer123"),
       "ORDER#2024-03")
```

**Use Case:** `begins_with` is perfect for hierarchical data or time-based queries with formatted sort keys.

## Filter Expressions

Filter expressions apply additional filtering after items are retrieved by the key condition. They reduce data transfer but don't reduce consumed capacity.

### Basic Filters

```csharp
// Query orders with status filter
var response = await table.Query
    .Where($"{OrderFields.CustomerId} = {{0}}", OrderKeys.Pk("customer123"))
    .WithFilter($"{OrderFields.Status} = {{0}}", "pending")
    .ExecuteAsync();
```

### Multiple Filter Conditions

```csharp
// Combine multiple conditions with AND
var response = await table.Query
    .Where($"{OrderFields.CustomerId} = {{0}}", OrderKeys.Pk("customer123"))
    .WithFilter($"{OrderFields.Status} = {{0}} AND {OrderFields.Total} > {{1}}", 
                "pending", 
                100.00m)
    .ExecuteAsync();

// Combine conditions with OR
var response = await table.Query
    .Where($"{OrderFields.CustomerId} = {{0}}", OrderKeys.Pk("customer123"))
    .WithFilter($"{OrderFields.Status} = {{0}} OR {OrderFields.Status} = {{1}}", 
                "pending", 
                "processing")
    .ExecuteAsync();
```

### Filter Functions

DynamoDB provides several functions for filter expressions:

#### attribute_exists / attribute_not_exists

```csharp
// Only return items with a discount attribute
.WithFilter($"attribute_exists({OrderFields.Discount})")

// Only return items without a cancellation reason
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
// Filter items where tags contain a specific value
.WithFilter($"contains({OrderFields.Tags}, {{0}})", "priority")

// Filter items where description contains text
.WithFilter($"contains({ProductFields.Description}, {{0}})", "premium")
```

#### size

```csharp
// Filter items where list has specific size
.WithFilter($"size({OrderFields.Items}) > {{0}}", 5)

// Filter items where string length is within range
.WithFilter($"size({UserFields.Name}) BETWEEN {{0}} AND {{1}}", 3, 50)
```

### Filter Expression Best Practices

```csharp
// ✅ Good - use key condition for primary filtering
.Where($"{OrderFields.CustomerId} = {{0}} AND {OrderFields.OrderId} > {{1}}", 
       OrderKeys.Pk("customer123"),
       OrderKeys.Sk("ORDER#2024-01-01"))
.WithFilter($"{OrderFields.Status} = {{0}}", "pending")

// ❌ Avoid - using filter for what should be a key condition
.Where($"{OrderFields.CustomerId} = {{0}}", OrderKeys.Pk("customer123"))
.WithFilter($"{OrderFields.OrderId} > {{0}}", "ORDER#2024-01-01")  // Should be in key condition
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
    
    var response = await query.ExecuteAsync();
    
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
    .ExecuteAsync();

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
    
    var response = await query.ExecuteAsync();
    
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
    .ExecuteAsync();
```

### Descending Order

```csharp
// Returns items in descending order by sort key
// Useful for getting most recent items first
var response = await table.Query
    .Where($"{OrderFields.CustomerId} = {{0}}", OrderKeys.Pk("customer123"))
    .OrderDescending()
    .ExecuteAsync();
```

**Use Case:** When your sort key represents timestamps, use `OrderDescending()` to get the most recent items first.

### Limit with Ordering

```csharp
// Get the 10 most recent orders
var response = await table.Query
    .Where($"{OrderFields.CustomerId} = {{0}}", OrderKeys.Pk("customer123"))
    .OrderDescending()
    .Take(10)
    .ExecuteAsync();
```

## Projection Expressions

Retrieve only specific attributes to reduce data transfer and improve performance.

### Basic Projection

```csharp
// Get only specific fields
var response = await table.Query
    .Where($"{OrderFields.CustomerId} = {{0}}", OrderKeys.Pk("customer123"))
    .WithProjection($"{OrderFields.OrderId}, {OrderFields.Total}, {OrderFields.Status}")
    .ExecuteAsync();

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
    .ExecuteAsync();
```

### Query GSI with Sort Key Condition

```csharp
// Query pending orders created after a specific date
var response = await table.Query
    .UsingIndex(OrderIndexes.StatusIndex)
    .Where($"{OrderFields.Status} = {{0}} AND {OrderFields.CreatedAt} > {{1:o}}", 
           "pending",
           new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc))
    .ExecuteAsync();
```

### GSI Query Limitations

```csharp
// ❌ Cannot use consistent reads on GSI
var response = await table.Query
    .UsingIndex(OrderIndexes.StatusIndex)
    .Where($"{OrderFields.Status} = {{0}}", "pending")
    .UsingConsistentRead()  // This will throw an exception!
    .ExecuteAsync();

// ✅ GSI queries are always eventually consistent
var response = await table.Query
    .UsingIndex(OrderIndexes.StatusIndex)
    .Where($"{OrderFields.Status} = {{0}}", "pending")
    .ExecuteAsync();
```

**Important:** GSIs only support eventually consistent reads. Strongly consistent reads will throw a `ValidationException`.

### GSI Projection Considerations

If your GSI uses a projection (not ALL), only projected attributes are available:

```csharp
// If GSI projects only: status, createdAt, orderId, total
var response = await table.Query
    .UsingIndex(OrderIndexes.StatusIndex)
    .Where($"{OrderFields.Status} = {{0}}", "pending")
    .ExecuteAsync();

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
    .ExecuteAsync();

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
    .ExecuteAsync();
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
    
    var response = await scan.ExecuteAsync();
    
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
        
        var response = await scan.ExecuteAsync();
        
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
    .ExecuteAsync();

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

// ❌ Avoid - retrieves all attributes
.ExecuteAsync()
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
    var response = await query.StartAt(lastKey).ExecuteAsync();
    // Process page
    lastKey = response.LastEvaluatedKey;
} while (lastKey != null);

// ❌ Avoid - may hit 1MB limit
var response = await query.ExecuteAsync();
```

### 6. Use Consistent Reads Sparingly

```csharp
// ✅ Good - eventually consistent (default)
.ExecuteAsync()

// ⚠️ Use only when necessary - 2x cost
.UsingConsistentRead().ExecuteAsync()
```

### 7. Monitor Consumed Capacity

```csharp
var response = await table.Query
    .Where($"{OrderFields.CustomerId} = {{0}}", OrderKeys.Pk("customer123"))
    .ReturnTotalConsumedCapacity()
    .ExecuteAsync();

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
    var response = await query.ExecuteAsync();
    
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

For complex dynamic scenarios, you can use manual parameter binding:

```csharp
// Manual parameter approach
await table.Query
    .Where($"{OrderFields.CustomerId} = :pk AND {OrderFields.OrderDate} > :date")
    .WithValue(":pk", OrderKeys.Pk("customer123"))
    .WithValue(":date", startDate)
    .WithFilter($"{OrderFields.Status} = :status")
    .WithValue(":status", "pending")
    .ExecuteAsync();
```

See [Manual Patterns](../advanced-topics/ManualPatterns.md) for more details on lower-level approaches.

## Next Steps

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
