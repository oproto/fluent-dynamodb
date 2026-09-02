# Implementation Plan: Async KMS Key Resolver

## Overview

Convert the `IKmsKeyResolver` interface from synchronous to asynchronous, add per-property key alias support via the `[Encrypted]` attribute, and thread the alias through the encryption pipeline. This is a breaking change to `IKmsKeyResolver`. Implementation uses C# 12 / .NET 8.0 with `ConfigureAwait(false)` on all library `await` calls.

## Tasks

- [x] 1. Update core interfaces and data models
  - [x] 1.1 Add `KeyAlias` property to `EncryptedAttribute`
    - Add `public string? KeyAlias { get; set; }` to `Oproto.FluentDynamoDb/Attributes/EncryptedAttribute.cs`
    - Default value is `null`
    - _Requirements: 4.1, 4.2_

  - [x] 1.2 Add `KeyAlias` property to `FieldEncryptionContext`
    - Add `public string? KeyAlias { get; init; }` to `Oproto.FluentDynamoDb/Providers/Encryption/FieldEncryptionContext.cs`
    - Default value is `null`
    - _Requirements: 5.1, 5.2_

  - [x] 1.3 Convert `IKmsKeyResolver` to async interface
    - Replace `string ResolveKeyId(string? contextId)` with `Task<string> ResolveKeyIdAsync(string? contextId, string? keyAlias = null, CancellationToken cancellationToken = default)`
    - Remove the synchronous method entirely
    - File: `Oproto.FluentDynamoDb.Encryption.Kms/IKmsKeyResolver.cs`
    - _Requirements: 1.1, 1.2, 1.3, 1.4, 1.5_

  - [x] 1.4 Add `KeyAlias` property to `FieldEncryptionException`
    - Add `public string? KeyAlias { get; }` property
    - Add new constructor overloads that accept `keyAlias` parameter
    - Maintain backward compatibility with existing constructor signatures
    - File: `Oproto.FluentDynamoDb.Encryption.Kms/FieldEncryptionException.cs`
    - _Requirements: 8.1, 8.2, 8.3_

- [x] 2. Implement `DefaultKmsKeyResolver` async with alias support
  - [x] 2.1 Rewrite `DefaultKmsKeyResolver` to implement async interface
    - Change constructor to accept `string defaultKeyId`, `IReadOnlyDictionary<string, string>? contextKeyMap = null`, `IReadOnlyDictionary<string, string>? aliasKeyMap = null`
    - Implement `ResolveKeyIdAsync` with resolution priority: aliasKeyMap → contextKeyMap → defaultKeyId
    - All code paths return `Task.FromResult` (no actual async work)
    - Case-sensitive lookups for both maps
    - Throw `ArgumentException` if `defaultKeyId` is null/empty/whitespace
    - File: `Oproto.FluentDynamoDb.Encryption.Kms/DefaultKmsKeyResolver.cs`
    - _Requirements: 2.1, 2.2, 2.3, 2.4, 2.5, 2.6, 2.7, 2.8_

  - [x] 2.2 Write unit tests for `DefaultKmsKeyResolver` async + alias
    - Update existing tests in `DefaultKmsKeyResolverTests.cs` from `ResolveKeyId` to `ResolveKeyIdAsync`
    - Add tests for alias lookup hit, alias lookup miss falling through to context, both maps miss returning default
    - Add case sensitivity tests for alias map
    - Add pre-cancelled token test → `OperationCanceledException`
    - Add constructor validation for new `aliasKeyMap` parameter
    - _Requirements: 2.1, 2.2, 2.3, 2.4, 2.5, 2.6, 2.7, 2.8_

  - [x] 2.3 Write property test: Resolution priority ordering
    - **Property 1: Resolution priority ordering**
    - **Validates: Requirements 2.2, 2.3, 2.4, 2.5, 2.8**
    - Generate random aliasKeyMap, contextKeyMap, defaultKeyId, contextId, and keyAlias inputs
    - Verify resolution follows alias > context > default priority with case-sensitive lookups
    - File: `Oproto.FluentDynamoDb.Encryption.Kms.UnitTests/DefaultKmsKeyResolverTests.cs` (or new property test file)

  - [x] 2.4 Write property test: Synchronous task completion
    - **Property 2: Synchronous task completion**
    - **Validates: Requirements 2.7**
    - Generate random inputs and verify `Task.IsCompletedSuccessfully == true` on returned task
    - File: `Oproto.FluentDynamoDb.Encryption.Kms.UnitTests/DefaultKmsKeyResolverTests.cs` (or new property test file)

