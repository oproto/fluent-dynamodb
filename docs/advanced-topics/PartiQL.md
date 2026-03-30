# PartiQL Support

PartiQL is a SQL-compatible query language for DynamoDB that allows you to query, insert, update, and delete data using familiar SQL syntax. Oproto.FluentDynamoDb provides full PartiQL support with entity hydration.

## Overview

PartiQL support in Oproto.FluentDynamoDb follows the same request builder pattern as other operations:

- `PartiQLRequestBuilder<TEntity>` for building and executing statements
- Format string placeholders for parameter substitution
- Entity hydration for SELECT results
- Response metadata access after execution
- Batch PartiQL via `DynamoDbBatch.PartiQL`

## Basic Usage

### SELECT Queries

```csharp
// Simple SELECT with parameter
var users = await table.ExecutePartiQL<User>(
    "SELECT * FROM Users WHERE pk = {0}",
    "USER#123")
    .ToListAsync();

// SELECT with multiple parameters
var orders = await table.ExecutePartiQL<Order>(
    "SELECT * FROM Orders WHERE pk = {0} AND sk BETWEEN {1} AND {2}",
    "TENANT#A", "ORDER#2024-01", "ORDER#2024-12")
    .ToListAsync();

// SELECT specific attributes
var names = await table.ExecutePartiQL<User>(
    "SELECT name, email FROM Users WHERE pk = {0}",
    "USER#123")
    .ToListAsync();
```

### INSERT Statements

```csharp
await table.ExecutePartiQL<User>(
    "INSERT INTO Users VALUE {'pk': {0}, 'sk': {1}, 'name': {2}, 'email': {3}}",
    "USER#456", "PROFILE", "Jane Doe", "jane@example.com")
    .ExecuteAsync();
```

### UPDATE Statements

```csharp
await table.ExecutePartiQL<User>(
    "UPDATE Users SET name = {0}, updated_at = {1:o} WHERE pk = {2} AND sk = {3}",
    "Jane Smith", DateTime.UtcNow, "USER#456", "PROFILE")
    .ExecuteAsync();
```

### DELETE Statements

```csharp
await table.ExecutePartiQL<User>(
    "DELETE FROM Users WHERE pk = {0} AND sk = {1}",
    "USER#456", "PROFILE")
    .ExecuteAsync();
```

## Format String Placeholders

PartiQL statements support format string placeholders with optional format specifiers:

### Basic Placeholders

```csharp
// {0}, {1}, {2} - positional parameters
var users = await table.ExecutePartiQL<User>(
    "SELECT * FROM Users WHERE pk = {0} AND status = {1}",
    "USER#123", "active")
    .ToListAsync();
```

### DateTime Formatting

```csharp
// {0:o} - ISO 8601 format (recommended for DynamoDB)
var recentOrders = await table.ExecutePartiQL<Order>(
    "SELECT * FROM Orders WHERE pk = {0} AND created_at > {1:o}",
    "TENANT#A", DateTime.UtcNow.AddDays(-7))
    .ToListAsync();

// Other DateTime formats
// {0:s} - Sortable format
// {0:u} - Universal sortable format
// {0:yyyy-MM-dd} - Custom format
```

### Numeric Formatting

```csharp
// {0:F2} - Fixed-point with 2 decimal places
await table.ExecutePartiQL<Product>(
    "UPDATE Products SET price = {0:F2} WHERE pk = {1}",
    19.99m, "PROD#123")
    .ExecuteAsync();
```

## PartiQLRequestBuilder

### Builder Methods

```csharp
var builder = table.ExecutePartiQL<User>(
    "SELECT * FROM Users WHERE pk = {0}",
    "USER#123");

// Execute SELECT and get list
var users = await builder.ToListAsync();

// Execute SELECT for compound entity tables
var result = await builder.ToCompoundEntityAsync();
var users = result.GetEntities<User>();
var profiles = result.GetEntities<UserProfile>();

// Execute INSERT/UPDATE/DELETE
await builder.ExecuteAsync();

// Get underlying SDK request
ExecuteStatementRequest request = builder.ToRequest();
```

