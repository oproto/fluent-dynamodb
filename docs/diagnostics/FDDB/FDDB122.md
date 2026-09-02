# FDDB122: Cannot extract from constant key

## Code & Severity

| Field | Value |
|-------|-------|
| Code | `FDDB122` |
| Severity | Error |

## Message

`Property '{0}' has [Extracted] referencing constant key property '{1}' — extraction from a constant is invalid`

## Description

An `[Extracted]` attribute references a key property that has been detected as a constant key. Extraction splits a composite key into component parts, but a constant key has a fixed value with no variable components to extract.

When this diagnostic fires, code generation is halted for the affected entity.

## Example

The following code triggers this diagnostic:

```csharp
[DynamoDbTable("customers")]
public partial class Customer
{
    [PartitionKey]
    [DynamoDbAttribute("pk")]
    public string Pk { get; set; } = string.Empty;

    [SortKey]
    [DynamoDbAttribute("sk")]
    public string Sk => "PROFILE";  // Constant key

    [Extracted("Sk", 0)]
    [DynamoDbAttribute("skPart")]
    public string SkPart { get; set; } = string.Empty;  // Can't extract from constant
}
```

## Fix

Remove the `[Extracted]` attribute — there are no variable components in a constant key to extract:

```csharp
[DynamoDbTable("customers")]
public partial class Customer
{
    [PartitionKey]
    [DynamoDbAttribute("pk")]
    public string Pk { get; set; } = string.Empty;

    [SortKey]
    [DynamoDbAttribute("sk")]
    public string Sk => "PROFILE";

    // If you need the constant value elsewhere, reference it directly
    // or use a regular property
}
```
