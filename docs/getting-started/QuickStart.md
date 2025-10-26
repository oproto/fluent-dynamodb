---
title: "Quick Start Guide"
category: "getting-started"
order: 1
keywords: ["quickstart", "getting started", "installation", "first entity", "basic operations", "CRUD"]
related: ["Installation.md", "FirstEntity.md"]
---

[Documentation](../README.md) > [Getting Started](README.md) > Quick Start

# Quick Start Guide

[Next: Installation](Installation.md)

---

Get up and running with Oproto.FluentDynamoDb in 5 minutes. This guide shows you how to define your first entity using source generation and perform basic DynamoDB operations.

## Prerequisites

Before you begin, ensure you have:

- **.NET 8.0 SDK or later** installed
- **AWS credentials** configured (via AWS CLI, environment variables, or IAM role)
- **A DynamoDB table** created in AWS (or use DynamoDB Local for development)
- **Visual Studio 2022**, **JetBrains Rider**, or **VS Code** with C# extension

## Installation

Install the required NuGet packages:

```bash
dotnet add package Oproto.FluentDynamoDb
dotnet add package Oproto.FluentDynamoDb.SourceGenerator
dotnet add package Oproto.FluentDynamoDb.Attributes
dotnet add package AWSSDK.DynamoDBv2
```

For detailed installation instructions, see [Installation Guide](Installation.md).

## Define Your First Entity

Create a partial class decorated with DynamoDB attributes. The source generator will automatically create field constants, key builders, and mapping code.

```csharp
using Oproto.FluentDynamoDb.Attributes;

[DynamoDbTable("users")]
public partial class User
{
    // Partition key - uniquely identifies the user
    [PartitionKey]
    [DynamoDbAttribute("pk")]
    public string UserId { get; set; } = string.Empty;
    
    // User attributes
    [DynamoDbAttribute("email")]
    public string Email { get; set; } = string.Empty;
    
    [DynamoDbAttribute("name")]
    public string Name { get; set; } = string.Empty;
    
    [DynamoDbAttribute("status")]
    public string Status { get; set; } = "active";
    
    [DynamoDbAttribute("createdAt")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    
    [DynamoDbAttribute("updatedAt")]
    public DateTime? UpdatedAt { get; set; }
}
```

**Key Points:**
- The `partial` keyword is required for source generation
- `[DynamoDbTable]` specifies the DynamoDB table name
- `[PartitionKey]` marks the partition key property
- `[DynamoDbAttribute]` maps properties to DynamoDB attribute names

The source generator creates:
- `UserFields` - Type-safe field name constants
- `UserKeys` - Key builder methods
- `UserMapper` - Serialization/deserialization logic

## Basic Operations

### Setup DynamoDB Client

```csharp
using Amazon.DynamoDBv2;
using Oproto.FluentDynamoDb.Storage;

// Create DynamoDB client
var client = new AmazonDynamoDBClient();

// Option 1: Manual approach - create a class that inherits from DynamoDbTableBase
public class UsersTableManual : DynamoDbTableBase
{
    public UsersTableManual(IAmazonDynamoDB client, string tableName) 
        : base(client, tableName) { }
}
var table = new UsersTableManual(client, "users");

// Option 2: Use source-generated table class (recommended)
// Table name is configurable at runtime for different environments
var usersTable = new UsersTable(client, "users");

// For multi-entity tables, use entity accessors
var ordersTable = new OrdersTable(client, "orders");
// Access via: ordersTable.Orders.Get(), ordersTable.OrderLines.Query(), etc.
```

> **Note**: This guide uses source-generated table classes. For manual usage without source generation, create a class that inherits from `DynamoDbTableBase`. The table name is passed to the constructor, allowing you to use different table names per environment (e.g., "users-dev", "users-prod"). See [Single-Entity Tables](SingleEntityTables.md) and [Multi-Entity Tables](../advanced-topics/MultiEntityTables.md) for details.

### Put (Create/Update Item)

```csharp
var user = new User
{
    UserId = "user123",
    Email = "john.doe@example.com",
    Name = "John Doe",
    Status = "active",
    CreatedAt = DateTime.UtcNow
};

// Put item into DynamoDB
await table.Put
    .WithItem(user)
    .ExecuteAsync();

Console.WriteLine("User created successfully!");
```

### Get (Retrieve Item)

```csharp
// Get item by partition key using generated key builder
var response = await table.Get
    .WithKey(UserFields.UserId, UserKeys.Pk("user123"))
    .ExecuteAsync<User>();

if (response.IsSuccess)
{
    var user = response.Item;
    Console.WriteLine($"Found user: {user.Name} ({user.Email})");
}
else
{
    Console.WriteLine("User not found");
}
```

### Query (Find Items)

```csharp
// Query items with filter using expression formatting
var queryResponse = await table.Query
    .WithKey(UserFields.UserId, UserKeys.Pk("user123"))
    .Where($"{UserFields.Status} = {{0}}", "active")
    .ExecuteAsync<User>();

foreach (var user in queryResponse.Items)
{
    Console.WriteLine($"Active user: {user.Name}");
}
```

