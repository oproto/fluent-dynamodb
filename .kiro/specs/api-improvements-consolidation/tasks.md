# Implementation Plan

- [x] 1. Make request builders generic and remove redundant type parameters
  - Convert QueryRequestBuilder to QueryRequestBuilder<TEntity>
  - Convert ScanRequestBuilder to ScanRequestBuilder<TEntity>
  - Convert GetItemRequestBuilder to GetItemRequestBuilder<TEntity>
  - Convert UpdateItemRequestBuilder to UpdateItemRequestBuilder<TEntity>
  - Convert DeleteItemRequestBuilder to DeleteItemRequestBuilder<TEntity>
  - Remove type parameters from all chained methods (Where, WithFilter, etc.)
  - Update return types to maintain generic type through chain
  - _Requirements: 1, 2, 3, 4, 7, 11, 24_

- [x] 2. Update source generator to emit generic builders
  - Modify table class generation to return QueryRequestBuilder<TEntity>
  - Modify table class generation to return ScanRequestBuilder<TEntity>
  - Modify table class generation to return GetItemRequestBuilder<TEntity>
  - Modify table class generation to return UpdateItemRequestBuilder<TEntity>
  - Update index generation to return generic builders
  - _Requirements: 8, 11_

- [x] 3. Add LINQ expression overloads to source-generated tables
  - Generate Query(Expression<Func<TEntity, bool>> keyCondition) overload
  - Generate Query(Expression<Func<TEntity, bool>> keyCondition, Expression<Func<TEntity, bool>> filterCondition) overload
  - Generate Scan(Expression<Func<TEntity, bool>> filterCondition) overload
  - Wire overloads to call existing Query()/Scan() methods and chain Where/WithFilter
  - _Requirements: 15_

- [x] 4. Add LINQ expression overloads to source-generated indexes
  - Generate index Query(Expression<Func<TEntity, bool>> keyCondition) overload
  - Generate index Query(Expression<Func<TEntity, bool>> keyCondition, Expression<Func<TEntity, bool>> filterCondition) overload
  - Ensure IndexName is automatically set in generated code
  - _Requirements: 19_

- [x] 4.2 Make PutItemRequestBuilder generic and add entity overload
  - Convert PutItemRequestBuilder to PutItemRequestBuilder<TEntity>
  - Add WithItem(TEntity entity) method that calls TEntity.ToDynamoDb<TEntity>(entity)
  - Keep existing WithItem(Dictionary<string, AttributeValue>) for backward compatibility
  - Update DynamoDbTableBase.Put() to be generic: Put<TEntity>()
  - Update IDynamoDbTable.Put() to be generic or remove from interface
  - Update all chained methods to return PutItemRequestBuilder<TEntity>
  - Update source generator to emit Put<TEntity>() methods
  - Update extension methods (EncryptionExtensions, WithClientExtensions) to support generic PutItemRequestBuilder
  - _Requirements: 1, 2, 3, 4, 7, 11, 24_

- [ ] 5. Remove non-functional QueryAsync methods from DynamoDbIndex
  - Remove QueryAsync(Action<QueryRequestBuilder>) from DynamoDbIndex<TDefault>
  - Remove QueryAsync<TResult>(Action<QueryRequestBuilder>) from DynamoDbIndex<TDefault>
  - Update any documentation referencing these methods
  - _Requirements: 20_

- [ ] 6. Add Format property to DynamoDbAttribute
  - Add Format property to DynamoDbAttributeAttribute class
  - Update XML documentation with format examples
  - _Requirements: 17_

- [ ] 7. Update source generator to emit Format in property metadata
  - Read Format property from DynamoDbAttribute during analysis
  - Emit Format value in generated PropertyMetadata
  - _Requirements: 17_

- [ ] 8. Enhance PropertyMetadata with security and format fields
  - Add Format property to PropertyMetadata
  - Add IsSensitive property to PropertyMetadata
  - Add IsEncrypted property to PropertyMetadata
  - Add EncryptionContext property to PropertyMetadata
  - _Requirements: 16, 17, 18_

