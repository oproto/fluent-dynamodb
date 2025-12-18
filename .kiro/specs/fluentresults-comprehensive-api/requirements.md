# Requirements Document

## Introduction

This document specifies the requirements for a comprehensive FluentResults integration with Oproto.FluentDynamoDb. The goal is to provide a complete Result<T> pattern alternative to the traditional async/exception-based API across all DynamoDB operations including CRUD operations, batch operations, transactions, geospatial queries, encryption, blob storage, and schema validation. The implementation will use a combination of extension methods in the FluentResults package and source generation via a `[UseFluentResults]` attribute to provide a seamless, opt-in experience.

## Glossary

- **FluentResults**: A .NET library providing a Result<T> pattern for explicit success/failure handling instead of exceptions
- **Result<T>**: A discriminated union type representing either a successful value or a collection of errors
- **DynamoDbError**: A custom FluentResults Error subclass representing DynamoDB-specific error conditions
- **UseFluentResults Attribute**: A source generator attribute that enables FluentResults API generation for entities or tables
- **Extension Method**: A static method that extends existing types without modifying them
- **Source Generator**: A compile-time code generation mechanism in .NET
- **Builder Pattern**: A fluent API pattern where methods return the builder for chaining
- **Terminal Method**: A method that executes the operation and returns results (e.g., GetItemAsync, ToListAsync)

## Requirements

### Requirement 1: DynamoDbErrors Integration

**User Story:** As a developer, I want standardized DynamoDB error types that integrate with FluentResults, so that I can handle DynamoDB-specific errors in a type-safe manner.

#### Acceptance Criteria

1. THE DynamoDbErrors class SHALL be located in the Oproto.FluentDynamoDb.FluentResults namespace
2. WHEN a DynamoDB exception is caught THEN the DynamoDbErrors.FromException method SHALL return an appropriate typed error
3. THE DynamoDbErrors class SHALL provide error types for all common DynamoDB exceptions including TransactionCanceledException, ConditionalCheckFailedException, ProvisionedThroughputExceededException, ResourceNotFoundException, and ValidationException
4. WHEN an error is created THEN the error SHALL include an ErrorCode property for programmatic error handling
5. THE error hierarchy SHALL use a base DynamoDbError class that extends FluentResults.Error

### Requirement 2: Core CRUD Operation Extensions

**User Story:** As a developer, I want FluentResults extensions for all core CRUD operations, so that I can use the Result pattern for basic DynamoDB operations.

#### Acceptance Criteria

1. WHEN GetItemRequestBuilder<T>.GetItemAsyncResult is called THEN the system SHALL return Result<T?> instead of throwing exceptions
2. WHEN PutItemRequestBuilder<T>.PutAsyncResult is called THEN the system SHALL return Result instead of throwing exceptions
3. WHEN UpdateItemRequestBuilder<T>.UpdateAsyncResult is called THEN the system SHALL return Result instead of throwing exceptions
4. WHEN DeleteItemRequestBuilder<T>.DeleteAsyncResult is called THEN the system SHALL return Result instead of throwing exceptions
5. WHEN QueryRequestBuilder<T>.ToListAsyncResult is called THEN the system SHALL return Result<List<T>> instead of throwing exceptions
6. WHEN ScanRequestBuilder<T>.ToListAsyncResult is called THEN the system SHALL return Result<List<T>> instead of throwing exceptions
7. WHEN any operation fails THEN the Result SHALL contain a DynamoDbError with the appropriate error type and original exception

### Requirement 3: Composite Entity Operation Extensions

**User Story:** As a developer, I want FluentResults extensions for composite entity operations, so that I can use the Result pattern when working with multi-item entities.

#### Acceptance Criteria

1. WHEN QueryRequestBuilder<T>.ToCompositeEntityAsyncResult is called THEN the system SHALL return Result<T?> instead of throwing exceptions
2. WHEN QueryRequestBuilder<T>.ToCompositeEntityListAsyncResult is called THEN the system SHALL return Result<List<T>> instead of throwing exceptions
3. WHEN ScanRequestBuilder<T>.ToCompositeEntityListAsyncResult is called THEN the system SHALL return Result<List<T>> instead of throwing exceptions

