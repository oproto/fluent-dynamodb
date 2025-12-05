# Options Propagation and Documentation Fixes - Tasks

## Phase 1: Documentation Fixes

### Task 1.1: Fix docs/advanced-topics/AdvancedTypes.md
- [x] Replace `.Put(document).ExecuteAsync()` with `.Put(document).PutAsync()`
- [x] Verify all Put operations use correct method
- **Requirement**: 1.1, 1.2

### Task 1.2: Fix docs/advanced-topics/TableGenerationCustomization.md
- [x] Replace all `.Put(entity).ExecuteAsync()` with `.Put(entity).PutAsync()`
- [x] Review all 10 occurrences
- **Requirement**: 1.1, 1.2

### Task 1.3: Fix docs/core-features/Transactions.md
- [x] Verified - no `.Put().ExecuteAsync()` patterns found (uses transaction context correctly)
- **Requirement**: 1.1, 1.2

### Task 1.4: Fix docs/core-features/BatchOperations.md
- [x] Verified - no `.Put().ExecuteAsync()` patterns found (uses batch context correctly)
- **Requirement**: 1.1, 1.2

### Task 1.5: Fix docs/QUICK_REFERENCE.md
- [x] Replace `.Put().WithItem(entity).ExecuteAsync()` with `.Put().WithItem(entity).PutAsync()`
- **Requirement**: 1.1, 1.2

### Task 1.6: Fix docs/DeveloperGuide.md
- [x] Replace `.Put().WithItem(user).ExecuteAsync()` with `.Put().WithItem(user).PutAsync()`
- **Requirement**: 1.1, 1.2

### Task 1.7: Fix docs/reference/AttributeReference.md
- [x] Replace `.Put(document).ExecuteAsync()` with `.Put(document).PutAsync()`
- **Requirement**: 1.1, 1.2

### Task 1.8: Fix Oproto.FluentDynamoDb.SystemTextJson/README.md
- [x] Replace `.Put(order).ExecuteAsync()` with `.Put(order).PutAsync()`
- **Requirement**: 1.1, 1.2

### Task 1.9: Fix Oproto.FluentDynamoDb.NewtonsoftJson/README.md
- [x] Replace `.Put(order).ExecuteAsync()` with `.Put(order).PutAsync()`
- **Requirement**: 1.1, 1.2

### Task 1.10: Fix .kiro/specs/integration-test-build-fixes/design.md
- [x] Replace `.Put(entity).ExecuteAsync()` with `.Put(entity).PutAsync()`
- **Requirement**: 1.1, 1.2

### Task 1.11: Fix Oproto.FluentDynamoDb/Attributes/GenerateAccessorsAttribute.cs
- [x] Replace `.Put(order).ExecuteAsync()` with `.Put(order).PutAsync()` in XML documentation
- **Requirement**: 1.1, 1.2

## Phase 2: BatchGetResponse Options Propagation

### Task 2.1: Update BatchGetResponse class
- [x] Add `private readonly FluentDynamoDbOptions? _options;` field
- [x] Add `FluentDynamoDbOptions? options = null` parameter to constructor
- [x] Store options in constructor: `_options = options;`
- [x] Update `GetItem<TEntity>()` to pass `_options` to `FromDynamoDb()`
- [x] Update `GetItems<TEntity>()` (uses GetItem internally, should work)
- [x] Update `GetItemsRange<TEntity>()` (uses GetItem internally, should work)
- **File**: `Oproto.FluentDynamoDb/Requests/BatchGetResponse.cs`
- **Requirement**: 2.1, 2.2

### Task 2.2: Update BatchGetBuilder to pass options
- [x] Identify where `BatchGetResponse` is constructed
- [x] Add `_options` field to track options from added builders
- [x] Capture options from first builder with options in `Add()` method
- [x] Pass `_options` when constructing `BatchGetResponse`
- **File**: `Oproto.FluentDynamoDb/Requests/BatchGetBuilder.cs`
- **Requirement**: 2.3

## Phase 3: TransactionGetResponse Options Propagation

### Task 3.1: Update TransactionGetResponse class
- [x] Add `private readonly FluentDynamoDbOptions? _options;` field
- [x] Add `FluentDynamoDbOptions? options = null` parameter to constructor
- [x] Store options in constructor: `_options = options;`
- [x] Update `GetItem<TEntity>()` to pass `_options` to `FromDynamoDb()`
- [x] Update `GetItems<TEntity>()` (uses GetItem internally, should work)
- [x] Update `GetItemsRange<TEntity>()` (uses GetItem internally, should work)
- **File**: `Oproto.FluentDynamoDb/Requests/TransactionGetResponse.cs`
- **Requirement**: 3.1, 3.2

