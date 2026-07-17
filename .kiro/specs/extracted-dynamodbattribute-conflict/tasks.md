# Implementation Plan

## Overview

Add FDDB124 diagnostic that fires when a property has both `[Extracted]` and `[DynamoDbAttribute]` applied simultaneously. Follows the exploratory bugfix workflow: write tests before fix to understand the bug, implement the fix, then verify correctness and preservation.

## Tasks

- [x] 1. Write bug condition exploration test
  - **Property 1: Bug Condition** - Extracted Property With Attribute Mapping Emits No Diagnostic
  - **CRITICAL**: This test MUST FAIL on unfixed code - failure confirms the bug exists
  - **DO NOT attempt to fix the test or the code when it fails**
  - **NOTE**: This test encodes the expected behavior - it will validate the fix when it passes after implementation
  - **GOAL**: Surface counterexamples that demonstrate the bug exists
  - **Scoped PBT Approach**: Scope the property to concrete failing cases: any property with `IsExtracted == true` AND `HasAttributeMapping == true`
  - Write a source generator unit test that provides an entity with both `[Extracted("Pk", 0)]` and `[DynamoDbAttribute("year")]` on the same property
  - Run the analyzer and assert that diagnostic FDDB124 is emitted with Error severity
  - Assert diagnostic message contains the property name (e.g., "Year")
  - Test cases: (1) basic conflict `[Extracted("Pk", 0)] [DynamoDbAttribute("year")] public int Year`, (2) multiple conflicting properties on same entity, (3) conflict where source property is valid and computed
  - Run test on UNFIXED code
  - **EXPECTED OUTCOME**: Test FAILS (no FDDB124 diagnostic is produced - this confirms the bug exists)
  - Document counterexamples found (e.g., "No FDDB124 emitted when both attributes present; generated code includes both serialization and extraction paths")
  - Mark task complete when test is written, run, and failure is documented
  - _Requirements: 1.1, 2.1, 2.2, 2.3_

- [x] 2. Write preservation property tests (BEFORE implementing fix)
  - **Property 2: Preservation** - Existing Validation Behavior Unchanged For Non-Conflicting Properties
  - **IMPORTANT**: Follow observation-first methodology
  - Observe: `[Extracted("Pk", 0)] public int Year` (no `[DynamoDbAttribute]`) emits no FDDB124 on unfixed code and proceeds through normal validation
  - Observe: `[DynamoDbAttribute("status")] public string Status` (no `[Extracted]`) is completely unaffected by extracted validation
  - Observe: `[Extracted("ConstantProp", 0)]` referencing a constant key property emits FDDB122 (not FDDB124) on unfixed code
  - Observe: `[Extracted("Pk", -1)]` with negative index emits the invalid index diagnostic on unfixed code
  - Write property-based tests: for all properties where `IsExtracted == true` AND `HasAttributeMapping == false`, the validation produces the same results as current behavior (source existence, FDDB122 constant key conflict, invalid index checks)
  - Test cases: (1) extracted-only property with valid source emits no error, (2) extracted property referencing non-existent source emits InvalidExtractedKeySource, (3) extracted property referencing constant key emits FDDB122, (4) extracted property with negative index emits InvalidExtractedKeyIndex, (5) standard `[DynamoDbAttribute]`-only property is unaffected
  - Verify tests pass on UNFIXED code
  - **EXPECTED OUTCOME**: Tests PASS (this confirms baseline behavior to preserve)
  - Mark task complete when tests are written, run, and passing on unfixed code
  - _Requirements: 3.1, 3.2, 3.3, 3.4, 3.5_

