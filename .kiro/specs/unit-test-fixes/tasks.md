# Implementation Plan

- [x] 1. Create migration tracking and analysis infrastructure
  - Create a script or document to track migration status for each test file
  - Analyze all test files to count total tests and identify string-based assertions
  - Document the baseline: number of tests per file, types of assertions used
  - _Requirements: 1.1, 1.2, 1.3_

- [x] 2. Migrate MapperGeneratorTests.cs (High Priority)
- [x] 2.1 Add compilation verification to all tests in MapperGeneratorTests.cs
  - Add `CompilationVerifier.AssertGeneratedCodeCompiles(code, source)` after diagnostic checks
  - Ensure all tests compile successfully
  - _Requirements: 2.1, 2.2, 2.4_

- [x] 2.2 Replace structural assertions with semantic assertions in MapperGeneratorTests.cs
  - Replace method existence checks with `.ShouldContainMethod()`
  - Replace assignment checks with `.ShouldContainAssignment()`
  - Replace LINQ checks with `.ShouldUseLinqMethod()`
  - Replace type reference checks with `.ShouldReferenceType()`
  - _Requirements: 3.1, 3.2, 3.3, 3.4_

- [x] 2.3 Preserve DynamoDB-specific checks in MapperGeneratorTests.cs
  - Keep string checks for DynamoDB attribute types (S, N, SS, NS, L, M)
  - Keep string checks for null handling patterns
  - Add descriptive "because" messages to all retained string checks
  - _Requirements: 4.1, 4.2, 4.3, 4.4_

- [x] 2.4 Validate MapperGeneratorTests.cs migration
  - Run all tests to ensure they pass
  - Intentionally modify generated code formatting to verify tests still pass
  - Intentionally break generated code to verify tests catch errors
  - _Requirements: 8.1, 8.2, 8.4_

- [x] 2.5 Document MapperGeneratorTests.cs migration
  - Add file header comment indicating migration is complete
  - Update migration tracking document
  - _Requirements: 5.1, 5.4_

- [x] 3. Migrate ComplexTypeGenerationTests.cs (High Priority)
- [x] 3.1 Add compilation verification to all tests in ComplexTypeGenerationTests.cs
  - Add `CompilationVerifier.AssertGeneratedCodeCompiles(code, source)` after diagnostic checks
  - Handle tests with multiple source files by passing additional sources
  - _Requirements: 2.1, 2.2, 2.4_

- [x] 3.2 Replace structural assertions with semantic assertions in ComplexTypeGenerationTests.cs
  - Replace method existence checks with semantic assertions
  - Replace assignment checks with semantic assertions
  - Replace LINQ checks with semantic assertions
  - Replace type reference checks with semantic assertions
  - _Requirements: 3.1, 3.2, 3.3, 3.4_

- [x] 3.3 Preserve DynamoDB-specific checks in ComplexTypeGenerationTests.cs
  - Keep string checks for collection type conversions (List, HashSet, Dictionary)
  - Keep string checks for DynamoDB attribute types
  - Keep string checks for null and empty collection handling
  - Add descriptive "because" messages
  - _Requirements: 4.1, 4.2, 4.3, 4.4_

- [x] 3.4 Validate ComplexTypeGenerationTests.cs migration
  - Run all tests to ensure they pass
  - Test with formatting changes
  - Test with intentional errors
  - _Requirements: 8.1, 8.2, 8.4_

- [x] 3.5 Document ComplexTypeGenerationTests.cs migration
  - Add file header comment
  - Update migration tracking
  - _Requirements: 5.1, 5.4_

- [x] 4. Migrate KeysGeneratorTests.cs (High Priority)
- [x] 4.1 Add compilation verification to all tests in KeysGeneratorTests.cs
  - Add compilation verification after diagnostic checks
  - _Requirements: 2.1, 2.4_

- [x] 4.2 Replace structural assertions with semantic assertions in KeysGeneratorTests.cs
  - Replace method existence checks
  - Replace assignment checks for key generation
  - Replace string concatenation checks where appropriate
  - _Requirements: 3.1, 3.2, 3.5_

- [x] 4.3 Preserve key format checks in KeysGeneratorTests.cs
  - Keep string checks for partition key format strings
  - Keep string checks for sort key format strings
  - Add "because" messages explaining format requirements
  - _Requirements: 4.2, 4.4_

- [x] 4.4 Validate KeysGeneratorTests.cs migration
  - Run tests and verify they pass
  - Test with formatting changes
  - Test with intentional errors
  - _Requirements: 8.1, 8.2, 8.4_

- [x] 4.5 Document KeysGeneratorTests.cs migration
  - Add file header comment
  - Update migration tracking
  - _Requirements: 5.1, 5.4_

