---
title: "Entity Definition"
category: "core-features"
order: 1
keywords: ["entity", "attributes", "keys", "computed", "extracted", "GSI", "queryable", "source generation"]
related: ["BasicOperations.md", "QueryingData.md", "../getting-started/FirstEntity.md", "../reference/AttributeReference.md"]
---

[Documentation](../README.md) > [Core Features](README.md) > Entity Definition

# Entity Definition

[Next: Basic Operations](BasicOperations.md)

---

This guide covers comprehensive entity definition patterns in Oproto.FluentDynamoDb, including advanced features like computed keys, extracted keys, Global Secondary Indexes, and queryable attributes.

## Basic Entity Structure

Every DynamoDB entity requires:

1. **`[DynamoDbTable]` attribute** - Specifies the table name
2. **`partial` keyword** - Enables source generation
3. **At least one `[PartitionKey]`** - Defines the partition key
4. **`[DynamoDbAttribute]` on properties** - Maps properties to DynamoDB attributes

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
}
```

## Record Type Entities

C# record types are fully supported as DynamoDB entities. Records provide immutable data patterns with built-in value equality.

### Basic Record Entity

```csharp
using Oproto.FluentDynamoDb.Attributes;

[DynamoDbTable("products")]
public partial record Product
{
    [PartitionKey]
    [DynamoDbAttribute("pk")]
    public string ProductId { get; init; } = string.Empty;
    
    [DynamoDbAttribute("name")]
    public string Name { get; init; } = string.Empty;
    
    [DynamoDbAttribute("price")]
    public decimal Price { get; init; }
}

// Usage
var product = new Product
{
    ProductId = "prod-123",
    Name = "Widget",
    Price = 29.99m
};

await table.Products.PutAsync(product);
```

### Record with Positional Parameters

Records with positional (primary constructor) parameters are supported:

```csharp
[DynamoDbTable("events")]
public partial record Event(
    [property: PartitionKey]
    [property: DynamoDbAttribute("pk")]
    string EventId,
    
    [property: DynamoDbAttribute("name")]
    string Name,
    
    [property: DynamoDbAttribute("timestamp")]
    DateTime Timestamp
);

// Usage
var evt = new Event("evt-123", "UserLogin", DateTime.UtcNow);
await table.Events.PutAsync(evt);
```

**Note:** Use `[property: AttributeName]` syntax to apply attributes to the generated properties.

### Record with Init-Only Properties

Init-only properties work seamlessly:

```csharp
[DynamoDbTable("orders")]
public partial record Order
{
    [PartitionKey]
    [DynamoDbAttribute("pk")]
    public required string OrderId { get; init; }
    
    [SortKey]
    [DynamoDbAttribute("sk")]
    public required string CustomerId { get; init; }
    
    [DynamoDbAttribute("total")]
    public decimal Total { get; init; }
    
    [DynamoDbAttribute("status")]
    public string Status { get; init; } = "pending";
    
    [DynamoDbAttribute("items")]
    public IReadOnlyList<string> Items { get; init; } = Array.Empty<string>();
}

// Usage with object initializer
var order = new Order
{
    OrderId = "order-123",
    CustomerId = "cust-456",
    Total = 99.99m,
    Items = new[] { "item-1", "item-2" }
};

// Usage with 'with' expression for updates
var updatedOrder = order with { Status = "shipped" };
```

### Record Class vs Record Struct

Both `record class` (reference type) and `record struct` (value type) are supported:

```csharp
// Record class (reference type, default)
[DynamoDbTable("users")]
public partial record class User
{
    [PartitionKey]
    [DynamoDbAttribute("pk")]
    public string UserId { get; init; } = string.Empty;
}

// Record struct (value type)
[DynamoDbTable("coordinates")]
public partial record struct Coordinate
{
    [PartitionKey]
    [DynamoDbAttribute("pk")]
    public string Id { get; init; }
    
    [DynamoDbAttribute("lat")]
    public double Latitude { get; init; }
    
