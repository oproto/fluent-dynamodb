# DYNDB025: Potential data loss

## Code & Severity

| Field | Value |
|-------|-------|
| Code | `DYNDB025` |
| Severity | Warning |

## Message

`Property '{0}' configuration may cause data loss during serialization: {1}`

## Description

Certain property configurations may result in data loss during DynamoDB serialization. This occurs when a type conversion loses precision or when a format truncates information that cannot be recovered on deserialization.

## Example

The following code triggers this diagnostic:

```csharp
[DynamoDbTable("Events")]
public partial class Event
{
    [PartitionKey]
    [DynamoDbAttribute("pk")]
    public string Pk { get; set; } = string.Empty;

    [DynamoDbAttribute("eventDate", Format = "yyyy-MM-dd")]
    public DateTime EventTimestamp { get; set; }
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
    public string Pk { get; set; } = string.Empty;

    [DynamoDbAttribute("eventTimestamp")]
    public DateTime EventTimestamp { get; set; }

    [DynamoDbAttribute("eventDate", Format = "yyyy-MM-dd")]
    public DateOnly EventDate { get; set; }
}
```
