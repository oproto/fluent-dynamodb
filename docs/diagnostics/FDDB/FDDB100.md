# FDDB100: Key prefix conflicts with explicit computed format

## Code & Severity

| Field | Value |
|-------|-------|
| Code | `FDDB100` |
| Severity | Error |

## Message

`Property '{0}' has Prefix='{1}' (expecting format to start with '{2}') but ComputedAttribute.Format='{3}' does not match`

## Description

When a key property specifies both a Prefix (via [PartitionKey] or [SortKey]) and an explicit Format (via [Computed]), the format string must start with the expected prefix and separator. A mismatch between these two configurations creates a conflict in how the key value is constructed.

The source generator uses the Prefix to derive discriminator patterns and key construction helpers. If the explicit format contradicts the prefix, the generated code would produce inconsistent keys.

## Example

The following code triggers this diagnostic:

```csharp
[DynamoDbTable("orders")]
public partial class Order
{
    [PartitionKey(Prefix = "ORDER")]
    [DynamoDbAttribute("pk")]
    [Computed("CustomerId", "OrderId",
        Format = "CUST#{0}#{1}")]  // Doesn't start with "ORDER#"
    public string Pk { get; set; } = string.Empty;

    [Extracted("Pk", 0)]
    public string CustomerId { get; set; } = string.Empty;

    [Extracted("Pk", 1)]
    public string OrderId { get; set; } = string.Empty;
}
```

## Fix

The corrected version:

```csharp
[DynamoDbTable("orders")]
public partial class Order
{
    [PartitionKey(Prefix = "ORDER")]
    [DynamoDbAttribute("pk")]
    [Computed("CustomerId", "OrderId",
        Format = "ORDER#{0}#{1}")]  // Starts with "ORDER#"
    public string Pk { get; set; } = string.Empty;

    [Extracted("Pk", 0)]
    public string CustomerId { get; set; } = string.Empty;

    [Extracted("Pk", 1)]
    public string OrderId { get; set; } = string.Empty;
}
```
