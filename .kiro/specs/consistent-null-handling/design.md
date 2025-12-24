# Design Document

## Overview

This design addresses the inconsistent handling of `null` values in update expressions by:
1. Making `null` consistently mean "set to DynamoDB NULL" in all contexts
2. Adding a `.NoUpdate()` extension method for explicit skip behavior

The change is a breaking change that affects code using the `flag ? value : null` pattern to conditionally skip updates.

## Architecture

The implementation modifies two components:
1. **UpdateExpressionPropertyExtensions** - Add the `NoUpdate<T>()` extension method
2. **UpdateExpressionTranslator** - Remove special null handling in conditionals, add NoUpdate detection

```
┌─────────────────────────────────────────────────────────────────┐
│                    Update Expression Flow                        │
├─────────────────────────────────────────────────────────────────┤
│                                                                  │
│  Lambda Expression                                               │
│       │                                                          │
│       ▼                                                          │
│  ┌─────────────────────────────────────────────────────────┐    │
│  │           UpdateExpressionTranslator                     │    │
│  │  ┌─────────────────────────────────────────────────┐    │    │
│  │  │ ClassifyOperationWithPath()                      │    │    │
│  │  │   - null value → TranslateSimpleSet (SET NULL)   │    │    │
│  │  │   - NoUpdate() → Skip operation                  │    │    │
│  │  │   - Remove()   → REMOVE operation                │    │    │
│  │  └─────────────────────────────────────────────────┘    │    │
│  └─────────────────────────────────────────────────────────┘    │
│       │                                                          │
│       ▼                                                          │
│  DynamoDB Update Expression String                               │
│                                                                  │
└─────────────────────────────────────────────────────────────────┘
```

## Components and Interfaces


### UpdateExpressionPropertyExtensions

Add the `NoUpdate<T>()` extension method:

```csharp
/// <summary>
/// Signals that this property should not be updated.
/// </summary>
/// <typeparam name="T">The property type.</typeparam>
/// <param name="property">The property to skip.</param>
/// <returns>Never returns - this method throws if called directly.</returns>
/// <exception cref="InvalidOperationException">Always thrown - this method is only for use in expressions.</exception>
/// <remarks>
/// <para>
/// Use this method in conditional expressions to skip updating a property based on runtime conditions.
/// </para>
/// <para><strong>Example:</strong></para>
/// <code>
/// // Only update Name if shouldUpdate is true, otherwise skip
/// .Set(x => new UserUpdateModel 
/// { 
///     Name = shouldUpdate ? newName : x.Name.NoUpdate() 
/// })
/// </code>
/// </remarks>
[ExpressionOnly]
public static T NoUpdate<T>(this UpdateExpressionProperty<T> property)
    => throw new InvalidOperationException(
        "This method is only for use in update expressions and should not be called directly. " +
        "Use it within a Set() lambda to skip updating a property conditionally.");
```

### UpdateExpressionTranslator Changes

#### Remove Special Null Handling in Conditionals

Current code in `HandleConditionalUpdateWithPath`:
```csharp
// REMOVE THIS BLOCK:
if (IsNullExpression(conditional.IfFalse))
{
    return new Operation
    {
        Type = OperationType.Skip,
        Expression = string.Empty
    };
}
```

#### Add NoUpdate Detection

In `ClassifyOperationWithPath`, add detection for `NoUpdate()` method calls:

```csharp
// Check for method calls (Add, Remove, Delete, IfNotExists, NoUpdate, etc.)
if (unwrapped is MethodCallExpression methodCall)
{
    // Check for NoUpdate() first
    if (IsNoUpdateMethodCall(methodCall))
    {
        return new Operation
        {
            Type = OperationType.Skip,
            Expression = string.Empty
        };
    }
    
    return TranslateMethodCallWithPath(methodCall, parameter, propertyName, context, pathPrefix);
}
```

Add helper method:
```csharp
private bool IsNoUpdateMethodCall(MethodCallExpression methodCall)
{
    return methodCall.Method.Name == "NoUpdate" &&
           methodCall.Method.DeclaringType == typeof(UpdateExpressionPropertyExtensions);
}
```

## Data Models

No new data models required. The existing `Operation` class with `OperationType.Skip` is reused.

## Correctness Properties

*A property is a characteristic or behavior that should hold true across all valid executions of a system—essentially, a formal statement about what the system should do. Properties serve as the bridge between human-readable specifications and machine-verifiable correctness guarantees.*


### Property 1: Null Consistency

*For any* update expression containing a `null` assignment (direct, in conditional true branch, or in conditional false branch), the translator SHALL generate a SET operation with `AttributeValue.NULL = true`.

**Validates: Requirements 1.1, 1.2, 1.3, 1.4, 3.1**

### Property 2: NoUpdate Skips Property

*For any* update expression where a property is assigned `x.Property.NoUpdate()`, the resulting DynamoDB expression SHALL NOT contain any operation (SET, ADD, REMOVE, DELETE) for that property.

**Validates: Requirements 2.2**

### Property 3: NoUpdate in Conditionals

*For any* conditional expression `flag ? branchA : branchB` where one branch contains `NoUpdate()`, when the condition evaluates to select the NoUpdate branch, the property SHALL be skipped entirely.

**Validates: Requirements 2.3, 2.4**

### Property 4: NoUpdate Works for All Types

*For any* property type T, the `NoUpdate<T>()` extension method SHALL be available and SHALL cause the property to be skipped when used in an update expression.

**Validates: Requirements 2.5**

### Property 5: NoUpdate Throws When Called Directly

*For any* direct invocation of `NoUpdate()` outside an expression context, the method SHALL throw `InvalidOperationException` with a message indicating it is only for use in update expressions.

**Validates: Requirements 2.6, 4.1, 4.2**

## Error Handling

| Scenario | Error Type | Message |
|----------|------------|---------|
| `NoUpdate()` called directly | `InvalidOperationException` | "This method is only for use in update expressions and should not be called directly. Use it within a Set() lambda to skip updating a property conditionally." |

## Testing Strategy

### Unit Tests

1. **NoUpdate Extension Method**
   - Verify method exists on `UpdateExpressionPropertyExtensions`
   - Verify direct call throws `InvalidOperationException`
   - Verify error message content

2. **Translator Behavior**
   - Test null assignment generates SET NULL
   - Test NoUpdate() generates no operation
   - Test conditional with null in both branches
   - Test conditional with NoUpdate() in both branches
   - Test mixed expressions (some null, some NoUpdate, some values)

### Property-Based Tests

Use FsCheck to generate:
- Random property names and types
- Random boolean conditions for conditionals
- Random combinations of null, NoUpdate(), and actual values

Verify:
- Null always produces SET NULL
- NoUpdate always produces Skip
- Expression output is deterministic for same input

### Integration Tests

1. **DynamoDB Integration**
   - Verify SET NULL actually sets attribute to null in DynamoDB
   - Verify NoUpdate() leaves attribute unchanged in DynamoDB
   - Verify Remove() removes attribute entirely (distinct from NULL)

### Migration Testing

1. **Breaking Change Verification**
   - Document that `flag ? value : null` now sets NULL instead of skipping
   - Provide migration examples in release notes
