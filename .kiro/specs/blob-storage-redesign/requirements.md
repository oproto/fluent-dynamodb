# Requirements Document

## Introduction

This specification defines the redesign of the blob storage feature in Oproto.FluentDynamoDb. The current `[BlobReference]` implementation has several issues including confusing semantics, no transaction safety between S3 and DynamoDB operations, eager loading only, and no failure recovery for orphaned blobs. This redesign introduces a new `[BlobStorage]` attribute, a `BlobData<T>` wrapper type for lazy/eager loading control, and an `IBlobStorageStrategy` interface for coordinating failure handling between blob storage and DynamoDB operations.

## Glossary

- **Blob_Storage_System**: The external storage system (e.g., Amazon S3) where large data is stored outside of DynamoDB
- **BlobData_Wrapper**: A generic wrapper type `BlobData<T>` that encapsulates blob storage behavior including lazy loading, reference key access, and data retrieval
- **Blob_Storage_Strategy**: An implementation of `IBlobStorageStrategy` that coordinates S3 and DynamoDB operations to handle failures
- **Reference_Key**: A unique identifier stored in DynamoDB that points to the actual data in the Blob_Storage_System
- **Lazy_Loading**: A loading mode where blob data is not downloaded until explicitly requested via `LoadAsync()`
- **Eager_Loading**: A loading mode where blob data is automatically downloaded when the entity is retrieved from DynamoDB
- **Orphaned_Blob**: A blob stored in the Blob_Storage_System that has no corresponding reference in DynamoDB due to a failed write operation
- **FluentDynamoDbOptions**: The centralized configuration object for the library
- **Logging_System**: The library's logging infrastructure that respects `[Sensitive]` attribute for data redaction
- **Field_Encryptor**: An implementation of `IFieldEncryptor` configured via FluentDynamoDbOptions for encrypting sensitive data
- **BlobStoreOptions**: A configuration object for blob storage operations containing optional metadata like content type, custom metadata, and tags
- **IBlobStorageProvider**: The cloud-agnostic interface for blob storage operations (store, retrieve, delete, exists)

## Requirements

### Requirement 1

**User Story:** As a developer, I want to use a clearly named attribute for blob storage, so that the purpose of the attribute is immediately understandable.

#### Acceptance Criteria

1. WHEN a developer applies the `[BlobStorage]` attribute to a property THEN the Source_Generator SHALL generate code to store the property data externally and persist only the Reference_Key in DynamoDB
2. WHEN the `[BlobReference]` attribute is used THEN the Source_Generator SHALL emit a deprecation warning (DYNDB104) indicating migration to `[BlobStorage]`
3. WHEN the `[BlobStorage]` attribute is applied THEN the Source_Generator SHALL support properties of type `BlobData<T>` where T is the data type to be stored

### Requirement 2

**User Story:** As a developer, I want a wrapper type that encapsulates blob storage behavior, so that I can control when data is loaded and access the reference key when needed.

#### Acceptance Criteria

1. THE BlobData_Wrapper SHALL expose a `Value` property that returns the loaded data or throws `InvalidOperationException` if data is not loaded
2. THE BlobData_Wrapper SHALL expose a `ReferenceKey` property that returns the storage key or null if no data has been stored
3. THE BlobData_Wrapper SHALL expose an `IsLoaded` property that returns true when data has been retrieved from the Blob_Storage_System
4. THE BlobData_Wrapper SHALL expose a `LoadAsync()` method that retrieves data from the Blob_Storage_System using the configured provider
5. WHEN `LoadAsync()` is called on an already-loaded BlobData_Wrapper THEN the BlobData_Wrapper SHALL return immediately without re-fetching
6. THE BlobData_Wrapper SHALL expose a static `Create(T value)` factory method for creating instances with data to be stored
7. THE BlobData_Wrapper SHALL be serializable to DynamoDB as a string containing only the Reference_Key

### Requirement 3

**User Story:** As a developer, I want to choose between lazy and eager loading for blob properties, so that I can optimize performance based on my access patterns.

#### Acceptance Criteria

