# DYNDB030: Invalid attribute name

## Code & Severity

| Field | Value |
|-------|-------|
| Code | `DYNDB030` |
| Severity | Error |

## Message

`Attribute name '{0}' on property '{1}' is invalid: {2}`

## Description

DynamoDB attribute names must follow naming conventions and cannot contain certain characters. Attribute names must be between 1 and 255 characters long and should not begin with a number when used in expressions.

## Example

The following code triggers this diagnostic:

```csharp
[DynamoDbTable("Orders")]
public partial class Order
{
    [PartitionKey]
    [DynamoDbAttribute("pk")]
    public string Pk { get; set; } = string.Empty;

    [DynamoDbAttribute("")]
    public string Status { get; set; } = string.Empty;
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

    [DynamoDbAttribute("orderStatus")]
    public string Status { get; set; } = string.Empty;
}
```
