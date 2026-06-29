# Requirements Document

## Introduction

This feature converts the `IKmsKeyResolver` interface from synchronous to asynchronous and adds per-property key alias support. The synchronous `ResolveKeyId` method is the only blocking call in an otherwise fully-async encryption pipeline, forcing multi-tenant implementations into anti-patterns (preloading all mappings, blocking on async contexts, or maintaining separate caches). Additionally, per-property key alias support enables different encrypted properties on the same entity to use different KMS keys based on data classification (e.g., PII vs. financial data).

## Glossary

- **KMS_Key_Resolver**: The component responsible for mapping a context identifier and optional key alias to an AWS KMS key ARN or alias string. Implemented via the `IKmsKeyResolver` interface in the `Oproto.FluentDynamoDb.Encryption.Kms` package.
- **Field_Encryptor**: The component that performs field-level encryption and decryption using AWS Encryption SDK. Implemented via `AwsEncryptionSdkFieldEncryptor`.
- **Encryption_Context**: The `FieldEncryptionContext` record that carries metadata (context ID, key alias, cache TTL, etc.) through the encryption pipeline.
- **Source_Generator**: The Roslyn-based code generator (`MapperGenerator`) that emits serialization/deserialization code including `FieldEncryptionContext` construction.
- **Key_Alias**: An optional string identifier declared on the `[Encrypted]` attribute that represents a data classification category (e.g., "pii", "financial") used to select a specific KMS key.
- **Context_ID**: A runtime value (e.g., tenant ID, customer ID) that determines key isolation scope, passed to the KMS_Key_Resolver.
- **Encrypted_Attribute**: The `[Encrypted]` attribute applied to entity properties to mark them for field-level encryption.

## Requirements

### Requirement 1: Async Key Resolution Interface

**User Story:** As a library consumer implementing multi-tenant encryption, I want the key resolver interface to be async, so that I can dynamically resolve KMS keys from external sources without blocking the thread pool.

#### Acceptance Criteria

1. THE KMS_Key_Resolver interface SHALL declare a method named `ResolveKeyIdAsync` that returns `Task<string>` where the resolved string is a non-empty AWS KMS key ARN or KMS key alias
2. THE KMS_Key_Resolver interface `ResolveKeyIdAsync` method SHALL accept a nullable `contextId` parameter of type `string?` representing the tenant or scope identifier for key isolation
3. THE KMS_Key_Resolver interface `ResolveKeyIdAsync` method SHALL accept a nullable `keyAlias` parameter of type `string?` with a default value of `null` representing the data classification for per-property key selection
4. THE KMS_Key_Resolver interface `ResolveKeyIdAsync` method SHALL accept a `CancellationToken` parameter with a default value of `default`
5. THE KMS_Key_Resolver interface SHALL NOT contain the synchronous `ResolveKeyId` method
6. IF `ResolveKeyIdAsync` returns a null or empty string, THEN THE AwsEncryptionSdkFieldEncryptor SHALL throw an `ArgumentException` indicating the resolver returned an invalid key identifier
7. IF `ResolveKeyIdAsync` throws an `OperationCanceledException`, THEN THE AwsEncryptionSdkFieldEncryptor SHALL propagate the exception to the caller without wrapping it

### Requirement 2: Default Key Resolver Async Implementation

**User Story:** As a library consumer with simple key mapping needs, I want the default key resolver to implement the async interface with dictionary-based lookup, so that I can use it without writing a custom implementation.

#### Acceptance Criteria

1. THE `DefaultKmsKeyResolver` SHALL implement the `ResolveKeyIdAsync` method with signature `Task<string> ResolveKeyIdAsync(string? contextId, string? keyAlias = null, CancellationToken cancellationToken = default)`
2. WHEN `keyAlias` is non-null and exists as a key in the alias-to-key map, THE `DefaultKmsKeyResolver` SHALL return the corresponding mapped value from the alias-to-key map
3. WHEN `keyAlias` is non-null but does not exist as a key in the alias-to-key map, THE `DefaultKmsKeyResolver` SHALL proceed to evaluate `contextId` against the context-to-key map as if `keyAlias` were null
4. WHEN `contextId` is non-null and exists as a key in the context-to-key map, THE `DefaultKmsKeyResolver` SHALL return the corresponding mapped value from the context-to-key map
5. WHEN `keyAlias` is null or unmapped AND `contextId` is null or unmapped, THE `DefaultKmsKeyResolver` SHALL return the `defaultKeyId` value provided at construction
6. THE `DefaultKmsKeyResolver` constructor SHALL accept parameters `string defaultKeyId`, `IReadOnlyDictionary<string, string>? contextKeyMap = null`, and `IReadOnlyDictionary<string, string>? aliasKeyMap = null`, throwing `ArgumentException` if `defaultKeyId` is null, empty, or whitespace
7. THE `DefaultKmsKeyResolver` SHALL return completed tasks (via `Task.FromResult`) for all code paths, performing no asynchronous work internally
8. THE `DefaultKmsKeyResolver` SHALL perform case-sensitive lookups against both the alias-to-key map and the context-to-key map

