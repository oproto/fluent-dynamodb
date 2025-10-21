# Implementation Plan

- [x] 1. Create integration test project structure
  - Create Oproto.FluentDynamoDb.IntegrationTests project targeting .NET 8
  - Add references to AWSSDK.DynamoDBv2, xUnit
  - Add FluentAssertions version 7.x (NOT 8.0+) to avoid Apache 2.0 licensing issues
  - Add project reference to Oproto.FluentDynamoDb
  - Add project reference to Oproto.FluentDynamoDb.SourceGenerator (for compile-time generation)
  - Create folder structure: Infrastructure/, AdvancedTypes/, BasicTypes/, RealWorld/, TestEntities/
  - _Requirements: 2.1, 2.2, 2.3, 2.6_

- [x] 2. Implement DynamoDB Local fixture
  - [x] 2.1 Create DynamoDbLocalFixture class implementing IAsyncLifetime
    - Implement InitializeAsync to start DynamoDB Local
    - Implement DisposeAsync to stop DynamoDB Local
    - Add logic to check if DynamoDB Local is already running
    - Add logic to download DynamoDB Local if not present
    - Create IAmazonDynamoDB client with local endpoint
    - _Requirements: 1.1, 1.2, 1.3_
  
  - [x] 2.2 Add DynamoDB Local process management
    - Find Java executable on system
    - Start DynamoDB Local with in-memory mode
    - Capture stdout/stderr for debugging
    - Wait for service to be ready (retry ListTables)
    - Handle process cleanup on disposal
    - _Requirements: 1.1, 1.2, 1.3, 1.5_
  
  - [x] 2.3 Create xUnit collection fixture
    - Define DynamoDbLocalCollection with ICollectionFixture
    - Ensure fixture is shared across test classes
    - _Requirements: 1.4_

- [x] 3. Create integration test base class
  - [x] 3.1 Implement IntegrationTestBase class
    - Accept DynamoDbLocalFixture in constructor
    - Generate unique table name per test class
    - Implement IAsyncLifetime for setup/cleanup
    - Track tables created during tests
    - _Requirements: 2.2, 12.1, 12.3_
  
  - [x] 3.2 Add table management methods
    - Implement CreateTableAsync<TEntity>() using entity metadata
    - Add WaitForTableActive helper
    - Implement cleanup in DisposeAsync
    - Handle ResourceNotFoundException gracefully
    - _Requirements: 2.2, 12.1, 12.2_
  
  - [x] 3.3 Add SaveAndLoadAsync helper method
    - Convert entity to DynamoDB item using ToDynamoDb
    - Save item with PutItemAsync
    - Load item back with GetItemAsync
    - Convert item to entity using FromDynamoDb
    - _Requirements: 3.1, 3.2, 3.3_

- [ ] 4. Write integration tests for HashSet types
  - [ ] 4.1 Create HashSetIntegrationTests class
    - Inherit from IntegrationTestBase
    - Add Collection attribute for DynamoDB Local
    - Create test table in InitializeAsync
    - _Requirements: 2.1, 3.1_
  
  - [ ] 4.2 Test HashSet<int> round-trip
    - Create entity with HashSet<int> values
    - Save and load entity
    - Verify all values preserved
    - _Requirements: 3.1_
  
  - [ ] 4.3 Test HashSet<string> round-trip
    - Create entity with HashSet<string> values
    - Save and load entity
    - Verify all values preserved
    - _Requirements: 3.1_
  
  - [ ] 4.4 Test HashSet<byte[]> round-trip
    - Create entity with HashSet<byte[]> values
    - Save and load entity
    - Verify all values preserved
    - _Requirements: 3.1_
  
  - [ ] 4.5 Test HashSet null handling
    - Create entity with null HashSet property
    - Save and load entity
    - Verify null is preserved
    - _Requirements: 3.4_
  
  - [ ] 4.6 Test HashSet empty collection handling
    - Create entity with empty HashSet
    - Verify empty set is omitted from DynamoDB item
    - _Requirements: 3.5_


- [ ] 5. Write integration tests for List types
  - [ ] 5.1 Create ListIntegrationTests class
    - Inherit from IntegrationTestBase
    - Add Collection attribute
    - Create test table in InitializeAsync
    - _Requirements: 2.1, 3.2_
  
  - [ ] 5.2 Test List<string> round-trip
    - Create entity with List<string> values
    - Save and load entity
    - Verify all values and order preserved
    - _Requirements: 3.2_
  
  - [ ] 5.3 Test List<int> round-trip
    - Create entity with List<int> values
    - Save and load entity
    - Verify all values and order preserved
    - _Requirements: 3.2_
  
  - [ ] 5.4 Test List<decimal> round-trip
    - Create entity with List<decimal> values
    - Save and load entity
    - Verify all values and order preserved
    - _Requirements: 3.2_
  
  - [ ] 5.5 Test List null and empty handling
    - Test null List property
    - Test empty List property
    - Verify correct behavior
    - _Requirements: 3.4, 3.5_

