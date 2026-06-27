# PROJ001: Projection property not found

## Code & Severity

| Field | Value |
|-------|-------|
| Code | `PROJ001` |
| Severity | Error |

## Message

`Property '{0}' on projection '{1}' does not exist on source entity '{2}'`

## Description

All properties in a projection model must exist on the source entity. When you define a projection class with `[DynamoDbProjection(typeof(SourceEntity))]`, every property declared in the projection must have a corresponding property with the same name on the source entity.

This diagnostic is emitted when a property name in the projection does not match any property on the referenced source entity. This typically occurs due to a typo in the property name or when the source entity has been refactored without updating the projection.

## Example

The following code triggers this diagnostic:

```csharp
[DynamoDbTable("Orders")]
public partial class Order
{
    [PartitionKey]
    [DynamoDbAttribute("pk")]
    public string Pk { get; set; } = string.Empty;

    [DynamoDbAttribute("status")]
    public string Status { get; set; } = string.Empty;
}

[DynamoDbProjection(typeof(Order))]
public partial class OrderSummary
{
    // PROJ001: 'OrderStatus' does not exist on Order
    [DynamoDbAttribute("status")]
    public string OrderStatus { get; set; } = string.Empty;
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

    [DynamoDbAttribute("status")]
    public string Status { get; set; } = string.Empty;
}

[DynamoDbProjection(typeof(Order))]
public partial class OrderSummary
{
    [DynamoDbAttribute("status")]
    public string Status { get; set; } = string.Empty;
}
```
