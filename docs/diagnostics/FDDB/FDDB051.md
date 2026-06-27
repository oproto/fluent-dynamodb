# FDDB051: Non-partial table type

## Code & Severity

| Field | Value |
|-------|-------|
| Code | `FDDB051` |
| Severity | Error |

## Message

`Type '{0}' must be declared as partial when used in [DynamoDbTable(typeof({0}))]`

## Description

When using type-based table references with [DynamoDbTable(typeof(T))], the referenced type must be declared as a partial class to allow the source generator to add implementation code.

Type-based table references provide compile-time safety for table name resolution. The referenced type becomes the generated table class, so it must be partial to receive the generated members.

## Example

The following code triggers this diagnostic:

```csharp
// Non-partial table type
public class OrdersTable { }

[DynamoDbTable(typeof(OrdersTable))]
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

## Fix

The corrected version:

```csharp
// Declared as partial
public partial class OrdersTable { }

[DynamoDbTable(typeof(OrdersTable))]
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
