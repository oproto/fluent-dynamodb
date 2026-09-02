# FDDB120: Constant key conflicts with computed attribute

## Code & Severity

| Field | Value |
|-------|-------|
| Code | `FDDB120` |
| Severity | Error |

## Message

`Property '{0}' is a constant key but also has [Computed] — these are mutually exclusive`

## Description

A key property detected as a constant key (via expression-body or read-only auto-property syntax) cannot also have a `[Computed]` attribute. Constant keys return a fixed compile-time value, while computed keys combine multiple source properties at runtime. These concepts are mutually exclusive.

When this diagnostic fires, code generation is halted for the affected entity.

## Example

The following code triggers this diagnostic:

```csharp
[DynamoDbTable("orders")]
public partial class Order
{
    [PartitionKey]
    [DynamoDbAttribute("pk")]
    public string Pk { get; set; } = string.Empty;

    [SortKey]
    [DynamoDbAttribute("sk")]
    [Computed("Type", "Status", Separator = "#")]
    public string Sk => "PROFILE";  // Constant + Computed = conflict
}
```

## Fix

Remove either the constant value or the `[Computed]` attribute:

```csharp
// Option 1: Use constant key (no [Computed])
[SortKey]
[DynamoDbAttribute("sk")]
public string Sk => "PROFILE";

// Option 2: Use computed key (no constant value)
[SortKey]
[DynamoDbAttribute("sk")]
[Computed("Type", "Status", Separator = "#")]
public string Sk { get; set; } = string.Empty;
```
