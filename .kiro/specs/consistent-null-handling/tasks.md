# Implementation Plan: Consistent Null Handling

## Overview

This plan implements consistent null handling in update expressions by adding a `NoUpdate()` extension method and removing the special null-skip behavior in conditionals.

## Tasks

- [x] 1. Add NoUpdate extension method
  - [x] 1.1 Add `NoUpdate<T>()` method to UpdateExpressionPropertyExtensions
    - Add method with `[ExpressionOnly]` attribute
    - Throw `InvalidOperationException` with descriptive message
    - Add XML documentation with examples
    - _Requirements: 2.1, 2.6, 4.1, 4.2_

  - [x] 1.2 Write unit tests for NoUpdate extension method
    - Test method exists via reflection
    - Test direct call throws InvalidOperationException
    - Test error message contains expected text
    - _Requirements: 2.6, 4.1, 4.2_

- [x] 2. Update translator to detect NoUpdate
  - [x] 2.1 Add `IsNoUpdateMethodCall` helper method to UpdateExpressionTranslator
    - Check method name is "NoUpdate"
    - Check declaring type is UpdateExpressionPropertyExtensions
    - _Requirements: 2.2_

  - [x] 2.2 Update `ClassifyOperationWithPath` to handle NoUpdate
    - Add check for NoUpdate before other method calls
    - Return `OperationType.Skip` when NoUpdate detected
    - _Requirements: 2.2, 2.3, 2.4_

  - [x] 2.3 Write property test for NoUpdate skipping
    - **Property 2: NoUpdate Skips Property**
    - **Validates: Requirements 2.2**

- [x] 3. Remove special null handling in conditionals
  - [x] 3.1 Remove `IsNullExpression` skip logic from `HandleConditionalUpdateWithPath`
    - Remove the if block that checks for null in false branch
    - Let null values flow through to TranslateSimpleSet
    - _Requirements: 1.3, 3.1_

  - [x] 3.2 Write property test for null consistency
    - **Property 1: Null Consistency**
    - **Validates: Requirements 1.1, 1.2, 1.3, 1.4, 3.1**

- [x] 4. Checkpoint - Ensure all tests pass
  - Ensure all tests pass, ask the user if questions arise.

- [x] 5. Update existing tests
  - [x] 5.1 Update `UpdateExpressionTranslatorConditionalTests` 
    - Change tests that expected skip-on-null to expect SET NULL
    - Add new tests for NoUpdate() behavior
    - _Requirements: 1.3, 2.2, 2.3, 2.4_

  - [x] 5.2 Update `UpdateExpressionTranslatorConditionalPropertyTests`
    - Update property tests that relied on null-skip behavior
    - Add property tests for NoUpdate in conditionals
    - _Requirements: 2.3, 2.4_

- [x] 6. Write property test for NoUpdate in conditionals
  - **Property 3: NoUpdate in Conditionals**
  - **Validates: Requirements 2.3, 2.4**

- [x] 7. Write property test for NoUpdate type coverage
  - **Property 4: NoUpdate Works for All Types**
  - **Validates: Requirements 2.5**

- [x] 8. Update documentation
  - [x] 8.1 Update fluentdynamodb.md steering file
    - Document NoUpdate() method
    - Document null behavior (SET NULL)
    - Add migration note for breaking change
    - _Requirements: 3.2, 3.3_

  - [x] 8.2 Update docs/ documentation
    - Update relevant docs/ files with NoUpdate() usage
    - Document null vs NoUpdate() vs Remove() distinction
    - _Requirements: 3.2, 3.3_

  - [x] 8.3 Update docs/DOCUMENTATION_CHANGELOG.md
    - Add entry for null handling behavior change
    - Document before/after patterns for documentation sync
    - _Requirements: 3.3_

  - [x] 8.4 Add CHANGELOG entry
    - Document breaking change
    - Provide migration guidance
    - _Requirements: 3.2, 3.3_

  - [x] 8.5 Update RELEASE_NOTES.md 1.0 breaking changes
    - Add null handling change to breaking changes section
    - Document migration from `flag ? value : null` to `flag ? value : x.Property.NoUpdate()`
    - _Requirements: 3.2, 3.3_

- [x] 9. Final checkpoint - Ensure all tests pass
  - Ensure all tests pass, ask the user if questions arise.

## Notes

- This is a breaking change - existing code using `flag ? value : null` for skipping will now SET NULL
- Migration path: replace `null` with `x.Property.NoUpdate()` for skip behavior
