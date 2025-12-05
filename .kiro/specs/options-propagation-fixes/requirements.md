# Options Propagation and Documentation Fixes

## Overview

This spec addresses two related issues discovered after the JSON serializer refactor:

1. **Documentation Issue**: Many documentation files still use `Put().ExecuteAsync()` instead of the correct `PutAsync()` method
2. **Architectural Issue**: `BatchGetResponse`, `TransactionGetResponse`, and `HydratorGenerator` pass `options: null` to `FromDynamoDb()`, which means JSON deserialization and logging won't work for entities retrieved through these paths

## Problem Statement

### Documentation Problem

Despite previous documentation cleanup efforts, there are still ~20+ occurrences of incorrect `Put().ExecuteAsync()` or `Put(entity).ExecuteAsync()` patterns in documentation files. The correct method is `PutAsync()`.

### Architectural Problem

After the JSON serializer refactor, `FluentDynamoDbOptions` is required for:
- JSON deserialization of `[JsonBlob]` properties
- Logging via `IDynamoDbLogger`
- Field encryption via `IFieldEncryptor`

However, several code paths pass `options: null`:

1. **BatchGetResponse.GetItem<TEntity>()** (line 159):
   ```csharp
   return TEntity.FromDynamoDb<TEntity>(item, options: null);
   ```

2. **TransactionGetResponse.GetItem<TEntity>()** (line 79):
   ```csharp
   return TEntity.FromDynamoDb<TEntity>(item, options: null);
   ```

3. **HydratorGenerator** generates code with `options: null` in:
   - `GenerateHydrateAsyncSingleMethod` (line 122)
   - `GenerateHydrateAsyncMultiMethod` (line 157)

This means entities with `[JsonBlob]` properties retrieved via batch get, transaction get, or async hydration will fail at runtime.

## Requirements

### 1. Documentation Fixes

1.1. GIVEN documentation files with `Put().ExecuteAsync()` or `Put(entity).ExecuteAsync()`, WHEN I search the docs folder, THEN all occurrences SHALL be replaced with `PutAsync()`

1.2. GIVEN the files identified in the search results, WHEN I update them, THEN the following files SHALL be corrected:
- `docs/advanced-topics/AdvancedTypes.md`
- `docs/advanced-topics/TableGenerationCustomization.md`
- `docs/core-features/Transactions.md`
- `docs/core-features/BatchOperations.md`
- `docs/QUICK_REFERENCE.md`
- `docs/DeveloperGuide.md`
- `docs/reference/AttributeReference.md`
- `docs/DOCUMENTATION_CHANGELOG.md`
- `.kiro/specs/integration-test-build-fixes/design.md`
- `Oproto.FluentDynamoDb.SystemTextJson/README.md`
- `Oproto.FluentDynamoDb.NewtonsoftJson/README.md`

### 2. BatchGetResponse Options Propagation

2.1. GIVEN the `BatchGetResponse` class, WHEN it is constructed, THEN it SHALL accept an optional `FluentDynamoDbOptions?` parameter

2.2. GIVEN the `BatchGetResponse.GetItem<TEntity>()` method, WHEN deserializing an entity, THEN it SHALL pass the stored options to `FromDynamoDb()`

2.3. GIVEN the `BatchGetBuilder` class, WHEN constructing `BatchGetResponse`, THEN it SHALL pass the options from the request builders

### 3. TransactionGetResponse Options Propagation

3.1. GIVEN the `TransactionGetResponse` class, WHEN it is constructed, THEN it SHALL accept an optional `FluentDynamoDbOptions?` parameter

3.2. GIVEN the `TransactionGetResponse.GetItem<TEntity>()` method, WHEN deserializing an entity, THEN it SHALL pass the stored options to `FromDynamoDb()`

3.3. GIVEN the `TransactGetItemsRequestBuilder` class, WHEN constructing `TransactionGetResponse`, THEN it SHALL pass the options from the request builders

### 4. HydratorGenerator Options Propagation

4.1. GIVEN the `IAsyncEntityHydrator<T>` interface, WHEN I review the `HydrateAsync` methods, THEN they SHALL accept an optional `FluentDynamoDbOptions?` parameter

4.2. GIVEN the `HydratorGenerator`, WHEN generating `HydrateAsync` methods, THEN the generated code SHALL accept and pass `FluentDynamoDbOptions?` to `FromDynamoDbAsync()`

4.3. GIVEN callers of `IAsyncEntityHydrator<T>.HydrateAsync()`, WHEN invoking hydration, THEN they SHALL pass the available options

### 5. Changelog Updates

5.1. GIVEN the `CHANGELOG.md` file, WHEN these fixes are complete, THEN it SHALL include entries under "Fixed" for:
- Documentation API corrections for `Put().ExecuteAsync()` → `PutAsync()`
- Options propagation fix for BatchGetResponse
- Options propagation fix for TransactionGetResponse
- Options propagation fix for HydratorGenerator

5.2. GIVEN the `docs/DOCUMENTATION_CHANGELOG.md` file, WHEN documentation is updated, THEN it SHALL include entries for all corrected files with before/after examples

### 6. Testing

6.1. GIVEN entities with `[JsonBlob]` properties, WHEN retrieved via `BatchGetResponse.GetItem<T>()`, THEN JSON deserialization SHALL work correctly

6.2. GIVEN entities with `[JsonBlob]` properties, WHEN retrieved via `TransactionGetResponse.GetItem<T>()`, THEN JSON deserialization SHALL work correctly

6.3. GIVEN entities with blob references, WHEN hydrated via `IAsyncEntityHydrator<T>`, THEN JSON deserialization SHALL work correctly

## Affected Files

### Documentation Files (Put().ExecuteAsync() → PutAsync())
- `docs/advanced-topics/AdvancedTypes.md`
- `docs/advanced-topics/TableGenerationCustomization.md`
- `docs/core-features/Transactions.md`
- `docs/core-features/BatchOperations.md`
- `docs/QUICK_REFERENCE.md`
- `docs/DeveloperGuide.md`
- `docs/reference/AttributeReference.md`
- `docs/DOCUMENTATION_CHANGELOG.md`
- `.kiro/specs/integration-test-build-fixes/design.md`
- `Oproto.FluentDynamoDb.SystemTextJson/README.md`
- `Oproto.FluentDynamoDb.NewtonsoftJson/README.md`

### Source Files (Options Propagation)
- `Oproto.FluentDynamoDb/Requests/BatchGetResponse.cs`
- `Oproto.FluentDynamoDb/Requests/TransactionGetResponse.cs`
- `Oproto.FluentDynamoDb/Requests/BatchGetBuilder.cs`
- `Oproto.FluentDynamoDb/Requests/TransactGetItemsRequestBuilder.cs`
- `Oproto.FluentDynamoDb/Storage/IAsyncEntityHydrator.cs`
- `Oproto.FluentDynamoDb.SourceGenerator/Generators/HydratorGenerator.cs`

### Changelog Files
- `CHANGELOG.md`
- `docs/DOCUMENTATION_CHANGELOG.md`

## Out of Scope

- Changes to other request builders (already properly propagate options)
- Changes to synchronous `FromDynamoDb` paths that already work
- New features or API additions
