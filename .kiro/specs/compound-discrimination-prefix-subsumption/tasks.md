# Implementation Plan

## Overview

This task list implements the bugfix for three related bugs in the source generator's discriminator analysis: prefix subsumption in `CompoundPromotionPass` (Bug 1), unconditional `true` return for ExactMatch vs Complex in `PatternOverlapAnalyzer.ExactValueMatchesPattern` (Bug 2), and premature FDDB102 emission in `PatternOverlapAnalyzer.Analyze` (Bug 3). The workflow follows the exploratory bugfix methodology — write bug condition and preservation tests before implementing the fix, then verify all tests pass after.

## Tasks

- [x] 1. Write bug condition exploration tests (BEFORE implementing fix)
  - **Property 1: Bug Condition** - Prefix Subsumption, ExactMatch vs Complex, and Spurious FDDB102
  - **CRITICAL**: These tests MUST FAIL on unfixed code — failure confirms the bugs exist
  - **DO NOT attempt to fix the tests or the code when they fail**
  - **NOTE**: These tests encode the expected behavior — they will validate the fix when they pass after implementation
  - **GOAL**: Surface counterexamples that demonstrate all three bugs exist
  - **Test file**: `Oproto.FluentDynamoDb.SourceGenerator.UnitTests/Analysis/PrefixSubsumptionBugConditionTests.cs`
  - **Trait**: `[Trait("Category", "BugExploration")]`, `[Trait("Feature", "compound-discrimination-prefix-subsumption")]`
  - **Bug 1 — Prefix Subsumption Exploration**:
    - **Scoped PBT Approach**: Scope the property to the concrete failing case — PlatformRoleCapabilityEntity (PK `TENANT#PLATFORM#ROLE#*`) and RoleCapabilityEntity (PK `TENANT#*#ROLE#*`, reduced to `TENANT#*`) with same SK score 2 (`CAP#*#*`)
    - Create entity models using the existing `CreateEntity` helper pattern from `CompoundPromotionPassTests.cs`
    - Run `CompoundPromotionPass.Analyze` on unfixed code
    - Assert the shorter-prefix entity (RoleCapabilityEntity, `LiteralText="TENANT#"`) receives an exclusion `CompoundConstraint` in `AdditionalExclusions` that rejects items starting with `"TENANT#PLATFORM#ROLE#"`
    - `isBugCondition_Bug1`: Both constraints are positive, both are `StartsWith`, one `LiteralText` is a prefix of the other, and they are not identical
    - On UNFIXED code: test FAILS — both entities get positive-only constraints with no exclusion guard
    - Document counterexample: "Both PlatformRoleCapabilityEntity and RoleCapabilityEntity have positive `StartsWith` constraints without exclusion guard — RoleCapabilityEntity with `StartsWith('TENANT#')` incorrectly matches items with PK `TENANT#PLATFORM#ROLE#xyz`"
    - Also test edge case: entities with identical PK prefixes (`"TENANT#"` vs `"TENANT#"`) should NOT trigger prefix subsumption — verify no exclusion guard is added (this should PASS on unfixed code)
  - **Bug 2 — ExactMatch vs Complex Exploration**:
    - Call `ExactValueMatchesPattern("SETTINGS", config{Pattern="CAP#*#*", Strategy=Complex})` via reflection or by testing through `PatternsOverlap`
    - Assert result is `false` (because `"SETTINGS"` does not start with `"CAP#"`)
    - `isBugCondition_Bug2`: Pattern strategy is Complex, pattern has non-empty leading prefix, exact value does NOT start with leading prefix
    - On UNFIXED code: test FAILS — returns `true` unconditionally for Complex patterns
    - Document counterexample: "`ExactValueMatchesPattern('SETTINGS', Complex('CAP#*#*'))` returns `true` but should return `false`"
    - Also test: `ExactValueMatchesPattern("CAP#read", Complex("CAP#*#*"))` should return `true` (starts with `"CAP#"`)
    - Also test: `ExactValueMatchesPattern("ANYTHING", Complex("*#DATA#*"))` should return `true` (no leading prefix — pattern starts with `*`)
  - **Bug 3 — FDDB102 Spurious Emission Exploration**:
    - Create CapabilityDefinitionEntity (score 1, `CAP#*`) and PlatformRoleCapabilityEntity (score 2, `CAP#*#*`), both auto-derived
    - Run `PatternOverlapAnalyzer.Analyze`
    - Assert FDDB102 is NOT present in diagnostics for this pair (because the exclusion `IndexOf("#", 4) >= 0` is non-tautological)
    - `isBugCondition_Bug3`: Different scores, both auto-derived, `IsTautologicalExclusion` returns `false`
    - On UNFIXED code: test FAILS — FDDB102 is emitted before exclusion evaluation
    - Document counterexample: "FDDB102 diagnostic present for CapDef vs PlatformRoleCap pair despite non-tautological exclusion resolution"
  - **EXPECTED OUTCOME**: Tests FAIL on unfixed code (this is correct — it proves the bugs exist)
  - Mark task complete when tests are written, run, and failures are documented
  - _Requirements: 1.1, 1.2, 1.3, 1.4, 2.1, 2.2, 2.3, 2.4_

