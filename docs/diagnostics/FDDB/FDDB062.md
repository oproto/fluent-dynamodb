# FDDB062: Projection interface violation

## Code & Severity

| Field | Value |
|-------|-------|
| Code | `FDDB062` |
| Severity | Error |

## Message

`Projection '{0}' cannot be used in this context. Projections are read-only and implement IReadOnlyEntity<T>, not IDynamoDbEntity<T>. For write operations (Put, Update, Delete), use the source entity '{1}' instead.`

## Description

Projections implement IReadOnlyEntity<T> which only supports read operations (Query, Get). For write operations, use the full source entity type that implements IDynamoDbEntity<T>.

Projections are intentionally read-only to enforce a clear separation between read and write models. They represent a subset of attributes optimized for querying, not for persisting data back to DynamoDB.

## Example

The following code triggers this diagnostic:

```csharp
[DynamoDbTable("orders")]
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

[DynamoDbProjection(typeof(Order))]
public partial class OrderSummary
{
    [DynamoDbAttribute("status")]
    public string Status { get; set; } = string.Empty;
}

// Attempting to write a projection:
// await table.Put(orderSummary).PutAsync();  // Error!
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

    [DynamoDbAttribute("status")]
    public string Status { get; set; } = string.Empty;

    [DynamoDbAttribute("total")]
    public decimal Total { get; set; }
}

// Use the full entity for write operations:
// await table.Put(order).PutAsync();  // Correct!
// Use the projection only for reads:
// var summary = await table.StatusIndex
//     .Query<OrderSummary>(x => x.Status == "active")
//     .ToListAsync();
```
