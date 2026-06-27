# DYNDB125: Empty GsiSortKey index name

## Code & Severity

| Field | Value |
|-------|-------|
| Code | `DYNDB125` |
| Severity | Error |

## Message

`[GsiSortKey] on property '{0}' has an empty or whitespace index name`

## Description

The index name parameter on [GsiSortKey] must be a non-empty, non-whitespace string. The index name links the sort key to its corresponding GSI partition key definition.

## Example

The following code triggers this diagnostic:

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

    [GsiSortKey("  ")]
    [DynamoDbAttribute("createdAt")]
    public DateTime CreatedAt { get; set; }
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

    [GsiSortKey("status-index")]
    [DynamoDbAttribute("createdAt")]
    public DateTime CreatedAt { get; set; }
}
```
