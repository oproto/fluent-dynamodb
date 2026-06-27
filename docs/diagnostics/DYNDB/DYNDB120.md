# DYNDB120: GSI sort key without partition key

## Code & Severity

| Field | Value |
|-------|-------|
| Code | `DYNDB120` |
| Severity | Error |

## Message

`GSI '{0}' on entity '{1}' has a sort key but no partition key. Add [GsiPartitionKey("{0}")] to a property.`

## Description

Every Global Secondary Index that has a sort key must also have a partition key. Add a [GsiPartitionKey] attribute with the same index name to a property. A GSI sort key alone is meaningless without a corresponding partition key to define the index's key schema.

## Example

The following code triggers this diagnostic:

```csharp
[DynamoDbTable("Orders")]
public partial class Order
{
    [PartitionKey]
    [DynamoDbAttribute("pk")]
    public string Pk { get; set; } = string.Empty;

    [GsiSortKey("status-date-index")]
    [DynamoDbAttribute("createdAt")]
    public DateTime CreatedAt { get; set; }
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

    [GsiPartitionKey("status-date-index")]
    [DynamoDbAttribute("status")]
    public string Status { get; set; } = string.Empty;

    [GsiSortKey("status-date-index")]
    [DynamoDbAttribute("createdAt")]
    public DateTime CreatedAt { get; set; }
}
```
