# Implementation Plan

## Overview

This implementation plan breaks down the data serialization and formatting fixes into discrete, manageable coding tasks. Each task builds incrementally on previous tasks and includes specific requirements references.

## Task List

- [x] 1. Add DateTime Kind support to DynamoDbAttribute
  - Add `DateTimeKind` property to `DynamoDbAttributeAttribute` class with default value `DateTimeKind.Unspecified`
  - Update XML documentation with examples and usage guidance
  - Add validation to ensure only valid DateTimeKind values are used
  - _Requirements: 3.1, 3.6_

- [x] 2. Update source generator models for DateTime Kind
  - [x] 2.1 Add DateTimeKind property to PropertyModel
    - Add nullable `DateTimeKind?` property to track the specified kind
    - Update PropertyModel documentation
    - _Requirements: 3.1_

  - [x] 2.2 Update EntityAnalyzer to extract DateTimeKind
    - Extract DateTimeKind from DynamoDbAttribute during property analysis
    - Handle missing DateTimeKind (default to null/Unspecified)
    - Add diagnostic logging for DateTimeKind extraction
    - _Requirements: 3.1_

- [x] 3. Implement DateTime Kind in MapperGenerator
  - [x] 3.1 Update ToDynamoDb generation for DateTime Kind
    - Generate code to convert DateTime to specified kind before serialization
    - Handle Utc (ToUniversalTime), Local (ToLocalTime), and Unspecified cases
    - Combine with format string application if both are specified
    - Add try-catch for conversion errors
    - _Requirements: 3.2, 4.1, 4.3_

  - [x] 3.2 Update FromDynamoDb generation for DateTime Kind
    - Generate code to set DateTime.Kind after parsing
    - Use DateTime.SpecifyKind for Utc and Local
    - Handle parsing errors with clear error messages
    - Combine with format string parsing if format is specified
    - _Requirements: 3.4, 3.5, 4.2, 4.4_

  - [x] 3.3 Add unit tests for DateTime Kind generation
    - Test code generation for Utc, Local, and Unspecified
    - Test combination with format strings
    - Test error handling for invalid conversions
    - Verify generated code compiles and runs correctly
    - _Requirements: 7.3_

- [x] 4. Implement format string application in MapperGenerator
  - [x] 4.1 Create format string serialization helper
    - Add `GenerateFormattedPropertySerialization` method to MapperGenerator
    - Support DateTime, decimal, double, float, int, and IFormattable types
    - Use CultureInfo.InvariantCulture for all formatting
    - Add try-catch with FormatException for invalid format strings
    - _Requirements: 1.1, 1.2, 1.3, 1.4, 1.5_

  - [x] 4.2 Create format string deserialization helper
    - Add `GenerateFormattedPropertyDeserialization` method to MapperGenerator
    - Support parsing DateTime with TryParseExact
    - Support parsing numeric types with TryParse and NumberStyles.Any
    - Add error handling with DynamoDbMappingException for parsing failures
    - _Requirements: 2.1, 2.2, 2.3, 2.4, 2.5_

  - [x] 4.3 Update ToDynamoDb generation to use format strings
    - Check for Format property in PropertyModel
    - Call GenerateFormattedPropertySerialization when format is present
    - Fall back to default serialization when format is absent
    - Ensure backward compatibility (no format = no change)
    - _Requirements: 1.1, 1.2, 1.3, 1.4_

  - [x] 4.4 Update FromDynamoDb generation to use format strings
    - Check for Format property in PropertyModel
    - Call GenerateFormattedPropertyDeserialization when format is present
    - Fall back to default parsing when format is absent
    - Ensure backward compatibility (no format = no change)
    - _Requirements: 2.1, 2.2, 2.3, 2.4_

  - [x] 4.5 Add unit tests for format string generation
    - Test DateTime formatting (yyyy-MM-dd, o, custom formats)
    - Test decimal formatting (F2, F4, N2)
    - Test integer formatting (D5, D8)
    - Test invalid format strings (should generate error-throwing code)
    - Test format string parsing in FromDynamoDb
    - _Requirements: 7.1, 7.2_

