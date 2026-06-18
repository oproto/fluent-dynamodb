# nameof() Resolution in Attributes Bugfix Design

## Overview

The source generator's `EntityAnalyzer` fails to resolve `nameof()` expressions and other compile-time constants when used as positional arguments in `[Computed]` and `[Extracted]` attributes. The root cause is that both `ExtractComputedKeyAttributes()` and `ExtractExtractedKeyAttributes()` only check for `LiteralExpressionSyntax`, but `nameof()` is represented as `InvocationExpressionSyntax` in Roslyn's syntax tree. The fix uses `semanticModel.GetConstantValue()` to resolve any compile-time constant expression to its value, with a fallback to the existing `LiteralExpressionSyntax` check.

## Glossary

- **Bug_Condition (C)**: The condition that triggers the bug — when a positional argument in `[Computed]` or `[Extracted]` is an expression other than a string/int literal (e.g., `nameof()`, `const` variable)
- **Property (P)**: The desired behavior — compile-time constant expressions are resolved to their string/int values identically to literals
- **Preservation**: Existing string literal and integer literal argument handling must remain unchanged
- **`EntityAnalyzer`**: The class in `Oproto.FluentDynamoDb.SourceGenerator/Analysis/EntityAnalyzer.cs` that analyzes type declarations to extract DynamoDB entity metadata
- **`ExtractComputedKeyAttributes()`**: Method that parses `[Computed]` attribute arguments into a `ComputedKeyModel`
- **`ExtractExtractedKeyAttributes()`**: Method that parses `[Extracted]` attribute arguments into an `ExtractedKeyModel`
- **`SemanticModel.GetConstantValue()`**: Roslyn API that resolves compile-time constant expressions (including `nameof()`, `const` fields, and constant folding) to their runtime values

## Bug Details

### Bug Condition

The bug manifests when a user uses `nameof()` or a `const` variable as a positional argument in `[Computed]` or `[Extracted]` attributes. The `ExtractComputedKeyAttributes()` and `ExtractExtractedKeyAttributes()` methods only pattern-match against `LiteralExpressionSyntax`, causing them to silently skip any other expression type — even those that are compile-time constants.

**Formal Specification:**
```
FUNCTION isBugCondition(input)
  INPUT: input of type AttributeArgumentSyntax (positional argument in [Computed] or [Extracted])
  OUTPUT: boolean
  
  RETURN input.Expression is NOT LiteralExpressionSyntax
         AND input.Expression IS a compile-time constant
             (e.g., InvocationExpressionSyntax for nameof(),
              IdentifierNameSyntax for const variable,
              MemberAccessExpressionSyntax for const field)
END FUNCTION
```

### Examples

- `[Computed(nameof(UserId), Format = "USER#{0}")]` — `nameof(UserId)` is `InvocationExpressionSyntax`, skipped by the literal check, resulting in empty `SourceProperties` array and broken `string.Format()` output
- `[Extracted(nameof(Pk), 0)]` — `nameof(Pk)` is `InvocationExpressionSyntax`, skipped by the literal check, resulting in empty `SourceProperty` and an "Extracted property references non-existent source property ''" error
- `const string Source = "Pk"; [Extracted(Source, 0)]` — `Source` is `IdentifierNameSyntax`, skipped by the literal check
- `[Computed("UserId", Format = "USER#{0}")]` — `"UserId"` is `LiteralExpressionSyntax`, works correctly (not a buggy input)

## Expected Behavior

### Preservation Requirements

**Unchanged Behaviors:**
- String literals as positional arguments in `[Computed]` must continue to populate `SourceProperties` correctly
- String literals as the first positional argument in `[Extracted]` must continue to assign `SourceProperty` correctly
- Integer literals as the second positional argument in `[Extracted]` must continue to assign `Index` correctly
- Named arguments (`Format`, `Separator`) must continue to be extracted via `LiteralExpressionSyntax` pattern matching

**Scope:**
All inputs that are already `LiteralExpressionSyntax` should be completely unaffected by this fix. The semantic model resolution is only attempted for expressions that do NOT match `LiteralExpressionSyntax`, so existing behavior is preserved by short-circuit evaluation.

## Hypothesized Root Cause

Based on the bug description and code analysis, the root cause is clear:

