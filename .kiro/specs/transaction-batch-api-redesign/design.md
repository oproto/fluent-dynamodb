# Design Document

## Overview

This design document describes the architecture for redesigning transaction and batch operation APIs in Oproto.FluentDynamoDb. The new design introduces static entry points (`DynamoDbTransactions` and `DynamoDbBatch`) that accept existing request builders, enabling developers to leverage all fluent methods, string formatting, lambda expressions, and source-generated key accessors without code duplication.

The key innovation is reusing existing request builders (Put, Update, Delete, Get, ConditionCheck) within transaction and batch contexts through marker interfaces and typed `Add()` method overloads. This approach eliminates the need for parallel transaction-specific builders while maintaining compile-time safety.

## Architecture

### High-Level Component Diagram

```
┌─────────────────────────────────────────────────────────────┐
│                    Developer Code                            │
└────────────────────┬────────────────────────────────────────┘
                     │
                     ▼
┌──────────────────────┐         ┌──────────────────────┐
│ DynamoDbTransactions │         │   DynamoDbBatch      │
│  - Write             │         │   - Write            │
│  - Get               │         │   - Get              │
└──────────┬───────────┘         └──────────┬───────────┘
           │                                 │
           ▼                                 ▼
┌──────────────────────┐         ┌──────────────────────┐
│ TransactionWrite     │         │  BatchWrite          │
│ Builder              │         │  Builder             │
└──────────┬───────────┘         └──────────┬───────────┘
           │                                 │
           │ Add() overloads                 │ Add() overloads
           ▼                                 ▼
┌─────────────────────────────────────────────────────────────┐
│              Request Builders (Existing)                     │
│  - PutItemRequestBuilder<T>                                 │
│  - UpdateItemRequestBuilder<T>                              │
│  - DeleteItemRequestBuilder<T>                              │
│  - GetItemRequestBuilder<T>                                 │
│  - ConditionCheckBuilder<T> (New)                           │
└─────────────────────────────────────────────────────────────┘
           │
           │ Implements marker interfaces
           ▼
┌─────────────────────────────────────────────────────────────┐
│              Marker Interfaces                               │
│  - ITransactablePutBuilder                                  │
│  - ITransactableUpdateBuilder                               │
│  - ITransactableDeleteBuilder                               │
│  - ITransactableGetBuilder                                  │
│  - ITransactableConditionCheckBuilder                       │
└─────────────────────────────────────────────────────────────┘
```

### Key Design Principles

1. **Builder Reuse**: Existing request builders are reused in transaction/batch contexts
2. **Marker Interfaces**: Type-safe composition through marker interfaces
3. **Client Inference**: Automatic client extraction from first builder
4. **Compile-Time Safety**: Invalid operations prevented at compile time
5. **Zero Duplication**: No parallel transaction-specific builder implementations

## Components and Interfaces

### Marker Interfaces

These interfaces serve as type markers to enable type-safe `Add()` method overloads:

