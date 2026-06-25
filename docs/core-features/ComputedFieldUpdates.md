---
title: "Computed Field Updates"
category: "core-features"
order: 36
keywords: ["computed", "update", "source properties", "recomputation", "GSI", "FDDB071", "FDDB072", "FDDB073"]
related: ["ExpressionBasedUpdates.md", "EntityDefinition.md"]
---

[Documentation](../README.md) > [Core Features](README.md) > Computed Field Updates

# Computed Field Updates

Source-property-based update patterns for computed fields, with automatic recomputation and compile-time safety.

---

## Table of Contents

- [Overview](#overview)
- [Update Model Property Exclusions](#update-model-property-exclusions)
- [Source-Property-Based Updates](#source-property-based-updates)
- [Direct Assignment](#direct-assignment)
- [Diagnostics](#diagnostics)
- [Examples](#examples)
- [Best Practices](#best-practices)

---

## Overview

Computed fields (properties decorated with `[Computed]`) combine multiple source properties into a single DynamoDB attribute value. When a computed field is used as a GSI key, you often need to update that key when its constituent values change.

The update model redesign provides two ways to update non-key computed fields:

1. **Direct assignment** — Set the computed field to an explicit value
2. **Source-property-based update** — Set the source properties and let the framework recompute the concatenated value

Key properties (partition key, sort key) and their related source/extracted properties are excluded from update models entirely, providing compile-time safety against invalid update attempts.

---

## Update Model Property Exclusions

The source generator excludes certain properties from generated update model classes. Attempting to set these properties results in a compile error rather than a runtime exception.

### Excluded Properties

| Property Type | Reason |
|--------------|--------|
| `[PartitionKey]` properties | DynamoDB does not allow updating key attributes |
| `[SortKey]` properties | DynamoDB does not allow updating key attributes |
| `[Extracted]` properties of key fields | Derived from keys, no independent DynamoDB attribute |
| Source properties of key-based computed fields | Part of a key computation, cannot be updated independently |

### Example

```csharp
[DynamoDbTable("Products")]
public partial class Product
{
    [PartitionKey]
    [DynamoDbAttribute("pk")]
    [Computed(nameof(Department), nameof(Category), Separator = "#")]
    public string Pk { get; set; } = string.Empty;

    [Extracted(nameof(Pk), 0)]
    public string Department { get; set; } = string.Empty;

    [Extracted(nameof(Pk), 1)]
    public string Category { get; set; } = string.Empty;

    [SortKey]
    [DynamoDbAttribute("sk")]
    public string Sk { get; set; } = string.Empty;

    // Non-key computed field (GSI partition key)
    [GsiPartitionKey("gsi1")]
    [DynamoDbAttribute("gsi1pk")]
    [Computed(nameof(Status), nameof(Region), Separator = "#")]
    public string Gsi1Pk { get; set; } = string.Empty;

    [Extracted(nameof(Gsi1Pk), 0)]
    public string Status { get; set; } = string.Empty;

    [Extracted(nameof(Gsi1Pk), 1)]
    public string Region { get; set; } = string.Empty;

    [DynamoDbAttribute("name")]
    public string Name { get; set; } = string.Empty;

    [DynamoDbAttribute("price")]
    public decimal Price { get; set; }
}
```

The generated `ProductUpdateModel` includes:
- ✅ `Gsi1Pk` — non-key computed field (direct assignment)
- ✅ `Status` — source property of non-key computed field
- ✅ `Region` — source property of non-key computed field
- ✅ `Name` — regular property
- ✅ `Price` — regular property
- ❌ `Pk` — partition key (excluded)
- ❌ `Sk` — sort key (excluded)
- ❌ `Department` — source property of key-based computed field (excluded)
- ❌ `Category` — source property of key-based computed field (excluded)

---

## Source-Property-Based Updates

When you set all source properties of a non-key computed field, the expression translator automatically recomputes the concatenated value and generates a SET expression targeting the computed field's DynamoDB attribute.

```csharp
// Set the source properties — the computed field is automatically recomputed
await table.Products.Update(pk, sk)
    .Set(x => new ProductUpdateModel
    {
        Status = "Active",
        Region = "US-East"
    })
    .UpdateAsync();

// Generates: SET #gsi1pk = :p0, #status = :p1, #region = :p2
// Where :p0 = "Active#US-East", :p1 = "Active", :p2 = "US-East"
// (source properties with their own [DynamoDbAttribute] are also updated)
```

### How It Works

1. The translator detects that `Status` and `Region` are source properties of the `Gsi1Pk` computed field
2. It validates that all source properties are assigned (FDDB072)
3. It validates no entity parameter references are used (FDDB071)
4. It concatenates the values in order using the configured separator
5. It generates a SET expression for the computed field's DynamoDB attribute
6. For source properties that have their own `[DynamoDbAttribute]` (standalone DynamoDB columns), it also generates individual SET expressions to keep them in sync
7. Source properties without a `[DynamoDbAttribute]` (purely virtual) do not generate individual SET operations

### Prefix Handling

If the computed field has a configured prefix (from `[PartitionKey(Prefix = "...")]` or `[SortKey(Prefix = "...")]`), the prefix is automatically prepended during recomputation:

```csharp
// If Gsi1Pk had Prefix = "PRODUCT":
// Result would be: "PRODUCT#Active#US-East"
```

---

## Direct Assignment

You can still assign the computed field directly if you already have the final value:

```csharp
// Direct assignment — you provide the complete value
await table.Products.Update(pk, sk)
    .Set(x => new ProductUpdateModel
    {
        Gsi1Pk = "Active#US-East"
    })
    .UpdateAsync();

// Generates: SET #gsi1pk = :p0
// Where :p0 = "Active#US-East"
```

This is useful when you already have the pre-computed value or when migrating existing code.

---

## Diagnostics

Three diagnostics enforce correctness when updating computed fields via source properties. These are thrown as `InvalidOperationException` during expression translation (before any DynamoDB call is made).

### FDDB071: Entity Parameter Reference in Source Property

**Message:** `Source properties of computed fields must be assigned constant or local values. '{PropertyName}' references the entity parameter, but computed fields are evaluated client-side.`

**Cause:** You assigned a source property using a value derived from the entity parameter (`x`). Computed field recomputation happens client-side during expression translation, so all source values must be known at that time.

```csharp
// ❌ Invalid — references entity parameter
.Set(x => new ProductUpdateModel
{
    Status = x.Status,  // FDDB071: references entity parameter
    Region = "US-East"
})

// ✅ Valid — uses local variables or constants
var newStatus = "Active";
.Set(x => new ProductUpdateModel
{
    Status = newStatus,
    Region = "US-East"
})
```

### FDDB072: Partial Source Property Assignment

**Message:** `All source properties of computed field '{ComputedFieldName}' must be specified when updating via sources. Missing: {MissingProperties}`

**Cause:** You assigned some but not all source properties of a computed field. The framework cannot recompute the correct concatenated value without all components.

```csharp
// ❌ Invalid — only Status assigned, Region is missing
.Set(x => new ProductUpdateModel
{
    Status = "Active"
    // Missing: Region
})
// Throws: "All source properties of computed field 'Gsi1Pk' must be specified
//          when updating via sources. Missing: Region"

// ✅ Valid — all source properties assigned
.Set(x => new ProductUpdateModel
{
    Status = "Active",
    Region = "US-East"
})
```

### FDDB073: Mixed Direct and Source-Based Assignment

**Message:** `Cannot set both computed field '{ComputedFieldName}' and its source properties in the same update expression. Use one approach or the other.`

**Cause:** You assigned both the computed field directly and one or more of its source properties in the same expression. Choose one approach: either set the computed field directly, or set all source properties.

```csharp
// ❌ Invalid — sets both computed field and source properties
.Set(x => new ProductUpdateModel
{
    Gsi1Pk = "Active#US-East",
    Status = "Active",    // FDDB073
    Region = "US-East"
})

// ✅ Valid — direct assignment only
.Set(x => new ProductUpdateModel
{
    Gsi1Pk = "Active#US-East"
})

// ✅ Valid — source properties only
.Set(x => new ProductUpdateModel
{
    Status = "Active",
    Region = "US-East"
})
```

---

## Examples

### Updating a GSI Key via Source Properties

```csharp
// Move a product to a different category and region
await table.Products.Update(Product.Keys.Pk("Electronics", "Phones"), "META")
    .Set(x => new ProductUpdateModel
    {
        Status = "Clearance",
        Region = "US-West",
        Price = 29.99m  // Regular property update alongside computed field sources
    })
    .UpdateAsync();

// Generates:
// SET #gsi1pk = :p0, #status = :p1, #region = :p2, #price = :p3
// Where :p0 = "Clearance#US-West", :p1 = "Clearance", :p2 = "US-West", :p3 = 29.99
```

### Multiple Independent Computed Fields

Each computed field is validated independently. A violation on one does not affect others:

```csharp
[DynamoDbTable("Events")]
public partial class Event
{
    // ... keys ...

    [GsiPartitionKey("gsi1")]
    [DynamoDbAttribute("gsi1pk")]
    [Computed(nameof(EventType), nameof(Status), Separator = "#")]
    public string Gsi1Pk { get; set; } = string.Empty;

    [GsiPartitionKey("gsi2")]
    [DynamoDbAttribute("gsi2pk")]
    [Computed(nameof(Year), nameof(Month), Separator = "-")]
    public string Gsi2Pk { get; set; } = string.Empty;

    // ... source/extracted properties ...
}

// ✅ Valid — update sources for both computed fields independently
.Set(x => new EventUpdateModel
{
    EventType = "Conference",
    Status = "Active",
    Year = "2026",
    Month = "07"
})
// Generates: SET #gsi1pk = :p0, #gsi2pk = :p1
// :p0 = "Conference#Active", :p1 = "2026-07"
```

### Conditional Source Property Update

Since computed source properties cannot reference the entity parameter (FDDB071), the `NoUpdate()` pattern does not work for them. Instead, make the decision outside the lambda:

```csharp
var shouldUpdateGsi = true;
var newStatus = "Active";
var newRegion = "US-East";

if (shouldUpdateGsi)
{
    // Update sources (triggers recomputation) alongside regular properties
    await table.Products.Update(pk, sk)
        .Set(x => new ProductUpdateModel
        {
            Status = newStatus,
            Region = newRegion,
            Name = "Updated Product Name"
        })
        .UpdateAsync();
}
else
{
    // Only update regular properties — computed field left unchanged
    await table.Products.Update(pk, sk)
        .Set(x => new ProductUpdateModel
        {
            Name = "Updated Product Name"
        })
        .UpdateAsync();
}
```

> **Note:** You cannot use `x.Status.NoUpdate()` for computed source properties because the expression tree structurally references `x`, which triggers FDDB071 even when the branch wouldn't execute at runtime. The expression translator analyzes the full tree structure, not the runtime path.

---

## Best Practices

### 1. Prefer Source Properties Over Direct Assignment

Source-property-based updates ensure the computed value is always correctly formatted:

```csharp
// ✅ Preferred — framework handles separator and ordering
.Set(x => new ProductUpdateModel
{
    Status = "Active",
    Region = "US-East"
})

// ⚠️ Less safe — manual concatenation can introduce bugs
.Set(x => new ProductUpdateModel
{
    Gsi1Pk = $"{status}#{region}"
})
```

### 2. Keep Source Values in Local Variables

Since source properties cannot reference the entity parameter, compute values before the expression:

```csharp
var newStatus = DetermineStatus(order);
var newRegion = LookupRegion(order.LocationId);

await table.Products.Update(pk, sk)
    .Set(x => new ProductUpdateModel
    {
        Status = newStatus,
        Region = newRegion
    })
    .UpdateAsync();
```

### 3. Don't Mix Approaches

Choose either direct assignment or source-property-based update for each computed field:

```csharp
// ✅ Consistent — direct for one field, sources for another
.Set(x => new ProductUpdateModel
{
    Gsi1Pk = preComputedValue,       // Direct for gsi1pk
    Year = "2026",                    // Sources for gsi2pk
    Month = "07"
})
```

---

## See Also

- **[Expression-Based Updates](ExpressionBasedUpdates.md)** — General update expression patterns
- **[Entity Definition](EntityDefinition.md)** — Defining computed and extracted properties
- **[Error Handling](../reference/ErrorHandling.md)** — Exception handling patterns

---

[Back to Core Features](README.md) | [Back to Documentation Home](../README.md)
