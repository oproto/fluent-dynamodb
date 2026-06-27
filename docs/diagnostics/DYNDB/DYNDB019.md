# DYNDB019: Potential key collision

## Code & Severity

| Field | Value |
|-------|-------|
| Code | `DYNDB019` |
| Severity | Warning |

## Message

`Key format '{0}' on property '{1}' may produce non-unique keys for different values`

## Description

Key formats should ensure uniqueness to avoid DynamoDB key collisions. If a key format can produce the same key string for different input values, items may overwrite each other silently in DynamoDB.

## Example

The following code triggers this diagnostic:

```csharp
[DynamoDbTable("Events")]
public partial class Event
{
    [PartitionKey]
    [DynamoDbAttribute("pk")]
    [Computed("Category", "SubCategory", Separator = "")]
    public string Pk { get; set; } = string.Empty;

    [Extracted("Pk", 0)]
    public string Category { get; set; } = string.Empty;

    [Extracted("Pk", 1)]
    public string SubCategory { get; set; } = string.Empty;
}
```

## Fix

The corrected version:

```csharp
[DynamoDbTable("Events")]
public partial class Event
{
    [PartitionKey]
    [DynamoDbAttribute("pk")]
    [Computed("Category", "SubCategory", Separator = "#")]
    public string Pk { get; set; } = string.Empty;

    [Extracted("Pk", 0)]
    public string Category { get; set; } = string.Empty;

    [Extracted("Pk", 1)]
    public string SubCategory { get; set; } = string.Empty;
}
```
