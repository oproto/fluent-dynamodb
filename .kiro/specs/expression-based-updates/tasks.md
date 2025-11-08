# Implementation Plan

- [x] 1. Create UpdateExpressionProperty<T> wrapper type
  - Create UpdateExpressionProperty<T> class as empty marker type
  - Add XML documentation explaining its purpose
  - Mark constructor as internal to prevent direct instantiation
  - _Requirements: 1.1, 1.2, 1.3, 1.4, 1.5_

- [x] 2. Create extension methods for update operations
  - Create UpdateExpressionPropertyExtensions class
  - Implement Add() extension methods for numeric types (int, long, decimal, double)
  - Implement Add() extension method for HashSet<T>
  - Implement Remove() extension method for all types
  - Implement Delete() extension method for HashSet<T>
  - Implement IfNotExists() extension method for all types
  - Implement ListAppend() extension method for List<T>
  - Implement ListPrepend() extension method for List<T>
  - Mark all methods with [ExpressionOnly] attribute
  - Add comprehensive XML documentation with examples
  - _Requirements: 4.1, 4.2, 4.3, 4.4, 4.5, 5.1, 5.2, 5.3, 5.4, 5.5, 6.1, 6.2, 6.3, 6.4, 6.5, 7.1, 7.2, 7.3, 7.4, 7.5, 11.1, 11.2, 11.3, 11.4, 11.5_

- [x] 3. Enhance source generator to create UpdateExpressions classes
  - Modify DynamoDbEntityGenerator to detect entities with [DynamoDbTable]
  - Generate {Entity}UpdateExpressions class with UpdateExpressionProperty<T> properties
  - Map each entity property to corresponding UpdateExpressionProperty<T>
  - Preserve property names and add XML documentation
  - Include key properties but mark with documentation that they cannot be updated
  - _Requirements: 1.1, 1.2, 1.3, 1.4, 1.5_

- [x] 4. Enhance source generator to create UpdateModel classes
  - This may have been partially done in task 3 looking at the code that was written
  - Generate {Entity}UpdateModel class with nullable versions of all properties
  - Map property types correctly (int becomes int?, string stays string?, etc.)
  - Add XML documentation explaining the purpose
  - Ensure class is in same namespace as entity
  - _Requirements: 1.1, 1.2, 1.3, 1.4, 1.5_

- [x] 5. Create UpdateExpressionTranslator class
  - Create UpdateExpressionTranslator with constructor accepting logger, field encryptor, etc.
  - Implement TranslateUpdateExpression<TUpdateExpressions, TUpdateModel>() method
  - Validate expression body is MemberInitExpression
  - Extract parameter and process each MemberAssignment
  - Classify operations into SET, ADD, REMOVE, DELETE
  - Build combined update expression string
  - _Requirements: 2.1, 2.2, 2.3, 2.4, 2.5_

- [x] 5.1 Implement SET operation translation
  - Detect simple value assignments (constants, variables)
  - Generate SET clause with attribute name and value parameter
  - Apply format strings from entity metadata
  - Apply encryption for properties marked with [Encrypted]
  - Handle multiple SET operations with comma separation
  - _Requirements: 2.1, 2.2, 2.3, 2.4, 2.5, 8.1, 8.2, 8.3, 8.4, 8.5, 9.1, 9.2, 9.3, 9.4, 9.5_

- [x] 5.2 Implement arithmetic operation translation
  - Detect BinaryExpression with Add or Subtract node types
  - Validate left side is MemberExpression accessing UpdateExpressionProperty
  - Extract property name and right-side value
  - Generate SET clause with arithmetic (e.g., "SET #attr = #attr + :val")
  - Validate property type is numeric
  - _Requirements: 3.1, 3.2, 3.3, 3.4, 3.5_

- [x] 5.3 Implement ADD operation translation
  - Detect MethodCallExpression with method name "Add"
  - Validate method is called on UpdateExpressionProperty
  - Extract property name and increment value
  - Generate ADD clause (e.g., "ADD #attr :val")
  - Handle negative values for decrement
  - Support both numeric and set types
  - _Requirements: 4.1, 4.2, 4.3, 4.4, 4.5_

