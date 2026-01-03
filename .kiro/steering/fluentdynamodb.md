# FluentDynamoDb API Reference
# Updated 2026-01-03
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

// DateOnly and TimeOnly types (.NET 6+)
[DynamoDbTable("Events")]
public partial class Event
{
    [PartitionKey]
    [DynamoDbAttribute("pk")]
    public string Pk { get; set; } = string.Empty;
    
    // Default ISO 8601 format: "2024-12-28"
    [DynamoDbAttribute("eventDate")]
    public DateOnly EventDate { get; set; }
    
    // Custom format: "12/28/2024"
    [DynamoDbAttribute("displayDate", Format = "MM/dd/yyyy")]
    public DateOnly DisplayDate { get; set; }
    
    // Default ISO 8601 format: "14:30:45.0000000"
    [DynamoDbAttribute("startTime")]
    public TimeOnly StartTime { get; set; }
    
    // Custom format: "2:30 PM"
    [DynamoDbAttribute("displayTime", Format = "h:mm tt")]
    public TimeOnly DisplayTime { get; set; }
    
    // Collections supported
    [DynamoDbAttribute("availableDates")]
    public List<DateOnly> AvailableDates { get; set; } = new();
}
```

## Projection Definition

Projections are read-only entity types representing a subset of attributes. They implement `IReadOnlyEntity` and `IProjectionModel`.

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

Composite entities span multiple DynamoDB items sharing the same partition key but different sort keys. Use `[RelatedEntity]` to define parent-child relationships.

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
    .ToCompositeEntityAsync<Invoice>();  // invoice.Lines auto-populated

// For multiple composite entities
var invoices = await table.Invoices.Query().Where(x => x.Pk == pk).ToCompositeEntityListAsync<Invoice>();
```

### Key Design Pattern

Hierarchical sort keys enable single-query retrieval. Query with `begins_with(sk, "INVOICE#INV-001")` returns all items, `ToCompositeEntityAsync` assembles them.

## Key Handling & Prefixes

### Understanding Key Prefixes

When you define a key with a prefix, the source generator creates helper methods but does NOT automatically apply prefixes in CRUD operations:

```csharp
[DynamoDbTable("Orders")]
public partial class Order
{
    [PartitionKey(Prefix = "ORDER")]  // Generates keys like "ORDER#12345"
    [DynamoDbAttribute("pk")]
    public string Pk { get; set; } = string.Empty;
}
```

### Generated Keys Class

The source generator creates a nested `Keys` class with builder methods:

```csharp
// Generated methods:
Order.Keys.Pk("12345")           // Returns "ORDER#12345"
Order.Keys.Sk("abc")             // Returns "META#abc" (if SK has prefix)
Order.Keys.Key("12345", "abc")   // Returns ("ORDER#12345", "META#abc")

// Extraction helpers (for composite keys):
Order.Keys.ExtractPkComponents("ORDER#12345")  // Returns "12345"
```

### CRITICAL: Get/Update/Delete Take RAW Values

The generated convenience methods pass values directly to DynamoDB - they do NOT prepend prefixes:

```csharp
// ❌ WRONG - This looks for pk="12345", not "ORDER#12345"
var order = await table.Orders.Get("12345").GetItemAsync();

// ✅ CORRECT - Use Keys.Pk() to build the prefixed key
var order = await table.Orders.Get(Order.Keys.Pk("12345")).GetItemAsync();

// ✅ CORRECT - Or use the full prefixed value directly
var order = await table.Orders.Get("ORDER#12345").GetItemAsync();
```

### When Reading Back: Full Prefixed Value

When you read an entity from DynamoDB, the key properties contain the FULL prefixed value:

```csharp
var order = await table.Orders.Get(Order.Keys.Pk("12345")).GetItemAsync();
Console.WriteLine(order.Pk);  // Prints "ORDER#12345", NOT "12345"

// To extract the raw value, manually strip the prefix:
var rawId = order.Pk.Replace("ORDER#", "");  // Returns "12345"

// Or use Split for more complex keys:
var parts = order.Pk.Split('#');  // ["ORDER", "12345"]
var rawId = parts[1];
```

### Computed/Concatenated Keys (Advanced)

For keys that combine multiple values into a single DynamoDB attribute, use `[Computed]` and `[Extracted]` attributes:

