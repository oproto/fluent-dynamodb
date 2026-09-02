# Implementation Plan

## Overview

Fix the positional `IndexOf` checks generated for Complex pattern discrimination to enforce "one or more characters" semantics for wildcards. Currently, wildcards are treated as "zero or more characters", allowing values with trailing separators (e.g., `"ORDER#123#"`) or empty first wildcards (e.g., `"ORDER##LINE1"`) to incorrectly pass structural checks for patterns like `"ORDER#*#*"`. The fix tightens the search offset and adds length bounds to ensure every wildcard position contains at least one character.

## Tasks

- [x] 1. Write bug condition exploration test
  - **Property 1: Bug Condition** - Wildcards Allow Zero-Character Matches in Positional IndexOf Checks
  - **CRITICAL**: This test MUST FAIL on unfixed code - failure confirms the bug exists
  - **DO NOT attempt to fix the test or the code when it fails**
  - **NOTE**: This test encodes the expected behavior - it will validate the fix when it passes after implementation
  - **GOAL**: Surface counterexamples that demonstrate the bug exists — trailing separators and empty first wildcards incorrectly pass positional checks
  - **Scoped PBT Approach**: Scope the property to concrete failing cases where zero-character wildcards are accepted
  - Create test file: `Oproto.FluentDynamoDb.SourceGenerator.UnitTests/WildcardOnePlusSemanticsBugConditionTests.cs`
  - **Return mode trailing separator**: Test that `GenerateComplexPatternCheck("ORDER#*#*", "return")` generates code that rejects `"ORDER#123#"` (trailing separator, second wildcard is empty) — currently generates `IndexOf("#", 6) >= 0` which incorrectly passes because IndexOf finds the separator at position 9 without checking if content exists after it
  - **Return mode first wildcard empty**: Test that `GenerateComplexPatternCheck("CAP#*#*", "return")` generates code that rejects `"CAP##bar"` (separator immediately after prefix, first wildcard is empty) — currently uses offset `prefixLength` (4) which finds the separator at position 4 itself
  - **Negated mode trailing separator**: Test that `GenerateComplexPatternCheck("CAP#*#*", "negated")` generates code that correctly rejects `"CAP#foo#"` — currently `IndexOf("#", 4) < 0` does not reject values where separator is at terminal position
  - **Exclusion check trailing separator**: Test that `GenerateComplexExclusionCheck("ORDER#*#*")` generates code that rejects `"ORDER#123#"` — currently `IndexOf("#", 6) >= 0` incorrectly includes values with empty final wildcard
  - **Expected generated code (return mode)**: `discriminatorValue.S.IndexOf("#", 5) >= 0 && discriminatorValue.S.IndexOf("#", 5) < discriminatorValue.S.Length - 1` (offset `prefixLength + 1` for first wildcard 1+, `< Length - 1` for last wildcard 1+)
  - **Expected generated code (negated mode)**: `discriminatorValue.S.IndexOf("#", 5) < 0 || discriminatorValue.S.IndexOf("#", 5) >= discriminatorValue.S.Length - 1`
  - **Expected generated code (exclusion check)**: `discriminatorValue.S.IndexOf("#", 5) >= 0 && discriminatorValue.S.IndexOf("#", 5) < discriminatorValue.S.Length - 1`
  - Run test on UNFIXED code
  - **EXPECTED OUTCOME**: Tests FAIL (confirms bug exists — current code uses `prefixLength` as offset without `+ 1`, and lacks `< Length - 1` bound)
  - Document counterexamples: e.g., `"ORDER#123#"` passes `IndexOf("#", 6) >= 0` because position 9 >= 0, but should fail because 9 is NOT < 9 (Length - 1)
  - Mark task complete when tests are written, run, and failure is documented
  - _Requirements: 1.1, 1.2, 1.3, 1.4_

