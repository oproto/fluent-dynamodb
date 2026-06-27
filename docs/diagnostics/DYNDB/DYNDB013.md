# DYNDB013: Collection property cannot be key

## Code & Severity

| Field | Value |
|-------|-------|
| Code | `DYNDB013` |
| Severity | Error |

## Message

`Collection property '{0}' in entity '{1}' cannot be marked as partition key or sort key`

## Description

Collection properties represent multiple values and cannot be used as DynamoDB keys. DynamoDB keys must be scalar values (string, number, or binary).

## Example

The following code triggers this diagnostic:

```csharp
[DynamoDbTable("Orders")]
public partial class Order
{
    [PartitionKey]
    [DynamoDbAttribute("pk")]
    public List<string> Pk { get; set; } = new();

    [DynamoDbAttribute("status")]
    public string Status { get; set; } = string.Empty;
}
```

## Fix

The corrected version:

```csharp
[DynamoDbTable("Orders")]
public partial class Order
{
    [PartitionKey]
    [DynamoDbAttribute("pk")]
    public string Pk { get; set; } = string.Empty;

    [DynamoDbAttribute("tags")]
    public List<string> Tags { get; set; } = new();

    [DynamoDbAttribute("status")]
    public string Status { get; set; } = string.Empty;
}
```