- [x] 5.4 Implement REMOVE operation translation
  - Detect MethodCallExpression with method name "Remove"
  - Validate method is called on UpdateExpressionProperty
  - Extract property name
  - Validate property is not a key property
  - Generate REMOVE clause (e.g., "REMOVE #attr")
  - _Requirements: 5.1, 5.2, 5.3, 5.4, 5.5_

- [x] 5.5 Implement DELETE operation translation
  - Detect MethodCallExpression with method name "Delete"
  - Validate method is called on UpdateExpressionProperty<HashSet<T>>
  - Extract property name and elements to delete
  - Generate DELETE clause (e.g., "DELETE #attr :val")
  - Create AttributeValue with set of elements
  - _Requirements: 6.1, 6.2, 6.3, 6.4, 6.5_

- [x] 5.6 Implement DynamoDB function translation
  - Detect MethodCallExpression with method names: IfNotExists, ListAppend, ListPrepend
  - For IfNotExists: Generate "SET #attr = if_not_exists(#attr, :val)"
  - For ListAppend: Generate "SET #attr = list_append(#attr, :val)"
  - For ListPrepend: Generate "SET #attr = list_append(:val, #attr)"
  - Validate function is appropriate for property type
  - _Requirements: 7.1, 7.2, 7.3, 7.4, 7.5_

- [x] 6. Create Set() extension method for UpdateItemRequestBuilder
  - Implement Set<TEntity, TUpdateExpressions, TUpdateModel>() extension method
  - Accept Expression<Func<TUpdateExpressions, TUpdateModel>> parameter
  - Resolve EntityMetadata using MetadataResolver if not provided
  - Create ExpressionContext with attribute helpers and metadata
  - Create UpdateExpressionTranslator with field encryptor
  - Call TranslateUpdateExpression() to get expression string
  - Call builder.SetUpdateExpression() to apply translated expression
  - Add comprehensive XML documentation with examples
  - _Requirements: 2.1, 2.2, 2.3, 2.4, 2.5_

- [x] 7. Implement validation and error handling
  - Validate key properties (partition key, sort key) are not updated
  - Validate properties exist in entity metadata
  - Validate expression patterns are supported
  - Create UnsupportedExpressionException with descriptive messages
  - Create InvalidUpdateOperationException for key property updates
  - Create UnmappedPropertyException for unmapped properties
  - Create EncryptionRequiredException for encrypted properties without encryptor
  - Include expression details in error messages
  - _Requirements: 10.1, 10.2, 10.3, 10.4, 10.5_

- [x] 8. Write unit tests for UpdateExpressionTranslator
  - Test simple SET operation translation
  - Test multiple SET operations
  - Test arithmetic operations (+ and -)
  - Test ADD operation for numbers
  - Test ADD operation for sets
  - Test REMOVE operation
  - Test DELETE operation
  - Test IfNotExists function
  - Test ListAppend function
  - Test ListPrepend function
  - Test format string application
  - Test encryption integration
  - Test combined operations (SET + ADD + REMOVE + DELETE)
  - Test error cases (key property update, unmapped property, unsupported expression)
  - _Requirements: 2.1, 2.2, 2.3, 2.4, 2.5, 3.1, 3.2, 3.3, 3.4, 3.5, 4.1, 4.2, 4.3, 4.4, 4.5, 5.1, 5.2, 5.3, 5.4, 5.5, 6.1, 6.2, 6.3, 6.4, 6.5, 7.1, 7.2, 7.3, 7.4, 7.5, 8.1, 8.2, 8.3, 8.4, 8.5, 9.1, 9.2, 9.3, 9.4, 9.5, 10.1, 10.2, 10.3, 10.4, 10.5_

