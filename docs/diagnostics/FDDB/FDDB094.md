# FDDB094: Malformed placeholder

## Code & Severity

| Field | Value |
|-------|-------|
| Code | `FDDB094` |
| Severity | Error |

## Message

`Format string on property '{0}' has an unclosed brace at character offset {1}: '{2}'`

## Description

Every opening brace `{` in a format string must have a matching closing brace `}`. An unclosed brace indicates a malformed placeholder that the source generator cannot parse. This typically occurs when a closing brace is accidentally omitted while writing a named or positional placeholder.

## Example

The following code triggers this diagnostic:

```csharp
[DynamoDbTable("invoices")]
public partial class Invoice
{
    [PartitionKey(Prefix = "CUSTOMER")]
    [DynamoDbAttribute("pk")]
    public string Pk { get; set; } = string.Empty;

    [SortKey]
    [DynamoDbAttribute("sk")]
    [Computed("INVOICE#{InvoiceNumber")]  // ❌ Missing closing brace
    public string Sk { get; set; } = string.Empty;

    [DynamoDbAttribute("invoiceNumber")]
    public string InvoiceNumber { get; set; } = string.Empty;
}
```

**Diagnostic output:**
```
Error FDDB094: Format string on property 'Sk' has an unclosed brace at character offset 8: 'INVOICE#{InvoiceNumber'
```

## Fix

Add the missing closing brace:

```csharp
[SortKey]
[DynamoDbAttribute("sk")]
[Computed("INVOICE#{InvoiceNumber}")]  // ✅ Properly closed placeholder
public string Sk { get; set; } = string.Empty;
```
