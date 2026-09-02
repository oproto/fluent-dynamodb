# DYNDB026: Invalid GSI projection

## Code & Severity

| Field | Value |
|-------|-------|
| Code | `DYNDB026` |
| Severity | Error |

## Message

`Global Secondary Index '{0}' has invalid projection configuration: {1}`

## Description

GSI projections must be properly configured to include all necessary attributes. Invalid projection configurations may cause runtime failures or missing data when querying the index.

## Example

The following code triggers this diagnostic:

```csharp
[DynamoDbTable("Orders")]
public partial class Order
{
    [PartitionKey]
    [DynamoDbAttribute("pk")]
    public string Pk { get; set; } = string.Empty;

    [GsiPartitionKey("status-index", ProjectionType = ProjectionType.Include)]
    [DynamoDbAttribute("status")]
    public string Status { get; set; } = string.Empty;
}
```

## Fix

The corrected version:

```csharp
[DynamoDbTable("Orders")]
public partial class Order
{
    [PartitionKey]
    [DynamoDbAttribute("pk")]
    public string Pk { get; set; } = string.Empty;

    [GsiPartitionKey("status-index", ProjectionType = ProjectionType.All)]
    [DynamoDbAttribute("status")]
    public string Status { get; set; } = string.Empty;
}
```
