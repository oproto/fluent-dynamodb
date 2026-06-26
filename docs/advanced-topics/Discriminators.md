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

## Auto-Derived Discriminators from Key Formats

The source generator can automatically derive discriminator patterns from key format configurations, eliminating the need to manually specify `DiscriminatorPattern` in most cases.

### How It Works

When a key property has a prefix (via `[PartitionKey(Prefix = "...")]` or `[SortKey(Prefix = "...")]`) or a computed format, the generator:

1. Normalizes the key format into a format string (e.g., `"ORDER#{0}"`)
2. Replaces all `{N}` placeholders with `*` wildcards to get the discriminator pattern (e.g., `"ORDER#*"`)
3. Selects the best key property for discrimination (sort key preferred over partition key)
4. Populates the entity's `DiscriminatorConfig` automatically

### Before and After

```csharp
// BEFORE: Manual discriminator specification (redundant with key format)
[DynamoDbTable("orders", DiscriminatorProperty = "sk", DiscriminatorPattern = "ORDER#*")]
public partial class Order
{
    [PartitionKey(Prefix = "CUSTOMER")]
    [DynamoDbAttribute("pk")]
    public string Pk { get; set; } = string.Empty;

    [SortKey(Prefix = "ORDER")]
    [DynamoDbAttribute("sk")]
    public string Sk { get; set; } = string.Empty;
}

// AFTER: Auto-derived — no manual discriminator needed
[DynamoDbTable("orders")]
public partial class Order
{
    [PartitionKey(Prefix = "CUSTOMER")]
    [DynamoDbAttribute("pk")]
    public string Pk { get; set; } = string.Empty;

    [SortKey(Prefix = "ORDER")]
    [DynamoDbAttribute("sk")]
    public string Sk { get; set; } = string.Empty;
    // Auto-derived: DiscriminatorProperty = "sk", DiscriminatorPattern = "ORDER#*"
}
```

### Derivation Rules

| Key Configuration | NormalizedKeyFormat | Derived Pattern |
|---|---|---|
| `[SortKey(Prefix = "ORDER")]` | `"ORDER#{0}"` | `"ORDER#*"` |
| `[SortKey(Prefix = "USER", Separator = "_")]` | `"USER_{0}"` | `"USER_*"` |
| `[PartitionKey(Prefix = "CUST")]` | `"CUST#{0}"` | `"CUST#*"` |
| `[SortKey]` (no prefix) | `"{0}"` | `null` (no discrimination) |
| `[Computed("A", "B", Separator = "#")]` + `Prefix = "TENANT"` | `"TENANT#{0}#{1}"` | `"TENANT#*#*"` |
| `[Computed("A", "B", Format = "TENANT#{0}#USER#{1}")]` | `"TENANT#{0}#USER#{1}"` | `"TENANT#*#USER#*"` |

### Selection Priority

When both partition key and sort key have derivable patterns, the sort key is preferred because sort keys typically carry entity-type semantics in single-table designs:

```csharp
[DynamoDbTable("shared-table")]
public partial class Order
{
    [PartitionKey(Prefix = "CUSTOMER")]  // Derives "CUSTOMER#*"
    [DynamoDbAttribute("pk")]
    public string Pk { get; set; } = string.Empty;

    [SortKey(Prefix = "ORDER")]  // Derives "ORDER#*" ← selected for discrimination
    [DynamoDbAttribute("sk")]
    public string Sk { get; set; } = string.Empty;
}
// Generated MatchesEntity checks: item["sk"].S.StartsWith("ORDER#")
```

If the sort key has no useful pattern (no prefix, format is `"{0}"`), the partition key pattern is used as a fallback.

### When Auto-Derivation Does Not Apply

Auto-derivation is skipped when:
- The entity already has an explicit `DiscriminatorProperty`/`DiscriminatorPattern`/`DiscriminatorValue` on `[DynamoDbTable]`
- All key properties have trivial formats (`"{0}"` — no prefix, no computed format)
- The derived pattern would start with `*` (no useful fixed prefix for discrimination)