    [DynamoDbAttribute("lon")]
    public double Longitude { get; init; }
}
```

### Record with Computed Keys

Records work with all key patterns including computed keys:

```csharp
[DynamoDbTable("transactions")]
public partial record Transaction
{
    public string TransactionId { get; init; } = string.Empty;
    public string AccountId { get; init; } = string.Empty;
    
    [PartitionKey]
    [Computed(nameof(AccountId), Format = "ACCOUNT#{0}")]
    [DynamoDbAttribute("pk")]
    public string PartitionKey { get; init; } = string.Empty;
    
    [SortKey]
    [Computed(nameof(TransactionId), Format = "TXN#{0}")]
    [DynamoDbAttribute("sk")]
    public string SortKey { get; init; } = string.Empty;
    
    [DynamoDbAttribute("amount")]
    public decimal Amount { get; init; }
    
    [DynamoDbAttribute("timestamp")]
    public DateTimeOffset Timestamp { get; init; }
}
```

### Considerations for Record Types

**Benefits:**
- Immutable by default (safer concurrent access)
- Built-in value equality (useful for comparisons)
- Concise syntax with positional parameters
- `with` expressions for creating modified copies

**Limitations:**
- Init-only properties require object initializer or constructor
- Cannot modify properties after creation (use `with` to create new instance)
- Positional parameters require `[property:]` attribute syntax

**Best Practices:**
```csharp
// ✅ Good: Use init for immutability
public string Name { get; init; }

// ✅ Good: Provide defaults for optional properties
public string Status { get; init; } = "active";

// ✅ Good: Use required for mandatory properties
public required string Id { get; init; }

// ✅ Good: Use IReadOnlyList for immutable collections
public IReadOnlyList<string> Tags { get; init; } = Array.Empty<string>();
```

## Attribute Mapping

### DynamoDbTable Attribute

Marks a class as a DynamoDB entity and specifies the table name:

```csharp
// Simple table name
[DynamoDbTable("users")]
public partial class User { }

// Legacy entity discriminator (deprecated - use new discriminator properties)
[DynamoDbTable("entities", EntityDiscriminator = "USER")]
public partial class User { }
```

**Entity Discriminator Use Case:** When storing multiple entity types in a single table (single-table design), the discriminator helps identify the entity type.

### Flexible Discriminator Configuration

The library supports flexible discriminator strategies for single-table designs:

#### Attribute-Based Discriminator

Use a dedicated attribute to identify entity types:

```csharp
[DynamoDbTable("entities",
    DiscriminatorProperty = "entity_type",
    DiscriminatorValue = "USER")]
public partial class User { }

[DynamoDbTable("entities",
    DiscriminatorProperty = "entity_type",
    DiscriminatorValue = "ORDER")]
public partial class Order { }
```

#### Sort Key Pattern Discriminator

Use sort key prefixes to identify entity types:

```csharp
[DynamoDbTable("entities",
    DiscriminatorProperty = "SK",
    DiscriminatorPattern = "USER#*")]
public partial class User 
{
    [PartitionKey]
    [DynamoDbAttribute("pk")]
    public string TenantId { get; set; } = string.Empty;
    
    [SortKey]
    [DynamoDbAttribute("sk")]
    public string SortKey { get; set; } = string.Empty; // e.g., "USER#user123"
}
```

#### Pattern Matching Syntax

Discriminator patterns support wildcard matching:

| Pattern | Matches | Example |
|---------|---------|---------|
| `USER#*` | Starts with "USER#" | `USER#123`, `USER#abc` |
| `*#USER` | Ends with "#USER" | `TENANT#abc#USER` |
| `*#USER#*` | Contains "#USER#" | `TENANT#abc#USER#123` |
| `USER` | Exact match | `USER` only |

#### GSI-Specific Discriminators

Different discriminators can be used for GSI queries:

```csharp
[DynamoDbTable("entities",
    DiscriminatorProperty = "SK",
    DiscriminatorPattern = "USER#*")]
public partial class User 
{
    [PartitionKey]
    [DynamoDbAttribute("pk")]
    public string TenantId { get; set; } = string.Empty;
    
    [SortKey]
    [DynamoDbAttribute("sk")]
    public string SortKey { get; set; } = string.Empty;
    
    // GSI uses different discriminator pattern
    [GsiPartitionKey("StatusIndex",
        DiscriminatorProperty = "GSI1SK",
        DiscriminatorPattern = "USER#*")]
    [DynamoDbAttribute("status")]
    public string Status { get; set; } = string.Empty;
    
    [GsiSortKey("StatusIndex")]
    [DynamoDbAttribute("gsi1sk")]
    public string StatusSortKey { get; set; } = string.Empty;
}
```

**Backward Compatibility:** The legacy `EntityDiscriminator` property is still supported but deprecated. It's equivalent to setting `DiscriminatorProperty="entity_type"` and `DiscriminatorValue` to the discriminator value.

### DynamoDbAttribute Attribute

Maps C# properties to DynamoDB attribute names:

```csharp
[DynamoDbTable("products")]
public partial class Product
{
    [PartitionKey]
    [DynamoDbAttribute("pk")]
    public string ProductId { get; set; } = string.Empty;
    
    // Property name: Name
    // DynamoDB attribute name: product_name
    [DynamoDbAttribute("product_name")]
    public string Name { get; set; } = string.Empty;
    
    // Property name: Price
    // DynamoDB attribute name: price
    [DynamoDbAttribute("price")]
    public decimal Price { get; set; }
}
```

**Best Practice:** Use short, generic attribute names (like `pk`, `sk`) for single-table design, or descriptive names (like `userId`, `email`) for dedicated tables.

## Key Definitions

### Partition Key

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

**Generated Code:**
```csharp
// UserKeys.g.cs
public static class UserKeys
{
    public static string Pk(string userId)
    {
        return userId;
    }
}
```

### Sort Key

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
}
```

**Generated Code:**
```csharp
// OrderKeys.g.cs
public static class OrderKeys
{
    public static string Pk(string customerId)
    {
        return customerId;
    }
    
    public static string Sk(string orderId)
    {
        return orderId;
    }
}
```

### Non-String Key Types

Partition keys and sort keys can use non-string .NET types such as enums, numeric types, `Guid`, `DateTime`, `DateOnly`, and `TimeOnly`. The source generator automatically handles the correct DynamoDB serialization:

```csharp
[DynamoDbTable("sensors")]
public partial class SensorReading
{
    [PartitionKey]
    [DynamoDbAttribute("pk")]
    public int SensorId { get; set; }
    
    [SortKey]
    [DynamoDbAttribute("sk")]
    public DateTime Timestamp { get; set; }
}
```

The generated accessor methods use the native .NET type as the parameter and construct the correct `AttributeValue` internally:

```csharp
// Generated: Get(int pK, DateTime sK) with correct DynamoDB AttributeValue construction
var reading = await table.SensorReadings.Get(42, dateTime).GetItemAsync();
await table.SensorReadings.Delete(42, dateTime).DeleteAsync();
```

**Supported non-string key types:**

| .NET Type | DynamoDB Type | Serialization |
|-----------|--------------|---------------|
| `int`, `long`, `double`, `decimal`, etc. | N (number) | `.ToString()` |
| `Guid` | S (string) | `.ToString()` |
| `DateTime` | S (string) | `.ToString("O")` (ISO 8601) |
| `DateOnly` | S (string) | `.ToString("O", CultureInfo.InvariantCulture)` |
| `TimeOnly` | S (string) | `.ToString("O", CultureInfo.InvariantCulture)` |
| Enum types | S (string) | `.ToString()` (name) or `.ToString("D")` (numeric) |
| `bool` | BOOL | Direct value |

**Custom formats** via `[DynamoDbAttribute(Format = "...")]`:

```csharp
// Enum with numeric representation
[SortKey]
[DynamoDbAttribute("sk", Format = "D")]
public MyEnum Category { get; set; }

// DateTime with custom format
[SortKey]
[DynamoDbAttribute("sk", Format = "yyyy-MM-dd")]
public DateTime EventDate { get; set; }