```csharp
[DynamoDbTable("Events")]
public partial class Event
{
    // Computed key: combines Year + Month + Day into single attribute
    [PartitionKey]
    [DynamoDbAttribute("pk")]
    [Computed("Year", "Month", "Day", Separator = "#")]
    public string Pk { get; set; } = string.Empty;
    
    // Source properties - values extracted from Pk when reading from DynamoDB
    [Extracted("Pk", 0)]
    public int Year { get; set; }
    
    [Extracted("Pk", 1)]
    public int Month { get; set; }
    
    [Extracted("Pk", 2)]
    public int Day { get; set; }
}

// Generated methods:
Event.Keys.BuildPk(2024, 12, 25)              // Returns "2024#12#25"
Event.Keys.ExtractPkComponents("2024#12#25")  // Returns (Year: 2024, Month: 12, Day: 25)

// When reading, extracted properties are auto-populated:
var evt = await table.Events.Get(Event.Keys.BuildPk(2024, 12, 25)).GetItemAsync();
Console.WriteLine(evt.Year);   // 2024
Console.WriteLine(evt.Month);  // 12
Console.WriteLine(evt.Day);    // 25
```

### Key Handling Summary

| Operation | What You Pass | What's Stored in DynamoDB |
|-----------|---------------|---------------------------|
| `Get(value)` | Raw value OR prefixed value | N/A (read operation) |
| `Put(entity)` | Entity with `Pk` set | Whatever is in `entity.Pk` |
| `entity.Pk` after read | N/A | Full prefixed value |
| `Keys.Pk(value)` | Raw value | Returns prefixed value |
| `Keys.ExtractPkComponents(pk)` | Full prefixed value | Returns raw value(s) |

### Best Practice: Always Use Keys Class

```csharp
// Building keys for operations - ALWAYS use Keys.Pk() for prefixed keys
var pk = Order.Keys.Pk(orderId);                    // "ORDER#12345"
var (pk, sk) = Order.Keys.Key(orderId, lineId);     // ("ORDER#12345", "LINE#abc")

// CRUD operations with prefixed keys
var order = await table.Orders.Get(Order.Keys.Pk(orderId)).GetItemAsync();
await table.Orders.Delete(Order.Keys.Pk(orderId), Order.Keys.Sk(lineId)).DeleteAsync();

// Creating new entities - set the prefixed key value
var newOrder = new Order 
{ 
    Pk = Order.Keys.Pk(newOrderId),  // "ORDER#newId"
    Sk = Order.Keys.Sk(lineId),      // "LINE#lineId"
    // ... other properties
};
await table.Orders.Put(newOrder).PutAsync();

// Extracting raw values from prefixed keys (manual)
var rawOrderId = order.Pk.Split('#')[1];  // "12345" from "ORDER#12345"
```

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

// With key condition - create only (fail if exists)
await table.Users.Put(user).IfNotExists().PutAsync();
await table.Users.PutAsync(user, KeyCondition.MustNotExist);  // Convenience parameter

// With key condition - update only (fail if not exists)
await table.Users.Put(user).IfExists().PutAsync();
await table.Users.PutAsync(user, KeyCondition.MustExist);  // Convenience parameter
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

// With key condition - prevent upsert (fail if not exists)
await table.Users.Update(userId, KeyCondition.MustExist)
    .Set(x => new UserUpdateModel { Name = "New" })
    .UpdateAsync();

// With key condition - builder method
await table.Users.Update(userId)
    .IfExists()
    .Set(x => new UserUpdateModel { Name = "New" })
    .UpdateAsync();

// Conditional update - skip property with NoUpdate()
await table.Users.Update(userId)
    .Set(x => new UserUpdateModel 
    { 
        Name = shouldUpdate ? newName : x.Name.NoUpdate(),  // Skip if !shouldUpdate
        Status = "active"  // Always update
    })
    .UpdateAsync();

// Null assignment - sets DynamoDB NULL (not skip)
await table.Users.Update(userId)
    .Set(x => new UserUpdateModel { MiddleName = null })  // Sets attribute to NULL
    .UpdateAsync();
