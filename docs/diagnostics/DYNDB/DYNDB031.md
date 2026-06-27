# DYNDB031: Invalid computed key source

## Code & Severity

| Field | Value |
|-------|-------|
| Code | `DYNDB031` |
| Severity | Error |

## Message

`Computed property '{0}' references non-existent source property '{1}'`

## Description

Computed properties must reference existing properties in the same entity. The source generator uses these references to build computed key values from the specified source properties at runtime.

## Example

The following code triggers this diagnostic:

```csharp
[DynamoDbTable("Events")]
public partial class Event
{
    [PartitionKey]
    [DynamoDbAttribute("pk")]
    [Computed("Year", "MonthDay", Separator = "#")]
    public string Pk { get; set; } = string.Empty;

    [Extracted("Pk", 0)]
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
