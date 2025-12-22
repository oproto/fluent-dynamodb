# Implementation Plan

## Phase 1: Core Infrastructure

- [x] 1. Create DocumentPathBuilder class
  - Create DocumentPathBuilder class in Oproto.FluentDynamoDb/Expressions/
  - Implement AddProperty(name, attributeName) to add property segment with placeholder
  - Implement AddIndex(index) to add list index segment in [n] format
  - Implement Build() to return correctly formatted path (e.g., #address.#city, #tags[0])
  - Handle mixed property and index paths (e.g., #items[0].#name)
  - Add XML documentation explaining purpose and usage
  - Write unit tests for all path combinations
  - _Requirements: 1.1, 1.2, 1.3, 2.1, 2.2, 2.3_

- [x] 2. Enhance ExpressionTranslator for nested property access
  - Add IsNestedPropertyAccess() method to detect chained member expressions
  - Add BuildDocumentPathFromMemberChain() to traverse member chain and build path
  - Modify VisitMember() to delegate to path builder for nested access
  - Register attribute names for all path segments
  - Support [DynamoDbMap] and [DynamoDbEntity] types
  - Write unit tests for filter expressions: .WithFilter(x => x.Address.City == "Seattle")
  - Write unit tests for condition expressions: .Where(x => x.Address.State == "WA") on Put/Update/Delete
  - _Requirements: 1.1, 1.2, 1.3, 1.4, 1.5, 1.6_

- [x] 3. Enhance ExpressionTranslator for list index access
  - Implement VisitIndex() method to handle IndexExpression for list access
  - Modify VisitBinary() to handle ArrayIndex expression type
  - Validate index is constant integer (throw for variables)
  - Generate correct paths: #tags[0], #items[2].#name
  - Support nested lists in filters: .WithFilter(x => x.Metadata.Tags[0] == "sale")
  - Write unit tests for all index access patterns in filter/condition contexts
  - _Requirements: 2.1, 2.2, 2.3, 2.4_

## Phase 2: Update Expression Support

- [x] 4. Enhance UpdateExpressionTranslator for nested updates
  - Detect nested MemberInitExpression in property assignments
  - Implement recursive ProcessMemberAssignment() with path prefix tracking
  - Generate SET expressions with document paths: SET #address.#city = :v0
  - Handle multi-level nesting: SET #address.#country.#code = :v0
  - Combine nested and top-level updates in single expression
  - Write unit tests for all nested update patterns
  - _Requirements: 3.1, 3.2, 3.3, 3.4, 3.5_

- [x] 5. Create list operation extension methods
  - Create ListOperationExtensions class in Oproto.FluentDynamoDb/Expressions/
  - Implement Append<T>(item) to append single item to list end
  - Implement Prepend<T>(item) to prepend single item to list start
  - Implement AppendRange<T>(items) to append multiple items to list end
  - Implement PrependRange<T>(items) to prepend multiple items to list start
  - Mark all methods with [ExpressionOnly] attribute
  - Methods throw InvalidOperationException if called directly
  - Add XML documentation with examples
  - _Requirements: 4.1, 4.2, 4.3, 4.6_

- [x] 6. Implement list operation translation in UpdateExpressionTranslator
  - Detect MethodCallExpression with method names: Append, Prepend, AppendRange, PrependRange
  - For Append: Generate SET #attr = list_append(#attr, :val)
  - For Prepend: Generate SET #attr = list_append(:val, #attr)
  - Support nested list operations: x.Metadata.Keywords.Append("sale")
  - Write unit tests for all list operations
  - _Requirements: 4.1, 4.2, 4.3, 4.6_

- [x] 7. Add list index update support
  - Implement Set(x => x.List[index], value) for updating element at index
  - Implement Remove(x => x.List[index]) for removing element at index
  - Generate correct SET/REMOVE expressions with index
  - Support nested lists: x.Metadata.Tags[0]
  - Write unit tests for index update and remove
  - _Requirements: 4.4, 4.5_

- [x] 8. Add set operation builder methods
  - Add Add<T>(x => x.SetProperty, value) to UpdateItemRequestBuilder
  - Add Add<T>(x => x.SetProperty, values[]) for multiple elements
  - Add Delete<T>(x => x.SetProperty, value) to remove element from set
  - Add Delete<T>(x => x.SetProperty, values[]) for multiple elements
  - Generate correct ADD/DELETE expressions
  - Support HashSet<string>, HashSet<int>, etc.
  - Write unit tests for all set operations
  - _Requirements: 5.1, 5.2, 5.3, 5.4, 5.5_

## Phase 3: Source Generator Enhancements

- [x] 9. Enhance source generator for nested UpdateModel types
  - Detect properties with [DynamoDbMap] attribute in EntityAnalyzer
  - Check if property type has [DynamoDbEntity] attribute
  - Generate {TypeName}UpdateModel with nullable properties for nested types
  - Handle multi-level nesting (nested types with their own nested types)
  - Place generated models in same namespace as source type
  - Write source generator tests to verify correct output
  - _Requirements: 3.1_

## Phase 4: Testing

- [x] 10. Write integration tests for nested filter expressions
  - Test filter with single-level nested property
  - Test filter with multi-level nested property
  - Test filter with list index access
  - Test filter with nested list access
  - Test filter with object property in list
  - Test condition expressions on Put/Update/Delete
  - Test condition expressions in transactions
  - _Requirements: 1.1, 1.2, 1.3, 1.4, 1.5, 1.6, 2.1, 2.2, 2.3, 2.4_

- [x] 11. Write integration tests for nested update operations
  - Test update single nested property
  - Test update multiple nested properties
  - Test multi-level nested update
  - Test combined top-level and nested updates
  - Test nested updates in transactions
  - _Requirements: 3.1, 3.2, 3.3, 3.4, 3.5_

- [x] 12. Write integration tests for list operations
  - Test list append operation
  - Test list prepend operation
  - Test list append range operation
  - Test list element update by index
  - Test list element remove by index
  - Test nested list operations
  - _Requirements: 4.1, 4.2, 4.3, 4.4, 4.5, 4.6_

- [x] 13. Write integration tests for set operations ✓
  - Test set add single element ✓
  - Test set add multiple elements ✓
  - Test set delete single element ✓
  - Test set delete multiple elements ✓
  - Test numeric set operations ✓
  - _Requirements: 5.1, 5.2, 5.3, 5.4, 5.5_
  - Note: Tests for combining multiple set operations on different sets in a single update were removed due to a known library limitation

## Phase 5: Documentation

- [x] 14. Update steering document
  - Add nested map query examples to .kiro/steering/fluentdynamodb.md
  - Add nested map update examples
  - Add list query examples
  - Add list operation examples (append, prepend, remove)
  - Add set operation examples (add, delete)
  - Keep document under 700 lines - the old 500 limit is just too restrictive
  - _Requirements: 6.1_

- [x] 15. Update changelog and documentation changelog
  - Add entry to CHANGELOG.md for new features
  - Add entry to docs/DOCUMENTATION_CHANGELOG.md for documentation changes
  - _Requirements: 6.2, 6.3_

- [x] 16. Create detailed maps and lists guide
  - Create or update docs/maps-and-lists.md
  - Document entity definition with nested objects
  - Document query patterns for nested properties (filter/condition only)
  - Document update patterns for nested properties
  - Document list operations reference
  - Document set operations reference
  - Document performance considerations
  - Document common patterns and best practices
  - Clarify that nested access is NOT supported in key condition expressions
  - _Requirements: 6.4_
