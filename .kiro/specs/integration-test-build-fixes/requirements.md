# Requirements Document

## Introduction

THE Integration Test Build Fix System SHALL restore compilation of the integration test project after multiple refactorings where integration tests were not included in the solution, resulting in 404 compilation errors across multiple test files.

## Glossary

- **Integration Test Project**: The Oproto.FluentDynamoDb.IntegrationTests project containing tests that verify library functionality against actual DynamoDB operations
- **Source Generator**: The Roslyn source generator that creates table accessor classes and helper methods at compile time
- **Table Accessor**: Generated properties on table classes that provide type-safe access to entity operations (e.g., Orders, OrderLines)
- **Request Builder**: Fluent API classes for building DynamoDB operation requests (e.g., QueryRequestBuilder, TransactWriteItemsRequestBuilder)
- **Encryption Context**: A class or namespace providing encryption-related functionality for field-level security tests
- **Mock Field Encryptor**: A test double used in integration tests to verify encryption behavior without actual encryption

## Requirements

### Requirement 1: Source Generator Output Verification

**User Story:** As a developer, I want to verify that the source generator produces the expected table accessor properties, so that integration tests can access generated code correctly.

#### Acceptance Criteria

1. WHEN THE Integration Test Project is compiled, THE Source Generator SHALL generate table accessor properties for all entity types defined in test table classes
2. WHEN a test table class defines multiple entity types, THE Source Generator SHALL create accessor properties for each entity type (e.g., Orders, OrderLines, Payments)
3. IF THE Source Generator fails to generate expected accessors, THEN THE Integration Test Project SHALL report compilation errors identifying missing properties
4. WHEN examining generated code, THE Source Generator SHALL produce properties matching the naming convention used in test files

### Requirement 2: Request Builder API Compatibility

**User Story:** As a developer, I want the request builder APIs to match what integration tests expect, so that batch and transaction operations compile successfully.

#### Acceptance Criteria

1. WHEN THE TransactWriteItemsRequestBuilder is used, THE Request Builder SHALL provide AddPut, AddDelete, AddUpdate, and AddConditionCheck methods
2. WHEN THE TransactGetItemsRequestBuilder is used, THE Request Builder SHALL provide an AddGet method
3. WHEN THE BatchWriteItemRequestBuilder is used, THE Request Builder SHALL provide AddPut and AddDelete methods
4. WHEN THE BatchGetItemRequestBuilder is used, THE Request Builder SHALL provide an AddGet method
5. WHEN THE DynamoDbTableBase is used, THE Table Base Class SHALL provide Scan, BatchGet, BatchWrite, TransactGet, and TransactWrite methods

### Requirement 3: Encryption Context Availability

**User Story:** As a developer, I want encryption context functionality to be available in integration tests, so that field-level security tests can execute.

#### Acceptance Criteria

1. WHEN integration tests reference EncryptionContext, THE Integration Test Project SHALL resolve the type from the appropriate namespace
2. IF EncryptionContext was renamed or moved, THEN THE Integration Test Project SHALL use the correct updated type name
3. WHEN encryption tests execute, THE Integration Test Project SHALL have access to all encryption context functionality required by tests

### Requirement 4: Expression API Type Safety

**User Story:** As a developer, I want expression-based query and scan operations to infer types correctly, so that LINQ-style queries compile without explicit type parameters.

#### Acceptance Criteria

1. WHEN THE Query method is called without type parameters, THE Request Builder SHALL infer the entity type from context
2. WHEN THE Scan method is called without type parameters, THE Request Builder SHALL infer the entity type from context
3. WHEN THE Put method is called without type parameters, THE Request Builder SHALL infer the entity type from the provided entity
4. IF type inference fails, THEN THE Integration Test Project SHALL provide clear compilation errors indicating which type parameters are required

### Requirement 5: Lambda Expression Compatibility

**User Story:** As a developer, I want lambda expressions in test code to match expected delegate types, so that expression-based operations compile correctly.

#### Acceptance Criteria

1. WHEN a lambda expression is passed to a query or filter method, THE Request Builder SHALL accept the lambda with the correct delegate type
2. WHEN THE SensitiveDataRedactionIntegrationTests uses lambda expressions, THE Integration Test Project SHALL compile without type conversion errors
3. IF a method signature changed from string to Expression<Func<T, bool>>, THEN THE Integration Test Project SHALL update test code to match

### Requirement 6: Test Infrastructure Compatibility

**User Story:** As a developer, I want test infrastructure classes to provide the APIs that tests expect, so that test setup and assertions work correctly.

#### Acceptance Criteria

1. WHEN THE MockFieldEncryptor is used in tests, THE Mock Field Encryptor SHALL provide an EncryptCalls property or equivalent for verification
2. WHEN FluentAssertions methods are called, THE Integration Test Project SHALL use the correct method names for the installed version (e.g., BeGreaterThanOrEqualTo vs BeGreaterOrEqualTo)
3. WHEN test tables are instantiated, THE Integration Test Project SHALL provide TableName and Client properties as expected by tests

### Requirement 7: Method Signature Consistency

**User Story:** As a developer, I want method signatures to be consistent with how tests invoke them, so that all integration tests compile successfully.

#### Acceptance Criteria

1. WHEN THE Update method is referenced, THE Integration Test Project SHALL treat it as a method call, not a property
2. WHEN THE Put method is called, THE Request Builder SHALL accept the correct number and types of parameters
3. WHEN THE Get method is called, THE Request Builder SHALL accept the correct number and types of parameters
4. WHEN THE Delete method is called, THE Request Builder SHALL accept the correct number and types of parameters
5. IF a method signature changed during refactoring, THEN THE Integration Test Project SHALL update all call sites to match the new signature

### Requirement 8: Build Success Verification

**User Story:** As a developer, I want the integration test project to build successfully, so that I can run tests to verify library functionality.

#### Acceptance Criteria

1. WHEN THE Integration Test Project is compiled, THE Build System SHALL complete without compilation errors
2. WHEN all fixes are applied, THE Integration Test Project SHALL have zero CS error codes in build output
3. WHEN the build succeeds, THE Integration Test Project SHALL be ready for test execution
4. IF warnings remain after fixes, THEN THE Integration Test Project SHALL document which warnings are acceptable and which require attention
