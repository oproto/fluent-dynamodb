# Requirements Document

## Introduction

This document specifies the requirements for a comprehensive set of architecture improvements and new features for Oproto.FluentDynamoDb targeting the v1.0 release. The changes include removing legacy base classes, adding PartiQL support, enabling direct SDK request passing, introducing a DynamicEntity for schema-less table access, fixing the GeoHash query bug, and clarifying interface relationships. These improvements aim to provide a cleaner architecture, better flexibility for advanced scenarios, and improved developer experience.

## Glossary

- **DynamoDbTableBase**: The current abstract base class that generated table classes inherit from, providing common functionality like Client and TableName properties
- **PartiQL**: A SQL-compatible query language for DynamoDB that allows querying data using familiar SQL syntax
- **DynamicEntity**: A built-in entity type that uses only dynamic fields, enabling schema-less access to any DynamoDB table
- **DynamicTable**: A table class for working with DynamicEntity instances without requiring entity definitions
- **IDynamoDbEntity**: Interface implemented by all entities providing ToDynamoDb/FromDynamoDb serialization methods
- **IEntityMetadataProvider**: Interface providing static metadata about an entity (table name, key info, property mappings)
- **GeoHash**: A hierarchical spatial data structure that encodes geographic coordinates into a string
- **Expression Translator**: The component that converts C# lambda expressions into DynamoDB expression strings
- **Entity Accessor**: A generated property on table classes that provides type-safe access to entity operations
- **SDK Request**: Native AWS SDK request objects like QueryRequest, GetItemRequest, etc.

## Requirements

### Requirement 1

**User Story:** As a library maintainer, I want to remove DynamoDbTableBase and fully source-generate table classes, so that users can control the visibility of all operations via attributes and the architecture is cleaner.

#### Acceptance Criteria

1. WHEN the Source Generator processes an entity with `[DynamoDbTable]` THEN the generator SHALL produce a complete table class without inheriting from DynamoDbTableBase
2. WHEN a table class is generated THEN the class SHALL include Client, TableName, and Options properties directly in the generated code
3. WHEN a table class is generated THEN the class SHALL include entity accessors with visibility controlled by the `[GenerateAccessors]` attribute
4. WHEN a table class is generated THEN the class SHALL include index accessors for all defined GSIs and LSIs
5. WHEN the DynamoDbTableBase class is removed THEN existing code using generated table classes SHALL continue to compile without changes
6. WHEN a user attempts to instantiate DynamoDbTableBase directly THEN the compiler SHALL report an error because the class no longer exists

### Requirement 2

**User Story:** As a library maintainer, I want to clarify the relationship between IDynamoDbEntity and IEntityMetadataProvider, so that the interface hierarchy is clear and supports unit testing scenarios.

#### Acceptance Criteria

1. WHEN an entity is source-generated THEN the entity SHALL implement both IDynamoDbEntity and IEntityMetadataProvider
2. WHEN IDynamoDbEntity is used as a type constraint THEN it SHALL be sufficient for operations requiring serialization (ToDynamoDb/FromDynamoDb)
3. WHEN IEntityMetadataProvider is used as a type constraint THEN it SHALL be sufficient for operations requiring only metadata access
4. WHEN a developer creates a mock entity for unit testing THEN they SHALL be able to implement IEntityMetadataProvider without implementing full serialization
5. WHEN documentation describes these interfaces THEN it SHALL clearly explain the purpose of each interface and when to use which constraint

### Requirement 3

**User Story:** As a developer, I want to execute PartiQL queries against DynamoDB, so that I can use SQL-like syntax for complex queries or migration scenarios.

#### Acceptance Criteria

1. WHEN executing a PartiQL query THEN the system SHALL provide a method that accepts a PartiQL statement string and parameters
2. WHEN executing a PartiQL query with parameters THEN the system SHALL support format string placeholders for parameter substitution
3. WHEN a PartiQL query returns results THEN the system SHALL hydrate the results into the specified entity type using FromDynamoDb
4. WHEN a PartiQL query returns results for DynamicEntity THEN the system SHALL populate the DynamicFields collection with all returned attributes
5. WHEN executing a PartiQL statement that modifies data THEN the system SHALL support ExecuteStatement for INSERT, UPDATE, and DELETE operations
6. WHEN executing a batch of PartiQL statements THEN the system SHALL support BatchExecuteStatement for multiple operations

### Requirement 4

**User Story:** As a developer, I want to pass native SDK request objects directly to the library, so that I can leverage existing SDK code or handle edge cases not covered by the fluent builders.

#### Acceptance Criteria

1. WHEN calling GetAsync with a GetItemRequest THEN the system SHALL execute the request and hydrate the response to the entity type
2. WHEN calling PutAsync with a PutItemRequest THEN the system SHALL execute the request using the SDK client
3. WHEN calling UpdateAsync with an UpdateItemRequest THEN the system SHALL execute the request and optionally hydrate the response
4. WHEN calling DeleteAsync with a DeleteItemRequest THEN the system SHALL execute the request and optionally hydrate the response
5. WHEN calling QueryAsync with a QueryRequest THEN the system SHALL execute the request and hydrate all items to the entity type
6. WHEN calling ScanAsync with a ScanRequest THEN the system SHALL execute the request and hydrate all items to the entity type
7. WHEN using DynamoDbTransactions.Write with TransactWriteItemsRequest THEN the system SHALL execute the transaction directly
8. WHEN using DynamoDbBatch.Write with BatchWriteItemRequest THEN the system SHALL execute the batch directly
9. WHEN using DynamoDbBatch.Get with BatchGetItemRequest THEN the system SHALL execute the batch and hydrate responses

