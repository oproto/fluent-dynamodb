# Implementation Plan

## Overview

This plan follows the exploratory bugfix workflow: write tests to confirm the bug exists, write preservation tests to capture correct baseline behavior, then implement the fix and verify both sets of tests pass.

The bug is that `PropertyMetadata.ComputedFieldTarget` (typed `string?`) can only hold one target. When a source property contributes to multiple non-key computed fields, only the first match (via `FirstOrDefault`) is emitted. The fix renames/retypes to `ComputedFieldTargets` (`string[]?`) and uses `Where` to capture all matches.

## Tasks

- [x] 1. Write bug condition exploration test
  - **Property 1: Bug Condition** - Multi-Target Source Only Emits First Target
  - **CRITICAL**: This test MUST FAIL on unfixed code - failure confirms the bug exists
  - **DO NOT attempt to fix the test or the code when it fails**
  - **NOTE**: This test encodes the expected behavior - it will validate the fix when it passes after implementation
  - **GOAL**: Surface counterexamples that demonstrate the bug exists
  - **Scoped PBT Approach**: Scope the property to concrete failing cases: a source property listed in `SourceProperties` of 2+ non-key computed fields
  - Create a `PropertyMetadata` with `ComputedFieldTarget` (current singular string) and verify it can only hold one target
  - Specifically: configure entity where `Status` is a source of both `Gsi1Pk` and `Gsi2Pk` computed fields, invoke MapperGenerator logic, and assert that `ComputedFieldTarget` contains ALL target names
  - The assertion should verify: `metadata.ComputedFieldTargets` contains both "Gsi1Pk" AND "Gsi2Pk" (this will FAIL on unfixed code because only the first is emitted)
  - Test file: `Oproto.FluentDynamoDb.UnitTests/Expressions/MultiComputedFieldTargetBugConditionTests.cs`
  - Use FsCheck property-based testing with xUnit (`[Property]` attribute)
  - Generate arbitrary source property names and 2-5 computed field target names, verify all targets are present in metadata
  - Run test on UNFIXED code
  - **EXPECTED OUTCOME**: Test FAILS (this is correct - it proves the bug exists because `ComputedFieldTarget` is `string?` and can only hold one value)
  - Document counterexamples found: e.g., "Status source property only has ComputedFieldTarget = 'Gsi1Pk', missing 'Gsi2Pk'"
  - Mark task complete when test is written, run, and failure is documented
  - _Requirements: 1.1, 1.2, 1.3_