- [x] 2. Write preservation property tests (BEFORE implementing fix)
  - **Property 2: Preservation** - Valid Multi-Segment Values and Non-IndexOf Patterns Unchanged
  - **IMPORTANT**: Follow observation-first methodology
  - Create test file: `Oproto.FluentDynamoDb.SourceGenerator.UnitTests/WildcardOnePlusSemanticsPreservationTests.cs`
  - Observe: `GenerateComplexPatternCheck("ORDER#*#*", "return")` correctly matches `"ORDER#123#LINE1"` on unfixed code (IndexOf("#", 6) = 9, passes)
  - Observe: `GenerateComplexPatternCheck("ORDER#*#*", "return")` correctly matches `"ORDER#123#LINE1#DETAIL"` on unfixed code (multiple segments after separator)
  - Observe: `GenerateComplexPatternCheck("INVOICE#*#LINE#*", "return")` produces `StartsWith("INVOICE#") && Contains("#LINE#")` unchanged (meaningful segment, not affected by IndexOf changes)
  - Observe: Wildcard-first patterns (e.g., `"*#SUFFIX"`) do not use positional IndexOf checks and are unaffected
  - Observe: Simple prefix patterns (e.g., `"ORDER#*"`) use only `StartsWith` and are unaffected
  - Observe: `GenerateComplexPatternCheck("CAP#*#*", "negated")` correctly rejects `"CAP#foo#bar"` on unfixed code (value with content in both wildcards should NOT be rejected)
  - Write property-based tests: for all values where both wildcard positions contain 1+ characters, the pattern continues to match correctly
  - Write property-based tests: for patterns with meaningful internal segments (Contains checks), behavior is unchanged
  - Write property-based tests: for wildcard-first and simple prefix patterns, behavior is unchanged
  - Run tests on UNFIXED code
  - **EXPECTED OUTCOME**: Tests PASS (confirms baseline behavior to preserve)
  - Mark task complete when tests are written, run, and passing on unfixed code
  - _Requirements: 3.1, 3.2, 3.3, 3.4, 3.5_

- [x] 3. Fix for wildcard one-plus semantics in positional IndexOf checks

  - [x] 3.1 Change search offset from `prefixLength` to `prefixLength + 1` in GenerateComplexPatternCheck (return mode)
    - File: `Oproto.FluentDynamoDb.SourceGenerator/Generators/MapperGenerator.cs`
    - In `GenerateComplexPatternCheck()` "return" mode, where bare-separator positional IndexOf is emitted
    - Change the offset from `prefixSegment.Length` (prefixLength) to `prefixSegment.Length + 1` (prefixLength + 1)
    - This ensures the first wildcard must be at least 1 character (separator cannot be immediately after prefix)
    - Add `&& discriminatorValue.S.IndexOf("<sep>", <offset>) < discriminatorValue.S.Length - 1` to the condition
    - This ensures the last wildcard must be at least 1 character (content must exist after the found separator)
    - Generated output for `"CAP#*#*"` (prefixLength=4): `IndexOf("#", 5) >= 0 && IndexOf("#", 5) < discriminatorValue.S.Length - 1`
    - Generated output for `"ORDER#*#*"` (prefixLength=6): `IndexOf("#", 7) >= 0 && IndexOf("#", 7) < discriminatorValue.S.Length - 1`
    - _Bug_Condition: isBugCondition(input) where offset=prefixLength allows empty first wildcard and missing length bound allows empty last wildcard_
    - _Expected_Behavior: offset=prefixLength+1 AND IndexOf < Length-1 ensures both wildcards are 1+ characters_
    - _Preservation: Meaningful segment Contains checks unaffected; wildcard-first patterns unaffected_
    - _Requirements: 2.1, 2.4, 3.1, 3.2, 3.3_

  - [x] 3.2 Change search offset and add length bound in GenerateComplexPatternCheck (negated mode)
    - File: `Oproto.FluentDynamoDb.SourceGenerator/Generators/MapperGenerator.cs`
    - In `GenerateComplexPatternCheck()` "negated" mode, where bare-separator positional IndexOf is emitted
    - Change the offset from `prefixSegment.Length` to `prefixSegment.Length + 1`
    - Change condition from `IndexOf("<sep>", <offset>) < 0` to `IndexOf("<sep>", <offset>) < 0 || IndexOf("<sep>", <offset>) >= discriminatorValue.S.Length - 1`
    - Generated output for `"CAP#*#*"`: `IndexOf("#", 5) < 0 || IndexOf("#", 5) >= discriminatorValue.S.Length - 1`
    - _Bug_Condition: isBugCondition(input) where negated check only rejects missing separator, not terminal separator_
    - _Expected_Behavior: negated check also rejects terminal separator position (>= Length - 1) and uses offset+1_
    - _Preservation: Meaningful segment !Contains checks unaffected_
    - _Requirements: 2.2, 2.4, 3.1, 3.2_

  - [x] 3.3 Change search offset and add length bound in GenerateComplexExclusionCheck
    - File: `Oproto.FluentDynamoDb.SourceGenerator/Generators/MapperGenerator.cs`
    - In `GenerateComplexExclusionCheck()`, where bare-separator positional IndexOf is emitted
    - Change the offset from `prefixSegment.Length` to `prefixSegment.Length + 1`
    - Add `&& discriminatorValue.S.IndexOf("<sep>", <offset>) < discriminatorValue.S.Length - 1` to the condition
    - Generated output for `"CAP#*#*"`: `IndexOf("#", 5) >= 0 && IndexOf("#", 5) < discriminatorValue.S.Length - 1`
    - _Bug_Condition: isBugCondition(input) where exclusion incorrectly includes values with empty wildcard portions_
    - _Expected_Behavior: exclusion only includes values where both wildcards are 1+ characters_
    - _Preservation: Meaningful segment Contains checks in exclusion unaffected_
    - _Requirements: 2.3, 2.4, 3.1, 3.2_

  - [x] 3.4 Verify bug condition exploration test now passes
    - **Property 1: Expected Behavior** - Wildcards Enforce One-Plus Character Semantics
    - **IMPORTANT**: Re-run the SAME test from task 1 - do NOT write a new test
    - The test from task 1 encodes the expected behavior
    - When this test passes, it confirms the expected behavior is satisfied
    - Run bug condition exploration test from step 1
    - **EXPECTED OUTCOME**: Test PASSES (confirms bug is fixed)
    - _Requirements: 2.1, 2.2, 2.3, 2.4_

  - [x] 3.5 Verify preservation tests still pass
    - **Property 2: Preservation** - Valid Multi-Segment Values and Non-IndexOf Patterns Unchanged
    - **IMPORTANT**: Re-run the SAME tests from task 2 - do NOT write new tests
    - Run preservation property tests from step 2
    - **EXPECTED OUTCOME**: Tests PASS (confirms no regressions)
    - Confirm all tests still pass after fix (no regressions)

