# DYNDB011: Multi-item entity missing partition key

## Code & Severity

| Field | Value |
|-------|-------|
| Code | `DYNDB011` |
| Severity | Error |

## Message

`Multi-item entity '{0}' must have a partition key for grouping related items`

## Description

Multi-item entities require a partition key to group related DynamoDB items together. Without a partition key, the source generator cannot determine how to associate parent and child items in composite entity queries.

## Example

The following code triggers this diagnostic:

```csharp
[DynamoDbTable("Invoices")]
public partial class Invoice
{
    [SortKey(Prefix = "INVOICE")]
    [DynamoDbAttribute("sk")]
    public string Sk { get; set; } = string.Empty;

    [RelatedEntity("INVOICE#*#LINE#*", EntityType = typeof(InvoiceLine))]
    public List<InvoiceLine> Lines { get; set; } = new();
}
```

## Fix

The corrected version:

```csharp
[DynamoDbTable("Invoices")]
public partial class Invoice
{
    [PartitionKey(Prefix = "CUSTOMER")]
    [DynamoDbAttribute("pk")]
    public string Pk { get; set; } = string.Empty;

    [SortKey(Prefix = "INVOICE")]
    [DynamoDbAttribute("sk")]
    public string Sk { get; set; } = string.Empty;

    [RelatedEntity("INVOICE#*#LINE#*", EntityType = typeof(InvoiceLine))]
    public List<InvoiceLine> Lines { get; set; } = new();
}
```
