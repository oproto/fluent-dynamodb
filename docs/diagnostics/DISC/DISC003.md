# DISC003: Invalid discriminator pattern syntax

## Code & Severity

| Field | Value |
|-------|-------|
| Code | `DISC003` |
| Severity | Error |

## Message

`Entity '{0}' has invalid discriminator pattern '{1}': {2}. Patterns should use '*' as a wildcard (e.g., 'USER#*', '*#USER', '*USER*').`

## Description

Discriminator patterns must use valid syntax with `*` as a wildcard character. The wildcard matches zero or more characters in the discriminator attribute value. Supported patterns include prefix match (`USER#*`), suffix match (`*#USER`), and contains match (`*USER*`).

Complex patterns with multiple wildcards in non-standard positions may not be supported. The source generator validates pattern syntax during compilation and emits this error when it cannot parse the pattern into a usable matching strategy.

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
        DiscriminatorPattern = "[ORDER]")]
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
        DiscriminatorPattern = "ORDER*")]
    [DynamoDbAttribute("gsi1pk")]
    public string Gsi1Pk { get; set; } = string.Empty;
}
```