### Requirement 3: Field Encryptor Async Key Resolution

**User Story:** As a library maintainer, I want the field encryptor to await the async key resolver, so that the entire encryption pipeline is non-blocking.

#### Acceptance Criteria

1. THE Field_Encryptor `EncryptAsync` method SHALL await the `ResolveKeyIdAsync` method on the KMS_Key_Resolver using `ConfigureAwait(false)`
2. THE Field_Encryptor `DecryptAsync` method SHALL await the `ResolveKeyIdAsync` method on the KMS_Key_Resolver using `ConfigureAwait(false)`
3. THE Field_Encryptor SHALL pass `context.ContextId` as the `contextId` argument and `context.KeyAlias` as the `keyAlias` argument to `ResolveKeyIdAsync`
4. THE Field_Encryptor SHALL pass the `cancellationToken` parameter received by the calling `EncryptAsync` or `DecryptAsync` method as the `cancellationToken` argument to `ResolveKeyIdAsync`
5. IF `ResolveKeyIdAsync` throws an exception other than `OperationCanceledException`, THEN THE Field_Encryptor SHALL wrap the exception in a `FieldEncryptionException` that includes the field name and context ID
6. IF `ResolveKeyIdAsync` returns a null or whitespace-only string, THEN THE Field_Encryptor SHALL throw a `FieldEncryptionException` indicating that the key resolver returned an invalid key ARN

### Requirement 4: Key Alias on Encrypted Attribute

**User Story:** As an entity author, I want to specify a key alias on individual encrypted properties, so that different fields can use different KMS keys based on data classification.

#### Acceptance Criteria

1. THE Encrypted_Attribute SHALL expose a `KeyAlias` property of type `string?`
2. THE Encrypted_Attribute `KeyAlias` property SHALL default to `null`
3. WHEN `KeyAlias` is not specified on the attribute or is set to an empty/whitespace-only string, THE Source_Generator SHALL emit `null` for the `KeyAlias` in the Encryption_Context

### Requirement 5: Key Alias in Encryption Context

**User Story:** As a library maintainer, I want the encryption context to carry the key alias through the pipeline, so that the field encryptor can pass it to the key resolver.

#### Acceptance Criteria

1. THE Encryption_Context SHALL expose a gettable `KeyAlias` property of type `string?` with an `init` accessor
2. THE Encryption_Context `KeyAlias` property SHALL default to `null`

### Requirement 6: Source Generator Key Alias Propagation

**User Story:** As an entity author, I want the source generator to propagate the key alias from my attribute declaration into the encryption context, so that it reaches the key resolver at runtime.

#### Acceptance Criteria

1. WHEN an encrypted property has a `KeyAlias` value specified on the Encrypted_Attribute, THE Source_Generator SHALL emit that value as the `KeyAlias` property in the Encryption_Context initializer using a string literal matching the declared value exactly
2. WHEN an encrypted property does not have a `KeyAlias` value on the Encrypted_Attribute, THE Source_Generator SHALL omit the `KeyAlias` property from the Encryption_Context initializer, resulting in a default value of null at runtime
3. IF the `KeyAlias` value specified on the Encrypted_Attribute is an empty string, THEN THE Source_Generator SHALL treat it as unspecified and omit the `KeyAlias` property from the Encryption_Context initializer

### Requirement 7: Cancellation Support

**User Story:** As a library consumer, I want the async key resolution to respect cancellation tokens, so that I can cancel long-running operations gracefully.

#### Acceptance Criteria

1. WHEN the CancellationToken is cancelled before `ResolveKeyIdAsync` completes, THE KMS_Key_Resolver SHALL throw an `OperationCanceledException` that references the cancelled token
2. IF the CancellationToken is already cancelled when `ResolveKeyIdAsync` is invoked, THEN THE KMS_Key_Resolver SHALL throw an `OperationCanceledException` without initiating the resolution operation
3. THE Field_Encryptor SHALL propagate the CancellationToken from the encrypt/decrypt call through to `ResolveKeyIdAsync`
4. IF cancellation occurs during an encrypt or decrypt operation, THEN THE Field_Encryptor SHALL not produce partial or corrupted output for the field being processed

### Requirement 8: Error Handling

**User Story:** As a library consumer, I want clear error reporting when async key resolution fails, so that I can diagnose issues in multi-tenant environments.

#### Acceptance Criteria

1. IF `ResolveKeyIdAsync` throws an exception other than `OperationCanceledException`, THEN THE Field_Encryptor SHALL throw a `FieldEncryptionException` with the `FieldName` set to the current field name, the `ContextId` set to the context ID passed to the resolver, the `KeyAlias` value that was passed to the resolver, and the original exception set as `InnerException`
2. IF `ResolveKeyIdAsync` returns a null or empty string, THEN THE Field_Encryptor SHALL throw a `FieldEncryptionException` with the `FieldName` set to the current field name, the `ContextId` set to the context ID passed to the resolver, and a message indicating the resolver returned an invalid key
3. IF `ResolveKeyIdAsync` returns a null or empty string, THEN THE `FieldEncryptionException` SHALL include the key alias value that was passed to the resolver
