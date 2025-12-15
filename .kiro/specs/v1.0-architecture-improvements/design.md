# Design Document

## Overview

This document describes the design for a comprehensive set of architecture improvements targeting the v1.0 release of Oproto.FluentDynamoDb. The changes include:

1. **DynamoDbTableBase Removal** - Fully source-generate table classes without inheritance
2. **Interface Clarification** - Document the relationship between IDynamoDbEntity and IEntityMetadataProvider
3. **PartiQL Support** - Add SQL-like query capability with entity hydration
4. **Direct SDK Request Passing** - Accept native SDK request objects with response hydration
5. **DynamicEntity/DynamicTable** - Schema-less table access using the DynamicFields pattern
6. **GeoHash Query Bug Fix** - Fix the BETWEEN clause syntax error in StoreLocator

## Architecture

### Current Architecture

```
┌─────────────────────────────────────────────────────────────┐
│                    Generated Table Class                     │
│                  (e.g., UsersTable)                         │
├─────────────────────────────────────────────────────────────┤
│  - Entity Accessors (Users, Orders, etc.)                   │
│  - Index Accessors (Gsi1, Gsi2, etc.)                       │
│  - Inherits from DynamoDbTableBase                          │
└─────────────────────────────────────────────────────────────┘
                              │
                              ▼
┌─────────────────────────────────────────────────────────────┐
│                    DynamoDbTableBase                         │
│                  (Abstract Base Class)                       │
├─────────────────────────────────────────────────────────────┤
│  - Client, Name, Options properties                         │
│  - Query<T>(), Get<T>(), Put<T>(), Update<T>(), Delete<T>() │
│  - PutAsync<T>(), etc. convenience methods                  │
│  - Always public visibility                                 │
└─────────────────────────────────────────────────────────────┘
```

### Proposed Architecture

```
┌─────────────────────────────────────────────────────────────┐
│                    Generated Table Class                     │
│                  (e.g., UsersTable)                         │
├─────────────────────────────────────────────────────────────┤
│  - Client, Name, Options properties (generated)             │
│  - Entity Accessors (visibility controlled by attribute)    │
│  - Index Accessors (visibility controlled by attribute)     │
│  - Query<T>(), Get<T>(), etc. (visibility controlled)       │
│  - No inheritance - fully self-contained                    │
└─────────────────────────────────────────────────────────────┘

┌─────────────────────────────────────────────────────────────┐
│                      DynamicTable                            │
│                  (Built-in Table Class)                      │
├─────────────────────────────────────────────────────────────┤
│  - Works with DynamicEntity (all fields in DynamicFields)   │
│  - Optional key configuration for typed key methods         │
│  - Supports lambda expressions via DynamicFields indexer    │
│  - No entity definition required                            │
└─────────────────────────────────────────────────────────────┘
```

## Components and Interfaces

### 1. Interface Hierarchy (Clarification)

The current interface hierarchy is intentional and should be preserved:

```csharp
// Provides static metadata about an entity - can be implemented without serialization
public interface IEntityMetadataProvider
{
    static abstract EntityMetadata GetEntityMetadata();
}

// Full entity interface - includes serialization methods
// Extends IEntityMetadataProvider for convenience
public interface IDynamoDbEntity : IEntityMetadataProvider
{
    static abstract Dictionary<string, AttributeValue> ToDynamoDb<TSelf>(...);
    static abstract TSelf FromDynamoDb<TSelf>(...);
    // ... other methods
}
```

**Design Decision**: Keep both interfaces separate because:
- `IEntityMetadataProvider` can be implemented by tooling/analyzers without full serialization
- Unit tests can mock metadata without implementing serialization
- Type constraints can be more specific (metadata-only vs full entity)

### 2. Generated Table Class Structure

The source generator will produce complete table classes without inheritance:

```csharp
// Generated code - no base class
public partial class UsersTable
{
    public IAmazonDynamoDB DynamoDbClient { get; }
    public string Name { get; }
    public FluentDynamoDbOptions Options { get; }
    
    // Constructor
    public UsersTable(IAmazonDynamoDB client, string tableName, FluentDynamoDbOptions? options = null)
    {
        DynamoDbClient = client;
        Name = tableName;
        Options = options ?? new FluentDynamoDbOptions();
    }
    
    // Entity accessor (visibility from [GenerateAccessors])
    public UserEntityAccessor Users { get; }
    
    // Index accessor
    public DynamoDbIndex EmailIndex { get; }
    
    // Generic query methods (visibility controlled)
    public QueryRequestBuilder<TEntity> Query<TEntity>() where TEntity : class
        => new QueryRequestBuilder<TEntity>(DynamoDbClient, Options).ForTable(Name);
    
    // ... other methods
}
```