### Requirement 4: Blob Storage Operation Extensions

**User Story:** As a developer, I want FluentResults extensions for blob storage operations, so that I can use the Result pattern when working with entities that have blob references.

#### Acceptance Criteria

1. WHEN GetItemRequestBuilder<T>.GetItemAsyncResult is called with an IBlobStorageProvider THEN the system SHALL return Result<T?> with blob data hydrated
2. WHEN PutItemRequestBuilder<T>.PutAsyncResult is called with an IBlobStorageProvider THEN the system SHALL return Result with blob data stored
3. WHEN QueryRequestBuilder<T>.ToListAsyncResult is called with an IBlobStorageProvider THEN the system SHALL return Result<List<T>> with blob data hydrated
4. WHEN ScanRequestBuilder<T>.ToListAsyncResult is called with an IBlobStorageProvider THEN the system SHALL return Result<List<T>> with blob data hydrated
5. WHEN blob storage operations fail THEN the Result SHALL contain a BlobStorageError with details about the failure

### Requirement 5: Batch Operation Extensions

**User Story:** As a developer, I want FluentResults extensions for batch operations, so that I can use the Result pattern for bulk DynamoDB operations.

#### Acceptance Criteria

1. WHEN BatchGetBuilder.ExecuteAsyncResult is called THEN the system SHALL return Result<BatchGetResponse> instead of throwing exceptions
2. WHEN BatchWriteBuilder.ExecuteAsyncResult is called THEN the system SHALL return Result<BatchWriteItemResponse> instead of throwing exceptions
3. WHEN BatchPartiQLBuilder.ExecuteAsyncResult is called THEN the system SHALL return Result<BatchPartiQLResponse> instead of throwing exceptions
4. WHEN batch operations have unprocessed items THEN the Result SHALL be successful but include warnings about unprocessed items
5. WHEN BatchGetBuilder.ExecuteAndMapAsyncResult<T1,T2> is called THEN the system SHALL return Result<(T1?,T2?)> with typed tuple results

### Requirement 6: Transaction Operation Extensions

**User Story:** As a developer, I want FluentResults extensions for transaction operations, so that I can use the Result pattern for atomic DynamoDB operations.

#### Acceptance Criteria

1. WHEN TransactionWriteBuilder.ExecuteAsyncResult is called THEN the system SHALL return Result instead of throwing exceptions
2. WHEN TransactionGetBuilder.ExecuteAsyncResult is called THEN the system SHALL return Result<TransactionGetResponse> instead of throwing exceptions
3. WHEN a transaction is cancelled THEN the Result SHALL contain a TransactionCancelledError with cancellation reasons
4. WHEN a transaction conflict occurs THEN the Result SHALL contain a TransactionConflictError
5. WHEN an idempotency token mismatch occurs THEN the Result SHALL contain an IdempotencyError

### Requirement 7: Geospatial Query Extensions

**User Story:** As a developer, I want FluentResults extensions for geospatial queries, so that I can use the Result pattern for spatial DynamoDB operations.

#### Acceptance Criteria

1. WHEN IDynamoDbTable.SpatialQueryAsyncResult is called for proximity queries THEN the system SHALL return Result<SpatialQueryResponse<T>> instead of throwing exceptions
2. WHEN IDynamoDbTable.SpatialQueryAsyncResult is called for bounding box queries THEN the system SHALL return Result<SpatialQueryResponse<T>> instead of throwing exceptions
3. WHEN DynamoDbIndex.SpatialQueryAsyncResult is called THEN the system SHALL return Result<SpatialQueryResponse<T>> instead of throwing exceptions
4. WHEN spatial queries fail due to invalid coordinates THEN the Result SHALL contain a SpatialQueryError with validation details

### Requirement 8: UseFluentResults Attribute

