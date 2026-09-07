# Implementation Plan

## Overview

Fix for trailing bare-separator `< Length - 1` constraint bug in `GenerateComplexPatternCheck` (return and negated modes) and `GenerateComplexExclusionCheck` in `MapperGenerator.cs`. The bug causes `MatchesEntity` to silently reject valid items when a `[Computed]` format ends with a trailing literal (e.g., `"EMPLOYEE#{0}#"` → pattern `"EMPLOYEE#*#"`).

## Tasks

- [x] 1. Write bug condition exploration test
  - **Property 1: Bug Condition** - Trailing Bare-Separator Incorrectly Enforces Length Constraint
  - **CRITICAL**: This test MUST FAIL on unfixed code - failure confirms the bug exists
  - **DO NOT attempt to fix the test or the code when it fails**
  - **NOTE**: This test encodes the expected behavior - it will validate the fix when it passes after implementation
  - **GOAL**: Surface counterexamples that demonstrate the trailing bare-separator `< Length - 1` constraint is incorrectly applied
  - **Scoped PBT Approach**: Scope properties to concrete trailing-literal patterns (`"EMPLOYEE#*#"`, `"ORDER#*#"`, `"NS:*:"`, `"X_*_"`) across all three modes (return, negated, exclusion)
  - **Test file**: `Oproto.FluentDynamoDb.SourceGenerator.UnitTests/TrailingBareSeparatorBugConditionTests.cs`
  - **Pattern**: Follow the existing `ComplexPatternDiscriminationPositiveBugConditionTests.cs` pattern — use `FsCheck.Xunit` `[Property]` attribute with `MaxTest = 1`, invoke `GenerateComplexPatternCheck` and `GenerateComplexExclusionCheck` via reflection
  - **Bug Condition** (from design `isBugCondition`): Pattern ends with a literal after the last wildcard (`!pattern.EndsWith("*")`), and the trailing segment is a bare-separator (contained in the prefix segment), and it is the last non-empty segment
  - **Return mode tests** (`"EMPLOYEE#*#"`, `"ORDER#*#"`, `"NS:*:"`, `"X_*_"`): Assert the generated code contains `IndexOf(separator, offset) >= 0` for the trailing segment but does NOT contain `< discriminatorValue.S.Length - 1` for that trailing position. On unfixed code, the `< Length - 1` constraint IS present, so the test will FAIL
  - **Negated mode tests** (`"EMPLOYEE#*#"`, `"ORDER#*#"`): Assert the generated code contains `IndexOf(separator, offset) < 0` for the trailing segment but does NOT contain `>= discriminatorValue.S.Length - 1` for that trailing position. On unfixed code, the `>= Length - 1` branch IS present, so the test will FAIL
  - **Exclusion mode tests** (`"EMPLOYEE#*#"`, `"ORDER#*#"`): Assert the generated code contains `IndexOf(separator, offset) >= 0` for the trailing segment but does NOT contain `< discriminatorValue.S.Length - 1` for that trailing position. On unfixed code, the `< Length - 1` constraint IS present, so the test will FAIL
  - **Multi-segment trailing test** (`"TYPE#*#VERSION#*#"`): Verify the trailing `#` after the last wildcard triggers the same bug — the trailing bare-separator check should omit the length constraint while any non-trailing bare-separator segments keep it
  - Run test on UNFIXED code
  - **EXPECTED OUTCOME**: Test FAILS (this is correct — it proves the bug exists by showing the `< Length - 1` / `>= Length - 1` constraint is unconditionally emitted for trailing bare-separator segments)
  - Document counterexamples found: e.g., `"EMPLOYEE#*#"` generates `IndexOf("#", 10) < discriminatorValue.S.Length - 1` which rejects `"EMPLOYEE#abc123#"` (length 16, position 15, `15 < 15` → false)
  - Mark task complete when test is written, run, and failure is documented
  - _Requirements: 1.1, 1.2, 1.3, 2.1, 2.2, 2.3_

