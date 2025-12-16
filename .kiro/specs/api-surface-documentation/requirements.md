# Requirements Document

## Introduction

This specification defines the requirements for creating comprehensive API surface documentation and compile-time validation tests for Oproto.FluentDynamoDb. The goal is to establish a single source of truth for the expected API patterns, create a compact steering document for consuming projects, and build out compile-time tests that validate all expected API variations exist.

## Glossary

- **API Surface**: The complete set of public methods, classes, and patterns available to consumers of the library
- **ApiConsistencyTests**: A test project that validates API patterns compile correctly without executing runtime tests
- **Steering Document**: A markdown file included in consuming projects to provide AI assistants with API context
- **Generated Table Class**: Source-generated class (e.g., `BasicPkTable`) that implements `IDynamoDbTable` and provides all DynamoDB operations
- **Entity Accessor**: Generated nested class property on table classes providing type-safe access to entity operations (e.g., `table.Orders`)
- **Request Builder**: Fluent builder classes for constructing DynamoDB requests (e.g., `QueryRequestBuilder<T>`)
- **Expression Style**: The three ways to specify expressions - Lambda, Format String, or Manual WithValue
- **Convenience Method**: Direct async methods that combine builder creation and execution (e.g., `GetAsync`, `PutAsync`)
- **Raw SDK Overload**: Methods that accept pre-built AWS SDK request objects
- **DynamoDbIndex**: Class for querying GSI/LSI indexes, instantiated as properties on generated table classes

## Requirements

### Requirement 1

**User Story:** As a developer consuming FluentDynamoDb, I want a compact steering document that shows all available API patterns, so that AI assistants can help me write correct code without trial and error.

#### Acceptance Criteria

1. WHEN the steering document is included in a consuming project THEN the system SHALL provide examples of all CRUD operations (Get, Put, Update, Delete, Query, Scan)
2. WHEN showing API patterns THEN the system SHALL demonstrate all three expression styles (Lambda, Format String, Manual) for each operation type
3. WHEN documenting operations THEN the system SHALL show both builder pattern (`.GetItemAsync()`) and convenience methods (`GetAsync()`)
4. WHEN documenting table access THEN the system SHALL show both direct table methods and entity accessor patterns
5. WHEN documenting index operations THEN the system SHALL show GSI and LSI query patterns with the `DynamoDbIndex` class
6. WHEN documenting batch operations THEN the system SHALL show `DynamoDbBatch.Get`, `DynamoDbBatch.Write`, and `DynamoDbBatch.PartiQL` patterns
7. WHEN documenting transactions THEN the system SHALL show `DynamoDbTransactions.Get` and `DynamoDbTransactions.Write` patterns
8. WHEN documenting PartiQL THEN the system SHALL show `ExecutePartiQL` methods and batch PartiQL operations
9. WHEN documenting raw SDK access THEN the system SHALL show methods accepting pre-built SDK request objects
10. WHEN the steering document is created THEN the system SHALL keep total size under 500 lines to minimize context consumption

### Requirement 2

**User Story:** As a library maintainer, I want compile-time tests that validate all expected API patterns exist, so that I can catch API surface regressions before release.

#### Acceptance Criteria

1. WHEN the ApiConsistencyTests project builds THEN the system SHALL validate that all documented API patterns compile successfully
2. WHEN testing Get operations THEN the system SHALL validate builder pattern, convenience methods, entity accessor, and raw SDK overloads
3. WHEN testing Put operations THEN the system SHALL validate entity puts, dictionary puts, condition expressions, and raw SDK overloads
4. WHEN testing Update operations THEN the system SHALL validate all three expression styles, condition expressions, and raw SDK overloads
5. WHEN testing Delete operations THEN the system SHALL validate key-based deletes, condition expressions, and raw SDK overloads
6. WHEN testing Query operations THEN the system SHALL validate all three expression styles, filter expressions, pagination, and projections
7. WHEN testing Scan operations THEN the system SHALL validate filter expressions and pagination on scannable tables
8. WHEN testing index operations THEN the system SHALL validate GSI and LSI query patterns through `DynamoDbIndex`
9. WHEN testing batch operations THEN the system SHALL validate BatchGet, BatchWrite, and BatchPartiQL patterns
10. WHEN testing transactions THEN the system SHALL validate TransactionGet and TransactionWrite with all operation types
11. WHEN testing PartiQL THEN the system SHALL validate single and batch PartiQL execution patterns
12. WHEN testing convenience methods THEN the system SHALL validate `GetAsync`, `PutAsync`, `DeleteAsync` on both table and entity accessor

### Requirement 3

**User Story:** As a library maintainer, I want the steering document to stay synchronized with API changes, so that consuming projects always have accurate API information.

#### Acceptance Criteria

1. WHEN API changes are made to request builders THEN the documentation.md steering file SHALL instruct maintainers to update fluentdynamodb.md
2. WHEN the fluentdynamodb.md steering document is created THEN the system SHALL place it in `.kiro/steering/` directory
3. WHEN documenting the update process THEN the system SHALL add instructions to documentation.md for keeping fluentdynamodb.md current

### Requirement 4

**User Story:** As a developer, I want the API tests organized by operation category, so that I can easily find and verify specific API patterns.

#### Acceptance Criteria

1. WHEN organizing test files THEN the system SHALL group tests by operation category (SingleEntityTables, Batch, Transactions, GeoSpatial, etc.)
2. WHEN creating test entities THEN the system SHALL provide entities covering PK-only, PK+SK, scannable, and GSI/LSI patterns
3. WHEN writing test methods THEN the system SHALL use descriptive names indicating the API pattern being validated
4. WHEN test methods are created THEN the system SHALL use `[Fact(Skip = "API Surface Validation")]` to prevent runtime execution

### Requirement 5

**User Story:** As a developer, I want to see the correct terminal methods for each operation type, so that I don't use incorrect method names like `ExecuteAsync()`.

#### Acceptance Criteria

1. WHEN documenting Get operations THEN the system SHALL show `GetItemAsync()` as the terminal method
2. WHEN documenting Put operations THEN the system SHALL show `PutAsync()` as the terminal method
3. WHEN documenting Update operations THEN the system SHALL show `UpdateAsync()` as the terminal method
4. WHEN documenting Delete operations THEN the system SHALL show `DeleteAsync()` as the terminal method
5. WHEN documenting Query/Scan operations THEN the system SHALL show `ToListAsync()` as the terminal method
6. WHEN documenting batch and transaction operations THEN the system SHALL show `ExecuteAsync()` as the terminal method
7. WHEN documenting composite entity queries THEN the system SHALL show `ToCompositeEntityAsync()` and `ToCompositeEntityListAsync()` methods
