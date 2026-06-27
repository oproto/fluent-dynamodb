# FDDB060: Projection source entity not found

## Code & Severity

| Field | Value |
|-------|-------|
| Code | `FDDB060` |
| Severity | Error |

## Message

`Projection '{0}' references source entity '{1}' which could not be found or is not a valid DynamoDB entity. Ensure the source entity exists and is marked with [DynamoDbTable] attribute.`

## Description

Projections must reference a valid DynamoDB entity as their source. The source entity must exist in the compilation and be properly configured with [DynamoDbTable] attribute.

The [DynamoDbProjection] attribute's type parameter must point to an existing entity class that has been configured with [DynamoDbTable]. If the referenced type doesn't exist or isn't a valid entity, the projection cannot inherit the required metadata.

## Example

The following code triggers this diagnostic:

```csharp
// Source entity doesn't exist or is missing [DynamoDbTable]
public class Order
{
    public string OrderId { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
}

[DynamoDbProjection(typeof(Order))]
public partial class OrderSummary
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
[DynamoDbTable("orders")]
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

[DynamoDbProjection(typeof(Order))]
public partial class OrderSummary
{
    [DynamoDbAttribute("orderId")]
    public string OrderId { get; set; } = string.Empty;

    [DynamoDbAttribute("status")]
    public string Status { get; set; } = string.Empty;
}
```
