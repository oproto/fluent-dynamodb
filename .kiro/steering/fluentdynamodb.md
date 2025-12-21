# FluentDynamoDb API Reference
# Updated 2025-12-21
Compact reference for Oproto.FluentDynamoDb API patterns.

## Setup & DI

```csharp
// ASP.NET Core DI
services.AddSingleton<IAmazonDynamoDB>(new AmazonDynamoDBClient());
services.AddSingleton(sp => new MyTable(
    sp.GetRequiredService<IAmazonDynamoDB>(), "TableName", 
    new FluentDynamoDbOptions().WithLogger(new MicrosoftExtensionsLoggingAdapter(sp.GetRequiredService<ILoggerFactory>()))));

// Manual instantiation
var table = new MyTable(new AmazonDynamoDBClient(), "TableName", new FluentDynamoDbOptions());

// Options
var options = new FluentDynamoDbOptions()
    .WithLogger(logger)
    .WithBlobStorage(new S3BlobProvider(...))
    .WithEncryption(new AwsEncryptionSdkFieldEncryptor(...))
    .UseConsistentRead(true);
```

## Entity Definition

```csharp
// Basic entity (PK only)
[DynamoDbTable("Users")]
public partial class User
{
    [PartitionKey]
    [DynamoDbAttribute("pk")]
    public string UserId { get; set; } = string.Empty;
    
    [DynamoDbAttribute("name")]
    public string Name { get; set; } = string.Empty;
}

// Composite key (PK + SK)
[DynamoDbTable("Orders")]
public partial class Order
{
    [PartitionKey(Prefix = "CUSTOMER")]
    [DynamoDbAttribute("pk")]
    public string CustomerId { get; set; } = string.Empty;
    
    [SortKey(Prefix = "ORDER")]
    [DynamoDbAttribute("sk")]
    public string OrderId { get; set; } = string.Empty;
}

// GSI/LSI
[GlobalSecondaryIndex("gsi1", IsPartitionKey = true)]
[DynamoDbAttribute("gsi1pk")]
public string CategoryId { get; set; }

[LocalSecondaryIndex("lsi1")]
[DynamoDbAttribute("createdAt")]
public DateTime CreatedAt { get; set; }

// GSI/LSI with custom property names
[GlobalSecondaryIndex("status-index", Name = "StatusIndex", IsPartitionKey = true)]
[DynamoDbAttribute("status")]
public string Status { get; set; }

// Type-based table reference (compile-time safe)
[DynamoDbTable(typeof(MyCustomTable))]
public partial class Order { ... }

// Define the table class as partial
public partial class MyCustomTable { }

// Scannable (required for Scan operations)
[DynamoDbTable("Logs")]
[Scannable]
public partial class LogEntry { ... }
```

## Projection Definition

Projections are read-only entity types that represent a subset of attributes from a source entity. They implement both `IReadOnlyEntity` and `IProjectionModel` interfaces.

```csharp
// Define a projection for an entity
[DynamoDbProjection(typeof(Order))]
public partial class OrderSummary
{
    [DynamoDbAttribute("orderId")]
    public string OrderId { get; set; } = string.Empty;

    [DynamoDbAttribute("status")]
    public string Status { get; set; } = string.Empty;

    [DynamoDbAttribute("totalAmount")]
    public decimal TotalAmount { get; set; }
}

// Generated code implements:
// - IReadOnlyEntity (for QueryRequestBuilder compatibility)
// - IProjectionModel<OrderSummary> (for projection expression)
// - Inherits metadata from source entity (table name, keys)
```

## Composite Entity Definition (Multi-Item Entities)

Composite entities span multiple DynamoDB items sharing the same partition key but different sort keys. Use `[RelatedEntity]` to define parent-child relationships that `ToCompositeEntityAsync()` automatically assembles.

