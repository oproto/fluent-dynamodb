# Implementation Plan

## Phase 1: New Extension Methods

- [x] 1. Add SetAt extension method to ListOperationExtensions
  - Add SetAt<T>(this List<T> list, int index, T value) method
  - Mark with [ExpressionOnly] attribute
  - Throw InvalidOperationException if called directly
  - Add comprehensive XML documentation with examples
  - Document dynamic index support in remarks
  - Write unit test to verify direct call throws InvalidOperationException
  - _Requirements: 1.1, 1.3, 1.5_

- [x] 2. Add RemoveAt extension method to ListOperationExtensions
  - Add RemoveAt<T>(this List<T> list, int index) method
  - Mark with [ExpressionOnly] attribute
  - Throw InvalidOperationException if called directly
  - Add comprehensive XML documentation with examples
  - Document dynamic index support in remarks
  - Write unit test to verify direct call throws InvalidOperationException
  - _Requirements: 1.2, 1.3, 1.5_

## Phase 2: Update Expression Translation

- [x] 3. Implement SetAt translation in UpdateExpressionTranslator
  - Detect MethodCallExpression with method name "SetAt"
  - Extract list path from methodCall.Object
  - Extract index from methodCall.Arguments[0]
  - Extract value from methodCall.Arguments[1]
  - Generate SET expression: SET #path[index] = :val
  - Support nested lists: x.Metadata.Tags.SetAt(0, "value")
  - Write unit tests for constant index translation
  - _Requirements: 1.1, 1.3_

- [x] 4. Implement RemoveAt translation in UpdateExpressionTranslator
  - Detect MethodCallExpression with method name "RemoveAt"
  - Extract list path from methodCall.Object
  - Extract index from methodCall.Arguments[0]
  - Generate REMOVE expression: REMOVE #path[index]
  - Support nested lists: x.Metadata.Tags.RemoveAt(1)
  - Write unit tests for constant index translation
  - _Requirements: 1.2, 1.3_

- [x] 5. Implement chained SetAt support ✓
  - Detect chained SetAt calls: x.Tags.SetAt(0, "a").SetAt(1, "b") ✓
  - Walk the method call chain to collect all SetAt operations ✓
  - Generate combined SET expression: SET #tags[0] = :v0, #tags[1] = :v1 ✓
  - Validate all indices are different (throw if duplicate index) ✓
  - Write unit tests for chained SetAt operations ✓
  - _Requirements: 1.1, 1.3_

- [x] 6. Implement overlapping path detection and rejection
  - Detect when chained operations would create overlapping paths
  - Throw UnsupportedExpressionException for: SetAt + Append, SetAt + RemoveAt, Append + RemoveAt
  - Provide clear error message explaining DynamoDB limitation
  - Write unit tests for all disallowed combinations
  - _Requirements: DynamoDB limitation handling_

## Phase 3: Dynamic Index Support

- [x] 7. Create ParameterReferenceVisitor helper class
  - Create visitor that walks expression tree
  - Track if target ParameterExpression is referenced
  - Expose ReferencesParameter boolean property
  - Write unit tests for various expression patterns
  - _Requirements: 2.4, 3.5_

- [x] 8. Implement dynamic index evaluation for update expressions
  - Add EvaluateIndexExpression method to UpdateExpressionTranslator
  - Fast path for ConstantExpression (existing behavior)
  - Check for entity parameter reference using ParameterReferenceVisitor
  - Throw UnsupportedExpressionException if entity parameter referenced
  - Compile and invoke expression to get integer value
  - Add ValidateIndex method to check non-negative
  - Throw ArgumentOutOfRangeException for negative indices
  - Update SetAt translation to use EvaluateIndexExpression
  - Update RemoveAt translation to use EvaluateIndexExpression
  - Write unit tests for variable index: int i = 1; .Set(x => x.Tags.SetAt(i, "val"))
  - Write unit tests for method call index: .Set(x => x.Tags.SetAt(GetIndex(), "val"))
  - Write unit tests for property access index: .Set(x => x.Tags.SetAt(config.Index, "val"))
  - Write unit tests for entity parameter rejection
  - Write unit tests for negative index rejection
  - _Requirements: 3.1, 3.2, 3.3, 3.4, 3.5, 3.6_

