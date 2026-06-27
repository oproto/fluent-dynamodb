# DYNDB008: Ambiguous related entity pattern

## Code & Severity

| Field | Value |
|-------|-------|
| Code | `DYNDB008` |
| Severity | Warning |

## Message

`Related entity pattern '{0}' on property '{1}' might match multiple entity types`

## Description

Related entity patterns should be specific enough to avoid ambiguous matches. When patterns are too broad, the composite entity assembly logic may incorrectly assign items to the wrong related entity collection.

## Example

The following code triggers this diagnostic:

```csharp
[DynamoDbTable("Invoices")]
public partial class Invoice
{
    [PartitionKey]
    [DynamoDbAttribute("pk")]
    public string Pk { get; set; } = string.Empty;

    [SortKey]
    [DynamoDbAttribute("sk")]
    public string Sk { get; set; } = string.Empty;

    [RelatedEntity("*", EntityType = typeof(InvoiceLine))]
    public List<InvoiceLine> Lines { get; set; } = new();
}
```

## Fix

The corrected version:

```csharp
[DynamoDbTable("Invoices")]
public partial class Invoice
{
    [PartitionKey]
    [DynamoDbAttribute("pk")]
    public string Pk { get; set; } = string.Empty;

    [SortKey]
    [DynamoDbAttribute("sk")]
    public string Sk { get; set; } = string.Empty;

    [RelatedEntity("INVOICE#*#LINE#*", EntityType = typeof(InvoiceLine))]
    public List<InvoiceLine> Lines { get; set; } = new();
}
```
