# Requirements Document

## Introduction

This document specifies requirements for redesigning the transaction and batch operation APIs in Oproto.FluentDynamoDb. The current API requires passing table references to transaction/batch builders and uses action-based configuration, which is verbose and doesn't leverage the existing fluent builders and source-generated methods. The new design will allow reusing existing request builders (Put, Update, Delete, Get, ConditionCheck) within transaction and batch contexts, providing access to all string formatting, lambda expressions, and source-generated key methods while maintaining compile-time safety.

## Glossary

- **Transaction Builder**: A fluent builder for composing DynamoDB transaction operations (TransactWriteItems or TransactGetItems)
- **Batch Builder**: A fluent builder for composing DynamoDB batch operations (BatchWriteItem or BatchGetItem)
- **Request Builder**: An existing fluent builder for individual operations (PutItemRequestBuilder, UpdateItemRequestBuilder, etc.)
- **Marker Interface**: An interface implemented by request builders to indicate they can be used in transaction/batch contexts
- **Source Generator**: Code generation that produces strongly-typed methods eliminating generic parameters
- **String Formatting**: Expression syntax using placeholders like `Where("pk = {0}", value)`
- **Lambda Expression**: Type-safe expression syntax like `Set(x => new UpdateModel { Value = "123" })`
- **DynamoDbTransactions**: Static class providing entry points for transaction operations
- **DynamoDbBatch**: Static class providing entry points for batch operations
- **WithClient Pattern**: A fluent pattern for specifying the DynamoDB client to use for operations, supporting scoped IAM credentials and multi-client scenarios

## Requirements

### Requirement 1: Transaction Write API

**User Story:** As a developer, I want to compose transaction write operations using existing request builders, so that I can leverage all fluent methods, string formatting, lambda expressions, and source-generated key accessors without code duplication.

#### Acceptance Criteria

1. WHEN a developer calls `DynamoDbTransactions.Write`, THE System SHALL return a transaction write builder that accepts request builders via `Add()` method overloads
2. WHEN a developer passes a `PutItemRequestBuilder<TEntity>` to the `Add()` method, THE System SHALL extract the put request and add it to the transaction items
3. WHEN a developer passes an `UpdateItemRequestBuilder<TEntity>` to the `Add()` method, THE System SHALL extract the update request and add it to the transaction items
4. WHEN a developer passes a `DeleteItemRequestBuilder<TEntity>` to the `Add()` method, THE System SHALL extract the delete request and add it to the transaction items
5. WHEN a developer passes a `ConditionCheckBuilder<TEntity>` to the `Add()` method, THE System SHALL extract the condition check request and add it to the transaction items
6. WHEN the transaction write builder calls `ExecuteAsync()`, THE System SHALL execute all operations atomically using TransactWriteItems API
7. WHEN extracting requests from builders, THE System SHALL ignore transaction-incompatible settings such as ReturnValues, ReturnConsumedCapacity at the item level, and ReturnItemCollectionMetrics at the item level
8. WHEN a developer chains multiple `Add()` calls, THE System SHALL maintain the order of operations in the transaction

### Requirement 2: Transaction Get API

**User Story:** As a developer, I want to compose transaction get operations using existing get request builders, so that I can retrieve multiple items atomically with consistent snapshot isolation.

#### Acceptance Criteria

1. WHEN a developer calls `DynamoDbTransactions.Get`, THE System SHALL return a transaction get builder that accepts get request builders via `Add()` method overloads
2. WHEN a developer passes a `GetItemRequestBuilder<TEntity>` to the `Add()` method, THE System SHALL extract the get request and add it to the transaction items
3. WHEN the transaction get builder calls `ExecuteAsync()`, THE System SHALL execute all get operations atomically using TransactGetItems API with snapshot isolation
4. WHEN extracting get requests from builders, THE System SHALL preserve projection expressions and attribute name mappings
5. WHEN a developer chains multiple `Add()` calls, THE System SHALL maintain the order of get operations in the transaction

### Requirement 3: Batch Write API

**User Story:** As a developer, I want to compose batch write operations using existing request builders, so that I can efficiently write multiple items across tables with minimal API calls.