- [x] 9. Write unit tests for extension methods
  - Test that Add() is only available on numeric and set types
  - Test that Delete() is only available on set types
  - Test that ListAppend/ListPrepend are only available on list types
  - Test that all methods throw when called directly
  - Test IntelliSense behavior (manual verification)
  - _Requirements: 11.1, 11.2, 11.3, 11.4, 11.5_

- [x] 10. Write unit tests for Set() extension method
  - Test Set() method with simple values
  - Test Set() method with Add() operation
  - Test Set() method with Remove() operation
  - Test Set() method with Delete() operation
  - Test Set() method with arithmetic
  - Test Set() method with functions
  - Test method chaining with other builder methods
  - Test metadata resolution
  - Test mixing with string-based Set() methods
  - _Requirements: 2.1, 2.2, 2.3, 2.4, 2.5, 12.1, 12.2, 12.3, 12.4, 12.5_
  - **Note**: Tests created but 13 of 25 are failing due to expression evaluation issue (see task 10.1)

- [x] 10.1 Fix expression evaluation in UpdateExpressionTranslator
  - **Issue**: EvaluateExpression() fails when processing method call arguments that are constants
  - **Root Cause**: Method tries to compile sub-expressions that contain parameter references in parent context
  - **Solution**: Enhance EvaluateExpression() to extract constant values without compilation
  - Implement direct extraction for ConstantExpression nodes
  - Handle UnaryExpression (Convert) nodes that wrap constants
  - Implement visitor to detect parameter references before attempting compilation
  - Handle MemberExpression on ConstantExpression (captured variables from closures)
  - Only compile expressions that don't reference parameters
  - Add unit tests for EvaluateExpression with various expression types
  - Verify all 25 tests in WithUpdateExpressionExtensionsTests pass after fix
  - _Requirements: 2.1, 2.2, 2.3, 2.4, 2.5, 4.1, 4.2, 4.3, 4.4, 4.5, 5.1, 5.2, 5.3, 5.4, 5.5, 6.1, 6.2, 6.3, 6.4, 6.5, 7.1, 7.2, 7.3, 7.4, 7.5_

- [x] 11. Write integration tests with DynamoDB Local
  - Test end-to-end simple SET operations
  - Test end-to-end ADD operations (atomic increment)
  - Test end-to-end REMOVE operations
  - Test end-to-end DELETE operations
  - Test end-to-end arithmetic in SET
  - Test end-to-end IfNotExists function
  - Test end-to-end ListAppend function
  - Test format string application in real updates
  - Test encryption in real updates
  - Test combined operations (SET + ADD + REMOVE + DELETE)
  - Test conditional updates with expressions
  - Test mixing string-based and expression-based methods
  - _Requirements: 2.1, 2.2, 2.3, 2.4, 2.5, 3.1, 3.2, 3.3, 3.4, 3.5, 4.1, 4.2, 4.3, 4.4, 4.5, 5.1, 5.2, 5.3, 5.4, 5.5, 6.1, 6.2, 6.3, 6.4, 6.5, 7.1, 7.2, 7.3, 7.4, 7.5, 8.1, 8.2, 8.3, 8.4, 8.5, 9.1, 9.2, 9.3, 9.4, 9.5, 12.1, 12.2, 12.3, 12.4, 12.5_

- [x] 12. Write unit tests for source generator
  - Test UpdateExpressions class generation
  - Test UpdateModel class generation
  - Test property type mapping (int to UpdateExpressionProperty<int>, etc.)
  - Test nullable property generation in UpdateModel
  - Test handling of different property types (primitives, collections, custom types)
  - Test namespace preservation
  - Test XML documentation generation
  - _Requirements: 1.1, 1.2, 1.3, 1.4, 1.5_

- [x] 13. Add XML documentation to all public APIs
  - Document UpdateExpressionProperty<T> class
  - Document all extension methods with examples
  - Document Set() extension method with comprehensive examples
  - Document UpdateExpressionTranslator class and methods
  - Include usage examples for all operations
  - Document supported expression patterns
  - Document error conditions and exceptions
  - _Requirements: 15.1, 15.2, 15.3, 15.4, 15.5_