### 3. DynamicEntity and DynamicTable

#### DynamicEntity

A built-in entity that uses only DynamicFields:

```csharp
namespace Oproto.FluentDynamoDb.Entities;

/// <summary>
/// A schema-less entity where all attributes are stored in DynamicFields.
/// Use with DynamicTable for accessing tables without defining entity classes.
/// </summary>
public sealed class DynamicEntity : IDynamoDbEntity
{
    /// <summary>
    /// All attributes from the DynamoDB item.
    /// </summary>
    public DynamicFieldCollection DynamicFields { get; set; } = new();
    
    // IDynamoDbEntity implementation
    public static Dictionary<string, AttributeValue> ToDynamoDb<TSelf>(
        TSelf entity, FluentDynamoDbOptions? options = null) where TSelf : IDynamoDbEntity
    {
        if (entity is not DynamicEntity dynamicEntity)
            throw new InvalidOperationException("Expected DynamicEntity");
        return dynamicEntity.DynamicFields.ToAttributeValueDictionary();
    }
    
    public static TSelf FromDynamoDb<TSelf>(
        Dictionary<string, AttributeValue> item, FluentDynamoDbOptions? options = null) 
        where TSelf : IDynamoDbEntity
    {
        var entity = new DynamicEntity();
        entity.DynamicFields = DynamicFieldCollection.FromAttributeValues(item);
        return (TSelf)(object)entity;
    }
    
    // Metadata indicates this is a dynamic entity (skip key validation)
    public static EntityMetadata GetEntityMetadata() => new EntityMetadata
    {
        EntityType = typeof(DynamicEntity),
        TableName = null, // Set at runtime
        IsDynamicEntity = true, // New flag for expression translator
        // No mapped properties - everything is dynamic
    };
    
    // Other IDynamoDbEntity members...
    public static bool RequiresWriteTransaction => false;
    public static string GetPartitionKey(Dictionary<string, AttributeValue> item) 
        => throw new NotSupportedException("DynamicEntity requires explicit key specification");
    public static bool MatchesEntity(Dictionary<string, AttributeValue> item) => true;
}
```

#### DynamicTable

A table class for working with DynamicEntity:

```csharp
namespace Oproto.FluentDynamoDb.Storage;

/// <summary>
/// Options for configuring a DynamicTable's key schema.
/// </summary>
public class DynamicTableKeyOptions
{
    public string PartitionKeyName { get; set; } = "pk";
    public ScalarAttributeType PartitionKeyType { get; set; } = ScalarAttributeType.S;
    public string? SortKeyName { get; set; }
    public ScalarAttributeType? SortKeyType { get; set; }
}

/// <summary>
/// A table class for schema-less access to any DynamoDB table.
/// </summary>
public class DynamicTable
{
    public IAmazonDynamoDB DynamoDbClient { get; }
    public string Name { get; }
    public FluentDynamoDbOptions Options { get; }
    public DynamicTableKeyOptions? KeyOptions { get; }
    
    public DynamicTable(
        IAmazonDynamoDB client, 
        string tableName, 
        DynamicTableKeyOptions? keyOptions = null,
        FluentDynamoDbOptions? options = null)
    {
        DynamoDbClient = client;
        Name = tableName;
        KeyOptions = keyOptions;
        Options = options ?? new FluentDynamoDbOptions();
    }
    
    // Query with lambda expression support
    public QueryRequestBuilder<DynamicEntity> Query()
        => new QueryRequestBuilder<DynamicEntity>(DynamoDbClient, Options).ForTable(Name);
    
    // Get with typed keys (when KeyOptions configured)
    public async Task<DynamicEntity?> GetAsync(string partitionKey, CancellationToken ct = default)
    {
        ValidateKeyOptions();
        var builder = new GetItemRequestBuilder<DynamicEntity>(DynamoDbClient, Options)
            .ForTable(Name)
            .WithKey(KeyOptions!.PartitionKeyName, new AttributeValue { S = partitionKey });
        return await builder.GetItemAsync(ct);
    }
    
    public async Task<DynamicEntity?> GetAsync(string partitionKey, string sortKey, CancellationToken ct = default)
    {
        ValidateKeyOptions(requireSortKey: true);
        var builder = new GetItemRequestBuilder<DynamicEntity>(DynamoDbClient, Options)
            .ForTable(Name)
            .WithKey(KeyOptions!.PartitionKeyName, new AttributeValue { S = partitionKey })
            .WithKey(KeyOptions.SortKeyName!, new AttributeValue { S = sortKey });
        return await builder.GetItemAsync(ct);
    }
    
    // Get with AttributeValue keys (always available)
    public async Task<DynamicEntity?> GetAsync(
        AttributeValue partitionKey, 
        AttributeValue? sortKey = null,
        CancellationToken ct = default)
    {
        // Implementation using raw AttributeValues
    }
    
    // Similar patterns for Update, Delete, Put...
}
```

