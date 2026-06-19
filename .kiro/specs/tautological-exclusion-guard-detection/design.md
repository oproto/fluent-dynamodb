# Tautological Exclusion Guard Detection Bugfix Design

## Overview

The `PatternOverlapAnalyzer.CreateExclusionPattern()` method can produce exclusion guards that are logically tautological — the exclusion check is identical to the entity's own positive match check, causing `MatchesEntity` to always return `false`. This fix adds compile-time detection of tautological exclusions in the `Analyze()` method and emits a DISC006 diagnostic error instead of silently generating broken code. The fix is entirely in the source generator (compile-time); no runtime changes are required.

## Glossary

- **Bug_Condition (C)**: The condition under which `CreateExclusionPattern()` produces an exclusion whose strategy and literal text are identical to the less-specific entity's own positive match criterion
- **Property (P)**: When a tautological exclusion is detected, the system emits DISC006 diagnostic error and does NOT add the exclusion to `OverlappingPatterns`
- **Preservation**: All existing behavior for valid (non-tautological) exclusion hierarchies, DISC004 ambiguous overlaps, DISC005 resolved overlaps, and non-overlapping patterns must remain unchanged
- **PatternOverlapAnalyzer**: The static class in `Analysis/PatternOverlapAnalyzer.cs` that detects overlaps between discriminator patterns and populates exclusion guards
- **CreateExclusionPattern()**: The method that converts a more-specific entity's discriminator config into an `ExclusionPattern` for the less-specific entity
- **Positive Match Criterion**: The strategy + literal text that the less-specific entity uses in its own MatchesEntity positive check (e.g., `Contains("#ROLE#")` for pattern `*#ROLE#*`)
- **Tautological Exclusion**: An exclusion guard where `exclusion.Strategy == positiveMatchStrategy AND exclusion.LiteralText == positiveMatchLiteral`, making the generated code always return false

## Bug Details

### Bug Condition

The bug manifests when a Complex-strategy entity (e.g., `USER#*#ROLE#*`) overlaps with a simpler entity (e.g., `*#ROLE#*` with Contains strategy) and `CreateExclusionPattern()` extracts a literal segment from the Complex pattern that is identical to the simpler entity's positive match literal. The generated exclusion guard then contradicts the positive match, making `MatchesEntity` unreachable.

**Formal Specification:**
```
FUNCTION isBugCondition(lessSpecificEntity, moreSpecificEntity)
  INPUT: lessSpecificEntity of type EntityModel, moreSpecificEntity of type EntityModel
  OUTPUT: boolean
  
  LET exclusion = CreateExclusionPattern(moreSpecificEntity, moreSpecificEntity.Discriminator)
  LET positiveStrategy = lessSpecificEntity.Discriminator.Strategy
  LET positiveLiteral = GetPatternText(lessSpecificEntity.Discriminator.Pattern, positiveStrategy)
  
  RETURN exclusion.Strategy == positiveStrategy
         AND exclusion.LiteralText == positiveLiteral
END FUNCTION
```

### Examples

- **Tautological (BUG)**: Entity A has pattern `*#ROLE#*` (Contains, literal `#ROLE#`). Entity B has pattern `USER#*#ROLE#*` (Complex, score 2). `CreateExclusionPattern(B)` extracts last internal segment `#ROLE#` with Contains strategy → exclusion is `Contains("#ROLE#")` which equals the positive check `Contains("#ROLE#")`. Result: `MatchesEntity` always returns false.

- **Valid hierarchy (NO BUG)**: Entity A has pattern `USER#*` (StartsWith, literal `USER#`). Entity B has pattern `USER#*#ROLE#*` (Complex, score 2). `CreateExclusionPattern(B)` extracts `#ROLE#` with Contains strategy → exclusion is `Contains("#ROLE#")` which differs from positive check `StartsWith("USER#")`. Result: correct mutual exclusivity.

- **Valid hierarchy (NO BUG)**: Entity A has pattern `INVOICE#*` (StartsWith, literal `INVOICE#`). Entity B has pattern `INVOICE#*#LINE#*` (Complex, score 2). `CreateExclusionPattern(B)` extracts `#LINE#` with Contains strategy → exclusion is `Contains("#LINE#")` which differs from positive check `StartsWith("INVOICE#")`. Result: correct mutual exclusivity.

