---
title: "Direct SDK Request Passing"
category: "advanced-topics"
order: 12
keywords: ["SDK", "request", "native", "AWS", "migration", "interoperability"]
related: ["ManualPatterns.md", "ClientConfiguration.md", "../core-features/BasicOperations.md"]
---

[Documentation](../README.md) > [Advanced Topics](README.md) > Direct SDK Request Passing

# Direct SDK Request Passing

This guide covers how to use native AWS SDK request objects with Oproto.FluentDynamoDb, enabling seamless interoperability with existing code and gradual migration paths.

## Overview

Direct SDK Request Passing allows you to:
- Inject pre-built AWS SDK request objects into FluentDynamoDb builders
- Get entity hydration and response metadata for SDK requests
- Gradually migrate existing SDK code to FluentDynamoDb
- Use FluentDynamoDb's entity mapping with complex SDK requests

## Use Cases

- **Migration**: Gradually migrate existing AWS SDK code to FluentDynamoDb
- **Complex Requests**: Use SDK features not yet exposed by FluentDynamoDb builders
- **Interoperability**: Mix FluentDynamoDb with existing SDK-based code
- **Testing**: Inject mock or pre-configured requests for testing

## WithRequest() Method

All request builders support the `WithRequest()` method to inject a pre-built SDK request:

### Get Operations

```csharp
// Create SDK request
var sdkRequest = new GetItemRequest
{
    TableName = "users",
    Key = new Dictionary<string, AttributeValue>
    {
        ["pk"] = new AttributeValue { S = "USER#123" }
    },
    ConsistentRead = true
};

// Simple inline usage - no need to capture the builder
var user = await table.Users.Get().WithRequest(sdkRequest).GetItemAsync();

// If you need response metadata, capture the builder first
var builder = table.Users.Get().WithRequest(sdkRequest);
var user = await builder.GetItemAsync();
var capacity = builder.Response?.ConsumedCapacity;
```

### Query Operations

```csharp
// Create SDK request
var sdkRequest = new QueryRequest
{
    TableName = "orders",
    KeyConditionExpression = "pk = :pk",
    ExpressionAttributeValues = new Dictionary<string, AttributeValue>
    {
        [":pk"] = new AttributeValue { S = "CUSTOMER#123" }
    },
    ScanIndexForward = false,
    Limit = 10
};

// Simple inline usage - no need to capture the builder
var orders = await table.Orders.Query().WithRequest(sdkRequest).ToListAsync();

// If you need response metadata (pagination, counts), capture the builder first
var builder = table.Orders.Query().WithRequest(sdkRequest);
var orders = await builder.ToListAsync();
var lastKey = builder.Response?.LastEvaluatedKey;
var scannedCount = builder.Response?.ScannedCount;
```

### Put Operations

```csharp
// Create SDK request
var sdkRequest = new PutItemRequest
{
    TableName = "users",
    Item = User.ToDynamoDb(user),
    ConditionExpression = "attribute_not_exists(pk)",
    ReturnValues = ReturnValue.ALL_OLD
};

// Simple inline usage - fire and forget
await table.Users.Put().WithRequest(sdkRequest).PutAsync();

// If you need response metadata, capture the builder first
var builder = table.Users.Put().WithRequest(sdkRequest);
await builder.PutAsync();
var capacity = builder.Response?.ConsumedCapacity;
```

### Update Operations

```csharp
// Create SDK request
var sdkRequest = new UpdateItemRequest
{
    TableName = "users",
    Key = new Dictionary<string, AttributeValue>
    {
        ["pk"] = new AttributeValue { S = "USER#123" }
    },
    UpdateExpression = "SET #name = :name, #updated = :updated",
    ExpressionAttributeNames = new Dictionary<string, string>
    {
        ["#name"] = "name",
        ["#updated"] = "updated_at"
    },
    ExpressionAttributeValues = new Dictionary<string, AttributeValue>
    {
        [":name"] = new AttributeValue { S = "Jane Doe" },
        [":updated"] = new AttributeValue { S = DateTime.UtcNow.ToString("o") }
    },
    ReturnValues = ReturnValue.ALL_NEW
};

// Simple inline usage
await table.Users.Update().WithRequest(sdkRequest).UpdateAsync();
```

