# FDDB072: KeysOnly with UseProjection

## Code & Severity

| Field | Value |
|-------|-------|
| Code | `FDDB072` |
| Severity | Warning |

## Message

`Index '{0}' on entity '{1}' has both ProjectionType = KeysOnly and [UseProjection] attribute. The [UseProjection] attribute takes precedence and the auto-generated Keys Only projection will not be used.`

## Description

When both ProjectionType = KeysOnly and [UseProjection] are specified, the [UseProjection] attribute takes precedence. The auto-generated Keys Only projection record will not be generated. Consider removing one of these configurations to avoid confusion.

ProjectionType = KeysOnly normally auto-generates a lightweight record containing only the key attributes. When [UseProjection] overrides this with a custom projection type, the auto-generated record becomes dead code.

## Example

The following code triggers this diagnostic:

```csharp
[DynamoDbProjection(typeof(Order))]
public partial class OrderKeys
{
    [DynamoDbAttribute("pk")]
    public string Pk { get; set; } = string.Empty;

    [DynamoDbAttribute("status")]
    public string Status { get; set; } = string.Empty;
}

[DynamoDbTable("orders")]
public partial class Order
{
    [PartitionKey]
    [DynamoDbAttribute("pk")]
    public string Pk { get; set; } = string.Empty;

    [GsiPartitionKey("status-index",
        ProjectionType = ProjectionType.KeysOnly)]
    [UseProjection(typeof(OrderKeys))]
    [DynamoDbAttribute("status")]
    public string Status { get; set; } = string.Empty;
}
```

## Fix

The corrected version:

```csharp
[DynamoDbProjection(typeof(Order))]
public partial class OrderKeys
{
    [DynamoDbAttribute("pk")]
    public string Pk { get; set; } = string.Empty;

    [DynamoDbAttribute("status")]
    public string Status { get; set; } = string.Empty;
}

[DynamoDbTable("orders")]
public partial class Order
{
    [PartitionKey]
    [DynamoDbAttribute("pk")]
    public string Pk { get; set; } = string.Empty;

    [GsiPartitionKey("status-index")]
    [UseProjection(typeof(OrderKeys))]
    [DynamoDbAttribute("status")]
    public string Status { get; set; } = string.Empty;
}
```
