# DISC001: Both DiscriminatorValue and DiscriminatorPattern specified

## Code & Severity

| Field | Value |
|-------|-------|
| Code | `DISC001` |
| Severity | Warning |

## Message

`Entity '{0}' has both DiscriminatorValue and DiscriminatorPattern specified. Only one should be used. DiscriminatorValue will take precedence.`

## Description

DiscriminatorValue and DiscriminatorPattern are mutually exclusive configuration options on a GSI partition key attribute. When both are specified on the same entity, the source generator cannot determine the intended discriminator strategy unambiguously.

When both are present, DiscriminatorValue takes precedence and DiscriminatorPattern is ignored. This warning alerts you to remove the redundant option to make the configuration intent clear and avoid confusion during maintenance.

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
        DiscriminatorProperty = "entityType",
        DiscriminatorValue = "ORDER",
        DiscriminatorPattern = "ORDER*")]
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