#### Acceptance Criteria

1. WHEN a developer calls `DynamoDbBatch.Write`, THE System SHALL return a batch write builder that accepts request builders via `Add()` method overloads
2. WHEN a developer passes a `PutItemRequestBuilder<TEntity>` to the `Add()` method, THE System SHALL extract the put request and add it to the batch write items for the appropriate table
3. WHEN a developer passes a `DeleteItemRequestBuilder<TEntity>` to the `Add()` method, THE System SHALL extract the delete request and add it to the batch write items for the appropriate table
4. WHEN the batch write builder calls `ExecuteAsync()`, THE System SHALL execute all operations using BatchWriteItem API
5. WHEN extracting requests from builders, THE System SHALL ignore batch-incompatible settings such as condition expressions, ReturnValues, and ReturnConsumedCapacity at the item level
6. WHEN multiple operations target the same table, THE System SHALL group them under the same table key in the batch request
7. WHEN the batch contains more than twenty-five operations, THE System SHALL throw a validation exception with a clear message about the limit

### Requirement 4: Batch Get API

**User Story:** As a developer, I want to compose batch get operations using existing get request builders, so that I can efficiently retrieve multiple items across tables with minimal API calls.

#### Acceptance Criteria

1. WHEN a developer calls `DynamoDbBatch.Get`, THE System SHALL return a batch get builder that accepts get request builders via `Add()` method overloads
2. WHEN a developer passes a `GetItemRequestBuilder<TEntity>` to the `Add()` method, THE System SHALL extract the get request and add it to the batch get items for the appropriate table
3. WHEN the batch get builder calls `ExecuteAsync()`, THE System SHALL execute all operations using BatchGetItem API
4. WHEN extracting get requests from builders, THE System SHALL preserve projection expressions, attribute name mappings, and consistent read settings
5. WHEN multiple get operations target the same table, THE System SHALL group them under the same table key in the batch request
6. WHEN the batch contains more than one hundred operations, THE System SHALL throw a validation exception with a clear message about the limit

### Requirement 5: Condition Check Builder

**User Story:** As a developer, I want to create condition check operations for transactions using a dedicated builder, so that I can verify conditions without modifying data.

#### Acceptance Criteria

1. WHEN a developer calls a table's `ConditionCheck()` method, THE System SHALL return a `ConditionCheckBuilder<TEntity>` instance
2. WHEN the condition check builder is configured, THE System SHALL support `WithKey()`, `Where()`, string formatting with placeholders, and lambda expressions for conditions
3. WHEN the condition check builder is passed to a transaction write builder's `Add()` method, THE System SHALL extract the condition check request
4. WHEN the condition check builder is used, THE System SHALL NOT expose execution methods like `ExecuteAsync()` or `ToDynamoDbResponseAsync()`
5. WHEN source generation is applied to a table, THE System SHALL generate strongly-typed `ConditionCheck(pk, sk)` methods that eliminate generic parameters

### Requirement 6: Request Builder Marker Interfaces

**User Story:** As a developer, I want request builders to implement marker interfaces, so that the transaction and batch builders can accept them in a type-safe manner.

#### Acceptance Criteria

1. WHEN a `PutItemRequestBuilder<TEntity>` is created, THE System SHALL implement an `ITransactablePutBuilder` marker interface
2. WHEN an `UpdateItemRequestBuilder<TEntity>` is created, THE System SHALL implement an `ITransactableUpdateBuilder` marker interface
3. WHEN a `DeleteItemRequestBuilder<TEntity>` is created, THE System SHALL implement an `ITransactableDeleteBuilder` marker interface
4. WHEN a `GetItemRequestBuilder<TEntity>` is created, THE System SHALL implement an `ITransactableGetBuilder` marker interface
5. WHEN a `ConditionCheckBuilder<TEntity>` is created, THE System SHALL implement an `ITransactableConditionCheckBuilder` marker interface
6. WHEN transaction and batch builders define `Add()` method overloads, THE System SHALL use these marker interfaces as type constraints

### Requirement 7: String Formatting Support

