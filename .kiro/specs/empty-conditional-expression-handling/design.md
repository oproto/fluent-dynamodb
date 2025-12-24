# Design Document: Empty Conditional Expression Handling

## Overview

This feature enhances the conditional filter expression handling in Oproto.FluentDynamoDb to gracefully handle scenarios where all conditional parts of an expression evaluate to "skip". Currently, when this happens, the expression translator produces an empty string, which causes DynamoDB to throw an "Invalid FilterExpression: The expression can not be empty" error.

The solution is to detect empty expressions at the request builder level and simply not apply the filter/condition expression, allowing the operation to proceed without the expression.

## Architecture

The change is minimal and localized to the request builder layer. The `ExpressionTranslator` already correctly returns empty strings when all conditionals evaluate to skip. The fix is to check for empty strings in the `SetFilterExpression` and `SetConditionExpression` methods before setting the expression on the request.

```
┌─────────────────────────────────────────────────────────────────┐
│                     Lambda Expression                            │
│         x => (skipFilter || x.Status == status)                  │
└─────────────────────────────────────────────────────────────────┘
                              │
                              ▼
┌─────────────────────────────────────────────────────────────────┐
│                   ExpressionTranslator                           │
│  - Evaluates conditional patterns                                │
│  - Returns empty string when all conditionals skip               │
│  (No changes needed - already works correctly)                   │
└─────────────────────────────────────────────────────────────────┘
                              │
                              ▼
┌─────────────────────────────────────────────────────────────────┐
│                    Request Builders                              │
│  SetFilterExpression / SetConditionExpression                    │
│  - NEW: Check if expression is empty/whitespace                  │
│  - If empty: return builder without setting expression           │
│  - If non-empty: set expression as before                        │
└─────────────────────────────────────────────────────────────────┘
                              │
                              ▼
┌─────────────────────────────────────────────────────────────────┐
│                    DynamoDB Request                              │
│  - FilterExpression/ConditionExpression is null (not set)        │
│  - Operation executes without filter/condition                   │
└─────────────────────────────────────────────────────────────────┘
```

## Components and Interfaces

### Modified Components

#### 1. QueryRequestBuilder.SetFilterExpression

```csharp
public QueryRequestBuilder<TEntity> SetFilterExpression(string expression)
{
    // NEW: Skip setting if expression is empty (all conditionals evaluated to skip)
    if (string.IsNullOrWhiteSpace(expression))
    {
        return this;
    }
    
    if (string.IsNullOrEmpty(_req.FilterExpression))
    {
        _req.FilterExpression = expression;
    }
    else
    {
        _req.FilterExpression = $"({_req.FilterExpression}) AND ({expression})";
    }
    return this;
}
```

#### 2. ScanRequestBuilder.SetFilterExpression

Same pattern as QueryRequestBuilder.

#### 3. PutItemRequestBuilder.SetConditionExpression

```csharp
public PutItemRequestBuilder<TEntity> SetConditionExpression(string expression)
{
    // NEW: Skip setting if expression is empty (all conditionals evaluated to skip)
    if (string.IsNullOrWhiteSpace(expression))
    {
        return this;
    }
    
    if (string.IsNullOrEmpty(_req.ConditionExpression))
    {
        _req.ConditionExpression = expression;
    }
    else
    {
        _req.ConditionExpression = $"({_req.ConditionExpression}) AND ({expression})";
    }
    return this;
}
```

#### 4. UpdateItemRequestBuilder.SetConditionExpression

Same pattern as PutItemRequestBuilder.

#### 5. DeleteItemRequestBuilder.SetConditionExpression

Same pattern as PutItemRequestBuilder.

### Affected Request Builders

| Builder | Method | Change |
|---------|--------|--------|
| `QueryRequestBuilder<TEntity>` | `SetFilterExpression` | Add empty check |
| `ScanRequestBuilder<TEntity>` | `SetFilterExpression` | Add empty check |
| `PutItemRequestBuilder<TEntity>` | `SetConditionExpression` | Add empty check |
| `UpdateItemRequestBuilder<TEntity>` | `SetConditionExpression` | Add empty check |
| `DeleteItemRequestBuilder<TEntity>` | `SetConditionExpression` | Add empty check |

## Data Models

No new data models are required. The change is purely behavioral.

## Correctness Properties

*A property is a characteristic or behavior that should hold true across all valid executions of a system-essentially, a formal statement about what the system should do. Properties serve as the bridge between human-readable specifications and machine-verifiable correctness guarantees.*

### Property 1: All-Skip Conditional Expressions Produce No Filter

*For any* filter expression composed entirely of conditional clauses where all local conditions evaluate to true (skip), the resulting request SHALL have no FilterExpression set.

**Validates: Requirements 1.1, 1.2, 1.3, 3.1, 3.2, 3.3**

### Property 2: All-Skip Conditional Expressions Produce No Condition

