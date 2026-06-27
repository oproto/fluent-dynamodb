# DISC004: Ambiguous overlapping discriminator patterns

## Code & Severity

| Field | Value |
|-------|-------|
| Code | `DISC004` |
| Severity | Error |

## Message

`Ambiguous overlapping discriminator patterns: '{0}' on {1} and '{2}' on {3} have the same specificity score on property '{4}'`

## Description

Two overlapping discriminator patterns with the same specificity score cannot be automatically resolved by the source generator. Specificity is determined by the length and position of literal characters in a pattern — patterns with more literal characters are considered more specific.

When two patterns overlap (both could match the same discriminator value) and have identical specificity scores, the generator cannot determine which entity should take precedence. You must change one pattern to be more or less specific to establish a clear hierarchy.

## Example

The following code triggers this diagnostic:

```csharp
[DynamoDbTable("shared-table", IsDefault = true)]
public partial class OrderA
{
    [PartitionKey]
    [DynamoDbAttribute("pk")]
    public string Pk { get; set; } = string.Empty;

    [GsiPartitionKey("gsi1",
        DiscriminatorProperty = "entityType",
        DiscriminatorPattern = "ORD*A")]
    [DynamoDbAttribute("gsi1pk")]
    public string Gsi1Pk { get; set; } = string.Empty;
}

[DynamoDbTable("shared-table")]
public partial class OrderB
{
    [PartitionKey]
    [DynamoDbAttribute("pk")]
    public string Pk { get; set; } = string.Empty;

    [GsiPartitionKey("gsi1",
        DiscriminatorProperty = "entityType",
        DiscriminatorPattern = "ORD*B")]
    [DynamoDbAttribute("gsi1pk")]
    public string Gsi1Pk { get; set; } = string.Empty;
}
```

## Fix

The corrected version:

```csharp
[DynamoDbTable("shared-table", IsDefault = true)]
public partial class OrderA
{
    [PartitionKey]
    [DynamoDbAttribute("pk")]
    public string Pk { get; set; } = string.Empty;

    [GsiPartitionKey("gsi1",
        DiscriminatorProperty = "entityType",
        DiscriminatorValue = "ORDER_A")]
    [DynamoDbAttribute("gsi1pk")]
    public string Gsi1Pk { get; set; } = string.Empty;
}

[DynamoDbTable("shared-table")]
public partial class OrderB
{
    [PartitionKey]
    [DynamoDbAttribute("pk")]
    public string Pk { get; set; } = string.Empty;

    [GsiPartitionKey("gsi1",
        DiscriminatorProperty = "entityType",
        DiscriminatorValue = "ORDER_B")]
    [DynamoDbAttribute("gsi1pk")]
    public string Gsi1Pk { get; set; } = string.Empty;
}
```
