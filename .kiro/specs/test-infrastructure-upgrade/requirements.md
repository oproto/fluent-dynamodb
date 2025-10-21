# Requirements Document

## Introduction

This feature upgrades the testing infrastructure for the Oproto.FluentDynamoDb source generator to use more maintainable and reliable testing approaches. The current tests rely heavily on brittle string matching against generated code, which causes high maintenance overhead and doesn't verify that the generated code actually works at runtime. This upgrade will introduce integration tests with DynamoDB Local, add compilation verification, and provide a migration path for existing tests.

The enhancement maintains backward compatibility with existing tests while adding new test layers that provide higher confidence and lower maintenance burden. The focus is on pragmatic, incremental improvements rather than a disruptive big-bang rewrite.

## Glossary

- **DynamoDB Local**: Amazon's downloadable version of DynamoDB for local development and testing
- **LocalStack**: A fully functional local AWS cloud stack for testing
- **Integration Test**: A test that verifies multiple components working together, including actual DynamoDB operations
- **Compilation Test**: A test that verifies generated code compiles without errors
- **String Matching Test**: Current test approach that checks for exact strings in generated code
- **Semantic Test**: A test that verifies code structure using syntax tree analysis rather than string matching
- **Round-trip Test**: A test that saves data to DynamoDB and loads it back to verify correctness
- **Test Fixture**: Shared test infrastructure that manages lifecycle of test dependencies
- **xUnit Collection Fixture**: xUnit pattern for sharing context across multiple test classes
- **Source Generator**: Compile-time code generation tool that analyzes attributes and generates mapping code

## Requirements

### Requirement 1: DynamoDB Local Integration Test Infrastructure

**User Story:** As a developer, I want integration tests that run against DynamoDB Local so that I can verify the generated code actually works with a real database.

#### Acceptance Criteria

1. WHEN I run integration tests, THE Test_Infrastructure SHALL start DynamoDB Local automatically before tests execute
2. WHEN DynamoDB Local starts, THE Test_Infrastructure SHALL wait for the service to be ready before running tests
3. WHEN tests complete, THE Test_Infrastructure SHALL stop DynamoDB Local and clean up resources
4. WHEN multiple test classes need DynamoDB Local, THE Test_Infrastructure SHALL share a single instance across all tests
5. WHEN a test fails, THE Test_Infrastructure SHALL provide clear error messages including DynamoDB Local logs

### Requirement 2: Integration Test Project Structure

**User Story:** As a developer, I want a well-organized integration test project so that tests are easy to find and maintain.

#### Acceptance Criteria

1. WHEN I create integration tests, THE Project_Structure SHALL organize tests by feature area (AdvancedTypes, BasicTypes, RealWorld)
2. WHEN I add new integration tests, THE Project_Structure SHALL provide base classes with common setup and utilities
3. WHEN I run integration tests, THE Test_Project SHALL reference the source generator and generate code at compile time
4. WHEN I need test entities, THE Test_Project SHALL provide reusable entity definitions with various type combinations
5. WHEN I run tests locally, THE Test_Project SHALL use the same DynamoDB Local configuration as CI/CD
6. WHEN I add test dependencies, THE Test_Project SHALL use FluentAssertions version 7.x or earlier to avoid Apache 2.0 licensing issues

### Requirement 3: Round-trip Integration Tests for Advanced Types

**User Story:** As a developer, I want integration tests that verify advanced types work correctly so that I know HashSet, List, and Dictionary types persist and load correctly.

#### Acceptance Criteria

1. WHEN I save an entity with HashSet properties, THE Integration_Test SHALL verify the data round-trips correctly through DynamoDB
2. WHEN I save an entity with List properties, THE Integration_Test SHALL verify element order is preserved
3. WHEN I save an entity with Dictionary properties, THE Integration_Test SHALL verify all key-value pairs are preserved
4. WHEN I save an entity with null collection properties, THE Integration_Test SHALL verify nulls are handled correctly
5. WHEN I save an entity with empty collections, THE Integration_Test SHALL verify empty collections are omitted from DynamoDB

### Requirement 4: Compilation Verification for Generator Tests

**User Story:** As a developer, I want existing generator tests to verify generated code compiles so that I catch compilation errors early.