**User Story:** As a developer, I want to use string formatting with placeholders in transaction and batch operations, so that I can write concise expressions without manually managing attribute names and values.

#### Acceptance Criteria

1. WHEN a developer uses `Where("pk = {0}", value)` on a request builder within a transaction, THE System SHALL process the placeholder and add the value to expression attribute values
2. WHEN a developer uses `Set("value = {0}", newValue)` on an update builder within a transaction, THE System SHALL process the placeholder and add the value to expression attribute values
3. WHEN the transaction or batch builder extracts the request, THE System SHALL preserve all processed expression attribute names and values
4. WHEN multiple placeholders are used in a single expression, THE System SHALL correctly map all placeholders to their corresponding values
5. WHEN the same request builder pattern is used in batch operations, THE System SHALL support string formatting identically to transaction operations

### Requirement 8: Lambda Expression Support

**User Story:** As a developer, I want to use lambda expressions for type-safe updates and conditions in transaction and batch operations, so that I can leverage compile-time checking and IntelliSense.

#### Acceptance Criteria

1. WHEN a developer uses `Set(x => new UpdateModel { Value = "123" })` on an update builder within a transaction, THE System SHALL process the lambda expression and generate the appropriate update expression
2. WHEN a developer uses `Where(x => x.Status == "active")` on a condition check builder within a transaction, THE System SHALL process the lambda expression and generate the appropriate condition expression
3. WHEN the transaction or batch builder extracts the request, THE System SHALL preserve all expression attribute names and values generated from lambda expressions
4. WHEN lambda expressions reference properties marked for encryption, THE System SHALL apply field encryption before executing the transaction
5. WHEN the same request builder pattern is used in batch operations, THE System SHALL support lambda expressions identically to transaction operations where applicable

### Requirement 9: Source-Generated Key Methods

**User Story:** As a developer, I want source-generated strongly-typed key methods to work seamlessly in transaction and batch contexts, so that I can use methods like `Update(pk, sk)` without generic parameters.

#### Acceptance Criteria

1. WHEN a table has source-generated methods like `Put(entity)`, `Update(pk, sk)`, `Delete(pk, sk)`, and `Get(pk, sk)`, THE System SHALL allow these methods to return request builders usable in transactions and batches
2. WHEN a developer calls `table.Update(pk, sk).Set(...)` and passes the result to a transaction builder, THE System SHALL correctly extract the update request with the specified keys
3. WHEN a table supports multiple entity types with different key structures, THE System SHALL generate appropriate overloads for each entity type
4. WHEN source-generated methods are used in batch operations, THE System SHALL correctly identify the target table name from the builder
5. WHEN a developer uses source-generated `ConditionCheck(pk, sk)` methods, THE System SHALL return a properly configured condition check builder

### Requirement 10: Encryption Support in Transactions

**User Story:** As a developer, I want field encryption to work automatically in transaction operations, so that sensitive data is encrypted before being sent to DynamoDB.

#### Acceptance Criteria

1. WHEN an update builder within a transaction uses lambda expressions that reference encrypted properties, THE System SHALL identify parameters requiring encryption
2. WHEN the transaction write builder calls `ExecuteAsync()`, THE System SHALL encrypt all parameters marked as requiring encryption before building the final request
3. WHEN encryption is required but no `IFieldEncryptor` is configured, THE System SHALL throw an `InvalidOperationException` with a clear message indicating which properties require encryption
4. WHEN encryption fails for a parameter, THE System SHALL throw a `FieldEncryptionException` with details about the failure
5. WHEN the transaction contains multiple update operations with encrypted fields, THE System SHALL encrypt all parameters across all operations before execution

### Requirement 11: Transaction-Level Configuration

**User Story:** As a developer, I want to configure transaction-level settings like consumed capacity reporting and client request tokens, so that I can monitor and control transaction behavior.

#### Acceptance Criteria

