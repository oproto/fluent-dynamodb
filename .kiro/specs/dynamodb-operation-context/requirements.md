# Requirements Document

## Introduction

This feature provides a centralized context mechanism for accessing DynamoDB operation metadata and response details. Applications using the FluentDynamoDb library need access to consumed capacity, item counts, raw AttributeValue collections, and pre/post operation values for monitoring, debugging, and auditing purposes. Since async methods cannot use `out` or `ref` parameters, an AsyncLocal-based context provides a clean solution for accessing this metadata after operations complete.

## Glossary

- **DynamoDbOperationContext**: A context object that captures metadata and response details from DynamoDB operations, accessible via AsyncLocal storage
- **FluentDynamoDb Library**: The Oproto.FluentDynamoDb library that provides fluent API wrappers for Amazon DynamoDB
- **Request Builder**: Classes like QueryRequestBuilder, GetItemRequestBuilder that build and execute DynamoDB operations
- **Extension Methods**: Methods like ToListAsync<T>, ExecuteAsync<T> that provide strongly-typed entity mapping
- **Raw Response**: The original AWS SDK response objects (QueryResponse, GetItemResponse, etc.) containing AttributeValue dictionaries
- **Consumed Capacity**: DynamoDB metrics indicating read/write capacity units consumed by an operation
- **Pre/Post Values**: Item attribute values before and after update/delete operations (when ReturnValues is configured)
- **AsyncLocal**: .NET mechanism for maintaining context across async call chains without explicit parameter passing
- **Encryption Context**: Existing AsyncLocal-based context used for encryption-related operations in the library

## Requirements

### Requirement 1

**User Story:** As a developer using FluentDynamoDb, I want to access DynamoDB operation metadata after calling extension methods, so that I can monitor consumed capacity and performance metrics without changing my method signatures.

#### Acceptance Criteria

1. WHEN a developer executes a Query operation using ToListAsync<T>, THE DynamoDbOperationContext SHALL capture the consumed capacity information from the QueryResponse
2. WHEN a developer executes a GetItem operation using ExecuteAsync<T>, THE DynamoDbOperationContext SHALL capture the consumed capacity information from the GetItemResponse
3. WHEN a developer accesses DynamoDbOperationContext.Current after an operation completes, THE DynamoDbOperationContext SHALL provide the consumed capacity value from the most recent operation
4. WHEN a developer executes a Scan operation using ToListAsync<T>, THE DynamoDbOperationContext SHALL capture both the item count and scanned count from the ScanResponse
5. WHEN a developer executes multiple sequential operations, THE DynamoDbOperationContext SHALL contain metadata only from the most recently completed operation

### Requirement 2

**User Story:** As a developer debugging DynamoDB queries, I want to access the raw AttributeValue collections returned by DynamoDB, so that I can inspect the original data structure before deserialization.

#### Acceptance Criteria

1. WHEN a developer executes a Query operation that returns items, THE DynamoDbOperationContext SHALL store the raw Items collection from the QueryResponse
2. WHEN a developer executes a GetItem operation that returns an item, THE DynamoDbOperationContext SHALL store the raw Item dictionary from the GetItemResponse
3. WHEN a developer accesses DynamoDbOperationContext.Current.RawItems after a Query operation, THE DynamoDbOperationContext SHALL provide the List<Dictionary<string, AttributeValue>> from the response
4. WHEN a developer accesses DynamoDbOperationContext.Current.RawItem after a GetItem operation, THE DynamoDbOperationContext SHALL provide the Dictionary<string, AttributeValue> from the response
5. WHEN a Query operation returns zero items, THE DynamoDbOperationContext SHALL store an empty collection rather than null

### Requirement 3

**User Story:** As a developer implementing auditing, I want to access pre-operation and post-operation attribute values for update and delete operations, so that I can track what changed without making additional queries.

#### Acceptance Criteria

1. WHEN a developer executes an UpdateItem operation with ReturnValues set to ALL_OLD, THE DynamoDbOperationContext SHALL capture the Attributes dictionary from the UpdateItemResponse as PreOperationValues
2. WHEN a developer executes an UpdateItem operation with ReturnValues set to ALL_NEW, THE DynamoDbOperationContext SHALL capture the Attributes dictionary from the UpdateItemResponse as PostOperationValues
3. WHEN a developer executes a DeleteItem operation with ReturnValues set to ALL_OLD, THE DynamoDbOperationContext SHALL capture the Attributes dictionary from the DeleteItemResponse as PreOperationValues
4. WHEN a developer executes a PutItem operation with ReturnValues set to ALL_OLD, THE DynamoDbOperationContext SHALL capture the Attributes dictionary from the PutItemResponse as PreOperationValues
5. WHEN ReturnValues is not configured for an operation, THE DynamoDbOperationContext SHALL set PreOperationValues and PostOperationValues to null

