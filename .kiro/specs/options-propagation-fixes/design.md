# Options Propagation and Documentation Fixes - Design

## Overview

This design document describes the implementation approach for fixing options propagation in batch/transaction responses and the hydrator generator, plus documentation corrections.

## Part 1: Documentation Fixes

### Files to Update

Search results identified these files with `Put().ExecuteAsync()` or `Put(entity).ExecuteAsync()`:

| File | Occurrences | Pattern |
|------|-------------|---------|
| `docs/advanced-topics/AdvancedTypes.md` | 2 | `.Put(document).ExecuteAsync()` |
| `docs/advanced-topics/TableGenerationCustomization.md` | 10 | Various `.Put(entity).ExecuteAsync()` |
| `docs/core-features/Transactions.md` | 1 | `.Put(user)).ExecuteAsync()` |
| `docs/core-features/BatchOperations.md` | 1 | `.Put(user)).ExecuteAsync()` |
| `docs/QUICK_REFERENCE.md` | 1 | `.Put().WithItem(entity).ExecuteAsync()` |
| `docs/DeveloperGuide.md` | 1 | `.Put().WithItem(user).ExecuteAsync()` |
| `docs/reference/AttributeReference.md` | 1 | `.Put(document).ExecuteAsync()` |
| `docs/DOCUMENTATION_CHANGELOG.md` | 1 | `.Put(document).ExecuteAsync()` |
| `.kiro/specs/integration-test-build-fixes/design.md` | 1 | `.Put(entity).ExecuteAsync()` |
| `Oproto.FluentDynamoDb.SystemTextJson/README.md` | 1 | `.Put(order).ExecuteAsync()` |
| `Oproto.FluentDynamoDb.NewtonsoftJson/README.md` | 1 | `.Put(order).ExecuteAsync()` |

### Correction Pattern

**Before:**
```csharp
await table.Documents.Put(document).ExecuteAsync();
await table.Put().WithItem(entity).ExecuteAsync();
```

**After:**
```csharp
await table.Documents.Put(document).PutAsync();
await table.Put().WithItem(entity).PutAsync();
```

## Part 2: Options Propagation Architecture

### Current State (Problem)

```
┌─────────────────────────────────────────────────────────────────┐
│                    Current Flow (Broken)                        │
├─────────────────────────────────────────────────────────────────┤
│                                                                 │
│  BatchGetBuilder                                                │
│       │                                                         │
│       ▼                                                         │
│  ExecuteAsync()                                                 │
│       │                                                         │
│       ▼                                                         │
│  new BatchGetResponse(response, tableOrder, requestedKeys)      │
│       │                                                         │
│       │  ❌ No options passed!                                  │
│       ▼                                                         │
│  BatchGetResponse.GetItem<T>(index)                             │
│       │                                                         │
│       ▼                                                         │
│  TEntity.FromDynamoDb<TEntity>(item, options: null)  ❌         │
│       │                                                         │
│       ▼                                                         │
│  [JsonBlob] properties fail - no serializer configured!         │
│                                                                 │
└─────────────────────────────────────────────────────────────────┘
```

### Target State (Fixed)

```
┌─────────────────────────────────────────────────────────────────┐
│                    Fixed Flow                                   │
├─────────────────────────────────────────────────────────────────┤
│                                                                 │
│  BatchGetBuilder (has _options from request builders)           │
│       │                                                         │
│       ▼                                                         │
│  ExecuteAsync()                                                 │
│       │                                                         │
│       ▼                                                         │
│  new BatchGetResponse(response, tableOrder, requestedKeys,      │
│                       options)  ✅ Options passed               │
│       │                                                         │
│       ▼                                                         │
│  BatchGetResponse.GetItem<T>(index)                             │
│       │                                                         │
│       ▼                                                         │
│  TEntity.FromDynamoDb<TEntity>(item, _options)  ✅              │
│       │                                                         │
│       ▼                                                         │
│  [JsonBlob] properties work - serializer available!             │
│                                                                 │
└─────────────────────────────────────────────────────────────────┘
```

### Design: BatchGetResponse

#### Current Implementation