- [x] 2. Write preservation property tests (BEFORE implementing fix)
  - **Property 2: Preservation** - Non-Trailing Segments and Wildcard-Ending Patterns Unchanged
  - **IMPORTANT**: Follow observation-first methodology
  - **Test file**: `Oproto.FluentDynamoDb.SourceGenerator.UnitTests/TrailingBareSeparatorPreservationTests.cs`
  - **Pattern**: Follow the existing `ComplexPatternDiscriminationPreservationTests.cs` pattern — use `FsCheck.Xunit` `[Property]` attribute, invoke methods via reflection
  - **Observation-first methodology**: Run unfixed code with non-buggy inputs (patterns where `isBugCondition` returns false) and record outputs, then write property-based tests asserting those outputs
  - **Concrete observations on UNFIXED code**:
    - Observe: `GenerateComplexPatternCheck("INVOICE#*#LINE#*", "return")` produces `StartsWith("INVOICE#") && Contains("#LINE#")` — no bare-separator segments, ends with wildcard
    - Observe: `GenerateComplexPatternCheck("INVOICE#*#LINE#*", "negated")` produces `!StartsWith("INVOICE#") || !Contains("#LINE#")` — no bare-separator segments, ends with wildcard
    - Observe: `GenerateComplexExclusionCheck("INVOICE#*#LINE#*")` produces `StartsWith("INVOICE#") && Contains("#LINE#")` — wildcard-ending pattern
    - Observe: `GenerateComplexPatternCheck("ORDER#*", "return")` produces `StartsWith("ORDER#")` only — simple StartsWith strategy pattern
    - Observe: `GenerateComplexPatternCheck("*#SUFFIX#*", "return")` produces `Contains("#SUFFIX#")` — wildcard-first pattern
    - Observe: Non-trailing bare-separator in `"PREFIX#*#*#"` — the first `#` (non-trailing) keeps `IndexOf("#", offset) >= 0 && IndexOf("#", offset) < discriminatorValue.S.Length - 1` on unfixed code
  - **Property-based tests (50+ generated cases)**:
    - Property: For all Complex patterns ending with a wildcard (`pattern.EndsWith("*")`), the generated output from all three methods is byte-for-byte identical before and after fix — generate random patterns like `"PREFIX#*#SEGMENT#*"` with meaningful segments
    - Property: For all non-trailing bare-separator segments, `< Length - 1` (return/exclusion) and `>= Length - 1` (negated) constraints are preserved
    - Property: For wildcard-first patterns (`"*#SEGMENT#*"`), all segments use `Contains()` without `StartsWith` — unchanged behavior
    - Property: Simple single-wildcard patterns (`"PREFIX#*"`) produce only `StartsWith("PREFIX#")` with no `Contains` or `IndexOf`
  - Run tests on UNFIXED code
  - **EXPECTED OUTCOME**: Tests PASS (this confirms baseline behavior to preserve)
  - Mark task complete when tests are written, run, and passing on unfixed code
  - _Requirements: 3.1, 3.2, 3.3, 3.4, 3.5, 3.6_

