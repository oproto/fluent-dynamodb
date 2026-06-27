# DYNDB036: Invalid computed key format

## Code & Severity

| Field | Value |
|-------|-------|
| Code | `DYNDB036` |
| Severity | Warning |

## Message

`Computed property '{0}' has format '{1}' that may produce invalid keys: {2}`

## Description

Computed key formats should produce valid DynamoDB key values. Formats that can produce empty strings, excessively long keys, or keys with problematic characters are flagged as potentially invalid.

## Example

The following code triggers this diagnostic:

```csharp
[DynamoDbTable("Events")]
public partial class Event
{
    [PartitionKey]
    [DynamoDbAttribute("pk")]
    [Computed("Year", "Month", Separator = "#", Format = "{0}###{1}")]
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
