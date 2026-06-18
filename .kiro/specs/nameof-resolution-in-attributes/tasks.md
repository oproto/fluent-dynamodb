# Implementation Plan

## Overview

This implementation plan follows the exploratory bugfix workflow to fix `nameof()` and compile-time constant resolution in `[Computed]` and `[Extracted]` attribute arguments within `EntityAnalyzer.cs`. The fix uses `semanticModel.GetConstantValue()` as a fallback when the expression isn't a `LiteralExpressionSyntax`.

## Tasks

- [x] 1. Write bug condition exploration test
  - **Property 1: Bug Condition** - nameof() and Const Expression Resolution
  - **CRITICAL**: This test MUST FAIL on unfixed code - failure confirms the bug exists
  - **DO NOT attempt to fix the test or the code when it fails**
  - **NOTE**: This test encodes the expected behavior - it will validate the fix when it passes after implementation
  - **GOAL**: Surface counterexamples that demonstrate the bug exists in `ExtractComputedKeyAttributes` and `ExtractExtractedKeyAttributes`
  - **Scoped PBT Approach**: Scope the property to concrete failing cases: `nameof()` expressions and `const` variables as positional arguments in `[Computed]` and `[Extracted]` attributes
  - Create test file `Oproto.FluentDynamoDb.SourceGenerator.UnitTests/Analysis/NameofResolutionBugConditionPropertyTests.cs`
  - Use FsCheck with xUnit (`[Property]` attribute) following existing test patterns in the Analysis folder
  - Test 1: Entity with `[Computed(nameof(UserId), Format = "USER#{0}")]` — assert `ComputedKey.SourceProperties` contains `"UserId"`
  - Test 2: Entity with `[Extracted(nameof(Pk), 0)]` — assert `ExtractedKey.SourceProperty == "Pk"`
  - Test 3: Entity with `[Computed(nameof(Year), nameof(Month), Separator = "#")]` — assert both source properties resolve
  - Test 4: Entity with `const string Source = "Pk"; [Extracted(Source, 0)]` — assert `SourceProperty == "Pk"`
  - Test 5: Entity with `const int Idx = 1; [Extracted("Pk", Idx)]` — assert `Index == 1`
  - Use `TestHelpers` and compile source code via Roslyn CSharpCompilation (same pattern as `IndexAttributeExtractionPropertyTests`)
  - Run test on UNFIXED code
  - **EXPECTED OUTCOME**: Tests FAIL (this is correct - it proves the bug exists: `LiteralExpressionSyntax` pattern match silently skips non-literal expressions)
  - Document counterexamples: `SourceProperties` is empty array when `nameof()` used; `SourceProperty` is empty string when `nameof()` used; `Index` is 0 when `const int` used
  - Mark task complete when tests are written, run, and failure is documented
  - _Requirements: 1.1, 1.2, 1.3, 1.4_

- [x] 2. Write preservation property tests (BEFORE implementing fix)
  - **Property 2: Preservation** - String Literal and Integer Literal Behavior
  - **IMPORTANT**: Follow observation-first methodology
  - Create test file `Oproto.FluentDynamoDb.SourceGenerator.UnitTests/Analysis/NameofResolutionPreservationPropertyTests.cs`
  - Use FsCheck with xUnit (`[Property]` attribute) following existing test patterns
  - Observe on UNFIXED code: `[Computed("UserId", Format = "USER#{0}")]` produces `SourceProperties = ["UserId"]`
  - Observe on UNFIXED code: `[Computed("Year", "Month", Separator = "#")]` produces `SourceProperties = ["Year", "Month"]`
  - Observe on UNFIXED code: `[Extracted("Pk", 0)]` produces `SourceProperty = "Pk"` and `Index = 0`
  - Observe on UNFIXED code: `[Extracted("Pk", 2)]` produces `Index = 2`
  - Observe on UNFIXED code: `[Computed("UserId", Format = "USER#{0}")]` correctly extracts `Format` named argument
  - Observe on UNFIXED code: `[Computed("A", "B", Separator = "#")]` correctly extracts `Separator` named argument
  - Write property-based tests: for all valid C# identifier strings used as string literal positional args in `[Computed]`, `SourceProperties` contains those values
  - Write property-based tests: for all non-negative integer literals used as index in `[Extracted]`, `Index` equals that value
  - Write property-based tests: for all valid string literals as first arg in `[Extracted]`, `SourceProperty` equals that value
  - Verify tests PASS on UNFIXED code
  - **EXPECTED OUTCOME**: Tests PASS (this confirms baseline literal behavior to preserve)
  - Mark task complete when tests are written, run, and passing on unfixed code
  - _Requirements: 3.1, 3.2, 3.3, 3.4_

