---
title: "Discriminators"
category: "advanced-topics"
order: 8
keywords: ["discriminator", "single-table", "multi-entity", "pattern matching", "entity type"]
related: ["CompositeEntities.md", "../core-features/EntityDefinition.md", "../reference/AttributeReference.md"]
---

[Documentation](../README.md) > [Advanced Topics](README.md) > Discriminators

# Discriminators

[Previous: STS Integration](STSIntegration.md) | [Next: Performance Optimization](PerformanceOptimization.md)

---

This guide covers the flexible discriminator system for identifying entity types in single-table DynamoDB designs.

## Overview

In single-table design, multiple entity types share the same DynamoDB table. Discriminators help identify which entity type each item represents. The library supports multiple discriminator strategies to accommodate various design patterns.

## Why Discriminators Matter

When querying a multi-entity table, you need to:
1. **Filter** items to only the entity type you want
2. **Validate** that items match the expected type
3. **Handle** type mismatches gracefully

The discriminator system provides compile-time configuration and runtime validation for these scenarios.

## Discriminator Strategies

### 1. Attribute-Based Discriminator

Store entity type in a dedicated attribute (traditional approach).

```csharp
[DynamoDbTable("entities",
    DiscriminatorProperty = "entity_type",
    DiscriminatorValue = "USER")]
public partial class User
{
    [PartitionKey]
    [DynamoDbAttribute("pk")]
    public string UserId { get; set; } = string.Empty;
    
    [DynamoDbAttribute("name")]
    public string Name { get; set; } = string.Empty;
}

[DynamoDbTable("entities",
    DiscriminatorProperty = "entity_type",
    DiscriminatorValue = "ORDER")]
public partial class Order
{
    [PartitionKey]
    [DynamoDbAttribute("pk")]
    public string OrderId { get; set; } = string.Empty;
    
    [DynamoDbAttribute("total")]
    public decimal Total { get; set; }
}
```

**DynamoDB Items:**
```json
// User item
{
  "pk": "USER#user123",
  "sk": "METADATA",
  "entity_type": "USER",
  "name": "John Doe"
}

// Order item
{
  "pk": "ORDER#order456",
  "sk": "METADATA",
  "entity_type": "ORDER",
  "total": 99.99
}
```

**Use Case:** Simple, explicit entity type identification. Good for tables with many entity types.

### 2. Sort Key Pattern Discriminator

Encode entity type in the sort key prefix.

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
    
    [DynamoDbAttribute("name")]
    public string Name { get; set; } = string.Empty;
}

[DynamoDbTable("entities",
    DiscriminatorProperty = "SK",
    DiscriminatorPattern = "ORDER#*")]
public partial class Order
{
    [PartitionKey]
    [DynamoDbAttribute("pk")]
    public string TenantId { get; set; } = string.Empty;
    
    [SortKey]
    [DynamoDbAttribute("sk")]
    public string SortKey { get; set; } = string.Empty;
    
    [DynamoDbAttribute("total")]
    public decimal Total { get; set; }
}
```

**DynamoDB Items:**
```json
// User item
{
  "pk": "TENANT#abc",
  "sk": "USER#user123",
  "name": "John Doe"
}

// Order item
{
  "pk": "TENANT#abc",
  "sk": "ORDER#order456",
  "total": 99.99
}
```

**Use Case:** Efficient for hierarchical data where entity type is naturally part of the sort key. Saves storage by not requiring a separate attribute.

### 3. Partition Key Pattern Discriminator

Encode entity type in the partition key.

```csharp
[DynamoDbTable("entities",
    DiscriminatorProperty = "PK",
    DiscriminatorPattern = "USER#*")]
public partial class User
{
    [PartitionKey]
    [DynamoDbAttribute("pk")]
    public string PartitionKey { get; set; } = string.Empty;
    
    [SortKey]
    [DynamoDbAttribute("sk")]
    public string SortKey { get; set; } = string.Empty;
}
```

**DynamoDB Item:**
```json
{
  "pk": "USER#user123",
  "sk": "METADATA",
  "name": "John Doe"
}
```

**Use Case:** When entity type is naturally part of the partition key structure.

### 4. Exact Match Discriminator

Match an exact sort key value for entity type.

```csharp
[DynamoDbTable("entities",
    DiscriminatorProperty = "SK",
    DiscriminatorValue = "METADATA")]
