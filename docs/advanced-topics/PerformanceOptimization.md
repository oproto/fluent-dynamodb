---
title: "Performance Optimization"
category: "advanced-topics"
order: 4
keywords: ["performance", "optimization", "capacity", "throughput", "latency", "cost", "efficiency"]
related: ["GlobalSecondaryIndexes.md", "CompositeEntities.md", "../core-features/QueryingData.md", "../core-features/BatchOperations.md"]
---

[Documentation](../README.md) > [Advanced Topics](README.md) > Performance Optimization

# Performance Optimization

[Previous: STS Integration](STSIntegration.md) | [Next: Manual Patterns](ManualPatterns.md)

---

This guide covers performance optimization strategies for Oproto.FluentDynamoDb applications, including source generator benefits, query optimization, and capacity management.

## Source Generator Performance Benefits

### Zero Runtime Reflection

The source generator eliminates reflection overhead by generating mapping code at compile time:

```csharp
// Traditional approach (reflection-based)
// Slow: Uses reflection to discover properties and convert values
var item = ReflectionMapper.ToAttributeMap(user);

// Source generator approach (compile-time)
// Fast: Direct property access, no reflection
var item = UserMapper.ToAttributeMap(user);
```

**Performance Impact:**
- 10-100x faster serialization/deserialization
- No runtime type discovery
- Predictable performance characteristics
- AOT-compatible (Native AOT support)


### Pre-Allocated Collections

Generated code pre-allocates collections with exact capacity:

```csharp
// Generated code (optimized)
public static Dictionary<string, AttributeValue> ToDynamoDb<TSelf>(TSelf entity)
{
    // Pre-allocated with exact capacity - no resizing needed
    var item = new Dictionary<string, AttributeValue>(10);
    
    item["pk"] = new AttributeValue { S = typedEntity.UserId };
    item["email"] = new AttributeValue { S = typedEntity.Email };
    // ... 8 more properties
    
    return item;
}

// Manual approach (slower)
var item = new Dictionary<string, AttributeValue>();  // Starts with capacity 0
item["pk"] = new AttributeValue { S = user.UserId };  // May resize
item["email"] = new AttributeValue { S = user.Email };  // May resize again
// ... multiple resizes as items are added
```

**Performance Impact:**
- Eliminates dictionary resizing
- Reduces memory allocations
- Improves throughput by 20-30%

### Aggressive Inlining

Generated methods use aggressive inlining for hot paths:

```csharp
[MethodImpl(MethodImplOptions.AggressiveInlining)]
public static Dictionary<string, AttributeValue> ToDynamoDb<TSelf>(TSelf entity)
{
    // Method body inlined at call site
    // Eliminates method call overhead
}
```

**Performance Impact:**
- Eliminates method call overhead
- Enables further JIT optimizations
- Improves CPU cache utilization

### Compile-Time Type Safety

Type errors caught at compile time, not runtime:

```csharp
// Compile-time error (caught during build)
await table.Get
    .WithKey(UserFields.UserId, 123)  // Error: Expected string, got int
    .ExecuteAsync<User>();

// Runtime error (discovered during execution)
await table.Get
    .WithKey("userId", 123)  // Compiles, fails at runtime
    .ExecuteAsync();
```

**Performance Impact:**
- No runtime type checking overhead
- Faster execution
- Fewer error handling paths

## Query Optimization

### Use Specific Partition Keys

**✅ Efficient: Specific partition key**
```csharp
// Reads from single partition
var response = await table.Query
    .Where($"{OrderFields.CustomerId} = {{0}}", OrderKeys.Pk("customer123"))
    .ExecuteAsync<Order>();

// Consumed capacity: ~5 RCUs for 40KB of data
```

**❌ Inefficient: Scan entire table**
```csharp
// Reads entire table
var response = await table.AsScannable().Scan
    .WithFilter($"{OrderFields.CustomerId} = {{0}}", "customer123")
    .ExecuteAsync();

// Consumed capacity: 500+ RCUs for 4MB table
```

**Performance Impact:**
- 100x faster query times
- 100x lower capacity consumption
- Predictable latency

### Leverage Sort Key Conditions