```

### Null vs NoUpdate() vs Remove()

| Method | DynamoDB Result | Use Case |
|--------|-----------------|----------|
| `= null` | SET attr = NULL | Set attribute to DynamoDB NULL type |
| `.NoUpdate()` | No operation | Skip updating this property conditionally |
| `.Remove()` | REMOVE attr | Delete the attribute entirely |

```csharp
// null → SET NULL (attribute exists with NULL value)
.Set(x => new UserUpdateModel { OptionalField = null })

// NoUpdate() → Skip (attribute unchanged)
.Set(x => new UserUpdateModel { Field = condition ? value : x.Field.NoUpdate() })

// Remove() → REMOVE (attribute deleted)
.Set(x => new UserUpdateModel { TempData = x.TempData.Remove() })
```

### Counter Patterns

```csharp
// Atomic increment - ADD creates attribute if missing (initializes to 0)
await table.Users.Update(userId)
    .Set(x => new UserUpdateModel { Count = x.Count.Add(1) })
    .UpdateAsync();
// Generates: ADD #count :p0

// Arithmetic on existing value - fails if attribute doesn't exist
await table.Users.Update(userId)
    .Set(x => new UserUpdateModel { Count = x.Count + 1 })
    .UpdateAsync();
// Generates: SET #count = #count + :p0

// IfNotExists with arithmetic - initialize to non-zero default then increment
await table.Users.Update(userId)
    .Set(x => new UserUpdateModel { Count = x.Count.IfNotExists(100) + 1 })
    .UpdateAsync();
// Generates: SET #count = if_not_exists(#count, :p0) + :p1
// If Count doesn't exist: sets to 101 (100 + 1)
// If Count exists: increments existing value by 1
```

| Pattern | Use Case | Behavior if Missing |
|---------|----------|---------------------|
| `x.Count.Add(1)` | Simple counter | Creates with value 1 |
| `x.Count + 1` | Increment existing | Fails |
| `x.Count.IfNotExists(0) + 1` | Counter with explicit zero default | Creates with value 1 |
| `x.Count.IfNotExists(100) + 1` | Counter with non-zero default | Creates with value 101 |

## Delete Operations

```csharp
await table.Delete(userId).DeleteAsync();
await table.Users.Delete(userId).DeleteAsync();
await table.Users.DeleteAsync(userId);  // Convenience

// With condition
await table.Users.Delete(userId).Where(x => x.Status == "inactive").DeleteAsync();

// With key condition - fail if not exists
await table.Users.Delete(userId).IfExists().DeleteAsync();
await table.Users.DeleteAsync(userId, KeyCondition.MustExist);  // Convenience parameter

// Composite key with key condition
await table.Orders.Delete(customerId, orderId).IfExists().DeleteAsync();
await table.Orders.DeleteAsync(customerId, orderId, KeyCondition.MustExist);
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

## Automatic Index Projections

Single-entity tables automatically use the entity type as the default projection:

```csharp
// Single-entity: DynamoDbIndex<Order> StatusIndex generated automatically
[DynamoDbTable("orders")]
public partial class Order
{
    [PartitionKey] [DynamoDbAttribute("pk")] public string Pk { get; set; } = string.Empty;
    [GlobalSecondaryIndex("status-index", IsPartitionKey = true)]
    [DynamoDbAttribute("status")] public string Status { get; set; } = string.Empty;
}
var orders = await table.StatusIndex.Query(x => x.Status == "pending").ToListAsync();

// Keys Only: auto-generates {IndexName}KeysProjection record
[GlobalSecondaryIndex("gsi1", IsPartitionKey = true, ProjectionType = ProjectionType.KeysOnly)]
```

| ProjectionType | Behavior |
|----------------|----------|
| `All` (default) | Single-entity: entity type; Multi-entity: `DynamoDbIndex` |
| `KeysOnly` | Auto-generates `{IndexName}KeysProjection` record |

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
// Dynamic index: int idx = 0; .WithFilter(x => x.Tags[idx] == "featured")

using Oproto.FluentDynamoDb.Expressions;

// Append/Prepend - SET #tags = list_append(#tags, :v0) or list_append(:v0, #tags)
await table.Items.Update(itemId).Set(x => x.Tags.Append("new-tag")).UpdateAsync();
await table.Items.Update(itemId).Set(x => x.Tags.Prepend("priority")).UpdateAsync();
await table.Items.Update(itemId).Set(x => x.Tags.AppendRange(new[] { "tag1", "tag2" })).UpdateAsync();

