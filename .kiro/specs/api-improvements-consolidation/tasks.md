# Implementation Plan

## Completed Tasks (from original spec)

- [x] 1. Make request builders generic and remove redundant type parameters
- [x] 2. Update source generator to emit generic builders
- [x] 3. Add LINQ expression overloads to source-generated tables
- [x] 4. Add LINQ expression overloads to source-generated indexes
- [x] 4.2 Make PutItemRequestBuilder generic and add entity overload
- [x] 5. Remove non-functional QueryAsync methods from DynamoDbIndex

## New Tasks (focused scope)

- [x] 1. Add Format property to DynamoDbAttribute
  - Add Format property (string?) to DynamoDbAttributeAttribute class
  - Update XML documentation with format examples (DateTime, decimal, etc.)
  - Add examples showing "yyyy-MM-dd", "F2", etc.
  - _Requirements: 2_

- [x] 2. Update source generator to emit Format in property metadata
  - Read Format property from DynamoDbAttribute during property analysis
  - Emit Format value in generated PropertyMetadata
  - Handle null/empty format strings appropriately
  - _Requirements: 2_

- [x] 3. Add Format application to ExpressionTranslator
  - Add ApplyFormat method that handles DateTime, decimal, double, float, IFormattable
  - Integrate format application into SerializeValue method
  - Use CultureInfo.InvariantCulture for consistent formatting
  - Handle format errors with clear exception messages
  - _Requirements: 2_

- [x] 4. Add sensitive data redaction to ExpressionTranslator
  - Update Translate method to accept SecurityMetadata parameter (optional)
  - Check SecurityMetadata.IsSensitiveField before logging parameter values
  - Replace sensitive values with "[REDACTED]" in log messages
  - Preserve property names in logs while redacting values
  - Ensure redaction only applies when logger is configured
  - _Requirements: 1_

- [x] 5. Add Encrypt and EncryptValue methods to DynamoDbTableBase
  - Add Encrypt(object value, string fieldName) method for use in all expression types
  - Add EncryptValue(object value, string fieldName) helper method (alias for Encrypt)
  - Use ambient EncryptionContext.Current for context ID (compatible with existing pattern)
  - Build FieldEncryptionContext with ContextId from EncryptionContext.Current and default CacheTtlSeconds
  - Check if IFieldEncryptor is configured, throw clear exception if not
  - Call IFieldEncryptor.EncryptAsync with plaintext bytes, fieldName, and context
  - Return base64-encoded encrypted value for use in expressions or variables
  - Works with LINQ expressions, format strings, and WithValue
  - _Requirements: 3_

- [x] 6. Update ExpressionTranslator to detect table.Encrypt() calls
  - Detect MethodCallExpression for Encrypt method in LINQ expression tree
  - Extract value and fieldName arguments from method call
  - Call the Encrypt method to get encrypted value
  - Use encrypted value in DynamoDB query parameter
  - Handle errors if Encrypt call fails
  - _Requirements: 3_

- [x] 13. Add unit tests for format string application
  - Test DateTime format is applied in LINQ expressions
  - Test decimal format is applied in LINQ expressions
  - Test double/float format is applied
  - Test IFormattable types are formatted correctly
  - Test missing format uses default serialization
  - Test invalid format string throws clear exception
  - _Requirements: 2_

- [x] 14. Add unit tests for sensitive data redaction
  - Test [Sensitive] property values are redacted in logs
  - Test non-sensitive property values are not redacted
  - Test redaction only applies when logger is configured
  - Test redaction preserves property names
  - Test mixed sensitive/non-sensitive properties
  - _Requirements: 1_

- [ ] 7. Add unit tests for table.Encrypt() method
  - Test Encrypt() encrypts value correctly using ambient EncryptionContext.Current
  - Test Encrypt() in LINQ expression is detected by ExpressionTranslator
  - Test Encrypt() in format string expressions works correctly
  - Test Encrypt() with WithValue works correctly
  - Test Encrypt() throws exception when no encryptor configured
  - Test Encrypt() builds FieldEncryptionContext correctly (ContextId from ambient context)
  - Test encryption errors are handled with clear messages
  - _Requirements: 3_

- [ ] 8. Add unit tests for EncryptValue helper
  - Test EncryptValue encrypts value correctly (same as Encrypt)
  - Test EncryptValue can be used in LINQ expressions
  - Test EncryptValue can be used in format string expressions
  - Test EncryptValue throws exception when no encryptor configured
  - _Requirements: 3_

- [x] 15. Add integration tests for format application
  - Test Query with formatted DateTime property end-to-end
  - Test Query with formatted decimal property end-to-end
  - Test Scan with formatted properties end-to-end
  - Verify formatted values are sent to DynamoDB correctly
  - Verify results are deserialized correctly
  - _Requirements: 2_

- [x] 16. Add integration tests for sensitive data redaction
  - Test Query with sensitive property logs redacted values
  - Test Scan with sensitive property logs redacted values
  - Test non-sensitive properties are logged normally
  - Verify actual query values are not affected (only logs)
  - _Requirements: 1_

- [ ] 9. Add integration tests for table.Encrypt() in LINQ expressions
  - Test Query with table.Encrypt() in LINQ expression end-to-end
  - Test Scan with table.Encrypt() in LINQ expression end-to-end
  - Verify encrypted values are sent to DynamoDB
  - Verify results can be decrypted correctly
  - Test error handling when encryptor not configured
  - _Requirements: 3_

- [ ] 10. Add integration tests for table.Encrypt() in format strings
  - Test Query with table.Encrypt() in format string expression end-to-end
  - Test Query with table.Encrypt() and WithValue end-to-end
  - Test Scan with table.Encrypt() in format string expression end-to-end
  - Verify encrypted values are sent to DynamoDB
  - Verify results can be decrypted correctly
  - _Requirements: 3_

- [ ] 11. Add integration tests for EncryptValue helper
  - Test Query with pre-encrypted value in LINQ expression end-to-end
  - Test Query with pre-encrypted value in format string end-to-end
  - Test Scan with pre-encrypted value end-to-end
  - Verify encrypted values are sent to DynamoDB
  - Verify results can be decrypted correctly
  - _Requirements: 3_

- [ ] 12. Update documentation for new features
  - Add Format property examples to DynamoDbAttribute documentation
  - Document when to use manual encryption (equality only, not range queries)
  - Add examples of table.Encrypt() in LINQ expressions, format strings, and WithValue
  - Add examples of EncryptValue helper for pre-encryption
  - Document use of ambient EncryptionContext.Current (same as Put/Get operations)
  - Document sensitive data redaction behavior
  - Add migration examples for format support
  - _Requirements: 1, 2, 3_
