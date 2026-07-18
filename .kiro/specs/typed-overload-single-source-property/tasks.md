# Implementation Plan

## Overview

Fix the `>= 2` restriction in `QualifiesForTypedOverload` and `GetTypedOverloadParameters` that incorrectly prevents typed overload generation for computed keys with a single non-string source property. The fix relies on the existing `WouldBeAmbiguous` method to reject truly ambiguous cases.

## Tasks

- [x] 1. Write bug condition exploration test
  - **Property 1: Bug Condition** - Single Source Computed Key Typed Overload Eligibility
  - **CRITICAL**: This test MUST FAIL on unfixed code - failure confirms the bug exists
  - **DO NOT attempt to fix the test or the code when it fails**
  - **NOTE**: This test encodes the expected behavior - it will validate the fix when it passes after implementation
  - **GOAL**: Surface counterexamples that demonstrate the bug exists
  - **Scoped PBT Approach**: Scope the property to entities with exactly one non-string source property on a computed key (e.g., single `DateTime` SK source, single `int` PK source, single `Guid` SK source)
  - Write a property-based test (FsCheck) in `ComputedOverloadEligibilityPropertyTests.cs` that generates `EntityModel` instances satisfying `isBugCondition`: at least one key is computed with exactly one source property
  - Generate entities with single non-string source properties (DateTime, int, Guid, long, decimal, DateOnly) on PK or SK
  - Assert `QualifiesForTypedOverload(entity)` returns `true` for all generated entities
  - Assert `GetTypedOverloadParameters(entity)` resolves the source property to its declared type (not fallback "string")
  - For single non-string sources, also assert `WouldBeAmbiguous(entity)` returns `false` (signature differs from standard overload)
  - Run test on UNFIXED code
  - **EXPECTED OUTCOME**: Test FAILS because `QualifiesForTypedOverload` returns `false` due to `SourceProperties.Length >= 2` gate rejecting single-source entities
  - Document counterexamples found (e.g., "Entity with single DateTime SK source: QualifiesForTypedOverload returns false instead of true")
  - Mark task complete when test is written, run, and failure is documented
  - _Requirements: 1.1, 2.1, 2.3_

- [x] 2. Write preservation property tests (BEFORE implementing fix)
  - **Property 2: Preservation** - Multi-Source and Non-Computed Entity Behavior Unchanged
  - **IMPORTANT**: Follow observation-first methodology
  - Observe: `QualifiesForTypedOverload` returns `true` for entities with 2+ source computed keys on UNFIXED code
  - Observe: `QualifiesForTypedOverload` returns `false` for entities with no computed keys on UNFIXED code
  - Observe: `WouldBeAmbiguous` returns `true` for entities with 2+ string-only sources on UNFIXED code
  - Observe: `GetTypedOverloadParameters` resolves 2+ source properties correctly on UNFIXED code
  - Observe: `QualifiesForKeyInputMode` returns expected values for multi-source and non-computed entities on UNFIXED code
  - Write property-based tests (FsCheck) in `ComputedOverloadEligibilityPropertyTests.cs` covering:
    - For all entities where `NOT isBugCondition(X)` (0 or 2+ sources, or no computed keys): assert `QualifiesForTypedOverload` returns consistent result (true for 2+ sources, false for 0/no-computed)
    - For all entities with 2+ source computed keys: assert `WouldBeAmbiguous` returns consistent result based on type comparison
    - For all entities with 2+ source computed keys: assert `GetTypedOverloadParameters` resolves all source properties correctly
    - For all non-computed entities: assert `QualifiesForKeyInputMode` evaluates prefix eligibility unchanged
  - Verify tests PASS on UNFIXED code
  - **EXPECTED OUTCOME**: Tests PASS (this confirms baseline behavior to preserve)
  - Mark task complete when tests are written, run, and passing on unfixed code
  - _Requirements: 3.1, 3.2, 3.3, 3.4, 3.5_

