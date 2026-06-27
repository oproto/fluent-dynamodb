# DYNDB010: Entity must be partial

## Code & Severity

| Field | Value |
|-------|-------|
| Code | `DYNDB010` |
| Severity | Error |

## Message

`Entity class '{0}' must be declared as 'partial' to support source generation`

## Description

DynamoDB entity classes must be declared as partial to allow the source generator to add implementation code. The generator creates mapping methods, key builders, and interface implementations in a separate partial class file.

## Example

The following code triggers this diagnostic:

```csharp
[DynamoDbTable("Orders")]
public class Order
{
    [PartitionKey]
    [DynamoDbAttribute("pk")]
    public string Pk { get; set; } = string.Empty;

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
```
