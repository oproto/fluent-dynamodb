# Requirements Document

## Introduction

This feature adds the ability to create DynamoDB tables programmatically using the same entity metadata that powers schema validation. While not intended for production use (except for time-series tables created on-the-fly), this capability is primarily designed for integration testing scenarios where developers need to create tables that match their entity definitions without manual CloudFormation or Terraform setup.

The feature complements the existing `ValidateSchemaAsync` method by providing a `CreateTableAsync` method that uses `EntityMetadata` to construct and execute a `CreateTableRequest`.

## Glossary

- **EntityMetadata**: A class containing comprehensive metadata about a DynamoDB entity including table name, key schema, indexes, and TTL configuration
- **IndexMetadata**: Metadata about a secondary index (GSI or LSI) including key schema and projection configuration
- **CreateTableRequest**: AWS SDK request object for creating a DynamoDB table
- **BillingMode**: DynamoDB billing configuration (PAY_PER_REQUEST or PROVISIONED)
- **ProvisionedThroughput**: Read and write capacity units for provisioned billing mode
- **TableCreator**: The component responsible for building and executing CreateTableRequest from EntityMetadata

## Requirements

### Requirement 1

**User Story:** As a developer writing integration tests, I want to create a DynamoDB table from my entity metadata, so that I can run tests against a table that matches my entity definition without manual setup.

#### Acceptance Criteria

1. WHEN a developer calls CreateTableAsync with a DynamoDB client and entity metadata THEN the TableCreator SHALL create a table with the correct partition key name and type
2. WHEN a developer calls CreateTableAsync with entity metadata that includes a sort key THEN the TableCreator SHALL create a table with the correct sort key name and type
3. WHEN a developer calls CreateTableAsync with entity metadata that has no sort key THEN the TableCreator SHALL create a table with only a partition key
4. WHEN a developer calls CreateTableAsync THEN the TableCreator SHALL use PAY_PER_REQUEST billing mode by default
5. WHEN a developer calls CreateTableAsync with custom options specifying provisioned throughput THEN the TableCreator SHALL use PROVISIONED billing mode with the specified capacity units

### Requirement 2

**User Story:** As a developer, I want the table creation to include my Global Secondary Indexes, so that I can test queries against GSIs.

#### Acceptance Criteria

1. WHEN entity metadata includes GSI definitions THEN the TableCreator SHALL create the table with all defined GSIs
2. WHEN a GSI has both partition key and sort key THEN the TableCreator SHALL configure the GSI with both keys
3. WHEN a GSI has only a partition key THEN the TableCreator SHALL configure the GSI with only the partition key
4. WHEN a GSI has projection type ALL THEN the TableCreator SHALL configure the GSI projection as ALL
5. WHEN a GSI has projection type KEYS_ONLY THEN the TableCreator SHALL configure the GSI projection as KEYS_ONLY
6. WHEN a GSI has projection type INCLUDE THEN the TableCreator SHALL configure the GSI projection as INCLUDE with the specified attributes

### Requirement 3

**User Story:** As a developer, I want the table creation to include my Local Secondary Indexes, so that I can test queries against LSIs.

#### Acceptance Criteria

1. WHEN entity metadata includes LSI definitions THEN the TableCreator SHALL create the table with all defined LSIs
2. WHEN an LSI is defined THEN the TableCreator SHALL configure the LSI with the table's partition key and the LSI's sort key
3. WHEN an LSI has projection type ALL THEN the TableCreator SHALL configure the LSI projection as ALL
4. WHEN an LSI has projection type KEYS_ONLY THEN the TableCreator SHALL configure the LSI projection as KEYS_ONLY
5. WHEN an LSI has projection type INCLUDE THEN the TableCreator SHALL configure the LSI projection as INCLUDE with the specified attributes

### Requirement 4

**User Story:** As a developer, I want to optionally enable TTL on the created table, so that I can test TTL-dependent functionality.

#### Acceptance Criteria

1. WHEN entity metadata includes a TTL attribute name and TTL is enabled in options THEN the TableCreator SHALL enable TTL on the table with the specified attribute
2. WHEN entity metadata includes a TTL attribute name but TTL is not enabled in options THEN the TableCreator SHALL NOT enable TTL on the table
3. WHEN entity metadata does not include a TTL attribute name THEN the TableCreator SHALL NOT attempt to enable TTL regardless of options

### Requirement 5

**User Story:** As a developer, I want the table creation to wait for the table to become active, so that I can immediately use the table after creation.

#### Acceptance Criteria

1. WHEN CreateTableAsync is called with waitForActive option enabled THEN the TableCreator SHALL poll the table status until it becomes ACTIVE
2. WHEN CreateTableAsync is called with waitForActive option disabled THEN the TableCreator SHALL return immediately after the CreateTable call
3. WHEN waiting for table to become active and a timeout is specified THEN the TableCreator SHALL throw a TimeoutException if the table does not become active within the timeout period
4. WHEN waiting for table to become active THEN the TableCreator SHALL use a configurable polling interval

### Requirement 6

**User Story:** As a developer using the source generator, I want a convenient static method on my table class to create the table, so that I can create tables with minimal boilerplate.

#### Acceptance Criteria

1. WHEN a table class is generated THEN the source generator SHALL include a static CreateTableAsync method that requires a table name parameter
2. WHEN CreateTableAsync is called on a generated table class THEN the method SHALL use the entity's metadata to create the table with the specified name

### Requirement 7

**User Story:** As a developer, I want clear error handling when table creation fails, so that I can diagnose and fix issues.

#### Acceptance Criteria

1. WHEN the table already exists THEN the TableCreator SHALL throw a TableAlreadyExistsException with a descriptive message
2. WHEN the DynamoDB client returns an error THEN the TableCreator SHALL propagate the exception with context about the operation
3. WHEN invalid metadata is provided THEN the TableCreator SHALL throw an ArgumentException with details about the invalid configuration
