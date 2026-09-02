# DYNDB006: Invalid GSI configuration

## Code & Severity

| Field | Value |
|-------|-------|
| Code | `DYNDB006` |
| Severity | Error |

## Message

`Global Secondary Index '{0}' on entity '{1}' must have at least a partition key`

## Description

Every Global Secondary Index must have at least a partition key property. A GSI without a partition key is invalid because DynamoDB requires a partition key for every index to distribute data across partitions.

## Example

The following code triggers this diagnostic:

```csharp
[DynamoDbTable("Orders")]
public partial class Order
{
    [PartitionKey]
    [DynamoDbAttribute("pk")]
    public string Pk { get; set; } = string.Empty;

    [GsiSortKey("status-index")]
    [DynamoDbAttribute("createdAt")]
    public DateTime CreatedAt { get; set; }
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

    [GsiPartitionKey("status-index")]
    [DynamoDbAttribute("status")]
    public string Status { get; set; } = string.Empty;

    [GsiSortKey("status-index")]
    [DynamoDbAttribute("createdAt")]
    public DateTime CreatedAt { get; set; }
}
```
