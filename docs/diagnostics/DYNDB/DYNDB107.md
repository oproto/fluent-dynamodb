# DYNDB107: Nested map type missing [DynamoDbEntity]

## Code & Severity

| Field | Value |
|-------|-------|
| Code | `DYNDB107` |
| Severity | Error |

## Message

`Property '{0}' with [DynamoDbMap] has type '{1}' which must be marked with [DynamoDbEntity] to generate mapping code`

## Description

Custom types used with [DynamoDbMap] must be marked with [DynamoDbEntity] to generate the required mapping methods. This ensures AOT compatibility by avoiding reflection. The source generator needs [DynamoDbEntity] to generate the ToDynamoDb/FromDynamoDb methods for the nested type.

## Example

The following code triggers this diagnostic:

```csharp
public partial class Address
{
    [DynamoDbAttribute("street")]
    public string Street { get; set; } = string.Empty;

    [DynamoDbAttribute("city")]
    public string City { get; set; } = string.Empty;
}

[DynamoDbTable("Customers")]
public partial class Customer
{
    [PartitionKey]
    [DynamoDbAttribute("pk")]
    public string Pk { get; set; } = string.Empty;

    [DynamoDbMap]
    [DynamoDbAttribute("address")]
    public Address ShippingAddress { get; set; } = new();
}
```

## Fix

The corrected version:

```csharp
[DynamoDbEntity]
public partial class Address
{
    [DynamoDbAttribute("street")]
    public string Street { get; set; } = string.Empty;

    [DynamoDbAttribute("city")]
    public string City { get; set; } = string.Empty;
}

[DynamoDbTable("Customers")]
public partial class Customer
{
    [PartitionKey]
    [DynamoDbAttribute("pk")]
    public string Pk { get; set; } = string.Empty;

    [DynamoDbMap]
    [DynamoDbAttribute("address")]
    public Address ShippingAddress { get; set; } = new();
}
```