1. WHEN a developer calls `ReturnConsumedCapacity()` on a transaction write builder, THE System SHALL set the return consumed capacity setting at the transaction level
2. WHEN a developer calls `WithClientRequestToken()` on a transaction write builder, THE System SHALL set the client request token for idempotency
3. WHEN a developer calls `ReturnItemCollectionMetrics()` on a transaction write builder, THE System SHALL set the return item collection metrics setting at the transaction level
4. WHEN transaction-level settings are configured, THE System SHALL NOT be affected by item-level settings from individual request builders
5. WHEN a transaction get builder is configured, THE System SHALL support `ReturnConsumedCapacity()` at the transaction level

### Requirement 12: Batch-Level Configuration

**User Story:** As a developer, I want to configure batch-level settings like consumed capacity reporting, so that I can monitor batch operation performance.

#### Acceptance Criteria

1. WHEN a developer calls `ReturnConsumedCapacity()` on a batch write builder, THE System SHALL set the return consumed capacity setting at the batch level
2. WHEN a developer calls `ReturnItemCollectionMetrics()` on a batch write builder, THE System SHALL set the return item collection metrics setting at the batch level
3. WHEN batch-level settings are configured, THE System SHALL NOT be affected by item-level settings from individual request builders
4. WHEN a batch get builder is configured, THE System SHALL support `ReturnConsumedCapacity()` at the batch level
5. WHEN batch operations are executed, THE System SHALL return consumed capacity information in the response when requested

### Requirement 13: Error Handling and Validation

**User Story:** As a developer, I want clear error messages when I misuse transaction or batch APIs, so that I can quickly identify and fix issues.

#### Acceptance Criteria

1. WHEN a batch write operation exceeds twenty-five items, THE System SHALL throw a `ValidationException` with a message indicating the limit and suggesting chunking
2. WHEN a batch get operation exceeds one hundred items, THE System SHALL throw a `ValidationException` with a message indicating the limit and suggesting chunking
3. WHEN a transaction write operation exceeds one hundred items, THE System SHALL throw a `ValidationException` with a message indicating the DynamoDB limit
4. WHEN a transaction get operation exceeds one hundred items, THE System SHALL throw a `ValidationException` with a message indicating the DynamoDB limit
5. WHEN a request builder is missing required configuration (such as table name or key), THE System SHALL throw an `InvalidOperationException` with a clear message indicating what is missing
6. WHEN encryption is required but not configured, THE System SHALL throw an `InvalidOperationException` with instructions on how to configure the field encryptor
7. WHEN a transaction fails due to a condition check failure, THE System SHALL preserve the original AWS SDK exception with all cancellation reason details

### Requirement 14: Client Configuration

**User Story:** As a developer, I want the DynamoDB client to be automatically inferred from request builders or explicitly specified, so that I can use scoped IAM credentials with STS tokens or select from multiple configured clients without extra boilerplate.

#### Acceptance Criteria

1. WHEN a developer adds the first request builder to a transaction or batch builder, THE System SHALL extract and store the DynamoDB client from that builder
2. WHEN subsequent request builders are added, THE System SHALL verify they use the same client instance as the first builder
3. WHEN request builders with different client instances are added to the same transaction or batch, THE System SHALL throw an `InvalidOperationException` with a message indicating that all operations must use the same client
4. WHEN a developer calls `WithClient(client)` on a transaction or batch builder, THE System SHALL use the specified client and ignore clients from request builders
5. WHEN a developer calls `ExecuteAsync(client)` on a transaction or batch builder, THE System SHALL use the provided client with highest precedence
6. WHEN a developer calls `ExecuteAsync()` without any client specified via `WithClient()` or parameter, THE System SHALL use the client extracted from the first added request builder
7. WHEN a developer calls `ExecuteAsync()` without any client and no request builders have been added, THE System SHALL throw an `InvalidOperationException` with a clear message

### Requirement 15: Logging and Diagnostics

**User Story:** As a developer, I want transaction and batch operations to log diagnostic information, so that I can troubleshoot issues and monitor performance.

#### Acceptance Criteria

