# Implementation Plan: Consolidate Hydration Paths

## Overview

Remove all legacy `IBlobStorageProvider` parameter overloads from `EntityExecuteAsyncExtensions` (8 overloads + `WithItemAsync`) and `FluentResultsExtensions` (4 overloads), update API consistency tests, document the breaking change in CHANGELOG, and verify the steering documentation. All removals happen simultaneously since the FluentResults methods only call the EntityExecuteAsyncExtensions methods being removed.

## Tasks

- [x] 1. Remove IBlobStorageProvider overloads from FluentResultsExtensions
  - [x] 1.1 Remove the four IBlobStorageProvider overloads from FluentResultsExtensions.cs
    - Remove `GetItemAsyncResult<T>(GetItemRequestBuilder<T>, IBlobStorageProvider, CancellationToken)`
    - Remove `ToListAsyncResult<T>(QueryRequestBuilder<T>, IBlobStorageProvider, CancellationToken)`
    - Remove `ToListAsyncResult<T>(ScanRequestBuilder<T>, IBlobStorageProvider, CancellationToken)`
    - Remove `PutAsyncResult<T>(PutItemRequestBuilder<T>, IBlobStorageProvider, CancellationToken)`
    - Remove `using Oproto.FluentDynamoDb.Providers.BlobStorage;` if no remaining references exist
    - _Requirements: 2.1, 2.2, 2.3, 2.4, 2.5_

- [x] 2. Remove IBlobStorageProvider overloads from EntityExecuteAsyncExtensions
  - [x] 2.1 Remove the eight IBlobStorageProvider terminal method overloads and WithItemAsync from EntityExecuteAsyncExtensions.cs
    - Remove `GetItemAsync<T>(GetItemRequestBuilder<T>, IBlobStorageProvider, CancellationToken)`
    - Remove `ToListAsync<T>(QueryRequestBuilder<T>, IBlobStorageProvider, CancellationToken)`
    - Remove `ToListAsync<T>(ScanRequestBuilder<T>, IBlobStorageProvider, CancellationToken)`
    - Remove `ToCompositeEntityListAsync<T>(QueryRequestBuilder<T>, IBlobStorageProvider, CancellationToken)`
    - Remove `ToCompositeEntityListAsync<T>(ScanRequestBuilder<T>, IBlobStorageProvider, CancellationToken)`
    - Remove `ToCompositeEntityAsync<T>(QueryRequestBuilder<T>, IBlobStorageProvider, CancellationToken)`
    - Remove `ToCompositeEntityAsync<T>(ScanRequestBuilder<T>, IBlobStorageProvider, CancellationToken)`
    - Remove `PutAsync<T>(PutItemRequestBuilder<T>, IBlobStorageProvider, CancellationToken)`
    - Remove `WithItemAsync<T>(PutItemRequestBuilder<T>, T, IBlobStorageProvider, CancellationToken)`
    - Remove `using Oproto.FluentDynamoDb.Providers.BlobStorage;` if no remaining references exist
    - _Requirements: 1.1, 1.2, 1.3_

- [x] 3. Checkpoint - Verify library projects build
  - Ensure all tests pass, ask the user if questions arise.

- [x] 4. Update API consistency tests
  - [x] 4.1 Remove WithBlobProvider test methods and blob-provider lines from GetApiSurfaceFluentResults.cs
    - Remove `GetItemAsyncResult_WithBlobProvider_ShouldCompile` test method
    - Remove `var blobProvider = Substitute.For<IBlobStorageProvider>()` and associated blob-provider call lines from cancellation-token tests
    - Remove `using Oproto.FluentDynamoDb.Providers.BlobStorage;` if unused
    - _Requirements: 3.1, 3.2, 3.3, 3.5_
  - [x] 4.2 Remove WithBlobProvider test methods and blob-provider lines from PutApiSurfaceFluentResults.cs
    - Remove `PutAsyncResult_WithBlobProvider_ShouldCompile` test method
    - Remove `var blobProvider = Substitute.For<IBlobStorageProvider>()` and associated blob-provider call lines from cancellation-token tests
    - Remove `using Oproto.FluentDynamoDb.Providers.BlobStorage;` if unused
    - _Requirements: 3.1, 3.2, 3.3, 3.5_
  - [x] 4.3 Remove WithBlobProvider test methods and blob-provider lines from QueryApiSurfaceFluentResults.cs
    - Remove `ToListAsyncResult_WithBlobProvider_ShouldCompile` test method
    - Remove `var blobProvider = Substitute.For<IBlobStorageProvider>()` and associated blob-provider call lines from cancellation-token tests
    - Remove `using Oproto.FluentDynamoDb.Providers.BlobStorage;` if unused
    - _Requirements: 3.1, 3.2, 3.3, 3.5_
  - [x] 4.4 Remove WithBlobProvider test methods and blob-provider lines from ScanApiSurfaceFluentResults.cs
    - Remove `ToListAsyncResult_WithBlobProvider_ShouldCompile` test method
    - Remove `var blobProvider = Substitute.For<IBlobStorageProvider>()` and associated blob-provider call lines from cancellation-token tests
    - Remove `using Oproto.FluentDynamoDb.Providers.BlobStorage;` if unused
    - _Requirements: 3.1, 3.2, 3.3, 3.5_

- [x] 5. Checkpoint - Verify full solution builds and tests pass
  - Ensure all tests pass, ask the user if questions arise.

- [x] 6. Document breaking change and verify steering
  - [x] 6.1 Add breaking change entry to CHANGELOG.md
    - Add entry under the existing `## [Unreleased]` heading with a `### Removed` section
    - List all eight removed method names from `EntityExecuteAsyncExtensions` plus `WithItemAsync`
    - List all four removed method names from `FluentResultsExtensions`
    - Include migration example showing before pattern (`await table.Users.Get(userId).GetItemAsync(blobProvider)`) and after pattern (`new FluentDynamoDbOptions().WithBlobStorage(blobProvider)` at table construction time)
    - Follow [Keep a Changelog](https://keepachangelog.com/) conventions
    - _Requirements: 4.1, 4.2, 4.3, 4.4_
  - [x] 6.2 Verify steering documentation has no per-call IBlobStorageProvider examples
    - Check `.kiro/steering/fluentdynamodb.md` for any code examples passing `IBlobStorageProvider` to terminal methods
    - If found, remove those references and ensure only the options-based pattern is documented
    - Confirm no method signatures show `IBlobStorageProvider` as a parameter to terminal methods
    - _Requirements: 5.1, 5.2, 5.3_

- [x] 7. Final checkpoint - Ensure all tests pass
  - Ensure all tests pass, ask the user if questions arise.

## Notes

- This is a pure code-removal feature — no new logic is introduced
- Property-based testing does not apply since there are no new algorithms or input-dependent behavior
- The primary verification is binary: `dotnet build` succeeds and `dotnet test` passes
- All removals can happen simultaneously since the FluentResults blob-provider methods only call the EntityExecuteAsyncExtensions blob-provider methods
- The retained options-based path is unchanged and covered by the existing test suite
- Requirements 6.1–6.6 (retained path handling all hydration scenarios) are verified by the existing unit tests passing without modification

## Task Dependency Graph

```json
{
  "waves": [
    { "id": 0, "tasks": ["1.1", "2.1"] },
    { "id": 1, "tasks": ["4.1", "4.2", "4.3", "4.4"] },
    { "id": 2, "tasks": ["6.1", "6.2"] }
  ]
}
```
