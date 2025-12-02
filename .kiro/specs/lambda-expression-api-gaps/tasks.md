# Implementation Plan

- [x] 1. Add lambda Where() extension method for PutItemRequestBuilder
  - [x] 1.1 Add `Where<TEntity>(this PutItemRequestBuilder<TEntity>, Expression<Func<TEntity, bool>>)` extension method to WithConditionExpressionExtensions.cs
    - Use ExpressionValidationMode.None since condition expressions can reference any property
    - Follow the same pattern as the existing ConditionCheckBuilder Where() method
    - _Requirements: 2.1, 2.2, 2.3, 2.4, 4.1_
  - [x] 1.2 Write property test for AttributeExists expression generation on Put
    - **Property 1: AttributeExists generates correct expression**
    - **Validates: Requirements 2.3**
  - [x] 1.3 Write property test for AttributeNotExists expression generation on Put
    - **Property 2: AttributeNotExists generates correct expression**
    - **Validates: Requirements 2.4**

- [x] 2. Add lambda Where() extension method for DeleteItemRequestBuilder
  - [x] 2.1 Add `Where<TEntity>(this DeleteItemRequestBuilder<TEntity>, Expression<Func<TEntity, bool>>)` extension method to WithConditionExpressionExtensions.cs
    - Use ExpressionValidationMode.None since condition expressions can reference any property
    - Follow the same pattern as the existing ConditionCheckBuilder Where() method
    - _Requirements: 3.1, 3.2, 3.3, 4.2_
  - [x] 2.2 Write property test for comparison operators in Delete Where()
    - **Property 3: Comparison operators generate correct expressions**
    - **Validates: Requirements 3.3**

- [x] 3. Checkpoint - Verify extension methods compile
  - Ensure all tests pass, ask the user if questions arise.

- [x] 4. Add Scan() method to entity accessors in source generator
  - [x] 4.1 Add `GenerateAccessorScanMethods` method to TableGenerator.cs
    - Generate parameterless Scan() method returning ScanRequestBuilder<TEntity>
    - Generate Scan(string, params object[]) method with filter expression
    - Generate Scan(Expression<Func<TEntity, bool>>) method with lambda filter
    - _Requirements: 1.1, 1.2, 4.3_
  - [x] 4.2 Add case for TableOperation.Scan in GenerateAccessorOperationMethods switch statement
    - Call GenerateAccessorScanMethods when entity.IsScannable is true
    - _Requirements: 1.1_

- [-] 5. Enforce Scan opt-in pattern by removing base class Scan method
  - [x] 5.1 Remove generic `Scan<TEntity>()` method from DynamoDbTableBase
    - Scan operations should only be available when entity has `[Scannable]` attribute
    - This enforces the opt-in pattern since scanning is not a recommended access pattern
    - _Requirements: 1.1, 4.3_
  - [x] 5.2 Add `[Scannable]` attribute to entities that need Scan support
    - Add to Order entity in OperationSamples
    - Add to Account entity in TransactionDemo
    - Add to other entities as needed based on build errors
    - _Requirements: 1.1_
  - [x] 5.3 Update code using `table.Scan<TEntity>()` to use entity accessor `table.Entitys.Scan()`
    - Update ScanSamples.cs to use `table.Scan()` (non-generic for default entity)
    - Update TransactionDemoTable.cs to use `Accounts.Scan()` or `Scan()` 
    - Update other files as needed based on build errors
    - _Requirements: 1.1, 1.2_

- [x] 6. Fix failing tests and examples after Scan opt-in enforcement
  - [x] 6.1 Add `[Scannable]` to test entities that use Scan operations
    - Update entities in UnitTests, IntegrationTests, and examples
    - _Requirements: 1.1_

- [x] 7. Checkpoint - Verify all code compiles after Scan opt-in changes
  - Ensure all tests pass, ask the user if questions arise.

- [x] 8. Verify sample code compiles
  - [x] 8.1 Update TransactionWriteSamples.cs FluentLambdaTransactionWriteAsync to use the new lambda Where() methods
    - Replace format string Where() with lambda Where() using AttributeExists/AttributeNotExists
    - Verify table.Orders.Scan() compiles
    - _Requirements: 1.1, 1.2, 2.1, 2.2, 3.1, 3.2_

- [x] 9. Write property test for generic vs entity accessor equivalence
  - [x] 9.1 Write property test verifying Put generic and entity accessor produce identical results
    - **Property 4: Generic and entity accessor methods produce identical results**
    - **Validates: Requirements 5.1, 5.2**

- [x] 10. Update documentation for Scan opt-in pattern
  - [x] 10.1 Add entry to docs/DOCUMENTATION_CHANGELOG.md documenting the Scan opt-in change
    - Document that `table.Scan<TEntity>()` is no longer available on base class
    - Document that `[Scannable]` attribute is required for Scan operations
    - Document the new pattern: `table.Entitys.Scan()` or `table.Scan()` for default entity, table.Scan<TEntity>() for generic
    - _Requirements: 1.1_
  - [x] 10.2 Update CHANGELOG.md with the breaking change
    - Add entry under "Changed" or "Breaking Changes" section
    - _Requirements: 1.1_

- [x] 11. Final Checkpoint - Verify all tests pass
  - Ensure all tests pass, ask the user if questions arise.
