# Computed Key Typed Parameter Overloads

This guide explains the convenience overloads generated for entities with computed keys. These overloads let you pass individual source property components directly to Get, Update, Delete, and ConditionCheck operations without manually calling `Entity.Keys.BuildPk(...)` or `Entity.Keys.BuildSk(...)`.

## Overview

When an entity has a computed key with two or more source properties, the source generator produces additional overloads that accept the individual component values as typed parameters. This eliminates boilerplate and provides compile-time type safety for key composition.

## Before and After

### Before: Manual Key Composition

```csharp
[DynamoDbTable("events")]
public partial class Event
{
    [PartitionKey]
    [DynamoDbAttribute("pk")]
    [Computed(nameof(Year), nameof(Month), nameof(Day), Separator = "#")]
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

// Manual key construction required for every operation
var pk = Event.Keys.BuildPk(2024, 12, 25);
var evt = await table.Events.Get(pk, "EVT#christmas").GetItemAsync();

await table.Events.Delete(Event.Keys.BuildPk(2024, 12, 25), "EVT#christmas").DeleteAsync();

await table.Events.Update(Event.Keys.BuildPk(2024, 12, 25), "EVT#christmas")
    .Set(x => new EventUpdateModel { Status = "archived" })
    .UpdateAsync();
```

### After: Typed Async Convenience Methods (Simplest)

```csharp
// One-shot convenience methods — pass typed parameters and get the result directly
var evt = await table.Events.GetAsync(2024, 12, 25, "EVT#christmas");

await table.Events.DeleteAsync(2024, 12, 25, "EVT#christmas");

// DeleteAsync also accepts an optional KeyCondition
await table.Events.DeleteAsync(2024, 12, 25, "EVT#christmas", KeyCondition.MustExist);
```

### After: Typed Builder Overloads (When You Need Options)

```csharp
// Use the builder pattern when you need projections, conditions, or consistent reads
var evt = await table.Events.Get(2024, 12, 25, "EVT#christmas")
    .UsingConsistentRead()
    .GetItemAsync();

await table.Events.Delete(2024, 12, 25, "EVT#christmas")
    .Where(x => x.Status == "cancelled")
    .DeleteAsync();

await table.Events.Update(2024, 12, 25, "EVT#christmas")
    .Set(x => new EventUpdateModel { Status = "archived" })
    .UpdateAsync();
```

The generated overloads call `Event.Keys.BuildPk(year, month, day)` internally and pass the composed key to the standard accessor.

## When Typed Overloads Are Generated

The source generator produces typed overloads when **all** of the following conditions are met:

1. At least one key (partition key or sort key) has a `[Computed]` attribute with **two or more source properties**
2. The generated overload signature would **not be ambiguous** with the existing string overload (i.e., not all source properties resolve to `string` with the same parameter count as the standard overload)

### Scenarios That Generate Typed Overloads

| Key Configuration | Overload Parameters |
|---|---|
| Computed PK (≥2 sources), no SK | PK source property params only |
| Computed PK (≥2 sources) + simple SK | PK source params + SK string |
| Simple PK + computed SK (≥2 sources) | PK string + SK source params |
| Both PK and SK computed | All PK source params + all SK source params |
| One computed + one non-computed | Computed source params + single string for non-computed |

### When Overloads Are NOT Generated

- Neither key is computed (no `[Computed]` attribute)
- Computed key has only one source property (signature would be `(string)` — same as standard overload)
- All source properties are `string` and the parameter count matches the existing overload (ambiguous signature)

When a typed overload cannot be generated due to ambiguity, the entity falls through to `KeyInputMode` parameter injection instead (see [KeyInputMode](KeyInputMode.md)).

## Parameter Types and Naming

Parameters in the generated overload match the types declared on your source properties:

```csharp
[DynamoDbTable("metrics")]
public partial class Metric
{
    [PartitionKey]
    [DynamoDbAttribute("pk")]
    [Computed(nameof(TenantId), nameof(Year), nameof(Month), Separator = "#")]
    public string Pk { get; set; } = string.Empty;

    [Extracted("Pk", 0)]
    public string TenantId { get; set; } = string.Empty;

    [Extracted("Pk", 1)]
    public int Year { get; set; }

    [Extracted("Pk", 2)]
    public int Month { get; set; }
}
```

**Generated overload signature:**
```csharp
public GetItemRequestBuilder<Metric> Get(string tenantId, int year, int month)
```

Key points:
- Parameter names are camelCase versions of the source property names
- Non-string types (`int`, `DateTime`, `Guid`, enums) are preserved exactly
- Nullable types (`int?`, `DateTime?`) are preserved
- Enum types use their full namespace-qualified type

## Computed Key with Prefix

When a computed key also has a configured prefix, the typed overload composes the key using `Keys.BuildPk(...)` which already incorporates the prefix:

```csharp
[DynamoDbTable("orders")]
public partial class Order
{
    [PartitionKey(Prefix = "ORD")]
    [DynamoDbAttribute("pk")]
    [Computed(nameof(Region), nameof(OrderNumber), Separator = "#")]
    public string Pk { get; set; } = string.Empty;

    [Extracted("Pk", 0)]
    public string Region { get; set; } = string.Empty;

    [Extracted("Pk", 1)]
    public int OrderNumber { get; set; }
}

// Typed overload — prefix is applied internally by Keys.BuildPk
var order = await table.Orders.Get("us-east", 12345).GetItemAsync();
// DynamoDB receives pk = "ORD#us-east#12345"
```

