# Requirements Document

## Introduction

This feature removes the legacy `IBlobStorageProvider` parameter overloads from all async terminal methods in `EntityExecuteAsyncExtensions` and corresponding FluentResults wrappers. These overloads predate the options-based configuration (`FluentDynamoDbOptions.WithBlobStorage()`) and have become a maintenance burden with known bugs, null-safety mismatches, and behavioral divergence from the primary (options-based) code path. This is a breaking API change that consolidates hydration to a single, well-tested path.

## Glossary

- **Hydration_Path**: The code path that resolves blob references and encrypted fields during entity deserialization from DynamoDB attribute maps into strongly-typed entities.
- **Path_A**: The options-based hydration path that resolves `IBlobStorageProvider` from `FluentDynamoDbOptions.BlobStorageProvider`. This is the current primary path.
- **Path_B**: The legacy explicit-parameter hydration path where `IBlobStorageProvider` is passed directly to each terminal method call. This is the path being removed.
- **EntityExecuteAsyncExtensions**: The static class containing extension methods for executing DynamoDB operations and mapping results to entities.
- **FluentResultsExtensions**: The static class providing `Result<T>`-returning wrappers around `EntityExecuteAsyncExtensions` methods.
- **Terminal_Method**: An extension method that executes a DynamoDB operation and returns a result (e.g., `GetItemAsync`, `ToListAsync`, `PutAsync`, `ToCompositeEntityAsync`, `ToCompositeEntityListAsync`).
- **IBlobStorageProvider**: The interface for blob storage operations used during hydration of entities with `[BlobReference]` properties.
- **FluentDynamoDbOptions**: The configuration object that holds options including the blob storage provider, hydrator registry, encryption settings, and other runtime configuration.
- **HydratorRegistry**: The registry within `FluentDynamoDbOptions` that maps entity types to their async hydration implementations.
- **ApiConsistencyTests**: Compile-time tests that validate the public API surface by ensuring documented patterns compile correctly.

## Requirements

### Requirement 1: Remove IBlobStorageProvider Overloads from EntityExecuteAsyncExtensions

**User Story:** As a library maintainer, I want to remove the legacy `IBlobStorageProvider` parameter overloads from `EntityExecuteAsyncExtensions`, so that the codebase has a single hydration path and eliminates the source of divergence bugs.

#### Acceptance Criteria

1. THE EntityExecuteAsyncExtensions class SHALL NOT contain the following public method overloads: `GetItemAsync<T>(GetItemRequestBuilder<T>, IBlobStorageProvider, CancellationToken)`, `ToListAsync<T>(QueryRequestBuilder<T>, IBlobStorageProvider, CancellationToken)`, `ToListAsync<T>(ScanRequestBuilder<T>, IBlobStorageProvider, CancellationToken)`, `ToCompositeEntityListAsync<T>(QueryRequestBuilder<T>, IBlobStorageProvider, CancellationToken)`, `ToCompositeEntityListAsync<T>(ScanRequestBuilder<T>, IBlobStorageProvider, CancellationToken)`, `ToCompositeEntityAsync<T>(QueryRequestBuilder<T>, IBlobStorageProvider, CancellationToken)`, `ToCompositeEntityAsync<T>(ScanRequestBuilder<T>, IBlobStorageProvider, CancellationToken)`, and `PutAsync<T>(PutItemRequestBuilder<T>, IBlobStorageProvider, CancellationToken)`.
2. THE EntityExecuteAsyncExtensions class SHALL provide only the options-based overloads for `GetItemAsync`, `ToListAsync`, `ToCompositeEntityListAsync`, `ToCompositeEntityAsync`, and `PutAsync` that resolve blob providers via `FluentDynamoDbOptions.BlobStorageProvider`.
3. THE EntityExecuteAsyncExtensions class SHALL retain all existing terminal methods that resolve blob providers from `FluentDynamoDbOptions` with identical method signatures and identical observable behavior for existing callers.

### Requirement 2: Remove IBlobStorageProvider Overloads from FluentResultsExtensions

**User Story:** As a library maintainer, I want to remove the legacy `IBlobStorageProvider` parameter overloads from `FluentResultsExtensions`, so that the FluentResults API surface stays consistent with the core library.

#### Acceptance Criteria

1. THE FluentResultsExtensions class SHALL NOT contain any public method overloads that accept an `IBlobStorageProvider` parameter.
2. WHEN a consumer calls `GetItemAsyncResult`, `ToListAsyncResult` (for both `QueryRequestBuilder<T>` and `ScanRequestBuilder<T>`), or `PutAsyncResult`, THE FluentResultsExtensions class SHALL provide only the overload that does not accept an `IBlobStorageProvider` parameter.
3. WHEN a consumer calls `ToCompositeEntityListAsyncResult` or `ToCompositeEntityAsyncResult`, THE FluentResultsExtensions class SHALL provide only the overload that does not accept an `IBlobStorageProvider` parameter.
4. THE FluentResultsExtensions class SHALL retain all existing Result-returning wrappers that do not accept an `IBlobStorageProvider` parameter, including `GetItemAsyncResult`, `ToListAsyncResult`, `ToCompositeEntityListAsyncResult`, `ToCompositeEntityAsyncResult`, `PutAsyncResult`, `UpdateAsyncResult`, `DeleteAsyncResult`, `ExecuteAsyncResult`, `ExecuteAndMapAsyncResult`, and `ToListAsyncResult` for `PartiQLRequestBuilder<T>`, without modification to their signatures or behavior.
5. WHEN the project is compiled after the removal, THE build SHALL produce zero compiler errors and zero compiler warnings related to the removed overloads.