```csharp
namespace Oproto.FluentDynamoDb.Requests.Interfaces;

/// <summary>
/// Marker interface indicating a builder can be used in transaction/batch put operations.
/// </summary>
public interface ITransactablePutBuilder
{
    internal string GetTableName();
    internal Dictionary<string, AttributeValue> GetItem();
    internal string? GetConditionExpression();
    internal Dictionary<string, string>? GetExpressionAttributeNames();
    internal Dictionary<string, AttributeValue>? GetExpressionAttributeValues();
}

/// <summary>
/// Marker interface indicating a builder can be used in transaction/batch update operations.
/// </summary>
public interface ITransactableUpdateBuilder
{
    internal string GetTableName();
    internal Dictionary<string, AttributeValue> GetKey();
    internal string GetUpdateExpression();
    internal string? GetConditionExpression();
    internal Dictionary<string, string>? GetExpressionAttributeNames();
    internal Dictionary<string, AttributeValue>? GetExpressionAttributeValues();
    internal Task EncryptParametersIfNeededAsync(CancellationToken cancellationToken);
}

/// <summary>
/// Marker interface indicating a builder can be used in transaction/batch delete operations.
/// </summary>
public interface ITransactableDeleteBuilder
{
    internal string GetTableName();
    internal Dictionary<string, AttributeValue> GetKey();
    internal string? GetConditionExpression();
    internal Dictionary<string, string>? GetExpressionAttributeNames();
    internal Dictionary<string, AttributeValue>? GetExpressionAttributeValues();
}

/// <summary>
/// Marker interface indicating a builder can be used in transaction/batch get operations.
/// </summary>
public interface ITransactableGetBuilder
{
    internal string GetTableName();
    internal Dictionary<string, AttributeValue> GetKey();
    internal string? GetProjectionExpression();
    internal Dictionary<string, string>? GetExpressionAttributeNames();
    internal bool GetConsistentRead();
}

/// <summary>
/// Marker interface indicating a builder can be used in transaction condition check operations.
/// </summary>
public interface ITransactableConditionCheckBuilder
{
    internal string GetTableName();
    internal Dictionary<string, AttributeValue> GetKey();
    internal string GetConditionExpression();
    internal Dictionary<string, string>? GetExpressionAttributeNames();
    internal Dictionary<string, AttributeValue>? GetExpressionAttributeValues();
}
```

### Static Entry Points

#### DynamoDbTransactions

```csharp
namespace Oproto.FluentDynamoDb;

/// <summary>
/// Static entry point for composing DynamoDB transaction operations.
/// Provides fluent builders for TransactWriteItems and TransactGetItems operations.
/// </summary>
public static class DynamoDbTransactions
{
    /// <summary>
    /// Creates a new transaction write builder for composing atomic write operations.
    /// </summary>
    /// <returns>A new TransactionWriteBuilder instance.</returns>
    /// <example>
    /// <code>
    /// await DynamoDbTransactions.Write
    ///     .Add(table.Put(entity))
    ///     .Add(table.Update(pk, sk).Set(x => new { Value = "123" }))
    ///     .Add(table.Delete(pk2, sk2).Where("attribute_exists(id)"))
    ///     .ExecuteAsync();
    /// </code>
    /// </example>
    public static TransactionWriteBuilder Write => new();

    /// <summary>
    /// Creates a new transaction get builder for composing atomic read operations.
    /// </summary>
    /// <returns>A new TransactionGetBuilder instance.</returns>
    /// <example>
    /// <code>
    /// var response = await DynamoDbTransactions.Get
    ///     .Add(table.Get(pk, sk))
    ///     .Add(table2.Get(pk2, sk2).WithProjection("name, email"))
    ///     .ExecuteAsync();
    /// </code>
    /// </example>
    public static TransactionGetBuilder Get => new();
}
```

#### DynamoDbBatch

```csharp
namespace Oproto.FluentDynamoDb;

/// <summary>
/// Static entry point for composing DynamoDB batch operations.
/// Provides fluent builders for BatchWriteItem and BatchGetItem operations.
/// </summary>
public static class DynamoDbBatch
{
    /// <summary>
    /// Creates a new batch write builder for composing bulk write operations.
    /// </summary>
    /// <returns>A new BatchWriteBuilder instance.</returns>
    /// <example>
    /// <code>
    /// await DynamoDbBatch.Write
    ///     .Add(table.Put(entity1))
    ///     .Add(table.Put(entity2))
    ///     .Add(table.Delete(pk, sk))
    ///     .ExecuteAsync();
    /// </code>
    /// </example>
    public static BatchWriteBuilder Write => new();

    /// <summary>
    /// Creates a new batch get builder for composing bulk read operations.
    /// </summary>
    /// <returns>A new BatchGetBuilder instance.</returns>
    /// <example>
    /// <code>
    /// var response = await DynamoDbBatch.Get
    ///     .Add(table.Get(pk1, sk1))
    ///     .Add(table.Get(pk2, sk2))
    ///     .Add(table2.Get(pk3, sk3))
    ///     .ExecuteAsync();
    /// </code>
    /// </example>
    public static BatchGetBuilder Get => new();
}
```

### Transaction Write Builder

