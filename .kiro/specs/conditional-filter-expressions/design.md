# Design Document: Conditional Filter Expressions

## Overview

This feature enhances the `ExpressionTranslator` to support natural conditional filtering patterns using `||` and `&&` operators with local boolean conditions. The implementation extends the existing `VisitBinary` method to detect when one operand of a logical operator doesn't reference the entity parameter, evaluate it at translation time, and conditionally include or omit the filter clause.

## Architecture

The enhancement is localized to the `ExpressionTranslator.VisitBinary` method. No new classes or interfaces are required.

### Current Flow (Ternary Pattern)
```
Lambda Expression → Visit → VisitConditional → Evaluate condition → Select branch
```

### Enhanced Flow (Binary Pattern)
```
Lambda Expression → Visit → VisitBinary → Detect local operand → Evaluate → Conditionally translate
```

## Components and Interfaces

### Modified Component: ExpressionTranslator

The `VisitBinary` method will be enhanced to handle conditional filtering patterns:

```csharp
private string VisitBinary(BinaryExpression node, ParameterExpression entityParameter, ExpressionContext context)
{
    // Handle logical operators (&&, ||)
    if (node.NodeType == ExpressionType.AndAlso || node.NodeType == ExpressionType.OrElse)
    {
        var leftReferencesEntity = ReferencesEntityParameter(node.Left, entityParameter);
        var rightReferencesEntity = ReferencesEntityParameter(node.Right, entityParameter);
        
        // Case 1: Neither side references entity - evaluate entire expression
        if (!leftReferencesEntity && !rightReferencesEntity)
        {
            return EvaluateAndHandleLocalBooleanExpression(node, context);
        }
        
        // Case 2: Only one side references entity - conditional filter pattern
        if (leftReferencesEntity != rightReferencesEntity)
        {
            return HandleConditionalFilterPattern(node, entityParameter, context, 
                leftReferencesEntity, rightReferencesEntity);
        }
        
        // Case 3: Both sides reference entity
        if (node.NodeType == ExpressionType.OrElse)
        {
            // OR between two entity conditions is not supported in DynamoDB key expressions
            throw new UnsupportedExpressionException(
                "OR operator between two entity property conditions is not supported in DynamoDB expressions. " +
                "Use separate queries or restructure your data model.",
                node);
        }
        
        // AND between two entity conditions - existing behavior
        return TranslateBothOperands(node, entityParameter, context);
    }
    
    // ... rest of existing VisitBinary logic
}

private string HandleConditionalFilterPattern(
    BinaryExpression node, 
    ParameterExpression entityParameter, 
    ExpressionContext context,
    bool leftReferencesEntity,
    bool rightReferencesEntity)
{
    var localOperand = leftReferencesEntity ? node.Right : node.Left;
    var entityOperand = leftReferencesEntity ? node.Left : node.Right;
    
    // Evaluate the local operand
    bool localValue;
    try
    {
        var evaluated = EvaluateExpression(localOperand);
        localValue = evaluated is bool b ? b : Convert.ToBoolean(evaluated);
    }
    catch (Exception ex)
    {
        throw new ExpressionTranslationException(
            $"Failed to evaluate local condition in filter expression: {ex.Message}",
            node);
    }
    
    if (node.NodeType == ExpressionType.OrElse)
    {
        // OR pattern: (localCondition || entityFilter)
        // If local is true → skip filter (return empty)
        // If local is false → apply entity filter
        if (localValue)
        {
            return string.Empty;
        }
        return Visit(entityOperand, entityParameter, context);
    }
    else // AndAlso
    {
        // AND pattern: (localCondition && entityFilter)
        // If local is true → apply entity filter
        // If local is false → skip filter (return empty)
        if (localValue)
        {
            return Visit(entityOperand, entityParameter, context);
        }
        return string.Empty;
    }
}
```

### Helper Method: EvaluateAndHandleLocalBooleanExpression

For expressions where neither operand references the entity:

```csharp
private string EvaluateAndHandleLocalBooleanExpression(
    BinaryExpression node, 
    ExpressionContext context)
{
    bool result;
    try
    {
        var evaluated = EvaluateExpression(node);
        result = evaluated is bool b ? b : Convert.ToBoolean(evaluated);
    }
    catch (Exception ex)
    {
        throw new ExpressionTranslationException(
            $"Failed to evaluate local boolean expression: {ex.Message}",
            node);
    }
    
    if (result)
    {
        // Expression evaluates to true - return empty to omit
        return string.Empty;
    }
    else
    {
        // Expression evaluates to false - this would filter out everything
        throw new UnsupportedExpressionException(
            "Filter expression evaluates to constant false, which would return no results. " +
            "Remove the filter or fix the condition.",
            node);
    }
}
```

## Data Models

No new data models are required. The feature uses existing:
- `ExpressionContext` for translation state
- `UnsupportedExpressionException` for error cases
- `ExpressionTranslationException` for evaluation failures

## Correctness Properties

*A property is a characteristic or behavior that should hold true across all valid executions of a system—essentially, a formal statement about what the system should do. Properties serve as the bridge between human-readable specifications and machine-verifiable correctness guarantees.*

### Property 1: OR with Local Condition Behavior

*For any* binary OR expression where exactly one operand does not reference the entity parameter:
- If the local operand evaluates to `true`, the translator SHALL return an empty string
- If the local operand evaluates to `false`, the translator SHALL return only the translation of the entity operand

**Validates: Requirements 1.1, 1.2, 2.1, 2.2**