- [ ] 9. Update source generator to emit security flags in metadata
  - Detect [Sensitive] attribute during analysis
  - Detect [Encrypted] attribute during analysis
  - Emit IsSensitive flag in generated PropertyMetadata
  - Emit IsEncrypted flag in generated PropertyMetadata
  - _Requirements: 16, 18_

- [ ] 10. Enhance ExpressionTranslator to apply format strings
  - Add ProcessValue method to apply formatting
  - Integrate format application into value translation
  - Support standard .NET format strings (DateTime, decimal, etc.)
  - _Requirements: 17_

- [ ] 11. Enhance ExpressionTranslator to redact sensitive data in logs
  - Check IsSensitive flag during value processing
  - Replace sensitive values with "[REDACTED]" in log messages
  - Ensure redaction applies to all log levels
  - Preserve property names in logs while redacting values
  - _Requirements: 16_

- [ ] 12. Enhance ExpressionTranslator to handle encrypted fields
  - Check IsEncrypted flag during value processing
  - Call IFieldEncryptor.Encrypt for encrypted field values
  - Validate encryption mode (deterministic vs non-deterministic)
  - Throw clear exception for non-deterministic encryption in equality comparisons
  - _Requirements: 18_

- [ ] 13. Add EncryptionMode to EncryptedAttribute
  - Add EncryptionMode enum (Deterministic, NonDeterministic)
  - Add EncryptionMode property to EncryptedAttribute
  - Update documentation with encryption mode examples
  - _Requirements: 18_

- [ ] 14. Verify discriminator usage in ToCompoundEntity
  - Review ToCompoundEntity implementation
  - Ensure discriminator value is used for type resolution
  - Ensure DiscriminatorMismatchException is thrown for invalid discriminators
  - Ensure exception is thrown for missing discriminators when expected
  - _Requirements: 21_

- [ ] 15. Add unit tests for generic builder type flow
  - Test Query<T>().Where().ToListAsync() returns List<T>
  - Test Scan<T>().WithFilter().ToListAsync() returns List<T>
  - Test Get<T>(key).ExecuteAsync() returns GetItemResponse<T>
  - Test Update<T>(key).Set().ExecuteAsync() works without type parameters
  - Test compiler errors for missing initial type parameter
  - _Requirements: 1, 2, 3, 4, 24_

- [ ] 16. Add unit tests for LINQ expression overloads
  - Test Query(x => x.Pk == value) configures key condition
  - Test Query(x => x.Pk == value, x => x.Status == "ACTIVE") configures both
  - Test Scan(x => x.Amount > 100) configures filter
  - Test index.Query(x => x.Gsi1Pk == value) sets IndexName
  - Test chaining additional methods after LINQ overload
  - _Requirements: 15, 19_

- [ ] 17. Add unit tests for sensitive data redaction
  - Test [Sensitive] property values are redacted in logs
  - Test non-sensitive property values are not redacted
  - Test redaction applies to text expressions
  - Test redaction applies to format string expressions
  - Test redaction applies to LINQ expressions
  - _Requirements: 16_

- [ ] 18. Add unit tests for format string application
  - Test DateTime format is applied in LINQ expressions
  - Test decimal format is applied in LINQ expressions
  - Test format is applied in text expressions
  - Test format is applied in format string expressions
  - Test missing format uses default serialization
  - _Requirements: 17_

- [ ] 19. Add unit tests for encryption in expressions
  - Test encrypted field values are encrypted in queries
  - Test deterministic encryption allows equality comparisons
  - Test non-deterministic encryption throws exception
  - Test encryption context is passed correctly
  - Test encrypted values are not logged (even redacted)
  - _Requirements: 18_

- [ ] 20. Add unit tests for discriminator validation
  - Test ToCompoundEntity uses discriminator for type resolution
  - Test DiscriminatorMismatchException for invalid discriminator
  - Test exception for missing discriminator when expected
  - Test correct entity type is deserialized based on discriminator
  - _Requirements: 21_

