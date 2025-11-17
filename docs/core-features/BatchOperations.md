---
title: "Batch Operations"
category: "core-features"
order: 5
keywords: ["batch", "batch get", "batch write", "bulk operations", "performance", "unprocessed items"]
related: ["BasicOperations.md", "QueryingData.md", "Transactions.md", "../advanced-topics/PerformanceOptimization.md"]
---

[Documentation](../README.md) > [Core Features](README.md) > Batch Operations

# Batch Operations

[Previous: Expression Formatting](ExpressionFormatting.md) | [Next: Transactions](Transactions.md)

---

Batch operations allow you to read or write multiple items in a single request, significantly improving performance and reducing API calls compared to individual operations. This guide covers batch get and batch write operations with best practices for handling unprocessed items.

## Overview

DynamoDB provides two batch operations:

**BatchGetItem:**
- Retrieve up to 100 items or 16MB of data
- Read from one or more tables
- Items retrieved in parallel
- Supports projection expressions and consistent reads

**BatchWriteItem:**
- Put or delete up to 25 items
- Write to one or more tables
- Operations processed in parallel
- No conditional expressions supported

## Quick Start

The new batch API uses static entry points and reuses existing request builders:

```csharp
// Batch write
await DynamoDbBatch.Write
    .Add(userTable.Put(user1))
    .Add(userTable.Put(user2))
    .Add(orderTable.Delete(orderId))
    .ExecuteAsync();

// Batch get with deserialization
var (user, order) = await DynamoDbBatch.Get
    .Add(userTable.Get(userId))
    .Add(orderTable.Get(orderId))
    .ExecuteAndMapAsync<User, Order>();
```


## Batch Get Operations

Batch get operations retrieve multiple items efficiently in a single request.

### Basic Batch Get

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

// Get multiple users
var response = await DynamoDbBatch.Get
    .Add(userTable.Get("user1"))
    .Add(userTable.Get("user2"))
    .Add(userTable.Get("user3"))
    .ExecuteAsync();

// Deserialize items
var users = response.GetItemsRange<User>(0, 2);
foreach (var user in users)
{
    if (user != null)
    {
        Console.WriteLine($"User: {user.Name}");
    }
}
```

### ExecuteAndMapAsync - Tuple Destructuring

For convenience with small numbers of items, use `ExecuteAndMapAsync`:

```csharp
// 2 items
var (user, order) = await DynamoDbBatch.Get
    .Add(userTable.Get(userId))
    .Add(orderTable.Get(orderId))
    .ExecuteAndMapAsync<User, Order>();

// 3 items
var (user, account, order) = await DynamoDbBatch.Get
    .Add(userTable.Get(userId))
    .Add(accountTable.Get(accountId))
    .Add(orderTable.Get(orderId))
    .ExecuteAndMapAsync<User, Account, Order>();

// Up to 8 items supported
```


### Response Deserialization Methods

The `BatchGetResponse` provides multiple ways to deserialize items:

```csharp
var response = await DynamoDbBatch.Get
    .Add(userTable.Get("user1"))
    .Add(userTable.Get("user2"))
    .Add(userTable.Get("user3"))
    .Add(orderTable.Get("order1"))
    .ExecuteAsync();

// Get single item by index
var user1 = response.GetItem<User>(0);

// Get multiple items of same type by indices
var users = response.GetItems<User>(0, 1, 2);

// Get contiguous range of items
var allUsers = response.GetItemsRange<User>(0, 2); // indices 0, 1, 2

// Get item from different table
var order = response.GetItem<Order>(3);

// Check total count
Console.WriteLine($"Retrieved {response.Count} items");

// Check for unprocessed keys
if (response.HasUnprocessedKeys)
{
    Console.WriteLine($"Warning: {response.UnprocessedKeys.Count} tables have unprocessed keys");
    // Implement retry logic
}
```

### Batch Get with Composite Keys

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
}

// Using source-generated methods (no generic parameters)
var response = await DynamoDbBatch.Get
    .Add(orderTable.Get("customer123", "order1"))
    .Add(orderTable.Get("customer123", "order2"))
    .Add(orderTable.Get("customer456", "order3"))
    .ExecuteAsync();

var orders = response.GetItemsRange<Order>(0, 2);
```


