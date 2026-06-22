# Implementation Plan

- [x] 1. Write bug condition exploration test
  - **Property 1: Bug Condition** - Computed↔Extracted Bidirectional Mapping False Positive
  - **CRITICAL**: This test MUST FAIL on unfixed code - failure confirms the bug exists
  - **DO NOT attempt to fix the test or the code when it fails**
  - **NOTE**: This test encodes the expected behavior - it will validate the fix when it passes after implementation
  - **GOAL**: Surface counterexamples that demonstrate the false positive DYNDB033 fires incorrectly
  - **Scoped PBT Approach**: Scope the property to concrete failing cases where `[Extracted]` references a `[Computed]` source property and the extracted property appears in the computed source list
  - Write a Roslyn source generator unit test in `Oproto.FluentDynamoDb.SourceGenerator.UnitTests` that defines an entity with the bidirectional mapping pattern:
    - Entity with `[Computed("Year", "Month", "Day", Separator = "#")] string Pk` and `[Extracted("Pk", 0)] int Year`, `[Extracted("Pk", 1)] int Month`, `[Extracted("Pk", 2)] int Day`
    - Entity with `[Computed("TenantId", "UserId")] string Pk` and `[Extracted("Pk", 0)] string TenantId`, `[Extracted("Pk", 1)] string UserId`
  - Assert that DYNDB033 is NOT reported for valid Computed↔Extracted round-trip patterns (expected behavior)
  - Run test on UNFIXED code
  - **EXPECTED OUTCOME**: Test FAILS because DYNDB033 is incorrectly reported (this proves the false positive exists)
  - Document counterexamples found (e.g., "DYNDB033 reported with cycle path 'Year -> Pk -> Year' for valid bidirectional mapping")
  - Mark task complete when test is written, run, and failure is documented
  - _Requirements: 1.1, 1.2, 2.1, 2.2_

- [x] 2. Write preservation property tests (BEFORE implementing fix)
  - **Property 2: Preservation** - Genuine Computed→Computed Cycle Detection Preserved
  - **IMPORTANT**: Follow observation-first methodology
  - Observe behavior on UNFIXED code for non-buggy inputs (entities with genuine Computed→Computed cycles, self-references, invalid Extracted sources)
  - Write property-based tests in `Oproto.FluentDynamoDb.SourceGenerator.UnitTests` capturing observed behavior:
    - Observe: Entity with `[Computed("B")] string A` and `[Computed("A")] string B` → DYNDB033 fires on unfixed code
    - Observe: Entity with A→B→C→A multi-hop all via `[Computed]` → DYNDB033 fires on unfixed code
    - Observe: Entity with `[Computed("Pk")] string Pk` (self-reference) → DYNDB034 fires on unfixed code
    - Observe: Entity with `[Extracted("NonExistent", 0)]` → appropriate diagnostic fires on unfixed code
    - Observe: Entity with `[Extracted("Source", -1)]` (negative index) → appropriate diagnostic fires on unfixed code
  - Write property-based tests: for all entities where NO property pair satisfies isBugCondition (no Computed↔Extracted bidirectional link), the analyzer produces the expected diagnostics
  - Verify tests pass on UNFIXED code
  - **EXPECTED OUTCOME**: Tests PASS (this confirms baseline behavior to preserve)
  - Mark task complete when tests are written, run, and passing on unfixed code
  - _Requirements: 3.1, 3.2, 3.3, 3.4_

- [x] 3. Fix for DYNDB033 false positive on Computed↔Extracted bidirectional mapping

  - [x] 3.1 Implement the fix
    - Remove the Computed↔Extracted cross-check block from `ValidateExtractedProperty` in `Oproto.FluentDynamoDb.SourceGenerator/Analysis/EntityAnalyzer.cs`
    - Delete the entire block (lines ~2082-2093) starting at comment `// Check if source property is also computed (potential circular dependency)` through the closing brace
    - This removes the `if (sourceProperty?.IsComputed == true) { ... }` block that incorrectly calls `ReportDiagnostic(DiagnosticDescriptors.CircularKeyDependency, ...)` for valid Computed↔Extracted pairs
    - No replacement code needed — the DFS-based `ValidateComputedKeyCircularDependencies` already correctly catches genuine Computed→Computed cycles
    - No changes to `ValidateComputedKeyCircularDependencies`, `ValidateComputedProperty`, or `HasCircularDependency`
    - Run `dotnet build-server shutdown` before building to clear cached source generator
    - _Bug_Condition: isBugCondition(extractedProperty, entityModel) where sourceProperty.IsComputed AND sourceProperty.ComputedKey.SourceProperties.Contains(extractedProperty.PropertyName)_
    - _Expected_Behavior: System SHALL NOT report DYNDB033 for valid Computed↔Extracted bidirectional mapping patterns_
    - _Preservation: Genuine Computed→Computed cycles via ValidateComputedKeyCircularDependencies, self-references via ValidateComputedProperty, invalid Extracted references unchanged_
    - _Requirements: 2.1, 2.2, 3.1, 3.2, 3.3, 3.4_

  - [x] 3.2 Verify bug condition exploration test now passes
    - **Property 1: Expected Behavior** - Computed↔Extracted Bidirectional Mapping Allowed
    - **IMPORTANT**: Re-run the SAME test from task 1 - do NOT write a new test
    - The test from task 1 encodes the expected behavior (no DYNDB033 for valid round-trip patterns)
    - When this test passes, it confirms the expected behavior is satisfied
    - Run bug condition exploration test from step 1
    - **EXPECTED OUTCOME**: Test PASSES (confirms false positive is eliminated)
    - _Requirements: 2.1, 2.2_

  - [x] 3.3 Verify preservation tests still pass
    - **Property 2: Preservation** - Genuine Circular Dependencies Still Detected
    - **IMPORTANT**: Re-run the SAME tests from task 2 - do NOT write new tests
    - Run preservation property tests from step 2
    - **EXPECTED OUTCOME**: Tests PASS (confirms no regressions)
    - Confirm genuine Computed→Computed cycles still trigger DYNDB033
    - Confirm self-referencing computed properties still trigger DYNDB034
    - Confirm invalid Extracted sources still trigger appropriate diagnostics

- [x] 4. Checkpoint - Ensure all tests pass
  - Run `dotnet build-server shutdown` then `dotnet test` across the full solution
  - Ensure all tests pass, ask the user if questions arise.