- [x] 2. Write preservation property tests (BEFORE implementing fix)
  - **Property 2: Preservation** - Single-Target and Non-Source Behavior Unchanged
  - **IMPORTANT**: Follow observation-first methodology
  - Observe: For a source property contributing to exactly one non-key computed field, `ComputedFieldTarget` is set to that target name and `IsComputedSourceProperty` returns true on unfixed code
  - Observe: For a property not in any computed field's sources, `ComputedFieldTarget` is null and `IsComputedSourceProperty` returns false on unfixed code
  - Observe: For an extracted property targeting a non-key computed field, `IsComputedSourceProperty` returns true via the extracted-field path on unfixed code
  - Write property-based tests (FsCheck with `[Property]` attribute):
    - For all single-target source properties: `IsComputedSourceProperty` returns true and metadata identifies the property as a computed source
    - For all non-source properties (not in any computed field's SourceProperties): `IsComputedSourceProperty` returns false
    - For all extracted properties targeting non-key computed fields: `IsComputedSourceProperty` returns true
  - Test file: `Oproto.FluentDynamoDb.UnitTests/Expressions/MultiComputedFieldTargetPreservationTests.cs`
  - Use the same `EntityMetadata` / `PropertyMetadata` pattern from existing `ComputedFieldValidationTests.cs`
  - Verify tests pass on UNFIXED code
  - **EXPECTED OUTCOME**: Tests PASS (this confirms baseline behavior to preserve)
  - Mark task complete when tests are written, run, and passing on unfixed code
  - _Requirements: 3.1, 3.2, 3.3, 3.4, 3.5_

- [x] 3. Fix for multi-computed-field-target bug

  - [x] 3.1 Rename and retype `PropertyMetadata.ComputedFieldTarget` to `ComputedFieldTargets`
    - In `Oproto.FluentDynamoDb/Metadata/PropertyMetadata.cs`:
    - Change `public string? ComputedFieldTarget { get; set; }` to `public string[]? ComputedFieldTargets { get; set; }`
    - Update XML doc summary to: "If this property is a source property of one or more non-key computed fields, contains the names of all target computed properties. Null if the property is not a source of any computed field."
    - _Bug_Condition: isBugCondition(input) where sourceProperty is in SourceProperties of >1 non-key computed fields_
    - _Expected_Behavior: ComputedFieldTargets contains ALL matching computed field names_
    - _Preservation: Single-target sources get single-element array; non-sources remain null_
    - _Requirements: 2.1, 2.2, 3.1, 3.2_

  - [x] 3.2 Update MapperGenerator to emit all targets using `Where` instead of `FirstOrDefault`
    - In `Oproto.FluentDynamoDb.SourceGenerator/Generators/MapperGenerator.cs` (~line 4870-4878):
    - Replace `FirstOrDefault` with `.Where(...).Select(p => p.PropertyName).ToArray()`
    - Emit `ComputedFieldTargets = new[] { "target1", "target2" }` instead of `ComputedFieldTarget = "target"`
    - Conditional: only emit if `targetComputedFields.Length > 0`
    - _Bug_Condition: isBugCondition(input) where FirstOrDefault discards subsequent matches_
    - _Expected_Behavior: All N matching computed field names emitted in array initializer_
    - _Preservation: Single-target emits single-element array; zero-target emits nothing_
    - _Requirements: 2.1, 2.2, 3.1, 3.2_

  - [x] 3.3 Update `IsComputedSourceProperty` to check `ComputedFieldTargets?.Length > 0`
    - In `Oproto.FluentDynamoDb/Expressions/UpdateExpressionTranslator.cs` (~line 730):
    - Change `if (propertyMetadata.ComputedFieldTarget != null)` to `if (propertyMetadata.ComputedFieldTargets?.Length > 0)`
    - Update XML doc `<see cref="..."/>` references from `ComputedFieldTarget` to `ComputedFieldTargets`
    - _Bug_Condition: N/A (this is the consumer-side adaptation)_
    - _Expected_Behavior: Returns true when ComputedFieldTargets has any elements_
    - _Preservation: Boolean behavior unchanged for single-target and non-source properties_
    - _Requirements: 2.3, 3.1, 3.2, 3.5_

  - [x] 3.4 Update existing test files to use new array form
    - In `Oproto.FluentDynamoDb.UnitTests/Expressions/UpdateExpressionTranslatorComputedFieldPropertyTests.cs`:
      - Replace all `ComputedFieldTarget = "X"` with `ComputedFieldTargets = new[] { "X" }`
    - In `Oproto.FluentDynamoDb.UnitTests/Expressions/ComputedFieldValidationTests.cs`:
      - Replace all `ComputedFieldTarget = "X"` with `ComputedFieldTargets = new[] { "X" }`
    - In `Oproto.FluentDynamoDb.IntegrationTests/RealWorld/ComputedGsiFieldUpdateIntegrationTests.cs`:
      - Replace all `ComputedFieldTarget = "X"` with `ComputedFieldTargets = new[] { "X" }`
    - _Preservation: All existing tests must continue to pass with the new array form_
    - _Requirements: 3.1, 3.3, 3.4_

  - [x] 3.5 Add multi-target integration test
    - In `Oproto.FluentDynamoDb.IntegrationTests/RealWorld/ComputedGsiFieldUpdateIntegrationTests.cs`:
    - Add new test: entity with `Status` as source of both `Gsi1Pk` and `Gsi2Pk` computed fields
    - Configure `ComputedFieldTargets = new[] { "Gsi1Pk", "Gsi2Pk" }` on the Status property
    - Assign `Status` in an update expression, verify BOTH computed fields are recomputed in the emitted SET expression
    - Add test: assign shared source but omit one computed field's other required source, verify FDDB072 fires for the incomplete computed field only
    - _Bug_Condition: Source property in SourceProperties of 2+ non-key computed fields_
    - _Expected_Behavior: Both computed fields are recomputed; FDDB072 fires independently_
    - _Requirements: 2.1, 2.2, 3.3, 3.4_

  - [x] 3.6 Verify bug condition exploration test now passes
    - **Property 1: Expected Behavior** - Multi-Target Source Emits All Targets
    - **IMPORTANT**: Re-run the SAME test from task 1 - do NOT write a new test
    - The test from task 1 encodes the expected behavior (all targets present in array)
    - When this test passes, it confirms the expected behavior is satisfied
    - Run bug condition exploration test from step 1
    - **EXPECTED OUTCOME**: Test PASSES (confirms bug is fixed)
    - _Requirements: 2.1, 2.2_

  - [x] 3.7 Verify preservation tests still pass
    - **Property 2: Preservation** - Single-Target and Non-Source Behavior Unchanged
    - **IMPORTANT**: Re-run the SAME tests from task 2 - do NOT write new tests
    - Run preservation property tests from step 2
    - **EXPECTED OUTCOME**: Tests PASS (confirms no regressions)
    - Confirm all tests still pass after fix (no regressions)
    - _Requirements: 3.1, 3.2, 3.3, 3.4, 3.5_

- [x] 4. Checkpoint - Ensure all tests pass
  - Run full test suite: `dotnet test` across all test projects
  - Verify no compilation errors after property rename
  - Verify `dotnet build` succeeds for `Oproto.FluentDynamoDb.SourceGenerator` (run `dotnet build-server shutdown` first to clear cached generator)
  - Ensure all tests pass, ask the user if questions arise.

## Notes

- FsCheck is already available in the test project for property-based testing
- `ValidateAndProcessComputedFields` requires NO changes — it already iterates all computed fields independently via `cf.SourceProperties.Contains(sourceName)`
- The source generator must be restarted after changes (`dotnet build-server shutdown`) due to caching
- The `UpdateExpressionTranslator.ValidateAndProcessComputedFields` comment at line 2608 references `ComputedFieldTarget` in a comment — update comment text but no logic change needed

## Task Dependency Graph

```json
{
  "waves": [
    { "id": 0, "tasks": ["1"] },
    { "id": 1, "tasks": ["2"] },
    { "id": 2, "tasks": ["3.1", "3.2", "3.3"] },
    { "id": 3, "tasks": ["3.4", "3.5"] },
    { "id": 4, "tasks": ["3.6", "3.7"] },
    { "id": 5, "tasks": ["4"] }
  ]
}
```
