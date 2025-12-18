# Implementation Plan

- [x] 1. Create core provisioning infrastructure
  - [x] 1.1 Create TableCreationOptions class
    - Create `Oproto.FluentDynamoDb/Provisioning/TableCreationOptions.cs`
    - Include BillingMode, ProvisionedThroughputConfig, EnableTtl, WaitForActive, WaitTimeout, PollingInterval properties
    - Create ProvisionedThroughputConfig nested class with ReadCapacityUnits and WriteCapacityUnits
    - _Requirements: 1.4, 1.5, 4.1, 4.2, 5.1, 5.2, 5.3, 5.4_

  - [x] 1.2 Create TableCreationResult class
    - Create `Oproto.FluentDynamoDb/Provisioning/TableCreationResult.cs`
    - Include TableName, TableArn, TableStatus, TtlEnabled properties
    - _Requirements: 5.1_

- [x] 2. Implement TableCreator core functionality
  - [x] 2.1 Create TableCreator class with BuildCreateTableRequest method
    - Create `Oproto.FluentDynamoDb/Provisioning/TableCreator.cs`
    - Implement BuildCreateTableRequest that maps EntityMetadata to CreateTableRequest
    - Handle partition key and sort key mapping to KeySchema
    - Handle AttributeDefinitions generation for all key attributes
    - _Requirements: 1.1, 1.2, 1.3_

  - [x] 2.2 Write property test for primary key schema round-trip
    - **Property 1: Primary key schema round-trip**
    - **Validates: Requirements 1.1, 1.2, 1.3**

  - [x] 2.3 Implement GSI mapping in BuildCreateTableRequest
    - Map IndexMetadata (GSI type) to GlobalSecondaryIndexes
    - Handle GSI key schema (partition key and optional sort key)
    - Handle GSI projection type mapping (ALL, KEYS_ONLY, INCLUDE)
    - _Requirements: 2.1, 2.2, 2.3, 2.4, 2.5, 2.6_

  - [x] 2.4 Write property test for GSI configuration preservation
    - **Property 2: GSI configuration preservation**
    - **Validates: Requirements 2.1, 2.2, 2.3, 2.4, 2.5, 2.6**

  - [x] 2.5 Implement LSI mapping in BuildCreateTableRequest
    - Map IndexMetadata (LSI type) to LocalSecondaryIndexes
    - Ensure LSI uses table's partition key with LSI's sort key
    - Handle LSI projection type mapping
    - _Requirements: 3.1, 3.2, 3.3, 3.4, 3.5_

  - [x] 2.6 Write property test for LSI configuration preservation
    - **Property 3: LSI configuration preservation**
    - **Validates: Requirements 3.1, 3.2, 3.3, 3.4, 3.5**

  - [x] 2.7 Write property test for attribute definitions completeness
    - **Property 4: Attribute definitions completeness**
    - **Validates: Requirements 1.1, 1.2, 2.2, 2.3, 3.2**

  - [x] 2.8 Implement billing mode and throughput configuration
    - Handle PAY_PER_REQUEST default billing mode
    - Handle PROVISIONED billing mode with throughput configuration
    - Apply throughput to table and GSIs
    - _Requirements: 1.4, 1.5_

  - [x] 2.9 Write property test for provisioned throughput configuration
    - **Property 5: Provisioned throughput configuration**
    - **Validates: Requirements 1.5**

- [x] 3. Checkpoint - Make sure all tests are passing
  - Ensure all tests pass, ask the user if questions arise.

- [x] 4. Implement CreateAsync method
  - [x] 4.1 Implement CreateAsync with table creation
    - Call BuildCreateTableRequest and execute via IAmazonDynamoDB.CreateTableAsync
    - Return TableCreationResult with table information
    - _Requirements: 1.1, 1.2, 1.3_

  - [x] 4.2 Implement wait-for-active functionality
    - Poll DescribeTable until TableStatus is ACTIVE
    - Respect WaitTimeout and PollingInterval options
    - Throw TimeoutException if timeout exceeded
    - _Requirements: 5.1, 5.2, 5.3, 5.4_

  - [x] 4.3 Implement TTL enablement
    - Call UpdateTimeToLive after table is active if EnableTtl is true and metadata has TtlAttributeName
    - Update TableCreationResult.TtlEnabled accordingly
    - _Requirements: 4.1, 4.2, 4.3_

  - [x] 4.4 Implement input validation and error handling
    - Validate tableName is not null or empty
    - Validate metadata has partition key defined
    - Validate key attribute types are valid (S, N, B)
    - _Requirements: 7.1, 7.2, 7.3_

  - [x] 4.5 Write unit tests for CreateAsync
    - Test default options behavior
    - Test TTL enablement logic
    - Test wait-for-active disabled behavior
    - Test error handling for invalid metadata
    - _Requirements: 1.4, 4.1, 4.2, 4.3, 5.2, 7.3_

- [x] 5. Implement source generator
  - [x] 5.1 Create TableCreationGenerator
    - Create `Oproto.FluentDynamoDb.SourceGenerator/Generators/TableCreationGenerator.cs`
    - Generate static CreateTableAsync method on table classes with required tableName parameter
    - _Requirements: 6.1, 6.2_

  - [x] 5.2 Integrate TableCreationGenerator into DynamoDbSourceGenerator
    - Call TableCreationGenerator from main source generator
    - Ensure method is generated for all table classes
    - _Requirements: 6.1_

  - [x] 5.3 Write property test for table name in request
    - **Property 6: Table name is used in request**
    - **Validates: Requirements 6.2**

  - [x] 5.4 Write unit tests for TableCreationGenerator
    - Verify generated method signature includes required tableName parameter
    - Verify generated method calls TableCreator correctly
    - _Requirements: 6.1, 6.2_

- [x] 6. Checkpoint - Make sure all tests are passing
  - Ensure all tests pass, ask the user if questions arise.

- [x] 7. Integration tests
  - [x] 7.1 Write integration tests for table creation
    - Test end-to-end table creation with DynamoDB Local
    - Test table creation with GSIs and LSIs
    - Test TTL enablement
    - Test generated static method
    - _Requirements: 1.1, 2.1, 3.1, 4.1, 6.2_

- [x] 8. Documentation
  - [x] 8.1 Create TableCreation.md documentation
    - Create `docs/advanced-topics/TableCreation.md`
    - Document TableCreator, TableCreationOptions, and TableCreationResult
    - Include code examples for integration testing scenarios
    - Reference both ValidateSchemaAsync and CreateTableAsync as complementary features
    - _Requirements: All_

  - [x] 8.2 Update fluentdynamodb.md steering file
    - Add Table Creation section to `.kiro/steering/fluentdynamodb.md`
    - Document CreateTableAsync method signature and options
    - Include example usage for integration tests
    - _Requirements: All_

  - [x] 8.3 Update CHANGELOG.md
    - Add entry for new Table Creation feature in CHANGELOG.md
    - _Requirements: All_

  - [x] 8.4 Update docs/DOCUMENTATION_CHANGELOG.md
    - Track documentation additions for TableCreation.md
    - _Requirements: All_

- [ ] 9. Final Checkpoint - Make sure all tests are passing
  - Ensure all tests pass, ask the user if questions arise.