#### Acceptance Criteria

1. WHEN a generator test runs, THE Test_Infrastructure SHALL compile the generated code and verify no compilation errors occur
2. WHEN generated code has compilation errors, THE Test_Infrastructure SHALL report the specific errors with line numbers
3. WHEN I add a new generator test, THE Test_Infrastructure SHALL provide a helper method to add compilation verification
4. WHEN compilation verification fails, THE Test_Infrastructure SHALL include the full generated source in the error message
5. WHEN generated code references external types, THE Test_Infrastructure SHALL include necessary assembly references

### Requirement 5: Semantic Code Verification Utilities

**User Story:** As a developer, I want utilities to verify code structure semantically so that tests are less brittle than string matching.

#### Acceptance Criteria

1. WHEN I need to verify a method exists, THE Test_Utilities SHALL provide syntax tree helpers to find methods by name
2. WHEN I need to verify an assignment occurs, THE Test_Utilities SHALL provide helpers to find assignments by target
3. WHEN I need to verify a type is used, THE Test_Utilities SHALL provide helpers to find type references
4. WHEN I need to verify LINQ usage, THE Test_Utilities SHALL provide helpers to find Select/Where/ToList calls
5. WHEN semantic verification fails, THE Test_Utilities SHALL provide clear error messages about what was expected vs found

### Requirement 6: CI/CD Integration for DynamoDB Local

**User Story:** As a developer, I want integration tests to run in CI/CD so that pull requests are validated against real DynamoDB behavior.

#### Acceptance Criteria

1. WHEN CI/CD runs, THE Build_Pipeline SHALL download and start DynamoDB Local before running integration tests
2. WHEN DynamoDB Local is not available, THE Build_Pipeline SHALL fail with a clear error message
3. WHEN integration tests complete, THE Build_Pipeline SHALL report test results separately from unit tests
4. WHEN integration tests fail, THE Build_Pipeline SHALL include DynamoDB Local logs in the failure output
5. WHEN running on different platforms (Linux, macOS, Windows), THE Build_Pipeline SHALL use the appropriate DynamoDB Local binary

### Requirement 7: Migration Path for Existing Tests

**User Story:** As a developer, I want a clear migration path for existing tests so that I can gradually improve test quality without a big-bang rewrite.

#### Acceptance Criteria

1. WHEN I update an existing test, THE Migration_Guide SHALL provide examples of how to add compilation verification
2. WHEN I update an existing test, THE Migration_Guide SHALL provide examples of replacing string checks with semantic checks
3. WHEN I keep existing string checks, THE Test_Infrastructure SHALL continue to support them during the migration period
4. WHEN I prioritize which tests to migrate, THE Migration_Guide SHALL identify high-value tests to migrate first
5. WHEN I complete migration of a test file, THE Migration_Guide SHALL provide a checklist to verify the migration is complete

### Requirement 8: Performance and Reliability

**User Story:** As a developer, I want integration tests to run quickly and reliably so that they don't slow down development.

#### Acceptance Criteria

1. WHEN integration tests run, THE Test_Infrastructure SHALL reuse DynamoDB Local instance across test classes to minimize startup time
2. WHEN a test creates tables, THE Test_Infrastructure SHALL use unique table names to avoid conflicts between parallel tests
3. WHEN tests run in parallel, THE Test_Infrastructure SHALL ensure each test has isolated data
4. WHEN DynamoDB Local crashes, THE Test_Infrastructure SHALL detect the failure and restart it for subsequent tests
5. WHEN integration tests complete, THE Test_Suite SHALL run in under 30 seconds for the full suite

### Requirement 9: Test Data Builders and Utilities

**User Story:** As a developer, I want test data builders so that creating test entities is easy and consistent.

#### Acceptance Criteria

1. WHEN I need a test entity, THE Test_Utilities SHALL provide builder methods for common entity configurations
2. WHEN I need random test data, THE Test_Utilities SHALL provide generators for strings, numbers, and collections
3. WHEN I need to verify DynamoDB items, THE Test_Utilities SHALL provide assertion helpers for AttributeValue dictionaries
4. WHEN I need to compare entities, THE Test_Utilities SHALL provide deep equality comparison that handles collections correctly
5. WHEN I need to debug test failures, THE Test_Utilities SHALL provide methods to dump entity and DynamoDB item state

