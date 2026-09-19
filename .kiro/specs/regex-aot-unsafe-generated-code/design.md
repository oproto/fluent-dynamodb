# Regex AOT-Unsafe Generated Code Bugfix Design

## Overview

The source generator (`MapperGenerator.cs`) emits `System.Text.RegularExpressions.Regex.IsMatch(sortKey, @"...")` calls into generated code for `[RelatedEntity]` composite entity pattern matching. This breaks Native AOT deployment because the interpreted regex engine relies on runtime code paths that may be trimmed. The `[GeneratedRegex]` AOT-safe alternative cannot be used because source generators cannot chain off each other's output in the same compilation pass.

The fix replaces the `ConvertWildcardPatternToRegex` helper and all five emission sites with a new `EmitWildcardPatternMatch` helper that emits plain `string.Split` + segment-count + literal-segment equality checks directly into generated code. The wildcard patterns are fully known at compile time and decompose into literal segments separated by a delimiter (typically `#`), where each `*` matches exactly one segment. This approach is both AOT-safe and faster than regex.

## Glossary

- **Bug_Condition (C)**: The source generator emits `Regex.IsMatch` calls into generated code for wildcard pattern matching in composite entity scenarios
- **Property (P)**: The source generator should emit equivalent `string.Split` + segment comparison checks that are AOT-safe and produce identical match/reject results
- **Preservation**: All existing pattern matching behavior (match acceptance, match rejection, exact-match patterns, custom delimiters) must remain identical after the fix
- **`ConvertWildcardPatternToRegex`**: The existing helper in `MapperGenerator.cs` (~line 5843) that converts a wildcard pattern like `"INVOICE#*#LINE#*"` into a regex pattern like `^INVOICE\#[^#]*\#LINE\#[^#]*$`. To be replaced.
- **`InferDelimiterFromPattern`**: Existing helper (~line 5864) that looks at the character before the first `*` to determine the delimiter. Will be reused by the new helper.
- **Emission site**: A location in the source generator where `sb.AppendLine(...)` writes `Regex.IsMatch` into generated code
- **Wildcard segment**: A `*` in a pattern like `"INVOICE#*#LINE#*"` that matches any characters except the delimiter within a single segment

## Bug Details

### Bug Condition

The bug manifests when a `[RelatedEntity]` attribute with a wildcard pattern is defined on an entity and the generated code is compiled for Native AOT or with trimming enabled. The source generator emits `System.Text.RegularExpressions.Regex.IsMatch(sortKey, @"...")` into five locations in generated code, creating a runtime dependency on the interpreted regex engine which is not AOT-safe.

**Formal Specification:**
```
FUNCTION isBugCondition(input)
  INPUT: input of type GeneratedCodeOutput
  OUTPUT: boolean
  
  RETURN input.entityHasRelatedEntityAttribute == true
         AND input.relatedEntityPattern.contains("*")
         AND input.generatedCode.contains("System.Text.RegularExpressions.Regex.IsMatch")
END FUNCTION
```

### Examples

- **Pattern `"INVOICE#*#LINE#*"`**: Generator emits `Regex.IsMatch(sortKey, @"^INVOICE\#[^#]*\#LINE\#[^#]*$")` — fails under AOT trimming because `System.Text.RegularExpressions` runtime paths are removed
- **Pattern `"audit#*"`**: Generator emits `Regex.IsMatch(sortKey, @"^audit\#[^#]*$")` — same AOT failure for single-wildcard patterns
- **Pattern `"TYPE_*_SUB_*"` (underscore delimiter)**: Generator emits `Regex.IsMatch(sortKey, @"^TYPE\_[^\_]*\_SUB\_[^\_]*$")` — same AOT failure with custom delimiters
- **Pattern `"summary"` (no wildcards)**: Generator emits exact-match `sortKey == "summary" || sortKey.StartsWith("summary#")` — NOT affected, no bug condition

### Affected Emission Sites

Five sites in `MapperGenerator.cs` emit `Regex.IsMatch`, plus the `ConvertWildcardPatternToRegex` helper:

| # | Method | Purpose | Line | Context |
|---|--------|---------|------|---------|
| 1 | `GenerateAsyncPrimaryEntityIdentification` | Regex exclusion of related patterns (negation: `!Regex.IsMatch`) | ~2452 | Sets `isPrimaryEntity = false` when sort key matches a related pattern |
| 2 | `GenerateRelatedEntityCollectionMappingAsync` | Pattern matching for async collections | ~2674 | Maps items to related entity collections in async path |
| 3 | `GenerateRelatedEntitySingleMappingAsync` | Pattern matching for async single entities | ~2788 | Maps items to single related entities in async path |
| 4 | `GeneratePrimaryEntityPatternMatching` | Sync primary entity identification (negation) | ~4277 | Sets `isPrimaryEntity = false` in sync path |
| 5 | `GenerateSortKeyPatternMatching` | Shared helper for sync collection and single mapping | ~5829 | Used by sync related entity mapping methods |
| 6 | `ConvertWildcardPatternToRegex` | Constructs regex patterns from wildcard patterns | ~5843 | Called by all 5 emission sites above |

