# DYNDB001: Missing partition key

## Code & Severity

| Field | Value |
|-------|-------|
| Code | `DYNDB001` |
| Severity | Error |

## Message

`Entity '{0}' must have exactly one property marked with [PartitionKey]`

## Description

Every DynamoDB entity must have exactly one partition key property. The source generator requires a partition key to generate proper key handling, query builders, and CRUD operations for the entity.

## Example

The following code triggers this diagnostic:

```csharp
[DynamoDbTable("Orders")]
public partial class Order
{
    [DynamoDbAttribute("orderId")]
    public string OrderId { get; set; } = string.Empty;

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

    [DynamoDbAttribute("orderId")]
    public string OrderId { get; set; } = string.Empty;

    [DynamoDbAttribute("status")]
    public string Status { get; set; } = string.Empty;
}
```
