# FDDB095: Empty placeholder

## Code & Severity

| Field | Value |
|-------|-------|
| Code | `FDDB095` |
| Severity | Error |

## Message

`Format string on property '{0}' has an empty placeholder '{{}}' at character offset {1}`

## Description

Placeholders must contain either a property name (`{PropertyName}`) or a positional index (`{N}`). Empty placeholders (`{}`) are not allowed because the source generator cannot determine which property or index the placeholder refers to. This typically occurs when braces are placed in a format string without specifying the intended content.

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
    [Computed("INVOICE#{}#LINE")]  // ❌ Empty placeholder
    public string Sk { get; set; } = string.Empty;

    [DynamoDbAttribute("invoiceNumber")]
    public string InvoiceNumber { get; set; } = string.Empty;
}
```

**Diagnostic output:**
```
Error FDDB095: Format string on property 'Sk' has an empty placeholder '{}' at character offset 8
```

## Fix

Add a property name or positional index inside the braces:

```csharp
[SortKey]
[DynamoDbAttribute("sk")]
[Computed("INVOICE#{InvoiceNumber}#LINE")]  // ✅ Named placeholder with property name
public string Sk { get; set; } = string.Empty;
```