- **Tautological (BUG)**: Entity A has pattern `*#DEDUCTION#*` (Contains, literal `#DEDUCTION#`). Entity B has pattern `EMPLOYEE#*#DEDUCTION#*` (Complex, score 2). `CreateExclusionPattern(B)` extracts last internal segment `#DEDUCTION#` → exclusion is `Contains("#DEDUCTION#")` which equals positive check `Contains("#DEDUCTION#")`. Result: always returns false.

## Expected Behavior

### Preservation Requirements

**Unchanged Behaviors:**
- Valid hierarchies where the exclusion literal differs from the positive match literal (e.g., `StartsWith("USER#")` excluded by `Contains("#ROLE#")`) must continue to generate correct exclusion guards
- DISC004 diagnostic must continue to fire for ambiguous same-score overlapping patterns
- DISC005 informational diagnostic must continue to fire for successfully resolved overlaps with valid exclusions
- Non-overlapping patterns on the same property must continue to produce independent MatchesEntity methods without exclusion guards
- `ComputeSpecificityScore()` behavior must remain unchanged
- `PatternsOverlap()` detection logic must remain unchanged
- Code generation in `MapperGenerator.GenerateDiscriminatorCheckWithExclusions()` must remain unchanged

**Scope:**
All pattern pairs where `isBugCondition` returns false should be completely unaffected by this fix. This includes:
- Pairs where strategies differ (e.g., StartsWith positive + Contains exclusion)
- Pairs where strategies match but literals differ
- ExactMatch exclusions (which can never be tautological with a wildcard positive)
- Non-overlapping pattern pairs that never reach exclusion creation

## Hypothesized Root Cause

Based on the code analysis, the root cause is in `PatternOverlapAnalyzer.CreateExclusionPattern()` combined with the lack of post-creation validation in `Analyze()`:

1. **CreateExclusionPattern reduces Complex patterns to a single Contains check**: For a Complex pattern like `USER#*#ROLE#*`, the method skips the first segment (`USER#`) and takes the **last internal segment** (`#ROLE#`). This is the correct heuristic for most hierarchies (where the parent uses StartsWith on the shared prefix).

2. **No tautology validation exists**: After creating the exclusion, `Analyze()` unconditionally adds it to `lessSpecific.Discriminator.OverlappingPatterns` and emits DISC005. There is no check to verify the exclusion is actually distinguishing from the entity's own positive match.

3. **The problem is inherent to Contains-vs-Complex overlap**: When a Contains entity overlaps with a Complex entity that shares the same internal segment, the "last internal segment" heuristic produces the same literal the Contains entity already uses positively. The heuristic works perfectly when the less-specific entity uses StartsWith (the common case), but fails for Contains parents.

4. **Silent failure mode**: The generated code compiles without errors — it's syntactically valid C# — but logically dead. No diagnostic alerts the user to the impossible condition.

## Correctness Properties

Property 1: Bug Condition - Tautological Exclusion Detection

_For any_ pair of entities where `CreateExclusionPattern()` produces an exclusion with the same strategy and literal text as the less-specific entity's positive match criterion, the `Analyze()` method SHALL emit a DISC006 diagnostic error and SHALL NOT add the exclusion to the entity's `OverlappingPatterns` list.

**Validates: Requirements 2.1, 2.2**

Property 2: Preservation - Valid Exclusion Hierarchies

_For any_ pair of entities where `CreateExclusionPattern()` produces an exclusion with a different strategy or different literal text than the less-specific entity's positive match criterion, the `Analyze()` method SHALL produce the same result as the original code: adding the exclusion to `OverlappingPatterns` and emitting DISC005 informational diagnostic.

**Validates: Requirements 3.1, 3.2, 3.3, 3.4**

## Fix Implementation

### Changes Required

Assuming our root cause analysis is correct:

**File**: `Oproto.FluentDynamoDb.SourceGenerator/Analysis/PatternOverlapAnalyzer.cs`

**Function**: `Analyze()` — the else branch that handles different-score overlaps

**Specific Changes**:

1. **Add tautology detection after exclusion creation**: After the call to `CreateExclusionPattern(moreSpecific, moreSpecificConfig)`, compute the less-specific entity's positive match literal and strategy, then compare them against the exclusion's strategy and literal text.

2. **Extract positive match literal helper**: Add a private helper method `GetPositiveMatchLiteral(DiscriminatorConfig config)` that returns the (strategy, literal) tuple representing what the entity's MatchesEntity positive check uses. For ExactMatch it returns the exact value; for StartsWith/EndsWith/Contains it delegates to `DiscriminatorAnalyzer.GetPatternText()`; for Complex it returns the StartsWith segment (first non-empty segment).

