# Compound Discrimination Prefix Subsumption Bugfix Design

## Overview

Three related bugs in the source generator's discriminator analysis produce incorrect `MatchesEntity` code generation, spurious FDDB102 warnings, and unnecessary exclusion patterns when entities share a table with overlapping SK patterns and PK patterns that have prefix-subset relationships.

**Bug 1** (functional): `CompoundPromotionPass` assigns dual positive `StartsWith` compound constraints where one PK prefix subsumes the other (e.g., `"TENANT#"` is a prefix of `"TENANT#PLATFORM#ROLE#"`). The shorter-prefix entity's `MatchesEntity` incorrectly claims items belonging to the longer-prefix entity, violating mutual exclusivity. This is the root cause of 66 test failures.

**Bug 2** (functional): `PatternOverlapAnalyzer.ExactValueMatchesPattern` returns `true` unconditionally for Complex patterns, even when the exact value structurally cannot match (e.g., `"SETTINGS"` vs `"CAP#*#*"`). This produces spurious FDDB102 warnings and unnecessary exclusion patterns.

**Bug 3** (cosmetic): `PatternOverlapAnalyzer.Analyze` emits FDDB102 for different-score auto-derived pairs before evaluating whether the overlap is resolved by a non-tautological exclusion pattern, misleading users into thinking overlaps are unresolved.

The fix strategy is minimal and targeted: add a post-assignment prefix subsumption check in `CompoundPromotionPass`, add a leading-prefix structural check in `ExactValueMatchesPattern`, and reorder the FDDB102 emission logic in `Analyze` to suppress it for non-tautological exclusions.

## Glossary

- **Bug_Condition (C)**: The condition(s) that trigger each bug — prefix subsumption in dual positive constraints (Bug 1), unconditional `true` return for ExactMatch vs Complex (Bug 2), and FDDB102 emission before exclusion evaluation (Bug 3)
- **Property (P)**: The desired behavior — mutual exclusivity of `MatchesEntity` (Bug 1), structural matching for ExactMatch vs Complex (Bug 2), suppressed FDDB102 for resolved pairs (Bug 3)
- **Preservation**: Existing behavior for non-subsumptive prefixes, non-Complex patterns, same-score pairs, and tautological exclusions that must remain unchanged
- **CompoundPromotionPass**: The class in `Analysis/CompoundPromotionPass.cs` that resolves same-score discriminator overlaps by inspecting cross-key `DerivedDiscriminatorPatterns` and assigning `CompoundConstraint` objects
- **PatternOverlapAnalyzer**: The class in `Analysis/PatternOverlapAnalyzer.cs` that detects overlapping discriminator patterns, computes specificity scores, and populates `ExclusionPattern` lists for less-specific entities
- **GetEffectiveCrossKeyPattern**: Method in `CompoundPromotionPass` that reduces Complex PK patterns to synthetic `StartsWith` patterns (e.g., `"TENANT#*#ROLE#*"` → `"TENANT#*"`)
- **AssignPositiveConstraint**: Method in `CompoundPromotionPass` that sets a positive `CompoundConstraint` on an entity's `DiscriminatorConfig`
- **ExactValueMatchesPattern**: Method in `PatternOverlapAnalyzer` that determines whether an ExactMatch value could match a wildcard pattern
- **Prefix Subsumption**: When one `StartsWith` literal text is an ordinal string prefix of another (e.g., `"TENANT#"` is a prefix of `"TENANT#PLATFORM#ROLE#"`)

## Bug Details

### Bug Condition

The bugs manifest across three distinct code paths in the discriminator analysis pipeline. Bug 1 occurs when `CompoundPromotionPass` assigns dual positive `StartsWith` compound constraints for a same-score pair and the shorter entity's literal text is a prefix of the longer entity's literal text. Bug 2 occurs when `ExactValueMatchesPattern` evaluates an ExactMatch value against a Complex pattern. Bug 3 occurs when `Analyze` processes a different-score auto-derived pair that is resolved by a non-tautological exclusion.