### Requirement 3: Remove Associated API Consistency Tests

**User Story:** As a library maintainer, I want to remove API consistency tests that validate the removed overloads, so that the test suite compiles and reflects the current API surface.

#### Acceptance Criteria

1. WHEN the `IBlobStorageProvider` overloads are removed from `EntityExecuteAsyncExtensions` and `FluentResultsExtensions`, THE ApiConsistencyTests project SHALL remove all test methods whose names contain "WithBlobProvider" that invoke terminal methods with an explicit `IBlobStorageProvider` parameter.
2. WHEN the `IBlobStorageProvider` overloads are removed, THE ApiConsistencyTests project SHALL remove all lines within cancellation-token test methods that call terminal methods with an `IBlobStorageProvider` parameter and remove the associated `var blobProvider = Substitute.For<IBlobStorageProvider>()` declarations that become unused.
3. WHEN the `IBlobStorageProvider` overloads are removed, THE ApiConsistencyTests project SHALL remove the `using Oproto.FluentDynamoDb.Providers.BlobStorage;` directive from any file that no longer references `IBlobStorageProvider`.
4. THE ApiConsistencyTests project SHALL compile successfully with zero errors after all `IBlobStorageProvider`-referencing test code is removed.
5. THE ApiConsistencyTests project SHALL retain all test methods that validate options-based terminal method overloads (i.e., test methods that invoke `GetItemAsyncResult()`, `PutAsyncResult()`, `ToListAsyncResult()`, `ToCompositeEntityAsyncResult()`, `ToCompositeEntityListAsyncResult()` without an `IBlobStorageProvider` parameter), including tests for cancellation-token-only overloads.

### Requirement 4: Document Breaking Change in CHANGELOG

**User Story:** As a library consumer, I want the breaking change documented in the CHANGELOG, so that I understand how to migrate my code when upgrading.

#### Acceptance Criteria

1. WHEN the overloads are removed, THE CHANGELOG SHALL contain an entry under an "Unreleased" heading with a "Removed" section describing the removed `IBlobStorageProvider` parameter overloads from `EntityExecuteAsyncExtensions` and `FluentResultsExtensions`.
2. THE CHANGELOG entry SHALL include a migration example showing the before pattern (`await table.Users.Get(userId).GetItemAsync(blobProvider)`) and after pattern (`new FluentDynamoDbOptions().WithBlobStorage(blobProvider)` at table construction time).
3. THE CHANGELOG entry SHALL list all eight affected method names from `EntityExecuteAsyncExtensions` and corresponding FluentResults wrappers that were removed.
4. THE CHANGELOG entry SHALL be placed at the top of the file following [Keep a Changelog](https://keepachangelog.com/) conventions.

### Requirement 5: Update Steering Documentation

**User Story:** As a library maintainer, I want the steering documentation updated to remove references to the per-call blob provider pattern, so that AI assistants and contributors do not suggest the removed API pattern.

#### Acceptance Criteria

1. IF the `.kiro/steering/fluentdynamodb.md` file contains any code examples or prose that pass an `IBlobStorageProvider` instance as a parameter to terminal methods (e.g., `.GetItemAsync(blobProvider)`, `.ToListAsync(blobProvider)`, `.PutAsync(blobProvider)`), THEN those references SHALL be removed.
2. THE `.kiro/steering/fluentdynamodb.md` file SHALL document only the options-based pattern (`new FluentDynamoDbOptions().WithBlobStorage(...)`) for configuring blob storage.
3. THE `.kiro/steering/fluentdynamodb.md` file SHALL NOT contain any method signatures or examples showing `IBlobStorageProvider` as a parameter to terminal methods.

### Requirement 6: Ensure Retained Path Handles All Hydration Scenarios

**User Story:** As a library consumer, I want the retained options-based hydration path to correctly handle all entity types (blob, encrypted, composite), so that removing the legacy path does not introduce regressions.

#### Acceptance Criteria

1. WHEN a `GetItemAsync` operation is performed on an entity with a registered hydrator, THE retained path SHALL resolve the blob provider from `FluentDynamoDbOptions.BlobStorageProvider` and pass it to `IAsyncEntityHydrator<T>.HydrateAsync` to produce the deserialized entity.
2. WHEN a `PutAsync` operation is performed on an entity with deferred encrypted serialization, THE retained path SHALL invoke the registered hydrator's `SerializeAsync` method to produce the final `Dictionary<string, AttributeValue>` before building the DynamoDB `PutItemRequest`.
3. WHEN a `ToListAsync` operation returns multiple items requiring hydration, THE retained path SHALL hydrate each item sequentially (one at a time, not in parallel) with `ConfigureAwait(false)` on each await, preserving ordering consistent with the DynamoDB response.
4. WHEN a `ToCompositeEntityAsync` or `ToCompositeEntityListAsync` operation is performed, THE retained path SHALL group items by partition key, identify primary and related entities by sort key pattern matching against `[RelatedEntity]` attributes, and hydrate the assembled composite structure including all related entity collections.
5. IF `FluentDynamoDbOptions.HydratorRegistry` is null or `GetHydrator<T>()` returns null, THEN THE retained path SHALL skip async hydration and fall back to the synchronous `FromDynamoDb` mapping without throwing an exception.
6. WHEN a `ToListAsync` or `ToCompositeEntityListAsync` operation is performed via a `ScanRequestBuilder`, THE retained path SHALL apply the same hydration logic (hydrator lookup, sequential hydration, fallback to synchronous mapping) as the equivalent `QueryRequestBuilder` operations.
