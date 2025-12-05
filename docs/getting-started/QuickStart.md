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
dotnet add package AWSSDK.DynamoDBv2
```

> **Note:** The source generator and attributes are bundled in the main package. No additional packages are required for basic usage.

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
- `User.Fields` (or `UserFields`) - Type-safe field name constants
- `User.Keys` (or `UserKeys`) - Key builder methods for formatted keys
- `UserMapper` - Serialization/deserialization logic

### Key Builders

When you add prefixes to your keys, the generated key builders format them automatically:

```csharp
[DynamoDbTable("entities")]
public partial class User
{
    [PartitionKey(Prefix = "USER")]  // Adds "USER#" prefix
    [DynamoDbAttribute("pk")]
    public string UserId { get; set; } = string.Empty;
}

// User.Keys.Pk("123") returns "USER#123"
```

This ensures consistent key formatting across your application. See [Entity Definition](../core-features/EntityDefinition.md) for more on key prefixes.

> **Note:** You can also use string literals directly (e.g., `"pk"` instead of `User.Fields.UserId`). The generated constants provide compile-time validation but aren't required.

## Basic Operations

### Setup DynamoDB Client

```csharp
using Amazon.DynamoDBv2;
using Oproto.FluentDynamoDb;
using Oproto.FluentDynamoDb.Storage;

// Create DynamoDB client
var client = new AmazonDynamoDBClient();

// Use source-generated table class (recommended)
// Table name is configurable at runtime for different environments
var table = new UsersTable(client, "users");

// With configuration options (for logging, encryption, etc.)
var options = new FluentDynamoDbOptions();
var tableWithOptions = new UsersTable(client, "users", options);
```

### Entity Accessors

For multi-entity tables, the source generator creates entity-specific accessor properties:

```csharp
var ordersTable = new OrdersTable(client, "orders");

// Entity accessors eliminate generic type parameters
var order = await ordersTable.Orders.GetAsync("order123");
var lines = await ordersTable.OrderLines.Query()
    .Where(x => x.OrderId == "order123")
    .ToListAsync();

// Express-route methods for simple operations
await ordersTable.Orders.PutAsync(newOrder);
await ordersTable.Orders.DeleteAsync("order123");
```

> **Note**: This guide uses source-generated table classes. The table name is passed to the constructor, allowing you to use different table names per environment (e.g., "users-dev", "users-prod"). For advanced configuration options like logging, encryption, and geospatial support, see the [Configuration Guide](../core-features/Configuration.md).

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

// Express-route method (recommended for simple puts)
await table.Users.PutAsync(user);

// Builder pattern (for conditional puts, return values, etc.)
await table.Users.Put(user).PutAsync();

// Generic method (also available)
await table.Put<User>().WithItem(user).PutAsync();

Console.WriteLine("User created successfully!");
```

### Get (Retrieve Item)

```csharp
// Get item by partition key using generated key builder
var response = await table.Get<User>()
    .WithKey(User.Fields.UserId, "user123")
    .GetItemAsync();

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
var queryResponse = await table.Query<User>()
    .Where($"{User.Fields.UserId} = {{0}}", "user123")
    .WithFilter($"{User.Fields.Status} = {{0}}", "active")
    .ToListAsync();

foreach (var user in queryResponse.Items)
{
    Console.WriteLine($"Active user: {user.Name}");
}
```

**Expression Formatting Benefits:**
- `{0}`, `{1}` placeholders are automatically converted to DynamoDB parameter names
- Type-safe field references using generated `User.Fields` constants
- No manual parameter name management

### Update (Modify Item)

```csharp
// Update specific attributes using expression formatting
await table.Update<User>()
    .WithKey(User.Fields.UserId, "user123")
    .Set($"SET {User.Fields.Name} = {{0}}, {User.Fields.UpdatedAt} = {{1:o}}", 
         "Jane Doe", 
         DateTime.UtcNow)
    .UpdateAsync();

Console.WriteLine("User updated successfully!");
```

**Format Specifiers:**
- `{1:o}` formats DateTime in ISO 8601 format
- See [Expression Formatting](../core-features/ExpressionFormatting.md) for more format options

### Delete (Remove Item)

```csharp
// Delete item by key
await table.Delete<User>()
    .WithKey(User.Fields.UserId, "user123")
    .DeleteAsync();

Console.WriteLine("User deleted successfully!");
```

### Conditional Delete

```csharp
// Delete only if status is inactive
await table.Delete<User>()
    .WithKey(User.Fields.UserId, "user123")
    .Where($"{User.Fields.Status} = {{0}}", "inactive")
    .DeleteAsync();
```

## Complete Example

Here's a complete working example:

```csharp
using Amazon.DynamoDBv2;
using Oproto.FluentDynamoDb;
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
        
        // Create table instance - no options needed for basic usage
        var table = new UsersTable(client, "users");
        
        // Create user
        var user = new User
        {
            UserId = "user123",
            Email = "john@example.com",
            Name = "John Doe",
            Status = "active",
            CreatedAt = DateTime.UtcNow
        };
        
        await table.Users.PutAsync(user);
        
        // Retrieve user
        var getResponse = await table.Get<User>()
            .WithKey(User.Fields.UserId, "user123")
            .GetItemAsync();
        
        if (getResponse.IsSuccess)
        {
            Console.WriteLine($"User: {getResponse.Item.Name}");
        }
        
        // Update user
        await table.Update<User>()
            .WithKey(User.Fields.UserId, "user123")
            .Set($"SET {User.Fields.Name} = {{0}}", "Jane Doe")
            .UpdateAsync();
        
        // Query active users
        var queryResponse = await table.Query<User>()
            .Where($"{User.Fields.UserId} = {{0}}", "user123")
            .WithFilter($"{User.Fields.Status} = {{0}}", "active")
            .ToListAsync();
        
        Console.WriteLine($"Found {queryResponse.Items.Count} active users");
    }
}
```

## Adding Optional Features

For advanced scenarios, use `FluentDynamoDbOptions` to configure optional features:

```csharp
using Oproto.FluentDynamoDb;
using Oproto.FluentDynamoDb.Logging.Extensions;

// With logging
var options = new FluentDynamoDbOptions()
    .WithLogger(loggerFactory.CreateLogger<UsersTable>().ToDynamoDbLogger());

var table = new UsersTable(client, "users", options);
```

See the [Configuration Guide](../core-features/Configuration.md) for complete documentation on:
- Logging configuration
- Geospatial support
- Blob storage (S3)
- Field-level encryption (KMS)

## Next Steps

Now that you've completed the quick start, explore these topics:

- **[Installation Guide](Installation.md)** - Detailed setup and configuration
- **[First Entity](FirstEntity.md)** - Deep dive into entity definition and generated code
- **[Configuration Guide](../core-features/Configuration.md)** - Configure logging, encryption, and more
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
4. Check that `Oproto.FluentDynamoDb` package is installed (source generator is bundled)

See [Troubleshooting Guide](../reference/Troubleshooting.md) for more help.

### AWS Credentials

If you get authentication errors:

1. Configure AWS credentials using AWS CLI: `aws configure`
2. Or set environment variables: `AWS_ACCESS_KEY_ID`, `AWS_SECRET_ACCESS_KEY`
3. Or use IAM roles (recommended for production)

---

[Previous: Getting Started](README.md) | [Next: Installation](Installation.md)

**See Also:**
- [Configuration Guide](../core-features/Configuration.md)
- [Entity Definition](../core-features/EntityDefinition.md)
- [Expression Formatting](../core-features/ExpressionFormatting.md)
- [Attribute Reference](../reference/AttributeReference.md)
