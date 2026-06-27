# DYNDB033: Circular key dependency

## Code & Severity

| Field | Value |
|-------|-------|
| Code | `DYNDB033` |
| Severity | Error |

## Message

`Circular dependency detected between computed properties: {0}`

## Description

Computed properties cannot have circular dependencies on each other. The source generator resolves computed property values in dependency order, and circular references make this resolution impossible.

## Example

The following code triggers this diagnostic:

```csharp
[DynamoDbTable("Items")]
public partial class Item
{
    [PartitionKey]
    [DynamoDbAttribute("pk")]
    [Computed("Category", Separator = "#")]
    public string Pk { get; set; } = string.Empty;

    [SortKey]
    [DynamoDbAttribute("sk")]
    [Computed("Pk", Separator = "#")]
    public string Sk { get; set; } = string.Empty;

    [Extracted("Sk", 0)]
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
    [Computed("Category", Separator = "#")]
    public string Pk { get; set; } = string.Empty;

    [SortKey]
    [DynamoDbAttribute("sk")]
    [Computed("SubCategory", Separator = "#")]
    public string Sk { get; set; } = string.Empty;

    [Extracted("Pk", 0)]
    public string Category { get; set; } = string.Empty;

    [Extracted("Sk", 0)]
    public string SubCategory { get; set; } = string.Empty;
}
```
