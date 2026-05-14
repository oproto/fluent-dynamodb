# DynamicTableDemo Example

A demonstration of schema-less access to DynamoDB tables using `DynamicTable` and `DynamicEntity`.

## Features Demonstrated

- **Schema-less Access**: Work with any DynamoDB table without defining entity classes
- **Key Configuration**: Configure typed key methods for convenient access
- **CRUD Operations**: Create, Read, Update, and Delete using dynamic entities
- **Lambda Expressions**: Query and filter using `DynamicFields` indexer syntax
- **Multiple Data Types**: Store and retrieve strings, numbers, booleans, and dates

## Key Concepts

### When to Use DynamicTable

`DynamicTable` is ideal for:
- **Schema Exploration**: Discovering the structure of unknown tables
- **Migration Tools**: Moving data between tables with different schemas
- **Truly Schema-less Data**: Tables where items have varying attributes
- **Quick Prototyping**: Testing ideas without defining entity classes

For production applications with known schemas, typed entities provide better compile-time safety and IntelliSense support.

### Key Configuration

Configure `DynamicTableKeyOptions` to enable typed key methods:

```csharp
var keyOptions = new DynamicTableKeyOptions
{
    PartitionKeyName = "pk",
    PartitionKeyType = ScalarAttributeType.S,
    SortKeyName = "sk",
    SortKeyType = ScalarAttributeType.S
};

var table = new DynamicTable(client, "my-table", keyOptions);

// Now you can use typed key methods
var item = await table.GetAsync("USER#123", "PROFILE");
await table.DeleteAsync("USER#123", "PROFILE");
```

Without key configuration, you must use `AttributeValue` parameters:

```csharp
var table = new DynamicTable(client, "my-table");

// Must use AttributeValue for keys
var item = await table.GetAsync(
    new AttributeValue { S = "USER#123" },
    new AttributeValue { S = "PROFILE" });
```

### Working with DynamicEntity

`DynamicEntity` stores all attributes in a `DynamicFields` collection:

```csharp
// Creating an entity
var entity = new DynamicEntity();
entity.DynamicFields.SetString("pk", "USER#123");
entity.DynamicFields.SetString("sk", "PROFILE");
entity.DynamicFields.SetString("name", "Alice");
entity.DynamicFields.SetInt("age", 28);
entity.DynamicFields.SetDateTime("createdAt", DateTime.UtcNow);

await table.PutAsync(entity);

// Reading an entity
var item = await table.GetAsync("USER#123", "PROFILE");
var name = item.DynamicFields.GetString("name");
var age = item.DynamicFields.GetInt("age");
```

### Query and Scan Operations

Use lambda expressions with the `DynamicFields` indexer:

```csharp
// Query by partition key
var items = await table.Query()
    .Where(x => x.DynamicFields["pk"] == "USER#123")
    .ToListAsync();

// Scan with filter
var activeUsers = await table.Scan()
    .WithFilter(x => x.DynamicFields["status"] == "active")
    .ToListAsync();

// Format string alternative
var items = await table.Query()
    .Where("pk = {0}", "USER#123")
    .ToListAsync();
```

### Update Operations

```csharp
// Update using format strings
await table.Update("USER#123", "PROFILE")
    .Set("name = {0}", "Alice Smith")
    .Set("updatedAt = {0}", DateTime.UtcNow.ToString("o"))
    .UpdateAsync();
```

## Running the Example

### Prerequisites

1. **DynamoDB Local** must be running on port 8000:
   ```bash
   cd dynamodb-local
   java -Djava.library.path=./DynamoDBLocal_lib -jar DynamoDBLocal.jar -sharedDb
   ```

2. **.NET 8.0 SDK** installed

### Run the Application

```bash
cd examples/DynamicTableDemo
dotnet run
```

### Interactive Menu

1. **Add Item** - Create a new item with custom fields
2. **Get Item by Key** - Retrieve an item using pk/sk
3. **Query Items** - Query by partition key
4. **Scan All Items** - List all items with optional filter
5. **Update Item** - Modify an existing item's fields
6. **Delete Item** - Remove an item
7. **Seed Sample Data** - Populate with example items
8. **Exit** - Close the application

## Project Structure

```
DynamicTableDemo/
├── Program.cs               # Interactive console application
├── DynamicTableDemo.csproj  # Project file
└── README.md                # This file
```

## Trade-offs vs Typed Entities

| Aspect | DynamicTable | Typed Entities |
|--------|--------------|----------------|
| Compile-time safety | ❌ No | ✅ Yes |
| IntelliSense | ❌ Limited | ✅ Full |
| Schema flexibility | ✅ Any schema | ❌ Fixed schema |
| Setup required | ✅ Minimal | ❌ Entity classes |
| Performance | ⚠️ Slightly slower | ✅ Optimized |
| Best for | Exploration, migration | Production apps |

## Learn More

- [DynamicTable Documentation](../../docs/advanced-topics/DynamicTable.md)
- [FluentDynamoDb Documentation](https://fluentdynamodb.dev)
