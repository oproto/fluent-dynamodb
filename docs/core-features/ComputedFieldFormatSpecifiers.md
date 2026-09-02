---
title: "Computed Field Format Specifiers"
category: "core-features"
order: 37
keywords: ["computed", "format specifier", "IFormattable", "DateOnly", "enum", "zero-padding", "InvariantCulture", "string.Format"]
related: ["ComputedFieldUpdates.md", "format-strings-guide.md", "EntityDefinition.md"]
---

[Documentation](../README.md) > [Core Features](README.md) > Computed Field Format Specifiers

# Computed Field Format Specifiers

Use .NET format specifiers in computed field format strings to control how typed values (dates, integers, enums) are rendered in composite keys.

---

## Table of Contents

- [Overview](#overview)
- [Basic Usage](#basic-usage)
- [Format Specifier Examples](#format-specifier-examples)
- [Format Specifier Precedence](#format-specifier-precedence)
- [Source Property Format Fallback](#source-property-format-fallback)
- [Culture Handling](#culture-handling)
- [Backwards Compatibility](#backwards-compatibility)
- [Best Practices](#best-practices)

---

## Overview

Computed fields combine multiple source properties into a single DynamoDB attribute using a format string. With format specifier support, you can use the full power of .NET composite formatting (`{0:yyyy-MM-dd}`, `{0:D4}`, `{0:G}`) to control how each source value is rendered in the resulting key.

Format specifiers work by passing typed values directly to `string.Format`, allowing the .NET `IFormattable` interface to apply the specifier. This means any type implementing `IFormattable` (DateTime, DateOnly, int, decimal, enums, etc.) can be formatted within computed keys.

All three operation paths — Put, Update, and Key builder — produce identical output when format specifiers are used.

---

## Basic Usage

Add format specifiers inside placeholders in the `Format` property of the `[Computed]` attribute:

```csharp
[DynamoDbTable("Events")]
public partial class Event
{
    [PartitionKey]
    [DynamoDbAttribute("pk")]
    [Computed(nameof(EventDate), nameof(Category), Format = "{0:yyyy-MM-dd}#{1}")]
    public string Pk { get; set; } = string.Empty;

    [Extracted(nameof(Pk), 0)]
    public DateOnly EventDate { get; set; }

    [Extracted(nameof(Pk), 1)]
    public string Category { get; set; } = string.Empty;
}
```

With `EventDate = new DateOnly(2024, 3, 15)` and `Category = "electronics"`, the computed key produces:

```
2024-03-15#electronics
```

---

## Format Specifier Examples

### DateOnly with Date Format

```csharp
[DynamoDbTable("Events")]
public partial class Event
{
    [PartitionKey]
    [DynamoDbAttribute("pk")]
    [Computed(nameof(EventDate), nameof(Category), Format = "{0:yyyy-MM-dd}#{1}")]
    public string Pk { get; set; } = string.Empty;

    [Extracted(nameof(Pk), 0)]
    public DateOnly EventDate { get; set; }

    [Extracted(nameof(Pk), 1)]
    public string Category { get; set; } = string.Empty;
}

// EventDate = 2024-03-15, Category = "electronics"
// Result: "2024-03-15#electronics"
```

### Integer with Zero-Padding

```csharp
[DynamoDbTable("Tasks")]
public partial class TaskItem
{
    [SortKey]
    [DynamoDbAttribute("sk")]
    [Computed(nameof(Priority), nameof(Name), Format = "{0:D4}#{1}")]
    public string Sk { get; set; } = string.Empty;

    [Extracted(nameof(Sk), 0)]
    public int Priority { get; set; }

    [Extracted(nameof(Sk), 1)]
    public string Name { get; set; } = string.Empty;
}

// Priority = 42, Name = "TaskName"
// Result: "0042#TaskName"
```

### Enum with General Format

```csharp
public enum OrderStatus
{
    Pending,
    Active,
    Completed
}

[DynamoDbTable("Orders")]
public partial class Order
{
    [GsiPartitionKey("status-index")]
    [DynamoDbAttribute("gsi1pk")]
    [Computed(nameof(Status), nameof(Id), Format = "{0:G}#{1}")]
    public string Gsi1Pk { get; set; } = string.Empty;

    [Extracted(nameof(Gsi1Pk), 0)]
    public OrderStatus Status { get; set; }

    [Extracted(nameof(Gsi1Pk), 1)]
    public string Id { get; set; } = string.Empty;
}

// Status = OrderStatus.Active, Id = "id123"
// Result: "Active#id123"
```

---

## Format Specifier Precedence

When determining how a source property value is formatted in a computed key, the system uses the following precedence (highest to lowest):

| Priority | Source | Example | Behavior |
|----------|--------|---------|----------|
| 1 | Explicit specifier in computed format | `{0:yyyy-MM-dd}` | Format specifier applied directly by `string.Format` |
| 2 | Source property `DynamoDbAttribute.Format` | `[DynamoDbAttribute("date", Format = "yyyy-MM-dd")]` | Injected into placeholder at compile time |
| 3 | Default `ToString()` | No format specified | Standard .NET conversion |

If a computed format placeholder has an explicit specifier, it always wins — even if the source property also has a `DynamoDbAttribute.Format` defined.

---

## Source Property Format Fallback

When a computed format placeholder has no explicit specifier (`{0}`) but the source property has a `DynamoDbAttribute.Format`, the source generator automatically injects the source property's format at compile time.

```csharp
[DynamoDbTable("Events")]
public partial class Event
{
    [PartitionKey]
    [DynamoDbAttribute("pk")]
    [Computed(nameof(EventDate), nameof(Category))]  // No explicit Format
    public string Pk { get; set; } = string.Empty;

    // Source property with its own Format
    [DynamoDbAttribute("date", Format = "yyyy-MM-dd")]
    [Extracted(nameof(Pk), 0)]
    public DateOnly EventDate { get; set; }

    [Extracted(nameof(Pk), 1)]
    public string Category { get; set; } = string.Empty;
}
```

The source generator produces an effective format string of `{0:yyyy-MM-dd}#{1}` — the source property's format is injected into the placeholder automatically. The resulting key value is identical to specifying the format explicitly in the `[Computed]` attribute.

### Explicit Specifier Overrides Source Format

If both are present, the explicit specifier in the computed format wins:

```csharp
// Explicit format specifier takes precedence over source property Format
[Computed(nameof(EventDate), nameof(Category), Format = "{0:MM/dd/yyyy}#{1}")]
public string Pk { get; set; } = string.Empty;

// Source property Format "yyyy-MM-dd" is ignored for this computed field
[DynamoDbAttribute("date", Format = "yyyy-MM-dd")]
public DateOnly EventDate { get; set; }

// Result uses MM/dd/yyyy: "03/15/2024#electronics"
```

---

## Culture Handling

All format specifier paths use `CultureInfo.InvariantCulture` to ensure deterministic output. This means:

- Date separators are always `/` (not locale-dependent)
- Decimal separators are always `.`
- Enum names use invariant casing
- Key values are consistent regardless of the machine's locale settings

This is critical for DynamoDB key integrity — a key generated on a machine with `en-US` locale must match a key generated on a machine with `de-DE` locale.

---

## Backwards Compatibility

Existing entities without format specifiers are completely unaffected by this feature:

- Format strings containing only simple placeholders (`{0}#{1}`) continue to use the existing pre-stringification behavior
- No code changes are required for existing entity definitions
- Discriminator pattern derivation produces the same results for simple placeholders
- Placeholder count validation works identically for simple placeholders

Format specifier behavior only activates when a placeholder contains a colon followed by a format string (e.g., `{0:D4}`).

---

## Best Practices

### 1. Prefer Explicit Specifiers for Clarity

When the computed format is defined on the same entity, use explicit specifiers for readability:

```csharp
// ✅ Clear — format is visible at the computed field declaration
[Computed(nameof(EventDate), nameof(Category), Format = "{0:yyyy-MM-dd}#{1}")]
public string Pk { get; set; } = string.Empty;
```

### 2. Use Source Property Format for Consistency

When a source property is used in multiple computed fields and should always use the same format, define the format once on the source property:

```csharp
// ✅ Consistent — format defined once, used in multiple computed fields
[DynamoDbAttribute("date", Format = "yyyy-MM-dd")]
public DateOnly EventDate { get; set; }
```

### 3. Use Standard .NET Format Strings

Stick to well-known .NET format strings for predictable behavior:

| Type | Common Specifiers |
|------|-------------------|
| DateOnly / DateTime | `yyyy-MM-dd`, `o`, `HH:mm:ss` |
| int / long | `D4` (zero-pad), `X` (hex) |
| decimal / double | `F2` (fixed), `N2` (thousands) |
| enum | `G` (name), `D` (numeric value) |

### 4. Test Key Values in Unit Tests

Verify that computed keys produce expected values, especially when using format specifiers with dates or locale-sensitive types:

```csharp
var pk = Event.Keys.BuildPk(new DateOnly(2024, 3, 15), "electronics");
Assert.Equal("2024-03-15#electronics", pk);
```

---

## See Also

- **[Computed Field Updates](ComputedFieldUpdates.md)** — Updating computed fields via source properties
- **[Format Strings Guide](format-strings-guide.md)** — Format strings for individual property serialization
- **[Entity Definition](EntityDefinition.md)** — Defining computed and extracted properties

---

[Back to Core Features](README.md) | [Back to Documentation Home](../README.md)