### Delete Operations

```csharp
// Create SDK request
var sdkRequest = new DeleteItemRequest
{
    TableName = "users",
    Key = new Dictionary<string, AttributeValue>
    {
        ["pk"] = new AttributeValue { S = "USER#123" }
    },
    ConditionExpression = "#status = :status",
    ExpressionAttributeNames = new Dictionary<string, string>
    {
        ["#status"] = "status"
    },
    ExpressionAttributeValues = new Dictionary<string, AttributeValue>
    {
        [":status"] = new AttributeValue { S = "inactive" }
    }
};

// Simple inline usage
await table.Users.Delete().WithRequest(sdkRequest).DeleteAsync();
```

### Scan Operations

```csharp
// Create SDK request
var sdkRequest = new ScanRequest
{
    TableName = "users",
    FilterExpression = "#status = :status",
    ExpressionAttributeNames = new Dictionary<string, string>
    {
        ["#status"] = "status"
    },
    ExpressionAttributeValues = new Dictionary<string, AttributeValue>
    {
        [":status"] = new AttributeValue { S = "active" }
    },
    Limit = 100
};

// Simple inline usage
var users = await table.Users.Scan().WithRequest(sdkRequest).ToListAsync();
```

## Table-Level Convenience Methods

For simpler scenarios, table classes provide convenience methods that accept SDK requests directly:

### Synchronous Builder Methods

```csharp
// Returns a builder for further configuration or execution
var builder = table.Users.Get(sdkRequest);
var builder = table.Users.Query(sdkRequest);
var builder = table.Users.Scan(sdkRequest);
var builder = table.Users.Put(sdkRequest);
var builder = table.Users.Update(sdkRequest);
var builder = table.Users.Delete(sdkRequest);
```

### Async Convenience Methods

```csharp
// Execute directly and return hydrated entity/entities
var user = await table.Users.GetAsync(sdkRequest);
var orders = await table.Users.QueryAsync(sdkRequest);
var users = await table.Users.ScanAsync(sdkRequest);
```

## Direct Transaction Execution

Execute pre-built transaction requests directly:

### TransactWriteItems

```csharp
// Create SDK transaction request
var transactRequest = new TransactWriteItemsRequest
{
    TransactItems = new List<TransactWriteItem>
    {
        new TransactWriteItem
        {
            Put = new Put
            {
                TableName = "orders",
                Item = Order.ToDynamoDb(order)
            }
        },
        new TransactWriteItem
        {
            Update = new Update
            {
                TableName = "inventory",
                Key = new Dictionary<string, AttributeValue>
                {
                    ["pk"] = new AttributeValue { S = "PRODUCT#123" }
                },
                UpdateExpression = "SET quantity = quantity - :qty",
                ExpressionAttributeValues = new Dictionary<string, AttributeValue>
                {
                    [":qty"] = new AttributeValue { N = "1" }
                }
            }
        }
    }
};

// Execute directly
await DynamoDbTransactions.WriteAsync(client, transactRequest);
```

### TransactGetItems

```csharp
// Create SDK transaction get request
var transactRequest = new TransactGetItemsRequest
{
    TransactItems = new List<TransactGetItem>
    {
        new TransactGetItem
        {
            Get = new Get
            {
                TableName = "users",
                Key = new Dictionary<string, AttributeValue>
                {
                    ["pk"] = new AttributeValue { S = "USER#123" }
                }
            }
        },
        new TransactGetItem
        {
            Get = new Get
            {
                TableName = "orders",
                Key = new Dictionary<string, AttributeValue>
                {
                    ["pk"] = new AttributeValue { S = "ORDER#456" }
                }
            }
        }
    }
};

// Execute directly
var response = await DynamoDbTransactions.GetAsync(client, transactRequest);
```