### Requirement 4

**User Story:** As a developer working with paginated queries, I want to access pagination tokens and item counts, so that I can implement efficient pagination without parsing response objects manually.

#### Acceptance Criteria

1. WHEN a Query operation returns a LastEvaluatedKey, THE DynamoDbOperationContext SHALL capture the LastEvaluatedKey dictionary from the QueryResponse
2. WHEN a Scan operation returns a LastEvaluatedKey, THE DynamoDbOperationContext SHALL capture the LastEvaluatedKey dictionary from the ScanResponse
3. WHEN a Query operation completes without additional pages, THE DynamoDbOperationContext SHALL set LastEvaluatedKey to null
4. WHEN a developer accesses DynamoDbOperationContext.Current.ItemCount after a Query operation, THE DynamoDbOperationContext SHALL provide the Count property from the QueryResponse
5. WHEN a developer accesses DynamoDbOperationContext.Current.ScannedCount after a Scan operation, THE DynamoDbOperationContext SHALL provide the ScannedCount property from the ScanResponse

### Requirement 5

**User Story:** As a developer using the library, I want the operation context to be automatically scoped to each operation, so that concurrent operations do not interfere with each other's metadata.

#### Acceptance Criteria

1. WHEN multiple operations execute concurrently on different async contexts, THE DynamoDbOperationContext SHALL maintain separate context instances for each async flow
2. WHEN an operation completes, THE DynamoDbOperationContext SHALL populate the context before the ExecuteAsync method returns to the caller
3. WHEN a developer accesses DynamoDbOperationContext.Current before any operation executes, THE DynamoDbOperationContext SHALL return null
4. WHEN an operation throws an exception, THE DynamoDbOperationContext SHALL not populate the context with partial data
5. WHEN nested operations occur within the same async context, THE DynamoDbOperationContext SHALL contain metadata from the most recently completed operation

### Requirement 6

**User Story:** As a developer needing to deserialize raw AttributeValue data, I want helper methods to convert raw items back to strongly-typed entities, so that I can work with both raw and typed data as needed.

#### Acceptance Criteria

1. WHEN a developer calls DynamoDbOperationContext.Current.DeserializeRawItem<T>(), THE DynamoDbOperationContext SHALL use the entity's FromDynamoDb method to convert the raw item to type T
2. WHEN a developer calls DynamoDbOperationContext.Current.DeserializeRawItems<T>(), THE DynamoDbOperationContext SHALL convert all raw items in the collection to a List<T>
3. WHEN a developer calls DeserializePreOperationValue<T>(), THE DynamoDbOperationContext SHALL convert the PreOperationValues dictionary to type T
4. WHEN a developer calls DeserializePostOperationValue<T>(), THE DynamoDbOperationContext SHALL convert the PostOperationValues dictionary to type T
5. WHEN raw data is null and a deserialization method is called, THE DynamoDbOperationContext SHALL return null without throwing an exception

### Requirement 7

**User Story:** As a developer maintaining the library, I want to consolidate the existing encryption AsyncLocal context into the new operation context, so that we have a single unified context mechanism.

#### Acceptance Criteria

1. WHEN the DynamoDbOperationContext is created, THE DynamoDbOperationContext SHALL include properties for encryption-related context data
2. WHEN encryption operations occur, THE DynamoDbOperationContext SHALL store encryption context information in the same AsyncLocal instance as operation metadata
3. WHEN a developer accesses encryption context, THE DynamoDbOperationContext SHALL provide the same interface as the existing encryption AsyncLocal implementation
4. WHEN the migration is complete, THE Library SHALL remove the separate encryption AsyncLocal implementation
5. WHEN existing code accesses the old encryption context, THE Library SHALL provide backward compatibility through the unified context

### Requirement 8

**User Story:** As a developer concerned about memory usage, I want to understand the memory implications of storing raw AttributeValue collections, so that I can make informed decisions about production usage.

#### Acceptance Criteria

1. WHEN raw items are captured in the context, THE DynamoDbOperationContext SHALL store references to the existing response objects rather than creating deep copies
2. WHEN an operation completes and the context is no longer referenced, THE DynamoDbOperationContext SHALL allow garbage collection of the captured data
3. WHEN a new operation executes, THE DynamoDbOperationContext SHALL replace the previous context data rather than accumulating multiple operation results
4. WHEN a developer accesses DynamoDbOperationContext.Current, THE Documentation SHALL clearly explain that raw data is held in memory until the next operation or context disposal
5. WHEN large query results are returned, THE DynamoDbOperationContext SHALL store the same data already present in the response object without additional allocation
