---
title: "Put Key Prefix Behavior"
category: "core-features"
order: 14
keywords: ["put", "prefix", "key input mode", "auto", "value", "raw", "serialization", "WithKeyMode"]
related: ["KeyInputMode.md", "BasicOperations.md", "EntityDefinition.md", "Configuration.md"]
---

[Documentation](../README.md) > [Core Features](README.md) > Put Key Prefix Behavior

# Put Key Prefix Behavior

[Related: Key Input Mode](KeyInputMode.md)

---

This guide explains how key prefixes are automatically applied during Put (create/replace) operations. With automatic prefix application, you no longer need to manually call `Entity.Keys.Pk(value)` when constructing entities for Put — the library handles prefix logic during serialization based on the configured `KeyInputMode`.

## Overview

Prior to this feature, Put operations required you to manually construct prefixed key values:

```csharp
// Before: Manual prefix construction required
var order = new Order
{
    Pk = Order.Keys.Pk(orderId),   // "ORDER#12345"
    Sk = Order.Keys.Sk(lineId),   // "LINE#abc"
    Status = "pending"
};
await table.Orders.Put(order).PutAsync();
```

Now, the source-generated `ToDynamoDb()` serialization method automatically applies prefixes based on the resolved `KeyInputMode`:

```csharp
// After: Just set the raw value — prefix is applied during serialization
var order = new Order
{
    Pk = orderId,     // "12345" → serialized as "ORDER#12345"
    Sk = lineId,      // "abc"   → serialized as "LINE#abc"
    Status = "pending"
};
await table.Orders.Put(order).PutAsync();
```

## How It Works

During Put serialization, the generated `ToDynamoDb()` method applies `KeyPrefixHelper.ApplyKeyPrefix` to each key property that has a configured prefix (and is not computed). The behavior depends on the resolved `KeyInputMode`:

| Mode | Behavior During Put Serialization |
|------|----------------------------------|
| **Auto** (default) | Checks if value starts with prefix+separator. If yes, passes through. If no, prepends. |
| **Value** | Always prepends prefix+separator regardless of current value. |
| **Raw** | Always passes value through unchanged — no prefix logic applied. |

## Entity Definition (Used in Examples)

All examples below use this entity definition:

```csharp
[DynamoDbTable("Orders")]
public partial class Order
{
    [PartitionKey(Prefix = "ORDER")]
    [DynamoDbAttribute("pk")]
    public string Pk { get; set; } = string.Empty;

    [SortKey(Prefix = "META")]
    [DynamoDbAttribute("sk")]
    public string Sk { get; set; } = string.Empty;

    [DynamoDbAttribute("status")]
    public string Status { get; set; } = string.Empty;

    [DynamoDbAttribute("total")]
    public decimal Total { get; set; }
}
```

## Auto Mode (Default)

Auto mode is the default and recommended setting. It uses an ordinal case-sensitive `StartsWith(prefix + separator)` comparison to detect whether the value is already prefixed:

- If the value starts with the prefix+separator (exact case), it passes through unchanged.
- If the value does not start with the prefix+separator, the prefix and separator are prepended.

This provides a safe upgrade path — existing code that already passes fully-prefixed values continues to work.

### Putting an Entity with Raw Values

```csharp
// Auto mode (default): raw values get prefix prepended during serialization
var order = new Order
{
    Pk = "12345",         // Serialized as "ORDER#12345"
    Sk = "receipt",       // Serialized as "META#receipt"
    Status = "pending",
    Total = 99.99m
};

await table.Orders.Put(order).PutAsync();
// DynamoDB receives: pk = "ORDER#12345", sk = "META#receipt"
```

### Backward Compatibility — Existing `Entity.Keys.Pk(value)` Pattern

Existing code that constructs prefixed keys manually continues to work. Auto mode detects the prefix is already present and passes through unchanged:

```csharp
// Auto mode: already-prefixed values pass through unchanged (no double-prefix)
var order = new Order
{
    Pk = Order.Keys.Pk("12345"),   // "ORDER#12345" → stays "ORDER#12345"
    Sk = Order.Keys.Sk("receipt"), // "META#receipt" → stays "META#receipt"
    Status = "shipped",
    Total = 149.99m
};

await table.Orders.Put(order).PutAsync();
// DynamoDB receives: pk = "ORDER#12345", sk = "META#receipt"
```

### Case Sensitivity

Auto mode uses `StringComparison.Ordinal` (exact case). A different-case prefix is not recognized as already present:

```csharp
var order = new Order
{
    Pk = "order#12345",   // Lowercase 'o' — NOT recognized as prefixed
    Sk = "receipt",
    Status = "pending",
    Total = 50.00m
};

await table.Orders.Put(order).PutAsync();
// DynamoDB receives: pk = "ORDER#order#12345", sk = "META#receipt"
// The library prepended "ORDER#" because "order#" ≠ "ORDER#"
```

