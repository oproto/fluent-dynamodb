---
title: "First Entity Guide"
category: "getting-started"
order: 3
keywords: ["entity", "attributes", "partial class", "DynamoDbTable", "PartitionKey", "source generation"]
related: ["Installation.md", "QuickStart.md"]
---

[Documentation](../README.md) > [Getting Started](README.md) > First Entity

# First Entity Guide

[Previous: Installation](Installation.md)

---

This guide provides a deep dive into creating your first DynamoDB entity with Oproto.FluentDynamoDb, explaining how source generation works and what code gets generated for you.

## Entity Class Requirements

### The Partial Keyword

Your entity class **must** be marked as `partial` to enable source generation:

```csharp
// ✅ Correct - partial keyword allows source generator to extend the class
public partial class User
{
    // ...
}

// ❌ Wrong - source generator cannot extend non-partial classes
public class User
{
    // ...
}
```

**Why partial?** The source generator creates additional code in a separate file that extends your class. The `partial` keyword allows the compiler to merge both parts into a single class.

### Namespace and Accessibility

```csharp
namespace MyApp.Models;

// Can be public, internal, or private
public partial class User
{
    // ...
}
```

The generated code will match your class's accessibility level and namespace.

## Property Mapping with [DynamoDbAttribute]

Map C# properties to DynamoDB attribute names using the `[DynamoDbAttribute]` attribute:

```csharp
using Oproto.FluentDynamoDb.Attributes;

[DynamoDbTable("users")]
public partial class User
{
    // Maps to DynamoDB attribute "pk"
    [DynamoDbAttribute("pk")]
    public string UserId { get; set; } = string.Empty;
    
    // Maps to DynamoDB attribute "email"
    [DynamoDbAttribute("email")]
    public string Email { get; set; } = string.Empty;
    
    // Maps to DynamoDB attribute "full_name"
    [DynamoDbAttribute("full_name")]
    public string FullName { get; set; } = string.Empty;
}
```

### Attribute Naming Conventions

**Recommended Patterns:**

```csharp
// Pattern 1: Short, generic names (recommended for single-table design)
[DynamoDbAttribute("pk")]  // Partition key
[DynamoDbAttribute("sk")]  // Sort key
[DynamoDbAttribute("gsi1pk")]  // GSI partition key

// Pattern 2: Descriptive names (recommended for dedicated tables)
[DynamoDbAttribute("userId")]
[DynamoDbAttribute("email")]
[DynamoDbAttribute("createdAt")]

// Pattern 3: Snake case (common in some organizations)
[DynamoDbAttribute("user_id")]
[DynamoDbAttribute("created_at")]
```

**Best Practice:** Choose a naming convention and stick with it across your project.

### Supported Property Types

The source generator supports standard .NET types:

```csharp
[DynamoDbTable("examples")]
public partial class TypeExamples
{
    [PartitionKey]
    [DynamoDbAttribute("pk")]
    public string Id { get; set; } = string.Empty;
    
    // Strings
    [DynamoDbAttribute("name")]
    public string Name { get; set; } = string.Empty;
    
    // Numbers
    [DynamoDbAttribute("age")]
    public int Age { get; set; }
    
    [DynamoDbAttribute("price")]
    public decimal Price { get; set; }
    
    [DynamoDbAttribute("score")]
    public double Score { get; set; }
    
    // Booleans
    [DynamoDbAttribute("isActive")]
    public bool IsActive { get; set; }
    
    // DateTime
    [DynamoDbAttribute("createdAt")]
    public DateTime CreatedAt { get; set; }
    
    // Nullable types
    [DynamoDbAttribute("updatedAt")]
    public DateTime? UpdatedAt { get; set; }
    
    // Collections
    [DynamoDbAttribute("tags")]
    public List<string> Tags { get; set; } = new();
    
    [DynamoDbAttribute("metadata")]
    public Dictionary<string, string> Metadata { get; set; } = new();
}
```

## Partition and Sort Keys

### Partition Key (Required)

Every entity must have exactly one partition key:

```csharp
[DynamoDbTable("users")]
public partial class User
{
    [PartitionKey]
    [DynamoDbAttribute("pk")]
    public string UserId { get; set; } = string.Empty;
}
```

