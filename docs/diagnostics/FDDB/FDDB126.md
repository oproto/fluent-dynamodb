# FDDB126: Key property references non-compile-time-constant value

## Code & Severity

| Field | Value |
|-------|-------|
| Code | `FDDB126` |
| Severity | Error |

## Message

`Property '{0}' uses expression-body or read-only auto-property syntax but its value is not a compile-time constant — use a string literal or a 'const' field instead`

## Description

A key property (annotated with `[PartitionKey]` or `[SortKey]`) uses expression-body (`=>`) or read-only auto-property (`{ get; }`) syntax, but its value is not a compile-time constant. References to `static readonly` fields, properties, or method calls cannot be resolved at compile time and will produce uncompilable generated code.

The source generator detects constant key values so it can skip property assignment in the `FromDynamoDb()` mapper (since the property has no setter). When the value cannot be resolved as a compile-time constant, the generator cannot safely generate code for this property.

Use a string literal (e.g., `=> "VALUE"`) or a `const` field (e.g., `=> MyConstants.Value` where `Value` is `const`) instead.

## Example

The following code triggers this diagnostic:

```csharp
public static class StaticFields
{
    public static readonly string KeyValue = "PROFILE";  // readonly, NOT const
}

[DynamoDbTable("customers")]
public partial class Customer
{
    [PartitionKey]
    [DynamoDbAttribute("pk")]
    public string Pk { get; set; } = string.Empty;

    [SortKey]
    [DynamoDbAttribute("sk")]
    public string Sk => StaticFields.KeyValue;  // ❌ FDDB126: not a compile-time constant
}
```

Other patterns that trigger this diagnostic:

```csharp
// Read-only auto-property with static readonly initializer
[SortKey]
[DynamoDbAttribute("sk")]
public string Sk { get; } = StaticFields.KeyValue;  // ❌ FDDB126

// Expression-body with method call
[SortKey]
[DynamoDbAttribute("sk")]
public string Sk => GetKey();  // ❌ FDDB126

// Expression-body with property access
[SortKey]
[DynamoDbAttribute("sk")]
public string Sk => Config.DefaultKey;  // ❌ FDDB126
```

## Fix

Use a string literal or a `const` field reference:

```csharp
// Option 1: String literal (simplest)
[SortKey]
[DynamoDbAttribute("sk")]
public string Sk => "PROFILE";  // ✅ Compile-time constant

// Option 2: Const field reference
public const string ProfileKey = "PROFILE";

[SortKey]
[DynamoDbAttribute("sk")]
public string Sk => ProfileKey;  // ✅ Const field resolves at compile time

// Option 3: External const field
public static class Keys
{
    public const string Profile = "PROFILE";
}

[SortKey]
[DynamoDbAttribute("sk")]
public string Sk => Keys.Profile;  // ✅ Const field resolves at compile time
```
