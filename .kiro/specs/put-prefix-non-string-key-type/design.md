# Put Prefix Non-String Key Type Bugfix Design

## Overview

The source generator's `MapperGenerator.GenerateKeyPrefixApplication` emits code that passes non-string key property values directly to `KeyPrefixHelper.ApplyKeyPrefix(string, ...)`. Since `ApplyKeyPrefix` expects a `string` first argument, any entity with a non-string key type (enum, DateTime, Guid, Ulid, numeric) and a configured prefix produces uncompilable generated code. The fix reuses the existing type-to-string conversion logic in `KeysGenerator.GetValueExpression` by widening its accessibility from `private` to `internal`.

## Glossary

- **Bug_Condition (C)**: A key property has a non-string type AND has a configured prefix (via `[PartitionKey(Prefix = "...")]` or `[SortKey(Prefix = "...")]`)
- **Property (P)**: The generated code applies the correct type-to-string conversion before passing the value to `ApplyKeyPrefix`, producing compilable output
- **Preservation**: String key properties with prefixes continue to pass directly without conversion; key properties without prefixes are unaffected; `KeysGenerator` callers continue to work identically
- **GetValueExpression**: The method in `KeysGenerator.cs` that converts a parameter expression to its string representation based on type (handles string passthrough, Guid/Ulid `.ToString()`, DateTime format strings, numeric `.ToString()`, enum `.ToString()`)
- **GenerateKeyPrefixApplication**: The method in `MapperGenerator.cs` that emits `ApplyKeyPrefix(...)` calls for key properties with a configured prefix
- **KeyPrefixHelper.ApplyKeyPrefix**: Runtime utility that prepends/strips prefix+separator to/from a string key value based on `KeyInputMode`

## Bug Details

### Bug Condition

The bug manifests when a DynamoDB entity has a key property (partition or sort) with both a non-string type and a configured prefix. The `GenerateKeyPrefixApplication` method emits `typedEntity.{PropertyName}` directly as the first argument to `ApplyKeyPrefix(string, ...)`, causing a C# compilation error because the value is not a string.

**Formal Specification:**
```
FUNCTION isBugCondition(input)
  INPUT: input of type (PropertyModel, EntityModel)
  OUTPUT: boolean
  
  RETURN input.Property.PropertyType NOT IN ["string", "String", "System.String"]
         AND (input.Property.IsPartitionKey OR input.Property.IsSortKey)
         AND NOT input.Property.IsComputed
         AND NOT input.Property.IsConstantKey
         AND input.Property.KeyFormat != null
         AND input.Property.KeyFormat.Prefix IS NOT empty
END FUNCTION
```

### Examples

- **Enum key with prefix**: `[SortKey(Prefix = "TOPIC")] public SnsSubscriptionTopic Topic { get; set; }` → generates `ApplyKeyPrefix(typedEntity.Topic, ...)` which fails to compile because `Topic` is an enum, not a string. Expected: `ApplyKeyPrefix(typedEntity.Topic.ToString(), ...)`
- **DateTime key with prefix**: `[SortKey(Prefix = "DATE")] public DateTime CreatedAt { get; set; }` → generates `ApplyKeyPrefix(typedEntity.CreatedAt, ...)`. Expected: `ApplyKeyPrefix(typedEntity.CreatedAt.ToString("yyyy-MM-ddTHH:mm:ss.fffZ"), ...)`
- **Guid key with prefix**: `[PartitionKey(Prefix = "ID")] public Guid EntityId { get; set; }` → generates `ApplyKeyPrefix(typedEntity.EntityId, ...)`. Expected: `ApplyKeyPrefix(typedEntity.EntityId.ToString(), ...)`
- **String key with prefix (no bug)**: `[PartitionKey(Prefix = "USER")] public string UserId { get; set; }` → generates `ApplyKeyPrefix(typedEntity.UserId, ...)` which compiles correctly because `UserId` is already a string

## Expected Behavior

### Preservation Requirements

**Unchanged Behaviors:**
- String key properties with a prefix continue to pass `typedEntity.{PropertyName}` directly (no conversion wrapper added)
- Key properties without a prefix are not affected by this fix (they do not enter `GenerateKeyPrefixApplication`)
- `KeysGenerator.GetValueExpression` continues to produce identical results for all existing callers (key builder methods, computed key construction)
- The `ArgumentNullException.ThrowIfNull` null-check emitted before `ApplyKeyPrefix` remains unchanged
- No behavioral change to runtime `KeyPrefixHelper.ApplyKeyPrefix` logic

