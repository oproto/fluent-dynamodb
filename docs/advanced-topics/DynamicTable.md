# DynamicTable - Schema-less Table Access

DynamicTable provides schema-less access to any DynamoDB table without requiring entity class definitions. This is useful for schema exploration, migration tools, and working with tables that have no fixed schema.

## Overview

While typed entities provide compile-time safety and IntelliSense, there are scenarios where you need to work with DynamoDB tables without predefined schemas:

- **Schema Exploration**: Inspecting tables with unknown or evolving schemas
- **Migration Tools**: Moving data between tables with different structures
- **Admin Utilities**: Building tools that work with any table
- **Truly Schema-less Data**: Tables where items have varying attributes

DynamicTable and DynamicEntity provide this flexibility while maintaining the fluent API patterns you're familiar with.

## Basic Usage

### Creating a DynamicTable

```csharp
using Oproto.FluentDynamoDb.Storage;
using Oproto.FluentDynamoDb.Entities;

// Basic DynamicTable - requires AttributeValue keys for all operations
var table = new DynamicTable(client, "my-table");

// With key configuration - enables typed key methods
var keyOptions = new DynamicTableKeyOptions
{
    PartitionKeyName = "pk",
    PartitionKeyType = ScalarAttributeType.S,
    SortKeyName = "sk",
    SortKeyType = ScalarAttributeType.S
};
var table = new DynamicTable(client, "my-table", keyOptions);

// With FluentDynamoDbOptions
var options = new FluentDynamoDbOptions()
    .WithLogger(logger);
var table = new DynamicTable(client, "my-table", keyOptions, options);
```

### Reading Items

```csharp
// With typed keys (requires key configuration)
var item = await table.GetAsync("USER#123", "PROFILE");

// Access attributes via DynamicFields
if (item != null)
{
    var name = item.DynamicFields.GetString("name");
    var age = item.DynamicFields.GetInt("age");
    var isActive = item.DynamicFields.GetBool("is_active");
    var balance = item.DynamicFields.GetDecimal("balance");
}

// With AttributeValue keys (always available)
var pk = new AttributeValue { S = "USER#123" };
var sk = new AttributeValue { S = "PROFILE" };
var item = await table.GetAsync(pk, sk);
```

### Writing Items

```csharp
// Create a DynamicEntity
var entity = new DynamicEntity();
entity.DynamicFields.SetString("pk", "USER#456");
entity.DynamicFields.SetString("sk", "PROFILE");
entity.DynamicFields.SetString("name", "Jane Doe");
entity.DynamicFields.SetInt("age", 30);
entity.DynamicFields.SetBool("is_active", true);
entity.DynamicFields.SetDecimal("balance", 1234.56m);

// Put the item
await table.PutAsync(entity);
```

### Querying

```csharp
// Query with lambda expressions
var items = await table.Query()
    .Where(x => x.DynamicFields["pk"] == "USER#123")
    .ToListAsync();

// Query with sort key condition
var items = await table.Query()
    .Where(x => x.DynamicFields["pk"] == "TENANT#A" 
             && x.DynamicFields["sk"].BeginsWith("ORDER#"))
    .ToListAsync();

// Query with filter
var items = await table.Query()
    .Where(x => x.DynamicFields["pk"] == "TENANT#A")
    .WithFilter(x => x.DynamicFields["status"] == "active")
    .ToListAsync();
```

### Scanning

```csharp
// Scan all items
var allItems = await table.Scan().ToListAsync();

// Scan with filter
var activeItems = await table.Scan()
    .WithFilter(x => x.DynamicFields["status"] == "active")
    .ToListAsync();
```

### Updating Items

```csharp
// Update with typed keys
await table.Update("USER#123", "PROFILE")
    .Set("name", new AttributeValue { S = "Jane Smith" })
    .Set("updated_at", new AttributeValue { S = DateTime.UtcNow.ToString("O") })
    .UpdateAsync();

// Update with AttributeValue keys
var pk = new AttributeValue { S = "USER#123" };
var sk = new AttributeValue { S = "PROFILE" };
await table.Update(pk, sk)
    .Set("name", new AttributeValue { S = "Jane Smith" })
    .UpdateAsync();
```

### Deleting Items

