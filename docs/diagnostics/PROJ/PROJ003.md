# PROJ003: Invalid projection source entity

## Code & Severity

| Field | Value |
|-------|-------|
| Code | `PROJ003` |
| Severity | Error |

## Message

`Source entity type '{0}' for projection '{1}' does not exist or is not a DynamoDB entity`

## Description

Projection source entity must be a valid DynamoDB entity class. The type passed to `[DynamoDbProjection(typeof(...))]` must reference an existing class that is decorated with `[DynamoDbTable]`.

This diagnostic is emitted when the source entity type cannot be found at compile time or when the referenced type is not a valid DynamoDB entity. This can occur when the type name is misspelled, the referenced class has been removed, or the type exists but lacks the `[DynamoDbTable]` attribute.

## Example

The following code triggers this diagnostic:

```csharp
// PROJ003: 'OrderEntity' does not exist or is not a DynamoDB entity
[DynamoDbProjection(typeof(OrderEntity))]
public partial class OrderSummary
{
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
