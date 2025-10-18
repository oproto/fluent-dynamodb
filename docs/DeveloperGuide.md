# DynamoDB Source Generator Developer Guide

## Table of Contents
- [Overview](#overview)
- [Getting Started](#getting-started)
- [Entity Definition](#entity-definition)
- [Generated Code](#generated-code)
- [Usage Patterns](#usage-patterns)
- [Advanced Features](#advanced-features)
- [Best Practices](#best-practices)
- [Performance Considerations](#performance-considerations)

## Overview

The Oproto.FluentDynamoDb source generator automatically creates entity mapping code, field constants, key builders, and enhanced ExecuteAsync methods. This eliminates boilerplate code while maintaining AOT compatibility and providing an EF/LINQ-like developer experience.

### Key Benefits

- **Zero Runtime Reflection**: All mapping code generated at compile time
- **Type Safety**: Compile-time validation of entity configurations
- **AOT Compatible**: Works with Native AOT and trimming
- **Incremental Adoption**: Use alongside existing fluent API code
- **Performance**: Optimized generated code with minimal allocations

## Getting Started

### Installation

```bash
dotnet add package Oproto.FluentDynamoDb
```

The source generator is automatically included as an analyzer and runs during compilation.

### Basic Entity Definition

```csharp
using Oproto.FluentDynamoDb.Attributes;

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

    [DynamoDbAttribute("created_at")]
    public DateTime CreatedAt { get; set; }
}
```

**Important Requirements:**
- Class must be marked as `partial`
- Must have exactly one `[PartitionKey]` property
- All mapped properties need `[DynamoDbAttribute]`

## Entity Definition

### Core Attributes

#### Table Definition
```csharp
[DynamoDbTable("table-name")]
public partial class MyEntity
{
    // Entity properties
}
```

#### Property Mapping
```csharp
[DynamoDbAttribute("dynamodb_attribute_name")]
public string PropertyName { get; set; }
```

#### Key Attributes
```csharp
// Partition key (required, exactly one per entity)
[PartitionKey]
[DynamoDbAttribute("pk")]
public string Id { get; set; }

// Sort key (optional)
[SortKey]
[DynamoDbAttribute("sk")]
public string SortKey { get; set; }
```

### Global Secondary Indexes

```csharp
[DynamoDbTable("products")]
public partial class Product
{
    [PartitionKey]
    [DynamoDbAttribute("pk")]
    public string ProductId { get; set; } = string.Empty;

    [SortKey]
    [DynamoDbAttribute("sk")]
    public string Category { get; set; } = string.Empty;

    // GSI partition key
    [GlobalSecondaryIndex("StatusIndex", IsPartitionKey = true)]
    [DynamoDbAttribute("status")]
    public string Status { get; set; } = string.Empty;

    // GSI sort key
    [GlobalSecondaryIndex("StatusIndex", IsSortKey = true)]
    [DynamoDbAttribute("created_date")]
    public DateTime CreatedDate { get; set; }

    [DynamoDbAttribute("name")]
    public string Name { get; set; } = string.Empty;

    [DynamoDbAttribute("price")]
    public decimal Price { get; set; }
}
```

### Computed and Composite Keys

#### Computed Keys
```csharp
[DynamoDbTable("customers")]
public partial class Customer
{
    // Source properties
    public string TenantId { get; set; } = string.Empty;
    public string CustomerId { get; set; } = string.Empty;

    // Computed composite key
    [PartitionKey]
    [DynamoDbAttribute("pk")]
    [Computed(nameof(TenantId), nameof(CustomerId))]
    public string Pk { get; set; } = string.Empty;

    // Computed with custom format
    [GlobalSecondaryIndex("StatusIndex", IsPartitionKey = true)]
    [DynamoDbAttribute("gsi1_pk")]
    [Computed(nameof(Status), Format = "STATUS#{0}")]
    public string StatusIndexPk { get; set; } = string.Empty;

    [DynamoDbAttribute("status")]
    public string Status { get; set; } = string.Empty;
}
```

#### Extracted Keys
```csharp
[DynamoDbTable("orders")]
public partial class Order
{
    // Composite key from DynamoDB
    [PartitionKey]
    [DynamoDbAttribute("pk")]
    public string Pk { get; set; } = string.Empty;

    // Extracted components
    [Extracted(nameof(Pk), 0)]
    public string TenantId { get; set; } = string.Empty;

    [Extracted(nameof(Pk), 1)]
    public string OrderId { get; set; } = string.Empty;
}
```

## Generated Code

### Field Constants

For each entity, the generator creates a static class with field name constants:

```csharp
// Generated: UserFields.cs
public static partial class UserFields
{
    public const string UserId = "pk";
    public const string Email = "email";
    public const string Name = "name";
    public const string CreatedAt = "created_at";
}
```

### Key Builders

Static methods for constructing keys safely:

```csharp
// Generated: UserKeys.cs
public static partial class UserKeys
{
    public static string Pk(string userId) => userId;
}
```

For composite keys:
```csharp
// Generated: CustomerKeys.cs
public static partial class CustomerKeys
{
    public static string Pk(string tenantId, string customerId) 
        => $"{tenantId}#{customerId}";
}
```

### Entity Implementation

The generator implements `IDynamoDbEntity` interface:

```csharp
public partial class User : IDynamoDbEntity
{
    public static Dictionary<string, AttributeValue> ToDynamoDb<TSelf>(TSelf entity) 
        where TSelf : IDynamoDbEntity
    {
        // Generated mapping logic
    }

    public static TSelf FromDynamoDb<TSelf>(Dictionary<string, AttributeValue> item) 
        where TSelf : IDynamoDbEntity
    {
        // Generated mapping logic
    }

    // Additional interface methods...
}
```

## Format String Support

The library supports string.Format-style parameter syntax in condition expressions and update expressions, eliminating the need for manual parameter naming and separate `.WithValue()` calls.

### Supported Format Specifiers

| Format | Description | Example Input | Example Output |
|--------|-------------|---------------|----------------|
| `o` | ISO 8601 DateTime | `DateTime.Now` | `2024-01-15T10:30:00.000Z` |
| `F2` | Fixed-point with 2 decimals | `99.999m` | `100.00` |
| `X` | Hexadecimal uppercase | `255` | `FF` |
| `x` | Hexadecimal lowercase | `255` | `ff` |
| `D` | Decimal integer | `123` | `123` |
| `P2` | Percentage with 2 decimals | `0.1234m` | `12.34%` |

### Format String Examples

```csharp
// Basic query with format strings
var response = await table.Query
    .Where($"{UserFields.UserId} = {{0}}", UserKeys.Pk("user123"))
    .ToListAsync<User>();

// Date range query with format specifiers
var startDate = DateTime.UtcNow.AddDays(-30);
var endDate = DateTime.UtcNow;
var response = await table.Query
    .Where($"{UserFields.UserId} = {{0}} AND {UserFields.CreatedAt} BETWEEN {{1:o}} AND {{2:o}}", 
           UserKeys.Pk("user123"), startDate, endDate)
    .ToListAsync<User>();

// Update with format strings
await table.Update
    .WithKey(UserFields.UserId, UserKeys.Pk("user123"))
    .Set($"SET {UserFields.Name} = {{0}}, {UserFields.UpdatedAt} = {{1:o}}", 
         "New Name", DateTime.UtcNow)
    .ExecuteAsync();

// Conditional operations with format strings
await table.Put
    .WithItem(user)
    .Where($"attribute_not_exists({{0}})", UserFields.UserId)
    .ExecuteAsync();
```

### Backward Compatibility

All existing code continues to work. You can mix format strings with traditional parameters:

```csharp
// Mix format strings with traditional parameters
var result = await table.Query
    .Where($"{UserFields.UserId} = {{0}} AND {UserFields.Status} BETWEEN :startStatus AND :endStatus", 
           UserKeys.Pk("user123"))
    .WithValue(":startStatus", "active")
    .WithValue(":endStatus", "pending")
    .ToListAsync<User>();
```

## Usage Patterns

### Basic CRUD Operations

```csharp
var table = new DynamoDbTableBase(dynamoDbClient, "users");

// Create
var user = new User
{
    UserId = "user123",
    Email = "user@example.com",
    Name = "John Doe",
    CreatedAt = DateTime.UtcNow
};

await table.Put
    .WithItem(user)
    .ExecuteAsync();

// Read
var response = await table.Get
    .WithKey(UserFields.UserId, UserKeys.Pk("user123"))
    .ExecuteAsync<User>();

if (response.Item != null)
{
    Console.WriteLine($"Found user: {response.Item.Name}");
}

// Update with format strings
await table.Update
    .WithKey(UserFields.UserId, UserKeys.Pk("user123"))
    .Set($"SET {UserFields.Name} = {{0}}", "Jane Doe")
    .ExecuteAsync();

// Delete
await table.Delete
    .WithKey(UserFields.UserId, UserKeys.Pk("user123"))
    .ExecuteAsync();
```

### Query Operations

#### Basic Query
```csharp
var queryResponse = await table.Query
    .Where($"{UserFields.UserId} = {{0}}", UserKeys.Pk("user123"))
    .ToListAsync<User>();

foreach (var user in queryResponse)
{
    Console.WriteLine($"User: {user.Name}");
}
```

#### GSI Query
```csharp
var productsByStatus = await table.Query
    .FromIndex("StatusIndex")
    .Where($"{ProductFields.StatusIndex.Status} = {{0}}", "active")
    .ToListAsync<Product>();
```

#### Range Queries
```csharp
var recentProducts = await table.Query
    .Where($"{ProductFields.ProductId} = {{0}} AND {ProductFields.CreatedDate} > {{1:o}}", 
           ProductKeys.Pk("PROD123"), DateTime.UtcNow.AddDays(-30))
    .ToListAsync<Product>();
```

### Batch Operations

```csharp
// Batch get
var batchResponse = await table.BatchGet
    .WithKeys(new[]
    {
        new Dictionary<string, AttributeValue>
        {
            [UserFields.UserId] = new AttributeValue { S = UserKeys.Pk("user1") }
        },
        new Dictionary<string, AttributeValue>
        {
            [UserFields.UserId] = new AttributeValue { S = UserKeys.Pk("user2") }
        }
    })
    .ExecuteAsync<User>();

// Batch write
var users = new[]
{
    new User { UserId = "user1", Name = "User 1", Email = "user1@example.com" },
    new User { UserId = "user2", Name = "User 2", Email = "user2@example.com" }
};

await table.BatchWrite
    .WithPutItems(users.Select(u => User.ToDynamoDb(u)))
    .ExecuteAsync();
```

## Advanced Features

### Multi-Item Entities

Entities that span multiple DynamoDB items with the same partition key:

```csharp
[DynamoDbTable("transactions")]
public partial class TransactionWithEntries
{
    [PartitionKey]
    [DynamoDbAttribute("pk")]
    public string TransactionId { get; set; } = string.Empty;

    [SortKey]
    [DynamoDbAttribute("sk")]
    public string SortKey { get; set; } = string.Empty;

    [DynamoDbAttribute("amount")]
    public decimal Amount { get; set; }

    // Collection mapped to separate DynamoDB items
    public List<LedgerEntry> LedgerEntries { get; set; } = new();
}

// Query automatically groups items by partition key
var transaction = await table.Query
    .Where($"{TransactionWithEntriesFields.TransactionId} = :pk", 
           new { pk = TransactionWithEntriesKeys.Pk("txn123") })
    .ToCompositeEntityAsync<TransactionWithEntries>();
```

### Related Entities

Define related entities that are automatically populated:

```csharp
[DynamoDbTable("orders")]
public partial class OrderWithRelated
{
    [PartitionKey]
    [DynamoDbAttribute("pk")]
    public string OrderId { get; set; } = string.Empty;

    [SortKey]
    [DynamoDbAttribute("sk")]
    public string SortKey { get; set; } = string.Empty;

    [DynamoDbAttribute("total")]
    public decimal Total { get; set; }

    // Related entities populated based on sort key patterns
    [RelatedEntity(SortKeyPattern = "item#*")]
    public List<OrderItem>? Items { get; set; }

    [RelatedEntity(SortKeyPattern = "payment#*")]
    public List<Payment>? Payments { get; set; }

    [RelatedEntity(SortKeyPattern = "summary")]
    public OrderSummary? Summary { get; set; }
}

// Query brings back all related data
var order = await table.Query
    .Where($"{OrderWithRelatedFields.OrderId} = :pk", 
           new { pk = OrderWithRelatedKeys.Pk("order123") })
    .ToCompositeEntityAsync<OrderWithRelated>();

// order.Items, order.Payments, and order.Summary are automatically populated
// based on what data the query returns
```

### Conditional Operations

```csharp
// Conditional put (only if item doesn't exist)
await table.Put
    .WithItem(user)
    .Where($"attribute_not_exists({{0}})", UserFields.UserId)
    .ExecuteAsync();

// Conditional update with version check
await table.Update
    .WithKey(UserFields.UserId, UserKeys.Pk("user123"))
    .Set($"SET {UserFields.Name} = {{0}}", "New Name")
    .Where($"{UserFields.Version} = {{0}}", currentVersion)
    .ExecuteAsync();
```

### Transactions

```csharp
await new TransactWriteItemsRequestBuilder(dynamoDbClient)
    .Put(table, put => put
        .WithItem(newUser)
        .Where($"attribute_not_exists({{0}})", UserFields.UserId))
    .Update(table, update => update
        .WithKey(UserFields.UserId, UserKeys.Pk("existing-user"))
        .Set($"SET {UserFields.Name} = {{0}}", "Updated Name"))
    .Delete(table, delete => delete
        .WithKey(UserFields.UserId, UserKeys.Pk("user-to-delete")))
    .ExecuteAsync();
```

## Best Practices

### Entity Design

1. **Use Composite Keys Wisely**
   ```csharp
   // Good: Hierarchical access pattern
   [Computed(nameof(TenantId), nameof(UserId))]
   public string Pk { get; set; } // "tenant123#user456"
   
   // Good: Time-based sorting
   [Computed(nameof(Date), nameof(EventType), Format = "{0:yyyy-MM-dd}#{1}")]
   public string Sk { get; set; } // "2024-03-15#login"
   ```

2. **Leverage GSIs for Access Patterns**
   ```csharp
   // Support queries by status and date
   [GlobalSecondaryIndex("StatusDateIndex", IsPartitionKey = true)]
   public string Status { get; set; }
   
   [GlobalSecondaryIndex("StatusDateIndex", IsSortKey = true)]
   public DateTime CreatedDate { get; set; }
   ```

3. **Use Related Entities for Complex Data**
   ```csharp
   // Main entity with optional related data
   [RelatedEntity(SortKeyPattern = "metadata")]
   public EntityMetadata? Metadata { get; set; }
   
   [RelatedEntity(SortKeyPattern = "audit#*")]
   public List<AuditEntry>? AuditTrail { get; set; }
   ```

### Performance Optimization

1. **Minimize Attribute Count**
   ```csharp
   // Good: Only map necessary properties
   [DynamoDbAttribute("essential_data")]
   public string EssentialData { get; set; }
   
   // Avoid: Mapping large objects that aren't queried
   // public LargeObject Details { get; set; } // Don't map if not needed
   ```

2. **Use Projection for GSIs**
   ```csharp
   // Only project necessary attributes to GSI
   [GlobalSecondaryIndex("StatusIndex", IsPartitionKey = true)]
   [QueryableAttribute(AvailableInIndexes = new[] { "StatusIndex" })]
   public string Status { get; set; }
   ```

3. **Batch Operations When Possible**
   ```csharp
   // Process multiple items in single request
   var items = users.Select(u => User.ToDynamoDb(u)).ToList();
   await table.BatchWrite.WithPutItems(items).ExecuteAsync();
   ```

### Error Handling

1. **Use Conditional Expressions**
   ```csharp
   try
   {
       await table.Put
           .WithItem(user)
           .WithConditionExpression($"attribute_not_exists({UserFields.UserId})")
           .ExecuteAsync();
   }
   catch (ConditionalCheckFailedException)
   {
       // Handle duplicate user
   }
   ```

2. **Validate Before Mapping**
   ```csharp
   if (string.IsNullOrEmpty(user.UserId))
   {
       throw new ArgumentException("UserId is required");
   }
   
   await table.Put.WithItem(user).ExecuteAsync();
   ```

## Performance Considerations

### Generated Code Performance

- **Zero Reflection**: All type information resolved at compile time
- **Minimal Allocations**: Optimized object creation and mapping
- **Efficient String Operations**: Pre-computed format strings for keys
- **AOT Friendly**: No runtime code generation

### DynamoDB Best Practices

1. **Hot Partitions**: Use composite keys to distribute load
2. **Query Efficiency**: Design GSIs for your access patterns
3. **Batch Size**: Keep batch operations under 25 items or 16MB
4. **Consistent Reads**: Use only when necessary (costs 2x RCU)

### Monitoring and Debugging

```csharp
// Enable request/response logging
var config = new AmazonDynamoDBConfig
{
    LogResponse = true,
    LogMetrics = true
};

// Monitor consumed capacity
var response = await table.Query
    .Where($"{UserFields.UserId} = :pk", new { pk = UserKeys.Pk("user123") })
    .WithReturnConsumedCapacity(ReturnConsumedCapacity.TOTAL)
    .ToListAsync<User>();

Console.WriteLine($"Consumed RCU: {response.ConsumedCapacity?.ReadCapacityUnits}");
```

This developer guide provides comprehensive coverage of the DynamoDB source generator features and usage patterns. The next step would be to create specific migration and troubleshooting guides.