# Design: Local Method Evaluation in Update Expressions

## Overview

This design document describes the implementation approach for supporting local method calls (like `.ToString()`, `.ToUpper()`, `.Trim()`) in update expressions when those method calls do not reference the entity parameter.

## Architecture

### Current Flow

```
ClassifyOperationWithPath()
    │
    ├── ConditionalExpression → HandleConditionalUpdateWithPath()
    ├── MemberInitExpression → TranslateNestedMemberInit()
    ├── MethodCallExpression → TranslateMethodCallWithPath()
    │       │
    │       └── Switch on method name:
    │           ├── "Add" → TranslateAddOperation()
    │           ├── "Remove" → TranslateRemoveOperation()
    │           ├── ... (other extension methods)
    │           └── _ → throw UnsupportedExpressionException ❌
    │
    ├── BinaryExpression → TranslateBinaryOperationWithPath()
    └── Default → TranslateSimpleSetWithPath()
```

### Proposed Flow

```
ClassifyOperationWithPath()
    │
    ├── ConditionalExpression → HandleConditionalUpdateWithPath()
    ├── MemberInitExpression → TranslateNestedMemberInit()
    ├── MethodCallExpression → TranslateMethodCallWithPath()
    │       │
    │       ├── Check: IsListOperationExtensionMethodOnList() → TranslateListOperationExtensionMethod()
    │       │
    │       ├── NEW: Check: !ReferencesEntityParameter() → TranslateSimpleSetWithPath() ✅
    │       │
    │       ├── Check: pathPrefix.Length > 0 → throw (nested not supported)
    │       │
    │       └── Switch on method name:
    │           ├── "Add" → TranslateAddOperation()
    │           ├── "Remove" → TranslateRemoveOperation()
    │           ├── ... (other extension methods)
    │           └── _ → throw UnsupportedExpressionException
    │
    ├── BinaryExpression → TranslateBinaryOperationWithPath()
    └── Default → TranslateSimpleSetWithPath()
```

## Implementation Details

### Change Location

**File**: `Oproto.FluentDynamoDb/Expressions/UpdateExpressionTranslator.cs`  
**Method**: `TranslateMethodCallWithPath()` (around line 990)

### Code Change

Add the following check after the list operation check and before the nested property check:

```csharp
private Operation TranslateMethodCallWithPath(
    MethodCallExpression methodCall,
    ParameterExpression parameter,
    string propertyName,
    ExpressionContext context,
    string[] pathPrefix)
{
    var methodName = methodCall.Method.Name;
    
    // Check if this is a list operation extension method from UpdateExpressionPropertyExtensions
    if (IsListOperationExtensionMethodOnList(methodCall))
    {
        return TranslateListOperationExtensionMethod(methodCall, parameter, context, pathPrefix, propertyName);
    }
    
    // NEW: Check if the method call references the entity parameter
    // If it doesn't, it's a local method call that can be evaluated at translation time
    // Examples: TransactionStatus.Active.ToString(), myVar.Trim().ToUpper(), guid.ToString()
    if (!ReferencesEntityParameter(methodCall, parameter))
    {
        // Evaluate the method call and treat it as a simple value assignment
        return TranslateSimpleSetWithPath(methodCall, parameter, propertyName, context, pathPrefix);
    }
    
    // For nested properties, only certain methods are supported
    if (pathPrefix.Length > 0)
    {
        throw new UnsupportedExpressionException(
            $"Method '{methodName}' is not supported for nested properties. " +
            $"Property path: '{string.Join(".", pathPrefix)}.{propertyName}'. " +
            $"Use simple value assignment for nested properties.",
            methodName,
            methodCall);
    }
    
    return methodName switch
    {
        // ... existing switch cases
    };
}
```

### Why This Works

1. **`ReferencesEntityParameter()`** - Already exists in the class (line 641) and uses an `ExpressionVisitor` to check if any part of the expression references the entity parameter.

2. **`TranslateSimpleSetWithPath()`** - Already exists (line 677) and handles:
   - Key property validation
   - Property metadata lookup
   - Expression evaluation via `EvaluateExpression()`
   - Format string application
   - Value capture with encryption support
   - SET expression generation

3. **`EvaluateExpression()`** - Already exists (line 2699) and can evaluate any expression that doesn't reference the entity parameter, including method calls.

### Order of Checks

The order of checks is important:

1. **List operations first** - These are extension methods on `UpdateExpressionProperty<List<T>>` that DO reference the parameter but need special handling
2. **Local method calls second** - Method calls that DON'T reference the parameter should be evaluated
3. **Nested property check third** - After we know it references the parameter, check if it's a nested path
4. **Known extension methods last** - Handle Add, Remove, Delete, etc.

## Testing Strategy

### Existing Tests

The test file `UpdateExpressionTranslatorEnumToStringTests.cs` already contains 5 tests that will pass after this fix:

| Test | Description |
|------|-------------|
| `TranslateUpdateExpression_EnumConstantToString_ShouldEvaluateAndCapture` | `TransactionStatus.Active.ToString()` |
| `TranslateUpdateExpression_EnumVariableToString_ShouldEvaluateAndCapture` | `status.ToString()` where status is captured |
| `TranslateUpdateExpression_IntToString_ShouldEvaluateAndCapture` | `id.ToString()` where id is int |
| `TranslateUpdateExpression_GuidToString_ShouldEvaluateAndCapture` | `guid.ToString()` |
| `TranslateUpdateExpression_ChainedMethodCalls_ShouldEvaluateAndCapture` | `name.Trim().ToUpper()` |

### Regression Tests

Existing tests in `UpdateExpressionTranslatorTests.cs` cover:
- Extension methods (Add, Remove, Delete, etc.)
- Arithmetic operations
- Conditional expressions
- Nested property updates

These should continue to pass as the new check only affects method calls that don't reference the entity parameter.

## Rollout Plan

1. Implement the code change in `TranslateMethodCallWithPath()`
2. Run the new tests to verify they pass
3. Run the full test suite to verify no regressions
4. Update documentation if needed (the steering file `fluentdynamodb.md` may need a note about this capability)

## Alternatives Considered

### Alternative 1: Handle in ClassifyOperationWithPath

Instead of modifying `TranslateMethodCallWithPath()`, we could add the check in `ClassifyOperationWithPath()` before calling `TranslateMethodCallWithPath()`.

**Rejected because**: This would require duplicating the `ReferencesEntityParameter` check logic and would make the flow less clear. The current approach keeps all method call handling in one place.

### Alternative 2: Add specific method name checks

We could add specific checks for common methods like `ToString`, `ToUpper`, `Trim`, etc.

**Rejected because**: This would require maintaining a list of allowed methods and wouldn't support custom methods or future .NET methods. The generic approach of checking entity parameter references is more robust and future-proof.

## Security Considerations

- **No new security risks**: The `EvaluateExpression()` method already exists and is used throughout the translator
- **Expression evaluation is sandboxed**: Only expressions that don't reference the entity parameter are evaluated
- **Sensitive field handling**: The existing `CaptureValue()` method handles sensitive field redaction in logs
