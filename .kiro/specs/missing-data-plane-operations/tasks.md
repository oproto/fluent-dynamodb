# Implementation Plan

- [x] 1. Create core table interfaces and infrastructure
  - Create `IDynamoDbTable` interface with core operations (Get, Put, Update, Query, Delete)
  - Create `IScannableDynamoDbTable` interface extending `IDynamoDbTable` with Scan operation
  - Implement `ScannableDynamoDbTable` wrapper class with pass-through functionality
  - Update `DynamoDbTableBase` to implement `IDynamoDbTable` and add `AsScannable()` method
  - _Requirements: 5.1, 5.2, 5.3, 5.4, 5.5_

- [ ] 2. Implement DeleteItem operation
- [x] 2.1 Create DeleteItemRequestBuilder class
  - Implement `DeleteItemRequestBuilder` class with fluent interface
  - Add support for key specification using `IWithKey<DeleteItemRequestBuilder>` interface
  - Add support for condition expressions using `IWithConditionExpression<DeleteItemRequestBuilder>` interface
  - Add support for attribute names and values using appropriate interfaces
  - Implement return value options (ALL_OLD, NONE) and consumed capacity methods
  - Add `ExecuteAsync()` and `ToDeleteItemRequest()` methods
  - _Requirements: 2.1, 2.2, 2.3, 2.4, 2.5, 2.6, 6.1, 6.2, 6.3, 6.4_

- [x] 2.2 Add Delete property to DynamoDbTableBase
  - Add `Delete` property to `DynamoDbTableBase` that returns configured `DeleteItemRequestBuilder`
  - Add `Delete` property to `IDynamoDbTable` interface
  - Update `ScannableDynamoDbTable` to pass through Delete operation
  - _Requirements: 2.1, 6.5_

- [x] 2.3 Create unit tests for DeleteItemRequestBuilder
  - Write comprehensive unit tests covering all builder methods
  - Test key specification patterns (single key, composite key, AttributeValue overloads)
  - Test condition expression functionality
  - Test return value and consumed capacity options
  - Test request building and execution patterns
  - _Requirements: 2.1, 2.2, 2.3, 2.4, 2.5, 2.6_

- [ ] 3. Implement Scan operations with intentional friction
- [x] 3.1 Create ScanRequestBuilder class
  - Implement `ScanRequestBuilder` class with fluent interface
  - Add support for filter expressions and projection expressions
  - Add support for index scanning, pagination, and consistent read options
  - Add support for parallel scanning with `WithSegment(int segment, int totalSegments)` method
  - Add support for limit, count, and consumed capacity options
  - Implement attribute names and values using `IWithAttributeNames` and `IWithAttributeValues` interfaces
  - Add `ExecuteAsync()` and `ToScanRequest()` methods
  - _Requirements: 1.5, 6.1, 6.2, 6.3, 6.4_

- [x] 3.2 Add Scan property to IScannableDynamoDbTable
  - Add `Scan` property to `IScannableDynamoDbTable` interface
  - Implement `Scan` property in `ScannableDynamoDbTable` that returns configured `ScanRequestBuilder`
  - Ensure scan operations are only accessible through `AsScannable()` method
  - _Requirements: 1.1, 1.2, 1.3, 1.4, 1.5_

- [x] 3.3 Create unit tests for ScanRequestBuilder and scannable functionality
  - Write comprehensive unit tests for `ScanRequestBuilder` covering all methods
  - Test filter expressions, projections, and index scanning
  - Test parallel scan functionality with segment parameters
  - Test that scan operations are only accessible through `AsScannable()`
  - Test that `ScannableDynamoDbTable` properly passes through all operations
  - Verify access to underlying table through `UnderlyingTable` property
  - _Requirements: 1.1, 1.2, 1.3, 1.4, 1.5, 5.5_

- [ ] 4. Implement BatchGetItem operations
- [x] 4.1 Create BatchGetItemBuilder class
  - Implement `BatchGetItemBuilder` class following transaction builder pattern
  - Add support for key specification using `IWithKey<BatchGetItemBuilder>` interface
  - Add support for projection expressions and consistent read options
  - Add support for attribute names using `IWithAttributeNames<BatchGetItemBuilder>` interface
  - Implement `ToKeysAndAttributes()` method for internal use
  - _Requirements: 3.2, 3.3, 3.4, 6.1, 6.2, 6.4_

