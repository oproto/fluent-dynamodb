# Requirements Document

## Introduction

This specification defines the reorganization of the Oproto.FluentDynamoDb library's internal structure in preparation for the first public release. The current `Storage/` folder contains a mix of concerns (physical storage, entity contracts, metadata, hydration, providers, mapping, and context). This reorganization separates these concerns into distinct folders and namespaces, improving code organization, discoverability, and maintainability.

## Glossary

- **Storage**: Physical DynamoDB table and index abstractions for executing operations
- **Entity**: Contracts and interfaces that define DynamoDB entity behavior
- **Metadata**: Classes describing entity structure, properties, relationships, and indexes
- **Hydration**: Process of materializing entities from DynamoDB attribute dictionaries
- **Provider**: Pluggable service implementations (encryption, blob storage)
- **Mapping**: Error handling and exceptions related to entity-to-DynamoDB mapping
- **Context**: Operation context for tracking request/response metadata
- **Source Generator**: Roslyn-based code generator that creates entity mapping code at compile time

## Requirements

### Requirement 1

**User Story:** As a library maintainer, I want the Storage folder to contain only physical storage abstractions, so that the codebase has clear separation of concerns.

#### Acceptance Criteria

1. WHEN the reorganization is complete THEN the Storage folder SHALL contain only DynamoDbTableBase.cs, DynamoDbIndex.cs, and IDynamoDbTable.cs
2. WHEN a developer imports Oproto.FluentDynamoDb.Storage THEN the developer SHALL have access only to table and index abstractions
3. WHEN files are moved from Storage THEN the original files SHALL be deleted from the Storage folder

### Requirement 2

**User Story:** As a library consumer, I want entity contracts in a dedicated Entities namespace, so that I can easily find and implement entity interfaces.

#### Acceptance Criteria

1. WHEN the reorganization is complete THEN the Entities folder SHALL contain IDynamoDbEntity.cs, IProjectionModel.cs, and IDiscriminatedProjection.cs
2. WHEN a developer imports Oproto.FluentDynamoDb.Entities THEN the developer SHALL have access to all entity contract interfaces
3. WHEN entity interfaces are moved THEN the namespace declaration in each file SHALL be updated to Oproto.FluentDynamoDb.Entities

### Requirement 3

**User Story:** As a library consumer, I want metadata classes in a dedicated Metadata namespace, so that I can easily access entity structure information.

#### Acceptance Criteria

1. WHEN the reorganization is complete THEN the Metadata folder SHALL contain EntityMetadata.cs, PropertyMetadata.cs, RelationshipMetadata.cs, IndexMetadata.cs, and IEntityMetadataProvider.cs
2. WHEN a developer imports Oproto.FluentDynamoDb.Metadata THEN the developer SHALL have access to all metadata classes
3. WHEN metadata classes are moved THEN the namespace declaration in each file SHALL be updated to Oproto.FluentDynamoDb.Metadata

### Requirement 4

**User Story:** As a library consumer, I want hydration classes in a dedicated Hydration namespace, so that entity materialization logic is clearly separated.

#### Acceptance Criteria

1. WHEN the reorganization is complete THEN the Hydration folder SHALL contain IAsyncEntityHydrator.cs, IEntityHydratorRegistry.cs, and DefaultEntityHydratorRegistry.cs
2. WHEN a developer imports Oproto.FluentDynamoDb.Hydration THEN the developer SHALL have access to all hydration interfaces and implementations
3. WHEN hydration classes are moved THEN the namespace declaration in each file SHALL be updated to Oproto.FluentDynamoDb.Hydration

### Requirement 5

**User Story:** As a library consumer, I want encryption provider interfaces in a dedicated Providers.Encryption namespace, so that encryption concerns are clearly isolated.

#### Acceptance Criteria