## Expected Behavior

### Preservation Requirements

**Unchanged Behaviors:**
- Pattern `"INVOICE#*#LINE#*"` must continue to match `"INVOICE#INV-001#LINE#1"` and reject `"ORDER#123"`, `"INVOICE#INV-001"`, and `"INVOICE#INV-001#LINE#1#EXTRA"`
- Pattern `"audit#*"` must continue to match `"audit#entry1"` and reject `"audit#entry1#sub"` (wildcard matches single segment only)
- Pattern `"INVOICE#*"` must continue to match `"INVOICE#"` (empty segment after delimiter)
- Exact-match patterns (no wildcards) must continue to use existing `sortKey == pattern || sortKey.StartsWith(pattern + delimiter)` logic with zero changes
- Primary entity identification (negation sites 1 and 4) must continue to correctly exclude related entity items and identify the primary entity
- Custom delimiters (`_`, `:`, `|`) must continue to be inferred and handled correctly
- Sync and async code paths must continue to produce identical entity mapping results
- Special regex characters in literal segments (e.g., `"TYPE.NAME#*"`) must continue to be handled correctly (the dot matches only a literal dot, not any character)
- All existing `ToCompositeEntityAsync` and `ToCompositeEntityListAsync` behavior must remain unchanged

**Scope:**
All inputs that do NOT involve `[RelatedEntity]` wildcard pattern matching should be completely unaffected by this fix. This includes:
- Standard Get, Put, Update, Delete operations
- Query and Scan operations without composite entities
- Entity serialization/deserialization (ToDynamoDb/FromDynamoDb for single items)
- Index operations
- Transaction and batch operations
- Exact-match `[RelatedEntity]` patterns

## Hypothesized Root Cause

The root cause is straightforward: when the source generator was originally implemented, `Regex.IsMatch` was the simplest way to emit pattern matching logic. The generator calls `ConvertWildcardPatternToRegex` at compile time to build a regex string literal, then emits that literal into generated code as a `Regex.IsMatch` call. This creates a runtime dependency on `System.Text.RegularExpressions` in the consuming project's compiled output.

The AOT-safe alternative `[GeneratedRegex]` cannot be used because:
1. `[GeneratedRegex]` is itself a source generator
2. Source generators cannot chain — one generator's output cannot be consumed by another in the same compilation pass
3. The generated code would need to declare `[GeneratedRegex]` attributes, which the Roslyn compiler cannot process in time

The fix is to replace the regex approach entirely with plain string operations that the generator emits directly. The wildcard patterns are simple enough (literal segments + single-segment wildcards) that `string.Split` + length check + literal equality is semantically equivalent and requires no `System.Text.RegularExpressions` at runtime.

## Correctness Properties

Property 1: Bug Condition - AOT-Safe Generated Code

_For any_ entity with `[RelatedEntity]` attributes containing wildcard patterns, the source generator SHALL emit pattern matching code that uses only `string.Split`, `string.Length`, and `string` equality comparisons — with zero references to `System.Text.RegularExpressions.Regex` in the generated output.

**Validates: Requirements 2.1, 2.2, 2.3, 2.4**

Property 2: Preservation - Pattern Matching Equivalence

_For any_ sort key string and any wildcard pattern, the new `string.Split`-based matching logic SHALL produce the same boolean result (match or reject) as the previous `Regex.IsMatch`-based logic, preserving all existing composite entity assembly behavior including primary entity identification, collection mapping, and single entity mapping.

**Validates: Requirements 3.1, 3.2, 3.3, 3.4, 3.5**

## Fix Implementation

### Changes Required

Assuming our root cause analysis is correct:

**File**: `Oproto.FluentDynamoDb.SourceGenerator/Generators/MapperGenerator.cs`

**Approach**: Replace `ConvertWildcardPatternToRegex` with a new `EmitWildcardPatternMatch` helper, and update all five emission sites to call it.

**Specific Changes**:

1. **New helper method — `EmitWildcardPatternMatch`**: Create a new method that, given a `StringBuilder`, an indentation string, a wildcard pattern, and the sort key variable name, emits the equivalent `string.Split` + segment comparison code. This method decomposes the pattern at compile time (in the generator) and emits the checks as plain string operations.

   The helper will:
   - Call `InferDelimiterFromPattern` to determine the delimiter (reuse existing helper)
   - Split the pattern by the delimiter to get segments
   - Count total segments and identify which are literal vs wildcard (`*`)
   - Emit a unique local variable name (e.g., `_segments_{patternIndex}`) to avoid collisions when multiple patterns are checked in sequence
   - Emit: `var _segments_N = sortKey.Split('{delimiter}');`
   - Emit: `if (_segments_N.Length == {expectedCount} && _segments_N[0] == "{literal0}" && _segments_N[2] == "{literal2}" ...)`

   Example output for pattern `"INVOICE#*#LINE#*"`:
   ```csharp
   var _seg = sortKey.Split('#');
   if (_seg.Length == 4 && _seg[0] == "INVOICE" && _seg[2] == "LINE")
   ```

2. **Variable naming for collision avoidance**: When multiple patterns are checked in sequence (sites 1 and 4 loop over `relatedPatterns`), each pattern needs a unique variable name. The helper will accept a `patternIndex` parameter and use it to generate unique names like `_seg0`, `_seg1`, etc. Alternatively, each pattern check can be wrapped in a block scope `{ ... }` so that `_seg` can be reused.

   **Decision**: Use block scoping. Each emission wraps the split + check in `{ }` so the same variable name `_seg` is reused safely. This is simpler and matches how the existing code already uses block-level scoping in the generated output.

3. **Negation handling (sites 1 and 4)**: The primary entity identification sites use `if (Regex.IsMatch(sortKey, pattern)) { isPrimaryEntity = false; }`. The new code will use the same `if` structure — only the condition expression changes from `Regex.IsMatch(...)` to the split-based check. Since the check is multi-statement (declare variable, then test), the emitted code will use a block:
   ```csharp
   {
       var _seg = sortKey.Split('#');
       if (_seg.Length == 4 && _seg[0] == "INVOICE" && _seg[2] == "LINE")
       {
           isPrimaryEntity = false;
       }
   }
   ```

4. **Update emission site 1 — `GenerateAsyncPrimaryEntityIdentification` (~line 2452)**: Replace the `ConvertWildcardPatternToRegex` + `Regex.IsMatch` emission with a call to `EmitWildcardPatternMatch`. The loop over `relatedPatterns` already provides a natural index for variable naming if needed.

5. **Update emission site 2 — `GenerateRelatedEntityCollectionMappingAsync` (~line 2674)**: Replace the wildcard branch's `Regex.IsMatch` emission with `EmitWildcardPatternMatch`. The exact-match `else` branch remains untouched.

6. **Update emission site 3 — `GenerateRelatedEntitySingleMappingAsync` (~line 2788)**: Same change as site 2.

7. **Update emission site 4 — `GeneratePrimaryEntityPatternMatching` (~line 4277)**: Same change as site 1 (sync version).

8. **Update emission site 5 — `GenerateSortKeyPatternMatching` (~line 5829)**: Replace the wildcard branch's `Regex.IsMatch` emission with `EmitWildcardPatternMatch`. The exact-match `else` branch remains untouched.

9. **Retain `ConvertWildcardPatternToRegex` as `internal`**: The method is currently `internal static` and is used by test projects (`WildcardPatternMatchingTests.cs`, `WildcardPatternMatchingPropertyTests.cs`, `PrimaryEntityIdentificationPropertyTests.cs`) for property-based testing of the pattern matching logic. Keep it for backward compatibility in tests, but it is no longer called by any emission site. Optionally mark it with `[Obsolete]` to signal it's no longer used in code generation.

10. **Retain `InferDelimiterFromPattern`**: This helper is reused by the new `EmitWildcardPatternMatch` method.

11. **New helper method — `MatchesWildcardPattern` (runtime utility)**: Create a new `internal static bool MatchesWildcardPattern(string sortKey, string pattern)` method on `MapperGenerator` that performs the same string-split logic at runtime (in the generator process or test code). This allows existing property-based tests to validate the new logic against the old regex logic without running the source generator. The test can call both `Regex.IsMatch(sortKey, ConvertWildcardPatternToRegex(pattern))` and `MatchesWildcardPattern(sortKey, pattern)` and assert they agree.

## Testing Strategy

### Validation Approach

The testing strategy follows a two-phase approach: first, surface counterexamples that demonstrate the bug on unfixed code, then verify the fix works correctly and preserves existing behavior.

### Exploratory Bug Condition Checking

**Goal**: Surface counterexamples that demonstrate the bug BEFORE implementing the fix. Confirm that `Regex.IsMatch` is present in generated code output.

