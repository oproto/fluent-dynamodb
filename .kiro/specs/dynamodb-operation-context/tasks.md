# Implementation Plan

- [x] 1. Create core context infrastructure
  - Create `OperationContextData` class in `Oproto.FluentDynamoDb/Storage/` with all properties for metadata, raw data, and deserialization helpers
  - Create `DynamoDbOperationContext` static class in `Oproto.FluentDynamoDb/Storage/` with AsyncLocal storage and public API
  - Add helper methods to access IAmazonDynamoDB client from request builders (needed for extension methods to call AWS SDK directly)
  - _Requirements: 1.1, 1.2, 1.3, 1.4, 1.5, 5.1, 5.2, 5.3, 5.4, 5.5_

- [x] 2. Implement Primary API extension methods for Query operations
  - [x] 2.1 Implement `ToListAsync<T>()` in EnhancedExecuteAsyncExtensions that calls AWS SDK directly and populates context
    - Call `QueryAsync()` directly instead of `ExecuteAsync()`
    - Populate `DynamoDbOperationContext.Current` with QueryResponse metadata
    - Return `List<T>` (POCO list)
    - _Requirements: 1.1, 2.1, 2.2, 2.3, 2.5, 4.1, 4.4_
  
  - [x] 2.2 Implement `ToCompositeEntityAsync<T>()` with context population
    - Call `QueryAsync()` directly and populate context
    - Return single composite entity `T?`
    - _Requirements: 1.1, 2.1, 2.2, 2.3, 2.5_
  
  - [x] 2.3 Implement `ToCompositeEntityListAsync<T>()` with context population
    - Call `QueryAsync()` directly and populate context
    - Return `List<T>` of composite entities
    - _Requirements: 1.1, 2.1, 2.2, 2.3, 2.5_
  
  - [x] 2.4 Add blob provider overloads for all Query extension methods
    - Add async overloads accepting `IBlobStorageProvider`
    - Handle blob reference deserialization
    - _Requirements: 1.1, 2.1, 2.2, 2.3_

- [x] 3. Implement Primary API extension methods for GetItem operations
  - [x] 3.1 Implement `GetItemAsync<T>()` extension method (replaces `ExecuteAsync<T>()`)
    - Call `GetItemAsync()` on AWS SDK directly instead of builder's ExecuteAsync
    - Populate `DynamoDbOperationContext.Current` with GetItemResponse metadata
    - Return `T?` (nullable POCO)
    - _Requirements: 1.2, 2.2, 2.4_
  
  - [x] 3.2 Add blob provider overload for GetItemAsync
    - Add async overload accepting `IBlobStorageProvider`
    - Handle blob reference deserialization
    - _Requirements: 1.2, 2.2, 2.4_

- [x] 4. Implement Primary API extension methods for Scan operations
  - [x] 4.1 Implement `ToListAsync<T>()` for ScanRequestBuilder with context population
    - Call `ScanAsync()` directly and populate context
    - Return `List<T>`
    - _Requirements: 1.4, 2.1, 2.2, 2.3, 2.5, 4.5_
  
  - [x] 4.2 Implement `ToCompositeEntityListAsync<T>()` for ScanRequestBuilder with context population
    - Call `ScanAsync()` directly and populate context
    - Return `List<T>` of composite entities
    - _Requirements: 1.4, 2.1, 2.2, 2.3, 2.5_
  
  - [x] 4.3 Add blob provider overloads for Scan extension methods
    - Add async overloads accepting `IBlobStorageProvider`
    - _Requirements: 1.4, 2.1, 2.2, 2.3_

- [x] 5. Implement Primary API extension methods for write operations
  - [x] 5.1 Implement `PutAsync<T>()` extension method
    - Call `PutItemAsync()` on AWS SDK directly
    - Populate `DynamoDbOperationContext.Current` with PutItemResponse metadata including PreOperationValues
    - Return `void`
    - _Requirements: 3.4, 8.1, 8.2, 8.3_
  
  - [x] 5.2 Implement `UpdateAsync()` extension method
    - Call `UpdateItemAsync()` on AWS SDK directly
    - Populate `DynamoDbOperationContext.Current` with UpdateItemResponse metadata including Pre/PostOperationValues
    - Return `void`
    - _Requirements: 3.1, 3.2, 3.5, 8.1, 8.2, 8.3_
  
  - [x] 5.3 Implement `DeleteAsync()` extension method
    - Call `DeleteItemAsync()` on AWS SDK directly
    - Populate `DynamoDbOperationContext.Current` with DeleteItemResponse metadata including PreOperationValues
    - Return `void`
    - _Requirements: 3.3, 3.5, 8.1, 8.2, 8.3_
  
  - [x] 5.4 Add blob provider overloads for write operations
    - Add async overloads for PutAsync accepting `IBlobStorageProvider`
    - Handle blob reference serialization
    - _Requirements: 3.1, 3.2, 3.3, 3.4_