- [x] 4. Update CHANGELOG.md
  - Add entry under the appropriate version section (or `[Unreleased]`)
  - Section: **Fixed**
  - Entry: Wildcard `*` in complex key patterns now enforces "one or more characters" semantics — values with trailing separators (e.g., `"ORDER#123#"`) or empty wildcard portions (e.g., `"ORDER##LINE1"`) are correctly rejected by `IndexOf` positional checks in `GenerateComplexPatternCheck` and `GenerateComplexExclusionCheck`
  - Reference the issue/spec: `wildcard-one-plus-semantics`
  - _Requirements: 2.1, 2.2, 2.3, 2.4_

- [x] 5. Checkpoint - Ensure all tests pass
  - Run `dotnet build-server shutdown` then `dotnet build` to verify no compilation errors with fresh generator
  - Run `dotnet test` to verify all tests pass
  - Verify bug condition test from task 1 now passes (confirms fix works)
  - Verify preservation tests from task 2 still pass (confirms valid multi-segment values like `"ORDER#123#LINE1"` still match correctly)
  - Verify existing tests from the `complex-pattern-discrimination-fix` and `complex-pattern-exclusion-contains-separator` specs still pass (previous fixes unbroken)
  - Ensure no existing tests in the project are broken by the changes
  - Ensure all tests pass, ask the user if questions arise.

## Notes

- The source generator caches in memory; run `dotnet build-server shutdown` before rebuilding after generator changes
- This fix builds on top of the positional `IndexOf` emission added in the `complex-pattern-discrimination-fix` spec — the existing `IndexOf` calls need their offset incremented by 1 and a `< Length - 1` upper bound added
- The fix is separator-agnostic: works for any separator character (#, _, :, -, etc.)
- Only the same three methods modified in the prior spec: `GenerateComplexPatternCheck` (two modes) and `GenerateComplexExclusionCheck`, all in `MapperGenerator.cs`
- The `+ 1` offset change and `< Length - 1` bound change are orthogonal but both needed for full one-plus enforcement
- Test projects: `Oproto.FluentDynamoDb.SourceGenerator.UnitTests` for generator unit tests

## Task Dependency Graph

```json
{
  "waves": [
    { "wave": 1, "tasks": ["1", "2"] },
    { "wave": 2, "tasks": ["3.1", "3.2", "3.3"] },
    { "wave": 3, "tasks": ["3.4", "3.5"] },
    { "wave": 4, "tasks": ["4"] },
    { "wave": 5, "tasks": ["5"] }
  ]
}
```