**Formal Specification:**
```
FUNCTION isBugCondition_Bug1(entityA, entityB)
  INPUT: entityA, entityB — two entities processed by CompoundPromotionPass
  OUTPUT: boolean

  LET constraintA = entityA.Discriminator.CompoundConstraint
  LET constraintB = entityB.Discriminator.CompoundConstraint

  RETURN constraintA IS NOT NULL
         AND constraintB IS NOT NULL
         AND constraintA.IsExclusion == false
         AND constraintB.IsExclusion == false
         AND constraintA.Strategy == StartsWith
         AND constraintB.Strategy == StartsWith
         AND (constraintA.LiteralText.StartsWith(constraintB.LiteralText)
              OR constraintB.LiteralText.StartsWith(constraintA.LiteralText))
         AND constraintA.LiteralText != constraintB.LiteralText
END FUNCTION

FUNCTION isBugCondition_Bug2(exactValue, patternConfig)
  INPUT: exactValue — string, patternConfig — DiscriminatorConfig
  OUTPUT: boolean

  RETURN patternConfig.Strategy == Complex
         AND patternConfig.Pattern IS NOT NULL
         AND LET segments = patternConfig.Pattern.Split('*')
         AND LET leadingPrefix = segments.FirstNonEmpty()
         AND leadingPrefix IS NOT NULL
         AND NOT exactValue.StartsWith(leadingPrefix)
END FUNCTION

FUNCTION isBugCondition_Bug3(configA, configB, exclusion)
  INPUT: configA, configB — DiscriminatorConfig, exclusion — ExclusionPattern
  OUTPUT: boolean

  LET scoreA = ComputeSpecificityScore(configA)
  LET scoreB = ComputeSpecificityScore(configB)

  RETURN scoreA != scoreB
         AND configA.IsAutoDerived == true
         AND configB.IsAutoDerived == true
         AND IsTautologicalExclusion(lessSpecificConfig, exclusion) == false
END FUNCTION
```

### Examples

- **Bug 1**: PlatformRoleCapabilityEntity gets `pk.StartsWith("TENANT#PLATFORM#ROLE#")` and RoleCapabilityEntity gets `pk.StartsWith("TENANT#")`. For item `pk="TENANT#PLATFORM#ROLE#admin"`, both `MatchesEntity` methods return `true` — expected: only PlatformRoleCapabilityEntity matches.
- **Bug 1**: Two entities with prefixes `"SERVICE#"` and `"SERVICE#ADMIN#"` — the shorter-prefix entity's `MatchesEntity` also matches items with `pk="SERVICE#ADMIN#xyz"`.
- **Bug 2**: `ExactValueMatchesPattern("SETTINGS", config{Pattern="CAP#*#*", Strategy=Complex})` returns `true` — expected: `false` because `"SETTINGS"` does not start with `"CAP#"`.
- **Bug 2**: `ExactValueMatchesPattern("PROFILE", config{Pattern="*#DATA#*", Strategy=Complex})` should still return `true` (conservative) because the Complex pattern starts with `*` and has no leading prefix to rule out overlap.
- **Bug 3**: CapabilityDefinitionEntity (score 1, `CAP#*`) vs PlatformRoleCapabilityEntity (score 2, `CAP#*#*`) — overlap is resolved by exclusion `IndexOf("#", 4) >= 0`, but FDDB102 is still emitted. Expected: no FDDB102 because the exclusion is non-tautological.
- **Bug 3**: If an exclusion IS tautological (e.g., `Contains("#")` when positive is `StartsWith("CAP#")`), FDDB102 should still be emitted.

## Expected Behavior

### Preservation Requirements

**Unchanged Behaviors:**
- Dual positive `StartsWith` compound constraints for non-subsumptive prefix pairs (e.g., `"PLATFORM#"` and `"TENANT#"`) must continue to work without exclusion guards
- One-null-one-non-null cross-key pattern pairs must continue to produce positive + exclusion constraints
- Same-score pairs with identical or both-null cross-key patterns must continue to emit FDDB102/DISC004 diagnostics
- `ExactValueMatchesPattern` for `StartsWith`, `EndsWith`, and `Contains` strategies must use the existing structural matching logic unchanged
- Same-score FDDB102 diagnostics must continue to be emitted regardless of subsequent `CompoundPromotionPass` resolution
- Internal-segment fallback resolution in `CompoundPromotionPass` must continue to function for entities with Complex PK patterns sharing the same reduced prefix
- `IsTautologicalExclusion` logic must remain unchanged
- FDDB104 info diagnostics for compound promotion resolutions must continue to be emitted
- Different-score pairs with non-auto-derived discriminators must continue to skip FDDB102

