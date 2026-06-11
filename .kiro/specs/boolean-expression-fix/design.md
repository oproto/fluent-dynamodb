# Boolean Expression Fix - Bugfix Design

## Overview

The `ExpressionTranslator` in Oproto.FluentDynamoDb produces invalid DynamoDB expression syntax when translating bare boolean property access in filter and condition expressions. Negated booleans (`!x.IsDeleted`) produce `NOT (#attr0)` and affirmative booleans (`x.IsActive`) produce just `#attr0` — neither is a valid DynamoDB condition. The fix converts these to equality comparisons against boolean literal values: `#attr0 = :p0` where `:p0` is `false` (for negation) or `true` (for affirmative).

## Glossary

- **Bug_Condition (C)**: A bare boolean member expression (type `bool`) used as a standalone condition or inside a NOT unary — the translator produces an attribute placeholder without a comparison operator
- **Property (P)**: The desired behavior — bare boolean access translates to `#attrN = :pN` with the appropriate `BOOL` value
- **Preservation**: All non-boolean-member expressions (comparisons, method calls, existing NOT-wrapped comparisons) must continue to produce the same output
- **ExpressionTranslator**: The class in `Oproto.FluentDynamoDb/Expressions/ExpressionTranslator.cs` that converts C# lambda expression trees to DynamoDB expression strings
- **ExpressionContext**: Holds attribute name/value mappings accumulated during translation
- **VisitUnary**: Method handling `!` (NOT) expressions — currently wraps operand with `NOT (...)` unconditionally
- **VisitMember**: Method handling property access — returns an attribute name placeholder like `#attr0`
- **VisitBinary**: Method handling `&&` / `||` / comparisons — dispatches to `Visit` for each operand

## Bug Details

### Bug Condition

The bug manifests when a boolean-typed property of the entity is used as a bare condition (without an explicit comparison operator like `== true` or `== false`). The `VisitMember` method returns just an attribute placeholder (`#attr0` or `#attr0.#attr1` for nested), which is not a valid DynamoDB condition expression. The `VisitUnary` method wraps this with `NOT (...)`, but `NOT (#attr0)` is also invalid because the operand of NOT must itself be a condition.

**Formal Specification:**
```
FUNCTION isBugCondition(input)
  INPUT: input of type Expression (C# expression tree node)
  OUTPUT: boolean
  
  RETURN (input IS UnaryExpression with NodeType == Not
          AND input.Operand IS MemberExpression
          AND input.Operand.Type == typeof(bool)
          AND IsEntityPropertyAccess(input.Operand))
         OR
         (input IS MemberExpression
          AND input.Type == typeof(bool)
          AND IsEntityPropertyAccess(input)
          AND input IS used as a standalone condition operand
              (i.e., as an operand of AND/OR or as the top-level body))
END FUNCTION
```

### Examples

- `x => !x.IsDeleted` → Currently produces `NOT (#attr0)` — invalid. Expected: `#attr0 = :p0` where `:p0` = `{BOOL: false}`
- `x => x.IsActive` → Currently produces `#attr0` — invalid. Expected: `#attr0 = :p0` where `:p0` = `{BOOL: true}`
- `x => !x.Settings.IsEnabled` → Currently produces `NOT (#attr0.#attr1)` — invalid. Expected: `#attr0.#attr1 = :p0` where `:p0` = `{BOOL: false}`
- `x => x.Settings.IsEnabled` → Currently produces `#attr0.#attr1` — invalid. Expected: `#attr0.#attr1 = :p0` where `:p0` = `{BOOL: true}`
- `x => x.IsActive && x.Age > 18` → `x.IsActive` part currently produces just `#attr0`. Expected: `(#attr0 = :p0) AND (#attr1 > :p1)` where `:p0` = `{BOOL: true}`

## Expected Behavior

### Preservation Requirements

**Unchanged Behaviors:**
- Negated comparison expressions like `!(x.Age > 18)` must continue to produce `NOT (#attr0 > :p0)` — the NOT wrapping is correct when the operand is already a valid condition
- Negated equality expressions like `!(x.Status == "active")` must continue to produce `NOT (#attr0 = :p0)`
- Explicit boolean comparisons like `x.IsActive == true` or `x.IsDeleted == false` must continue to produce `#attr0 = :p0` with correct boolean values
- Non-boolean method call negation like `!(x.Name.Contains("test"))` must continue to produce `NOT (contains(#attr0, :p0))`
- Compound expressions with explicit comparisons like `x.IsActive == true && x.Age > 18` must remain unchanged
- All other expression types (Between, AttributeExists, Size, StartsWith, Contains) remain unchanged

**Scope:**
All inputs where the expression operand is NOT a bare boolean member expression should be completely unaffected by this fix. This includes:
- Comparison expressions (==, !=, <, >, <=, >=)
- Method call expressions (Contains, StartsWith, Between, etc.)
- DynamoDB function expressions (attribute_exists, attribute_not_exists, size)
- Logical combinations of the above

