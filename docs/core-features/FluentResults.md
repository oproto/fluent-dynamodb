# FluentResults Integration

This guide covers the comprehensive FluentResults integration with Oproto.FluentDynamoDb, providing a `Result<T>` pattern alternative to the traditional async/exception-based API.

## Overview

The `Oproto.FluentDynamoDb.FluentResults` package integrates [FluentResults](https://github.com/altmann/FluentResults) with FluentDynamoDb, allowing you to use explicit success/failure handling instead of exceptions. This approach:

- Makes error handling explicit and type-safe
- Provides rich error context with typed error objects
- Supports error aggregation for batch and transaction operations
- Maintains full AOT compatibility

## Installation

```bash
dotnet add package Oproto.FluentDynamoDb.FluentResults
```

## Quick Start

```csharp
using Oproto.FluentDynamoDb.FluentResults;

// Traditional async pattern (throws exceptions)
try
{
    var user = await table.Users.Get(userId).GetItemAsync();
    // Process user
}
catch (ConditionalCheckFailedException)
{
    // Handle concurrent modification
}
catch (AmazonDynamoDBException ex)
{
    // Handle other DynamoDB errors
}

// FluentResults pattern (returns Result<T>)
var result = await table.Users.Get(userId).GetItemAsyncResult();

if (result.IsSuccess)
{
    var user = result.Value;
    // Process user
}
else
{
    // Handle errors with pattern matching
    foreach (var error in result.Errors.OfType<DynamoDbError>())
    {
        Console.WriteLine($"[{error.ErrorCode}]: {error.Message}");
    }
}
```

## Core CRUD Operations

### Get Operations

```csharp
// Basic get
var result = await table.Users.Get(userId).GetItemAsyncResult();

// With consistent read
var result = await table.Users.Get(userId)
    .UsingConsistentRead()
    .GetItemAsyncResult();

// With projection
var result = await table.Users.Get(userId)
    .WithProjection("name, email, status")
    .GetItemAsyncResult();

// With blob storage support
var result = await table.Users.Get(userId)
    .GetItemAsyncResult(blobProvider);

// Handle result
if (result.IsSuccess)
{
    if (result.Value is null)
        Console.WriteLine("User not found");
    else
        Console.WriteLine($"Found user: {result.Value.Name}");
}
```

### Put Operations

```csharp
// Simple put
var result = await table.Users.Put(user).PutAsyncResult();

// Conditional put (create only)
var result = await table.Users.Put(user)
    .Where(x => x.UserId.AttributeNotExists())
    .PutAsyncResult();

// With blob storage
var result = await table.Users.Put(user).PutAsyncResult(blobProvider);

// Handle result
if (result.IsFailed)
{
    var lockingError = result.Errors.OfType<OptimisticLockingError>().FirstOrDefault();
    if (lockingError != null)
        Console.WriteLine("Item already exists");
}
```

### Update Operations

```csharp
// Lambda expression update
var result = await table.Users.Update(userId)
    .Set(x => new UserUpdateModel { 
        Name = "New Name", 
        Age = x.Age + 1,
        UpdatedAt = DateTime.UtcNow 
    })
    .UpdateAsyncResult();

// Optimistic locking pattern
var result = await table.Users.Update(userId)
    .Set(x => new UserUpdateModel { 
        Status = "active",
        Version = x.Version + 1 
    })
    .Where(x => x.Version == currentVersion)
    .UpdateAsyncResult();

if (result.IsFailed && result.Errors.OfType<OptimisticLockingError>().Any())
{
    // Concurrent modification detected - refresh and retry
}

// Format string update
var result = await table.Users.Update(userId)
    .Set("SET #name = {0}, age = age + {1}", "New Name", 1)
    .WithAttribute("#name", "name")
    .UpdateAsyncResult();
```

### Delete Operations

```csharp
// Simple delete
var result = await table.Users.Delete(userId).DeleteAsyncResult();

// Conditional delete
var result = await table.Users.Delete(userId)
    .Where(x => x.Status == "inactive")
    .DeleteAsyncResult();

// Handle result
if (result.IsSuccess)
    Console.WriteLine("User deleted successfully");
```

### Query Operations

```csharp
// Lambda expression query
var result = await table.Users.Query()
    .Where(x => x.TenantId == tenantId && x.CreatedAt > startDate)
    .ToListAsyncResult();

// With filter
var result = await table.Users.Query()
    .Where(x => x.TenantId == tenantId)
    .WithFilter(x => x.Status == "active" && x.Email.AttributeExists())
    .ToListAsyncResult();

// With pagination
var result = await table.Users.Query()
    .Where(x => x.TenantId == tenantId)
    .Take(25)
    .ToListAsyncResult();

// With blob storage
var result = await table.Users.Query()
    .Where(x => x.TenantId == tenantId)
    .ToListAsyncResult(blobProvider);

// Handle result
if (result.IsSuccess)
{
    foreach (var user in result.Value)
        Console.WriteLine($"User: {user.Name}");
}
```

### Scan Operations

```csharp
// Full table scan (requires [Scannable] attribute on entity)
var result = await table.Logs.Scan().ToListAsyncResult();

// With filter
var result = await table.Logs.Scan()
    .WithFilter(x => x.Level == "ERROR" && x.Timestamp > cutoffDate)
    .Take(100)
    .ToListAsyncResult();

// With blob storage
var result = await table.Logs.Scan()
    .ToListAsyncResult(blobProvider);
```

### Composite Entity Operations

```csharp
// Single composite entity
var result = await table.Orders.Query()
    .Where(x => x.CustomerId == customerId && x.OrderId == orderId)
    .ToCompositeEntityAsyncResult();

// List of composite entities
var result = await table.Orders.Query()
    .Where(x => x.CustomerId == customerId)
    .ToCompositeEntityListAsyncResult();

// Scan composite entities
var result = await table.Orders.Scan()
    .WithFilter(x => x.Status == "pending")
    .ToCompositeEntityListAsyncResult();
```

## Batch Operations

### Batch Get

```csharp
// Basic batch get
var result = await DynamoDbBatch.Get
    .Add(table.Users.Get(userId1))
    .Add(table.Users.Get(userId2))
    .Add(table.Users.Get(userId3))
    .ExecuteAsyncResult();

if (result.IsSuccess)
{
    var response = result.Value;
    var users = response.Responses["Users"];
    
    // Check for unprocessed keys (partial success)
    var warnings = result.Reasons.OfType<UnprocessedItemsWarning>();
    if (warnings.Any())
    {
        var warning = warnings.First();
        Console.WriteLine($"Warning: {warning.UnprocessedCount} keys not processed");
        Console.WriteLine($"Tables affected: {string.Join(", ", warning.TableNames)}");
    }
}
```

### Batch Get with Tuple Mapping

```csharp
// Two items
var result = await DynamoDbBatch.Get
    .Add(table.Users.Get(userId))
    .Add(table.Orders.Get(customerId, orderId))
    .ExecuteAndMapAsyncResult<User, Order>();

if (result.IsSuccess)
{
    var (user, order) = result.Value;
    if (user != null) Console.WriteLine($"User: {user.Name}");
    if (order != null) Console.WriteLine($"Order: {order.OrderId}");
}

// Up to 8 items supported
var result = await DynamoDbBatch.Get
    .Add(table.Users.Get(userId))
    .Add(table.Orders.Get(orderId))
    .Add(table.Products.Get(productId))
    .Add(table.Inventory.Get(warehouseId, productId))
    .ExecuteAndMapAsyncResult<User, Order, Product, Inventory>();
```

### Batch Write

```csharp
var result = await DynamoDbBatch.Write
    .Add(table.Users.Put(newUser))
    .Add(table.Users.Put(updatedUser))
    .Add(table.Users.Delete(oldUserId))
    .ExecuteAsyncResult();

if (result.IsSuccess)
{
    // Check for unprocessed items
    var warnings = result.Reasons.OfType<UnprocessedItemsWarning>();
    if (warnings.Any())
    {
        // Some items weren't processed - may need retry
        var warning = warnings.First();
        Console.WriteLine($"Unprocessed: {warning.UnprocessedCount} items");
    }
}
```

### Batch PartiQL

```csharp
var result = await DynamoDbBatch.PartiQL
    .Add(table.ExecutePartiQL<User>("SELECT * FROM Users WHERE pk = ?", userId1))
    .Add(table.ExecutePartiQL<User>("SELECT * FROM Users WHERE pk = ?", userId2))
    .ExecuteAsyncResult();

if (result.IsSuccess)
{
    var response = result.Value;
    var user1 = response.GetItem<User>(0);
    var user2 = response.GetItem<User>(1);
    
    // Check for statement errors
    var warnings = result.Reasons.OfType<BatchStatementErrorWarning>();
    foreach (var warning in warnings)
    {
        Console.WriteLine($"Statement {warning.StatementIndex} failed: {warning.ErrorCode}");
    }
}
```

## Transaction Operations

### Transaction Write

```csharp
var result = await DynamoDbTransactions.Write
    .Add(table.Users.Put(newUser))
    .Add(table.Accounts.Update(accountId)
        .Set(x => new AccountUpdateModel { Balance = x.Balance - amount }))
    .Add(table.Orders.Put(order)
        .Where(x => x.OrderId.AttributeNotExists()))
    .Add(table.Audit.ConditionCheck(auditId)
        .Where(x => x.Version == expectedVersion))
    .ExecuteAsyncResult();

if (result.IsFailed)
{
    // Handle transaction-specific errors
    var cancelledError = result.Errors.OfType<TransactionCancelledError>().FirstOrDefault();
    if (cancelledError != null)
    {
        Console.WriteLine("Transaction cancelled:");
        foreach (var reason in cancelledError.CancellationReasons)
            Console.WriteLine($"  - {reason}");
    }
    
    var conflictError = result.Errors.OfType<TransactionConflictError>().FirstOrDefault();
    if (conflictError != null)
        Console.WriteLine("Transaction conflict - another transaction is modifying these items");
}
```

### Transaction Get

```csharp
var result = await DynamoDbTransactions.Get
    .Add(table.Users.Get(userId))
    .Add(table.Accounts.Get(accountId))
    .Add(table.Orders.Get(customerId, orderId))
    .ExecuteAsyncResult();

if (result.IsSuccess)
{
    var response = result.Value;
    var userItem = response.Responses[0].Item;
    var accountItem = response.Responses[1].Item;
    var orderItem = response.Responses[2].Item;
}
```

### Idempotent Transactions

```csharp
var clientToken = Guid.NewGuid().ToString();

var result = await DynamoDbTransactions.Write
    .Add(table.Orders.Put(order))
    .Add(table.Inventory.Update(productId)
        .Set(x => new InventoryUpdateModel { Quantity = x.Quantity - orderQuantity }))
    .WithClientRequestToken(clientToken)
    .ExecuteAsyncResult();

// If retried with same token, DynamoDB returns success without re-executing
if (result.IsFailed)
{
    var idempotencyError = result.Errors.OfType<IdempotencyError>().FirstOrDefault();
    if (idempotencyError != null)
        Console.WriteLine("Token mismatch - different parameters used with same token");
}
```

## PartiQL Operations

```csharp
// SELECT query
var result = await table.ExecutePartiQL<User>(
    "SELECT * FROM Users WHERE pk = ?", userId)
    .ToListAsyncResult();

// SELECT with date formatting
var result = await table.ExecutePartiQL<Order>(
    "SELECT * FROM Orders WHERE pk = ? AND created > ?",
    customerId, DateTime.UtcNow.AddDays(-7))
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

if (result.IsFailed)
{
    var coordError = result.Errors.OfType<InvalidCoordinatesError>().FirstOrDefault();
    if (coordError != null)
        Console.WriteLine($"Invalid coordinates: lat={coordError.Latitude}, lon={coordError.Longitude}");
}

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

// Index-based spatial query
var result = await table.gsi1.SpatialQueryAsyncResult<Store>(
    locationSelector: s => s.Location,
    spatialIndexType: SpatialIndexType.H3,
    precision: 7,
    center: new GeoLocation(47.6062, -122.3321),
    radiusKilometers: 5.0,
    queryBuilder: (builder, cell, pagination) => builder
        .Where(x => x.H3Cell == cell));
```

## Error Handling

### Error Type Hierarchy

All errors inherit from `DynamoDbError`:

```
DynamoDbError (abstract)
├── TransactionError (abstract)
│   ├── TransactionCancelledError
│   ├── TransactionConflictError
│   ├── TransactionInProgressError
│   ├── OptimisticLockingError
│   ├── ProvisionedThroughputExceededError
│   ├── RequestLimitExceededError
│   ├── ResourceNotFoundError
│   ├── IdempotencyError
│   ├── CollectionSizeLimitError
│   ├── ServiceError
│   ├── ExpiredIteratorError
│   └── LimitExceededError
├── ConfigurationError (abstract)
│   ├── MissingClientError
│   ├── EncryptionConfigurationError
│   ├── WriteTransactionRequiredError
│   ├── ClientMismatchError
│   ├── EmptyOperationError
│   ├── OperationLimitExceededError
│   └── UpdateExpressionConflictError
├── MappingError
│   ├── DiscriminatorMismatchError
│   ├── ProjectionValidationError
│   └── ExpressionTranslationError
├── ValidationError (abstract)
│   ├── SchemaValidationError
│   ├── EmptyCollectionError
│   └── FormatStringError
├── StorageError (abstract)
│   ├── BlobStorageError
│   ├── EncryptionError
│   └── DecryptionError
└── GeospatialError (abstract)
    ├── SpatialQueryError
    ├── InvalidCoordinatesError
    └── InvalidBoundingBoxError
```

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
                // Concurrent modification - refresh and retry
                await RefreshAndRetry();
                break;
                
            case TransactionCancelledError tce:
                // Log all cancellation reasons
                foreach (var reason in tce.CancellationReasons)
                    logger.LogWarning("Transaction cancelled: {Reason}", reason);
                break;
                
            case TransactionConflictError:
                // Another transaction is in progress - wait and retry
                await Task.Delay(100);
                await RetryOperation();
                break;
                
            case OperationLimitExceededError ole:
                // Too many operations - split into smaller batches
                logger.LogError("Limit exceeded: {Actual}/{Limit}", ole.ActualCount, ole.Limit);
                break;
                
            case EncryptionError ee:
                // Encryption failed
                logger.LogError("Encryption failed for field: {Field}", ee.FieldName);
                break;
                
            case BlobStorageError bse:
                // Blob storage operation failed
                logger.LogError("Blob storage error for key: {Key}", bse.BlobKey);
                break;
                
            case MappingError me:
                // Entity mapping failed
                logger.LogError("Mapping error for {Entity}.{Field}", me.EntityType, me.FieldName);
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
| `IDEMPOTENCY_MISMATCH` | `IdempotencyError` | Token mismatch |
| `COLLECTION_SIZE_LIMIT` | `CollectionSizeLimitError` | Item collection too large |
| `SERVICE_ERROR` | `ServiceError` | DynamoDB internal error |
| `EXPIRED_ITERATOR` | `ExpiredIteratorError` | Query iterator expired |
| `LIMIT_EXCEEDED` | `LimitExceededError` | DynamoDB limit exceeded |
| `MISSING_CLIENT` | `MissingClientError` | No DynamoDB client configured |
| `ENCRYPTION_CONFIGURATION_ERROR` | `EncryptionConfigurationError` | Encryption not configured |
| `WRITE_TRANSACTION_REQUIRED` | `WriteTransactionRequiredError` | Entity requires transaction |
| `CLIENT_MISMATCH` | `ClientMismatchError` | Mixed clients in batch |
| `EMPTY_OPERATION` | `EmptyOperationError` | No operations in batch |
| `OPERATION_LIMIT_EXCEEDED` | `OperationLimitExceededError` | Too many operations |
| `UPDATE_EXPRESSION_CONFLICT` | `UpdateExpressionConflictError` | Mixed update approaches |
| `MAPPING_ERROR` | `MappingError` | Entity mapping failed |
| `DISCRIMINATOR_MISMATCH` | `DiscriminatorMismatchError` | Wrong discriminator value |
| `PROJECTION_VALIDATION_ERROR` | `ProjectionValidationError` | Projection validation failed |
| `EXPRESSION_TRANSLATION_ERROR` | `ExpressionTranslationError` | Expression translation failed |
| `SCHEMA_VALIDATION_FAILED` | `SchemaValidationError` | Schema validation failed |
| `EMPTY_COLLECTION` | `EmptyCollectionError` | Empty collection provided |
| `FORMAT_STRING_ERROR` | `FormatStringError` | Invalid format string |
| `BLOB_STORAGE_ERROR` | `BlobStorageError` | Blob storage operation failed |
| `ENCRYPTION_FAILED` | `EncryptionError` | Field encryption failed |
| `DECRYPTION_FAILED` | `DecryptionError` | Field decryption failed |
| `SPATIAL_QUERY_ERROR` | `SpatialQueryError` | Geospatial query failed |
| `INVALID_COORDINATES` | `InvalidCoordinatesError` | Invalid lat/long |
| `INVALID_BOUNDING_BOX` | `InvalidBoundingBoxError` | Invalid bounding box |

## UseFluentResults Attribute

The `[UseFluentResults]` attribute generates Result-returning convenience methods on entity accessors:

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
```

### Generated Methods

```csharp
// These methods are generated on the entity accessor:
var result = await table.Users.GetAsyncResult(userId);
var result = await table.Users.PutAsyncResult(user);
var result = await table.Users.DeleteAsyncResult(userId);
var result = await table.Users.QueryAsyncResult(x => x.TenantId == tenantId);
```

### HideGeneratedAsyncMethods Option

By default, traditional async methods are hidden when `[UseFluentResults]` is applied:

```csharp
// Default: Only Result methods generated
[DynamoDbTable("Users")]
[UseFluentResults]
public partial class User { ... }

// Both traditional and Result methods generated
[DynamoDbTable("Users")]
[UseFluentResults(HideGeneratedAsyncMethods = false)]
public partial class User { ... }

// Now both are available:
var user = await table.Users.GetAsync(userId);           // Traditional
var result = await table.Users.GetAsyncResult(userId);   // FluentResults
```

## Cancellation Token Support

All FluentResults methods support cancellation tokens. When cancelled, `OperationCanceledException` is re-thrown (not wrapped):

```csharp
var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));