**✅ Efficient: Sort key range**
```csharp
// Query with sort key condition
var sevenDaysAgo = DateTime.UtcNow.AddDays(-7);

var response = await table.Query
    .Where($"{OrderFields.CustomerId} = {{0}} AND {OrderFields.CreatedAt} > {{1:o}}", 
           OrderKeys.Pk("customer123"), 
           sevenDaysAgo)
    .ExecuteAsync<Order>();

// Returns only recent orders
```

**❌ Inefficient: Filter expression only**
```csharp
// Query all orders, filter in application
var response = await table.Query
    .Where($"{OrderFields.CustomerId} = {{0}}", OrderKeys.Pk("customer123"))
    .WithFilter($"{OrderFields.CreatedAt} > {{0:o}}", sevenDaysAgo)
    .ExecuteAsync<Order>();

// Reads all orders, filters after (consumes more RCUs)
```

**Performance Impact:**
- 50-90% reduction in data read
- Lower capacity consumption
- Faster response times

### Use GSIs for Alternative Access Patterns

**✅ Efficient: Query GSI**
```csharp
// Query by status using GSI
var response = await table.Query
    .WithIndex(OrderIndexes.StatusIndex)
    .Where($"{OrderFields.StatusIndex.Status} = {{0}}", "pending")
    .ExecuteAsync<Order>();

// Efficient: Uses index
```

**❌ Inefficient: Scan with filter**
```csharp
// Scan entire table
var response = await table.AsScannable().Scan
    .WithFilter($"{OrderFields.Status} = {{0}}", "pending")
    .ExecuteAsync();

// Inefficient: Reads entire table
```

**Performance Impact:**
- 10-100x faster queries
- Significantly lower costs
- Better scalability


## Projection Expressions

### Request Only Needed Attributes

**✅ Efficient: Projection expression**
```csharp
// Request only needed attributes
var response = await table.Query
    .Where($"{OrderFields.CustomerId} = {{0}}", OrderKeys.Pk("customer123"))
    .WithProjection($"{OrderFields.OrderId}, {OrderFields.Total}, {OrderFields.Status}")
    .ExecuteAsync();

// Reads: 10KB (3 attributes)
// Consumed: 2 RCUs
```

**❌ Inefficient: Fetch all attributes**
```csharp
// Fetch entire items
var response = await table.Query
    .Where($"{OrderFields.CustomerId} = {{0}}", OrderKeys.Pk("customer123"))
    .ExecuteAsync<Order>();

// Reads: 40KB (all attributes)
// Consumed: 5 RCUs
```

**Performance Impact:**
- 50-80% reduction in data transfer
- Lower capacity consumption
- Faster network transfer
- Reduced deserialization time

### Projection for List Operations

```csharp
// Efficient: Get only IDs and names for dropdown
var response = await table.Query
    .Where($"{ProductFields.Category} = {{0}}", "electronics")
    .WithProjection($"{ProductFields.ProductId}, {ProductFields.Name}")
    .ExecuteAsync();

// Process lightweight results
var dropdown = response.Items.Select(item => new
{
    Id = item[ProductFields.ProductId].S,
    Name = item[ProductFields.Name].S
}).ToList();
```

## Batch Operations vs Individual Calls

### Batch Get Operations

**✅ Efficient: Batch get**
```csharp
// Single request for multiple items
var batchBuilder = new BatchGetItemRequestBuilder(client);

foreach (var userId in userIds)
{
    batchBuilder.Get(table, builder => builder
        .WithKey(UserFields.UserId, UserKeys.Pk(userId)));
}

var response = await batchBuilder.ExecuteAsync();

// 1 request for up to 100 items
// Latency: ~10ms
```

**❌ Inefficient: Individual gets**
```csharp
// Multiple sequential requests
var users = new List<User>();

foreach (var userId in userIds)
{
    var response = await table.Get
        .WithKey(UserFields.UserId, UserKeys.Pk(userId))
        .ExecuteAsync<User>();
    
    users.Add(response.Item);
}

// 100 requests for 100 items
// Latency: ~1000ms (100 * 10ms)
```

**Performance Impact:**
- 100x lower latency
- Reduced network overhead
- Better throughput

### Batch Write Operations

