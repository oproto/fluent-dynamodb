# Typed Overload Single Source Property Bugfix Design

## Overview

The source generator incorrectly gates typed parameter overload eligibility on `ComputedKey.SourceProperties.Length >= 2`. This prevents entities with a single non-string computed source property (e.g., `DateTime`) from receiving typed overloads, even though such overloads are clearly non-ambiguous with the standard `(string)` overload. The fix removes the `>= 2` gate from both `QualifiesForTypedOverload` and `GetTypedOverloadParameters`, relying on the existing `WouldBeAmbiguous` method to suppress truly ambiguous cases (single `string` source → same signature as standard overload).

## Glossary

- **Bug_Condition (C)**: An entity has at least one computed key with exactly one source property — the `>= 2` gate prevents typed overload generation regardless of the source property's type
- **Property (P)**: After the fix, `QualifiesForTypedOverload` returns `true` for any entity with a computed key (regardless of source count), and `GetTypedOverloadParameters` resolves source properties for single-source computed keys
- **Preservation**: Entities with 2+ source computed keys, non-computed entities, and the `WouldBeAmbiguous` / `QualifiesForKeyInputMode` logic must behave identically before and after the fix
- **`QualifiesForTypedOverload`**: Method in `ComputedOverloadEligibility.cs` that determines if an entity is a candidate for typed parameter overloads
- **`GetTypedOverloadParameters`**: Method in `OverloadParameterResolver.cs` that resolves the typed parameter list for overload generation
- **`WouldBeAmbiguous`**: Method in `ComputedOverloadEligibility.cs` that compares typed overload parameter types/counts against the standard overload to detect signature collisions
- **`QualifiesForKeyInputMode`**: Method that falls through to prefix-based eligibility when no non-ambiguous typed overload exists

## Bug Details

### Bug Condition

The bug manifests when a computed key has exactly one source property. Both `QualifiesForTypedOverload` and `GetTypedOverloadParameters` require `SourceProperties.Length >= 2`, causing single-source computed keys to be rejected before `WouldBeAmbiguous` is ever consulted.

**Formal Specification:**
```
FUNCTION isBugCondition(input)
  INPUT: input of type EntityModel
  OUTPUT: boolean
  
  pk ← input.PartitionKeyProperty
  sk ← input.SortKeyProperty
  
  pkSingleSource ← (pk IS NOT NULL) AND (pk.IsComputed = true) AND (pk.ComputedKey.SourceProperties.Length = 1)
  skSingleSource ← (sk IS NOT NULL) AND (sk.IsComputed = true) AND (sk.ComputedKey.SourceProperties.Length = 1)
  
  RETURN pkSingleSource OR skSingleSource
END FUNCTION
```

### Examples

- **Single `DateTime` SK source**: Entity with `[Computed(nameof(CreationDateTime), Format = "ORDER#{0:o}")]` — typed overload `Get(string pK, DateTime creationDateTime)` should be generated but is not. Standard overload is `Get(string pK, string sK)` — different types, not ambiguous.
- **Single `int` PK source**: Entity with `[Computed(nameof(Year))]` on PK — typed overload `Get(int year)` should be generated but is not. Standard overload is `Get(string pK)` — different type, not ambiguous.
- **Single `Guid` SK source**: Entity with `[Computed(nameof(CorrelationId))]` on SK — typed overload `Get(string pK, Guid correlationId)` should be generated. Standard is `Get(string pK, string sK)` — different type at position 2.
- **Single `string` SK source (ambiguous)**: Entity with `[Computed(nameof(Label))]` on SK where `Label` is `string` — typed overload would be `Get(string pK, string label)` which collides with `Get(string pK, string sK)`. `WouldBeAmbiguous` correctly suppresses this — no overload generated. Correct outcome.

## Expected Behavior

### Preservation Requirements

**Unchanged Behaviors:**
- Entities with 2+ source computed keys continue to generate typed overloads as before
- Entities with 2+ source computed keys where all sources are `string` continue to be suppressed by `WouldBeAmbiguous`
- Entities with no computed keys continue to skip typed overload generation
- `QualifiesForKeyInputMode` continues to fall through to prefix-based evaluation when typed overload is ambiguous
- `QualifiesForKeyInputMode` continues to return `false` when a non-ambiguous typed overload exists
- `WouldBeAmbiguous` logic is untouched — only the gating checks change

**Scope:**
All inputs where `isBugCondition` returns `false` (entities with 0 or 2+ source computed keys, or entities with no computed keys at all) should produce identical results from `QualifiesForTypedOverload`, `WouldBeAmbiguous`, `GetTypedOverloadParameters`, and `QualifiesForKeyInputMode`.

## Hypothesized Root Cause

Based on the code analysis, the root cause is clear and confirmed:

1. **Overly Restrictive Gate in `QualifiesForTypedOverload`**: The condition `pk.ComputedKey!.SourceProperties.Length >= 2` was added as a conservative heuristic to avoid generating overloads that would collide with the standard `(string)` overload. However, this heuristic is incorrect — a single `DateTime` source produces a different type than `string`, so no collision occurs.

2. **Matching Gate in `GetTypedOverloadParameters`**: The same `>= 2` check exists in `GetTypedOverloadParameters`, causing single-source computed keys to fall through to the plain `string` parameter branch instead of resolving the actual source property type.

3. **Redundant Safety Check**: The `WouldBeAmbiguous` method already performs the correct type-based comparison. The `>= 2` gate was a premature optimization that prevented `WouldBeAmbiguous` from being reached for single-source cases.

## Correctness Properties

Property 1: Bug Condition - Single Source Computed Key Generates Typed Overload

_For any_ EntityModel where at least one key is computed with exactly one source property AND the source property type differs from `string`, the fixed `QualifiesForTypedOverload` SHALL return `true` and `GetTypedOverloadParameters` SHALL resolve the source property to its declared type, resulting in a non-ambiguous typed overload being generated.

**Validates: Requirements 2.1, 2.3**

Property 2: Preservation - Multi-Source and Non-Computed Entity Behavior Unchanged

_For any_ EntityModel where no key is computed with exactly one source property (either 0 or 2+ sources), the fixed functions SHALL produce identical results to the original functions: `QualifiesForTypedOverload'(X) = QualifiesForTypedOverload(X)`, `WouldBeAmbiguous'(X) = WouldBeAmbiguous(X)`, and `QualifiesForKeyInputMode'(X) = QualifiesForKeyInputMode(X)`.

**Validates: Requirements 3.1, 3.2, 3.3, 3.4, 3.5**

## Fix Implementation

### Changes Required

The root cause is confirmed. Both gates need relaxation:

**File**: `Oproto.FluentDynamoDb.SourceGenerator/Generators/ComputedOverloadEligibility.cs`

**Function**: `QualifiesForTypedOverload`

**Specific Changes**:
1. **Remove `>= 2` from PK check**: Change `pk?.IsComputed == true && pk.ComputedKey!.SourceProperties.Length >= 2` to `pk?.IsComputed == true`
2. **Remove `>= 2` from SK check**: Change `sk?.IsComputed == true && sk.ComputedKey!.SourceProperties.Length >= 2` to `sk?.IsComputed == true`
3. **Update XML doc comment**: Remove mention of "SourceProperties.Length >= 2" from the summary

---

**File**: `Oproto.FluentDynamoDb.SourceGenerator/Generators/OverloadParameterResolver.cs`

**Function**: `GetTypedOverloadParameters`

**Specific Changes**:
4. **Remove `>= 2` from PK branch**: Change `if (pk?.IsComputed == true && pk.ComputedKey!.SourceProperties.Length >= 2)` to `if (pk?.IsComputed == true)`
5. **Remove `>= 2` from SK branch**: Change `if (sk?.IsComputed == true && sk.ComputedKey!.SourceProperties.Length >= 2)` to `if (sk?.IsComputed == true)`
6. **Update XML doc comment**: Change "For computed keys with 2+ source properties" to "For computed keys" in the summary

---

**File**: `Oproto.FluentDynamoDb.SourceGenerator.UnitTests/Generators/ComputedOverloadEligibilityPropertyTests.cs`

**Specific Changes**:
7. **Update test generators**: Tests that generate entities with single-source computed keys as "non-qualifying" scenarios need to be re-evaluated — single-source entities now qualify (unless ambiguous)
8. **Update assertions**: The `NonComputedEntities_DoNotQualifyForTypedOverloads` property test needs its generator updated to exclude single-source computed entities (they now qualify)

---

**File**: `Oproto.FluentDynamoDb.SourceGenerator.UnitTests/Generators/ComputedOverloadPropertyTests.cs`

**Specific Changes**:
9. **Update `BuildExpectedParams` helper**: The helper uses `>= 2` to decide whether to expand source properties; this must be updated to match the new logic (any `IsComputed` key expands)
10. **Add single-source generators**: Add new generators that produce entities with exactly one computed source property to verify typed overload generation

## Testing Strategy

### Validation Approach

The testing strategy follows a two-phase approach: first, surface counterexamples that demonstrate the bug on unfixed code (exploratory checking), then verify the fix works correctly (fix checking) and preserves existing behavior (preservation checking).

### Exploratory Bug Condition Checking

**Goal**: Surface counterexamples that demonstrate the bug BEFORE implementing the fix. Confirm the root cause analysis.

**Test Plan**: Write property-based tests that generate EntityModels with single-source non-string computed keys and assert that `QualifiesForTypedOverload` returns `true` and typed overloads are generated. Run on UNFIXED code to observe failures.