try
{
    var result = await table.Users.Query()
        .Where(x => x.TenantId == tenantId)
        .ToListAsyncResult(cts.Token);
        
    if (result.IsSuccess)
        ProcessUsers(result.Value);
}
catch (OperationCanceledException)
{
    Console.WriteLine("Operation was cancelled");
}
```

## Migration Guide

### From Traditional Async

```csharp
// Before: Traditional async with try/catch
try
{
    var user = await table.Users.Get(userId).GetItemAsync();
    if (user == null)
    {
        // Handle not found
    }
    // Process user
}
catch (ConditionalCheckFailedException)
{
    // Handle concurrent modification
}
catch (ProvisionedThroughputExceededException)
{
    // Handle throttling
}
catch (AmazonDynamoDBException ex)
{
    // Handle other errors
    logger.LogError(ex, "DynamoDB error");
}

// After: FluentResults pattern
var result = await table.Users.Get(userId).GetItemAsyncResult();

if (result.IsSuccess)
{
    if (result.Value == null)
    {
        // Handle not found
    }
    // Process user
}
else
{
    foreach (var error in result.Errors.OfType<DynamoDbError>())
    {
        switch (error)
        {
            case OptimisticLockingError:
                // Handle concurrent modification
                break;
            case ProvisionedThroughputExceededError:
                // Handle throttling
                break;
            default:
                logger.LogError("DynamoDB error [{Code}]: {Message}", 
                    error.ErrorCode, error.Message);
                break;
        }
    }
}
```

### Gradual Migration

You can migrate gradually by using both patterns:

```csharp
[DynamoDbTable("Users")]
[UseFluentResults(HideGeneratedAsyncMethods = false)]
public partial class User { ... }

