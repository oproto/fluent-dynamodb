# Design: Unify Keys Class API

## Overview

This design unifies the key construction API in the generated `Keys` class by eliminating the split between `Pk()`/`Sk()` and `BuildPk()`/`BuildSk()`. A single set of methods — `Pk()` and `Sk()` — handles both prefix-based and computed key construction. The `Key()` composite method is also removed.

### Key Decision: Method Naming

**`Pk()` / `Sk()`** — not `BuildPk()` / `BuildSk()`.

Rationale:
- `Pk`/`Sk` is shorter and already used in all documentation, examples, and consuming code for the prefix case.
- The "Build" prefix communicates assembly from components, but from the consumer's perspective ALL key construction is "give inputs, get formatted key" — the distinction between prefix-prepend and format-string is an internal implementation detail.
- Fewer characters, less cognitive overhead.

## Architecture

The generated `Keys` class currently has two code paths for key construction:

1. `GenerateMainTableKeyBuilders` → calls `GeneratePartitionKeyBuilder` / `GenerateSortKeyBuilder` → emits `Pk(value)`, `Sk(value)`, `Key(pk, sk)`
2. `GenerateComputedKeyBuilders` → calls `GenerateComputedKeyBuilder` → emits `BuildPk(...)`, `BuildSk(...)`

After this change, there is a single code path:

1. `GenerateMainTableKeyBuilders` → calls `GeneratePartitionKeyBuilder` / `GenerateSortKeyBuilder` → emits `Pk(...)` or `Sk(...)` with the correct signature and logic for the key type.

## Components and Interfaces

### KeysGenerator.cs

#### `GeneratePartitionKeyBuilder` modification

Currently checks `IsConstantKey`, then generates a single-param method using `GetKeyFormat()`.

New logic:
```
if (property.IsConstantKey) → emit static property (unchanged)
else if (property.IsComputed) → emit multi-param method using computed format (moved from GenerateComputedKeyBuilder)
else if (property has prefix) → emit single-param method prepending prefix (unchanged)
else → emit nothing (bare key, no useful builder)
```

#### `GenerateSortKeyBuilder` modification

Same pattern as above.

#### Remove `GenerateComputedKeyBuilders` and `GenerateComputedKeyBuilder`

Delete entirely. The logic moves into the main key builder methods.

#### Remove `GenerateCompositeKeyBuilder`

Delete entirely. The `Key()` method is removed from the API.

#### `GenerateMainTableKeyBuilders` modification

Remove the call to `GenerateCompositeKeyBuilder`. Remove the call to `GenerateComputedKeyBuilders`.

### TableGenerator.cs

Any references to `Keys.BuildPk` or `Keys.BuildSk` in the typed overload generation (from the computed-key-accessor-overloads feature) must be updated to `Keys.Pk` / `Keys.Sk`.

## Data Models

### Generated Output Examples

#### Before (Customer — prefix-only PK, constant SK)
```csharp
public static partial class Keys
{
    public static string Pk(string pk) => "CUSTOMER#" + pk;
    public static string Sk => "PROFILE";
    public static (string, string) Key(string pk) => (Pk(pk), Sk);
}
```

#### After (Customer — prefix-only PK, constant SK)
```csharp
public static partial class Keys
{
    public static string Pk(string pk) => "CUSTOMER#" + pk;
    public static string Sk => "PROFILE";
}
```

#### Before (Invoice — prefix PK, computed SK with 1 component)
```csharp
public static partial class Keys
{
    public static string Pk(string pk) => "CUSTOMER#" + pk;
    public static string Sk(string sk) => sk;  // useless passthrough
    public static (string, string) Key(string pk, string sk) => (Pk(pk), Sk(sk));
    public static string BuildSk(string invoiceNumber) => string.Format("INVOICE#{0}", invoiceNumber);
}
```

#### After (Invoice — prefix PK, computed SK with 1 component)
```csharp
public static partial class Keys
{
    public static string Pk(string pk) => "CUSTOMER#" + pk;
    public static string Sk(string invoiceNumber) => string.Format("INVOICE#{0}", invoiceNumber);
}
```

#### Before (InvoiceLine — prefix PK, computed SK with 2 components)
```csharp
public static partial class Keys
{
    public static string Pk(string pk) => "CUSTOMER#" + pk;
    public static string Sk(string sk) => sk;  // useless passthrough
    public static (string, string) Key(string pk, string sk) => (Pk(pk), Sk(sk));
    public static string BuildSk(string invoiceNumber, int lineNumber) => string.Format("INVOICE#{0}#LINE#{1}", invoiceNumber, lineNumber);
}
```

#### After (InvoiceLine — prefix PK, computed SK with 2 components)
```csharp
public static partial class Keys
{
    public static string Pk(string pk) => "CUSTOMER#" + pk;
    public static string Sk(string invoiceNumber, int lineNumber) => string.Format("INVOICE#{0}#LINE#{1}", invoiceNumber, lineNumber);
}
```