### Requirement 10: Documentation and Examples

**User Story:** As a developer, I want clear documentation and examples so that I know how to write and maintain tests.

#### Acceptance Criteria

1. WHEN I write a new integration test, THE Documentation SHALL provide examples for common scenarios
2. WHEN I add compilation verification, THE Documentation SHALL explain how to use the helper methods
3. WHEN I write semantic assertions, THE Documentation SHALL provide examples of syntax tree queries
4. WHEN I run tests locally, THE Documentation SHALL explain how to set up DynamoDB Local
5. WHEN I troubleshoot test failures, THE Documentation SHALL provide debugging tips and common issues

### Requirement 11: Backward Compatibility

**User Story:** As a developer, I want existing tests to continue working so that the upgrade doesn't break the build.

#### Acceptance Criteria

1. WHEN I add integration tests, THE Existing_Tests SHALL continue to run and pass without modification
2. WHEN I add compilation verification, THE Existing_Tests SHALL not be affected unless explicitly updated
3. WHEN I run the full test suite, THE Test_Infrastructure SHALL run both old and new tests
4. WHEN I run tests in CI/CD, THE Build_Pipeline SHALL report results for both unit and integration tests
5. WHEN I update the test infrastructure, THE Changes SHALL not require updating all existing tests at once

### Requirement 12: Test Isolation and Cleanup

**User Story:** As a developer, I want tests to be isolated and clean up after themselves so that test failures don't affect other tests.

#### Acceptance Criteria

1. WHEN a test creates a table, THE Test_Infrastructure SHALL delete the table after the test completes
2. WHEN a test fails, THE Test_Infrastructure SHALL still perform cleanup to avoid affecting subsequent tests
3. WHEN tests run in parallel, THE Test_Infrastructure SHALL ensure each test uses unique table names
4. WHEN a test needs to verify cleanup, THE Test_Infrastructure SHALL provide methods to check if resources were cleaned up
5. WHEN cleanup fails, THE Test_Infrastructure SHALL log the failure but not fail the test

### Requirement 13: Real-world Scenario Tests

**User Story:** As a developer, I want tests that verify realistic use cases so that I know the library works for actual applications.

#### Acceptance Criteria

1. WHEN I test complex entities, THE Integration_Tests SHALL include entities with multiple advanced types combined
2. WHEN I test query operations, THE Integration_Tests SHALL verify queries work with advanced type properties
3. WHEN I test update operations, THE Integration_Tests SHALL verify updates work correctly with collections
4. WHEN I test transactions, THE Integration_Tests SHALL verify transactional operations work with advanced types
5. WHEN I test error cases, THE Integration_Tests SHALL verify appropriate exceptions are thrown with clear messages

### Requirement 14: Test Execution Modes

**User Story:** As a developer, I want to run different subsets of tests so that I can get fast feedback during development.

#### Acceptance Criteria

1. WHEN I run unit tests only, THE Test_Infrastructure SHALL skip integration tests that require DynamoDB Local
2. WHEN I run integration tests only, THE Test_Infrastructure SHALL skip unit tests
3. WHEN I run all tests, THE Test_Infrastructure SHALL run both unit and integration tests
4. WHEN I run tests for a specific feature, THE Test_Infrastructure SHALL support filtering by category or trait
5. WHEN I run tests in watch mode, THE Test_Infrastructure SHALL only run affected tests based on code changes

### Requirement 15: Test Metrics and Reporting

**User Story:** As a developer, I want test metrics so that I can track test quality and coverage over time.

#### Acceptance Criteria

1. WHEN tests complete, THE Test_Infrastructure SHALL report the number of unit vs integration tests
2. WHEN tests complete, THE Test_Infrastructure SHALL report execution time for each test category
3. WHEN tests complete, THE Test_Infrastructure SHALL report code coverage for generated code
4. WHEN tests fail, THE Test_Infrastructure SHALL categorize failures by type (compilation, runtime, assertion)
5. WHEN running in CI/CD, THE Test_Infrastructure SHALL export metrics in a format compatible with build dashboards
