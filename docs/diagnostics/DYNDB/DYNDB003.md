# DYNDB003: Multiple sort keys

## Code & Severity

| Field | Value |
|-------|-------|
| Code | `DYNDB003` |
| Severity | Error |

## Message

`Entity '{0}' has multiple properties marked with [SortKey]. Only one is allowed.`

## Description

A DynamoDB entity can only have one sort key property. DynamoDB tables support at most one sort key attribute, and the source generator enforces this constraint at compile time.

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

    [SortKey]
    [DynamoDbAttribute("createdAt")]
    public string CreatedAt { get; set; } = string.Empty;
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

    [DynamoDbAttribute("createdAt")]
    public string CreatedAt { get; set; } = string.Empty;
}
```
