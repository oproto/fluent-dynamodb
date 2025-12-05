# Implementation Plan

- [x] 1. Verify source generator output and update test entity definitions
  - Check generated code in obj/Generated folder for test tables
  - Verify test entity classes have correct attributes
  - Update test entity attributes if needed to match current patterns
  - Ensure test table classes are properly configured for generation
  - _Requirements: 1.1, 1.2, 1.3, 1.4_

- [x] 2. Discover current API patterns and update test code
- [x] 2.1 Review current transaction builder API
  - Examine TransactWriteItemsRequestBuilder for current method names
  - Check unit tests for correct transaction usage patterns
  - Update integration tests to use current transaction API
  - Replace old AddPut/AddDelete/AddConditionCheck calls with current equivalents
  - _Requirements: 2.1, 2.2_

- [x] 2.2 Review current batch operation API
  - Examine BatchWriteItemRequestBuilder and BatchGetItemRequestBuilder
  - Check unit tests for correct batch operation patterns
  - Update integration tests to use current batch API
  - Replace old AddPut/AddGet/AddDelete calls with current equivalents
  - _Requirements: 2.3, 2.4_

- [x] 2.3 Review current table operation methods
  - Check DynamoDbTableBase for Scan, BatchGet, BatchWrite, TransactGet, TransactWrite
  - Review unit tests for correct usage of these operations
  - Update integration tests to use current method names and patterns
  - Fix any method calls that reference old/renamed methods
  - _Requirements: 2.5_

- [x] 3. Replace EncryptionContext with DynamoDbOperationContext
- [x] 3.1 Update Security test files
  - Replace EncryptionContext references in LinqEncryptionIntegrationTests.cs
  - Replace EncryptionContext references in FormatStringEncryptionIntegrationTests.cs
  - Replace EncryptionContext references in EncryptValueHelperIntegrationTests.cs
  - _Requirements: 3.1, 3.2, 3.3_

- [x] 3.2 Implement diagnostic adapter pattern
  - Review unit test examples of diagnostic adapter for xUnit
  - Implement adapter pattern in integration test base class
  - Update test setup to use diagnostic adapter
  - _Requirements: 3.1, 3.2, 3.3_

- [x] 4. Fix type inference issues by adding explicit type parameters
  - Review library API to understand which methods require explicit types
  - Add explicit type parameters to Query<TEntity>() calls
  - Add explicit type parameters to Scan<TEntity>() calls  
  - Add explicit type parameters to Get<TEntity>() calls
  - Add explicit type parameters to Put<TEntity>() calls
  - Add explicit type parameters to Delete<TEntity>() calls
  - Follow patterns from unit tests for correct usage
  - _Requirements: 4.1, 4.2, 4.3, 4.4_

- [x] 5. Fix lambda expression type mismatches
  - Update SensitiveDataRedactionIntegrationTests lambda expressions
  - Update FormatApplicationIntegrationTests lambda expressions
  - Ensure lambda expressions match expected delegate types
  - Convert string parameters to lambda where appropriate
  - _Requirements: 5.1, 5.2, 5.3_

- [x] 6. Update test infrastructure
- [x] 6.1 Update MockFieldEncryptor
  - Add EncryptCalls property to MockFieldEncryptor
  - Implement call tracking in Encrypt/Decrypt methods
  - Update tests to use EncryptCalls for verification
  - _Requirements: 6.1_

- [x] 6.2 Update FluentAssertions method calls
  - Replace BeGreaterOrEqualTo with BeGreaterThanOrEqualTo
  - Replace HaveCountGreaterOrEqualTo with BeGreaterThanOrEqualTo
  - Update any other deprecated FluentAssertions methods
  - _Requirements: 6.2_

- [x] 6.3 Verify test table properties
  - Ensure test tables expose TableName property
  - Ensure test tables expose Client property
  - Add missing properties if needed
  - _Requirements: 6.3_

- [x] 7. Fix method signature mismatches by updating test code
- [x] 7.1 Review and fix Update method usage
  - Check current Update method signature in library
  - Update test code to call Update as method with correct syntax
  - Fix any property-style references to use method call
  - _Requirements: 7.1, 7.2, 7.3, 7.4, 7.5_

- [x] 7.2 Review and fix Put/Get/Delete calls
  - Check current Put/Get/Delete method signatures in library
  - Update test code to pass correct number and types of parameters
  - Follow unit test patterns for correct usage
  - _Requirements: 7.1, 7.2, 7.3, 7.4, 7.5_

- [x] 7.3 Fix incorrect method calls on builder types
  - Identify calls to methods that don't exist on specific builders
  - Find correct method names in current library code
  - Update test code to use correct methods on correct builders
  - _Requirements: 7.1, 7.2, 7.3, 7.4, 7.5_

- [x] 8. Fix remaining generated code issues (120 errors)
- [x] 8.1 Investigate source generator for test tables
  - Check if MultiEntityTestTable and other test tables are properly configured
  - Verify entity classes have correct attributes for generation
  - Examine generated files in obj/Generated to see what's actually being generated
  - Identify why Orders, OrderLines, Payments, Item accessors are missing
  - _Requirements: 1.1, 1.2, 1.3, 1.4_

