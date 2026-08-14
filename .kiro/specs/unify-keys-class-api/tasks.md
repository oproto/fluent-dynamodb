# Implementation Plan: Unify Keys Class API

## Overview

Unify the key construction API in the generated `Keys` class by eliminating the split between `Pk()`/`Sk()` and `BuildPk()`/`BuildSk()`. A single set of methods — `Pk()` and `Sk()` — will handle both prefix-based and computed key construction. The `Key()` composite method and useless passthrough methods are also removed. This involves modifying the source generator (`KeysGenerator.cs`, `TableGenerator.cs`), updating all tests, examples, and documentation.

## Tasks

- [x] 1. Modify KeysGenerator to unify Pk()/Sk() for computed keys
  - [x] 1.1 Modify `GeneratePartitionKeyBuilder` to handle `IsComputed` properties by generating multi-param `Pk(...)` with format string logic
    - _Requirements: 2.1, 2.2, 2.3, 2.4, 5.1, 5.2_
  - [x] 1.2 Modify `GenerateSortKeyBuilder` to handle `IsComputed` properties by generating multi-param `Sk(...)` with format string logic
    - _Requirements: 2.1, 2.2, 2.3, 2.4, 5.1, 5.2_
  - [x] 1.3 When property `IsComputed`, use source property names as parameter names and source property types as parameter types
    - _Requirements: 2.3, 2.4_
  - [x] 1.4 When property `IsComputed`, generate the format string application body (validation + `string.Format(...)`)
    - _Requirements: 2.1, 2.2_
  - [x] 1.5 Ensure parameter validation (null checks, whitespace checks, etc.) is preserved from existing `GenerateComputedKeyBuilder`
    - _Requirements: 2.1, 2.2_

- [x] 2. Remove BuildPk()/BuildSk() generation
  - [x] 2.1 Remove `GenerateComputedKeyBuilders` method from `KeysGenerator.cs`
    - _Requirements: 3.1, 3.2_
  - [x] 2.2 Remove `GenerateComputedKeyBuilder` method from `KeysGenerator.cs`
    - _Requirements: 3.1, 3.2_
  - [x] 2.3 Remove the call to `GenerateComputedKeyBuilders` from `GenerateKeysClass` and `GenerateNestedKeysClass`
    - _Requirements: 3.1, 3.2_

- [x] 3. Remove Key() composite method generation
  - [x] 3.1 Remove `GenerateCompositeKeyBuilder` method from `KeysGenerator.cs`
    - _Requirements: 4.1, 4.2_
  - [x] 3.2 Remove the call to `GenerateCompositeKeyBuilder` from `GenerateMainTableKeyBuilders`
    - _Requirements: 4.1, 4.2_

- [x] 4. Suppress passthrough methods for bare keys
  - [x] 4.1 In `GeneratePartitionKeyBuilder`, skip generation when property has no prefix and is not computed (and not constant)
    - _Requirements: 7.1, 7.2_
  - [x] 4.2 In `GenerateSortKeyBuilder`, skip generation when property has no prefix and is not computed (and not constant)
    - _Requirements: 7.1, 7.2_

- [x] 5. Update typed overload delegation in TableGenerator
  - [x] 5.1 Find all references to `Keys.BuildPk` and `Keys.BuildSk` in `TableGenerator.cs` typed overload generation
    - _Requirements: 10.1_
  - [x] 5.2 Replace with `Keys.Pk` and `Keys.Sk` respectively
    - _Requirements: 10.1_
  - [x] 5.3 Verify the generated typed Get/Delete/Update methods delegate correctly
    - _Requirements: 10.1_

- [x] 6. Update GSI key builder generation
  - [x] 6.1 Review `GenerateGsiKeyBuilderClasses` / `GenerateGsiKeyBuilderClass` for any computed key handling that uses "Build" prefix
    - _Requirements: 8.1_
  - [x] 6.2 Apply same unification pattern to GSI key builders
    - _Requirements: 8.1_

- [x] 7. Checkpoint - Ensure generator changes compile
  - Ensure all generator changes compile cleanly, ask the user if questions arise.

- [x] 8. Update unit tests for KeysGenerator
  - [x] 8.1 Update `KeysGeneratorTests.cs` — assertions for `BuildPk`/`BuildSk` → `Pk`/`Sk`
    - _Requirements: 2.1, 2.2, 3.1_
  - [x] 8.2 Update `KeysGenerator_FormatSpecifierTests.cs` — same pattern
    - _Requirements: 2.1, 2.2_
  - [x] 8.3 Update `KeysGenerator_FormatSpecifierPropertyTests.cs` — same pattern
    - _Requirements: 2.1, 2.2_
  - [x] 8.4 Update `ConstantKeyKeysGeneratorPropertyTests.cs` — remove assertions about `Key()` method
    - _Requirements: 4.1_
  - [x] 8.5 Remove or update any tests asserting passthrough `Sk(string)` behavior for computed keys
    - _Requirements: 5.1_
  - [x] 8.6 Update `DelegationToKeysBuildPropertyTests.cs` — change `BuildPk`/`BuildSk` references to `Pk`/`Sk`
    - _Requirements: 10.1_