```csharp
namespace Oproto.FluentDynamoDb.Requests;

/// <summary>
/// Fluent builder for composing DynamoDB TransactWriteItems operations.
/// Accepts existing request builders and extracts transaction-compatible settings.
/// </summary>
public class TransactionWriteBuilder
{
    private readonly List<TransactWriteItem> _items = new();
    private IAmazonDynamoDB? _client;
    private IAmazonDynamoDB? _explicitClient;
    private ReturnConsumedCapacity? _returnConsumedCapacity;
    private ReturnItemCollectionMetrics? _returnItemCollectionMetrics;
    private string? _clientRequestToken;

    /// <summary>
    /// Adds a put operation to the transaction.
    /// </summary>
    public TransactionWriteBuilder Add<TEntity>(PutItemRequestBuilder<TEntity> builder)
        where TEntity : class
    {
        InferClientIfNeeded(builder);
        
        var item = new TransactWriteItem
        {
            Put = new Put
            {
                TableName = builder.GetTableName(),
                Item = builder.GetItem(),
                ConditionExpression = builder.GetConditionExpression(),
                ExpressionAttributeNames = builder.GetExpressionAttributeNames(),
                ExpressionAttributeValues = builder.GetExpressionAttributeValues()
            }
        };
        
        _items.Add(item);
        return this;
    }

    /// <summary>
    /// Adds an update operation to the transaction.
    /// </summary>
    public TransactionWriteBuilder Add<TEntity>(UpdateItemRequestBuilder<TEntity> builder)
        where TEntity : class
    {
        InferClientIfNeeded(builder);
        
        // Encryption will be handled before execution
        var item = new TransactWriteItem
        {
            Update = new Update
            {
                TableName = builder.GetTableName(),
                Key = builder.GetKey(),
                UpdateExpression = builder.GetUpdateExpression(),
                ConditionExpression = builder.GetConditionExpression(),
                ExpressionAttributeNames = builder.GetExpressionAttributeNames(),
                ExpressionAttributeValues = builder.GetExpressionAttributeValues()
            }
        };
        
        _items.Add(item);
        return this;
    }

    /// <summary>
    /// Adds a delete operation to the transaction.
    /// </summary>
    public TransactionWriteBuilder Add<TEntity>(DeleteItemRequestBuilder<TEntity> builder)
        where TEntity : class
    {
        InferClientIfNeeded(builder);
        
        var item = new TransactWriteItem
        {
            Delete = new Delete
            {
                TableName = builder.GetTableName(),
                Key = builder.GetKey(),
                ConditionExpression = builder.GetConditionExpression(),
                ExpressionAttributeNames = builder.GetExpressionAttributeNames(),
                ExpressionAttributeValues = builder.GetExpressionAttributeValues()
            }
        };
        
        _items.Add(item);
        return this;
    }

    /// <summary>
    /// Adds a condition check operation to the transaction.
    /// </summary>
    public TransactionWriteBuilder Add<TEntity>(ConditionCheckBuilder<TEntity> builder)
        where TEntity : class
    {
        InferClientIfNeeded(builder);
        
        var item = new TransactWriteItem
        {
            ConditionCheck = new ConditionCheck
            {
                TableName = builder.GetTableName(),
                Key = builder.GetKey(),
                ConditionExpression = builder.GetConditionExpression(),
                ExpressionAttributeNames = builder.GetExpressionAttributeNames(),
                ExpressionAttributeValues = builder.GetExpressionAttributeValues()
            }
        };
        
        _items.Add(item);
        return this;
    }

    /// <summary>
    /// Explicitly sets the DynamoDB client to use for this transaction.
    /// </summary>
    public TransactionWriteBuilder WithClient(IAmazonDynamoDB client)
    {
        _explicitClient = client;
        return this;
    }

    /// <summary>
    /// Configures the transaction to return consumed capacity information.
    /// </summary>
    public TransactionWriteBuilder ReturnConsumedCapacity(ReturnConsumedCapacity consumedCapacity)
    {
        _returnConsumedCapacity = consumedCapacity;
        return this;
    }

    /// <summary>
    /// Sets a client request token for idempotency.
    /// </summary>
    public TransactionWriteBuilder WithClientRequestToken(string token)
    {
        _clientRequestToken = token;
        return this;
    }

    /// <summary>
    /// Configures the transaction to return item collection metrics.
    /// </summary>
    public TransactionWriteBuilder ReturnItemCollectionMetrics()
    {
        _returnItemCollectionMetrics = ReturnItemCollectionMetrics.SIZE;
        return this;
    }

    /// <summary>
    /// Executes the transaction using the specified or inferred client.
    /// </summary>
    public async Task<TransactWriteItemsResponse> ExecuteAsync(
        IAmazonDynamoDB? client = null,
        CancellationToken cancellationToken = default)
    {
        var effectiveClient = client ?? _explicitClient ?? _client;
        
        if (effectiveClient == null)
        {
            throw new InvalidOperationException(
                "No DynamoDB client specified. Either pass a client to ExecuteAsync(), " +
                "call WithClient(), or add at least one request builder to infer the client.");
        }

        if (_items.Count == 0)
        {
            throw new InvalidOperationException(
                "Transaction contains no operations. Add at least one operation using Add().");
        }

        if (_items.Count > 100)
        {
            throw new ValidationException(
                $"Transaction contains {_items.Count} operations, but DynamoDB supports a maximum of 100 operations per transaction.");
        }

        var request = new TransactWriteItemsRequest
        {
            TransactItems = _items,
            ReturnConsumedCapacity = _returnConsumedCapacity,
            ReturnItemCollectionMetrics = _returnItemCollectionMetrics,
            ClientRequestToken = _clientRequestToken
        };

        return await effectiveClient.TransactWriteItemsAsync(request, cancellationToken);
    }

    private void InferClientIfNeeded<TEntity>(object builder) where TEntity : class
    {
        if (_client == null && _explicitClient == null)
        {
            // Extract client from builder using reflection or internal accessor
            _client = ExtractClientFromBuilder(builder);
        }
        else if (_explicitClient == null)
        {
            // Verify all builders use the same client
            var builderClient = ExtractClientFromBuilder(builder);
            if (builderClient != _client)
            {
                throw new InvalidOperationException(
                    "All request builders in a transaction must use the same DynamoDB client instance. " +
                    "Use WithClient() to explicitly specify a client if needed.");
            }
        }
    }

    private IAmazonDynamoDB ExtractClientFromBuilder(object builder)
    {
        // Use internal GetDynamoDbClient() method via reflection or direct access
        var method = builder.GetType().GetMethod("GetDynamoDbClient", 
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        
        if (method == null)
        {
            throw new InvalidOperationException(
                $"Unable to extract DynamoDB client from builder type {builder.GetType().Name}");
        }

        return (IAmazonDynamoDB)method.Invoke(builder, null)!;
    }
}
```