The `[PartitionKey]` attribute tells the source generator which property is the partition key.

### Sort Key (Optional)

Add a sort key for composite primary keys:

```csharp
[DynamoDbTable("orders")]
public partial class Order
{
    [PartitionKey]
    [DynamoDbAttribute("pk")]
    public string CustomerId { get; set; } = string.Empty;
    
    [SortKey]
    [DynamoDbAttribute("sk")]
    public string OrderId { get; set; } = string.Empty;
    
    [DynamoDbAttribute("total")]
    public decimal Total { get; set; }
}
```

**Use Cases for Sort Keys:**
- One-to-many relationships (customer → orders)
- Time-series data (sensor → readings by timestamp)
- Hierarchical data (folder → files)
- Multi-item entities (order → order items)

## Generated Code Overview

When you build your project, the source generator creates nested support classes within your entity:

### 1. Fields Class (Type-Safe Field Names)

```csharp
// Generated: User.g.cs (nested within User class)
public partial class User
{
    public static partial class Fields
    {
        public const string UserId = "pk";
        public const string Email = "email";
        public const string FullName = "full_name";
    }
}
```

**Usage:**
```csharp
// Access through entity class
await table.Query<User>()
    .Where($"{User.Fields.UserId} = {{0}}", "user123")
    .WithFilter($"{User.Fields.Email} = {{0}}", "john@example.com")
    .ExecuteAsync();
```

**Benefits:**
- Compile-time safety (typos caught at build time)
- IntelliSense support
- Refactoring support (rename property → field name updates automatically)
- No namespace pollution - Fields class is nested within entity

> **Tip:** You can also use string literals directly (e.g., `"pk"` instead of `User.Fields.UserId`). The generated constants provide compile-time validation but aren't required. Use whichever approach is cleaner for your use case.

### 2. Keys Class (Key Builder Methods)

```csharp
// Generated: User.g.cs (nested within User class)
public partial class User
{
    public static partial class Keys
    {
        public static string Pk(string userId)
        {
            return $"USER#{userId}";
        }
    }
}
```

**Usage:**
```csharp
// Build partition key value through entity class
var key = User.Keys.Pk("user123");  // Returns "USER#user123"

// Use in operations
await table.Get<User>()
    .WithKey(User.Fields.UserId, User.Keys.Pk("user123"))
    .ExecuteAsync();
```

**Benefits:**
- Consistent key formatting
- Prevents key format errors
- Supports composite key patterns
- Clear relationship between entity and its keys

> **Note:** The `Keys.Pk()` method formats keys based on your entity's `[Computed]` or `[PartitionKey(Prefix = "...")]` attributes. If no prefix is configured, it returns the value as-is. You can also pass raw values directly to `WithKey()` if you don't need key formatting.

### 3. Mapper Class (Serialization/Deserialization)

```csharp
// Generated: UserMapper.g.cs
public static class UserMapper
{
    public static Dictionary<string, AttributeValue> ToAttributeMap(User entity)
    {
        // Converts User object to DynamoDB attribute map
    }
    
    public static User FromAttributeMap(Dictionary<string, AttributeValue> attributes)
    {
        // Converts DynamoDB attribute map to User object
    }
}
```

**Usage:**
```csharp
// Typically used internally by the library
// You rarely need to call these directly
var attributeMap = UserMapper.ToAttributeMap(user);
var user = UserMapper.FromAttributeMap(attributeMap);
```

**Benefits:**
- Zero-boilerplate serialization
- Type-safe conversions
- Optimized performance

## Common Patterns

### Pattern 1: Simple Entity (Partition Key Only)

```csharp
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
}

// Usage
await table.Get<User>()
    .WithKey(User.Fields.UserId, "user123")
    .ExecuteAsync();
```

### Pattern 2: Composite Key Entity