public partial class UserMetadata
{
    [PartitionKey]
    [DynamoDbAttribute("pk")]
    public string UserId { get; set; } = string.Empty;
    
    [SortKey]
    [DynamoDbAttribute("sk")]
    public string SortKey { get; set; } = "METADATA";
}
```

**DynamoDB Item:**
```json
{
  "pk": "USER#user123",
  "sk": "METADATA",
  "email": "john@example.com"
}
```

**Use Case:** Fixed sort key values for specific entity types in composite entity patterns.

## Pattern Matching

Discriminator patterns support wildcard matching for flexible entity identification.

### Pattern Syntax

| Pattern | Strategy | Description | Example Matches |
|---------|----------|-------------|-----------------|
| `USER#*` | StartsWith | Starts with prefix | `USER#123`, `USER#abc`, `USER#2024-01-15` |
| `*#USER` | EndsWith | Ends with suffix | `TENANT#abc#USER`, `ORG#xyz#USER` |
| `*#USER#*` | Contains | Contains substring | `TENANT#abc#USER#123`, `A#USER#B` |
| `USER` | ExactMatch | Exact match only | `USER` (no other values) |

### Pattern Examples

```csharp
// StartsWith pattern
[DynamoDbTable("entities",
    DiscriminatorProperty = "SK",
    DiscriminatorPattern = "USER#*")]
public partial class User { }
// Matches: USER#123, USER#abc, USER#2024-01-15

// EndsWith pattern
[DynamoDbTable("entities",
    DiscriminatorProperty = "SK",
    DiscriminatorPattern = "*#USER")]
public partial class User { }
// Matches: TENANT#abc#USER, ORG#xyz#USER

// Contains pattern
[DynamoDbTable("entities",
    DiscriminatorProperty = "SK",
    DiscriminatorPattern = "*#USER#*")]
public partial class User { }
// Matches: TENANT#abc#USER#123, PREFIX#USER#SUFFIX

// Exact match
[DynamoDbTable("entities",
    DiscriminatorProperty = "SK",
    DiscriminatorValue = "METADATA")]
public partial class Metadata { }
// Matches: METADATA only
```

### Performance

Pattern matching is optimized at compile-time:
- Patterns are analyzed during source generation
- Optimal string comparison methods are generated (StartsWith, EndsWith, Contains, Equals)
- No regular expressions or runtime parsing
- Zero allocations during matching

## GSI-Specific Discriminators

Different discriminators can be used for GSI queries when the GSI uses different key structures.

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
    
    // GSI uses different discriminator
    [GlobalSecondaryIndex("StatusIndex",
        IsPartitionKey = true,
        DiscriminatorProperty = "GSI1SK",
        DiscriminatorPattern = "USER#*")]
    [DynamoDbAttribute("status")]
    public string Status { get; set; } = string.Empty;
    
    [GlobalSecondaryIndex("StatusIndex", IsSortKey = true)]
    [DynamoDbAttribute("gsi1sk")]
    public string StatusSortKey { get; set; } = string.Empty;
}
```

**Behavior:**
- When querying the primary table, validates against `SK` with pattern `USER#*`
- When querying `StatusIndex`, validates against `GSI1SK` with pattern `USER#*`
- GSI discriminator overrides table-level discriminator for that specific index

## Discriminator Validation

### Automatic Validation

Discriminator validation occurs automatically during entity hydration:

```csharp
// Query returns items from multi-entity table
var response = await table.Query<User>()
    .Where($"{UserFields.TenantId} = {{0}}", "TENANT#abc")
    .ToListAsync();

// Each item is validated:
// 1. Check if discriminator property exists
// 2. Check if value matches pattern
// 3. Throw DiscriminatorMismatchException if validation fails
```

### Exception Handling

```csharp
using Oproto.FluentDynamoDb.Storage;

try
{
    var response = await table.Query<User>()
        .Where($"{UserFields.TenantId} = {{0}}", "TENANT#abc")
        .ToListAsync();
    
    var users = response.Items;
}
catch (DiscriminatorMismatchException ex)
{
    Console.WriteLine($"Expected: {ex.ExpectedDiscriminator}");
    Console.WriteLine($"Actual: {ex.ActualDiscriminator}");
    Console.WriteLine($"Entity Type: {ex.EntityType}");
}
```

### Projection Expressions

Discriminator properties are automatically included in projection expressions:

```csharp
// Generated projection expression includes discriminator
var response = await table.Query
    .Where($"{UserFields.TenantId} = {{0}}", "TENANT#abc")
    .WithProjectionExpression($"{UserFields.Name}, {UserFields.Email}")
    .ToListAsync();

// Actual projection: "name, email, sk" (sk is discriminator property)
```

## Migration from Legacy Discriminator

### Legacy Syntax (Deprecated)

```csharp
[DynamoDbTable("entities", EntityDiscriminator = "USER")]
public partial class User { }
```

### New Syntax (Recommended)

```csharp
[DynamoDbTable("entities",
    DiscriminatorProperty = "entity_type",
    DiscriminatorValue = "USER")]
public partial class User { }
```

### Migration Steps

1. **Identify legacy discriminators:**
   ```csharp
   // Old
   [DynamoDbTable("entities", EntityDiscriminator = "USER")]
   ```

2. **Update to new syntax:**
   ```csharp
   // New
   [DynamoDbTable("entities",
       DiscriminatorProperty = "entity_type",
       DiscriminatorValue = "USER")]
   ```

3. **Rebuild project** - source generator will create updated code

4. **Test** - behavior is functionally identical

### Backward Compatibility

- Legacy `EntityDiscriminator` is still supported
- Automatically maps to `DiscriminatorProperty="entity_type"` and `DiscriminatorValue`
- Compiler emits obsolescence warning
- No runtime behavior changes

## Best Practices

### 1. Choose the Right Strategy

```csharp
// ✅ Good - attribute-based for many entity types
[DynamoDbTable("entities",
    DiscriminatorProperty = "entity_type",
    DiscriminatorValue = "USER")]

// ✅ Good - sort key pattern for hierarchical data
[DynamoDbTable("entities",
    DiscriminatorProperty = "SK",
    DiscriminatorPattern = "USER#*")]

// ❌ Avoid - overly complex patterns
[DynamoDbTable("entities",
    DiscriminatorProperty = "SK",
    DiscriminatorPattern = "*#*#*#USER#*#*#*")]
```

### 2. Use Consistent Patterns

```csharp
// ✅ Good - consistent prefix pattern across entities
[DynamoDbTable("entities",
    DiscriminatorProperty = "SK",
    DiscriminatorPattern = "USER#*")]
public partial class User { }

[DynamoDbTable("entities",
    DiscriminatorProperty = "SK",
    DiscriminatorPattern = "ORDER#*")]
public partial class Order { }

[DynamoDbTable("entities",
    DiscriminatorProperty = "SK",
    DiscriminatorPattern = "PRODUCT#*")]
public partial class Product { }
```

### 3. Document Discriminator Strategy

```csharp
/// <summary>
/// User entity stored in multi-entity table.
/// Discriminator: SK starts with "USER#"
/// Example SK: USER#user123, USER#2024-01-15
/// </summary>
[DynamoDbTable("entities",
    DiscriminatorProperty = "SK",
    DiscriminatorPattern = "USER#*")]
public partial class User { }
```

### 4. Handle Validation Errors

```csharp
// ✅ Good - handle discriminator mismatches
try
{
    var users = await table.Query<User>()
        .Where($"{UserFields.TenantId} = {{0}}", "TENANT#abc")
        .ToListAsync();
}
catch (DiscriminatorMismatchException ex)
{
    _logger.LogWarning(ex, 
        "Discriminator mismatch: expected {Expected}, got {Actual}",
        ex.ExpectedDiscriminator, ex.ActualDiscriminator);
    // Handle gracefully - maybe data corruption or wrong entity type
}
```

### 5. Test Discriminator Patterns

```csharp
[Fact]
public async Task Query_WithDiscriminator_ReturnsOnlyMatchingEntities()
{
    // Arrange - insert mixed entity types
    await table.Put.WithItem(new User { TenantId = "TENANT#abc", SortKey = "USER#user1" }).PutAsync();
    await table.Put.WithItem(new Order { TenantId = "TENANT#abc", SortKey = "ORDER#order1" }).PutAsync();
    
    // Act - query for users only
    var response = await table.Query<User>()
        .Where($"{UserFields.TenantId} = {{0}}", "TENANT#abc")
        .ToListAsync();
    
    // Assert - only users returned
    Assert.All(response.Items, user => 
        Assert.StartsWith("USER#", user.SortKey));
}
```

## Common Patterns

### Multi-Tenant with Entity Type

