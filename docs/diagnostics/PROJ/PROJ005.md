# PROJ005: UseProjection references invalid type

## Code & Severity

| Field | Value |
|-------|-------|
| Code | `PROJ005` |
| Severity | Error |

## Message

`UseProjection attribute on GSI '{0}' references non-existent or invalid projection type '{1}'`

## Description

UseProjection attribute must reference a valid projection model type. When you apply `[UseProjection(typeof(...))]` to a GSI definition, the referenced type must be a class decorated with `[DynamoDbProjection]`.

This diagnostic is emitted when the type referenced in `[UseProjection]` does not exist, is not a projection class, or cannot be resolved at compile time. Verify that the type name is correct and that the class has the `[DynamoDbProjection]` attribute.

## Example

The following code triggers this diagnostic:

```csharp
[DynamoDbTable("Orders")]
public partial class Order
{
    [PartitionKey]
    [DynamoDbAttribute("pk")]
    public string Pk { get; set; } = string.Empty;

    // PROJ005: 'InvalidProjection' is non-existent or invalid
    [GsiPartitionKey("status-index")]
    [UseProjection(typeof(InvalidProjection))]
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

    [GsiPartitionKey("status-index")]
    [UseProjection(typeof(OrderSummary))]
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