```csharp
[DynamoDbTable("orders")]
public partial class Order
{
    [PartitionKey]
    [DynamoDbAttribute("pk")]
    public string CustomerId { get; set; } = string.Empty;
    
    [SortKey]
    [DynamoDbAttribute("sk")]
    public string OrderId { get; set; } = string.Empty;
    
    [DynamoDbAttribute("total")]
    public decimal Total { get; set; }
    
    [DynamoDbAttribute("status")]
    public string Status { get; set; } = "pending";
}

// Usage - requires both partition and sort key
await table.Get<Order>()
    .WithKey(Order.Fields.CustomerId, "customer123", Order.Fields.OrderId, "order456")
    .ExecuteAsync();
```

### Pattern 3: Computed Keys (Format Strings)

Use `[Computed]` attribute for keys with dynamic formatting:

```csharp
[DynamoDbTable("products")]
public partial class Product
{
    [PartitionKey]
    [Computed("PRODUCT#{ProductId}")]
    [DynamoDbAttribute("pk")]
    public string ProductId { get; set; } = string.Empty;
    
    [SortKey]
    [Computed("v{Version:D3}")]  // Formats as v001, v002, etc.
    [DynamoDbAttribute("sk")]
    public int Version { get; set; }
    
    [DynamoDbAttribute("name")]
    public string Name { get; set; } = string.Empty;
}

// Generated key builder
// ProductKeys.Pk("abc123") returns "PRODUCT#abc123"
// ProductKeys.Sk(5) returns "v005"
```

**Format Specifiers:**
- `{PropertyName}` - Simple substitution
- `{PropertyName:D3}` - Numeric formatting (3 digits, zero-padded)
- `{PropertyName:o}` - DateTime ISO 8601 format
- See [Expression Formatting](../core-features/ExpressionFormatting.md) for more options

### Pattern 4: Extracted Keys (Composite Patterns)

Use `[Extracted]` for keys derived from other properties:

```csharp
[DynamoDbTable("events")]
public partial class Event
{
    [PartitionKey]
    [Extracted("Date", "yyyy-MM-dd")]
    [DynamoDbAttribute("pk")]
    public string DateKey { get; set; } = string.Empty;
    
    [DynamoDbAttribute("date")]
    public DateTime Date { get; set; }
    
    [DynamoDbAttribute("eventName")]
    public string EventName { get; set; } = string.Empty;
}

// The DateKey is automatically populated from Date property
// Date = 2024-03-15 → DateKey = "2024-03-15"
```

## Viewing Generated Code

### Visual Studio 2022

1. Right-click on your project in Solution Explorer
2. Select **Analyze and Code Cleanup** → **View Generated Files**
3. Expand **Oproto.FluentDynamoDb.SourceGenerator**
4. View the generated `.g.cs` files

### JetBrains Rider

1. In Solution Explorer, expand **Dependencies**
2. Expand **Analyzers**
3. Expand **Oproto.FluentDynamoDb.SourceGenerator**
4. View the generated files

### Visual Studio Code

Generated files are in the `obj/` directory:

```bash
# Find generated files
find obj -name "*.g.cs"
```

## Complete Example

Here's a complete entity with all common features:

```csharp
using Oproto.FluentDynamoDb.Attributes;

[DynamoDbTable("users")]
public partial class User
{
    // Partition key with computed format
    [PartitionKey]
    [Computed("USER#{UserId}")]
    [DynamoDbAttribute("pk")]
    public string UserId { get; set; } = string.Empty;
    
    // Sort key (optional)
    [SortKey]
    [Computed("PROFILE")]
    [DynamoDbAttribute("sk")]
    public string RecordType { get; set; } = "PROFILE";
    
    // Standard attributes
    [DynamoDbAttribute("email")]
    public string Email { get; set; } = string.Empty;
    
    [DynamoDbAttribute("name")]
    public string Name { get; set; } = string.Empty;
    
    [DynamoDbAttribute("status")]
    public string Status { get; set; } = "active";
    
    // Timestamps
    [DynamoDbAttribute("createdAt")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    
    [DynamoDbAttribute("updatedAt")]
    public DateTime? UpdatedAt { get; set; }
    
    // Collections
    [DynamoDbAttribute("roles")]
    public List<string> Roles { get; set; } = new();
    
    [DynamoDbAttribute("preferences")]
    public Dictionary<string, string> Preferences { get; set; } = new();
}
```

