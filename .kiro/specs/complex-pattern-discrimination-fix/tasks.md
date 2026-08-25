# Implementation Plan

## Overview

Fix the source generator's Complex pattern discrimination positive-side code generation. The exclusion side (OffsetIndex, IsTautologicalExclusion) is already complete. The remaining work replaces the `continue` (skip) for bare-separator segments in `GenerateComplexPatternCheck` (both "return" and "negated" modes) and `GenerateComplexExclusionCheck` with positional `IndexOf(segment, prefixLength) >= 0` (or `< 0` for negated).

## Tasks

- [x] 1. Write bug condition exploration test
  - **Property 1: Bug Condition** - Positive Complex Pattern Check Skips Bare-Separator Discrimination
  - **CRITICAL**: This test MUST FAIL on unfixed code - failure confirms the bug exists
  - **DO NOT attempt to fix the test or the code when it fails**
  - **NOTE**: This test encodes the expected behavior - it will validate the fix when it passes after implementation
  - **GOAL**: Surface counterexamples that demonstrate the bug exists — the positive check degrades to just StartsWith when bare-separator segments are skipped
  - **Scoped PBT Approach**: Scope the property to concrete failing cases: patterns `"CAP#*#*"`, `"ORDER#*#*"`, `"NS:*:*"`, `"X_*_*"` where the generated positive check must include a positional IndexOf
  - Create test file: `Oproto.FluentDynamoDb.SourceGenerator.UnitTests/ComplexPatternDiscriminationPositiveBugConditionTests.cs`
  - Test that `GenerateComplexPatternCheck()` in "return" mode for `"CAP#*#*"` generates `StartsWith("CAP#") && IndexOf("#", 4) >= 0` (NOT just `StartsWith("CAP#")`)
  - Test that `GenerateComplexPatternCheck()` in "negated" mode for `"CAP#*#*"` generates `!StartsWith("CAP#") || IndexOf("#", 4) < 0` (NOT just `!StartsWith("CAP#")`)
  - Test that `GenerateComplexExclusionCheck()` for `"CAP#*#*"` generates `StartsWith("CAP#") && IndexOf("#", 4) >= 0` (NOT `StartsWith("CAP#") && Contains("#")`)
  - **Hash separator (#)**: Pattern `"CAP#*#*"` prefix length 4 — positional check uses offset 4
  - **Underscore separator (_)**: Pattern `"X_*_*"` prefix length 2 — positional check uses offset 2
  - **Colon separator (:)**: Pattern `"NS:*:*"` prefix length 3 — positional check uses offset 3
  - Run test on UNFIXED code
  - **EXPECTED OUTCOME**: Tests FAIL (confirms bug exists — "return" mode produces only `StartsWith`, "negated" mode produces only `!StartsWith`, exclusion check produces tautological `Contains`)
  - Document counterexamples: e.g., `GenerateComplexPatternCheck("CAP#*#*", "return")` produces `return discriminatorValue.S.StartsWith("CAP#");` with no structural discrimination beyond the prefix
  - Mark task complete when tests are written, run, and failure is documented
  - _Requirements: 1.2, 1.3_

- [x] 2. Write preservation property tests (BEFORE implementing fix)
  - **Property 2: Preservation** - Meaningful Segments and Non-Complex Patterns Unchanged
  - **IMPORTANT**: Follow observation-first methodology
  - Create test file: `Oproto.FluentDynamoDb.SourceGenerator.UnitTests/ComplexPatternDiscriminationPreservationTests.cs`
  - Observe: `GenerateComplexPatternCheck("INVOICE#*#LINE#*", "return")` produces `StartsWith("INVOICE#") && Contains("#LINE#")` on unfixed code
  - Observe: `GenerateComplexPatternCheck("USER#*#ROLE#*", "return")` produces `StartsWith("USER#") && Contains("#ROLE#")` on unfixed code
  - Observe: `GenerateComplexPatternCheck("INVOICE#*#LINE#*", "negated")` produces `!StartsWith("INVOICE#") || !Contains("#LINE#")` on unfixed code
  - Observe: `GenerateComplexPatternCheck("*#SUFFIX#*", "return")` produces `Contains("#SUFFIX#")` on unfixed code (wildcard-first pattern)
  - Observe: `GenerateComplexExclusionCheck("INVOICE#*#LINE#*")` produces `StartsWith("INVOICE#") && Contains("#LINE#")` on unfixed code
  - Observe: Simple StartsWith patterns (e.g., `"ORDER#*"`) continue to generate `StartsWith("ORDER#")` unchanged
  - Observe: Custom discriminator entities are not affected by Complex pattern code generation
  - Write property-based tests: for all Complex patterns with meaningful internal segments (where `!prefixSegment.Contains(internalSegment)`), `Contains(segment)` is preserved in all three methods
  - Write property-based tests: for wildcard-first patterns (`"*segment*"`), all segments use `Contains()` unchanged
  - Run tests on UNFIXED code
  - **EXPECTED OUTCOME**: Tests PASS (confirms baseline behavior to preserve)
  - Mark task complete when tests are written, run, and passing on unfixed code
  - _Requirements: 3.1, 3.2, 3.3, 3.5, 3.6, 3.7_

- [x] 3. Fix for positive-side Complex pattern discrimination

  - [x] 3.1 Implement positional IndexOf in GenerateComplexPatternCheck "return" mode
    - File: `Oproto.FluentDynamoDb.SourceGenerator/Generators/MapperGenerator.cs`
    - In `GenerateComplexPatternCheck()` "return" mode, replace the `continue` for bare-separator segments with a positional IndexOf condition
    - When `prefixSegment.Contains(nonEmptySegments[i])` is true: emit `discriminatorValue.S.IndexOf("<segment>", <prefixSegment.Length>) >= 0`
    - When false: emit existing `discriminatorValue.S.Contains("<segment>")` (meaningful segment — unchanged)
    - Generated output for `"CAP#*#*"`: `return discriminatorValue.S.StartsWith("CAP#") && discriminatorValue.S.IndexOf("#", 4) >= 0;`
    - Generated output for `"INVOICE#*#LINE#*"`: `return discriminatorValue.S.StartsWith("INVOICE#") && discriminatorValue.S.Contains("#LINE#");` (unchanged)
    - _Bug_Condition: isBugCondition(input) where prefixSegment.Contains(internalSegment) causes `continue` instead of positional check_
    - _Expected_Behavior: bare-separator segments produce IndexOf(segment, prefixLength) >= 0 in return mode_
    - _Preservation: Meaningful segments continue using Contains(); wildcard-first patterns unchanged_
    - _Requirements: 2.2, 2.4, 3.1, 3.6_

  - [x] 3.2 Implement positional IndexOf in GenerateComplexPatternCheck "negated" mode
    - File: `Oproto.FluentDynamoDb.SourceGenerator/Generators/MapperGenerator.cs`
    - In `GenerateComplexPatternCheck()` "negated" mode, replace the `continue` for bare-separator segments with a negated positional IndexOf condition
    - When `prefixSegment.Contains(nonEmptySegments[i])` is true: emit `discriminatorValue.S.IndexOf("<segment>", <prefixSegment.Length>) < 0`
    - When false: emit existing `!discriminatorValue.S.Contains("<segment>")` (meaningful segment — unchanged)
    - Generated output for `"CAP#*#*"`: `if (!discriminatorValue.S.StartsWith("CAP#") || discriminatorValue.S.IndexOf("#", 4) < 0) return false;`
    - Generated output for `"INVOICE#*#LINE#*"`: `if (!discriminatorValue.S.StartsWith("INVOICE#") || !discriminatorValue.S.Contains("#LINE#")) return false;` (unchanged)
    - _Bug_Condition: isBugCondition(input) where prefixSegment.Contains(internalSegment) causes `continue` instead of negated positional check_
    - _Expected_Behavior: bare-separator segments produce IndexOf(segment, prefixLength) < 0 in negated mode_
    - _Preservation: Meaningful segments continue using !Contains(); wildcard-first patterns unchanged_
    - _Requirements: 2.2, 2.4, 3.1, 3.6_

  - [x] 3.3 Implement positional IndexOf in GenerateComplexExclusionCheck
    - File: `Oproto.FluentDynamoDb.SourceGenerator/Generators/MapperGenerator.cs`
    - In `GenerateComplexExclusionCheck()`, add bare-separator detection for internal segments
    - Extract `prefixSegment = nonEmptySegments[0]` when pattern doesn't start with `*`
    - For each internal segment (i > 0): check `prefixSegment.Contains(nonEmptySegments[i])`
    - When true: emit `discriminatorValue.S.IndexOf("<segment>", <prefixSegment.Length>) >= 0`
    - When false: emit existing `discriminatorValue.S.Contains("<segment>")` (meaningful segment — unchanged)
    - Generated output for `"CAP#*#*"`: `if (discriminatorValue.S.StartsWith("CAP#") && discriminatorValue.S.IndexOf("#", 4) >= 0) return false;`
    - Generated output for `"INVOICE#*#LINE#*"`: `if (discriminatorValue.S.StartsWith("INVOICE#") && discriminatorValue.S.Contains("#LINE#")) return false;` (unchanged)
    - _Bug_Condition: GenerateComplexExclusionCheck uses Contains for bare separators, producing tautological exclusion_
    - _Expected_Behavior: bare-separator segments produce IndexOf(segment, prefixLength) >= 0 in exclusion check_
    - _Preservation: Meaningful segments and wildcard-first patterns continue using Contains()_
    - _Requirements: 2.1, 2.2, 2.4, 3.1, 3.6_

  - [x] 3.4 Verify bug condition exploration test now passes
    - **Property 1: Expected Behavior** - Positive Complex Pattern Check Uses Positional IndexOf
    - **IMPORTANT**: Re-run the SAME test from task 1 - do NOT write a new test
    - The test from task 1 encodes the expected behavior
    - When this test passes, it confirms the expected behavior is satisfied for all separator types
    - Run bug condition exploration test from step 1
    - **EXPECTED OUTCOME**: Test PASSES (confirms bug is fixed)
    - _Requirements: 2.1, 2.2, 2.4_

  - [x] 3.5 Verify preservation tests still pass
    - **Property 2: Preservation** - Meaningful Segments and Non-Complex Patterns Unchanged
    - **IMPORTANT**: Re-run the SAME tests from task 2 - do NOT write new tests
    - Run preservation property tests from step 2
    - **EXPECTED OUTCOME**: Tests PASS (confirms no regressions)
    - Confirm all tests still pass after fix (no regressions)

- [x] 4. Checkpoint - Ensure all tests pass
  - Run `dotnet build-server shutdown` then `dotnet build` to verify no compilation errors with fresh generator
  - Run `dotnet test` to verify all tests pass
  - Verify bug condition test from task 1 now passes (confirms fix works for all separator types)
  - Verify preservation tests from task 2 still pass (confirms meaningful segments like "#LINE#", "#ROLE#" unchanged)
  - Verify existing tests from the `complex-pattern-exclusion-contains-separator` spec still pass (exclusion-side fix unbroken)
  - Ensure no existing tests in the project are broken by the changes
  - Ensure all tests pass, ask the user if questions arise.

## Notes

- The source generator caches in memory; run `dotnet build-server shutdown` before rebuilding after generator changes
- The exclusion-side fix (OffsetIndex on ExclusionPattern, IsTautologicalExclusion enhancement, IndexOf emission in exclusion loop) is ALREADY COMPLETE — do not modify those paths
- The fix is separator-agnostic: works for any separator character (#, _, :, -, etc.)
- Only three methods need modification, all in `MapperGenerator.cs`: `GenerateComplexPatternCheck` (two modes) and `GenerateComplexExclusionCheck`
- The change in each method is identical in principle: replace `continue` with `IndexOf(segment, prefixLength) >= 0` (or `< 0` for negated)
- Test projects: `Oproto.FluentDynamoDb.SourceGenerator.UnitTests` for generator unit tests, `Oproto.FluentDynamoDb.UnitTests` for integration-level tests

## Task Dependency Graph

```json
{
  "waves": [
    { "wave": 1, "tasks": ["1", "2"] },
    { "wave": 2, "tasks": ["3.1", "3.2", "3.3"] },
    { "wave": 3, "tasks": ["3.4"] },
    { "wave": 4, "tasks": ["3.5"] },
    { "wave": 5, "tasks": ["4"] }
  ]
}
```
