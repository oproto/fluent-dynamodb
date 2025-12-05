# Entity Definition Patterns

This steering file defines the correct patterns for defining DynamoDB entities in Oproto.FluentDynamoDb. Follow these patterns to ensure your entities work correctly with the source generator.

## Attribute Usage

### Table Entities: Use `[DynamoDbTable]` Only

For entities that represent items stored directly in a DynamoDB table, use only the `[DynamoDbTable]` attribute:

```csharp
// ✅ CORRECT: Table entity with [DynamoDbTable] only
[DynamoDbTable("Orders")]
public partial class Order
{
    [PartitionKey(Prefix = "ORDER")]
    [DynamoDbAttribute("pk")]
    public string Pk { get; set; } = string.Empty;

    [SortKey]
    [DynamoDbAttribute("sk")]
    public string Sk { get; set; } = string.Empty;
    
    // ... other properties
}
```

```csharp
// ❌ INCORRECT: Do NOT combine [DynamoDbEntity] with [DynamoDbTable]
[DynamoDbEntity]  // ❌ Remove this
[DynamoDbTable("Orders")]
public partial class Order { }
```

### Nested Types: Use `[DynamoDbEntity]`

The `[DynamoDbEntity]` attribute is **only** for nested types used with `[DynamoDbMap]` that need AOT-compatible mapping:

```csharp
// ✅ CORRECT: Nested type with [DynamoDbEntity]
[DynamoDbEntity]
public partial class Address
{
    [DynamoDbAttribute("street")]
    public string Street { get; set; } = string.Empty;
    
    [DynamoDbAttribute("city")]
    public string City { get; set; } = string.Empty;
}

// Used in a table entity:
[DynamoDbTable("Customers")]
public partial class Customer
{
    [DynamoDbMap]
    [DynamoDbAttribute("address")]
    public Address ShippingAddress { get; set; } = new();
}
```

## Interface Implementation

### Do NOT Manually Implement `IDynamoDbEntity`

The source generator automatically adds the `IDynamoDbEntity` interface to your entity's partial class. Never add it manually:

```csharp
// ✅ CORRECT: Let the source generator add the interface
[DynamoDbTable("Orders")]
public partial class Order
{
    // ... properties
}

// ❌ INCORRECT: Do NOT manually implement IDynamoDbEntity
[DynamoDbTable("Orders")]
public partial class Order : IDynamoDbEntity  // ❌ Remove this
{
    // ... properties
}
```

## Key Configuration

### Use Key Prefix Properties

Configure key prefixes using the `Prefix` property on `[PartitionKey]` and `[SortKey]` attributes:

```csharp
[DynamoDbTable("Orders")]
public partial class Order
{
    // Generates keys like "ORDER#12345"
    [PartitionKey(Prefix = "ORDER")]
    [DynamoDbAttribute("pk")]
    public string Pk { get; set; } = string.Empty;

    // No prefix - uses raw value
    [SortKey]
    [DynamoDbAttribute("sk")]
    public string Sk { get; set; } = string.Empty;
}

[DynamoDbTable("Orders")]
public partial class OrderLine
{
    // Same prefix as Order - shares partition key space
    [PartitionKey(Prefix = "ORDER")]
    [DynamoDbAttribute("pk")]
    public string Pk { get; set; } = string.Empty;

    // Generates keys like "LINE#abc123"
    [SortKey(Prefix = "LINE")]
    [DynamoDbAttribute("sk")]
    public string Sk { get; set; } = string.Empty;
}
```

### Key Attribute Properties

| Property | Default | Description |
|----------|---------|-------------|
| `Prefix` | `null` | Optional prefix prepended to key values (e.g., `"ORDER"`, `"USER"`) |
| `Separator` | `"#"` | Separator between prefix and value (e.g., `"#"`, `"_"`, `":"`) |

**Examples:**
```csharp
// Generates: "ORDER#12345"
[PartitionKey(Prefix = "ORDER")]

// Generates: "USER_12345"
[PartitionKey(Prefix = "USER", Separator = "_")]

// Generates: "12345" (no prefix)
[PartitionKey]
```

### Do NOT Write Manual Key Methods

The source generator creates a `Keys` class with `Pk()` and `Sk()` methods. Do not write manual `CreatePk()` or `CreateSk()` methods:

```csharp
// ✅ CORRECT: Use source-generated Keys class
var pk = Order.Keys.Pk(orderId);      // Returns "ORDER#12345"
var sk = OrderLine.Keys.Sk(lineId);   // Returns "LINE#abc123"

// ❌ INCORRECT: Do NOT write manual key methods
public partial class Order
{
    // ❌ Remove these - use Order.Keys.Pk() instead
    public static string CreatePk(string orderId) => $"ORDER#{orderId}";
    public static string CreateSk() => MetaSk;
}
```

### Constant Values for Documentation

You MAY keep constant values for documentation purposes, but use the generated `Keys` class for actual key construction:

```csharp
[DynamoDbTable("Orders")]
public partial class Order
{
    // ✅ OK: Constant for documentation
    public const string MetaSk = "META";
    
    // ... properties
}

// Usage: Use the constant directly for fixed values
var order = new Order { Sk = Order.MetaSk };
```

## Quick Reference

| Pattern | Correct | Incorrect |
|---------|---------|-----------|
| Table entity attribute | `[DynamoDbTable("Name")]` | `[DynamoDbEntity]` + `[DynamoDbTable]` |
| Nested type attribute | `[DynamoDbEntity]` | `[DynamoDbTable]` on nested types |
| Interface | Let source generator add it | `: IDynamoDbEntity` manually |
| Key construction | `Entity.Keys.Pk(value)` | `Entity.CreatePk(value)` |
| Key prefix | `[PartitionKey(Prefix = "X")]` | Manual string interpolation |

## Common Mistakes to Avoid

1. **Combining attributes**: Never use both `[DynamoDbEntity]` and `[DynamoDbTable]` on the same class
2. **Manual interface**: Never add `: IDynamoDbEntity` to your class declaration
3. **Manual key methods**: Never write `CreatePk()` or `CreateSk()` methods - use the generated `Keys` class
4. **Missing partial**: Always declare entity classes as `partial` to allow source generation
