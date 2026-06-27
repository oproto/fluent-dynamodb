# DYNDB035: Invalid extracted key index

## Code & Severity

| Field | Value |
|-------|-------|
| Code | `DYNDB035` |
| Severity | Error |

## Message

`Extracted property '{0}' has invalid index {1} for source property '{2}'`

## Description

Extracted property index must be valid for the expected number of components in the source property. The index is zero-based and must be less than the number of source properties in the [Computed] attribute on the target property.

## Example

The following code triggers this diagnostic:

```csharp
[DynamoDbTable("Events")]
public partial class Event
{
    [PartitionKey]
    [DynamoDbAttribute("pk")]
    [Computed("Year", "Month", Separator = "#")]
    public string Pk { get; set; } = string.Empty;

    [Extracted("Pk", 0)]
    public int Year { get; set; }

    [Extracted("Pk", 5)]
    public int Month { get; set; }
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
    [Computed("Year", "Month", Separator = "#")]
    public string Pk { get; set; } = string.Empty;

    [Extracted("Pk", 0)]
    public int Year { get; set; }

    [Extracted("Pk", 1)]
    public int Month { get; set; }
}
```
