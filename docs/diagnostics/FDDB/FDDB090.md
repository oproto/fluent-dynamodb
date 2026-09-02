# FDDB090: Format placeholder count mismatch

## Code & Severity

| Field | Value |
|-------|-------|
| Code | `FDDB090` |
| Severity | Error |

## Message

`Computed property '{0}' has format '{1}' with {2} placeholders but {3} source properties`

## Description

The format string must contain exactly one placeholder ({0}, {1}, etc.) for each source property.

When a [Computed] attribute specifies an explicit Format string, the number of placeholders in that format must match the number of source properties listed. A mismatch means the computed key cannot be correctly assembled.

## Example

The following code triggers this diagnostic:

```csharp
[DynamoDbTable("events")]
public partial class Event
{
    [PartitionKey]
    [DynamoDbAttribute("pk")]
    [Computed("Year", "Month", "Day",
        Format = "{0}#{1}")]  // 2 placeholders, 3 sources
    public string Pk { get; set; } = string.Empty;

    [Extracted("Pk", 0)]
    public int Year { get; set; }

    [Extracted("Pk", 1)]
    public int Month { get; set; }

    [Extracted("Pk", 2)]
    public int Day { get; set; }
}
```

## Fix

The corrected version:

```csharp
[DynamoDbTable("events")]
public partial class Event
{
    [PartitionKey]
    [DynamoDbAttribute("pk")]
    [Computed("Year", "Month", "Day",
        Format = "{0}#{1}#{2}")]  // 3 placeholders for 3 sources
    public string Pk { get; set; } = string.Empty;

    [Extracted("Pk", 0)]
    public int Year { get; set; }

    [Extracted("Pk", 1)]
    public int Month { get; set; }

    [Extracted("Pk", 2)]
    public int Day { get; set; }
}
```