**✅ Efficient: Batch write**
```csharp
// Single request for multiple writes
var batchBuilder = new BatchWriteItemRequestBuilder(client);

foreach (var order in orders)
{
    batchBuilder.Put(table, builder => builder
        .WithItem(order));
}

await batchBuilder.ExecuteAsync();

// 1 request for up to 25 items
```

**❌ Inefficient: Individual puts**
```csharp
// Multiple sequential requests
foreach (var order in orders)
{
    await table.Put
        .WithItem(order)
        .ExecuteAsync();
}

// 25 requests for 25 items
```

**Performance Impact:**
- 25x lower latency
- Reduced WCU consumption (no per-request overhead)
- Better throughput

### Parallel Batch Operations

For large datasets, parallelize batch operations:

```csharp
// Efficient: Parallel batch operations
var batches = userIds
    .Chunk(100)  // DynamoDB batch limit
    .Select(async batch =>
    {
        var batchBuilder = new BatchGetItemRequestBuilder(client);
        
        foreach (var userId in batch)
        {
            batchBuilder.Get(table, builder => builder
                .WithKey(UserFields.UserId, UserKeys.Pk(userId)));
        }
        
        return await batchBuilder.ExecuteAsync();
    });

var results = await Task.WhenAll(batches);

// Process 1000 items in ~10 parallel batches
// Total time: ~100ms (vs 10 seconds sequential)
```

## Pagination Strategies

### Efficient Pagination

**✅ Efficient: Use LastEvaluatedKey**
```csharp
var allOrders = new List<Order>();
string? lastEvaluatedKey = null;

do
{
    var response = await table.Query
        .Where($"{OrderFields.CustomerId} = {{0}}", OrderKeys.Pk("customer123"))
        .Take(100)  // Page size
        .WithExclusiveStartKey(lastEvaluatedKey)
        .ExecuteAsync<Order>();
    
    allOrders.AddRange(response.Items);
    lastEvaluatedKey = response.LastEvaluatedKey;
    
} while (lastEvaluatedKey != null);
```

**❌ Inefficient: Fetch all at once**
```csharp
// No pagination - may hit 1MB limit
var response = await table.Query
    .Where($"{OrderFields.CustomerId} = {{0}}", OrderKeys.Pk("customer123"))
    .ExecuteAsync<Order>();

// May require multiple round trips internally
// No control over memory usage
```

### Optimal Page Size

Choose page size based on item size and use case:

```csharp
// Small items (1KB each): Larger page size
var response = await table.Query
    .Where($"{OrderFields.CustomerId} = {{0}}", OrderKeys.Pk("customer123"))
    .Take(1000)  // 1MB / 1KB = 1000 items
    .ExecuteAsync<Order>();

// Large items (100KB each): Smaller page size
var response = await table.Query
    .Where($"{OrderFields.CustomerId} = {{0}}", OrderKeys.Pk("customer123"))
    .Take(10)  // 1MB / 100KB = 10 items
    .ExecuteAsync<Order>();
```

**Guidelines:**
- Target ~1MB per page (DynamoDB limit)
- Balance between round trips and memory usage
- Consider network latency

### Cursor-Based Pagination for APIs

```csharp
[HttpGet]
public async Task<ActionResult<PagedResult<Order>>> GetOrders(
    string customerId,
    string? cursor = null,
    int pageSize = 50)
{
    var response = await table.Query
        .Where($"{OrderFields.CustomerId} = {{0}}", OrderKeys.Pk(customerId))
        .Take(pageSize)
        .WithExclusiveStartKey(cursor)
        .ExecuteAsync<Order>();
    
    return Ok(new PagedResult<Order>
    {
        Items = response.Items,
        NextCursor = response.LastEvaluatedKey,
        HasMore = response.LastEvaluatedKey != null
    });
}
```


## Consistent Reads vs Eventual Consistency

### Eventually Consistent Reads (Default)

```csharp
// Eventually consistent (default)
var response = await table.Get
    .WithKey(UserFields.UserId, UserKeys.Pk("user123"))
    .ExecuteAsync<User>();

// Cost: 0.5 RCU per 4KB
// Latency: ~5-10ms
// Consistency: May read stale data (< 1 second old)
```

