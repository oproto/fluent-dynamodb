# Local Method Evaluation in Update Expressions

## Problem Statement

The `UpdateExpressionTranslator` does not support local method calls like `.ToString()`, `.ToUpper()`, `.Trim()` in update expressions, even when those method calls do not reference the entity parameter. This is inconsistent with the `ExpressionTranslator` (used for filter/query expressions) which correctly evaluates such method calls.

### Current Behavior

When a user writes an update expression like:
```csharp
await table.Users.Update(userId)
    .Set(x => new UserUpdateModel { Status = TransactionStatus.Active.ToString() })
    .UpdateAsync();
```

The translator throws:
```
UnsupportedExpressionException: Method 'ToString' is not supported in update expressions. 
Supported methods: Add, Remove, Delete, IfNotExists, ListAppend, ListPrepend, Append, Prepend, AppendRange, PrependRange, SetDynamicField, RemoveDynamicField.
```

### Expected Behavior

The translator should recognize that `TransactionStatus.Active.ToString()` does not reference the entity parameter (`x`), evaluate it at translation time, and treat it as a simple value assignment resulting in `SET #status = :p0` where `:p0 = "Active"`.

## User Stories

### US-1: Enum ToString in Update Expressions
**As a** developer using FluentDynamoDb  
**I want to** use `.ToString()` on enum values in update expressions  
**So that** I can store enum values as strings without manual conversion

**Acceptance Criteria:**
- Given an update expression with `Status = TransactionStatus.Active.ToString()`
- When the expression is translated
- Then it should generate `SET #status = :p0` with `:p0 = "Active"`
- And no exception should be thrown

### US-2: Variable ToString in Update Expressions
**As a** developer using FluentDynamoDb  
**I want to** use `.ToString()` on captured variables in update expressions  
**So that** I can convert values to strings inline

**Acceptance Criteria:**
- Given a captured variable `var status = TransactionStatus.Completed;`
- And an update expression with `Status = status.ToString()`
- When the expression is translated
- Then it should generate `SET #status = :p0` with `:p0 = "Completed"`

### US-3: Numeric ToString in Update Expressions
**As a** developer using FluentDynamoDb  
**I want to** use `.ToString()` on numeric values in update expressions  
**So that** I can convert numbers to strings inline

**Acceptance Criteria:**
- Given a captured variable `var id = 12345;`
- And an update expression with `Name = id.ToString()`
- When the expression is translated
- Then it should generate `SET #name = :p0` with `:p0 = "12345"`

### US-4: Guid ToString in Update Expressions
**As a** developer using FluentDynamoDb  
**I want to** use `.ToString()` on Guid values in update expressions  
**So that** I can convert GUIDs to strings inline

**Acceptance Criteria:**
- Given a captured Guid variable
- And an update expression with `Name = guid.ToString()`
- When the expression is translated
- Then it should generate `SET #name = :p0` with the GUID string value

### US-5: Chained Method Calls in Update Expressions
**As a** developer using FluentDynamoDb  
**I want to** use chained method calls like `.Trim().ToUpper()` in update expressions  
**So that** I can transform values inline

**Acceptance Criteria:**
- Given a captured variable `var name = "  John Doe  ";`
- And an update expression with `Name = name.Trim().ToUpper()`
- When the expression is translated
- Then it should generate `SET #name = :p0` with `:p0 = "JOHN DOE"`

## Technical Analysis

### Root Cause

In `UpdateExpressionTranslator.ClassifyOperationWithPath()` (line ~430), when a `MethodCallExpression` is encountered, it delegates to `TranslateMethodCallWithPath()` which only handles specific extension methods (Add, Remove, Delete, etc.) and throws for all others.

Unlike `ExpressionTranslator.VisitMethodCall()` (lines 1132-1157) which:
1. First checks if the method is a DynamoDB function
2. Then checks if the method references the entity parameter
3. If it doesn't reference the entity parameter, evaluates it and captures the result

The `UpdateExpressionTranslator` lacks step 2 and 3.

### Proposed Solution

Modify `TranslateMethodCallWithPath()` to:
1. Before the switch statement that handles known extension methods
2. Check if the method call references the entity parameter using `ReferencesEntityParameter()`
3. If it does NOT reference the entity parameter, evaluate it using `EvaluateExpression()` and return a simple SET operation via `TranslateSimpleSetWithPath()`

### Code Location

- **File**: `Oproto.FluentDynamoDb/Expressions/UpdateExpressionTranslator.cs`
- **Method**: `TranslateMethodCallWithPath()` (around line 990)
- **Reference Implementation**: `ExpressionTranslator.VisitMethodCall()` (lines 1132-1157)

### Existing Tests

Test file created at: `Oproto.FluentDynamoDb.UnitTests/Expressions/UpdateExpressionTranslatorEnumToStringTests.cs`

All 5 tests currently fail, confirming the bug:
1. `TranslateUpdateExpression_EnumConstantToString_ShouldEvaluateAndCapture`
2. `TranslateUpdateExpression_EnumVariableToString_ShouldEvaluateAndCapture`
3. `TranslateUpdateExpression_IntToString_ShouldEvaluateAndCapture`
4. `TranslateUpdateExpression_GuidToString_ShouldEvaluateAndCapture`
5. `TranslateUpdateExpression_ChainedMethodCalls_ShouldEvaluateAndCapture`

## Out of Scope

- Method calls that DO reference the entity parameter (e.g., `x.Name.ToUpper()`) - these remain unsupported as DynamoDB cannot execute C# methods
- Changes to the filter/query expression translator (already working correctly)
- New DynamoDB function mappings

## Dependencies

- None - this is a self-contained bug fix in the expression translator

## Risks

- **Low Risk**: The fix follows the same pattern already proven in `ExpressionTranslator`
- **Regression Risk**: Minimal - existing tests cover the supported extension methods, and new tests cover the new functionality
