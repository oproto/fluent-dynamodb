# Key Input Mode

This guide explains the `KeyInputMode` feature, which controls how key values are interpreted when passed to DynamoDB operations. It enables the library to automatically apply key prefixes based on a configurable strategy, reducing the need to manually call `Entity.Keys.Pk(value)` for every operation.

## Overview

When entities define key prefixes (e.g., `[PartitionKey(Prefix = "ORDER")]`), you typically need to ensure the full prefixed value (like `"ORDER#12345"`) is passed to Get, Put, Update, and Delete operations. `KeyInputMode` automates this by letting you pass raw values (like `"12345"`) and having the library prepend the prefix for you.

The default behavior (`Auto` mode) intelligently detects whether a value is already prefixed and only prepends when needed, providing a safe upgrade path for existing code.

## KeyInputMode Enum Values

The `KeyInputMode` enum defines four interpretation strategies:

| Value | Ordinal | Behavior |
|-------|---------|----------|
| `Default` | 0 | Defers to `FluentDynamoDbOptions.DefaultKeyInputMode`. Only valid as a per-call parameter. |
| `Auto` | 1 | Checks if value already starts with prefix+separator. If yes, passes through unchanged; otherwise prepends. |
| `Value` | 2 | Always prepends prefix+separator to the input value. Equivalent to calling `Entity.Keys.Pk(value)`. |
| `Raw` | 3 | Passes value through to DynamoDB unchanged. This is the legacy behavior. |

### Default

`KeyInputMode.Default` is a sentinel value used as the default parameter on operation methods. It resolves to whatever mode is configured on `FluentDynamoDbOptions.DefaultKeyInputMode`. You cannot set the global default to `Default` — doing so throws an `ArgumentException`.

### Auto (Recommended)

`Auto` mode uses an ordinal case-sensitive `StartsWith` check to determine whether the prefix is already present:

```csharp
// Entity with [PartitionKey(Prefix = "ORDER")]
// Auto mode behavior:
//   "ORDER#12345" → "ORDER#12345" (already prefixed, passes through unchanged)
//   "12345"       → "ORDER#12345" (not prefixed, prepends ORDER#)
```

This is the default mode and provides backward compatibility — existing code that already passes fully-prefixed values continues to work, while new code can pass raw values for convenience.

### Value

`Value` mode always prepends the prefix and separator, regardless of the input. This is equivalent to calling `Entity.Keys.Pk(value)` manually:

```csharp
// Entity with [PartitionKey(Prefix = "ORDER")]
// Value mode behavior:
//   "ORDER#12345" → "ORDER#ORDER#12345" (always prepends — be careful!)
//   "12345"       → "ORDER#12345" (always prepends)
```

Use `Value` mode when you always pass raw component values and want explicit, predictable prefix application.

### Raw

`Raw` mode passes the value through to DynamoDB unchanged. This is the legacy behavior prior to the introduction of `KeyInputMode`:

```csharp
// Entity with [PartitionKey(Prefix = "ORDER")]
// Raw mode behavior:
//   "ORDER#12345" → "ORDER#12345" (unchanged)
//   "12345"       → "12345" (unchanged — no prefix applied!)
```

Use `Raw` mode to opt out of automatic prefix handling entirely.

## Configuration

### Setting the Global Default

Configure the default key input mode on `FluentDynamoDbOptions` using the `UseKeyInputMode()` method:

```csharp
// Lambda-style configuration (preferred)
var options = new FluentDynamoDbOptions()
    .UseKeyInputMode(KeyInputMode.Auto);

var table = new OrdersTable(client, "orders", options);
```

The `UseKeyInputMode()` method follows the same immutable clone pattern as other configuration methods — it returns a new `FluentDynamoDbOptions` instance with the specified mode, leaving the original instance unchanged.

### Combining with Other Options

Chain `UseKeyInputMode()` with other configuration methods:

```csharp
var options = new FluentDynamoDbOptions()
    .WithLogger(loggerFactory.ToDynamoDbLogger<OrdersTable>())
    .UseKeyInputMode(KeyInputMode.Auto)
    .UseConsistentRead(true);

var table = new OrdersTable(client, "orders", options);
```

### Invalid Configuration

Passing `KeyInputMode.Default` to `UseKeyInputMode()` throws an `ArgumentException`:

```csharp
// ❌ This throws ArgumentException
var options = new FluentDynamoDbOptions()
    .UseKeyInputMode(KeyInputMode.Default);
// Message: "KeyInputMode.Default is only valid as a per-call parameter value.
//           Specify Auto, Value, or Raw for the global default."
```

## Default Behavior

When no explicit `KeyInputMode` is configured, the library uses `KeyInputMode.Auto`. This means:

1. Values that already include the prefix pass through unchanged (no double-prefixing)
2. Values without the prefix get the prefix prepended automatically
3. Keys with no configured prefix are always passed through unchanged

This makes `Auto` mode safe for existing codebases — your current calls with fully-prefixed values continue working, and you can gradually adopt raw values where convenient.

## Examples