**User Story:** As a developer, I want to opt-in to FluentResults API generation via an attribute, so that I can choose which entities or tables use the Result pattern.

#### Acceptance Criteria

1. WHEN [UseFluentResults] is applied to an entity class THEN the source generator SHALL generate Result-returning convenience methods on the entity accessor
2. WHEN [UseFluentResults] is applied to a table class THEN the source generator SHALL generate Result-returning convenience methods for all entity accessors on that table
3. WHEN [UseFluentResults(HideGeneratedAsyncMethods = true)] is specified THEN the source generator SHALL suppress generation of traditional async methods
4. WHEN [UseFluentResults(HideGeneratedAsyncMethods = false)] is specified THEN the source generator SHALL generate both traditional async and Result-returning methods
5. THE default value for HideGeneratedAsyncMethods SHALL be true

### Requirement 9: Generated Convenience Methods

**User Story:** As a developer, I want generated convenience methods that return Results, so that I can use a clean API without manually calling extension methods.

#### Acceptance Criteria

1. WHEN [UseFluentResults] is applied THEN the source generator SHALL generate GetAsyncResult methods on entity accessors
2. WHEN [UseFluentResults] is applied THEN the source generator SHALL generate PutAsyncResult methods on entity accessors
3. WHEN [UseFluentResults] is applied THEN the source generator SHALL generate DeleteAsyncResult methods on entity accessors
4. WHEN [UseFluentResults] is applied THEN the source generator SHALL generate QueryAsyncResult methods on entity accessors
5. THE generated methods SHALL follow the same signature patterns as existing generated async methods but return Result<T> types

### Requirement 10: PartiQL Operation Extensions

**User Story:** As a developer, I want FluentResults extensions for PartiQL operations, so that I can use the Result pattern for SQL-like DynamoDB queries.

#### Acceptance Criteria

1. WHEN PartiQLRequestBuilder<T>.ToListAsyncResult is called THEN the system SHALL return Result<List<T>> instead of throwing exceptions
2. WHEN PartiQLRequestBuilder<T>.ExecuteAsyncResult is called for non-SELECT statements THEN the system SHALL return Result instead of throwing exceptions
3. WHEN PartiQL syntax errors occur THEN the Result SHALL contain a PartiQLError with syntax details

### Requirement 11: Error Aggregation

**User Story:** As a developer, I want errors to be properly aggregated in batch and transaction operations, so that I can understand all failures in a single Result.

#### Acceptance Criteria

1. WHEN multiple errors occur in a batch operation THEN the Result SHALL contain all errors aggregated
2. WHEN a transaction is cancelled with multiple reasons THEN the Result SHALL contain errors for each cancellation reason
3. THE error aggregation SHALL preserve the order of operations for correlation with request items

### Requirement 12: Cancellation Token Support

**User Story:** As a developer, I want cancellation tokens to work correctly with FluentResults extensions, so that I can cancel long-running operations.

#### Acceptance Criteria

1. WHEN a CancellationToken is cancelled THEN the FluentResults extension SHALL re-throw OperationCanceledException without wrapping
2. THE CancellationToken parameter SHALL be optional with a default value on all FluentResults extension methods

### Requirement 13: Package Dependencies

**User Story:** As a developer, I want the FluentResults package to only depend on the core Oproto.FluentDynamoDb package, so that I can use it without pulling in unnecessary dependencies.

#### Acceptance Criteria

1. THE Oproto.FluentDynamoDb.FluentResults package SHALL only reference Oproto.FluentDynamoDb and FluentResults packages
2. THE FluentResults extensions for geospatial operations SHALL be implemented as extension methods that work when Oproto.FluentDynamoDb.Geospatial is referenced
3. THE FluentResults extensions for encryption operations SHALL be implemented as extension methods that work when Oproto.FluentDynamoDb.Encryption.Kms is referenced
4. THE FluentResults extensions for blob storage operations SHALL be implemented as extension methods that work when Oproto.FluentDynamoDb.BlobStorage.S3 is referenced

### Requirement 14: Exception to Error Mapping