## Hypothesized Root Cause

Based on the bug description and code analysis, the root causes are:

1. **VisitUnary does not check operand type**: The `VisitUnary` method (line ~864) unconditionally wraps the operand with `NOT (...)`. When the operand is a bare boolean member access, the result `NOT (#attr0)` is invalid because `#attr0` alone is not a condition. The fix is to detect when the operand is a `MemberExpression` with `Type == typeof(bool)` that is entity property access, and instead translate `!x.IsDeleted` as `#attr0 = :p0` with `:p0 = false`.

2. **No handler for bare boolean MemberExpression as condition**: When `x.IsActive` appears as an operand of `&&` or `||` (in `VisitBinary`), or as the top-level expression body, the `Visit` method dispatches to `VisitMember` which returns just `#attr0`. There is no logic to detect that a boolean member expression used in a condition context needs to be expanded to `#attr0 = :p0` with `:p0 = true`. The detection needs to happen either:
   - In `VisitBinary` before visiting operands (check if an operand is a bare boolean member), or
   - In `Visit` itself when the caller is in a "condition context", or
   - By adding a wrapper method that checks the result of `Visit` and the expression type

3. **The `Visit` dispatcher lacks context awareness**: The `Visit` method doesn't know whether the caller expects a condition (full expression) or a value (attribute reference). A boolean `MemberExpression` is valid as a value in `x.IsActive == true` but invalid as a standalone condition in `x => x.IsActive`.

## Correctness Properties

Property 1: Bug Condition - Bare Boolean Negation Produces Valid Equality

_For any_ expression where a negated bare boolean property is used (`!x.BoolProp` where BoolProp is a boolean-typed entity property), the fixed ExpressionTranslator SHALL produce `#attrN = :pM` where `:pM` has a BOOL value of `false`, instead of the invalid `NOT (#attrN)`.

**Validates: Requirements 2.1, 2.3**

Property 2: Bug Condition - Bare Boolean Affirmative Produces Valid Equality

_For any_ expression where an affirmative bare boolean property is used as a standalone condition (`x.BoolProp` within AND/OR or as the body), the fixed ExpressionTranslator SHALL produce `#attrN = :pM` where `:pM` has a BOOL value of `true`, instead of the invalid bare `#attrN`.

**Validates: Requirements 2.2, 2.4**

Property 3: Preservation - Non-Boolean NOT Expressions Unchanged

_For any_ NOT expression where the operand is not a bare boolean member expression (e.g., `!(x.Age > 18)`, `!(x.Name.Contains("test"))`), the fixed ExpressionTranslator SHALL produce the same `NOT (...)` wrapping as the original code, preserving all existing NOT behavior for comparison and function-call operands.

**Validates: Requirements 3.1, 3.2, 3.4**

Property 4: Preservation - Explicit Boolean Comparisons Unchanged

_For any_ expression using explicit boolean comparisons (`x.IsActive == true`, `x.IsDeleted == false`), the fixed ExpressionTranslator SHALL produce the same result as the original code, preserving the existing equality comparison translation.

**Validates: Requirements 3.3, 3.5**

## Fix Implementation

### Changes Required

Assuming our root cause analysis is correct:

**File**: `Oproto.FluentDynamoDb/Expressions/ExpressionTranslator.cs`

**Function**: `VisitUnary`

**Specific Changes**:
1. **Detect boolean member operand in NOT**: Before wrapping with `NOT (...)`, check if `node.Operand` is a `MemberExpression` (or resolves to one through Convert) with `Type == typeof(bool)` and is entity property access. If so, visit the operand to get the attribute path, then produce `{attrPath} = :pN` with `:pN` set to `{BOOL: false}`.

**Function**: `VisitBinary` (or a helper called from it)

**Specific Changes**:
2. **Detect bare boolean member in AND/OR operands**: When visiting operands of `&&` or `||`, if an operand is a `MemberExpression` with boolean type and references the entity parameter, translate it as `{attrPath} = :pN` with `:pN` set to `{BOOL: true}` instead of returning just the attribute placeholder.

**Function**: `Visit` (or new helper)

**Specific Changes**:
3. **Handle top-level bare boolean**: If the lambda body itself is a bare boolean `MemberExpression` (e.g., `x => x.IsActive`), the `Translate` method's call to `Visit` will return just `#attr0`. Add detection at this level to wrap it as `#attr0 = :p0` with `true`.

4. **Helper method `TranslateBooleanMemberAsCondition`**: Create a private helper that takes a `MemberExpression`, visits it to get the attribute path, then appends ` = :pN` and registers the boolean attribute value in `context.AttributeValues`.

5. **Nested property support**: Ensure the detection works for nested boolean properties (`x.Settings.IsEnabled`) which produce document paths like `#attr0.#attr1` — the same logic applies, just the attribute path is longer.

## Testing Strategy

### Validation Approach

The testing strategy follows a two-phase approach: first, surface counterexamples that demonstrate the bug on unfixed code, then verify the fix works correctly and preserves existing behavior.

