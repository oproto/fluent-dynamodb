---
title: "Table Creation"
category: "advanced-topics"
order: 65
keywords: ["table", "creation", "provisioning", "integration testing", "DynamoDB Local", "GSI", "LSI", "TTL"]
---

[Documentation](../README.md) > [Advanced Topics](README.md) > Table Creation

# Table Creation

Create DynamoDB tables programmatically from entity metadata for integration testing and dynamic table scenarios.

## Overview

Table creation allows you to create DynamoDB tables that match your entity definitions without manual CloudFormation or Terraform setup. This feature complements [Schema Validation](SchemaValidation.md) by providing the inverse operation: while `ValidateSchemaAsync` verifies an existing table matches your entity, `CreateTableAsync` creates a new table from your entity metadata.

### Primary Use Cases

- **Integration Testing**: Create tables matching entity definitions for automated tests with DynamoDB Local
- **Time-Series Tables**: Dynamically create tables at runtime (e.g., monthly partitioned tables)
- **Development Environments**: Quickly spin up tables for local development

> **Note**: For production deployments, infrastructure-as-code tools (CloudFormation, Terraform, CDK) are recommended for better change management and rollback capabilities.

### Key Features

- **Primary Key Configuration**: Automatically configures partition key and optional sort key
- **GSI Support**: Creates all Global Secondary Indexes defined in entity metadata
- **LSI Support**: Creates all Local Secondary Indexes defined in entity metadata
- **TTL Configuration**: Optionally enables Time-To-Live on the created table
- **Billing Mode**: Supports both PAY_PER_REQUEST (default) and PROVISIONED billing modes
- **Wait for Active**: Optionally waits for the table to become ACTIVE before returning

## Table of Contents

