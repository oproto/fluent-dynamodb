# Implementation Plan

- [x] 1. Create core expression translation infrastructure
  - Create `Oproto.FluentDynamoDb/Expressions/` directory for expression-related classes
  - Implement `ExpressionContext` class with validation mode, parameter tracking, and entity metadata support
  - Implement `ExpressionValidationMode` enum (None, KeysOnly)
  - _Requirements: 2.1, 2.2, 2.5_

- [x] 2. Implement basic expression translator with operator support
  - [x] 2.1 Create `ExpressionTranslator` class with core translation method
    - Implement `Translate<TEntity>(Expression<Func<TEntity, bool>>, ExpressionContext)` method
    - Implement expression visitor pattern for walking expression trees
    - Add basic error handling and exception types
    - _Requirements: 1.1, 1.2, 1.3, 2.1, 2.2_

  - [x] 2.2 Implement binary operator translation
    - Implement `VisitBinary` method for BinaryExpression nodes
    - Map C# operators to DynamoDB syntax (==, !=, <, >, <=, >=)
    - Map logical operators (&&, ||)
    - Handle operator precedence with parentheses
    - _Requirements: 3.1, 3.2, 3.6, 3.7_

  - [x] 2.3 Implement member access translation
    - Implement `VisitMember` method for MemberExpression nodes
    - Distinguish between entity property access and value capture
    - Validate entity property access against metadata
    - Generate attribute name placeholders for entity properties
    - _Requirements: 1.4, 4.1, 4.2, 4.3_

  - [x] 2.4 Implement constant and value capture
    - Implement `VisitConstant` method for ConstantExpression nodes
    - Implement value capture for variables and closures
    - Generate unique parameter names using ParameterGenerator
    - Convert captured values to AttributeValue using AttributeValueInternal
    - _Requirements: 5.1, 5.2, 5.3, 5.6_

- [x] 3. Implement DynamoDB function support
  - [x] 3.1 Create DynamoDB expression extension methods
    - Create `DynamoDbExpressionExtensions` class
    - Implement `Between<T>(this T value, T low, T high)` extension method
    - Implement `AttributeExists<T>(this T value)` extension method
    - Implement `AttributeNotExists<T>(this T value)` extension method
    - Implement `Size<T>(this IEnumerable<T> collection)` extension method
    - Add `[ExpressionOnly]` attribute to mark these as expression-only methods
    - _Requirements: 3.4, 3.9_

  - [x] 3.2 Implement method call translation
    - Implement `VisitMethodCall` method for MethodCallExpression nodes
    - Detect and translate `string.StartsWith()` to `begins_with()`
    - Detect and translate `string.Contains()` to `contains()`
    - Detect and translate `Between()` extension to `BETWEEN`
    - Detect and translate `AttributeExists()` to `attribute_exists()`
    - Detect and translate `AttributeNotExists()` to `attribute_not_exists()`
    - Detect and translate `Size()` to `size()`
    - _Requirements: 3.3, 3.4, 3.5, 3.9_

  - [x] 3.3 Implement entity parameter reference validation
    - Create `ReferencesEntityParameter` helper method
    - Walk expression trees to detect references to entity parameter
    - Reject method calls that reference entity parameter or properties
    - Allow method calls on captured values only
    - _Requirements: 5.4, 5.5, 8.3_

- [x] 4. Implement validation and error handling
  - [x] 4.1 Create exception types
    - Implement `ExpressionTranslationException` base class
    - Implement `UnmappedPropertyException` for unmapped properties
    - Implement `UnsupportedExpressionException` for unsupported patterns
    - Implement `InvalidKeyExpressionException` for non-key properties in Query().Where()
    - Include original expression in exceptions for debugging
    - _Requirements: 8.1, 8.2, 8.3, 8.4, 8.5_

  - [x] 4.2 Implement property validation
    - Validate property access against EntityMetadata when available
    - Check if property has DynamoDB attribute mapping
    - Check if property is queryable (not marked as non-queryable)
    - Validate computed and extracted attributes
    - _Requirements: 4.1, 4.2, 4.3, 4.4, 4.5_

  - [x] 4.3 Implement key-only validation for Query().Where()
    - Check validation mode in ExpressionContext
    - When KeysOnly mode, validate properties are partition key or sort key
    - Use EntityMetadata to determine key properties
    - Throw InvalidKeyExpressionException for non-key properties
    - _Requirements: 6.1, 6.2, 6.5_

  - [x] 4.4 Implement unary operator support and validation
    - Implement `VisitUnary` method for UnaryExpression nodes
    - Support logical NOT (!) operator
    - Reject unsupported unary operators
    - _Requirements: 3.8_

- [x] 5. Add expression-based extension method overloads
  - [x] 5.1 Add expression overload to Where() for key conditions
    - Add `Where<T, TEntity>(Expression<Func<TEntity, bool>>, EntityMetadata?)` to WithConditionExpressionExtensions
    - Create ExpressionContext with KeysOnly validation mode
    - Call ExpressionTranslator to generate expression string
    - Integrate with existing SetConditionExpression method
    - _Requirements: 1.1, 1.5, 1.6, 6.1_

  - [x] 5.2 Add expression overload to WithFilter() for filter expressions
    - Add `WithFilter<T, TEntity>(Expression<Func<TEntity, bool>>, EntityMetadata?)` to WithFilterExpressionExtensions
    - Create ExpressionContext with None validation mode
    - Call ExpressionTranslator to generate expression string
    - Integrate with existing SetFilterExpression method
    - _Requirements: 1.2, 1.3, 1.5, 1.6, 6.3, 6.4_

  - [x] 5.3 Implement expression combining for multiple calls
    - Handle multiple Where() calls with AND logic
    - Handle multiple WithFilter() calls with AND logic
    - Ensure parameter name uniqueness across calls
    - Support mixing expression-based and string-based calls
    - _Requirements: 7.1, 7.2, 7.3, 7.4, 7.5, 7.6_