#### Expression Translator Changes

The expression translator needs to handle DynamicEntity specially:

```csharp
// In ExpressionTranslator
private void TranslateKeyCondition(Expression expression)
{
    var entityMetadata = GetEntityMetadata<TEntity>();
    
    // Skip key validation for DynamicEntity
    if (entityMetadata.IsDynamicEntity)
    {
        // Allow DynamicFields["pk"] in key conditions
        TranslateDynamicFieldAccess(expression);
        return;
    }
    
    // Normal key validation for typed entities
    ValidateKeyCondition(expression, entityMetadata);
}
```

### 4. PartiQL Support

PartiQL support follows the same request builder pattern as other operations, with methods on the base table that return builders supporting fluent execution.

#### Format String Support

PartiQL statements support the same format string syntax as other API methods, including format specifiers:
- `{0}` - Simple parameter substitution
- `{0:o}` - DateTime with ISO 8601 format
- `{0:F2}` - Decimal with 2 decimal places
- `{0:X}` - Integer as hexadecimal

The implementation uses `FormatStringProcessor` for consistency with existing API methods.

#### PartiQL Request Builder

```csharp
namespace Oproto.FluentDynamoDb.Requests;

/// <summary>
/// Builder for executing PartiQL statements against DynamoDB.
/// Follows the same pattern as QueryRequestBuilder for consistency.
/// </summary>
public class PartiQLRequestBuilder<TEntity> where TEntity : class, IDynamoDbEntity
{
    private readonly IAmazonDynamoDB _client;
    private readonly FluentDynamoDbOptions _options;
    private string _statement = string.Empty;
    private readonly List<object> _parameters = new();
    
    // Response metadata accessible after execution
    public ResponseMetadata? ResponseMetadata { get; private set; }
    public ConsumedCapacity? ConsumedCapacity { get; private set; }
    
    public PartiQLRequestBuilder(IAmazonDynamoDB client, FluentDynamoDbOptions options)
    {
        _client = client;
        _options = options;
    }
    
    /// <summary>
    /// Sets the PartiQL statement with optional format string placeholders.
    /// Supports format specifiers like {0:o} for ISO 8601 dates.
    /// </summary>
    public PartiQLRequestBuilder<TEntity> WithStatement(string statement, params object[] parameters)
    {
        _statement = statement;
        _parameters.Clear();
        _parameters.AddRange(parameters);
        return this;
    }
    
    /// <summary>
    /// Executes a SELECT query and returns hydrated entities as a list.
    /// </summary>
    public async Task<List<TEntity>> ToListAsync(CancellationToken ct = default)
    {
        var request = CreateRequest();
        var response = await _client.ExecuteStatementAsync(request, ct);
        
        ResponseMetadata = response.ResponseMetadata;
        ConsumedCapacity = response.ConsumedCapacity;
        
        return response.Items
            .Where(TEntity.MatchesEntity)
            .Select(item => TEntity.FromDynamoDb<TEntity>(item, _options))
            .ToList();
    }
    
    /// <summary>
    /// Executes a SELECT query and returns hydrated entities for compound entity tables.
    /// </summary>
    public async Task<CompoundEntityResult> ToCompoundEntityAsync(CancellationToken ct = default)
    {
        var request = CreateRequest();
        var response = await _client.ExecuteStatementAsync(request, ct);
        
        ResponseMetadata = response.ResponseMetadata;
        ConsumedCapacity = response.ConsumedCapacity;
        
        return new CompoundEntityResult(response.Items, _options);
    }
    
    /// <summary>
    /// Executes a non-SELECT statement (INSERT, UPDATE, DELETE).
    /// </summary>
    public async Task ExecuteAsync(CancellationToken ct = default)
    {
        var request = CreateRequest();
        var response = await _client.ExecuteStatementAsync(request, ct);
        
        ResponseMetadata = response.ResponseMetadata;
        ConsumedCapacity = response.ConsumedCapacity;
    }
    
    /// <summary>
    /// Returns the underlying SDK request for inspection or modification.
    /// </summary>
    public ExecuteStatementRequest ToRequest() => CreateRequest();
    
    private ExecuteStatementRequest CreateRequest()
    {
        // Use FormatStringProcessor for consistent format string handling
        var (formattedStatement, attributeValues) = FormatStringProcessor.Process(
            _statement, _parameters.ToArray());
        
        return new ExecuteStatementRequest
        {
            Statement = ConvertToPartiQLPlaceholders(formattedStatement),
            Parameters = attributeValues.Values.ToList()
        };
    }
    
    // Converts :v0, :v1 placeholders to ? for PartiQL positional parameters
    private static string ConvertToPartiQLPlaceholders(string statement) { ... }
}
```

