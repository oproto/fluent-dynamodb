# BlobData Internal Methods Fix - Bugfix Design

## Overview

The source generator (`MapperGenerator`) emits entity mapping code that directly calls `internal` methods on `BlobData<T>` — specifically `FromReferenceKey`, `SetReferenceKey`, and `GetPendingValue`. Since generated code executes in the consuming assembly (not in the library assembly), external NuGet consumers cannot access these `internal` members, resulting in CS1061/CS0117 compile errors.

The fix introduces a public static helper class `BlobDataOperations` within the library assembly that wraps these internal methods. The source generator is then updated to emit calls to these public helpers instead of calling internal methods directly. This maintains the API surface cleanliness (helpers are hidden from IntelliSense via `[EditorBrowsable(EditorBrowsableState.Never)]`) while enabling correct compilation from external assemblies.

## Glossary

- **Bug_Condition (C)**: The condition that triggers the bug — when generated mapper code calls `internal` BlobData<T> methods from a consuming assembly that lacks `InternalsVisibleTo` access
- **Property (P)**: The desired behavior — generated code compiles successfully in any consuming assembly by calling public helper methods instead of internal methods
- **Preservation**: Existing blob storage functionality (Create, LoadAsync, Value access, ReferenceKey access) must remain unchanged. Existing unit tests must pass without modification.
- **BlobData\<T\>**: The generic wrapper type in `Oproto.FluentDynamoDb/Providers/BlobStorage/BlobData.cs` that encapsulates blob storage behavior including lazy loading and reference key management
- **MapperGenerator**: The source generator class in `Oproto.FluentDynamoDb.SourceGenerator/Generators/MapperGenerator.cs` that emits entity mapping code (ToDynamoDbAsync / FromDynamoDbAsync)
- **BlobDataOperations**: The new public static helper class (to be created) that wraps internal BlobData<T> methods for use by generated code
- **InternalsVisibleTo**: The assembly-level attribute that grants internal member access to specified assemblies — currently used as a workaround for `S3BlobDemo`

## Bug Details

### Bug Condition

The bug manifests when a consuming assembly (external NuGet consumer or example project without `InternalsVisibleTo`) defines an entity with `[BlobStorage]` on a `BlobData<T>` property. The source generator emits code that calls internal methods directly, which are inaccessible from the consuming assembly.

**Formal Specification:**
```
FUNCTION isBugCondition(input)
  INPUT: input of type GeneratedCodeContext
  OUTPUT: boolean
  
  RETURN input.entityHasBlobStorageProperty == true
         AND input.consumingAssembly NOT IN internalsVisibleToList
         AND generatedCodeCallsInternalMethod(input.generatedSource)
END FUNCTION

FUNCTION generatedCodeCallsInternalMethod(source)
  RETURN source CONTAINS "BlobData<T>.FromReferenceKey("
         OR source CONTAINS ".GetPendingValue()"
         OR source CONTAINS ".SetReferenceKey("
         OR source CONTAINS ".SetLoadedValue("
END FUNCTION
```

### Examples

- **Deserialization (FromReferenceKey)**: Generated code emits `entity.Content = BlobData<byte[]>.FromReferenceKey(referenceKey, blobProvider, deserializer);` → CS0117 because `FromReferenceKey` is internal
- **Serialization (GetPendingValue)**: Generated code emits `var pendingValue = typedEntity.Content.GetPendingValue();` → CS1061 because `GetPendingValue` is internal  
- **Post-upload (SetReferenceKey)**: Generated code emits `typedEntity.Content.SetReferenceKey(reference);` → CS1061 because `SetReferenceKey` is internal
- **Non-BlobData entity**: An entity without `[BlobStorage]` properties compiles fine — no internal methods are referenced in generated code (not a bug condition)

## Expected Behavior

### Preservation Requirements

**Unchanged Behaviors:**
- `BlobData<T>.Create(value)` must continue to create instances with `IsLoaded = true` and `HasPendingData = true`
- `blobInstance.LoadAsync()` must continue to retrieve data from the configured blob storage provider
- `blobInstance.Value` must continue to return the loaded data value after loading
- `blobInstance.ReferenceKey` must continue to return the stored reference key
- `blobInstance.HasPendingData` must continue to indicate whether there is pending data to upload
- All existing blob storage unit tests in `Oproto.FluentDynamoDb.UnitTests` must pass without modification
- Mouse/keyboard interactions with the public API surface of `BlobData<T>` remain unchanged

**Scope:**
All inputs that do NOT involve generated code calling internal BlobData<T> methods should be completely unaffected by this fix. This includes:
- Direct consumer usage of `BlobData<T>.Create(value)` (public method)
- Consumer calls to `blobInstance.LoadAsync()` (public method)
- Consumer reads of `blobInstance.Value`, `blobInstance.ReferenceKey`, `blobInstance.IsLoaded`, `blobInstance.HasPendingData` (public properties)
- Entities without `[BlobStorage]` properties — their generated code is unaffected
- The internal methods themselves remain internal — they are still called by `BlobDataOperations` within the same assembly

