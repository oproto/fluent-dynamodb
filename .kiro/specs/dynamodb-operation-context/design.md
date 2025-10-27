# Design Document: DynamoDB Operation Context

## Overview

The DynamoDB Operation Context feature provides a centralized, thread-safe mechanism for accessing DynamoDB operation metadata and response details after operations complete. Since async methods cannot use `out` or `ref` parameters, this design leverages .NET's `AsyncLocal<T>` to maintain operation context that flows through async call chains without explicit parameter passing.

This design also includes a comprehensive API cleanup to establish consistent patterns before the 1.0 release:

1. **Primary API**: Methods like `GetItemAsync()`, `ToListAsync()`, `PutAsync()`, `UpdateAsync()` return POCOs/void and populate AsyncLocal context
2. **Advanced API**: `ToDynamoDbResponseAsync()` returns raw AWS responses without populating context (for rare advanced scenarios)
3. **Removed**: Custom wrapper classes (`GetItemResponse<T>`, `QueryResponse<T>`, `ScanResponse<T>`) are deleted in favor of POCOs + context
4. **Unified**: Consolidates the existing `EncryptionContext` AsyncLocal implementation into the unified context object

## Architecture

### High-Level Design

```
┌─────────────────────────────────────────────────────────────┐
│                    Application Code                          │
│  // Primary API (populates context)                         │
│  var items = await table.Query<T>().ToListAsync();          │
│  var item = await table.Get<T>().GetItemAsync();            │
│  await table.Put<T>().PutAsync(entity);                     │
│                                                              │
│  // Access metadata via context                             │
│  var context = DynamoDbOperationContext.Current;            │
│  var capacity = context?.ConsumedCapacity;                  │
│                                                              │
│  // Advanced API (no context, raw response)                 │
│  var response = await table.Query<T>()                      │
│      .ToDynamoDbResponseAsync();                            │
└─────────────────────────────────────────────────────────────┘
                            │
                            ▼
┌─────────────────────────────────────────────────────────────┐
│              Request Builders & Extensions                   │
│  Primary API:                                               │
│  - GetItemAsync<T>() → T?                                   │
│  - ToListAsync<T>() → List<T>                               │
│  - PutAsync<T>() → void                                     │
│  - UpdateAsync() → void                                     │
│                                                              │
│  Advanced API:                                              │
│  - ToDynamoDbResponseAsync() → AWS Response                 │
└─────────────────────────────────────────────────────────────┘
                            │
                            ▼
┌─────────────────────────────────────────────────────────────┐
│           Context Population (Primary API Only)              │
│  - Captures response metadata                               │
│  - Populates DynamoDbOperationContext                       │
│  - Sets AsyncLocal<OperationContextData>                   │
└─────────────────────────────────────────────────────────────┘
                            │
                            ▼
┌─────────────────────────────────────────────────────────────┐
│                  AWS DynamoDB Client                         │
│  - QueryAsync(), GetItemAsync(), etc.                       │
└─────────────────────────────────────────────────────────────┘
```

### Context Lifecycle

1. **Before Operation**: Context is null or contains data from previous operation
2. **During Operation**: AWS SDK executes the request
3. **After Operation**: Response is captured and context is populated
4. **Access**: Application code accesses `DynamoDbOperationContext.Current`
5. **Next Operation**: Context is replaced with new operation data

### Thread Safety & Isolation

`AsyncLocal<T>` provides automatic isolation:
- Each async flow has its own context instance
- Concurrent operations don't interfere with each other
- Context flows through `await` calls within the same logical operation
- No manual cleanup required (garbage collected when out of scope)

## API Refactoring Strategy

### Current State (Inconsistent)

The library currently has an inconsistent mix of patterns:

1. **Base builders** return raw AWS responses: `QueryResponse`, `GetItemResponse`, etc.
2. **Extension methods** return custom wrappers: `GetItemResponse<T>`, or POCOs: `List<T>`
3. **Custom wrapper classes** exist: `GetItemResponse<T>`, `QueryResponse<T>`, `ScanResponse<T>`

### Target State (Consistent)

**Primary API** (99% of use cases):
- Returns POCOs or void
- Populates AsyncLocal context with metadata
- Clean, simple method signatures

**Advanced API** (rare scenarios):
- Returns raw AWS SDK responses
- Does NOT populate context
- For users who need full AWS response control

### Method Naming Convention

| Operation | Primary API Method | Advanced API Method | Return Type (Primary) | Return Type (Advanced) |
|-----------|-------------------|---------------------|----------------------|------------------------|
| GetItem | `GetItemAsync<T>()` | `ToDynamoDbResponseAsync()` | `T?` | `GetItemResponse` |
| Query (list) | `ToListAsync<T>()` | `ToDynamoDbResponseAsync()` | `List<T>` | `QueryResponse` |
| Query (composite) | `ToCompositeEntityAsync<T>()` | `ToDynamoDbResponseAsync()` | `T?` | `QueryResponse` |
| Query (composite list) | `ToCompositeEntityListAsync<T>()` | `ToDynamoDbResponseAsync()` | `List<T>` | `QueryResponse` |
| Scan (list) | `ToListAsync<T>()` | `ToDynamoDbResponseAsync()` | `List<T>` | `ScanResponse` |
| Scan (composite list) | `ToCompositeEntityListAsync<T>()` | `ToDynamoDbResponseAsync()` | `List<T>` | `ScanResponse` |
| PutItem | `PutAsync<T>()` | `ToDynamoDbResponseAsync()` | `void` | `PutItemResponse` |
| UpdateItem | `UpdateAsync()` | `ToDynamoDbResponseAsync()` | `void` | `UpdateItemResponse` |
| DeleteItem | `DeleteAsync()` | `ToDynamoDbResponseAsync()` | `void` | `DeleteItemResponse` |