**Scope:**
All inputs that do NOT involve prefix subsumption (Bug 1), ExactMatch vs Complex evaluation (Bug 2), or different-score auto-derived pair FDDB102 emission (Bug 3) should be completely unaffected by these fixes. This includes:
- Same-strategy wildcard overlap checks (`WildcardPatternsOverlap`, `ComplexPatternsOverlap`)
- `CreateExclusionPattern` logic
- `ComputeSpecificityScore` logic
- All `CompoundPromotionPass` paths for non-`StartsWith` compound constraints

## Hypothesized Root Cause

Based on the bug descriptions and code analysis, the root causes are:

1. **Missing Post-Assignment Subsumption Check (Bug 1)**: `CompoundPromotionPass.Analyze` calls `AreDisambiguable` to check if two cross-key patterns differ, then calls `AssignPositiveConstraint` for both entities. However, `AreDisambiguable` only checks that the patterns are not identical — it does not check whether one `StartsWith` literal is a prefix of the other. After both positive constraints are assigned, there is no verification step to detect that `"TENANT#"` subsumes `"TENANT#PLATFORM#ROLE#"`. The `GetEffectiveCrossKeyPattern` method correctly reduces Complex patterns to prefix-only form, but the downstream logic does not account for the reduced prefix creating a subsumption relationship.

2. **Unconditional `true` Return for Complex (Bug 2)**: `ExactValueMatchesPattern` has a `case DiscriminatorStrategy.Complex: return true;` path that was intentionally conservative but overly so. Complex patterns like `"CAP#*#*"` have a clear leading prefix segment (`"CAP#"`) that can be checked against the exact value. The code already extracts `literalText` from the pattern using `DiscriminatorAnalyzer.GetPatternText`, but for Complex patterns this extraction doesn't apply the right logic — it should split on `*` and check the first non-empty segment.

3. **Premature FDDB102 Emission (Bug 3)**: In the different-score branch of `PatternOverlapAnalyzer.Analyze`, the FDDB102 diagnostic is added unconditionally for auto-derived pairs, before the code calls `CreateExclusionPattern` and `IsTautologicalExclusion`. The emission should be deferred until after the tautological check, and only emitted when the exclusion IS tautological (meaning the overlap cannot actually be resolved).

## Correctness Properties

Property 1: Bug Condition - Prefix Subsumption Produces Exclusion Guard

_For any_ pair of entities where `CompoundPromotionPass` assigns dual positive `StartsWith` compound constraints and one entity's `LiteralText` is an ordinal string prefix of the other's (and they are not identical), the fixed `CompoundPromotionPass.Analyze` SHALL add an exclusion `CompoundConstraint` to the shorter-prefix entity that rejects items matching the longer prefix, ensuring that for any DynamoDB item, at most one of the two entities' `MatchesEntity` methods returns `true`.

**Validates: Requirements 2.1, 2.2**

Property 2: Bug Condition - ExactValueMatchesPattern Returns False for Non-Matching Complex Prefix

_For any_ ExactMatch value and Complex pattern where the pattern has a non-empty leading prefix segment (text before the first `*`) and the exact value does not start with that leading prefix segment, the fixed `ExactValueMatchesPattern` SHALL return `false`.

**Validates: Requirements 2.3**

Property 3: Bug Condition - FDDB102 Suppressed for Non-Tautological Different-Score Pairs

_For any_ different-score auto-derived overlapping pair where `CreateExclusionPattern` produces an exclusion for which `IsTautologicalExclusion` returns `false`, the fixed `PatternOverlapAnalyzer.Analyze` SHALL NOT emit an FDDB102 diagnostic for that pair, while still adding the `ExclusionPattern` to `OverlappingPatterns` and emitting the DISC005 informational diagnostic.

**Validates: Requirements 2.4, 2.5**

Property 4: Preservation - Non-Subsumptive Prefix Pairs Unchanged

_For any_ pair of entities where `CompoundPromotionPass` assigns dual positive `StartsWith` compound constraints and neither entity's `LiteralText` is a prefix of the other's (or they are identical), the fixed code SHALL produce exactly the same result as the original code — no exclusion guards added, no changes to existing constraints.