### Batch Get with Projection

Retrieve only specific attributes to reduce data transfer:

```csharp
var response = await DynamoDbBatch.Get
    .Add(userTable.Get("user1").WithProjection("name, email"))
    .Add(userTable.Get("user2").WithProjection("name, email"))
    .Add(userTable.Get("user3").WithProjection("name, email"))
    .ExecuteAsync();

var users = response.GetItemsRange<User>(0, 2);
```

### Batch Get with Consistent Reads

```csharp
var response = await DynamoDbBatch.Get
    .Add(userTable.Get("user1").UsingConsistentRead())
    .Add(userTable.Get("user2").UsingConsistentRead())
    .ExecuteAsync();

var users = response.GetItemsRange<User>(0, 1);
```

**Note:** Consistent reads consume twice the read capacity. Use them only when you need the most up-to-date data.

### Batch Get from Multiple Tables

```csharp
var response = await DynamoDbBatch.Get
    .Add(userTable.Get("user123"))
    .Add(userTable.Get("user456"))
    .Add(orderTable.Get("customer123", "order1"))
    .Add(productTable.Get("prod789"))
    .ExecuteAsync();

// Items are returned in the order they were added
var user1 = response.GetItem<User>(0);
var user2 = response.GetItem<User>(1);
var order = response.GetItem<Order>(2);
var product = response.GetItem<Product>(3);
```


## Batch Write Operations

Batch write operations put or delete multiple items in a single request.

### Basic Batch Put

```csharp
var users = new List<User>
{
    new User { UserId = "user1", Name = "Alice", Email = "alice@example.com" },
    new User { UserId = "user2", Name = "Bob", Email = "bob@example.com" },
    new User { UserId = "user3", Name = "Charlie", Email = "charlie@example.com" }
};

await DynamoDbBatch.Write
    .Add(userTable.Put(users[0]))
    .Add(userTable.Put(users[1]))
    .Add(userTable.Put(users[2]))
    .ExecuteAsync();
```

### Basic Batch Delete

```csharp
await DynamoDbBatch.Write
    .Add(userTable.Delete("user1"))
    .Add(userTable.Delete("user2"))
    .Add(userTable.Delete("user3"))
    .ExecuteAsync();
```

### Mixed Put and Delete Operations

```csharp
await DynamoDbBatch.Write
    // Add new users
    .Add(userTable.Put(newUser1))
    .Add(userTable.Put(newUser2))
    
    // Delete old users
    .Add(userTable.Delete("oldUser1"))
    .Add(userTable.Delete("oldUser2"))
    .ExecuteAsync();
```

### Batch Write to Multiple Tables

```csharp
await DynamoDbBatch.Write
    .Add(userTable.Put(user))
    .Add(orderTable.Put(order))
    .Add(auditTable.Put(auditEntry))
    .ExecuteAsync();
```

### Batch Delete with Composite Keys

```csharp
// Using source-generated methods
await DynamoDbBatch.Write
    .Add(orderTable.Delete("customer123", "order1"))
    .Add(orderTable.Delete("customer123", "order2"))
    .Add(orderTable.Delete("customer456", "order3"))
    .ExecuteAsync();
```


## Handling Unprocessed Items

DynamoDB may not process all items in a batch request due to capacity limits or other constraints. Always check for and handle unprocessed items.

### Checking for Unprocessed Items

```csharp
// Batch get
var getResponse = await DynamoDbBatch.Get
    .Add(userTable.Get("user1"))
    .Add(userTable.Get("user2"))
    .ExecuteAsync();

if (getResponse.HasUnprocessedKeys)
{
    Console.WriteLine($"Unprocessed keys in {getResponse.UnprocessedKeys.Count} tables");
    // Implement retry logic
}

// Batch write
var writeResponse = await DynamoDbBatch.Write
    .Add(userTable.Put(user1))
    .Add(userTable.Put(user2))
    .ExecuteAsync();

if (writeResponse.UnprocessedItems.Count > 0)
{
    Console.WriteLine($"Unprocessed items in {writeResponse.UnprocessedItems.Count} tables");
    // Implement retry logic
}
```

