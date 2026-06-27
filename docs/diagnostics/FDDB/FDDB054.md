# FDDB054: Conflicting index sort key attribute

## Code & Severity

| Field | Value |
|-------|-------|
| Code | `FDDB054` |
| Severity | Error |

## Message

`Index '{0}' has conflicting sort key attributes: '{1}' on entity '{2}' vs '{3}' on entity '{4}'`

## Description

When multiple entities define the same DynamoDB index, they must use the same DynamoDB attribute for the sort key (or both have no sort key). Different C# property names are allowed as long as they map to the same DynamoDB attribute name.

A DynamoDB GSI has a fixed schema—the sort key attribute is defined at the index level, not per-item. All entities writing to the same index must agree on which DynamoDB attribute serves as the sort key.

## Example

The following code triggers this diagnostic:

```csharp
[DynamoDbTable("shared-table", IsDefault = true)]
public partial class Order
{
    [PartitionKey]
    [DynamoDbAttribute("pk")]
    public string Pk { get; set; } = string.Empty;
    [GsiPartitionKey("gsi1")]
    [DynamoDbAttribute("gsi1pk")]
    public string Gsi1Pk { get; set; } = string.Empty;
    [GsiSortKey("gsi1")]
    [DynamoDbAttribute("createdAt")]
    public DateTime CreatedAt { get; set; }
}
[DynamoDbTable("shared-table")]
public partial class Customer
{
    [PartitionKey]
    [DynamoDbAttribute("pk")]
    public string Pk { get; set; } = string.Empty;
    [GsiPartitionKey("gsi1")]
    [DynamoDbAttribute("gsi1pk")]
    public string Gsi1Pk { get; set; } = string.Empty;
    [GsiSortKey("gsi1")]
    [DynamoDbAttribute("updatedAt")]
    public DateTime UpdatedAt { get; set; }
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
    [GsiPartitionKey("gsi1")]
    [DynamoDbAttribute("gsi1pk")]
    public string Gsi1Pk { get; set; } = string.Empty;
    [GsiSortKey("gsi1")]
    [DynamoDbAttribute("gsi1sk")]
    public DateTime CreatedAt { get; set; }
}
[DynamoDbTable("shared-table")]
public partial class Customer
{
    [PartitionKey]
    [DynamoDbAttribute("pk")]
    public string Pk { get; set; } = string.Empty;
    [GsiPartitionKey("gsi1")]
    [DynamoDbAttribute("gsi1pk")]
    public string Gsi1Pk { get; set; } = string.Empty;
    [GsiSortKey("gsi1")]
    [DynamoDbAttribute("gsi1sk")]
    public DateTime UpdatedAt { get; set; }
}
```