**Use Cases:**
- Dashboard displays
- List views
- Non-critical reads
- High-throughput scenarios

### Strongly Consistent Reads

```csharp
// Strongly consistent
var response = await table.Get
    .WithKey(UserFields.UserId, UserKeys.Pk("user123"))
    .UsingConsistentRead()
    .ExecuteAsync<User>();

// Cost: 1 RCU per 4KB (2x more expensive)
// Latency: ~10-15ms
// Consistency: Always reads latest data
```

**Use Cases:**
- Financial transactions
- Inventory management
- After write operations
- Critical business logic

### Cost Comparison

```csharp
// Scenario: Read 1000 items, 4KB each

// Eventually consistent
// Cost: 1000 * 0.5 = 500 RCUs
// Monthly cost: ~$0.06 (at $0.25 per million RCUs)

// Strongly consistent
// Cost: 1000 * 1 = 1000 RCUs
// Monthly cost: ~$0.13 (at $0.25 per million RCUs)
```

**Recommendation:** Use eventually consistent reads by default, strongly consistent only when necessary.

## Monitoring Consumed Capacity

### Track Capacity Usage

```csharp
// Enable capacity tracking
var response = await table.Query
    .Where($"{OrderFields.CustomerId} = {{0}}", OrderKeys.Pk("customer123"))
    .WithReturnConsumedCapacity(ReturnConsumedCapacity.TOTAL)
    .ExecuteAsync<Order>();

// Log consumed capacity
Console.WriteLine($"Consumed capacity: {response.ConsumedCapacity?.CapacityUnits} RCUs");
Console.WriteLine($"Table: {response.ConsumedCapacity?.TableName}");

// Track GSI capacity separately
if (response.ConsumedCapacity?.GlobalSecondaryIndexes != null)
{
    foreach (var gsi in response.ConsumedCapacity.GlobalSecondaryIndexes)
    {
        Console.WriteLine($"GSI {gsi.Key}: {gsi.Value.CapacityUnits} RCUs");
    }
}
```

### Detailed Capacity Tracking

```csharp
// Track capacity per operation
var response = await table.Query
    .Where($"{OrderFields.CustomerId} = {{0}}", OrderKeys.Pk("customer123"))
    .WithReturnConsumedCapacity(ReturnConsumedCapacity.INDEXES)
    .ExecuteAsync<Order>();

// Detailed breakdown
var capacity = response.ConsumedCapacity;
Console.WriteLine($"Total: {capacity?.CapacityUnits} RCUs");
Console.WriteLine($"Table: {capacity?.Table?.CapacityUnits} RCUs");
Console.WriteLine($"Local Secondary Indexes: {capacity?.LocalSecondaryIndexes?.Sum(x => x.Value.CapacityUnits)} RCUs");
Console.WriteLine($"Global Secondary Indexes: {capacity?.GlobalSecondaryIndexes?.Sum(x => x.Value.CapacityUnits)} RCUs");
```

### Capacity Monitoring Service

```csharp
public class CapacityMonitoringService
{
    private readonly ILogger<CapacityMonitoringService> _logger;
    private readonly IMetrics _metrics;
    
    public async Task<QueryResponse> QueryWithMonitoringAsync<T>(
        QueryRequestBuilder query,
        string operationName)
    {
        var stopwatch = Stopwatch.StartNew();
        
        var response = await query
            .WithReturnConsumedCapacity(ReturnConsumedCapacity.TOTAL)
            .ExecuteAsync();
        
        stopwatch.Stop();
        
        // Log metrics
        var consumedRCUs = response.ConsumedCapacity?.CapacityUnits ?? 0;
        _metrics.Gauge("dynamodb.consumed_capacity", consumedRCUs, 
            tags: new[] { $"operation:{operationName}" });
        _metrics.Timer("dynamodb.latency", stopwatch.ElapsedMilliseconds,
            tags: new[] { $"operation:{operationName}" });
        
        // Log warnings for high consumption
        if (consumedRCUs > 100)
        {
            _logger.LogWarning(
                "High capacity consumption: {Operation} consumed {RCUs} RCUs in {Latency}ms",
                operationName, consumedRCUs, stopwatch.ElapsedMilliseconds);
        }
        
        return response;
    }
}
```