**Scope:**
All inputs that do NOT have a non-string key type with a configured prefix are completely unaffected by this fix. This includes:
- String key properties (with or without prefix)
- Key properties without any prefix configured
- Computed key properties (handled separately)
- Constant key properties (handled separately)
- Non-key properties of any type

## Hypothesized Root Cause

Based on the code analysis, the root cause is clear and singular:

1. **Missing type conversion in prefix application path**: `GenerateKeyPrefixApplication` at line 332 of `MapperGenerator.cs` emits:
   ```csharp
   sb.AppendLine($"... ApplyKeyPrefix(typedEntity.{escapedPropertyName}, \"{prefix}\", \"{separator}\", resolvedMode) ...");
   ```
   It uses `typedEntity.{escapedPropertyName}` directly without checking whether the property type requires conversion to string. The parallel code path in `KeysGenerator` (used for key building) already handles this correctly via `GetValueExpression`, but `MapperGenerator` was never wired to call it.

2. **Accessibility barrier**: `GetValueExpression` is `private static` in `KeysGenerator`, making it inaccessible to `MapperGenerator`. Both classes are `internal static` within the same assembly (`Oproto.FluentDynamoDb.SourceGenerator`), so changing to `internal static` is sufficient.

## Correctness Properties

Property 1: Bug Condition - Non-String Key Types Generate Compilable Prefix Code

_For any_ key property where the type is not `string` and a prefix is configured (isBugCondition returns true), the fixed `GenerateKeyPrefixApplication` SHALL emit code that converts the property value to a string expression (via `KeysGenerator.GetValueExpression`) before passing it to `ApplyKeyPrefix`, producing compilable C# output.

**Validates: Requirements 2.1, 2.2, 2.3, 2.4**

Property 2: Preservation - String Key Properties Unchanged

_For any_ key property where the type IS `string` and a prefix is configured (isBugCondition returns false), the fixed code SHALL produce the same generated output as the original code, passing `typedEntity.{PropertyName}` directly to `ApplyKeyPrefix` without any conversion wrapper.

**Validates: Requirements 3.1, 3.2, 3.3**

## Fix Implementation

### Changes Required

**File**: `Oproto.FluentDynamoDb.SourceGenerator/Generators/KeysGenerator.cs`

**Method**: `GetValueExpression`

**Specific Changes**:
1. **Change visibility from `private static` to `internal static`**: This allows `MapperGenerator` (in the same assembly) to call `GetValueExpression` directly. No signature or logic change needed.

---

**File**: `Oproto.FluentDynamoDb.SourceGenerator/Generators/MapperGenerator.cs`

**Method**: `GenerateKeyPrefixApplication`

**Specific Changes**:
1. **Call `KeysGenerator.GetValueExpression` to produce the string expression**: Replace the direct `typedEntity.{escapedPropertyName}` usage with the result of `KeysGenerator.GetValueExpression($"typedEntity.{escapedPropertyName}", property.PropertyType)`.

2. **Updated emission code**: The loop body becomes:
   ```csharp
   var valueExpr = KeysGenerator.GetValueExpression($"typedEntity.{escapedPropertyName}", property.PropertyType);
   sb.AppendLine($"                ArgumentNullException.ThrowIfNull(typedEntity.{escapedPropertyName}, nameof(typedEntity.{escapedPropertyName}));");
   sb.AppendLine($"                item[\"{attributeName}\"] = new AttributeValue {{ S = Oproto.FluentDynamoDb.Utility.KeyPrefixHelper.ApplyKeyPrefix({valueExpr}, \"{prefix}\", \"{separator}\", resolvedMode) }};");
   ```

3. **String type behavior is preserved**: For string properties, `GetValueExpression` returns the parameter name unchanged (e.g., `typedEntity.UserId`), so the generated output is identical to before.

## Testing Strategy

### Validation Approach

The testing strategy follows a two-phase approach: first, surface counterexamples that demonstrate the bug on unfixed code, then verify the fix works correctly and preserves existing behavior.

### Exploratory Bug Condition Checking

