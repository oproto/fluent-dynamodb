# DYNDB123: Duplicate LSI sort keys

## Code & Severity

| Field | Value |
|-------|-------|
| Code | `DYNDB123` |
| Severity | Error |

## Message

`LSI '{0}' on entity '{1}' has multiple sort keys: properties '{2}' and '{3}'. Only one is allowed.`

## Description

A Local Secondary Index can only have one sort key property. Remove the duplicate [LsiSortKey] attribute from one of the properties. DynamoDB Local Secondary Indexes support exactly one sort key attribute.

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

    [LsiSortKey("lsi-dates")]
    [DynamoDbAttribute("createdAt")]
    public DateTime CreatedAt { get; set; }

    [LsiSortKey("lsi-dates")]
    [DynamoDbAttribute("updatedAt")]
    public DateTime UpdatedAt { get; set; }
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

    [LsiSortKey("lsi-updated")]
    [DynamoDbAttribute("updatedAt")]
    public DateTime UpdatedAt { get; set; }
}
```