- [ ] 6. Implement performance optimizations
  - [ ] 6.1 Create expression caching mechanism
    - Implement `ExpressionCache` class with ConcurrentDictionary
    - Create cache key from expression and validation mode
    - Cache translated expression strings (not parameter values)
    - Add thread-safety for concurrent access
    - _Requirements: 9.1, 9.2_

  - [ ] 6.2 Optimize allocations during translation
    - Use StringBuilder for expression building
    - Reuse ParameterGenerator from context
    - Minimize string allocations in hot paths
    - _Requirements: 9.2, 9.3_

- [ ] 7. Write comprehensive unit tests
  - [ ] 7.1 Test operator translation
    - Test equality operators (==, !=)
    - Test comparison operators (<, >, <=, >=)
    - Test logical operators (&&, ||, !)
    - Test operator precedence and parentheses
    - _Requirements: 3.1, 3.2, 3.6, 3.7, 3.8_

  - [ ] 7.2 Test DynamoDB function translation
    - Test StartsWith → begins_with
    - Test Contains → contains
    - Test Between → BETWEEN
    - Test AttributeExists → attribute_exists
    - Test AttributeNotExists → attribute_not_exists
    - Test Size → size
    - _Requirements: 3.3, 3.4, 3.5, 3.9_

  - [ ] 7.3 Test value capture
    - Test constant values
    - Test local variables
    - Test closure captures
    - Test method calls on captured values
    - Test various types (string, int, DateTime, enum, etc.)
    - _Requirements: 5.1, 5.2, 5.3_

  - [ ] 7.4 Test validation and error handling
    - Test unmapped property detection
    - Test non-key property in Query().Where()
    - Test entity parameter reference in method calls
    - Test unsupported operators
    - Test unsupported methods
    - Test assignment expression rejection
    - Verify error message clarity
    - _Requirements: 4.1, 4.2, 4.3, 4.4, 4.5, 6.1, 6.2, 8.1, 8.2, 8.3, 8.4, 8.5_

  - [ ] 7.5 Test expression combining
    - Test multiple Where() calls
    - Test multiple WithFilter() calls
    - Test mixing expression and string-based calls
    - Test parameter name uniqueness
    - _Requirements: 7.1, 7.2, 7.3, 7.4, 7.5, 7.6_

  - [ ] 7.6 Test edge cases
    - Test null values
    - Test nullable properties
    - Test enum values
    - Test DateTime values
    - Test collection properties
    - Test nested expressions
    - _Requirements: 5.1, 5.2, 5.3_

- [ ] 8. Create AOT compatibility test project
  - [ ] 8.1 Set up Native AOT test project
    - Create new console project with Native AOT enabled
    - Add reference to FluentDynamoDb library
    - Configure PublishAot and trimming settings
    - _Requirements: 2.1, 2.2, 2.4_

  - [ ] 8.2 Test closure captures in AOT
    - Test local variable capture
    - Test field capture from outer class
    - Test nested closure captures
    - Test complex closure scenarios
    - Verify identical behavior to JIT
    - _Requirements: 2.5, 9.4_

  - [ ] 8.3 Test expression translation in AOT
    - Test all operator types
    - Test all DynamoDB functions
    - Test value capture with various types
    - Test validation and error handling
    - Verify no runtime code generation
    - _Requirements: 2.1, 2.2, 2.3, 2.4, 2.5_

  - [ ] 8.4 Test generic method expressions in AOT
    - Test expressions with generic entity types
    - Test expressions with generic property types
    - Test generic method calls in expressions
    - _Requirements: 2.5_

  - [ ] 8.5 Verify trimming compatibility
    - Build with trimming enabled
    - Verify no trim warnings
    - Test that trimmed binary works correctly
    - _Requirements: 2.1, 2.2_

- [ ] 9. Write integration tests with DynamoDB
  - [ ] 9.1 Test Query with expression-based Where()
    - Test simple partition key query
    - Test partition key + sort key query
    - Test with DynamoDB functions (begins_with, between)
    - Verify generated expressions work with DynamoDB Local
    - _Requirements: 1.1, 6.1_

  - [ ] 9.2 Test Query with expression-based WithFilter()
    - Test filter on non-key attributes
    - Test complex filter expressions
    - Test mixing Where() and WithFilter()
    - _Requirements: 1.2, 6.3_

  - [ ] 9.3 Test Scan with expression-based WithFilter()
    - Test filter expressions on scan
    - Test various operators and functions
    - _Requirements: 1.3, 6.4_

  - [ ] 9.4 Test mixing expression and string-based calls
    - Test expression Where() + string WithFilter()
    - Test string Where() + expression WithFilter()
    - Test multiple calls of each type
    - _Requirements: 7.3, 7.4, 7.5_

- [ ] 10. Add XML documentation and examples
  - Add XML documentation to ExpressionTranslator
  - Add XML documentation to extension method overloads
  - Add XML documentation to DynamoDbExpressionExtensions
  - Document supported operators and methods
  - Document validation rules and error scenarios
  - Add code examples showing common patterns
  - Add examples comparing expression vs string-based approaches
  - Document what expressions are valid vs invalid
  - _Requirements: 10.1, 10.2, 10.3, 10.4, 10.5_
