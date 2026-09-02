# DYNDB012: Multi-item entity missing sort key

## Code & Severity

| Field | Value |
|-------|-------|
| Code | `DYNDB012` |
| Severity | Warning |

## Message

`Multi-item entity '{0}' should have a sort key for proper item ordering`

## Description

Multi-item entities should have a sort key to ensure consistent ordering of related items. Without a sort key, the order of items within a partition is undefined and related entity pattern matching may not work correctly.

## Example

The following code triggers this diagnostic:

```csharp
[DynamoDbTable("Invoices")]
public partial class Invoice
{
    [PartitionKey(Prefix = "CUSTOMER")]
    [DynamoDbAttribute("pk")]
    public string Pk { get; set; } = string.Empty;

    [RelatedEntity("LINE#*", EntityType = typeof(InvoiceLine))]
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