## Hot Partition Avoidance

### Problem: Hot Partitions

```csharp
// ❌ Bad: All items have same partition key
[DynamoDbTable("events")]
public partial class Event
{
    [PartitionKey]
    [DynamoDbAttribute("pk")]
    public string Type { get; set; } = "EVENT";  // Same for all events
    
    [SortKey]
    [DynamoDbAttribute("sk")]
    public DateTime Timestamp { get; set; }
}

// All writes go to single partition
// Throughput limited to 1000 WCU per partition
```

### Solution 1: Distribute Across Partitions

```csharp
// ✅ Good: Distribute by date
[DynamoDbTable("events")]
public partial class Event
{
    public DateTime Timestamp { get; set; }
    
    [PartitionKey]
    [Computed(nameof(Timestamp), Format = "EVENT#{0:yyyy-MM-dd}")]
    [DynamoDbAttribute("pk")]
    public string PartitionKey { get; set; } = string.Empty;
    
    [SortKey]
    [Computed(nameof(Timestamp), Format = "{0:HH:mm:ss.fff}")]
    [DynamoDbAttribute("sk")]
    public string SortKey { get; set; } = string.Empty;
}

// Distributes writes across daily partitions
// Each partition handles 1 day of events
```

### Solution 2: Add Shard Suffix

```csharp
// ✅ Good: Add shard suffix
[DynamoDbTable("events")]
public partial class Event
{
    public string EventId { get; set; } = string.Empty;
    
    [PartitionKey]
    [Computed(nameof(EventId), Format = "EVENT#{0}")]
    [DynamoDbAttribute("pk")]
    public string PartitionKey { get; set; } = string.Empty;
}

// Generate EventId with shard suffix
var shardId = Random.Shared.Next(0, 10);  // 10 shards
var eventId = $"{Guid.NewGuid()}-{shardId}";

// Distributes across 10 partitions
```

### Solution 3: Use Write Sharding

```csharp
public class ShardedWriteService
{
    private const int ShardCount = 10;
    
    public async Task WriteEventAsync(Event evt)
    {
        // Add shard suffix to partition key
        var shardId = Random.Shared.Next(0, ShardCount);
        evt.PartitionKey = $"{evt.PartitionKey}#{shardId}";
        
        await table.Put
            .WithItem(evt)
            .ExecuteAsync();
    }
    
    public async Task<List<Event>> ReadAllEventsAsync(string basePartitionKey)
    {
        var allEvents = new List<Event>();
        
        // Query all shards in parallel
        var queries = Enumerable.Range(0, ShardCount)
            .Select(async shardId =>
            {
                var pk = $"{basePartitionKey}#{shardId}";
                var response = await table.Query
                    .Where($"{EventFields.PartitionKey} = {{0}}", pk)
                    .ExecuteAsync<Event>();
                return response.Items;
            });
        
        var results = await Task.WhenAll(queries);
        allEvents.AddRange(results.SelectMany(x => x));
        
        return allEvents;
    }
}
```

## Caching Strategies

### Application-Level Caching

```csharp
public class CachedUserService
{
    private readonly IMemoryCache _cache;
    private readonly DynamoDbTableBase _table;
    
    public async Task<User?> GetUserAsync(string userId)
    {
        var cacheKey = $"user:{userId}";
        
        // Check cache first
        if (_cache.TryGetValue<User>(cacheKey, out var cachedUser))
        {
            return cachedUser;
        }
        
        // Query DynamoDB
        var response = await _table.Get
            .WithKey(UserFields.UserId, UserKeys.Pk(userId))
            .ExecuteAsync<User>();
        
        // Cache result
        if (response.Item != null)
        {
            _cache.Set(cacheKey, response.Item, TimeSpan.FromMinutes(5));
        }
        
        return response.Item;
    }
}
```

### DAX (DynamoDB Accelerator)

