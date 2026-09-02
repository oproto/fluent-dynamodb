# DYNDB127: GSI/LSI index name conflict

## Code & Severity

| Field | Value |
|-------|-------|
| Code | `DYNDB127` |
| Severity | Error |

## Message

`Index name '{0}' on entity '{1}' is used as both a GSI and an LSI. An index name must be exclusively GSI or LSI.`

## Description

A DynamoDB index name cannot be used as both a Global Secondary Index and a Local Secondary Index within the same entity. Use distinct index names for GSI and LSI. GSIs and LSIs have fundamentally different key schemas and projection behaviors.

## Example

The following code triggers this diagnostic:

```csharp
[DynamoDbTable("Orders")]
public partial class Order
{
    [PartitionKey]
    [DynamoDbAttribute("pk")]
    public string Pk { get; set; } = string.Empty;

    [SortKey]
    [DynamoDbAttribute("sk")]
    public string Sk { get; set; } = string.Empty;

    [GsiPartitionKey("date-index")]
    [DynamoDbAttribute("status")]
    public string Status { get; set; } = string.Empty;

    [LsiSortKey("date-index")]
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

    [SortKey]
    [DynamoDbAttribute("sk")]
    public string Sk { get; set; } = string.Empty;

    [GsiPartitionKey("status-index")]
    [DynamoDbAttribute("status")]
    public string Status { get; set; } = string.Empty;

    [LsiSortKey("lsi-created")]
    [DynamoDbAttribute("createdAt")]
    public DateTime CreatedAt { get; set; }
}
```
