# Constant Key Detection

This guide explains the constant key detection feature, which allows the source generator to recognize key properties that return a fixed compile-time string value. When a `[PartitionKey]` or `[SortKey]` property is detected as constant, the generator automatically simplifies the Keys class, convenience methods, serialization, and deserialization — and auto-derives a discriminator pattern without manual configuration.

## Overview

In single-table designs, entities often use a fixed sort key value (e.g., `"PROFILE"`) to represent a specific item type. Previously, you'd need to manually configure `DiscriminatorProperty` and `DiscriminatorValue` on `[DynamoDbTable]` and always pass the known constant to convenience methods. Constant key detection eliminates that boilerplate.

The source generator detects constant keys via two C# patterns:
1. **Expression-body syntax**: `public string Sk => "PROFILE";`
2. **Read-only auto-property syntax**: `public string Sk { get; } = "PROFILE";`

When detected, the constant value propagates through the entire generation pipeline.

## Expression-Body Syntax

Use the `=>` syntax to declare a constant key concisely:

```csharp
[DynamoDbTable("Customers")]
public partial class Customer
{
    [PartitionKey(Prefix = "CUSTOMER")]
    [DynamoDbAttribute("pk")]
    public string CustomerId { get; set; } = string.Empty;

    [SortKey]
    [DynamoDbAttribute("sk")]
    public string Sk => "PROFILE";  // Constant key — expression body
}
```

The property must return a string literal or a reference to a `const string` field that the compiler can resolve at build time.

### Const Field References

You can also reference a `const` field from the same compilation:

```csharp
public static class KeyConstants
{
    public const string ProfileSk = "PROFILE";
}

[DynamoDbTable("Customers")]
public partial class Customer
{
    [PartitionKey(Prefix = "CUSTOMER")]
    [DynamoDbAttribute("pk")]
    public string CustomerId { get; set; } = string.Empty;

    [SortKey]
    [DynamoDbAttribute("sk")]
    public string Sk => KeyConstants.ProfileSk;  // Resolves to "PROFILE"
}
```

## Read-Only Auto-Property Syntax

Alternatively, use a get-only auto-property with an initializer:

```csharp
[DynamoDbTable("Config")]
public partial class AppConfig
{
    [PartitionKey]
    [DynamoDbAttribute("pk")]
    public string Pk { get; } = "APP_CONFIG";  // Constant key — read-only auto-property

    [SortKey]
    [DynamoDbAttribute("sk")]
    public string Sk { get; } = "SETTINGS";    // Constant key — read-only auto-property
}
```

The property must:
- Have only a `get` accessor (no `set` or `init`)
- Have an initializer that resolves to a compile-time constant string

### What Is NOT Detected

The following patterns are **not** detected as constant keys:

```csharp
// ❌ Has a setter — not constant
public string Sk { get; set; } = "PROFILE";

// ❌ Has an init accessor — not constant
public string Sk { get; init; } = "PROFILE";

// ❌ Method call — not a compile-time constant
public string Sk => GetSortKey();

// ❌ Interpolated string — not a compile-time constant
public string Sk => $"PROFILE_{Version}";

// ❌ Conditional expression — not a compile-time constant
public string Sk => IsActive ? "ACTIVE" : "INACTIVE";

// ❌ Property access — not a compile-time constant
public string Sk => SomeOtherProperty;
```

## Keys Class Behavior

When a key is constant, the generated Keys class changes:

### Parameterless Accessor

Instead of a parameterized method, the Keys class provides a parameterless static property:

```csharp
// Generated for constant sort key:
Customer.Keys.Sk       // Returns "PROFILE" (parameterless property)

// Compare with a variable key:
Customer.Keys.Pk("123")  // Returns "CUSTOMER#123" (parameterized method)
```

Use `Pk()` and `Sk` independently to construct key values:

## Convenience Method Simplification

Generated `Get`, `Delete`, and `Update` methods omit parameters for constant keys:

```csharp
// Variable PK + Constant SK → single parameter methods
var customer = await table.Customers.Get(customerId).GetItemAsync();
await table.Customers.Delete(customerId).DeleteAsync();
await table.Customers.Update(customerId).Set(...).UpdateAsync();

// Convenience async methods also simplified:
var customer = await table.Customers.GetAsync(customerId);
await table.Customers.DeleteAsync(customerId);

// Table-level methods follow the same pattern:
var customer = await table.Get<Customer>(customerId).GetItemAsync();
```

The constant sort key value `"PROFILE"` is injected internally when constructing the DynamoDB request — you never need to pass it.

### All Keys Constant

When both partition key and sort key are constant, methods become parameterless:

```csharp
var config = await table.AppConfig.Get().GetItemAsync();
await table.AppConfig.Delete().DeleteAsync();
```

## Serialization Behavior

### ToDynamoDb

The generated `ToDynamoDb` method emits the constant value directly without reading from the entity instance:

```csharp
// Generated serialization for constant SK:
item["sk"] = new AttributeValue { S = "PROFILE" };
// The property value is NOT read from the entity — it may not have a setter
```

This ensures correct serialization regardless of property accessibility.

### FromDynamoDb (Deserialization)

During deserialization, the generated code validates the incoming value:

- **Value matches**: No action needed — the property retains its declared constant value
- **Value mismatch**: Logs a warning via `IDynamoDbLogger` with the expected and actual values
- **Attribute missing**: Logs a warning indicating the expected attribute was absent

