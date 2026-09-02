# PROJ101: Projection includes all properties

## Code & Severity

| Field | Value |
|-------|-------|
| Code | `PROJ101` |
| Severity | Warning |

## Message

`Projection '{0}' includes all properties from source entity '{1}'. Consider using the full entity type instead for better performance.`

## Description

Projections that include all properties provide no optimization benefit over using the full entity type. The purpose of a projection is to reduce the amount of data read from DynamoDB by selecting a subset of attributes.

When a projection includes every property from the source entity, it adds indirection without any read cost savings. In this case, using the full entity type directly is simpler and avoids the overhead of maintaining a separate projection class.

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

    [DynamoDbAttribute("total")]
    public decimal Total { get; set; }
}

// PROJ101: includes all properties from Order
[DynamoDbProjection(typeof(Order))]
public partial class OrderProjection
{
    [DynamoDbAttribute("status")]
    public string Status { get; set; } = string.Empty;

    [DynamoDbAttribute("total")]
    public decimal Total { get; set; }
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

    [DynamoDbAttribute("total")]
    public decimal Total { get; set; }
}

// Use Order directly instead of a projection, or reduce properties:
[DynamoDbProjection(typeof(Order))]
public partial class OrderStatusProjection
{
    [DynamoDbAttribute("status")]
    public string Status { get; set; } = string.Empty;
}
```
