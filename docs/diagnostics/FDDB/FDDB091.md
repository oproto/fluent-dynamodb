# FDDB091: Ambiguous named placeholder usage

## Code & Severity

| Field | Value |
|-------|-------|
| Code | `FDDB091` |
| Severity | Error |

## Message

`[Computed] on property '{0}' has multiple positional arguments containing '{{'. Use a single format string with {{PropertyName}} placeholders, or use property names without braces as separate positional arguments with a Format parameter.`

## Description

Named placeholders in format strings cannot be combined with multiple positional source property arguments. The source generator uses the presence of `{` in a positional argument to detect named-placeholder format strings, but this detection only works when there is exactly one positional argument. When multiple positional arguments contain `{`, the intent is ambiguous — the generator cannot determine whether the arguments are meant to be separate named-placeholder format strings or a list of source property names.

This diagnostic also fires when a `[Computed]` attribute has a `Format` named parameter containing named placeholders (e.g., `{PropertyName}`) alongside explicit source property names in the positional arguments. Named placeholders in `Format` infer source properties from the format string, so combining them with explicit source properties creates a conflict.

## Example

The following code triggers this diagnostic:

```csharp
[DynamoDbTable("invoices")]
public partial class InvoiceLine
{
    [PartitionKey(Prefix = "CUSTOMER")]
    [DynamoDbAttribute("pk")]
    public string Pk { get; set; } = string.Empty;

    [SortKey]
    [DynamoDbAttribute("sk")]
    [Computed("{InvoiceNumber}", "{LineNumber}")]  // ❌ Multiple args with '{'
    public string Sk { get; set; } = string.Empty;

    [DynamoDbAttribute("invoiceNumber")]
    public string InvoiceNumber { get; set; } = string.Empty;

    [DynamoDbAttribute("lineNumber")]
    public int LineNumber { get; set; }
}
```

Or combining named `Format` with explicit source properties:

```csharp
[SortKey]
[DynamoDbAttribute("sk")]
[Computed("InvoiceNumber", Format = "INVOICE#{InvoiceNumber}#LINE#{LineNumber}")]  // ❌ Named Format + explicit source properties
public string Sk { get; set; } = string.Empty;
```

## Fix

Use a single format string with `{PropertyName}` placeholders as the sole positional argument:

```csharp
[SortKey]
[DynamoDbAttribute("sk")]
[Computed("INVOICE#{InvoiceNumber}#LINE#{LineNumber}")]  // ✅ Single arg with named placeholders
public string Sk { get; set; } = string.Empty;
```

Or use property names without braces as separate positional arguments with a `Format` parameter using positional `{N}` placeholders:

```csharp
[SortKey]
[DynamoDbAttribute("sk")]
[Computed("InvoiceNumber", "LineNumber", Format = "INVOICE#{0}#LINE#{1}")]  // ✅ Explicit source properties + positional Format
public string Sk { get; set; } = string.Empty;
```