#### Table-Level PartiQL Methods

Methods on DynamoDbTableBase (and eventually source-generated tables):

```csharp
// In DynamoDbTableBase (and generated table classes)
public class UsersTable
{
    /// <summary>
    /// Creates a PartiQL request builder for executing SQL-like queries.
    /// Supports format specifiers like {0:o} for ISO 8601 dates.
    /// </summary>
    public PartiQLRequestBuilder<TEntity> ExecutePartiQL<TEntity>(
        string statement, 
        params object[] parameters) where TEntity : class, IDynamoDbEntity
    {
        return new PartiQLRequestBuilder<TEntity>(DynamoDbClient, Options)
            .WithStatement(statement, parameters);
    }
    
    // Non-generic version for DynamicEntity
    public PartiQLRequestBuilder<DynamicEntity> ExecutePartiQL(
        string statement, 
        params object[] parameters)
    {
        return ExecutePartiQL<DynamicEntity>(statement, parameters);
    }
}
```

#### Usage Examples

```csharp
// SELECT query with hydration to list
var users = await table.ExecutePartiQL<User>(
    "SELECT * FROM Users WHERE pk = {0}",
    "USER#123")
    .ToListAsync();

// SELECT query with DateTime formatting
var recentOrders = await table.ExecutePartiQL<Order>(
    "SELECT * FROM Orders WHERE pk = {0} AND created > {1:o}",
    "ORDER#456", DateTime.UtcNow.AddDays(-7))
    .ToListAsync();

// SELECT query for compound entity tables
var result = await table.ExecutePartiQL<Order>(
    "SELECT * FROM Orders WHERE pk = {0}",
    "ORDER#456")
    .ToCompoundEntityAsync();
var orders = result.GetEntities<Order>();
var orderLines = result.GetEntities<OrderLine>();

// INSERT/UPDATE/DELETE statements
await table.ExecutePartiQL<User>(
    "UPDATE Users SET name = {0}, modified = {1:o} WHERE pk = {2} AND sk = {3}",
    "Jane Doe", DateTime.UtcNow, "USER#123", "PROFILE")
    .ExecuteAsync();

// Access response metadata after execution
var builder = table.ExecutePartiQL<User>("SELECT * FROM Users WHERE pk = {0}", "USER#123");
var users = await builder.ToListAsync();
var metadata = builder.Response?.ResponseMetadata;
var capacity = builder.Response?.ConsumedCapacity;

// DynamicTable usage (non-generic)
var items = await dynamicTable.ExecutePartiQL(
    "SELECT * FROM MyTable WHERE pk = {0}",
    "ITEM#789")
    .ToListAsync();
```

#### Batch PartiQL Operations

Batch PartiQL operations are accessed via `DynamoDbBatch.PartiQL`, keeping all batch operations discoverable under the existing `DynamoDbBatch` static class. DynamoDB's `BatchExecuteStatement` API handles all statement types (SELECT, INSERT, UPDATE, DELETE) in a single batch - there's no separate read/write distinction like with `BatchGetItem`/`BatchWriteItem`.

The builder follows the same patterns as `BatchGetBuilder` and `TransactionGetBuilder`:
- `ExecuteAsync()` returns a `BatchPartiQLResponse` wrapper
- `ExecuteAndMapAsync<T1>()`, `ExecuteAndMapAsync<T1, T2>()`, etc. for typed tuple results
- Response wrapper with `GetItem<T>(index)` for accessing individual SELECT results

