# Requirements Document

## Introduction

This specification covers a set of API enhancements and improvements for Oproto.FluentDynamoDb version 0.9.0. The changes include automatic sensitivity marking for encrypted properties, namespace customization for generated code, deprecation of the `[Queryable]` attribute, a new write transaction requirement attribute, default request options configuration, and NuGet packaging fixes. These improvements aim to reduce boilerplate, improve developer experience, and fix packaging issues discovered after the v0.8.0 release.

## Glossary

- **FluentDynamoDb**: The Oproto.FluentDynamoDb library providing a fluent API for Amazon DynamoDB operations
- **Source Generator**: The compile-time code generator that produces entity mapping code, field constants, and key builders
- **PropertyMetadata**: Runtime metadata about entity properties including sensitivity, encryption, and key information
- **IsSensitive**: A boolean flag in PropertyMetadata indicating the property value should be redacted in logs
- **IsEncrypted**: A boolean flag in PropertyMetadata indicating the property requires field-level encryption
- **FluentDynamoDbOptions**: Configuration object passed to table constructors for optional features
- **Request Builder**: Fluent builder classes for constructing DynamoDB requests (Query, Get, Put, Update, Delete, Scan)
- **Entity Accessor**: Generated type-safe accessor for entity operations on a table
- **NuGet Package**: The distributable package format for .NET libraries

## Requirements

### Requirement 1

**User Story:** As a developer, I want encrypted properties to automatically be treated as sensitive, so that I don't have to apply both `[Encrypted]` and `[Sensitive]` attributes to protect data in logs.

#### Acceptance Criteria

1. WHEN the source generator processes a property with the `[Encrypted]` attribute THEN the system SHALL set `IsSensitive = true` in the generated PropertyMetadata
2. WHEN a property has both `[Encrypted]` and `[Sensitive]` attributes THEN the system SHALL set `IsSensitive = true` without duplication or conflict
3. WHEN an encrypted property value is logged THEN the system SHALL display "[REDACTED]" instead of the actual value

### Requirement 2

**User Story:** As a developer, I want to specify a custom namespace for my generated table class, so that I can organize my code according to my project's namespace conventions.

#### Acceptance Criteria

1. WHEN the `[DynamoDbTable]` attribute includes a `Namespace` parameter THEN the source generator SHALL place the generated table class in the specified namespace
2. WHEN the `[DynamoDbTable]` attribute does not include a `Namespace` parameter THEN the source generator SHALL use the entity's namespace as the default
3. WHEN a custom namespace is specified THEN the generated code SHALL include appropriate using directives for referenced types

### Requirement 3

**User Story:** As a developer, I want the `[Queryable]` attribute to be deprecated, so that the codebase is simplified by deriving query capabilities from partition key and sort key metadata.

#### Acceptance Criteria

1. WHEN the source generator encounters a `[Queryable]` attribute THEN the system SHALL emit a compiler warning indicating the attribute is deprecated
2. WHEN determining supported operations for a property THEN the system SHALL derive capabilities from `[PartitionKey]` and `[SortKey]` attributes instead of `[Queryable]`
3. WHEN a partition key property is processed THEN the system SHALL support equality operations
4. WHEN a sort key property is processed THEN the system SHALL support equality, begins_with, between, greater_than, and less_than operations

### Requirement 4

**User Story:** As a developer, I want to mark entity classes as requiring write transactions, so that accidental non-transactional writes are prevented at runtime.

#### Acceptance Criteria

1. WHEN an entity class has the `[RequireWriteTransaction]` attribute THEN the system SHALL store this requirement in EntityMetadata
2. WHEN a Put operation is attempted on a transaction-required entity outside a transaction THEN the system SHALL throw an InvalidOperationException with a clear message
3. WHEN an Update operation is attempted on a transaction-required entity outside a transaction THEN the system SHALL throw an InvalidOperationException with a clear message
4. WHEN a Delete operation is attempted on a transaction-required entity outside a transaction THEN the system SHALL throw an InvalidOperationException with a clear message
5. WHEN a BatchWrite operation includes a transaction-required entity THEN the system SHALL throw an InvalidOperationException with a clear message
6. WHEN a TransactWrite operation includes a transaction-required entity THEN the system SHALL allow the operation to proceed

### Requirement 5

**User Story:** As a developer, I want to configure default request options in FluentDynamoDbOptions, so that I don't have to call the same builder methods on every request.

#### Acceptance Criteria

1. WHEN FluentDynamoDbOptions includes `UseConsistentRead(true)` THEN all Get and Query request builders SHALL default to consistent reads
2. WHEN FluentDynamoDbOptions includes `ReturnConsumedCapacity(ReturnConsumedCapacity.Total)` THEN all request builders SHALL default to returning consumed capacity
3. WHEN FluentDynamoDbOptions includes `ReturnItemCollectionMetrics(ReturnItemCollectionMetrics.Size)` THEN write request builders SHALL default to returning item collection metrics
4. WHEN FluentDynamoDbOptions includes `ReturnValues(ReturnValue.AllNew)` THEN Update and Delete request builders SHALL default to the specified return values
5. WHEN a request builder method is called that overrides a default option THEN the explicit value SHALL take precedence over the default
6. WHEN default options are configured THEN the extension method names SHALL match the existing builder method names for consistency

### Requirement 6

**User Story:** As a package consumer, I want the NuGet packages to contain only the library assemblies and documentation, so that test projects and example code are not included in the distributed packages.

#### Acceptance Criteria

1. WHEN the NuGet packages are built THEN the system SHALL exclude all unit test project assemblies from the package contents
2. WHEN the NuGet packages are built THEN the system SHALL exclude all example project assemblies from the package contents
3. WHEN the NuGet packages are built THEN the system SHALL exclude duplicate icon files that were incorrectly copied to test/example projects
4. WHEN the NuGet packages are built THEN the system SHALL include only the main library assembly, README, icon, and source generator analyzer

### Requirement 7

**User Story:** As a developer, I want comprehensive documentation for all new features, so that I can understand and use the new capabilities effectively.

#### Acceptance Criteria

1. WHEN a new attribute is added THEN the system SHALL include XML documentation comments with examples
2. WHEN FluentDynamoDbOptions is extended THEN the documentation SHALL include usage examples for each new configuration method
3. WHEN breaking changes are introduced THEN the CHANGELOG.md SHALL document the change with migration guidance
4. WHEN documentation is updated THEN the docs/DOCUMENTATION_CHANGELOG.md SHALL track the changes for downstream documentation synchronization

### Requirement 8

**User Story:** As a developer upgrading from v0.8.0, I want a clear breaking changes document, so that I can understand what changes are required in my code.

#### Acceptance Criteria

1. WHEN the `[Queryable]` attribute is deprecated THEN the breaking changes document SHALL explain the migration path
2. WHEN new runtime exceptions are introduced for `[RequireWriteTransaction]` THEN the breaking changes document SHALL list the affected operations
3. WHEN default options behavior changes request builder defaults THEN the breaking changes document SHALL explain how to opt out if needed
