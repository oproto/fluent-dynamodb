# Oproto.FluentDynamoDb.FluentResults

FluentResults extensions for Oproto.FluentDynamoDb providing `Result<T>` return patterns instead of exceptions.

## Installation

```bash
dotnet add package Oproto.FluentDynamoDb.FluentResults
```

## Usage

This package provides extension methods that wrap the Primary API methods to return `Result<T>` instead of throwing exceptions:

```csharp
using Oproto.FluentDynamoDb.FluentResults;

// GetItem - returns Result<T?>
var result = await table.Get
    .WithKey("id", "123")
    .GetItemAsyncResult<MyEntity>();

if (result.IsSuccess)
{
    var entity = result.Value; // T? (nullable entity)
    // Handle success
}
else
{
    // Handle errors without exceptions
    foreach (var error in result.Errors)
    {
        Console.WriteLine(error.Message);
    }
}

// Query - returns Result<List<T>>
var queryResult = await table.Query
    .Where("pk = {0}", "value")
    .ToListAsyncResult<MyEntity>();

if (queryResult.IsSuccess)
{
    var items = queryResult.Value; // List<MyEntity>
    // Process items
}

// Write operations - returns Result (no value)
var updateResult = await table.Update
    .WithKey("id", "123")
    .Set("status", "active")
    .UpdateAsyncResult();

if (updateResult.IsSuccess)
{
    // Access metadata via context
    var context = DynamoDbOperationContext.Current;
    var oldValue = context?.DeserializePreOperationValue<MyEntity>();
}
```

## Available Methods

### Read Operations (Return Result<T>)

- `GetItemAsyncResult<T>()` - For GetItem operations, returns `Result<T?>`
- `ToListAsyncResult<T>()` - For Query and Scan operations (1:1 mapping), returns `Result<List<T>>`
- `ToCompositeEntityAsyncResult<T>()` - For Query operations (N:1 mapping), returns `Result<T?>`
- `ToCompositeEntityListAsyncResult<T>()` - For Query and Scan operations (N:1 mapping), returns `Result<List<T>>`

### Write Operations (Return Result)

- `PutAsyncResult()` - For PutItem operations, returns `Result`
- `UpdateAsyncResult()` - For UpdateItem operations, returns `Result`
- `DeleteAsyncResult()` - For DeleteItem operations, returns `Result`

### Batch Operations

- `ExecuteAsyncResult()` - For BatchGetItem operations, returns `Result`
- `ExecuteAsyncResult()` - For BatchWriteItem operations, returns `Result`
- `ExecuteAsyncResult()` - For TransactGetItems operations, returns `Result`
- `ExecuteAsyncResult()` - For TransactWriteItems operations, returns `Result`

## Accessing Operation Metadata

All methods populate `DynamoDbOperationContext.Current` with operation metadata:

```csharp
var result = await table.Query
    .Where("pk = {0}", "value")
    .ToListAsyncResult<MyEntity>();

if (result.IsSuccess)
{
    var items = result.Value;
    
    // Access metadata via context
    var context = DynamoDbOperationContext.Current;
    Console.WriteLine($"Consumed capacity: {context?.ConsumedCapacity?.CapacityUnits}");
    Console.WriteLine($"Items returned: {context?.ItemCount}");
    Console.WriteLine($"Items scanned: {context?.ScannedCount}");
}
```

## Migration from Old API

**Before (v0.x):**
```csharp
// Old API returned custom wrapper
var result = await table.Get
    .WithKey("id", "123")
    .ExecuteAsyncResult<MyEntity>();

var entity = result.Value.Item; // Wrapper object
var capacity = result.Value.ConsumedCapacity;
```

**After (v1.0+):**
```csharp
// New API returns entity directly
var result = await table.Get
    .WithKey("id", "123")
    .GetItemAsyncResult<MyEntity>();

var entity = result.Value; // Direct entity (T?)

// Access metadata via context
var context = DynamoDbOperationContext.Current;
var capacity = context?.ConsumedCapacity;
```

## Requirements

- Requires `Oproto.FluentDynamoDb` package (v1.0+)
- Entities must implement `IDynamoDbEntity` interface
- .NET 8.0 or later

## Optional Dependency

This package is completely optional. The main `Oproto.FluentDynamoDb` library works independently and this package only adds FluentResults support for those who prefer the Result pattern over exceptions.