```csharp
// In DynamoDbBatch.cs - add PartiQL property
public static class DynamoDbBatch
{
    // Existing properties
    public static BatchWriteBuilder Write => new();
    public static BatchGetBuilder Get => new();
    
    /// <summary>
    /// Creates a new batch PartiQL builder for composing multiple PartiQL statements.
    /// Unlike Write/Get, PartiQL batch can mix SELECT, INSERT, UPDATE, DELETE statements.
    /// </summary>
    public static BatchPartiQLBuilder PartiQL => new();
}

/// <summary>
/// Builder for batch PartiQL operations.
/// </summary>
public class BatchPartiQLBuilder
{
    private readonly List<BatchStatementRequest> _statements = new();
    private IAmazonDynamoDB? _client;
    private IAmazonDynamoDB? _explicitClient;
    private FluentDynamoDbOptions? _options;
    
    /// <summary>
    /// Adds a PartiQL statement builder to the batch.
    /// </summary>
    public BatchPartiQLBuilder Add<TEntity>(PartiQLRequestBuilder<TEntity> builder) 
        where TEntity : class, IDynamoDbEntity
    {
        InferClientIfNeeded(builder);
        _options ??= builder.Options;
        
        var request = builder.ToRequest();
        _statements.Add(new BatchStatementRequest
        {
            Statement = request.Statement,
            Parameters = request.Parameters
        });
        return this;
    }
    
    /// <summary>
    /// Explicitly sets the DynamoDB client.
    /// </summary>
    public BatchPartiQLBuilder WithClient(IAmazonDynamoDB client)
    {
        _explicitClient = client;
        return this;
    }
    
    /// <summary>
    /// Executes all statements in the batch.
    /// Returns a response wrapper for accessing results.
    /// </summary>
    public async Task<BatchPartiQLResponse> ExecuteAsync(
        IAmazonDynamoDB? client = null,
        CancellationToken ct = default)
    {
        var effectiveClient = client ?? _explicitClient ?? _client 
            ?? throw new InvalidOperationException("No DynamoDB client configured.");
        
        var request = new BatchExecuteStatementRequest { Statements = _statements };
        var response = await effectiveClient.BatchExecuteStatementAsync(request, ct);
        
        return new BatchPartiQLResponse(response, _options);
    }
    
    /// <summary>
    /// Executes the batch and deserializes a single SELECT result.
    /// </summary>
    public async Task<T1?> ExecuteAndMapAsync<T1>(
        IAmazonDynamoDB? client = null,
        CancellationToken ct = default)
        where T1 : class, IDynamoDbEntity
    {
        var response = await ExecuteAsync(client, ct);
        return response.GetItem<T1>(0);
    }
    
    /// <summary>
    /// Executes the batch and deserializes two SELECT results.
    /// </summary>
    public async Task<(T1?, T2?)> ExecuteAndMapAsync<T1, T2>(
        IAmazonDynamoDB? client = null,
        CancellationToken ct = default)
        where T1 : class, IDynamoDbEntity
        where T2 : class, IDynamoDbEntity
    {
        var response = await ExecuteAsync(client, ct);
        return (response.GetItem<T1>(0), response.GetItem<T2>(1));
    }
    
    // ... ExecuteAndMapAsync overloads up to 8 types (same as BatchGetBuilder)
}

/// <summary>
/// Response wrapper for batch PartiQL operations.
/// Provides typed access to SELECT results.
/// </summary>
public class BatchPartiQLResponse
{
    private readonly BatchExecuteStatementResponse _response;
    private readonly FluentDynamoDbOptions? _options;
    
    public BatchPartiQLResponse(BatchExecuteStatementResponse response, FluentDynamoDbOptions? options)
    {
        _response = response;
        _options = options;
    }
    
    /// <summary>
    /// Gets the raw SDK response.
    /// </summary>
    public BatchExecuteStatementResponse RawResponse => _response;
    
    /// <summary>
    /// Gets a hydrated entity from a SELECT result at the specified index.
    /// Returns null for non-SELECT statements or if no item was returned.
    /// </summary>
    public TEntity? GetItem<TEntity>(int index) where TEntity : class, IDynamoDbEntity
    {
        if (index >= _response.Responses.Count)
            return null;
            
        var item = _response.Responses[index].Item;
        if (item == null || item.Count == 0)
            return null;
            
        return TEntity.FromDynamoDb<TEntity>(item, _options ?? new FluentDynamoDbOptions());
    }
    
    /// <summary>
    /// Gets all items from a SELECT result at the specified index as a list.
    /// Useful when a SELECT returns multiple rows.
    /// </summary>
    public List<TEntity> GetItems<TEntity>(int index) where TEntity : class, IDynamoDbEntity
    {
        // Note: BatchExecuteStatement returns one item per statement
        // For multi-row results, use single ExecutePartiQL().ToListAsync()
        var item = GetItem<TEntity>(index);
        return item != null ? new List<TEntity> { item } : new List<TEntity>();
    }
}

// Usage - discoverable under DynamoDbBatch
var response = await DynamoDbBatch.PartiQL
    .Add(table.ExecutePartiQL<User>(
        "SELECT * FROM Users WHERE pk = {0}",
        "USER#123"))
    .Add(table.ExecutePartiQL<Order>(
        "SELECT * FROM Orders WHERE pk = {0}",
        "ORDER#456"))
    .ExecuteAsync();

var user = response.GetItem<User>(0);
var order = response.GetItem<Order>(1);

// Or use tuple convenience method
var (user, order) = await DynamoDbBatch.PartiQL
    .Add(table.ExecutePartiQL<User>("SELECT * FROM Users WHERE pk = {0}", "USER#123"))
    .Add(table.ExecutePartiQL<Order>("SELECT * FROM Orders WHERE pk = {0}", "ORDER#456"))
    .ExecuteAndMapAsync<User, Order>();

// Mixed operations (SELECT + UPDATE/DELETE)
await DynamoDbBatch.PartiQL
    .Add(table.ExecutePartiQL<User>(
        "UPDATE Users SET name = {0}, modified = {1:o} WHERE pk = {2}", 
        "Jane", DateTime.UtcNow, "USER#123"))
    .Add(table.ExecutePartiQL<User>(
        "DELETE FROM Users WHERE pk = {0}", 
        "USER#456"))
    .ExecuteAsync();
```

