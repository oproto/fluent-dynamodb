# Implementation Plan

## Overview

Fix the bug where the source generator emits calls to `internal` BlobData<T> methods (`FromReferenceKey`, `GetPendingValue`, `SetReferenceKey`) that are inaccessible from external consuming assemblies. The fix introduces a public `BlobDataOperations` helper class and updates the generator to emit calls to the public helpers instead.

## Tasks

- [x] 1. Write bug condition exploration test
  - **Property 1: Bug Condition** - Generated Code Calls Internal BlobData Methods
  - **CRITICAL**: This test MUST FAIL on unfixed code - failure confirms the bug exists
  - **DO NOT attempt to fix the test or the code when it fails**
  - **NOTE**: This test encodes the expected behavior - it will validate the fix when it passes after implementation
  - **GOAL**: Surface counterexamples that demonstrate the bug exists
  - **Scoped PBT Approach**: The bug is deterministic — scope the property to the concrete failing case: remove `[assembly: InternalsVisibleTo("S3BlobDemo")]` from `Oproto.FluentDynamoDb/Properties/AssemblyInfo.cs` and attempt to build `examples/S3BlobDemo/`
  - Write a test in `Oproto.FluentDynamoDb.UnitTests` that verifies the MapperGenerator output for an entity with a `BlobData<T>` property does NOT contain direct internal method calls (`BlobData<T>.FromReferenceKey(`, `.GetPendingValue()`, `.SetReferenceKey(`)
  - Use the existing `MapperGenerator.GenerateEntityImplementation()` with a test `EntityModel` that has `IsBlobStorage = true` on a property
  - Assert generated source DOES NOT contain `"BlobData<" + innerType + ">.FromReferenceKey("` (direct internal call)
  - Assert generated source DOES NOT contain `".GetPendingValue()"` (direct internal call)
  - Assert generated source DOES NOT contain `".SetReferenceKey("` (direct internal call)
  - Assert generated source CONTAINS `"BlobDataOperations.CreateFromReferenceKey<"` (public helper)
  - Assert generated source CONTAINS `"BlobDataOperations.GetBlobPendingValue("` (public helper)
  - Assert generated source CONTAINS `"BlobDataOperations.SetBlobReferenceKey("` (public helper)
  - Run test on UNFIXED code
  - **EXPECTED OUTCOME**: Test FAILS (the generated code still calls internal methods directly — this confirms the bug exists)
  - Document counterexamples found: generated output contains `BlobData<byte[]>.FromReferenceKey(...)`, `.GetPendingValue()`, and `.SetReferenceKey(...)` instead of public helpers
  - Mark task complete when test is written, run, and failure is documented
  - _Requirements: 1.1, 1.2, 1.3, 1.4, 2.1, 2.2, 2.3_

- [x] 2. Write preservation property tests (BEFORE implementing fix)
  - **Property 2: Preservation** - Existing BlobData Public API Behavior Unchanged
  - **IMPORTANT**: Follow observation-first methodology
  - Observe behavior on UNFIXED code for non-buggy inputs (public API usage of `BlobData<T>`)
  - Observe: `BlobData<T>.Create(value)` returns instance with `IsLoaded = true`, `HasPendingData = true`, and `Value = value`
  - Observe: After `SetReferenceKey(key)` is called (via internal access in test project), `ReferenceKey == key` and `HasPendingData == false`
  - Observe: After `SetLoadedValue(value)` is called, `IsLoaded == true` and `Value == value`
  - Observe: `GetPendingValue()` returns the value when `HasPendingData == true`, default when false
  - Write property-based tests in `Oproto.FluentDynamoDb.UnitTests`:
    - For all string values `v`, `BlobData<string>.Create(v)` yields `IsLoaded == true`, `HasPendingData == true`, `Value == v`
    - For all string values `key`, after creating a BlobData via `FromReferenceKey(key, null, null)`, `ReferenceKey == key` and `IsLoaded == false`
    - For all non-null instances created via `Create(v)`, `GetPendingValue()` returns `v`
    - For all instances not created via `Create`, `GetPendingValue()` returns `default`
  - Additionally verify existing unit tests in `Oproto.FluentDynamoDb.UnitTests` all pass on unfixed code by running `dotnet test`
  - Run tests on UNFIXED code
  - **EXPECTED OUTCOME**: Tests PASS (confirms baseline behavior to preserve)
  - Mark task complete when tests are written, run, and passing on unfixed code
  - _Requirements: 3.1, 3.2, 3.3, 3.4, 3.5, 3.6_