You never need to worry about prefix handling when using typed overloads — the `Keys.BuildPk()`/`Keys.BuildSk()` methods handle it.

## Both Keys Computed

When both partition key and sort key are computed, a single overload is generated with all PK source params followed by all SK source params:

```csharp
[DynamoDbTable("timeseries")]
public partial class TimeSeriesEntry
{
    [PartitionKey]
    [DynamoDbAttribute("pk")]
    [Computed(nameof(SensorId), nameof(Region), Separator = "#")]
    public string Pk { get; set; } = string.Empty;

    [SortKey]
    [DynamoDbAttribute("sk")]
    [Computed(nameof(Year), nameof(Month), nameof(Day), Separator = "#")]
    public string Sk { get; set; } = string.Empty;

    [Extracted("Pk", 0)]
    public string SensorId { get; set; } = string.Empty;

    [Extracted("Pk", 1)]
    public string Region { get; set; } = string.Empty;

    [Extracted("Sk", 0)]
    public int Year { get; set; }

    [Extracted("Sk", 1)]
    public int Month { get; set; }

    [Extracted("Sk", 2)]
    public int Day { get; set; }
}

// Single typed overload: PK components first, then SK components
var entry = await table.TimeSeriesEntries
    .Get("sensor-42", "us-west", 2024, 12, 25)
    .GetItemAsync();

// Or use the async convenience method for simple gets:
var entry2 = await table.TimeSeriesEntries.GetAsync("sensor-42", "us-west", 2024, 12, 25);
```

## Consistent Across All CRUD Methods

The same typed overload signature is generated for Get, Delete, Update, and ConditionCheck. For Get and Delete, one-shot async convenience methods are also available:

```csharp
// GetAsync — simplest approach for retrieving an item
var evt = await table.Events.GetAsync(2024, 12, 25, "EVT#party");

// DeleteAsync — simplest approach for deleting an item
await table.Events.DeleteAsync(2024, 12, 25, "EVT#party");

// DeleteAsync with KeyCondition
await table.Events.DeleteAsync(2024, 12, 25, "EVT#party", KeyCondition.MustExist);

// Get builder — when you need options like consistent read or projections
var evt2 = await table.Events.Get(2024, 12, 25, "EVT#party")
    .UsingConsistentRead()
    .GetItemAsync();

// Delete builder — when you need a condition expression
await table.Events.Delete(2024, 12, 25, "EVT#party")
    .Where(x => x.Status == "cancelled")
    .DeleteAsync();

// Update — always uses the builder pattern (needs Set clause)
await table.Events.Update(2024, 12, 25, "EVT#party")
    .Set(x => new EventUpdateModel { Title = "Updated" })
    .UpdateAsync();

// ConditionCheck (in transactions)
await DynamoDbTransactions.Write
    .Add(table.Events.ConditionCheck(2024, 12, 25, "EVT#party")
        .Where(x => x.Status == "confirmed"))
    .Add(table.Events.Put(newEvent))
    .ExecuteAsync();
```

## Table-Level Overloads

Typed overloads and async convenience methods are also generated at the table level for single-entity tables:

```csharp
// Table-level GetAsync (delegates to entity accessor)
var evt = await table.GetAsync(2024, 12, 25, "EVT#party");

// Table-level DeleteAsync
await table.DeleteAsync(2024, 12, 25, "EVT#party");

// Table-level builder (when you need options)
var evt2 = await table.Get(2024, 12, 25, "EVT#party").GetItemAsync();
```

## FluentResults Variants

When an entity has `[UseFluentResults]`, the generator also produces `GetAsyncResult` and `DeleteAsyncResult` methods that return `Result<T?>` and `Result` instead of throwing exceptions:

```csharp
// GetAsyncResult — returns Result<Event?> instead of throwing
var result = await table.Events.GetAsyncResult(2024, 12, 25, "EVT#party");
if (result.IsSuccess)
{
    var evt = result.Value;
}

// DeleteAsyncResult — returns Result instead of throwing
var deleteResult = await table.Events.DeleteAsyncResult(2024, 12, 25, "EVT#party", KeyCondition.MustExist);
if (deleteResult.IsFailed)
{
    // Handle error
}
```

Table-level `GetAsyncResult` and `DeleteAsyncResult` are also generated for single-entity tables.

## Standard Overloads Remain Unchanged

The existing `(string)` and `(string, string)` overloads are never removed or modified. You can continue using them with pre-built keys:

```csharp
// Standard overload still works exactly as before
var pk = Event.Keys.BuildPk(2024, 12, 25);
var evt = await table.Events.Get(pk, "EVT#party").GetItemAsync();
```

When a typed overload exists for an entity, the standard string overload does **not** get a `KeyInputMode` parameter — it is unambiguously for pre-built keys.

## Relationship with KeyInputMode

These two features are mutually exclusive per entity:

| Entity Configuration | Behavior |
|---|---|
| Computed key (typed overload generated) | Typed overload for raw components; string overload for pre-built keys. No `KeyInputMode` parameter. |
| String key with prefix (no typed overload) | String overload gets `KeyInputMode mode = KeyInputMode.Default` parameter. See [KeyInputMode](KeyInputMode.md). |

## See Also

- [Entity Definition](EntityDefinition.md#computed-keys-with-format-strings) — Defining computed keys
- [KeyInputMode](KeyInputMode.md) — Controlling prefix behavior on non-computed string keys
- [Basic Operations](BasicOperations.md) — Standard Get, Put, Update, Delete operations