### 5. Direct SDK Request Passing

The direct SDK request passing feature allows users to inject pre-built AWS SDK request objects into the existing builder pattern. This approach:
- Reuses existing hydration logic in the builders
- Provides access to response metadata on the builder after execution
- Follows the same patterns as existing convenience methods
- Works naturally with source-generated table classes

#### Builder Enhancement: WithRequest Methods

Each request builder gains a `WithRequest()` method that accepts a pre-built SDK request:

```csharp
// GetItemRequestBuilder enhancement
public class GetItemRequestBuilder<TEntity> where TEntity : class
{
    // Existing internal request
    private GetItemRequest _request;
    
    // Response metadata accessible after execution
    public ConsumedCapacity? ConsumedCapacity { get; private set; }
    public ResponseMetadata? ResponseMetadata { get; private set; }
    
    /// <summary>
    /// Configures the builder with a pre-built GetItemRequest.
    /// This replaces any previously configured request state.
    /// </summary>
    public GetItemRequestBuilder<TEntity> WithRequest(GetItemRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        _request = request;
        return this;
    }
    
    // Existing GetItemAsync populates response metadata
    public async Task<TEntity?> GetItemAsync(CancellationToken ct = default)
    {
        var response = await _client.GetItemAsync(_request, ct);
        
        // Store response metadata on builder for post-execution access
        ConsumedCapacity = response.ConsumedCapacity;
        ResponseMetadata = response.ResponseMetadata;
        
        // Existing hydration logic...
    }
}

// Similar pattern for QueryRequestBuilder, ScanRequestBuilder, etc.
```

#### Table-Level Convenience Methods

Tables (both generated and DynamicTable) provide convenience overloads that wrap the builder pattern:

```csharp
// Generated table class or DynamicTable
public class UsersTable
{
    // Builder access - allows post-execution metadata access
    public GetItemRequestBuilder<User> Get(GetItemRequest request)
        => Get<User>().WithRequest(request);
    
    // Convenience method - all in one shot
    public async Task<User?> GetAsync(GetItemRequest request, CancellationToken ct = default)
        => await Get(request).GetItemAsync(ct);
    
    // Same pattern for Query, Scan, Update, Delete, Put
    public QueryRequestBuilder<User> Query(QueryRequest request)
        => Query<User>().WithRequest(request);
    
    public async Task<List<User>> QueryAsync(QueryRequest request, CancellationToken ct = default)
        => await Query(request).ToListAsync(ct);
}
```

#### Usage Examples

