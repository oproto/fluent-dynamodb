# TodoList Example

A simple todo list application demonstrating basic CRUD operations with FluentDynamoDb.

## Features Demonstrated

- **CRUD Operations**: Create, Read, Update, and Delete todo items
- **Scannable Tables**: Using the `[Scannable]` attribute for small datasets
- **Entity Mapping**: Automatic mapping between C# objects and DynamoDB items
- **Lambda Expression Updates**: Using type-safe lambda expressions with update models

## Key Concepts

### Scannable Tables

The `[Scannable]` attribute enables full table scan operations. This is appropriate for:
- Small datasets (hundreds of items)
- Personal todo lists
- Development and testing scenarios

```csharp
[DynamoDbTable("todo-items", IsDefault = true)]
[Scannable]  // Enables Scan() operations
[GenerateEntityProperty(Name = "TodoItems")]
public partial class TodoItem
{
    // ...
}
```

### Simple Key Design

This example uses a simple partition key design (no sort key):
- **Partition Key**: `pk` - The unique todo item ID

### CRUD Operations

```csharp
// Create - using generated entity accessor
await table.TodoItems.PutAsync(item);

// Read all - using generated Scan accessor
var items = await table.TodoItems.Scan().ToListAsync();

// Update - using lambda expression with update model
await table.TodoItems.Update(itemId)
    .Set(x => new TodoItemUpdateModel { IsComplete = true, CompletedAt = DateTime.UtcNow })
    .UpdateAsync();

// Delete - using generated entity accessor
await table.TodoItems.DeleteAsync(itemId);
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
│   ├── TodoItem.cs          # Entity with DynamoDB attributes
│   └── TodoItemsTable.cs    # Table class with generated entity accessor
├── Program.cs               # Interactive console application
├── TodoList.csproj          # Project file
└── README.md                # This file
```

## Code Highlights

### Entity Definition

```csharp
[DynamoDbTable("todo-items", IsDefault = true)]
[Scannable]
[GenerateEntityProperty(Name = "TodoItems")]
public partial class TodoItem
{
    [PartitionKey]
    [DynamoDbAttribute("pk")]
    public string Id { get; set; } = string.Empty;

    [DynamoDbAttribute("description")]
    public string Description { get; set; } = string.Empty;

    [DynamoDbAttribute("isComplete")]
    public bool IsComplete { get; set; }

    [DynamoDbAttribute("createdAt")]
    public DateTime CreatedAt { get; set; }

    [DynamoDbAttribute("completedAt")]
    public DateTime? CompletedAt { get; set; }
}
```

### Update with Lambda Expressions

```csharp
// PREFERRED: Lambda expression approach - type-safe with IntelliSense
await table.TodoItems.Update(id)
    .Set(x => new TodoItemUpdateModel { 
        IsComplete = true, 
        CompletedAt = DateTime.UtcNow 
    })
    .UpdateAsync();
```

## Learn More

- [FluentDynamoDb Documentation](https://fluentdynamodb.dev)
- [Scannable Tables Guide](../../docs/core-features/ScannableTables.md)
- [Update Expressions](../../docs/core-features/ExpressionBasedUpdates.md)
