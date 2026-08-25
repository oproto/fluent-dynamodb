# Complex Pattern Exclusion Contains Separator — Bugfix Design

## Overview

When `PatternOverlapAnalyzer.CreateExclusionPattern()` decomposes a Complex pattern like `"CAP#*#*"` into segments, the internal segment between adjacent wildcards is just the separator character `"#"`. This produces a `Contains("#")` exclusion guard that is always true for any value already passing `StartsWith("CAP#")`, making the less-specific entity invisible to all queries.

The fix replaces bare-separator `Contains` checks with positional `IndexOf` checks that look for the separator character AFTER the shared prefix length. A secondary fix in `GenerateComplexPatternCheck()` omits non-discriminating `Contains` clauses from positive match generation.

## Glossary

- **Bug_Condition (C)**: The internal segment extracted from between adjacent wildcards in a Complex pattern is a bare separator character (length ≤ separator length, and the literal is already guaranteed present in any value matching the prefix)
- **Property (P)**: When the bug condition holds, the exclusion check uses `IndexOf(separator, prefixLength) >= 0` to verify the separator appears AFTER the prefix — providing actual discrimination
- **Preservation**: Patterns with meaningful internal segments (e.g., `"#LINE#"`, `"#ROLE#"`) continue to use `Contains` with their full literal text, unchanged
- **CreateExclusionPattern()**: Method in `PatternOverlapAnalyzer.cs` (~line 423) that derives exclusion guards from more-specific overlapping patterns
- **IsTautologicalExclusion()**: Method in `PatternOverlapAnalyzer.cs` (~line 491) that detects when an exclusion would always be true given the positive match
- **GenerateComplexPatternCheck()**: Method in `MapperGenerator.cs` (~line 4696) that emits the C# code for Complex pattern positive/negated match checks
- **Bare-separator segment**: An internal segment between adjacent wildcards whose text is only the separator character(s) — carries no discriminating information

## Bug Details

### Bug Condition

The bug manifests when a Complex pattern's internal segments (text between `*` wildcards after the shared prefix) consist solely of the separator character. The `CreateExclusionPattern()` method treats this bare separator as a meaningful literal and generates a `Contains("<sep>")` check that is semantically subsumed by the positive `StartsWith` match.

**Formal Specification:**
```
FUNCTION isBugCondition(input)
  INPUT: input of type (lessSpecificConfig: DiscriminatorConfig, moreSpecificConfig: DiscriminatorConfig)
  OUTPUT: boolean

  segments := moreSpecificConfig.Pattern.Split('*')
  internalSegments := segments.Where(s => s.Length > 0).Skip(1)  // skip shared prefix
  lastSegment := internalSegments.Last()

  prefixSegment := segments.First(s => s.Length > 0)  // e.g., "CAP#"

  RETURN lastSegment.Length <= separatorLength
         AND prefixSegment.Contains(lastSegment)
         AND lessSpecificConfig.Strategy IN [StartsWith, Complex]
END FUNCTION
```

### Examples

- Pattern `"CAP#*#*"` with less-specific `"CAP#*"`: internal segment = `"#"` → `Contains("#")` always true after `StartsWith("CAP#")` → **BUG**: entity invisible
- Pattern `"CAP_*_*"` with less-specific `"CAP_*"`: internal segment = `"_"` → `Contains("_")` always true after `StartsWith("CAP_")` → **BUG**: entity invisible
- Pattern `"NS:*:*"` with less-specific `"NS:*"`: internal segment = `":"` → `Contains(":")` always true after `StartsWith("NS:")` → **BUG**: entity invisible
- Pattern `"INVOICE#*#LINE#*"` with less-specific `"INVOICE#*"`: internal segment = `"#LINE#"` → `Contains("#LINE#")` is meaningful → **NOT A BUG**: works correctly
- Pattern `"USER#*#ROLE#*"` with less-specific `"USER#*"`: internal segment = `"#ROLE#"` → `Contains("#ROLE#")` is meaningful → **NOT A BUG**: works correctly

## Expected Behavior

### Preservation Requirements

