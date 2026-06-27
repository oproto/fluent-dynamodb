# DISC005: Overlapping discriminator pattern resolved

## Code & Severity

| Field | Value |
|-------|-------|
| Code | `DISC005` |
| Severity | Info |

## Message

`Overlapping discriminator pattern resolved: {0} excludes pattern '{1}' from more-specific entity {2}`

## Description

Overlapping discriminator patterns were resolved by specificity ordering. When two entities share a GSI with overlapping discriminator patterns, the source generator compares their specificity scores and automatically determines which is more specific.

The less-specific entity's generated `MatchesEntity` method will include an exclusion guard that rejects items matching the more-specific pattern. This ensures each item is claimed by exactly one entity type. This informational diagnostic confirms the resolution was successful and shows which exclusion was applied.

## Example

The following code triggers this diagnostic:

```csharp
[DynamoDbTable("shared-table", IsDefault = true)]
public partial class AnyOrder
{
    [PartitionKey]
    [DynamoDbAttribute("pk")]
    public string Pk { get; set; } = string.Empty;
    [GsiPartitionKey("gsi1",
        DiscriminatorProperty = "entityType",
        DiscriminatorPattern = "ORDER*")]
    [DynamoDbAttribute("gsi1pk")]
    public string Gsi1Pk { get; set; } = string.Empty;
}
[DynamoDbTable("shared-table")]
public partial class PriorityOrder
{
    [PartitionKey]
    [DynamoDbAttribute("pk")]
    public string Pk { get; set; } = string.Empty;
    [GsiPartitionKey("gsi1",
        DiscriminatorProperty = "entityType",
        DiscriminatorPattern = "ORDER#PRIORITY*")]
    [DynamoDbAttribute("gsi1pk")]
    public string Gsi1Pk { get; set; } = string.Empty;
}
```

## Fix

The corrected version:

```csharp
// No fix needed — this is an informational diagnostic confirming
// successful resolution. AnyOrder's MatchesEntity will automatically
// exclude items matching "ORDER#PRIORITY*".
[DynamoDbTable("shared-table", IsDefault = true)]
public partial class AnyOrder
{
    [PartitionKey]
    [DynamoDbAttribute("pk")]
    public string Pk { get; set; } = string.Empty;
    [GsiPartitionKey("gsi1",
        DiscriminatorProperty = "entityType",
        DiscriminatorPattern = "ORDER*")]
    [DynamoDbAttribute("gsi1pk")]
    public string Gsi1Pk { get; set; } = string.Empty;
}
[DynamoDbTable("shared-table")]
public partial class PriorityOrder
{
    [PartitionKey]
    [DynamoDbAttribute("pk")]
    public string Pk { get; set; } = string.Empty;
    [GsiPartitionKey("gsi1",
        DiscriminatorProperty = "entityType",
        DiscriminatorPattern = "ORDER#PRIORITY*")]
    [DynamoDbAttribute("gsi1pk")]
    public string Gsi1Pk { get; set; } = string.Empty;
}
```