// DateTime with UTC normalization
[SortKey]
[DynamoDbAttribute("sk", DateTimeKind = DateTimeKind.Utc)]
public DateTime CreatedAt { get; set; }
```

**Nullable key types** are also supported — the generated code uses `.Value` before serialization:

```csharp
[PartitionKey]
[DynamoDbAttribute("pk")]
public int? Score { get; set; }
```

> **Note:** When a non-string key has a `Prefix` configured or is `[Computed]`, the parameter type remains `string` and uses `.WithKey()` as before — the caller supplies the fully-formed prefixed string value.

### Key Prefixes

Add prefixes to partition and sort keys for better organization:

```csharp
[DynamoDbTable("entities")]
public partial class User
{
    [PartitionKey(Prefix = "USER")]
    [DynamoDbAttribute("pk")]
    public string UserId { get; set; } = string.Empty;
    
    [SortKey(Prefix = "PROFILE")]
    [DynamoDbAttribute("sk")]
    public string ProfileType { get; set; } = "MAIN";
}
```

**Generated Code:**
```csharp
// UserKeys.g.cs
public static class UserKeys
{
    public static string Pk(string userId)
    {
        return $"USER#{userId}";
    }
    
    public static string Sk(string profileType)
    {
        return $"PROFILE#{profileType}";
    }
}
```

**Usage:**
```csharp
// UserKeys.Pk("user123") returns "USER#user123"
// UserKeys.Sk("MAIN") returns "PROFILE#MAIN"

var user = await table.Get
    .WithKey(UserFields.UserId, UserKeys.Pk("user123"))
    .WithKey(UserFields.ProfileType, UserKeys.Sk("MAIN"))
    .GetItemAsync();
```

### Custom Separators

Change the default separator from `#` to another character:

```csharp
[DynamoDbTable("entities")]
public partial class User
{
    [PartitionKey(Prefix = "USER", Separator = "|")]
    [DynamoDbAttribute("pk")]
    public string UserId { get; set; } = string.Empty;
}

// Generated: UserKeys.Pk("user123") returns "USER|user123"
```

## Computed Keys with Format Strings

Use the `[Computed]` attribute to create keys from multiple properties or with custom formatting:

### Simple Computed Keys

```csharp
[DynamoDbTable("products")]
public partial class Product
{
    // Source property
    public string ProductId { get; set; } = string.Empty;
    
    // Computed from ProductId with format string
    [PartitionKey]
    [Computed(nameof(ProductId), Format = "PRODUCT#{0}")]
    [DynamoDbAttribute("pk")]
    public string PartitionKey { get; set; } = string.Empty;
}
```

**Generated Code:**
```csharp
public static class ProductKeys
{
    public static string Pk(string productId)
    {
        return $"PRODUCT#{productId}";
    }
}
```

### Multi-Property Computed Keys

Combine multiple properties into a single key:

```csharp
[DynamoDbTable("events")]
public partial class Event
{
    public string TenantId { get; set; } = string.Empty;
    public string EventType { get; set; } = string.Empty;
    
    // Computed from TenantId and EventType
    [PartitionKey]
    [Computed(nameof(TenantId), nameof(EventType), Format = "TENANT#{0}#EVENT#{1}")]
    [DynamoDbAttribute("pk")]
    public string PartitionKey { get; set; } = string.Empty;
}
```

**Generated Code:**
```csharp
public static class EventKeys
{
    public static string Pk(string tenantId, string eventType)
    {
        return $"TENANT#{tenantId}#EVENT#{eventType}";
    }
}
```

**Usage:**
```csharp
var event = await table.Get
    .WithKey(EventFields.PartitionKey, EventKeys.Pk("tenant123", "LOGIN"))
    .GetItemAsync();
```

### DateTime Format Strings

Use .NET format specifiers for DateTime properties:

```csharp
[DynamoDbTable("logs")]
public partial class LogEntry
{
    public DateTime Timestamp { get; set; }
    
    // Format as ISO 8601: 2024-03-15T10:30:00.000Z
    [PartitionKey]
    [Computed(nameof(Timestamp), Format = "LOG#{0:o}")]
    [DynamoDbAttribute("pk")]
    public string PartitionKey { get; set; } = string.Empty;
    
    // Format as date only: 2024-03-15
    [SortKey]
    [Computed(nameof(Timestamp), Format = "{0:yyyy-MM-dd}")]
    [DynamoDbAttribute("sk")]
    public string DateKey { get; set; } = string.Empty;
}
```

