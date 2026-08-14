# Implementation Plan: Typed Async Convenience Methods

## Overview

Add typed async convenience methods (`GetAsync`, `DeleteAsync`, `GetAsyncResult`, `DeleteAsyncResult`) to the source generator for computed key entities. All changes are in `TableGenerator.cs`, with new API surface tests in `ApiConsistencyTests` and property-based tests using FsCheck in the unit test project. The implementation follows the same eligibility/ambiguity patterns as existing typed builder overloads.

## Tasks

- [x] 1. Implement entity-accessor typed GetAsync and DeleteAsync methods
  - [x] 1.1 Add `GenerateTypedGetAsyncMethod` private method to `TableGenerator.cs`
    - Add method immediately after `GenerateTypedGetOverload` (around line 1177)
    - Reuse `ComputedOverloadEligibility.QualifiesForTypedOverload`, `WouldBeAmbiguous`, and `OverloadParameterResolver.GetTypedOverloadParameters` for eligibility gating
    - Generate `public async Task<T?> GetAsync(...)` that delegates to `Get(...).GetItemAsync(cancellationToken)`
    - Include `CancellationToken cancellationToken = default` as trailing parameter
    - Include XML doc comments matching existing patterns
    - Call this method from `GenerateAccessorGetMethod` at the same location where `GenerateTypedGetOverload` is called
    - Respect `HideGeneratedAsyncMethods` flag: skip generation when `entity.UseFluentResults == true && entity.HideGeneratedAsyncMethods == true`
    - _Requirements: 1.1, 1.2, 1.3, 1.4, 1.5_

  - [x] 1.2 Add `GenerateTypedDeleteAsyncMethod` private method to `TableGenerator.cs`
    - Add method immediately after `GenerateTypedGetAsyncMethod`
    - Same eligibility checks as GetAsync
    - Generate `public async Task DeleteAsync(...)` that delegates to `Delete(...)` builder, conditionally applies `.WithKeyCondition(keyCondition)` when not `KeyCondition.None`, then calls `.DeleteAsync(cancellationToken)`
    - Parameter list: typed params + `KeyCondition keyCondition = KeyCondition.None` + `CancellationToken cancellationToken = default`
    - Call this method from `GenerateAccessorDeleteMethod` at the same location where `GenerateTypedDeleteOverload` is called
    - Respect `HideGeneratedAsyncMethods` flag same as GetAsync
    - _Requirements: 2.1, 2.2, 2.3, 2.4, 2.5_

  - [x] 1.3 Write property test: Eligible entities produce typed async methods (Property 1)
    - **Property 1: Eligible entities produce typed async methods with correct signatures**
    - **Validates: Requirements 1.1, 1.3, 2.1, 2.3**
    - Create `Oproto.FluentDynamoDb.UnitTests/SourceGenerator/TypedAsyncConveniencePropertyTests.cs`
    - Use FsCheck to generate random `EntityModel` instances with computed keys (2+ source properties), non-ambiguous types
    - Run `TableGenerator.GenerateTableClass` and assert output contains `GetAsync` and `DeleteAsync` methods with correct return types and parameter lists

  - [x] 1.4 Write property test: Ineligible entities produce no typed async methods (Property 2)
    - **Property 2: Ineligible or ambiguous entities produce no typed async methods**
    - **Validates: Requirements 1.4, 1.5, 2.4, 2.5**
    - In same file `TypedAsyncConveniencePropertyTests.cs`
    - Use FsCheck to generate `EntityModel` instances that fail eligibility (no computed key, single source property, ambiguous types)
    - Assert generated output does NOT contain typed-parameter `GetAsync` or `DeleteAsync` signatures

- [x] 2. Implement entity-accessor FluentResults typed async methods
  - [x] 2.1 Add `GenerateTypedGetAsyncResultMethod` private method to `TableGenerator.cs`
    - Generate only when `entity.UseFluentResults == true`
    - Generate `public Task<Result<T?>> GetAsyncResult(...)` that delegates to `Get(...).GetItemAsyncResult(cancellationToken)`
    - Same typed parameter list as `GetAsync` plus `CancellationToken cancellationToken = default`
    - Use expression-body syntax for the delegation (no async/await needed since it just returns the Task)
    - Call from `GenerateAccessorGetMethod` after typed GetAsync generation, gated on `UseFluentResults`
    - _Requirements: 5.1, 5.2, 5.3, 5.4, 5.5_

  - [x] 2.2 Add `GenerateTypedDeleteAsyncResultMethod` private method to `TableGenerator.cs`
    - Generate only when `entity.UseFluentResults == true`
    - Generate `public Task<Result> DeleteAsyncResult(...)` with typed params + `KeyCondition` + `CancellationToken`
    - Delegate to `Delete(...)` builder, conditionally apply `.WithKeyCondition(keyCondition)`, call `.DeleteAsyncResult(cancellationToken)`
    - Call from `GenerateAccessorDeleteMethod` after typed DeleteAsync generation, gated on `UseFluentResults`
    - _Requirements: 6.1, 6.2, 6.3, 6.4, 6.5, 6.6_

  - [x] 2.3 Write property test: FluentResults entities produce typed Result variants (Property 5)
    - **Property 5: FluentResults-enabled entities produce typed Result variants**
    - **Validates: Requirements 5.1, 5.3, 6.1, 6.4**
    - In `TypedAsyncConveniencePropertyTests.cs`
    - Generate `EntityModel` instances with `UseFluentResults = true` and qualifying computed keys
    - Assert output contains `GetAsyncResult` and `DeleteAsyncResult` with correct `Result<T?>` and `Result` return types