### Property 2: AND with Local Condition Behavior

*For any* binary AND expression where exactly one operand does not reference the entity parameter:
- If the local operand evaluates to `true`, the translator SHALL return only the translation of the entity operand
- If the local operand evaluates to `false`, the translator SHALL return an empty string

**Validates: Requirements 3.1, 3.2**

### Property 3: Negation Evaluation

*For any* local condition that includes NOT operators, the translator SHALL correctly evaluate the complete boolean expression including all negations before determining filter behavior.

**Validates: Requirements 4.1, 4.2, 4.3**

### Property 4: Method Call and Compound Condition Evaluation

*For any* local condition that is a method call or compound boolean expression not referencing the entity parameter, the translator SHALL evaluate it at translation time and use the result to determine filter behavior.

**Validates: Requirements 5.1, 5.2**

### Property 5: Chained Conditional Filters

*For any* expression containing multiple conditional filter patterns combined with AND, the translator SHALL:
- Evaluate each conditional independently
- Combine non-empty results with AND
- Return only non-conditional parts if all conditionals evaluate to empty

**Validates: Requirements 6.1, 6.2, 6.3**

### Property 6: Backward Compatibility

*For any* ternary conditional expression using the existing pattern `(condition ? branch1 : branch2)`, the translator SHALL produce the same output as before this enhancement.

**Validates: Requirements 7.1, 7.2, 7.3**

### Property 7: OR Between Entity Conditions Throws

*For any* binary OR expression where both operands reference the entity parameter, the translator SHALL throw an `UnsupportedExpressionException`.

**Validates: Requirements 1.3, 2.3**

## Error Handling

| Scenario | Exception Type | Message |
|----------|---------------|---------|
| OR between two entity conditions | `UnsupportedExpressionException` | "OR operator between two entity property conditions is not supported..." |
| Local condition evaluation fails | `ExpressionTranslationException` | "Failed to evaluate local condition in filter expression: {details}" |
| Expression evaluates to constant false | `UnsupportedExpressionException` | "Filter expression evaluates to constant false..." |

## Testing Strategy

### Unit Tests

Unit tests will cover specific examples and edge cases:

1. **Basic OR patterns**: `(true || x.Prop == val)`, `(false || x.Prop == val)`
2. **Basic AND patterns**: `(true && x.Prop == val)`, `(false && x.Prop == val)`
3. **Operand order**: Local condition on left vs right
4. **Negation**: `(!flag || x.Prop == val)`
5. **Method calls**: `(string.IsNullOrWhiteSpace(s) || x.Prop == val)`
6. **Compound conditions**: `((a && b) || x.Prop == val)`
7. **Chained conditionals**: Multiple conditional filters in one expression
8. **Error cases**: OR between entity conditions, evaluation failures

### Property-Based Tests

Property tests will verify universal properties across generated inputs:

1. **OR behavior property**: Generate random boolean values and entity filters, verify correct omission/inclusion
2. **AND behavior property**: Generate random boolean values and entity filters, verify correct omission/inclusion
3. **Negation property**: Generate negated conditions, verify correct evaluation
4. **Chaining property**: Generate chains of conditionals, verify correct combination
5. **Backward compatibility property**: Run existing ternary patterns, verify unchanged behavior

### Test Configuration

- Property tests: Minimum 100 iterations per property
- Use FsCheck for property-based testing (consistent with existing test infrastructure)
- Tag format: **Feature: conditional-filter-expressions, Property {number}: {property_text}**

## Documentation Updates

### Steering Document (.kiro/steering/fluentdynamodb.md)

Add conditional filter patterns to the Lambda Expression Functions section:

```markdown
## Conditional Filter Patterns

| Pattern | Behavior | Example |
|---------|----------|---------|
| `localTrue \|\| x.Prop == val` | Skip filter | `string.IsNullOrWhiteSpace(s) \|\| x.Status == s` |
| `localFalse \|\| x.Prop == val` | Apply filter | `hasFilter \|\| x.Status == status` |
| `localTrue && x.Prop == val` | Apply filter | `includeFilter && x.Status == status` |
| `localFalse && x.Prop == val` | Skip filter | `!includeFilter && x.Status == status` |

**Common Use Cases:**
```csharp
// Optional filter based on parameter presence
.Where(x => x.Key == key && (string.IsNullOrWhiteSpace(status) || x.Status == status))

// Feature flag controlled filter
.WithFilter(x => enableDateFilter && x.Date > minDate)

// Multiple optional filters
.Where(x => x.Key == key)
.WithFilter(x => (skipStatusFilter || x.Status == status) && (skipDateFilter || x.Date > minDate))
```
```

### CHANGELOG.md

Add entry under appropriate version:

```markdown
### Added
- Conditional filter expressions: Support for natural `||` and `&&` patterns with local boolean conditions
  - `(localCondition || x.Property == value)` - skip filter when condition is true
  - `(localCondition && x.Property == value)` - include filter only when condition is true
  - Works with method calls like `string.IsNullOrWhiteSpace()`, negations, and compound conditions
```

### DOCUMENTATION_CHANGELOG.md

Add entry for documentation synchronization:

```markdown
## [YYYY-MM-DD]

### File: .kiro/steering/fluentdynamodb.md

**Added:**
New section "Conditional Filter Patterns" documenting natural conditional filtering with `||` and `&&` operators.

**Reason:** New feature added to ExpressionTranslator supporting conditional filter expressions without requiring ternary operators.
```