- [x] 5. Migrate FieldsGeneratorTests.cs (Medium Priority)
- [x] 5.1 Add compilation verification to all tests in FieldsGeneratorTests.cs
  - Add compilation verification after diagnostic checks
  - _Requirements: 2.1, 2.4_

- [x] 5.2 Replace structural assertions with semantic assertions in FieldsGeneratorTests.cs
  - Replace constant field checks with semantic assertions
  - Replace class existence checks
  - _Requirements: 3.1, 3.4_

- [x] 5.3 Preserve field name and value checks in FieldsGeneratorTests.cs
  - Keep string checks for field constant values
  - Keep string checks for attribute name mappings
  - Add "because" messages
  - _Requirements: 4.4, 4.5_

- [x] 5.4 Validate FieldsGeneratorTests.cs migration
  - Run tests and verify they pass
  - Test with formatting changes
  - _Requirements: 8.1, 8.2_

- [x] 5.5 Document FieldsGeneratorTests.cs migration
  - Add file header comment
  - Update migration tracking
  - _Requirements: 5.1, 5.4_

- [x] 6. Migrate DynamoDbSourceGeneratorTests.cs (Medium Priority)
- [x] 6.1 Add compilation verification to all tests in DynamoDbSourceGeneratorTests.cs
  - Add compilation verification for end-to-end generator tests
  - _Requirements: 2.1, 2.4_

- [x] 6.2 Replace structural assertions with semantic assertions in DynamoDbSourceGeneratorTests.cs
  - Replace class and namespace checks
  - Replace method existence checks
  - Replace field constant checks
  - _Requirements: 3.1, 3.2, 3.4_

- [x] 6.3 Validate DynamoDbSourceGeneratorTests.cs migration
  - Run tests and verify they pass
  - Test with formatting changes
  - _Requirements: 8.1, 8.2_

- [x] 6.4 Document DynamoDbSourceGeneratorTests.cs migration
  - Add file header comment
  - Update migration tracking
  - _Requirements: 5.1, 5.4_

- [x] 7. Migrate MapperGeneratorBugFixTests.cs (Medium Priority)
- [x] 7.1 Add compilation verification to all tests in MapperGeneratorBugFixTests.cs
  - Add compilation verification for bug fix tests
  - _Requirements: 2.1, 2.4_

- [x] 7.2 Replace structural assertions with semantic assertions in MapperGeneratorBugFixTests.cs
  - Replace type reference checks
  - Keep specific bug-related string checks that verify the fix
  - _Requirements: 3.4, 3.5_

- [x] 7.3 Validate MapperGeneratorBugFixTests.cs migration
  - Run tests and verify they pass
  - Verify bug fix tests still catch the original bug if reintroduced
  - _Requirements: 8.1, 8.2_

- [x] 7.4 Document MapperGeneratorBugFixTests.cs migration
  - Add file header comment
  - Update migration tracking
  - _Requirements: 5.1, 5.4_

- [x] 8. Review and selectively migrate low-priority tests
- [x] 8.1 Review EntityAnalyzerTests.cs
  - Analyze if migration is needed (mostly diagnostic tests)
  - Add compilation verification if beneficial
  - Document decision to migrate or skip
  - _Requirements: 1.5, 7.4_

- [x] 8.2 Review EdgeCaseTests.cs
  - Analyze if migration is needed
  - Add compilation verification if beneficial
  - Document decision to migrate or skip
  - _Requirements: 1.5, 7.4_

- [x] 8.3 Review Model tests (EntityModelTests.cs, PropertyModelTests.cs, RelationshipModelTests.cs)
  - Analyze if migration is needed (simple structure tests)
  - Document decision to migrate or skip
  - _Requirements: 1.5, 7.4_

- [x] 9. Final validation and documentation
- [x] 9.1 Run full test suite
  - Execute all unit tests to ensure no regressions
  - Verify all migrated tests pass
  - Check test execution time hasn't significantly increased
  - _Requirements: 6.3, 8.4_

- [x] 9.2 Create migration summary report
  - Document total tests migrated
  - List files migrated and files skipped with reasons
  - Document any issues encountered and resolutions
  - Provide before/after comparison of test brittleness
  - _Requirements: 5.2, 5.3, 5.4_

- [x] 9.3 Update related documentation
  - Ensure MIGRATION_GUIDE.md is up to date
  - Update any references to test patterns in other documentation
  - Add examples of migrated tests to documentation
  - _Requirements: 5.4_

- [x] 9.4 Clean up backup files
  - Remove or archive any .bak files (e.g., ComplexTypeGenerationTests.cs.bak)
  - Ensure no temporary migration files remain
  - _Requirements: 5.4_
