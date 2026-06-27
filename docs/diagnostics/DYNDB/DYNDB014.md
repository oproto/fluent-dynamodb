# DYNDB014: Multi-item entity partition key format

## Code & Severity

| Field | Value |
|-------|-------|
| Code | `DYNDB014` |
| Severity | Warning |

## Message

`Partition key '{0}' in multi-item entity '{1}' should have a consistent format for proper grouping`

## Description

Multi-item entities should use consistent partition key formats to ensure related items are properly grouped. Inconsistent key formats across related entities can lead to items not being found during composite entity assembly.

## Example

The following code triggers this diagnostic:

```csharp
[DynamoDbTable("Invoices", IsDefault = true)]
public partial class Invoice
{
    [PartitionKey]
    [DynamoDbAttribute("pk")]
    public string Pk { get; set; } = string.Empty;

    [SortKey(Prefix = "INVOICE")]
    [DynamoDbAttribute("sk")]
    public string Sk { get; set; } = string.Empty;
}

[DynamoDbTable("Invoices")]
public partial class InvoiceLine
{
    [PartitionKey(Prefix = "LINE")]
    [DynamoDbAttribute("pk")]
    public string Pk { get; set; } = string.Empty;

    [SortKey]
    [DynamoDbAttribute("sk")]
    public string Sk { get; set; } = string.Empty;
}
```

## Fix

The corrected version:

```csharp
[DynamoDbTable("Invoices", IsDefault = true)]
public partial class Invoice
{
    [PartitionKey(Prefix = "CUSTOMER")]
    [DynamoDbAttribute("pk")]
    public string Pk { get; set; } = string.Empty;

    [SortKey(Prefix = "INVOICE")]
    [DynamoDbAttribute("sk")]
    public string Sk { get; set; } = string.Empty;
}

[DynamoDbTable("Invoices")]
public partial class InvoiceLine
{
    [PartitionKey(Prefix = "CUSTOMER")]
    [DynamoDbAttribute("pk")]
    public string Pk { get; set; } = string.Empty;

    [SortKey(Prefix = "LINE")]
    [DynamoDbAttribute("sk")]
    public string Sk { get; set; } = string.Empty;
}
```
