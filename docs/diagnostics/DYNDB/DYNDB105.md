# DYNDB105: Multiple TTL fields

## Code & Severity

| Field | Value |
|-------|-------|
| Code | `DYNDB105` |
| Severity | Error |

## Message

`Entity '{0}' has multiple [TimeToLive] properties, but only one TTL field is allowed per entity`

## Description

DynamoDB entities can only have one TTL field. DynamoDB tables support a single TTL attribute, and the source generator enforces this constraint to prevent configuration confusion.

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
    public DateTime ExpiresAt { get; set; }

    [TimeToLive]
    [DynamoDbAttribute("softDeleteAt")]
    public DateTime SoftDeleteAt { get; set; }
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

    [DynamoDbAttribute("softDeleteAt")]
    public DateTime SoftDeleteAt { get; set; }
}
```