- [x] 2. Write preservation property tests (BEFORE implementing fix)
  - **Property 2: Preservation** - Non-Subsumptive Prefixes, Non-Complex ExactMatch, and Same-Score FDDB102
  - **IMPORTANT**: Follow observation-first methodology — observe behavior on UNFIXED code first, then encode
  - **Test file**: `Oproto.FluentDynamoDb.SourceGenerator.UnitTests/Analysis/PrefixSubsumptionPreservationTests.cs`
  - **Trait**: `[Trait("Category", "Preservation")]`, `[Trait("Feature", "compound-discrimination-prefix-subsumption")]`
  - Uses FsCheck.Xunit `[Property]` attribute with generators for random prefix strings and pattern configurations
  - **Preservation 1 — Non-Subsumptive Prefix Pairs (Req 3.1, 3.7)**:
    - Observe: `CompoundPromotionPass.Analyze` with non-subsumptive pairs (e.g., `"PLATFORM#"` vs `"TENANT#"`) assigns dual positive `StartsWith` constraints with no exclusion guards on unfixed code
    - Write property-based test: generate random prefix string pairs where neither is a prefix of the other and they are not identical
    - Assert no exclusion guard is added (no `AdditionalExclusions`, no exclusion `CompoundConstraint`)
    - Assert both entities receive positive `StartsWith` compound constraints
    - Verify test PASSES on unfixed code
  - **Preservation 2 — One-Null-One-NonNull Cross-Key (Req 3.2)**:
    - Observe: `CompoundPromotionPass.Analyze` with one null and one non-null cross-key pattern produces positive + exclusion constraints on unfixed code
    - Write property-based test: generate random non-null prefix patterns paired with null
    - Assert non-null entity gets positive constraint, null entity gets exclusion guard
    - Verify test PASSES on unfixed code
  - **Preservation 3 — ExactValueMatchesPattern Non-Complex Strategies (Req 3.5)**:
    - Observe: `ExactValueMatchesPattern` returns expected results for `StartsWith`, `EndsWith`, `Contains` strategies on unfixed code
    - Write property-based test: generate random exact values and `StartsWith`/`EndsWith`/`Contains` patterns
    - Assert `PatternsOverlap` returns the same result as the structural matching logic for these strategies
    - Verify test PASSES on unfixed code
  - **Preservation 4 — Same-Score Auto-Derived FDDB102 (Req 3.4, 3.6)**:
    - Observe: `PatternOverlapAnalyzer.Analyze` emits FDDB102 for same-score auto-derived pairs on unfixed code
    - Write property-based test: create same-score auto-derived entity pairs with overlapping patterns
    - Assert FDDB102 diagnostic is present in results
    - Verify test PASSES on unfixed code
  - **Preservation 5 — Internal-Segment Fallback (Req 3.8)**:
    - Observe: `CompoundPromotionPass.Analyze` resolves entities with Complex PK patterns sharing the same reduced prefix via internal-segment positional constraints on unfixed code
    - Write example-based test: two entities with PK `TENANT#*#ROLE#*` and `TENANT#*#DEPT#*` (both reduce to `TENANT#*`) — verify internal-segment resolution still works
    - Verify test PASSES on unfixed code
  - **EXPECTED OUTCOME**: All tests PASS on unfixed code (this confirms baseline behavior to preserve)
  - Mark task complete when tests are written, run, and passing on unfixed code
  - _Requirements: 3.1, 3.2, 3.4, 3.5, 3.6, 3.7, 3.8_

