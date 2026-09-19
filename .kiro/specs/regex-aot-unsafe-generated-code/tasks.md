# Implementation Plan

## Overview

Replace all five `Regex.IsMatch` emission sites in `MapperGenerator.cs` with AOT-safe `string.Split` + segment comparison logic for `[RelatedEntity]` wildcard pattern matching. The fix follows the exploratory bugfix workflow: write bug condition and preservation tests first, implement the fix, then verify all tests pass.

## Tasks

- [x] 1. Write bug condition exploration test
  - **Property 1: Bug Condition** - Generated Code Contains AOT-Unsafe Regex.IsMatch
  - **CRITICAL**: This test MUST FAIL on unfixed code — failure confirms the bug exists
  - **DO NOT attempt to fix the test or the code when it fails**
  - **NOTE**: This test encodes the expected behavior — it will validate the fix when it passes after implementation
  - **GOAL**: Surface counterexamples demonstrating that generated code contains `System.Text.RegularExpressions.Regex.IsMatch`
  - **Scoped PBT Approach**: Run the source generator on entities with `[RelatedEntity]` wildcard patterns and assert the generated code does NOT contain `Regex.IsMatch` (the expected AOT-safe behavior)
  - Create test file `Oproto.FluentDynamoDb.SourceGenerator.UnitTests/Generators/RegexAotUnsafeBugExplorationTests.cs`
  - Use FsCheck property-based testing with generated entity configurations containing `[RelatedEntity]` wildcard patterns (e.g., `"INVOICE#*#LINE#*"`, `"audit#*"`, `"TYPE_*_SUB_*"`)
  - For each generated entity, run the source generator and inspect the generated `.g.cs` output
  - Assert: generated code does NOT contain `System.Text.RegularExpressions.Regex.IsMatch` (expected behavior per design)
  - Assert: generated code does NOT contain `System.Text.RegularExpressions.Regex` namespace references in pattern matching
  - Use the same test infrastructure as `CompositeEntityPreservationPropertyTests` (CSharp compilation + `DynamoDbSourceGenerator` + `DynamicCompilationHelper.GetFluentDynamoDbReferences()`)
  - Run test on UNFIXED code
  - **EXPECTED OUTCOME**: Test FAILS because generated code currently contains `Regex.IsMatch` at all 5 emission sites — this proves the bug exists
  - Document counterexamples: "Generated code for entity with `[RelatedEntity("INVOICE#*#LINE#*")]` contains `System.Text.RegularExpressions.Regex.IsMatch(sortKey, @"^INVOICE\#[^#]*\#LINE\#[^#]*$")`"
  - Mark task complete when test is written, run, and failure is documented
  - _Requirements: 1.1, 1.2, 1.3, 1.4_

- [x] 2. Write preservation property tests (BEFORE implementing fix)
  - **Property 2: Preservation** - Pattern Matching Equivalence Between Regex and String-Split Approaches
  - **IMPORTANT**: Follow observation-first methodology — implement `MatchesWildcardPattern` first, then verify equivalence
  - Step 1: Add new `internal static bool MatchesWildcardPattern(string sortKey, string pattern)` method to `MapperGenerator` in `Oproto.FluentDynamoDb.SourceGenerator/Generators/MapperGenerator.cs`
    - This method performs the string-split logic at runtime (same logic that `EmitWildcardPatternMatch` will later emit)
    - Call `InferDelimiterFromPattern(pattern)` to get the delimiter
    - Split the pattern by delimiter to get expected segments
    - Split the sortKey by delimiter
    - Check segment count matches and all literal (non-`*`) segments are equal
    - This is a testable unit that does NOT touch emission sites
  - Step 2: Create test file `Oproto.FluentDynamoDb.SourceGenerator.UnitTests/Generators/RegexAotUnsafePreservationTests.cs`
  - Step 3: Write property-based tests comparing `MatchesWildcardPattern(sortKey, pattern)` against `Regex.IsMatch(sortKey, ConvertWildcardPatternToRegex(pattern))` for equivalence
  - Property test cases:
    - For random patterns with wildcards and matching sort keys, both methods return `true`
    - For random patterns with wildcards and non-matching sort keys, both methods return `false`
    - Edge case: empty segments (e.g., `"INVOICE##LINE#1"`) handled identically by both approaches
    - Edge case: special regex characters in literal segments (e.g., `"TYPE.NAME#*"`) — dot must match literally, not as any character
    - Edge case: custom delimiters (`_`, `:`, `|`) inferred correctly
    - Edge case: sort keys with more segments than pattern expects — both reject
    - Edge case: sort keys with fewer segments than pattern expects — both reject
  - Reuse FsCheck generators from existing `WildcardPatternMatchingPropertyTests` (Prefixes, Values, Segments arrays)
  - Verify tests PASS on UNFIXED code (both methods agree on all inputs)
  - **EXPECTED OUTCOME**: Tests PASS — confirms `MatchesWildcardPattern` is equivalent to the regex approach
  - Mark task complete when tests are written, run, and passing on unfixed code
  - _Requirements: 3.1, 3.2, 3.3, 3.4, 3.5_