```csharp
// Configure DAX client
var daxConfig = new DaxClientConfig("my-cluster.dax-clusters.us-east-1.amazonaws.com:8111")
{
    AwsCredentials = new DefaultAWSCredentialsProvider().GetCredentials()
};

var daxClient = new ClusterDaxClient(daxConfig);

// Use DAX client for reads
var response = await table.Get
    .WithKey(UserFields.UserId, UserKeys.Pk("user123"))
    .WithClient(daxClient)
    .ExecuteAsync<User>();

// Microsecond latency for cached items
// Millisecond latency for cache misses
```

**DAX Benefits:**
- Microsecond read latency
- Reduces DynamoDB read costs
- Transparent caching
- Write-through cache

## Best Practices Summary

### 1. Use Source Generation

```csharp
// ✅ Always use source generation
[DynamoDbTable("users")]
public partial class User
{
    [PartitionKey]
    [DynamoDbAttribute("pk")]
    public string UserId { get; set; } = string.Empty;
}

// Automatic: Zero-overhead mapping
```

### 2. Query, Don't Scan

```csharp
// ✅ Query with partition key
await table.Query
    .Where($"{OrderFields.CustomerId} = {{0}}", OrderKeys.Pk("customer123"))
    .ExecuteAsync<Order>();

// ❌ Avoid scans
await table.AsScannable().Scan.ExecuteAsync();
```

### 3. Use Batch Operations

```csharp
// ✅ Batch get for multiple items
var batchBuilder = new BatchGetItemRequestBuilder(client);
// Add items...
await batchBuilder.ExecuteAsync();

// ❌ Avoid individual gets in loop
foreach (var id in ids)
{
    await table.Get.WithKey(...).ExecuteAsync();
}
```

### 4. Project Only Needed Attributes

```csharp
// ✅ Request specific attributes
await table.Query
    .Where($"{OrderFields.CustomerId} = {{0}}", OrderKeys.Pk("customer123"))
    .WithProjection($"{OrderFields.OrderId}, {OrderFields.Total}")
    .ExecuteAsync();
```

### 5. Use Eventually Consistent Reads

```csharp
// ✅ Eventually consistent (default)
await table.Get
    .WithKey(UserFields.UserId, UserKeys.Pk("user123"))
    .ExecuteAsync<User>();

// Only use strongly consistent when necessary
await table.Get
    .WithKey(UserFields.UserId, UserKeys.Pk("user123"))
    .UsingConsistentRead()
    .ExecuteAsync<User>();
```

### 6. Monitor Capacity Usage

```csharp
// ✅ Track consumed capacity
var response = await table.Query
    .Where($"{OrderFields.CustomerId} = {{0}}", OrderKeys.Pk("customer123"))
    .WithReturnConsumedCapacity(ReturnConsumedCapacity.TOTAL)
    .ExecuteAsync<Order>();

Console.WriteLine($"Consumed: {response.ConsumedCapacity?.CapacityUnits} RCUs");
```

### 7. Avoid Hot Partitions

```csharp
// ✅ Distribute across partitions
[PartitionKey]
[Computed(nameof(Timestamp), Format = "EVENT#{0:yyyy-MM-dd}")]
[DynamoDbAttribute("pk")]
public string PartitionKey { get; set; } = string.Empty;
```

### 8. Implement Caching

```csharp
// ✅ Cache frequently accessed data
if (_cache.TryGetValue(cacheKey, out var cachedValue))
{
    return cachedValue;
}

var response = await table.Get.WithKey(...).ExecuteAsync();
_cache.Set(cacheKey, response.Item, TimeSpan.FromMinutes(5));
```

## Next Steps

- **[Global Secondary Indexes](GlobalSecondaryIndexes.md)** - Optimize GSI usage
- **[Composite Entities](CompositeEntities.md)** - Efficient multi-item patterns
- **[Querying Data](../core-features/QueryingData.md)** - Query optimization techniques
- **[Batch Operations](../core-features/BatchOperations.md)** - Batch operation patterns

---

[Previous: STS Integration](STSIntegration.md) | [Next: Manual Patterns](ManualPatterns.md)

**See Also:**
- [Expression Formatting](../core-features/ExpressionFormatting.md)
- [Pagination](../core-features/QueryingData.md#pagination)
- [Error Handling](../reference/ErrorHandling.md)
- [Troubleshooting](../reference/Troubleshooting.md)