- [x] 3. Fix for prefix subsumption, conservative ExactMatch vs Complex, and spurious FDDB102

  - [x] 3.1 Add post-assignment prefix subsumption detection and exclusion guard in `CompoundPromotionPass.Analyze`
    - After both entities receive positive `StartsWith` compound constraints via `AssignPositiveConstraint` in the `AreDisambiguable` → both-non-null branch, add a check:
    - Retrieve both entities' `CompoundConstraint` objects after assignment
    - If both have `Strategy == StartsWith` and both are positive (`IsExclusion == false`):
      - Compare `LiteralText` values: if `litA != litB` and `litB.StartsWith(litA)`, then `litA` is the shorter prefix — apply exclusion for `litB` to `entityA`
      - If `litA != litB` and `litA.StartsWith(litB)`, then `litB` is the shorter prefix — apply exclusion for `litA` to `entityB`
    - Use a new `ApplyPrefixSubsumptionExclusion` helper method (task 3.2)
    - The shorter-prefix entity KEEPS its positive constraint AND receives an additional exclusion in `AdditionalExclusions`
    - _Bug_Condition: `isBugCondition_Bug1(entityA, entityB)` — both positive StartsWith, one LiteralText is prefix of other, not identical_
    - _Expected_Behavior: Shorter-prefix entity gets exclusion guard for longer prefix in `AdditionalExclusions`_
    - _Preservation: Non-subsumptive prefix pairs unchanged (Req 3.1), one-null pairs unchanged (Req 3.2), identical prefix pairs fall through to internal-segment path (Req 3.8)_
    - _Requirements: 2.1.1, 2.1.2, 2.1.3, 2.1.4, 2.2.1, 2.2.2, 2.2.3_

  - [x] 3.2 Add `ApplyPrefixSubsumptionExclusion` helper method in `CompoundPromotionPass`
    - Create private static method `ApplyPrefixSubsumptionExclusion(EntityModel shorterPrefixEntity, string crossKeyAttrName, string longerPrefixLiteralText, string sourceEntityName)`
    - Creates a `CompoundConstraint` with `IsExclusion = true`, `Strategy = StartsWith`, `LiteralText = longerPrefixLiteralText`, `Pattern = longerPrefixLiteralText + "*"`, `ExclusionSourceEntity = sourceEntityName`
    - Attaches the exclusion to the existing positive constraint's `AdditionalExclusions` list (entity already has a positive constraint from `AssignPositiveConstraint`)
    - If `AdditionalExclusions` is null, initialize it first
    - _Requirements: 2.1.2, 2.1.3_

  - [x] 3.3 Replace unconditional `return true` for Complex in `PatternOverlapAnalyzer.ExactValueMatchesPattern`
    - In the `DiscriminatorStrategy.Complex` case of the switch expression in `ExactValueMatchesPattern`:
    - Split `patternConfig.Pattern` on `'*'`
    - Find the first non-empty segment (leading prefix)
    - If a non-empty leading prefix exists AND the exact value does NOT start with it (ordinal comparison), return `false`
    - Otherwise return `true` (conservative for remaining structural ambiguity and for patterns starting with `*`)
    - _Bug_Condition: `isBugCondition_Bug2(exactValue, patternConfig)` — Complex pattern with non-empty leading prefix, exact value doesn't start with it_
    - _Expected_Behavior: Returns `false` when exact value cannot structurally match the Complex pattern's leading prefix_
    - _Preservation: Patterns starting with `*` still return `true`; ExactMatch values that DO start with the leading prefix still return `true`; StartsWith/EndsWith/Contains strategies unchanged (Req 3.5)_
    - _Requirements: 2.3.1, 2.3.2, 2.3.3, 2.3.4_

  - [x] 3.4 Defer FDDB102 emission in `PatternOverlapAnalyzer.Analyze` different-score branch until after tautological check
    - In the `else` (different-score) branch of `Analyze`, restructure the FDDB102 emission logic:
    - Move the `if (configA.IsAutoDerived && configB.IsAutoDerived)` FDDB102 block from BEFORE `CreateExclusionPattern` to INSIDE the `IsTautologicalExclusion` branch
    - If `IsTautologicalExclusion` returns `true`: emit FDDB102 (for auto-derived pairs) AND DISC006 as before
    - If `IsTautologicalExclusion` returns `false`: do NOT emit FDDB102, add to `OverlappingPatterns`, emit DISC005 as before
    - This ensures FDDB102 is only emitted for different-score auto-derived pairs when the exclusion IS tautological (unresolvable)
    - _Bug_Condition: `isBugCondition_Bug3(configA, configB, exclusion)` — different scores, both auto-derived, non-tautological exclusion_
    - _Expected_Behavior: FDDB102 NOT emitted for non-tautological exclusions; still emitted for tautological exclusions (Req 2.5)_
    - _Preservation: Same-score FDDB102 unchanged (Req 3.6); DISC005/DISC006 logic unchanged; `OverlappingPatterns` population unchanged_
    - _Requirements: 2.4.1, 2.4.2, 2.4.3, 2.4.4, 2.5.1, 2.5.2_

  - [x] 3.5 Verify bug condition exploration tests now pass
    - **Property 1: Expected Behavior** - Prefix Subsumption, ExactMatch vs Complex, and Spurious FDDB102
    - **IMPORTANT**: Re-run the SAME tests from task 1 — do NOT write new tests
    - The tests from task 1 encode the expected behavior
    - When these tests pass, it confirms the expected behavior is satisfied for all three bugs
    - Run bug condition exploration tests from step 1
    - **EXPECTED OUTCOME**: Tests PASS (confirms bugs are fixed)
    - _Requirements: 2.1, 2.2, 2.3, 2.4_

  - [x] 3.6 Verify preservation tests still pass
    - **Property 2: Preservation** - Non-Subsumptive Prefixes, Non-Complex ExactMatch, and Same-Score FDDB102
    - **IMPORTANT**: Re-run the SAME tests from task 2 — do NOT write new tests
    - Run preservation property tests from step 2
    - **EXPECTED OUTCOME**: Tests PASS (confirms no regressions)
    - Confirm all preservation tests still pass after fix (no regressions to non-subsumptive pairs, non-Complex strategies, same-score diagnostics, or internal-segment resolution)