**Validates: Requirements 3.1, 3.2, 3.7, 3.8**

Property 5: Preservation - ExactValueMatchesPattern Non-Complex Strategies Unchanged

_For any_ ExactMatch value evaluated against a `StartsWith`, `EndsWith`, or `Contains` pattern, the fixed `ExactValueMatchesPattern` SHALL return the same result as the original function.

**Validates: Requirements 3.5**

Property 6: Preservation - FDDB102 Preserved for Tautological Exclusions and Same-Score Pairs

_For any_ different-score auto-derived pair where `IsTautologicalExclusion` returns `true`, and for any same-score auto-derived pair, the fixed code SHALL continue to emit the FDDB102 diagnostic exactly as the original code does.

**Validates: Requirements 3.4, 3.6**

## Fix Implementation

### Changes Required

Assuming our root cause analysis is correct:

**File**: `Oproto.FluentDynamoDb.SourceGenerator/Analysis/CompoundPromotionPass.cs`

**Function**: `Analyze`

**Specific Changes**:

1. **Add Post-Assignment Prefix Subsumption Detection**: After both entities receive positive `StartsWith` compound constraints via `AssignPositiveConstraint`, add a check to detect if one `LiteralText` is an ordinal string prefix of the other. This check applies only when both constraints have `Strategy == StartsWith` and both are positive (non-exclusion).

   ```
   // After both AssignPositiveConstraint calls:
   IF constraintA.Strategy == StartsWith AND constraintB.Strategy == StartsWith THEN
     LET litA = constraintA.LiteralText
     LET litB = constraintB.LiteralText
     IF litA != litB AND litA.StartsWith(litB) THEN
       // litB is shorter prefix — add exclusion for litA to entityB
       AddExclusionToShorterPrefix(entityB, crossKeyAttrName, litA, entityA.ClassName)
     ELSE IF litA != litB AND litB.StartsWith(litA) THEN
       // litA is shorter prefix — add exclusion for litB to entityA
       AddExclusionToShorterPrefix(entityA, crossKeyAttrName, litB, entityB.ClassName)
     END IF
   END IF
   ```

2. **Implement Exclusion Guard Application for Subsumptive Prefix**: The exclusion guard for the shorter-prefix entity should be a `StartsWith` exclusion for the longer prefix. This can reuse the existing `AssignExclusionConstraint` pattern but applied after the positive constraints are already set. The shorter-prefix entity keeps its positive constraint and additionally receives an exclusion constraint that rejects items matching the longer prefix. Since `AssignExclusionConstraint` sets the primary compound constraint, and the entity already has a positive constraint, accumulate the exclusion in `AdditionalExclusions` on the existing positive constraint — or replace with a new structure that carries both. The simplest approach: add the exclusion as an additional exclusion on the existing positive constraint.

3. **Add Helper Method for Subsumption Exclusion**: Create a private method `ApplyPrefixSubsumptionExclusion` that:
   - Takes the shorter-prefix entity, cross-key attribute name, the longer prefix literal text, and the source entity name
   - Creates a `CompoundConstraint` with `IsExclusion = true`, `Strategy = StartsWith`, `LiteralText = longerPrefix`
   - Attaches it to the existing positive constraint's `AdditionalExclusions` list

---

**File**: `Oproto.FluentDynamoDb.SourceGenerator/Analysis/PatternOverlapAnalyzer.cs`

**Function**: `ExactValueMatchesPattern`

**Specific Changes**:

4. **Add Leading Prefix Check for Complex Patterns**: Replace the unconditional `return true` for `DiscriminatorStrategy.Complex` with a structural check:
   - Split `patternConfig.Pattern` on `'*'`
   - Find the first non-empty segment (leading prefix)
   - If a non-empty leading prefix exists and the exact value does NOT start with it (ordinal comparison), return `false`
   - Otherwise return `true` (conservative for remaining structural ambiguity and for patterns starting with `*`)

---

**Function**: `Analyze` (different-score branch)

**Specific Changes**:

5. **Defer FDDB102 Emission Until After Tautological Check**: Restructure the different-score branch to:
   - First call `CreateExclusionPattern`
   - Then call `IsTautologicalExclusion`
   - If tautological: emit FDDB102 (for auto-derived pairs) AND DISC006, do NOT add to `OverlappingPatterns`
   - If non-tautological: do NOT emit FDDB102, add to `OverlappingPatterns`, emit DISC005
   - Move the FDDB102 emission from before the exclusion logic to inside the tautological branch

## Testing Strategy

### Validation Approach

The testing strategy follows a two-phase approach: first, surface counterexamples that demonstrate the bugs on unfixed code, then verify the fixes work correctly and preserve existing behavior.

### Exploratory Bug Condition Checking

**Goal**: Surface counterexamples that demonstrate the bugs BEFORE implementing the fix. Confirm or refute the root cause analysis. If we refute, we will need to re-hypothesize.

**Test Plan**: Write unit tests that exercise the three bug conditions against the unfixed code. For Bug 1, create entity models with subsumptive PK prefixes and verify both `MatchesEntity` methods return `true` for the same item. For Bug 2, call `ExactValueMatchesPattern` with `"SETTINGS"` and a `CAP#*#*` Complex config. For Bug 3, run `Analyze` on a different-score auto-derived pair and check that FDDB102 is in the diagnostics list.

**Test Cases**:
1. **Prefix Subsumption Test**: Create RoleCapabilityEntity (PK `TENANT#*`) and PlatformRoleCapabilityEntity (PK `TENANT#PLATFORM#ROLE#*`) with same SK score, run `CompoundPromotionPass.Analyze`, verify both get positive-only constraints without exclusion (will demonstrate Bug 1 on unfixed code)
2. **ExactMatch vs Complex Test**: Call `ExactValueMatchesPattern("SETTINGS", {Pattern="CAP#*#*", Strategy=Complex})`, expect `true` on unfixed code (Bug 2)
3. **FDDB102 Emission Test**: Create CapabilityDefinitionEntity (score 1, `CAP#*`) and PlatformRoleCapabilityEntity (score 2, `CAP#*#*`), run `Analyze`, expect FDDB102 in diagnostics on unfixed code (Bug 3)
4. **Edge Case Test**: Create entities with identical PK prefixes (not subsumptive) — verify no exclusion guard needed

**Expected Counterexamples**:
- Bug 1: Both entities have positive `StartsWith` constraints, no exclusion guard, allowing dual `MatchesEntity` true returns
- Bug 2: `ExactValueMatchesPattern` returns `true` for structurally incompatible exact value
- Bug 3: FDDB102 diagnostic present for a pair that is correctly resolved by exclusion

### Fix Checking

**Goal**: Verify that for all inputs where the bug conditions hold, the fixed functions produce the expected behavior.

**Pseudocode:**
```
FOR ALL (entityA, entityB) WHERE isBugCondition_Bug1(entityA, entityB) DO
  result := CompoundPromotionPass.Analyze_fixed(entities)
  LET shorterEntity = entity with shorter LiteralText
  ASSERT shorterEntity.Discriminator.CompoundConstraint.AdditionalExclusions contains
         exclusion with LiteralText == longerPrefix AND IsExclusion == true
END FOR

FOR ALL (exactValue, patternConfig) WHERE isBugCondition_Bug2(exactValue, patternConfig) DO
  result := ExactValueMatchesPattern_fixed(exactValue, patternConfig)
  ASSERT result == false
END FOR

FOR ALL (configA, configB) WHERE isBugCondition_Bug3(configA, configB, exclusion) DO
  diagnostics := Analyze_fixed(tableEntities)
  ASSERT diagnostics does NOT contain FDDB102 for this pair
  ASSERT diagnostics contains DISC005 for this pair
  ASSERT lessSpecificEntity.Discriminator.OverlappingPatterns contains the exclusion
END FOR
```

### Preservation Checking

**Goal**: Verify that for all inputs where the bug conditions do NOT hold, the fixed functions produce the same results as the original functions.