1. **`LiteralExpressionSyntax`-Only Pattern Matching**: In `ExtractComputedKeyAttributes()`, the loop condition `arg.Expression is LiteralExpressionSyntax literal` only matches raw string literals like `"UserId"`. The expression `nameof(UserId)` is an `InvocationExpressionSyntax` node in the syntax tree and is silently skipped.

2. **Same Issue in `ExtractExtractedKeyAttributes()`**: The first argument check `args[0].Expression is LiteralExpressionSyntax sourcePropertyLiteral` fails for `nameof()` expressions, leaving `SourceProperty` as its default empty value.

3. **Missing Semantic Analysis**: The methods use only syntactic pattern matching (checking node types) without leveraging the `SemanticModel` that is already available as a parameter. Roslyn's `semanticModel.GetConstantValue(expression)` can resolve any compile-time constant — including `nameof()`, `const` variables, and constant expressions — to their actual values.

4. **Index Argument Also Affected**: The second argument in `[Extracted]` (the index) uses the same `LiteralExpressionSyntax` pattern, meaning a `const int` index would also fail to resolve.

## Correctness Properties

Property 1: Bug Condition - Compile-Time Constant Resolution

_For any_ positional argument in `[Computed]` or `[Extracted]` attributes where the expression is a compile-time constant but not a `LiteralExpressionSyntax` (e.g., `nameof()`, `const` variable), the fixed methods SHALL resolve the expression to its compile-time value using `semanticModel.GetConstantValue()` and include it in the model identically to how a string/int literal would be handled.

**Validates: Requirements 2.1, 2.2, 2.3, 2.4**

Property 2: Preservation - Literal Expression Behavior

_For any_ positional argument in `[Computed]` or `[Extracted]` attributes where the expression IS a `LiteralExpressionSyntax` (string literal or integer literal), the fixed methods SHALL produce exactly the same result as the original methods, preserving all existing string literal and integer literal handling.

**Validates: Requirements 3.1, 3.2, 3.3, 3.4**

## Fix Implementation

### Changes Required

Assuming our root cause analysis is correct:

**File**: `Oproto.FluentDynamoDb.SourceGenerator/Analysis/EntityAnalyzer.cs`

**Method 1**: `ExtractComputedKeyAttributes`

**Specific Changes**:
1. **Add semantic resolution for positional string arguments**: After checking `arg.Expression is LiteralExpressionSyntax`, add an `else` branch that calls `semanticModel.GetConstantValue(arg.Expression)` and checks if the result `HasValue` and `Value is string`.
2. **Preserve existing literal fast-path**: Keep the `LiteralExpressionSyntax` check as the primary path for efficiency and compatibility — semantic model resolution is the fallback.

**Method 2**: `ExtractExtractedKeyAttributes`

**Specific Changes**:
3. **Add semantic resolution for the first argument (source property string)**: After checking `args[0].Expression is LiteralExpressionSyntax`, add a fallback using `semanticModel.GetConstantValue(args[0].Expression)` to resolve `nameof()` and `const string` to their string values.
4. **Add semantic resolution for the second argument (index int)**: After checking `args[1].Expression is LiteralExpressionSyntax`, add a fallback using `semanticModel.GetConstantValue(args[1].Expression)` to resolve `const int` to its integer value.
5. **Handle type coercion for index**: `GetConstantValue().Value` for an integer may be `int`, `short`, `byte`, etc. — use `Convert.ToInt32()` or pattern matching on `is int`.

**Implementation Pattern** (applied to both methods):
```csharp
// Existing fast-path: direct literal
if (argExpression is LiteralExpressionSyntax literal)
{
    value = literal.Token.ValueText;
}
// Fallback: resolve compile-time constants (nameof, const, etc.)
else
{
    var constantValue = semanticModel.GetConstantValue(argExpression);
    if (constantValue.HasValue && constantValue.Value is string strValue)
    {
        value = strValue;
    }
}
```

## Testing Strategy

### Validation Approach

The testing strategy follows a two-phase approach: first, surface counterexamples that demonstrate the bug on unfixed code, then verify the fix works correctly and preserves existing behavior.

### Exploratory Bug Condition Checking

**Goal**: Surface counterexamples that demonstrate the bug BEFORE implementing the fix. Confirm or refute the root cause analysis. If we refute, we will need to re-hypothesize.

**Test Plan**: Write source generator unit tests that provide input source code using `nameof()` in `[Computed]` and `[Extracted]` attributes, run the generator, and assert on the generated output. Run these tests on the UNFIXED code to observe failures.