## Value Mode

Value mode always prepends the configured prefix and separator, regardless of the current value. Use this when you always pass raw component values and want explicit, predictable prefix application.

```csharp
// Configure globally
var options = new FluentDynamoDbOptions()
    .UseKeyInputMode(KeyInputMode.Value);
var table = new OrdersTable(client, "orders", options);

// Value mode: always prepends prefix+separator
var order = new Order
{
    Pk = "12345",         // Serialized as "ORDER#12345"
    Sk = "receipt",       // Serialized as "META#receipt"
    Status = "pending",
    Total = 75.00m
};

await table.Orders.Put(order).PutAsync();
// DynamoDB receives: pk = "ORDER#12345", sk = "META#receipt"
```

**Warning**: If you pass an already-prefixed value in Value mode, it gets double-prefixed:

```csharp
// ⚠️ Value mode double-prefix trap
var order = new Order
{
    Pk = "ORDER#12345",   // Serialized as "ORDER#ORDER#12345" — probably wrong!
    Sk = "META#receipt",  // Serialized as "META#META#receipt" — probably wrong!
    Status = "pending",
    Total = 75.00m
};

await table.Orders.Put(order).PutAsync();
// DynamoDB receives: pk = "ORDER#ORDER#12345" ✗
```

## Raw Mode

Raw mode passes key values through to DynamoDB unchanged — no prefix logic is applied. This matches the legacy behavior before automatic prefix application was introduced.

```csharp
// Configure globally
var options = new FluentDynamoDbOptions()
    .UseKeyInputMode(KeyInputMode.Raw);
var table = new OrdersTable(client, "orders", options);

// Raw mode: values pass through unchanged
var order = new Order
{
    Pk = "ORDER#12345",   // Passes through as "ORDER#12345"
    Sk = "META#receipt",  // Passes through as "META#receipt"
    Status = "pending",
    Total = 200.00m
};

await table.Orders.Put(order).PutAsync();
// DynamoDB receives: pk = "ORDER#12345", sk = "META#receipt"
```

With Raw mode, you are responsible for constructing the full prefixed value:

```csharp
// ⚠️ Raw mode: raw values are NOT prefixed
var order = new Order
{
    Pk = "12345",         // Passes through as "12345" — no prefix!
    Sk = "receipt",       // Passes through as "receipt" — no prefix!
    Status = "pending",
    Total = 200.00m
};

await table.Orders.Put(order).PutAsync();
// DynamoDB receives: pk = "12345", sk = "receipt" — probably wrong!
```

## Per-Call Override with `WithKeyMode`

You can override the global `KeyInputMode` for a specific Put operation using the `WithKeyMode` builder method. This is useful for edge cases where a single call needs different prefix behavior.

```csharp
// Global mode is Auto (default), but this specific Put uses Raw mode
var order = new Order
{
    Pk = "ORDER#12345",   // Already fully constructed
    Sk = "META#receipt",
    Status = "complete",
    Total = 300.00m
};

await table.Orders.Put(order)
    .WithKeyMode(KeyInputMode.Raw)
    .PutAsync();
// DynamoDB receives values unchanged: pk = "ORDER#12345", sk = "META#receipt"
```

### Per-Call Value Mode

```csharp
// Global mode is Raw, but this specific Put uses Value mode
var order = new Order
{
    Pk = "12345",
    Sk = "receipt",
    Status = "new",
    Total = 50.00m
};

await table.Orders.Put(order)
    .WithKeyMode(KeyInputMode.Value)
    .PutAsync();
// DynamoDB receives: pk = "ORDER#12345", sk = "META#receipt"
```

### Per-Call Auto Mode

```csharp
// Global mode is Raw, but this specific Put uses Auto mode
var order = new Order
{
    Pk = "12345",
    Sk = "META#receipt",   // Already has SK prefix
    Status = "processing",
    Total = 125.00m
};

await table.Orders.Put(order)
    .WithKeyMode(KeyInputMode.Auto)
    .PutAsync();
// pk: "ORDER#12345" (prefix prepended — was missing)
// sk: "META#receipt" (passes through — already prefixed)
```

## Global Configuration

Set the default `KeyInputMode` for all operations via `FluentDynamoDbOptions`:

```csharp
// Configure Raw mode globally (opt out of automatic prefix handling)
var options = new FluentDynamoDbOptions()
    .UseKeyInputMode(KeyInputMode.Raw);

var table = new OrdersTable(client, "orders", options);

// All Put operations now pass values unchanged by default
await table.Orders.Put(order).PutAsync();
// Uses Raw mode (global default)

// Override per-call when needed
await table.Orders.Put(anotherOrder)
    .WithKeyMode(KeyInputMode.Auto)
    .PutAsync();
// Uses Auto mode (per-call override)
```

## Computed Keys

