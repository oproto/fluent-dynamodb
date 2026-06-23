# Implementation Plan: Put Key Prefix Application

## Overview

Implement automatic key prefix application during Put serialization by modifying the source generator (`MapperGenerator.cs`, `HydratorGenerator.cs`, `TableGenerator.cs`), adding `WithKeyMode(KeyInputMode)` to `PutItemRequestBuilder`, adding a new `SerializeAsync` overload to `IAsyncEntityHydrator`, and updating documentation. The implementation is incremental: library changes first, then source generator modifications, followed by comprehensive testing and documentation.

## Tasks

- [x] 1. Add WithKeyMode to PutItemRequestBuilder and update IAsyncEntityHydrator
  - [x] 1.1 Add `_keyInputMode` field and `WithKeyMode(KeyInputMode)` method to `PutItemRequestBuilder<TEntity>`
    - Add `private KeyInputMode _keyInputMode = KeyInputMode.Default;` field
    - Add `WithKeyMode(KeyInputMode mode)` method returning `this` for fluent chaining
    - Modify `WithItem(TEntity entity)` to pass `_keyInputMode` to `TEntity.ToDynamoDb(entity, _options, _keyInputMode)` (new overload, will be generated later — use try/catch fallback to existing overload for now)
    - Modify `ToDynamoDbResponseAsync` to pass `_keyInputMode` to hydrator's `SerializeAsync` overload
    - Ensure `.ConfigureAwait(false)` on all await calls
    - _Requirements: 4.1, 4.2, 4.3, 4.4, 4.5, 4.6, 4.7, 7.2_

  - [x] 1.2 Add new `SerializeAsync` overload to `IAsyncEntityHydrator<TEntity>` interface
    - Add overload: `Task<Dictionary<string, AttributeValue>> SerializeAsync(TEntity entity, IBlobStorageProvider? blobProvider, FluentDynamoDbOptions? options, KeyInputMode keyInputMode, CancellationToken cancellationToken = default);`
    - Existing `SerializeAsync` (without `keyInputMode`) remains unchanged for backward compatibility
    - _Requirements: 7.3, 8.3_

  - [x] 1.3 Write unit tests for `PutItemRequestBuilder.WithKeyMode`
    - Test `WithKeyMode` returns same builder instance (fluent chaining)
    - Test default `_keyInputMode` is `KeyInputMode.Default`
    - Test explicit mode is stored and propagated
    - Test `WithKeyMode(KeyInputMode.Raw)` passes values unchanged
    - _Requirements: 4.1, 4.2, 4.3_

- [x] 2. Modify Source Generator — MapperGenerator for ToDynamoDb overload
  - [x] 2.1 Add new `ToDynamoDb` overload generation in `MapperGenerator.cs`
    - Generate `ToDynamoDb<TSelf>(TSelf entity, FluentDynamoDbOptions? options, KeyInputMode keyInputMode)` overload
    - Existing `ToDynamoDb(entity, options)` overload delegates to new one with `KeyInputMode.Default`
    - New overload calls `KeyInputModeResolver.Resolve(keyInputMode, options ?? new FluentDynamoDbOptions())` at the top
    - For each key property with a configured prefix that is NOT `[Computed]`: wrap value in `KeyPrefixHelper.ApplyKeyPrefix(value, prefix, separator, resolvedMode)`
    - For computed key properties: emit value as-is (no prefix call)
    - For key properties without prefix: emit value as-is
    - Apply same logic to GSI/LSI key properties that carry a `[PartitionKey]`/`[SortKey]` attribute with a prefix
    - _Requirements: 1.1, 1.2, 1.3, 1.4, 1.5, 1.6, 1.7, 1.8, 2.1, 2.2, 2.3, 2.4, 2.5, 2.6, 2.7, 3.1, 3.2, 3.3, 7.1, 7.4, 7.5, 10.1, 10.2, 10.3, 10.4, 10.5_

  - [x] 2.2 Update `HydratorGenerator.cs` to emit new `SerializeAsync` overload
    - Generate implementation of `SerializeAsync(entity, blobProvider, options, keyInputMode, ct)` that passes `keyInputMode` through to `ToDynamoDb(entity, options, keyInputMode)`
    - Existing `SerializeAsync` (3-param) delegates to new overload with `KeyInputMode.Default`
    - _Requirements: 7.3_

  - [x] 2.3 Verify `TableGenerator.cs` convenience methods do NOT set KeyInputMode
    - Ensure generated `PutAsync(entity)` and `PutAsync(entity, KeyCondition)` delegate to `Put(entity).PutAsync()` without calling `WithKeyMode`
    - This allows `KeyInputMode.Default` to resolve from `FluentDynamoDbOptions.DefaultKeyInputMode` at execution time
    - _Requirements: 5.1, 5.2, 5.3, 5.4_

  - [x] 2.4 Write unit tests for generated ToDynamoDb overloads
    - Test both overloads produce consistent results when mode is Default
    - Test existing overload delegates correctly (backward compatibility)
    - Test null options handling
    - _Requirements: 7.1, 7.4, 8.1, 8.3_