3. **Conditional exclusion addition**: If the exclusion is tautological (same strategy AND same literal as positive match), do NOT add it to `OverlappingPatterns`. Instead, emit a DISC006 diagnostic error.

4. **DISC006 diagnostic descriptor**: Add a new `DiagnosticDescriptor` in `DiagnosticDescriptors.cs` with ID `DISC006`, severity Error, with a message explaining that the exclusion pattern is tautological and would make MatchesEntity always return false.

5. **Update comment block**: Change the `// Discriminator Configuration Diagnostics (DISC001-DISC005)` comment to `(DISC001-DISC006)`.

**File**: `Oproto.FluentDynamoDb.SourceGenerator/Diagnostics/DiagnosticDescriptors.cs`

**Addition**: New DISC006 descriptor:
```csharp
public static readonly DiagnosticDescriptor TautologicalExclusionGuard = new(
    "DISC006",
    "Tautological exclusion guard detected",
    "Entity '{0}' (pattern '{1}') cannot exclude pattern '{2}' from entity '{3}' because the exclusion check ({4}(\"{5}\")) is identical to the entity's own positive match. This would make MatchesEntity always return false.",
    "DynamoDb",
    DiagnosticSeverity.Error,
    isEnabledByDefault: true,
    description: "A computed exclusion guard is tautological — it uses the same strategy and literal as the entity's own positive match criterion. This indicates the pattern hierarchy cannot be automatically resolved. Consider redesigning the discriminator patterns to use distinct literals.");
```

### Algorithm for Tautology Detection

```
FUNCTION isTautologicalExclusion(lessSpecificConfig, exclusion)
  INPUT: lessSpecificConfig of type DiscriminatorConfig
         exclusion of type ExclusionPattern
  OUTPUT: boolean

  // Get the positive match criterion for the less-specific entity
  LET positiveStrategy = lessSpecificConfig.Strategy
  LET positiveLiteral = CASE positiveStrategy OF
    ExactMatch  → lessSpecificConfig.ExactValue
    StartsWith  → GetPatternText(lessSpecificConfig.Pattern, StartsWith)
    EndsWith    → GetPatternText(lessSpecificConfig.Pattern, EndsWith)
    Contains    → GetPatternText(lessSpecificConfig.Pattern, Contains)
    Complex     → first non-empty segment of lessSpecificConfig.Pattern (StartsWith portion)
  END CASE

  // For Complex positive strategy, the generated code uses StartsWith for first segment
  // and Contains for subsequent segments. The primary distinguishing check is StartsWith.
  IF positiveStrategy == Complex THEN
    positiveStrategy = StartsWith
  END IF

  // Compare exclusion against positive match
  RETURN exclusion.Strategy == positiveStrategy
         AND String.Equals(exclusion.LiteralText, positiveLiteral, Ordinal)
END FUNCTION
```

### Where Detection Fits in the Pipeline

```
Attribute Parsing (DiscriminatorAnalyzer)
    ↓
Pattern Classification (DeterminePatternStrategy)
    ↓
Overlap Detection (PatternOverlapAnalyzer.Analyze)
    ↓  pairwise comparison loop
    ↓
Exclusion Creation (CreateExclusionPattern)
    ↓
┌─────────────────────────────────────────┐
│ ★ NEW: Tautology Check                 │
│   Compare exclusion vs positive match   │
│   If tautological → emit DISC006, skip  │
│   If valid → add to OverlappingPatterns │
└─────────────────────────────────────────┘
    ↓
Code Generation (MapperGenerator)
    ↓
Generated MatchesEntity (runtime)
```

## Testing Strategy

### Validation Approach

The testing strategy follows a two-phase approach: first, surface counterexamples that demonstrate the bug on unfixed code, then verify the fix works correctly and preserves existing behavior.

### Exploratory Bug Condition Checking

**Goal**: Surface counterexamples that demonstrate the bug BEFORE implementing the fix. Confirm or refute the root cause analysis. If we refute, we will need to re-hypothesize.

**Test Plan**: Write tests that create entity pairs where a Contains-strategy entity overlaps with a Complex-strategy entity sharing the same internal segment. Run `PatternOverlapAnalyzer.Analyze()` on the UNFIXED code and assert the resulting exclusion produces a tautological guard.

