# PROJ006: Conflicting UseProjection attributes

## Code & Severity

| Field | Value |
|-------|-------|
| Code | `PROJ006` |
| Severity | Error |

## Message

`GSI '{0}' has multiple conflicting UseProjection attributes specifying different projection types`

## Description

A GSI can only have one projection type constraint across all entities. When multiple entities in a multi-entity table define the same GSI, they must agree on the projection type used for that index.

This diagnostic is emitted when two or more `[UseProjection]` attributes on the same GSI reference different projection types. Resolve the conflict by ensuring all entities sharing the index use the same projection type, or remove the conflicting attributes.

## Example

The following code triggers this diagnostic:

```csharp
[DynamoDbTable("shared-table", IsDefault = true)]
public partial class Order
{
    [PartitionKey]
    [DynamoDbAttribute("pk")]
    public string Pk { get; set; } = string.Empty;

    [GsiPartitionKey("gsi1")]
    [UseProjection(typeof(OrderSummary))]
    [DynamoDbAttribute("gsi1pk")]
    public string Gsi1Pk { get; set; } = string.Empty;
}

[DynamoDbTable("shared-table")]
public partial class Customer
{
    [PartitionKey]
    [DynamoDbAttribute("pk")]
    public string Pk { get; set; } = string.Empty;

    // PROJ006: gsi1 already uses OrderSummary
    [GsiPartitionKey("gsi1")]
    [UseProjection(typeof(CustomerSummary))]
    [DynamoDbAttribute("gsi1pk")]
    public string Gsi1Pk { get; set; } = string.Empty;
}
```

## Fix

The corrected version:

```csharp
[DynamoDbTable("shared-table", IsDefault = true)]
public partial class Order
{
    [PartitionKey]
    [DynamoDbAttribute("pk")]
    public string Pk { get; set; } = string.Empty;

    [GsiPartitionKey("gsi1")]
    [UseProjection(typeof(SharedSummary))]
    [DynamoDbAttribute("gsi1pk")]
    public string Gsi1Pk { get; set; } = string.Empty;
}

[DynamoDbTable("shared-table")]
public partial class Customer
{
    [PartitionKey]
    [DynamoDbAttribute("pk")]
    public string Pk { get; set; } = string.Empty;

    [GsiPartitionKey("gsi1")]
    [UseProjection(typeof(SharedSummary))]
    [DynamoDbAttribute("gsi1pk")]
    public string Gsi1Pk { get; set; } = string.Empty;
}
```