*For any* condition expression on a write operation (Put, Update, Delete) composed entirely of conditional clauses where all local conditions evaluate to true (skip), the resulting request SHALL have no ConditionExpression set.

**Validates: Requirements 2.1, 2.2, 2.3**

### Property 3: Partial-Skip Conditional Expressions Produce Valid Filter

*For any* filter expression containing at least one conditional clause where the local condition evaluates to false (apply), the resulting request SHALL have a valid FilterExpression containing only the applied clauses.

**Validates: Requirements 1.4**

### Property 4: Conditional Filter Pattern Truth Table

*For any* conditional filter pattern:
- `(true || entityFilter)` SHALL skip the filter (return empty)
- `(false || entityFilter)` SHALL apply the filter
- `(true && entityFilter)` SHALL apply the filter
- `(false && entityFilter)` SHALL skip the filter (return empty)

**Validates: Requirements 4.1, 4.2, 4.3, 4.4**

## Error Handling

### Existing Behavior Preserved

The existing error handling for invalid expressions remains unchanged:
- `UnsupportedExpressionException`: Thrown for unsupported operators or patterns
- `ExpressionTranslationException`: Thrown when expression cannot be translated
- `UnmappedPropertyException`: Thrown when property doesn't map to DynamoDB attribute

### New Behavior

When all conditionals evaluate to skip:
- **Before**: DynamoDB throws "Invalid FilterExpression: The expression can not be empty"
- **After**: Operation executes without filter/condition (returns all items for queries, unconditional write for mutations)

This is the expected behavior because the user explicitly wrote conditional filters that all evaluated to "skip this filter".

## Documentation Updates

### CHANGELOG.md

Add entry under "Changed" section:
```markdown
### Changed
- Conditional filter expressions that resolve to all-skip conditions now gracefully execute without a filter instead of throwing "Invalid FilterExpression: The expression can not be empty" error
```

### docs/BREAKING_CHANGES_v1.0.md

Add entry documenting the behavior change:
```markdown
### Conditional Filter Expression Empty Handling

**Previous Behavior:** When all conditional clauses in a filter expression evaluated to "skip" (e.g., `x => true || x.Status == status` where the local condition is always true), DynamoDB would throw an error: "Invalid FilterExpression: The expression can not be empty".

**New Behavior:** The operation executes without a filter, returning all items (for queries) or performing an unconditional write (for mutations).

**Migration:** No code changes required. This is a quality-of-life improvement that eliminates the need to wrap `.WithFilter()` calls in conditional checks when using conditional filter patterns.
```

### docs/DOCUMENTATION_CHANGELOG.md

Add entry documenting the documentation update:
```markdown
## [YYYY-MM-DD]

### File: docs/core-features/ConditionalFilters.md (or relevant file)

**Before:**
Conditional filter patterns that all evaluate to skip would throw a DynamoDB error.

**After:**
Conditional filter patterns that all evaluate to skip gracefully execute without a filter.

**Reason:** Behavior change to improve developer experience with conditional filter patterns.
```

### .kiro/steering/fluentdynamodb.md

Update the Conditional Filter Patterns section to document the new behavior:
```markdown
## Conditional Filter Patterns

...

**Empty Expression Handling:**
When all conditional clauses evaluate to skip (e.g., all local conditions are `true` in OR patterns), the filter is gracefully omitted and the operation executes without filtering. This eliminates the need to wrap `.WithFilter()` in conditional checks.

```csharp
// Safe to use even when all conditions might skip
var orders = await table.Orders.Query(x => x.CustomerId == customerId)
    .WithFilter(x => 
        (string.IsNullOrWhiteSpace(status) || x.Status == status) &&
        (string.IsNullOrWhiteSpace(category) || x.Category == category))
    .ToListAsync();
// If both status and category are null/empty, query executes without filter
```
```

## Testing Strategy

### Unit Tests

1. **Empty expression handling in SetFilterExpression**
   - Test that empty string is not set as FilterExpression
   - Test that whitespace-only string is not set as FilterExpression
   - Test that valid expressions are still set correctly

2. **Empty expression handling in SetConditionExpression**
   - Same tests for each builder type (Put, Update, Delete)

3. **Integration with conditional filter patterns**
   - Test `(true || x.Status == status)` produces no filter
   - Test `(false || x.Status == status)` produces filter
   - Test `(true && x.Status == status)` produces filter
   - Test `(false && x.Status == status)` produces no filter

### Property-Based Tests

Using FsCheck for property-based testing:

1. **Property 1**: Generate random combinations of all-true conditional clauses, verify no FilterExpression
2. **Property 2**: Generate random combinations of all-true conditional clauses for write operations, verify no ConditionExpression
3. **Property 3**: Generate expressions with at least one false conditional, verify FilterExpression contains only applied clauses
4. **Property 4**: Test the conditional filter truth table with random entity filter expressions

### Test Configuration

- Minimum 100 iterations per property test
- Each property test must reference its design document property
- Tag format: **Feature: empty-conditional-expression-handling, Property {number}: {property_text}**