**Test Cases**:
1. **Contains vs Complex same segment**: `*#ROLE#*` overlaps `USER#*#ROLE#*` — assert exclusion literal equals positive literal (will demonstrate bug on unfixed code)
2. **Contains vs Complex same segment variant**: `*#DEDUCTION#*` overlaps `EMPLOYEE#*#DEDUCTION#*` — same tautology pattern (will demonstrate bug on unfixed code)
3. **Generated code analysis**: Run source generator with tautological pattern pair, inspect generated `MatchesEntity` — assert the positive + exclusion are contradictory (will demonstrate bug on unfixed code)
4. **Code reachability check**: Verify the `return true` statement in generated MatchesEntity is unreachable when tautological exclusion is present (will demonstrate bug on unfixed code)

**Expected Counterexamples**:
- `exclusion.LiteralText` equals `DiscriminatorAnalyzer.GetPatternText(lessSpecificConfig.Pattern, lessSpecificConfig.Strategy)` for the Contains entity
- Possible causes: CreateExclusionPattern's "last internal segment" heuristic produces the same literal as the Contains entity's positive match

### Fix Checking

**Goal**: Verify that for all inputs where the bug condition holds, the fixed function produces the expected behavior.

**Pseudocode:**
```
FOR ALL (lessSpecificEntity, moreSpecificEntity) WHERE isBugCondition(lessSpecific, moreSpecific) DO
  diagnostics := PatternOverlapAnalyzer.Analyze_fixed([lessSpecific, moreSpecific])
  ASSERT diagnostics.Any(d => d.Id == "DISC006")
  ASSERT lessSpecificEntity.Discriminator.OverlappingPatterns.Count == 0
END FOR
```

### Preservation Checking

**Goal**: Verify that for all inputs where the bug condition does NOT hold, the fixed function produces the same result as the original function.

**Pseudocode:**
```
FOR ALL (lessSpecificEntity, moreSpecificEntity) WHERE NOT isBugCondition(lessSpecific, moreSpecific) DO
  ASSERT Analyze_original([lessSpecific, moreSpecific]) == Analyze_fixed([lessSpecific, moreSpecific])
END FOR
```

**Testing Approach**: Property-based testing is recommended for preservation checking because:
- It generates many discriminator pattern combinations automatically across the input domain
- It catches edge cases in the tautology detection that manual unit tests might miss
- It provides strong guarantees that valid hierarchies are unchanged by the fix

**Test Plan**: Observe behavior on UNFIXED code first for valid hierarchies (StartsWith parent + Complex child, non-overlapping patterns), then write property-based tests capturing that behavior.

**Test Cases**:
1. **StartsWith + Complex preservation**: Verify `USER#*` with exclusion from `USER#*#ROLE#*` continues to produce valid exclusion `Contains("#ROLE#")` — different strategy than `StartsWith`
2. **DISC005 preservation**: Verify the informational diagnostic continues to be emitted for valid resolved overlaps
3. **DISC004 preservation**: Verify ambiguous same-score patterns continue to emit error diagnostic
4. **Non-overlapping preservation**: Verify `USER#*` and `ORDER#*` continue to produce no exclusions and no overlap diagnostics
5. **Hydration correctness for valid patterns**: Verify that entities with valid (non-tautological) exclusion guards correctly match only their own items at runtime

### Unit Tests

- Test `isTautologicalExclusion` helper directly with Contains-vs-Contains (tautological) and StartsWith-vs-Contains (valid) pairs
- Test that DISC006 diagnostic is emitted with correct message format arguments
- Test that `OverlappingPatterns` is NOT populated when tautology is detected
- Test edge cases: ExactMatch exclusion (never tautological with wildcard positive), empty literal, Complex positive strategy

### Property-Based Tests

- Generate random valid hierarchies (StartsWith parent + Complex children with distinct internal segments) and verify no DISC006 is emitted and exclusions are correctly populated
- Generate random tautological configurations (Contains parent + Complex child sharing the same segment) and verify DISC006 is always emitted
- Generate random non-overlapping pattern pairs and verify zero diagnostics and zero exclusions (preservation)

### Integration Tests

- Full source generator integration test: tautological pattern pair → verify DISC006 in output diagnostics, verify NO exclusion guard in generated code
- Full source generator integration test: valid hierarchy → verify DISC005 in diagnostics AND correct exclusion guard in generated MatchesEntity
- Hydration test: valid hierarchy with exclusion guards → verify `MatchesEntity` correctly accepts own items and rejects more-specific items
- Regression test: existing `EmployeePayrollComplexPatternIntegrationTests` and `TwoEntityHierarchyIntegrationTests` continue to pass unchanged
