# Implementation Plan: Local Method Evaluation in Update Expressions

## Overview

This implementation adds support for local method calls (like `.ToString()`, `.ToUpper()`, `.Trim()`) in update expressions when those method calls do not reference the entity parameter. The fix modifies `TranslateMethodCallWithPath()` to check if a method call references the entity parameter and, if not, evaluates it as a simple value assignment.

## Tasks

- [x] 1. Implement local method call detection and handling
  - [x] 1.1 Modify `TranslateMethodCallWithPath` to detect local method calls
    - Add check for `!ReferencesEntityParameter(methodCall, parameter)` after the list operation check
    - If the method doesn't reference the entity parameter, delegate to `TranslateSimpleSetWithPath()`
    - Place the check BEFORE the nested property check and the switch statement
    - _Requirements: US-1, US-2, US-3, US-4, US-5_

- [x] 2. Verify implementation with existing tests
  - [x] 2.1 Run enum ToString tests
    - Run `UpdateExpressionTranslatorEnumToStringTests`
    - Verify `TranslateUpdateExpression_EnumConstantToString_ShouldEvaluateAndCapture` passes
    - Verify `TranslateUpdateExpression_EnumVariableToString_ShouldEvaluateAndCapture` passes
    - _Requirements: US-1, US-2_

  - [x] 2.2 Run numeric and GUID ToString tests
    - Verify `TranslateUpdateExpression_IntToString_ShouldEvaluateAndCapture` passes
    - Verify `TranslateUpdateExpression_GuidToString_ShouldEvaluateAndCapture` passes
    - _Requirements: US-3, US-4_

  - [x] 2.3 Run chained method call test
    - Verify `TranslateUpdateExpression_ChainedMethodCalls_ShouldEvaluateAndCapture` passes
    - _Requirements: US-5_

- [x] 3. Checkpoint - Ensure new functionality works
  - Ensure all new tests pass, ask the user if questions arise.

- [x] 4. Run regression tests
  - [x] 4.1 Run full UpdateExpressionTranslator test suite
    - Run `dotnet test --filter "UpdateExpressionTranslator"`
    - Verify no existing tests fail
    - _Requirements: All_

  - [x] 4.2 Run full test suite
    - Run `dotnet test`
    - Verify no regressions across the codebase
    - _Requirements: All_

- [x] 5. Final checkpoint - All tests pass
  - Ensure all tests pass, ask the user if questions arise.

- [x] 6. Update changelog
  - [x] 6.1 Update CHANGELOG.md
    - Add entry documenting the fix for local method calls in update expressions
    - _Requirements: All_

## Notes

- All tasks are required for this bug fix
- Each task references specific requirements for traceability
- Checkpoints ensure incremental validation
- The fix leverages existing methods: `ReferencesEntityParameter()`, `TranslateSimpleSetWithPath()`, and `EvaluateExpression()`
- No new tests need to be written - existing tests in `UpdateExpressionTranslatorEnumToStringTests.cs` cover the functionality