- [x] 3. Fix for trailing bare-separator `< Length - 1` constraint in Complex pattern code generation

  - [x] 3.1 Implement the fix in `GenerateComplexPatternCheck` and `GenerateComplexExclusionCheck`
    - **File**: `Oproto.FluentDynamoDb.SourceGenerator/Generators/MapperGenerator.cs`
    - Add trailing bare-separator detection in the loop over `nonEmptySegments` (index > 0):
      ```csharp
      bool isLastSegment = (i == nonEmptySegments.Count - 1);
      bool patternEndsWithLiteral = !pattern.EndsWith("*");
      bool isTrailingBareSeparator = isLastSegment && patternEndsWithLiteral;
      ```
    - **`GenerateComplexPatternCheck` "return" mode** (~line 4755): When `isTrailingBareSeparator` is true, emit only `IndexOf(separator, offset) >= 0` without the `&& IndexOf(separator, offset) < discriminatorValue.S.Length - 1` part. When false, keep existing two-part condition unchanged
    - **`GenerateComplexPatternCheck` "negated" mode** (~line 4797): When `isTrailingBareSeparator` is true, emit only `IndexOf(separator, offset) < 0` without the `|| IndexOf(separator, offset) >= discriminatorValue.S.Length - 1` branch. When false, keep existing two-part condition unchanged
    - **`GenerateComplexExclusionCheck`** (~line 4845): When `isTrailingBareSeparator` is true, emit only `IndexOf(separator, offset) >= 0` without the `&& IndexOf(separator, offset) < discriminatorValue.S.Length - 1` part. When false, keep existing two-part condition unchanged
    - Update inline comments in all three locations to explain the trailing bare-separator distinction
    - Run `dotnet build-server shutdown` before building to clear cached source generator
    - Run `dotnet build` to verify no compilation errors
    - _Bug_Condition: isBugCondition(input) where pattern ends with trailing bare-separator literal after last wildcard_
    - _Expected_Behavior: Trailing bare-separator check uses only `IndexOf >= 0` (return/exclusion) or `IndexOf < 0` (negated) without length constraint_
    - _Preservation: Non-trailing bare-separator segments and wildcard-ending patterns retain existing `< Length - 1` / `>= Length - 1` constraints unchanged_
    - _Requirements: 1.1, 1.2, 1.3, 2.1, 2.2, 2.3, 2.4, 3.1, 3.2, 3.3, 3.4, 3.5, 3.6_

  - [x] 3.2 Verify bug condition exploration test now passes
    - **Property 1: Expected Behavior** - Trailing Bare-Separator Omits Length Constraint
    - **IMPORTANT**: Re-run the SAME test from task 1 - do NOT write a new test
    - The test from task 1 encodes the expected behavior (trailing bare-separator checks without length constraint)
    - When this test passes, it confirms: `"EMPLOYEE#*#"` generates `IndexOf("#", 10) >= 0` without `< Length - 1` in return mode, `IndexOf("#", 10) < 0` without `>= Length - 1` in negated mode, and exclusion mode similarly corrected
    - Run: `dotnet test --filter "FullyQualifiedName~TrailingBareSeparatorBugConditionTests" Oproto.FluentDynamoDb.SourceGenerator.UnitTests/`
    - **EXPECTED OUTCOME**: Test PASSES (confirms bug is fixed)
    - _Requirements: 2.1, 2.2, 2.3_

  - [x] 3.3 Verify preservation tests still pass
    - **Property 2: Preservation** - Non-Trailing Segments and Wildcard-Ending Patterns Unchanged
    - **IMPORTANT**: Re-run the SAME tests from task 2 - do NOT write new tests
    - Run: `dotnet test --filter "FullyQualifiedName~TrailingBareSeparatorPreservationTests" Oproto.FluentDynamoDb.SourceGenerator.UnitTests/`
    - **EXPECTED OUTCOME**: Tests PASS (confirms no regressions)
    - Confirm all preservation tests still pass after fix — wildcard-ending patterns, non-trailing bare-separator segments, wildcard-first patterns, simple StartsWith patterns all produce identical output
    - _Requirements: 3.1, 3.2, 3.3, 3.4, 3.5, 3.6_

- [x] 4. Checkpoint - Ensure all tests pass
  - Run `dotnet build-server shutdown` to clear any cached source generator state
  - Run full test suite: `dotnet test Oproto.FluentDynamoDb.SourceGenerator.UnitTests/`
  - Verify all bug condition exploration tests (task 1) now PASS
  - Verify all preservation tests (task 2) still PASS
  - Verify all existing tests (ComplexPatternDiscriminationPositiveBugConditionTests, ComplexPatternDiscriminationPreservationTests, WildcardOnePlusSemanticsBugConditionTests, WildcardOnePlusSemanticsPreservationTests, etc.) still PASS
  - Ensure all tests pass, ask the user if questions arise


## Notes

- The source generator caches in memory — always run `dotnet build-server shutdown` before building after modifying generator code
- Tests use reflection to invoke `GenerateComplexPatternCheck` and `GenerateComplexExclusionCheck` (both `private static`) via `BindingFlags.NonPublic | BindingFlags.Static`
- Test project: `Oproto.FluentDynamoDb.SourceGenerator.UnitTests/` — uses FsCheck, FsCheck.Xunit, xUnit, AwesomeAssertions
- Follow existing test patterns in `ComplexPatternDiscriminationPositiveBugConditionTests.cs` and `ComplexPatternDiscriminationPreservationTests.cs`
- The fix only affects bare-separator segments that are both the last non-empty segment AND the pattern does not end with `*`

## Task Dependency Graph

```json
{
  "waves": [
    {"tasks": ["1", "2"]},
    {"tasks": ["3.1"]},
    {"tasks": ["3.2", "3.3"]},
    {"tasks": ["4"]}
  ]
}
```
