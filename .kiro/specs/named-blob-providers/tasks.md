# Implementation Plan: Named Blob Providers

## Overview

This plan implements named blob storage provider support for FluentDynamoDb, enabling per-property blob provider resolution. The implementation follows the copy-on-write immutable pattern already used by `FluentDynamoDbOptions`, adds a `Provider` property to `BlobStorageAttribute`, extends the source generators (HydratorGenerator and MapperGenerator) to emit per-property `GetBlobProvider` calls, and includes property-based tests with FsCheck plus unit tests.

## Tasks

- [x] 1. Add Provider property to BlobStorageAttribute and extend FluentDynamoDbOptions
  - [x] 1.1 Add `Provider` property to `BlobStorageAttribute`
    - Add `public string? Provider { get; set; }` to `Oproto.FluentDynamoDb/Attributes/BlobStorageAttribute.cs`
    - Property defaults to `null` (preserving backwards compatibility)
    - Add XML documentation referencing `WithBlobStorage(name, provider)` registration
    - _Requirements: 3.1, 3.2, 3.3, 5.4, 5.5_

  - [x] 1.2 Add `NamedBlobProviders` property and `WithBlobStorage(string name, IBlobStorageProvider provider)` method to `FluentDynamoDbOptions`
    - Add `internal ImmutableDictionary<string, IBlobStorageProvider> NamedBlobProviders { get; private init; } = ImmutableDictionary<string, IBlobStorageProvider>.Empty;` property
    - Add `using System.Collections.Immutable;` import
    - Implement `WithBlobStorage(string name, IBlobStorageProvider provider)` using `ArgumentException.ThrowIfNullOrWhiteSpace(name)` and `ArgumentNullException.ThrowIfNull(provider)`
    - Method calls `CloneWith(namedBlobProviders: NamedBlobProviders.SetItem(name, provider))` to return new instance
    - _Requirements: 1.1, 1.2, 1.3, 1.4, 1.5, 1.6, 6.3_

  - [x] 1.3 Add `GetBlobProvider(string? name)` resolution method to `FluentDynamoDbOptions`
    - If `name` is null or empty, return `BlobStorageProvider` or throw `InvalidOperationException` with message suggesting `.WithBlobStorage(provider)`
    - If named provider found in registry, return it
    - If not found and registry is empty, throw with message stating no named providers configured
    - If not found and registry has entries, throw with message listing all available provider names (sorted)
    - _Requirements: 2.1, 2.2, 2.3, 2.4, 7.1, 7.2, 7.3, 7.4_

  - [x] 1.4 Update `CloneWith` method to include `namedBlobProviders` parameter
    - Add `ImmutableDictionary<string, IBlobStorageProvider>? namedBlobProviders = null` parameter
    - Add `NamedBlobProviders = namedBlobProviders ?? NamedBlobProviders` to the returned instance
    - _Requirements: 6.1, 6.2, 6.3_

- [x] 2. Checkpoint - Verify library compiles and existing tests pass
  - Ensure all tests pass, ask the user if questions arise.

- [x] 3. Property-based tests for named blob provider registration and resolution
  - [x] 3.1 Write property test for registration round-trip
    - **Property 1: Registration Round-Trip**
    - **Validates: Requirements 1.1, 1.2, 2.1**
    - For any valid provider name and any IBlobStorageProvider, registering via `WithBlobStorage(name, provider)` then calling `GetBlobProvider(name)` returns the same instance
    - Use FsCheck string generator filtered for non-null/non-empty/non-whitespace names
    - Minimum 100 iterations

  - [x] 3.2 Write property test for invalid name rejection
    - **Property 2: Invalid Name Rejection**
    - **Validates: Requirements 1.3**
    - For any string that is null, empty, or whitespace-only, `WithBlobStorage(name, provider)` throws `ArgumentException`
    - Generate from whitespace character set including null and empty
    - Minimum 100 iterations

  - [x] 3.3 Write property test for replacement semantics
    - **Property 3: Replacement Semantics**
    - **Validates: Requirements 1.6**
    - For any valid name and two distinct providers A and B, registering A then B under the same name results in `GetBlobProvider(name)` returning B
    - Minimum 100 iterations

  - [x] 3.4 Write property test for missing provider error with diagnostic info
    - **Property 4: Missing Provider Error with Diagnostic Info**
    - **Validates: Requirements 2.3, 7.1, 7.2**
    - For any set of registered names and a name not in that set, `GetBlobProvider(missingName)` throws `InvalidOperationException` whose message contains the requested name and lists available providers
    - Minimum 100 iterations

  - [x] 3.5 Write property test for registration preservation through chaining
    - **Property 5: Registration Preservation Through Chaining**
    - **Validates: Requirements 6.1, 6.2**
    - For any sequence of registrations (one default + N named), the final instance exposes all via `GetBlobProvider`
    - Minimum 100 iterations

  - [x] 3.6 Write property test for copy-on-write immutability
    - **Property 6: Copy-on-Write Immutability**
    - **Validates: Requirements 6.3**
    - For any instance with existing registrations, calling `WithBlobStorage(name, provider)` returns a new instance without mutating the original
    - Minimum 100 iterations