```csharp
// Parent entity with related child collection
[DynamoDbTable("invoices", IsDefault = true)]
public partial class Invoice
{
    [PartitionKey(Prefix = "CUSTOMER")]
    [DynamoDbAttribute("pk")]
    public string Pk { get; set; } = string.Empty;

    [SortKey(Prefix = "INVOICE")]
    [DynamoDbAttribute("sk")]
    public string Sk { get; set; } = string.Empty;

    [DynamoDbAttribute("invoiceNumber")]
    public string InvoiceNumber { get; set; } = string.Empty;

    // Related entity collection - automatically populated by ToCompositeEntityAsync
    // Pattern "INVOICE#*#LINE#*" matches sort keys like "INVOICE#INV-001#LINE#1"
    [RelatedEntity("INVOICE#*#LINE#*", EntityType = typeof(InvoiceLine))]
    public List<InvoiceLine> Lines { get; set; } = new();
}

// Child entity with hierarchical sort key
[DynamoDbTable("invoices")]
public partial class InvoiceLine
{
    [PartitionKey(Prefix = "CUSTOMER")]
    [DynamoDbAttribute("pk")]
    public string Pk { get; set; } = string.Empty;

    // Sort key extends parent: "INVOICE#INV-001#LINE#1"
    [SortKey]
    [DynamoDbAttribute("sk")]
    public string Sk { get; set; } = string.Empty;

    [DynamoDbAttribute("lineNumber")]
    public int LineNumber { get; set; }

    [DynamoDbAttribute("amount")]
    public decimal Amount { get; set; }
}
```

### RelatedEntity Attribute

| Property | Description |
|----------|-------------|
| Pattern (positional) | Sort key pattern with `*` wildcards (e.g., `"INVOICE#*#LINE#*"`) |
| `EntityType` | The type to map matching items to (required for collections) |

### Querying Composite Entities

```csharp
// Query with begins_with to fetch parent + all children in one call
var invoice = await table.Invoices.Query()
    .Where(x => x.Pk == pk && x.Sk.StartsWith("INVOICE#INV-001"))
    .ToCompositeEntityAsync<Invoice>();

// invoice.Lines is automatically populated with matching InvoiceLine items

// For multiple composite entities
var invoices = await table.Invoices.Query()
    .Where(x => x.Pk == pk)
    .ToCompositeEntityListAsync<Invoice>();
```

### Key Design Pattern

Hierarchical sort keys enable single-query retrieval:
- Invoice: `sk = "INVOICE#INV-001"`
- Line 1: `sk = "INVOICE#INV-001#LINE#1"`
- Line 2: `sk = "INVOICE#INV-001#LINE#2"`

Query with `begins_with(sk, "INVOICE#INV-001")` returns all items, and `ToCompositeEntityAsync` assembles them into a single Invoice with populated Lines collection.

## Get Operations

```csharp
// Builder pattern
var user = await table.Get<User>().WithKey("pk", userId).GetItemAsync();
var user = await table.Get(userId).GetItemAsync();                    // Generated shortcut
var user = await table.Users.Get(userId).GetItemAsync();              // Entity accessor

// Convenience methods
var user = await table.GetAsync(userId);
var user = await table.Users.GetAsync(userId);

// Options
var user = await table.Users.Get(userId).UsingConsistentRead().WithProjection("name, email").GetItemAsync();
```

## Put Operations

```csharp
// Builder pattern
await table.Put(user).PutAsync();
await table.Users.Put(user).PutAsync();
await table.Users.PutAsync(user);  // Convenience

// With condition - Lambda (Preferred)
await table.Users.Put(user).Where(x => x.UserId == null).PutAsync();

// With condition - Format String
await table.Users.Put(user).Where("attribute_not_exists(pk)").PutAsync();

// With condition - Manual
await table.Users.Put(user).Where("version = :v").WithValue(":v", expectedVersion).PutAsync();
```

## Update Operations