- [x] 4. Write additional unit and integration tests for edge cases
  - **Test file**: `Oproto.FluentDynamoDb.SourceGenerator.UnitTests/Analysis/PrefixSubsumptionUnitTests.cs` (unit) and `Oproto.FluentDynamoDb.SourceGenerator.UnitTests/Integration/AuthorizationTablePrefixSubsumptionIntegrationTests.cs` (integration)
  - **Unit Tests**:
    - Test `CompoundPromotionPass.Analyze` with subsumptive prefix pair (`"TENANT#"` vs `"TENANT#PLATFORM#ROLE#"`) — verify exclusion guard on shorter-prefix entity's `AdditionalExclusions`
    - Test `CompoundPromotionPass.Analyze` with reverse subsumptive pair (`"SERVICE#ADMIN#"` vs `"SERVICE#"`) — verify exclusion guard on the `"SERVICE#"` entity
    - Test `CompoundPromotionPass.Analyze` with identical prefix pair (`"TENANT#"` vs `"TENANT#"`) — verify no exclusion guard, falls through to internal-segment path
    - Test `ExactValueMatchesPattern("SETTINGS", Complex("CAP#*#*"))` returns `false`
    - Test `ExactValueMatchesPattern("CAP#read", Complex("CAP#*#*"))` returns `true`
    - Test `ExactValueMatchesPattern("ANYTHING", Complex("*#DATA#*"))` returns `true` (pattern starts with `*`)
    - Test `ExactValueMatchesPattern("", Complex("CAP#*#*"))` returns `false` (empty exact value)
    - Test `Analyze` different-score pair resolved by non-tautological exclusion — no FDDB102
    - Test `Analyze` different-score pair with tautological exclusion — FDDB102 present
    - Test `Analyze` same-score auto-derived pair — FDDB102 still present
  - **Integration Test — Full AuthorizationTable Pipeline**:
    - Define all four entities: CapabilityDefinitionEntity (PK `SERVICE#*`, SK `CAP#*`, score 1), PlatformRoleCapabilityEntity (PK `TENANT#PLATFORM#ROLE#*`, SK `CAP#*#*`, score 2), RoleCapabilityEntity (PK `TENANT#*#ROLE#*` → reduced `TENANT#*`, SK `CAP#*#*`, score 2), TenantSettingsEntity (PK `TENANT#*`, SK `SETTINGS` ExactMatch, score ∞)
    - Run `PatternOverlapAnalyzer.Analyze` then `CompoundPromotionPass.Analyze`
    - Verify PlatformRoleCapabilityEntity and RoleCapabilityEntity get compound constraints with mutual exclusivity (RoleCapabilityEntity gets exclusion guard for `"TENANT#PLATFORM#ROLE#"`)
    - Verify no FDDB102 for CapDef vs PlatformRoleCap and CapDef vs RoleCap pairs (non-tautological exclusions resolved by Bug 3 fix)
    - Verify no FDDB102 for PlatformRoleCap vs TenantSettings and RoleCap vs TenantSettings (structurally non-overlapping after Bug 2 fix — `"SETTINGS"` doesn't start with `"CAP#"`)
    - Verify DISC005 informational diagnostics present for resolved different-score pairs
    - Verify total diagnostic count matches expected (no spurious warnings)
  - _Requirements: 2.1, 2.2, 2.3, 2.4, 2.5, 3.1, 3.2, 3.3, 3.4, 3.5, 3.6, 3.7, 3.8_

- [x] 5. Checkpoint — Ensure all tests pass
  - Run `dotnet test` on the full test suite
  - Ensure all new tests pass (bug condition, preservation, unit, integration)
  - Ensure all existing tests pass (no regressions)
  - Ensure `dotnet build` succeeds with no new warnings from the source generator changes
  - Ask the user if questions arise

## Task Dependency Graph

```json
{
  "waves": [
    { "wave": 1, "tasks": ["1", "2"] },
    { "wave": 2, "tasks": ["3.1", "3.2", "3.3", "3.4"] },
    { "wave": 3, "tasks": ["3.5", "3.6"] },
    { "wave": 4, "tasks": ["4"] },
    { "wave": 5, "tasks": ["5"] }
  ]
}
```

## Notes

- Tasks 1 and 2 can be executed in parallel since they are independent test-writing tasks
- Tasks 3.1–3.4 (implementation sub-tasks) can be executed in any order, but 3.2 must be completed before 3.1 since 3.1 depends on the helper method
- The exploration tests (task 1) are expected to FAIL on unfixed code — this is intentional and confirms the bugs exist
- The preservation tests (task 2) are expected to PASS on unfixed code — this confirms baseline behavior
- After the fix (tasks 3.1–3.4), both exploration and preservation tests should PASS
- FsCheck.Xunit is used for property-based preservation tests; ensure the package is referenced in the test project