- [x] 5. Add encryption metadata to PropertyMetadata
  - [x] 5.1 Add IsEncrypted property to PropertyMetadata class
    - Add boolean `IsEncrypted` property
    - Add DateTimeKind property for completeness
    - Update XML documentation
    - _Requirements: 5.1, 5.2_

  - [x] 5.2 Update MapperGenerator to include encryption metadata
    - Update `GenerateGetEntityMetadataMethod` to include IsEncrypted flag
    - Extract IsEncrypted from PropertyModel.Security?.IsEncrypted
    - Include DateTimeKind in metadata generation
    - _Requirements: 5.1, 5.2_

- [x] 6. Create parameter metadata tracking for encryption
  - [x] 6.1 Create ParameterMetadata class
    - Add ParameterName, Value, RequiresEncryption, PropertyName, AttributeName properties
    - Add XML documentation explaining the purpose
    - Place in Oproto.FluentDynamoDb/Expressions namespace
    - _Requirements: 5.1, 5.2_

  - [x] 6.2 Update ExpressionContext to track parameter metadata
    - Add `List<ParameterMetadata> ParameterMetadata` property
    - Initialize in constructor
    - Update XML documentation
    - _Requirements: 5.1, 5.2_

  - [x] 6.3 Update UpdateExpressionTranslator to mark encrypted parameters
    - Modify `CaptureValue` method to check PropertyMetadata.IsEncrypted
    - Add ParameterMetadata entry when encryption is required
    - Do NOT encrypt inline - just mark for later
    - Update `IsEncryptedProperty` to use PropertyMetadata.IsEncrypted
    - _Requirements: 5.1, 5.2, 5.3_

  - [x] 6.4 Add unit tests for parameter metadata tracking
    - Test that encrypted parameters are marked correctly
    - Test that non-encrypted parameters are not marked
    - Test multiple encrypted parameters in one expression
    - Verify ParameterMetadata contains correct information
    - _Requirements: 7.5_

- [x] 7. Implement encryption in UpdateItemRequestBuilder
  - [x] 7.1 Add EncryptParametersAsync method to UpdateItemRequestBuilder
    - Create private async method to encrypt marked parameters
    - Iterate through ExpressionContext.ParameterMetadata
    - For each parameter requiring encryption, call IFieldEncryptor.EncryptAsync
    - Replace AttributeValue in request.ExpressionAttributeValues with encrypted value
    - Convert encrypted bytes to base64 string for storage
    - _Requirements: 5.1, 5.2, 5.3, 5.4_

  - [x] 7.2 Update UpdateAsync to call EncryptParametersAsync
    - Check if any parameters require encryption
    - Throw clear exception if encryption required but no encryptor configured
    - Call EncryptParametersAsync before sending request to DynamoDB
    - Handle encryption errors with FieldEncryptionException
    - _Requirements: 5.1, 5.2, 5.3, 5.4_

  - [x] 7.3 Update ToRequest to pass ExpressionContext
    - Ensure ExpressionContext is accessible in UpdateAsync
    - May need to store context as instance field
    - Update method signatures as needed
    - _Requirements: 5.1, 5.2_

  - [x] 7.4 Add unit tests for encryption in request builder
    - Test that EncryptParametersAsync encrypts marked parameters
    - Test error when encryptor is missing
    - Test multiple encrypted parameters
    - Test encryption with format strings
    - Mock IFieldEncryptor for testing
    - _Requirements: 7.5_

- [x] 8. Apply encryption pattern to other request builders
  - [x] 8.1 Update TransactUpdateBuilder for encryption
    - Add same EncryptParametersAsync pattern
    - Update BuildTransactWriteItem to encrypt before building
    - Handle encryption errors appropriately
    - _Requirements: 5.1, 5.2_

  - [x] 8.2 Update any other builders that use update expressions
    - Identify all request builders that support Set() with expressions
    - Apply same encryption pattern consistently
    - Ensure all builders have access to IFieldEncryptor
    - _Requirements: 5.1, 5.2_

  - [x] 8.3 Add unit tests for encryption in transaction builders
    - Test encryption in TransactUpdateBuilder
    - Test encryption in other builders
    - Verify consistency across all builders
    - _Requirements: 7.5_

