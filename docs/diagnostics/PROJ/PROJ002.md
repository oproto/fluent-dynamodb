# PROJ002: Projection property type mismatch

## Code & Severity

| Field | Value |
|-------|-------|
| Code | `PROJ002` |
| Severity | Error |

## Message

`Property '{0}' type '{1}' on projection '{2}' does not match source entity type '{3}'`

## Description

Projection property types must match the corresponding source entity property types. When a projection declares a property that exists on the source entity, the type of that property must be identical.

This diagnostic is emitted when a projection property has a different type than the matching property on the source entity. Type mismatches prevent the source generator from producing correct mapping code between the DynamoDB item and the projection model.

## Example

The following code triggers this diagnostic:

```csharp
[DynamoDbTable("Orders")]
public partial class Order
{
    [PartitionKey]
    [DynamoDbAttribute("pk")]
    public string Pk { get; set; } = string.Empty;

    [DynamoDbAttribute("totalAmount")]
    public decimal TotalAmount { get; set; }
}

[DynamoDbProjection(typeof(Order))]
public partial class OrderSummary
{
    // PROJ002: type 'string' does not match source type 'decimal'
    [DynamoDbAttribute("totalAmount")]
    public string TotalAmount { get; set; } = string.Empty;
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

    [DynamoDbAttribute("totalAmount")]
    public decimal TotalAmount { get; set; }
}

[DynamoDbProjection(typeof(Order))]
public partial class OrderSummary
{
    [DynamoDbAttribute("totalAmount")]
    public decimal TotalAmount { get; set; }
}
```