**Test Cases**:
1. **Single DateTime SK Source**: Generate entity with `[Computed(nameof(CreationDateTime))]` on SK where `CreationDateTime` is `DateTime` — assert `QualifiesForTypedOverload` returns `true` (will fail on unfixed code)
2. **Single int PK Source**: Generate entity with single `int` source on PK — assert `QualifiesForTypedOverload` returns `true` (will fail on unfixed code)
3. **Single Guid SK Source**: Generate entity with single `Guid` source on SK — assert typed overload parameters resolve correctly (will fail on unfixed code)
4. **Single string SK Source (ambiguous)**: Generate entity with single `string` source on SK — assert `QualifiesForTypedOverload` returns `true` but `WouldBeAmbiguous` returns `true` (will fail on first assertion on unfixed code)

**Expected Counterexamples**:
- `QualifiesForTypedOverload` returns `false` for all single-source computed entities
- `GetTypedOverloadParameters` falls through to plain `string` parameter for single-source keys
- Root cause confirmed: the `>= 2` gate short-circuits before type comparison

### Fix Checking

**Goal**: Verify that for all inputs where the bug condition holds, the fixed functions produce the expected behavior.

**Pseudocode:**
```
FOR ALL input WHERE isBugCondition(input) DO
  result ← QualifiesForTypedOverload'(input)
  ASSERT result = true
  
  typedParams ← GetTypedOverloadParameters'(input)
  ASSERT typedParams IS NOT NULL
  ASSERT typedParams contains resolved source property types (not fallback "string")
  
  IF NOT WouldBeAmbiguous(input) THEN
    ASSERT generated code contains typed overload method
  ELSE
    ASSERT generated code does NOT contain typed overload method
  END IF
END FOR
```

### Preservation Checking

**Goal**: Verify that for all inputs where the bug condition does NOT hold, the fixed functions produce the same result as the original functions.

**Pseudocode:**
```
FOR ALL input WHERE NOT isBugCondition(input) DO
  ASSERT QualifiesForTypedOverload(input) = QualifiesForTypedOverload'(input)
  ASSERT WouldBeAmbiguous(input) = WouldBeAmbiguous'(input)
  ASSERT QualifiesForKeyInputMode(input) = QualifiesForKeyInputMode'(input)
  ASSERT GetTypedOverloadParameters(input) = GetTypedOverloadParameters'(input)
END FOR
```

**Testing Approach**: Property-based testing with FsCheck is used for preservation checking because:
- It generates many EntityModel configurations automatically across the input domain
- It catches edge cases (0-source computed keys, entities with no keys, mixed configurations)
- It provides strong guarantees that behavior is unchanged for all non-buggy inputs

**Test Plan**: Capture original function behavior for non-bug-condition entities, then write property-based tests asserting fixed functions produce identical results.

**Test Cases**:
1. **Multi-Source Computed Key Preservation**: Verify entities with 2+ source computed keys produce identical `QualifiesForTypedOverload`, `WouldBeAmbiguous`, and `GetTypedOverloadParameters` results
2. **Non-Computed Entity Preservation**: Verify entities with no computed keys produce identical results from all eligibility functions
3. **KeyInputMode Preservation**: Verify `QualifiesForKeyInputMode` returns identical results for entities outside the bug condition
4. **Generated Code Preservation**: Verify generated table class code is identical for entities with 2+ source computed keys

### Unit Tests

- Test `QualifiesForTypedOverload` returns `true` for single `DateTime` source on SK
- Test `QualifiesForTypedOverload` returns `true` for single `int` source on PK
- Test `WouldBeAmbiguous` returns `true` for single `string` source (collides with standard overload)
- Test `WouldBeAmbiguous` returns `false` for single `DateTime` source (different type)
- Test `GetTypedOverloadParameters` resolves single `DateTime` source to `DateTime` parameter (not fallback `string`)
- Test `GetTypedOverloadParameters` resolves single `int` source to `int` parameter
- Test end-to-end: entity with single `DateTime` SK source generates `Get(string pK, DateTime creationDateTime)` overload

### Property-Based Tests

- Generate random EntityModels with single non-string computed source properties and verify typed overloads are generated with correct parameter types
- Generate random EntityModels with single string computed source properties and verify typed overloads are suppressed by `WouldBeAmbiguous`
- Generate random EntityModels with 2+ source properties and verify behavior is unchanged from current implementation
- Generate random non-computed EntityModels and verify no typed overloads are generated

### Integration Tests

- Full source generator integration: compile entity class with single `DateTime` computed SK and verify generated output contains typed Get/Delete/Update + async overloads
- Full source generator integration: compile entity class with single `string` computed SK and verify no typed overload appears in output
- Verify `QualifiesForKeyInputMode` still works correctly for prefix-eligible entities that have ambiguous single-source typed overloads