- [x] 9. Update integration and API consistency tests
  - [x] 9.1 Update `ComputedKeyTypedOverloadEquivalenceTests.cs` — `Keys.BuildSk(...)` → `Keys.Sk(...)`
    - _Requirements: 10.1, 11.2_
  - [x] 9.2 Update `ComputedKeyPathEquivalencePropertyTests.cs` — same
    - _Requirements: 10.1, 11.2_
  - [x] 9.3 Update `ComputedKeyTypedOverloadsApiSurface.cs` — same
    - _Requirements: 10.1, 11.2_
  - [x] 9.4 Update any other integration tests referencing `BuildPk`/`BuildSk`
    - _Requirements: 11.2_

- [x] 10. Update example projects and tests
  - [x] 10.1 Update `InvoicePropertyTests.cs` — `InvoiceLine.Keys.BuildSk(...)` → `InvoiceLine.Keys.Sk(...)`
    - _Requirements: 11.1, 11.4_
  - [x] 10.2 Verify `Invoice.Keys.Sk(invoiceNumber)` in tests now produces correct value (no code change needed, behavior fixed)
    - _Requirements: 11.4_
  - [x] 10.3 Update `Program.cs` in InvoiceManager if any `BuildSk` references exist
    - _Requirements: 11.1_
  - [x] 10.4 Search all example projects for `BuildPk`/`BuildSk` usage and update
    - _Requirements: 11.1_

- [x] 11. Update documentation and steering files
  - [x] 11.1 Update `.kiro/steering/fluentdynamodb.md` — remove `BuildPk`/`BuildSk` references, update `Keys.Key()` examples
    - _Requirements: 11.3_
  - [x] 11.2 Update `.kiro/steering/entity-patterns.md` — update key construction patterns
    - _Requirements: 11.3_
  - [x] 11.3 Update `docs/core-features/BasicOperations.md` — remove `Key()` examples
    - _Requirements: 11.3_
  - [x] 11.4 Update `docs/core-features/ConstantKeyDetection.md` — remove `Key()` examples
    - _Requirements: 11.3_
  - [x] 11.5 Update `CHANGELOG.md` — add breaking change entry
    - _Requirements: 11.3_
  - [x] 11.6 Update `DISCUSSION_whats_new_since_1.0.7.md` — update Keys class documentation
    - _Requirements: 11.3_

- [x] 12. Final checkpoint - Build and verify
  - [x] 12.1 Run `dotnet build-server shutdown` to clear cached source generator
    - _Requirements: all_
  - [x] 12.2 Run `dotnet build` across the full solution and fix any remaining compile errors
    - _Requirements: all_
  - [x] 12.3 Run `dotnet test` on unit test projects (excluding property tests requiring DynamoDB Local)
    - _Requirements: all_
  - [x] 12.4 Regenerate InvoiceManager inspect output and verify Keys class looks correct
    - _Requirements: all_

## Notes

- This is a breaking change for consumers — `BuildPk()`/`BuildSk()`/`Key()` are removed
- The source generator caches in memory; run `dotnet build-server shutdown` before testing generator changes
- Tasks 1-6 modify the source generator and must be completed before test updates
- Property-based tests validate correctness properties defined in the design document
- All sub-tasks within a parent task that modify the same file should be executed sequentially
- After Task 7 checkpoint, all generator logic should compile — remaining tasks are test/doc updates

## Task Dependency Graph

```json
{
  "waves": [
    { "id": 0, "tasks": ["1.1", "1.2"] },
    { "id": 1, "tasks": ["1.3", "1.4", "1.5"] },
    { "id": 2, "tasks": ["2.1", "2.2", "3.1"] },
    { "id": 3, "tasks": ["2.3", "3.2", "4.1", "4.2"] },
    { "id": 4, "tasks": ["5.1", "6.1"] },
    { "id": 5, "tasks": ["5.2", "5.3", "6.2"] },
    { "id": 6, "tasks": ["8.1", "8.2", "8.3", "9.1"] },
    { "id": 7, "tasks": ["8.4", "8.5", "8.6", "9.2", "9.3", "9.4"] },
    { "id": 8, "tasks": ["10.1", "10.2", "10.3", "10.4"] },
    { "id": 9, "tasks": ["11.1", "11.2", "11.3", "11.4", "11.5", "11.6"] },
    { "id": 10, "tasks": ["12.1", "12.2", "12.3", "12.4"] }
  ]
}
```