```csharp
// Convenience method - simple one-liner
var user = await table.Users.GetAsync(existingGetItemRequest);

// Builder pattern - access response metadata after execution
var builder = table.Users.Get(existingGetItemRequest);
var user = await builder.GetItemAsync();
var capacity = builder.Response?.ConsumedCapacity;  // Access after execution
var metadata = builder.Response?.ResponseMetadata;

// Query with pre-built request
var orders = await table.Orders.QueryAsync(existingQueryRequest);

// Or with builder for metadata access
var queryBuilder = table.Orders.Query(existingQueryRequest);
var orders = await queryBuilder.ToListAsync();
var scannedCount = queryBuilder.Response?.ScannedCount;
var lastKey = queryBuilder.Response?.LastEvaluatedKey;
```

#### Response Metadata on Builders

Each builder stores response metadata in a `.Response` property after execution, providing a reliable alternative to AsyncLocal context:

| Builder | Response Type | Properties |
|---------|---------------|------------|
| `GetItemRequestBuilder` | `GetItemOperationResponse` | `ConsumedCapacity`, `ResponseMetadata` |
| `QueryRequestBuilder` | `QueryOperationResponse` | `ConsumedCapacity`, `ResponseMetadata`, `ScannedCount`, `ResultCount`, `LastEvaluatedKey`, `HasMorePages` |
| `ScanRequestBuilder` | `ScanOperationResponse` | `ConsumedCapacity`, `ResponseMetadata`, `ScannedCount`, `ResultCount`, `LastEvaluatedKey`, `HasMorePages` |
| `UpdateItemRequestBuilder` | `UpdateItemOperationResponse` | `ConsumedCapacity`, `ResponseMetadata`, `ItemCollectionMetrics` |
| `DeleteItemRequestBuilder` | `DeleteItemOperationResponse` | `ConsumedCapacity`, `ResponseMetadata`, `ItemCollectionMetrics` |
| `PutItemRequestBuilder` | `PutItemOperationResponse` | `ConsumedCapacity`, `ResponseMetadata`, `ItemCollectionMetrics` |

#### Transaction and Batch Direct SDK Support

For transactions and batches, the existing `DynamoDbTransactions` and `DynamoDbBatch` static classes provide direct SDK request methods:

```csharp
public static class DynamoDbTransactions
{
    /// <summary>
    /// Executes a TransactWriteItemsRequest directly.
    /// </summary>
    public static async Task<TransactWriteItemsResponse> WriteAsync(
        IAmazonDynamoDB client,
        TransactWriteItemsRequest request,
        CancellationToken cancellationToken = default)
    {
        await client.TransactWriteItemsAsync(request, cancellationToken);
    }
    
    /// <summary>
    /// Executes a TransactGetItemsRequest and returns raw responses.
    /// </summary>
    public static async Task<TransactGetItemsResponse> GetAsync(
        IAmazonDynamoDB client,
        TransactGetItemsRequest request,
        CancellationToken cancellationToken = default)
    {
        return await client.TransactGetItemsAsync(request, cancellationToken);
    }
}
```

### 6. GeoHash Query Bug Fix

#### Root Cause

In `examples/StoreLocator/Program.cs`, the GeoHash query uses:

```csharp
.Where($"geohash_cell BETWEEN {0} AND {1}", cell.Split(':')[0], cell.Split(':')[1])
```

The `$` prefix makes this a C# interpolated string, so `{0}` and `{1}` are literal text, not format placeholders. The expression becomes `"geohash_cell BETWEEN 0 AND 1"` which is invalid DynamoDB syntax.

#### Fix

Remove the `$` prefix to use format string syntax:

```csharp
// Before (broken)
.Where($"geohash_cell BETWEEN {0} AND {1}", cell.Split(':')[0], cell.Split(':')[1])

// After (fixed)
.Where("geohash_cell BETWEEN {0} AND {1}", cell.Split(':')[0], cell.Split(':')[1])
```

## Data Models

### EntityMetadata Enhancement

```csharp
public class EntityMetadata
{
    // Existing properties...
    
    /// <summary>
    /// Indicates this is a DynamicEntity that should skip key validation
    /// in expression translation.
    /// </summary>
    public bool IsDynamicEntity { get; init; }
}
```

### DynamicTableKeyOptions

```csharp
public class DynamicTableKeyOptions
{
    public string PartitionKeyName { get; set; } = "pk";
    public ScalarAttributeType PartitionKeyType { get; set; } = ScalarAttributeType.S;
    public string? SortKeyName { get; set; }
    public ScalarAttributeType? SortKeyType { get; set; }
}
```

