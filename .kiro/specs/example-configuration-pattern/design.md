# Design Document: Example Configuration Pattern Refactoring

## Overview

This design document describes the refactoring of the four example projects (TodoList, TransactionDemo, InvoiceManager, and StoreLocator) to follow the recommended configuration pattern for FluentDynamoDb. The current pattern has table classes with custom constructors that build configuration inline and hardcode table names. The new pattern builds configuration once at the application level and passes both the options and table name to table constructors.

## Architecture

### Current Pattern (Anti-Pattern)

```csharp
// Table class builds configuration inline
public partial class StoresS2Table : DynamoDbTableBase
{
    public const string TableName = "stores-s2";

    public StoresS2Table(IAmazonDynamoDB client) 
        : this(client, TableName, new FluentDynamoDbOptions().AddGeospatial())
    {
    }
}

// Program.cs - simple instantiation
var table = new StoresS2Table(client);
```

### New Pattern (Recommended)

```csharp
// Program.cs - configuration at application level
var options = new FluentDynamoDbOptions().AddGeospatial();
var table = new StoresS2Table(client, StoresS2Table.TableName, options);

// Table class - minimal or removed entirely
// If table class has utility methods, keep only those:
public partial class StoresS2Table : DynamoDbTableBase
{
    public const string TableName = "stores-s2";

    public static int SelectS2Level(double radiusKilometers) => radiusKilometers switch
    {
        <= 2.0 => 14,
        <= 10.0 => 12,
        _ => 10
    };
}
```

### Decision: Table Class Retention

| Project | Table Class | Has Utility Methods | Action |
|---------|-------------|---------------------|--------|
| TodoList | TodoItemsTable.cs | No | Remove - rely on source-generated code |
| TransactionDemo | TransactionDemoTable.cs | No | Remove - rely on source-generated code |
| InvoiceManager | InvoicesTable.cs | No | Remove - rely on source-generated code |
| StoreLocator | StoresGeohashTable.cs | No | Remove - rely on source-generated code |
| StoreLocator | StoresS2Table.cs | Yes (`SelectS2Level`) | Keep - remove constructor, keep utility method |
| StoreLocator | StoresH3Table.cs | Yes (`SelectH3Resolution`) | Keep - remove constructor, keep utility method |

## Components and Interfaces

### DynamoDbTableBase Constructor

The base class already supports the recommended pattern:

```csharp
public DynamoDbTableBase(IAmazonDynamoDB client, string tableName, FluentDynamoDbOptions? options)
```

### Source-Generated Table Class

The source generator creates a partial class with a constructor that accepts client, tableName, and options:

```csharp
// Generated code (simplified)
public partial class TodoItemsTable : DynamoDbTableBase
{
    public TodoItemsTable(IAmazonDynamoDB client, string tableName, FluentDynamoDbOptions? options = null)
        : base(client, tableName, options)
    {
    }
    
    // Generated entity accessors
    public EntityAccessor<TodoItem> TodoItems => ...;
}
```

### Application-Level Configuration

Each Program.cs will be updated to:

1. Build `FluentDynamoDbOptions` once (if needed)
2. Pass client, table name, and options to table constructor
3. Use the table name constant for both table creation and instantiation

## Data Models

No changes to data models. Entity definitions remain unchanged.

## Correctness Properties

*A property is a characteristic or behavior that should hold true across all valid executions of a system-essentially, a formal statement about what the system should do. Properties serve as the bridge between human-readable specifications and machine-verifiable correctness guarantees.*

This refactoring is primarily a code organization and documentation change. All acceptance criteria relate to code structure, API design, and documentation content rather than runtime behavior. As such, there are no testable correctness properties that can be verified through property-based testing.

The correctness of this refactoring is verified through:
1. **Compilation** - The code must compile successfully after changes
2. **Manual Testing** - The example applications must run correctly
3. **Code Review** - The pattern must match the documented best practices

## Error Handling

No changes to error handling. The refactoring does not affect runtime behavior.

## Testing Strategy

### Verification Approach

Since this is a refactoring of example code with no new runtime behavior:

1. **Build Verification**: All four example projects must compile successfully
2. **Runtime Verification**: Each example application should be manually tested to ensure it still functions correctly
3. **Documentation Review**: README files should accurately reflect the new pattern

### Build Commands

```bash
# Build all example projects
dotnet build examples/TodoList/TodoList.csproj
dotnet build examples/TransactionDemo/TransactionDemo.csproj
dotnet build examples/InvoiceManager/InvoiceManager.csproj
dotnet build examples/StoreLocator/StoreLocator.csproj
```

### No Property-Based Tests Required

This refactoring involves:
- Removing custom constructors from table classes
- Moving configuration to Program.cs
- Updating documentation

None of these changes introduce new runtime behavior that would benefit from property-based testing.
