# Compound Discrimination Complex Pattern Fix — Bugfix Design

## Overview

The `GetEffectiveCrossKeyPattern` method in `CompoundPromotionPass.cs` currently returns `null` for any cross-key pattern classified as `Complex` (2+ wildcards). This prevents entities with multi-wildcard PK patterns like `TENANT#*#ROLE#*` from participating in compound promotion, even when the pattern's leading prefix (e.g., `TENANT#`) could distinguish it from another entity's PK pattern (e.g., `SERVICE#*`).

The fix modifies `GetEffectiveCrossKeyPattern` to extract the leading prefix from Complex patterns — the text before the first `*` character — and construct a synthetic `StartsWith` pattern (`prefix + "*"`) when the prefix is non-empty. This allows compound promotion to proceed using the prefix as a discriminator. If the prefix is empty (pattern starts with `*`), the method continues returning `null`.

This is a minimal, targeted change: `DiscriminatorAnalyzer.DeterminePatternStrategy` is not modified. The fix operates entirely within `GetEffectiveCrossKeyPattern`, and the downstream `AssignPositiveConstraint` call on the synthetic pattern produces the correct `StartsWith` strategy and literal text via the existing `DeterminePatternStrategy`/`GetPatternText` pipeline.

## Glossary

- **Bug_Condition (C)**: A cross-key pattern is classified as `Complex` by `DeterminePatternStrategy` (contains 2+ wildcards, e.g., `TENANT#*#ROLE#*`) AND has a non-empty leading prefix (text before the first `*`). Currently treated as `null`, preventing compound promotion.
- **Property (P)**: When the bug condition holds, `GetEffectiveCrossKeyPattern` SHALL return a synthetic `StartsWith` pattern (`prefix + "*"`) instead of `null`, enabling compound promotion with the prefix.
- **Preservation**: All behavior for non-Complex patterns (StartsWith, ExactMatch, EndsWith, Contains) and for Complex patterns with no leading prefix (`*#ROLE#*#TENANT#*`) must remain unchanged. Existing tests for the original compound-key-discrimination feature must continue to pass.
- **GetEffectiveCrossKeyPattern**: Private method in `CompoundPromotionPass.cs` that resolves an entity's cross-key `DerivedDiscriminatorPattern` into an effective pattern for compound promotion. Currently returns `null` for Complex patterns.
- **DeterminePatternStrategy**: Static method in `DiscriminatorAnalyzer` that classifies a pattern string into a `DiscriminatorStrategy` enum value (ExactMatch, StartsWith, EndsWith, Contains, Complex).
- **Leading Prefix**: The substring of a pattern before the first `*` character. For `TENANT#*#ROLE#*`, the leading prefix is `TENANT#`. For `*#ROLE#*`, the leading prefix is empty.

## Bug Details

### Bug Condition

The bug manifests when `CompoundPromotionPass` evaluates an entity pair where one or both entities have a cross-key `DerivedDiscriminatorPattern` classified as `Complex`. The `GetEffectiveCrossKeyPattern` method unconditionally returns `null` for Complex patterns, preventing compound promotion even when the leading prefix could disambiguate the pair.

**Formal Specification:**
```
FUNCTION isBugCondition(pattern)
  INPUT: pattern of type string (DerivedDiscriminatorPattern)
  OUTPUT: boolean
  
  strategy := DeterminePatternStrategy(pattern)
  prefixBeforeFirstWildcard := pattern.Substring(0, pattern.IndexOf('*'))
  
  RETURN strategy = Complex
         AND prefixBeforeFirstWildcard.Length > 0
END FUNCTION
```

### Examples

- `TENANT#*#ROLE#*` → Complex pattern with prefix `TENANT#` → currently returns `null`, should return `TENANT#*`
- `SERVICE#*#REGION#*` → Complex pattern with prefix `SERVICE#` → currently returns `null`, should return `SERVICE#*`
- `TENANT#*#ROLE#*` vs `SERVICE#*` → entities have distinguishable prefixes (`TENANT#` vs `SERVICE#`) but Complex entity is treated as null → only exclusion guard assigned instead of dual positive constraints
- `*#ROLE#*#TENANT#*` → Complex pattern with empty prefix → correctly returns `null` (no change needed)
- `TENANT#*#ROLE#*` vs `TENANT#*` → Complex pattern reduces to `TENANT#*`, same as the other entity's pattern → `AreDisambiguable` returns false (identical effective patterns) → not resolvable by this fix

## Expected Behavior

### Preservation Requirements

**Unchanged Behaviors:**
- Non-Complex cross-key patterns (StartsWith, ExactMatch, EndsWith, Contains) must continue to be returned as-is by `GetEffectiveCrossKeyPattern`
- Null or empty cross-key patterns must continue to return `null`
- Complex patterns with no leading prefix (starting with `*`) must continue to return `null`
- Same-prefix Complex patterns that reduce to identical effective patterns must remain classified as not disambiguable
- The `AreDisambiguable`, `AssignPositiveConstraint`, `AssignExclusionConstraint`, and `IsSameScoreOverlap` methods are not modified
- `DiscriminatorAnalyzer.DeterminePatternStrategy` is not modified
- All existing CompoundPromotionPass tests (unit and property-based) must continue to pass
- All existing PatternOverlapAnalyzer behavior is unchanged

