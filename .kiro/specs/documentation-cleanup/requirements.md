# Requirements Document

## Introduction

This specification addresses documentation cleanup and reorganization needed to correct inaccurate API examples and improve the organization of advanced topics documentation. The primary issues are:

1. **STSIntegration.md** conflates two distinct topics: client configuration (dev environments, multi-region) and STS-scoped security credentials
2. **Batch operation examples** incorrectly use `new BatchWriteItemRequestBuilder(client)` instead of the correct `DynamoDbBatch.Write` static entry point
3. **Transaction examples** incorrectly use `new TransactWriteItemsRequestBuilder(client)` instead of the correct `DynamoDbTransactions.Write` static entry point
4. **Transaction execution** incorrectly uses `CommitAsync()` instead of the correct `ExecuteAsync()` method

## Glossary

- **DynamoDbBatch**: Static entry point class providing `Write` and `Get` properties for composing batch operations
- **DynamoDbTransactions**: Static entry point class providing `Write` and `Get` properties for composing transaction operations
- **BatchWriteBuilder**: Fluent builder returned by `DynamoDbBatch.Write` for composing batch write operations
- **BatchGetBuilder**: Fluent builder returned by `DynamoDbBatch.Get` for composing batch get operations
- **TransactionWriteBuilder**: Fluent builder returned by `DynamoDbTransactions.Write` for composing transaction write operations
- **TransactionGetBuilder**: Fluent builder returned by `DynamoDbTransactions.Get` for composing transaction get operations
- **WithClient**: Method on builders that allows specifying a custom DynamoDB client for execution
- **STS**: AWS Security Token Service, used for assuming roles with temporary credentials

## Requirements

### Requirement 1

**User Story:** As a developer reading the documentation, I want batch operation examples to use the correct API pattern, so that I can successfully implement batch operations in my code.

#### Acceptance Criteria

1. WHEN a developer reads batch write examples THEN the documentation SHALL show `DynamoDbBatch.Write.Add(...)` pattern instead of `new BatchWriteItemRequestBuilder(client)`
2. WHEN a developer reads batch get examples THEN the documentation SHALL show `DynamoDbBatch.Get.Add(...)` pattern instead of `new BatchGetItemRequestBuilder(client)`
3. WHEN batch operations need a custom client THEN the documentation SHALL show `.WithClient(client)` method or passing client to `ExecuteAsync(client)`

### Requirement 2

**User Story:** As a developer reading the documentation, I want transaction operation examples to use the correct API pattern, so that I can successfully implement transactions in my code.

#### Acceptance Criteria

1. WHEN a developer reads transaction write examples THEN the documentation SHALL show `DynamoDbTransactions.Write.Add(...)` pattern instead of `new TransactWriteItemsRequestBuilder(client)`
2. WHEN a developer reads transaction get examples THEN the documentation SHALL show `DynamoDbTransactions.Get.Add(...)` pattern instead of `new TransactGetItemsRequestBuilder(client)`
3. WHEN transaction operations need a custom client THEN the documentation SHALL show `.WithClient(client)` method or passing client to `ExecuteAsync(client)`
4. WHEN a developer reads transaction execution examples THEN the documentation SHALL show `ExecuteAsync()` instead of `CommitAsync()`

### Requirement 3

**User Story:** As a developer, I want the STSIntegration.md document to be reorganized into focused topics, so that I can find relevant information about either client configuration or scoped security.

#### Acceptance Criteria

1. WHEN a developer needs information about STS-scoped credentials THEN the documentation SHALL provide a dedicated document named `ScopedSecurity.md` focused on the `WithClient()` method for security scenarios
2. WHEN a developer needs information about client configuration THEN the documentation SHALL provide a dedicated document named `ClientConfiguration.md` covering dev environments, multi-region, custom timeouts, and LocalStack/DynamoDB Local
3. WHEN the STSIntegration.md file is reorganized THEN the system SHALL delete the original file after content is migrated
4. WHEN the advanced topics README is updated THEN it SHALL reflect the new document structure

### Requirement 4

**User Story:** As a developer, I want the documentation changelog to record all corrections made, so that teams maintaining derived documentation can synchronize their content.

#### Acceptance Criteria

1. WHEN documentation corrections are made THEN the documentation changelog SHALL receive new entries documenting the before/after patterns
2. WHEN the documentation changelog is updated THEN it SHALL NOT modify historical entries (they are a record of past changes)
3. WHEN new changelog entries are added THEN they SHALL follow the established format with date, file path, before pattern, after pattern, and reason

### Requirement 5

**User Story:** As a developer, I want all documentation files to be consistent with the actual API and follow preferred style patterns, so that code examples compile, work correctly, and demonstrate best practices.

#### Acceptance Criteria

1. WHEN documentation is updated THEN all affected files SHALL be corrected in a single sweep
2. WHEN documentation corrections are made THEN the documentation changelog SHALL be updated with the corrections
3. WHEN documentation is corrected THEN the QUICK_REFERENCE.md SHALL reflect the correct patterns
4. WHEN documentation shows API examples THEN it SHALL prefer lambda expressions over format strings where appropriate
5. WHEN documentation shows table operations THEN it SHALL prefer entity accessor patterns (table.Users.Get()) over generic patterns (table.Get<User>())
6. WHEN documentation uses key helper methods THEN it SHALL only use Keys.Pk()/Keys.Sk() when the entity has key prefixes configured

### Requirement 6

**User Story:** As a developer, I want documentation examples to use realistic typed table classes, so that I understand the recommended patterns for production code.

#### Acceptance Criteria

1. WHEN documentation shows repository or service examples THEN it SHALL use concrete typed table classes (e.g., UserTable) instead of DynamoDbTableBase as field types
2. WHEN documentation shows table instantiation THEN it SHALL prefer source-generated table classes over direct DynamoDbTableBase instantiation
3. WHEN documentation references DynamoDbTableBase THEN it SHALL only do so when explaining the base class itself or showing manual patterns as alternatives

### Requirement 7

**User Story:** As a developer, I want documentation to use only types that actually exist in the API, so that I don't encounter compilation errors when following examples.

#### Acceptance Criteria

1. WHEN documentation shows generic types THEN it SHALL only use generics that exist in the actual API
2. WHEN documentation shows DynamoDbTableBase THEN it SHALL NOT use a generic form (DynamoDbTableBase<T>) as this type does not exist
