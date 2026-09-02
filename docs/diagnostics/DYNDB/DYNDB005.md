# DYNDB005: Conflicting entity types

## Code & Severity

| Field | Value |
|-------|-------|
| Code | `DYNDB005` |
| Severity | Error |

## Message

`Multiple entities in table '{0}' have conflicting sort key patterns`

## Description

Entities sharing the same table must have distinct sort key patterns for proper discrimination. When multiple entity types share a single DynamoDB table, the source generator uses sort key patterns to distinguish between them. Conflicting patterns make it impossible to determine which entity type a given item belongs to.

## Example

The following code triggers this diagnostic:

```csharp
[DynamoDbTable("SharedTable", IsDefault = true)]
public partial class Order
{
    [PartitionKey]
    [DynamoDbAttribute("pk")]
    public string Pk { get; set; } = string.Empty;

    [SortKey(Prefix = "META")]
    [DynamoDbAttribute("sk")]
    public string Sk { get; set; } = string.Empty;
}

[DynamoDbTable("SharedTable")]
public partial class Invoice
{
    [PartitionKey]
    [DynamoDbAttribute("pk")]
    public string Pk { get; set; } = string.Empty;

    [SortKey(Prefix = "META")]
    [DynamoDbAttribute("sk")]
    public string Sk { get; set; } = string.Empty;
}
```

## Fix

The corrected version:

```csharp
[DynamoDbTable("SharedTable", IsDefault = true)]
public partial class Order
{
    [PartitionKey]
    [DynamoDbAttribute("pk")]
    public string Pk { get; set; } = string.Empty;

    [SortKey(Prefix = "ORDER")]
    [DynamoDbAttribute("sk")]
    public string Sk { get; set; } = string.Empty;
}

[DynamoDbTable("SharedTable")]
public partial class Invoice
{
    [PartitionKey]
    [DynamoDbAttribute("pk")]
    public string Pk { get; set; } = string.Empty;

    [SortKey(Prefix = "INVOICE")]
    [DynamoDbAttribute("sk")]
    public string Sk { get; set; } = string.Empty;
}
```