**Test Plan**: Write tests that run the source generator on an entity with `[RelatedEntity]` wildcard patterns and inspect the generated code string for `Regex.IsMatch`. Run these tests on the UNFIXED code to observe that the generated code contains regex calls.

**Test Cases**:
1. **Single wildcard pattern test**: Generate code for entity with `[RelatedEntity("audit#*")]` and assert generated code contains `Regex.IsMatch` (will pass on unfixed code, demonstrating the bug)
2. **Multi-wildcard pattern test**: Generate code for entity with `[RelatedEntity("INVOICE#*#LINE#*")]` and assert generated code contains `Regex.IsMatch` (will pass on unfixed code)
3. **Multiple related entities test**: Generate code for entity with multiple `[RelatedEntity]` attributes and assert all pattern checks use `Regex.IsMatch` (will pass on unfixed code)
4. **Custom delimiter test**: Generate code for entity with `[RelatedEntity("TYPE_*_SUB_*")]` and assert generated code contains `Regex.IsMatch` (will pass on unfixed code)

**Expected Counterexamples**:
- Generated code strings contain `System.Text.RegularExpressions.Regex.IsMatch` which is the AOT-unsafe pattern
- All five emission sites produce regex-based pattern matching

### Fix Checking

**Goal**: Verify that for all inputs where the bug condition holds, the fixed function produces the expected behavior.

**Pseudocode:**
```
FOR ALL entity WHERE entity.hasRelatedEntityWithWildcard DO
  generatedCode := runSourceGenerator(entity)
  ASSERT NOT generatedCode.contains("System.Text.RegularExpressions.Regex")
  ASSERT generatedCode.contains("Split")
  ASSERT generatedCode.compiles()
END FOR
```

### Preservation Checking

**Goal**: Verify that for all inputs where the bug condition does NOT hold, the fixed function produces the same result as the original function.

**Pseudocode:**
```
FOR ALL (sortKey, pattern) WHERE pattern.contains("*") DO
  regexResult := Regex.IsMatch(sortKey, ConvertWildcardPatternToRegex(pattern))
  splitResult := MatchesWildcardPattern(sortKey, pattern)
  ASSERT regexResult == splitResult
END FOR
```

**Testing Approach**: Property-based testing is recommended for preservation checking because:
- It generates many sort key + pattern combinations automatically
- It catches edge cases (empty segments, special characters, custom delimiters)
- It provides strong guarantees that the new string-based matching is equivalent to the old regex-based matching

**Test Plan**: Use `MatchesWildcardPattern` as the oracle function and compare its output against `Regex.IsMatch(sortKey, ConvertWildcardPatternToRegex(pattern))` across many generated inputs.

**Test Cases**:
1. **Regex-equivalence for matching sort keys**: For random patterns and matching sort keys, assert `MatchesWildcardPattern` returns `true` when `Regex.IsMatch` returns `true`
2. **Regex-equivalence for non-matching sort keys**: For random patterns and non-matching sort keys, assert `MatchesWildcardPattern` returns `false` when `Regex.IsMatch` returns `false`
3. **Edge case: empty segments**: Sort keys like `"INVOICE##LINE#1"` should be handled identically by both approaches
4. **Edge case: special characters in literal segments**: Patterns like `"TYPE.NAME#*"` should match `"TYPE.NAME#value"` and reject `"TYPExNAME#value"` under both approaches

### Unit Tests

- Test `EmitWildcardPatternMatch` output for various patterns and verify the emitted code structure
- Test `MatchesWildcardPattern` for known match/reject cases mirroring existing `WildcardPatternMatchingTests`
- Test that generated code for entities with `[RelatedEntity]` no longer contains `System.Text.RegularExpressions`
- Test that generated code for entities with `[RelatedEntity]` compiles successfully
- Update existing `MapperGeneratorTests.GenerateEntityImplementation_WithRelatedEntities_GeneratesRelationshipMapping` to assert `Split` instead of `Regex.IsMatch`

### Property-Based Tests

- Generate random sort keys and wildcard patterns to verify `MatchesWildcardPattern` agrees with `Regex.IsMatch`+`ConvertWildcardPatternToRegex` on all inputs
- Generate random entity configurations with `[RelatedEntity]` and verify generated code never contains `Regex.IsMatch`
- Test delimiter inference across many patterns to verify `InferDelimiterFromPattern` works correctly with the new approach

### Integration Tests

- Run full source generator on composite entity definitions and verify the generated code compiles
- Verify `ToCompositeEntityAsync` and `ToCompositeEntityListAsync` continue to work correctly with the new pattern matching
- Test that the AOT test project (`Oproto.FluentDynamoDb.AotTests`) passes after adding `[RelatedEntity]` coverage