```csharp
public class BatchGetResponse
{
    private readonly BatchGetItemResponse _response;
    private readonly List<Dictionary<string, AttributeValue>?> _items;
    private readonly Dictionary<string, KeysAndAttributes> _unprocessedKeys;

    internal BatchGetResponse(
        BatchGetItemResponse response, 
        List<string> tableOrder,
        List<Dictionary<string, AttributeValue>> requestedKeys)
    {
        // ...
    }

    public TEntity? GetItem<TEntity>(int index) where TEntity : class, IDynamoDbEntity
    {
        // ...
        return TEntity.FromDynamoDb<TEntity>(item, options: null);  // ❌
    }
}
```

#### New Implementation

```csharp
public class BatchGetResponse
{
    private readonly BatchGetItemResponse _response;
    private readonly List<Dictionary<string, AttributeValue>?> _items;
    private readonly Dictionary<string, KeysAndAttributes> _unprocessedKeys;
    private readonly FluentDynamoDbOptions? _options;  // ✅ Add field

    internal BatchGetResponse(
        BatchGetItemResponse response, 
        List<string> tableOrder,
        List<Dictionary<string, AttributeValue>> requestedKeys,
        FluentDynamoDbOptions? options = null)  // ✅ Add parameter
    {
        _options = options;
        // ...
    }

    public TEntity? GetItem<TEntity>(int index) where TEntity : class, IDynamoDbEntity
    {
        // ...
        return TEntity.FromDynamoDb<TEntity>(item, _options);  // ✅ Pass options
    }
}
```

### Design: TransactionGetResponse

#### Current Implementation

```csharp
public class TransactionGetResponse
{
    private readonly TransactGetItemsResponse _response;
    private readonly List<Dictionary<string, AttributeValue>?> _items;

    internal TransactionGetResponse(TransactGetItemsResponse response)
    {
        // ...
    }

    public TEntity? GetItem<TEntity>(int index) where TEntity : class, IDynamoDbEntity
    {
        // ...
        return TEntity.FromDynamoDb<TEntity>(item, options: null);  // ❌
    }
}
```

#### New Implementation

```csharp
public class TransactionGetResponse
{
    private readonly TransactGetItemsResponse _response;
    private readonly List<Dictionary<string, AttributeValue>?> _items;
    private readonly FluentDynamoDbOptions? _options;  // ✅ Add field

    internal TransactionGetResponse(
        TransactGetItemsResponse response,
        FluentDynamoDbOptions? options = null)  // ✅ Add parameter
    {
        _options = options;
        // ...
    }

    public TEntity? GetItem<TEntity>(int index) where TEntity : class, IDynamoDbEntity
    {
        // ...
        return TEntity.FromDynamoDb<TEntity>(item, _options);  // ✅ Pass options
    }
}
```

### Design: IAsyncEntityHydrator Interface

#### Current Interface

```csharp
public interface IAsyncEntityHydrator<TEntity> where TEntity : class, IDynamoDbEntity
{
    Task<TEntity> HydrateAsync(
        Dictionary<string, AttributeValue> item,
        IBlobStorageProvider blobProvider,
        CancellationToken cancellationToken = default);

    Task<TEntity> HydrateAsync(
        IList<Dictionary<string, AttributeValue>> items,
        IBlobStorageProvider blobProvider,
        CancellationToken cancellationToken = default);
}
```

#### New Interface

```csharp
public interface IAsyncEntityHydrator<TEntity> where TEntity : class, IDynamoDbEntity
{
    Task<TEntity> HydrateAsync(
        Dictionary<string, AttributeValue> item,
        IBlobStorageProvider blobProvider,
        FluentDynamoDbOptions? options = null,  // ✅ Add parameter
        CancellationToken cancellationToken = default);

    Task<TEntity> HydrateAsync(
        IList<Dictionary<string, AttributeValue>> items,
        IBlobStorageProvider blobProvider,
        FluentDynamoDbOptions? options = null,  // ✅ Add parameter
        CancellationToken cancellationToken = default);
}
```

### Design: HydratorGenerator Changes

#### Current Generated Code