**Common DateTime Formats:**
- `:o` - ISO 8601 (2024-03-15T10:30:00.000Z)
- `:yyyy-MM-dd` - Date only (2024-03-15)
- `:yyyy-MM` - Year-month (2024-03)
- `:yyyyMMddHHmmss` - Compact timestamp (20240315103000)

### Numeric Format Strings

Format numbers with padding or precision:

```csharp
[DynamoDbTable("versions")]
public partial class Version
{
    public int VersionNumber { get; set; }
    
    // Format with zero-padding: v001, v002, v010, v100
    [SortKey]
    [Computed(nameof(VersionNumber), Format = "v{0:D3}")]
    [DynamoDbAttribute("sk")]
    public string VersionKey { get; set; } = string.Empty;
}
```

**Common Numeric Formats:**
- `:D3` - Zero-padded integer (001, 010, 100)
- `:F2` - Fixed-point with 2 decimals (10.50)
- `:N0` - Number with thousands separator (1,000)

### Default Separator (No Format)

If you don't specify a format, properties are joined with the separator:

```csharp
[DynamoDbTable("composite")]
public partial class CompositeEntity
{
    public string TenantId { get; set; } = string.Empty;
    public string UserId { get; set; } = string.Empty;
    
    // Uses default separator "#"
    [PartitionKey]
    [Computed(nameof(TenantId), nameof(UserId))]
    [DynamoDbAttribute("pk")]
    public string PartitionKey { get; set; } = string.Empty;
}

// Generated: CompositeEntityKeys.Pk("tenant1", "user1") returns "tenant1#user1"
```

**Custom Separator:**
```csharp
[PartitionKey]
[Computed(nameof(TenantId), nameof(UserId), Separator = "|")]
[DynamoDbAttribute("pk")]
public string PartitionKey { get; set; } = string.Empty;

// Generated: CompositeEntityKeys.Pk("tenant1", "user1") returns "tenant1|user1"
```

## Extracted Keys

Use the `[Extracted]` attribute to extract components from composite keys:

### Basic Extraction

```csharp
[DynamoDbTable("events")]
public partial class Event
{
    // Composite partition key: "TENANT#tenant123"
    [PartitionKey]
    [DynamoDbAttribute("pk")]
    public string PartitionKey { get; set; } = string.Empty;
    
    // Extract tenant ID from partition key (index 1 after splitting by #)
    [Extracted(nameof(PartitionKey), 1, Separator = "#")]
    public string TenantId { get; set; } = string.Empty;
}
```

**How It Works:**
1. `PartitionKey` value: `"TENANT#tenant123"`
2. Split by separator `#`: `["TENANT", "tenant123"]`
3. Extract index `1`: `"tenant123"`
4. Assign to `TenantId`

### Multiple Extractions

Extract multiple components from the same key:

```csharp
[DynamoDbTable("hierarchical")]
public partial class HierarchicalEntity
{
    // Composite key: "ORG#org1#DEPT#dept2#USER#user3"
    [PartitionKey]
    [DynamoDbAttribute("pk")]
    public string PartitionKey { get; set; } = string.Empty;
    
    // Extract organization (index 1)
    [Extracted(nameof(PartitionKey), 1, Separator = "#")]
    public string OrganizationId { get; set; } = string.Empty;
    
    // Extract department (index 3)
    [Extracted(nameof(PartitionKey), 3, Separator = "#")]
    public string DepartmentId { get; set; } = string.Empty;
    
    // Extract user (index 5)
    [Extracted(nameof(PartitionKey), 5, Separator = "#")]
    public string UserId { get; set; } = string.Empty;
}
```

### Extraction from Sort Keys

Extract from sort keys as well:

```csharp
[DynamoDbTable("timeseries")]
public partial class TimeSeriesData
{
    [PartitionKey]
    [DynamoDbAttribute("pk")]
    public string SensorId { get; set; } = string.Empty;
    
    // Sort key: "2024-03-15#10:30:00"
    [SortKey]
    [DynamoDbAttribute("sk")]
    public string TimestampKey { get; set; } = string.Empty;
    
    // Extract date (index 0)
    [Extracted(nameof(TimestampKey), 0, Separator = "#")]
    public string Date { get; set; } = string.Empty;
    
    // Extract time (index 1)
    [Extracted(nameof(TimestampKey), 1, Separator = "#")]
    public string Time { get; set; } = string.Empty;
}
```

**Use Case:** Query by date range while maintaining time precision in the sort key.

## Global Secondary Indexes

Define Global Secondary Indexes (GSIs) using the `[GsiPartitionKey]` and `[GsiSortKey]` attributes:

### Simple GSI

```csharp
[DynamoDbTable("users")]
public partial class User
{
    [PartitionKey]
    [DynamoDbAttribute("pk")]
    public string UserId { get; set; } = string.Empty;
    
    // GSI partition key
    [GsiPartitionKey("EmailIndex")]
    [DynamoDbAttribute("email")]
    public string Email { get; set; } = string.Empty;
}
```

**Generated Code:**
```csharp
// UserIndexes.g.cs
public static class UserIndexes
{
    public const string EmailIndex = "EmailIndex";
}

// UserFields.g.cs
public static class UserFields
{
    public const string Email = "email";
}
```

**Usage:**
```csharp
// Query by email using GSI
var users = await table.Query
    .UsingIndex(UserIndexes.EmailIndex)
    .Where($"{UserFields.Email} = {{0}}", "john@example.com")
    .ToListAsync();
```

### GSI with Sort Key

```csharp
[DynamoDbTable("orders")]
public partial class Order
{
    [PartitionKey]
    [DynamoDbAttribute("pk")]
    public string OrderId { get; set; } = string.Empty;
    
    // GSI partition key
    [GsiPartitionKey("StatusIndex")]
    [DynamoDbAttribute("status")]
    public string Status { get; set; } = string.Empty;
    
    // GSI sort key
    [GsiSortKey("StatusIndex")]
    [DynamoDbAttribute("createdAt")]
    public DateTime CreatedAt { get; set; }
}
```

**Usage:**
```csharp
// Query orders by status, sorted by creation date
var orders = await table.Query
    .UsingIndex(OrderIndexes.StatusIndex)
    .Where($"{OrderFields.Status} = {{0}}", "pending")
    .ToListAsync();
```

### Multiple GSIs

Define multiple GSIs on the same entity:

```csharp
[DynamoDbTable("products")]
public partial class Product
{
    [PartitionKey]
    [DynamoDbAttribute("pk")]
    public string ProductId { get; set; } = string.Empty;
    
    // GSI 1: Query by category
    [GsiPartitionKey("CategoryIndex")]
    [DynamoDbAttribute("category")]
    public string Category { get; set; } = string.Empty;
    
    // GSI 2: Query by vendor
    [GsiPartitionKey("VendorIndex")]
    [DynamoDbAttribute("vendorId")]
    public string VendorId { get; set; } = string.Empty;
    
    // GSI 2 sort key
    [GsiSortKey("VendorIndex")]
    [DynamoDbAttribute("price")]
    public decimal Price { get; set; }
}
```

**Generated Code:**
```csharp
public static class ProductIndexes
{
    public const string CategoryIndex = "CategoryIndex";
    public const string VendorIndex = "VendorIndex";
}
```

### GSI with Computed Keys

Combine GSIs with computed keys for advanced patterns:

```csharp
[DynamoDbTable("transactions")]
public partial class Transaction
{
    [PartitionKey]
    [DynamoDbAttribute("pk")]
    public string TransactionId { get; set; } = string.Empty;
    
    public string TenantId { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    
    // GSI partition key: "TENANT#tenant123#STATUS#pending"
    [GsiPartitionKey("TenantStatusIndex")]
    [Computed(nameof(TenantId), nameof(Status), Format = "TENANT#{0}#STATUS#{1}")]
    [DynamoDbAttribute("gsi1pk")]
    public string TenantStatusKey { get; set; } = string.Empty;
    
    // GSI sort key
    [GsiSortKey("TenantStatusIndex")]
    [DynamoDbAttribute("gsi1sk")]
    public DateTime CreatedAt { get; set; }
}
```

