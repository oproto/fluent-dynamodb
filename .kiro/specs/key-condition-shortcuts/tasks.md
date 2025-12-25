# Implementation Plan: Key Condition Shortcuts

## Overview

This implementation adds `KeyCondition` enum and builder methods (`IfExists()`, `IfNotExists()`) to simplify common conditional patterns for Put, Update, and Delete operations. The implementation spans the main library (enum and builder methods) and source generator (convenience method parameters).

## Tasks

- [x] 1. Implement KeyCondition enum and core builder support
  - [x] 1.1 Create `KeyCondition` enum in `Oproto.FluentDynamoDb/`
    - Add `None = 0`, `MustExist = 1`, `MustNotExist = 2` values
    - Add XML documentation for each value
    - _Requirements: 1.1, 1.2, 1.3, 1.4_

  - [x] 1.2 Add key condition support to `PutItemRequestBuilder<TEntity>`
    - Add `_keyCondition` field
    - Add `IfExists()` method returning builder
    - Add `IfNotExists()` method returning builder
    - Add `WithKeyCondition(KeyCondition)` method returning builder
    - Add `ApplyKeyCondition()` helper method
    - Call `ApplyKeyCondition()` in request building
    - _Requirements: 2.1, 2.4, 2.5_

  - [x] 1.3 Add key condition support to `UpdateItemRequestBuilder<TEntity>`
    - Same pattern as PutItemRequestBuilder
    - _Requirements: 2.2, 2.4, 2.5_

  - [x] 1.4 Add key condition support to `DeleteItemRequestBuilder<TEntity>`
    - Same pattern as PutItemRequestBuilder
    - _Requirements: 2.3, 2.4, 2.5_

  - [x] 1.5 Write unit tests for builder methods
    - Test `IfExists()` sets correct key condition
    - Test `IfNotExists()` sets correct key condition
    - Test `WithKeyCondition()` sets correct key condition
    - Test method chaining works correctly

- [x] 2. Implement condition generation logic
  - [x] 2.1 Implement simple key condition generation
    - Generate `attribute_exists(pk)` for MustExist
    - Generate `attribute_not_exists(pk)` for MustNotExist
    - Use `EntityMetadata.PartitionKeyAttributeName`
    - _Requirements: 3.1, 3.2, 3.3_

  - [x] 2.2 Implement composite key condition generation
    - Generate `attribute_exists(pk) AND attribute_exists(sk)` for MustExist
    - Generate `attribute_not_exists(pk) AND attribute_not_exists(sk)` for MustNotExist
    - Use `EntityMetadata.SortKeyAttributeName`
    - _Requirements: 4.1, 4.2, 4.3_

  - [x] 2.3 Implement condition combination with Where clauses
    - Prepend key condition to existing ConditionExpression
    - Use `({keyCondition}) AND ({existingCondition})` format
    - Handle case where no existing condition
    - _Requirements: 5.1, 5.2, 5.3, 5.4_

  - [x] 2.4 Write property test for simple key condition generation
    - **Property 1: Simple Key Condition Generation**
    - **Validates: Requirements 3.1, 3.2**

  - [x] 2.5 Write property test for composite key condition generation
    - **Property 2: Composite Key Condition Generation**
    - **Validates: Requirements 4.1, 4.2, 4.3**

  - [x] 2.6 Write property test for condition combination
    - **Property 3: Condition Combination**
    - **Validates: Requirements 5.1, 5.2, 5.3, 5.4**

- [x] 3. Checkpoint - Ensure core functionality works
  - Ensure all tests pass, ask the user if questions arise.

