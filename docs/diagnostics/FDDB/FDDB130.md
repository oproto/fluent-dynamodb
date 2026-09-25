# FDDB130: Bare RelatedEntity unresolved entity type

## Code & Severity

| Field | Value |
|-------|-------|
| Code | `FDDB130` |
| Severity | Error |

## Message

`[RelatedEntity] on property '{0}' in entity '{1}' references type '{2}' which is not a known [DynamoDbTable] entity. Specify EntityType explicitly or provide an explicit pattern string.`

## Description

A bare `[RelatedEntity]` attribute (with no explicit pattern string) infers the child entity type from the property's generic type argument (e.g., `List<OrderLine>` → `OrderLine`). The resolved type must be a class annotated with `[DynamoDbTable]` in the same table group.

This diagnostic is emitted when the inferred type cannot be found among any analyzed `[DynamoDbTable]` entities. Common causes include:
- The type is not annotated with `[DynamoDbTable]`
- The type is in a different assembly that isn't part of the current compilation
- A typo in the type name

## Example

The following code triggers this diagnostic:

```csharp
// OrderLine is NOT annotated with [DynamoDbTable]
public class OrderLine
{
    public string LineId { get; set; } = string.Empty;
    public decimal Amount { get; set; }
}

[DynamoDbTable("orders")]
public partial class Order
{
    [PartitionKey]
    [DynamoDbAttribute("pk")]
    public string Pk { get; set; } = string.Empty;

    [RelatedEntity]  // ❌ FDDB130: OrderLine is not a [DynamoDbTable] entity
    public List<OrderLine> Lines { get; set; } = new();
}
```

## Fix

Either annotate the child entity with `[DynamoDbTable]`, specify `EntityType` explicitly, or provide an explicit pattern string:

```csharp
// Option 1: Annotate child entity with [DynamoDbTable]
[DynamoDbTable("orders")]
public partial class OrderLine
{
    [PartitionKey]
    [DynamoDbAttribute("pk")]
    public string Pk { get; set; } = string.Empty;

    [SortKey(Prefix = "LINE")]
    [DynamoDbAttribute("sk")]
    public string Sk { get; set; } = string.Empty;
}

// Option 2: Use explicit pattern string
[RelatedEntity("LINE#*")]
public List<OrderLine> Lines { get; set; } = new();
```