### Explicit Discriminators Still Supported

Explicit discriminators are never overridden by auto-derivation. They remain necessary when:
- Discrimination is based on a non-key attribute (e.g., `entity_type`)
- The key format doesn't provide sufficient discrimination
- You need `DiscriminatorValue` for exact match semantics

```csharp
// Explicit discriminator on non-key attribute — auto-derivation skipped
[DynamoDbTable("entities",
    DiscriminatorProperty = "entity_type",
    DiscriminatorValue = "ORDER")]
public partial class Order
{
    [PartitionKey]
    [DynamoDbAttribute("pk")]
    public string Pk { get; set; } = string.Empty;
}
```

### GSI Auto-Derivation

GSI discriminators are also auto-derived when the GSI partition key property has a derivable pattern:

```csharp
[DynamoDbTable("orders")]
public partial class Order
{
    [PartitionKey(Prefix = "CUSTOMER")]
    [DynamoDbAttribute("pk")]
    public string Pk { get; set; } = string.Empty;

    [SortKey(Prefix = "ORDER")]
    [DynamoDbAttribute("sk")]
    public string Sk { get; set; } = string.Empty;

    [GsiPartitionKey("status-index")]
    [DynamoDbAttribute("gsi1pk")]
    public string StatusKey { get; set; } = string.Empty;
    // If StatusKey has a NormalizedKeyFormat with a prefix (e.g., from a Prefix on
    // a co-located key attribute or a Computed format), the GSI discriminator is
    // auto-derived. Otherwise, no GSI discriminator is populated.
}
```

### Single-Entity Tables

For tables with only one entity, the pattern is still derived and stored internally (available for key building and metadata), but `MatchesEntity` generation may use the derived discriminator for stricter filtering. This is safe — the discriminator pattern always matches all items produced by the entity's own key format.

## Compile-Time Diagnostics: FDDB100–FDDB103

The source generator emits diagnostics when key format and discriminator configurations conflict or are redundant.

### FDDB100 — Prefix Conflicts with Computed Format (Error)

Emitted when a key property has both a `Prefix` on its key attribute and an explicit `Format` on `[Computed]` that doesn't start with the expected prefix+separator.

```csharp
// ❌ FDDB100: Property 'Pk' has Prefix='ORDER' (expecting format to start with 'ORDER#')
//            but ComputedAttribute.Format='TENANT#{0}#{1}' does not match
[PartitionKey(Prefix = "ORDER")]
[DynamoDbAttribute("pk")]
[Computed("CustomerId", "OrderId", Format = "TENANT#{0}#{1}")]
public string Pk { get; set; } = string.Empty;

// ✅ Fix: Align the format with the prefix
[PartitionKey(Prefix = "ORDER")]
[DynamoDbAttribute("pk")]
[Computed("CustomerId", "OrderId", Format = "ORDER#{0}#{1}")]
public string Pk { get; set; } = string.Empty;

// ✅ Or remove the prefix (let the format be the sole definition)
[PartitionKey]
[DynamoDbAttribute("pk")]
[Computed("CustomerId", "OrderId", Format = "TENANT#{0}#{1}")]
public string Pk { get; set; } = string.Empty;
```

**Not emitted when:**
- Prefix is null or empty
- No explicit `Format` on `[Computed]` (separator-based concatenation is fine)
- Format starts with the expected `"{Prefix}{Separator}"` string

### FDDB101 — Explicit Discriminator Conflicts with Key Format (Error)

Emitted when an explicit `DiscriminatorPattern` on `[DynamoDbTable]` references a key property whose derived pattern is different.