// Existing code continues to work
var user = await table.Users.GetAsync(userId);

// New code can use Result pattern
var result = await table.Users.GetAsyncResult(userId);
```

## Best Practices

### 1. Use Pattern Matching for Error Handling

```csharp
// Good: Pattern match on specific error types
if (result.IsFailed)
{
    var error = result.Errors.OfType<DynamoDbError>().FirstOrDefault();
    switch (error)
    {
        case OptimisticLockingError: /* retry */ break;
        case TransactionConflictError: /* wait and retry */ break;
        default: /* log and fail */ break;
    }
}
```

### 2. Check for Warnings in Batch Operations

```csharp
// Good: Always check for unprocessed items
var result = await DynamoDbBatch.Write.Add(...).ExecuteAsyncResult();
if (result.IsSuccess)
{
    var warnings = result.Reasons.OfType<UnprocessedItemsWarning>();
    if (warnings.Any())
        // Handle partial success
}
```

### 3. Use ErrorCode for Programmatic Handling

```csharp
// Good: Use ErrorCode for switch statements
foreach (var error in result.Errors.OfType<DynamoDbError>())
{
    switch (error.ErrorCode)
    {
        case "OPTIMISTIC_LOCKING_FAILED":
            // Handle
            break;
    }
}
```

### 4. Access Inner Exception When Needed

```csharp
// Good: Access original exception for detailed logging
foreach (var error in result.Errors.OfType<DynamoDbError>())
{
    if (error.InnerException != null)
        logger.LogError(error.InnerException, "Original exception");
}
```
