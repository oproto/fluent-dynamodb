# Design Document

## Overview

This design addresses API consistency issues in list index operations and adds support for dynamic (non-constant) indices in lambda expressions. The implementation extends the existing `ListOperationExtensions` class with new `SetAt` and `RemoveAt` extension methods, and enhances the expression translator to evaluate non-constant index expressions at translation time.

## Architecture

### Component Diagram

```
┌─────────────────────────────────────────────────────────────────┐
│                    Current API (Inconsistent)                    │
│  .Set(x => x.Tags.Append("new"))        ← Extension method      │
│  .SetAt(x => x.Tags[0], "updated")      ← Builder method        │
│  .RemoveAt(x => x.Tags[2])              ← Builder method        │
└─────────────────────────────────────────────────────────────────┘
                              ↓
┌─────────────────────────────────────────────────────────────────┐
│                    New API (Consistent)                          │
│  .Set(x => x.Tags.Append("new"))        ← Extension method      │
│  .Set(x => x.Tags.SetAt(0, "updated"))  ← Extension method      │
│  .Set(x => x.Tags.RemoveAt(2))          ← Extension method      │
│                                                                  │
│  // Old builder methods REMOVED                                  │
│  // .SetAt(x => x.Tags[0], "updated")   ← REMOVED               │
│  // .RemoveAt(x => x.Tags[2])           ← REMOVED               │
└─────────────────────────────────────────────────────────────────┘
```

### Dynamic Index Support Flow

```
Developer writes:
  int index = GetIndex();
  .WithFilter(x => x.Tags[index] == "featured")

↓

ExpressionTranslator detects non-constant index:
  MethodCallExpression {
    Method = "get_Item",
    Arguments = [ MemberExpression { index } ]  ← Not ConstantExpression
  }

↓

Check if index references entity parameter:
  - Walk expression tree looking for ParameterExpression
  - If found → throw UnsupportedExpressionException
  - If not found → evaluate expression

↓

Evaluate index expression:
  Expression.Lambda(indexExpr).Compile().DynamicInvoke()
  → Returns: 2 (for example)

↓

Validate index:
  - Must be non-negative
  - If negative → throw ArgumentOutOfRangeException

↓

Generate expression with evaluated index:
  "#tags[2] = :v0"
```

## DynamoDB Limitations - Overlapping Paths

DynamoDB does not allow multiple operations on overlapping document paths in a single update expression. This is a fundamental DynamoDB limitation, not a library limitation.

### What Works

```csharp
// Multiple SetAt on different indices - ALLOWED
.Set(x => x.Tags.SetAt(0, "a").SetAt(1, "b"))
// Generates: SET #tags[0] = :v0, #tags[1] = :v1
```

### What Doesn't Work

```csharp
// SetAt + Append - NOT ALLOWED (overlapping paths)
.Set(x => x.Tags.SetAt(0, "a").Append("new"))
// Would generate: SET #tags[0] = :v0, #tags = list_append(#tags, :v1)
// DynamoDB error: "Two document paths overlap with each other"
```

### Implementation Approach

The extension methods return `List<T>` for C# syntax compatibility, but the translator will detect and reject chained operations that would create overlapping paths at translation time with a clear error message.

## Components and Interfaces

### 1. New Extension Methods in ListOperationExtensions