```csharp
// Lambda (Preferred)
await table.Users.Update(userId)
    .Set(x => new UserUpdateModel { Name = "New", Age = x.Age + 1 })
    .UpdateAsync();

// Format String
await table.Users.Update(userId)
    .Set("SET #name = {0}, age = age + {1}", "New", 1)
    .WithAttribute("#name", "name")
    .UpdateAsync();

// Manual
await table.Users.Update(userId)
    .Set("SET #name = :name").WithAttribute("#name", "name").WithValue(":name", "New")
    .UpdateAsync();

// With condition
await table.Users.Update(userId)
    .Set(x => new UserUpdateModel { Status = "active" })
    .Where(x => x.Status == "pending")
    .UpdateAsync();
```

## Delete Operations

```csharp
await table.Delete(userId).DeleteAsync();
await table.Users.Delete(userId).DeleteAsync();
await table.Users.DeleteAsync(userId);  // Convenience

// With condition
await table.Users.Delete(userId).Where(x => x.Status == "inactive").DeleteAsync();
```

## Query Operations

```csharp
// Lambda (Preferred) - Query(keyCondition) is shorthand for Query().Where(keyCondition)
var users = await table.Users.Query(x => x.CustomerId == tenantId && x.OrderId.StartsWith("2024")).ToListAsync();

// Format String
var users = await table.Users.Query("pk = {0} AND begins_with(sk, {1})", tenantId, "2024").ToListAsync();

// With filter - Query(keyCondition, filterCondition)
var users = await table.Users.Query(x => x.CustomerId == tenantId, x => x.Status == "active").ToListAsync();

// Pagination
var query = table.Users.Query(x => x.CustomerId == tenantId).Take(25);
var users = await query.ToListAsync();
var hasMore = query.Response?.HasMorePages ?? false;
var nextPage = await table.Users.Query(x => x.CustomerId == tenantId).StartAt(query.Response?.LastEvaluatedKey!).ToListAsync();

// Options: UsingConsistentRead(), ScanIndexForward(false), WithProjection("name, email")
```

## Scan Operations

> Requires `[Scannable]` attribute on entity.

```csharp
var logs = await table.Logs.Scan().ToListAsync();
var logs = await table.Logs.Scan().WithFilter(x => x.Level == "ERROR").Take(100).ToListAsync();
```

## Response Metadata & Pagination

```csharp
// Response properties: LastEvaluatedKey, HasMorePages, ScannedCount, ResultCount, ConsumedCapacity
var query = table.Users.Query(x => x.TenantId == tenantId).Take(25);
var users = await query.ToListAsync();
var token = query.Response?.GetEncodedPaginationToken() ?? string.Empty;

// Continue with token
var nextPage = await table.Users.Query(x => x.TenantId == tenantId).Paginate(new PaginationRequest(25, token)).ToListAsync();
```

## Index Operations (GSI/LSI)

```csharp
// Query GSI/LSI - Query<T>(keyCondition) is shorthand for Query<T>().Where(keyCondition)
var products = await table.gsi1.Query<Product>(x => x.CategoryId == categoryId).ToListAsync();
var orders = await table.StatusIndex.Query<Order>(x => x.Status == "pending").ToListAsync();
var recentOrders = await table.lsi1.Query<Order>(x => x.CustomerId == customerId && x.CreatedAt > startDate).ToListAsync();

// Index with projection type - non-generic Query() defaults to projection
var projectedOrders = await table.StatusIndex.Query(x => x.Status == "active").ToListAsync();
```

## Multi-Entity Index Consolidation

In multi-entity tables, indexes from all entities are consolidated onto the generated table class:

```csharp
// Multi-entity table with indexes on different entities
[DynamoDbTable("shared-table", IsDefault = true)]
public partial class Order
{
    [PartitionKey]
    [DynamoDbAttribute("pk")]
    public string Pk { get; set; } = string.Empty;

    [GlobalSecondaryIndex("status-index", IsPartitionKey = true)]
    [DynamoDbAttribute("status")]
    public string Status { get; set; } = string.Empty;
}

[DynamoDbTable("shared-table")]
public partial class Customer
{
    [PartitionKey]
    [DynamoDbAttribute("pk")]
    public string Pk { get; set; } = string.Empty;

    [GlobalSecondaryIndex("email-index", IsPartitionKey = true)]
    [DynamoDbAttribute("email")]
    public string Email { get; set; } = string.Empty;
}

// Both indexes are available on the generated table class
var ordersByStatus = await table.StatusIndex.Query<Order>(x => x.Status == "pending").ToListAsync();
var customersByEmail = await table.EmailIndex.Query<Customer>(x => x.Email == "user@example.com").ToListAsync();
```

### Index Consolidation Rules

| Scenario | Behavior |
|----------|----------|
| Same index, same config | Single index property generated |
| Same index, different partition key | FDDB053 diagnostic error |
| Same index, different sort key | FDDB054 diagnostic error |
| Same index, different type (GSI vs LSI) | FDDB055 diagnostic error |
| Index on non-default entity | Index property generated normally |

## Projection Queries

Projections work seamlessly with QueryRequestBuilder through the `IReadOnlyEntity` interface - just use `ToListAsync()` like any other entity.

```csharp
// Define an index with a default projection type
public DynamoDbIndex<OrderSummary> StatusIndex => 
    new DynamoDbIndex<OrderSummary>(this, "status-index", OrderSummary.ProjectionExpression);

// Query the index - non-generic Query() uses OrderSummary automatically
var results = await table.StatusIndex.Query().Where(x => x.Status == "pending").ToListAsync();
var results = await table.StatusIndex.Query("status = {0}", "pending").ToListAsync();

// Query entity and project to different type
var summaries = await table.Orders.Query(x => x.CustomerId == customerId).ToListAsync<Order, OrderSummary>();

// Discriminated projections (filter by entity type in multi-entity tables)
var orderSummaries = await table.gsi1.Query<Order>(x => x.Status == "active").ToDiscriminatedListAsync<Order, OrderSummary>();
```

### Projection Interface Hierarchy

```
IEntityMetadataProvider
        │
        ▼
  IReadOnlyEntity ◄── Projections implement this
        │
        ▼
  IDynamoDbEntity ◄── Full entities implement this
```

- `IReadOnlyEntity`: Read operations (FromDynamoDb, GetPartitionKey, GetEntityMetadata)
- `IDynamoDbEntity`: Adds write operations (ToDynamoDb, MatchesEntity, RequiresWriteTransaction)
- Projections inherit metadata from source entity (table name, keys)

## Batch Operations

```csharp
// Batch Get
var response = await DynamoDbBatch.Get.Add(table.Users.Get(userId1)).Add(table.Users.Get(userId2)).ExecuteAsync();
var (user, order) = await DynamoDbBatch.Get.Add(table.Users.Get(userId)).Add(table.Orders.Get(customerId, orderId)).ExecuteAndMapAsync<User, Order>();

// Batch Write
await DynamoDbBatch.Write.Add(table.Users.Put(user1)).Add(table.Users.Delete(oldUserId)).ExecuteAsync();

// Batch PartiQL
var response = await DynamoDbBatch.PartiQL.Add(table.ExecutePartiQL<User>("SELECT * FROM Users WHERE pk = ?", userId)).ExecuteAsync();
```

## Transactions

```csharp
// Transaction Write
await DynamoDbTransactions.Write
    .Add(table.Users.Put(newUser))
    .Add(table.Accounts.Update(accountId).Set(x => new AccountUpdateModel { Balance = x.Balance - 100 }))
    .Add(table.Orders.Put(order).Where(x => x.OrderId.AttributeNotExists()))
    .ExecuteAsync();

// Transaction Get
var response = await DynamoDbTransactions.Get.Add(table.Users.Get(userId)).Add(table.Accounts.Get(accountId)).ExecuteAsync();

// Idempotency
await DynamoDbTransactions.Write.Add(table.Orders.Put(order)).WithClientRequestToken(token).ExecuteAsync();
```

