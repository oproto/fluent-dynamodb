# Oproto.FluentDynamoDb.FluentResults

FluentResults extensions for [Oproto.FluentDynamoDb](https://www.nuget.org/packages/Oproto.FluentDynamoDb), providing `Result<T>` return patterns instead of exceptions.

## Installation

```bash
dotnet add package Oproto.FluentDynamoDb.FluentResults
```

## Overview

This package integrates [FluentResults](https://github.com/altmann/FluentResults) with Oproto.FluentDynamoDb, allowing you to use the Result pattern for error handling instead of exceptions. All DynamoDB operations have Result-returning equivalents that wrap exceptions in typed error objects.

## Quick Start

```csharp
using Oproto.FluentDynamoDb.FluentResults;

// Traditional async (throws exceptions)
var user = await table.Users.Get(userId).GetItemAsync();

// FluentResults pattern (returns Result<T>)
var result = await table.Users.Get(userId).GetItemAsyncResult();

if (result.IsSuccess)
{
    var user = result.Value;
    // Process user
}
else
{
    // Handle errors with type-safe error handling
    foreach (var error in result.Errors)
    {
        if (error is TransactionCancelledError tce)
            Console.WriteLine($"Transaction cancelled: {string.Join(", ", tce.CancellationReasons)}");
        else
            Console.WriteLine($"Error [{error.ErrorCode}]: {error.Message}");
    }
}
```

## Core CRUD Operations

### Get Operations

```csharp
// Single item retrieval
var result = await table.Users.Get(userId).GetItemAsyncResult();

// With blob storage support
var result = await table.Users.Get(userId).GetItemAsyncResult(blobProvider);

// With consistent read
var result = await table.Users.Get(userId)
    .UsingConsistentRead()
    .GetItemAsyncResult();
```

### Put Operations

```csharp
// Simple put
var result = await table.Users.Put(user).PutAsyncResult();

// With condition (create only)
var result = await table.Users.Put(user)
    .Where(x => x.UserId.AttributeNotExists())
    .PutAsyncResult();

// With blob storage
var result = await table.Users.Put(user).PutAsyncResult(blobProvider);
```

### Update Operations

```csharp
// Lambda expression update
var result = await table.Users.Update(userId)
    .Set(x => new UserUpdateModel { Name = "New Name", Age = x.Age + 1 })
    .UpdateAsyncResult();

// With condition (optimistic locking)
var result = await table.Users.Update(userId)
    .Set(x => new UserUpdateModel { Version = x.Version + 1 })
    .Where(x => x.Version == currentVersion)
    .UpdateAsyncResult();
```

### Delete Operations

```csharp
// Simple delete
var result = await table.Users.Delete(userId).DeleteAsyncResult();

// With condition
var result = await table.Users.Delete(userId)
    .Where(x => x.Status == "inactive")
    .DeleteAsyncResult();
```

### Query Operations

```csharp
// Query with lambda expression
var result = await table.Users.Query()
    .Where(x => x.TenantId == tenantId && x.CreatedAt > startDate)
    .ToListAsyncResult();

// With filter and pagination
var result = await table.Users.Query()
    .Where(x => x.TenantId == tenantId)
    .WithFilter(x => x.Status == "active")
    .Take(25)
    .ToListAsyncResult();

// Composite entity queries
var result = await table.Orders.Query()
    .Where(x => x.CustomerId == customerId)
    .ToCompositeEntityAsyncResult();

var listResult = await table.Orders.Query()
    .Where(x => x.CustomerId == customerId)
    .ToCompositeEntityListAsyncResult();
```

### Scan Operations

```csharp
// Full table scan (requires [Scannable] attribute)
var result = await table.Logs.Scan().ToListAsyncResult();

// With filter
var result = await table.Logs.Scan()
    .WithFilter(x => x.Level == "ERROR")
    .Take(100)
    .ToListAsyncResult();
```

## Batch Operations

```csharp
// Batch Get
var result = await DynamoDbBatch.Get
    .Add(table.Users.Get(userId1))
    .Add(table.Users.Get(userId2))
    .ExecuteAsyncResult();

if (result.IsSuccess)
{
    // Check for warnings about unprocessed keys
    var warnings = result.Reasons.OfType<UnprocessedItemsWarning>();
    if (warnings.Any())
        Console.WriteLine($"Warning: {warnings.First().UnprocessedCount} items not processed");
}

// Batch Get with tuple mapping
var result = await DynamoDbBatch.Get
    .Add(table.Users.Get(userId))
    .Add(table.Orders.Get(customerId, orderId))
    .ExecuteAndMapAsyncResult<User, Order>();

if (result.IsSuccess)
{
    var (user, order) = result.Value;
}

// Batch Write
var result = await DynamoDbBatch.Write
    .Add(table.Users.Put(user1))
    .Add(table.Users.Delete(oldUserId))
    .ExecuteAsyncResult();

// Batch PartiQL
var result = await DynamoDbBatch.PartiQL
    .Add(table.ExecutePartiQL<User>("SELECT * FROM Users WHERE pk = ?", userId))
    .ExecuteAsyncResult();
```

## Transaction Operations

```csharp
// Transaction Write
var result = await DynamoDbTransactions.Write
    .Add(table.Users.Put(newUser))
    .Add(table.Accounts.Update(accountId)
        .Set(x => new AccountUpdateModel { Balance = x.Balance - 100 }))
    .Add(table.Orders.Put(order)
        .Where(x => x.OrderId.AttributeNotExists()))
    .ExecuteAsyncResult();

if (result.IsFailed)
{
    // Handle transaction-specific errors
    var cancelledError = result.Errors.OfType<TransactionCancelledError>().FirstOrDefault();
    if (cancelledError != null)
    {
        foreach (var reason in cancelledError.CancellationReasons)
            Console.WriteLine($"Cancellation reason: {reason}");
    }
}

// Transaction Get
var result = await DynamoDbTransactions.Get
    .Add(table.Users.Get(userId))
    .Add(table.Accounts.Get(accountId))
    .ExecuteAsyncResult();
```

## PartiQL Operations

```csharp
// SELECT query
var result = await table.ExecutePartiQL<User>(
    "SELECT * FROM Users WHERE pk = ?", userId)
    .ToListAsyncResult();

// INSERT/UPDATE/DELETE
var result = await table.ExecutePartiQL(
    "UPDATE Users SET name = ? WHERE pk = ?", newName, userId)
    .ExecuteAsyncResult();
```

## Geospatial Operations

```csharp
// Proximity query
var result = await table.SpatialQueryAsyncResult<Store>(
    locationSelector: s => s.Location,
    spatialIndexType: SpatialIndexType.GeoHash,
    precision: 6,
    center: new GeoLocation(47.6062, -122.3321),
    radiusKilometers: 10.0,
    queryBuilder: (builder, cell, pagination) => builder
        .Where(x => x.GeoHashCell == cell)
        .Take(pagination.PageSize ?? 100));

// Bounding box query
var result = await table.SpatialQueryAsyncResult<Store>(
    locationSelector: s => s.Location,
    spatialIndexType: SpatialIndexType.S2,
    precision: 12,
    boundingBox: new GeoBoundingBox(
        southwest: new GeoLocation(47.5, -122.5),
        northeast: new GeoLocation(47.7, -122.1)),
    queryBuilder: (builder, cell, pagination) => builder
        .Where(x => x.S2Cell == cell));
```

## Error Handling

### Error Type Hierarchy

All errors inherit from `DynamoDbError`, which provides:
- `ErrorCode`: A string code for programmatic error handling
- `InnerException`: The original exception that caused the error
- `Message`: A human-readable error message


### Error Categories

| Category | Error Types | Description |
|----------|-------------|-------------|
| **Transaction** | `TransactionCancelledError`, `TransactionConflictError`, `TransactionInProgressError`, `OptimisticLockingError` | Transaction-related failures |
| **Configuration** | `MissingClientError`, `ClientMismatchError`, `EmptyOperationError`, `OperationLimitExceededError` | Configuration and setup errors |
| **Mapping** | `MappingError`, `DiscriminatorMismatchError`, `ProjectionValidationError`, `ExpressionTranslationError` | Entity mapping failures |
| **Validation** | `SchemaValidationError`, `EmptyCollectionError`, `FormatStringError` | Input validation errors |
| **Storage** | `BlobStorageError`, `EncryptionError`, `DecryptionError` | Storage and encryption failures |
| **Geospatial** | `SpatialQueryError`, `InvalidCoordinatesError`, `InvalidBoundingBoxError` | Geospatial query errors |

### Pattern Matching on Errors

```csharp
var result = await table.Users.Update(userId)
    .Set(x => new UserUpdateModel { Version = x.Version + 1 })
    .Where(x => x.Version == expectedVersion)
    .UpdateAsyncResult();

if (result.IsFailed)
{
    foreach (var error in result.Errors.OfType<DynamoDbError>())
    {
        switch (error)
        {
            case OptimisticLockingError:
                // Handle concurrent modification - retry with fresh data
                break;
            case TransactionCancelledError tce:
                // Log cancellation reasons
                foreach (var reason in tce.CancellationReasons)
                    logger.LogWarning("Transaction cancelled: {Reason}", reason);
                break;
            case OperationLimitExceededError ole:
                // Handle batch size exceeded
                logger.LogError("Exceeded limit: {Actual}/{Limit}", ole.ActualCount, ole.Limit);
                break;
            case EncryptionError ee:
                // Handle encryption failures
                logger.LogError("Encryption failed for field: {Field}", ee.FieldName);
                break;
            default:
                logger.LogError("DynamoDB error [{Code}]: {Message}", 
                    error.ErrorCode, error.Message);
                break;
        }
    }
}
```

### Error Code Reference

| Error Code | Error Type | Description |
|------------|------------|-------------|
| `TRANSACTION_CANCELLED` | `TransactionCancelledError` | Transaction was cancelled |
| `TRANSACTION_CONFLICT` | `TransactionConflictError` | Concurrent transaction conflict |
| `TRANSACTION_IN_PROGRESS` | `TransactionInProgressError` | Another transaction is active |
| `OPTIMISTIC_LOCKING_FAILED` | `OptimisticLockingError` | Conditional check failed |
| `THROUGHPUT_EXCEEDED` | `ProvisionedThroughputExceededError` | Throughput limit exceeded |
| `REQUEST_LIMIT_EXCEEDED` | `RequestLimitExceededError` | Request rate limit exceeded |
| `RESOURCE_NOT_FOUND` | `ResourceNotFoundError` | Table or index not found |
| `MISSING_CLIENT` | `MissingClientError` | No DynamoDB client configured |
| `CLIENT_MISMATCH` | `ClientMismatchError` | Mixed clients in batch/transaction |
| `EMPTY_OPERATION` | `EmptyOperationError` | No operations in batch/transaction |
| `OPERATION_LIMIT_EXCEEDED` | `OperationLimitExceededError` | Too many operations |
| `MAPPING_ERROR` | `MappingError` | Entity mapping failed |
| `SCHEMA_VALIDATION_FAILED` | `SchemaValidationError` | Schema validation failed |
| `ENCRYPTION_FAILED` | `EncryptionError` | Field encryption failed |
| `DECRYPTION_FAILED` | `DecryptionError` | Field decryption failed |
| `BLOB_STORAGE_ERROR` | `BlobStorageError` | Blob storage operation failed |
| `SPATIAL_QUERY_ERROR` | `SpatialQueryError` | Geospatial query failed |
| `INVALID_COORDINATES` | `InvalidCoordinatesError` | Invalid lat/long coordinates |

## UseFluentResults Attribute

For a cleaner API, use the `[UseFluentResults]` attribute on your entities to generate Result-returning convenience methods:

```csharp
[DynamoDbTable("Users")]
[UseFluentResults]
public partial class User
{
    [PartitionKey]
    [DynamoDbAttribute("pk")]
    public string UserId { get; set; } = string.Empty;
    
    [DynamoDbAttribute("name")]
    public string Name { get; set; } = string.Empty;
}

// Generated convenience methods on entity accessor:
var result = await table.Users.GetAsyncResult(userId);
var result = await table.Users.PutAsyncResult(user);
var result = await table.Users.DeleteAsyncResult(userId);
var result = await table.Users.QueryAsyncResult(x => x.TenantId == tenantId);
```

### HideGeneratedAsyncMethods Option

By default, when `[UseFluentResults]` is applied, traditional async methods are hidden. To generate both:

```csharp
[DynamoDbTable("Users")]
[UseFluentResults(HideGeneratedAsyncMethods = false)]
public partial class User { ... }

// Now both are available:
var user = await table.Users.GetAsync(userId);           // Traditional
var result = await table.Users.GetAsyncResult(userId);   // FluentResults
```

## Cancellation Token Support

All FluentResults extension methods support cancellation tokens. When cancelled, `OperationCanceledException` is re-thrown (not wrapped in a Result):

```csharp
var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));

try
{
    var result = await table.Users.Query()
        .Where(x => x.TenantId == tenantId)
        .ToListAsyncResult(cts.Token);
}
catch (OperationCanceledException)
{
    // Handle cancellation
}
```

## Batch Warnings

Batch operations may succeed with warnings when some items couldn't be processed:

```csharp
var result = await DynamoDbBatch.Write
    .Add(table.Users.Put(user1))
    .Add(table.Users.Put(user2))
    .ExecuteAsyncResult();

if (result.IsSuccess)
{
    // Check for unprocessed items
    var warnings = result.Reasons.OfType<UnprocessedItemsWarning>();
    foreach (var warning in warnings)
    {
        Console.WriteLine($"Unprocessed: {warning.UnprocessedCount} items in tables: {string.Join(", ", warning.TableNames)}");
    }
}
```

## Features

- **Result Pattern**: Replace try/catch with explicit success/failure handling
- **Typed Errors**: All exceptions map to specific error types with relevant context
- **Error Aggregation**: Collect multiple errors from batch and transaction operations
- **Cancellation Support**: Proper cancellation token handling
- **AOT Compatible**: Full support for Native AOT compilation
- **Type Safe**: Strongly-typed results with `Result<T>`

## Links

- 📚 **Documentation**: [fluentdynamodb.dev](https://fluentdynamodb.dev/)
- 🐙 **GitHub**: [github.com/oproto/fluent-dynamodb](https://github.com/oproto/fluent-dynamodb)
- 📦 **NuGet**: [Oproto.FluentDynamoDb.FluentResults](https://www.nuget.org/packages/Oproto.FluentDynamoDb.FluentResults)

## License

MIT License - see [LICENSE](https://github.com/oproto/fluent-dynamodb/blob/main/LICENSE) for details.
