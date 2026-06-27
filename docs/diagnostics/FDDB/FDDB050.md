# FDDB050: Conflicting index Name values

## Code & Severity

| Field | Value |
|-------|-------|
| Code | `FDDB050` |
| Severity | Error |

## Message

`Index '{0}' has conflicting Name values: '{1}' and '{2}'`

## Description

When multiple entities define the same DynamoDB index, they must use the same Name property value or only one entity should specify it. All entities must agree on the C# property name for the generated index accessor.

The Name property controls the generated property name on the table class (e.g., `table.StatusIndex`). If two entities disagree on what this property should be called, the generator cannot resolve the conflict.

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

    [GsiPartitionKey("gsi1", Name = "TypeIndex")]
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
