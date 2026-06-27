# DYNDB121: Duplicate GSI partition keys

## Code & Severity

| Field | Value |
|-------|-------|
| Code | `DYNDB121` |
| Severity | Error |

## Message

`GSI '{0}' on entity '{1}' has multiple partition keys: properties '{2}' and '{3}'. Only one is allowed.`

## Description

A Global Secondary Index can only have one partition key property. Remove the duplicate [GsiPartitionKey] attribute from one of the properties. DynamoDB indexes support exactly one partition key attribute.

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

    [GsiPartitionKey("status-index")]
    [DynamoDbAttribute("category")]
    public string Category { get; set; } = string.Empty;
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
    [DynamoDbAttribute("category")]
    public string Category { get; set; } = string.Empty;
}
```