- [x] 3. Replace Regex.IsMatch emission with AOT-safe string operations

  - [x] 3.1 Create `EmitWildcardPatternMatch` helper method in `MapperGenerator.cs`
    - Add new method: `private static void EmitWildcardPatternMatch(StringBuilder sb, string indent, string sortKeyVariable, string wildcardPattern, string resultVariable = null)`
    - Call `InferDelimiterFromPattern(wildcardPattern)` to determine the delimiter
    - Split the pattern by delimiter at compile time (in the generator) to get segments
    - Count total expected segments and identify which are literal vs wildcard (`*`)
    - Emit block-scoped code using `{ }` for collision-free variable reuse:
      ```csharp
      {
          var _seg = sortKey.Split('#');
          if (_seg.Length == 4 && _seg[0] == "INVOICE" && _seg[2] == "LINE")
          {
              // inner logic (varies by call site)
          }
      }
      ```
    - For negation sites (1 and 4): emit `isPrimaryEntity = false;` inside the if block
    - For match sites (2, 3, 5): emit the existing mapping logic inside the if block
    - If `resultVariable` is provided, emit `resultVariable = true;` for flexible call-site integration
    - _Bug_Condition: isBugCondition(input) where input.generatedCode.contains("System.Text.RegularExpressions.Regex.IsMatch")_
    - _Expected_Behavior: Emit string.Split + segment-count + literal-segment equality checks, zero Regex references_
    - _Preservation: All pattern matching results must be identical to regex approach_
    - _Requirements: 2.1, 2.4, 2.5_

  - [x] 3.2 Update emission site 1: `GenerateAsyncPrimaryEntityIdentification` (~line 2452)
    - Replace `ConvertWildcardPatternToRegex` + `Regex.IsMatch` emission with call to `EmitWildcardPatternMatch`
    - This is a negation site: the emitted code sets `isPrimaryEntity = false` when the sort key matches a related pattern
    - The loop over `relatedPatterns` already provides natural iteration; each pattern emits a block-scoped check
    - Preserve the existing comment: `// Exclude items matching related pattern: {pattern}`
    - _Requirements: 2.1, 2.2, 2.3, 3.3_

  - [x] 3.3 Update emission site 2: `GenerateRelatedEntityCollectionMappingAsync` (~line 2674)
    - Replace the wildcard branch's `ConvertWildcardPatternToRegex` + `Regex.IsMatch` with `EmitWildcardPatternMatch`
    - The exact-match `else` branch (non-wildcard patterns) MUST remain untouched
    - _Requirements: 2.1, 2.2, 2.6, 3.1, 3.4_

  - [x] 3.4 Update emission site 3: `GenerateRelatedEntitySingleMappingAsync` (~line 2788)
    - Replace the wildcard branch's `ConvertWildcardPatternToRegex` + `Regex.IsMatch` with `EmitWildcardPatternMatch`
    - The exact-match `else` branch (non-wildcard patterns) MUST remain untouched
    - _Requirements: 2.1, 2.2, 2.6, 3.1, 3.4_

  - [x] 3.5 Update emission site 4: `GeneratePrimaryEntityPatternMatching` (~line 4277)
    - Replace `ConvertWildcardPatternToRegex` + `Regex.IsMatch` emission with call to `EmitWildcardPatternMatch`
    - This is a negation site (sync version of site 1): emitted code sets `isPrimaryEntity = false`
    - Preserve the existing comment: `// Exclude items matching related pattern: {pattern}`
    - _Requirements: 2.1, 2.2, 2.3, 3.3, 3.5_

  - [x] 3.6 Update emission site 5: `GenerateSortKeyPatternMatching` (~line 5829)
    - Replace the wildcard branch's `ConvertWildcardPatternToRegex` + `Regex.IsMatch` with `EmitWildcardPatternMatch`
    - This is the shared helper used by sync collection and single entity mapping
    - The exact-match `else` branch MUST remain untouched
    - _Requirements: 2.1, 2.2, 2.6, 3.1, 3.4, 3.5_

  - [x] 3.7 Retain `ConvertWildcardPatternToRegex` as internal
    - Do NOT delete `ConvertWildcardPatternToRegex` — it is called by existing test projects (`WildcardPatternMatchingTests.cs`, `WildcardPatternMatchingPropertyTests.cs`, `PrimaryEntityIdentificationPropertyTests.cs`)
    - It is no longer called by any emission site after this fix
    - Optionally mark with `[Obsolete("No longer used in code generation. Retained for test backward compatibility.")]`
    - _Requirements: 3.4_

  - [x] 3.8 Update existing tests that check generated code for `Regex.IsMatch` string presence
    - `CompositeEntityPreservationPropertyTests.cs`: Update checks from `entityCode.Contains("Regex.IsMatch")` to `entityCode.Contains(".Split(")` (or equivalent string-split indicator)
    - `CompositeEntityAsyncMultiItemBugExplorationTests.cs`: Update checks from `entityCode.Contains("Regex.IsMatch")` to `entityCode.Contains(".Split(")` (or equivalent)
    - Update `MapperGeneratorTests.GenerateEntityImplementation_WithRelatedEntities_GeneratesRelationshipMapping` to verify generated code contains `Split` instead of `Regex.IsMatch`
    - _Requirements: 2.1, 3.1, 3.3_

  - [x] 3.9 Build and verify all projects compile
    - Run `dotnet build-server shutdown` to clear cached source generator
    - Run `dotnet build` on the full solution
    - Fix any compilation errors introduced by the changes
    - _Requirements: 2.2, 2.3_

  - [x] 3.10 Verify bug condition exploration test now passes
    - **Property 1: Expected Behavior** - Generated Code Is AOT-Safe (No Regex.IsMatch)
    - **IMPORTANT**: Re-run the SAME test from task 1 — do NOT write a new test
    - The test from task 1 encodes the expected behavior (no `Regex.IsMatch` in generated code)
    - When this test passes, it confirms the generated code is now AOT-safe
    - Run `RegexAotUnsafeBugExplorationTests` from task 1
    - **EXPECTED OUTCOME**: Test PASSES — confirms all 5 emission sites now emit `string.Split` instead of `Regex.IsMatch`
    - _Requirements: 2.1, 2.2, 2.3, 2.4, 2.5_

  - [x] 3.11 Verify preservation tests still pass
    - **Property 2: Preservation** - Pattern Matching Equivalence Unchanged
    - **IMPORTANT**: Re-run the SAME tests from task 2 — do NOT write new tests
    - Run `RegexAotUnsafePreservationTests` from task 2
    - Also run existing test suites to verify no regressions:
      - `WildcardPatternMatchingTests` — all 14 existing unit tests
      - `WildcardPatternMatchingPropertyTests` — all 5 existing property tests
      - `PrimaryEntityIdentificationPropertyTests` — all property tests
      - `CompositeEntityPreservationPropertyTests` — all 4 preservation properties (updated in 3.8)
    - **EXPECTED OUTCOME**: All tests PASS — confirms no regressions in pattern matching behavior
    - _Requirements: 3.1, 3.2, 3.3, 3.4, 3.5_

- [x] 4. Checkpoint — Ensure all tests pass
  - Run `dotnet build-server shutdown` then `dotnet test` on the full solution
  - Verify zero test failures across all test projects
  - Verify no new compiler warnings related to `System.Text.RegularExpressions` in generated code
  - If any test fails, diagnose and fix before proceeding
  - Ask the user if questions arise

## Notes

- The `ConvertWildcardPatternToRegex` helper is retained (not deleted) because existing test projects reference it directly for property-based testing validation.
- The `InferDelimiterFromPattern` helper is reused by the new `EmitWildcardPatternMatch` method — no changes needed to it.
- Block scoping (`{ }`) is used in emitted code so the `_seg` variable name can be reused across multiple pattern checks without collisions.
- Exact-match `[RelatedEntity]` patterns (no wildcards) are not affected by this fix — their emission logic remains unchanged.
- After implementing the fix, run `dotnet build-server shutdown` before building to clear the cached source generator from memory.
