# Implementation Plan

- [x] 1. Write bug condition exploration test
  - **Property 1: Bug Condition** — Complex Pattern Prefix Extraction
  - **CRITICAL**: This test MUST FAIL on unfixed code — failure confirms the bug exists
  - **DO NOT attempt to fix the test or the code when it fails**
  - **NOTE**: This test encodes the expected behavior — it will validate the fix when it passes after implementation
  - **GOAL**: Surface counterexamples that demonstrate Complex patterns with non-empty leading prefixes are incorrectly treated as null
  - **Scoped PBT Approach**: Write a property-based test in a new file `CompoundPromotionPassComplexPatternTests.cs` in `Oproto.FluentDynamoDb.SourceGenerator.UnitTests/Analysis/`
  - Use `[Property(MaxTest = 100)]` with FsCheck, following the existing patterns in `CompoundPromotionPassPropertyTests.cs`
  - Add `[Trait("Feature", "compound-discrimination-complex-pattern-fix")]` and `[Trait("Category", "Property")]`
  - **Generator**: Generate entity pairs where both entities share a same-score SK discriminator overlap. One or both entities have Complex cross-key PK patterns (e.g., `PREFIX_A#*#SEGMENT#*`) with different non-empty leading prefixes. Use `GenPrefix.Two().Where(p => p.Item1 != p.Item2)` to ensure different prefixes.
  - **Bug condition** from design: `DeterminePatternStrategy(pattern) = Complex AND pattern.IndexOf('*') > 0`
  - **Assertion**: The pair IS in `result.ResolvedPairs` AND both entities receive positive `CompoundConstraint` (IsExclusion = false) with `Strategy = StartsWith` and `LiteralText` equal to the extracted prefix
  - Also test: Complex vs non-Complex (e.g., `TENANT#*#ROLE#*` vs `SERVICE#*`) with different prefixes → both get positive constraints
  - Run test on UNFIXED code
  - **EXPECTED OUTCOME**: Test FAILS — Complex patterns are treated as null, so pairs are either not resolved (both Complex) or resolved asymmetrically (Complex gets exclusion instead of positive)
  - Document counterexamples found to confirm root cause
  - Mark task complete when test is written, run, and failure is documented
  - _Requirements: 1.1, 1.2, 1.3, 2.1, 2.2, 2.3_

- [x] 2. Write preservation property tests (BEFORE implementing fix)
  - **Property 2: Preservation** — Non-Complex and Empty-Prefix Behavior
  - **IMPORTANT**: Follow observation-first methodology
  - Write a preservation property-based test in the same `CompoundPromotionPassComplexPatternTests.cs` file
  - **Test 2a — Non-Complex pattern preservation**: Generate entity pairs where BOTH cross-key patterns are non-Complex (StartsWith, ExactMatch, EndsWith, Contains). Verify that `CompoundPromotionPass.Analyze` produces the exact same `ResolvedPairs`, `CompoundConstraint` assignments (IsExclusion, Pattern, Strategy, LiteralText), and diagnostic counts as observed on the UNFIXED code. This is already covered extensively by the existing `CompoundPromotionPassPropertyTests.cs` (Properties 1-8), so this test should focus on the boundary: patterns that are borderline Complex but aren't (e.g., single-wildcard patterns like `TENANT#*`, `*#SUFFIX`, `*#MIDDLE#*`).
  - **Test 2b — Empty-prefix Complex pattern preservation**: Generate entity pairs where one or both entities have Complex patterns that START with `*` (e.g., `*#ROLE#*#TENANT#*`). Verify these are still treated as null — pairs where both are empty-prefix Complex should NOT be resolved; pairs where one is empty-prefix Complex and the other has a valid non-Complex pattern should be resolved via exclusion (same as current behavior).
  - **Test 2c — Same-prefix Complex pattern preservation**: Generate entity pairs where both entities have Complex patterns with the SAME leading prefix (e.g., `TENANT#*#ROLE#*` and `TENANT#*#DEPT#*`). After prefix extraction, both reduce to `TENANT#*` → identical effective patterns → `AreDisambiguable` returns false → pair NOT resolved. Verify this.
  - Run all existing `CompoundPromotionPassPropertyTests` to establish baseline: `dotnet test --filter "Feature=compound-key-discrimination&Category=Property"` — all should pass on UNFIXED code
  - Run all existing `CompoundPromotionPassTests` unit tests: `dotnet test --filter "FullyQualifiedName~CompoundPromotionPassTests"` — all should pass on UNFIXED code
  - Run new preservation tests on UNFIXED code
  - **EXPECTED OUTCOME**: All preservation tests PASS (confirms baseline behavior to preserve)
  - Mark task complete when tests are written, run, and passing on unfixed code
  - _Requirements: 3.1, 3.2, 3.3, 3.4, 3.5, 3.6_