## Hypothesized Root Cause

Based on the bug description, the root cause is clear and confirmed:

1. **Design Oversight in API Accessibility**: The `BlobData<T>` methods (`FromReferenceKey`, `SetReferenceKey`, `GetPendingValue`, `SetLoadedValue`) were intentionally made `internal` to keep the public API clean. However, the source generator emits code that runs in the *consuming* assembly, not in the library assembly, so `internal` access is insufficient.

2. **Missing Indirection Layer**: There is no public indirection layer (helper class) that generated code can call from external assemblies. The generator directly emits calls to internal members.

3. **Testing Gap**: The issue was never caught because:
   - Unit tests run in `Oproto.FluentDynamoDb.UnitTests` which has `InternalsVisibleTo` access
   - The `S3BlobDemo` example project was given `InternalsVisibleTo` as a workaround
   - No integration test exercises the scenario of a truly external consuming assembly

4. **Workaround Masking the Problem**: `[assembly: InternalsVisibleTo("S3BlobDemo")]` in `AssemblyInfo.cs` masks the issue for the example project but cannot help NuGet consumers.

## Correctness Properties

Property 1: Bug Condition - Generated Code Uses Public Helpers

_For any_ entity with `[BlobStorage]` on a `BlobData<T>` property, the source generator SHALL emit calls to `BlobDataOperations.CreateFromReferenceKey<T>(...)`, `BlobDataOperations.GetBlobPendingValue<T>(...)`, and `BlobDataOperations.SetBlobReferenceKey<T>(...)` instead of calling internal methods directly, and the generated code SHALL compile without errors from any consuming assembly.

**Validates: Requirements 2.1, 2.2, 2.3, 2.4**

Property 2: Preservation - Existing BlobData Behavior Unchanged

_For any_ usage of `BlobData<T>` that does NOT involve generated mapper code calling internal methods (i.e., public API usage like `Create`, `LoadAsync`, `Value`, `ReferenceKey`, `HasPendingData`), the fixed code SHALL produce exactly the same behavior as the original code, preserving all existing functionality and ensuring all existing unit tests pass without modification.

**Validates: Requirements 3.1, 3.2, 3.3, 3.4, 3.5, 3.6, 3.7**

## Fix Implementation

### Changes Required

Assuming our root cause analysis is correct:

**File**: `Oproto.FluentDynamoDb/Providers/BlobStorage/BlobDataOperations.cs` (NEW)

**Purpose**: Public static helper class that wraps internal BlobData<T> methods for use by generated code.

**Specific Changes**:
1. **Create `BlobDataOperations` class**: A `public static class` in the `Oproto.FluentDynamoDb.Providers.BlobStorage` namespace with `[EditorBrowsable(EditorBrowsableState.Never)]` attribute on the class and each method
2. **`CreateFromReferenceKey<T>` method**: Wraps `BlobData<T>.FromReferenceKey(referenceKey, provider, deserializer)` — same signature, delegates directly
3. **`GetBlobPendingValue<T>` method**: Wraps `blobData.GetPendingValue()` — takes `BlobData<T>` instance, returns `T?`
4. **`SetBlobReferenceKey<T>` method**: Wraps `blobData.SetReferenceKey(referenceKey)` — takes `BlobData<T>` instance and string
5. **`SetBlobLoadedValue<T>` method**: Wraps `blobData.SetLoadedValue(value)` — takes `BlobData<T>` instance and value (included for completeness even though not currently emitted)

---

**File**: `Oproto.FluentDynamoDb.SourceGenerator/Generators/MapperGenerator.cs` (MODIFY)

**Function**: `GenerateBlobPropertyToAttributeValue` (serialization) and `GenerateBlobPropertyFromAttributeValue` (deserialization)

**Specific Changes**:
1. **Replace `BlobData<T>.FromReferenceKey(...)` emission**: Change to emit `BlobDataOperations.CreateFromReferenceKey<T>(referenceKey, blobProvider, deserializer)`
2. **Replace `.GetPendingValue()` emission**: Change to emit `BlobDataOperations.GetBlobPendingValue(typedEntity.Property)`
3. **Replace `.SetReferenceKey(reference)` emission**: Change to emit `BlobDataOperations.SetBlobReferenceKey(typedEntity.Property, reference)`

---

**File**: `Oproto.FluentDynamoDb/Properties/AssemblyInfo.cs` (MODIFY)

**Specific Changes**:
1. **Remove `[assembly: InternalsVisibleTo("S3BlobDemo")]`**: This workaround is no longer needed once the fix is in place — the generated code will call public helpers instead

## Testing Strategy

### Validation Approach