## Direct Batch Execution

Execute pre-built batch requests directly:

### BatchWriteItem

```csharp
// Create SDK batch write request
var batchRequest = new BatchWriteItemRequest
{
    RequestItems = new Dictionary<string, List<WriteRequest>>
    {
        ["users"] = new List<WriteRequest>
        {
            new WriteRequest
            {
                PutRequest = new PutRequest
                {
                    Item = User.ToDynamoDb(user1)
                }
            },
            new WriteRequest
            {
                DeleteRequest = new DeleteRequest
                {
                    Key = new Dictionary<string, AttributeValue>
                    {
                        ["pk"] = new AttributeValue { S = "USER#old" }
                    }
                }
            }
        }
    }
};

// Execute directly
await DynamoDbBatch.WriteAsync(client, batchRequest);
```

### BatchGetItem

```csharp
// Create SDK batch get request
var batchRequest = new BatchGetItemRequest
{
    RequestItems = new Dictionary<string, KeysAndAttributes>
    {
        ["users"] = new KeysAndAttributes
        {
            Keys = new List<Dictionary<string, AttributeValue>>
            {
                new Dictionary<string, AttributeValue>
                {
                    ["pk"] = new AttributeValue { S = "USER#123" }
                },
                new Dictionary<string, AttributeValue>
                {
                    ["pk"] = new AttributeValue { S = "USER#456" }
                }
            }
        }
    }
};

// Execute directly
var response = await DynamoDbBatch.GetAsync(client, batchRequest);
```

## Migration Pattern

Use Direct SDK Request Passing to gradually migrate existing code:

### Before (Pure SDK)

```csharp
public async Task<User?> GetUserAsync(string userId)
{
    var request = new GetItemRequest
    {
        TableName = _tableName,
        Key = new Dictionary<string, AttributeValue>
        {
            ["pk"] = new AttributeValue { S = $"USER#{userId}" }
        }
    };
    
    var response = await _client.GetItemAsync(request);
    
    if (response.Item == null || response.Item.Count == 0)
        return null;
    
    // Manual mapping
    return new User
    {
        UserId = response.Item["pk"].S.Replace("USER#", ""),
        Name = response.Item["name"].S,
        Email = response.Item["email"].S
    };
}
```

### After (FluentDynamoDb with SDK Request)

```csharp
public async Task<User?> GetUserAsync(string userId)
{
    // Keep existing request construction
    var request = new GetItemRequest
    {
        TableName = _tableName,
        Key = new Dictionary<string, AttributeValue>
        {
            ["pk"] = new AttributeValue { S = $"USER#{userId}" }
        }
    };
    
    // Use FluentDynamoDb for entity hydration
    return await _table.Users.GetAsync(request);
}
```

### Final (Pure FluentDynamoDb)

```csharp
public async Task<User?> GetUserAsync(string userId)
{
    return await _table.Users.GetAsync(User.Keys.Pk(userId));
}
```

## Best Practices

1. **Use for Migration**: Direct SDK Request Passing is ideal for gradual migration, not as a primary API
2. **Prefer Builders**: For new code, use FluentDynamoDb builders for type safety and cleaner syntax
3. **Inline When Possible**: If you don't need response metadata, call the async method inline (e.g., `await table.Users.Get().WithRequest(req).GetItemAsync()`)
4. **Capture Builder for Metadata**: Only capture the builder to a variable when you need to access `builder.Response` after execution
5. **Table Name Consistency**: Ensure SDK request table names match your FluentDynamoDb table configuration

## See Also

- [Manual Patterns](ManualPatterns.md) - Low-level API usage
- [Client Configuration](ClientConfiguration.md) - DynamoDB client setup
- [Basic Operations](../core-features/BasicOperations.md) - Standard CRUD operations
- [Batch Operations](../core-features/BatchOperations.md) - Batch get and write
- [Transactions](../core-features/Transactions.md) - Transactional operations