1. WHEN a transaction write operation is executed, THE System SHALL log the number of operations and operation types at the Information level
2. WHEN a transaction write operation completes, THE System SHALL log the total consumed capacity at the Information level
3. WHEN a transaction write operation fails, THE System SHALL log the error with operation details at the Error level
4. WHEN encryption is performed on transaction parameters, THE System SHALL log encryption details at the Debug level without exposing sensitive values
5. WHEN batch operations are executed, THE System SHALL log the number of operations per table and total operations at the Information level
6. WHEN batch operations return unprocessed items, THE System SHALL log the count of unprocessed items at the Warning level
7. WHEN trace-level logging is enabled, THE System SHALL log detailed operation breakdowns including put, update, delete, and check counts

### Requirement 16: Transaction Get Response Deserialization

**User Story:** As a developer, I want to deserialize transaction get responses to strongly-typed entities in an AOT-safe manner, so that I can work with typed objects without reflection.

#### Acceptance Criteria

1. WHEN a transaction get operation completes, THE System SHALL return a response object that provides type-safe deserialization methods
2. WHEN a developer calls `GetItem<TEntity>(index)` on the response, THE System SHALL deserialize the item at the specified index using the source-generated `FromDynamoDb` method for that entity type
3. WHEN a developer calls `GetItems<TEntity>(params int[] indices)` on the response, THE System SHALL deserialize multiple items of the same type at the specified indices
4. WHEN a developer calls `GetItemsRange<TEntity>(startIndex, endIndex)` on the response, THE System SHALL deserialize a contiguous range of items of the same type
5. WHEN a developer calls `ExecuteAndMapAsync<T1, T2, ...>()` on the transaction get builder with up to eight type parameters, THE System SHALL execute the transaction and return a tuple with deserialized entities in order
6. WHEN an item at a requested index is null or missing, THE System SHALL return null for that item without throwing an exception
7. WHEN deserialization fails for an item, THE System SHALL throw a `DynamoDbMappingException` with details about which index failed and why

### Requirement 17: Batch Get Response Deserialization

**User Story:** As a developer, I want to deserialize batch get responses to strongly-typed entities in an AOT-safe manner, so that I can work with typed objects without reflection.

#### Acceptance Criteria

1. WHEN a batch get operation completes, THE System SHALL return a response object that provides type-safe deserialization methods
2. WHEN a developer calls `GetItem<TEntity>(index)` on the response, THE System SHALL deserialize the item at the specified index using the source-generated `FromDynamoDb` method for that entity type
3. WHEN a developer calls `GetItems<TEntity>(params int[] indices)` on the response, THE System SHALL deserialize multiple items of the same type at the specified indices
4. WHEN a developer calls `GetItemsRange<TEntity>(startIndex, endIndex)` on the response, THE System SHALL deserialize a contiguous range of items of the same type
5. WHEN a developer calls `ExecuteAndMapAsync<T1, T2, ...>()` on the batch get builder with up to eight type parameters, THE System SHALL execute the batch and return a tuple with deserialized entities in order
6. WHEN an item at a requested index is null or missing, THE System SHALL return null for that item without throwing an exception
7. WHEN the batch response contains unprocessed keys, THE System SHALL make them accessible via the response object for retry logic
8. WHEN deserialization fails for an item, THE System SHALL throw a `DynamoDbMappingException` with details about which index failed and why

### Requirement 18: Batch Write Encryption Support

**User Story:** As a developer, I want field encryption to work automatically in batch write operations when putting entities with encrypted fields, so that sensitive data is encrypted before being sent to DynamoDB.

#### Acceptance Criteria

1. WHEN a put item request builder is added to a batch write operation and the entity has encrypted fields, THE System SHALL ensure the entity was encrypted during the `ToDynamoDb` conversion
2. WHEN a batch write builder calls `ExecuteAsync()` with put operations containing encrypted fields, THE System SHALL execute the batch with the encrypted attribute values
3. WHEN encryption is required but no `IFieldEncryptor` is configured on the table, THE System SHALL throw an `InvalidOperationException` during the `Put(entity)` call with a clear message
4. WHEN the batch contains multiple put operations with encrypted fields across different tables, THE System SHALL handle encryption for each table's field encryptor configuration
5. WHEN batch write operations include only delete operations, THE System SHALL NOT perform any encryption processing