- [x] 3. Fix for BlobData internal methods inaccessible from generated code in external assemblies

  - [x] 3.1 Create `BlobDataOperations` public static helper class
    - Create new file: `Oproto.FluentDynamoDb/Providers/BlobStorage/BlobDataOperations.cs`
    - Namespace: `Oproto.FluentDynamoDb.Providers.BlobStorage`
    - Mark class with `[EditorBrowsable(EditorBrowsableState.Never)]`
    - Add XML doc: `/// <summary>Internal helper for generated code. Do not use directly.</summary>`
    - Implement `CreateFromReferenceKey<T>(string referenceKey, IBlobStorageProvider? provider, Func<Stream, CancellationToken, Task<T>>? deserializer)` — delegates to `BlobData<T>.FromReferenceKey(...)`
    - Implement `GetBlobPendingValue<T>(BlobData<T> blobData)` — delegates to `blobData.GetPendingValue()`
    - Implement `SetBlobReferenceKey<T>(BlobData<T> blobData, string referenceKey)` — delegates to `blobData.SetReferenceKey(referenceKey)`
    - Implement `SetBlobLoadedValue<T>(BlobData<T> blobData, T value)` — delegates to `blobData.SetLoadedValue(value)`
    - Mark each method with `[EditorBrowsable(EditorBrowsableState.Never)]`
    - Add XML doc on each method indicating "for generated code use only"
    - _Bug_Condition: isBugCondition(input) where input.consumingAssembly NOT IN internalsVisibleToList AND generatedCodeCallsInternalMethod(input.generatedSource)_
    - _Expected_Behavior: Generated code calls public BlobDataOperations methods which delegate to internal BlobData<T> methods within the same assembly_
    - _Preservation: All existing BlobData<T> public API behavior (Create, LoadAsync, Value, ReferenceKey, HasPendingData) remains unchanged_
    - _Requirements: 2.1, 2.2, 2.3, 2.4, 3.7_

  - [x] 3.2 Update MapperGenerator to emit BlobDataOperations calls
    - Modify `Oproto.FluentDynamoDb.SourceGenerator/Generators/MapperGenerator.cs`
    - In the deserialization code (~line 3574): Replace `BlobData<{innerType}>.FromReferenceKey(referenceKey, blobProvider_{propertyName}, deserializer)` with `BlobDataOperations.CreateFromReferenceKey<{innerType}>(referenceKey, blobProvider_{propertyName}, deserializer)`
    - In the serialization code (~line 887): Replace `typedEntity.{escapedPropertyName}.GetPendingValue()` with `BlobDataOperations.GetBlobPendingValue(typedEntity.{escapedPropertyName})`
    - In the post-upload code (~line 979): Replace `typedEntity.{escapedPropertyName}.SetReferenceKey(reference)` with `BlobDataOperations.SetBlobReferenceKey(typedEntity.{escapedPropertyName}, reference)`
    - Run `dotnet build-server shutdown` after changes (source generator cache)
    - _Bug_Condition: generatedCodeCallsInternalMethod(source) returns true when source contains direct internal calls_
    - _Expected_Behavior: generatedCodeCallsInternalMethod(source) returns false; source contains BlobDataOperations.* calls instead_
    - _Preservation: Non-blob entities produce identical generated output_
    - _Requirements: 2.1, 2.2, 2.3, 2.4_

  - [x] 3.3 Remove InternalsVisibleTo workaround for S3BlobDemo
    - Modify `Oproto.FluentDynamoDb/Properties/AssemblyInfo.cs`
    - Remove the line `[assembly: InternalsVisibleTo("S3BlobDemo")]`
    - Keep `[assembly: InternalsVisibleTo("Oproto.FluentDynamoDb.UnitTests")]` and `[assembly: InternalsVisibleTo("Examples.Tests")]`
    - _Bug_Condition: S3BlobDemo relied on InternalsVisibleTo as a workaround — it must now compile without it_
    - _Expected_Behavior: S3BlobDemo compiles successfully using BlobDataOperations public helpers in generated code_
    - _Requirements: 2.1, 2.2, 2.3_

  - [x] 3.4 Verify bug condition exploration test now passes
    - **Property 1: Expected Behavior** - Generated Code Uses Public BlobDataOperations Helpers
    - **IMPORTANT**: Re-run the SAME test from task 1 - do NOT write a new test
    - The test from task 1 encodes the expected behavior (generated code uses BlobDataOperations.* instead of internal methods)
    - When this test passes, it confirms the expected behavior is satisfied
    - Run bug condition exploration test from step 1
    - **EXPECTED OUTCOME**: Test PASSES (confirms bug is fixed — generated code now uses public helpers)
    - _Requirements: 2.1, 2.2, 2.3, 2.4_

  - [x] 3.5 Verify preservation tests still pass
    - **Property 2: Preservation** - Existing BlobData Public API Behavior Unchanged
    - **IMPORTANT**: Re-run the SAME tests from task 2 - do NOT write new tests
    - Run preservation property tests from step 2
    - **EXPECTED OUTCOME**: Tests PASS (confirms no regressions)
    - Run `dotnet test` on full test suite to confirm all existing tests pass
    - Confirm all tests still pass after fix (no regressions)

  - [x] 3.6 Verify S3BlobDemo builds without InternalsVisibleTo
    - Run `dotnet build examples/S3BlobDemo/` to confirm it compiles without errors
    - Verify zero CS1061/CS0117 errors on BlobData internal method calls
    - This confirms that external assemblies can now use `[BlobStorage]` with `BlobData<T>` properties

- [x] 4. Checkpoint - Ensure all tests pass
  - Run `dotnet test` across the entire solution
  - Verify S3BlobDemo builds cleanly: `dotnet build examples/S3BlobDemo/`
  - Verify the full solution builds: `dotnet build`
  - Ensure all tests pass, ask the user if questions arise.


## Task Dependency Graph

```json
{
  "waves": [
    { "id": 0, "tasks": ["1", "2"] },
    { "id": 1, "tasks": ["3.1"] },
    { "id": 2, "tasks": ["3.2"] },
    { "id": 3, "tasks": ["3.3"] },
    { "id": 4, "tasks": ["3.4", "3.5", "3.6"] },
    { "id": 5, "tasks": ["4"] }
  ]
}
```

## Notes

- The source generator caches in memory — run `dotnet build-server shutdown` after modifying `MapperGenerator.cs`
- `SetLoadedValue` is included in `BlobDataOperations` for completeness even though the generator does not currently emit it (eager loading uses `LoadAsync()` directly)
- The `[EditorBrowsable(EditorBrowsableState.Never)]` attribute hides the helpers from IntelliSense for normal consumers while keeping them publicly accessible for generated code
- The test project `Oproto.FluentDynamoDb.UnitTests` has `InternalsVisibleTo` access, so it can test both the internal methods and the public wrappers