### Response Metadata

Access response metadata after execution:

```csharp
var builder = table.ExecutePartiQL<User>(
    "SELECT * FROM Users WHERE pk = {0}",
    "USER#123");

var users = await builder.ToListAsync();

// Access metadata after execution via .Response property
ResponseMetadata? metadata = builder.Response?.ResponseMetadata;
ConsumedCapacity? capacity = builder.Response?.ConsumedCapacity;

if (capacity != null)
{
    Console.WriteLine($"Read capacity consumed: {capacity.ReadCapacityUnits}");
}
```

## Batch PartiQL

Execute multiple PartiQL statements in a single batch using `DynamoDbBatch.PartiQL`:

### Basic Batch

```csharp
var response = await DynamoDbBatch.PartiQL
    .Add(table.ExecutePartiQL<User>(
        "SELECT * FROM Users WHERE pk = {0}",
        "USER#123"))
    .Add(table.ExecutePartiQL<Order>(
        "SELECT * FROM Orders WHERE pk = {0}",
        "ORDER#456"))
    .ExecuteAsync();

// Access results by index
var user = response.GetItem<User>(0);
var order = response.GetItem<Order>(1);
```

### Tuple Convenience Methods

```csharp
// Single result
var user = await DynamoDbBatch.PartiQL
    .Add(table.ExecutePartiQL<User>("SELECT * FROM Users WHERE pk = {0}", "USER#123"))
    .ExecuteAndMapAsync<User>();

// Two results
var (user, order) = await DynamoDbBatch.PartiQL
    .Add(table.ExecutePartiQL<User>("SELECT * FROM Users WHERE pk = {0}", "USER#123"))
    .Add(table.ExecutePartiQL<Order>("SELECT * FROM Orders WHERE pk = {0}", "ORDER#456"))
    .ExecuteAndMapAsync<User, Order>();

// Up to 8 results supported
var (a, b, c, d) = await DynamoDbBatch.PartiQL
    .Add(table.ExecutePartiQL<EntityA>("SELECT * FROM TableA WHERE pk = {0}", "A"))
    .Add(table.ExecutePartiQL<EntityB>("SELECT * FROM TableB WHERE pk = {0}", "B"))
    .Add(table.ExecutePartiQL<EntityC>("SELECT * FROM TableC WHERE pk = {0}", "C"))
    .Add(table.ExecutePartiQL<EntityD>("SELECT * FROM TableD WHERE pk = {0}", "D"))
    .ExecuteAndMapAsync<EntityA, EntityB, EntityC, EntityD>();
```

### Mixed Operations

Unlike `BatchGetItem`/`BatchWriteItem`, PartiQL batch can mix SELECT, INSERT, UPDATE, and DELETE:

```csharp
await DynamoDbBatch.PartiQL
    .Add(table.ExecutePartiQL<User>(
        "UPDATE Users SET last_login = {0:o} WHERE pk = {1}",
        DateTime.UtcNow, "USER#123"))
    .Add(table.ExecutePartiQL<AuditLog>(
        "INSERT INTO AuditLogs VALUE {'pk': {0}, 'sk': {1}, 'action': {2}}",
        "AUDIT#123", DateTime.UtcNow.ToString("O"), "LOGIN"))
    .ExecuteAsync();
```

### Explicit Client Configuration

```csharp
// Client inferred from first builder added
var response = await DynamoDbBatch.PartiQL
    .Add(table.ExecutePartiQL<User>("SELECT * FROM Users WHERE pk = {0}", "USER#123"))
    .ExecuteAsync();

// Or explicitly set client
var response = await DynamoDbBatch.PartiQL
    .WithClient(customClient)
    .Add(table.ExecutePartiQL<User>("SELECT * FROM Users WHERE pk = {0}", "USER#123"))
    .ExecuteAsync();

// Or pass client to ExecuteAsync
var response = await DynamoDbBatch.PartiQL
    .Add(table.ExecutePartiQL<User>("SELECT * FROM Users WHERE pk = {0}", "USER#123"))
    .ExecuteAsync(customClient);
```

## DynamicTable PartiQL

