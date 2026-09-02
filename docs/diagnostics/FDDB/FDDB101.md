# FDDB101: Explicit discriminator pattern conflicts with key format

## Code & Severity

| Field | Value |
|-------|-------|
| Code | `FDDB101` |
| Severity | Error |

## Message

`Entity '{0}' specifies DiscriminatorPattern on attribute '{1}' as '{2}' but the key format derives pattern '{3}'`

## Description

When an entity has both an explicit DiscriminatorPattern and a key format that automatically derives a discriminator pattern, these two patterns must be consistent. The source generator derives a pattern from the key's prefix and format structure; if the explicitly specified pattern contradicts this derivation, it creates an irreconcilable conflict.

Remove the explicit DiscriminatorPattern and let the source generator derive it automatically from the key format, or adjust the explicit pattern to match what the key format would derive.

## Example

The following code triggers this diagnostic:

```csharp
[DynamoDbTable("shared-table",
    DiscriminatorPattern = "CUSTOMER#*")]
public partial class Order
{
    [PartitionKey(Prefix = "ORDER")]
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
[DynamoDbTable("shared-table")]
public partial class Order
{
    [PartitionKey(Prefix = "ORDER")]
    [DynamoDbAttribute("pk")]
    public string Pk { get; set; } = string.Empty;

    [SortKey]
    [DynamoDbAttribute("sk")]
    public string Sk { get; set; } = string.Empty;
}
// Pattern "ORDER#*" is auto-derived from Prefix = "ORDER"
```