#### Before (Event — computed PK with 3 components, simple SK)
```csharp
public static partial class Keys
{
    public static string Pk(string pk) => pk;  // useless passthrough
    public static string Sk(string sk) => sk;  // useless passthrough
    public static (string, string) Key(string pk, string sk) => (Pk(pk), Sk(sk));
    public static string BuildPk(int year, int month, int day) => string.Format("{0}#{1}#{2}", year, month, day);
}
```

#### After (Event — computed PK with 3 components, bare SK)
```csharp
public static partial class Keys
{
    public static string Pk(int year, int month, int day) => string.Format("{0}#{1}#{2}", year, month, day);
    // No Sk() — bare key with no prefix/computed has no useful builder
}
```

### Migration Guide (for consumers)

| Before | After |
|--------|-------|
| `Entity.Keys.BuildPk(a, b)` | `Entity.Keys.Pk(a, b)` |
| `Entity.Keys.BuildSk(a, b)` | `Entity.Keys.Sk(a, b)` |
| `Entity.Keys.Key(pk, sk)` | Use `Entity.Keys.Pk(...)` and `Entity.Keys.Sk(...)` separately |
| `Entity.Keys.Sk(rawValue)` on computed key | `Entity.Keys.Sk(component)` — now returns correct formatted value |

## Correctness Properties

*A property is a characteristic or behavior that should hold true across all valid executions of a system — essentially, a formal statement about what the system should do. Properties serve as the bridge between human-readable specifications and machine-verifiable correctness guarantees.*

### Property 1: Prefix key construction preserves prefix format

*For any* valid value string, `Pk(value)` (or `Sk(value)`) on a prefix-based key SHALL return a string that starts with the configured prefix followed by the separator and the original value.

**Validates: Requirements 1.1, 1.2**

### Property 2: Computed key construction applies format string

*For any* valid set of component values matching the source property types, `Pk(...)` (or `Sk(...)`) on a computed key SHALL return a string equivalent to `string.Format(formatString, components...)`.

**Validates: Requirements 2.1, 2.2, 2.3, 2.4**

### Property 3: No Build-prefixed methods in output

*For any* entity processed by the generator, the emitted Keys class SHALL contain no method whose name starts with "Build".

**Validates: Requirements 3.1, 3.2**

### Property 4: No Key() composite method in output

*For any* entity processed by the generator, the emitted Keys class SHALL contain no method named "Key".

**Validates: Requirements 4.1, 4.2**

### Property 5: Bare keys produce no method

*For any* key property with no prefix and no `[Computed]` attribute, the generator SHALL emit no `Pk()` or `Sk()` method for that key.

**Validates: Requirements 7.1, 7.2**

### Property 6: Typed overloads delegate to unified methods

*For any* entity with typed Get/Delete/Update overloads, those overloads SHALL delegate to `Keys.Pk(...)` and `Keys.Sk(...)` and produce the same key string as calling those methods directly.

**Validates: Requirements 10.1**

## Error Handling

This feature does not introduce new runtime error paths. The key construction methods are pure string formatting operations. Errors are caught at compile time by the source generator:

- If a computed key references a source property that doesn't exist, the generator already emits a diagnostic.
- If typed overloads reference removed methods (`BuildPk`/`BuildSk`), the build will fail until `TableGenerator.cs` is updated.
- Consumer code referencing `BuildPk()`/`BuildSk()`/`Key()` will fail to compile after the change, providing clear migration signals.

## Testing Strategy

### Unit Tests (KeysGeneratorTests)

- Tests asserting `BuildPk`/`BuildSk` in generated output need to assert `Pk`/`Sk` instead.
- Tests asserting `Key(` in generated output need to be removed.
- Tests asserting passthrough `Sk(string sk) { return sk; }` for computed keys need to assert the computed logic instead.

### Integration Tests (ComputedKeyTypedOverloadEquivalenceTests)

- References to `Keys.BuildSk(...)` → `Keys.Sk(...)`
- References to `Keys.BuildPk(...)` → `Keys.Pk(...)`

### Example Tests (InvoicePropertyTests)

- `Invoice.Keys.Sk(invoiceNumber)` now correctly returns "INVOICE#{invoiceNumber}" — tests should pass without code changes.
- `InvoiceLine.Keys.BuildSk(invoiceNumber, lineNumber)` → `InvoiceLine.Keys.Sk(invoiceNumber, lineNumber)`

### Property-Based Tests (DelegationToKeysBuildPropertyTests)

- Update assertions to check delegation to `Keys.Pk(...)` / `Keys.Sk(...)`.

### Property Test Configuration

- Minimum 100 iterations per property test
- Each property test references its design document property
- Tag format: **Feature: unify-keys-class-api, Property {number}: {property_text}**
