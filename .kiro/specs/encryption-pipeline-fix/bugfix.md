# Bugfix Requirements Document

## Introduction

Entities with `[Encrypted]` properties cannot be used with standard CRUD operations (`PutAsync`, `GetAsync`, etc.) because the request builder pipeline never invokes the async serialization/deserialization methods that handle encryption. Three interconnected gaps in the pipeline prevent encryption-only entities from working end-to-end, even though the encryption implementation itself (`AwsEncryptionSdkFieldEncryptor`, generated `ToDynamoDbAsync`/`FromDynamoDbAsync`) is complete and tested.

## Bug Analysis

### Current Behavior (Defect)

1.1 WHEN an entity has only `[Encrypted]` properties (no blob storage) THEN the system does not generate a hydrator class because `HydratorGenerator.RequiresHydrator()` only checks `IsBlobStorage`, causing the read path (`GetItemAsync`) to fall through to the synchronous `FromDynamoDb` stub which throws `NotSupportedException`

1.2 WHEN an entity has only `[Encrypted]` properties (no blob storage) THEN the generated `ToDynamoDbAsync` method requires a non-nullable `IBlobStorageProvider blobProvider` parameter, and the null check throws `ArgumentNullException` because there is no blob provider to pass

1.3 WHEN `PutItemRequestBuilder.WithItem(entity)` is called for an entity with `[Encrypted]` properties THEN the system calls the synchronous `ToDynamoDb` stub at builder-configuration time, which throws `NotSupportedException` because encrypted entities require async serialization

### Expected Behavior (Correct)

2.1 WHEN an entity has `[Encrypted]` properties (with or without blob storage) THEN the system SHALL generate a hydrator class so the read path can use `FromDynamoDbAsync` for decryption via the hydrator registry

2.2 WHEN an entity has only `[Encrypted]` properties (no blob storage) THEN the generated `ToDynamoDbAsync` method SHALL accept `blobProvider` as nullable (or not require it) so that encryption-only entities can be serialized without a blob storage provider

2.3 WHEN `PutItemRequestBuilder.WithItem(entity)` is called for an entity with `[Encrypted]` properties THEN the system SHALL defer serialization to execution time (inside `PutAsync`) when an async context is available, rather than calling the synchronous `ToDynamoDb` stub at builder-configuration time

### Unchanged Behavior (Regression Prevention)

3.1 WHEN an entity has no `[Encrypted]` and no blob storage properties THEN the system SHALL CONTINUE TO use synchronous `ToDynamoDb`/`FromDynamoDb` for serialization and deserialization without requiring a hydrator

3.2 WHEN an entity has blob storage properties (no encryption) THEN the system SHALL CONTINUE TO generate a hydrator and use `ToDynamoDbAsync`/`FromDynamoDbAsync` with the blob storage provider as before

3.3 WHEN an entity has both blob storage and `[Encrypted]` properties THEN the system SHALL CONTINUE TO generate a hydrator and use `ToDynamoDbAsync`/`FromDynamoDbAsync` with both the blob storage provider and field encryptor

3.4 WHEN `PutItemRequestBuilder.WithItem(entity)` is called for a non-encrypted entity THEN the system SHALL CONTINUE TO serialize synchronously at builder-configuration time via `ToDynamoDb`

3.5 WHEN `GetItemAsync` is called for a non-encrypted entity without blob storage THEN the system SHALL CONTINUE TO deserialize synchronously via `FromDynamoDb` without consulting the hydrator registry