**Unchanged Behaviors:**
- Patterns with meaningful internal segments (e.g., `"INVOICE#*#LINE#*"` → `Contains("#LINE#")`) must continue to generate `Contains` exclusions and positive checks as before
- `ExactMatch` exclusion strategy must continue returning the exact value
- Non-Complex strategy patterns (StartsWith, EndsWith, Contains) must continue delegating to `DiscriminatorAnalyzer.GetPatternText()`
- Non-overlapping entities must continue to generate independent `MatchesEntity` checks without exclusion guards
- The `IsTautologicalExclusion` check must continue to detect identity-based tautologies (same Strategy AND same LiteralText)

**Scope:**
All inputs where internal segments are longer than the separator character and are NOT already contained within the prefix should be completely unaffected by this fix. This includes:
- Patterns like `"INVOICE#*#LINE#*"` (segment `"#LINE#"` is meaningful)
- Patterns like `"USER#*#ROLE#*"` (segment `"#ROLE#"` is meaningful)
- Patterns like `"A#*#BC#*#DEF#*"` (segments `"#BC#"` and `"#DEF#"` are meaningful)
- All ExactMatch, StartsWith, EndsWith, and simple Contains patterns

## Hypothesized Root Cause

Based on the bug analysis, there are three interrelated issues:

1. **CreateExclusionPattern() blindly uses last internal segment**: The method splits on `'*'`, skips the first non-empty segment (prefix), and uses the LAST remaining non-empty segment as `Contains` text. When adjacent wildcards are separated only by the separator char (e.g., `"CAP#*#*"` → segments `["CAP#", "#", ""]` → internal = `["#"]`), the segment `"#"` is inherently present in any value matching the prefix `"CAP#"`.

2. **IsTautologicalExclusion() only checks identity**: The method compares exclusion strategy/text to positive strategy/text for exact match. It does NOT detect semantic subsumption — where the exclusion literal is guaranteed to appear in any string matching the positive check (e.g., `Contains("#")` is always true when `StartsWith("CAP#")` already passed).

3. **GenerateComplexPatternCheck() emits non-discriminating Contains**: For the more-specific entity's positive match, the method generates `StartsWith("CAP#") && Contains("#")` where the `Contains("#")` adds zero filtering power — it matches the exact same set of strings as `StartsWith("CAP#")` alone.

## Correctness Properties

Property 1: Bug Condition - Bare-separator exclusions use positional IndexOf

_For any_ pattern pair where the more-specific pattern has bare-separator internal segments (segments that equal only the separator character), the generated exclusion check SHALL use `IndexOf(separator, prefixLength) >= 0` instead of `Contains(separator)`, ensuring that `"CAP#capability1"` is NOT excluded while `"CAP#svc1#cap1"` IS excluded.

**Validates: Requirements 2.1, 2.2, 2.3**

Property 2: Preservation - Meaningful internal segments unchanged

_For any_ pattern pair where the more-specific pattern has meaningful internal segments (segments longer than the separator character and not already contained in the prefix), the generated exclusion check SHALL continue to use `Contains(literal)` with the full segment text, preserving existing correct discrimination behavior.

**Validates: Requirements 3.1, 3.4, 3.6**

## Fix Implementation

### Changes Required

Assuming our root cause analysis is correct:

**File**: `Oproto.FluentDynamoDb.SourceGenerator/Analysis/PatternOverlapAnalyzer.cs`

**Function**: `CreateExclusionPattern()`

**Specific Changes**:

1. **Detect bare-separator internal segments**: After extracting internal segments, check whether the last segment is a bare separator (its text is contained within the prefix segment). A segment is "bare" when it satisfies: `prefixSegment.EndsWith(lastSegment)` or more precisely `prefixSegment.Contains(lastSegment)`.

2. **Generate positional exclusion for bare separators**: When a bare-separator segment is detected, instead of returning `Strategy = Contains, LiteralText = "#"`, return a new strategy or augmented data that tells the code generator to emit `IndexOf('#', prefixLength) >= 0`. This requires either:
   - A new `DiscriminatorStrategy` value (e.g., `IndexOfAfterPrefix`), or
   - Storing the prefix length on the `ExclusionPattern` model and handling it in the generator

   Recommended: Add an `OffsetIndex` property to `ExclusionPattern`. When `OffsetIndex > 0`, the generator emits `IndexOf(literal, offset)` instead of `Contains(literal)`.

3. **Skip non-discriminating segments in iteration**: When multiple internal segments exist and the last one is bare, try earlier segments. If ALL internal segments are bare separators, fall back to the positional approach using the first bare separator.

**Function**: `IsTautologicalExclusion()`