**Use Case:** Query all pending transactions for a tenant, sorted by creation date.

## Queryable Attributes

Mark properties as queryable to document supported operations (useful for future LINQ support):

```csharp
[DynamoDbTable("products")]
public partial class Product
{
    [PartitionKey]
    [DynamoDbAttribute("pk")]
    public string ProductId { get; set; } = string.Empty;
    
    // Supports equality and range comparisons
    [QueryableAttribute(
        SupportedOperations = new[] { 
            DynamoDbOperation.Equals, 
            DynamoDbOperation.GreaterThan, 
            DynamoDbOperation.LessThan,
            DynamoDbOperation.Between
        }
    )]
    [DynamoDbAttribute("price")]
    public decimal Price { get; set; }
    
    // Supports string operations
    [QueryableAttribute(
        SupportedOperations = new[] { 
            DynamoDbOperation.Equals, 
            DynamoDbOperation.BeginsWith,
            DynamoDbOperation.Contains
        }
    )]
    [DynamoDbAttribute("name")]
    public string Name { get; set; } = string.Empty;
    
    // Available in specific indexes
    [QueryableAttribute(
        SupportedOperations = new[] { DynamoDbOperation.Equals },
        AvailableInIndexes = new[] { "CategoryIndex", "VendorIndex" }
    )]
    [DynamoDbAttribute("category")]
    public string Category { get; set; } = string.Empty;
}
```

**Supported Operations:**
- `Equals` - Equality comparison (`=`)
- `BeginsWith` - String prefix matching
- `Between` - Range comparison
- `GreaterThan` - Greater than (`>`)
- `LessThan` - Less than (`<`)
- `Contains` - Set/string contains
- `In` - Multiple value matching

**Note:** This attribute is primarily for documentation and future LINQ support. It doesn't enforce query restrictions at runtime.

## Best Practices

### 1. Use Computed Keys for Consistency

```csharp
// ✅ Good - consistent key format enforced by source generator
[PartitionKey]
[Computed(nameof(UserId), Format = "USER#{0}")]
[DynamoDbAttribute("pk")]
public string PartitionKey { get; set; } = string.Empty;

// ❌ Avoid - manual key construction prone to errors
[PartitionKey]
[DynamoDbAttribute("pk")]
public string PartitionKey { get; set; } = string.Empty;

// Manual construction elsewhere:
user.PartitionKey = $"USER#{user.UserId}";  // Easy to make mistakes
```

### 2. Extract Components for Querying

```csharp
// ✅ Good - can query by TenantId without parsing
[PartitionKey]
[DynamoDbAttribute("pk")]
public string PartitionKey { get; set; } = string.Empty;

[Extracted(nameof(PartitionKey), 1, Separator = "#")]
public string TenantId { get; set; } = string.Empty;

// Query: WHERE TenantId = 'tenant123'
```

### 3. Use GSIs for Alternative Access Patterns

```csharp
// ✅ Good - supports multiple query patterns
[PartitionKey]
[DynamoDbAttribute("pk")]
public string OrderId { get; set; } = string.Empty;

// Access pattern 1: Query by customer
[GsiPartitionKey("CustomerIndex")]
[DynamoDbAttribute("customerId")]
public string CustomerId { get; set; } = string.Empty;

// Access pattern 2: Query by status
[GsiPartitionKey("StatusIndex")]
[DynamoDbAttribute("status")]
public string Status { get; set; } = string.Empty;
```

### 4. Use DateTime Format Strings for Sortable Keys

```csharp
// ✅ Good - sortable timestamp format
[SortKey]
[Computed(nameof(Timestamp), Format = "{0:yyyy-MM-ddTHH:mm:ss.fffZ}")]
[DynamoDbAttribute("sk")]
public string TimestampKey { get; set; } = string.Empty;

// ❌ Avoid - not sortable
[SortKey]
[Computed(nameof(Timestamp), Format = "{0:MM/dd/yyyy}")]
[DynamoDbAttribute("sk")]
public string TimestampKey { get; set; } = string.Empty;
```

