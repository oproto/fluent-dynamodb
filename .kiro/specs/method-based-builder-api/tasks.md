# Implementation Plan

- [x] 1. Update DynamoDbTableBase to use method-based builder access
  - Remove property-based Query, Get, Update, Delete, Put properties
  - Add Query() method returning QueryRequestBuilder
  - Add Query(string expression, params object[] values) overload for format string support
  - Add virtual Get(), Update(), Delete() methods for derived class overrides
  - Add Put() method
  - Update AsScannable() to work with method-based API
  - _Requirements: 1.1, 1.2, 1.3, 1.4, 5.1, 5.2, 6.1, 6.2_

- [x] 2. Update DynamoDbIndex classes for method-based access
  - [x] 2.1 Update non-generic DynamoDbIndex class
    - Remove property-based Query property
    - Add Query() method returning QueryRequestBuilder
    - Add constructor overload accepting partition and sort key names
    - Add Query(string pk) overload for partition key queries
    - Add Query(string pk, string sk, string operator) overload for composite key queries
    - _Requirements: 4.1, 4.2, 4.3, 4.4, 10.3, 10.4_

  - [x] 2.2 Update generic DynamoDbIndex<TDefault> class
    - Remove property-based Query property
    - Delegate to non-generic DynamoDbIndex for all Query methods
    - Add constructor overload accepting key names
    - Maintain backward compatibility for existing generic index usage
    - _Requirements: 4.1, 4.2, 4.3, 4.4, 10.1, 10.2_

- [x] 3. Create example manual table implementations
  - [x] 3.1 Create single partition key table example
    - Implement table class deriving from DynamoDbTableBase
    - Override Get(string pk) method
    - Override Update(string pk) method
    - Override Delete(string pk) method
    - Add example index definitions with key names
    - _Requirements: 2.1, 2.4, 3.1, 3.2, 3.3_

  - [x] 3.2 Create composite key table example
    - Implement table class deriving from DynamoDbTableBase
    - Override Get(string pk, string sk) method
    - Override Update(string pk, string sk) method
    - Override Delete(string pk, string sk) method
    - Add example index definitions with key names
    - _Requirements: 2.2, 2.4, 3.2, 3.3, 3.4_

- [x] 4. Update source generator for method-based table generation
  - [x] 4.1 Analyze entity attributes to determine key structure
    - Detect partition key from [PartitionKey] attribute
    - Detect sort key from [SortKey] attribute
    - Determine if table has single or composite key
    - _Requirements: 7.1, 7.2, 7.3_

  - [x] 4.2 Generate appropriate Get/Update/Delete overloads
    - Generate Get(pk), Update(pk), Delete(pk) for single-key tables
    - Generate Get(pk, sk), Update(pk, sk), Delete(pk, sk) for composite-key tables
    - Use correct attribute names from entity definition
    - Add XML documentation to generated methods
    - _Requirements: 7.2, 7.3, 8.1, 8.2, 8.4_

  - [x] 4.3 Generate index definitions with key names
    - Analyze [GlobalSecondaryIndex] attributes for key structure
    - Generate DynamoDbIndex constructors with partition and sort key names
    - Include projection expressions where specified
    - Enable Query(pk) and Query(pk, sk) overloads on generated indexes
    - _Requirements: 7.4, 4.1, 4.2, 4.3_

- [x] 5. Update unit tests for method-based API
  - [x] 5.1 Update DynamoDbTableBase tests
    - Test Query() returns correct builder type
    - Test Query(expression, params) configures key condition correctly
    - Test Get(), Update(), Delete(), Put() return correct builder types
    - Test virtual methods can be overridden
    - _Requirements: 1.1, 1.2, 1.3, 1.4, 6.1, 6.2_

  - [x] 5.2 Update DynamoDbIndex tests
    - Test Query() returns correct builder with index configuration
    - Test Query(pk) configures partition key condition
    - Test Query(pk, sk) configures composite key condition
    - Test Query(pk, sk, operator) with different operators
    - Test error handling for unconfigured key names
    - _Requirements: 4.1, 4.2, 4.3, 4.4, 10.1, 10.2, 10.3, 10.4_

  - [x] 5.3 Create tests for manual table implementations
    - Test single-key table Get/Update/Delete overloads
    - Test composite-key table Get/Update/Delete overloads
    - Test index Query overloads work correctly
    - Verify key values are properly configured
    - _Requirements: 2.1, 2.2, 2.4, 3.1, 3.2, 3.3, 3.4_

- [x] 6. Update integration tests for end-to-end scenarios
  - Test complete query workflows with Query(expression, params)
  - Test Get/Update/Delete operations with key parameters
  - Test index queries with various key configurations
  - Verify results match expected DynamoDB behavior
  - Test format string integration with other fluent methods
  - _Requirements: 6.1, 6.2, 6.3, 6.4_

- [ ] 7. Update source generator tests
  - Test generator produces correct Get/Update/Delete overloads for single-key tables
  - Test generator produces correct Get/Update/Delete overloads for composite-key tables
  - Test generator creates index definitions with key names
  - Verify generated code compiles and works correctly
  - Test edge cases (no keys, multiple indexes, etc.)
  - _Requirements: 7.1, 7.2, 7.3, 7.4, 9.1, 9.2, 9.3, 9.4_

- [ ] 9. Update documentation and examples
  - [ ] 9.1 Update XML documentation for new methods
    - Add comprehensive XML docs to Query(), Query(expression, params)
    - Document virtual Get(), Update(), Delete() methods
    - Add examples showing method-based usage patterns
    - Document Query expression parameter syntax
    - _Requirements: 8.1, 8.2, 8.4_

  - [ ] 9.2 Update code examples in documentation
    - Update examples to use method-based API
    - Show Query(expression, params) usage patterns
    - Demonstrate Get/Update/Delete with key parameters
    - Include index Query overload examples
    - _Requirements: 8.1, 8.2, 8.3_

- [ ] 10. Update RealworldExample code
  - Update TransactionsTable to use method-based API
  - Update TransactionRepository to use new patterns
  - Demonstrate Query(expression, params) usage
  - Show Get/Update/Delete with key parameters
  - Update index queries to use new overloads
  - _Requirements: 1.1, 1.2, 2.1, 2.2, 4.1, 4.2_