```csharp
// Delete with typed keys
await table.DeleteAsync("USER#123", "PROFILE");

// Delete with AttributeValue keys
var pk = new AttributeValue { S = "USER#123" };
var sk = new AttributeValue { S = "PROFILE" };
await table.DeleteAsync(pk, sk);
```

## Key Configuration

### DynamicTableKeyOptions

Configure key schema to enable typed key methods:

```csharp
public class DynamicTableKeyOptions
{
    // Partition key configuration
    public string PartitionKeyName { get; set; } = "pk";
    public ScalarAttributeType PartitionKeyType { get; set; } = ScalarAttributeType.S;
    
    // Sort key configuration (optional)
    public string? SortKeyName { get; set; }
    public ScalarAttributeType? SortKeyType { get; set; }
}
```

### String Keys (Most Common)

```csharp
var keyOptions = new DynamicTableKeyOptions
{
    PartitionKeyName = "pk",
    PartitionKeyType = ScalarAttributeType.S,
    SortKeyName = "sk",
    SortKeyType = ScalarAttributeType.S
};

var table = new DynamicTable(client, "my-table", keyOptions);

// Now you can use string key methods
var item = await table.GetAsync("partition-value", "sort-value");
await table.DeleteAsync("partition-value", "sort-value");
```

### Numeric Keys

```csharp
var keyOptions = new DynamicTableKeyOptions
{
    PartitionKeyName = "id",
    PartitionKeyType = ScalarAttributeType.N,
    SortKeyName = "version",
    SortKeyType = ScalarAttributeType.N
};

var table = new DynamicTable(client, "my-table", keyOptions);

// Use numeric key methods
var item = await table.GetAsync(12345L, 1L);
await table.DeleteAsync(12345L, 1L);
```

### Partition Key Only (No Sort Key)

```csharp
var keyOptions = new DynamicTableKeyOptions
{
    PartitionKeyName = "id",
    PartitionKeyType = ScalarAttributeType.S
    // SortKeyName and SortKeyType left null
};

var table = new DynamicTable(client, "my-table", keyOptions);

// Use single-key methods
var item = await table.GetAsync("item-123");
await table.DeleteAsync("item-123");
```

## DynamicEntity

### Working with DynamicFields

DynamicEntity stores all attributes in a `DynamicFieldCollection`:

```csharp
var entity = new DynamicEntity();

// Typed setters
entity.DynamicFields.SetString("name", "John Doe");
entity.DynamicFields.SetInt("age", 25);
entity.DynamicFields.SetLong("timestamp", DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
entity.DynamicFields.SetDouble("score", 98.5);
entity.DynamicFields.SetDecimal("price", 19.99m);
entity.DynamicFields.SetBool("active", true);
entity.DynamicFields.SetDateTime("created", DateTime.UtcNow);
entity.DynamicFields.SetBytes("data", new byte[] { 1, 2, 3 });

// Typed getters
var name = entity.DynamicFields.GetString("name");
var age = entity.DynamicFields.GetInt("age");
var timestamp = entity.DynamicFields.GetLong("timestamp");
var score = entity.DynamicFields.GetDouble("score");
var price = entity.DynamicFields.GetDecimal("price");
var active = entity.DynamicFields.GetBool("active");
var created = entity.DynamicFields.GetDateTime("created");
var data = entity.DynamicFields.GetBytes("data");

// Check if field exists
if (entity.DynamicFields.ContainsKey("optional_field"))
{
    var value = entity.DynamicFields.GetString("optional_field");
}

// Get field type
var fieldType = entity.DynamicFields.GetFieldType("name"); // DynamicFieldType.String
```

### Expression Support

DynamicEntity supports lambda expressions in queries and filters:

```csharp
// Equality
.Where(x => x.DynamicFields["pk"] == "value")

// Comparison operators
.WithFilter(x => x.DynamicFields["age"] > 18)
.WithFilter(x => x.DynamicFields["score"] >= 90)
.WithFilter(x => x.DynamicFields["price"] < 100)
.WithFilter(x => x.DynamicFields["count"] <= 10)
.WithFilter(x => x.DynamicFields["status"] != "deleted")

// String operations
.Where(x => x.DynamicFields["sk"].BeginsWith("ORDER#"))
.WithFilter(x => x.DynamicFields["name"].Contains("John"))

// Existence checks
.WithFilter(x => x.DynamicFields.Exists("optional_field"))
.WithFilter(x => x.DynamicFields.NotExists("deleted_at"))
```