- [x] 4. Unit tests for FluentDynamoDbOptions named blob providers
  - [x] 4.1 Write unit tests for `GetBlobProvider` edge cases and error messages
    - `GetBlobProvider(null)` returns default provider when configured
    - `GetBlobProvider("")` returns default provider when configured
    - `GetBlobProvider(null)` throws when no default is configured, message suggests `WithBlobStorage(provider)`
    - `GetBlobProvider("missing")` throws with message containing "missing" and listing available providers
    - `GetBlobProvider("missing")` with empty registry throws with "no named providers" message
    - `WithBlobStorage(null, provider)` throws `ArgumentException`
    - `WithBlobStorage("name", null)` throws `ArgumentNullException`
    - _Requirements: 2.2, 2.3, 2.4, 7.1, 7.2, 7.3, 7.4_

  - [x] 4.2 Write unit tests for `BlobStorageAttribute` Provider property
    - `Provider` property defaults to `null`
    - `LazyLoad` property still defaults to `false`
    - `Provider` can be set to a non-empty string
    - _Requirements: 3.1, 5.4_

- [x] 5. Source generator changes for per-property blob provider resolution
  - [x] 5.1 Add `BlobStorageProviderName` to `ComplexTypeInfo` model
    - Add `public string? BlobStorageProviderName { get; set; }` to `Oproto.FluentDynamoDb.SourceGenerator/Models/ComplexTypeInfo.cs`
    - Add XML documentation explaining it carries the `[BlobStorage(Provider = "x")]` value
    - _Requirements: 4.1, 4.2, 4.3_

  - [x] 5.2 Update `ComplexTypeAnalyzer` to extract `Provider` from `BlobStorageAttribute`
    - In the analysis phase, read the `Provider` named argument from the `[BlobStorage]` attribute
    - Assign to `complexTypeInfo.BlobStorageProviderName`
    - Handle the case where Provider is not set (null)
    - _Requirements: 4.3, 4.4_

  - [x] 5.3 Update `MapperGenerator` to emit per-property `GetBlobProvider` calls
    - In `GenerateBlobStoragePropertyToAttributeValue`, emit `var blobProvider_{PropertyName} = options.GetBlobProvider({providerNameLiteral});` where `providerNameLiteral` is `null` or `"name"`
    - In `GenerateBlobStoragePropertyFromAttributeValue`, emit the same pattern for deserialization
    - Replace usage of the single `blobProvider` parameter with per-property resolved provider variable
    - Ensure `.ConfigureAwait(false)` on all async calls in generated code
    - _Requirements: 4.1, 4.2, 4.3, 4.4, 4.5_

  - [x] 5.4 Update `HydratorGenerator` to emit per-property `GetBlobProvider` calls
    - In `GenerateHydrateAsyncSingleMethod` and `GenerateHydrateAsyncMultiMethod`, ensure generated code resolves provider per property via `options.GetBlobProvider(...)`
    - In `GenerateSerializeAsyncMethod`, ensure generated code resolves provider per property
    - The `blobProvider` parameter remains for backwards compatibility but per-property resolution uses `options.GetBlobProvider(...)` internally
    - _Requirements: 4.1, 4.2, 4.3, 4.4, 4.5, 5.1, 5.2, 5.3_