```csharp
[DynamoDbTable("multi-tenant",
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
}

// Query: All users for tenant
var users = await table.Query<User>()
    .Where($"{UserFields.TenantId} = {{0}}", "TENANT#abc")
    .ToListAsync();
```

### Hierarchical Entities

```csharp
[DynamoDbTable("hierarchy",
    DiscriminatorProperty = "SK",
    DiscriminatorPattern = "*#USER")]
public partial class User
{
    [PartitionKey]
    [DynamoDbAttribute("pk")]
    public string PartitionKey { get; set; } = string.Empty;
    
    [SortKey]
    [DynamoDbAttribute("sk")]
    public string SortKey { get; set; } = string.Empty;
}

// Matches: ORG#org1#DEPT#dept2#USER
```

### Composite Entity with Metadata

```csharp
[DynamoDbTable("orders",
    DiscriminatorProperty = "SK",
    DiscriminatorValue = "METADATA")]
public partial class OrderMetadata
{
    [PartitionKey]
    [DynamoDbAttribute("pk")]
    public string OrderId { get; set; } = string.Empty;
    
    [SortKey]
    [DynamoDbAttribute("sk")]
    public string SortKey { get; set; } = "METADATA";
}

[DynamoDbTable("orders",
    DiscriminatorProperty = "SK",
    DiscriminatorPattern = "ITEM#*")]
public partial class OrderItem
{
    [PartitionKey]
    [DynamoDbAttribute("pk")]
    public string OrderId { get; set; } = string.Empty;
    
    [SortKey]
    [DynamoDbAttribute("sk")]
    public string SortKey { get; set; } = string.Empty;
}
```

## Troubleshooting

### Discriminator Mismatch Exception

**Problem:** `DiscriminatorMismatchException` thrown during query

**Causes:**
1. Wrong entity type for query results
2. Data corruption or migration issues
3. Incorrect discriminator configuration

**Solutions:**
```csharp
// Check discriminator configuration
[DynamoDbTable("entities",
    DiscriminatorProperty = "SK",  // Verify property name
    DiscriminatorPattern = "USER#*")]  // Verify pattern

// Verify DynamoDB data
// Expected: sk = "USER#user123"
// Actual: sk = "ORDER#order456" (wrong entity type)

// Handle gracefully
try
{
    var users = await query.ToListAsync();
}
catch (DiscriminatorMismatchException ex)
{
    _logger.LogError(ex, "Discriminator mismatch");
    // Investigate data or configuration
}
```

### Pattern Not Matching

**Problem:** Pattern doesn't match expected items

**Causes:**
1. Incorrect wildcard placement
2. Wrong separator in pattern
3. Case sensitivity issues

**Solutions:**
```csharp
// ❌ Wrong - missing wildcard
DiscriminatorPattern = "USER#"  // Matches exactly "USER#"

// ✅ Correct - with wildcard
DiscriminatorPattern = "USER#*"  // Matches "USER#123", "USER#abc"

// ❌ Wrong - case mismatch
DiscriminatorPattern = "user#*"  // Won't match "USER#123"

// ✅ Correct - match case in data
DiscriminatorPattern = "USER#*"  // Matches "USER#123"
```

### Missing Discriminator Property

**Problem:** Discriminator property not found in items

**Causes:**
1. Property name typo
2. Items don't have discriminator attribute
3. Projection expression excludes discriminator

**Solutions:**
```csharp
// Verify property name matches DynamoDB attribute
[DynamoDbTable("entities",
    DiscriminatorProperty = "sk")]  // Must match actual attribute name

// Discriminator automatically included in projections
// No manual action needed
```

## Next Steps

- **[Composite Entities](CompositeEntities.md)** - Use discriminators with composite entities
- **[Global Secondary Indexes](GlobalSecondaryIndexes.md)** - GSI-specific discriminators
- **[Entity Definition](../core-features/EntityDefinition.md)** - Complete entity configuration
- **[Attribute Reference](../reference/AttributeReference.md)** - Discriminator attribute details

---

[Previous: STS Integration](STSIntegration.md) | [Next: Performance Optimization](PerformanceOptimization.md)

**See Also:**
- [Entity Definition](../core-features/EntityDefinition.md)
- [Attribute Reference](../reference/AttributeReference.md)
- [Composite Entities](CompositeEntities.md)
- [Error Handling](../reference/ErrorHandling.md)
