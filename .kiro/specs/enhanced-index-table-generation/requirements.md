# Requirements Document

## Introduction

This feature enhances the source generator to provide more consistent and type-safe APIs for DynamoDB index operations and table class references. Currently, index access uses a different pattern than entity accessors, and table references are string-based which can lead to typos. This feature addresses these inconsistencies by generating typed index classes with consistent API patterns and supporting type-safe table class references.

## Glossary

- **GSI**: Global Secondary Index - A DynamoDB index with a different partition key than the base table
- **LSI**: Local Secondary Index - A DynamoDB index that shares the partition key with the base table but has a different sort key
- **Index Property Name**: The C# property name used to access an index on the table class (e.g., `table.Gsi1`)
- **Index Name**: The actual DynamoDB index name (e.g., `"gsi1"`, `"status-index"`)
- **Projection Type**: An entity type that represents the projected attributes of an index
- **Entity Accessor**: A generated nested class that provides entity-specific operations (e.g., `table.Users.Query()`)

## Requirements

### Requirement 1: Index Property Naming

**User Story:** As a developer, I want to customize the C# property name for generated index accessors, so that I can use meaningful names like `StatusIndex` instead of `gsi1`.

#### Acceptance Criteria

1. WHEN a developer specifies a `Name` property on `[GlobalSecondaryIndex]` THEN the source generator SHALL use that name for the generated index property
2. WHEN a developer specifies a `Name` property on `[LocalSecondaryIndex]` THEN the source generator SHALL use that name for the generated index property
3. WHEN no `Name` property is specified THEN the source generator SHALL derive the property name from the DynamoDB index name using PascalCase conversion
4. WHEN multiple entities on the same table define the same index with different `Name` values THEN the source generator SHALL emit a compile-time diagnostic error
5. WHEN multiple entities on the same table define the same index and only one specifies a `Name` THEN the source generator SHALL use the specified name for all entities

### Requirement 2: Generated Typed Index Classes

**User Story:** As a developer, I want generated index classes to provide the same API patterns as entity accessors, so that I have a consistent experience across all table operations.

#### Acceptance Criteria

1. WHEN an index is defined on an entity THEN the source generator SHALL generate a typed index class as a nested class in the table
2. WHEN querying a typed index THEN the system SHALL support `Index.Query<T>()` returning `QueryRequestBuilder<T>`
3. WHEN querying a typed index THEN the system SHALL support `Index.Query<T>(Expression<Func<T, bool>>)` for LINQ-based key conditions
4. WHEN querying a typed index THEN the system SHALL support `Index.Query<T>(string, params object[])` for format-string key conditions
5. WHEN querying a typed index with key and filter THEN the system SHALL support `Index.Query<T>(Expression<Func<T, bool>>, Expression<Func<T, bool>>)`
6. WHEN an index has a projection type defined THEN the system SHALL support `Index.Query()` without generic parameter, defaulting to the projection type
7. WHEN an index has a projection type defined THEN the system SHALL automatically apply the projection expression to queries

### Requirement 3: Index Class Base Type

**User Story:** As a developer, I want generated index classes to inherit from a base class, so that I can extend them with custom methods in partial classes.

#### Acceptance Criteria

1. WHEN generating a typed index class THEN the source generator SHALL make the class partial
2. WHEN generating a typed index class THEN the source generator SHALL inherit from `DynamoDbIndex` base class
3. WHEN a developer creates a partial class for the index THEN the developer SHALL be able to add custom methods
4. THE `DynamoDbIndex` base class SHALL remain non-abstract to support dynamic/raw index scenarios

### Requirement 4: Type-Safe Table Class References

**User Story:** As a developer, I want to reference table classes using `typeof()` instead of strings, so that I get compile-time safety and refactoring support.

#### Acceptance Criteria

1. WHEN a developer uses `[DynamoDbTable(typeof(MyTableClass))]` THEN the source generator SHALL use the specified type as the table class
2. WHEN using type-based table reference THEN the developer SHALL define a partial class for the table
3. WHEN using type-based table reference THEN the source generator SHALL emit a diagnostic error if the referenced type is not a partial class
4. WHEN using string-based table reference `[DynamoDbTable("TableName")]` THEN the source generator SHALL continue to auto-generate the table class name
5. THE system SHALL support both string-based and type-based table references simultaneously across different entities

### Requirement 5: Index Name Conflict Resolution

**User Story:** As a developer working with multi-entity tables, I want the source generator to validate index configurations across entities, so that I catch configuration errors at compile time.

#### Acceptance Criteria

1. WHEN multiple entities define the same DynamoDB index name THEN the source generator SHALL merge the index definitions
2. WHEN multiple entities define conflicting `Name` properties for the same index THEN the source generator SHALL emit a compile-time diagnostic error with both conflicting values
3. WHEN multiple entities define the same index with compatible configurations THEN the source generator SHALL generate a single index property on the table class
4. WHEN an index is defined on multiple entities THEN the generated index class SHALL support querying all entity types that use that index

### Requirement 6: Backward Compatibility

**User Story:** As a developer with existing code, I want the new features to be opt-in, so that my existing code continues to work without changes.

#### Acceptance Criteria

1. WHEN no `Name` property is specified on index attributes THEN the system SHALL use the existing behavior of deriving names from DynamoDB index names
2. WHEN using string-based `[DynamoDbTable("name")]` THEN the system SHALL continue to work as before
3. THE existing `DynamoDbIndex` and `DynamoDbIndex<T>` classes SHALL remain available for manual/dynamic index usage
4. WHEN upgrading to the new version THEN existing code SHALL compile without modifications