### Task 3.2: Update TransactionGetBuilder to pass options
- [x] Identify where `TransactionGetResponse` is constructed
- [x] Add `_options` field to track options from added builders
- [x] Capture options from first builder with options in `Add()` method
- [x] Pass `_options` when constructing `TransactionGetResponse`
- **File**: `Oproto.FluentDynamoDb/Requests/TransactionGetBuilder.cs`
- **Requirement**: 3.3

## Phase 4: HydratorGenerator Options Propagation

### Task 4.1: Update IAsyncEntityHydrator interface
- [x] Add `FluentDynamoDbOptions? options = null` parameter to single-item `HydrateAsync`
- [x] Add `FluentDynamoDbOptions? options = null` parameter to multi-item `HydrateAsync`
- [x] Add `FluentDynamoDbOptions? options = null` parameter to `SerializeAsync`
- [x] Update XML documentation
- **File**: `Oproto.FluentDynamoDb/Storage/IAsyncEntityHydrator.cs`
- **Requirement**: 4.1

### Task 4.2: Update HydratorGenerator
- [x] Update `GenerateHydrateAsyncSingleMethod` to:
  - Add `FluentDynamoDbOptions? options = null` parameter
  - Pass `options` to `FromDynamoDbAsync`
  - Extract `fieldEncryptor` from `options?.FieldEncryptor`
- [x] Update `GenerateHydrateAsyncMultiMethod` to:
  - Add `FluentDynamoDbOptions? options = null` parameter
  - Pass `options` to `FromDynamoDbAsync`
  - Extract `fieldEncryptor` from `options?.FieldEncryptor`
- [x] Update `GenerateSerializeAsyncMethod` to accept options
- [x] Add `using Oproto.FluentDynamoDb;` to generated code
- **File**: `Oproto.FluentDynamoDb.SourceGenerator/Generators/HydratorGenerator.cs`
- **Requirement**: 4.2

### Task 4.3: Update hydrator callers
- [x] Search for usages of `IAsyncEntityHydrator<T>.HydrateAsync`
- [x] Update callers to pass available options
- [x] Verify `EntityExecuteAsyncExtensions` passes options
- **File**: `Oproto.FluentDynamoDb/Requests/Extensions/EntityExecuteAsyncExtensions.cs`
- **Requirement**: 4.3

## Phase 5: Changelog Updates

### Task 5.1: Update CHANGELOG.md
- [x] Add entry under `### Fixed` section for documentation corrections
- [x] Add entry under `### Fixed` section for BatchGetResponse options propagation
- [x] Add entry under `### Fixed` section for TransactionGetResponse options propagation
- [x] Add entry under `### Fixed` section for HydratorGenerator options propagation
- **File**: `CHANGELOG.md`
- **Requirement**: 5.1

### Task 5.2: Update docs/DOCUMENTATION_CHANGELOG.md
- [x] Add new date section `## [2025-12-04]`
- [x] Add entries for each documentation file corrected
- [x] Include before/after code examples
- [x] Include reason for each correction
- **File**: `docs/DOCUMENTATION_CHANGELOG.md`
- **Requirement**: 5.2

## Phase 6: Testing

### Task 6.1: Add BatchGetResponse unit tests
- [ ] Test `GetItem<T>()` with entity containing `[JsonBlob]` property and options
- [ ] Test `GetItem<T>()` with entity without `[JsonBlob]` and null options
- [ ] Verify JSON deserialization works correctly
- **Requirement**: 6.1

### Task 6.2: Add TransactionGetResponse unit tests
- [ ] Test `GetItem<T>()` with entity containing `[JsonBlob]` property and options
- [ ] Test `GetItem<T>()` with entity without `[JsonBlob]` and null options
- [ ] Verify JSON deserialization works correctly
- **Requirement**: 6.2

### Task 6.3: Add HydratorGenerator unit tests
- [ ] Verify generated code includes options parameter
- [ ] Verify generated code passes options to `FromDynamoDbAsync`
- [ ] Verify generated code extracts `FieldEncryptor` from options
- **Requirement**: 6.3

### Task 6.4: Build and verify
- [x] Run `dotnet build` to verify no compilation errors
- [x] Build succeeds with 0 warnings and 0 errors
- [ ] Run `dotnet test` to verify all tests pass
- **Requirement**: 6.4

## Completion Checklist

- [x] All documentation files updated (Tasks 1.1-1.11)
- [x] BatchGetResponse propagates options (Tasks 2.1-2.2)
- [x] TransactionGetResponse propagates options (Tasks 3.1-3.2)
- [x] HydratorGenerator generates correct code (Tasks 4.1-4.3)
- [x] CHANGELOG.md updated (Task 5.1)
- [x] docs/DOCUMENTATION_CHANGELOG.md updated (Task 5.2)
- [ ] All tests pass (Tasks 6.1-6.4) - Unit tests not yet added
- [x] Build succeeds with no new warnings
