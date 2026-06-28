# Requirements Document

## Introduction

This feature adds support for registering multiple `IBlobStorageProvider` instances by name in `FluentDynamoDbOptions`, and allows the `[BlobStorage]` attribute to specify which named provider to use for each property. This enables entities with properties stored across different blob backends (e.g., images in one S3 bucket, documents in another bucket or provider entirely).

## Glossary

- **Options**: The `FluentDynamoDbOptions` class that holds configuration for a table instance
- **BlobStorage_Attribute**: The `[BlobStorage]` attribute applied to entity properties to mark them for external blob storage
- **Provider_Registry**: The internal dictionary within Options that maps provider names to IBlobStorageProvider instances
- **Default_Provider**: The unnamed IBlobStorageProvider registered via the existing `WithBlobStorage(provider)` method
- **Named_Provider**: An IBlobStorageProvider registered with an explicit string name via `WithBlobStorage(name, provider)`
- **Provider_Name**: A non-null, non-empty string identifier used to register and look up a Named_Provider
- **HydratorGenerator**: The source generator that emits async hydration code for entities including blob load/store operations
- **MapperGenerator**: The source generator that emits async mapping code for serializing entities including blob store operations

## Requirements

### Requirement 1: Named Provider Registration on Options

**User Story:** As a developer, I want to register multiple blob storage providers with distinct names on FluentDynamoDbOptions, so that different entity properties can use different blob backends.

#### Acceptance Criteria

1. THE Options SHALL expose a `WithBlobStorage(string name, IBlobStorageProvider provider)` method that registers a Named_Provider under the given Provider_Name and returns the Options instance to support fluent method chaining
2. WHEN `WithBlobStorage(string name, IBlobStorageProvider provider)` is called, THE Options SHALL store the provider in the Provider_Registry keyed by Provider_Name
3. IF `WithBlobStorage(string name, IBlobStorageProvider provider)` is called with a Provider_Name that is null, empty, or contains only whitespace characters, THEN THE Options SHALL throw an ArgumentException
4. IF `WithBlobStorage(string name, IBlobStorageProvider provider)` is called with a null provider, THEN THE Options SHALL throw an ArgumentNullException
5. THE Options SHALL continue to expose the existing `WithBlobStorage(IBlobStorageProvider provider)` method that registers the Default_Provider
6. WHEN `WithBlobStorage(string name, IBlobStorageProvider provider)` is called with a Provider_Name that already exists in the Provider_Registry, THE Options SHALL replace the previously registered provider for that name

### Requirement 2: Provider Resolution Method on Options

**User Story:** As a developer consuming the generated code, I want a method to resolve a blob provider by name, so that per-property provider lookup is possible at runtime.

#### Acceptance Criteria

1. THE Options SHALL expose a `GetBlobProvider(string? name)` method that returns the IBlobStorageProvider for the given name
2. WHEN `GetBlobProvider` is called with a null or empty name, THE Options SHALL return the Default_Provider
3. WHEN `GetBlobProvider` is called with a Provider_Name that has no registered provider, THE Options SHALL throw an InvalidOperationException with a message identifying the missing provider name
4. WHEN `GetBlobProvider` is called with a null or empty name and no Default_Provider is registered, THE Options SHALL throw an InvalidOperationException indicating no default blob provider is configured

### Requirement 3: Provider Property on BlobStorage Attribute

**User Story:** As a developer defining entities, I want to specify a named provider on the `[BlobStorage]` attribute, so that each blob property can target a specific blob backend.

#### Acceptance Criteria

1. THE BlobStorage_Attribute SHALL expose an optional `Provider` property of type string that defaults to null
2. WHEN the Provider property is not set on BlobStorage_Attribute, THE generated code SHALL resolve the Default_Provider during hydration and mapping
3. WHEN the Provider property is set to a non-empty value on BlobStorage_Attribute, THE generated code SHALL resolve the Named_Provider matching that value during hydration and mapping

### Requirement 4: Source Generator Per-Property Provider Resolution

