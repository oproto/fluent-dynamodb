# FDDB080: Unresolvable source property in computed key

## Code & Severity

| Field | Value |
|-------|-------|
| Code | `FDDB080` |
| Severity | Error |

## Message

`Cannot resolve source property '{0}' for computed key on '{1}.{2}'. Convenience overload will not be generated.`

## Description

A source property referenced in a computed key's SourceProperties array could not be found in the entity's property collection. The typed parameter convenience overload will not be generated for this entity.

When you define a [Computed] attribute with source property names, each name must correspond to an actual property on the entity. If a name is misspelled or references a non-existent property, the source generator cannot create the typed convenience overload for key construction.

## Example

The following code triggers this diagnostic:

```csharp
[DynamoDbTable("events")]
public partial class Event
{
    [PartitionKey]
    [DynamoDbAttribute("pk")]
    [Computed("Year", "Moth", "Day", Separator = "#")]
    public string Pk { get; set; } = string.Empty;

    [Extracted("Pk", 0)]
    public int Year { get; set; }

    // "Moth" doesn't match "Month"
    [Extracted("Pk", 1)]
    public int Month { get; set; }

    [Extracted("Pk", 2)]
    public int Day { get; set; }
}
```

## Fix

The corrected version:

```csharp
[DynamoDbTable("events")]
public partial class Event
{
    [PartitionKey]
    [DynamoDbAttribute("pk")]
    [Computed("Year", "Month", "Day", Separator = "#")]
    public string Pk { get; set; } = string.Empty;

    [Extracted("Pk", 0)]
    public int Year { get; set; }

    [Extracted("Pk", 1)]
    public int Month { get; set; }

    [Extracted("Pk", 2)]
    public int Day { get; set; }
}
```
