# DYNDB017: Conflicting related entity patterns

## Code & Severity

| Field | Value |
|-------|-------|
| Code | `DYNDB017` |
| Severity | Warning |

## Message

`Related entity patterns '{0}' and '{1}' in entity '{2}' may conflict`

## Description

Related entity patterns should be distinct to avoid mapping conflicts. When two patterns can match the same sort key value, items may be incorrectly assigned to the wrong related entity collection during composite entity assembly.

## Example

The following code triggers this diagnostic:

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

    [RelatedEntity("INVOICE#*", EntityType = typeof(InvoiceLine))]
    public List<InvoiceLine> Lines { get; set; } = new();

    [RelatedEntity("INVOICE#*", EntityType = typeof(InvoiceNote))]
    public List<InvoiceNote> Notes { get; set; } = new();
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

    [RelatedEntity("INVOICE#*#NOTE#*", EntityType = typeof(InvoiceNote))]
    public List<InvoiceNote> Notes { get; set; } = new();
}
```
