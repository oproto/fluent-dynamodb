# FDDB093: Mixed named and positional placeholders

## Code & Severity

| Field | Value |
|-------|-------|
| Code | `FDDB093` |
| Severity | Error |

## Message

`Format string on property '{0}' mixes named placeholders (e.g., {{{1}}}) with positional placeholders (e.g., {{{2}}}). Use all named or all positional placeholders.`

## Description

A format string must use either all named placeholders (`{PropertyName}`) or all positional placeholders (`{N}`). Mixing the two styles in the same format string is not allowed because the source generator cannot consistently determine which tokens reference properties by name and which reference properties by positional index.

Named placeholders infer source properties from the format string, while positional placeholders require explicitly listed source properties. These two approaches are incompatible in a single format string.

## Example

The following code triggers this diagnostic:

```csharp
[DynamoDbTable("events")]
public partial class Event
{
    [PartitionKey]
    [DynamoDbAttribute("pk")]
    [Computed("{Year}#{0}")]  // ❌ Mixes named {Year} with positional {0}
    public string Pk { get; set; } = string.Empty;

    [DynamoDbAttribute("year")]
    public int Year { get; set; }

    [DynamoDbAttribute("month")]
    public int Month { get; set; }
}
```

**Diagnostic output:**
```
Error FDDB093: Format string on property 'Pk' mixes named placeholders (e.g., {Year}) with positional placeholders (e.g., {0}). Use all named or all positional placeholders.
```

## Fix

Use all named placeholders:

```csharp
[PartitionKey]
[DynamoDbAttribute("pk")]
[Computed("{Year}#{Month}")]  // ✅ All named placeholders
public string Pk { get; set; } = string.Empty;
```

Or use all positional placeholders with explicit source properties:

```csharp
[PartitionKey]
[DynamoDbAttribute("pk")]
[Computed("Year", "Month", Format = "{0}#{1}")]  // ✅ All positional placeholders
public string Pk { get; set; } = string.Empty;
```