**User Story:** As a developer, I want the source generator to emit code that resolves the correct provider per property, so that each blob-annotated property uses its designated provider at runtime.

#### Acceptance Criteria

1. WHEN an entity has multiple properties with BlobStorage_Attribute using different Provider values, THE HydratorGenerator SHALL emit code that calls `GetBlobProvider` with the corresponding Provider value for each property, resulting in each property's hydration using the IBlobStorageProvider instance returned for its specific Provider_Name
2. WHEN an entity has multiple properties with BlobStorage_Attribute using different Provider values, THE MapperGenerator SHALL emit code that calls `GetBlobProvider` with the corresponding Provider value for each property, resulting in each property's serialization using the IBlobStorageProvider instance returned for its specific Provider_Name
3. WHEN a BlobStorage_Attribute has no Provider set, THE generated code SHALL call `GetBlobProvider(null)` to obtain the Default_Provider
4. WHEN an entity has N properties with BlobStorage_Attribute, THE generated code SHALL call `GetBlobProvider` exactly once per property, passing that property's Provider value (or null if unset), rather than resolving a single provider for all properties
5. IF `GetBlobProvider` throws an InvalidOperationException during hydration or serialization for any property, THEN THE generated code SHALL allow the exception to propagate without catching it, preserving the error context from the Options resolution layer

### Requirement 5: Backwards Compatibility

**User Story:** As a developer with existing entities using `[BlobStorage]` without a Provider property, I want my code to continue working without modification after upgrading.

#### Acceptance Criteria

1. THE existing `WithBlobStorage(IBlobStorageProvider provider)` method SHALL retain its current name, parameter type, return type, and public accessibility without changes
2. WHEN an entity uses BlobStorage_Attribute without specifying a Provider, THE generated hydration code SHALL resolve the Default_Provider via `GetBlobProvider(null)` and preserve existing LazyLoad semantics
3. THE IBlobStorageProvider interface SHALL remain unchanged with no added, removed, or modified members
4. THE BlobStorage_Attribute SHALL retain its existing `LazyLoad` property with a default value of `false` (eager loading)
5. WHEN existing source code that uses `[BlobStorage]` without a Provider property is compiled against the updated library, THE compilation SHALL succeed without errors or new warnings

### Requirement 6: Fluent Registration Chaining

**User Story:** As a developer, I want to chain multiple `WithBlobStorage` calls fluently, so that registering the default and named providers follows the existing options pattern.

#### Acceptance Criteria

1. THE `WithBlobStorage(string name, IBlobStorageProvider provider)` method SHALL return a new FluentDynamoDbOptions instance that contains the named provider accessible via the supplied name and preserves all previously registered named providers and the default provider
2. WHEN `WithBlobStorage(IBlobStorageProvider provider)` is chained with one or more `WithBlobStorage(string name, IBlobStorageProvider provider)` calls, THE resulting FluentDynamoDbOptions instance SHALL expose both the default provider via `BlobStorageProvider` and all named providers via `GetBlobProvider(string name)`
3. THE FluentDynamoDbOptions class SHALL follow the existing copy-on-write immutable pattern where each `WithBlobStorage` overload returns a new instance and no mutation of previously returned instances occurs

### Requirement 7: Runtime Validation Error Messages

**User Story:** As a developer, I want clear error messages when a named provider is missing at runtime, so that I can quickly diagnose misconfiguration.

#### Acceptance Criteria

1. WHEN `GetBlobProvider` throws for a missing Named_Provider, THE exception message SHALL include the exact requested Provider_Name string
2. WHEN `GetBlobProvider` throws for a missing Named_Provider and one or more other Named_Providers are registered, THE exception message SHALL list the names of all available registered providers
3. WHEN `GetBlobProvider` throws for a missing Named_Provider and no other Named_Providers are registered, THE exception message SHALL indicate that no named providers have been configured
4. WHEN `GetBlobProvider` throws for a missing Default_Provider, THE exception message SHALL indicate that no default blob storage provider has been configured and suggest using `WithBlobStorage(provider)` to register one