- [x] 3. Fix nameof() and compile-time constant resolution in EntityAnalyzer

  - [x] 3.1 Implement the fix in `ExtractComputedKeyAttributes`
    - In `Oproto.FluentDynamoDb.SourceGenerator/Analysis/EntityAnalyzer.cs`, method `ExtractComputedKeyAttributes`
    - Modify the positional argument loop: after the existing `arg.Expression is LiteralExpressionSyntax literal` check, add an `else` branch
    - In the else branch, call `semanticModel.GetConstantValue(arg.Expression)`
    - If `constantValue.HasValue && constantValue.Value is string strValue`, add `strValue` to `sourceProperties`
    - Keep the existing `LiteralExpressionSyntax` check as the primary fast-path for efficiency
    - _Bug_Condition: isBugCondition(input) where input.Expression is NOT LiteralExpressionSyntax AND is a compile-time constant_
    - _Expected_Behavior: resolve to compile-time string value and include in SourceProperties array_
    - _Preservation: String literal positional arguments continue to use LiteralExpressionSyntax fast-path unchanged_
    - _Requirements: 2.1, 2.2, 2.4_

  - [x] 3.2 Implement the fix in `ExtractExtractedKeyAttributes` (source property)
    - In `Oproto.FluentDynamoDb.SourceGenerator/Analysis/EntityAnalyzer.cs`, method `ExtractExtractedKeyAttributes`
    - After the existing `args[0].Expression is LiteralExpressionSyntax sourcePropertyLiteral` check, add an `else` branch
    - In the else branch, call `semanticModel.GetConstantValue(args[0].Expression)`
    - If `constantValue.HasValue && constantValue.Value is string strValue`, assign `extractedModel.SourceProperty = strValue`
    - _Bug_Condition: isBugCondition(input) where args[0].Expression is NOT LiteralExpressionSyntax AND is a compile-time constant_
    - _Expected_Behavior: resolve to compile-time string value and assign to SourceProperty_
    - _Preservation: String literal first arguments continue to use LiteralExpressionSyntax fast-path unchanged_
    - _Requirements: 2.3, 2.4_

  - [x] 3.3 Implement the fix in `ExtractExtractedKeyAttributes` (index)
    - In the same method, after the existing `args[1].Expression is LiteralExpressionSyntax indexLiteral` check, add an `else` branch
    - In the else branch, call `semanticModel.GetConstantValue(args[1].Expression)`
    - If `constantValue.HasValue && constantValue.Value is int intValue`, assign `extractedModel.Index = intValue`
    - Also handle other integer types: use `Convert.ToInt32(constantValue.Value)` wrapped in a try-catch or check `constantValue.Value is int or short or byte`
    - _Bug_Condition: isBugCondition(input) where args[1].Expression is NOT LiteralExpressionSyntax AND is a compile-time constant integer_
    - _Expected_Behavior: resolve to compile-time integer value and assign to Index_
    - _Preservation: Integer literal second arguments continue to use LiteralExpressionSyntax fast-path unchanged_
    - _Requirements: 2.4_

  - [x] 3.4 Verify bug condition exploration test now passes
    - **Property 1: Expected Behavior** - nameof() and Const Expression Resolution
    - **IMPORTANT**: Re-run the SAME test from task 1 - do NOT write a new test
    - The test from task 1 encodes the expected behavior (resolved nameof values in SourceProperties/SourceProperty/Index)
    - When this test passes, it confirms the expected behavior is satisfied
    - Run bug condition exploration test from step 1
    - **EXPECTED OUTCOME**: Test PASSES (confirms bug is fixed)
    - _Requirements: 2.1, 2.2, 2.3, 2.4_

  - [x] 3.5 Verify preservation tests still pass
    - **Property 2: Preservation** - String Literal and Integer Literal Behavior
    - **IMPORTANT**: Re-run the SAME tests from task 2 - do NOT write new tests
    - Run preservation property tests from step 2
    - **EXPECTED OUTCOME**: Tests PASS (confirms no regressions to string/int literal handling)
    - Confirm all tests still pass after fix (no regressions)
    - _Requirements: 3.1, 3.2, 3.3, 3.4_

- [x] 4. Checkpoint - Ensure all tests pass
  - Run `dotnet test` on the `Oproto.FluentDynamoDb.SourceGenerator.UnitTests` project
  - Ensure all property-based tests pass (both bug condition and preservation)
  - Ensure no existing tests in the project are broken by the changes
  - Ask the user if questions arise

## Task Dependency Graph

```json
{
  "waves": [
    {
      "wave": 1,
      "tasks": ["1", "2"],
      "description": "Write exploration and preservation property-based tests BEFORE implementing fix"
    },
    {
      "wave": 2,
      "tasks": ["3.1", "3.2", "3.3"],
      "description": "Implement the semantic model fallback fix in both methods"
    },
    {
      "wave": 3,
      "tasks": ["3.4", "3.5"],
      "description": "Re-run tests to verify fix works and no regressions"
    },
    {
      "wave": 4,
      "tasks": ["4"],
      "description": "Final checkpoint - ensure full test suite passes"
    }
  ]
}
```

## Notes

- Tasks 1 and 2 are standalone property-based test tasks that MUST be completed BEFORE the implementation in task 3
- Task 1 (bug condition) is expected to FAIL on unfixed code — this confirms the bug exists
- Task 2 (preservation) is expected to PASS on unfixed code — this captures baseline behavior
- After implementing task 3.1–3.3, re-running tests in 3.4 and 3.5 validates both the fix and preservation
- The test project uses FsCheck + xUnit for property-based testing with `[Property]` attribute
- The source generator test project is `Oproto.FluentDynamoDb.SourceGenerator.UnitTests`
- Existing test patterns compile source via Roslyn and run `EntityAnalyzer.AnalyzeEntity()` directly
