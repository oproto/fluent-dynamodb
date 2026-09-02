# DISC002: DiscriminatorValue or DiscriminatorPattern without DiscriminatorProperty

## Code & Severity

| Field | Value |
|-------|-------|
| Code | `DISC002` |
| Severity | Error |

## Message

`Entity '{0}' has DiscriminatorValue or DiscriminatorPattern specified but DiscriminatorProperty is missing. Specify DiscriminatorProperty to indicate which attribute contains the discriminator.`

## Description

DiscriminatorProperty must be specified when using DiscriminatorValue or DiscriminatorPattern. It tells the source generator which DynamoDB attribute contains the discriminator value used to distinguish entity types in a shared GSI.

Without DiscriminatorProperty, the generator cannot produce the correct `MatchesEntity` filter logic because it does not know which attribute to inspect when determining whether a given item belongs to this entity type.

## Example

The following code triggers this diagnostic:

```csharp
[DynamoDbTable("shared-table", IsDefault = true)]
public partial class Order
{
    [PartitionKey]
    [DynamoDbAttribute("pk")]
    public string Pk { get; set; } = string.Empty;

    [GsiPartitionKey("gsi1",
        DiscriminatorValue = "ORDER")]
    [DynamoDbAttribute("gsi1pk")]
    public string Gsi1Pk { get; set; } = string.Empty;
}
```

## Fix

The corrected version:

```csharp
[DynamoDbTable("shared-table", IsDefault = true)]
public partial class Order
{
    [PartitionKey]
    [DynamoDbAttribute("pk")]
    public string Pk { get; set; } = string.Empty;

    [GsiPartitionKey("gsi1",
        DiscriminatorProperty = "entityType",
        DiscriminatorValue = "ORDER")]
    [DynamoDbAttribute("gsi1pk")]
    public string Gsi1Pk { get; set; } = string.Empty;
}
```