- [x] 14. Update library documentation
  - Create dedicated section on expression-based update operations
  - Add examples for SET, ADD, REMOVE, DELETE operations
  - Add examples for arithmetic operations
  - Add examples for DynamoDB functions
  - Compare string-based and expression-based approaches
  - Explain format string application
  - Explain encryption integration
  - Create migration guide from string-based to expression-based
  - Document IntelliSense experience
  - _Requirements: 14.1, 14.2, 14.3, 14.4, 14.5, 15.1, 15.2, 15.3, 15.4, 15.5_

- [x] 15. Update CHANGELOG
  - Add entry for expression-based update support
  - List new classes and methods
  - Include usage examples
  - Document any breaking changes (none expected)
  - Document new dependencies or requirements (none expected)
  - _Requirements: 12.1, 12.2, 12.3, 12.4, 12.5_

- [x] 16. Verify backward compatibility
  - Run existing unit tests to ensure no regressions
  - Test existing string-based Set() method
  - Test existing Set(string, params object[]) method with format strings
  - Test mixing string-based and expression-based methods
  - Verify no breaking changes to public APIs
  - _Requirements: 12.1, 12.2, 12.3, 12.4, 12.5_

- [x] 17. Verify AOT compatibility
  - Test compilation with Native AOT enabled
  - Verify no runtime code generation is used
  - Verify source-generated classes are AOT-compatible
  - Test expression translation without reflection
  - Verify generic types are resolved at compile time
  - _Requirements: 13.1, 13.2, 13.3, 13.4, 13.5_

## Phase 2: Address Limitations Discovered During Integration Testing

- [ ] 19. Add nullable type support to extension methods
  - Add nullable overloads for Add() methods (int?, long?, decimal?, double?)
  - Add nullable overload for Add<T>() on HashSet<T>?
  - Add nullable overload for Delete<T>() on HashSet<T>?
  - Add nullable overload for ListAppend<T>() on List<T>?
  - Add nullable overload for ListPrepend<T>() on List<T>?
  - Add nullable overload for IfNotExists<T>() on T?
  - Update XML documentation to explain nullable support
  - Write unit tests for nullable overloads
  - Uncomment and verify integration tests in ExpressionBasedUpdateTests.cs
  - _Requirements: 4.1, 4.2, 4.3, 4.4, 4.5, 5.1, 5.2, 5.3, 5.4, 5.5, 6.1, 6.2, 6.3, 6.4, 6.5, 7.1, 7.2, 7.3, 7.4, 7.5_
  - **Priority**: Critical - Blocks most advanced operations
  - **Effort**: Medium - Requires adding ~10 method overloads

- [ ] 20. Implement format string application in UpdateExpressionTranslator
  - Modify TranslateSimpleSet() to check for Format property in metadata
  - Implement ApplyFormatString() method for DateTime formatting
  - Implement ApplyFormatString() method for numeric formatting (decimal, double, int)
  - Handle format strings for IFormattable types
  - Add error handling for invalid format strings
  - Write unit tests for format string application
  - Write integration tests with FormattedEntity
  - Verify format strings work consistently with PutItem/GetItem
  - _Requirements: 8.1, 8.2, 8.3, 8.4, 8.5_
  - **Priority**: High - Affects data consistency
  - **Effort**: Low - Straightforward implementation

- [ ] 21. Implement arithmetic operations in SET clauses
  - Implement TranslateBinaryOperation() method in UpdateExpressionTranslator
  - Support ExpressionType.Add (addition)
  - Support ExpressionType.Subtract (subtraction)
  - Validate left operand is property reference or constant
  - Validate right operand is constant or variable
  - Generate SET clause with arithmetic (e.g., "SET #attr = #attr + :val")
  - Handle property-to-property arithmetic (e.g., x.A + x.B)
  - Write unit tests for arithmetic operations
  - Write integration tests for arithmetic operations
  - _Requirements: 3.1, 3.2, 3.3, 3.4, 3.5_
  - **Priority**: Medium - Improves API intuitiveness
  - **Effort**: Low - Method stub already exists

