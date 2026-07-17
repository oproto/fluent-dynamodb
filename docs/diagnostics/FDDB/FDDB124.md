# FDDB124: Extracted property conflicts with DynamoDbAttribute

## Code & Severity

| Field | Value |
|-------|-------|
| Code | `FDDB124` |
| Severity | Error |

## Message

`Property '{0}' has both [Extracted] and [DynamoDbAttribute]. Extracted properties derive their value from a composite key and must not have independent DynamoDB attribute mapping. Remove one of the attributes.`

## Description

An `[Extracted]` property derives its value from a composite key at read time and should not also map to an independent DynamoDB attribute. Having both attributes creates a conflict: the property cannot simultaneously be an extracted component of a composite key and an independently stored/retrieved attribute.

When this diagnostic fires, code generation is halted for the affected property's extraction logic, and an early return prevents cascading diagnostics.

## Example

The following code triggers this diagnostic:

```csharp
[DynamoDbTable("events")]
public partial class Event
{
    [PartitionKey]
    [DynamoDbAttribute("pk")]
    [Computed("Year", "Month", Separator = "#")]
    public string Pk { get; set; } = string.Empty;

    [DynamoDbAttribute("year")]   // ❌ Conflict: also has [Extracted]
    [Extracted("Pk", 0)]
    public int Year { get; set; }
}
```

## Fix

Remove the `[DynamoDbAttribute]` from the extracted property. Extracted properties get their value from the composite key at read time and do not need an independent attribute mapping:

```csharp
[DynamoDbTable("events")]
public partial class Event
{
    [PartitionKey]
    [DynamoDbAttribute("pk")]
    [Computed("Year", "Month", Separator = "#")]
    public string Pk { get; set; } = string.Empty;

    [Extracted("Pk", 0)]
    public int Year { get; set; }  // ✅ Value extracted from Pk at read time
}
```