### Condition Check Builder (New)

```csharp
namespace Oproto.FluentDynamoDb.Requests;

/// <summary>
/// Fluent builder for DynamoDB condition check operations in transactions.
/// Condition checks verify conditions without modifying data.
/// </summary>
public class ConditionCheckBuilder<TEntity> :
    IWithKey<ConditionCheckBuilder<TEntity>>,
    IWithConditionExpression<ConditionCheckBuilder<TEntity>>,
    IWithAttributeNames<ConditionCheckBuilder<TEntity>>,
    IWithAttributeValues<ConditionCheckBuilder<TEntity>>,
    ITransactableConditionCheckBuilder
    where TEntity : class
{
    private readonly IAmazonDynamoDB _dynamoDbClient;
    private string _tableName;
    private Dictionary<string, AttributeValue> _key = new();
    private string? _conditionExpression;
    private readonly AttributeNameInternal _attrN = new();
    private readonly AttributeValueInternal _attrV = new();

    public ConditionCheckBuilder(IAmazonDynamoDB dynamoDbClient, string tableName)
    {
        _dynamoDbClient = dynamoDbClient;
        _tableName = tableName;
    }

    public AttributeNameInternal GetAttributeNameHelper() => _attrN;
    public AttributeValueInternal GetAttributeValueHelper() => _attrV;
    internal IAmazonDynamoDB GetDynamoDbClient() => _dynamoDbClient;

    public ConditionCheckBuilder<TEntity> SetConditionExpression(string expression)
    {
        if (string.IsNullOrEmpty(_conditionExpression))
        {
            _conditionExpression = expression;
        }
        else
        {
            _conditionExpression = $"({_conditionExpression}) AND ({expression})";
        }
        return this;
    }

    public ConditionCheckBuilder<TEntity> SetKey(Action<Dictionary<string, AttributeValue>> keyAction)
    {
        keyAction(_key);
        return this;
    }

    public ConditionCheckBuilder<TEntity> Self => this;

    public ConditionCheckBuilder<TEntity> ForTable(string tableName)
    {
        _tableName = tableName;
        return this;
    }

    // ITransactableConditionCheckBuilder implementation
    string ITransactableConditionCheckBuilder.GetTableName() => _tableName;
    Dictionary<string, AttributeValue> ITransactableConditionCheckBuilder.GetKey() => _key;
    string ITransactableConditionCheckBuilder.GetConditionExpression() => 
        _conditionExpression ?? throw new InvalidOperationException("Condition expression is required for condition checks");
    Dictionary<string, string>? ITransactableConditionCheckBuilder.GetExpressionAttributeNames() => 
        _attrN.AttributeNames.Count > 0 ? _attrN.AttributeNames : null;
    Dictionary<string, AttributeValue>? ITransactableConditionCheckBuilder.GetExpressionAttributeValues() => 
        _attrV.AttributeValues.Count > 0 ? _attrV.AttributeValues : null;
}
```