- [x] 4. Update source generator for convenience methods
  - [x] 4.1 Update `EntityAccessorGenerator` for Put convenience methods
    - Add optional `KeyCondition keyCondition = KeyCondition.None` parameter to `PutAsync`
    - Add optional `KeyCondition keyCondition = KeyCondition.None` parameter to `PutAsyncResult`
    - Apply key condition to builder when not None
    - _Requirements: 6.1, 6.2, 6.3, 6.4_

  - [x] 4.2 Update `EntityAccessorGenerator` for Update methods
    - Add optional `KeyCondition keyCondition = KeyCondition.None` parameter to `Update(pk)` for simple key
    - Add optional `KeyCondition keyCondition = KeyCondition.None` parameter to `Update(pk, sk)` for composite key
    - Apply key condition to builder when not None
    - _Requirements: 7.1, 7.2, 7.3, 7.4_

  - [x] 4.3 Update `EntityAccessorGenerator` for Delete convenience methods
    - Add optional `KeyCondition keyCondition = KeyCondition.None` parameter to `DeleteAsync(pk)` for simple key
    - Add optional `KeyCondition keyCondition = KeyCondition.None` parameter to `DeleteAsync(pk, sk)` for composite key
    - Apply key condition to builder when not None
    - _Requirements: 8.1, 8.2, 8.3, 8.4_

  - [x] 4.4 Write unit tests for generated convenience methods
    - Test generated code compiles correctly
    - Test key condition parameter is applied
    - Test default value (None) doesn't add condition

- [x] 5. Verify transaction and batch compatibility
  - [x] 5.1 Verify key conditions work in transactions
    - Test builder with key condition added to `DynamoDbTransactions.Write`
    - Verify condition is preserved in transaction item
    - _Requirements: 9.1, 9.3_

  - [x] 5.2 Verify key conditions work in batch operations
    - Test builder with key condition added to `DynamoDbBatch.Write`
    - Verify condition is preserved in batch item
    - _Requirements: 9.2, 9.3_

  - [x] 5.3 Write property test for transaction/batch compatibility
    - **Property 6: Transaction/Batch Compatibility**
    - **Validates: Requirements 9.1, 9.2, 9.3**

- [ ] 6. Write additional property tests
  - [ ] 6.1 Write property test for builder method equivalence
    - **Property 4: Builder Method Equivalence**
    - **Validates: Requirements 2.4, 2.5**

  - [ ] 6.2 Write property test for default behavior preservation
    - **Property 5: Default Behavior Preservation**
    - **Validates: Requirements 3.3, 1.2**

- [ ] 7. Checkpoint - Ensure all tests pass
  - Ensure all tests pass, ask the user if questions arise.

- [x] 8. Write integration tests
  - [x] 8.1 Test Put with MustNotExist on existing item (should fail)
    - _Requirements: 6.3_

  - [x] 8.2 Test Put with MustExist on non-existing item (should fail)
    - _Requirements: 6.4_

  - [x] 8.3 Test Update with MustExist on non-existing item (should fail, prevents upsert)
    - _Requirements: 7.3_

  - [x] 8.4 Test Delete with MustExist on non-existing item (should fail)
    - _Requirements: 8.3_

  - [x] 8.5 Test transaction with key condition
    - _Requirements: 9.1_

- [x] 9. Checkpoint - Ensure integration tests pass
  - Ensure all tests pass, ask the user if questions arise.

- [x] 10. Update documentation
  - [x] 10.1 Update `.kiro/steering/fluentdynamodb.md`
    - Add key condition examples to Put Operations section
    - Add key condition examples to Update Operations section
    - Add key condition examples to Delete Operations section
    - Add new "Key Condition Shortcuts" section with table and common patterns
    - _Requirements: All_

  - [x] 10.2 Update `CHANGELOG.md`
    - Add entry for Key Condition Shortcuts feature under [Unreleased]
    - Include usage examples
    - _Requirements: All_

  - [x] 10.3 Update `docs/core-features/BasicOperations.md`
    - Add section on key condition shortcuts
    - Include examples for Put, Update, and Delete
    - _Requirements: All_

  - [x] 10.4 Update `docs/DOCUMENTATION_CHANGELOG.md`
    - Add entry for documentation synchronization
    - _Requirements: All_

- [x] 11. Final checkpoint - All tests pass and documentation complete
  - Ensure all tests pass, ask the user if questions arise.

## Notes

- All tasks are required for comprehensive implementation
- Each task references specific requirements for traceability
- Checkpoints ensure incremental validation
- Property tests validate universal correctness properties
- Integration tests validate end-to-end behavior with DynamoDB
- Source generator changes require `dotnet build-server shutdown` to take effect
