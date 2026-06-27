# FDDB052: Redundant index Name specification

## Code & Severity

| Field | Value |
|-------|-------|
| Code | `FDDB052` |
| Severity | Warning |

## Message

`Index '{0}' has Name '{1}' specified on multiple entities`

## Description

When multiple entities define the same DynamoDB index, consider specifying the Name property on only one entity to avoid redundancy. The Name will be used for all entities sharing the index.

While specifying the same Name on multiple entities is not an error, it creates maintenance overhead. If you later want to rename the generated property, you'd need to update it in multiple places.

## Example

The following code triggers this diagnostic:

```csharp
[DynamoDbTable("shared-table", IsDefault = true)]
public partial class Order
{
    [PartitionKey]
    [DynamoDbAttribute("pk")]
    public string Pk { get; set; } = string.Empty;

    [GsiPartitionKey("gsi1", Name = "StatusIndex")]
    [DynamoDbAttribute("gsi1pk")]
    public string Status { get; set; } = string.Empty;
}

[DynamoDbTable("shared-table")]
public partial class Customer
{
    [PartitionKey]
    [DynamoDbAttribute("pk")]
    public string Pk { get; set; } = string.Empty;

    [GsiPartitionKey("gsi1", Name = "StatusIndex")]
    [DynamoDbAttribute("gsi1pk")]
    public string Type { get; set; } = string.Empty;
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

    [GsiPartitionKey("gsi1", Name = "StatusIndex")]
    [DynamoDbAttribute("gsi1pk")]
    public string Status { get; set; } = string.Empty;
}

[DynamoDbTable("shared-table")]
public partial class Customer
{
    [PartitionKey]
    [DynamoDbAttribute("pk")]
    public string Pk { get; set; } = string.Empty;

    [GsiPartitionKey("gsi1")]
    [DynamoDbAttribute("gsi1pk")]
    public string Type { get; set; } = string.Empty;
}
```