**Expression Formatting Benefits:**
- `{0}`, `{1}` placeholders are automatically converted to DynamoDB parameter names
- Type-safe field references using generated `UserFields` constants
- No manual parameter name management

### Update (Modify Item)

```csharp
// Update specific attributes using expression formatting
await table.Update
    .WithKey(UserFields.UserId, UserKeys.Pk("user123"))
    .Set($"SET {UserFields.Name} = {{0}}, {UserFields.UpdatedAt} = {{1:o}}", 
         "Jane Doe", 
         DateTime.UtcNow)
    .ExecuteAsync();

Console.WriteLine("User updated successfully!");
```

**Format Specifiers:**
- `{1:o}` formats DateTime in ISO 8601 format
- See [Expression Formatting](../core-features/ExpressionFormatting.md) for more format options

### Delete (Remove Item)

```csharp
// Delete item by key
await table.Delete
    .WithKey(UserFields.UserId, UserKeys.Pk("user123"))
    .ExecuteAsync();

Console.WriteLine("User deleted successfully!");
```

### Conditional Delete

```csharp
// Delete only if status is inactive
await table.Delete
    .WithKey(UserFields.UserId, UserKeys.Pk("user123"))
    .Where($"{UserFields.Status} = {{0}}", "inactive")
    .ExecuteAsync();
```

## Complete Example

Here's a complete working example:

```csharp
using Amazon.DynamoDBv2;
using Oproto.FluentDynamoDb.Attributes;
using Oproto.FluentDynamoDb.Storage;

// Entity definition
[DynamoDbTable("users")]
public partial class User
{
    [PartitionKey]
    [DynamoDbAttribute("pk")]
    public string UserId { get; set; } = string.Empty;
    
    [DynamoDbAttribute("email")]
    public string Email { get; set; } = string.Empty;
    
    [DynamoDbAttribute("name")]
    public string Name { get; set; } = string.Empty;
    
    [DynamoDbAttribute("status")]
    public string Status { get; set; } = "active";
    
    [DynamoDbAttribute("createdAt")]
    public DateTime CreatedAt { get; set; }
}

// Usage
class Program
{
    static async Task Main(string[] args)
    {
        var client = new AmazonDynamoDBClient();
        var table = new DynamoDbTableBase(client, "users");
        
        // Create user
        var user = new User
        {
            UserId = "user123",
            Email = "john@example.com",
            Name = "John Doe",
            Status = "active",
            CreatedAt = DateTime.UtcNow
        };
        
        await table.Put.WithItem(user).ExecuteAsync();
        
        // Retrieve user
        var getResponse = await table.Get
            .WithKey(UserFields.UserId, UserKeys.Pk("user123"))
            .ExecuteAsync<User>();
        
        if (getResponse.IsSuccess)
        {
            Console.WriteLine($"User: {getResponse.Item.Name}");
        }
        
        // Update user
        await table.Update
            .WithKey(UserFields.UserId, UserKeys.Pk("user123"))
            .Set($"SET {UserFields.Name} = {{0}}", "Jane Doe")
            .ExecuteAsync();
        
        // Query active users
        var queryResponse = await table.Query
            .WithKey(UserFields.UserId, UserKeys.Pk("user123"))
            .Where($"{UserFields.Status} = {{0}}", "active")
            .ExecuteAsync<User>();
        
        Console.WriteLine($"Found {queryResponse.Items.Count} active users");
    }
}
```

## Next Steps

Now that you've completed the quick start, explore these topics:

- **[Installation Guide](Installation.md)** - Detailed setup and configuration
- **[First Entity](FirstEntity.md)** - Deep dive into entity definition and generated code
- **[Entity Definition](../core-features/EntityDefinition.md)** - Advanced entity patterns (composite keys, GSIs)
- **[Basic Operations](../core-features/BasicOperations.md)** - Complete CRUD operation reference
- **[Querying Data](../core-features/QueryingData.md)** - Advanced query patterns and optimization
- **[Expression Formatting](../core-features/ExpressionFormatting.md)** - Complete format specifier reference

## Troubleshooting

### Source Generator Not Working

If the generated code (UserFields, UserKeys, UserMapper) is not available:

1. Ensure your class is marked as `partial`
2. Rebuild the project (`dotnet build`)
3. Restart your IDE
4. Check that `Oproto.FluentDynamoDb.SourceGenerator` package is installed

See [Troubleshooting Guide](../reference/Troubleshooting.md) for more help.

### AWS Credentials

If you get authentication errors:

1. Configure AWS credentials using AWS CLI: `aws configure`
2. Or set environment variables: `AWS_ACCESS_KEY_ID`, `AWS_SECRET_ACCESS_KEY`
3. Or use IAM roles (recommended for production)

---

[Previous: Getting Started](README.md) | [Next: Installation](Installation.md)

**See Also:**
- [Entity Definition](../core-features/EntityDefinition.md)
- [Expression Formatting](../core-features/ExpressionFormatting.md)
- [Attribute Reference](../reference/AttributeReference.md)
