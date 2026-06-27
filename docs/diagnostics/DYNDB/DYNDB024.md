# DYNDB024: Missing required attribute

## Code & Severity

| Field | Value |
|-------|-------|
| Code | `DYNDB024` |
| Severity | Error |

## Message

`Property '{0}' in entity '{1}' is missing required attribute '{2}'`

## Description

Properties used in DynamoDB operations must have appropriate attributes defined. The source generator requires specific attribute combinations for correct code generation of mapping, serialization, and key handling.

## Example

The following code triggers this diagnostic:

```csharp
[DynamoDbTable("Orders")]
public partial class Order
{
    [PartitionKey]
    [DynamoDbAttribute("pk")]
    public string Pk { get; set; } = string.Empty;

    [DynamoDbMap]
    public Address ShippingAddress { get; set; } = new();
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

    [DynamoDbMap]
    [DynamoDbAttribute("shippingAddress")]
    public Address ShippingAddress { get; set; } = new();
}
```