## Data Models

### Request Extraction Pattern

Each request builder implements its marker interface to expose internal state:

```csharp
// Example: PutItemRequestBuilder implements ITransactablePutBuilder
public class PutItemRequestBuilder<TEntity> : ITransactablePutBuilder
    where TEntity : class
{
    private PutItemRequest _req = new();
    private readonly IAmazonDynamoDB _dynamoDbClient;
    // ... existing fields ...

    // Explicit interface implementation
    string ITransactablePutBuilder.GetTableName() => _req.TableName;
    Dictionary<string, AttributeValue> ITransactablePutBuilder.GetItem() => _req.Item;
    string? ITransactablePutBuilder.GetConditionExpression() => _req.ConditionExpression;
    Dictionary<string, string>? ITransactablePutBuilder.GetExpressionAttributeNames() => 
        _attrN.AttributeNames.Count > 0 ? _attrN.AttributeNames : null;
    Dictionary<string, AttributeValue>? ITransactablePutBuilder.GetExpressionAttributeValues() => 
        _attrV.AttributeValues.Count > 0 ? _attrV.AttributeValues : null;
}
```

### Client Inference Flow

```
1. Developer calls DynamoDbTransactions.Write
2. Developer calls .Add(table.Put(entity))
3. TransactionWriteBuilder extracts client from PutItemRequestBuilder
4. TransactionWriteBuilder stores client reference
5. Developer calls .Add(table.Update(pk, sk).Set(...))
6. TransactionWriteBuilder verifies client matches stored client
7. Developer calls .ExecuteAsync()
8. TransactionWriteBuilder uses stored client for execution
```

## Error Handling

### Validation Errors

```csharp
public class TransactionWriteBuilder
{
    public async Task<TransactWriteItemsResponse> ExecuteAsync(...)
    {
        // Validate client
        if (effectiveClient == null)
        {
            throw new InvalidOperationException(
                "No DynamoDB client specified. Either pass a client to ExecuteAsync(), " +
                "call WithClient(), or add at least one request builder to infer the client.");
        }

        // Validate operations exist
        if (_items.Count == 0)
        {
            throw new InvalidOperationException(
                "Transaction contains no operations. Add at least one operation using Add().");
        }

        // Validate operation count
        if (_items.Count > 100)
        {
            throw new ValidationException(
                $"Transaction contains {_items.Count} operations, but DynamoDB supports " +
                $"a maximum of 100 operations per transaction.");
        }

        // Execute transaction
        return await effectiveClient.TransactWriteItemsAsync(request, cancellationToken);
    }
}
```