**Scope:**
The fix is isolated to the `GetEffectiveCrossKeyPattern` method. All inputs that do NOT involve Complex patterns with a non-empty leading prefix are completely unaffected.

## Hypothesized Root Cause

The root cause is confirmed by code inspection. In `CompoundPromotionPass.GetEffectiveCrossKeyPattern` (lines ~249-282), after calling `DeterminePatternStrategy(pattern)`, the method checks:

```csharp
if (strategy == DiscriminatorStrategy.Complex)
{
    return null;
}
```

This blanket `null` return was an intentional conservative choice in the original compound-key-discrimination feature (documented as "Requirement 7.6: Treat Complex-strategy patterns as null"). At the time, it was the safest approach since Complex patterns cannot be directly expressed as a single `StartsWith`/`EndsWith`/`Contains` check.

However, this is overly conservative: many Complex patterns have a meaningful leading prefix that can be used as a `StartsWith` discriminator. The fix replaces the blanket `null` return with prefix extraction logic.

## Correctness Properties

Property 1: Bug Condition — Complex Pattern Prefix Extraction

_For any_ cross-key pattern where `DeterminePatternStrategy` returns `Complex` and the pattern has a non-empty leading prefix (text before the first `*`), `GetEffectiveCrossKeyPattern` SHALL return the synthetic pattern `prefix + "*"` instead of `null`. When two entities in a same-score overlap pair have Complex patterns with different leading prefixes, `CompoundPromotionPass` SHALL resolve the pair via dual positive `CompoundConstraint` assignments using `StartsWith` with their respective prefixes.

**Validates: Requirements 2.1, 2.2, 2.3**

Property 2: Preservation — Non-Complex and Empty-Prefix Behavior

_For any_ cross-key pattern that is NOT Complex (StartsWith, ExactMatch, EndsWith, Contains), OR is Complex but has an empty leading prefix (starts with `*`), OR is null/empty, `GetEffectiveCrossKeyPattern` SHALL produce the same result as the original (unfixed) function. All existing compound promotion behavior for non-Complex patterns, same-prefix reductions, and null-pattern pairs SHALL be preserved unchanged.

**Validates: Requirements 3.1, 3.2, 3.3, 3.4, 3.5, 3.6**

## Fix Implementation

### Changes Required

**File**: `Oproto.FluentDynamoDb.SourceGenerator/Analysis/CompoundPromotionPass.cs`

**Method**: `GetEffectiveCrossKeyPattern`

**Specific Changes**:

1. **Replace blanket `null` return for Complex patterns**: Instead of returning `null` when `strategy == Complex`, extract the leading prefix from the pattern (substring before the first `*` character).

2. **Check prefix is non-empty**: If the extracted prefix has length > 0, construct a synthetic `StartsWith` pattern by appending `*` to the prefix (e.g., `TENANT#` → `TENANT#*`). Return this synthetic pattern.

3. **Preserve null for empty prefix**: If the leading prefix is empty (pattern starts with `*`, e.g., `*#ROLE#*`), continue returning `null`.

**Pseudocode for the change**:
```
// Replace:
if (strategy == Complex)
    return null;

// With:
if (strategy == Complex)
{
    var starIndex = pattern.IndexOf('*');
    if (starIndex > 0)
    {
        var prefix = pattern.Substring(0, starIndex);
        return prefix + "*";
    }
    return null;
}
```

**Why this works downstream**: When `AssignPositiveConstraint` receives the synthetic pattern `TENANT#*`, it calls `DeterminePatternStrategy("TENANT#*")` which correctly returns `StartsWith`, and `GetPatternText("TENANT#*", StartsWith)` returns `TENANT#`. So the compound constraint generated for the entity will use `StartsWith("TENANT#")`.

4. **Update XML doc comment**: Remove reference to "Requirement 7.6: Treat Complex-strategy patterns as null" and replace with documentation of the new prefix extraction behavior.

5. **Update Test 6 in CompoundPromotionPassTests.cs**: The existing `Analyze_ComplexCrossKeyPattern_TreatedAsNull` test asserts that a Complex pattern entity gets an exclusion guard (because Complex → null → one-null-one-non-null → exclusion). After the fix, the Complex entity with prefix `REGION#` will instead get a positive constraint with `StartsWith("REGION#")`, so the test must be updated to expect dual positive constraints.

## Testing Strategy

### Validation Approach

The testing strategy follows a two-phase approach: first, surface counterexamples that demonstrate the bug on unfixed code, then verify the fix works correctly and preserves existing behavior.

### Exploratory Bug Condition Checking