- [x] 3. Fix for Extracted + DynamoDbAttribute conflict detection

  - [x] 3.1 Add FDDB124 DiagnosticDescriptor
    - Add new `ExtractedPropertyHasAttributeMapping` DiagnosticDescriptor to `DiagnosticDescriptors.cs` after FDDB123 (`ConstantKeyEmptyValue`)
    - Code: `"FDDB124"`
    - Title: `"Extracted property conflicts with DynamoDbAttribute"`
    - Message: `"Property '{0}' has both [Extracted] and [DynamoDbAttribute]. Extracted properties derive their value from a composite key and must not have independent DynamoDB attribute mapping. Remove one of the attributes."`
    - Category: `"DynamoDb"`
    - Severity: `DiagnosticSeverity.Error`
    - Enabled by default: `true`
    - Description: `"An [Extracted] property derives its value from a composite key at read time and should not also map to an independent DynamoDB attribute. Remove either [Extracted] or [DynamoDbAttribute]."`
    - Help link: `string.Format(DiagnosticHelpLinks.BaseUrlFormat, "FDDB124")`
    - _Bug_Condition: isBugCondition(property) where property.IsExtracted == true AND property.HasAttributeMapping == true_
    - _Expected_Behavior: Emit FDDB124 error diagnostic at property identifier location_
    - _Preservation: Properties without both attributes are unaffected_
    - _Requirements: 2.1, 2.2_

  - [x] 3.2 Add HasAttributeMapping check to ValidateExtractedProperty()
    - In `EntityAnalyzer.cs`, insert check as the FIRST validation in `ValidateExtractedProperty()`, BEFORE the source property existence check
    - Check: `if (extractedProperty.HasAttributeMapping)`
    - Action: `ReportDiagnostic(DiagnosticDescriptors.ExtractedPropertyHasAttributeMapping, extractedProperty.PropertyDeclaration?.Identifier.GetLocation(), extractedProperty.PropertyName)`
    - Early return after reporting to avoid cascading diagnostics
    - Run `dotnet build-server shutdown` then `dotnet build` to verify compilation
    - _Bug_Condition: isBugCondition(property) where property.IsExtracted == true AND property.HasAttributeMapping == true_
    - _Expected_Behavior: ValidateExtractedProperty emits FDDB124 and returns early when HasAttributeMapping is true_
    - _Preservation: All subsequent checks (source existence, constant key FDDB122, index bounds) unchanged for non-conflicting properties_
    - _Requirements: 2.1, 2.2, 2.3, 3.1, 3.3, 3.4, 3.5_

  - [x] 3.3 Verify bug condition exploration test now passes
    - **Property 1: Expected Behavior** - Extracted Property With Attribute Mapping Emits FDDB124
    - **IMPORTANT**: Re-run the SAME test from task 1 - do NOT write a new test
    - The test from task 1 encodes the expected behavior (FDDB124 emitted for conflicting properties)
    - When this test passes, it confirms the expected behavior is satisfied
    - Run bug condition exploration test from step 1
    - **EXPECTED OUTCOME**: Test PASSES (confirms bug is fixed - FDDB124 now emitted correctly)
    - _Requirements: 2.1, 2.2, 2.3_

  - [x] 3.4 Verify preservation tests still pass
    - **Property 2: Preservation** - Existing Validation Behavior Unchanged
    - **IMPORTANT**: Re-run the SAME tests from task 2 - do NOT write new tests
    - Run preservation property tests from step 2
    - **EXPECTED OUTCOME**: Tests PASS (confirms no regressions - existing FDDB122, index checks, and standard properties all behave identically)
    - Confirm all tests still pass after fix (no regressions)

- [x] 4. Checkpoint - Ensure all tests pass
  - Run `dotnet build-server shutdown` then `dotnet test` to verify all tests pass
  - Ensure no new warnings or errors introduced
  - Verify existing test entities in the test suite continue to compile without new diagnostics
  - Ask the user if questions arise

## Task Dependency Graph

```json
{
  "waves": [
    { "id": 0, "tasks": ["1", "2"] },
    { "id": 1, "tasks": ["3.1"] },
    { "id": 2, "tasks": ["3.2"] },
    { "id": 3, "tasks": ["3.3", "3.4"] },
    { "id": 4, "tasks": ["4"] }
  ]
}
```

## Notes

- Tasks 1 and 2 are independent and can be done in parallel
- Task 1 MUST fail on unfixed code (confirming the bug exists)
- Task 2 MUST pass on unfixed code (confirming baseline behavior)
- After fix (3.1 + 3.2), task 3.3 re-runs the same test from task 1 expecting it to pass
- After fix, task 3.4 re-runs the same tests from task 2 expecting them to still pass
- Remember to run `dotnet build-server shutdown` before builds when source generator changes are made