1. WHEN `[BlobStorage(LazyLoad = false)]` is configured (default) THEN the Source_Generator SHALL generate code that automatically loads blob data during entity deserialization
2. WHEN `[BlobStorage(LazyLoad = true)]` is configured THEN the Source_Generator SHALL generate code that defers blob loading until `LoadAsync()` is explicitly called
3. WHEN a lazy-loaded BlobData_Wrapper's `Value` property is accessed before `LoadAsync()` is called THEN the BlobData_Wrapper SHALL throw `InvalidOperationException` with a clear message
4. WHEN eager loading is configured THEN the Source_Generator SHALL generate async hydration code that loads blob data as part of `FromDynamoDbAsync()`

### Requirement 4

**User Story:** As a developer, I want configurable strategies for handling failures between blob storage and DynamoDB operations, so that I can choose the appropriate consistency guarantees for my use case.

#### Acceptance Criteria

1. THE Blob_Storage_Strategy SHALL define `OnBeforeDynamoDbWriteAsync()` that executes before the DynamoDB write operation
2. THE Blob_Storage_Strategy SHALL define `OnAfterDynamoDbWriteSuccessAsync()` that executes after a successful DynamoDB write
3. THE Blob_Storage_Strategy SHALL define `OnAfterDynamoDbWriteFailureAsync()` that executes after a failed DynamoDB write
4. THE Blob_Storage_Strategy SHALL define `OnBeforeDynamoDbDeleteAsync()` that executes before a DynamoDB delete operation
5. WHEN no strategy is configured THEN the Blob_Storage_System SHALL use `BestEffortCleanupStrategy` as the default
6. THE FluentDynamoDbOptions SHALL expose `WithBlobStorageStrategy(IBlobStorageStrategy)` for configuring the strategy

### Requirement 5

**User Story:** As a developer, I want a best-effort cleanup strategy that attempts to clean up orphaned blobs, so that I have reasonable consistency without complex infrastructure.

#### Acceptance Criteria

1. WHEN using `BestEffortCleanupStrategy` and a DynamoDB write fails after blob upload THEN the Blob_Storage_Strategy SHALL attempt to delete the uploaded blob
2. WHEN the cleanup attempt fails THEN the Blob_Storage_Strategy SHALL log the failure and continue without throwing
3. WHEN using `BestEffortCleanupStrategy` and a DynamoDB delete succeeds THEN the Blob_Storage_Strategy SHALL attempt to delete the associated blob
4. THE `BestEffortCleanupStrategy` SHALL be the default strategy when no strategy is explicitly configured

### Requirement 6

**User Story:** As a developer, I want a no-cleanup strategy for non-critical data, so that I can have the simplest possible implementation when orphaned blobs are acceptable.

#### Acceptance Criteria

1. WHEN using `NoCleanupStrategy` THEN the Blob_Storage_Strategy SHALL upload blobs before DynamoDB writes without any cleanup on failure
2. WHEN using `NoCleanupStrategy` and a DynamoDB write fails THEN the Blob_Storage_Strategy SHALL not attempt to delete the uploaded blob
3. WHEN using `NoCleanupStrategy` and a DynamoDB delete succeeds THEN the Blob_Storage_Strategy SHALL not attempt to delete the associated blob

### Requirement 7

**User Story:** As a developer, I want the blob storage feature to integrate with the existing request builder pipeline, so that strategies are invoked automatically during Put, Update, and Delete operations.

#### Acceptance Criteria

1. WHEN `PutAsync()` is called on an entity with `[BlobStorage]` properties THEN the Request_Builder SHALL invoke the configured Blob_Storage_Strategy lifecycle methods
2. WHEN `UpdateAsync()` is called with changes to `[BlobStorage]` properties THEN the Request_Builder SHALL invoke the configured Blob_Storage_Strategy lifecycle methods
3. WHEN `DeleteAsync()` is called on an entity with `[BlobStorage]` properties THEN the Request_Builder SHALL invoke the configured Blob_Storage_Strategy lifecycle methods
4. WHEN batch or transaction operations include entities with `[BlobStorage]` properties THEN the Batch_Builder or Transaction_Builder SHALL invoke the configured Blob_Storage_Strategy lifecycle methods

### Requirement 8

**User Story:** As a developer, I want clear error messages when blob storage is misconfigured, so that I can quickly diagnose and fix configuration issues.

#### Acceptance Criteria