1. WHEN the reorganization is complete THEN the Providers/Encryption folder SHALL contain IFieldEncryptor.cs and FieldEncryptionContext.cs
2. WHEN a developer imports Oproto.FluentDynamoDb.Providers.Encryption THEN the developer SHALL have access to encryption provider interfaces
3. WHEN encryption files are moved THEN the namespace declaration in each file SHALL be updated to Oproto.FluentDynamoDb.Providers.Encryption

### Requirement 6

**User Story:** As a library consumer, I want blob storage provider interfaces in a dedicated Providers.BlobStorage namespace, so that blob storage concerns are clearly isolated.

#### Acceptance Criteria

1. WHEN the reorganization is complete THEN the Providers/BlobStorage folder SHALL contain IBlobStorageProvider.cs and IJsonBlobSerializer.cs
2. WHEN a developer imports Oproto.FluentDynamoDb.Providers.BlobStorage THEN the developer SHALL have access to blob storage provider interfaces
3. WHEN blob storage files are moved THEN the namespace declaration in each file SHALL be updated to Oproto.FluentDynamoDb.Providers.BlobStorage

### Requirement 7

**User Story:** As a library consumer, I want mapping utilities and exceptions in a dedicated Mapping namespace, so that error handling is clearly organized.

#### Acceptance Criteria

1. WHEN the reorganization is complete THEN the Mapping folder SHALL contain MappingErrorHandler.cs, DynamoDbMappingException.cs, DiscriminatorMismatchException.cs, ProjectionValidationException.cs, and FieldEncryptionException.cs
2. WHEN a developer imports Oproto.FluentDynamoDb.Mapping THEN the developer SHALL have access to all mapping utilities and exceptions
3. WHEN mapping files are moved THEN the namespace declaration in each file SHALL be updated to Oproto.FluentDynamoDb.Mapping

### Requirement 8

**User Story:** As a library consumer, I want operation context classes in a dedicated Context namespace, so that request/response tracking is clearly separated.

#### Acceptance Criteria

1. WHEN the reorganization is complete THEN the Context folder SHALL contain DynamoDbOperationContext.cs, DynamoDbOperationContextDiagnostics.cs, and OperationContextData.cs
2. WHEN a developer imports Oproto.FluentDynamoDb.Context THEN the developer SHALL have access to all operation context classes
3. WHEN context files are moved THEN the namespace declaration in each file SHALL be updated to Oproto.FluentDynamoDb.Context

### Requirement 9

**User Story:** As a library maintainer, I want the source generator to emit correct namespace references, so that generated code compiles without errors.

#### Acceptance Criteria

1. WHEN the source generator emits using directives THEN the generator SHALL reference the new namespace structure (Entities, Metadata, Hydration, etc.)
2. WHEN the source generator references IDynamoDbEntity THEN the generator SHALL use Oproto.FluentDynamoDb.Entities namespace
3. WHEN the source generator references EntityMetadata or PropertyMetadata THEN the generator SHALL use Oproto.FluentDynamoDb.Metadata namespace
4. WHEN the source generator references IAsyncEntityHydrator THEN the generator SHALL use Oproto.FluentDynamoDb.Hydration namespace

### Requirement 10

**User Story:** As a library maintainer, I want all existing code references updated to use new namespaces, so that the library compiles and functions correctly.

#### Acceptance Criteria

1. WHEN files reference types from moved namespaces THEN the using directives SHALL be updated to the new namespace locations
2. WHEN the solution is built THEN the build SHALL complete with zero namespace-related errors
3. WHEN unit tests are executed THEN all tests SHALL pass without namespace-related failures

### Requirement 11

**User Story:** As a library maintainer, I want the CHANGELOG and documentation updated, so that users understand the breaking namespace changes.

#### Acceptance Criteria

1. WHEN the reorganization is complete THEN the CHANGELOG.md SHALL document the namespace changes as breaking changes
2. WHEN the reorganization is complete THEN the docs/DOCUMENTATION_CHANGELOG.md SHALL document all documentation updates
3. WHEN documentation references old namespaces THEN the documentation SHALL be updated to reference new namespaces