The testing strategy follows a two-phase approach: first, confirm the bug exists on unfixed code by removing the `InternalsVisibleTo` workaround and observing compile failures, then verify the fix works correctly and preserves existing behavior.

### Exploratory Bug Condition Checking

**Goal**: Surface counterexamples that demonstrate the bug BEFORE implementing the fix. Confirm or refute the root cause analysis.

**Test Plan**: Remove `[assembly: InternalsVisibleTo("S3BlobDemo")]` from `AssemblyInfo.cs` and attempt to build the `S3BlobDemo` project. Observe compile errors on the internal method calls.

**Test Cases**:
1. **Build S3BlobDemo without InternalsVisibleTo**: Run `dotnet build examples/S3BlobDemo/` after removing the workaround (will fail on unfixed code with CS1061/CS0117)
2. **Inspect generated source**: Examine the generated `.g.cs` output to confirm direct internal method calls are present
3. **Count error types**: Verify that errors are specifically CS1061 (missing member) and CS0117 (inaccessible member) on BlobData methods

**Expected Counterexamples**:
- 6 compile errors referencing `FromReferenceKey`, `SetReferenceKey`, and `GetPendingValue`
- Errors specifically on generated code lines, not on hand-written consumer code

### Fix Checking

**Goal**: Verify that for all inputs where the bug condition holds, the fixed function produces the expected behavior.

**Pseudocode:**
```
FOR ALL entity WHERE entity.hasBlobStorageProperty == true DO
  generatedSource := MapperGenerator.GenerateEntityImplementation(entity)
  ASSERT NOT generatedCodeCallsInternalMethod(generatedSource)
  ASSERT generatedSource CONTAINS "BlobDataOperations.CreateFromReferenceKey"
  ASSERT generatedSource CONTAINS "BlobDataOperations.GetBlobPendingValue"
  ASSERT generatedSource CONTAINS "BlobDataOperations.SetBlobReferenceKey"
  ASSERT compiles(generatedSource, externalAssemblyContext)
END FOR
```

### Preservation Checking

**Goal**: Verify that for all inputs where the bug condition does NOT hold, the fixed function produces the same result as the original function.

**Pseudocode:**
```
FOR ALL entity WHERE entity.hasBlobStorageProperty == false DO
  ASSERT GenerateEntityImplementation_original(entity) = GenerateEntityImplementation_fixed(entity)
END FOR

FOR ALL blobData WHERE publicApiUsage(blobData) DO
  ASSERT behavior_original(blobData) = behavior_fixed(blobData)
END FOR
```

**Testing Approach**: Property-based testing is recommended for preservation checking because:
- It generates many entity configurations automatically across the input domain
- It catches edge cases where generated code might unexpectedly differ
- It provides strong guarantees that non-blob entities are completely unaffected

**Test Plan**: Run the full existing unit test suite to observe that all tests pass without modification. Additionally write targeted tests that exercise the `BlobDataOperations` helper methods to ensure they correctly delegate to the internal methods.

**Test Cases**:
1. **Existing Unit Tests Pass**: Run `dotnet test` on `Oproto.FluentDynamoDb.UnitTests` — all tests should pass without modification
2. **S3BlobDemo Builds**: Run `dotnet build examples/S3BlobDemo/` after removing `InternalsVisibleTo` — should now compile successfully
3. **BlobDataOperations Delegation**: Verify `CreateFromReferenceKey<T>` produces identical results to `FromReferenceKey`
4. **BlobDataOperations GetPendingValue**: Verify `GetBlobPendingValue<T>` returns same value as direct `GetPendingValue()` call

### Unit Tests

- Test `BlobDataOperations.CreateFromReferenceKey<T>` produces a valid `BlobData<T>` with correct reference key and provider
- Test `BlobDataOperations.GetBlobPendingValue<T>` returns pending value when `HasPendingData` is true, default when false
- Test `BlobDataOperations.SetBlobReferenceKey<T>` correctly sets the reference key and clears `HasPendingData`
- Test `BlobDataOperations.SetBlobLoadedValue<T>` correctly sets the value and marks `IsLoaded` as true
- Test that `[EditorBrowsable(EditorBrowsableState.Never)]` attribute is present on class and methods

### Property-Based Tests

- Generate random entity configurations and verify that entities without `[BlobStorage]` produce identical generated output before and after the fix
- Generate random `BlobData<T>` instances and verify that `BlobDataOperations` methods produce identical state transitions as direct internal method calls
- Test various inner types (byte[], string, complex objects) through `CreateFromReferenceKey` to ensure all type parameters work correctly

### Integration Tests

- Build the S3BlobDemo example project without `InternalsVisibleTo` and verify it compiles
- Run the full solution build (`dotnet build`) and verify zero errors
- Verify that the source generator emits `BlobDataOperations.*` calls in generated output when blob properties are present
