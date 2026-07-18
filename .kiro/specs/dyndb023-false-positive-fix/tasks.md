# Implementation Plan: DYNDB023 False Positive Fix

## Overview

Fix false positive DYNDB023 diagnostics by adding early-exit guards to `ValidatePropertyPerformance` for unmapped, extracted, and enum properties, and removing the duplicate call to `ValidatePropertyPerformance` from the outer validation loop in `EntityAnalyzer.cs`.

## Tasks

- [x] 1. Add early-exit guards to ValidatePropertyPerformance
  - [x] 1.1 Add guard for unmapped properties (no DynamoDbAttribute)
    - In `Oproto.FluentDynamoDb.SourceGenerator/Analysis/EntityAnalyzer.cs`, add an early return at the top of `ValidatePropertyPerformance` when `propertyModel.HasAttributeMapping` is false (or `string.IsNullOrEmpty(propertyModel.AttributeName)`)
    - This prevents DYNDB023 from firing on properties not mapped to DynamoDB
    - _Requirements: 1.1_

  - [x] 1.2 Add guard for extracted properties
    - Add an early return when `propertyModel.IsExtracted` is true
    - Place after the unmapped guard
    - This prevents DYNDB023 from firing on source-only properties populated from computed keys
    - _Requirements: 1.2_

  - [x] 1.3 Add guard for enum properties
    - Add an early return when `propertyModel.IsEnum` is true
    - Place after the extracted guard, before the existing `IsRelatedEntity` check
    - This prevents DYNDB023 from firing on simple value types stored as string/int
    - _Requirements: 1.3_

- [x] 2. Remove duplicate ValidatePropertyPerformance call
  - [x] 2.1 Remove the duplicate call from the outer validation loop
    - In `EntityAnalyzer.cs` at approximately line 77, remove the `ValidatePropertyPerformance(property)` call from the outer `foreach` loop
    - `ValidatePropertyPerformance` is already called internally by `ValidatePropertyModel`, so the outer call produces duplicate diagnostics
    - _Requirements: 2.1, 2.2_

- [x] 3. Checkpoint - Verify build passes
  - Ensure `dotnet build` succeeds with no errors, ask the user if questions arise.

- [x] 4. Write unit tests for DYNDB023 false positive fix
  - [x] 4.1 Create test file and test helpers for ValidatePropertyPerformance guards
    - Create `Oproto.FluentDynamoDb.UnitTests/SourceGenerator/Dyndb023FalsePositiveTests.cs`
    - Add test helper methods to construct `PropertyModel` instances with various configurations
    - _Requirements: 1.1, 1.2, 1.3, 1.4_

  - [ ]* 4.2 Write property test: unmapped properties never produce DYNDB023
    - **Property 1: Unmapped properties never produce DYNDB023**
    - For any PropertyModel where HasAttributeMapping is false, verify zero DYNDB023 diagnostics are produced regardless of type, extraction status, or enum status
    - **Validates: Requirement 1.1**

  - [ ]* 4.3 Write property test: extracted properties never produce DYNDB023
    - **Property 2: Extracted properties never produce DYNDB023**
    - For any PropertyModel where IsExtracted is true, verify zero DYNDB023 diagnostics are produced regardless of type or mapping status
    - **Validates: Requirement 1.2**

  - [ ]* 4.4 Write property test: enum properties never produce DYNDB023
    - **Property 3: Enum properties never produce DYNDB023**
    - For any PropertyModel where IsEnum is true, verify zero DYNDB023 diagnostics are produced regardless of mapping status or other attributes
    - **Validates: Requirement 1.3**

  - [ ]* 4.5 Write property test: legitimate complex types still produce DYNDB023
    - **Property 4: Legitimate complex types still produce DYNDB023**
    - For any PropertyModel where HasAttributeMapping is true, IsExtracted is false, IsEnum is false, IsRelatedEntity is false, and the type passes IsComplexNestedType, verify exactly one DYNDB023 diagnostic is produced
    - **Validates: Requirement 1.4**

  - [ ]* 4.6 Write property test: no duplicate diagnostics per property
    - **Property 5: No duplicate diagnostics per property**
    - For any entity model processed by the analyzer, verify each property that qualifies for DYNDB023 has exactly one diagnostic reported (no duplicates)
    - **Validates: Requirements 2.1, 2.2**

- [x] 5. Write regression unit tests for legitimate complex types
  - [x] 5.1 Write unit tests verifying DYNDB023 still fires for mapped complex types
    - Test that a property with `[DynamoDbAttribute]`, non-enum, non-extracted, non-related-entity, and a complex user-defined type still triggers DYNDB023
    - Test with various complex type patterns (nested classes, custom types)
    - _Requirements: 1.4_

  - [ ]* 5.2 Write unit tests verifying no duplicate diagnostics after fix
    - Test that processing an entity with a legitimate complex type produces exactly one DYNDB023 diagnostic (not two from the duplicate call)
    - _Requirements: 2.1, 2.2_

- [x] 6. Final checkpoint - Ensure all tests pass
  - Ensure `dotnet build` and `dotnet test` succeed with no errors, ask the user if questions arise.

## Notes

- Tasks marked with `*` are optional and can be skipped for faster MVP
- Each task references specific requirements for traceability
- Checkpoints ensure incremental validation
- Property tests validate universal correctness properties from the design document
- Unit tests validate specific examples and edge cases
- The source file is `Oproto.FluentDynamoDb.SourceGenerator/Analysis/EntityAnalyzer.cs`
- New test file goes in `Oproto.FluentDynamoDb.UnitTests/SourceGenerator/Dyndb023FalsePositiveTests.cs`
- After modifying the source generator, run `dotnet build-server shutdown` before rebuilding to clear cached generator

## Task Dependency Graph

```json
{
  "waves": [
    { "id": 0, "tasks": ["1.1"] },
    { "id": 1, "tasks": ["1.2"] },
    { "id": 2, "tasks": ["1.3"] },
    { "id": 3, "tasks": ["2.1"] },
    { "id": 4, "tasks": ["4.1"] },
    { "id": 5, "tasks": ["4.2", "4.3", "4.4", "4.5", "5.1"] },
    { "id": 6, "tasks": ["4.6", "5.2"] }
  ]
}
```
