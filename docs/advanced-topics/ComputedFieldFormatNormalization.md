---
title: "Computed Field Format Normalization"
category: "advanced-topics"
order: 16
keywords: ["computed fields", "format string", "source generator", "internal", "ComputedFieldMetadata", "string.Format"]
related: ["InternalArchitecture.md", "../core-features/ComputedFieldUpdates.md"]
---

[Documentation](../README.md) > [Advanced Topics](README.md) > Computed Field Format Normalization

# Computed Field Format Normalization

---

This document describes an internal refactoring of how computed field metadata is represented at runtime. **The user-facing `ComputedAttribute` API is unchanged** — this is purely an internal simplification that affects library internals and contributors.

---

## Summary

All computed field configurations are now normalized into a single `Format` string at compile time by the source generator. The runtime `ComputedFieldMetadata` class no longer carries `Separator`, `Prefix`, or `PrefixSeparator` properties. Instead, a single `Format` property holds a .NET composite format string (e.g., `"{0}#{1}"`) that all runtime paths use via `string.Format(format, values)`.

### What Changed

| Aspect | Before | After |
|--------|--------|-------|
| `ComputedFieldMetadata` properties | `SourceProperties`, `Separator`, `Prefix`, `PrefixSeparator` | `SourceProperties`, `Format` |
| Runtime recomputation (Update path) | `string.Join(sep, parts)` + prefix concatenation | `string.Format(cf.Format, parts)` |
| Source generator output | Emits `Separator`, `Prefix`, `PrefixSeparator` literals | Emits pre-computed `Format` string |
| Cross-operation consistency | Implicit (different code paths must agree) | Guaranteed by construction (all paths use same format) |

### What Did NOT Change

- The `ComputedAttribute` class (user-facing API)
- The `ComputedAttribute.Separator` and `ComputedAttribute.Format` properties
- Entity definitions using `[Computed(..., Separator = "#")]`
- Generated values for existing configurations (byte-for-byte identical output)
- The `Keys` builder path and `Put` mapper path logic

---

## Motivation

Prior to this change, the library computed field values using different mechanisms depending on the operation path:

- **Keys builder & Put mapper**: Already used `string.Format` when `HasCustomFormat` was true
- **Update recomputation**: Used `string.Join` with manual prefix prepend logic

This inconsistency meant that any change to format logic had to be replicated across multiple code paths. By normalizing everything into a single format string at compile time, all runtime paths become identical:

```csharp
// All paths now use the same code:
var recomputedValue = string.Format(cf.Format, values);
```

---

## ComputedFieldMetadata (Simplified)

The runtime metadata class now has only two properties:

```csharp
public class ComputedFieldMetadata
{
    /// <summary>
    /// Ordered list of source property names that compose this computed field.
    /// </summary>
    public string[] SourceProperties { get; set; } = Array.Empty<string>();

    /// <summary>
    /// .NET composite format string for string.Format(). Contains positional
    /// placeholders {0} through {N-1}. Always non-null at runtime.
    /// </summary>
    public string Format { get; set; } = "{0}";
}
```

**Removed properties**: `Separator` (string), `Prefix` (string?), `PrefixSeparator` (string?)

---

## Format String Generation Rules

The source generator translates `ComputedAttribute` configurations into format strings at compile time using these rules:

| Configuration | Generated Format |
|---|---|
| `Separator="#"`, 2 sources | `"{0}#{1}"` |
| `Separator="#"`, 3 sources | `"{0}#{1}#{2}"` |
| `Separator="_"`, 2 sources | `"{0}_{1}"` |
| `Prefix="ORDER"`, KeySep="#", CompSep="#", 2 sources | `"ORDER#{0}#{1}"` |
| Explicit `Format="TENANT#{0}#USER#{1}#"` | `"TENANT#{0}#USER#{1}#"` |

### Priority Rules

1. **Explicit `Format`** takes highest priority — used verbatim if specified
2. **Separator** is used to interleave between positional placeholders when no explicit `Format` is set
3. **Key prefix** (from `[PartitionKey(Prefix = "...")]` or `[SortKey(Prefix = "...")]`) is prepended with the key attribute's separator

### Format String Construction Logic

```
If explicit Format is specified:
    → use Format unchanged

Else:
    → join placeholders with Separator: "{0}<sep>{1}<sep>...{N-1}"
    → if key prefix exists: prepend "prefix<keySep>" to the result
```

---

## Runtime Recomputation

The `UpdateExpressionTranslator` now uses a single `string.Format` call for all computed field recomputation:

```csharp
// Build ordered array of source values (null → string.Empty)
var parts = cf.SourceProperties
    .Select(s => (object)(assignedSources[s]?.ToString() ?? string.Empty))
    .ToArray();

// Single format call — identical to Keys and Put paths
var recomputedValue = string.Format(cf.Format, parts);
```

This replaces the previous multi-step approach:
```csharp
// OLD (removed):
var parts = cf.SourceProperties.Select(s => assignedSources[s]?.ToString() ?? string.Empty).ToArray();
var recomputedValue = string.Join(cf.Separator, parts);
if (!string.IsNullOrEmpty(cf.Prefix))
{
    var prefixSep = cf.PrefixSeparator ?? cf.Separator;
    recomputedValue = cf.Prefix + prefixSep + recomputedValue;
}
```

---

## FDDB090 Diagnostic

A new compile-time error diagnostic is emitted when an explicit `Format` has a placeholder count that doesn't match the number of source properties:

| Property | Value |
|----------|-------|
| Code | `FDDB090` |
| Severity | Error |
| Message | `Computed property '{name}' has format '{format}' with {N} placeholders but {M} source properties` |
| Trigger | `[Computed("A", "B", Format = "{0}#{1}#{2}")]` — 3 placeholders but only 2 sources |

This diagnostic ensures that misconfigured explicit format strings are caught at compile time rather than causing runtime `FormatException` errors.

---

## Impact on Contributors

If you are contributing to the library and working with computed field internals:

1. **Do not reference** `cf.Separator`, `cf.Prefix`, or `cf.PrefixSeparator` — these no longer exist on `ComputedFieldMetadata`
2. **Always use** `string.Format(cf.Format, values)` for any new runtime path that reconstructs computed values
3. **Format generation** happens in `MapperGenerator.ComputeFormatString()` — this is the single point of truth for how configurations translate to format strings
4. **Testing**: The `ComputeFormatString` helper is a pure function that can be unit tested directly without source generator infrastructure

---

## See Also

- [Computed Field Updates](../core-features/ComputedFieldUpdates.md) — User-facing documentation for computed field update patterns
- [Internal Architecture](InternalArchitecture.md) — Overview of the source generator pipeline
- [Source Generator Guide](../SourceGeneratorGuide.md) — General source generator documentation