### Retry Logic with Exponential Backoff

```csharp
public async Task<BatchGetResponse> BatchGetWithRetry(
    BatchGetBuilder builder,
    int maxRetries = 3)
{
    var response = await builder.ExecuteAsync();
    var retryCount = 0;
    
    while (response.HasUnprocessedKeys && retryCount < maxRetries)
    {
        // Exponential backoff: 100ms, 200ms, 400ms
        var delayMs = 100 * (int)Math.Pow(2, retryCount);
        await Task.Delay(delayMs);
        
        Console.WriteLine($"Retry {retryCount + 1}: unprocessed keys remaining");
        
        // Retry with unprocessed keys
        var retryRequest = new BatchGetItemRequest
        {
            RequestItems = response.UnprocessedKeys
        };
        
        var retryResponse = await client.BatchGetItemAsync(retryRequest);
        response = new BatchGetResponse(retryResponse, tableOrder);
        retryCount++;
    }
    
    if (response.HasUnprocessedKeys)
    {
        Console.WriteLine($"Failed to process all items after {maxRetries} retries");
    }
    
    return response;
}
```


## Client Configuration

The batch builders automatically infer the DynamoDB client from the first request builder, or you can explicitly specify it.

### Automatic Client Inference

```csharp
// Client is automatically extracted from userTable
await DynamoDbBatch.Write
    .Add(userTable.Put(user))
    .Add(orderTable.Put(order))
    .ExecuteAsync();
```

### Explicit Client with WithClient()

```csharp
var scopedClient = GetScopedDynamoDbClient(); // e.g., with STS credentials

await DynamoDbBatch.Write
    .WithClient(scopedClient)
    .Add(userTable.Put(user))
    .Add(orderTable.Put(order))
    .ExecuteAsync();
```

### Client as ExecuteAsync Parameter

```csharp
var client = GetDynamoDbClient();

await DynamoDbBatch.Write
    .Add(userTable.Put(user))
    .ExecuteAsync(client); // Highest precedence
```

### Client Precedence

The client is determined in this order (highest to lowest precedence):

1. **ExecuteAsync parameter** - `ExecuteAsync(client)`
2. **Explicit WithClient()** - `.WithClient(client)`
3. **Inferred from first builder** - Extracted automatically

