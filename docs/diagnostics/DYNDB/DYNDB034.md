# DYNDB034: Self-referencing computed key

## Code & Severity

| Field | Value |
|-------|-------|
| Code | `DYNDB034` |
| Severity | Error |

## Message

`Computed property '{0}' cannot reference itself as a source property`

## Description

Computed properties cannot reference themselves as source properties. A computed key's value is built from other properties; it cannot be a source for its own computation.

## Example

The following code triggers this diagnostic:

```csharp
[DynamoDbTable("Items")]
public partial class Item
{
    [PartitionKey]
    [DynamoDbAttribute("pk")]
    [Computed("Pk", "Category", Separator = "#")]
    public string Pk { get; set; } = string.Empty;

    [Extracted("Pk", 1)]
    public string Category { get; set; } = string.Empty;
}
```

## Fix

The corrected version:

```csharp
[DynamoDbTable("Items")]
public partial class Item
{
    [PartitionKey]
    [DynamoDbAttribute("pk")]
    [Computed("TenantId", "Category", Separator = "#")]
    public string Pk { get; set; } = string.Empty;

    [Extracted("Pk", 0)]
    public string TenantId { get; set; } = string.Empty;

    [Extracted("Pk", 1)]
    public string Category { get; set; } = string.Empty;
}
```
