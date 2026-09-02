# DYNDB122: Duplicate GSI sort keys

## Code & Severity

| Field | Value |
|-------|-------|
| Code | `DYNDB122` |
| Severity | Error |

## Message

`GSI '{0}' on entity '{1}' has multiple sort keys: properties '{2}' and '{3}'. Only one is allowed.`

## Description

A Global Secondary Index can only have one sort key property. Remove the duplicate [GsiSortKey] attribute from one of the properties. DynamoDB indexes support at most one sort key attribute.

## Example

The following code triggers this diagnostic:

```csharp
[DynamoDbTable("Orders")]
public partial class Order
{
    [PartitionKey]
    [DynamoDbAttribute("pk")]
    public string Pk { get; set; } = string.Empty;

    [GsiPartitionKey("status-index")]
    [DynamoDbAttribute("status")]
    public string Status { get; set; } = string.Empty;

    [GsiSortKey("status-index")]
    [DynamoDbAttribute("createdAt")]
    public DateTime CreatedAt { get; set; }

    [GsiSortKey("status-index")]
    [DynamoDbAttribute("updatedAt")]
    public DateTime UpdatedAt { get; set; }
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
    [DynamoDbAttribute("status")]
    public string Status { get; set; } = string.Empty;

    [GsiSortKey("status-index")]
    [DynamoDbAttribute("createdAt")]
    public DateTime CreatedAt { get; set; }

    [DynamoDbAttribute("updatedAt")]
    public DateTime UpdatedAt { get; set; }
}
```