```csharp
namespace Oproto.FluentDynamoDb.Expressions;

public static class ListOperationExtensions
{
    // Existing methods: Append, Prepend, AppendRange, PrependRange
    
    /// <summary>
    /// Sets the value at a specific index in a list.
    /// Translates to: SET #attr[index] = :val
    /// </summary>
    /// <typeparam name="T">The type of elements in the list.</typeparam>
    /// <param name="list">The list to update (typically an entity property).</param>
    /// <param name="index">The zero-based index of the element to set.</param>
    /// <param name="value">The value to set at the specified index.</param>
    /// <returns>Always throws an exception if called directly.</returns>
    /// <exception cref="InvalidOperationException">Always thrown - this method is only for use in expressions.</exception>
    /// <remarks>
    /// <para><strong>DynamoDB Translation:</strong></para>
    /// <para>Translates to: SET #attr[index] = :val</para>
    /// 
    /// <para><strong>Dynamic Index Support:</strong></para>
    /// <para>
    /// The index can be a constant, variable, property access, or method call,
    /// as long as it does not reference the entity parameter.
    /// </para>
    /// 
    /// <para><strong>Examples:</strong></para>
    /// <code>
    /// // Constant index
    /// .Set(x => x.Tags.SetAt(0, "updated"))
    /// 
    /// // Variable index
    /// int idx = GetIndex();
    /// .Set(x => x.Tags.SetAt(idx, "updated"))
    /// 
    /// // Method call index
    /// .Set(x => x.Tags.SetAt(GetTargetIndex(), "updated"))
    /// </code>
    /// </remarks>
    [ExpressionOnly]
    public static List<T> SetAt<T>(this List<T> list, int index, T value)
        => throw new InvalidOperationException(
            "This method is only for use in update expressions and should not be called directly.");

    /// <summary>
    /// Removes the element at a specific index from a list.
    /// Translates to: REMOVE #attr[index]
    /// </summary>
    /// <typeparam name="T">The type of elements in the list.</typeparam>
    /// <param name="list">The list to update (typically an entity property).</param>
    /// <param name="index">The zero-based index of the element to remove.</param>
    /// <returns>Always throws an exception if called directly.</returns>
    /// <exception cref="InvalidOperationException">Always thrown - this method is only for use in expressions.</exception>
    /// <remarks>
    /// <para><strong>DynamoDB Translation:</strong></para>
    /// <para>Translates to: REMOVE #attr[index]</para>
    /// 
    /// <para><strong>DynamoDB Behavior:</strong></para>
    /// <list type="bullet">
    /// <item><description>Removes the element at the specified index</description></item>
    /// <item><description>Elements after the removed index shift down</description></item>
    /// <item><description>If the index doesn't exist, the operation succeeds without error</description></item>
    /// </list>
    /// 
    /// <para><strong>Dynamic Index Support:</strong></para>
    /// <para>
    /// The index can be a constant, variable, property access, or method call,
    /// as long as it does not reference the entity parameter.
    /// </para>
    /// 
    /// <para><strong>Examples:</strong></para>
    /// <code>
    /// // Constant index
    /// .Set(x => x.Tags.RemoveAt(2))
    /// 
    /// // Variable index
    /// int idx = GetIndexToRemove();
    /// .Set(x => x.Tags.RemoveAt(idx))
    /// </code>
    /// </remarks>
    [ExpressionOnly]
    public static List<T> RemoveAt<T>(this List<T> list, int index)
        => throw new InvalidOperationException(
            "This method is only for use in update expressions and should not be called directly.");
}
```

### 2. UpdateExpressionTranslator Enhancements

The `UpdateExpressionTranslator` needs to recognize the new `SetAt` and `RemoveAt` method calls:

```csharp
// In UpdateExpressionTranslator - handle SetAt/RemoveAt method calls
private void ProcessMethodCallExpression(MethodCallExpression methodCall, ExpressionContext context)
{
    var methodName = methodCall.Method.Name;
    
    switch (methodName)
    {
        case "Append":
        case "Prepend":
        case "AppendRange":
        case "PrependRange":
            // Existing handling...
            break;
            
        case "SetAt":
            ProcessSetAtExpression(methodCall, context);
            break;
            
        case "RemoveAt":
            ProcessRemoveAtExpression(methodCall, context);
            break;
    }
}

private void ProcessSetAtExpression(MethodCallExpression methodCall, ExpressionContext context)
{
    // methodCall.Object is the list property (e.g., x.Tags)
    // methodCall.Arguments[0] is the index
    // methodCall.Arguments[1] is the value
    
    var listPath = BuildDocumentPathFromExpression(methodCall.Object, context);
    var index = EvaluateIndexExpression(methodCall.Arguments[0], context);
    var value = EvaluateValueExpression(methodCall.Arguments[1]);
    
    ValidateIndex(index, methodCall.Arguments[0]);
    
    var valuePlaceholder = context.AddValue(value);
    _setOperations.Add($"{listPath}[{index}] = {valuePlaceholder}");
}

private void ProcessRemoveAtExpression(MethodCallExpression methodCall, ExpressionContext context)
{
    // methodCall.Object is the list property (e.g., x.Tags)
    // methodCall.Arguments[0] is the index
    
    var listPath = BuildDocumentPathFromExpression(methodCall.Object, context);
    var index = EvaluateIndexExpression(methodCall.Arguments[0], context);
    
    ValidateIndex(index, methodCall.Arguments[0]);
    
    _removeOperations.Add($"{listPath}[{index}]");
}
```