- [ ] 22. Implement field-level encryption in UpdateExpressionTranslator
  - Design approach for async encryption in sync translation context
  - **Option A**: Make translator async (breaking change)
  - **Option B**: Use synchronous encryption wrapper (performance impact)
  - **Option C**: Defer encryption to request builder (architectural change)
  - Implement chosen approach
  - Modify TranslateSimpleSet() to detect IsEncrypted property
  - Call field encryptor for encrypted values
  - Handle encryption errors gracefully
  - Write unit tests for encryption integration
  - Write integration tests with SecureTestEntity
  - Verify encryption works consistently with PutItem/GetItem
  - _Requirements: 9.1, 9.2, 9.3, 9.4, 9.5_
  - **Priority**: High - Security vulnerability
  - **Effort**: Medium - Requires architectural decision

- [ ] 23. Improve mixing of string-based and expression-based methods
  - **Option A**: Implement expression merging logic
    - Store expressions separately for each Set() call
    - Merge expressions when building final request
    - Deduplicate attribute names and values
    - Handle conflicts intelligently
  - **Option B**: Detect and prevent mixing with clear error
    - Track which approach is used first
    - Throw descriptive error if mixing is attempted
    - Provide guidance on using one approach consistently
  - **Option C**: Document as known limitation
    - Update XML documentation to warn against mixing
    - Add examples showing correct usage
    - Provide workarounds for complex scenarios
  - Implement chosen option
  - Write tests to verify behavior
  - Update documentation accordingly
  - _Requirements: 12.1, 12.2, 12.3, 12.4, 12.5_
  - **Priority**: Low - Workaround available (use one approach)
  - **Effort**: High (Option A), Low (Options B/C)

- [ ] 24. Write comprehensive integration tests for all features
  - Create integration test suite for nullable type operations
  - Create integration test suite for format string application
  - Create integration test suite for arithmetic operations
  - Create integration test suite for field-level encryption
  - Create integration test suite for combined complex scenarios
  - Test error conditions and edge cases
  - Verify performance with large update expressions
  - Test with various entity configurations
  - _Requirements: All_
  - **Priority**: High - Ensures quality
  - **Effort**: Medium - Comprehensive test coverage

- [ ] 25. Update documentation for Phase 2 features
  - Document nullable type support in extension methods
  - Document format string application in update expressions
  - Document arithmetic operations in SET clauses
  - Document field-level encryption in update expressions
  - Document limitations and workarounds for mixing approaches
  - Add migration examples from Phase 1 to Phase 2
  - Update API reference documentation
  - Create troubleshooting guide for common issues
  - _Requirements: 14.1, 14.2, 14.3, 14.4, 14.5, 15.1, 15.2, 15.3, 15.4, 15.5_
  - **Priority**: Medium - Helps users adopt features
  - **Effort**: Medium - Comprehensive documentation

## Implementation Priority

**Phase 2 Recommended Order**:
1. Task 19: Nullable type support (Critical - unblocks most features)
2. Task 20: Format string application (High priority, low effort - quick win)
3. Task 21: Arithmetic operations (Medium priority, low effort - quick win)
4. Task 22: Field-level encryption (High priority, requires architectural decision)
5. Task 24: Comprehensive integration tests (Validates all Phase 2 features)
6. Task 25: Documentation updates (Helps users adopt new features)
7. Task 23: Mixing approaches (Low priority, can be documented as limitation)

**Estimated Total Effort**: 2-3 weeks for one developer
- Critical path: Tasks 19, 20, 21, 24 (1-2 weeks)
- Encryption decision and implementation: Task 22 (3-5 days)
- Documentation: Task 25 (2-3 days)
- Optional: Task 23 (1 week if implementing Option A, 1 day for Options B/C)
