# FDDB102: Overlapping auto-derived discriminator patterns

## Code & Severity

| Field | Value |
|-------|-------|
| Code | `FDDB102` |
| Severity | Warning |

## Message

`Entities '{0}' and '{1}' have overlapping auto-derived patterns '{2}' and '{3}' on attribute '{4}' — consider adding more specificity to key formats`

## Description

Two entities sharing the same table have auto-derived discriminator patterns that overlap. While the source generator will still produce correct exclusion guards to resolve the ambiguity at runtime, overlapping patterns reduce clarity and may indicate a design issue.

Consider making the key prefixes more distinct to avoid the overlap entirely. This is advisory — the generated code will still work correctly.

## Example

The following code triggers this diagnostic:

```csharp
[DynamoDbTable("shared-table", IsDefault = true)]
public partial class Order
{
    [PartitionKey(Prefix = "ORD")]
    [DynamoDbAttribute("pk")]
    public string Pk { get; set; } = string.Empty;

    [SortKey]
    [DynamoDbAttribute("sk")]
    public string Sk { get; set; } = string.Empty;
}

[DynamoDbTable("shared-table")]
public partial class OrderReturn
{
    [PartitionKey(Prefix = "ORD")]
    [DynamoDbAttribute("pk")]
    public string Pk { get; set; } = string.Empty;

    [SortKey(Prefix = "RETURN")]
    [DynamoDbAttribute("sk")]
    public string Sk { get; set; } = string.Empty;
}
```

## Fix

The corrected version:

```csharp
[DynamoDbTable("shared-table", IsDefault = true)]
public partial class Order
{
    [PartitionKey(Prefix = "ORDER")]
    [DynamoDbAttribute("pk")]
    public string Pk { get; set; } = string.Empty;

    [SortKey(Prefix = "META")]
    [DynamoDbAttribute("sk")]
    public string Sk { get; set; } = string.Empty;
}

[DynamoDbTable("shared-table")]
public partial class OrderReturn
{
    [PartitionKey(Prefix = "ORDER")]
    [DynamoDbAttribute("pk")]
    public string Pk { get; set; } = string.Empty;

    [SortKey(Prefix = "RETURN")]
    [DynamoDbAttribute("sk")]
    public string Sk { get; set; } = string.Empty;
}
```