Use PartiQL with DynamicTable for schema-less queries:

```csharp
var dynamicTable = new DynamicTable(client, "my-table");

// SELECT returns DynamicEntity instances
var items = await dynamicTable.ExecutePartiQL(
    "SELECT * FROM \"my-table\" WHERE pk = {0}",
    "ITEM#123")
    .ToListAsync();

foreach (var item in items)
{
    var name = item.DynamicFields.GetString("name");
    var age = item.DynamicFields.GetInt("age");
}
```

## Compound Entity Tables

For tables with multiple entity types, use `ToCompoundEntityAsync()`:

```csharp
// Query returns mixed entity types
var result = await table.ExecutePartiQL<Order>(
    "SELECT * FROM Orders WHERE pk = {0}",
    "ORDER#123")
    .ToCompoundEntityAsync();

// Extract each entity type
var orders = result.GetEntities<Order>();
var orderLines = result.GetEntities<OrderLine>();
var orderNotes = result.GetEntities<OrderNote>();
```

## PartiQL vs Query/Scan

| Feature | PartiQL | Query/Scan Builders |
|---------|---------|---------------------|
| Syntax | SQL-like | Fluent methods |
| Learning curve | Familiar to SQL users | DynamoDB-specific |
| Type safety | String-based | Lambda expressions |
| Complex joins | Limited support | Not supported |
| Performance | Same as Query/Scan | Same as PartiQL |
| Batch operations | Yes (BatchExecuteStatement) | Yes (BatchGetItem, etc.) |

### When to Use PartiQL

- Migrating from SQL databases
- Complex queries that are easier to express in SQL
- Ad-hoc queries in admin tools
- When SQL syntax is more readable for your team

### When to Use Query/Scan Builders

- Type-safe queries with compile-time checking
- IntelliSense support for entity properties
- Consistent API across all operations
- When lambda expressions are preferred

## Error Handling

### Syntax Errors

```csharp
try
{
    await table.ExecutePartiQL<User>(
        "SELEC * FROM Users", // Typo in SELECT
        "USER#123")
        .ToListAsync();
}
catch (AmazonDynamoDBException ex)
{
    // PartiQL syntax error from DynamoDB
    Console.WriteLine($"PartiQL error: {ex.Message}");
}
```

### Hydration Errors

```csharp
try
{
    // Query returns data that doesn't match User entity
    var users = await table.ExecutePartiQL<User>(
        "SELECT * FROM DifferentTable WHERE pk = {0}",
        "ITEM#123")
        .ToListAsync();
}
catch (DynamoDbMappingException ex)
{
    Console.WriteLine($"Hydration error: {ex.Message}");
}
```

## Best Practices

### Use Parameterized Queries

Always use format placeholders instead of string concatenation:

```csharp
// ✅ Good - parameterized
var users = await table.ExecutePartiQL<User>(
    "SELECT * FROM Users WHERE pk = {0}",
    userId)
    .ToListAsync();

// ❌ Bad - string concatenation (SQL injection risk)
var users = await table.ExecutePartiQL<User>(
    $"SELECT * FROM Users WHERE pk = '{userId}'")
    .ToListAsync();
```

### Quote Table Names with Special Characters

```csharp
// Table names with hyphens need quotes
var items = await table.ExecutePartiQL<Item>(
    "SELECT * FROM \"my-table-name\" WHERE pk = {0}",
    "ITEM#123")
    .ToListAsync();
```

### Use ISO 8601 for Dates

```csharp
// Use {0:o} format specifier for consistent date handling
var orders = await table.ExecutePartiQL<Order>(
    "SELECT * FROM Orders WHERE created_at > {0:o}",
    DateTime.UtcNow.AddDays(-30))
    .ToListAsync();
```

## See Also

- [DynamicTable](DynamicTable.md) - Schema-less table access
- [Basic Operations](../core-features/BasicOperations.md) - Standard CRUD operations
- [Batch Operations](../core-features/BatchOperations.md) - Batch get and write
- [AWS PartiQL Documentation](https://docs.aws.amazon.com/amazondynamodb/latest/developerguide/ql-reference.html)
