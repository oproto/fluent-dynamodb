# Implementation Plan

- [x] 1. Create enhanced internal helper classes
  - Create ParameterGenerator class for per-builder parameter name generation
  - Enhance AttributeValueInternal with format string support methods
  - Add format value conversion logic supporting standard .NET format strings
  - _Requirements: 3.1, 3.2, 4.1, 4.2, 4.3, 6.1, 6.2, 6.3_

- [x] 2. Modify interface contracts for extension method support
  - [x] 2.1 Update IWithAttributeValues interface to expose helper access
    - Remove all method declarations from interface
    - Add GetAttributeValueHelper() method requirement
    - Add Self property requirement for method chaining
    - _Requirements: 1.1, 5.3, 5.4_

  - [x] 2.2 Update IWithAttributeNames interface to expose helper access
    - Remove all method declarations from interface
    - Add GetAttributeNameHelper() method requirement
    - Add Self property requirement for method chaining
    - _Requirements: 1.1, 5.3, 5.4_

  - [x] 2.3 Update IWithConditionExpression interface for format string support
    - Remove Where method declaration from interface
    - Add GetAttributeValueHelper() method requirement for parameter generation
    - Add SetConditionExpression() method requirement for processed expressions
    - Add Self property requirement for method chaining
    - _Requirements: 1.1, 3.1, 5.3, 5.4_

  - [x] 2.4 Update IWithKey interface to expose helper access
    - Remove all method declarations from interface
    - Add key setting mechanism access method
    - Add Self property requirement for method chaining
    - _Requirements: 1.1, 5.3, 5.4_

- [x] 3. Create extension method classes with existing functionality
  - [x] 3.1 Implement WithAttributeValuesExtensions class
    - Create all existing WithValue overloads as extension methods
    - Implement WithValues methods for bulk operations
    - Ensure identical behavior to current interface implementations
    - _Requirements: 1.1, 1.2, 2.1, 2.2, 2.3_

  - [x] 3.2 Implement WithAttributeNamesExtensions class
    - Create WithAttribute and WithAttributes extension methods
    - Ensure identical behavior to current interface implementations
    - _Requirements: 1.1, 1.2, 2.1, 2.2, 2.3_

  - [x] 3.3 Implement WithConditionExpressionExtensions class
    - Create existing Where method as extension method
    - Implement new Where method with format string support
    - Add format string parsing and parameter generation logic
    - _Requirements: 1.1, 1.2, 2.1, 2.2, 2.3, 3.1, 3.2, 3.3, 4.1, 4.2, 4.3_

  - [x] 3.4 Implement WithKeyExtensions class
    - Create all existing WithKey overloads as extension methods
    - Ensure identical behavior to current interface implementations
    - _Requirements: 1.1, 1.2, 2.1, 2.2, 2.3_

- [x] 4. Update all request builder classes to use new interface contracts
  - [x] 4.1 Update QueryRequestBuilder to new interface pattern
    - Remove all interface method implementations
    - Add GetAttributeValueHelper(), GetAttributeNameHelper() methods
    - Add SetConditionExpression() method for Where functionality
    - Add Self property returning builder instance
    - _Requirements: 1.1, 5.4, 2.1, 2.2, 2.3_

  - [x] 4.2 Update remaining core request builders
    - Apply same pattern to GetItemRequestBuilder, PutItemRequestBuilder, UpdateItemRequestBuilder, DeleteItemRequestBuilder
    - Remove interface method implementations and add helper access methods
    - Ensure each builder exposes appropriate interface helper methods
    - _Requirements: 1.1, 5.4, 2.1, 2.2, 2.3_

  - [x] 4.3 Update transaction request builders
    - Apply same pattern to TransactWriteItemsRequestBuilder, TransactGetItemsRequestBuilder
    - Apply pattern to individual transaction builders (TransactPutBuilder, TransactDeleteBuilder, etc.)
    - _Requirements: 1.1, 5.4, 2.1, 2.2, 2.3_

  - [x] 4.4 Update batch and scan request builders
    - Apply same pattern to BatchGetItemRequestBuilder, BatchWriteItemRequestBuilder, ScanRequestBuilder
    - Handle any builder-specific interface implementations
    - _Requirements: 1.1, 5.4, 2.1, 2.2, 2.3_