### 3. Dynamic Index Evaluation

The key enhancement is evaluating non-constant index expressions:

```csharp
/// <summary>
/// Evaluates an index expression to get the integer value.
/// Supports constants, variables, property access, and method calls.
/// Throws if the expression references the entity parameter.
/// </summary>
private int EvaluateIndexExpression(Expression indexExpr, ExpressionContext context)
{
    // Fast path: constant expression
    if (indexExpr is ConstantExpression constant && constant.Value is int constIndex)
    {
        return constIndex;
    }
    
    // Check if expression references entity parameter
    if (ReferencesEntityParameter(indexExpr, context.EntityParameter))
    {
        throw new UnsupportedExpressionException(
            "List index cannot reference the entity parameter. " +
            "Use a local variable, property, or method call that doesn't depend on the entity.",
            indexExpr);
    }
    
    // Evaluate the expression
    try
    {
        var lambda = Expression.Lambda<Func<int>>(indexExpr);
        var compiled = lambda.Compile();
        return compiled();
    }
    catch (Exception ex)
    {
        throw new UnsupportedExpressionException(
            $"Failed to evaluate list index expression: {ex.Message}",
            indexExpr);
    }
}

/// <summary>
/// Checks if an expression references the entity parameter.
/// </summary>
private bool ReferencesEntityParameter(Expression expr, ParameterExpression entityParam)
{
    var visitor = new ParameterReferenceVisitor(entityParam);
    visitor.Visit(expr);
    return visitor.ReferencesParameter;
}

private class ParameterReferenceVisitor : ExpressionVisitor
{
    private readonly ParameterExpression _targetParameter;
    public bool ReferencesParameter { get; private set; }
    
    public ParameterReferenceVisitor(ParameterExpression targetParameter)
    {
        _targetParameter = targetParameter;
    }
    
    protected override Expression VisitParameter(ParameterExpression node)
    {
        if (node == _targetParameter)
        {
            ReferencesParameter = true;
        }
        return base.VisitParameter(node);
    }
}

/// <summary>
/// Validates that an index is non-negative.
/// </summary>
private void ValidateIndex(int index, Expression sourceExpr)
{
    if (index < 0)
    {
        throw new ArgumentOutOfRangeException(
            "index",
            index,
            $"List index must be non-negative. Got: {index}");
    }
}
```

### 4. ExpressionTranslator Enhancements for Filter/Condition Expressions

Similar changes for filter and condition expressions:

```csharp
// In ExpressionTranslator - handle dynamic indices in list access
private void HandleListIndexAccess(Expression listExpr, Expression indexExpr)
{
    var listPath = BuildDocumentPath(listExpr);
    
    // Try constant first
    if (indexExpr is ConstantExpression constant && constant.Value is int constIndex)
    {
        ValidateIndex(constIndex, indexExpr);
        _builder.Append($"{listPath}[{constIndex}]");
        return;
    }
    
    // Check for entity parameter reference
    if (ReferencesEntityParameter(indexExpr, _entityParameter))
    {
        throw new UnsupportedExpressionException(
            "List index cannot reference the entity parameter. " +
            "Use a local variable, property, or method call that doesn't depend on the entity.",
            indexExpr);
    }
    
    // Evaluate dynamic index
    var index = EvaluateConstantExpression<int>(indexExpr);
    ValidateIndex(index, indexExpr);
    _builder.Append($"{listPath}[{index}]");
}
```

## Data Models

### No New Data Models Required

The implementation uses existing infrastructure:
- `DocumentPathBuilder` for building document paths
- `AttributeNameInternal` for attribute name placeholders
- `AttributeValueInternal` for attribute value placeholders