See [Transactions](Transactions.md#client-configuration) for more details on client configuration.


## Batch Limits and Validation

### Size Limits

**BatchGetItem:**
- Maximum 100 items per request
- Maximum 16MB of data per request
- Items retrieved in parallel

**BatchWriteItem:**
- Maximum 25 put or delete operations per request
- Each item can be up to 400KB
- Operations processed in parallel

### Validation Errors

The batch builder validates operations before execution:

```csharp
// Too many write operations
try
{
    var batch = DynamoDbBatch.Write;
    for (int i = 0; i < 26; i++)
    {
        batch.Add(userTable.Put(new User { UserId = $"user{i}" }));
    }
    await batch.ExecuteAsync();
}
catch (ValidationException ex)
{
    // "Batch contains 26 operations, but DynamoDB supports a maximum of 25 operations per batch write. Consider chunking your operations."
}

// Too many get operations
try
{
    var batch = DynamoDbBatch.Get;
    for (int i = 0; i < 101; i++)
    {
        batch.Add(userTable.Get($"user{i}"));
    }
    await batch.ExecuteAsync();
}
catch (ValidationException ex)
{
    // "Batch contains 101 operations, but DynamoDB supports a maximum of 100 operations per batch get. Consider chunking your operations."
}
```


## Batch-Level Configuration

Configure batch-level settings that apply to the entire batch:

### Return Consumed Capacity

```csharp
var response = await DynamoDbBatch.Write
    .Add(userTable.Put(user1))
    .Add(userTable.Put(user2))
    .ReturnConsumedCapacity(ReturnConsumedCapacity.TOTAL)
    .ExecuteAsync();

// Check capacity consumption
if (response.ConsumedCapacity != null)
{
    foreach (var capacity in response.ConsumedCapacity)
    {
        Console.WriteLine($"Table: {capacity.TableName}");
        Console.WriteLine($"Capacity: {capacity.CapacityUnits} units");
    }
}
```

### Return Item Collection Metrics

```csharp
var response = await DynamoDbBatch.Write
    .Add(userTable.Put(user))
    .ReturnItemCollectionMetrics()
    .ExecuteAsync();

// Check item collection metrics
if (response.ItemCollectionMetrics != null)
{
    foreach (var metric in response.ItemCollectionMetrics)
    {
        Console.WriteLine($"Table: {metric.Key}");
    }
}
```


## Encryption Support

Field encryption works automatically in batch write operations when putting entities with encrypted fields:

```csharp
[DynamoDbTable("users")]
public partial class User
{
    [PartitionKey]
    public string UserId { get; set; } = string.Empty;
    
    [Encrypted]
    public string SocialSecurityNumber { get; set; } = string.Empty;
}

// Encryption happens during Put(entity) call
var user = new User
{
    UserId = "user123",
    SocialSecurityNumber = "123-45-6789"
};

await DynamoDbBatch.Write
    .Add(userTable.Put(user)) // Encrypted during ToDynamoDb conversion
    .ExecuteAsync();
```

**How it works:**
1. When `Put(entity)` is called, the entity is converted to DynamoDB format using `ToDynamoDb()`
2. During conversion, encrypted fields are automatically encrypted using the configured `IFieldEncryptor`
3. The batch builder extracts the already-encrypted item
4. No additional encryption is needed during batch execution

**Error handling:**
```csharp
try
{
    await DynamoDbBatch.Write
        .Add(userTable.Put(user))
        .ExecuteAsync();
}
catch (InvalidOperationException ex)
{
    // "Field encryption is required for property 'SocialSecurityNumber' but no IFieldEncryptor is configured."
}
```


## Performance Considerations

### Chunking Large Batches

```csharp
public async Task BatchWriteInChunks<T>(
    List<T> items,
    Func<T, PutItemRequestBuilder<T>> putBuilder,
    int chunkSize = 25)
{
    // Split into chunks of 25 (BatchWriteItem limit)
    for (int i = 0; i < items.Count; i += chunkSize)
    {
        var chunk = items.Skip(i).Take(chunkSize).ToList();
        
        var batch = DynamoDbBatch.Write;
        foreach (var item in chunk)
        {
            batch.Add(putBuilder(item));
        }
        
        var response = await batch.ExecuteAsync();
        
        // Handle unprocessed items
        if (response.UnprocessedItems.Count > 0)
        {
            Console.WriteLine($"Chunk {i / chunkSize + 1}: unprocessed items");
            // Implement retry logic
        }
    }
}

// Usage
await BatchWriteInChunks(
    allUsers,
    user => userTable.Put(user)
);
```

### Parallel Batch Operations

```csharp
public async Task ParallelBatchWrite<T>(
    List<T> items,
    Func<T, PutItemRequestBuilder<T>> putBuilder,
    int maxParallel = 4)
{
    // Split into chunks
    var chunks = items
        .Select((item, index) => new { item, index })
        .GroupBy(x => x.index / 25)
        .Select(g => g.Select(x => x.item).ToList())
        .ToList();
    
    // Process chunks in parallel (with limit)
    var semaphore = new SemaphoreSlim(maxParallel);
    var tasks = chunks.Select(async chunk =>
    {
        await semaphore.WaitAsync();
        try
        {
            var batch = DynamoDbBatch.Write;
            foreach (var item in chunk)
            {
                batch.Add(putBuilder(item));
            }
            await batch.ExecuteAsync();
        }
        finally
        {
            semaphore.Release();
        }
    });
    
    await Task.WhenAll(tasks);
}
```


## Error Handling

### Common Exceptions

```csharp
using Amazon.DynamoDBv2.Model;

try
{
    await DynamoDbBatch.Write
        .Add(userTable.Put(user1))
        .Add(userTable.Put(user2))
        .ExecuteAsync();
}
catch (ProvisionedThroughputExceededException ex)
{
    // Throughput exceeded - implement exponential backoff
    Console.WriteLine("Throughput exceeded, retry with backoff");
}
catch (ResourceNotFoundException ex)
{
    // Table doesn't exist
    Console.WriteLine($"Table not found: {ex.Message}");
}
catch (ItemCollectionSizeLimitExceededException ex)
{
    // Item collection too large (for tables with LSI)
    Console.WriteLine($"Item collection size limit exceeded: {ex.Message}");
}
catch (ValidationException ex)
{
    // Invalid request parameters (e.g., too many items)
    Console.WriteLine($"Validation error: {ex.Message}");
}
```


## Best Practices

### 1. Always Handle Unprocessed Items

```csharp
// ✅ Good - handles unprocessed items
var response = await DynamoDbBatch.Write
    .Add(userTable.Put(user))
    .ExecuteAsync();
    
if (response.UnprocessedItems.Count > 0)
{
    // Retry with exponential backoff
}

// ❌ Avoid - ignores unprocessed items
await DynamoDbBatch.Write.Add(userTable.Put(user)).ExecuteAsync();
```

### 2. Use Projection Expressions

```csharp
// ✅ Good - only retrieve needed attributes
await DynamoDbBatch.Get
    .Add(userTable.Get("user123").WithProjection("name, email"))
    .ExecuteAsync();

// ❌ Avoid - retrieves all attributes
await DynamoDbBatch.Get
    .Add(userTable.Get("user123"))
    .ExecuteAsync();
```

### 3. Chunk Large Batches

```csharp
// ✅ Good - chunks into batches of 25
await BatchWriteInChunks(allUsers, user => userTable.Put(user), 25);

// ❌ Avoid - trying to write more than 25 items
var batch = DynamoDbBatch.Write;
foreach (var user in allUsers) // Could be > 25 items
{
    batch.Add(userTable.Put(user));
}
await batch.ExecuteAsync(); // Will throw ValidationException
```

### 4. Monitor Capacity Consumption

```csharp
// ✅ Good - monitors capacity
var response = await DynamoDbBatch.Write
    .Add(userTable.Put(user))
    .ReturnConsumedCapacity(ReturnConsumedCapacity.TOTAL)
    .ExecuteAsync();

// Check response
if (response.ConsumedCapacity != null)
{
    // Log or alert on high consumption
}
```


### 5. Use Batch Operations for Bulk Reads/Writes

```csharp
// ✅ Good - single batch request
await DynamoDbBatch.Get
    .Add(userTable.Get("user1"))
    .Add(userTable.Get("user2"))
    .Add(userTable.Get("user3"))
    .ExecuteAsync();

// ❌ Avoid - multiple individual requests
foreach (var userId in userIds)
{
    await userTable.Get(userId).ExecuteAsync();
}
```

### 6. Implement Exponential Backoff

```csharp
// ✅ Good - exponential backoff for retries
var delayMs = 100 * (int)Math.Pow(2, retryCount);
await Task.Delay(delayMs);

// ❌ Avoid - fixed delay or immediate retry
await Task.Delay(100); // Same delay every time
```

### 7. Use Source-Generated Methods

```csharp
// ✅ Good - no generic parameters, cleaner code
await DynamoDbBatch.Write
    .Add(userTable.Put(user))
    .Add(orderTable.Delete("customer123", "order456"))
    .ExecuteAsync();

// ⚠️ Acceptable - generic parameters required
await DynamoDbBatch.Write
    .Add(userTable.Put<User>().WithItem(user))
    .Add(orderTable.Delete<Order>().WithKey("pk", "customer123").WithKey("sk", "order456"))
    .ExecuteAsync();
```


## Batch Operations vs Transactions

**Use Batch Operations When:**
- You need to read/write many items efficiently
- Operations are independent (no atomicity required)
- You can handle partial failures
- Cost optimization is important (1x capacity vs 2x for transactions)

**Use Transactions When:**
- You need ACID guarantees
- Operations must succeed or fail together
- You need conditional writes across items
- Data consistency is critical

See [Transactions](Transactions.md) for transactional operations.

## Complete Example

Here's a comprehensive example with retry logic and error handling:

```csharp
public class BatchOperationService
{
    private readonly UserTable _userTable;
    private readonly int _maxRetries = 3;
    
    public BatchOperationService(UserTable userTable)
    {
        _userTable = userTable;
    }
    
    public async Task<List<User>> GetUsersInBatch(List<string> userIds)
    {
        var batch = DynamoDbBatch.Get;
        foreach (var userId in userIds)
        {
            batch.Add(_userTable.Get(userId).WithProjection("userId, name, email"));
        }
        
        var response = await batch
            .ReturnConsumedCapacity(ReturnConsumedCapacity.TOTAL)
            .ExecuteAsync();
        
        // Log capacity consumption
        if (response.RawResponse.ConsumedCapacity != null)
        {
            var capacity = response.RawResponse.ConsumedCapacity.FirstOrDefault();
            Console.WriteLine($"Consumed {capacity?.CapacityUnits} RCUs");
        }
        
        // Deserialize all users
        var users = new List<User>();
        for (int i = 0; i < response.Count; i++)
        {
            var user = response.GetItem<User>(i);
            if (user != null)
            {
                users.Add(user);
            }
        }
        
        // Handle unprocessed keys
        if (response.HasUnprocessedKeys)
        {
            Console.WriteLine("Warning: Some keys were not processed");
            // Implement retry logic here
        }
        
        return users;
    }
    
    public async Task SaveUsersInBatch(List<User> users)
    {
        // Chunk into batches of 25
        for (int i = 0; i < users.Count; i += 25)
        {
            var chunk = users.Skip(i).Take(25).ToList();
            await SaveChunkWithRetry(chunk);
        }
    }
    
    private async Task SaveChunkWithRetry(List<User> chunk)
    {
        var retryCount = 0;
        
        while (retryCount <= _maxRetries)
        {
            var batch = DynamoDbBatch.Write;
            foreach (var user in chunk)
            {
                batch.Add(_userTable.Put(user));
            }
            
            var response = await batch
                .ReturnConsumedCapacity(ReturnConsumedCapacity.TOTAL)
                .ExecuteAsync();
            
            // Log capacity consumption
            if (response.ConsumedCapacity != null)
            {
                var capacity = response.ConsumedCapacity.FirstOrDefault();
                Console.WriteLine($"Consumed {capacity?.CapacityUnits} WCUs");
            }
            
            // Check for unprocessed items
            if (response.UnprocessedItems.Count == 0)
            {
                break; // Success
            }
            
            if (retryCount < _maxRetries)
            {
                var delayMs = 100 * (int)Math.Pow(2, retryCount);
                Console.WriteLine($"Retry {retryCount + 1}: unprocessed items, waiting {delayMs}ms");
                await Task.Delay(delayMs);
                retryCount++;
            }
            else
            {
                throw new Exception($"Failed to save all users after {_maxRetries} retries");
            }
        }
    }
}
```

## Next Steps

- **[Transactions](Transactions.md)** - ACID transactions across items
- **[Performance Optimization](../advanced-topics/PerformanceOptimization.md)** - Optimize batch operations
- **[Error Handling](../reference/ErrorHandling.md)** - Handle batch operation errors
- **[Basic Operations](BasicOperations.md)** - Individual CRUD operations

---

[Previous: Expression Formatting](ExpressionFormatting.md) | [Next: Transactions](Transactions.md)

**See Also:**
- [Querying Data](QueryingData.md)
- [Entity Definition](EntityDefinition.md)
- [Troubleshooting](../reference/Troubleshooting.md)