- [x] 3. Checkpoint - Ensure core interfaces and DefaultKmsKeyResolver compile and tests pass
  - Ensure all tests pass, ask the user if questions arise.

- [x] 4. Update `AwsEncryptionSdkFieldEncryptor` for async key resolution
  - [x] 4.1 Update `EncryptAsync` and `DecryptAsync` to await `ResolveKeyIdAsync`
    - Replace `_keyResolver.ResolveKeyId(context.ContextId)` with `await _keyResolver.ResolveKeyIdAsync(context.ContextId, context.KeyAlias, cancellationToken).ConfigureAwait(false)`
    - Add `OperationCanceledException` catch block that rethrows without wrapping
    - Update `FieldEncryptionException` construction to include `KeyAlias` from context
    - Update null/empty key ARN check to throw `FieldEncryptionException` with key alias info
    - File: `Oproto.FluentDynamoDb.Encryption.Kms/AwsEncryptionSdkFieldEncryptor.cs`
    - _Requirements: 3.1, 3.2, 3.3, 3.4, 3.5, 3.6, 7.1, 7.2, 7.3, 7.4, 8.1, 8.2, 8.3_

  - [x] 4.2 Update unit tests for `AwsEncryptionSdkFieldEncryptor`
    - Update all mock setups from `ResolveKeyId(...)` to `ResolveKeyIdAsync(...)` returning `Task.FromResult(...)`
    - Add tests verifying `ResolveKeyIdAsync` is called with `context.ContextId` and `context.KeyAlias`
    - Add test for cancellation token forwarding to resolver
    - Add test: resolver throws `OperationCanceledException` → propagates unwrapped
    - Add test: resolver throws other exception → wrapped in `FieldEncryptionException` with KeyAlias
    - Add test: resolver returns null/empty → `FieldEncryptionException` with KeyAlias set
    - File: `Oproto.FluentDynamoDb.Encryption.Kms.UnitTests/AwsEncryptionSdkFieldEncryptorTests.cs`
    - _Requirements: 3.1, 3.2, 3.3, 3.4, 3.5, 3.6, 7.1, 7.2, 7.3, 7.4, 8.1, 8.2, 8.3_

  - [x] 4.3 Write property test: Context and alias forwarding
    - **Property 3: Context and alias forwarding**
    - **Validates: Requirements 3.3**
    - Generate random `FieldEncryptionContext` values with arbitrary ContextId and KeyAlias
    - Verify `ResolveKeyIdAsync` is invoked with matching contextId and keyAlias arguments
    - File: `Oproto.FluentDynamoDb.Encryption.Kms.UnitTests/AwsEncryptionSdkFieldEncryptorPropertyTests.cs`

  - [x] 4.4 Write property test: Non-cancellation exceptions are wrapped
    - **Property 4: Non-cancellation exceptions are wrapped**
    - **Validates: Requirements 3.5, 8.1**
    - Generate random exceptions (excluding `OperationCanceledException`)
    - Verify wrapping in `FieldEncryptionException` with correct FieldName, ContextId, KeyAlias, and InnerException
    - File: `Oproto.FluentDynamoDb.Encryption.Kms.UnitTests/AwsEncryptionSdkFieldEncryptorPropertyTests.cs`

  - [x] 4.5 Write property test: Invalid key return produces diagnostic exception
    - **Property 5: Invalid key return produces diagnostic exception**
    - **Validates: Requirements 1.6, 3.6, 8.2, 8.3**
    - Generate random field names, context IDs, and key aliases with null/whitespace returns from resolver
    - Verify `FieldEncryptionException` has correct FieldName, ContextId, KeyAlias, and message indicating invalid key
    - File: `Oproto.FluentDynamoDb.Encryption.Kms.UnitTests/AwsEncryptionSdkFieldEncryptorPropertyTests.cs`

- [x] 5. Checkpoint - Ensure encryptor compiles and all tests pass
  - Ensure all tests pass, ask the user if questions arise.