- [x] 6. Implement Advanced API (ToDynamoDbResponseAsync)
  - [x] 6.1 Rename `ExecuteAsync()` to `ToDynamoDbResponseAsync()` on QueryRequestBuilder
    - Keep existing implementation (calls AWS SDK)
    - Do NOT populate context
    - Return raw `QueryResponse`
    - _Requirements: 8.1, 8.2, 8.3_
  
  - [x] 6.2 Rename `ExecuteAsync()` to `ToDynamoDbResponseAsync()` on GetItemRequestBuilder
    - Keep existing implementation
    - Do NOT populate context
    - Return raw `GetItemResponse`
    - _Requirements: 8.1, 8.2, 8.3_
  
  - [x] 6.3 Rename `ExecuteAsync()` to `ToDynamoDbResponseAsync()` on ScanRequestBuilder
    - Keep existing implementation
    - Do NOT populate context
    - Return raw `ScanResponse`
    - _Requirements: 8.1, 8.2, 8.3_
  
  - [x] 6.4 Rename `ExecuteAsync()` to `ToDynamoDbResponseAsync()` on UpdateItemRequestBuilder
    - Keep existing implementation
    - Do NOT populate context
    - Return raw `UpdateItemResponse`
    - _Requirements: 8.1, 8.2, 8.3_
  
  - [x] 6.5 Rename `ExecuteAsync()` to `ToDynamoDbResponseAsync()` on DeleteItemRequestBuilder
    - Keep existing implementation
    - Do NOT populate context
    - Return raw `DeleteItemResponse`
    - _Requirements: 8.1, 8.2, 8.3_
  
  - [x] 6.6 Rename `ExecuteAsync()` to `ToDynamoDbResponseAsync()` on PutItemRequestBuilder
    - Keep existing implementation
    - Do NOT populate context
    - Return raw `PutItemResponse`
    - _Requirements: 8.1, 8.2, 8.3_

- [x] 7. Create AWS response extension methods
  - [x] 7.1 Create `DynamoDbResponseExtensions` class in `Oproto.FluentDynamoDb/Requests/Extensions/`
    - Add XML documentation explaining these are for advanced API users
    - _Requirements: 6.1, 6.2, 6.3, 6.4_
  
  - [x] 7.2 Implement extension methods for QueryResponse
    - `ToList<T>()` - converts Items to List<T>
    - `ToCompositeEntityList<T>()` - converts Items to composite entities
    - `ToCompositeEntity<T>()` - converts Items to single composite entity
    - _Requirements: 6.1, 6.2_
  
  - [x] 7.3 Implement extension methods for ScanResponse
    - `ToList<T>()` - converts Items to List<T>
    - `ToCompositeEntityList<T>()` - converts Items to composite entities
    - _Requirements: 6.1, 6.2_
  
  - [x] 7.4 Implement extension methods for GetItemResponse
    - `ToEntity<T>()` - converts Item to T?
    - _Requirements: 6.1, 6.2_
  
  - [x] 7.5 Implement extension methods for UpdateItemResponse
    - `ToPreOperationEntity<T>()` - converts Attributes (ALL_OLD/UPDATED_OLD) to T?
    - `ToPostOperationEntity<T>()` - converts Attributes (ALL_NEW/UPDATED_NEW) to T?
    - _Requirements: 6.3, 6.4_
  
  - [x] 7.6 Implement extension methods for DeleteItemResponse
    - `ToPreOperationEntity<T>()` - converts Attributes (ALL_OLD) to T?
    - _Requirements: 6.3, 6.4_
  
  - [x] 7.7 Implement extension methods for PutItemResponse
    - `ToPreOperationEntity<T>()` - converts Attributes (ALL_OLD) to T?
    - _Requirements: 6.3, 6.4_
  
  - [x] 7.8 Add blob provider async overloads for all response extension methods
    - Add async versions accepting `IBlobStorageProvider` for entities with blob references
    - _Requirements: 6.1, 6.2, 6.3, 6.4_

