# Requirements Document

## Introduction

This feature adds support for missing DynamoDB data plane operations to the Oproto.FluentDynamoDb library. The goal is to provide fluent builders for Scan, DeleteItem, and Batch operations while maintaining the library's design principles of type safety, AOT compatibility, and thoughtful API design. For Scan operations specifically, we implement a deliberate friction pattern to prevent accidental misuse while still providing access when legitimately needed.

## Requirements

### Requirement 1: Scan Operations with Intentional Friction

**User Story:** As a developer, I want to perform Scan operations on DynamoDB tables when necessary for legitimate use cases like data migration or analytics, but I want the API to discourage accidental usage since Scan is an anti-pattern for most applications.

#### Acceptance Criteria

1. WHEN a developer needs scan functionality THEN they SHALL call `AsScannable()` on a DynamoDbTableBase instance to get access to scan operations
2. WHEN `AsScannable()` is called THEN it SHALL return an `IScannableDynamoDbTable` interface that provides scan functionality
3. WHEN using the scannable interface THEN the developer SHALL still have access to all standard table operations (Get, Put, Update, Query) through pass-through methods
4. WHEN implementing the scannable interface THEN it SHALL wrap the original DynamoDbTableBase instance without losing core functionality
5. WHEN a developer accesses scan operations THEN they SHALL use a fluent `ScanRequestBuilder` with standard DynamoDB scan parameters

### Requirement 2: DeleteItem Operations

**User Story:** As a developer, I want to delete individual items from DynamoDB tables using a fluent API that matches the existing patterns in the library.

#### Acceptance Criteria

1. WHEN a developer wants to delete an item THEN they SHALL use a `DeleteItemRequestBuilder` accessible via `table.Delete`
2. WHEN building a delete request THEN the builder SHALL support key specification using the same patterns as other builders
3. WHEN building a delete request THEN the builder SHALL support condition expressions for conditional deletes
4. WHEN building a delete request THEN the builder SHALL support return values (ALL_OLD, NONE)
5. WHEN building a delete request THEN the builder SHALL support consumed capacity and item collection metrics options
6. WHEN executing a delete request THEN it SHALL return a `DeleteItemResponse` from the AWS SDK

### Requirement 3: BatchGetItem Operations

**User Story:** As a developer, I want to retrieve multiple items from one or more DynamoDB tables in a single request to optimize performance and reduce API calls.

#### Acceptance Criteria

1. WHEN a developer needs to get multiple items THEN they SHALL use a `BatchGetItemRequestBuilder` 
2. WHEN building a batch get request THEN the builder SHALL support adding items from multiple tables
3. WHEN adding items to batch get THEN the builder SHALL support key specification and projection expressions per table
4. WHEN building a batch get request THEN the builder SHALL support consistent read options per table
5. WHEN executing a batch get request THEN it SHALL handle unprocessed keys automatically or provide access to them
6. WHEN executing a batch get request THEN it SHALL return a `BatchGetItemResponse` from the AWS SDK

### Requirement 4: BatchWriteItem Operations

**User Story:** As a developer, I want to put or delete multiple items across one or more DynamoDB tables in a single request to optimize performance and reduce API calls.

#### Acceptance Criteria

1. WHEN a developer needs to write multiple items THEN they SHALL use a `BatchWriteItemRequestBuilder`
2. WHEN building a batch write request THEN the builder SHALL support adding put and delete operations for multiple tables
3. WHEN adding operations to batch write THEN the builder SHALL support the same item and condition patterns as individual operations
4. WHEN building a batch write request THEN the builder SHALL support consumed capacity and item collection metrics options
5. WHEN executing a batch write request THEN it SHALL handle unprocessed items automatically or provide access to them
6. WHEN executing a batch write request THEN it SHALL return a `BatchWriteItemResponse` from the AWS SDK

### Requirement 5: Interface Segregation for Table Operations

**User Story:** As a developer extending DynamoDbTableBase, I want to maintain access to my custom table methods and properties when using the scannable interface, while keeping the core interface clean.

#### Acceptance Criteria

1. WHEN implementing table interfaces THEN there SHALL be an `IDynamoDbTable` interface that defines core operations (Get, Put, Update, Query)
2. WHEN implementing scannable functionality THEN there SHALL be an `IScannableDynamoDbTable` interface that extends `IDynamoDbTable` and adds scan operations
3. WHEN `DynamoDbTableBase` is updated THEN it SHALL implement `IDynamoDbTable`
4. WHEN `AsScannable()` is called THEN it SHALL return a wrapper that implements `IScannableDynamoDbTable`
5. WHEN using the scannable wrapper THEN custom properties and methods from the original table class SHALL still be accessible through the underlying instance

### Requirement 6: Consistent API Patterns

**User Story:** As a developer familiar with the existing library, I want all new operations to follow the same fluent patterns and conventions as existing builders.

#### Acceptance Criteria

1. WHEN implementing new builders THEN they SHALL follow the same fluent interface patterns as existing builders
2. WHEN implementing new builders THEN they SHALL use the same attribute name and value handling patterns
3. WHEN implementing new builders THEN they SHALL support the same execution patterns (`ExecuteAsync()` and `ToRequest()` methods)
4. WHEN implementing new builders THEN they SHALL implement appropriate shared interfaces (`IWithAttributeNames`, `IWithAttributeValues`, etc.)
5. WHEN adding new operations to table classes THEN they SHALL follow the same property naming conventions as existing operations

### Requirement 7: AOT Compatibility

**User Story:** As a developer using AOT compilation, I want all new operations to maintain the library's AOT compatibility without introducing reflection or dynamic code generation.

#### Acceptance Criteria

1. WHEN implementing new builders THEN they SHALL not use reflection or dynamic code generation
2. WHEN implementing new builders THEN they SHALL be compatible with Native AOT compilation
3. WHEN implementing new interfaces THEN they SHALL not introduce generic constraints that break AOT compatibility
4. WHEN implementing new operations THEN they SHALL maintain the library's trimmer-safe characteristics