- [x] 8.2 Fix or work around missing table accessors
  - If generator should produce accessors: fix entity/table configuration
  - If generator pattern changed: update test code to use new accessor pattern
  - Update all references to table.Orders, table.OrderLines, table.Payments, table.Item
  - _Requirements: 1.1, 1.2, 1.3, 1.4_

- [-] 9. Fix method not found issues (60 errors)
- [x] 9.1 Fix PutAsync errors (34 errors)
  - Identify why QueryRequestBuilder and UpdateItemRequestBuilder have PutAsync calls
  - These are likely incorrect - "should be ExecuteAsync or different builder" -- this is wrong, ExecuteAsync was replaced with PutAsync, etc
  - Update test code to use correct method for the builder type
  - _Requirements: 2.1, 2.2, 2.3, 2.4, 2.5, 7.2_

- [x] 9.2 Fix ScanRequestBuilder.Where errors (6 errors)
  - Check if Where method exists on ScanRequestBuilder or if it's an extension
  - If method doesn't exist, use FilterExpression or alternative API
  - Update test code to use correct filtering approach for scans
  - _Requirements: 2.5, 7.3_

- [x] 9.3 Fix missing batch/transaction methods (8 errors)
  - Verify BatchGet, BatchWrite, TransactGet, TransactWrite exist on DynamoDbTableBase
  - If methods were removed, find replacement API
  - Update test code to use current batch/transaction patterns
  - _Requirements: 2.3, 2.4, 2.5_

- [x] 9.4 Fix ReturnValues errors (2 errors)
  - Check if ReturnValues method exists on DeleteItemRequestBuilder
  - Find correct method name or alternative approach
  - Update test code accordingly
  - _Requirements: 7.2, 7.3_

- [x] 10. Fix type inference issues (60 errors)
- [x] 10.1 Add explicit type parameters to Scan calls
  - Find all Scan() calls that cannot infer type
  - Add explicit type parameter: Scan<EntityType>()
  - Ensure ScanRequestBuilder has proper type argument
  - _Requirements: 4.1, 4.2, 4.4_

- [x] 11. Fix lambda and type conversion issues (72 errors)
- [x] 11.1 Fix lambda to string conversion errors (32 errors)
  - Identify methods expecting string but receiving lambda
  - Convert lambda expressions to string-based expressions
  - Or find expression-based overload if available
  - _Requirements: 5.1, 5.2, 5.3_

- [x] 11.2 Fix argument type mismatches (40 errors)
  - Fix AttributeValue to string conversions
  - Fix EntityMetadata to object conversions
  - Update method calls to pass correct types
  - _Requirements: 7.2, 7.5_

- [x] 12. Fix method signature issues (48 errors)
- [x] 12.1 Fix missing parameter errors (26 errors)
  - Identify methods with CS7036 errors (missing required parameters)
  - Add missing parameters or use correct overload
  - Common issue: Remove() method signature changed
  - _Requirements: 7.2, 7.5_

- [x] 12.2 Fix no matching overload errors (16 errors)
  - Find methods with CS1501 errors (no overload matches)
  - Check current method signatures in library
  - Update test code to match current signatures
  - _Requirements: 7.2, 7.5_

- [x] 12.3 Fix method vs property errors (6 errors)
  - Fix CS0119 errors where method is used as property
  - Add parentheses for method calls
  - _Requirements: 7.1_

- [x] 13. Fix type resolution issues (26 errors)
- [x] 13.1 Find or create missing types
  - Locate ProductEntity and other missing types
  - If types were renamed, update references
  - If types were removed, remove or replace test code
  - _Requirements: 1.1, 1.2_

- [x] 14. Fix FluentAssertions and operator issues (6 errors)
- [x] 14.1 Fix string comparison assertions (4 errors)
  - We are using AwesomeAssertions not FluentAssertions, it is a fork and there is a namespace change
  - Replace BeGreaterThanOrEqualTo/BeLessThanOrEqualTo for strings
  - Use string-specific comparison methods
  - _Requirements: 6.2_

- [x] 14.2 Fix string operator comparisons (2 errors)
  - We are using AwesomeAssertions not FluentAssertions, it is a fork and there is a namespace change
  - Replace >= operator on strings with CompareTo or other method
  - Update expression-based queries to use valid operators
  - _Requirements: 5.1, 5.2_

- [x] 15. Fix miscellaneous property issues (12 errors)
- [x] 15.1 Fix missing test table properties
  - Add NoDeletes, MixedEntities, InternalOps, InternalEntities properties
  - Or update test code to not reference these properties
  - _Requirements: 6.3_

- [ ] 16. Verify build success and run tests
- [ ] 16.1 Verify compilation
  - Run dotnet build on integration test project
  - Confirm zero compilation errors
  - Document any remaining warnings
  - _Requirements: 8.1, 8.2, 8.3, 8.4_

- [ ] 16.2 Run unit tests for regression check
  - Execute unit tests to ensure no breakage
  - Fix any unit test failures
  - _Requirements: 8.1, 8.2, 8.3, 8.4_

- [ ] 16.3 Run integration tests
  - Execute integration tests after successful build
  - Document any test failures for separate investigation
  - _Requirements: 8.1, 8.2, 8.3, 8.4_