**Pseudocode:**
```
FOR ALL (entityA, entityB) WHERE NOT isBugCondition_Bug1(entityA, entityB) DO
  ASSERT CompoundPromotionPass.Analyze_original(entities) = CompoundPromotionPass.Analyze_fixed(entities)
END FOR

FOR ALL (exactValue, patternConfig) WHERE NOT isBugCondition_Bug2(exactValue, patternConfig) DO
  ASSERT ExactValueMatchesPattern_original(exactValue, patternConfig)
       = ExactValueMatchesPattern_fixed(exactValue, patternConfig)
END FOR

FOR ALL (configA, configB) WHERE NOT isBugCondition_Bug3(configA, configB, exclusion) DO
  ASSERT Analyze_original(tableEntities) = Analyze_fixed(tableEntities)
END FOR
```

**Testing Approach**: Property-based testing is recommended for preservation checking because:
- It generates many test cases automatically across the input domain (random prefix strings, pattern configurations, specificity scores)
- It catches edge cases that manual unit tests might miss (e.g., empty prefixes, single-character prefixes, prefixes with special characters)
- It provides strong guarantees that behavior is unchanged for all non-buggy inputs

**Test Plan**: Observe behavior on UNFIXED code first for non-subsumptive pairs, non-Complex ExactMatch checks, and same-score/tautological pairs, then write property-based tests capturing that behavior.

**Test Cases**:
1. **Non-Subsumptive Pair Preservation**: Generate random prefix pairs where neither is a prefix of the other, verify no exclusion guards are added (same result as original)
2. **ExactMatch Non-Complex Preservation**: Generate random exact values and StartsWith/EndsWith/Contains patterns, verify `ExactValueMatchesPattern` returns the same result as original
3. **Same-Score FDDB102 Preservation**: Generate same-score auto-derived pairs, verify FDDB102 is still emitted
4. **Tautological Exclusion FDDB102 Preservation**: Generate different-score pairs with tautological exclusions, verify FDDB102 is still emitted

### Unit Tests

- Test `CompoundPromotionPass.Analyze` with subsumptive prefix pair (`"TENANT#"` vs `"TENANT#PLATFORM#ROLE#"`) — verify exclusion guard on shorter-prefix entity
- Test `CompoundPromotionPass.Analyze` with non-subsumptive pair (`"PLATFORM#"` vs `"TENANT#"`) — verify no exclusion guard
- Test `CompoundPromotionPass.Analyze` with identical prefix pair (`"TENANT#"` vs `"TENANT#"`) — verify no exclusion guard, falls through to internal-segment path
- Test `ExactValueMatchesPattern("SETTINGS", Complex("CAP#*#*"))` returns `false`
- Test `ExactValueMatchesPattern("CAP#read", Complex("CAP#*#*"))` returns `true` (starts with `"CAP#"`)
- Test `ExactValueMatchesPattern("ANYTHING", Complex("*#DATA#*"))` returns `true` (no leading prefix — starts with `*`)
- Test `Analyze` with different-score pair resolved by non-tautological exclusion — no FDDB102
- Test `Analyze` with different-score pair where exclusion is tautological — FDDB102 present
- Test `Analyze` with same-score auto-derived pair — FDDB102 still present

### Property-Based Tests

- Generate random pairs of prefix strings and verify: if one is a prefix of the other (and not equal), the shorter-prefix entity gets an exclusion guard; if not, no exclusion guard
- Generate random exact values and Complex patterns with non-empty leading prefixes; verify `ExactValueMatchesPattern` returns `false` when the exact value does not start with the leading prefix, and `true` otherwise
- Generate random different-score auto-derived pairs with varying exclusion tautological status; verify FDDB102 is present only when the exclusion is tautological

### Integration Tests

- Define the four AuthorizationTable entities (CapabilityDefinitionEntity, PlatformRoleCapabilityEntity, RoleCapabilityEntity, TenantSettingsEntity), run the full analysis pipeline, and verify:
  - PlatformRoleCapabilityEntity and RoleCapabilityEntity get compound constraints with mutual exclusivity (RoleCapabilityEntity gets exclusion guard for `"TENANT#PLATFORM#ROLE#"`)
  - No FDDB102 for CapDef vs PlatformRoleCap and CapDef vs RoleCap pairs (non-tautological exclusions)
  - No FDDB102 for PlatformRoleCap vs TenantSettings and RoleCap vs TenantSettings (structurally non-overlapping after Bug 2 fix)
  - DISC005 informational diagnostics present for resolved different-score pairs
  - Total diagnostic count matches expected (no spurious warnings)