## Correctness Properties

*A property is a characteristic or behavior that should hold true across all valid executions of a system-essentially, a formal statement about what the system should do. Properties serve as the bridge between human-readable specifications and machine-verifiable correctness guarantees.*

### Property 1: Generated table class backward compatibility
*For any* existing code using a generated table class (entity accessors, index accessors, Query/Get/Put/Update/Delete methods), the code should compile and behave identically after DynamoDbTableBase removal.
**Validates: Requirements 1.5, 10.1, 10.2, 10.3**

### Property 2: DynamicEntity round-trip consistency
*For any* DynamoDB item with arbitrary attributes, converting to DynamicEntity and back to AttributeValue dictionary should produce an equivalent dictionary.
**Validates: Requirements 5.6, 7.1**

### Property 3: DynamicTable key operations consistency
*For any* DynamicTable with configured key options, Get/Delete/Update operations using typed key parameters should produce the same results as operations using equivalent AttributeValue parameters.
**Validates: Requirements 5.2, 5.3, 5.4, 5.5**

### Property 4: PartiQL hydration consistency
*For any* PartiQL SELECT query that returns items, the hydrated entities should be equivalent to entities hydrated from the same items via Query or Scan operations.
**Validates: Requirements 3.3, 3.4**

### Property 5: Direct SDK request hydration consistency
*For any* pre-built SDK request (GetItemRequest, QueryRequest, etc.) injected via `WithRequest()` and executed via the builder's async methods, the hydrated entities should be equivalent to entities hydrated from the same response via the fluent builder configuration methods.
**Validates: Requirements 4.1, 4.5, 4.6**

### Property 6: GeoHash BETWEEN query validity
*For any* GeoHash spatial query, the generated KeyConditionExpression should be valid DynamoDB syntax with properly formatted string values.
**Validates: Requirements 6.1, 6.2, 6.3**

### Property 7: DynamicEntity expression translation
*For any* lambda expression using DynamicFields indexer on DynamicEntity, the expression translator should generate valid DynamoDB expressions without key validation errors.
**Validates: Requirements 5.7, 5.8, 7.1, 7.2, 7.3, 7.4, 7.5, 7.6**

## Error Handling

### DynamicTable Errors

| Scenario | Exception | Message |
|----------|-----------|---------|
| Get/Delete/Update with typed keys but no KeyOptions | `InvalidOperationException` | "Key options must be configured to use typed key methods. Use the constructor overload that accepts DynamicTableKeyOptions." |
| Get/Delete with sort key but KeyOptions has no sort key | `InvalidOperationException` | "Sort key was provided but DynamicTableKeyOptions does not define a sort key." |
| Key type mismatch | `ArgumentException` | "Expected {expectedType} for {keyName} but received {actualType}." |

### PartiQL Errors

| Scenario | Exception | Message |
|----------|-----------|---------|
| Invalid PartiQL syntax | `AmazonDynamoDBException` | Propagated from SDK |
| Hydration failure | `DynamoDbMappingException` | "Failed to hydrate PartiQL result to {entityType}." |

### Direct SDK Request Errors

| Scenario | Exception | Message |
|----------|-----------|---------|
| SDK request failure | `AmazonDynamoDBException` | Propagated from SDK |
| Hydration failure | `DynamoDbMappingException` | "Failed to hydrate response to {entityType}." |

## Testing Strategy

### Unit Tests

1. **DynamicEntity serialization** - Test ToDynamoDb/FromDynamoDb with various attribute types
2. **DynamicTable key methods** - Test typed key methods with different key configurations
3. **PartiQL statement formatting** - Test placeholder replacement and parameter conversion
4. **Expression translator** - Test DynamicEntity expressions skip key validation

### Property-Based Tests

1. **Round-trip property** - DynamicEntity serialization round-trip preserves all attributes
2. **Key equivalence property** - Typed key methods produce same results as AttributeValue methods
3. **Hydration equivalence property** - Direct SDK hydration matches builder hydration

### Integration Tests

1. **DynamicTable CRUD** - Full CRUD operations against DynamoDB Local
2. **PartiQL queries** - Execute PartiQL statements and verify hydration
3. **GeoHash queries** - Verify BETWEEN clause generates valid syntax
4. **Backward compatibility** - Existing example projects compile and run correctly