### Client Mismatch Detection

```csharp
private void InferClientIfNeeded<TEntity>(object builder) where TEntity : class
{
    if (_client == null && _explicitClient == null)
    {
        _client = ExtractClientFromBuilder(builder);
    }
    else if (_explicitClient == null)
    {
        var builderClient = ExtractClientFromBuilder(builder);
        if (builderClient != _client)
        {
            throw new InvalidOperationException(
                "All request builders in a transaction must use the same DynamoDB client instance. " +
                "Use WithClient() to explicitly specify a client if needed.");
        }
    }
}
```

## Testing Strategy

### Unit Tests

1. **Marker Interface Implementation Tests**
   - Verify all request builders implement appropriate marker interfaces
   - Test interface method implementations return correct values

2. **Transaction Builder Tests**
   - Test Add() overloads accept correct builder types
   - Test client inference from first builder
   - Test client mismatch detection
   - Test explicit client via WithClient()
   - Test client precedence (ExecuteAsync > WithClient > inferred)

3. **Validation Tests**
   - Test empty transaction throws exception
   - Test transaction with >100 operations throws exception
   - Test batch with >25 write operations throws exception
   - Test batch with >100 get operations throws exception

4. **Request Extraction Tests**
   - Test Put builder extraction preserves all settings
   - Test Update builder extraction preserves expressions
   - Test Delete builder extraction preserves conditions
   - Test Get builder extraction preserves projections
   - Test ConditionCheck builder extraction

5. **String Formatting Tests**
   - Test placeholders work in transaction context
   - Test attribute names/values preserved
   - Test multiple placeholders in single expression

6. **Lambda Expression Tests**
   - Test lambda-based Set() in transactions
   - Test lambda-based Where() in transactions
   - Test encryption metadata preserved

### Integration Tests

1. **End-to-End Transaction Tests**
   - Test successful multi-operation transaction
   - Test transaction rollback on condition failure
   - Test transaction with encrypted fields

2. **End-to-End Batch Tests**
   - Test batch write with multiple tables
   - Test batch get with multiple tables
   - Test unprocessed items handling

3. **Source Generator Integration**
   - Test generated Put/Update/Delete methods work in transactions
   - Test generated key methods work correctly
   - Test multi-entity table scenarios

## Response Deserialization for Get Operations

### Challenge

Transaction Get and Batch Get operations return lists of `Dictionary<string, AttributeValue>` items without type information. In an AOT-safe environment, we cannot use reflection to dynamically determine entity types. We need a compile-time safe approach that:

1. Preserves type safety
2. Works without reflection
3. Handles items from different tables/entity types
4. Provides a clean, fluent API

### Solution: Typed Response Wrapper

We introduce response wrapper classes that provide indexed access with explicit type parameters:

```csharp
namespace Oproto.FluentDynamoDb.Requests;

/// <summary>
/// Response wrapper for transaction get operations providing type-safe deserialization.
/// </summary>
public class TransactionGetResponse
{
    private readonly TransactGetItemsResponse _response;
    private readonly List<Dictionary<string, AttributeValue>> _items;

    internal TransactionGetResponse(TransactGetItemsResponse response)
    {
        _response = response;
        _items = response.Responses.Select(r => r.Item).ToList();
    }

    /// <summary>
    /// Gets the underlying AWS SDK response.
    /// </summary>
    public TransactGetItemsResponse RawResponse => _response;

    /// <summary>
    /// Gets the total number of items in the response.
    /// </summary>
    public int Count => _items.Count;

    /// <summary>
    /// Deserializes a single item at the specified index.
    /// </summary>
    /// <typeparam name="TEntity">The entity type to deserialize to.</typeparam>
    /// <param name="index">The zero-based index of the item.</param>
    /// <returns>The deserialized entity, or null if the item is missing.</returns>
    public TEntity? GetItem<TEntity>(int index) where TEntity : class
    {
        if (index < 0 || index >= _items.Count)
        {
            throw new ArgumentOutOfRangeException(nameof(index), 
                $"Index {index} is out of range. Response contains {_items.Count} items.");
        }

        var item = _items[index];
        if (item == null || item.Count == 0)
        {
            return null;
        }

        return TEntity.FromDynamoDb(item);
    }

    /// <summary>
    /// Deserializes multiple items of the same type at the specified indices.
    /// </summary>
    /// <typeparam name="TEntity">The entity type to deserialize to.</typeparam>
    /// <param name="indices">The zero-based indices of the items.</param>
    /// <returns>A list of deserialized entities (nulls for missing items).</returns>
    public List<TEntity?> GetItems<TEntity>(params int[] indices) where TEntity : class
    {
        return indices.Select(i => GetItem<TEntity>(i)).ToList();
    }

    /// <summary>
    /// Deserializes a contiguous range of items of the same type.
    /// </summary>
    /// <typeparam name="TEntity">The entity type to deserialize to.</typeparam>
    /// <param name="startIndex">The zero-based start index (inclusive).</param>
    /// <param name="endIndex">The zero-based end index (inclusive).</param>
    /// <returns>A list of deserialized entities (nulls for missing items).</returns>
    public List<TEntity?> GetItemsRange<TEntity>(int startIndex, int endIndex) where TEntity : class
    {
        if (startIndex < 0 || startIndex >= _items.Count)
        {
            throw new ArgumentOutOfRangeException(nameof(startIndex));
        }
        if (endIndex < startIndex || endIndex >= _items.Count)
        {
            throw new ArgumentOutOfRangeException(nameof(endIndex));
        }

        var result = new List<TEntity?>();
        for (int i = startIndex; i <= endIndex; i++)
        {
            result.Add(GetItem<TEntity>(i));
        }
        return result;
    }
}
```

### Batch Get Response

Similar pattern for batch get operations:

```csharp
/// <summary>
/// Response wrapper for batch get operations providing type-safe deserialization.
/// </summary>
public class BatchGetResponse
{
    private readonly BatchGetItemResponse _response;
    private readonly List<Dictionary<string, AttributeValue>> _items;
    private readonly Dictionary<string, KeysAndAttributes> _unprocessedKeys;

    internal BatchGetResponse(BatchGetItemResponse response, List<string> tableOrder)
    {
        _response = response;
        _unprocessedKeys = response.UnprocessedKeys;
        
        // Flatten items in the order they were added
        _items = new List<Dictionary<string, AttributeValue>>();
        foreach (var tableName in tableOrder)
        {
            if (response.Responses.TryGetValue(tableName, out var tableItems))
            {
                _items.AddRange(tableItems);
            }
        }
    }

    /// <summary>
    /// Gets the underlying AWS SDK response.
    /// </summary>
    public BatchGetItemResponse RawResponse => _response;

    /// <summary>
    /// Gets unprocessed keys that need to be retried.
    /// </summary>
    public Dictionary<string, KeysAndAttributes> UnprocessedKeys => _unprocessedKeys;

    /// <summary>
    /// Indicates whether there are unprocessed keys.
    /// </summary>
    public bool HasUnprocessedKeys => _unprocessedKeys.Count > 0;

    // Same GetItem, GetItems, GetItemsRange methods as TransactionGetResponse
}
```

### ExecuteAndMapAsync Overloads

For convenience with small numbers of items, provide tuple-returning overloads:

```csharp
public class TransactionGetBuilder
{
    // Existing ExecuteAsync returns wrapper
    public async Task<TransactionGetResponse> ExecuteAsync(
        IAmazonDynamoDB? client = null,
        CancellationToken cancellationToken = default)
    {
        var response = await effectiveClient.TransactGetItemsAsync(request, cancellationToken);
        return new TransactionGetResponse(response);
    }

    // Convenience overloads for 1-8 items
    public async Task<T1?> ExecuteAndMapAsync<T1>(
        IAmazonDynamoDB? client = null,
        CancellationToken cancellationToken = default)
        where T1 : class
    {
        var response = await ExecuteAsync(client, cancellationToken);
        return response.GetItem<T1>(0);
    }

    public async Task<(T1?, T2?)> ExecuteAndMapAsync<T1, T2>(
        IAmazonDynamoDB? client = null,
        CancellationToken cancellationToken = default)
        where T1 : class
        where T2 : class
    {
        var response = await ExecuteAsync(client, cancellationToken);
        return (response.GetItem<T1>(0), response.GetItem<T2>(1));
    }

    public async Task<(T1?, T2?, T3?)> ExecuteAndMapAsync<T1, T2, T3>(
        IAmazonDynamoDB? client = null,
        CancellationToken cancellationToken = default)
        where T1 : class
        where T2 : class
        where T3 : class
    {
        var response = await ExecuteAsync(client, cancellationToken);
        return (
            response.GetItem<T1>(0), 
            response.GetItem<T2>(1), 
            response.GetItem<T3>(2)
        );
    }

    // Continue up to 8 type parameters...
}
```

### Usage Examples

```csharp
// Example 1: Simple tuple destructuring
var (user, order) = await DynamoDbTransactions.Get
    .Add(userTable.Get(userId))
    .Add(orderTable.Get(orderId))
    .ExecuteAndMapAsync<User, Order>();

// Example 2: Indexed access for many items
var response = await DynamoDbTransactions.Get
    .Add(table.Get(pk1, sk1))
    .Add(table.Get(pk2, sk2))
    .Add(table.Get(pk3, sk3))
    .Add(table2.Get(pk4, sk4))
    .ExecuteAsync();

var users = response.GetItems<User>(0, 1, 2);
var order = response.GetItem<Order>(3);

// Example 3: Range access for same type
var response = await DynamoDbBatch.Get
    .Add(table.Get(pk1, sk1))
    .Add(table.Get(pk2, sk2))
    .Add(table.Get(pk3, sk3))
    .ExecuteAsync();

var users = response.GetItemsRange<User>(0, 2);

// Example 4: Handle unprocessed keys in batch
var response = await DynamoDbBatch.Get
    .Add(table.Get(pk1, sk1))
    // ... many more gets
    .ExecuteAsync();

if (response.HasUnprocessedKeys)
{
    // Retry logic here
}
```

## Encryption in Batch Write Operations

### Challenge

When entities with encrypted fields are put into batch write operations, the encryption must happen during the `ToDynamoDb` conversion, not during batch execution.

### Solution

The `PutItemRequestBuilder<TEntity>` already handles encryption when `WithItem(entity)` is called:

```csharp
public class PutItemRequestBuilder<TEntity>
{
    public PutItemRequestBuilder<TEntity> WithItem(TEntity entity)
    {
        // Encryption happens here via ToDynamoDb
        _req.Item = TEntity.ToDynamoDb(entity);
        return this;
    }
}
```

The batch write builder simply extracts the already-encrypted item:

```csharp
public class BatchWriteBuilder
{
    public BatchWriteBuilder Add<TEntity>(PutItemRequestBuilder<TEntity> builder)
        where TEntity : class
    {
        var tableName = builder.GetTableName();
        var item = builder.GetItem(); // Already encrypted
        
        // Add to batch request
        AddWriteRequest(tableName, new WriteRequest
        {
            PutRequest = new PutRequest { Item = item }
        });
        
        return this;
    }
}
```

No additional encryption logic is needed in the batch builder because encryption is handled at the entity-to-DynamoDB conversion layer.

## Migration from Old API

The old API will be removed entirely. No migration guide is needed as this is a breaking change.

### Old API (To Be Removed)

```csharp
// Old pattern - will be removed
await table.TransactWrite()
    .Put(table, put => put.WithItem(item))
    .Update(table, update => update.WithKey(...).Set(...))
    .ExecuteAsync();
```

### New API

```csharp
// New pattern
await DynamoDbTransactions.Write
    .Add(table.Put(item))
    .Add(table.Update(pk, sk).Set(...))
    .ExecuteAsync();

// New pattern with response deserialization
var (user, order) = await DynamoDbTransactions.Get
    .Add(userTable.Get(userId))
    .Add(orderTable.Get(orderId))
    .ExecuteAndMapAsync<User, Order>();
```
