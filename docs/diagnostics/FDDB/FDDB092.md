# FDDB092: Unresolved named placeholder

## Code & Severity

| Field | Value |
|-------|-------|
| Code | `FDDB092` |
| Severity | Error |

## Message

`Named placeholder '{{{1}}}' in [Computed] on property '{0}' does not match any property on entity '{2}'. Available properties: {3}`

## Description

Each `{PropertyName}` token in a named-placeholder format string must match a declared property on the entity (case-sensitive). The source generator resolves named placeholders against properties that carry a `[DynamoDbAttribute]` on the same entity class. If a placeholder references a property name that does not exist, the source generator cannot infer the source property and cannot generate the computed key.

Common causes include typos in the placeholder name, referencing a property that has not been annotated with `[DynamoDbAttribute]`, or case mismatches (property resolution is case-sensitive).

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
    [Computed("INVOICE#{InvoiceNubmer}")]  // ❌ Typo: "InvoiceNubmer" instead of "InvoiceNumber"
    public string Sk { get; set; } = string.Empty;

    [DynamoDbAttribute("invoiceNumber")]
    public string InvoiceNumber { get; set; } = string.Empty;
}
```

**Diagnostic output:**
```
Error FDDB092: Named placeholder '{InvoiceNubmer}' in [Computed] on property 'Sk' does not match any property on entity 'Invoice'. Available properties: Pk, Sk, InvoiceNumber
```

## Fix

Correct the property name in the placeholder to match a declared property on the entity:

```csharp
[SortKey]
[DynamoDbAttribute("sk")]
[Computed("INVOICE#{InvoiceNumber}")]  // ✅ Correct property name
public string Sk { get; set; } = string.Empty;
```