### Exploratory Bug Condition Checking

**Goal**: Surface counterexamples that demonstrate the bug BEFORE implementing the fix. Confirm or refute the root cause analysis. If we refute, we will need to re-hypothesize.

**Test Plan**: Write unit tests that call `ExpressionTranslator.Translate` with bare boolean expressions and assert the output. Run these tests on the UNFIXED code to observe failures and confirm the invalid syntax being produced.

**Test Cases**:
1. **Negated Boolean Test**: Translate `x => !x.IsDeleted` — will produce `NOT (#attr0)` on unfixed code
2. **Affirmative Boolean Test**: Translate `x => x.IsActive` — will produce just `#attr0` on unfixed code
3. **Negated Nested Boolean Test**: Translate `x => !x.Settings.IsEnabled` — will produce `NOT (#attr0.#attr1)` on unfixed code
4. **Affirmative in AND Test**: Translate `x => x.IsActive && x.Age > 18` — `x.IsActive` part produces just `#attr0` on unfixed code

**Expected Counterexamples**:
- `NOT (#attr0)` is produced instead of `#attr0 = :p0` (false)
- `#attr0` is produced as a standalone condition instead of `#attr0 = :p0` (true)
- Possible causes confirmed: VisitUnary blindly wraps, VisitMember returns bare placeholder for boolean member

### Fix Checking

**Goal**: Verify that for all inputs where the bug condition holds, the fixed function produces the expected behavior.

**Pseudocode:**
```
FOR ALL input WHERE isBugCondition(input) DO
  result := ExpressionTranslator_fixed.Translate(input)
  ASSERT result contains "= :pN" (equality comparison)
  ASSERT context.AttributeValues[":pN"].BOOL == expectedBoolValue(input)
  ASSERT result does NOT contain "NOT (#attrN)" for bare boolean operands
END FOR
```

### Preservation Checking

**Goal**: Verify that for all inputs where the bug condition does NOT hold, the fixed function produces the same result as the original function.

**Pseudocode:**
```
FOR ALL input WHERE NOT isBugCondition(input) DO
  ASSERT ExpressionTranslator_original.Translate(input) == ExpressionTranslator_fixed.Translate(input)
END FOR
```

**Testing Approach**: Property-based testing is recommended for preservation checking because:
- It generates many expression combinations automatically across the input domain
- It catches edge cases where the boolean detection might incorrectly trigger on non-boolean members
- It provides strong guarantees that existing `NOT (...)` wrapping behavior is unchanged for comparison operands

**Test Plan**: Observe behavior on UNFIXED code first for non-boolean NOT expressions and explicit boolean comparisons, then write property-based tests capturing that behavior.

**Test Cases**:
1. **NOT Comparison Preservation**: Verify `!(x.Age > 18)` continues producing `NOT (#attr0 > :p0)` after fix
2. **NOT Equality Preservation**: Verify `!(x.Status == "active")` continues producing `NOT (#attr0 = :p0)` after fix
3. **Explicit Boolean Preservation**: Verify `x.IsActive == true` continues producing `#attr0 = :p0` with BOOL true
4. **NOT Method Call Preservation**: Verify `!(x.Name.Contains("test"))` continues producing `NOT (contains(#attr0, :p0))`
5. **Compound Expression Preservation**: Verify `x.IsActive == true && x.Age > 18` is unchanged

### Unit Tests

- Test `!x.IsDeleted` produces `#attr0 = :p0` with BOOL false
- Test `x.IsActive` produces `#attr0 = :p0` with BOOL true
- Test `!x.Settings.IsEnabled` produces `#attr0.#attr1 = :p0` with BOOL false
- Test `x.Settings.IsEnabled` produces `#attr0.#attr1 = :p0` with BOOL true
- Test `x.IsActive && x.Age > 18` produces `(#attr0 = :p0) AND (#attr1 > :p1)` with correct values
- Test `!x.IsDeleted && x.IsActive` produces `(#attr0 = :p0) AND (#attr1 = :p1)` with false and true
- Test edge case: `x.IsActive || x.IsDeleted` produces two equality conditions joined by OR

### Property-Based Tests

- Generate random combinations of boolean property names and verify negation always produces `= :pN` with BOOL false
- Generate random combinations of boolean properties and verify affirmative access always produces `= :pN` with BOOL true
- Generate random non-boolean NOT expressions and verify they still produce `NOT (...)` wrapping
- Generate compound expressions mixing bare booleans with comparisons and verify each part translates correctly

### Integration Tests

- Test full query flow: `table.Users.Query(x => x.Pk == pk).WithFilter(x => !x.IsDeleted).ToListAsync()` produces valid DynamoDB request
- Test compound filter: `table.Users.Query(x => x.Pk == pk).WithFilter(x => x.IsActive && x.Age > 18).ToListAsync()` produces valid request
- Test condition expression on Put: `table.Users.Put(user).Where(x => x.IsActive).PutAsync()` produces valid condition