Key properties decorated with `[Computed]` are excluded from automatic prefix application. The computed value (assembled from source properties) is written to DynamoDB in its final form:

```csharp
[DynamoDbTable("Events")]
public partial class Event
{
    [PartitionKey]
    [DynamoDbAttribute("pk")]
    [Computed("Year", "Month", "Day", Separator = "#")]
    public string Pk { get; set; } = string.Empty;

    [SortKey(Prefix = "EVT")]
    [DynamoDbAttribute("sk")]
    public string Sk { get; set; } = string.Empty;

    [Extracted("Pk", 0)]
    public int Year { get; set; }

    [Extracted("Pk", 1)]
    public int Month { get; set; }

    [Extracted("Pk", 2)]
    public int Day { get; set; }
}

var evt = new Event
{
    Year = 2024,
    Month = 12,
    Day = 25,
    Sk = "holiday-sale"
};

await table.Events.Put(evt).PutAsync();
// pk: "2024#12#25" (computed value — NO prefix applied regardless of mode)
// sk: "EVT#holiday-sale" (non-computed with prefix — prefix applied per mode)
```

## Keys Without Prefix Configuration

When a key property has no prefix configured, the value always passes through unchanged regardless of the resolved `KeyInputMode`:

```csharp
[DynamoDbTable("Simple")]
public partial class SimpleEntity
{
    [PartitionKey]  // No prefix
    [DynamoDbAttribute("pk")]
    public string Pk { get; set; } = string.Empty;
}

var entity = new SimpleEntity { Pk = "my-id" };
await table.Simple.Put(entity).PutAsync();
// DynamoDB receives: pk = "my-id" (unchanged, regardless of mode)
```

## Convenience Methods

Generated convenience methods (`PutAsync(entity)`) use `KeyInputMode.Default`, which resolves to the global `FluentDynamoDbOptions.DefaultKeyInputMode` at execution time:

```csharp
// Convenience method — uses global default mode
await table.Orders.PutAsync(order);

// Equivalent builder pattern
await table.Orders.Put(order).PutAsync();

// Both resolve KeyInputMode.Default → FluentDynamoDbOptions.DefaultKeyInputMode (Auto)
```

To override the mode, use the builder pattern with `WithKeyMode`:

```csharp
// Convenience methods don't support per-call mode override
// Use the builder pattern instead:
await table.Orders.Put(order)
    .WithKeyMode(KeyInputMode.Raw)
    .PutAsync();
```

## Mode Comparison for Put Operations

Given an entity with `[PartitionKey(Prefix = "ORDER")]` and separator `"#"`:

| Entity Pk Value | Auto (default) | Value | Raw |
|-----------------|----------------|-------|-----|
| `"12345"` | `"ORDER#12345"` | `"ORDER#12345"` | `"12345"` |
| `"ORDER#12345"` | `"ORDER#12345"` | `"ORDER#ORDER#12345"` | `"ORDER#12345"` |
| `"order#12345"` | `"ORDER#order#12345"` | `"ORDER#order#12345"` | `"order#12345"` |
| `""` (empty) | `"ORDER#"` | `"ORDER#"` | `""` |

## Migration Guidance

### Existing Code (Pre-Feature)

If you were manually calling `Entity.Keys.Pk(value)` before Put operations, your code continues to work in Auto mode:

```csharp
// Before: Manual prefix construction (still works in Auto mode)
var order = new Order
{
    Pk = Order.Keys.Pk(orderId),   // "ORDER#12345"
    Sk = Order.Keys.Sk(metaType),  // "META#receipt"
    Status = "active"
};
await table.Orders.Put(order).PutAsync();
// Auto mode: "ORDER#12345" starts with "ORDER#" → passes through unchanged ✓
```

### Simplified Code (Post-Feature)

With Auto mode active, you can simplify entity construction:

```csharp
// After: Just set raw values — prefix applied automatically
var order = new Order
{
    Pk = orderId,     // "12345" → serialized as "ORDER#12345"
    Sk = metaType,    // "receipt" → serialized as "META#receipt"
    Status = "active"
};
await table.Orders.Put(order).PutAsync();
```

## Error Handling

| Scenario | Exception | Message |
|----------|-----------|---------|
| Null partition key at serialization | `ArgumentNullException` | Parameter name: property name |
| Null sort key at serialization (with prefix) | `ArgumentNullException` | Parameter name: property name |
| Invalid enum value passed to `WithKeyMode` | `ArgumentOutOfRangeException` | Undefined KeyInputMode value |

## See Also

- [Key Input Mode](KeyInputMode.md) — Full `KeyInputMode` reference covering all operations (Get, Update, Delete)
- [Basic Operations](BasicOperations.md) — Put operations overview
- [Entity Definition](EntityDefinition.md) — Defining entities with key prefixes
- [Configuration](Configuration.md) — `FluentDynamoDbOptions` configuration reference
