# DYNDB016: Related entities require sort key

## Code & Severity

| Field | Value |
|-------|-------|
| Code | `DYNDB016` |
| Severity | Warning |

## Message

`Entity '{0}' has related entity properties but no sort key for pattern matching`

## Description

Related entity mapping requires a sort key to match patterns and discriminate entity types. Without a sort key, the [RelatedEntity] patterns cannot be evaluated to determine which items belong to which related entity collection.

## Example

The following code triggers this diagnostic:

```csharp
[DynamoDbTable("Invoices")]
public partial class Invoice
{
    [PartitionKey(Prefix = "CUSTOMER")]
    [DynamoDbAttribute("pk")]
    public string Pk { get; set; } = string.Empty;

    [DynamoDbAttribute("invoiceNumber")]
    public string InvoiceNumber { get; set; } = string.Empty;

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

    [DynamoDbAttribute("invoiceNumber")]
    public string InvoiceNumber { get; set; } = string.Empty;

    [RelatedEntity("INVOICE#*#LINE#*", EntityType = typeof(InvoiceLine))]
    public List<InvoiceLine> Lines { get; set; } = new();
}
```
