# Implementation Tasks

## Task 1: DocumentPathBuilder Implementation

### Description
Create a new `DocumentPathBuilder` class that constructs DynamoDB document paths from expression member chains, handling both nested properties and list indices.

### Files to Create/Modify
- `Oproto.FluentDynamoDb/Expressions/DocumentPathBuilder.cs` (new)
- `Oproto.FluentDynamoDb.UnitTests/Expressions/DocumentPathBuilderTests.cs` (new)

### Acceptance Criteria
- [ ] `AddProperty(name, attributeName)` adds property segment with attribute name placeholder
- [ ] `AddIndex(index)` adds list index segment in `[n]` format
- [ ] `Build()` returns correctly formatted path (e.g., `#address.#city`, `#tags[0]`)
- [ ] Handles mixed property and index paths (e.g., `#items[0].#name`)
- [ ] Unit tests cover all path combinations

### Dependencies
- None (foundational component)

---

## Task 2: ExpressionTranslator Nested Property Support (Filter/Condition Only)

### Description
Enhance `ExpressionTranslator` to detect and handle chained member expressions for nested property access in **filter expressions** and **condition expressions** (not key condition expressions).

> **Important**: DynamoDB key condition expressions only support partition key and sort key attributes. Nested property access is only valid in filter expressions (`.WithFilter()`) and condition expressions (`.Where()` on Put/Update/Delete).

### Files to Create/Modify
- `Oproto.FluentDynamoDb/Expressions/ExpressionTranslator.cs`
- `Oproto.FluentDynamoDb.UnitTests/Expressions/ExpressionTranslatorNestedTests.cs` (new)

### Acceptance Criteria
- [ ] `IsNestedPropertyAccess()` correctly identifies chained member expressions
- [ ] `BuildDocumentPathFromMemberChain()` traverses member chain and builds path
- [ ] `VisitMember()` delegates to path builder for nested access
- [ ] Attribute names registered for all path segments
- [ ] Works with `[DynamoDbMap]` and `[DynamoDbEntity]` types
- [ ] Unit tests for filter expressions: `.WithFilter(x => x.Address.City == "Seattle")`
- [ ] Unit tests for condition expressions: `.Where(x => x.Address.State == "WA")` on Put/Update/Delete

### Dependencies
- Task 1: DocumentPathBuilder

---

## Task 3: ExpressionTranslator List Index Support (Filter/Condition Only)

### Description
Add support for list/array index access in **filter expressions** and **condition expressions** (not key condition expressions).

> **Important**: List index access is only valid in filter expressions and condition expressions, not in key condition expressions.

### Files to Create/Modify
- `Oproto.FluentDynamoDb/Expressions/ExpressionTranslator.cs`
- `Oproto.FluentDynamoDb.UnitTests/Expressions/ExpressionTranslatorListIndexTests.cs` (new)

### Acceptance Criteria
- [ ] `VisitIndex()` handles `IndexExpression` for list access
- [ ] `VisitBinary()` handles `ArrayIndex` expression type
- [ ] Index must be constant integer (throw for variables)
- [ ] Generates correct path: `#tags[0]`, `#items[2].#name`
- [ ] Works with nested lists in filters: `.WithFilter(x => x.Metadata.Tags[0] == "sale")`
- [ ] Unit tests for all index access patterns in filter/condition contexts

### Dependencies
- Task 1: DocumentPathBuilder
- Task 2: Nested Property Support

---

## Task 4: UpdateExpressionTranslator Nested Updates

### Description
Enhance `UpdateExpressionTranslator` to handle nested `MemberInitExpression` for partial updates of nested objects.

### Files to Create/Modify
- `Oproto.FluentDynamoDb/Expressions/UpdateExpressionTranslator.cs`
- `Oproto.FluentDynamoDb.UnitTests/Expressions/UpdateExpressionTranslatorNestedTests.cs` (new)

