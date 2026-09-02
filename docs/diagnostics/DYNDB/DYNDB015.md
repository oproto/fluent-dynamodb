# DYNDB015: Invalid related entity type

## Code & Severity

| Field | Value |
|-------|-------|
| Code | `DYNDB015` |
| Severity | Error |

## Message

`Related entity property '{0}' references unknown type '{1}'`

## Description

Related entity types must be valid DynamoDB entity classes. The type specified in the EntityType property of [RelatedEntity] must exist in the compilation and be decorated with [DynamoDbTable].

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

    [RelatedEntity("INVOICE#*#LINE#*", EntityType = typeof(LineItem))]
    public List<LineItem> Lines { get; set; } = new();
}
// LineItem class does not exist or is not a DynamoDB entity
```

## Fix

The corrected version:

```csharp
[DynamoDbTable("Invoices")]
public partial class InvoiceLine
{
    [PartitionKey(Prefix = "CUSTOMER")]
    [DynamoDbAttribute("pk")]
    public string Pk { get; set; } = string.Empty;

    [SortKey]
    [DynamoDbAttribute("sk")]
    public string Sk { get; set; } = string.Empty;
}

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