1. WHEN `[BlobStorage]` is used without configuring a blob provider via `FluentDynamoDbOptions.WithBlobStorage()` THEN the Blob_Storage_System SHALL throw `InvalidOperationException` with a message indicating the missing configuration
2. WHEN `LoadAsync()` is called without a configured blob provider THEN the BlobData_Wrapper SHALL throw `InvalidOperationException` with a clear message
3. WHEN the blob provider fails to retrieve data THEN the Blob_Storage_System SHALL throw `BlobStorageException` with the underlying error details
4. WHEN the blob provider fails to store data THEN the Blob_Storage_System SHALL throw `BlobStorageException` with the underlying error details

### Requirement 9

**User Story:** As a developer, I want the blob storage feature to work with JSON serialization, so that I can store complex objects as JSON blobs in external storage.

#### Acceptance Criteria

1. WHEN `[BlobStorage]` and `[JsonBlob]` are combined on a property THEN the Source_Generator SHALL serialize the object to JSON before storing in the Blob_Storage_System
2. WHEN retrieving a property with both `[BlobStorage]` and `[JsonBlob]` THEN the Source_Generator SHALL deserialize the JSON data after retrieval from the Blob_Storage_System
3. THE combination of `[BlobStorage]` and `[JsonBlob]` SHALL use the JSON serializer configured via `FluentDynamoDbOptions`

### Requirement 10

**User Story:** As a developer, I want blob storage to work with the `[Sensitive]` attribute, so that blob reference keys and data are properly redacted in logs.

#### Acceptance Criteria

1. WHEN `[BlobStorage]` and `[Sensitive]` are combined on a property THEN the Logging_System SHALL redact the Reference_Key in log output
2. WHEN `[BlobStorage]` and `[Sensitive]` are combined on a property THEN the Logging_System SHALL redact the blob data value in log output
3. THE Source_Generator SHALL set `IsSensitive = true` in PropertyMetadata for properties with both `[BlobStorage]` and `[Sensitive]`

### Requirement 11

**User Story:** As a developer, I want blob storage to work with the `[Encrypted]` attribute, so that blob data is encrypted before being stored externally.

#### Acceptance Criteria

1. WHEN `[BlobStorage]` and `[Encrypted]` are combined on a property THEN the Blob_Storage_System SHALL encrypt the data before uploading to the Blob_Storage_System
2. WHEN retrieving a property with both `[BlobStorage]` and `[Encrypted]` THEN the Blob_Storage_System SHALL decrypt the data after retrieval from the Blob_Storage_System
3. THE encryption SHALL use the field encryptor configured via `FluentDynamoDbOptions.WithEncryption()`
4. WHEN `[BlobStorage]` and `[Encrypted]` are used without configuring an encryptor THEN the Blob_Storage_System SHALL throw `EncryptionRequiredException` with a clear message
5. WHEN `[BlobStorage]`, `[Encrypted]`, and `[JsonBlob]` are combined THEN the Blob_Storage_System SHALL serialize to JSON first, then encrypt, then store

### Requirement 12

**User Story:** As a developer, I want the blob storage feature to be cloud-agnostic, so that I can use any blob storage provider (S3, Azure Blob, Google Cloud Storage, etc.) without changes to my entity code.

#### Acceptance Criteria

1. THE `IBlobStorageProvider` interface SHALL remain cloud-agnostic with no AWS, Azure, or GCP-specific types in the contract
2. THE `IBlobStorageProvider` interface SHALL support optional content type metadata via an overload of `StoreAsync()` that accepts `BlobStoreOptions`
3. THE `BlobStoreOptions` SHALL include `ContentType`, `Metadata` dictionary, and `Tags` dictionary for provider-specific metadata
4. THE `[BlobStorage]` attribute SHALL not include any provider-specific configuration (bucket names, container names, etc.)
5. WHEN configuring blob storage THEN the FluentDynamoDbOptions SHALL accept any `IBlobStorageProvider` implementation without type constraints
6. THE Reference_Key format SHALL be provider-defined, allowing each provider to use its native key format (S3 keys, Azure blob names, GCS object names)

### Requirement 13

**User Story:** As a developer, I want the blob storage redesign to be AOT-compatible, so that I can use it in Native AOT deployments.

#### Acceptance Criteria

1. THE BlobData_Wrapper SHALL not use reflection for any operations
2. THE Source_Generator SHALL generate AOT-safe code for blob storage serialization and deserialization
3. THE Blob_Storage_Strategy implementations SHALL not use reflection or dynamic code generation
4. WHEN using `[BlobStorage]` in an AOT-compiled application THEN the Blob_Storage_System SHALL function correctly without runtime code generation