- [Basic Usage](#basic-usage)
- [TableCreationOptions](#tablecreationoptions)
- [TableCreationResult](#tablecreationresult)
- [Generated Static Method](#generated-static-method)
- [Integration Testing Example](#integration-testing-example)
- [Error Handling](#error-handling)
- [Best Practices](#best-practices)

---

## Basic Usage

### Using TableCreator Directly

The `TableCreator` class provides the core functionality for creating tables:

```csharp
using Amazon.DynamoDBv2;
using Oproto.FluentDynamoDb.Provisioning;

var client = new AmazonDynamoDBClient();
var creator = new TableCreator();

// Create table from entity metadata
var result = await creator.CreateAsync(
    client,
    "my-users-table",
    User.GetEntityMetadata());

Console.WriteLine($"Created table: {result.TableName}");
Console.WriteLine($"Status: {result.TableStatus}");
Console.WriteLine($"ARN: {result.TableArn}");
```

### Inspecting the Request

Use `BuildCreateTableRequest` to inspect the request before execution:

```csharp
var creator = new TableCreator();
var request = creator.BuildCreateTableRequest(
    "my-users-table",
    User.GetEntityMetadata());

// Inspect the request
Console.WriteLine($"Table: {request.TableName}");
Console.WriteLine($"Key Schema: {string.Join(", ", request.KeySchema.Select(k => k.AttributeName))}");
Console.WriteLine($"GSIs: {request.GlobalSecondaryIndexes?.Count ?? 0}");
Console.WriteLine($"LSIs: {request.LocalSecondaryIndexes?.Count ?? 0}");

// Execute manually if needed
await client.CreateTableAsync(request);
```

---

## TableCreationOptions

Configure table creation behavior with `TableCreationOptions`:

```csharp
var options = new TableCreationOptions
{
    // Billing mode (default: PAY_PER_REQUEST)
    BillingMode = BillingMode.PAY_PER_REQUEST,
    
    // Enable TTL if entity defines a TTL attribute (default: false)
    EnableTtl = true,
    
    // Wait for table to become ACTIVE (default: true)
    WaitForActive = true,
    
    // Timeout for waiting (default: 60 seconds)
    WaitTimeout = TimeSpan.FromSeconds(120),
    
    // Polling interval when waiting (default: 1 second)
    PollingInterval = TimeSpan.FromMilliseconds(500)
};

var result = await creator.CreateAsync(client, "my-table", metadata, options);
```

### Provisioned Throughput

For PROVISIONED billing mode, specify capacity units:

```csharp
var options = new TableCreationOptions
{
    BillingMode = BillingMode.PROVISIONED,
    ProvisionedThroughput = new ProvisionedThroughputConfig
    {
        ReadCapacityUnits = 10,
        WriteCapacityUnits = 5
    },
    // Optional: Different throughput for GSIs
    GsiProvisionedThroughput = new ProvisionedThroughputConfig
    {
        ReadCapacityUnits = 5,
        WriteCapacityUnits = 2
    }
};
```

### Options Reference

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `BillingMode` | `BillingMode` | `PAY_PER_REQUEST` | Table billing mode |
| `ProvisionedThroughput` | `ProvisionedThroughputConfig?` | `null` | Table throughput (PROVISIONED mode only) |
| `GsiProvisionedThroughput` | `ProvisionedThroughputConfig?` | `null` | GSI throughput (uses table throughput if not specified) |
| `EnableTtl` | `bool` | `false` | Enable TTL if entity defines TTL attribute |
| `WaitForActive` | `bool` | `true` | Wait for table to become ACTIVE |
| `WaitTimeout` | `TimeSpan` | `60 seconds` | Maximum time to wait for ACTIVE status |
| `PollingInterval` | `TimeSpan` | `1 second` | Interval between status checks |

---

## TableCreationResult

The `CreateAsync` method returns a `TableCreationResult` with information about the created table:

```csharp
var result = await creator.CreateAsync(client, "my-table", metadata, options);

// Table information
Console.WriteLine($"Table Name: {result.TableName}");
Console.WriteLine($"Table ARN: {result.TableArn}");
Console.WriteLine($"Status: {result.TableStatus}");
Console.WriteLine($"TTL Enabled: {result.TtlEnabled}");
```

### Result Properties

| Property | Type | Description |
|----------|------|-------------|
| `TableName` | `string` | Name of the created table |
| `TableArn` | `string` | ARN of the created table |
| `TableStatus` | `TableStatus` | Current status (ACTIVE if WaitForActive was true) |
| `TtlEnabled` | `bool` | Whether TTL was enabled on the table |

---

## Generated Static Method

The source generator creates a static `CreateTableAsync` method on generated table classes:

```csharp
// Generated method signature
public static async Task<TableCreationResult> CreateTableAsync(
    IAmazonDynamoDB client,
    string tableName,
    TableCreationOptions? options = null,
    CancellationToken cancellationToken = default)
```

### Usage

```csharp
// Create table using generated static method
var result = await UsersTable.CreateTableAsync(
    client,
    "test-users-table");

// With options
var result = await UsersTable.CreateTableAsync(
    client,
    "test-users-table",
    new TableCreationOptions { EnableTtl = true });
```

> **Note**: The table name is always required because the `[DynamoDbTable]` attribute's table name is used for connecting multiple entities to a single generated table class, not for actual table naming at runtime.

---

## Integration Testing Example

A complete example for integration testing with DynamoDB Local:

```csharp
[Collection("DynamoDB Local")]
public class UserRepositoryTests : IAsyncLifetime
{
    private readonly IAmazonDynamoDB _client;
    private readonly string _tableName;
    private UsersTable _table = null!;
    
    public UserRepositoryTests()
    {
        // Connect to DynamoDB Local
        _client = new AmazonDynamoDBClient(new AmazonDynamoDBConfig
        {
            ServiceURL = "http://localhost:8000"
        });
        
        // Generate unique table name for test isolation
        _tableName = $"users_test_{Guid.NewGuid():N}";
    }
    
    public async Task InitializeAsync()
    {
        // Create table from entity metadata
        var result = await UsersTable.CreateTableAsync(
            _client,
            _tableName,
            new TableCreationOptions
            {
                EnableTtl = true,
                WaitForActive = true
            });
        
        // Initialize table instance
        _table = new UsersTable(_client, _tableName);
    }
    
    public async Task DisposeAsync()
    {
        // Clean up table after tests
        try
        {
            await _client.DeleteTableAsync(_tableName);
        }
        catch (ResourceNotFoundException)
        {
            // Table already deleted
        }
    }
    
    [Fact]
    public async Task CreateUser_StoresUserCorrectly()
    {
        // Arrange
        var user = new User
        {
            UserId = "user-123",
            Name = "John Doe",
            Email = "[email]"
        };
        
        // Act
        await _table.Users.PutAsync(user);
        var retrieved = await _table.Users.GetAsync("user-123");
        
        // Assert
        retrieved.Should().NotBeNull();
        retrieved!.Name.Should().Be("John Doe");
    }
    
    [Fact]
    public async Task QueryByStatus_ReturnsMatchingUsers()
    {
        // Arrange - seed test data
        await _table.Users.PutAsync(new User { UserId = "u1", Status = "active" });
        await _table.Users.PutAsync(new User { UserId = "u2", Status = "active" });
        await _table.Users.PutAsync(new User { UserId = "u3", Status = "inactive" });
        
        // Act - query GSI
        var activeUsers = await _table.StatusIndex.Query<User>()
            .Where(x => x.Status == "active")
            .ToListAsync();
        
        // Assert
        activeUsers.Should().HaveCount(2);
    }
}
```

### Test Fixture for Shared Setup

For multiple test classes sharing the same table:

```csharp
public class DynamoDbLocalFixture : IAsyncLifetime
{
    public IAmazonDynamoDB Client { get; private set; } = null!;
    public string UsersTableName { get; private set; } = null!;
    public string OrdersTableName { get; private set; } = null!;
    
    public async Task InitializeAsync()
    {
        Client = new AmazonDynamoDBClient(new AmazonDynamoDBConfig
        {
            ServiceURL = "http://localhost:8000"
        });
        
        // Create tables for all entities
        UsersTableName = $"users_{Guid.NewGuid():N}";
        await UsersTable.CreateTableAsync(Client, UsersTableName);
        
        OrdersTableName = $"orders_{Guid.NewGuid():N}";
        await OrdersTable.CreateTableAsync(Client, OrdersTableName);
    }
    
    public async Task DisposeAsync()
    {
        await Client.DeleteTableAsync(UsersTableName);
        await Client.DeleteTableAsync(OrdersTableName);
    }
}

[CollectionDefinition("DynamoDB Local")]
public class DynamoDbLocalCollection : ICollectionFixture<DynamoDbLocalFixture> { }
```

---

## Error Handling

### Common Exceptions

| Exception | Cause | Resolution |
|-----------|-------|------------|
| `ArgumentException` | Invalid table name or metadata | Ensure table name is not empty and metadata has partition key |
| `ResourceInUseException` | Table already exists | Use a unique table name or delete existing table first |
| `TimeoutException` | Table didn't become ACTIVE in time | Increase `WaitTimeout` or check DynamoDB service status |
| `LimitExceededException` | Too many tables or indexes | Check AWS account limits |

### Example Error Handling

```csharp
try
{
    var result = await creator.CreateAsync(client, tableName, metadata, options);
    Console.WriteLine($"Table created: {result.TableName}");
}
catch (ArgumentException ex)
{
    Console.WriteLine($"Invalid configuration: {ex.Message}");
}
catch (ResourceInUseException)
{
    Console.WriteLine($"Table '{tableName}' already exists");
}
catch (TimeoutException ex)
{
    Console.WriteLine($"Table creation timed out: {ex.Message}");
}
catch (AmazonDynamoDBException ex)
{
    Console.WriteLine($"DynamoDB error: {ex.Message}");
}
```

---

## Best Practices

### 1. Use Unique Table Names in Tests

Generate unique table names to enable parallel test execution:

```csharp
var tableName = $"users_test_{Guid.NewGuid():N}";
```

### 2. Always Clean Up Test Tables

Delete tables after tests to avoid resource accumulation:

```csharp
public async Task DisposeAsync()
{
    try
    {
        await _client.DeleteTableAsync(_tableName);
    }
    catch (ResourceNotFoundException) { }
}
```

### 3. Use PAY_PER_REQUEST for Testing

On-demand billing is simpler for testing and avoids throughput configuration:

```csharp
// Default is PAY_PER_REQUEST - no configuration needed
var result = await creator.CreateAsync(client, tableName, metadata);
```

### 4. Wait for Active in Tests

Always wait for the table to become ACTIVE before running tests:

```csharp
var options = new TableCreationOptions
{
    WaitForActive = true,  // Default
    WaitTimeout = TimeSpan.FromSeconds(60)
};
```

### 5. Use Schema Validation for Production

For production tables, use `ValidateSchemaAsync` instead of creating tables:

```csharp
// Production: Validate existing table
var result = await UsersTable.ValidateSchemaAsync(client);
result.ThrowOnError();

// Testing: Create table
await UsersTable.CreateTableAsync(client, testTableName);
```

---

## See Also

- **[Schema Validation](SchemaValidation.md)** - Validate existing tables against entity metadata
- **[Entity Definition](../core-features/EntityDefinition.md)** - Defining DynamoDB entities
- **[Global Secondary Indexes](GlobalSecondaryIndexes.md)** - GSI configuration and usage
- **[Attribute Reference](../reference/AttributeReference.md)** - Complete attribute documentation

---

[Back to Advanced Topics](README.md) | [Back to Documentation Home](../README.md)
