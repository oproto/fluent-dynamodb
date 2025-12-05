# TodoList Example

A simple todo list application demonstrating basic CRUD operations with FluentDynamoDb.

## Features Demonstrated

- **CRUD Operations**: Create, Read, Update, and Delete todo items
- **Scannable Tables**: Using the `[Scannable]` attribute for small datasets
- **Entity Mapping**: Automatic mapping between C# objects and DynamoDB items
- **Format String Expressions**: Using `{0}`, `{1:o}` syntax for update expressions

## Key Concepts

### Scannable Tables

The `[Scannable]` attribute enables full table scan operations. This is appropriate for:
- Small datasets (hundreds of items)
- Personal todo lists
- Development and testing scenarios

```csharp
[DynamoDbEntity]
[DynamoDbTable("todo-items", IsDefault = true)]
[Scannable]  // Enables Scan() operations
public partial class TodoItem : IDynamoDbEntity
{
    // ...
}
```

### Simple Key Design

This example uses a simple partition key design (no sort key):
- **Partition Key**: `pk` - The unique todo item ID

### CRUD Operations

```csharp
// Create
var item = await table.AddAsync("Buy groceries");

// Read all
var items = await table.GetAllAsync();

// Update
await table.MarkCompleteAsync(itemId);
await table.EditDescriptionAsync(itemId, "New description");

// Delete
await table.DeleteAsync(itemId);
```

## Running the Example

### Prerequisites

1. **DynamoDB Local** must be running on port 8000:
   ```bash
   # Using the included DynamoDB Local
   cd dynamodb-local
   java -Djava.library.path=./DynamoDBLocal_lib -jar DynamoDBLocal.jar -sharedDb
   ```

2. **.NET 8.0 SDK** installed

### Run the Application

```bash
cd examples/TodoList
dotnet run
```

### Interactive Menu

The application provides an interactive menu:
1. **Add Todo** - Create a new todo item
2. **List All Todos** - View all items (incomplete first, then completed)
3. **Mark Complete** - Mark a todo as done
4. **Edit Description** - Update a todo's description
5. **Delete Todo** - Remove a todo item
6. **Exit** - Close the application

## Project Structure

```
TodoList/
├── Entities/
│   └── TodoItem.cs      # Entity with DynamoDB attributes
├── Tables/
│   └── TodoTable.cs     # Table class with CRUD operations
├── Program.cs           # Interactive console application
├── TodoList.csproj      # Project file
└── README.md            # This file
```

## Code Highlights

### Entity Definition

```csharp
[DynamoDbEntity]
[DynamoDbTable("todo-items", IsDefault = true)]
[Scannable]
[GenerateEntityProperty(Name = "TodoItems")]
public partial class TodoItem : IDynamoDbEntity
{
    [PartitionKey]
    [DynamoDbAttribute("pk")]
    public string Id { get; set; }

    [DynamoDbAttribute("description")]
    public string Description { get; set; }

    [DynamoDbAttribute("isComplete")]
    public bool IsComplete { get; set; }

    [DynamoDbAttribute("createdAt")]
    public DateTime CreatedAt { get; set; }

    [DynamoDbAttribute("completedAt")]
    public DateTime? CompletedAt { get; set; }
}
```

### Update with Format Strings

```csharp
// Format string approach - concise and readable
await Update<TodoItem>()
    .WithKey("pk", id)
    .Set("SET isComplete = {0}, completedAt = {1:o}", true, DateTime.UtcNow)
    .UpdateAsync();
```

## Learn More

- [FluentDynamoDb Documentation](https://fluentdynamodb.dev)
- [Scannable Tables Guide](../../docs/core-features/ScannableTables.md)
- [Update Expressions](../../docs/core-features/ExpressionBasedUpdates.md)