- [x] 6. Update source generator to propagate `KeyAlias`
  - [x] 6.1 Update `MapperGenerator` to emit `KeyAlias` in `FieldEncryptionContext`
    - In `GenerateEncryptedPropertyToAttributeValue`: emit `KeyAlias = "value"` in the `FieldEncryptionContext` initializer when `KeyAlias` is a non-empty, non-whitespace string on the attribute; omit when null/empty/whitespace
    - In `GenerateEncryptedPropertyFromAttributeValue`: same logic for the decryption context
    - Read the `KeyAlias` named argument from the `[Encrypted]` attribute in the source generator's property model
    - File: `Oproto.FluentDynamoDb.SourceGenerator/Generators/MapperGenerator.cs` (lines ~5607-5740)
    - Note: Run `dotnet build-server shutdown` after changes to restart the generator
    - _Requirements: 6.1, 6.2, 6.3, 4.3_

  - [x] 6.2 Write source generator tests for `KeyAlias` propagation
    - Test entity with `[Encrypted(KeyAlias = "pii")]` → emitted code includes `KeyAlias = "pii"`
    - Test entity with `[Encrypted]` (no KeyAlias) → emitted code omits `KeyAlias`
    - Test entity with `[Encrypted(KeyAlias = "")]` → emitted code omits `KeyAlias`
    - Test entity with `[Encrypted(KeyAlias = "   ")]` → emitted code omits `KeyAlias`
    - _Requirements: 6.1, 6.2, 6.3_

- [x] 7. Final checkpoint - Ensure full build passes and all tests pass
  - Run `dotnet build-server shutdown` then `dotnet build` then `dotnet test`
  - Ensure all tests pass, ask the user if questions arise.

- [x] 8. Documentation updates
  - [x] 8.1 Update `docs/advanced-topics/FieldLevelSecurity.md` with async resolver and key alias usage
    - Document the new `IKmsKeyResolver` async interface and `ResolveKeyIdAsync` method signature
    - Document the new `[Encrypted(KeyAlias = "...")]` attribute property with usage examples
    - Document the `DefaultKmsKeyResolver` constructor change (new `aliasKeyMap` parameter)
    - Show multi-tenant + data classification example (combining contextId and keyAlias)
    - Show convention-based resolver example using KMS aliases
    - Note this is a breaking change — consumers must update from `ResolveKeyId` to `ResolveKeyIdAsync`
    - File: `docs/advanced-topics/FieldLevelSecurity.md`

  - [x] 8.2 Update `docs/DOCUMENTATION_CHANGELOG.md` with async KMS key resolver changes
    - Add entry documenting the `IKmsKeyResolver` interface change (before/after code blocks)
    - Add entry documenting the new `[Encrypted(KeyAlias = "...")]` attribute property
    - Add entry documenting the `DefaultKmsKeyResolver` constructor change (new `aliasKeyMap` parameter)
    - Add entry documenting the `FieldEncryptionContext.KeyAlias` property addition
    - Include migration guidance for existing implementations
    - File: `docs/DOCUMENTATION_CHANGELOG.md`

  - [x] 8.3 Update `CHANGELOG.md` with async KMS key resolver feature
    - Add entry under `[Unreleased]` → `### Changed` for the breaking `IKmsKeyResolver` interface change
    - Add entry under `[Unreleased]` → `### Added` for per-property key alias support (`[Encrypted(KeyAlias)]`, `FieldEncryptionContext.KeyAlias`, `DefaultKmsKeyResolver` alias map)
    - Include migration code examples (before/after for `IKmsKeyResolver` implementations and `DefaultKmsKeyResolver` construction)
    - File: `CHANGELOG.md`

## Notes

- Tasks marked with `*` are optional and can be skipped for faster MVP
- Each task references specific requirements for traceability
- Checkpoints ensure incremental validation
- Property tests validate universal correctness properties from the design document
- Unit tests validate specific examples and edge cases
- This is a **breaking change** to `IKmsKeyResolver` — consumers must update implementations
- The `IFieldEncryptor` interface does NOT change
- All library `await` calls MUST use `.ConfigureAwait(false)`
- Source generator must be restarted after changes: `dotnet build-server shutdown`

## Task Dependency Graph

```json
{
  "waves": [
    { "id": 0, "tasks": ["1.1", "1.2", "1.3", "1.4"] },
    { "id": 1, "tasks": ["2.1"] },
    { "id": 2, "tasks": ["2.2", "2.3", "2.4", "4.1"] },
    { "id": 3, "tasks": ["4.2", "4.3", "4.4", "4.5"] },
    { "id": 4, "tasks": ["6.1"] },
    { "id": 5, "tasks": ["6.2"] },
    { "id": 6, "tasks": ["8.1", "8.2", "8.3"] }
  ]
}
```
