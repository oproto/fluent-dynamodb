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
var userIds = new[] { "user1", "user2", "user3" };

var response = await new BatchGetItemRequestBuilder(client)
    .GetFromTable("users", builder =>
    {
        foreach (var userId in userIds)
        {
            builder.WithKey(UserFields.UserId, UserKeys.Pk(userId));
        }
    })
    .ExecuteAsync();

// Process results
if (response.Responses.TryGetValue("users", out var items))
{
    foreach (var item in items)
    {
        var user = UserMapper.FromAttributeMap(item);
        Console.WriteLine($"User: {user.Name}");
    }
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

// Get multiple orders
var orderKeys = new[]
{
    ("customer123", "order1"),
    ("customer123", "order2"),
    ("customer456", "order3")
};

var response = await new BatchGetItemRequestBuilder(client)
    .GetFromTable("orders", builder =>
    {
        foreach (var (customerId, orderId) in orderKeys)
        {
            builder
                .WithKey(OrderFields.CustomerId, OrderKeys.Pk(customerId))
                .WithKey(OrderFields.OrderId, OrderKeys.Sk(orderId));
        }
    })
    .ExecuteAsync();
```

### Batch Get with Projection

Retrieve only specific attributes to reduce data transfer:

```csharp
var response = await new BatchGetItemRequestBuilder(client)
    .GetFromTable("users", builder =>
    {
        foreach (var userId in userIds)
        {
            builder.WithKey(UserFields.UserId, UserKeys.Pk(userId));
        }
        
        // Only retrieve name and email
        builder
            .WithProjection($"{UserFields.Name}, {UserFields.Email}")
            .WithAttributeName("#name", UserFields.Name)
            .WithAttributeName("#email", UserFields.Email);
    })
    .ExecuteAsync();
```

### Batch Get with Consistent Reads

```csharp
var response = await new BatchGetItemRequestBuilder(client)
    .GetFromTable("users", builder =>
    {
        foreach (var userId in userIds)
        {
            builder.WithKey(UserFields.UserId, UserKeys.Pk(userId));
        }
        
        // Use strongly consistent reads (2x capacity cost)
        builder.UsingConsistentRead();
    })
    .ExecuteAsync();
```

**Note:** Consistent reads consume twice the read capacity. Use them only when you need the most up-to-date data.

### Batch Get from Multiple Tables

```csharp
var response = await new BatchGetItemRequestBuilder(client)
    .GetFromTable("users", builder =>
    {
        builder.WithKey(UserFields.UserId, UserKeys.Pk("user123"));
        builder.WithKey(UserFields.UserId, UserKeys.Pk("user456"));
    })
    .GetFromTable("orders", builder =>
    {
        builder
            .WithKey(OrderFields.CustomerId, OrderKeys.Pk("customer123"))
            .WithKey(OrderFields.OrderId, OrderKeys.Sk("order1"));
    })
    .GetFromTable("products", builder =>
    {
        builder.WithKey(ProductFields.ProductId, ProductKeys.Pk("prod789"));
    })
    .ExecuteAsync();

// Process results from each table
if (response.Responses.TryGetValue("users", out var users))
{
    foreach (var item in users)
    {
        var user = UserMapper.FromAttributeMap(item);
        Console.WriteLine($"User: {user.Name}");
    }
}

if (response.Responses.TryGetValue("orders", out var orders))
{
    foreach (var item in orders)
    {
        var order = OrderMapper.FromAttributeMap(item);
        Console.WriteLine($"Order: {order.OrderId}");
    }
}
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

var response = await new BatchWriteItemRequestBuilder(client)
    .WriteToTable("users", builder =>
    {
        foreach (var user in users)
        {
            builder.PutItem(user, UserMapper.ToAttributeMap);
        }
    })
    .ExecuteAsync();

// Check for unprocessed items
if (response.UnprocessedItems.Count > 0)
{
    Console.WriteLine($"Warning: {response.UnprocessedItems.Count} items not processed");
}
```

### Basic Batch Delete

```csharp
var userIdsToDelete = new[] { "user1", "user2", "user3" };

var response = await new BatchWriteItemRequestBuilder(client)
    .WriteToTable("users", builder =>
    {
        foreach (var userId in userIdsToDelete)
        {
            builder.DeleteItem(UserFields.UserId, UserKeys.Pk(userId));
        }
    })
    .ExecuteAsync();
```

### Mixed Put and Delete Operations

```csharp
var response = await new BatchWriteItemRequestBuilder(client)
    .WriteToTable("users", builder =>
    {
        // Add new users
        builder.PutItem(newUser1, UserMapper.ToAttributeMap);
        builder.PutItem(newUser2, UserMapper.ToAttributeMap);
        
        // Delete old users
        builder.DeleteItem(UserFields.UserId, UserKeys.Pk("oldUser1"));
        builder.DeleteItem(UserFields.UserId, UserKeys.Pk("oldUser2"));
    })
    .ExecuteAsync();
```

### Batch Write to Multiple Tables

```csharp
var response = await new BatchWriteItemRequestBuilder(client)
    .WriteToTable("users", builder =>
    {
        builder.PutItem(user, UserMapper.ToAttributeMap);
    })
    .WriteToTable("orders", builder =>
    {
        builder.PutItem(order, OrderMapper.ToAttributeMap);
    })
    .WriteToTable("audit_log", builder =>
    {
        builder.PutItem(auditEntry, AuditMapper.ToAttributeMap);
    })
    .ExecuteAsync();
```

### Batch Delete with Composite Keys

```csharp
var orderKeysToDelete = new[]
{
    ("customer123", "order1"),
    ("customer123", "order2"),
    ("customer456", "order3")
};

var response = await new BatchWriteItemRequestBuilder(client)
    .WriteToTable("orders", builder =>
    {
        foreach (var (customerId, orderId) in orderKeysToDelete)
        {
            builder.DeleteItem(
                OrderFields.CustomerId, OrderKeys.Pk(customerId),
                OrderFields.OrderId, OrderKeys.Sk(orderId)
            );
        }
    })
    .ExecuteAsync();
```

## Handling Unprocessed Items

DynamoDB may not process all items in a batch request due to capacity limits or other constraints. Always check for and handle unprocessed items.

### Checking for Unprocessed Items

```csharp
// Batch get
var getResponse = await new BatchGetItemRequestBuilder(client)
    .GetFromTable("users", builder =>
    {
        foreach (var userId in userIds)
        {
            builder.WithKey(UserFields.UserId, UserKeys.Pk(userId));
        }
    })
    .ExecuteAsync();

if (getResponse.UnprocessedKeys.Count > 0)
{
    Console.WriteLine($"Unprocessed keys: {getResponse.UnprocessedKeys.Count}");
    // Implement retry logic
}

// Batch write
var writeResponse = await new BatchWriteItemRequestBuilder(client)
    .WriteToTable("users", builder =>
    {
        foreach (var user in users)
        {
            builder.PutItem(user, UserMapper.ToAttributeMap);
        }
    })
    .ExecuteAsync();

if (writeResponse.UnprocessedItems.Count > 0)
{
    Console.WriteLine($"Unprocessed items: {writeResponse.UnprocessedItems.Count}");
    // Implement retry logic
}
```

### Retry Logic with Exponential Backoff

```csharp
public async Task<BatchWriteItemResponse> BatchWriteWithRetry(
    BatchWriteItemRequestBuilder builder,
    int maxRetries = 3)
{
    var response = await builder.ExecuteAsync();
    var retryCount = 0;
    
    while (response.UnprocessedItems.Count > 0 && retryCount < maxRetries)
    {
        // Exponential backoff: 100ms, 200ms, 400ms
        var delayMs = 100 * (int)Math.Pow(2, retryCount);
        await Task.Delay(delayMs);
        
        Console.WriteLine($"Retry {retryCount + 1}: {response.UnprocessedItems.Count} unprocessed items");
        
        // Retry with unprocessed items
        var retryRequest = new BatchWriteItemRequest
        {
            RequestItems = response.UnprocessedItems
        };
        
        response = await client.BatchWriteItemAsync(retryRequest);
        retryCount++;
    }
    
    if (response.UnprocessedItems.Count > 0)
    {
        Console.WriteLine($"Failed to process {response.UnprocessedItems.Count} items after {maxRetries} retries");
    }
    
    return response;
}
```

### Complete Retry Pattern

```csharp
public async Task<List<T>> BatchGetAllItems<T>(
    string tableName,
    List<Dictionary<string, AttributeValue>> keys,
    Func<Dictionary<string, AttributeValue>, T> mapper,
    int maxRetries = 3)
{
    var results = new List<T>();
    var remainingKeys = new List<Dictionary<string, AttributeValue>>(keys);
    var retryCount = 0;
    
    while (remainingKeys.Count > 0 && retryCount <= maxRetries)
    {
        var response = await new BatchGetItemRequestBuilder(client)
            .GetFromTable(tableName, builder =>
            {
                foreach (var key in remainingKeys)
                {
                    builder.SetKey(k => 
                    {
                        foreach (var kvp in key)
                        {
                            k[kvp.Key] = kvp.Value;
                        }
                    });
                }
            })
            .ExecuteAsync();
        
        // Process retrieved items
        if (response.Responses.TryGetValue(tableName, out var items))
        {
            results.AddRange(items.Select(mapper));
        }
        
        // Check for unprocessed keys
        if (response.UnprocessedKeys.TryGetValue(tableName, out var unprocessed))
        {
            remainingKeys = unprocessed.Keys;
            
            if (remainingKeys.Count > 0 && retryCount < maxRetries)
            {
                // Exponential backoff
                var delayMs = 100 * (int)Math.Pow(2, retryCount);
                await Task.Delay(delayMs);
                retryCount++;
            }
        }
        else
        {
            break;
        }
    }
    
    if (remainingKeys.Count > 0)
    {
        throw new Exception($"Failed to retrieve {remainingKeys.Count} items after {maxRetries} retries");
    }
    
    return results;
}
```

## Performance Considerations

### Batch Size Limits

**BatchGetItem:**
- Maximum 100 items per request
- Maximum 16MB of data per request
- Items retrieved in parallel

**BatchWriteItem:**
- Maximum 25 put or delete operations per request
- Each item can be up to 400KB
- Operations processed in parallel

### Capacity Consumption

```csharp
// Monitor consumed capacity
var response = await new BatchWriteItemRequestBuilder(client)
    .WriteToTable("users", builder =>
    {
        foreach (var user in users)
        {
            builder.PutItem(user, UserMapper.ToAttributeMap);
        }
    })
    .ReturnTotalConsumedCapacity()
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

### Chunking Large Batches

```csharp
public async Task BatchWriteInChunks<T>(
    string tableName,
    List<T> items,
    Func<T, Dictionary<string, AttributeValue>> mapper,
    int chunkSize = 25)
{
    // Split into chunks of 25 (BatchWriteItem limit)
    for (int i = 0; i < items.Count; i += chunkSize)
    {
        var chunk = items.Skip(i).Take(chunkSize).ToList();
        
        var response = await new BatchWriteItemRequestBuilder(client)
            .WriteToTable(tableName, builder =>
            {
                foreach (var item in chunk)
                {
                    builder.PutItem(item, mapper);
                }
            })
            .ExecuteAsync();
        
        // Handle unprocessed items
        if (response.UnprocessedItems.Count > 0)
        {
            Console.WriteLine($"Chunk {i / chunkSize + 1}: {response.UnprocessedItems.Count} unprocessed items");
            // Implement retry logic
        }
    }
}

// Usage
await BatchWriteInChunks("users", allUsers, UserMapper.ToAttributeMap);
```

### Parallel Batch Operations

```csharp
public async Task ParallelBatchWrite<T>(
    string tableName,
    List<T> items,
    Func<T, Dictionary<string, AttributeValue>> mapper,
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
            await new BatchWriteItemRequestBuilder(client)
                .WriteToTable(tableName, builder =>
                {
                    foreach (var item in chunk)
                    {
                        builder.PutItem(item, mapper);
                    }
                })
                .ExecuteAsync();
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
    var response = await new BatchWriteItemRequestBuilder(client)
        .WriteToTable("users", builder =>
        {
            foreach (var user in users)
            {
                builder.PutItem(user, UserMapper.ToAttributeMap);
            }
        })
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
    // Invalid request parameters
    Console.WriteLine($"Validation error: {ex.Message}");
}
```

### Validation Before Batch Operations

```csharp
public async Task<BatchWriteItemResponse> SafeBatchWrite<T>(
    string tableName,
    List<T> items,
    Func<T, Dictionary<string, AttributeValue>> mapper)
{
    // Validate batch size
    if (items.Count > 25)
    {
        throw new ArgumentException("Batch write supports maximum 25 items. Use chunking for larger batches.");
    }
    
    // Validate item sizes
    foreach (var item in items)
    {
        var mapped = mapper(item);
        var size = CalculateItemSize(mapped);
        
        if (size > 400 * 1024) // 400KB limit
        {
            throw new ArgumentException($"Item exceeds 400KB size limit: {size} bytes");
        }
    }
    
    // Execute batch write
    return await new BatchWriteItemRequestBuilder(client)
        .WriteToTable(tableName, builder =>
        {
            foreach (var item in items)
            {
                builder.PutItem(item, mapper);
            }
        })
        .ExecuteAsync();
}

private int CalculateItemSize(Dictionary<string, AttributeValue> item)
{
    // Simplified size calculation
    return item.Sum(kvp => 
        kvp.Key.Length + 
        (kvp.Value.S?.Length ?? 0) +
        (kvp.Value.N?.Length ?? 0)
    );
}
```

## Best Practices

### 1. Always Handle Unprocessed Items

```csharp
// ✅ Good - handles unprocessed items
var response = await batchBuilder.ExecuteAsync();
if (response.UnprocessedItems.Count > 0)
{
    // Retry with exponential backoff
}

// ❌ Avoid - ignores unprocessed items
var response = await batchBuilder.ExecuteAsync();
```

### 2. Use Projection Expressions

```csharp
// ✅ Good - only retrieve needed attributes
.GetFromTable("users", builder =>
{
    builder.WithKey(UserFields.UserId, UserKeys.Pk("user123"));
    builder.WithProjection($"{UserFields.Name}, {UserFields.Email}");
})

// ❌ Avoid - retrieves all attributes
.GetFromTable("users", builder =>
{
    builder.WithKey(UserFields.UserId, UserKeys.Pk("user123"));
})
```

### 3. Chunk Large Batches

```csharp
// ✅ Good - chunks into batches of 25
await BatchWriteInChunks("users", allUsers, UserMapper.ToAttributeMap, 25);

// ❌ Avoid - trying to write more than 25 items
await new BatchWriteItemRequestBuilder(client)
    .WriteToTable("users", builder =>
    {
        foreach (var user in allUsers) // Could be > 25 items
        {
            builder.PutItem(user, UserMapper.ToAttributeMap);
        }
    })
    .ExecuteAsync();
```

### 4. Monitor Capacity Consumption

```csharp
// ✅ Good - monitors capacity
.ReturnTotalConsumedCapacity()

// Check response
if (response.ConsumedCapacity != null)
{
    // Log or alert on high consumption
}
```

### 5. Use Batch Operations for Bulk Reads/Writes

```csharp
// ✅ Good - single batch request
await new BatchGetItemRequestBuilder(client)
    .GetFromTable("users", builder =>
    {
        foreach (var userId in userIds)
        {
            builder.WithKey(UserFields.UserId, UserKeys.Pk(userId));
        }
    })
    .ExecuteAsync();

// ❌ Avoid - multiple individual requests
foreach (var userId in userIds)
{
    await table.Get
        .WithKey(UserFields.UserId, UserKeys.Pk(userId))
        .ExecuteAsync<User>();
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

## Batch Operations vs Transactions

**Use Batch Operations When:**
- You need to read/write many items efficiently
- Operations are independent (no atomicity required)
- You can handle partial failures

**Use Transactions When:**
- You need ACID guarantees
- Operations must succeed or fail together
- You need conditional writes across items

See [Transactions](Transactions.md) for transactional operations.

## Complete Example

Here's a comprehensive example with retry logic and error handling:

```csharp
public class BatchOperationService
{
    private readonly IAmazonDynamoDB _client;
    private readonly int _maxRetries = 3;
    
    public BatchOperationService(IAmazonDynamoDB client)
    {
        _client = client;
    }
    
    public async Task<List<User>> GetUsersInBatch(List<string> userIds)
    {
        var users = new List<User>();
        var remainingIds = new List<string>(userIds);
        var retryCount = 0;
        
        while (remainingIds.Count > 0 && retryCount <= _maxRetries)
        {
            var response = await new BatchGetItemRequestBuilder(_client)
                .GetFromTable("users", builder =>
                {
                    foreach (var userId in remainingIds)
                    {
                        builder.WithKey(UserFields.UserId, UserKeys.Pk(userId));
                    }
                    
                    builder.WithProjection($"{UserFields.UserId}, {UserFields.Name}, {UserFields.Email}");
                })
                .ReturnTotalConsumedCapacity()
                .ExecuteAsync();
            
            // Process retrieved items
            if (response.Responses.TryGetValue("users", out var items))
            {
                users.AddRange(items.Select(UserMapper.FromAttributeMap));
            }
            
            // Log capacity consumption
            if (response.ConsumedCapacity != null)
            {
                var capacity = response.ConsumedCapacity.FirstOrDefault();
                Console.WriteLine($"Consumed {capacity?.CapacityUnits} RCUs");
            }
            
            // Handle unprocessed keys
            if (response.UnprocessedKeys.TryGetValue("users", out var unprocessed))
            {
                remainingIds = unprocessed.Keys
                    .Select(k => k[UserFields.UserId].S)
                    .ToList();
                
                if (remainingIds.Count > 0 && retryCount < _maxRetries)
                {
                    var delayMs = 100 * (int)Math.Pow(2, retryCount);
                    Console.WriteLine($"Retry {retryCount + 1}: {remainingIds.Count} unprocessed keys, waiting {delayMs}ms");
                    await Task.Delay(delayMs);
                    retryCount++;
                }
            }
            else
            {
                break;
            }
        }
        
        if (remainingIds.Count > 0)
        {
            throw new Exception($"Failed to retrieve {remainingIds.Count} users after {_maxRetries} retries");
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
        var remainingItems = chunk;
        
        while (remainingItems.Count > 0 && retryCount <= _maxRetries)
        {
            var response = await new BatchWriteItemRequestBuilder(_client)
                .WriteToTable("users", builder =>
                {
                    foreach (var user in remainingItems)
                    {
                        builder.PutItem(user, UserMapper.ToAttributeMap);
                    }
                })
                .ReturnTotalConsumedCapacity()
                .ExecuteAsync();
            
            // Log capacity consumption
            if (response.ConsumedCapacity != null)
            {
                var capacity = response.ConsumedCapacity.FirstOrDefault();
                Console.WriteLine($"Consumed {capacity?.CapacityUnits} WCUs");
            }
            
            // Handle unprocessed items
            if (response.UnprocessedItems.TryGetValue("users", out var unprocessed))
            {
                remainingItems = unprocessed
                    .Select(wr => UserMapper.FromAttributeMap(wr.PutRequest.Item))
                    .ToList();
                
                if (remainingItems.Count > 0 && retryCount < _maxRetries)
                {
                    var delayMs = 100 * (int)Math.Pow(2, retryCount);
                    Console.WriteLine($"Retry {retryCount + 1}: {remainingItems.Count} unprocessed items, waiting {delayMs}ms");
                    await Task.Delay(delayMs);
                    retryCount++;
                }
            }
            else
            {
                break;
            }
        }
        
        if (remainingItems.Count > 0)
        {
            throw new Exception($"Failed to save {remainingItems.Count} users after {_maxRetries} retries");
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
