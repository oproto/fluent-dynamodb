# DYNDB032: Invalid extracted key source

## Code & Severity

| Field | Value |
|-------|-------|
| Code | `DYNDB032` |
| Severity | Error |

## Message

`Extracted property '{0}' references non-existent source property '{1}'`

## Description

Extracted properties must reference existing properties in the same entity. The [Extracted] attribute's first parameter must be the name of a property that has a [Computed] attribute, so that values can be extracted from it.

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

    [Extracted("CompositeKey", 0)]
    public int Year { get; set; }

    [Extracted("Pk", 1)]
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