### Entity Definition

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
}
```

### Auto Mode (Default)

```csharp
var options = new FluentDynamoDbOptions()
    .UseKeyInputMode(KeyInputMode.Auto);

var table = new OrdersTable(client, "orders", options);

// Already-prefixed values pass through unchanged
var order = await table.Orders.Get("ORDER#12345").GetItemAsync();
// DynamoDB receives pk = "ORDER#12345" ✓

// Raw values get the prefix prepended automatically
var order = await table.Orders.Get("12345").GetItemAsync();
// DynamoDB receives pk = "ORDER#12345" ✓
```

### Value Mode

```csharp
var options = new FluentDynamoDbOptions()
    .UseKeyInputMode(KeyInputMode.Value);

var table = new OrdersTable(client, "orders", options);

// Always prepends — use only with raw component values
var order = await table.Orders.Get("12345").GetItemAsync();
// DynamoDB receives pk = "ORDER#12345" ✓

// ⚠️ Be careful: already-prefixed values get double-prefixed!
var order = await table.Orders.Get("ORDER#12345").GetItemAsync();
// DynamoDB receives pk = "ORDER#ORDER#12345" ✗
```

### Raw Mode (Legacy)

```csharp
var options = new FluentDynamoDbOptions()
    .UseKeyInputMode(KeyInputMode.Raw);

var table = new OrdersTable(client, "orders", options);

// Values pass through unchanged — you're responsible for prefixing
var order = await table.Orders.Get("ORDER#12345").GetItemAsync();
// DynamoDB receives pk = "ORDER#12345" ✓

// Raw values are NOT prefixed
var order = await table.Orders.Get("12345").GetItemAsync();
// DynamoDB receives pk = "12345" — probably not what you want!
```

### No Prefix Configured

When a key property has no prefix configured, all modes behave identically — the value passes through unchanged:

```csharp
[DynamoDbTable("Simple")]
public partial class SimpleEntity
{
    [PartitionKey]  // No prefix
    [DynamoDbAttribute("pk")]
    public string Pk { get; set; } = string.Empty;
}

// All modes produce the same result when no prefix is configured:
var entity = await table.Simple.Get("my-id").GetItemAsync();
// DynamoDB receives pk = "my-id" (unchanged, regardless of mode)
```

## Mode Comparison

Given an entity with `[PartitionKey(Prefix = "ORDER")]` and the default separator `"#"`:

| Input Value | Auto | Value | Raw |
|-------------|------|-------|-----|
| `"ORDER#12345"` | `"ORDER#12345"` | `"ORDER#ORDER#12345"` | `"ORDER#12345"` |
| `"12345"` | `"ORDER#12345"` | `"ORDER#12345"` | `"12345"` |
| `"order#12345"` | `"ORDER#order#12345"` | `"ORDER#order#12345"` | `"order#12345"` |

Note: Auto mode uses ordinal case-sensitive comparison, so `"order#12345"` (lowercase) is not recognized as already prefixed.

## Migration Guidance

### For Existing Users (Pre-KeyInputMode)

If you're upgrading from a version without `KeyInputMode`, no changes are required. The default mode is `Auto`, which handles both prefixed and unprefixed values correctly:

```csharp
// Your existing code continues to work as-is
var order = await table.Orders.Get(Order.Keys.Pk("12345")).GetItemAsync();
// "ORDER#12345" is recognized as already prefixed → passes through unchanged ✓
```

### Adopting Raw Value Convenience

Once `KeyInputMode.Auto` is active (the default), you can gradually simplify your code:

```csharp
// Before: Always calling Entity.Keys.Pk() manually
var order = await table.Orders.Get(Order.Keys.Pk(orderId)).GetItemAsync();

// After: Pass raw values directly (Auto mode prepends the prefix)
var order = await table.Orders.Get(orderId).GetItemAsync();
```

### Switching to Raw Mode for Backward Compatibility

If you prefer to opt out of automatic prefix handling entirely and maintain the exact pre-KeyInputMode behavior:

```csharp
var options = new FluentDynamoDbOptions()
    .UseKeyInputMode(KeyInputMode.Raw);

var table = new OrdersTable(client, "orders", options);

// No automatic prefix handling — you manage prefixes yourself
var order = await table.Orders.Get(Order.Keys.Pk(orderId)).GetItemAsync();
```

### Custom Separator

If your entity uses a custom separator, the same rules apply:

```csharp
[PartitionKey(Prefix = "ORDER", Separator = "_")]
[DynamoDbAttribute("pk")]
public string Pk { get; set; } = string.Empty;

