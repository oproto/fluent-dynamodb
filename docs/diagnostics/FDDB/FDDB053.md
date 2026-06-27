# FDDB053: Conflicting index partition key attribute

## Code & Severity

| Field | Value |
|-------|-------|
| Code | `FDDB053` |
| Severity | Error |

## Message

`Index '{0}' has conflicting partition key attributes: '{1}' on entity '{2}' vs '{3}' on entity '{4}'`

## Description

When multiple entities define the same DynamoDB index, they must use the same DynamoDB attribute for the partition key. Different C# property names are allowed as long as they map to the same DynamoDB attribute name.

A DynamoDB GSI has a fixed schema—it can only have one partition key attribute. If entities disagree on which DynamoDB attribute serves as the partition key for a given index, the index configuration is invalid.

## Example

The following code triggers this diagnostic:

```csharp
[DynamoDbTable("shared-table", IsDefault = true)]
public partial class Order
{
    [PartitionKey]
    [DynamoDbAttribute("pk")]
    public string Pk { get; set; } = string.Empty;

    [GsiPartitionKey("status-index")]
    [DynamoDbAttribute("status")]
    public string Status { get; set; } = string.Empty;
}

[DynamoDbTable("shared-table")]
public partial class Customer
{
    [PartitionKey]
    [DynamoDbAttribute("pk")]
    public string Pk { get; set; } = string.Empty;

    [GsiPartitionKey("status-index")]
    [DynamoDbAttribute("category")]
    public string Category { get; set; } = string.Empty;
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

    [GsiPartitionKey("status-index")]
    [DynamoDbAttribute("status")]
    public string Status { get; set; } = string.Empty;
}

[DynamoDbTable("shared-table")]
public partial class Customer
{
    [PartitionKey]
    [DynamoDbAttribute("pk")]
    public string Pk { get; set; } = string.Empty;

    [GsiPartitionKey("status-index")]
    [DynamoDbAttribute("status")]
    public string CustomerStatus { get; set; } = string.Empty;
}
```