- [x] 5. Implement format string processing functionality
  - [x] 5.1 Create format string parser
    - Parse {0}, {1:format} patterns from input strings
    - Extract parameter indices and format specifiers
    - Generate replacement expressions with parameter names
    - _Requirements: 3.1, 3.2, 4.1, 4.2, 6.2, 6.3_

  - [x] 5.2 Implement value formatting and conversion
    - Support standard .NET format strings (o, F2, X, etc.)
    - Handle DateTime, enum, numeric, and string conversions
    - Convert formatted values to appropriate AttributeValue types
    - _Requirements: 4.1, 4.2, 4.3, 6.1, 6.2, 6.3_

  - [x] 5.3 Add error handling for format operations
    - Validate format string syntax and parameter counts
    - Provide clear error messages for unsupported formats
    - Handle null values and edge cases gracefully
    - _Requirements: 4.4, 6.1, 6.3_

- [x] 6. Create comprehensive unit tests for extension methods
  - Write tests for all WithValue extension method overloads
  - Test format string parsing with various patterns and edge cases
  - Verify parameter generation and AttributeValue conversion
  - Test error conditions and validation scenarios
  - _Requirements: All requirements validation_

- [x] 7. Create integration tests for builder functionality
  - Test all request builders work correctly with extension methods
  - Verify mixed usage of old parameter style and new format strings
  - Test complex expressions with multiple parameters and formats
  - Validate backward compatibility with existing usage patterns
  - _Requirements: 2.1, 2.2, 2.3, 3.4_

- [ ]* 8. Add AOT compatibility validation tests
  - Create test project targeting Native AOT
  - Verify all extension methods work in AOT environment
  - Test format string operations are AOT-safe
  - Validate no reflection or dynamic code generation occurs
  - _Requirements: 6.1, 6.2, 6.3, 6.4_

- [x] 9. Update documentation and examples
  - [x] 9.1 Update XML documentation for all extension methods
    - Add comprehensive documentation with usage examples
    - Document format string syntax and supported format specifiers
    - Include migration guidance for existing code
    - _Requirements: 7.1, 7.2, 7.3, 7.4_

  - [x] 9.2 Create usage examples demonstrating new functionality
    - Show before/after examples of parameter handling improvements
    - Demonstrate format string usage with various data types
    - Provide examples of mixed old and new syntax usage
    - _Requirements: 7.1, 7.2, 7.4_

- [x] 10. Add update expression format string support
  - [x] 10.1 Create IWithUpdateExpression interface
    - Define interface with GetAttributeValueHelper(), SetUpdateExpression(), and Self properties
    - Follow same pattern as IWithConditionExpression for consistency
    - _Requirements: 1.1, 3.1, 5.3, 5.4_

  - [x] 10.2 Implement WithUpdateExpressionExtensions class
    - Create existing Set method as extension method
    - Implement new Set method with format string support using same logic as Where method
    - Reuse format string parsing and parameter generation from WithConditionExpressionExtensions
    - _Requirements: 1.1, 1.2, 2.1, 2.2, 2.3, 3.1, 3.2, 3.3, 4.1, 4.2, 4.3_

  - [x] 10.3 Update UpdateItemRequestBuilder to implement IWithUpdateExpression
    - Remove existing Set method implementation
    - Add IWithUpdateExpression interface implementation
    - Add SetUpdateExpression method that sets _req.UpdateExpression
    - _Requirements: 1.1, 5.4, 2.1, 2.2, 2.3_

  - [x] 10.4 Update TransactUpdateBuilder to implement IWithUpdateExpression
    - Remove existing Set method implementation
    - Add IWithUpdateExpression interface implementation
    - Add SetUpdateExpression method that sets _req.Update.UpdateExpression
    - _Requirements: 1.1, 5.4, 2.1, 2.2, 2.3_

  - [x] 10.5 Create unit tests for update expression format strings
    - Test Set method with various format string patterns
    - Verify parameter generation and AttributeValue conversion in update expressions
    - Test mixed usage of format strings and traditional parameters
    - Test error conditions specific to update expressions
    - _Requirements: All requirements validation_

  - [x] 10.6 Update documentation for update expression format strings
    - Add XML documentation to WithUpdateExpressionExtensions
    - Update usage examples to show Set method format string usage
    - Document the enhanced update expression capabilities
    - _Requirements: 7.1, 7.2, 7.3, 7.4_