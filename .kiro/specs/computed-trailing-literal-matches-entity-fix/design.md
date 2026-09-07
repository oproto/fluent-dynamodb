# Computed Trailing-Literal MatchesEntity Bugfix Design

## Overview

When a `[Computed]` format string ends with a trailing literal after the last `{N}` placeholder (e.g., `"EMPLOYEE#{0}#"`), the auto-derived `MatchesEntity` discrimination check incorrectly rejects items whose sort key value ends with that trailing literal. The three `Complex`-strategy code generation methods in `MapperGenerator.cs` all emit an `IndexOf(...) < Length - 1` constraint on the trailing bare-separator segment, which requires at least one character to follow the separator. This is wrong when the separator IS the terminal character of the pattern. The fix detects trailing bare-separator segments and omits the `< Length - 1` constraint for them, while preserving the existing constraint for non-trailing segments.

## Glossary

- **Bug_Condition (C)**: The condition that triggers the bug — a Complex discriminator pattern ends with a bare-separator literal after the last wildcard (i.e., the pattern does not end with `*` and the last non-empty segment is a single separator character already present in the prefix segment)
- **Property (P)**: The desired behavior — the generated trailing-position check only requires `IndexOf(separator, offset) >= 0` (return/exclusion mode) or `IndexOf(separator, offset) < 0` (negated mode), without the `< Length - 1` / `>= Length - 1` constraint
- **Preservation**: Existing behavior for non-trailing bare-separator segments and patterns that end with a wildcard must remain unchanged — they must continue to enforce `< Length - 1` / `>= Length - 1`
- **GenerateComplexPatternCheck**: The method in `MapperGenerator.cs` (~line 4724) that generates `MatchesEntity` checks for Complex-strategy patterns in both "return" and "negated" modes
- **GenerateComplexExclusionCheck**: The method in `MapperGenerator.cs` (~line 4817) that generates exclusion guards for Complex-strategy patterns used to reject more-specific overlapping patterns
- **Bare-separator segment**: A non-empty segment produced by `pattern.Split('*')` that is a substring already contained within the prefix segment (e.g., `"#"` when the prefix is `"EMPLOYEE#"`). These use positional `IndexOf` checks rather than `Contains`
- **Trailing bare-separator**: A bare-separator segment that is both the last non-empty segment in the pattern AND the pattern does not end with `*`
- **DeterminePatternStrategy**: Method in `DiscriminatorAnalyzer.cs` that classifies patterns — `"EMPLOYEE#*#"` has 1 wildcard, not at start, not at end → classified as `Complex`

## Bug Details

### Bug Condition

The bug manifests when a Complex discriminator pattern ends with a literal after the last wildcard (e.g., `"EMPLOYEE#*#"`). The `GenerateComplexPatternCheck` and `GenerateComplexExclusionCheck` methods unconditionally emit a `< Length - 1` constraint on bare-separator positional checks, including for the terminal segment where it is incorrect.

**Formal Specification:**
```
FUNCTION isBugCondition(input)
  INPUT: input of type { pattern: string, segmentIndex: int, mode: string }
  OUTPUT: boolean

  segments := pattern.Split('*').Where(s => s.Length > 0)
  prefixSegment := segments[0]
  currentSegment := segments[segmentIndex]

  RETURN segmentIndex > 0
         AND prefixSegment.Contains(currentSegment)           -- bare-separator
         AND segmentIndex == segments.Count - 1               -- last segment
         AND NOT pattern.EndsWith("*")                        -- pattern ends with literal
         AND mode IN ["return", "negated", "exclusion"]
END FUNCTION
```

### Examples

- **Pattern `"EMPLOYEE#*#"`, value `"EMPLOYEE#abc123#"` (length 16)**: `IndexOf("#", 10)` returns 15. Check `15 < 16 - 1` → `15 < 15` → `false` — **item rejected** (should be accepted)
- **Pattern `"EMPLOYEE#*#"`, value `"EMPLOYEE#x#"` (length 12)**: `IndexOf("#", 10)` returns 11. Check `11 < 12 - 1` → `11 < 11` → `false` — **item rejected** (should be accepted)
- **Pattern `"TYPE#*#VERSION#*#"`, value `"TYPE#a#VERSION#b#"` (length 18)**: The trailing `#` after the last wildcard triggers the same bug for the final bare-separator segment
- **Pattern `"EMPLOYEE#*#"`, value `"EMPLOYEE#abc#def#"` (length 18)**: `IndexOf("#", 10)` returns 13. Check `13 < 18 - 1` → `13 < 17` → `true` — **item accepted** (correctly, but for the wrong reason — it found an intermediate `#`, masking the bug for data that happens to contain extra separators)

## Expected Behavior