### Deprecated/Removed

- `ExecuteAsync()` on base builders → Replaced by `ToDynamoDbResponseAsync()`
- `ExecuteAsync<T>()` on GetItemRequestBuilder → Replaced by `GetItemAsync<T>()`
- Custom wrapper classes: `GetItemResponse<T>`, `QueryResponse<T>`, `ScanResponse<T>` → Deleted
- `ResponseMetadata` class (if custom) → Deleted (use AWS SDK's ResponseMetadata)

## Components and Interfaces

### 1. DynamoDbOperationContext (Public API)

```csharp
namespace Oproto.FluentDynamoDb.Storage;

/// <summary>
/// Provides thread-safe ambient context for DynamoDB operation metadata.
/// Uses AsyncLocal to ensure context flows through async calls without leaking across threads.
/// </summary>
public static class DynamoDbOperationContext
{
    private static readonly AsyncLocal<OperationContextData?> _current = new();

    /// <summary>
    /// Gets the current operation context data, or null if no operation has executed.
    /// </summary>
    public static OperationContextData? Current
    {
        get => _current.Value;
        internal set => _current.Value = value;
    }

    /// <summary>
    /// Clears the current operation context.
    /// This is typically not needed as context is automatically replaced on each operation.
    /// </summary>
    public static void Clear()
    {
        _current.Value = null;
    }

    /// <summary>
    /// Gets or sets the encryption context identifier for the current async flow.
    /// This is a convenience accessor that delegates to the unified context.
    /// </summary>
    public static string? EncryptionContextId
    {
        get => _current.Value?.EncryptionContextId;
        set
        {
            var data = _current.Value ?? new OperationContextData();
            data.EncryptionContextId = value;
            _current.Value = data;
        }
    }
}
```

### 2. OperationContextData (Data Container)

```csharp
namespace Oproto.FluentDynamoDb.Storage;

/// <summary>
/// Contains metadata and response details from a DynamoDB operation.
/// </summary>
public class OperationContextData
{
    // === Operation Metadata ===
    
    /// <summary>
    /// The type of operation that was executed (Query, GetItem, UpdateItem, etc.).
    /// </summary>
    public string? OperationType { get; set; }

    /// <summary>
    /// The table name the operation was executed against.
    /// </summary>
    public string? TableName { get; set; }

    /// <summary>
    /// The index name if the operation used an index.
    /// </summary>
    public string? IndexName { get; set; }

    // === Capacity & Performance ===
    
    /// <summary>
    /// Consumed capacity information from the operation.
    /// </summary>
    public ConsumedCapacity? ConsumedCapacity { get; set; }

    /// <summary>
    /// Number of items returned by the operation (for Query/Scan).
    /// </summary>
    public int? ItemCount { get; set; }

    /// <summary>
    /// Number of items evaluated before applying filter expression (for Query/Scan).
    /// </summary>
    public int? ScannedCount { get; set; }

    /// <summary>
    /// Item collection metrics (for operations that modify indexed attributes).
    /// </summary>
    public ItemCollectionMetrics? ItemCollectionMetrics { get; set; }

    // === Pagination ===
    
    /// <summary>
    /// The last evaluated key for pagination (for Query/Scan).
    /// Null if there are no more pages.
    /// </summary>
    public Dictionary<string, AttributeValue>? LastEvaluatedKey { get; set; }

    // === Raw Response Data ===
    
    /// <summary>
    /// Raw items from Query/Scan operations before deserialization.
    /// This is a reference to the response object's Items collection.
    /// </summary>
    public List<Dictionary<string, AttributeValue>>? RawItems { get; set; }

    /// <summary>
    /// Raw item from GetItem operation before deserialization.
    /// This is a reference to the response object's Item dictionary.
    /// </summary>
    public Dictionary<string, AttributeValue>? RawItem { get; set; }

    // === Pre/Post Operation Values ===
    
    /// <summary>
    /// Attribute values before the operation (from ReturnValues = ALL_OLD or UPDATED_OLD).
    /// Available for UpdateItem, DeleteItem, and PutItem operations.
    /// </summary>
    public Dictionary<string, AttributeValue>? PreOperationValues { get; set; }

    /// <summary>
    /// Attribute values after the operation (from ReturnValues = ALL_NEW or UPDATED_NEW).
    /// Available for UpdateItem and PutItem operations.
    /// </summary>
    public Dictionary<string, AttributeValue>? PostOperationValues { get; set; }

    // === Encryption Context (Migrated) ===
    
    /// <summary>
    /// Encryption context identifier (e.g., tenant ID, customer ID).
    /// This replaces the standalone EncryptionContext.Current property.
    /// </summary>
    public string? EncryptionContextId { get; set; }

    // === Response Metadata ===
    
    /// <summary>
    /// AWS response metadata (request ID, etc.).
    /// </summary>
    public ResponseMetadata? ResponseMetadata { get; set; }

    // === Deserialization Helpers ===
    
    /// <summary>
    /// Deserializes the RawItem to a strongly-typed entity.
    /// </summary>
    public T? DeserializeRawItem<T>() where T : class, IDynamoDbEntity
    {
        if (RawItem == null || !T.MatchesEntity(RawItem))
            return null;
        
        return T.FromDynamoDb<T>(RawItem);
    }

    /// <summary>
    /// Deserializes the RawItems collection to strongly-typed entities.
    /// </summary>
    public List<T> DeserializeRawItems<T>() where T : class, IDynamoDbEntity
    {
        if (RawItems == null)
            return new List<T>();
        
        return RawItems
            .Where(T.MatchesEntity)
            .Select(item => T.FromDynamoDb<T>(item))
            .ToList();
    }

    /// <summary>
    /// Deserializes the PreOperationValues to a strongly-typed entity.
    /// </summary>
    public T? DeserializePreOperationValue<T>() where T : class, IDynamoDbEntity
    {
        if (PreOperationValues == null || !T.MatchesEntity(PreOperationValues))
            return null;
        
        return T.FromDynamoDb<T>(PreOperationValues);
    }

    /// <summary>
    /// Deserializes the PostOperationValues to a strongly-typed entity.
    /// </summary>
    public T? DeserializePostOperationValue<T>() where T : class, IDynamoDbEntity
    {
        if (PostOperationValues == null || !T.MatchesEntity(PostOperationValues))
            return null;
        
        return T.FromDynamoDb<T>(PostOperationValues);
    }
}
```

### 3. Context Population Strategy

Context population happens **only in Primary API methods**. Advanced API methods do not populate context.

#### A. Primary API Methods (GetItemAsync, ToListAsync, PutAsync, etc.)

These methods call the AWS SDK directly and populate context:

```csharp
// In EnhancedExecuteAsyncExtensions.GetItemAsync<T>()
public static async Task<T?> GetItemAsync<T>(
    this GetItemRequestBuilder<T> builder,
    CancellationToken cancellationToken = default)
    where T : class, IDynamoDbEntity
{
    var request = builder.ToGetItemRequest();
    var response = await builder.GetDynamoDbClient().GetItemAsync(request, cancellationToken);
    
    // Populate context
    DynamoDbOperationContext.Current = new OperationContextData
    {
        OperationType = "GetItem",
        TableName = request.TableName,
        ConsumedCapacity = response.ConsumedCapacity,
        RawItem = response.Item,
        ResponseMetadata = response.ResponseMetadata
    };
    
    // Return POCO
    if (response.Item == null || !T.MatchesEntity(response.Item))
        return null;
    
    return T.FromDynamoDb<T>(response.Item);
}
```

```csharp
// In EnhancedExecuteAsyncExtensions.ToListAsync<T>()
public static async Task<List<T>> ToListAsync<T>(
    this QueryRequestBuilder<T> builder,
    CancellationToken cancellationToken = default)
    where T : class, IDynamoDbEntity
{
    var request = builder.ToQueryRequest();
    var response = await builder.GetDynamoDbClient().QueryAsync(request, cancellationToken);
    
    // Populate context
    DynamoDbOperationContext.Current = new OperationContextData
    {
        OperationType = "Query",
        TableName = request.TableName,
        IndexName = request.IndexName,
        ConsumedCapacity = response.ConsumedCapacity,
        ItemCount = response.Count,
        ScannedCount = response.ScannedCount,
        LastEvaluatedKey = response.LastEvaluatedKey,
        RawItems = response.Items,
        ResponseMetadata = response.ResponseMetadata
    };
    
    // Return POCOs
    return response.Items
        .Where(T.MatchesEntity)
        .Select(item => T.FromDynamoDb<T>(item))
        .ToList();
}
```

```csharp
// In EnhancedExecuteAsyncExtensions.PutAsync<T>()
public static async Task PutAsync<T>(
    this PutItemRequestBuilder<T> builder,
    CancellationToken cancellationToken = default)
    where T : class
{
    var request = builder.ToPutItemRequest();
    var response = await builder.GetDynamoDbClient().PutItemAsync(request, cancellationToken);
    
    // Populate context
    DynamoDbOperationContext.Current = new OperationContextData
    {
        OperationType = "PutItem",
        TableName = request.TableName,
        ConsumedCapacity = response.ConsumedCapacity,
        ItemCollectionMetrics = response.ItemCollectionMetrics,
        PreOperationValues = response.Attributes, // If ReturnValues was set
        ResponseMetadata = response.ResponseMetadata
    };
    
    // Return void
}
```

#### B. Advanced API Methods (ToDynamoDbResponseAsync)

These methods return raw AWS responses and **do not populate context**:

```csharp
// In QueryRequestBuilder
public async Task<QueryResponse> ToDynamoDbResponseAsync(CancellationToken cancellationToken = default)
{
    var request = ToQueryRequest();
    var response = await _dynamoDbClient.QueryAsync(request, cancellationToken);
    
    // NO context population - return raw response
    return response;
}
```

This keeps the advanced API clean for users who want full control without side effects.

### 4. AWS Response Extension Methods

To make the advanced API more convenient, we'll add extension methods to AWS SDK response classes:

```csharp
namespace Oproto.FluentDynamoDb.Requests.Extensions;

/// <summary>
/// Extension methods for AWS DynamoDB response objects to enable entity deserialization.
/// These are useful when using the advanced ToDynamoDbResponseAsync() API.
/// </summary>
public static class DynamoDbResponseExtensions
{
    // === QueryResponse Extensions ===
    
    /// <summary>
    /// Converts QueryResponse items to a list of strongly-typed entities (1:1 mapping).
    /// </summary>
    public static List<T> ToList<T>(this QueryResponse response)
        where T : class, IDynamoDbEntity
    {
        return response.Items
            .Where(T.MatchesEntity)
            .Select(item => T.FromDynamoDb<T>(item))
            .ToList();
    }
    
    /// <summary>
    /// Converts QueryResponse items to composite entities (N:1 mapping).
    /// </summary>
    public static List<T> ToCompositeEntityList<T>(this QueryResponse response)
        where T : class, IDynamoDbEntity
    {
        return response.Items
            .Where(T.MatchesEntity)
            .GroupBy(T.GetPartitionKey)
            .Select(group => group.Count() == 1
                ? T.FromDynamoDb<T>(group.First())
                : T.FromDynamoDb<T>(group.ToList()))
            .ToList();
    }
    
    /// <summary>
    /// Converts QueryResponse items to a single composite entity (N:1 mapping).
    /// </summary>
    public static T? ToCompositeEntity<T>(this QueryResponse response)
        where T : class, IDynamoDbEntity
    {
        var matchingItems = response.Items.Where(T.MatchesEntity).ToList();
        if (matchingItems.Count == 0)
            return null;
        
        return T.FromDynamoDb<T>(matchingItems);
    }
    
    // === ScanResponse Extensions ===
    
    /// <summary>
    /// Converts ScanResponse items to a list of strongly-typed entities (1:1 mapping).
    /// </summary>
    public static List<T> ToList<T>(this ScanResponse response)
        where T : class, IDynamoDbEntity
    {
        return response.Items
            .Where(T.MatchesEntity)
            .Select(item => T.FromDynamoDb<T>(item))
            .ToList();
    }
    
    /// <summary>
    /// Converts ScanResponse items to composite entities (N:1 mapping).
    /// </summary>
    public static List<T> ToCompositeEntityList<T>(this ScanResponse response)
        where T : class, IDynamoDbEntity
    {
        return response.Items
            .Where(T.MatchesEntity)
            .GroupBy(T.GetPartitionKey)
            .Select(group => group.Count() == 1
                ? T.FromDynamoDb<T>(group.First())
                : T.FromDynamoDb<T>(group.ToList()))
            .ToList();
    }
    
    // === GetItemResponse Extensions ===
    
    /// <summary>
    /// Converts GetItemResponse item to a strongly-typed entity.
    /// </summary>
    public static T? ToEntity<T>(this GetItemResponse response)
        where T : class, IDynamoDbEntity
    {
        if (response.Item == null || !T.MatchesEntity(response.Item))
            return null;
        
        return T.FromDynamoDb<T>(response.Item);
    }
    
    // === UpdateItemResponse Extensions ===
    
    /// <summary>
    /// Converts UpdateItemResponse Attributes (pre-operation values) to a strongly-typed entity.
    /// Only applicable when ReturnValues is set to ALL_OLD or UPDATED_OLD.
    /// </summary>
    public static T? ToPreOperationEntity<T>(this UpdateItemResponse response)
        where T : class, IDynamoDbEntity
    {
        if (response.Attributes == null || !T.MatchesEntity(response.Attributes))
            return null;
        
        return T.FromDynamoDb<T>(response.Attributes);
    }
    
    /// <summary>
    /// Converts UpdateItemResponse Attributes (post-operation values) to a strongly-typed entity.
    /// Only applicable when ReturnValues is set to ALL_NEW or UPDATED_NEW.
    /// </summary>
    public static T? ToPostOperationEntity<T>(this UpdateItemResponse response)
        where T : class, IDynamoDbEntity
    {
        if (response.Attributes == null || !T.MatchesEntity(response.Attributes))
            return null;
        
        return T.FromDynamoDb<T>(response.Attributes);
    }
    
    // === DeleteItemResponse Extensions ===
    
    /// <summary>
    /// Converts DeleteItemResponse Attributes (pre-deletion values) to a strongly-typed entity.
    /// Only applicable when ReturnValues is set to ALL_OLD.
    /// </summary>
    public static T? ToPreOperationEntity<T>(this DeleteItemResponse response)
        where T : class, IDynamoDbEntity
    {
        if (response.Attributes == null || !T.MatchesEntity(response.Attributes))
            return null;
        
        return T.FromDynamoDb<T>(response.Attributes);
    }
    
    // === PutItemResponse Extensions ===
    
    /// <summary>
    /// Converts PutItemResponse Attributes (pre-operation values) to a strongly-typed entity.
    /// Only applicable when ReturnValues is set to ALL_OLD.
    /// </summary>
    public static T? ToPreOperationEntity<T>(this PutItemResponse response)
        where T : class, IDynamoDbEntity
    {
        if (response.Attributes == null || !T.MatchesEntity(response.Attributes))
            return null;
        
        return T.FromDynamoDb<T>(response.Attributes);
    }
    
    // === Blob Provider Overloads ===
    
    // Add async overloads for all methods above that accept IBlobStorageProvider
    // for entities with [BlobReference] attributes
    // (Similar pattern to existing EnhancedExecuteAsyncExtensions)
}
```

### 5. Encryption Context Migration

The existing `EncryptionContext` class will be updated to delegate to the unified context:

```csharp
namespace Oproto.FluentDynamoDb.Storage;

/// <summary>
/// Provides thread-safe ambient context for encryption operations.
/// This class now delegates to DynamoDbOperationContext for unified context management.
/// </summary>
[Obsolete("Use DynamoDbOperationContext.EncryptionContextId instead. This class will be removed in a future version.")]
public static class EncryptionContext
{
    /// <summary>
    /// Gets or sets the current encryption context identifier.
    /// This property now delegates to DynamoDbOperationContext.EncryptionContextId.
    /// </summary>
    public static string? Current
    {
        get => DynamoDbOperationContext.EncryptionContextId;
        set => DynamoDbOperationContext.EncryptionContextId = value;
    }

    /// <summary>
    /// Gets the effective encryption context, checking both operation-specific and ambient contexts.
    /// </summary>
    internal static string? GetEffectiveContext()
    {
        // Check for operation-specific context first
        var operationContext = Requests.Extensions.EncryptionExtensions.GetOperationContext();
        if (operationContext != null)
        {
            return operationContext;
        }

        // Fall back to ambient context
        return Current;
    }
}
```

## Data Models

### Operation Type Enumeration

While we store operation type as a string for flexibility, common values include:

- `"Query"`
- `"Scan"`
- `"GetItem"`
- `"PutItem"`
- `"UpdateItem"`
- `"DeleteItem"`
- `"BatchGetItem"`
- `"BatchWriteItem"`
- `"TransactGetItems"`
- `"TransactWriteItems"`

### Memory Considerations

The context stores **references** to response objects, not deep copies:

- `RawItems` → Reference to `QueryResponse.Items`
- `RawItem` → Reference to `GetItemResponse.Item`
- `ConsumedCapacity` → Reference to response object
- `LastEvaluatedKey` → Reference to response object

This means:
- **No additional memory allocation** for raw data
- **Same memory footprint** as keeping the response object
- **Garbage collected** when context goes out of scope
- **Replaced** on each new operation (no accumulation)

## Error Handling

### Exception During Operation

If an operation throws an exception, context is **not populated**:

```csharp
public async Task<QueryResponse> ExecuteAsync(CancellationToken cancellationToken = default)
{
    var request = ToQueryRequest();
    
    try
    {
        var response = await _dynamoDbClient.QueryAsync(request, cancellationToken);
        
        // Only populate on success
        DynamoDbOperationContext.Current = new OperationContextData { ... };
        
        return response;
    }
    catch (Exception ex)
    {
        // Context remains unchanged (previous operation or null)
        _logger?.LogError(ex, "Query failed");
        throw;
    }
}
```

### Null Safety

All context properties are nullable. Application code should check for null:

```csharp
var context = DynamoDbOperationContext.Current;
if (context?.ConsumedCapacity != null)
{
    var capacity = context.ConsumedCapacity.CapacityUnits;
}
```

### Deserialization Errors

Deserialization helper methods handle errors gracefully:

```csharp
public T? DeserializeRawItem<T>() where T : class, IDynamoDbEntity
{
    try
    {
        if (RawItem == null || !T.MatchesEntity(RawItem))
            return null;
        
        return T.FromDynamoDb<T>(RawItem);
    }
    catch (Exception ex)
    {
        throw new DynamoDbMappingException(
            $"Failed to deserialize raw item to {typeof(T).Name}. Error: {ex.Message}", ex);
    }
}
```

## Testing Strategy

### Unit Tests

1. **Context Population Tests**
   - Verify context is populated after successful operations
   - Verify context contains correct metadata
   - Verify context is not populated on exception

2. **Isolation Tests**
   - Verify concurrent operations have separate contexts
   - Verify context flows through async calls
   - Verify context doesn't leak across async boundaries

3. **Deserialization Tests**
   - Verify helper methods correctly deserialize entities
   - Verify null handling
   - Verify error handling

4. **Encryption Context Migration Tests**
   - Verify backward compatibility with EncryptionContext
   - Verify delegation to unified context
   - Verify GetEffectiveContext() behavior

### Integration Tests

1. **Real Operation Tests**
   - Execute Query and verify context
   - Execute GetItem and verify context
   - Execute UpdateItem with ReturnValues and verify pre/post values

2. **Extension Method Tests**
   - Verify ToListAsync<T>() populates context
   - Verify ExecuteAsync<T>() populates context
   - Verify context accessible after extension methods

3. **Memory Tests**
   - Verify no memory leaks
   - Verify context is garbage collected
   - Verify references (not copies) are stored

## Implementation Phases

### Phase 1: Core Infrastructure
- Create `OperationContextData` class
- Create `DynamoDbOperationContext` static class with AsyncLocal storage
- Add helper methods for accessing DynamoDB client from builders

### Phase 2: Primary API - Extension Methods
- Implement `GetItemAsync<T>()` (replaces `ExecuteAsync<T>()`)
- Implement `ToListAsync<T>()` with context population
- Implement `ToCompositeEntityAsync<T>()` with context population
- Implement `ToCompositeEntityListAsync<T>()` with context population
- Implement `PutAsync<T>()` (new method)
- Implement `UpdateAsync()` (new method)
- Implement `DeleteAsync()` (new method)
- Add blob provider overloads for all methods

### Phase 3: Advanced API - ToDynamoDbResponseAsync
- Rename `ExecuteAsync()` to `ToDynamoDbResponseAsync()` on QueryRequestBuilder
- Rename `ExecuteAsync()` to `ToDynamoDbResponseAsync()` on GetItemRequestBuilder
- Rename `ExecuteAsync()` to `ToDynamoDbResponseAsync()` on ScanRequestBuilder
- Rename `ExecuteAsync()` to `ToDynamoDbResponseAsync()` on UpdateItemRequestBuilder
- Rename `ExecuteAsync()` to `ToDynamoDbResponseAsync()` on DeleteItemRequestBuilder
- Rename `ExecuteAsync()` to `ToDynamoDbResponseAsync()` on PutItemRequestBuilder
- Ensure these methods do NOT populate context

### Phase 3.5: AWS Response Extension Methods
- Create `DynamoDbResponseExtensions` class
- Implement `ToList<T>()` for QueryResponse and ScanResponse
- Implement `ToCompositeEntityList<T>()` for QueryResponse and ScanResponse
- Implement `ToCompositeEntity<T>()` for QueryResponse
- Implement `ToEntity<T>()` for GetItemResponse
- Implement `ToPreOperationEntity<T>()` for UpdateItemResponse, DeleteItemResponse, PutItemResponse
- Implement `ToPostOperationEntity<T>()` for UpdateItemResponse
- Add blob provider async overloads for all methods

### Phase 4: Remove Deprecated Code
- Delete `GetItemResponse<T>` class
- Delete `QueryResponse<T>` class (if exists)
- Delete `ScanResponse<T>` class (if exists)
- Delete custom `ResponseMetadata` class (if exists)
- Remove old `ExecuteAsync()` methods (or mark obsolete with clear migration path)

### Phase 5: Encryption Context Migration
- Update `EncryptionContext` to delegate to unified context
- Mark old implementation as obsolete
- Update internal usages to use `DynamoDbOperationContext.EncryptionContextId`

### Phase 6: Batch & Transaction Support
- Implement Primary API methods for BatchGetItem
- Implement Primary API methods for BatchWriteItem
- Implement Primary API methods for TransactGetItems
- Implement Primary API methods for TransactWriteItems
- Add context population for batch/transaction operations

### Phase 7: Update Tests
- Update all unit tests to use new API
- Add tests for context population
- Add tests for context isolation
- Add tests for deserialization helpers
- Update integration tests

### Phase 8: Update FluentResults Integration
- Update `ExecuteAsyncResult<T>()` to call `GetItemAsync<T>()` instead of `ExecuteAsync<T>()`
- Update `ToListAsyncResult<T>()` to match new signatures
- Update `ToCompositeEntityAsyncResult<T>()` to match new signatures
- Update `ToCompositeEntityListAsyncResult<T>()` to match new signatures
- Add `PutAsyncResult<T>()` wrapping `PutAsync<T>()`
- Add `UpdateAsyncResult()` wrapping `UpdateAsync()`
- Add `DeleteAsyncResult()` wrapping `DeleteAsync()`
- Remove `WithItemResult<T>()` (no longer needed with PutAsync)
- Remove `ExecuteAsyncResult<T>()` for PutItemRequestBuilder
- Update FluentResults tests

### Phase 9: Update Examples & Documentation
- Update all code examples to use new API
- Update XML documentation
- Update README with migration guide
- Add usage examples for context access
- Document breaking changes
- Update FluentResults README

## Usage Examples

### Example 1: Basic GetItem (New API)

```csharp
// Primary API - returns POCO, populates context
var transaction = await table.Get<Transaction>()
    .WithKey("id", "123")
    .ReturnTotalConsumedCapacity()
    .GetItemAsync();

// Access metadata via context
var context = DynamoDbOperationContext.Current;
if (context?.ConsumedCapacity != null)
{
    _logger.LogInformation(
        "GetItem consumed {Capacity} RCUs",
        context.ConsumedCapacity.CapacityUnits);
}
```

### Example 2: Query with Context (New API)

```csharp
// Primary API - returns List<T>, populates context
var items = await table.Query<Transaction>()
    .Where("pk = :pk")
    .WithValue(":pk", "USER#123")
    .ReturnTotalConsumedCapacity()
    .ToListAsync();

var context = DynamoDbOperationContext.Current;
_logger.LogInformation(
    "Query returned {Count} items, scanned {Scanned}, consumed {Capacity} RCUs",
    context?.ItemCount ?? 0,
    context?.ScannedCount ?? 0,
    context?.ConsumedCapacity?.CapacityUnits ?? 0);
```

### Example 3: PutItem (New API)

```csharp
var transaction = new Transaction { Id = "123", Status = "PENDING" };

// Primary API - returns void, populates context
await table.Put<Transaction>()
    .WithItem(transaction)
    .ReturnTotalConsumedCapacity()
    .PutAsync();

var context = DynamoDbOperationContext.Current;
_logger.LogInformation(
    "PutItem consumed {Capacity} WCUs",
    context?.ConsumedCapacity?.CapacityUnits ?? 0);
```

### Example 4: UpdateItem with Auditing (New API)

```csharp
// Primary API - returns void, populates context
await table.Update<Transaction>()
    .WithKey("id", "123")
    .Set("SET #status = :status")
    .WithAttribute("#status", "status")
    .WithValue(":status", "COMPLETED")
    .ReturnAllOldValues()
    .UpdateAsync();

var context = DynamoDbOperationContext.Current;
if (context?.PreOperationValues != null)
{
    var oldItem = context.DeserializePreOperationValue<Transaction>();
    _auditLog.LogChange(oldItem, newStatus: "COMPLETED");
}
```

### Example 5: Debugging with Raw Data

```csharp
var items = await table.Query<Transaction>()
    .Where("pk = :pk")
    .WithValue(":pk", "USER#123")
    .ToListAsync();

var context = DynamoDbOperationContext.Current;
if (context?.RawItems != null)
{
    // Inspect raw AttributeValue structure for debugging
    foreach (var rawItem in context.RawItems)
    {
        _logger.LogDebug("Raw item: {Item}", JsonSerializer.Serialize(rawItem));
    }
    
    // Or re-deserialize with different type
    var alternativeView = context.DeserializeRawItems<AlternativeEntity>();
}
```

### Example 6: Pagination

```csharp
Dictionary<string, AttributeValue>? lastKey = null;
var allItems = new List<Transaction>();

do
{
    var builder = table.Query<Transaction>()
        .Where("pk = :pk")
        .WithValue(":pk", "USER#123")
        .Take(100);
    
    if (lastKey != null)
    {
        builder.StartAt(lastKey);
    }
    
    var items = await builder.ToListAsync();
    allItems.AddRange(items);
    
    var context = DynamoDbOperationContext.Current;
    lastKey = context?.LastEvaluatedKey;
    
} while (lastKey != null);
```

### Example 7: Advanced API with Extension Methods

```csharp
// Advanced API - returns raw AWS response, does NOT populate context
var response = await table.Query<Transaction>()
    .Where("pk = :pk")
    .WithValue(":pk", "USER#123")
    .ToDynamoDbResponseAsync();

// Access response directly
var capacity = response.ConsumedCapacity?.CapacityUnits;

// Use extension methods to deserialize
var items = response.ToList<Transaction>();
var compositeItems = response.ToCompositeEntityList<Transaction>();

// Or for GetItem
var getResponse = await table.Get<Transaction>()
    .WithKey("id", "123")
    .ToDynamoDbResponseAsync();

var item = getResponse.ToEntity<Transaction>();

// Or for UpdateItem with ReturnValues
var updateResponse = await table.Update<Transaction>()
    .WithKey("id", "123")
    .Set("SET #status = :status")
    .WithAttribute("#status", "status")
    .WithValue(":status", "COMPLETED")
    .ReturnAllOldValues()
    .ToDynamoDbResponseAsync();

var oldItem = updateResponse.ToPreOperationEntity<Transaction>();
```

### Example 8: Encryption Context (Backward Compatible)

```csharp
// Old way (still works, but obsolete)
EncryptionContext.Current = "tenant-123";

// New way (recommended)
DynamoDbOperationContext.EncryptionContextId = "tenant-123";

// Both access the same underlying value
```

## Migration Guide

### Breaking Changes

1. **ExecuteAsync() removed from base builders**
   - **Old**: `await builder.ExecuteAsync()`
   - **New**: `await builder.ToDynamoDbResponseAsync()`

2. **ExecuteAsync<T>() removed from GetItemRequestBuilder**
   - **Old**: `var response = await builder.ExecuteAsync<T>(); var item = response.Item;`
   - **New**: `var item = await builder.GetItemAsync<T>();`

3. **Custom wrapper classes removed**
   - **Old**: `GetItemResponse<T>`, `QueryResponse<T>`, `ScanResponse<T>`
   - **New**: Use POCOs + `DynamoDbOperationContext.Current` for metadata

4. **PutItem/UpdateItem/DeleteItem now return void**
   - **Old**: `var response = await builder.ExecuteAsync(); var capacity = response.ConsumedCapacity;`
   - **New**: `await builder.PutAsync(); var capacity = DynamoDbOperationContext.Current?.ConsumedCapacity;`

### Migration Steps

1. **Replace ExecuteAsync() calls**:
   ```csharp
   // Before
   var response = await table.Query<T>().ExecuteAsync();
   var items = response.Items.Select(item => T.FromDynamoDb<T>(item)).ToList();
   
   // After
   var items = await table.Query<T>().ToListAsync();
   ```

2. **Replace GetItem ExecuteAsync<T>() calls**:
   ```csharp
   // Before
   var response = await table.Get<T>().ExecuteAsync<T>();
   var item = response.Item;
   var capacity = response.ConsumedCapacity;
   
   // After
   var item = await table.Get<T>().GetItemAsync<T>();
   var capacity = DynamoDbOperationContext.Current?.ConsumedCapacity;
   ```

3. **Replace PutItem/UpdateItem/DeleteItem calls**:
   ```csharp
   // Before
   var response = await table.Put<T>().WithItem(item).ExecuteAsync();
   var capacity = response.ConsumedCapacity;
   
   // After
   await table.Put<T>().WithItem(item).PutAsync();
   var capacity = DynamoDbOperationContext.Current?.ConsumedCapacity;
   ```

4. **For advanced scenarios needing raw responses**:
   ```csharp
   // Before
   var response = await table.Query<T>().ExecuteAsync();
   // Work with raw response
   
   // After
   var response = await table.Query<T>().ToDynamoDbResponseAsync();
   // Work with raw response (context NOT populated)
   ```

## Performance Considerations

### Memory Impact

- **Minimal overhead**: Context stores references, not copies
- **Automatic cleanup**: Garbage collected when out of scope
- **No accumulation**: Each operation replaces previous context

### CPU Impact

- **Negligible**: AsyncLocal access is very fast
- **No serialization**: Raw data is already in memory
- **Lazy deserialization**: Helper methods only called when needed

### Recommendations

1. **Don't hold context references long-term**: Let GC clean up
2. **Use deserialization helpers sparingly**: Only when debugging/auditing
3. **Consider disabling in production**: If not needed for monitoring

## Alternative Designs Considered

### Alternative 1: Return Wrapper Objects

**Approach**: Return `QueryResult<T>` instead of `List<T>` from extension methods

**Pros**:
- Explicit access to metadata
- No AsyncLocal magic

**Cons**:
- Breaking change to existing API
- More verbose usage
- Doesn't solve the problem for base ExecuteAsync() methods

**Decision**: Rejected due to breaking changes

### Alternative 2: Callback/Event Pattern

**Approach**: Fire events with response metadata

**Pros**:
- More flexible
- No AsyncLocal

**Cons**:
- Requires setup/registration
- More complex usage
- Doesn't flow through async naturally

**Decision**: Rejected due to complexity

### Alternative 3: Opt-in via Configuration

**Approach**: Only populate context when explicitly enabled

**Pros**:
- Slightly better performance when disabled

**Cons**:
- More configuration
- Inconsistent behavior
- Minimal performance benefit

**Decision**: Rejected - always populate is simpler

## FluentResults Integration Updates

The `Oproto.FluentDynamoDb.FluentResults` package provides `Result<T>` wrappers for operations. It needs updates to match the new API:

### Current FluentResults Methods (To Update)

| Current Method | New Method | Return Type |
|----------------|------------|-------------|
| `ExecuteAsyncResult<T>()` (GetItem) | `GetItemAsyncResult<T>()` | `Result<T?>` |
| `ToListAsyncResult<T>()` | No change | `Result<List<T>>` |
| `ToCompositeEntityAsyncResult<T>()` | No change | `Result<T?>` |
| `ToCompositeEntityListAsyncResult<T>()` | No change | `Result<List<T>>` |
| `WithItemResult<T>()` | Remove (not needed) | - |
| `ExecuteAsyncResult<T>()` (PutItem) | `PutAsyncResult<T>()` | `Result` |

### New FluentResults Methods (To Add)

| Method | Builder | Return Type |
|--------|---------|-------------|
| `GetItemAsyncResult<T>()` | GetItemRequestBuilder | `Result<T?>` |
| `PutAsyncResult<T>()` | PutItemRequestBuilder | `Result` |
| `UpdateAsyncResult()` | UpdateItemRequestBuilder | `Result` |
| `DeleteAsyncResult()` | DeleteItemRequestBuilder | `Result` |

### Example FluentResults Usage (After Update)

```csharp
// GetItem with Result
var result = await table.Get<Transaction>()
    .WithKey("id", "123")
    .GetItemAsyncResult<Transaction>();

if (result.IsSuccess)
{
    var item = result.Value; // T? (nullable)
    var context = DynamoDbOperationContext.Current;
    // Use item and context
}
else
{
    _logger.LogError("GetItem failed: {Errors}", result.Errors);
}

// Query with Result
var queryResult = await table.Query<Transaction>()
    .Where("pk = :pk")
    .WithValue(":pk", "USER#123")
    .ToListAsyncResult<Transaction>();

if (queryResult.IsSuccess)
{
    var items = queryResult.Value; // List<T>
    var context = DynamoDbOperationContext.Current;
}

// PutItem with Result
var putResult = await table.Put<Transaction>()
    .WithItem(transaction)
    .PutAsyncResult<Transaction>();

if (putResult.IsSuccess)
{
    var context = DynamoDbOperationContext.Current;
    _logger.LogInformation("Put consumed {Capacity} WCUs", 
        context?.ConsumedCapacity?.CapacityUnits);
}

// UpdateItem with Result
var updateResult = await table.Update<Transaction>()
    .WithKey("id", "123")
    .Set("SET #status = :status")
    .WithAttribute("#status", "status")
    .WithValue(":status", "COMPLETED")
    .UpdateAsyncResult();

if (updateResult.IsSuccess)
{
    var context = DynamoDbOperationContext.Current;
    // Access metadata
}
```

## Design Decisions

### Why Two APIs (Primary vs Advanced)?

**Primary API** (GetItemAsync, ToListAsync, PutAsync, etc.):
- Covers 99% of use cases
- Clean, simple signatures
- Automatic metadata capture
- Consistent with modern .NET patterns

**Advanced API** (ToDynamoDbResponseAsync):
- For rare scenarios needing full AWS response control
- No side effects (no context population)
- Explicit opt-in for advanced users

### Why Remove Custom Wrapper Classes?

The custom wrapper classes (`GetItemResponse<T>`, etc.) created inconsistency:
- Some methods returned wrappers, others returned POCOs
- Wrappers duplicated AWS SDK response properties
- Harder to maintain and document
- AsyncLocal context provides cleaner separation

### Why Void Return for Write Operations?

Write operations (PutAsync, UpdateAsync, DeleteAsync) return void because:
- Most applications don't need the response
- Metadata is available via context when needed
- Simpler method signatures
- Consistent with common patterns (e.g., Entity Framework's SaveChanges)

### Why Keep ToDynamoDbResponseAsync?

Some advanced scenarios genuinely need raw responses:
- Custom response processing
- Testing/mocking
- Integration with other AWS SDK code
- Performance-critical paths where context overhead matters

## Open Questions

1. **Should we support batch operations?** - Yes, in Phase 6
2. **Should we support transaction operations?** - Yes, in Phase 6
3. **Should we add operation timing?** - Future enhancement (could add start/end timestamps)
4. **Should we add request details?** - Future enhancement (could add request object reference)
5. **Should we provide a way to disable context population?** - Not initially, can add if performance concerns arise