- [ ] 6. Write integration tests for Dictionary types
  - [ ] 6.1 Create DictionaryIntegrationTests class
    - Inherit from IntegrationTestBase
    - Add Collection attribute
    - Create test table in InitializeAsync
    - _Requirements: 2.1, 3.3_
  
  - [ ] 6.2 Test Dictionary<string, string> round-trip
    - Create entity with Dictionary values
    - Save and load entity
    - Verify all key-value pairs preserved
    - _Requirements: 3.3_
  
  - [ ] 6.3 Test Dictionary null and empty handling
    - Test null Dictionary property
    - Test empty Dictionary property
    - Verify correct behavior
    - _Requirements: 3.4, 3.5_

- [ ] 7. Create test entity builders
  - [ ] 7.1 Create AdvancedTypesEntityBuilder class
    - Add fluent methods for each property
    - Implement Build() method
    - Provide sensible defaults
    - _Requirements: 9.1, 9.2_
  
  - [ ] 7.2 Create BasicEntityBuilder class
    - Add fluent methods for basic properties
    - Implement Build() method
    - _Requirements: 9.1_

- [ ] 8. Create compilation verifier utility
  - [ ] 8.1 Create CompilationVerifier class in unit test project
    - Implement AssertGeneratedCodeCompiles method
    - Create CSharpCompilation from source code
    - Add necessary assembly references
    - Check for compilation errors
    - _Requirements: 4.1, 4.3_
  
  - [ ] 8.2 Add detailed error reporting
    - Format compilation errors with line numbers
    - Include generated source in error message
    - Provide clear error messages
    - _Requirements: 4.2, 4.4_
  
  - [ ] 8.3 Add support for multiple source files
    - Accept additional source files parameter
    - Handle external type references
    - _Requirements: 4.5_

- [ ] 9. Add compilation verification to existing generator tests
  - [ ] 9.1 Update AdvancedTypeGenerationTests
    - Add CompilationVerifier.AssertGeneratedCodeCompiles to each test
    - Verify no compilation errors
    - _Requirements: 4.1, 4.3_
  
  - [ ] 9.2 Update MapperGeneratorTests
    - Add compilation verification to each test
    - _Requirements: 4.1_
  
  - [ ] 9.3 Update FieldsGeneratorTests
    - Add compilation verification to each test
    - _Requirements: 4.1_
  
  - [ ] 9.4 Update KeysGeneratorTests
    - Add compilation verification to each test
    - _Requirements: 4.1_

- [ ] 10. Create semantic assertion utilities
  - [ ] 10.1 Create SemanticAssertions class
    - Implement ShouldContainMethod extension
    - Implement ShouldContainAssignment extension
    - Implement ShouldUseLinqMethod extension
    - Implement ShouldReferenceType extension
    - _Requirements: 5.1, 5.2, 5.3, 5.4_
  
  - [ ] 10.2 Add helpful error messages
    - Include available methods when method not found
    - Show context when assertion fails
    - _Requirements: 5.5_

- [ ] 11. Write complex scenario integration tests
  - [ ] 11.1 Create ComplexEntityTests class
    - Test entity with multiple advanced types
    - Test round-trip with all properties
    - _Requirements: 13.1_
  
  - [ ] 11.2 Test query operations with advanced types
    - Create QueryOperationsTests class
    - Test queries that filter on advanced type properties
    - _Requirements: 13.2_
  
  - [ ] 11.3 Test update operations with collections
    - Test updating HashSet properties
    - Test updating List properties
    - Test updating Dictionary properties
    - _Requirements: 13.3_

- [ ] 12. Create setup scripts
  - [ ] 12.1 Create setup-dynamodb-local.sh script
    - Download DynamoDB Local if not present
    - Extract to appropriate directory
    - Verify Java installation
    - _Requirements: 6.1_
  
  - [ ] 12.2 Create run-integration-tests.sh script
    - Start DynamoDB Local if not running
    - Run integration tests
    - Stop DynamoDB Local on completion
    - _Requirements: 2.5_

