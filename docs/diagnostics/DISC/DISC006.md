# DISC006: Tautological exclusion guard detected

## Code & Severity

| Field | Value |
|-------|-------|
| Code | `DISC006` |
| Severity | Error |

## Message

`Entity '{0}' (pattern '{1}') cannot exclude pattern '{2}' from entity '{3}' because the exclusion check ({4}("{5}")) is identical to the entity's own positive match. This would make MatchesEntity always return false.`

## Description

A computed exclusion guard is tautological — it uses the same strategy and literal as the entity's own positive match criterion. If applied, the exclusion would reject every item that the entity's own pattern accepts, causing `MatchesEntity` to always return false.

This indicates the pattern hierarchy cannot be automatically resolved because the two patterns are effectively identical in their matching behavior. Consider redesigning the discriminator patterns to use distinct literals so the generator can produce meaningful exclusion guards.

## Example

The following code triggers this diagnostic:

```csharp
[DynamoDbTable("shared-table", IsDefault = true)]
public partial class GenericItem
{
    [PartitionKey]
    [DynamoDbAttribute("pk")]
    public string Pk { get; set; } = string.Empty;

    [GsiPartitionKey("gsi1",
        DiscriminatorProperty = "entityType",
        DiscriminatorPattern = "*ITEM*")]
    [DynamoDbAttribute("gsi1pk")]
    public string Gsi1Pk { get; set; } = string.Empty;
}

[DynamoDbTable("shared-table")]
public partial class SpecificItem
{
    [PartitionKey]
    [DynamoDbAttribute("pk")]
    public string Pk { get; set; } = string.Empty;

    [GsiPartitionKey("gsi1",
        DiscriminatorProperty = "entityType",
        DiscriminatorPattern = "*ITEM*")]
    [DynamoDbAttribute("gsi1pk")]
    public string Gsi1Pk { get; set; } = string.Empty;
}
```

## Fix

The corrected version:

```csharp
[DynamoDbTable("shared-table", IsDefault = true)]
public partial class GenericItem
{
    [PartitionKey]
    [DynamoDbAttribute("pk")]
    public string Pk { get; set; } = string.Empty;

    [GsiPartitionKey("gsi1",
        DiscriminatorProperty = "entityType",
        DiscriminatorPattern = "*ITEM*")]
    [DynamoDbAttribute("gsi1pk")]
    public string Gsi1Pk { get; set; } = string.Empty;
}

[DynamoDbTable("shared-table")]
public partial class SpecificItem
{
    [PartitionKey]
    [DynamoDbAttribute("pk")]
    public string Pk { get; set; } = string.Empty;

    [GsiPartitionKey("gsi1",
        DiscriminatorProperty = "entityType",
        DiscriminatorPattern = "SPECIAL#ITEM*")]
    [DynamoDbAttribute("gsi1pk")]
    public string Gsi1Pk { get; set; } = string.Empty;
}
```
