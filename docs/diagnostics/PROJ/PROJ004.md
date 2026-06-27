# PROJ004: Projection must be partial

## Code & Severity

| Field | Value |
|-------|-------|
| Code | `PROJ004` |
| Severity | Error |

## Message

`Projection class '{0}' must be declared as 'partial' to support source generation`

## Description

Projection classes must be declared as partial to allow the source generator to add mapping code. The source generator extends the projection class with implementations of `IReadOnlyEntity` and `IProjectionModel`, which requires the class to be declared with the `partial` keyword.

Without the `partial` modifier, the source generator cannot add the generated code to the class, and the projection will not function correctly at runtime.

## Example

The following code triggers this diagnostic:

```csharp
[DynamoDbTable("Orders")]
public partial class Order
{
    [PartitionKey]
    [DynamoDbAttribute("pk")]
    public string Pk { get; set; } = string.Empty;

    [DynamoDbAttribute("status")]
    public string Status { get; set; } = string.Empty;
}

// PROJ004: class must be declared as 'partial'
[DynamoDbProjection(typeof(Order))]
public class OrderSummary
{
    [DynamoDbAttribute("status")]
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

    [DynamoDbAttribute("status")]
    public string Status { get; set; } = string.Empty;
}

[DynamoDbProjection(typeof(Order))]
public partial class OrderSummary
{
    [DynamoDbAttribute("status")]
    public string Status { get; set; } = string.Empty;
}
```