- [x] 9. Verify format string application in UpdateExpressionTranslator
  - [x] 9.1 Review existing ApplyFormat implementation
    - Verify ApplyFormat method is correctly implemented
    - Check that it's called in all necessary places (TranslateSimpleSet, TranslateBinaryOperation, etc.)
    - Ensure error handling is adequate
    - _Requirements: 4.1, 4.2, 4.3, 4.4, 4.5_

  - [x] 9.2 Add missing format application if needed
    - If any operation types are missing format application, add it
    - Ensure consistency across all operation types
    - Update error messages to be clear and actionable
    - _Requirements: 4.1, 4.2, 4.3, 4.4, 4.5_

  - [x] 9.3 Add unit tests for format strings in update expressions
    - Test format application in SET operations
    - Test format application in arithmetic operations
    - Test format application in IfNotExists
    - Test format application in ListAppend/ListPrepend
    - Test error handling for invalid formats
    - _Requirements: 7.4_

- [x] 10. Integration testing
  - [x] 10.1 Add integration tests for DateTime Kind round-trips
    - Test storing and retrieving DateTime with Utc kind
    - Test storing and retrieving DateTime with Local kind
    - Test storing and retrieving DateTime with Unspecified kind
    - Verify Kind is preserved after round-trip
    - Test with DynamoDB Local
    - _Requirements: 7.3_

  - [x] 10.2 Add integration tests for format string round-trips
    - Test DateTime with various formats (yyyy-MM-dd, o, custom)
    - Test decimal with precision formats (F2, F4)
    - Test integer with zero-padding (D5, D8)
    - Verify values are stored and retrieved correctly
    - Test format consistency across PutItem and UpdateItem
    - _Requirements: 7.1, 7.2_

  - [x] 10.3 Add integration tests for encryption round-trips
    - Test storing and retrieving encrypted properties
    - Test updating encrypted properties via update expressions
    - Test multiple encrypted properties in one entity
    - Verify encryption is maintained across operations
    - Use mock IFieldEncryptor for testing
    - _Requirements: 7.5_

  - [x] 10.4 Add integration tests for combined scenarios
    - Test DateTime Kind + format string
    - Test encryption + format string
    - Test all three features together
    - Test error scenarios (invalid formats, missing encryptor, parsing failures)
    - _Requirements: 7.1, 7.2, 7.3, 7.5_

- [x] 11. Error handling and diagnostics
  - [x] 11.1 Enhance error messages for format string failures
    - Include property name, attribute name, format string, and property type
    - Provide examples of valid format strings
    - Add troubleshooting guidance
    - _Requirements: 12.1, 12.5_

  - [x] 11.2 Enhance error messages for encryption failures
    - Include property name, attribute name, and encryption error details
    - Provide guidance on configuring IFieldEncryptor
    - Add troubleshooting steps
    - _Requirements: 12.2, 12.5_

  - [x] 11.3 Enhance error messages for parsing failures
    - Include stored value, expected format, and property name
    - Suggest checking format string matches stored data
    - Add examples of correct format strings
    - _Requirements: 12.3, 12.5_

  - [x] 11.4 Add diagnostic logging for format and encryption
    - Log format application at Debug level
    - Log encryption operations at Debug level
    - Redact sensitive values in logs
    - Include property names and operation types
    - _Requirements: 12.4, 12.5_

- [x] 12. Documentation updates
  - [x] 12.1 Update DynamoDbAttribute XML documentation
    - Document DateTimeKind parameter with examples
    - Explain timezone handling behavior
    - Provide guidance on choosing Utc vs Local vs Unspecified
    - Update Format property documentation with more examples
    - _Requirements: 8.1, 8.2_

  - [x] 12.2 Update UpdateExpressionTranslator XML documentation
    - Document encryption behavior (deferred to request builder)
    - Explain parameter metadata tracking
    - Provide examples of encrypted property updates
    - _Requirements: 8.1, 8.2_

  - [x] 12.3 Create format strings user guide
    - Common format patterns for DateTime, decimal, int
    - Type-specific formatting examples
    - Performance considerations
    - Troubleshooting format errors
    - _Requirements: 8.1, 8.2_

  - [x] 12.4 Create DateTime Kind user guide
    - When to use Utc vs Local vs Unspecified
    - Timezone handling best practices
    - Migration from existing code
    - Examples of common scenarios
    - _Requirements: 8.1, 8.2_

  - [x] 12.5 Create encryption user guide
    - Setting up IFieldEncryptor
    - How encryption works in update expressions
    - Performance considerations
    - Security best practices
    - Troubleshooting encryption errors
    - _Requirements: 8.1, 8.2, 8.3_

  - [x] 12.6 Create migration guide
    - Explain that all changes are opt-in
    - Provide before/after examples for format strings
    - Provide before/after examples for DateTime Kind
    - Explain encryption now works in update expressions
    - Include performance notes
    - _Requirements: 8.1, 8.2, 8.4_

