# DYNDB002: Multiple partition keys

## Code & Severity

| Field | Value |
|-------|-------|
| Code | `DYNDB002` |
| Severity | Error |

## Message

`Entity '{0}' has multiple properties marked with [PartitionKey]. Only one is allowed.`

## Description

A DynamoDB entity can only have one partition key property. DynamoDB tables have a single partition key attribute, and the source generator enforces this constraint at compile time.

## Example

The following code triggers this diagnostic:

```csharp
[DynamoDbTable("Orders")]
public partial class Order
{
    [PartitionKey]
    [DynamoDbAttribute("pk")]
    public string Pk { get; set; } = string.Empty;

    [PartitionKey]
    [DynamoDbAttribute("customerId")]
    public string CustomerId { get; set; } = string.Empty;
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

    [DynamoDbAttribute("customerId")]
    public string CustomerId { get; set; } = string.Empty;
}
```