### Requirement 5

**User Story:** As a developer, I want to access any DynamoDB table without defining entity classes, so that I can explore unknown schemas, build migration tools, or work with tables that have no fixed schema.

#### Acceptance Criteria

1. WHEN creating a DynamicTable THEN the constructor SHALL accept the DynamoDB client, table name, and optional key configuration
2. WHEN key configuration is provided to DynamicTable THEN the system SHALL enable typed Get, Delete, and Update overloads based on the configured key types
3. WHEN key configuration specifies string keys THEN the system SHALL provide overloads accepting string parameters for partition key and optional sort key
4. WHEN key configuration specifies numeric keys THEN the system SHALL provide overloads accepting numeric parameters for partition key and optional sort key
5. WHEN no key configuration is provided THEN the system SHALL require AttributeValue parameters for key-based operations
6. WHEN querying a DynamicTable THEN the system SHALL return DynamicEntity instances with all attributes in the DynamicFields collection
7. WHEN using lambda expressions with DynamicEntity THEN the Expression Translator SHALL allow DynamicFields indexer access in key conditions without validation errors
8. WHEN using lambda expressions with DynamicEntity THEN the Expression Translator SHALL skip partition key and sort key validation that normally applies to typed entities

### Requirement 6

**User Story:** As a developer, I want the GeoHash query functionality to work correctly, so that I can perform spatial queries without syntax errors.

#### Acceptance Criteria

1. WHEN executing a GeoHash-based spatial query THEN the system SHALL generate valid KeyConditionExpression syntax for BETWEEN clauses
2. WHEN a GeoHash range query uses numeric values THEN the system SHALL properly format the values as AttributeValue with correct type indicators
3. WHEN a GeoHash query includes string-based geohash values THEN the system SHALL properly quote and escape the values in expressions
4. WHEN the GeoHash query executes successfully THEN the system SHALL return entities within the specified geographic bounds

### Requirement 7

**User Story:** As a developer, I want DynamicEntity to support all the same query patterns as regular entities, so that I have a consistent experience regardless of whether I use typed or dynamic entities.

#### Acceptance Criteria

1. WHEN building a Query for DynamicEntity THEN the system SHALL support Where clauses using DynamicFields indexer syntax
2. WHEN building a Query for DynamicEntity THEN the system SHALL support WithFilter clauses using DynamicFields indexer syntax
3. WHEN building a Scan for DynamicEntity THEN the system SHALL support WithFilter clauses using DynamicFields indexer syntax
4. WHEN using comparison operators with DynamicFields in expressions THEN the system SHALL support ==, !=, <, >, <=, >= operators
5. WHEN using string methods with DynamicFields in expressions THEN the system SHALL support BeginsWith, Contains operations
6. WHEN checking field existence with DynamicFields THEN the system SHALL support Exists() and NotExists() methods

### Requirement 8

**User Story:** As a developer, I want to update and delete items in a DynamicTable using the same patterns as typed tables, so that I can modify data without defining entity classes.

#### Acceptance Criteria

1. WHEN updating an item in DynamicTable THEN the system SHALL support Set operations using DynamicFields
2. WHEN updating an item in DynamicTable THEN the system SHALL support Remove operations for dynamic fields
3. WHEN updating an item in DynamicTable THEN the system SHALL support Add operations for numeric and set fields
4. WHEN deleting an item from DynamicTable with key configuration THEN the system SHALL accept typed key parameters
5. WHEN deleting an item from DynamicTable without key configuration THEN the system SHALL accept AttributeValue key parameters
6. WHEN a conditional expression is specified on update or delete THEN the system SHALL support DynamicFields in the condition

### Requirement 9

**User Story:** As a developer, I want clear documentation on when to use DynamicTable vs typed entities, so that I can make informed architectural decisions.

#### Acceptance Criteria

1. WHEN documentation describes DynamicTable THEN it SHALL explain use cases including schema exploration, migration tools, and truly schema-less scenarios
2. WHEN documentation describes DynamicTable THEN it SHALL explain the trade-offs compared to typed entities (no compile-time safety, no IntelliSense for fields)
3. WHEN documentation describes DynamicTable THEN it SHALL provide examples of key configuration for different key type scenarios
4. WHEN documentation describes DynamicTable THEN it SHALL show examples of Query, Scan, Get, Put, Update, and Delete operations

### Requirement 10

**User Story:** As a developer, I want the library to maintain backward compatibility where possible, so that upgrading to the new version requires minimal code changes.

#### Acceptance Criteria

1. WHEN upgrading from a version with DynamoDbTableBase THEN code using generated table classes SHALL compile without modification
2. WHEN upgrading from a version with DynamoDbTableBase THEN code using entity accessors SHALL work identically
3. WHEN upgrading from a version with DynamoDbTableBase THEN code using index accessors SHALL work identically
4. WHEN a breaking change is unavoidable THEN the BREAKING_CHANGES document SHALL document the change and provide migration guidance
5. WHEN deprecated APIs are removed THEN the CHANGELOG SHALL clearly indicate the removal and alternatives