**Specific Changes**:

4. **Detect semantic subsumption**: Expand the check to detect when an exclusion's literal text is guaranteed to be present in any string matching the positive check. Specifically:
   - If exclusion strategy is `Contains` and positive strategy is `StartsWith`, check whether the positive literal contains the exclusion literal. If yes → tautological.
   - If exclusion strategy is `Contains` and positive strategy is `Complex`, extract the prefix from the Complex pattern and check the same containment.

   This serves as a safety net: even if `CreateExclusionPattern()` somehow produces a bare-separator exclusion without the positional fix, `IsTautologicalExclusion` will catch it and emit DISC006 instead of silently generating broken code.

---

**File**: `Oproto.FluentDynamoDb.SourceGenerator/Models/ExclusionPattern.cs`

**Specific Changes**:

5. **Add OffsetIndex property**: Add `public int OffsetIndex { get; set; }` to `ExclusionPattern`. When set to a value > 0, the code generator should emit `IndexOf(LiteralText, OffsetIndex) >= 0` instead of `Contains(LiteralText)`.

---

**File**: `Oproto.FluentDynamoDb.SourceGenerator/Generators/MapperGenerator.cs`

**Function**: `GenerateDiscriminatorCheckWithExclusions()` (exclusion emission loop)

**Specific Changes**:

6. **Handle OffsetIndex in exclusion generation**: In the `DiscriminatorStrategy.Contains` case within the exclusion loop, check if `exclusion.OffsetIndex > 0`. If so, emit:
   ```csharp
   if (discriminatorValue.S.IndexOf("<literal>", <offset>) >= 0)
       return false;
   ```
   instead of:
   ```csharp
   if (discriminatorValue.S.Contains("<literal>"))
       return false;
   ```

**Function**: `GenerateComplexPatternCheck()`

**Specific Changes**:

7. **Omit non-discriminating Contains clauses**: When building the conditions list for a Complex pattern, skip any internal segment whose literal text is already contained within the first segment (the prefix). For `"CAP#*#*"` this means skipping the `Contains("#")` clause entirely, producing just `StartsWith("CAP#")`. For patterns with a mix (e.g., `"CAP#*#*#LINE#*"`), skip `"#"` but keep `"#LINE#"`.

   Alternatively, replace the non-discriminating `Contains` with a positional `IndexOf` check for the positive match as well — but simply omitting it is simpler and correct (the StartsWith already implies the separator exists).

## Testing Strategy

### Validation Approach

The testing strategy follows a two-phase approach: first, surface counterexamples that demonstrate the bug on unfixed code, then verify the fix works correctly and preserves existing behavior. Since this is a source generator, tests verify the GENERATED code's behavior, not the generator's runtime directly.

### Exploratory Bug Condition Checking

**Goal**: Surface counterexamples that demonstrate the bug BEFORE implementing the fix. Confirm or refute the root cause analysis. If we refute, we will need to re-hypothesize.

**Test Plan**: Write unit tests against `PatternOverlapAnalyzer` that exercise the bare-separator scenario and verify the generated exclusion pattern. Run these tests on the UNFIXED code to observe failures.

**Test Cases**:
1. **Hash separator test**: Pattern `"CAP#*#*"` vs `"CAP#*"` — verify the exclusion literal is `"#"` and would be tautological (will demonstrate the bug on unfixed code)
2. **Underscore separator test**: Pattern `"CAP_*_*"` vs `"CAP_*"` — verify same failure with `_` separator
3. **Colon separator test**: Pattern `"NS:*:*"` vs `"NS:*"` — verify same failure with `:` separator
4. **GenerateComplexPatternCheck output test**: Pattern `"CAP#*#*"` generates `StartsWith("CAP#") && Contains("#")` — verify the Contains is non-discriminating (will fail on unfixed code if we assert it should NOT produce Contains)

**Expected Counterexamples**:
- `CreateExclusionPattern("CAP#*#*")` returns `{Strategy: Contains, LiteralText: "#"}` which is tautological given `StartsWith("CAP#")`
- `IsTautologicalExclusion` returns `false` for this case (fails to detect subsumption)
- Generated MatchesEntity for `"CAP#*"` entity always returns `false`

### Fix Checking

**Goal**: Verify that for all inputs where the bug condition holds, the fixed function produces the expected behavior.

