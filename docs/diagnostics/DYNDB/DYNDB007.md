# DYNDB007: Missing DynamoDbAttribute

## Code & Severity

| Field | Value |
|-------|-------|
| Code | `DYNDB007` |
| Severity | Error |

## Message

`Property '{0}' has DynamoDB key attributes but is missing [DynamoDbAttribute]`

## Description

Properties with DynamoDB key attributes must also have [DynamoDbAttribute] to specify the attribute name. The source generator needs the DynamoDB attribute name to generate correct key handling code and map properties to DynamoDB item attributes.

## Example

The following code triggers this diagnostic:

```csharp
[DynamoDbTable("Orders")]
public partial class Order
{
    [PartitionKey]
    public string Pk { get; set; } = string.Empty;

    [SortKey]
    [DynamoDbAttribute("sk")]
    public string Sk { get; set; } = string.Empty;
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
}
```