## Use Cases

### Schema Exploration

```csharp
// Explore an unknown table
var table = new DynamicTable(client, "unknown-table");

// Scan a sample of items
var sample = await table.Scan()
    .Take(10)
    .ToListAsync();

// Discover the schema
foreach (var item in sample)
{
    Console.WriteLine("Item attributes:");
    foreach (var key in item.DynamicFields.Keys)
    {
        var type = item.DynamicFields.GetFieldType(key);
        Console.WriteLine($"  {key}: {type}");
    }
}
```

### Data Migration

```csharp
// Copy data between tables with transformation
var sourceTable = new DynamicTable(sourceClient, "old-table");
var targetTable = new DynamicTable(targetClient, "new-table", targetKeyOptions);

await foreach (var item in sourceTable.Scan().ToAsyncEnumerable())
{
    // Transform the item
    var newItem = new DynamicEntity();
    newItem.DynamicFields.SetString("pk", item.DynamicFields.GetString("old_pk"));
    newItem.DynamicFields.SetString("sk", item.DynamicFields.GetString("old_sk"));
    
    // Copy other fields
    foreach (var key in item.DynamicFields.Keys)
    {
        if (key != "old_pk" && key != "old_sk")
        {
            newItem.DynamicFields[key] = item.DynamicFields[key];
        }
    }
    
    await targetTable.PutAsync(newItem);
}
```

### Admin Utilities

```csharp
// Generic table browser
public async Task<List<DynamicEntity>> BrowseTable(
    IAmazonDynamoDB client,
    string tableName,
    string? filterExpression = null)
{
    var table = new DynamicTable(client, tableName);
    
    var scan = table.Scan();
    
    // Note: For complex filters, use the format string approach
    // or build the filter programmatically
    
    return await scan.Take(100).ToListAsync();
}
```

## Comparison: DynamicTable vs Typed Entities

| Feature | Typed Entities | DynamicTable |
|---------|---------------|--------------|
| Compile-time safety | ✅ Yes | ❌ No |
| IntelliSense for fields | ✅ Yes | ❌ No |
| Schema definition required | ✅ Yes | ❌ No |
| Works with unknown schemas | ❌ No | ✅ Yes |
| Performance | ✅ Optimal | ✅ Optimal |
| Lambda expression support | ✅ Full | ✅ Via DynamicFields indexer |
| Key validation | ✅ Automatic | ⚠️ Manual (via KeyOptions) |

### When to Use DynamicTable

- Exploring tables with unknown schemas
- Building migration or admin tools
- Working with truly schema-less data
- Prototyping before defining entity classes

### When to Use Typed Entities

- Production application code
- When schema is known and stable
- When compile-time safety is important
- When IntelliSense improves developer productivity

## Error Handling

### Key Configuration Errors

```csharp
// Using typed key methods without key configuration throws
var table = new DynamicTable(client, "my-table"); // No key options

try
{
    await table.GetAsync("pk-value"); // Throws InvalidOperationException
}
catch (InvalidOperationException ex)
{
    // "Key options must be configured to use typed key methods."
}
```

### Sort Key Errors

```csharp
// Providing sort key when not configured throws
var keyOptions = new DynamicTableKeyOptions
{
    PartitionKeyName = "pk",
    PartitionKeyType = ScalarAttributeType.S
    // No sort key configured
};
var table = new DynamicTable(client, "my-table", keyOptions);

try
{
    await table.GetAsync("pk-value", "sk-value"); // Throws InvalidOperationException
}
catch (InvalidOperationException ex)
{
    // "Sort key was provided but DynamicTableKeyOptions does not define a sort key."
}
```

### Type Mismatch Errors

```csharp
// Getting wrong type throws DynamicFieldTypeException
var entity = new DynamicEntity();
entity.DynamicFields.SetString("name", "John");

try
{
    var age = entity.DynamicFields.GetInt("name"); // Throws DynamicFieldTypeException
}
catch (DynamicFieldTypeException ex)
{
    // "Field 'name' is of type String, not Number."
}
```

## See Also

- [Dynamic Fields](../core-features/DynamicFields.md) - Using dynamic fields with typed entities
- [PartiQL](PartiQL.md) - SQL-like queries with DynamicEntity
- [Basic Operations](../core-features/BasicOperations.md) - Standard CRUD operations