**Pseudocode:**
```
FOR ALL input WHERE isBugCondition(input) DO
  exclusion := CreateExclusionPattern_fixed(moreSpecificEntity, moreSpecificConfig)
  ASSERT exclusion.OffsetIndex == prefixSegment.Length
  ASSERT exclusion.LiteralText == bareSeparator
  
  // Verify generated code behavior:
  ASSERT generatedMatchesEntity("CAP#capability1") == true   // single segment: NOT excluded
  ASSERT generatedMatchesEntity("CAP#svc1#cap1") == false    // multi segment: excluded
END FOR
```

### Preservation Checking

**Goal**: Verify that for all inputs where the bug condition does NOT hold, the fixed function produces the same result as the original function.

**Pseudocode:**
```
FOR ALL input WHERE NOT isBugCondition(input) DO
  ASSERT CreateExclusionPattern_original(input) = CreateExclusionPattern_fixed(input)
  ASSERT IsTautologicalExclusion_original(input) = IsTautologicalExclusion_fixed(input)
END FOR
```

**Testing Approach**: Property-based testing is recommended for preservation checking because:
- It generates many pattern combinations automatically across the input domain
- It catches edge cases where the fix might accidentally alter behavior for meaningful segments
- It provides strong guarantees that `"INVOICE#*#LINE#*"` style patterns are unchanged

**Test Plan**: Observe behavior on UNFIXED code first for meaningful-segment patterns, then write property-based tests capturing that behavior.

**Test Cases**:
1. **Meaningful segment preservation**: Verify `"INVOICE#*#LINE#*"` continues to produce `{Strategy: Contains, LiteralText: "#LINE#"}` exclusion
2. **Multi-meaningful-segment preservation**: Verify `"A#*#BC#*#DEF#*"` continues to use `"#DEF#"` (last meaningful segment)
3. **ExactMatch preservation**: Verify ExactMatch patterns continue unchanged
4. **Non-Complex preservation**: Verify StartsWith/EndsWith/Contains patterns delegate to `GetPatternText()` unchanged

### Unit Tests

- Test `CreateExclusionPattern()` with bare-separator patterns (`"CAP#*#*"`, `"X_*_*"`, `"NS:*:*"`)
- Test `CreateExclusionPattern()` with meaningful patterns (`"INVOICE#*#LINE#*"`) — verify unchanged
- Test `IsTautologicalExclusion()` detects semantic subsumption (`Contains("#")` vs `StartsWith("CAP#")`)
- Test `IsTautologicalExclusion()` does NOT flag meaningful exclusions (`Contains("#LINE#")` vs `StartsWith("INVOICE#")`)
- Test `GenerateComplexPatternCheck()` omits bare-separator Contains for pattern `"CAP#*#*"`
- Test `GenerateComplexPatternCheck()` keeps meaningful Contains for pattern `"INVOICE#*#LINE#*"`
- Test exclusion generation with `OffsetIndex > 0` emits `IndexOf` instead of `Contains`
- Test end-to-end: entity with `"CAP#*"` and overlapping `"CAP#*#*"` generates correct MatchesEntity that accepts `"CAP#x"` and rejects `"CAP#x#y"`

### Property-Based Tests

- Generate random prefix strings and separator characters, build `"PREFIX<sep>*<sep>*"` patterns, and verify the exclusion always uses positional IndexOf (never bare Contains)
- Generate random meaningful segment strings (length > 1, not equal to separator), build `"PREFIX<sep>*<seg>*"` patterns, and verify the exclusion uses `Contains(seg)` unchanged
- Generate random discriminator values and verify the IndexOf-based exclusion correctly discriminates single-segment vs multi-segment values

### Integration Tests

- Full source generator test: define two entities with overlapping `"CAP#*"` / `"CAP#*#*"` SK patterns, compile, and verify the generated MatchesEntity for the less-specific entity returns `true` for `"CAP#capability1"` and `false` for `"CAP#svc1#cap1"`
- Full source generator test: define `"INVOICE#*"` / `"INVOICE#*#LINE#*"` entities, compile, and verify the generated code uses `Contains("#LINE#")` (preservation)
- Multi-separator test: define entities with `_` separator and verify the fix works with non-`#` separators
- Three-entity overlap test: `"CAP#*"` / `"CAP#*#*"` / `"CAP#*#*#*"` — verify correct cascade of exclusions
