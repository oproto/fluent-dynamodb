# DYNDB124: Empty GsiPartitionKey index name

## Code & Severity

| Field | Value |
|-------|-------|
| Code | `DYNDB124` |
| Severity | Error |

## Message

`[GsiPartitionKey] on property '{0}' has an empty or whitespace index name`

## Description

The index name parameter on [GsiPartitionKey] must be a non-empty, non-whitespace string. The index name is used to identify the GSI in DynamoDB and to generate the corresponding property accessor on the table class.

## Example

The following code triggers this diagnostic:

```csharp
[DynamoDbTable("Orders")]
public partial class Order
{
    [PartitionKey]
    [DynamoDbAttribute("pk")]
    public string Pk { get; set; } = string.Empty;

    [GsiPartitionKey("")]
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

    [GsiPartitionKey("status-index")]
    [DynamoDbAttribute("status")]
    public string Status { get; set; } = string.Empty;
}
```