```csharp
// If DynamoDB returns sk = "WRONG_VALUE":
// Logger output: "Expected constant key 'sk' = "PROFILE" but got "WRONG_VALUE""

// If DynamoDB item is missing the 'sk' attribute entirely:
// Logger output: "Expected constant key attribute 'sk' was missing from item"
```

The property is **never assigned** during deserialization — expression-body properties have no setter, and read-only auto-properties are set only by their initializer.

## Update Model Exclusion

Constant key properties are automatically excluded from generated update model classes. Since the value cannot change, there's no reason to include it in update expressions:

```csharp
// CustomerUpdateModel does NOT include Sk — it's constant
await table.Customers.Update(customerId)
    .Set(x => new CustomerUpdateModel { Name = "New Name" })
    .UpdateAsync();
```

## Auto-Discriminator Derivation

Constant keys automatically derive a discriminator pattern using the `ExactMatch` strategy. This means you don't need to manually specify `DiscriminatorProperty` or `DiscriminatorValue`:

```csharp
// No DiscriminatorProperty/DiscriminatorValue needed!
[DynamoDbTable("shared-table")]
public partial class Customer
{
    [PartitionKey(Prefix = "CUSTOMER")]
    [DynamoDbAttribute("pk")]
    public string CustomerId { get; set; } = string.Empty;

    [SortKey]
    [DynamoDbAttribute("sk")]
    public string Sk => "PROFILE";
    // Auto-derives: ExactMatch discriminator with value "PROFILE" on "sk"
}
```

When an entity has both a constant sort key and a variable partition key with a prefix-derived pattern, the sort key pattern is preferred as the primary discriminator.

## Diagnostics

The source generator emits compile-time errors for invalid constant key configurations:

| ID | Severity | Description |
|----|----------|-------------|
| FDDB120 | Error | Constant key conflicts with `[Computed]` attribute — these are mutually exclusive |
| FDDB121 | Error | Prefix not applicable to constant key — prefix is meaningless on a constant value |
| FDDB122 | Error | Cannot extract from constant key — `[Extracted]` referencing a constant key is invalid |
| FDDB123 | Error | Empty constant key value — keys must contain at least one non-whitespace character |

All four diagnostics halt code generation for the affected entity to prevent invalid output.

### FDDB120 — Constant Key + Computed

```csharp
// ❌ Error: Property 'Sk' is a constant key but also has [Computed]
[SortKey]
[DynamoDbAttribute("sk")]
[Computed("Type", "Status")]
public string Sk => "PROFILE";
```

**Fix:** Remove either the constant value or the `[Computed]` attribute.

### FDDB121 — Constant Key + Prefix

```csharp
// ❌ Error: Property 'Sk' is a constant key but has Prefix configured
[SortKey(Prefix = "TYPE")]
[DynamoDbAttribute("sk")]
public string Sk => "PROFILE";
```

**Fix:** Remove the `Prefix` — the constant value already contains the exact key value.

### FDDB122 — Extracted from Constant Key

```csharp
// ❌ Error: Property 'ProfileType' has [Extracted] referencing constant key 'Sk'
[SortKey]
[DynamoDbAttribute("sk")]
public string Sk => "PROFILE";

[Extracted("Sk", 0)]
public string ProfileType { get; set; } = string.Empty;
```

**Fix:** Remove the `[Extracted]` attribute — constant keys have no variable components to extract.

### FDDB123 — Empty Constant Key Value

```csharp
// ❌ Error: Property 'Sk' has empty constant key value
[SortKey]
[DynamoDbAttribute("sk")]
public string Sk => "";

// ❌ Also an error for whitespace-only values:
public string Sk => "   ";
```

**Fix:** Provide a meaningful non-whitespace string value.

## Complete Example

```csharp
[DynamoDbTable("shared-table")]
public partial class CustomerProfile
{
    [PartitionKey(Prefix = "CUSTOMER")]
    [DynamoDbAttribute("pk")]
    public string CustomerId { get; set; } = string.Empty;

    [SortKey]
    [DynamoDbAttribute("sk")]
    public string Sk => "PROFILE";  // Constant key

    [DynamoDbAttribute("name")]
    public string Name { get; set; } = string.Empty;

    [DynamoDbAttribute("email")]
    public string Email { get; set; } = string.Empty;
}

// Usage:
// Keys class — only PK is parameterized
var pk = CustomerProfile.Keys.Pk("cust-123");     // "CUSTOMER#cust-123"
var sk = CustomerProfile.Keys.Sk;                  // "PROFILE"

// Convenience methods — only PK parameter needed
var profile = await table.CustomerProfiles.Get("cust-123").GetItemAsync();
await table.CustomerProfiles.Delete("cust-123").DeleteAsync();
await table.CustomerProfiles.Update("cust-123")
    .Set(x => new CustomerProfileUpdateModel { Email = "new@example.com" })
    .UpdateAsync();

// No manual discriminator configuration needed — auto-derived from constant SK
```

## See Also

- [Entity Definition](EntityDefinition.md) — Defining entities with key attributes
- [Key Input Mode](KeyInputMode.md) — Controlling prefix application on key values
- [Computed Key Overloads](ComputedKeyOverloads.md) — Typed parameter overloads for computed keys