## PartiQL

```csharp
var users = await table.ExecutePartiQL<User>("SELECT * FROM Users WHERE pk = ?", userId).ToListAsync();
await table.ExecutePartiQL("INSERT INTO Users VALUE {'pk': ?, 'name': ?}", userId, name).ExecuteAsync();
await table.ExecutePartiQL("UPDATE Users SET name = ? WHERE pk = ?", newName, userId).ExecuteAsync();
await table.ExecutePartiQL("DELETE FROM Users WHERE pk = ?", userId).ExecuteAsync();
```

## Raw SDK Access

```csharp
var user = await table.Get<User>(new GetItemRequest { TableName = "Users", Key = ... }).GetItemAsync();
var orders = await table.Query<Order>(new QueryRequest { TableName = "Orders", KeyConditionExpression = "pk = :pk", ... }).ToListAsync();
await DynamoDbTransactions.WriteAsync(client, transactWriteRequest);
```

## Terminal Methods Reference

| Operation | Builder Terminal | Convenience |
|-----------|-----------------|-------------|
| Get | `.GetItemAsync()` | `GetAsync()` |
| Put | `.PutAsync()` | `PutAsync()` |
| Update | `.UpdateAsync()` | - |
| Delete | `.DeleteAsync()` | `DeleteAsync()` |
| Query/Scan | `.ToListAsync()` | - |
| Batch/Transaction | `.ExecuteAsync()` | `.ExecuteAndMapAsync<T1,T2>()` |
| PartiQL | `.ToListAsync()`, `.ExecuteAsync()` | - |
| Composite Entity | `.ToCompositeEntityAsync()` | `.ToCompositeEntityListAsync()` |

## Expression Styles

1. **Lambda (Preferred)**: `.Where(x => x.Status == "active")` `.Set(x => new Model { Name = "value" })`
2. **Format String**: `.Where("status = {0}", "active")` `.Set("SET #name = {0}", "value")`
3. **Manual**: `.Where("status = :s").WithValue(":s", "active")`

## Lambda Expression Functions

| C# Method | DynamoDB Function | Example |
|-----------|------------------|---------|
| `StartsWith()` | `begins_with()` | `x => x.Name.StartsWith("John")` |
| `Contains()` | `contains()` | `x => x.Email.Contains("@example")` |
| `.Between(low, high)` | `BETWEEN` | `x => x.Age.Between(18, 65)` |
| `.AttributeExists()` | `attribute_exists()` | `x => x.Field.AttributeExists()` |
| `.AttributeNotExists()` | `attribute_not_exists()` | `x => x.Id.AttributeNotExists()` |
| `.Size()` | `size()` | `x => x.Items.Size() > 5` |

## Nested Map (Object) Expressions

Lambda expressions support nested property access for entities with `[DynamoDbMap]` attributes.

```csharp
// Entity with nested object
[DynamoDbEntity]
public partial class Address
{
    [DynamoDbAttribute("city")] public string City { get; set; } = string.Empty;
}

[DynamoDbTable("Customers")]
public partial class Customer
{
    [PartitionKey] [DynamoDbAttribute("pk")] public string CustomerId { get; set; } = string.Empty;
    [DynamoDbMap] [DynamoDbAttribute("address")] public Address ShippingAddress { get; set; } = new();
}

// Filter on nested property (works in .WithFilter() and .Where() on writes, NOT key conditions)
var customers = await table.Customers.Query(x => x.CustomerId == tenantId)
    .WithFilter(x => x.ShippingAddress.City == "Seattle").ToListAsync();
await table.Customers.Put(customer).Where(x => x.ShippingAddress.City == "Seattle").PutAsync();

// Update nested property - generates: SET #address.#city = :v0
await table.Customers.Update(customerId)
    .Set(x => new CustomerUpdateModel { ShippingAddress = new AddressUpdateModel { City = "Portland" } })
    .UpdateAsync();
```