- [x] 3. Checkpoint — Ensure all tests pass
  - Ensure all tests pass, ask the user if questions arise.
  - Run `dotnet build-server shutdown` then `dotnet build` to pick up source generator changes.

- [x] 4. Property-based tests for KeyPrefixHelper behavior
  - [x] 4.1 Write property test: ApplyKeyPrefix mode correctness (Property 1)
    - **Property 1: ApplyKeyPrefix mode correctness**
    - For any non-null key value, configured prefix/separator, and resolved KeyInputMode (Auto, Value, Raw), the output matches `KeyPrefixHelper.ApplyKeyPrefix(value, prefix, separator, resolvedMode)`
    - Use FsCheck with custom generators for valid key values, prefixes, separators, and modes
    - Minimum 100 iterations
    - **Validates: Requirements 1.1, 1.2, 1.3, 1.4, 1.5, 2.1, 2.2, 2.3, 2.4, 2.5, 4.2, 4.5, 4.6, 10.1, 10.2, 10.3**

  - [x] 4.2 Write property test: No-prefix pass-through (Property 2)
    - **Property 2: No-prefix pass-through**
    - For any key value and any resolved KeyInputMode, when prefix is null or empty, the serialized value equals the original input unchanged
    - **Validates: Requirements 1.6, 2.6, 10.4**

  - [x] 4.3 Write property test: Computed key exclusion (Property 3)
    - **Property 3: Computed key exclusion**
    - For any computed key property, regardless of prefix configuration and KeyInputMode, the serialized value equals the computed value without prefix transformation
    - **Validates: Requirements 3.1, 3.2, 3.3, 4.7, 10.5**

  - [x] 4.4 Write property test: Ordinal case-sensitivity in Auto mode (Property 4)
    - **Property 4: Ordinal case-sensitivity in Auto mode**
    - For any key value that starts with a case-variant (not exact case) of prefix+separator, Auto mode prepends the correct prefix+separator
    - **Validates: Requirements 6.1, 6.3**

  - [x] 4.5 Write property test: StartsWith positional check in Auto mode (Property 5)
    - **Property 5: StartsWith positional check in Auto mode**
    - For any key value containing prefix+separator at a non-zero position, Auto mode prepends the prefix+separator
    - **Validates: Requirements 6.4**

  - [x] 4.6 Write property test: Full prefix+separator boundary in Auto mode (Property 6)
    - **Property 6: Full prefix+separator boundary in Auto mode**
    - For any key value starting with a superset of the prefix (e.g., extra chars before separator), Auto mode prepends the prefix+separator
    - **Validates: Requirements 6.5**

