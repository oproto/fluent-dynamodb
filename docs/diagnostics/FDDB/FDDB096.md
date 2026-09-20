# FDDB096: Ambiguous placeholder name/index

## Code & Severity

| Field | Value |
|-------|-------|
| Code | `FDDB096` |
| Severity | Warning |

## Message

`Placeholder '{{{1}}}' on property '{0}' matches both a property name and a positional index. It was resolved as a property name. Consider renaming the property to avoid ambiguity.`

## Description

A placeholder token matches both a property name on the entity and a valid positional index. For example, `{0}` could be interpreted as either a positional placeholder referencing the first source property, or a named placeholder referencing a property literally named `"0"`. In this case, the source generator resolves the token as a property name reference and emits this warning.

In practice, this diagnostic is unreachable because valid C# property identifiers must start with a letter or underscore — they cannot be plain integers. However, the diagnostic exists as a defensive measure in case the source generator encounters an edge case where a property name could be confused with a positional index.

## Example

The following code would theoretically trigger this diagnostic:

```csharp
// Note: This is a theoretical example. In practice, C# identifiers
// cannot be plain integers, so this scenario is unreachable.
[DynamoDbTable("items")]
public partial class Item
{
    [PartitionKey]
    [DynamoDbAttribute("pk")]
    [Computed("PFX#{0}")]  // ⚠️ Ambiguous: is {0} a positional index or a property named "0"?
    public string Pk { get; set; } = string.Empty;

    // Hypothetical property named "0" — not valid C# without escaping
    [DynamoDbAttribute("zeroField")]
    public string @0 { get; set; } = string.Empty;
}
```

**Diagnostic output:**
```
Warning FDDB096: Placeholder '{0}' on property 'Pk' matches both a property name and a positional index. It was resolved as a property name. Consider renaming the property to avoid ambiguity.
```

## Fix

Rename the property to avoid ambiguity between property names and positional indices:

```csharp
[DynamoDbTable("items")]
public partial class Item
{
    [PartitionKey]
    [DynamoDbAttribute("pk")]
    [Computed("PFX#{ZeroField}")]  // ✅ Unambiguous named placeholder
    public string Pk { get; set; } = string.Empty;

    [DynamoDbAttribute("zeroField")]
    public string ZeroField { get; set; } = string.Empty;
}
```