```csharp
// ❌ FDDB101: Entity 'Order' specifies DiscriminatorPattern on attribute 'sk' as 'USER#*'
//            but the key format derives pattern 'ORDER#*'
[DynamoDbTable("orders", DiscriminatorProperty = "sk", DiscriminatorPattern = "USER#*")]
public partial class Order
{
    [SortKey(Prefix = "ORDER")]
    [DynamoDbAttribute("sk")]
    public string Sk { get; set; } = string.Empty;
}

// ✅ Fix: Remove explicit discriminator (let it auto-derive)
[DynamoDbTable("orders")]
public partial class Order
{
    [SortKey(Prefix = "ORDER")]
    [DynamoDbAttribute("sk")]
    public string Sk { get; set; } = string.Empty;
}

// ✅ Or align the explicit pattern with the key format
[DynamoDbTable("orders", DiscriminatorProperty = "sk", DiscriminatorPattern = "ORDER#*")]
public partial class Order
{
    [SortKey(Prefix = "ORDER")]
    [DynamoDbAttribute("sk")]
    public string Sk { get; set; } = string.Empty;
}
```

**Not emitted when:**
- The discriminator is auto-derived (not explicit)
- The explicit pattern matches the derived pattern exactly (→ FDDB103 info instead)
- The key property's derived pattern is null (trivial key — explicit supplements it)
- The `DiscriminatorProperty` doesn't match any key property's DynamoDB attribute name

### FDDB102 — Overlapping Auto-Derived Patterns (Warning)

Emitted when two entities on the same table both have auto-derived discriminator patterns that overlap with different specificity. This is advisory — exclusion guards are still generated.

```csharp
// ⚠️ FDDB102: Entities 'Order' and 'OrderLine' have overlapping auto-derived patterns
//            'ORDER#*' and 'ORDER#*#LINE#*' on attribute 'sk' — consider adding more
//            specificity to key formats
[DynamoDbTable("shared")]
public partial class Order
{
    [SortKey(Prefix = "ORDER")]
    [DynamoDbAttribute("sk")]
    public string Sk { get; set; } = string.Empty;
    // Auto-derived: "ORDER#*"
}

[DynamoDbTable("shared")]
public partial class OrderLine
{
    [SortKey]
    [DynamoDbAttribute("sk")]
    [Computed("OrderId", "LineId", Format = "ORDER#{0}#LINE#{1}")]
    public string Sk { get; set; } = string.Empty;
    // Auto-derived: "ORDER#*#LINE#*"
}
```