- [x] 3. Implement table-level typed async methods
  - [x] 3.1 Add `GenerateTableLevelTypedGetAsyncMethod` and `GenerateTableLevelTypedDeleteAsyncMethod` to `TableGenerator.cs`
    - Add after existing `GenerateTableLevelTypedGetOverload` / `GenerateTableLevelTypedDeleteOverload` methods
    - Generate expression-body methods that delegate to `{entityPropertyName}.GetAsync(...)` and `{entityPropertyName}.DeleteAsync(...)`
    - Call from `GenerateTableLevelGetMethod` and `GenerateTableLevelDeleteMethod` respectively
    - Respect `HideGeneratedAsyncMethods` flag
    - _Requirements: 3.1, 3.2, 3.3, 3.4, 4.1, 4.2, 4.3, 4.4_

  - [x] 3.2 Add `GenerateTableLevelTypedGetAsyncResultMethod` and `GenerateTableLevelTypedDeleteAsyncResultMethod` to `TableGenerator.cs`
    - Generate only when `entity.UseFluentResults == true`
    - Expression-body delegation to `{entityPropertyName}.GetAsyncResult(...)` and `{entityPropertyName}.DeleteAsyncResult(...)`
    - Call from `GenerateTableLevelGetMethod` and `GenerateTableLevelDeleteMethod` gated on `UseFluentResults`
    - _Requirements: 7.1, 7.2, 7.3, 7.4, 7.5, 7.6_

  - [x] 3.3 Write property test: Single-entity tables produce table-level typed async methods (Property 4)
    - **Property 4: Single-entity tables produce table-level typed async methods that delegate to accessor**
    - **Validates: Requirements 3.1, 3.2, 3.3, 4.1, 4.2, 4.3**
    - In `TypedAsyncConveniencePropertyTests.cs`
    - Generate single-entity table configurations with qualifying entities
    - Assert table class output contains table-level `GetAsync` and `DeleteAsync` that reference the entity accessor

  - [x] 3.4 Write property test: Single-entity FluentResults tables produce table-level Result variants (Property 6)
    - **Property 6: Single-entity FluentResults tables produce table-level Result variants**
    - **Validates: Requirements 7.1, 7.2, 7.3, 7.4**
    - In `TypedAsyncConveniencePropertyTests.cs`
    - Generate single-entity table with `UseFluentResults = true` and qualifying computed keys
    - Assert table class contains `GetAsyncResult` and `DeleteAsyncResult` at table level

- [x] 4. Checkpoint - Verify source generator builds and emits correct methods
  - Ensure all tests pass, ask the user if questions arise.
  - Run `dotnet build-server shutdown` then `dotnet build` to validate the source generator compiles
  - Run `dotnet test --filter "FullyQualifiedName~TypedAsyncConvenience"` to verify property tests pass

