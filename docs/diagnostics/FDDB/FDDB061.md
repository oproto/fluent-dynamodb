# FDDB061: Projection metadata inheritance failure

## Code & Severity

| Field | Value |
|-------|-------|
| Code | `FDDB061` |
| Severity | Error |

## Message

`Projection '{0}' cannot inherit metadata from source entity '{1}'. Ensure the source entity has proper DynamoDB attributes and metadata including partition key configuration.`

## Description

Projections inherit metadata (table name, partition key, sort key) from their source entity. The source entity must have valid metadata configuration for inheritance to succeed.

This error occurs when the source entity exists but is missing critical configuration such as a partition key, or has invalid attribute definitions that prevent the projection from inheriting the necessary metadata for query operations.

## Example

The following code triggers this diagnostic:

```csharp
// Source entity exists but is missing partition key
[DynamoDbTable("orders")]
public partial class Order
{
    // Missing [PartitionKey] attribute
    [DynamoDbAttribute("pk")]
    public string Pk { get; set; } = string.Empty;

    [DynamoDbAttribute("orderId")]
    public string OrderId { get; set; } = string.Empty;
}

[DynamoDbProjection(typeof(Order))]
public partial class OrderSummary
{
    [DynamoDbAttribute("orderId")]
    public string OrderId { get; set; } = string.Empty;
}
```

## Fix

The corrected version:

```csharp
[DynamoDbTable("orders")]
public partial class Order
{
    [PartitionKey]
    [DynamoDbAttribute("pk")]
    public string Pk { get; set; } = string.Empty;

    [DynamoDbAttribute("orderId")]
    public string OrderId { get; set; } = string.Empty;
}

[DynamoDbProjection(typeof(Order))]
public partial class OrderSummary
{
    [DynamoDbAttribute("orderId")]
    public string OrderId { get; set; } = string.Empty;
}
```