## List Expressions

```csharp
// Filter on list element by index (works in .WithFilter() and .Where(), NOT key conditions)
var items = await table.Items.Query(x => x.Category == "electronics")
    .WithFilter(x => x.Tags[0] == "featured").ToListAsync();
// Also: x.Metadata.Keywords[0] (nested list), x.LineItems[0].ProductId (object in list)

using Oproto.FluentDynamoDb.Expressions;

// Append/Prepend - generates: SET #tags = list_append(#tags, :v0) or list_append(:v0, #tags)
await table.Items.Update(itemId).Set(x => x.Tags.Append("new-tag")).UpdateAsync();
await table.Items.Update(itemId).Set(x => x.Tags.Prepend("priority")).UpdateAsync();
await table.Items.Update(itemId).Set(x => x.Tags.AppendRange(new[] { "tag1", "tag2" })).UpdateAsync();

// Update/Remove by index - generates: SET #tags[0] = :v0 or REMOVE #tags[2]
await table.Items.Update(itemId).Set(x => x.Tags[0], "updated").UpdateAsync();
await table.Items.Update(itemId).Remove(x => x.Tags[2]).UpdateAsync();
```

## Set Operations

```csharp
// Add to set - generates: ADD #categories :v0
await table.Items.Update(itemId).Add(x => x.Categories, "electronics").UpdateAsync();
await table.Items.Update(itemId).Add(x => x.Categories, new[] { "a", "b" }).UpdateAsync();
// Delete from set - generates: DELETE #categories :v0
await table.Items.Update(itemId).Delete(x => x.Categories, "clearance").UpdateAsync();
await table.Items.Update(itemId).Add(x => x.Scores, 100).UpdateAsync();  // Numeric sets
```

## Conditional Filter Patterns

Use `||` and `&&` operators with local boolean conditions to conditionally include or skip filter clauses at translation time.

| Pattern | Local Value | Behavior |
|---------|-------------|----------|
| `localCondition \|\| x.Prop == val` | `true` | Skip filter (return all) |
| `localCondition \|\| x.Prop == val` | `false` | Apply filter |
| `localCondition && x.Prop == val` | `true` | Apply filter |
| `localCondition && x.Prop == val` | `false` | Skip filter (return all) |

```csharp
// Optional filter based on parameter presence
var orders = await table.Orders.Query(x => x.CustomerId == customerId)
    .WithFilter(x => string.IsNullOrWhiteSpace(status) || x.Status == status)
    .ToListAsync();

// Feature flag controlled filter
var items = await table.Items.Query(x => x.Key == key)
    .WithFilter(x => enableDateFilter && x.Date > minDate)
    .ToListAsync();

// Multiple optional filters
var results = await table.Orders.Query(x => x.CustomerId == customerId)
    .WithFilter(x => (skipStatusFilter || x.Status == status) && (skipDateFilter || x.Date > minDate))
    .ToListAsync();

// Negated conditions
var items = await table.Items.Query(x => x.Key == key)
    .WithFilter(x => !excludeArchived || x.Archived == false)
    .ToListAsync();
```

**Rules:**
- Local condition must not reference the entity parameter (evaluated at translation time)
- OR between two entity conditions throws `UnsupportedExpressionException`
- Works with method calls (`string.IsNullOrWhiteSpace()`), negations (`!flag`), and compound expressions

## Common Patterns

```csharp
// Optimistic locking
await table.Users.Update(userId).Set(x => new UserUpdateModel { Version = x.Version + 1 }).Where(x => x.Version == currentVersion).UpdateAsync();

// Conditional put (create only)
await table.Users.Put(user).Where(x => x.UserId.AttributeNotExists()).PutAsync();

// Increment counter
await table.Users.Update(userId).Set(x => new UserUpdateModel { Count = x.Count + 1 }).UpdateAsync();
```

## Projection Error Handling