- [x] 5. Add API consistency tests for typed async methods
  - [x] 5.1 Create test entity with computed key + `[UseFluentResults]` in `ApiConsistencyTests/Entities/`
    - Create `ComputedKeyFluentResultsTable.cs` with a computed PK entity that also has `[UseFluentResults]`
    - Reuse similar pattern to `ComputedKeyEntity` but add `[UseFluentResults]` attribute
    - Also create a variant with `[UseFluentResults(HideGeneratedAsyncMethods = false)]` to test both-modes
    - _Requirements: 5.1, 6.1, 7.1_

  - [x] 5.2 Add typed async API surface tests to `ComputedKeyTypedOverloadsApiSurface.cs`
    - Add test method `TypedOverloads_ComputedKeyEntity_GetAsync_ShouldCompile` verifying `table.ComputedKeyEntitys.GetAsync(2024, 12, 25, "sk")` compiles and returns `Task<ComputedKeyEntity?>`
    - Add test method `TypedOverloads_ComputedKeyEntity_DeleteAsync_ShouldCompile` verifying `table.ComputedKeyEntitys.DeleteAsync(2024, 12, 25, "sk", KeyCondition.None)` compiles and returns `Task`
    - Add test for table-level: `table.GetAsync(2024, 12, 25, "sk")` and `table.DeleteAsync(2024, 12, 25, "sk")`
    - _Requirements: 1.1, 1.2, 1.3, 2.1, 2.2, 2.3, 3.1, 3.2, 4.1, 4.2_

  - [x] 5.3 Add FluentResults typed async API surface tests
    - Create `FluentResults/ComputedKeyFluentResultsApiSurface.cs`
    - Verify `table.Entity.GetAsyncResult(params...)` returns `Task<Result<T?>>`
    - Verify `table.Entity.DeleteAsyncResult(params..., keyCondition)` returns `Task<Result>`
    - Verify table-level `GetAsyncResult` and `DeleteAsyncResult` compile
    - Verify `HideGeneratedAsyncMethods = true` suppresses standard `GetAsync`/`DeleteAsync` but keeps `*Result` variants
    - Verify `HideGeneratedAsyncMethods = false` generates both standard and Result variants
    - _Requirements: 5.1, 5.3, 6.1, 6.4, 7.1, 7.2, 7.3, 7.4_

  - [x] 5.4 Write property test: Eligibility consistency between typed builder and typed async (Property 7)
    - **Property 7: Eligibility is consistent between typed builder and typed async generation**
    - **Validates: Requirements 8.1, 8.2, 8.3**
    - In `TypedAsyncConveniencePropertyTests.cs`
    - For any randomly generated `EntityModel`, verify that presence of typed `Get(...)` builder overload in output implies presence of typed `GetAsync(...)` (when `HideGeneratedAsyncMethods` is false), and absence of typed `Get(...)` implies absence of typed `GetAsync(...)`

- [x] 6. Checkpoint - Full build and test verification
  - Ensure all tests pass, ask the user if questions arise.
  - Run `dotnet build-server shutdown` then `dotnet build` to verify ApiConsistencyTests compile (proves generated API surface is correct)
  - Run `dotnet test` to verify all unit tests and property tests pass

- [x] 7. Wire into existing generation and validate delegation pattern
  - [x] 7.1 Verify generated method bodies use correct delegation pattern
    - Ensure `GetAsync` body calls `Get(typed params).GetItemAsync(cancellationToken)` — not duplicating key composition logic
    - Ensure `DeleteAsync` body includes conditional `WithKeyCondition` pattern
    - Ensure `*Result` variants use expression-body delegation (no async/await overhead)
    - Ensure table-level methods pass all parameters including `cancellationToken` unchanged to accessor
    - _Requirements: 1.2, 2.2, 3.2, 3.3, 4.1, 5.2, 6.2, 6.3, 7.3, 7.4_

  - [x] 7.2 Write unit tests for delegation correctness (Property 3)
    - **Property 3: Generated typed async methods delegate correctly to typed builder then terminal**
    - **Validates: Requirements 1.2, 2.2, 5.2, 6.2, 6.3**
    - In `TypedAsyncConveniencePropertyTests.cs`
    - For qualifying entities, assert that generated `GetAsync` body contains call to `Get(` followed by `.GetItemAsync(`
    - Assert `DeleteAsync` body contains `Delete(`, conditional `WithKeyCondition`, and `.DeleteAsync(`
    - Assert `GetAsyncResult` body contains `Get(` followed by `.GetItemAsyncResult(`
    - Assert `DeleteAsyncResult` body contains `Delete(`, conditional `WithKeyCondition`, and `.DeleteAsyncResult(`

- [x] 8. Final checkpoint - Complete verification
  - Ensure all tests pass, ask the user if questions arise.
  - Run `dotnet build-server shutdown && dotnet build` for clean build
  - Run `dotnet test` to verify all property tests, unit tests, and API consistency tests pass
  - Verify no regressions in existing typed builder overload generation

## Notes

- Tasks marked with `*` are optional and can be skipped for faster MVP
- Each task references specific requirements for traceability
- Checkpoints ensure incremental validation
- Property tests validate universal correctness properties from the design document using FsCheck
- The `dotnet build-server shutdown` command is required before rebuilding when modifying the source generator
- All new methods in `TableGenerator.cs` are private static — no new public API on the generator itself
- The generated code IS the public API — validated by ApiConsistencyTests compilation
- No `ConfigureAwait(false)` needed in the source generator project (compile-time, not async runtime)

## Task Dependency Graph

```json
{
  "waves": [
    { "id": 0, "tasks": ["1.1", "1.2"] },
    { "id": 1, "tasks": ["1.3", "1.4", "2.1", "2.2"] },
    { "id": 2, "tasks": ["2.3", "3.1", "3.2"] },
    { "id": 3, "tasks": ["3.3", "3.4", "5.1"] },
    { "id": 4, "tasks": ["5.2", "5.3", "5.4"] },
    { "id": 5, "tasks": ["7.1"] },
    { "id": 6, "tasks": ["7.2"] }
  ]
}
```