**Goal**: Surface counterexamples that demonstrate the bug BEFORE implementing the fix. Confirm the root cause by observing that the source generator emits uncompilable code for non-string key types with prefixes.

**Test Plan**: Create entity definitions with non-string key types and prefixes, run the source generator, and inspect the generated code for type mismatches in the `ApplyKeyPrefix` call.

**Test Cases**:
1. **Enum Key Test**: Entity with `[SortKey(Prefix = "TOPIC")] public SnsSubscriptionTopic Topic` — generated code should fail to compile (will fail on unfixed code)
2. **DateTime Key Test**: Entity with `[SortKey(Prefix = "DATE")] public DateTime CreatedAt` — generated code passes DateTime directly (will fail on unfixed code)
3. **Guid Key Test**: Entity with `[PartitionKey(Prefix = "ID")] public Guid EntityId` — generated code passes Guid directly (will fail on unfixed code)
4. **Numeric Key Test**: Entity with `[SortKey(Prefix = "NUM")] public int Sequence` — generated code passes int directly (will fail on unfixed code)

**Expected Counterexamples**:
- Generated code contains `ApplyKeyPrefix(typedEntity.Topic, ...)` instead of `ApplyKeyPrefix(typedEntity.Topic.ToString(), ...)`
- Root cause confirmed: `GenerateKeyPrefixApplication` does not call any type conversion logic

### Fix Checking

**Goal**: Verify that for all inputs where the bug condition holds, the fixed generator produces correct string conversion expressions.

**Pseudocode:**
```
FOR ALL property WHERE isBugCondition(property) DO
  result := GenerateKeyPrefixApplication_fixed(property)
  ASSERT result CONTAINS GetValueExpression(property) call to ApplyKeyPrefix
  ASSERT generatedCode COMPILES SUCCESSFULLY
END FOR
```

### Preservation Checking

**Goal**: Verify that for all inputs where the bug condition does NOT hold, the fixed generator produces the same output as before.

**Pseudocode:**
```
FOR ALL property WHERE NOT isBugCondition(property) DO
  ASSERT GenerateKeyPrefixApplication_original(property) = GenerateKeyPrefixApplication_fixed(property)
END FOR
```

**Testing Approach**: Property-based testing is recommended for preservation checking because:
- It generates many combinations of property types and prefix configurations
- It catches edge cases in type detection logic
- It provides strong guarantees that string-typed properties remain unaffected

**Test Plan**: Observe generated code on UNFIXED code for string key properties with prefixes, then write tests asserting the generated output is byte-for-byte identical after the fix.

**Test Cases**:
1. **String Key Preservation**: Verify `[PartitionKey(Prefix = "USER")] public string UserId` generates identical code before and after fix
2. **No-Prefix Key Preservation**: Verify `[PartitionKey] public Guid Id` (no prefix) does not enter `GenerateKeyPrefixApplication` at all
3. **Computed Key Preservation**: Verify computed keys with non-string source properties continue using their existing path
4. **Constant Key Preservation**: Verify constant keys are still excluded from prefix application

### Unit Tests

- Test `KeysGenerator.GetValueExpression` accessibility (now `internal static`, callable from other classes in the assembly)
- Test generated output for each non-string type: enum → `.ToString()`, DateTime → `.ToString("yyyy-MM-ddTHH:mm:ss.fffZ")`, Guid → `.ToString()`, numeric → `.ToString()`
- Test that string type still returns the parameter name unchanged
- Test that nullable types are handled correctly (current behavior returns parameter name for nullable)

### Property-Based Tests

- Generate random `PropertyModel` instances with various `PropertyType` values and prefix configurations, verify that `GetValueExpression` produces a valid string expression for every type
- Generate entities with mixed key types (some string, some non-string) and verify only non-string keys get conversion wrappers
- Verify that for all string-typed key properties, the generated code output is identical to the unfixed version

### Integration Tests

- Full source generator integration: define an entity with an enum sort key and prefix, run the generator, compile the output, and verify the generated `ToDynamoDb` method produces the correct `AttributeValue` at runtime
- Multi-key entity: entity with string PK (prefix) and enum SK (prefix), verify both are generated correctly
- Verify `KeyInputMode.RawValue` and `KeyInputMode.Default` both work correctly with the converted value