### 5. Document Queryable Attributes

```csharp
// ✅ Good - documents supported operations
[QueryableAttribute(
    SupportedOperations = new[] { 
        DynamoDbOperation.Equals, 
        DynamoDbOperation.GreaterThan 
    }
)]
[DynamoDbAttribute("price")]
public decimal Price { get; set; }
```

### 6. Keep Key Formats Simple

```csharp
// ✅ Good - simple, readable format
[Computed(nameof(UserId), Format = "USER#{0}")]

// ❌ Avoid - overly complex format
[Computed(nameof(UserId), nameof(TenantId), nameof(Region), 
          Format = "TENANT#{1}#REGION#{2}#USER#{0}#ACTIVE")]
```

## Complete Example

Here's a comprehensive entity using all features:

```csharp
using Oproto.FluentDynamoDb.Attributes;

[DynamoDbTable("orders", EntityDiscriminator = "ORDER")]
public partial class Order
{
    // Source properties
    public string OrderId { get; set; } = string.Empty;
    public string CustomerId { get; set; } = string.Empty;
    public string Status { get; set; } = "pending";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
    
    // Computed partition key: "ORDER#order123"
    [PartitionKey]
    [Computed(nameof(OrderId), Format = "ORDER#{0}")]
    [DynamoDbAttribute("pk")]
    public string PartitionKey { get; set; } = string.Empty;
    
    // Computed sort key: "METADATA"
    [SortKey]
    [Computed(Format = "METADATA")]
    [DynamoDbAttribute("sk")]
    public string SortKey { get; set; } = "METADATA";
    
    // Extract OrderId from partition key
    [Extracted(nameof(PartitionKey), 1, Separator = "#")]
    public string ExtractedOrderId { get; set; } = string.Empty;
    
    // GSI 1: Query by customer
    [GsiPartitionKey("CustomerIndex")]
    [Computed(nameof(CustomerId), Format = "CUSTOMER#{0}")]
    [DynamoDbAttribute("gsi1pk")]
    public string CustomerKey { get; set; } = string.Empty;
    
    [GsiSortKey("CustomerIndex")]
    [DynamoDbAttribute("gsi1sk")]
    public DateTime CustomerIndexSortKey { get; set; }
    
    // GSI 2: Query by status
    [GsiPartitionKey("StatusIndex")]
    [DynamoDbAttribute("status")]
    public string StatusKey { get; set; } = string.Empty;
    
    [GsiSortKey("StatusIndex")]
    [DynamoDbAttribute("createdAt")]
    public DateTime StatusIndexSortKey { get; set; }
    
    // Regular attributes
    [QueryableAttribute(
        SupportedOperations = new[] { 
            DynamoDbOperation.Equals, 
            DynamoDbOperation.GreaterThan,
            DynamoDbOperation.LessThan
        }
    )]
    [DynamoDbAttribute("total")]
    public decimal Total { get; set; }
    
    [DynamoDbAttribute("items")]
    public List<OrderItem> Items { get; set; } = new();
}

public class OrderItem
{
    public string ProductId { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public decimal Price { get; set; }
}
```

## Next Steps

- **[Basic Operations](BasicOperations.md)** - CRUD operations with entities
- **[Querying Data](QueryingData.md)** - Query and scan operations
- **[Global Secondary Indexes](../advanced-topics/GlobalSecondaryIndexes.md)** - Advanced GSI patterns
- **[Attribute Reference](../reference/AttributeReference.md)** - Complete attribute documentation

---

[Previous: Core Features](README.md) | [Next: Basic Operations](BasicOperations.md)

**See Also:**
- [First Entity Guide](../getting-started/FirstEntity.md)
- [Expression Formatting](ExpressionFormatting.md)
- [Composite Entities](../advanced-topics/CompositeEntities.md)
- [Troubleshooting](../reference/Troubleshooting.md)