- [x] 6. Checkpoint - Verify source generator compiles and generates correct code
  - Ensure all tests pass, ask the user if questions arise.

- [x] 7. Integration tests for source generator output
  - [x] 7.1 Write integration tests verifying generated code uses correct blob provider resolution
    - Entity with single `[BlobStorage]` (no Provider) verifies generated code calls `GetBlobProvider(null)`
    - Entity with `[BlobStorage(Provider = "docs")]` verifies generated code calls `GetBlobProvider("docs")`
    - Entity with multiple blob properties using different providers verifies per-property resolution
    - Entity mixing default + named providers verifies correct routing
    - Backwards compatibility: existing entity without Provider compiles without changes
    - _Requirements: 3.2, 3.3, 4.1, 4.2, 4.3, 4.4, 5.2, 5.5_

- [x] 8. Documentation and changelog updates
  - [x] 8.1 Update `CHANGELOG.md` with Named Blob Providers entry
    - Add entry under `[Unreleased]` → `### Added` section
    - Document `WithBlobStorage(string name, IBlobStorageProvider provider)` overload
    - Document `GetBlobProvider(string? name)` resolution method
    - Document `BlobStorageAttribute.Provider` property
    - Document per-property provider resolution in generated code
    - Include migration example showing single-provider vs multi-provider configuration
    - _Requirements: 1.1, 2.1, 3.1, 5.1, 5.5_

  - [x] 8.2 Add feature documentation page at `docs/core-features/NamedBlobProviders.md`
    - Document motivation (multiple blob backends per entity)
    - Document `[BlobStorage(Provider = "name")]` attribute usage with examples
    - Document `FluentDynamoDbOptions` registration patterns (default + named)
    - Document `GetBlobProvider` resolution behavior and error scenarios
    - Document backwards compatibility (existing entities work unchanged)
    - Include complete end-to-end example with multiple providers
    - _Requirements: 1.1, 2.1, 3.1, 5.1, 7.1_

  - [x] 8.3 Update `docs/DOCUMENTATION_CHANGELOG.md` with new feature entry
    - Add entry dated with current date under "New Feature Documentation" category
    - Reference `docs/core-features/NamedBlobProviders.md`
    - Include Before/After code examples showing single-provider vs named-provider patterns
    - Document the new `Provider` property on `[BlobStorage]` attribute
    - _Requirements: 3.1, 5.5_

  - [x] 8.4 Update `.kiro/steering/fluentdynamodb.md` with named blob provider configuration
    - Update the "Setup & DI" section to show `WithBlobStorage(name, provider)` overload in options example
    - Add brief mention of per-property blob provider configuration capability
    - Keep within the 500-line limit for the steering file
    - _Requirements: 1.1, 3.1_

- [x] 9. Final checkpoint - Ensure all tests pass
  - Ensure all tests pass, ask the user if questions arise.

## Notes

- Tasks marked with `*` are optional and can be skipped for faster MVP
- Each task references specific requirements for traceability
- Checkpoints ensure incremental validation
- Property tests validate universal correctness properties from the design document using FsCheck with xUnit
- Unit tests use xUnit, FluentAssertions (or AwesomeAssertions), and NSubstitute
- The source generator targets `netstandard2.0` — do not use `ImmutableDictionary` in the generator itself; it's only used in the runtime library
- After modifying the source generator, run `dotnet build-server shutdown` before rebuilding
- All library `await` calls must use `.ConfigureAwait(false)` per project conventions

## Task Dependency Graph

```json
{
  "waves": [
    { "id": 0, "tasks": ["1.1", "1.2"] },
    { "id": 1, "tasks": ["1.3", "1.4"] },
    { "id": 2, "tasks": ["3.1", "3.2", "3.3", "3.4", "3.5", "3.6", "4.1", "4.2", "5.1"] },
    { "id": 3, "tasks": ["5.2"] },
    { "id": 4, "tasks": ["5.3", "5.4"] },
    { "id": 5, "tasks": ["7.1"] },
    { "id": 6, "tasks": ["8.1", "8.2", "8.3", "8.4"] }
  ]
}
```
