# Requirements Document

## Introduction

This feature enhances the projection system in Oproto.FluentDynamoDb to provide better interface compatibility and API consistency. Currently, projections implement only `IProjectionModel<TSelf>` which limits their compatibility with `QueryRequestBuilder` and other entity operations. This feature introduces a new interface hierarchy that allows projections to work seamlessly with query builders while maintaining their read-only nature.

## Glossary

- **Projection**: A read-only entity type that represents a subset of attributes from a source entity, typically used for index queries
- **Source Entity**: The full entity type that a projection derives from via `[DynamoDbProjection(typeof(SourceEntity))]`
- **Read-Only Entity**: An entity that supports reading and querying but not write operations like Put, Update, or Delete
- **Interface Hierarchy**: A structured set of interfaces where derived interfaces inherit methods from base interfaces
- **Static Abstract Method**: A method declared in an interface that implementing types must provide as static methods
- **Entity Metadata**: Information about an entity's structure, keys, discriminators, and DynamoDB mapping

## Requirements

### Requirement 1: Read-Only Entity Interface Hierarchy

**User Story:** As a developer, I want projections to implement a consistent interface hierarchy, so that they can work with QueryRequestBuilder and other entity operations while remaining read-only.

#### Acceptance Criteria

1. THE system SHALL define an `IReadOnlyEntity<TSelf>` interface that inherits from `IEntityMetadataProvider`
2. THE `IReadOnlyEntity<TSelf>` interface SHALL include static abstract methods for reading operations: `FromDynamoDb()` and `GetPartitionKey()`
3. THE existing `IDynamoDbEntity` interface SHALL inherit from `IReadOnlyEntity<TSelf>` to maintain backward compatibility
4. THE `IDynamoDbEntity` interface SHALL add write-oriented static abstract methods: `ToDynamoDb()`, `MatchesEntity()`, and `RequiresWriteTransaction`
5. THE interface hierarchy SHALL maintain all existing method signatures for backward compatibility

### Requirement 2: Projection Interface Implementation

**User Story:** As a developer using projections, I want them to implement the read-only entity interface, so that I can use them with QueryRequestBuilder and index operations.

#### Acceptance Criteria

1. WHEN a projection is generated THEN it SHALL implement `IReadOnlyEntity<TSelf>` instead of only `IProjectionModel<TSelf>`
2. WHEN a projection implements `IReadOnlyEntity<TSelf>` THEN it SHALL provide `FromDynamoDb()` method implementation
3. WHEN a projection implements `IReadOnlyEntity<TSelf>` THEN it SHALL provide `GetPartitionKey()` method implementation
4. WHEN a projection implements `IReadOnlyEntity<TSelf>` THEN it SHALL inherit entity metadata from its source entity
5. THE projection SHALL maintain its existing `ProjectionExpression` property for backward compatibility

### Requirement 3: QueryRequestBuilder Compatibility

**User Story:** As a developer, I want to use projections with QueryRequestBuilder, so that I can perform type-safe queries that return projected results.

#### Acceptance Criteria

1. WHEN using `QueryRequestBuilder<T>` with a projection type THEN the system SHALL accept types implementing `IReadOnlyEntity<T>`
2. WHEN querying with a projection type THEN the system SHALL automatically apply the projection expression
3. WHEN querying with a projection type THEN the system SHALL return properly hydrated projection instances
4. THE `QueryRequestBuilder<T>` constraint SHALL be updated to `where T : class, IReadOnlyEntity<T>`
5. THE existing entity types SHALL continue to work without modification due to interface inheritance

### Requirement 4: Index Query Integration

**User Story:** As a developer, I want projections to work seamlessly with index queries, so that I can query indexes and receive projected results without manual projection setup.

#### Acceptance Criteria

1. WHEN an index has a projection type defined THEN the generated index class SHALL support non-generic `Query()` methods returning `QueryRequestBuilder<TProjection>`
2. WHEN querying an index with a projection type THEN the system SHALL automatically apply the projection expression
3. WHEN querying an index with a projection type THEN the system SHALL return properly hydrated projection instances
4. THE projection type SHALL be excluded from table entity accessors since projections are not full entities
5. THE projection type SHALL work with all index query patterns: lambda expressions, format strings, and manual expressions

### Requirement 5: Metadata Inheritance

**User Story:** As a developer, I want projections to inherit metadata from their source entities, so that they have the necessary information for query operations without duplicating configuration.

#### Acceptance Criteria

1. WHEN a projection is generated THEN it SHALL inherit partition key metadata from its source entity
2. WHEN a projection is generated THEN it SHALL inherit sort key metadata from its source entity if applicable
3. WHEN a projection is generated THEN it SHALL inherit discriminator metadata from its source entity if applicable
4. WHEN a projection is generated THEN it SHALL inherit table name from its source entity
5. THE projection SHALL NOT inherit write-specific metadata like transaction requirements

### Requirement 6: Backward Compatibility

**User Story:** As a developer with existing projection code, I want the new interface implementation to be backward compatible, so that my existing code continues to work without modifications.

#### Acceptance Criteria

1. THE existing `IProjectionModel<TSelf>` interface SHALL remain available and functional
2. WHEN existing projection code uses `IProjectionModel<TSelf>` THEN it SHALL continue to work without modification
3. THE existing projection extension methods SHALL continue to work with the new interface implementation
4. THE generated projection classes SHALL implement both `IProjectionModel<TSelf>` and `IReadOnlyEntity<TSelf>`
5. THE existing `ProjectionExpression` property SHALL remain available on projection types

### Requirement 7: Documentation and Testing

**User Story:** As a developer and maintainer, I want comprehensive documentation and test coverage for the projection interface enhancement, so that the feature is well-documented and thoroughly tested.

#### Acceptance Criteria

1. THE `fluentdynamodb.md` steering document SHALL be updated with projection interface examples and usage patterns
2. THE `CHANGELOG.md` SHALL be updated with the new projection interface feature
3. THE `docs/DOCUMENTATION_CHANGELOG.md` SHALL be updated to track documentation corrections and improvements
4. THE API consistency tests SHALL include projection interface compatibility tests
5. THE integration tests SHALL include end-to-end projection query scenarios with the new interfaces

### Requirement 8: Error Handling and Diagnostics

**User Story:** As a developer, I want clear error messages and diagnostics when projection interface issues occur, so that I can quickly identify and resolve configuration problems.

#### Acceptance Criteria

1. WHEN a projection cannot inherit metadata from its source entity THEN the system SHALL emit a clear diagnostic error
2. WHEN a projection is used in an incompatible context THEN the system SHALL provide helpful error messages
3. THE source generator SHALL validate that projection source entities exist and are properly configured
4. THE system SHALL provide clear compile-time errors for projection interface violations
5. THE error messages SHALL include suggestions for resolving projection configuration issues