- [ ] 21. Add integration tests for all operations with text expressions
  - Test Get with text expression
  - Test Put with text expression
  - Test Update with text expression
  - Test Delete with text expression
  - Test Query with text expression
  - Test Scan with text expression
  - Test BatchGet with text expression
  - Test BatchWrite with text expression
  - Test TransactWrite with text expression
  - Test TransactGet with text expression
  - Verify data round-trips correctly
  - _Requirements: 23_

- [ ] 22. Add integration tests for all operations with format string expressions
  - Test Get with format string expression
  - Test Put with format string expression
  - Test Update with format string expression
  - Test Delete with format string expression
  - Test Query with format string expression
  - Test Scan with format string expression
  - Test BatchGet with format string expression
  - Test BatchWrite with format string expression
  - Test TransactWrite with format string expression
  - Test TransactGet with format string expression
  - Verify data round-trips correctly
  - _Requirements: 23_

- [ ] 23. Add integration tests for all operations with LINQ expressions
  - Test Get with LINQ expression
  - Test Put with LINQ expression
  - Test Update with LINQ expression
  - Test Delete with LINQ expression
  - Test Query with LINQ expression
  - Test Scan with LINQ expression
  - Test BatchGet with LINQ expression
  - Test BatchWrite with LINQ expression
  - Test TransactWrite with LINQ expression
  - Test TransactGet with LINQ expression
  - Verify data round-trips correctly
  - _Requirements: 23_

- [ ] 24. Add integration tests for pagination with all expression modes
  - Test Query pagination with text expressions
  - Test Query pagination with format string expressions
  - Test Query pagination with LINQ expressions
  - Test Scan pagination with text expressions
  - Test Scan pagination with format string expressions
  - Test Scan pagination with LINQ expressions
  - Verify LastEvaluatedKey is handled correctly
  - Verify all pages return correct data
  - _Requirements: 23_

- [ ] 25. Add integration test for complete workflow with all features
  - Test query with LINQ expression, format, encryption, sensitive data, and pagination
  - Verify results are correct
  - Verify sensitive data was redacted in logs
  - Verify encrypted fields were handled correctly
  - Verify formatting was applied
  - Verify pagination works correctly
  - _Requirements: 23_

- [ ] 26. Update documentation examples to use method-based API
  - Replace .Get with .Get() in all examples
  - Replace .Query with .Query() in all examples
  - Replace .Scan with .Scan() in all examples
  - Replace .Put with .Put() in all examples
  - Replace .Update with .Update() in all examples
  - _Requirements: 22_

- [ ] 27. Add LINQ expression examples to documentation
  - Add Query(expression) examples
  - Add Query(expression, filter) examples
  - Add Scan(expression) examples
  - Add index Query(expression) examples
  - Show side-by-side comparison of text, format, and LINQ expressions
  - _Requirements: 22_

- [ ] 28. Create migration guide for API changes
  - Document type parameter removal (breaking change)
  - Provide before/after examples for common patterns
  - Include regex patterns for automated refactoring
  - Document new LINQ expression overloads
  - Document format string support in DynamoDbAttribute
  - Document sensitive data redaction behavior
  - Document encryption in expressions
  - _Requirements: 22, 26_

- [ ] 29. Update API reference documentation
  - Update QueryRequestBuilder documentation
  - Update ScanRequestBuilder documentation
  - Update GetItemRequestBuilder documentation
  - Update UpdateItemRequestBuilder documentation
  - Update DynamoDbAttribute documentation
  - Update ExpressionTranslator documentation
  - _Requirements: 22_

- [ ] 30. Run full test suite and verify no regressions
  - Run all unit tests
  - Run all integration tests
  - Verify AOT compatibility
  - Verify performance benchmarks
  - Fix any failing tests
  - _Requirements: 24, 25_