// Auto mode checks for "ORDER_" prefix:
//   "ORDER_12345" → "ORDER_12345" (already prefixed)
//   "12345"       → "ORDER_12345" (prepends prefix+separator)
```

## Per-Call KeyInputMode Parameter on Generated Accessors

When the source generator detects that an entity has a string key with a configured prefix and no typed parameter convenience overload (see [Computed Key Overloads](ComputedKeyOverloads.md)), it adds an optional `KeyInputMode mode = KeyInputMode.Default` parameter to the generated Get, Delete, Update, and ConditionCheck accessor methods. This lets you override the global default on a per-call basis.

### When the Parameter Appears

The `KeyInputMode mode` parameter is generated when **all** of the following are true:

1. At least one key is a `string` type with a configured prefix (e.g., `[PartitionKey(Prefix = "ORDER")]`)
2. No typed parameter convenience overload is generated for that entity (i.e., the entity has no computed key with ≥2 source properties, or the typed overload would be ambiguous)

The parameter is **not** generated when:
- No key has a prefix configured
- A typed overload exists (the typed overload handles raw values; the string overload is unambiguously for pre-built keys)
- All keys are non-string types (int, Guid, enum)

### Parameter Position

The parameter is positioned after key parameters and before any `CancellationToken`:

```csharp
// Generated accessor signature
public GetItemRequestBuilder<Order> Get(
    string pK, 
    string sK, 
    KeyInputMode mode = KeyInputMode.Default)
```

### Per-Call Override Examples

```csharp
[DynamoDbTable("Orders")]
public partial class Order
{
    [PartitionKey(Prefix = "ORDER")]
    [DynamoDbAttribute("pk")]
    public string Pk { get; set; } = string.Empty;

    [SortKey(Prefix = "LINE")]
    [DynamoDbAttribute("sk")]
    public string Sk { get; set; } = string.Empty;
}
```

#### Auto Mode (Default)

```csharp
// Default — resolves to Auto from FluentDynamoDbOptions
var order = await table.Orders.Get("12345", "001").GetItemAsync();
// DynamoDB receives pk = "ORDER#12345", sk = "LINE#001"
// (Auto detects missing prefix and prepends it)

// Already-prefixed values pass through unchanged
var order = await table.Orders.Get("ORDER#12345", "LINE#001").GetItemAsync();
// DynamoDB receives pk = "ORDER#12345", sk = "LINE#001"
```

#### Value Mode

```csharp
// Always prepend prefix — use when passing raw component values
var order = await table.Orders.Get("12345", "001", KeyInputMode.Value).GetItemAsync();
// DynamoDB receives pk = "ORDER#12345", sk = "LINE#001"
```

#### Raw Mode

```csharp
// Pass through unchanged — caller is responsible for correct key format
var order = await table.Orders.Get("ORDER#12345", "LINE#001", KeyInputMode.Raw).GetItemAsync();
// DynamoDB receives pk = "ORDER#12345", sk = "LINE#001"

// ⚠️ Without prefix, the raw value goes to DynamoDB as-is
var order = await table.Orders.Get("12345", "001", KeyInputMode.Raw).GetItemAsync();
// DynamoDB receives pk = "12345", sk = "001" — probably wrong!
```

### Convenience Async Methods

The same parameter propagates to `GetAsync`, `DeleteAsync`, and their FluentResults variants:

```csharp
// GetAsync convenience method
var order = await table.Orders.GetAsync("12345", "001", mode: KeyInputMode.Value);
// DynamoDB receives pk = "ORDER#12345", sk = "LINE#001"

// DeleteAsync convenience method
await table.Orders.DeleteAsync("12345", "001", mode: KeyInputMode.Raw);
```

### Table-Level Methods

Table-level methods also receive the `KeyInputMode` parameter and pass it through to the entity accessor:

```csharp
// Table-level Get
var order = await table.Get("12345", "001", KeyInputMode.Value).GetItemAsync();
```

### Interaction with Prefix Configuration

The `KeyInputMode` parameter interacts with each key's prefix independently:

```csharp
[DynamoDbTable("Mixed")]
public partial class MixedEntity
{
    [PartitionKey(Prefix = "PFX")]  // Has prefix
    [DynamoDbAttribute("pk")]
    public string Pk { get; set; } = string.Empty;

    [SortKey]  // No prefix
    [DynamoDbAttribute("sk")]
    public string Sk { get; set; } = string.Empty;
}

// The resolved mode applies to pk (which has prefix) but sk passes through unchanged
var item = await table.Mixed.Get("123", "sort-value", KeyInputMode.Value).GetItemAsync();
// pk: "PFX#123" (prefix applied via Value mode)
// sk: "sort-value" (no prefix configured — unchanged regardless of mode)
```

## Error Handling

| Scenario | Exception | Message |
|----------|-----------|---------|
| `UseKeyInputMode(KeyInputMode.Default)` | `ArgumentException` | "KeyInputMode.Default is only valid as a per-call parameter value. Specify Auto, Value, or Raw for the global default." |
| Null key value passed to prefix helper | `ArgumentNullException` | Parameter name: "value" |
| Invalid enum value (e.g., `(KeyInputMode)99`) | `ArgumentOutOfRangeException` | "Undefined KeyInputMode value: 99" |

## See Also

- [Configuration Guide](Configuration.md) — Full `FluentDynamoDbOptions` configuration reference
- [Entity Definition](EntityDefinition.md) — Defining entities with key prefixes
- [Computed Key Overloads](ComputedKeyOverloads.md) — Typed parameter overloads for computed keys
