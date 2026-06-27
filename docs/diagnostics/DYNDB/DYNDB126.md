# DYNDB126: Empty LsiSortKey index name

## Code & Severity

| Field | Value |
|-------|-------|
| Code | `DYNDB126` |
| Severity | Error |

## Message

`[LsiSortKey] on property '{0}' has an empty or whitespace index name`

## Description

The index name parameter on [LsiSortKey] must be a non-empty, non-whitespace string. The index name is used to identify the Local Secondary Index in DynamoDB and to generate the corresponding accessor on the table class.

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

    [LsiSortKey("")]
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

    [LsiSortKey("lsi-created")]
    [DynamoDbAttribute("createdAt")]
    public DateTime CreatedAt { get; set; }
}
```