The generator still produces correct exclusion guards (see [Overlapping Pattern Resolution](#overlapping-pattern-resolution) above). FDDB102 encourages you to consider whether the overlap is intentional.

**Not emitted when:**
- One or both patterns are explicit (manually specified) — only auto-derived pairs trigger this
- Both patterns have the same specificity score (→ DISC004 error instead)

### FDDB103 — Redundant Explicit Discriminator (Info)

Emitted when an explicit `DiscriminatorPattern` exactly matches what would be auto-derived from the key format. The explicit specification can be safely removed.

```csharp
// ℹ️ FDDB103: Entity 'Order' specifies DiscriminatorPattern='ORDER#*' which is
//            automatically derivable from the key format — the explicit specification
//            can be removed
[DynamoDbTable("orders", DiscriminatorProperty = "sk", DiscriminatorPattern = "ORDER#*")]
public partial class Order
{
    [SortKey(Prefix = "ORDER")]
    [DynamoDbAttribute("sk")]
    public string Sk { get; set; } = string.Empty;
}

// ✅ Simplified: remove redundant explicit discriminator
[DynamoDbTable("orders")]
public partial class Order
{
    [SortKey(Prefix = "ORDER")]
    [DynamoDbAttribute("sk")]
    public string Sk { get; set; } = string.Empty;
}
```

**Not emitted when:**
- The entity uses `DiscriminatorValue` (exact match) — these are never considered redundant
- The explicit pattern differs from the derived pattern (→ FDDB101 error instead)
- The discriminator is already auto-derived

### Diagnostic Summary

| Code | Severity | Description | Action Required |
|------|----------|-------------|-----------------|
| FDDB100 | Error | Prefix conflicts with explicit computed format | Fix the conflict (align prefix with format, or remove one) |
| FDDB101 | Error | Explicit discriminator contradicts key format | Fix the conflict (remove explicit, or align it) |
| FDDB102 | Warning | Overlapping auto-derived patterns | Advisory — consider more specific key formats |
| FDDB103 | Info | Redundant explicit discriminator | Optional cleanup — remove the explicit specification |

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

**Behavior:**
- When querying the primary table, validates against `SK` with pattern `USER#*`
- When querying `StatusIndex`, validates against `GSI1SK` with pattern `USER#*`
- GSI discriminator overrides table-level discriminator for that specific index

## Discriminator Validation

### Automatic Validation

Discriminator validation occurs automatically during entity hydration:

```csharp
// Query returns items from multi-entity table
var users = await table.Query<User>()
    .Where($"{UserFields.TenantId} = {{0}}", "TENANT#abc")
    .ToListAsync();

// Each item is validated:
// 1. Check if discriminator property exists
// 2. Check if value matches pattern
// 3. Throw DiscriminatorMismatchException if validation fails
```

### Exception Handling

```csharp
using Oproto.FluentDynamoDb.Mapping;

try
{
    var users = await table.Query<User>()
        .Where($"{UserFields.TenantId} = {{0}}", "TENANT#abc")
        .ToListAsync();
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
var users = await table.Query<User>()
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
    await table.Users.PutAsync(new User { TenantId = "TENANT#abc", SortKey = "USER#user1" });
    await table.Orders.PutAsync(new Order { TenantId = "TENANT#abc", SortKey = "ORDER#order1" });
    
    // Act - query for users only
    var users = await table.Query<User>()
        .Where($"{UserFields.TenantId} = {{0}}", "TENANT#abc")
        .ToListAsync();
    
    // Assert - only users returned
    Assert.All(users, user => 
        Assert.StartsWith("USER#", user.SortKey));
}
```

### 6. Always Configure Discriminators on Multi-Entity Tables

> **Important:** On multi-entity tables (multiple entity classes sharing the same `[DynamoDbTable]` name), the generated `MatchesEntity()` method uses only key attribute presence (partition key and sort key) to determine entity type membership when no discriminator is configured. This means any item with matching key attributes will pass the filter — including items belonging to a *different* entity type on the same table.

Without a discriminator, queries may return items from the wrong entity type, leading to hydration errors or incorrect data. Always configure a discriminator when multiple entities share a table:

```csharp
// ❌ RISKY: No discriminator on a multi-entity table
// Items from Order, Invoice, or any other entity with the same key structure
// will all pass the MatchesEntity check for User
[DynamoDbTable("shared-table")]
public partial class User { ... }

[DynamoDbTable("shared-table")]
public partial class Order { ... }

// ✅ CORRECT: Explicit discriminator prevents cross-type contamination
[DynamoDbTable("shared-table",
    DiscriminatorProperty = "entity_type",
    DiscriminatorValue = "USER")]
public partial class User { ... }

[DynamoDbTable("shared-table",
    DiscriminatorProperty = "entity_type",
    DiscriminatorValue = "ORDER")]
public partial class Order { ... }

// ✅ ALSO CORRECT: Sort key pattern as discriminator
[DynamoDbTable("shared-table",
    DiscriminatorProperty = "sk",
    DiscriminatorPattern = "USER#*")]
public partial class User { ... }

[DynamoDbTable("shared-table",
    DiscriminatorProperty = "sk",
    DiscriminatorPattern = "ORDER#*")]
public partial class Order { ... }
```

Single-entity tables (only one entity class references the table name) do not need a discriminator — key attribute presence is sufficient for type identification.

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

## Overlapping Pattern Resolution

When multiple entities on the same table have discriminator patterns that could match the same string value, the source generator automatically resolves the ambiguity using **most-specific pattern matching**. This eliminates the need for a dedicated `entity_type` attribute in hierarchical sort key designs.

### How Specificity Scoring Works

The source generator computes a specificity score for each discriminator pattern at compile time:

1. Split the pattern string on the `*` character
2. Count the number of resulting non-empty literal segments
3. The higher the count, the more specific the pattern

| Pattern | Segments after split | Non-empty literals | Score |
|---------|---------------------|--------------------|-------|
| `INVOICE#*` | `["INVOICE#", ""]` | `["INVOICE#"]` | 1 |
| `INVOICE#*#LINE#*` | `["INVOICE#", "#LINE#", ""]` | `["INVOICE#", "#LINE#"]` | 2 |
| `*#AUDIT` | `["", "#AUDIT"]` | `["#AUDIT"]` | 1 |
| `A#*#B#*#C#*` | `["A#", "#B#", "#C#", ""]` | `["A#", "#B#", "#C#"]` | 3 |

**ExactMatch always wins:** A `DiscriminatorValue` (exact match) is assigned the maximum possible score, so it always takes precedence over any wildcard pattern regardless of segment count.

### Example: Invoice / InvoiceLine Hierarchy

Consider a table with invoices and their line items, using hierarchical sort keys:

```csharp
[DynamoDbTable("invoices",
    DiscriminatorProperty = "sk",
    DiscriminatorPattern = "INVOICE#*")]
public partial class Invoice
{
    [PartitionKey]
    [DynamoDbAttribute("pk")]
    public string Pk { get; set; } = string.Empty;

    [SortKey]
    [DynamoDbAttribute("sk")]
    public string Sk { get; set; } = string.Empty;

    [DynamoDbAttribute("invoiceNumber")]
    public string InvoiceNumber { get; set; } = string.Empty;
}

[DynamoDbTable("invoices",
    DiscriminatorProperty = "sk",
    DiscriminatorPattern = "INVOICE#*#LINE#*")]
public partial class InvoiceLine
{
    [PartitionKey]
    [DynamoDbAttribute("pk")]
    public string Pk { get; set; } = string.Empty;

    [SortKey]
    [DynamoDbAttribute("sk")]
    public string Sk { get; set; } = string.Empty;

    [DynamoDbAttribute("amount")]
    public decimal Amount { get; set; }
}
```

**DynamoDB Items:**
```json
{ "pk": "CUSTOMER#C1", "sk": "INVOICE#INV-001", "invoiceNumber": "INV-001" }
{ "pk": "CUSTOMER#C1", "sk": "INVOICE#INV-001#LINE#1", "amount": 50.00 }
{ "pk": "CUSTOMER#C1", "sk": "INVOICE#INV-001#LINE#2", "amount": 75.00 }
```

Both patterns start with `INVOICE#`, so the sort key `INVOICE#INV-001#LINE#1` would match both `INVOICE#*` (StartsWith "INVOICE#") and `INVOICE#*#LINE#*` (StartsWith "INVOICE#" + Contains "#LINE#"). The source generator resolves this:

- `INVOICE#*` → score 1
- `INVOICE#*#LINE#*` → score 2 (more specific)

No `entity_type` attribute is required. The generated code handles disambiguation automatically.

### Generated Exclusion Guards

For the less-specific entity (`Invoice`), the source generator emits an **exclusion guard** — an additional check that returns `false` if the value also matches a more-specific pattern:

**Generated `Invoice.MatchesEntity`** (simplified):
```csharp
public static bool MatchesEntity(Dictionary<string, AttributeValue> item)
{
    if (!item.TryGetValue("sk", out var discriminatorValue) || discriminatorValue.S == null)
        return false;

    // Positive match: this entity's pattern (INVOICE#*)
    if (!discriminatorValue.S.StartsWith("INVOICE#"))
        return false;

    // Exclusion: more-specific pattern from InvoiceLine (INVOICE#*#LINE#*)
    if (discriminatorValue.S.Contains("#LINE#"))
        return false;

    return true;
}
```

**Generated `InvoiceLine.MatchesEntity`** (most-specific — no exclusion needed):
```csharp
public static bool MatchesEntity(Dictionary<string, AttributeValue> item)
{
    if (!item.TryGetValue("sk", out var discriminatorValue) || discriminatorValue.S == null)
        return false;

    return discriminatorValue.S.StartsWith("INVOICE#") && discriminatorValue.S.Contains("#LINE#");
}
```

The exclusion guard ensures that each DynamoDB item is claimed by exactly one entity type. The less-specific entity excludes values that match more-specific patterns, while the most-specific entity requires no exclusion logic.

For multi-level hierarchies (three or more overlapping patterns), each entity excludes all patterns with a higher specificity score than its own.

### Compile-Time Diagnostics

The source generator emits diagnostics to keep you informed about overlap resolution:

| ID | Severity | Description |
|----|----------|-------------|
| `DISC004` | Error | Two overlapping patterns have the **same** specificity score. The generator cannot determine precedence — you must resolve the ambiguity (e.g., add more structure to one pattern or use a dedicated `DiscriminatorValue`). |
| `DISC005` | Info | Overlapping patterns detected and automatically resolved by specificity ordering. No action required — this is informational. |
| `DISC006` | Error | A computed exclusion guard is **tautological** — it uses the same strategy and literal as the entity's own positive match. The generated `MatchesEntity` would always return `false`. Redesign your discriminator patterns. |

**Example DISC004 error:**
```csharp
// ❌ AMBIGUOUS: both patterns have score 1
[DynamoDbTable("entities", DiscriminatorProperty = "sk", DiscriminatorPattern = "INVOICE#*")]
public partial class Invoice { ... }

[DynamoDbTable("entities", DiscriminatorProperty = "sk", DiscriminatorPattern = "*#PENDING")]
public partial class PendingItem { ... }
// A value like "INVOICE#123#PENDING" could match both — DISC004 emitted
```

**Resolving DISC004:**
- Add more literal structure to one pattern so scores differ
- Use `DiscriminatorValue` (ExactMatch) for one entity
- Use different `DiscriminatorProperty` values so patterns don't overlap

**Example DISC006 error:**
```csharp
// ❌ TAUTOLOGICAL: exclusion guard would contradict positive match
[DynamoDbTable("entities", DiscriminatorProperty = "sk", DiscriminatorPattern = "*#ROLE#*")]
public partial class RoleItem { ... }

[DynamoDbTable("entities", DiscriminatorProperty = "sk", DiscriminatorPattern = "USER#*#ROLE#*")]
public partial class UserRole { ... }
// RoleItem uses Contains("#ROLE#") as its positive check.
// The computed exclusion from UserRole is also Contains("#ROLE#") — identical!
// This would make RoleItem.MatchesEntity always return false — DISC006 emitted
```

This happens when a Contains-strategy entity (e.g., `*#ROLE#*`) overlaps with a Complex-strategy entity (e.g., `USER#*#ROLE#*`) that shares the same internal segment. The exclusion extraction heuristic produces the same literal that the less-specific entity already uses for its positive match.

**Resolving DISC006:**
- Use a `StartsWith` pattern for the less-specific entity (e.g., `USER#*` instead of `*#ROLE#*`) so the positive match and exclusion use different strategies/literals
- Use a dedicated `DiscriminatorValue` (ExactMatch) for one of the entities
- Redesign your key structure so the overlapping entities use distinct discriminator segments

### When Overlap Resolution Does NOT Apply

Overlap analysis only applies when:
- Both entities are in the same table group (same `[DynamoDbTable]` name)
- Both entities use the same `DiscriminatorProperty`
- Both patterns could match the same string value

Entities with different `DiscriminatorProperty` values are never considered overlapping, regardless of pattern content.

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