// SetAt/RemoveAt - SET #tags[0] = :v0 or REMOVE #tags[2]
await table.Items.Update(itemId).Set(x => x.Tags.SetAt(0, "updated")).UpdateAsync();
await table.Items.Update(itemId).Set(x => x.Tags.RemoveAt(2)).UpdateAsync();
await table.Items.Update(itemId).Set(x => x.Metadata.Keywords.SetAt(0, "val")).UpdateAsync();  // Nested

// Dynamic index - variable, method call, or property (NOT entity parameter)
int index = GetIndex();
await table.Items.Update(itemId).Set(x => x.Tags.SetAt(index, "updated")).UpdateAsync();

// Chained SetAt - SET #tags[0] = :v0, #tags[1] = :v1
await table.Items.Update(itemId).Set(x => x.Tags.SetAt(0, "a").SetAt(1, "b")).UpdateAsync();
```

| Method | DynamoDB | Notes |
|--------|----------|-------|
| `.Append(item)` | `list_append(#attr, :v)` | Add to end |
| `.Prepend(item)` | `list_append(:v, #attr)` | Add to beginning |
| `.SetAt(idx, val)` | `SET #attr[idx] = :v` | Update at index |
| `.RemoveAt(idx)` | `REMOVE #attr[idx]` | Remove at index |

**DynamoDB Limitation:** Cannot chain SetAt with Append/Prepend/RemoveAt on same list (overlapping paths). Multiple SetAt with different indices is allowed.

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
    .WithFilter(x => string.IsNullOrWhiteSpace(status) || x.Status == status).ToListAsync();

// Feature flag controlled filter
var items = await table.Items.Query(x => x.Key == key)
    .WithFilter(x => enableDateFilter && x.Date > minDate).ToListAsync();
```

**Rules:** Local condition must not reference entity parameter. OR between two entity conditions throws `UnsupportedExpressionException`.

**Empty Expression Handling:**
When all conditional clauses evaluate to skip (e.g., all local conditions are `true` in OR patterns), the filter is gracefully omitted and the operation executes without filtering. This eliminates the need to wrap `.WithFilter()` in conditional checks.

```csharp
// Safe to use even when all conditions might skip
var orders = await table.Orders.Query(x => x.CustomerId == customerId)
    .WithFilter(x => 
        (string.IsNullOrWhiteSpace(status) || x.Status == status) &&
        (string.IsNullOrWhiteSpace(category) || x.Category == category))
    .ToListAsync();
// If both status and category are null/empty, query executes without filter

// Complex nested OR patterns with mutually exclusive conditions
var items = await table.Items.Query(x => x.Key == key)
    .WithFilter(x => 
        skipAllFilters ||
        (hasValue && x.OptionalField.AttributeExists()) ||
        (!hasValue && x.OptionalField.AttributeNotExists()))
    .ToListAsync();
// When skipAllFilters is true, entire filter is skipped correctly
```

## Common Patterns

```csharp
// Optimistic locking
await table.Users.Update(userId).Set(x => new UserUpdateModel { Version = x.Version + 1 }).Where(x => x.Version == currentVersion).UpdateAsync();

// Conditional put (create only)
await table.Users.Put(user).Where(x => x.UserId.AttributeNotExists()).PutAsync();

// Increment counter
await table.Users.Update(userId).Set(x => new UserUpdateModel { Count = x.Count + 1 }).UpdateAsync();
```

## Key Condition Shortcuts

Simplify common conditional patterns with `KeyCondition` enum and builder methods:

| Method | Enum | Generated Condition |
|--------|------|---------------------|
| `.IfExists()` | `KeyCondition.MustExist` | `attribute_exists(pk) [AND attribute_exists(sk)]` |
| `.IfNotExists()` | `KeyCondition.MustNotExist` | `attribute_not_exists(pk) [AND attribute_not_exists(sk)]` |

```csharp
// Create only (fail if exists)
await table.Users.Put(user).IfNotExists().PutAsync();
await table.Users.PutAsync(user, KeyCondition.MustNotExist);

// Update existing only (prevent upsert)
await table.Users.Update(pk, sk, KeyCondition.MustExist).Set(...).UpdateAsync();
await table.Users.Update(pk, sk).IfExists().Set(...).UpdateAsync();

// Delete only if exists
await table.Users.Delete(pk).IfExists().DeleteAsync();
await table.Users.DeleteAsync(pk, KeyCondition.MustExist);

// Combine with additional conditions
await table.Users.Update(pk, sk)
    .IfExists()
    .Set(x => new UserUpdateModel { Status = "active" })
    .Where(x => x.Status == "pending")
    .UpdateAsync();
```

**KeyCondition Enum Values:**
- `KeyCondition.None` - No automatic condition (default)
- `KeyCondition.MustExist` - Item must exist (all key attributes must exist)
- `KeyCondition.MustNotExist` - Item must not exist (key attributes must not exist)

## Projection Error Handling

| Diagnostic | Code | Description |
|------------|------|-------------|
| Source Entity Not Found | FDDB060 | Projection references non-existent source entity |
| Metadata Inheritance Failure | FDDB061 | Cannot inherit metadata from source entity |
| Projection Interface Violation | FDDB062 | Projection used in write operation context |

```csharp
// Projections are read-only - write operations fail at compile time
await table.Put(orderSummary).PutAsync();  // ❌ Compile error - use source entity
await table.Put(order).PutAsync();          // ✅ Correct
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

## Dynamic Fields

Capture and work with DynamoDB attributes not explicitly defined on your entity class. Enable with `[EnableDynamicFields]`.

```csharp
[DynamoDbTable("Nodes")]
[EnableDynamicFields]
public partial class Node
{
    [PartitionKey] [DynamoDbAttribute("pk")] public string Pk { get; set; } = string.Empty;
    [SortKey] [DynamoDbAttribute("sk")] public string Sk { get; set; } = string.Empty;
    [DynamoDbAttribute("v")] public int Version { get; set; }
    // Dynamic fields captured automatically in DynamicFields property
}
```

### Basic Operations

```csharp
var node = await table.Nodes.Get(pk, sk).GetItemAsync();

// Read/write individual fields
var value = node.DynamicFields.GetString("customField");
node.DynamicFields.SetString("customField", "newValue");
node.DynamicFields.SetInt("counter", 42);
node.DynamicFields.Remove("obsoleteField");

// Change tracking for incremental updates
await table.Nodes.Update(pk, sk)
    .Set(x => new NodeUpdateModel { DynamicFields = node.DynamicFields.ChangesOnly() })
    .UpdateAsync();
```

### Prefix-Based Operations

For sparse attribute patterns using naming conventions (e.g., `c_{id}` for children, `t_{id}` for transactions):

```csharp
// Discover field names by prefix
var childFieldNames = node.DynamicFields.GetFieldNamesByPrefix("c_");  // ["c_ABC", "c_DEF", ...]

// Get all fields matching prefix as raw AttributeValues
var childFields = node.DynamicFields.GetByPrefix("c_");  // Keys: "c_ABC", "c_DEF"
var childFieldsStripped = node.DynamicFields.GetByPrefixWithStrippedKeys("c_");  // Keys: "ABC", "DEF"

// Remove all fields matching prefix
int removedCount = node.DynamicFields.RemoveByPrefix("c_");  // Returns count removed
```

### Typed Map Operations

Store and retrieve nested `[DynamoDbEntity]` types as Map attributes:

```csharp
// Define nested entity type
[DynamoDbEntity]
public partial class ChildRef
{
    [DynamoDbAttribute("amt")] public decimal Amount { get; set; }
    [DynamoDbAttribute("status")] public string Status { get; set; } = string.Empty;
}

// Get typed entity from Map field
var child = node.DynamicFields.GetMap<ChildRef>("c_ABC123");  // Returns null if missing
if (node.DynamicFields.TryGetMap<ChildRef>("c_ABC123", out var childRef))
    Console.WriteLine(childRef.Amount);

// Set typed entity as Map field
node.DynamicFields.SetMap("c_ABC123", new ChildRef { Amount = 100m, Status = "active" });
node.DynamicFields.SetMap<ChildRef>("c_ABC123", null);  // Removes field

// Get all Map fields matching prefix as typed entities
var children = node.DynamicFields.GetMapsByPrefix<ChildRef>("c_");  // Keys: "c_ABC", "c_DEF"
var childrenStripped = node.DynamicFields.GetMapsByPrefixWithStrippedKeys<ChildRef>("c_");  // Keys: "ABC", "DEF"
```

### Bulk Operations

Efficiently add/remove multiple fields:

```csharp
// Set multiple raw AttributeValues
node.DynamicFields.SetMany(new Dictionary<string, AttributeValue>
{
    ["field1"] = new AttributeValue { S = "value1" },
    ["field2"] = new AttributeValue { N = "42" }
});

// Set multiple fields with prefix prepended to keys
node.DynamicFields.SetManyWithPrefix("t_", new Dictionary<string, AttributeValue>
{
    ["TXN001"] = new AttributeValue { S = "pending" },  // Stored as "t_TXN001"
    ["TXN002"] = new AttributeValue { S = "complete" }  // Stored as "t_TXN002"
});

// Set multiple typed entities with prefix
node.DynamicFields.SetMapsWithPrefix("c_", new Dictionary<string, ChildRef>
{
    ["ABC"] = new ChildRef { Amount = 100m },  // Stored as "c_ABC"
    ["DEF"] = new ChildRef { Amount = 200m }   // Stored as "c_DEF"
});

// Remove multiple fields by name
int removed = node.DynamicFields.RemoveMany(new[] { "c_ABC", "c_DEF", "t_TXN001" });
```

### Sparse Attribute Pattern Example

Complete example for tree nodes with dynamic children:

```csharp
[DynamoDbTable("BalanceTree")]
[EnableDynamicFields]
public partial class TreeNode
{
    [PartitionKey] [DynamoDbAttribute("pk")] public string Pk { get; set; } = string.Empty;
    [SortKey] [DynamoDbAttribute("sk")] public string Sk { get; set; } = string.Empty;
    [DynamoDbAttribute("v")] public int Version { get; set; }
}

[DynamoDbEntity]
public partial class ChildReference
{
    [DynamoDbAttribute("subtotal")] public decimal Subtotal { get; set; }
}

// Load and modify
var node = await table.TreeNodes.Get(pk, sk).GetItemAsync();

// Read all children
var children = node.DynamicFields.GetMapsByPrefixWithStrippedKeys<ChildReference>("c_");
foreach (var (childId, child) in children)
    Console.WriteLine($"Child {childId}: {child.Subtotal}");

// Add new children
node.DynamicFields.SetMapsWithPrefix("c_", new Dictionary<string, ChildReference>
{
    ["newChild1"] = new ChildReference { Subtotal = 500m },
    ["newChild2"] = new ChildReference { Subtotal = 300m }
});

// Remove old children
node.DynamicFields.RemoveByPrefix("old_");

// Save with optimistic locking
await table.TreeNodes.Update(pk, sk)
    .Set(x => new TreeNodeUpdateModel
    {
        Version = x.Version + 1,
        DynamicFields = node.DynamicFields.ChangesOnly()
    })
    .Where(x => x.Version == node.Version)
    .UpdateAsync();
```

### Method Reference

| Method | Returns | Description |
|--------|---------|-------------|
| `GetFieldNamesByPrefix(prefix)` | `IEnumerable<string>` | Field names matching prefix |
| `GetByPrefix(prefix)` | `Dictionary<string, AttributeValue>` | Fields with full keys |
| `GetByPrefixWithStrippedKeys(prefix)` | `Dictionary<string, AttributeValue>` | Fields with prefix stripped |
| `RemoveByPrefix(prefix)` | `int` | Remove all matching, return count |
| `GetMap<T>(fieldName)` | `T?` | Get Map as typed entity |
| `TryGetMap<T>(fieldName, out T?)` | `bool` | Try get Map as typed entity |
| `SetMap<T>(fieldName, entity)` | `void` | Set typed entity as Map |
| `GetMapsByPrefix<T>(prefix)` | `Dictionary<string, T>` | Get all Maps as typed entities |
| `GetMapsByPrefixWithStrippedKeys<T>(prefix)` | `Dictionary<string, T>` | Same with stripped keys |
| `SetMany(fields)` | `void` | Set multiple AttributeValues |
| `SetManyWithPrefix(prefix, fields)` | `void` | Set multiple with prefix |
| `SetMapsWithPrefix<T>(prefix, entities)` | `void` | Set multiple typed entities |
| `RemoveMany(fieldNames)` | `int` | Remove multiple, return count |
