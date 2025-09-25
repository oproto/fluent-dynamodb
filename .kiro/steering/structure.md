# Project Structure

## Solution Organization
The solution follows a standard .NET library structure with separate projects for the main library and unit tests.

```
Oproto.FluentDynamoDb.sln
├── Oproto.FluentDynamoDb/           # Main library project
├── Oproto.FluentDynamoDb.UnitTests/ # Unit test project
└── Solution Files/                   # README, LICENSE, compass.yml
```

## Main Library Structure (`Oproto.FluentDynamoDb/`)

### Core Folders
- **`Attributes/`**: Extension methods for AttributeValue handling
- **`Requests/`**: Request builder classes for DynamoDB operations
  - **`Interfaces/`**: Common interfaces for request builders
- **`Storage/`**: Table abstraction classes (`DynamoDbTableBase`, `DynamoDbIndex`)
- **`Streams/`**: DynamoDB stream processing utilities
- **`Pagination/`**: Pagination support classes and interfaces
- **`Utility/`**: General utility classes

### Request Builders
Each DynamoDB operation has its own fluent builder:
- `GetItemRequestBuilder`
- `PutItemRequestBuilder` 
- `QueryRequestBuilder`
- `UpdateItemRequestBuilder`
- `TransactWriteItemsRequestBuilder`
- `TransactGetItemsRequestBuilder`
- Individual transaction builders (`TransactPutBuilder`, `TransactDeleteBuilder`, etc.)

## Test Project Structure (`Oproto.FluentDynamoDb.UnitTests/`)

### Test Organization
Tests mirror the main library structure:
- **`Requests/`**: Tests for all request builders
- **`Storage/`**: Tests for table abstraction
- **`Streams/`**: Tests for stream processing
- **`Pagination/`**: Tests for pagination features

### Test Naming Convention
- Test classes: `{ClassName}Tests`
- Test methods: `{MethodName}{Scenario}` (e.g., `ForTableSuccess`)

## Architectural Patterns

### Fluent Interface Pattern
All request builders implement fluent interfaces allowing method chaining:
```csharp
table.Query
    .Where("pk = :pk")
    .WithValue(":pk", "value")
    .Take(10)
    .ExecuteAsync();
```

### Builder Pattern
Each operation uses the builder pattern with:
- Internal request object construction
- Method chaining for configuration
- Final `ExecuteAsync()` or `ToRequest()` methods

### Interface Segregation
Common functionality is extracted into focused interfaces:
- `IWithAttributes<T>`
- `IWithConditionExpression<T>`
- `IWithKey<T>`