```csharp
public async Task<MyEntity> HydrateAsync(
    Dictionary<string, AttributeValue> item,
    IBlobStorageProvider blobProvider,
    CancellationToken cancellationToken = default)
{
    ArgumentNullException.ThrowIfNull(item);
    ArgumentNullException.ThrowIfNull(blobProvider);

    return await MyEntity.FromDynamoDbAsync<MyEntity>(
        item,
        blobProvider,
        fieldEncryptor: null,
        options: null,  // ❌
        cancellationToken);
}
```

#### New Generated Code

```csharp
public async Task<MyEntity> HydrateAsync(
    Dictionary<string, AttributeValue> item,
    IBlobStorageProvider blobProvider,
    FluentDynamoDbOptions? options = null,  // ✅ Add parameter
    CancellationToken cancellationToken = default)
{
    ArgumentNullException.ThrowIfNull(item);
    ArgumentNullException.ThrowIfNull(blobProvider);

    return await MyEntity.FromDynamoDbAsync<MyEntity>(
        item,
        blobProvider,
        fieldEncryptor: options?.FieldEncryptor,  // ✅ Extract from options
        options: options,  // ✅ Pass options
        cancellationToken);
}
```

### Options Source in Builders

The options need to flow from the request builders to the response classes:

#### BatchGetBuilder

```csharp
// BatchGetBuilder needs to track options from added builders
public class BatchGetBuilder
{
    private FluentDynamoDbOptions? _options;
    
    public BatchGetBuilder Add<TEntity>(GetItemRequestBuilder<TEntity> builder)
    {
        // Capture options from first builder that has them
        _options ??= builder.Options;
        // ...
    }
    
    public async Task<BatchGetResponse> ExecuteAsync(...)
    {
        // ...
        return new BatchGetResponse(response, tableOrder, requestedKeys, _options);
    }
}
```

#### TransactGetItemsRequestBuilder

```csharp
public class TransactGetItemsRequestBuilder
{
    private FluentDynamoDbOptions? _options;
    
    public TransactGetItemsRequestBuilder Add<TEntity>(GetItemRequestBuilder<TEntity> builder)
    {
        _options ??= builder.Options;
        // ...
    }
    
    public async Task<TransactionGetResponse> ExecuteAsync(...)
    {
        // ...
        return new TransactionGetResponse(response, _options);
    }
}
```

## Implementation Order

1. **Phase 1: Documentation Fixes** (Low risk, high visibility)
   - Fix all `Put().ExecuteAsync()` → `PutAsync()` in docs
   - Update `docs/DOCUMENTATION_CHANGELOG.md`

2. **Phase 2: Response Class Changes** (Medium risk)
   - Update `BatchGetResponse` constructor and `GetItem<T>()`
   - Update `TransactionGetResponse` constructor and `GetItem<T>()`
   - Update callers to pass options

3. **Phase 3: Hydrator Interface Changes** (Higher risk - interface change)
   - Update `IAsyncEntityHydrator<T>` interface
   - Update `HydratorGenerator` to generate new signature
   - Update all callers of hydrator

4. **Phase 4: Changelog Updates**
   - Update `CHANGELOG.md` with fixes
   - Update `docs/DOCUMENTATION_CHANGELOG.md` with doc corrections

## Testing Strategy

### Unit Tests

1. **BatchGetResponse Tests**
   - Test `GetItem<T>()` with entity containing `[JsonBlob]` property
   - Verify JSON deserialization works when options provided
   - Verify graceful handling when options is null (for entities without JSON)

2. **TransactionGetResponse Tests**
   - Same as BatchGetResponse tests

3. **HydratorGenerator Tests**
   - Verify generated code includes options parameter
   - Verify options are passed through to `FromDynamoDbAsync`

### Integration Tests

1. **Batch Get with JSON Blob**
   - Create entity with `[JsonBlob]` property
   - Save via `PutAsync()`
   - Retrieve via `DynamoDbBatch.Get`
   - Verify JSON property is correctly deserialized

2. **Transaction Get with JSON Blob**
   - Same pattern as batch get

## Backward Compatibility

All changes are backward compatible:
- New constructor parameters have default values (`options = null`)
- Interface method additions have default parameter values
- Existing code without options continues to work (just without JSON/logging)