**Test Cases**:
1. **Computed with nameof()**: Entity with `[Computed(nameof(UserId), Format = "USER#{0}")]` — verify `SourceProperties` contains `"UserId"` (will fail on unfixed code)
2. **Extracted with nameof()**: Entity with `[Extracted(nameof(Pk), 0)]` — verify `SourceProperty` is `"Pk"` (will fail on unfixed code)
3. **Computed with multiple nameof()**: Entity with `[Computed(nameof(Year), nameof(Month), Separator = "#")]` — verify both source properties resolve (will fail on unfixed code)
4. **Extracted with const string**: Entity with `const string Source = "Pk"; [Extracted(Source, 0)]` — verify resolution (will fail on unfixed code)

**Expected Counterexamples**:
- `SourceProperties` array is empty when `nameof()` is used
- `SourceProperty` is empty string when `nameof()` is used
- Possible cause confirmed: `LiteralExpressionSyntax` pattern match silently skips non-literal expressions

### Fix Checking

**Goal**: Verify that for all inputs where the bug condition holds, the fixed function produces the expected behavior.

**Pseudocode:**
```
FOR ALL input WHERE isBugCondition(input) DO
  result := ExtractComputedKeyAttributes_fixed(input) OR ExtractExtractedKeyAttributes_fixed(input)
  ASSERT result.SourceProperties contains resolved nameof value
         OR result.SourceProperty == resolved nameof value
         OR result.Index == resolved const int value
END FOR
```

### Preservation Checking

**Goal**: Verify that for all inputs where the bug condition does NOT hold, the fixed function produces the same result as the original function.

**Pseudocode:**
```
FOR ALL input WHERE NOT isBugCondition(input) DO
  ASSERT ExtractComputedKeyAttributes_original(input) = ExtractComputedKeyAttributes_fixed(input)
  ASSERT ExtractExtractedKeyAttributes_original(input) = ExtractExtractedKeyAttributes_fixed(input)
END FOR
```

**Testing Approach**: Property-based testing is recommended for preservation checking because:
- It generates many test cases automatically across the input domain (various string literal values, various index integers)
- It catches edge cases that manual unit tests might miss (empty strings, special characters, large indices)
- It provides strong guarantees that behavior is unchanged for all non-buggy inputs

**Test Plan**: Observe behavior on UNFIXED code first for string literal arguments and integer literal arguments, then write property-based tests capturing that behavior.

**Test Cases**:
1. **String Literal Preservation in Computed**: Verify `[Computed("UserId", "TenantId", Format = "{0}#{1}")]` continues to produce `SourceProperties = ["UserId", "TenantId"]` after the fix
2. **String Literal Preservation in Extracted**: Verify `[Extracted("Pk", 0)]` continues to produce `SourceProperty = "Pk"` and `Index = 0` after the fix
3. **Named Argument Preservation**: Verify `Format = "USER#{0}"` and `Separator = "#"` continue to be extracted correctly
4. **Integer Literal Preservation in Extracted**: Verify various integer index values (0, 1, 2, 5) continue to be parsed correctly

### Unit Tests

- Test `nameof()` resolution in `[Computed]` with single source property
- Test `nameof()` resolution in `[Computed]` with multiple source properties
- Test `nameof()` resolution in `[Extracted]` first argument
- Test `const int` resolution in `[Extracted]` second argument
- Test mixed `nameof()` and string literals in same `[Computed]` attribute
- Test string literal arguments continue to work (regression)
- Test integer literal arguments continue to work (regression)
- Test edge cases: unresolvable expressions gracefully produce empty/default values

### Property-Based Tests

- Generate random valid property names as string literals and verify `ExtractComputedKeyAttributes` populates `SourceProperties` correctly (preservation)
- Generate random valid property names via `nameof()`-equivalent constant expressions and verify resolution (fix)
- Generate random integer indices and verify `ExtractExtractedKeyAttributes` populates `Index` correctly for both literals and constants

### Integration Tests

- Full source generator integration test: entity class using `nameof()` in `[Computed]` produces correct generated `string.Format()` call
- Full source generator integration test: entity class using `nameof()` in `[Extracted]` produces correct source property reference in generated code
- Verify no spurious diagnostics (FDDB warnings) are emitted when `nameof()` is used