- [x] 13. Update CHANGELOG.md
  - Add "Added" section for DateTime Kind support
  - Add "Added" section for format string application in serialization
  - Add "Fixed" section for format strings in update expressions (if any fixes needed)
  - Add "Added" section for encryption support in update expressions
  - Include migration notes and examples
  - Reference related requirements
  - _Requirements: 9.1, 9.2, 9.3, 9.4, 9.5_

- [ ] 14. Performance testing and optimization
  - [ ] 14.1 Benchmark format string application
    - Measure performance impact of format strings in ToDynamoDb
    - Compare with default serialization
    - Ensure overhead is <5%
    - Document results
    - _Requirements: 11.1, 11.5_

  - [ ] 14.2 Benchmark encryption performance
    - Measure encryption overhead in update expressions
    - Test with mock encryptor (fast) and real encryptor (AWS KMS)
    - Document performance characteristics
    - Provide optimization guidance
    - _Requirements: 11.2, 11.5_

  - [ ] 14.3 Memory allocation analysis
    - Profile memory allocations in hot paths
    - Identify unnecessary allocations
    - Optimize where possible
    - Document memory usage patterns
    - _Requirements: 11.3, 11.4_

- [ ] 15. Final validation and cleanup
  - [x] 15.1 Run full test suite
    - Ensure all unit tests pass
    - Ensure all integration tests pass
    - Verify code coverage >90%
    - Fix any failing tests
    - _Requirements: 7.7_

  - [ ] 15.2 Backward compatibility verification
    - Test existing code without format strings still works
    - Test existing code without DateTime Kind still works
    - Test existing code without encryption still works
    - Verify no breaking changes
    - _Requirements: 10.1, 10.2, 10.3, 10.4, 10.5_

  - [ ] 15.3 Code review and cleanup
    - Review all code changes for quality
    - Remove any debug code or comments
    - Ensure consistent code style
    - Update any outdated comments
    - _Requirements: All_

  - [ ] 15.4 Documentation review
    - Verify all documentation is accurate
    - Check for typos and formatting issues
    - Ensure examples compile and run
    - Verify links and references
    - _Requirements: 8.1, 8.2, 8.3, 8.4, 8.5_

## Task Dependencies

```
1 → 2.1 → 2.2 → 3.1, 3.2 → 3.3
4.1, 4.2 → 4.3, 4.4 → 4.5
5.1 → 5.2
6.1 → 6.2 → 6.3 → 6.4
6.3 → 7.1 → 7.2, 7.3 → 7.4
7.2 → 8.1, 8.2 → 8.3
9.1 → 9.2 → 9.3
3.2, 4.4, 7.2 → 10.1, 10.2, 10.3 → 10.4
11.1, 11.2, 11.3, 11.4 (can be done in parallel)
12.1, 12.2, 12.3, 12.4, 12.5, 12.6 (can be done in parallel)
All tasks → 13
All tasks → 14.1, 14.2, 14.3
All tasks → 15.1, 15.2, 15.3, 15.4
```

## Execution Notes

- All tasks are required for a comprehensive implementation
- Core implementation tasks (1-9) should be completed first
- Integration tests (10) should be done after core implementation
- Documentation (12) can be done in parallel with testing
- Performance testing (14) can be done after integration tests pass
- Final validation (15) should be done last

## Estimated Timeline

- **Week 1**: Tasks 1-5 (DateTime Kind + Format Strings in serialization)
- **Week 2**: Tasks 6-9 (Encryption support + verification)
- **Week 3**: Tasks 10-15 (Testing, documentation, validation)

**Total: 2-3 weeks for one developer**
