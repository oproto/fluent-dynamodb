# Integration Test Infrastructure

This folder contains the core infrastructure for integration tests that run against DynamoDB Local.

## Components

### DynamoDbLocalFixture

Manages the lifecycle of DynamoDB Local for integration tests. This fixture:
- Automatically downloads and starts DynamoDB Local if not already running
- Reuses an existing DynamoDB Local instance if available
- Provides a configured `IAmazonDynamoDB` client for tests
- Cleans up resources when tests complete

### DynamoDbLocalCollection

xUnit collection fixture that shares a single DynamoDB Local instance across all test classes. This improves test performance by avoiding repeated startup/shutdown of DynamoDB Local.

### IntegrationTestBase

Abstract base class for integration tests that provides:

#### Features

- **Automatic Table Management**: Creates and deletes DynamoDB tables automatically
- **Unique Table Names**: Each test class instance gets a unique table name to support parallel execution
- **Helper Methods**: Common operations like `SaveAndLoadAsync` for round-trip testing
- **Cleanup**: Automatically deletes all tables created during tests

#### Usage

```csharp
[Collection("DynamoDB Local")]
public class MyIntegrationTests : IntegrationTestBase
{
    public MyIntegrationTests(DynamoDbLocalFixture fixture) 
        : base(fixture)
    {
    }
    
    public override async Task InitializeAsync()
    {
        // Create table for your entity type
        await CreateTableAsync<MyEntity>();
    }
    
    [Fact]
    public async Task MyTest()
    {
        // Arrange
        var entity = new MyEntity { Id = "test-1", Name = "Test" };
        
        // Act - Save and load entity
        var loaded = await SaveAndLoadAsync(entity);
        
        // Assert
        loaded.Should().BeEquivalentTo(entity);
    }
}
```

#### Protected Members

- `DynamoDb`: The DynamoDB client connected to DynamoDB Local
- `TableName`: Unique table name for this test class instance
- `CreateTableAsync<TEntity>()`: Creates a table based on entity metadata
- `WaitForTableActiveAsync(tableName)`: Waits for a table to become active
- `SaveAndLoadAsync<TEntity>(entity)`: Saves an entity and loads it back for verification

## Running Tests

### Prerequisites

- .NET 8 SDK
- Java 17+ (for DynamoDB Local)

### Local Execution

```bash
# Run all integration tests
dotnet test Oproto.FluentDynamoDb.IntegrationTests

# Run specific test class
dotnet test --filter "FullyQualifiedName~MyIntegrationTests"
```

### CI/CD

The integration tests are designed to run in CI/CD environments. The fixture will automatically download and start DynamoDB Local if not present.

## Design Decisions

### Unique Table Names

Each test class instance generates a unique table name using a GUID. This allows:
- Tests to run in parallel without conflicts
- Multiple test runs to execute simultaneously
- Easier debugging by including the test class name in the table name

### Automatic Cleanup

Tables are automatically deleted in `DisposeAsync`. Cleanup failures are logged but don't fail the test, preventing cascading failures.

### Entity Metadata

The base class uses the `IDynamoDbEntity.GetEntityMetadata()` method to automatically create tables with the correct schema based on entity attributes.