- [x] 3. Fix for single source computed key typed overload eligibility

  - [x] 3.1 Remove `>= 2` gate from `QualifiesForTypedOverload` in `ComputedOverloadEligibility.cs`
    - Change `pk?.IsComputed == true && pk.ComputedKey!.SourceProperties.Length >= 2` to `pk?.IsComputed == true`
    - Change `sk?.IsComputed == true && sk.ComputedKey!.SourceProperties.Length >= 2` to `sk?.IsComputed == true`
    - Update XML doc comment: change "ComputedKey.SourceProperties.Length >= 2" to "IsComputed == true"
    - _Bug_Condition: isBugCondition(input) where input has at least one computed key with SourceProperties.Length = 1_
    - _Expected_Behavior: QualifiesForTypedOverload returns true for any entity with a computed key (regardless of source count)_
    - _Preservation: Entities with 2+ sources, non-computed entities, and WouldBeAmbiguous logic unchanged_
    - _Requirements: 1.1, 2.1, 2.2, 3.1, 3.2, 3.3_

  - [x] 3.2 Remove `>= 2` gate from `GetTypedOverloadParameters` in `OverloadParameterResolver.cs`
    - Change `if (pk?.IsComputed == true && pk.ComputedKey!.SourceProperties.Length >= 2)` to `if (pk?.IsComputed == true)`
    - Change `if (sk?.IsComputed == true && sk.ComputedKey!.SourceProperties.Length >= 2)` to `if (sk?.IsComputed == true)`
    - Update XML doc comment: change "For computed keys with 2+ source properties" to "For computed keys"
    - _Bug_Condition: isBugCondition(input) where GetTypedOverloadParameters falls through to plain string for single-source keys_
    - _Expected_Behavior: GetTypedOverloadParameters resolves source property types for any computed key regardless of count_
    - _Preservation: Multi-source parameter resolution unchanged_
    - _Requirements: 1.3, 2.3, 3.1_

  - [x] 3.3 Update test generators in `ComputedOverloadEligibilityPropertyTests.cs`
    - Update `CreateNonComputedEntityGenerator` scenarios: single-source computed keys now QUALIFY, so remove them from the "non-qualifying" generator (scenario 1 and 3 with single-source computed keys)
    - Update `CreateNonComputedStringKeyEntityGenerator`: when `hasSingleSourceComputed = true`, the entity now qualifies for typed overload (ambiguous since source is string), so this scenario should be moved to a separate ambiguity test or excluded from the non-qualifying generator
    - Ensure the `NonComputedEntities_DoNotQualifyForTypedOverloads` test only generates entities that truly don't qualify: entities with no computed keys at all, or computed keys with 0 source properties
    - _Requirements: 2.1, 2.2, 3.3_

  - [x] 3.4 Update `BuildExpectedParams` helper and add single-source generators in `ComputedOverloadPropertyTests.cs`
    - Update `BuildExpectedParams`: change `pk?.IsComputed == true && pk.ComputedKey!.SourceProperties.Length >= 2` to `pk?.IsComputed == true` (and same for SK)
    - Add new generator `CreateSingleSourceComputedSkGenerator()` that produces entities with simple PK + computed SK with exactly 1 non-string source property
    - Add new generator `CreateSingleSourceComputedPkGenerator()` that produces entities with computed PK with exactly 1 non-string source property + simple SK
    - Add new property test `TypedOverload_GeneratesCorrectParameters_ForSingleSourceComputedSk()` using the single-source SK generator
    - Add new property test `TypedOverload_GeneratesCorrectParameters_ForSingleSourceComputedPk()` using the single-source PK generator
    - _Requirements: 2.1, 2.3, 3.1_

  - [x] 3.5 Verify bug condition exploration test now passes
    - **Property 1: Expected Behavior** - Single Source Computed Key Typed Overload Eligibility
    - **IMPORTANT**: Re-run the SAME test from task 1 - do NOT write a new test
    - The test from task 1 encodes the expected behavior
    - When this test passes, it confirms the expected behavior is satisfied
    - Run bug condition exploration test from step 1
    - **EXPECTED OUTCOME**: Test PASSES (confirms bug is fixed — `QualifiesForTypedOverload` returns `true` for single-source computed keys)
    - _Requirements: 2.1, 2.3_

  - [x] 3.6 Verify preservation tests still pass
    - **Property 2: Preservation** - Multi-Source and Non-Computed Entity Behavior Unchanged
    - **IMPORTANT**: Re-run the SAME tests from task 2 - do NOT write new tests
    - Run preservation property tests from step 2
    - **EXPECTED OUTCOME**: Tests PASS (confirms no regressions for multi-source and non-computed entities)
    - Confirm all tests still pass after fix (no regressions)

- [x] 4. Checkpoint - Ensure all tests pass
  - Run `dotnet test` across the full test suite
  - Ensure all property-based tests pass (both new and existing)
  - Ensure no regressions in other test classes
  - Ask the user if questions arise

## Task Dependency Graph

```json
{
  "waves": [
    { "id": 0, "tasks": ["1", "2"] },
    { "id": 1, "tasks": ["3.1", "3.2"] },
    { "id": 2, "tasks": ["3.3", "3.4"] },
    { "id": 3, "tasks": ["3.5", "3.6"] },
    { "id": 4, "tasks": ["4"] }
  ]
}
```

## Notes

- Tasks 1 and 2 are independent and can be done in parallel (both run on UNFIXED code)
- Tasks 3.1 and 3.2 are the core implementation changes (can be done in parallel)
- Tasks 3.3 and 3.4 update existing tests to match new behavior (depend on 3.1/3.2)
- Tasks 3.5 and 3.6 are verification steps (re-run tests from tasks 1 and 2)
- The project uses FsCheck with xUnit (`[Property]` attribute) for property-based testing
- Run `dotnet build-server shutdown` before testing if source generator changes are cached