- [ ] 13. Set up CI/CD integration
  - [ ] 13.1 Create GitHub Actions workflow for integration tests
    - Set up Java for DynamoDB Local
    - Download and start DynamoDB Local
    - Run integration tests
    - Upload test results
    - _Requirements: 6.1, 6.2, 6.3_
  
  - [ ] 13.2 Add platform-specific handling
    - Handle Linux, macOS, Windows differences
    - Use appropriate DynamoDB Local binary
    - _Requirements: 6.5_
  
  - [ ] 13.3 Add test result reporting
    - Separate unit and integration test results
    - Include DynamoDB Local logs on failure
    - _Requirements: 6.3, 6.4_

- [ ] 14. Create documentation
  - [ ] 14.1 Write integration test README
    - Document prerequisites
    - Explain setup process
    - Show how to run tests locally
    - Add troubleshooting section
    - _Requirements: 10.1, 10.4, 10.5_
  
  - [ ] 14.2 Write migration guide
    - Provide examples of adding compilation verification
    - Show how to replace string checks with semantic assertions
    - Explain when to use each test type
    - _Requirements: 7.1, 7.2, 7.3, 10.2, 10.3_
  
  - [ ] 14.3 Create test writing guide
    - Provide templates for common test scenarios
    - Document test data builders
    - Explain test organization
    - _Requirements: 10.1, 10.2_

- [ ] 15. Add test utilities and helpers
  - [ ] 15.1 Create assertion helpers for AttributeValue
    - Add methods to compare AttributeValue dictionaries
    - Provide deep equality comparison
    - _Requirements: 9.3_
  
  - [ ] 15.2 Create debugging utilities
    - Add method to dump entity state
    - Add method to dump DynamoDB item
    - _Requirements: 9.5_
  
  - [ ] 15.3 Create random data generators
    - Generate random strings, numbers, collections
    - Provide consistent seed for reproducibility
    - _Requirements: 9.2_

- [ ] 16. Implement test isolation and cleanup
  - [ ] 16.1 Ensure unique table names per test
    - Use GUID in table name
    - Include test class name for debugging
    - _Requirements: 8.2, 12.3_
  
  - [ ] 16.2 Implement robust cleanup
    - Delete tables in DisposeAsync
    - Handle cleanup failures gracefully
    - Log cleanup issues without failing tests
    - _Requirements: 12.1, 12.2, 12.4_
  
  - [ ] 16.3 Add cleanup verification
    - Provide method to check if resources cleaned up
    - _Requirements: 12.4, 12.5_

- [ ] 17. Add test execution modes and filtering
  - [ ] 17.1 Add xUnit traits for test categories
    - Add Category trait to unit tests
    - Add Category trait to integration tests
    - _Requirements: 14.1, 14.2, 14.4_
  
  - [ ] 17.2 Document test filtering
    - Show how to run specific test categories
    - Explain filter syntax
    - _Requirements: 14.1, 14.2, 14.3_

- [ ] 18. Implement performance optimizations
  - [ ] 18.1 Add DynamoDB Local instance reuse
    - Use collection fixture to share instance
    - Measure startup time savings
    - _Requirements: 8.1_
  
  - [ ] 18.2 Enable parallel test execution
    - Ensure tests use unique table names
    - Verify no shared state between tests
    - _Requirements: 8.3_
  
  - [ ] 18.3 Optimize test execution time
    - Measure full suite execution time
    - Target < 30 seconds for integration tests
    - _Requirements: 8.5_

- [ ] 19. Add test metrics and reporting
  - [ ] 19.1 Add test execution metrics
    - Report unit vs integration test counts
    - Report execution time by category
    - _Requirements: 15.1, 15.2_
  
  - [ ] 19.2 Add code coverage reporting
    - Configure coverage for generated code
    - Report coverage metrics
    - _Requirements: 15.3_
  
  - [ ] 19.3 Add failure categorization
    - Categorize failures by type
    - Report in CI/CD dashboard format
    - _Requirements: 15.4, 15.5_

- [ ] 20. Validate and verify implementation
  - [ ] 20.1 Run full test suite locally
    - Verify all unit tests pass
    - Verify all integration tests pass
    - Check execution time
    - _Requirements: All_
  
  - [ ] 20.2 Run tests in CI/CD
    - Verify GitHub Actions workflow works
    - Check test result reporting
    - Verify DynamoDB Local starts correctly
    - _Requirements: 6.1, 6.2, 6.3_
  
  - [ ] 20.3 Verify backward compatibility
    - Ensure existing tests still pass
    - Verify no breaking changes
    - _Requirements: 11.1, 11.2, 11.3_