- [x] 8. Remove deprecated code
  - [x] 8.1 Delete `GetItemResponse<T>` class from `Oproto.FluentDynamoDb/Storage/`
    - This custom wrapper is replaced by POCO + context
    - _Requirements: 8.1, 8.2, 8.3_
  
  - [x] 8.2 Delete `QueryResponse<T>` class if it exists
    - Check if this class exists and remove it
    - _Requirements: 8.1, 8.2, 8.3_
  
  - [x] 8.3 Delete `ScanResponse<T>` class if it exists
    - Check if this class exists and remove it
    - _Requirements: 8.1, 8.2, 8.3_
  
  - [x] 8.4 Delete custom `ResponseMetadata` class if it exists
    - Check if custom ResponseMetadata exists (separate from AWS SDK's)
    - Use AWS SDK's ResponseMetadata instead
    - _Requirements: 8.1, 8.2, 8.3_
  
  - [x] 8.5 Mark old `ExecuteAsync()` methods as obsolete with migration guidance
    - Add `[Obsolete]` attributes with clear messages pointing to new methods
    - Include migration examples in obsolete messages
    - _Requirements: 8.1, 8.2, 8.3_

- [x] 9. Migrate encryption context to unified context
  - [x] 9.1 Update `EncryptionContext` class to delegate to `DynamoDbOperationContext`
    - Change `Current` property to get/set `DynamoDbOperationContext.EncryptionContextId`
    - Update `GetEffectiveContext()` to use unified context
    - Mark class as `[Obsolete]` with migration guidance
    - _Requirements: 7.1, 7.2, 7.3, 7.4, 7.5_
  
  - [x] 9.2 Update internal usages of EncryptionContext
    - Find all internal code using `EncryptionContext.Current`
    - Update to use `DynamoDbOperationContext.EncryptionContextId` directly
    - _Requirements: 7.1, 7.2, 7.3, 7.4, 7.5_

- [ ] 10. Add batch and transaction support
  - [ ] 10.1 Implement Primary API methods for BatchGetItem
    - Add extension methods that populate context
    - Return POCOs
    - _Requirements: 1.1, 2.1, 2.2, 2.3_
  
  - [ ] 10.2 Implement Primary API methods for BatchWriteItem
    - Add extension methods that populate context
    - Return void
    - _Requirements: 8.1, 8.2, 8.3_
  
  - [ ] 10.3 Implement Primary API methods for TransactGetItems
    - Add extension methods that populate context
    - Return POCOs
    - _Requirements: 1.1, 2.1, 2.2, 2.3_
  
  - [ ] 10.4 Implement Primary API methods for TransactWriteItems
    - Add extension methods that populate context
    - Return void
    - _Requirements: 8.1, 8.2, 8.3_
  
  - [ ] 10.5 Add ToDynamoDbResponseAsync for batch/transaction builders
    - Rename ExecuteAsync to ToDynamoDbResponseAsync
    - Do NOT populate context
    - _Requirements: 8.1, 8.2, 8.3_

- [ ] 11. Update FluentResults integration
  - [ ] 11.1 Rename `ExecuteAsyncResult<T>()` to `GetItemAsyncResult<T>()` for GetItemRequestBuilder
    - Update to call `GetItemAsync<T>()` instead of `ExecuteAsync<T>()`
    - Return `Result<T?>` instead of `Result<GetItemResponse<T>>`
    - _Requirements: 1.2, 2.2, 2.4_
  
  - [ ] 11.2 Update `ToListAsyncResult<T>()` methods
    - Verify they work with updated ToListAsync implementations
    - Update return types if needed
    - _Requirements: 1.1, 2.1, 2.2, 2.3_
  
  - [ ] 11.3 Update `ToCompositeEntityAsyncResult<T>()` methods
    - Verify they work with updated implementations
    - _Requirements: 1.1, 2.1, 2.2, 2.3_
  
  - [ ] 11.4 Update `ToCompositeEntityListAsyncResult<T>()` methods
    - Verify they work with updated implementations
    - _Requirements: 1.1, 2.1, 2.2, 2.3_
  
  - [ ] 11.5 Add `PutAsyncResult<T>()` extension method
    - Wrap `PutAsync<T>()` in Result
    - Return `Result` (not Result<T>)
    - _Requirements: 3.4, 8.1, 8.2, 8.3_
  
  - [ ] 11.6 Add `UpdateAsyncResult()` extension method
    - Wrap `UpdateAsync()` in Result
    - Return `Result`
    - _Requirements: 3.1, 3.2, 8.1, 8.2, 8.3_
  
  - [ ] 11.7 Add `DeleteAsyncResult()` extension method
    - Wrap `DeleteAsync()` in Result
    - Return `Result`
    - _Requirements: 3.3, 8.1, 8.2, 8.3_
  
  - [ ] 11.8 Remove deprecated FluentResults methods
    - Remove `WithItemResult<T>()`
    - Remove old `ExecuteAsyncResult<T>()` for PutItemRequestBuilder
    - _Requirements: 8.1, 8.2, 8.3_

- [ ] 12. Update unit tests
  - [ ] 12.1 Update GetItemRequestBuilder tests
    - Update tests to use `GetItemAsync<T>()` instead of `ExecuteAsync<T>()`
    - Add tests for context population
    - _Requirements: 1.2, 2.2, 2.4, 5.1, 5.2, 5.3, 5.4_
  
  - [ ] 12.2 Update QueryRequestBuilder tests
    - Update tests to use new extension methods
    - Add tests for context population
    - Add tests for context isolation
    - _Requirements: 1.1, 2.1, 2.2, 2.3, 4.1, 4.4, 5.1, 5.2, 5.3, 5.4, 5.5_
  
  - [ ] 12.3 Update ScanRequestBuilder tests
    - Update tests to use new extension methods
    - Add tests for context population
    - _Requirements: 1.4, 2.1, 2.2, 2.3, 4.5, 5.1, 5.2, 5.3, 5.4, 5.5_
  
  - [ ] 12.4 Update PutItemRequestBuilder tests
    - Update tests to use `PutAsync<T>()`
    - Add tests for context population
    - _Requirements: 3.4, 8.1, 8.2, 8.3_
  
  - [ ] 12.5 Update UpdateItemRequestBuilder tests
    - Update tests to use `UpdateAsync()`
    - Add tests for context population with Pre/PostOperationValues
    - _Requirements: 3.1, 3.2, 8.1, 8.2, 8.3_
  
  - [ ] 12.6 Update DeleteItemRequestBuilder tests
    - Update tests to use `DeleteAsync()`
    - Add tests for context population with PreOperationValues
    - _Requirements: 3.3, 8.1, 8.2, 8.3_
  
  - [ ] 12.7 Add context deserialization tests
    - Test `DeserializeRawItem<T>()`
    - Test `DeserializeRawItems<T>()`
    - Test `DeserializePreOperationValue<T>()`
    - Test `DeserializePostOperationValue<T>()`
    - _Requirements: 6.1, 6.2, 6.3, 6.4, 6.5_
  
  - [ ] 12.8 Add context isolation tests
    - Test concurrent operations have separate contexts
    - Test context flows through async calls
    - Test context doesn't leak across async boundaries
    - _Requirements: 5.1, 5.2, 5.3, 5.4, 5.5_
  
  - [ ] 12.9 Add encryption context migration tests
    - Test backward compatibility with EncryptionContext
    - Test delegation to unified context
    - _Requirements: 7.1, 7.2, 7.3, 7.4, 7.5_
  
  - [ ] 12.10 Update FluentResults tests
    - Update all FluentResults extension method tests
    - Test new methods (PutAsyncResult, UpdateAsyncResult, DeleteAsyncResult)
    - _Requirements: 1.1, 1.2, 3.1, 3.2, 3.3, 3.4_
  
  - [ ] 12.11 Add AWS response extension method tests
    - Test all ToList, ToEntity, ToPreOperationEntity, ToPostOperationEntity methods
    - Test blob provider overloads
    - _Requirements: 6.1, 6.2, 6.3, 6.4_

- [ ] 13. Update integration tests
  - [ ] 13.1 Update integration tests to use new API
    - Replace all ExecuteAsync calls with new methods
    - _Requirements: 1.1, 1.2, 3.1, 3.2, 3.3, 3.4_
  
  - [ ] 13.2 Add integration tests for context access
    - Test real operations populate context correctly
    - Test metadata accuracy
    - _Requirements: 1.1, 1.2, 1.3, 1.4, 1.5, 2.1, 2.2, 2.3, 2.4, 2.5, 3.1, 3.2, 3.3, 3.4, 3.5, 4.1, 4.2, 4.3, 4.4, 4.5_

- [ ] 14. Update documentation and examples
  - [ ] 14.1 Update all code examples in XML documentation
    - Update examples to use new API
    - Add examples showing context access
    - _Requirements: All_
  
  - [ ] 14.2 Update README.md with migration guide
    - Document breaking changes
    - Provide before/after examples
    - Explain new context access pattern
    - _Requirements: All_
  
  - [ ] 14.3 Update example projects
    - Update all example code to use new API
    - Add examples demonstrating context usage
    - _Requirements: All_
  
  - [ ] 14.4 Update FluentResults README
    - Document new FluentResults methods
    - Update examples
    - _Requirements: 1.1, 1.2, 3.1, 3.2, 3.3, 3.4_
  
  - [ ] 14.5 Create migration guide document
    - Detailed step-by-step migration instructions
    - Common patterns and their replacements
    - Troubleshooting section
    - _Requirements: All_