Common projection-related errors and diagnostics:

| Diagnostic | Code | Description |
|------------|------|-------------|
| Source Entity Not Found | FDDB060 | Projection references non-existent source entity |
| Metadata Inheritance Failure | FDDB061 | Cannot inherit metadata from source entity |
| Projection Interface Violation | FDDB062 | Projection used in write operation context |

```csharp
// Projections are read-only - write operations will fail at compile time
// ❌ This won't compile - projections don't implement IDynamoDbEntity
await table.Put(orderSummary).PutAsync();  // Compile error

// ✅ Use the source entity for write operations
await table.Put(order).PutAsync();

// Projection mapping errors throw DynamoDbMappingException
try
{
    var summaries = await table.gsi1.Query<OrderSummary>(x => x.Status == "pending").ToListAsync();
}
catch (DynamoDbMappingException ex)
{
    Console.WriteLine($"Mapping failed: {ex.Message}");
}
```

## FluentResults API (Result Pattern)

The `Oproto.FluentDynamoDb.FluentResults` package provides Result-returning alternatives to all async operations.

```csharp
using Oproto.FluentDynamoDb.FluentResults;
```

### Traditional vs Result Pattern

```csharp
// Traditional (throws exceptions)
var user = await table.Users.Get(userId).GetItemAsync();

// FluentResults (returns Result<T>)
var result = await table.Users.Get(userId).GetItemAsyncResult();
if (result.IsSuccess)
    var user = result.Value;
```

### Result-Returning Methods

| Traditional | FluentResults |
|-------------|---------------|
| `.GetItemAsync()` | `.GetItemAsyncResult()` |
| `.PutAsync()` | `.PutAsyncResult()` |
| `.UpdateAsync()` | `.UpdateAsyncResult()` |
| `.DeleteAsync()` | `.DeleteAsyncResult()` |
| `.ToListAsync()` | `.ToListAsyncResult()` |
| `.ExecuteAsync()` | `.ExecuteAsyncResult()` |
| `.ToCompositeEntityAsync()` | `.ToCompositeEntityAsyncResult()` |
| `.ExecuteAndMapAsync<T1,T2>()` | `.ExecuteAndMapAsyncResult<T1,T2>()` |

### Error Handling

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
                // Retry with fresh data
                break;
            case TransactionCancelledError tce:
                Console.WriteLine($"Cancelled: {string.Join(", ", tce.CancellationReasons)}");
                break;
            default:
                Console.WriteLine($"[{error.ErrorCode}]: {error.Message}");
                break;
        }
    }
}
```

### Common Error Types

| Error Type | ErrorCode | Description |
|------------|-----------|-------------|
| `OptimisticLockingError` | `OPTIMISTIC_LOCKING_FAILED` | Conditional check failed |
| `TransactionCancelledError` | `TRANSACTION_CANCELLED` | Transaction was cancelled |
| `TransactionConflictError` | `TRANSACTION_CONFLICT` | Concurrent transaction |
| `OperationLimitExceededError` | `OPERATION_LIMIT_EXCEEDED` | Too many operations |
| `MissingClientError` | `MISSING_CLIENT` | No DynamoDB client |
| `MappingError` | `MAPPING_ERROR` | Entity mapping failed |
| `EncryptionError` | `ENCRYPTION_FAILED` | Encryption failed |

### UseFluentResults Attribute

```csharp
[DynamoDbTable("Users")]
[UseFluentResults]  // Generates Result-returning convenience methods
public partial class User { ... }

// Generated: GetAsyncResult, PutAsyncResult, DeleteAsyncResult
```

## Table Creation (Integration Testing)

```csharp
using Oproto.FluentDynamoDb.Provisioning;

// Create table from entity metadata
var result = await UsersTable.CreateTableAsync(client, "test-users-table");
var result = await UsersTable.CreateTableAsync(client, "test-users-table", new TableCreationOptions { EnableTtl = true });
```
