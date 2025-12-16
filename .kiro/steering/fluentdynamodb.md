# FluentDynamoDb API Reference

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

// Scannable (required for Scan operations)
[DynamoDbTable("Logs")]
[Scannable]
public partial class LogEntry { ... }
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
// Lambda (Preferred)
var users = await table.Users.Query()
    .Where(x => x.CustomerId == tenantId && x.OrderId.StartsWith("2024"))
    .ToListAsync();

// Format String
var users = await table.Users.Query()
    .Where("pk = {0} AND begins_with(sk, {1})", tenantId, "2024")
    .ToListAsync();

// Filter
var users = await table.Users.Query()
    .Where(x => x.CustomerId == tenantId)
    .WithFilter(x => x.Status == "active")
    .ToListAsync();

// Pagination
var query = table.Users.Query()
    .Where(x => x.CustomerId == tenantId)
    .Take(25);
var users = await query.ToListAsync();
var lastKey = query.Response.LastEvaluatedKey;  // Access response after execution

var nextPage = await table.Users.Query()
    .Where(x => x.CustomerId == tenantId)
    .WithPaginationToken(lastKey)
    .ToListAsync();

// Options
var users = await table.Users.Query()
    .Where(x => x.CustomerId == tenantId)
    .UsingConsistentRead()
    .ScanIndexForward(false)
    .WithProjection("name, email")
    .ToListAsync();
```

## Scan Operations

> Requires `[Scannable]` attribute on entity.

```csharp
var logs = await table.Logs.Scan().ToListAsync();
var logs = await table.Logs.Scan().WithFilter(x => x.Level == "ERROR").Take(100).ToListAsync();
```

## Index Operations (GSI/LSI)

```csharp
var products = await table.gsi1.Query<Product>()
    .Where(x => x.CategoryId == categoryId)
    .WithProjection("productId, productName")
    .ToListAsync();

var orders = await table.lsi1.Query<Order>()
    .Where(x => x.CustomerId == customerId && x.CreatedAt > startDate)
    .ToListAsync();
```

## Batch Operations

```csharp
// Batch Get
var response = await DynamoDbBatch.Get
    .Add(table.Users.Get(userId1))
    .Add(table.Users.Get(userId2))
    .ExecuteAsync();
var users = response.Responses["Users"];

// Batch Get with tuple mapping
var (user, order) = await DynamoDbBatch.Get
    .Add(table.Users.Get(userId))
    .Add(table.Orders.Get(customerId, orderId))
    .ExecuteAndMapAsync<User, Order>();

// Batch Write
await DynamoDbBatch.Write
    .Add(table.Users.Put(user1))
    .Add(table.Users.Delete(oldUserId))
    .ExecuteAsync();

// Batch PartiQL
var response = await DynamoDbBatch.PartiQL
    .Add(table.ExecutePartiQL<User>("SELECT * FROM Users WHERE pk = ?", userId))
    .ExecuteAsync();
var user = response.GetItem<User>(0);
```

## Transactions

```csharp
// Transaction Write
await DynamoDbTransactions.Write
    .Add(table.Users.Put(newUser))
    .Add(table.Accounts.Update(accountId).Set(x => new AccountUpdateModel { Balance = x.Balance - 100 }))
    .Add(table.Orders.Put(order).Where(x => x.OrderId.AttributeNotExists()))
    .Add(table.Audit.ConditionCheck(auditId).Where(x => x.Version == expectedVersion))
    .ExecuteAsync();

// Transaction Get
var response = await DynamoDbTransactions.Get
    .Add(table.Users.Get(userId))
    .Add(table.Accounts.Get(accountId))
    .ExecuteAsync();
var userItem = response.Responses[0].Item;

// Idempotency
await DynamoDbTransactions.Write.Add(table.Orders.Put(order)).WithClientRequestToken(token).ExecuteAsync();
```

## PartiQL

```csharp
// Select
var users = await table.ExecutePartiQL<User>("SELECT * FROM Users WHERE pk = ?", userId).ToListAsync();

// Insert/Update/Delete
await table.ExecutePartiQL("INSERT INTO Users VALUE {'pk': ?, 'name': ?}", userId, name).ExecuteAsync();
await table.ExecutePartiQL("UPDATE Users SET name = ? WHERE pk = ?", newName, userId).ExecuteAsync();
await table.ExecutePartiQL("DELETE FROM Users WHERE pk = ?", userId).ExecuteAsync();
```

## Raw SDK Access

```csharp
// Pre-built SDK requests
var request = new GetItemRequest { TableName = "Users", Key = ... };
var user = await table.Get<User>(request).GetItemAsync();

var queryRequest = new QueryRequest { TableName = "Orders", KeyConditionExpression = "pk = :pk", ... };
var orders = await table.Query<Order>(queryRequest).ToListAsync();

// Direct SDK execution
await DynamoDbTransactions.WriteAsync(client, transactWriteRequest);
await DynamoDbBatch.GetAsync(client, batchGetRequest);
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

Extension methods for DynamoDB functions in lambda expressions:

| C# Method | DynamoDB Function | Example |
|-----------|------------------|---------|
| `string.StartsWith()` | `begins_with()` | `x => x.Name.StartsWith("John")` |
| `string.Contains()` | `contains()` | `x => x.Email.Contains("@example")` |
| `.Between(low, high)` | `BETWEEN` | `x => x.Age.Between(18, 65)` |
| `.AttributeExists()` | `attribute_exists()` | `x => x.OptionalField.AttributeExists()` |
| `.AttributeNotExists()` | `attribute_not_exists()` | `x => x.Id.AttributeNotExists()` |
| `.Size()` | `size()` | `x => x.Items.Size() > 5` |

```csharp
// Conditional put (create only) - Lambda style
await table.Users.Put(user).Where(x => x.UserId.AttributeNotExists()).PutAsync();

// Range query on sort key
var orders = await table.Orders.Query()
    .Where(x => x.CustomerId == customerId && x.OrderDate.Between("2024-01", "2024-12"))
    .ToListAsync();

// Filter by collection size
var users = await table.Users.Query()
    .Where(x => x.TenantId == tenantId)
    .WithFilter(x => x.Tags.Size() > 0 && x.Email.AttributeExists())
    .ToListAsync();

// Check for optional fields
var incomplete = await table.Users.Scan()
    .WithFilter(x => x.PhoneNumber.AttributeNotExists() || x.Email.AttributeNotExists())
    .ToListAsync();
```

## Common Patterns

```csharp
// Optimistic locking
await table.Users.Update(userId)
    .Set(x => new UserUpdateModel { Version = x.Version + 1 })
    .Where(x => x.Version == currentVersion)
    .UpdateAsync();

// Conditional put (create only)
await table.Users.Put(user).Where(x => x.UserId.AttributeNotExists()).PutAsync();

// Increment counter
await table.Users.Update(userId).Set(x => new UserUpdateModel { Count = x.Count + 1 }).UpdateAsync();
```
