# Implementation Plan

> **⚠️ ENVIRONMENT NOTE**: The current Kiro build cannot run `dotnet test` directly via terminal. When you need to run tests, prompt the user with the exact `dotnet test` command (including any `--filter` flags) and they will run it manually and provide the output.

- [x] 1. Write bug condition exploration test
  - **Property 1: Bug Condition** - Enum Properties Rejected by EntityAnalyzer
  - **CRITICAL**: This test MUST FAIL on unfixed code - failure confirms the bug exists
  - **DO NOT attempt to fix the test or the code when it fails**
  - **NOTE**: This test encodes the expected behavior - it will validate the fix when it passes after implementation
  - **GOAL**: Surface counterexamples that demonstrate the bug exists
  - **Scoped PBT Approach**: Scope the property to concrete failing cases: entity properties with user-defined enum types
  - Create test file `Oproto.FluentDynamoDb.SourceGenerator.UnitTests/Analysis/EnumTypeValidationTests.cs`
  - Use same `ParseSource` pattern as existing `EntityAnalyzerTests.cs` (CSharpCompilation with `TestHelpers.DynamicCompilationHelper.GetFluentDynamoDbReferences()`)
  - Test case 1: Entity with `public Status EntityStatus { get; set; }` where `Status` is a user-defined enum - assert DYNDB009 is NOT emitted and property IS included in result
  - Test case 2: Entity with `public Status? OptionalStatus { get; set; }` (nullable enum) - assert DYNDB009 is NOT emitted
  - Test case 3: Entity with `public List<Status> Statuses { get; set; }` (collection of enums) - assert DYNDB009 is NOT emitted
  - Test case 4: Entity with `[DynamoDbAttribute("status", Format = "D")] public Status EntityStatus { get; set; }` - assert DYNDB009 is NOT emitted
  - The test assertions encode expected behavior from design: enum properties accepted without DYNDB009
  - Run test on UNFIXED code
  - **EXPECTED OUTCOME**: Tests FAIL (assertions fail because DYNDB009 IS emitted on unfixed code - this proves the bug exists)
  - Document counterexamples: e.g., "EntityAnalyzer emits DYNDB009 for `Status EntityStatus` with message 'Property EntityStatus has type Status which is not supported for DynamoDB mapping'"
  - Mark task complete when tests are written, run, and failure is documented
  - _Requirements: 1.1, 1.2, 1.3_

- [x] 2. Write preservation property tests (BEFORE implementing fix)
  - **Property 2: Preservation** - Non-Enum Type Validation Unchanged
  - **IMPORTANT**: Follow observation-first methodology
  - Create test methods in `Oproto.FluentDynamoDb.SourceGenerator.UnitTests/Analysis/EnumTypeValidationTests.cs`
  - Observe on UNFIXED code: entity with `string`, `int`, `bool`, `DateTime`, `Guid` properties produces no DYNDB009
  - Observe on UNFIXED code: entity with `ulong`, `uint`, `ushort`, `byte`, `sbyte`, `short` properties produces no DYNDB009
  - Observe on UNFIXED code: entity with `[DynamoDbMap]` nested entity produces no DYNDB009
  - Observe on UNFIXED code: entity with unsupported arbitrary class type (e.g., `public SomeRandomClass Foo { get; set; }`) produces DYNDB009
  - Write preservation tests asserting these observed behaviors:
    - Test: All primitive types continue to pass validation (no DYNDB009)
    - Test: All unsigned integer types continue to pass validation (no DYNDB009)
    - Test: Complex types with `[DynamoDbMap]` continue to bypass validation (no DYNDB009)
    - Test: Genuinely unsupported types (arbitrary class without `[DynamoDbEntity]`/`[DynamoDbMap]`) continue to emit DYNDB009
  - Verify all preservation tests pass on UNFIXED code
  - **EXPECTED OUTCOME**: Tests PASS (this confirms baseline behavior to preserve)
  - Mark task complete when tests are written, run, and passing on unfixed code
  - _Requirements: 3.1, 3.2, 3.3, 3.4_

- [x] 3. Fix for enum type validation in EntityAnalyzer

  - [x] 3.1 Implement the fix in EntityAnalyzer.IsSupportedPropertyType
    - Modify `Oproto.FluentDynamoDb.SourceGenerator/Analysis/EntityAnalyzer.cs`
    - At the call site (around line 1312) where `IsSupportedPropertyType` is invoked, add enum detection using the semantic model BEFORE the DYNDB009 diagnostic is emitted
    - Use `propertySymbol.Type.TypeKind == TypeKind.Enum` for direct enum properties
    - Handle nullable enums: check if `propertySymbol.Type` is `Nullable<T>` where T has `TypeKind.Enum` (use `INamedTypeSymbol.TypeArguments[0].TypeKind`)
    - For collections (List/HashSet), ensure enum element types pass validation (the collection check returns true, but element type validation within collections may also need enum awareness)
    - The `MapperGenerator` already handles enum serialization via `.ToString()` / `Enum.Parse<T>()` — no changes needed there for string format
    - _Bug_Condition: isBugCondition(property) where property.Type.TypeKind == TypeKind.Enum AND IsSupportedPropertyType returns false_
    - _Expected_Behavior: IsSupportedPropertyType returns true for enum types, DYNDB009 not emitted_
    - _Preservation: All non-enum types produce identical validation results as before_
    - _Requirements: 2.1, 2.2, 2.3, 2.4, 3.1, 3.2, 3.3, 3.4_

  - [x] 3.2 Verify bug condition exploration test now passes
    - **Property 1: Expected Behavior** - Enum Properties Accepted by Validator
    - **IMPORTANT**: Re-run the SAME tests from task 1 - do NOT write a new test
    - The tests from task 1 encode the expected behavior (enum properties accepted without DYNDB009)
    - When these tests pass, it confirms the expected behavior is satisfied
    - Run bug condition exploration tests from step 1
    - **EXPECTED OUTCOME**: Tests PASS (confirms bug is fixed - enum properties no longer rejected)
    - _Requirements: 2.1, 2.2, 2.3, 2.4_

  - [x] 3.3 Verify preservation tests still pass
    - **Property 2: Preservation** - Non-Enum Type Validation Unchanged
    - **IMPORTANT**: Re-run the SAME tests from task 2 - do NOT write new tests
    - Run preservation property tests from step 2
    - **EXPECTED OUTCOME**: Tests PASS (confirms no regressions - primitives still accepted, unsupported types still rejected)
    - Confirm all tests still pass after fix (no regressions)

- [x] 4. Checkpoint - Ensure all tests pass
  - Run full test suite: `dotnet test` from solution root
  - Verify no existing tests broke (especially `UnsignedIntegerTypeTests`, `ComplexTypeGenerationTests`, `EndToEndSourceGeneratorTests`)
  - Verify DYNDB009 is still emitted for genuinely unsupported types in existing test assertions
  - Ensure all tests pass, ask the user if questions arise.