**Generated Code Usage:**

```csharp
using Amazon.DynamoDBv2;
using Oproto.FluentDynamoDb.Storage;

var client = new AmazonDynamoDBClient();
var table = new UsersTable(client, "users");

// Create user
var user = new User
{
    UserId = "user123",
    Email = "john@example.com",
    Name = "John Doe",
    Roles = new List<string> { "admin", "user" },
    Preferences = new Dictionary<string, string>
    {
        ["theme"] = "dark",
        ["language"] = "en"
    }
};

await table.Put<User>().WithItem(user).ExecuteAsync();

// Retrieve user using generated fields
var response = await table.Get<User>()
    .WithKey(User.Fields.UserId, "user123", User.Fields.RecordType, "PROFILE")
    .ExecuteAsync();

// Query with filter using generated fields
var activeUsers = await table.Query<User>()
    .Where($"{User.Fields.UserId} = {{0}}", "user123")
    .WithFilter($"{User.Fields.Status} = {{0}}", "active")
    .ExecuteAsync();

// Update using generated fields
await table.Update<User>()
    .WithKey(User.Fields.UserId, "user123", User.Fields.RecordType, "PROFILE")
    .Set($"SET {User.Fields.Name} = {{0}}, {User.Fields.UpdatedAt} = {{1:o}}", 
         "Jane Doe", 
         DateTime.UtcNow)
    .ExecuteAsync();
```

## Best Practices

### 1. Use Meaningful Property Names

```csharp
// ✅ Good - clear property names
public string Email { get; set; }
public DateTime CreatedAt { get; set; }

// ❌ Avoid - unclear abbreviations
public string Em { get; set; }
public DateTime Dt { get; set; }
```

### 2. Initialize Collections

```csharp
// ✅ Good - prevents null reference exceptions
public List<string> Tags { get; set; } = new();

// ❌ Risky - can be null
public List<string> Tags { get; set; }
```

### 3. Use Nullable Types Appropriately

```csharp
// ✅ Good - optional timestamp
public DateTime? UpdatedAt { get; set; }

// ✅ Good - required timestamp with default
public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
```

### 4. Keep Key Formats Consistent

```csharp
// ✅ Good - consistent prefix pattern
[Computed("USER#{UserId}")]
[Computed("ORDER#{OrderId}")]
[Computed("PRODUCT#{ProductId}")]

// ❌ Inconsistent - hard to maintain
[Computed("USER#{UserId}")]
[Computed("{OrderId}-ORDER")]
[Computed("prod_{ProductId}")]
```

## Next Steps

- **[Entity Definition](../core-features/EntityDefinition.md)** - Advanced entity patterns (GSIs, relationships)
- **[Basic Operations](../core-features/BasicOperations.md)** - CRUD operations with your entities
- **[Expression Formatting](../core-features/ExpressionFormatting.md)** - Advanced format specifiers
- **[Attribute Reference](../reference/AttributeReference.md)** - Complete attribute documentation

## Troubleshooting

### Generated Code Not Appearing

**Symptoms:** `UserFields`, `UserKeys`, or `UserMapper` not found

**Solutions:**
1. Ensure class is marked as `partial`
2. Rebuild project: `dotnet clean && dotnet build`
3. Restart IDE
4. Check that `[PartitionKey]` attribute is present

### Compilation Errors

**Error:** "Partial declarations must not specify different base classes"

**Solution:** Ensure all partial declarations of the same class don't specify base classes, or they all specify the same base class.

**Error:** "The type or namespace name 'DynamoDbTable' could not be found"

**Solution:** Add using statement:
```csharp
using Oproto.FluentDynamoDb.Attributes;
```

See [Troubleshooting Guide](../reference/Troubleshooting.md) for more help.

---

[Previous: Installation](Installation.md) | [Next: Entity Definition](../core-features/EntityDefinition.md)

**See Also:**
- [Quick Start](QuickStart.md)
- [Attribute Reference](../reference/AttributeReference.md)
- [Computed Keys](../core-features/EntityDefinition.md#computed-keys)
- [Global Secondary Indexes](../advanced-topics/GlobalSecondaryIndexes.md)