- [x] 3. Fix for Complex pattern prefix extraction in CompoundPromotionPass

  - [x] 3.1 Implement the fix in GetEffectiveCrossKeyPattern
    - **File**: `Oproto.FluentDynamoDb.SourceGenerator/Analysis/CompoundPromotionPass.cs`
    - **Method**: `GetEffectiveCrossKeyPattern` (private static, ~line 249)
    - Replace the blanket `return null` for Complex patterns with prefix extraction logic:
      ```csharp
      if (strategy == DiscriminatorStrategy.Complex)
      {
          var starIndex = pattern.IndexOf('*');
          if (starIndex > 0)
          {
              var prefix = pattern.Substring(0, starIndex);
              return prefix + "*";
          }
          return null;
      }
      ```
    - Update the XML doc comment to document the new prefix extraction behavior instead of referencing "Requirement 7.6: Treat Complex-strategy patterns as null"
    - _Bug_Condition: isBugCondition(pattern) where DeterminePatternStrategy(pattern) = Complex AND pattern.IndexOf('*') > 0_
    - _Expected_Behavior: GetEffectiveCrossKeyPattern returns prefix + "*" for Complex patterns with non-empty leading prefix_
    - _Preservation: Non-Complex patterns, null/empty patterns, and Complex patterns starting with '*' continue to return the same values as before_
    - _Requirements: 2.1, 2.2, 2.3, 2.4, 3.1, 3.2, 3.3, 3.4, 3.5, 3.6_

  - [x] 3.2 Update existing test that asserts Complex patterns are treated as null
    - **File**: `Oproto.FluentDynamoDb.SourceGenerator.UnitTests/Analysis/CompoundPromotionPassTests.cs`
    - **Test**: `Analyze_ComplexCrossKeyPattern_TreatedAsNull` (Test 6)
    - This test currently creates EntityA with PK=`REGION#*#TENANT#*` (Complex) and EntityB with PK=`PLATFORM#*` (StartsWith), then asserts EntityA gets an exclusion guard
    - After the fix, EntityA's Complex pattern reduces to `REGION#*` (StartsWith), which differs from EntityB's `PLATFORM#*` → both get positive constraints
    - Update test name to `Analyze_ComplexCrossKeyPattern_ReducedToPrefix_BothGetPositiveConstraint` (or similar)
    - Update assertions: EntityA gets positive CompoundConstraint with Pattern=`REGION#*`, Strategy=StartsWith, LiteralText=`REGION#`; EntityB gets positive CompoundConstraint with Pattern=`PLATFORM#*`, Strategy=StartsWith, LiteralText=`PLATFORM#`
    - _Requirements: 2.1, 2.3_

  - [x] 3.3 Update existing property-based test that asserts Complex patterns are treated as null
    - **File**: `Oproto.FluentDynamoDb.SourceGenerator.UnitTests/Analysis/CompoundPromotionPassPropertyTests.cs`
    - **Test**: `ComplexCrossKeyPatternsTreatedAsNull_NotDisambiguable` — this test generates pairs where BOTH entities have Complex patterns and asserts they are NOT disambiguable (both treated as null → both null → not disambiguable)
    - After the fix, two Complex patterns with DIFFERENT leading prefixes WILL be disambiguable (both reduce to different StartsWith patterns). Only Complex patterns with the SAME prefix or both starting with `*` remain not disambiguable.
    - Update the generator `GenSameScoreOverlapPairWithBothComplexCrossKey` to ensure both Complex patterns have the SAME leading prefix (so they reduce to identical effective patterns and remain not disambiguable). Or split into two tests: one for same-prefix (not disambiguable) and one for different-prefix (now disambiguable).
    - **Test**: `OneComplexOneValid_Disambiguable` — this test generates pairs where one entity has Complex (treated as null) and the other has valid non-Complex. After the fix, if the Complex pattern has a non-empty prefix, it's no longer null — it's a valid StartsWith pattern. If the prefixes differ, both get positive constraints (not exclusion). Update assertions accordingly OR update the generator to produce Complex patterns that start with `*` (empty prefix) to preserve the original test intent.
    - _Requirements: 2.1, 2.2, 3.6_

  - [x] 3.4 Verify bug condition exploration test now passes
    - **Property 1: Expected Behavior** — Complex Pattern Prefix Extraction
    - **IMPORTANT**: Re-run the SAME test from task 1 — do NOT write a new test
    - The test from task 1 encodes the expected behavior (Complex patterns with non-empty prefixes produce positive constraints)
    - When this test passes, it confirms the expected behavior is satisfied
    - Run: `dotnet test --filter "Feature=compound-discrimination-complex-pattern-fix&Category=Property"`
    - **EXPECTED OUTCOME**: Test PASSES (confirms bug is fixed)
    - _Requirements: 2.1, 2.2, 2.3_

  - [x] 3.5 Verify preservation tests still pass
    - **Property 2: Preservation** — Non-Complex and Empty-Prefix Behavior
    - **IMPORTANT**: Re-run the SAME tests from task 2 — do NOT write new tests
    - Run new preservation tests from task 2: `dotnet test --filter "Feature=compound-discrimination-complex-pattern-fix"`
    - Run ALL existing compound-key-discrimination property tests: `dotnet test --filter "Feature=compound-key-discrimination&Category=Property"`
    - Run ALL existing compound-key-discrimination unit tests: `dotnet test --filter "FullyQualifiedName~CompoundPromotionPassTests"`
    - **EXPECTED OUTCOME**: All tests PASS (confirms no regressions)
    - Confirm all tests still pass after fix (no regressions)

- [x] 4. Checkpoint — Ensure all tests pass
  - Run the full test suite: `dotnet test` in the `Oproto.FluentDynamoDb.SourceGenerator.UnitTests/` project
  - Verify: all new tests from tasks 1-2 pass, all updated tests from tasks 3.2-3.3 pass, all existing tests pass
  - Ensure no FDDB102 warnings are emitted for entity pairs that are now resolvable via prefix extraction
  - Ensure FDDB104 info diagnostics are emitted for newly resolved pairs
  - Ask the user if questions arise