- [x] 5. Integration tests for end-to-end Put prefix behavior
  - [x] 5.1 Write integration tests for Put with Auto mode (default)
    - Test `Put(entity).PutAsync()` with mock DynamoDB client receives correctly prefixed key values
    - Test entity with `Keys.Pk(value)` (already prefixed) passes through unchanged (backward compat)
    - Test entity with raw value gets prefix prepended
    - _Requirements: 1.2, 1.3, 2.2, 2.3, 8.1_

  - [x] 5.2 Write integration tests for Put with explicit KeyInputMode overrides
    - Test `Put(entity).WithKeyMode(KeyInputMode.Raw).PutAsync()` — mock client receives raw values
    - Test `Put(entity).WithKeyMode(KeyInputMode.Value).PutAsync()` — mock client receives always-prefixed values
    - Test `PutAsync(entity)` convenience method uses options default
    - _Requirements: 4.4, 4.5, 4.6, 5.2, 5.4, 8.2, 8.4_

  - [x] 5.3 Write integration tests for computed key exclusion and GSI/LSI keys
    - Test entity with computed PK + non-computed SK: only SK gets prefix
    - Test entity with non-computed PK + computed SK: only PK gets prefix
    - Test entity with GSI key carrying primary key prefix attribute: GSI key gets prefix
    - Test entity with hydrator path (encrypted fields + prefix): hydrator receives and applies KeyInputMode
    - _Requirements: 3.1, 3.2, 3.3, 10.1, 10.2, 10.3, 10.4, 10.5, 7.3_

- [x] 6. Checkpoint — Ensure all tests pass
  - Ensure all tests pass, ask the user if questions arise.

- [x] 7. API surface tests and backward compatibility verification
  - [x] 7.1 Write API surface compilation tests
    - Verify existing `Put` API patterns still compile (no signature breakage)
    - Verify `WithKeyMode` method is available and chainable
    - Verify both `ToDynamoDb` overloads are accessible
    - Verify `PutAsync(entity)` and `PutAsync(entity, KeyCondition)` convenience methods still work
    - _Requirements: 8.3_

  - [x] 7.2 Write backward compatibility integration tests
    - Test existing code using `Entity.Keys.Pk(value)` continues to work in Auto mode
    - Test `FluentDynamoDbOptions.DefaultKeyInputMode = Raw` passes all values unchanged
    - Test upgrading scenario: entity previously using raw values now gets prefix with Auto
    - _Requirements: 8.1, 8.2, 8.3, 8.4_

- [x] 8. Documentation updates
  - [x] 8.1 Create documentation for Put key prefix behavior in `docs/`
    - Create `docs/core-features/PutKeyPrefixBehavior.md` (or add section to existing Put docs)
    - Explain Auto mode detection and application during Put
    - Explain Value mode always prepends prefix
    - Explain Raw mode passes values unchanged
    - Include at least one code example per mode showing a Put operation
    - Include example of per-call `WithKeyMode` override
    - _Requirements: 9.1, 9.4_

  - [x] 8.2 Update `docs/DOCUMENTATION_CHANGELOG.md`
    - Add entry with date, category, file path, description
    - Include Before/After code example pair (Put without vs. with automatic prefix)
    - _Requirements: 9.2_

  - [x] 8.3 Update `CHANGELOG.md`
    - Add entry under `[Unreleased]` → `### Added` describing Put key prefix application feature
    - Follow Keep a Changelog format consistent with existing entries
    - _Requirements: 9.3_

- [x] 9. Final checkpoint — Ensure all tests pass
  - Ensure all tests pass, ask the user if questions arise.
  - Run `dotnet build-server shutdown` then `dotnet test` to verify everything works end-to-end.

## Notes

- Tasks marked with `*` are optional and can be skipped for faster MVP
- Each task references specific requirements for traceability
- Checkpoints ensure incremental validation
- Property tests validate universal correctness properties from the design document using FsCheck
- Unit tests validate specific examples and edge cases
- Source generator changes require `dotnet build-server shutdown` before rebuilding to clear cached generator instances
- All `await` calls in library code (non-test) must use `.ConfigureAwait(false)`
- The implementation must remain AOT-compatible and trimmer-safe

## Task Dependency Graph

```json
{
  "waves": [
    { "id": 0, "tasks": ["1.1", "1.2"] },
    { "id": 1, "tasks": ["1.3", "2.1"] },
    { "id": 2, "tasks": ["2.2", "2.3"] },
    { "id": 3, "tasks": ["2.4", "4.1", "4.2", "4.4", "4.5", "4.6"] },
    { "id": 4, "tasks": ["4.3", "5.1", "5.2"] },
    { "id": 5, "tasks": ["5.3", "7.1"] },
    { "id": 6, "tasks": ["7.2"] },
    { "id": 7, "tasks": ["8.1", "8.2", "8.3"] }
  ]
}
```