- [x] 9. Implement dynamic index evaluation for filter/condition expressions
  - Update ExpressionTranslator list index handling
  - Add EvaluateIndexExpression method (similar to update translator)
  - Check for entity parameter reference
  - Throw UnsupportedExpressionException if entity parameter referenced
  - Compile and invoke expression to get integer value
  - Validate index is non-negative
  - Update HandleListIndexAccess to use dynamic evaluation
  - Write unit tests for variable index in filter: int i = 0; .WithFilter(x => x.Tags[i] == "val")
  - Write unit tests for method call index in filter: .WithFilter(x => x.Tags[GetIndex()] == "val")
  - Write unit tests for property access index in filter: .WithFilter(x => x.Tags[config.Index] == "val")
  - Write unit tests for condition expressions: .Where(x => x.Tags[i] == "expected")
  - Write unit tests for entity parameter rejection in filter
  - _Requirements: 2.1, 2.2, 2.3, 2.4, 2.5_

## Phase 4: Remove Old Builder Methods

- [x] 10. Remove old SetAt/RemoveAt builder methods
  - Remove SetAt<T, TEntity, TElement> method from WithUpdateExpressionExtensions.cs
  - Remove RemoveAt<T, TEntity, TElement> method from WithUpdateExpressionExtensions.cs
  - Remove UpdateItemRequestBuilder<TEntity> overloads for SetAt/RemoveAt
  - Remove ExtractListIndexPath helper method (no longer needed)
  - Remove GetAttributeNameFromMember helper method (if only used by removed methods)
  - Remove CaptureValueForListIndex helper method (if only used by removed methods)
  - Update any tests that used the old builder methods to use new extension methods
  - _Requirements: 1.4_

## Phase 5: Integration Tests

- [x] 11. Write integration tests for new extension methods
  - Test SetAt with constant index against DynamoDB
  - Test SetAt with variable index against DynamoDB
  - Test RemoveAt with constant index against DynamoDB
  - Test RemoveAt with variable index against DynamoDB
  - Test nested list SetAt/RemoveAt operations (using update model pattern)
  - Test chained SetAt operations: x.Tags.SetAt(0, "a").SetAt(1, "b")
  - Verify correct DynamoDB behavior (element updated/removed)
  - _Requirements: 1.1, 1.2, 1.3, 3.1, 3.2_

- [x] 12. Write integration tests for dynamic index in queries ✓
  - Test filter with variable index against DynamoDB ✓
  - Test filter with method call index against DynamoDB ✓
  - Test condition expression with variable index ✓
  - Verify correct items are returned/affected ✓
  - _Requirements: 2.1, 2.2, 2.3, 2.5_

## Phase 6: Documentation

- [x] 13. Truncate DOCUMENTATION_CHANGELOG.md
  - Clear existing content (external sources synchronized)
  - Add header explaining purpose of file
  - Add single entry noting fresh start after synchronization
  - _Requirements: 4.4_

- [x] 14. Update steering document (fluentdynamodb.md)
  - Add SetAt extension method pattern to List Expressions section
  - Add RemoveAt extension method pattern to List Expressions section
  - Remove old builder method patterns (.SetAt/.RemoveAt on builder)
  - Add dynamic index examples for all list operations
  - Add note about chained SetAt support
  - Add note about DynamoDB overlapping path limitation
  - Update List Operations Reference table if needed
  - Keep document under 700 lines
  - Add entry to DOCUMENTATION_CHANGELOG.md for steering doc changes
  - _Requirements: 4.1_

- [x] 15. Update MapsAndLists.md documentation
  - Update "Updating List Elements by Index" section with new extension method pattern
  - Update "Removing List Elements by Index" section with new extension method pattern
  - Remove any references to old builder methods
  - Add "Dynamic Index Support" subsection
  - Add "Chaining List Operations" subsection explaining what's allowed
  - Document variable, method call, and property access index patterns
  - Document entity parameter restriction
  - Document DynamoDB overlapping path limitation
  - Add examples for all dynamic index patterns
  - Update Quick Reference table
  - Add entry to DOCUMENTATION_CHANGELOG.md for MapsAndLists.md changes
  - _Requirements: 4.2_

- [x] 16. Update CHANGELOG.md
  - Add entry for new SetAt/RemoveAt extension methods
  - Add entry for dynamic index support
  - Add entry for chained SetAt support
  - Note removal of old builder methods (breaking change, but methods were brand new)
  - _Requirements: 4.3_