### Acceptance Criteria
- [ ] Detect nested `MemberInitExpression` in property assignments
- [ ] Recursively process nested initializers with path prefix
- [ ] Generate SET expressions with document paths: `SET #address.#city = :v0`
- [ ] Handle multi-level nesting: `SET #address.#country.#code = :v0`
- [ ] Combine nested and top-level updates in single expression
- [ ] Unit tests for all nested update patterns

### Dependencies
- Task 1: DocumentPathBuilder

---

## Task 5: List Operation Extension Methods

### Description
Create extension methods for list operations (`Append`, `Prepend`, etc.) that can be used in update expressions.

### Files to Create/Modify
- `Oproto.FluentDynamoDb/Expressions/ListOperationExtensions.cs` (new)
- `Oproto.FluentDynamoDb/Expressions/UpdateExpressionTranslator.cs` (modify)
- `Oproto.FluentDynamoDb.UnitTests/Expressions/ListOperationExtensionsTests.cs` (new)

### Acceptance Criteria
- [ ] `Append<T>(item)` - appends single item to list end
- [ ] `Prepend<T>(item)` - prepends single item to list start
- [ ] `AppendRange<T>(items)` - appends multiple items to list end
- [ ] `PrependRange<T>(items)` - prepends multiple items to list start
- [ ] Methods throw `InvalidOperationException` if called directly
- [ ] `UpdateExpressionTranslator` recognizes and translates these methods
- [ ] Generates correct `list_append` expressions
- [ ] Unit tests for all list operations

### Dependencies
- Task 4: UpdateExpressionTranslator Nested Updates

---

## Task 6: Set Operation Builder Methods

### Description
Add `Add` and `Delete` methods to `UpdateItemRequestBuilder` for set operations.

### Files to Create/Modify
- `Oproto.FluentDynamoDb/Requests/UpdateItemRequestBuilder.cs`
- `Oproto.FluentDynamoDb/Requests/Extensions/UpdateSetOperationExtensions.cs` (new)
- `Oproto.FluentDynamoDb.UnitTests/Requests/UpdateItemRequestBuilderSetOperationsTests.cs` (new)

### Acceptance Criteria
- [ ] `Add<T>(x => x.SetProperty, value)` - adds element to set
- [ ] `Add<T>(x => x.SetProperty, values[])` - adds multiple elements
- [ ] `Delete<T>(x => x.SetProperty, value)` - removes element from set
- [ ] `Delete<T>(x => x.SetProperty, values[])` - removes multiple elements
- [ ] Generates correct ADD/DELETE expressions
- [ ] Works with `HashSet<string>`, `HashSet<int>`, etc.
- [ ] Unit tests for all set operations

### Dependencies
- Task 4: UpdateExpressionTranslator Nested Updates

---

## Task 7: Source Generator - Nested UpdateModel

### Description
Enhance the source generator to create `*UpdateModel` types for nested `[DynamoDbEntity]` types used with `[DynamoDbMap]`.

### Files to Create/Modify
- `Oproto.FluentDynamoDb.SourceGenerator/Generators/EntityGenerator.cs`
- `Oproto.FluentDynamoDb.SourceGenerator/Analysis/EntityAnalyzer.cs`
- `Oproto.FluentDynamoDb.SourceGenerator.UnitTests/NestedUpdateModelGenerationTests.cs` (new)

### Acceptance Criteria
- [ ] Detect properties with `[DynamoDbMap]` attribute
- [ ] Check if property type has `[DynamoDbEntity]` attribute
- [ ] Generate `{TypeName}UpdateModel` with nullable properties
- [ ] Handle multi-level nesting (nested types with their own nested types)
- [ ] Generated models are in same namespace as source type
- [ ] Source generator tests verify correct output

### Dependencies
- None (can be done in parallel with expression work)

---

## Task 8: List Index Update Support

### Description
Add support for updating list elements by index in update expressions.

