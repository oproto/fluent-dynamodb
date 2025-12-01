# Implementation Plan

- [x] 1. Set up project structure under examples folder
  - [x] 1.1 Create examples/OperationSamples/OperationSamples.csproj with .NET 8.0 target, nullable enabled, and project reference to Oproto.FluentDynamoDb
    - _Requirements: 3.1, 3.2, 3.3_
  - [x] 1.2 Create Models folder and Samples folder structure under examples/OperationSamples
    - _Requirements: 4.4_

- [x] 2. Implement domain models
  - [x] 2.1 Create Order entity with DynamoDbEntity attribute, PartitionKey (Pk), SortKey (Sk), and business properties (OrderId, CustomerId, OrderDate, Status, TotalAmount)
    - _Requirements: 4.1, 4.2_
  - [x] 2.2 Create OrderLine entity with DynamoDbEntity attribute, PartitionKey (Pk), SortKey (Sk), and business properties (LineId, ProductId, ProductName, Quantity, UnitPrice)
    - _Requirements: 4.1, 4.2_
  - [x] 2.3 Create OrdersTable class extending DynamoDbTableBase with entity accessors for Order and OrderLine
    - _Requirements: 4.1, 4.2_

- [x] 3. Implement single-item operation samples
  - [x] 3.1 Create GetSamples.cs with RawSdkGetAsync, FluentManualGetAsync, FluentFormattedGetAsync, FluentLambdaGetAsync methods
    - _Requirements: 1.1, 1.2, 1.3, 1.4, 1.5, 2.1, 5.1_
  - [x] 3.2 Create PutSamples.cs with RawSdkPutAsync, FluentManualPutAsync, FluentFormattedPutAsync, FluentLambdaPutAsync methods
    - _Requirements: 1.1, 1.2, 1.3, 1.4, 1.5, 2.1, 5.2_
  - [x] 3.3 Create UpdateSamples.cs with RawSdkUpdateAsync, FluentManualUpdateAsync, FluentFormattedUpdateAsync, FluentLambdaUpdateAsync methods demonstrating date formatting with {0:o}
    - _Requirements: 1.1, 1.2, 1.3, 1.4, 1.5, 2.1, 5.3, 6.1, 6.2_
  - [x] 3.4 Create DeleteSamples.cs with RawSdkDeleteAsync, FluentManualDeleteAsync, FluentFormattedDeleteAsync, FluentLambdaDeleteAsync methods
    - _Requirements: 1.1, 1.2, 1.3, 1.4, 1.5, 2.1, 5.4_

- [x] 4. Implement multi-item operation samples
  - [x] 4.1 Create QuerySamples.cs with RawSdkQueryAsync, FluentManualQueryAsync, FluentFormattedQueryAsync, FluentLambdaQueryAsync methods
    - _Requirements: 1.1, 1.2, 1.3, 1.4, 1.5, 2.1, 5.5_
  - [x] 4.2 Create ScanSamples.cs with RawSdkScanAsync, FluentManualScanAsync, FluentFormattedScanAsync, FluentLambdaScanAsync methods
    - _Requirements: 1.1, 1.2, 1.3, 1.4, 1.5, 2.1, 5.6_

- [x] 5. Implement transaction samples with full verbosity contrast
  - [x] 5.1 Create TransactionGetSamples.cs with RawSdkTransactionGetAsync (full verbose SDK), FluentManualTransactionGetAsync, FluentFormattedTransactionGetAsync, FluentLambdaTransactionGetAsync methods
    - _Requirements: 1.1, 1.2, 1.3, 1.4, 1.5, 2.2, 5.7, 7.1_
  - [x] 5.2 Create TransactionWriteSamples.cs with RawSdkTransactionWriteAsync (full verbose SDK showing Put+Update+Delete), FluentManualTransactionWriteAsync, FluentFormattedTransactionWriteAsync, FluentLambdaTransactionWriteAsync methods demonstrating multi-entity operations
    - _Requirements: 1.1, 1.2, 1.3, 1.4, 1.5, 2.2, 5.8, 7.1, 7.3_

- [x] 6. Implement batch samples with full verbosity contrast
  - [x] 6.1 Create BatchGetSamples.cs with RawSdkBatchGetAsync (full verbose SDK), FluentManualBatchGetAsync, FluentFormattedBatchGetAsync, FluentLambdaBatchGetAsync methods demonstrating multiple item types
    - _Requirements: 1.1, 1.2, 1.3, 1.4, 1.5, 2.2, 5.9, 7.2, 7.3_
  - [x] 6.2 Create BatchWriteSamples.cs with RawSdkBatchWriteAsync (full verbose SDK), FluentManualBatchWriteAsync, FluentFormattedBatchWriteAsync, FluentLambdaBatchWriteAsync methods demonstrating multiple item types
    - _Requirements: 1.1, 1.2, 1.3, 1.4, 1.5, 2.2, 5.10, 7.2, 7.3_

- [ ] 7. Refine samples for full equivalency and proper lambda expressions
  - [ ] 7.1 Update RawSdk methods to return domain models by manually converting AWS SDK responses
    - Add manual conversion from AttributeValue dictionaries to domain models
    - _Requirements: 8.1, 8.2_
  - [ ] 7.2 Create OrderUpdateModel class for lambda expression Set() operations
    - Add update model with nullable properties for Status, ModifiedAt, TotalAmount
    - _Requirements: 9.1_
  - [ ] 7.3 Update FluentLambdaUpdateAsync to use Set(x => new OrderUpdateModel { ... }) syntax
    - Replace format string Set() with lambda expression Set()
    - _Requirements: 9.1_
  - [ ] 7.4 Update FluentLambdaTransactionWriteAsync to use proper lambda expressions
    - Use Set(x => new OrderUpdateModel { ... }) for updates
    - Use Where(x => x.Pk.AttributeNotExists()) and Where(x => x.Pk.AttributeExists()) for conditions
    - _Requirements: 9.1, 9.2_
  - [ ] 7.5 Verify express-route methods are used in FluentLambda samples
    - Confirm PutAsync(entity), GetAsync(key), DeleteAsync(key) patterns are used
    - _Requirements: 9.3_

- [ ] 8. Checkpoint - Verify compilation
  - Ensure all tests pass, ask the user if questions arise.

- [ ] 9. Write property-based test for sample file structure
  - [ ] 9.1 Write property test verifying all sample files contain exactly four methods with correct naming patterns
    - **Property 1: Sample File Method Structure**
    - **Validates: Requirements 1.1**