**User Story:** As a developer, I want all custom exceptions in the library to have corresponding FluentResults error types, so that I can handle all error conditions consistently.

#### Acceptance Criteria

1. WHEN DynamoDbMappingException is thrown THEN the DynamoDbErrors.FromException method SHALL return a MappingError with entity type and field context
2. WHEN SchemaValidationException is thrown THEN the DynamoDbErrors.FromException method SHALL return a SchemaValidationError with validation details
3. WHEN ExpressionTranslationException is thrown THEN the DynamoDbErrors.FromException method SHALL return an ExpressionTranslationError with expression context
4. WHEN BlobStorageException is thrown THEN the DynamoDbErrors.FromException method SHALL return a BlobStorageError with blob key and operation details
5. WHEN FieldEncryptionException is thrown THEN the DynamoDbErrors.FromException method SHALL return an EncryptionError with field name and context ID
6. WHEN StreamProcessingException is thrown THEN the DynamoDbErrors.FromException method SHALL return a StreamProcessingError with record details
7. WHEN DiscriminatorMismatchException is thrown THEN the DynamoDbErrors.FromException method SHALL return a DiscriminatorMismatchError with expected and actual discriminator values
8. WHEN ProjectionValidationException is thrown THEN the DynamoDbErrors.FromException method SHALL return a ProjectionValidationError with index and type details
9. WHEN InvalidOperationException is thrown for RequireWriteTransaction violations THEN the DynamoDbErrors.FromException method SHALL return a WriteTransactionRequiredError with entity name
10. WHEN InvalidOperationException is thrown for batch/transaction client mismatch THEN the DynamoDbErrors.FromException method SHALL return a ClientMismatchError
11. WHEN InvalidOperationException is thrown for empty batch/transaction THEN the DynamoDbErrors.FromException method SHALL return an EmptyOperationError
12. WHEN InvalidOperationException is thrown for operation limit exceeded THEN the DynamoDbErrors.FromException method SHALL return an OperationLimitExceededError with the limit and actual count
13. WHEN InvalidOperationException is thrown for missing DynamoDB client THEN the DynamoDbErrors.FromException method SHALL return a MissingClientError
14. WHEN InvalidOperationException is thrown for mixed update expression approaches THEN the DynamoDbErrors.FromException method SHALL return an UpdateExpressionConflictError
15. WHEN InvalidOperationException is thrown for missing encryption configuration THEN the DynamoDbErrors.FromException method SHALL return an EncryptionConfigurationError with property names
16. WHEN ArgumentException is thrown for empty collections THEN the DynamoDbErrors.FromException method SHALL return an EmptyCollectionError with the parameter name
17. WHEN FormatException is thrown for invalid format strings THEN the DynamoDbErrors.FromException method SHALL return a FormatStringError with the invalid format details

### Requirement 15: Documentation Updates

**User Story:** As a developer, I want comprehensive documentation for the FluentResults integration, so that I can understand how to use the Result pattern with FluentDynamoDb.

#### Acceptance Criteria

1. THE FluentResults README.md SHALL be updated with comprehensive usage examples for all operation types
2. THE .kiro/steering/fluentdynamodb.md steering document SHALL be updated with FluentResults API patterns
3. THE docs/core-features folder SHALL contain a FluentResults.md guide with detailed examples
4. THE CHANGELOG.md SHALL be updated with the FluentResults enhancement entry
5. WHEN documenting FluentResults methods THEN the documentation SHALL show both traditional async and Result-returning patterns side by side

### Requirement 16: Encryption Operation Extensions

**User Story:** As a developer, I want FluentResults extensions for encryption operations, so that I can use the Result pattern when working with encrypted fields.

#### Acceptance Criteria

1. WHEN encryption operations fail during entity mapping THEN the Result SHALL contain an EncryptionError with field name and context
2. WHEN decryption operations fail during entity hydration THEN the Result SHALL contain a DecryptionError with field name and context
3. THE EncryptionError SHALL include the KMS key ARN when available for debugging purposes
