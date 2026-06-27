# DYNDB101: Invalid TTL property type

## Code & Severity

| Field | Value |
|-------|-------|
| Code | `DYNDB101` |
| Severity | Error |

## Message

`[TimeToLive] can only be used on DateTime or DateTimeOffset properties, but property '{0}' is type '{1}'`

## Description

TTL properties must be DateTime or DateTimeOffset to support Unix epoch conversion. DynamoDB TTL requires a numeric attribute representing a Unix timestamp in seconds, and the source generator only knows how to convert DateTime and DateTimeOffset to this format.

## Example

The following code triggers this diagnostic:

```csharp
[DynamoDbTable("Sessions")]
public partial class Session
{
    [PartitionKey]
    [DynamoDbAttribute("pk")]
    public string Pk { get; set; } = string.Empty;

    [TimeToLive]
    [DynamoDbAttribute("expiresAt")]
    public string ExpiresAt { get; set; } = string.Empty;
}
```

## Fix

The corrected version:

```csharp
[DynamoDbTable("Sessions")]
public partial class Session
{
    [PartitionKey]
    [DynamoDbAttribute("pk")]
    public string Pk { get; set; } = string.Empty;

    [TimeToLive]
    [DynamoDbAttribute("expiresAt")]
    public DateTime ExpiresAt { get; set; }
}
```