- [x] 4.2 Create BatchGetItemRequestBuilder class
  - Implement `BatchGetItemRequestBuilder` class following `TransactGetItemsRequestBuilder` pattern
  - Add `GetFromTable(string tableName, Action<BatchGetItemBuilder> builderAction)` method
  - Add support for consumed capacity options
  - Add `ExecuteAsync()` and `ToBatchGetItemRequest()` methods
  - Handle multiple tables and multiple keys per table
  - _Requirements: 3.1, 3.5, 3.6, 6.1, 6.3_

- [ ] 4.3 Create unit tests for BatchGetItem operations
  - Write comprehensive unit tests for `BatchGetItemBuilder` covering key specification and options
  - Write comprehensive unit tests for `BatchGetItemRequestBuilder` covering multi-table operations
  - Test projection expressions, consistent read, and consumed capacity options
  - Test request building with multiple tables and multiple keys per table
  - Test execution patterns and response handling
  - _Requirements: 3.1, 3.2, 3.3, 3.4, 3.5, 3.6_

- [ ] 5. Implement BatchWriteItem operations
- [ ] 5.1 Create BatchWriteItemBuilder class
  - Implement `BatchWriteItemBuilder` class for individual table write operations
  - Add `PutItem(Dictionary<string, AttributeValue> item)` method for put operations
  - Add `PutItem<T>(T item, Func<T, Dictionary<string, AttributeValue>> mapper)` method with model mapping
  - Add `DeleteItem` methods with key specification (single key and composite key overloads)
  - Implement `ToWriteRequests()` method for internal use
  - _Requirements: 4.2, 4.3, 6.1, 6.4_

- [ ] 5.2 Create BatchWriteItemRequestBuilder class
  - Implement `BatchWriteItemRequestBuilder` class following `TransactWriteItemsRequestBuilder` pattern
  - Add `WriteToTable(string tableName, Action<BatchWriteItemBuilder> builderAction)` method
  - Add support for consumed capacity and item collection metrics options
  - Add `ExecuteAsync()` and `ToBatchWriteItemRequest()` methods
  - Handle multiple tables with mixed put and delete operations
  - _Requirements: 4.1, 4.4, 4.5, 4.6, 6.1, 6.3_

- [ ] 5.3 Create unit tests for BatchWriteItem operations
  - Write comprehensive unit tests for `BatchWriteItemBuilder` covering put and delete operations
  - Write comprehensive unit tests for `BatchWriteItemRequestBuilder` covering multi-table operations
  - Test model mapping functionality for put operations
  - Test key specification patterns for delete operations
  - Test consumed capacity and item collection metrics options
  - Test request building with multiple tables and mixed operations
  - _Requirements: 4.1, 4.2, 4.3, 4.4, 4.5, 4.6_

- [ ] 6. Create integration tests and verify AOT compatibility
- [ ] 6.1 Create integration tests for all new operations
  - Write integration tests using mock DynamoDB client for all new request builders
  - Test end-to-end flows for Delete, Scan, BatchGet, and BatchWrite operations
  - Test scannable table wrapper functionality in integration scenarios
  - Verify that all operations follow consistent patterns and interfaces
  - _Requirements: 6.1, 6.2, 6.3, 6.4, 6.5_

- [ ] 6.2 Verify AOT compatibility for all new components
  - Ensure all new classes and interfaces are AOT-compatible
  - Verify no reflection or dynamic code generation is used
  - Test trimmer-safe characteristics of new components
  - Run AOT compilation tests on new functionality
  - _Requirements: 7.1, 7.2, 7.3, 7.4_

- [ ] 7. Update documentation and examples
- [ ] 7.1 Add XML documentation to all new public APIs
  - Add comprehensive XML documentation to all new public classes and methods
  - Document the intentional friction pattern for scan operations
  - Include usage examples in XML documentation
  - Document performance implications and best practices
  - _Requirements: 1.1, 1.2, 1.3, 1.4, 1.5_

- [ ] 7.2 Create usage examples for each new operation
  - Create example code demonstrating DeleteItem usage patterns
  - Create example code demonstrating Scan usage with `AsScannable()` pattern
  - Create example code demonstrating BatchGetItem usage for multiple tables
  - Create example code demonstrating BatchWriteItem usage with mixed operations
  - Include examples of parallel scan usage for large tables
  - _Requirements: 1.1, 2.1, 3.1, 4.1_