# FDDB133: Bare RelatedEntity non-generic collection type

## Code & Severity

| Field | Value |
|-------|-------|
| Code | `FDDB133` |
| Severity | Error |

## Message

`[RelatedEntity] on property '{0}' in entity '{1}' uses a non-generic collection type '{2}'. The element type cannot be inferred. Specify EntityType explicitly or use a generic collection type such as List<T>.`

## Description

A bare `[RelatedEntity]` attribute infers the child entity type from the property's generic type argument (e.g., `List<T>` → `T`). Non-generic collection types such as `ArrayList` or raw `IEnumerable` do not provide a type argument, so the source generator cannot determine which entity type to map matching items to.

This diagnostic is emitted when the property type is a collection but does not have a generic type argument that can be used for entity type inference.

## Example

The following code triggers this diagnostic:

```csharp
using System.Collections;

[DynamoDbTable("orders")]
public partial class Order
{
    [PartitionKey]
    [DynamoDbAttribute("pk")]
    public string Pk { get; set; } = string.Empty;

    [SortKey(Prefix = "ORDER")]
    [DynamoDbAttribute("sk")]
    public string Sk { get; set; } = string.Empty;

    [RelatedEntity]  // ❌ FDDB133: ArrayList is non-generic, element type cannot be inferred
    public ArrayList Lines { get; set; } = new();
}
```

## Fix

Either switch to a generic collection type so the element type can be inferred, or specify `EntityType` explicitly:

```csharp
// Option 1: Use a generic collection type (preferred)
[RelatedEntity]
public List<OrderLine> Lines { get; set; } = new();

// Option 2: Specify EntityType explicitly
[RelatedEntity(EntityType = typeof(OrderLine))]
public ArrayList Lines { get; set; } = new();
```
