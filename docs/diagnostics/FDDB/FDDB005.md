# FDDB005: Inconsistent discriminator properties

## Code & Severity

| Field | Value |
|-------|-------|
| Code | `FDDB005` |
| Severity | Warning |

## Message

`Table '{0}' has entities with stream conversion enabled that use different discriminator properties ({1}), but all entities should use the same discriminator property for consistent stream processing`

## Description

When multiple entities in the same table have stream conversion enabled, they should all use the same discriminator property to ensure consistent stream processing behavior. The OnStream method will use the discriminator property from the first entity.

Using different discriminator properties across entities in the same table can lead to inconsistent behavior when processing DynamoDB streams, since the stream event handler needs a single property to determine entity type.

## Example

The following code triggers this diagnostic:

```csharp
[DynamoDbTable("shared-table", IsDefault = true)]
[GenerateStreamConversion]
[GsiPartitionKey("gsi1", DiscriminatorProperty = "entityType")]
public partial class Order
{
    [PartitionKey]
    [DynamoDbAttribute("pk")]
    public string Pk { get; set; } = string.Empty;

    [DynamoDbAttribute("entityType")]
    public string EntityType { get; set; } = "ORDER";
}

[DynamoDbTable("shared-table")]
[GenerateStreamConversion]
[GsiPartitionKey("gsi1", DiscriminatorProperty = "type")]
public partial class Customer
{
    [PartitionKey]
    [DynamoDbAttribute("pk")]
    public string Pk { get; set; } = string.Empty;

    [DynamoDbAttribute("type")]
    public string Type { get; set; } = "CUSTOMER";
}
```

## Fix

The corrected version:

```csharp
[DynamoDbTable("shared-table", IsDefault = true)]
[GenerateStreamConversion]
[GsiPartitionKey("gsi1", DiscriminatorProperty = "entityType")]
public partial class Order
{
    [PartitionKey]
    [DynamoDbAttribute("pk")]
    public string Pk { get; set; } = string.Empty;

    [DynamoDbAttribute("entityType")]
    public string EntityType { get; set; } = "ORDER";
}

[DynamoDbTable("shared-table")]
[GenerateStreamConversion]
[GsiPartitionKey("gsi1", DiscriminatorProperty = "entityType")]
public partial class Customer
{
    [PartitionKey]
    [DynamoDbAttribute("pk")]
    public string Pk { get; set; } = string.Empty;

    [DynamoDbAttribute("entityType")]
    public string EntityType { get; set; } = "CUSTOMER";
}
```
