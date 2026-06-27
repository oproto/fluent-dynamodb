# DYNDB004: Invalid key format

## Code & Severity

| Field | Value |
|-------|-------|
| Code | `DYNDB004` |
| Severity | Error |

## Message

`Property '{0}' has invalid key format: {1}`

## Description

Key format must be a valid pattern for DynamoDB key construction. The source generator validates that key formats follow expected syntax and will produce valid DynamoDB keys at runtime.

## Example

The following code triggers this diagnostic:

```csharp
[DynamoDbTable("Orders")]
public partial class Order
{
    [PartitionKey(Prefix = "ORDER", Separator = "")]
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
[DynamoDbTable("Orders")]
public partial class Order
{
    [PartitionKey(Prefix = "ORDER", Separator = "#")]
    [DynamoDbAttribute("pk")]
    public string Pk { get; set; } = string.Empty;

    [SortKey]
    [DynamoDbAttribute("sk")]
    public string Sk { get; set; } = string.Empty;
}
```