### Files to Create/Modify
- `Oproto.FluentDynamoDb/Requests/UpdateItemRequestBuilder.cs`
- `Oproto.FluentDynamoDb/Expressions/UpdateExpressionTranslator.cs`
- `Oproto.FluentDynamoDb.UnitTests/Requests/UpdateItemRequestBuilderListIndexTests.cs` (new)

### Acceptance Criteria
- [ ] `Set(x => x.List[index], value)` - updates element at index
- [ ] `Remove(x => x.List[index])` - removes element at index
- [ ] Generates correct SET/REMOVE expressions with index
- [ ] Works with nested lists: `x.Metadata.Tags[0]`
- [ ] Unit tests for index update and remove

### Dependencies
- Task 3: List Index Support
- Task 4: UpdateExpressionTranslator Nested Updates

---

## Task 9: Integration Tests

### Description
Create integration tests that verify end-to-end functionality with DynamoDB Local.

### Files to Create/Modify
- `Oproto.FluentDynamoDb.IntegrationTests/NestedMapFilterTests.cs` (new)
- `Oproto.FluentDynamoDb.IntegrationTests/NestedMapConditionTests.cs` (new)
- `Oproto.FluentDynamoDb.IntegrationTests/NestedMapUpdateTests.cs` (new)
- `Oproto.FluentDynamoDb.IntegrationTests/ListOperationTests.cs` (new)
- `Oproto.FluentDynamoDb.IntegrationTests/SetOperationTests.cs` (new)

### Acceptance Criteria
- [ ] Filter with nested property works end-to-end (`.WithFilter(x => x.Address.City == "Seattle")`)
- [ ] Condition expression with nested property works (Put/Update/Delete `.Where()`)
- [ ] Update nested property works end-to-end
- [ ] List append/prepend operations work end-to-end
- [ ] List element update by index works end-to-end
- [ ] Set add/delete operations work end-to-end
- [ ] Combined operations work correctly

### Dependencies
- Tasks 1-8 (all implementation tasks)

---

## Task 10: Documentation Updates

### Description
Update all documentation to reflect new nested map and list capabilities.

### Files to Create/Modify
- `.kiro/steering/fluentdynamodb.md`
- `CHANGELOG.md`
- `docs/DOCUMENTATION_CHANGELOG.md`
- `docs/maps-and-lists.md` (new or update)

### Acceptance Criteria
- [ ] Steering document includes nested query examples
- [ ] Steering document includes nested update examples
- [ ] Steering document includes list operation examples
- [ ] Steering document includes set operation examples
- [ ] CHANGELOG has entry for new features
- [ ] Documentation changelog tracks changes
- [ ] Detailed guide covers all patterns and best practices

### Dependencies
- Tasks 1-9 (documentation after implementation)

---

## Task Dependencies Graph

```
Task 1 (DocumentPathBuilder)
    │
    ├──► Task 2 (Nested Property Support)
    │        │
    │        └──► Task 3 (List Index Support)
    │                 │
    │                 └──► Task 8 (List Index Updates)
    │
    └──► Task 4 (Nested Updates)
             │
             ├──► Task 5 (List Operations)
             │
             └──► Task 6 (Set Operations)

Task 7 (Source Generator) ──► [parallel, no dependencies]

Tasks 1-8 ──► Task 9 (Integration Tests)

Tasks 1-9 ──► Task 10 (Documentation)
```

## Estimated Effort

| Task | Complexity | Estimated Hours |
|------|------------|-----------------|
| Task 1 | Low | 2-3 |
| Task 2 | Medium | 4-6 |
| Task 3 | Medium | 3-4 |
| Task 4 | High | 6-8 |
| Task 5 | Medium | 4-5 |
| Task 6 | Medium | 4-5 |
| Task 7 | Medium | 4-6 |
| Task 8 | Medium | 3-4 |
| Task 9 | Medium | 4-6 |
| Task 10 | Low | 2-3 |
| **Total** | | **36-50 hours** |