## Error Handling

### Error Scenarios

| Scenario | Exception | Message |
|----------|-----------|---------|
| Index references entity parameter | `UnsupportedExpressionException` | "List index cannot reference the entity parameter..." |
| Negative index | `ArgumentOutOfRangeException` | "List index must be non-negative. Got: {index}" |
| Index evaluation fails | `UnsupportedExpressionException` | "Failed to evaluate list index expression: {details}" |
| Direct method call | `InvalidOperationException` | "This method is only for use in update expressions..." |

### Error Examples

```csharp
// ❌ Entity parameter reference - throws UnsupportedExpressionException
.WithFilter(x => x.Tags[x.PrimaryIndex] == "featured")

// ❌ Negative index - throws ArgumentOutOfRangeException
int index = -1;
.Set(x => x.Tags.SetAt(index, "value"))

// ❌ Direct call - throws InvalidOperationException
var list = new List<string>();
list.SetAt(0, "value");  // Throws
```

## Testing Strategy

### Unit Tests

1. **SetAt Extension Method Translation**
   - Constant index: `.Set(x => x.Tags.SetAt(0, "value"))` → `SET #tags[0] = :v0`
   - Variable index: `int i = 1; .Set(x => x.Tags.SetAt(i, "value"))` → `SET #tags[1] = :v0`
   - Method call index: `.Set(x => x.Tags.SetAt(GetIndex(), "value"))` → `SET #tags[N] = :v0`
   - Property access index: `.Set(x => x.Tags.SetAt(config.Index, "value"))` → `SET #tags[N] = :v0`
   - Nested list: `.Set(x => x.Metadata.Tags.SetAt(0, "value"))` → `SET #metadata.#tags[0] = :v0`

2. **RemoveAt Extension Method Translation**
   - Constant index: `.Set(x => x.Tags.RemoveAt(2))` → `REMOVE #tags[2]`
   - Variable index: `int i = 3; .Set(x => x.Tags.RemoveAt(i))` → `REMOVE #tags[3]`
   - Nested list: `.Set(x => x.Metadata.Tags.RemoveAt(0))` → `REMOVE #metadata.#tags[0]`

3. **Dynamic Index in Filter Expressions**
   - Variable: `int i = 0; .WithFilter(x => x.Tags[i] == "value")` → `#tags[0] = :v0`
   - Method call: `.WithFilter(x => x.Tags[GetIndex()] == "value")` → `#tags[N] = :v0`
   - Property: `.WithFilter(x => x.Tags[config.Index] == "value")` → `#tags[N] = :v0`

4. **Dynamic Index in Condition Expressions**
   - Variable: `int i = 0; .Where(x => x.Tags[i] == "expected")` → `#tags[0] = :v0`

5. **Error Cases**
   - Entity parameter reference throws `UnsupportedExpressionException`
   - Negative index throws `ArgumentOutOfRangeException`
   - Direct method call throws `InvalidOperationException`

6. **Backward Compatibility**
   - Old `.SetAt(x => x.Tags[0], "value")` builder method removed
   - Old `.RemoveAt(x => x.Tags[2])` builder method removed

### Integration Tests

1. **SetAt with DynamoDB**
   - Create item with list
   - Update element at index using new extension method
   - Verify element was updated

2. **RemoveAt with DynamoDB**
   - Create item with list
   - Remove element at index using new extension method
   - Verify element was removed and list shifted

3. **Dynamic Index Operations**
   - Query with variable index in filter
   - Update with variable index
   - Verify correct elements are accessed

## Performance Considerations

- Constant index path (existing): No change, O(1)
- Dynamic index evaluation: One-time compilation and invocation per expression
- Expression tree walking for parameter check: O(n) where n is expression depth
- Caching of compiled lambdas could be added if performance becomes an issue

## Backward Compatibility

- Old builder methods (`SetAt`, `RemoveAt`) are removed - they were just implemented and have no external users
- Existing code using constant indices in filter expressions continues to work
- New extension methods replace the builder methods with a consistent pattern
- This is a breaking change to the builder API, but acceptable since the methods are brand new

---

## Implementation Tasks

See `tasks.md` for the detailed implementation plan.
