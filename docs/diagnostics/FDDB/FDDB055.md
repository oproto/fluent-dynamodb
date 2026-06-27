# FDDB055: Conflicting index type

## Code & Severity

| Field | Value |
|-------|-------|
| Code | `FDDB055` |
| Severity | Error |

## Message

`Index '{0}' has conflicting types: {1} on entity '{2}' vs {3} on entity '{4}'`

## Description

When multiple entities define the same DynamoDB index, they must use the same index type (either all GSI or all LSI). An index cannot be both a Global Secondary Index and a Local Secondary Index.

GSIs and LSIs are fundamentally different DynamoDB constructs. A GSI has its own partition key and optional sort key, while an LSI shares the base table's partition key. The same index name cannot be both.

## Example

The following code triggers this diagnostic:

```csharp
[DynamoDbTable("shared-table", IsDefault = true)]
public partial class Order
{
    [PartitionKey]
    [DynamoDbAttribute("pk")]
    public string Pk { get; set; } = string.Empty;
    [SortKey]
    [DynamoDbAttribute("sk")]
    public string Sk { get; set; } = string.Empty;
    [GsiPartitionKey("date-index")]
    [DynamoDbAttribute("createdAt")]
    public string CreatedAt { get; set; } = string.Empty;
}
[DynamoDbTable("shared-table")]
public partial class Customer
{
    [PartitionKey]
    [DynamoDbAttribute("pk")]
    public string Pk { get; set; } = string.Empty;
    [SortKey]
    [DynamoDbAttribute("sk")]
    public string Sk { get; set; } = string.Empty;
    [LsiSortKey("date-index")]
    [DynamoDbAttribute("createdAt")]
    public string CreatedAt { get; set; } = string.Empty;
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
    [SortKey]
    [DynamoDbAttribute("sk")]
    public string Sk { get; set; } = string.Empty;
    [GsiPartitionKey("date-index")]
    [DynamoDbAttribute("createdAt")]
    public string CreatedAt { get; set; } = string.Empty;
}
[DynamoDbTable("shared-table")]
public partial class Customer
{
    [PartitionKey]
    [DynamoDbAttribute("pk")]
    public string Pk { get; set; } = string.Empty;
    [SortKey]
    [DynamoDbAttribute("sk")]
    public string Sk { get; set; } = string.Empty;
    [GsiPartitionKey("date-index")]
    [DynamoDbAttribute("createdAt")]
    public string CreatedAt { get; set; } = string.Empty;
}
```