### Preservation Requirements

**Unchanged Behaviors:**
- Patterns ending with a wildcard (e.g., `"INVOICE#*#LINE#*"`) must continue to enforce `< Length - 1` for all bare-separator segments, ensuring the trailing wildcard matches at least 1 character
- Non-trailing bare-separator segments in any pattern (e.g., the first `#` in `"PREFIX#*#*#"`) must continue to enforce `< Length - 1`
- Patterns using `StartsWith`, `EndsWith`, `Contains`, or `ExactMatch` strategies are unaffected (they don't go through the Complex generation path)
- Patterns starting with `*` that use `Contains`-based checks for all segments remain unchanged
- Meaningful (non-bare-separator) segments continue to use `Contains` checks
- Mouse/non-keyboard inputs, button display, and all non-discriminator code paths are completely unaffected

**Scope:**
All code generation paths that do NOT involve a trailing bare-separator segment should be completely unaffected by this fix. This includes:
- All `StartsWith`-strategy patterns (e.g., `"ORDER#*"`)
- All internal (non-trailing) bare-separator segments
- All `Contains`-based segment checks (both for patterns starting with `*` and for meaningful segments)
- All `ExactMatch`, `EndsWith`, and `Contains` strategy patterns

## Hypothesized Root Cause

Based on the bug description and source code analysis, the root cause is a missing conditional in all three Complex-strategy code generation methods:

1. **Unconditional `< Length - 1` constraint**: The loop over non-empty segments after the prefix applies the same `IndexOf(..., offset) >= 0 && IndexOf(..., offset) < Length - 1` check to every bare-separator segment, regardless of whether it is the last segment or whether the pattern ends with a wildcard. The `< Length - 1` constraint was designed to ensure the trailing wildcard in patterns like `"EMPLOYEE#*#LINE#*"` matches at least 1 character. But when the pattern ends with a literal (no trailing wildcard), the terminal separator IS the expected final character, and requiring content after it is incorrect.

2. **Same issue in three code paths**: The identical logic appears in:
   - `GenerateComplexPatternCheck` "return" mode (line ~4755): `IndexOf(...) >= 0 && IndexOf(...) < Length - 1`
   - `GenerateComplexPatternCheck` "negated" mode (line ~4797): `IndexOf(...) < 0 || IndexOf(...) >= Length - 1`
   - `GenerateComplexExclusionCheck` (line ~4845): `IndexOf(...) >= 0 && IndexOf(...) < Length - 1`

3. **Why it was overlooked**: The original implementation correctly handles the common case where patterns end with a wildcard (e.g., `"INVOICE#*#LINE#*"`). The trailing-literal case (`"EMPLOYEE#*#"`) was not anticipated during the initial Complex pattern implementation because most `[Computed]` formats end with a placeholder, not a literal.

## Correctness Properties

Property 1: Bug Condition - Trailing Bare-Separator Omits Length Constraint

_For any_ Complex discriminator pattern that ends with a bare-separator literal after the last wildcard (isBugCondition returns true), the fixed `GenerateComplexPatternCheck` in "return" mode SHALL generate a trailing check that only requires `IndexOf(separator, offset) >= 0` without the `< discriminatorValue.S.Length - 1` constraint, and in "negated" mode SHALL generate a check that only requires `IndexOf(separator, offset) < 0` without the `>= discriminatorValue.S.Length - 1` branch.

**Validates: Requirements 2.1, 2.2**

Property 2: Bug Condition - Trailing Bare-Separator Exclusion Omits Length Constraint

_For any_ Complex discriminator pattern that ends with a bare-separator literal after the last wildcard (isBugCondition returns true), the fixed `GenerateComplexExclusionCheck` SHALL generate an exclusion guard with a trailing check that only requires `IndexOf(separator, offset) >= 0` without the `< discriminatorValue.S.Length - 1` constraint.

**Validates: Requirements 2.3**

Property 3: Preservation - Non-Trailing Segments Retain Length Constraint

_For any_ Complex discriminator pattern where a bare-separator segment is NOT the trailing segment (isBugCondition returns false because it is not the last segment), the fixed code SHALL produce the same `IndexOf` checks as the original code, preserving the `< Length - 1` (return/exclusion) and `>= Length - 1` (negated) constraints for non-trailing bare-separator segments.

**Validates: Requirements 3.1, 3.3, 3.4, 3.5**

Property 4: Preservation - Wildcard-Ending Patterns Unchanged

_For any_ Complex discriminator pattern that ends with a wildcard (e.g., `"INVOICE#*#LINE#*"`), the fixed code SHALL produce exactly the same generated output as the original code for all three methods, preserving all `< Length - 1` and `>= Length - 1` constraints since no segment is a trailing bare-separator.

**Validates: Requirements 3.1, 3.4, 3.5**

## Fix Implementation

### Changes Required

Assuming our root cause analysis is correct:

**File**: `Oproto.FluentDynamoDb.SourceGenerator/Generators/MapperGenerator.cs`

**Methods**: `GenerateComplexPatternCheck`, `GenerateComplexExclusionCheck`

**Specific Changes**:

1. **Add trailing bare-separator detection**: In the loop over `nonEmptySegments` (starting at index 1), compute whether the current segment is a trailing bare-separator:
   ```csharp
   bool isLastSegment = (i == nonEmptySegments.Count - 1);
   bool patternEndsWithLiteral = !pattern.EndsWith("*");
   bool isTrailingBareSeparator = isLastSegment && patternEndsWithLiteral;
   ```

2. **Modify `GenerateComplexPatternCheck` "return" mode** (line ~4755): When `isTrailingBareSeparator` is true, emit only `IndexOf(..., offset) >= 0` without the `&& IndexOf(..., offset) < discriminatorValue.S.Length - 1` part. When false, keep the existing two-part condition.

3. **Modify `GenerateComplexPatternCheck` "negated" mode** (line ~4797): When `isTrailingBareSeparator` is true, emit only `IndexOf(..., offset) < 0` without the `|| IndexOf(..., offset) >= discriminatorValue.S.Length - 1` part. When false, keep the existing two-part condition.

4. **Modify `GenerateComplexExclusionCheck`** (line ~4845): When `isTrailingBareSeparator` is true, emit only `IndexOf(..., offset) >= 0` without the `&& IndexOf(..., offset) < discriminatorValue.S.Length - 1` part. When false, keep the existing two-part condition.

5. **Update comments**: Update the inline comments in all three locations to explain the trailing bare-separator distinction — why `< Length - 1` is correct for non-trailing segments but incorrect for trailing ones.

### Example: Corrected Generated Code

**Pattern `"EMPLOYEE#*#"` — return mode:**

Before (buggy):
```csharp
return discriminatorValue.S.StartsWith("EMPLOYEE#")
    && discriminatorValue.S.IndexOf("#", 10) >= 0
    && discriminatorValue.S.IndexOf("#", 10) < discriminatorValue.S.Length - 1;
```

After (fixed):
```csharp
return discriminatorValue.S.StartsWith("EMPLOYEE#")
    && discriminatorValue.S.IndexOf("#", 10) >= 0;
```

**Pattern `"EMPLOYEE#*#"` — negated mode:**

Before (buggy):
```csharp
if (!discriminatorValue.S.StartsWith("EMPLOYEE#")
    || discriminatorValue.S.IndexOf("#", 10) < 0
    || discriminatorValue.S.IndexOf("#", 10) >= discriminatorValue.S.Length - 1)
    return false;
```

After (fixed):
```csharp
if (!discriminatorValue.S.StartsWith("EMPLOYEE#")
    || discriminatorValue.S.IndexOf("#", 10) < 0)
    return false;
```

**Pattern `"INVOICE#*#LINE#*"` — unchanged (ends with wildcard):**
```csharp
return discriminatorValue.S.StartsWith("INVOICE#")
    && discriminatorValue.S.Contains("#LINE#");
```
(This pattern's `#LINE#` segment is not a bare-separator, so it uses `Contains` — unaffected.)

## Testing Strategy

### Validation Approach

The testing strategy follows a two-phase approach: first, surface counterexamples that demonstrate the bug on unfixed code, then verify the fix works correctly and preserves existing behavior.

### Exploratory Bug Condition Checking

**Goal**: Surface counterexamples that demonstrate the bug BEFORE implementing the fix. Confirm or refute the root cause analysis. If we refute, we will need to re-hypothesize.

**Test Plan**: Call `GenerateComplexPatternCheck` and `GenerateComplexExclusionCheck` with patterns ending in a trailing literal (e.g., `"EMPLOYEE#*#"`) and inspect the generated code string. Verify the generated code contains the `< discriminatorValue.S.Length - 1` constraint that causes the rejection. Then evaluate the generated expression against concrete values to demonstrate the bug.

**Test Cases**:
1. **Return mode with `"EMPLOYEE#*#"`**: Generate the code and verify it contains `< discriminatorValue.S.Length - 1` for the trailing `#` segment (will demonstrate the bug on unfixed code)
2. **Negated mode with `"EMPLOYEE#*#"`**: Generate the code and verify it contains `>= discriminatorValue.S.Length - 1` (will demonstrate the bug on unfixed code)
3. **Exclusion mode with `"EMPLOYEE#*#"`**: Generate the code and verify it contains `< discriminatorValue.S.Length - 1` (will demonstrate the bug on unfixed code)
4. **Evaluation test**: For value `"EMPLOYEE#abc123#"` (length 16), `IndexOf("#", 10)` returns 15, and `15 < 15` is false — demonstrates the item is incorrectly rejected

**Expected Counterexamples**:
- All three methods generate a length constraint on the trailing segment that rejects valid terminal-separator values
- Cause: unconditional `< Length - 1` applied to all bare-separator segments without distinguishing trailing from non-trailing

### Fix Checking

**Goal**: Verify that for all inputs where the bug condition holds, the fixed function produces the expected behavior.

**Pseudocode:**
```
FOR ALL pattern WHERE isBugCondition(pattern, lastSegmentIndex, mode) DO
  generatedCode := GenerateComplexPatternCheck_fixed(pattern, mode)
  // or GenerateComplexExclusionCheck_fixed(pattern)
  ASSERT trailingSegmentCheck(generatedCode) does NOT contain "< discriminatorValue.S.Length - 1"
  ASSERT trailingSegmentCheck(generatedCode) does NOT contain ">= discriminatorValue.S.Length - 1"
END FOR
```

### Preservation Checking

**Goal**: Verify that for all inputs where the bug condition does NOT hold, the fixed function produces the same result as the original function.

**Pseudocode:**
```
FOR ALL pattern WHERE NOT isBugCondition(pattern, segmentIndex, mode) DO
  ASSERT GenerateComplexPatternCheck_original(pattern, mode) == GenerateComplexPatternCheck_fixed(pattern, mode)
  ASSERT GenerateComplexExclusionCheck_original(pattern) == GenerateComplexExclusionCheck_fixed(pattern)
END FOR
```

**Testing Approach**: Property-based testing is recommended for preservation checking because:
- It generates many pattern combinations automatically across the input domain
- It catches edge cases with multi-wildcard patterns that manual unit tests might miss
- It provides strong guarantees that behavior is unchanged for all non-trailing-literal patterns

**Test Plan**: Observe the generated output on UNFIXED code first for patterns ending with a wildcard (e.g., `"INVOICE#*#LINE#*"`, `"PREFIX#*#SUFFIX#*"`), then write property-based tests that capture that exact output as the expected behavior after the fix.

**Test Cases**:
1. **Wildcard-ending preservation**: Verify `"INVOICE#*#LINE#*"` produces identical output before and after fix in all three modes
2. **Multi-wildcard preservation**: Verify `"A#*#B#*#C#*"` produces identical output before and after fix
3. **Non-trailing bare-separator preservation**: Verify `"PREFIX#*#*#"` keeps `< Length - 1` for the first `#` segment (non-trailing) while only the last `#` (trailing) drops it
4. **StartsWith-pattern preservation**: Verify `"ORDER#*"` is classified as `StartsWith` and never enters the Complex path

### Unit Tests

- Test `GenerateComplexPatternCheck` in "return" mode with `"EMPLOYEE#*#"` → generated code accepts `"EMPLOYEE#abc123#"`
- Test `GenerateComplexPatternCheck` in "negated" mode with `"EMPLOYEE#*#"` → generated code does not reject `"EMPLOYEE#abc123#"`
- Test `GenerateComplexExclusionCheck` with `"EMPLOYEE#*#"` → generated exclusion guard fires correctly for `"EMPLOYEE#abc123#"`
- Test edge case: `"EMPLOYEE#*#"` with value `"EMPLOYEE#x#"` (minimal 1-char wildcard match)
- Test edge case: `"EMPLOYEE#*#"` with value `"EMPLOYEE##"` (zero-char wildcard — should be rejected by the offset+1 check)
- Test `"PREFIX#*#*#"` — non-trailing `#` keeps constraint, trailing `#` does not

### Property-Based Tests

- Generate random Complex patterns ending with a trailing literal and verify the generated code string does not contain `< discriminatorValue.S.Length - 1` for the trailing segment
- Generate random Complex patterns ending with a wildcard and verify the generated code string is byte-for-byte identical to the unfixed code
- Generate random multi-segment patterns and verify non-trailing bare-separator segments always retain the `< Length - 1` constraint

### Integration Tests

- Full source generator integration test with an entity using `[Computed("EmployeeId", Format = "EMPLOYEE#{0}#")]` — verify the generated `MatchesEntity` code accepts `"EMPLOYEE#abc123#"`
- Multi-entity table integration test with `"EMPLOYEE#*#"` and `"EMPLOYEE#*#DEDUCTION#*"` — verify exclusion guards work correctly when one pattern has a trailing literal
- Verify `Query<Employee>()` scenario: items with sort key `"EMPLOYEE#abc123#"` are included in results