**Goal**: Surface counterexamples that demonstrate the bug BEFORE implementing the fix. Confirm that Complex patterns with non-empty prefixes are currently treated as null, preventing compound promotion.

**Test Plan**: Write a property-based test that generates entity pairs where one or both have Complex cross-key patterns with non-empty leading prefixes and different prefixes from the other entity. Assert that `CompoundPromotionPass` resolves the pair via dual positive constraints. Run on UNFIXED code to observe failures.

**Test Cases**:
1. **Complex vs StartsWith (different prefixes)**: Entity A has `TENANT#*#ROLE#*` (Complex), Entity B has `SERVICE#*` (StartsWith). Expected after fix: both get positive constraints. (Will fail on unfixed code — Complex entity gets exclusion instead of positive)
2. **Complex vs Complex (different prefixes)**: Entity A has `TENANT#*#ROLE#*`, Entity B has `SERVICE#*#REGION#*`. Expected after fix: both get positive constraints using reduced prefixes. (Will fail on unfixed code — both treated as null → not disambiguable)
3. **Complex vs StartsWith (same prefix)**: Entity A has `TENANT#*#ROLE#*`, Entity B has `TENANT#*`. Expected: identical reduced patterns → not disambiguable. (This case already works correctly — both treated as null or identical)

**Expected Counterexamples**:
- Complex pattern entities receive exclusion guards instead of positive constraints
- Complex vs Complex pairs are not resolved at all (both treated as null)

### Fix Checking

**Goal**: Verify that for all inputs where the bug condition holds (Complex pattern with non-empty leading prefix), the fixed function produces the expected behavior.

**Pseudocode:**
```
FOR ALL (entityA, entityB) WHERE isBugCondition(entityA.crossKeyPattern) OR isBugCondition(entityB.crossKeyPattern) DO
  result := CompoundPromotionPass.Analyze([entityA, entityB], overlapDiagnostics)
  IF prefixOf(entityA.crossKeyPattern) ≠ prefixOf(entityB.crossKeyPattern) THEN
    ASSERT (entityA, entityB) IN result.ResolvedPairs
    ASSERT entityA.CompoundConstraint.IsExclusion = false  // positive constraint
    ASSERT entityB.CompoundConstraint.IsExclusion = false  // positive constraint
  END IF
END FOR
```

### Preservation Checking

**Goal**: Verify that for all inputs where the bug condition does NOT hold (non-Complex patterns, or Complex with empty prefix), the fixed function produces the same result as the original function.

**Pseudocode:**
```
FOR ALL (entityA, entityB) WHERE NOT isBugCondition(entityA.crossKeyPattern) AND NOT isBugCondition(entityB.crossKeyPattern) DO
  ASSERT CompoundPromotionPass_original(entityA, entityB) = CompoundPromotionPass_fixed(entityA, entityB)
END FOR
```

**Testing Approach**: Property-based testing is recommended for preservation checking because:
- It generates many test cases automatically across the input domain of non-Complex patterns
- It catches edge cases that manual unit tests might miss
- It provides strong guarantees that behavior is unchanged for all non-buggy inputs

**Test Plan**: The existing property-based tests in `CompoundPromotionPassPropertyTests.cs` already cover the preservation domain thoroughly (Properties 1-10 covering non-Complex patterns). Run these on UNFIXED code to confirm they pass, then verify they still pass after the fix.

**Test Cases**:
1. **Existing Property 1 tests**: Verify disambiguability classification for non-Complex patterns continues working
2. **Existing Property 3 tests**: Verify dual positive constraint assignment for non-Complex differing patterns continues working
3. **Existing Property 4 tests**: Verify asymmetric constraint assignment for null-vs-non-null continues working
4. **Existing Property 8 tests**: Verify non-interference for non-overlapping entities continues working
5. **New preservation test**: Verify Complex patterns with empty prefix still return null (existing behavior)

### Unit Tests

- Update `Analyze_ComplexCrossKeyPattern_TreatedAsNull` to expect dual positive constraints instead of exclusion
- Add test: Complex vs Complex with different prefixes → both get positive constraints
- Add test: Complex with empty prefix (`*#ROLE#*`) → still treated as null
- Add test: Complex vs StartsWith with same reduced prefix → not disambiguable

### Property-Based Tests

- Generate random Complex patterns with non-empty prefixes and verify prefix extraction produces correct synthetic patterns
- Generate entity pairs with Complex patterns having different prefixes and verify dual positive constraint assignment
- Generate entity pairs where neither pattern triggers the bug condition and verify preservation of existing behavior
- Run all 10 existing properties to verify no regressions

### Integration Tests

- Full pipeline test with real entity pairs from the user's scenario (CapabilityDefinitionEntity vs RoleCapabilityEntity)
- Verify FDDB104 diagnostics are emitted for newly-resolvable Complex pattern pairs
- Verify FDDB102 diagnostics are suppressed for resolved pairs
