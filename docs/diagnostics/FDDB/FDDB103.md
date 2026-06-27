# FDDB103: Redundant explicit discriminator pattern

## Code & Severity

| Field | Value |
|-------|-------|
| Code | `FDDB103` |
| Severity | Info |

## Message

`Entity '{0}' specifies DiscriminatorPattern='{1}' which is automatically derivable from the key format — the explicit specification can be removed`

## Description

The entity specifies an explicit DiscriminatorPattern that exactly matches what the source generator would automatically derive from the key format. The explicit specification is unnecessary and can be removed to reduce configuration noise.

Removing redundant explicit patterns makes the entity definition cleaner and avoids the risk of the explicit pattern drifting out of sync if the key format is changed later.

## Example

The following code triggers this diagnostic:

```csharp
[DynamoDbTable("shared-table",
    DiscriminatorPattern = "ORDER#*")]
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
// "ORDER#*" pattern is automatically derived from Prefix = "ORDER"
```
