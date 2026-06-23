# Implementation Plan: Computed Key Accessor Overloads

## Overview

This plan implements two complementary enhancements to the source generator: (1) typed parameter convenience overloads for entities with computed keys, and (2) KeyInputMode integration for entities with string keys that have prefixes but no typed overloads. The implementation modifies `TableGenerator` and introduces new helper classes (`ComputedOverloadEligibility`, `OverloadParameterResolver`) within the source generator project.

## Tasks

- [x] 1. Core eligibility and parameter resolution infrastructure
  - [x] 1.1 Create `ComputedOverloadEligibility` static helper class
    - Create file `Oproto.FluentDynamoDb.SourceGenerator/Generators/ComputedOverloadEligibility.cs`
    - Implement `QualifiesForTypedOverload(EntityModel entity)` — returns true when at least one key has `IsComputed == true` and `ComputedKey.SourceProperties.Length >= 2`
    - Implement `WouldBeAmbiguous(EntityModel entity)` — compares typed overload parameter types/count against standard overload
    - Implement `QualifiesForKeyInputMode(EntityModel entity)` — returns true when at least one string key has a prefix AND no non-ambiguous typed overload exists
    - _Requirements: 1.1, 1.5, 4.1, 4.2, 4.7, 8.1, 8.2, 8.3, 8.4, 10.1, 10.2_

  - [x] 1.2 Create `OverloadParameterResolver` static helper class
    - Create file `Oproto.FluentDynamoDb.SourceGenerator/Generators/OverloadParameterResolver.cs`
    - Implement `ParameterInfo` record with `Name`, `Type`, `IsNullable` properties
    - Implement `ResolveParameters(EntityModel entity, PropertyModel keyProperty)` — resolves source property names to types from `entity.Properties`
    - Implement `GetTypedOverloadParameters(EntityModel entity)` — returns combined PK + SK parameter list in declaration order
    - Implement `GetStandardOverloadParameters(EntityModel entity)` — returns existing standard overload parameter types
    - Implement `ToCamelCase(string propertyName)` — first character lowercased, rest unchanged
    - Return null from `ResolveParameters` when a source property cannot be found (diagnostic will be emitted)
    - _Requirements: 2.1, 2.2, 2.3, 2.4, 2.5_

  - [x] 1.3 Add diagnostic descriptors for new diagnostics
    - Add `FDDB070` (Error) to `DiagnosticDescriptors.cs` — unresolvable source property in computed key
    - Add `FDDB071` (Warning) to `DiagnosticDescriptors.cs` — FluentResults entity with typed overloads reminder
    - _Requirements: 2.1_

- [x] 2. Typed convenience overload code emission in TableGenerator
  - [x] 2.1 Implement typed overload generation for entity accessor Get method
    - Modify `TableGenerator` to call `ComputedOverloadEligibility.QualifiesForTypedOverload()` after emitting the standard Get overload
    - When eligible and not ambiguous, emit an additional Get method with typed parameters from `OverloadParameterResolver.GetTypedOverloadParameters()`
    - Generated method body calls `Entity.Keys.BuildPk(...)` and/or `Entity.Keys.BuildSk(...)` and delegates to the standard overload
    - Handle case: computed PK only (no SK) — parameters are PK source props only
    - Handle case: computed SK only (simple PK) — PK string + SK source props
    - Handle case: both computed — all PK source props + all SK source props
    - Handle case: one computed + one non-computed — computed source props + single string param for the non-computed key
    - Emit diagnostic FDDB070 if parameter resolution fails (skip overload generation)
    - _Requirements: 1.1, 1.2, 1.3, 1.6, 1.7, 2.1, 2.2, 2.3, 2.4, 2.5, 3.1, 3.2, 3.3_

  - [x] 2.2 Implement typed overload generation for Delete, Update, ConditionCheck
    - Replicate the same typed overload emission logic for Delete, Update, and ConditionCheck accessor methods
    - Ensure identical parameter signatures (same names, types, order) across all four methods
    - Each method body delegates to its corresponding standard overload after composing keys
    - _Requirements: 1.4, 3.1, 3.2, 3.3_

  - [x] 2.3 Implement `KeyInputMode.Raw` delegation in typed overloads
    - Typed overload method bodies pass composed key values directly to the request builder (bypassing prefix logic)
    - The standard overload does NOT get a KeyInputMode parameter when typed overloads exist (per Req 4 AC 2)
    - Verify that `Entity.Keys.BuildPk(...)` output includes any configured prefix in its composition
    - _Requirements: 3.4, 5.1, 5.2, 5.3, 5.4, 9.1_

  - [x] 2.4 Write property tests for typed overload generation correctness
    - **Property 1: Typed overload generation correctness**
    - **Validates: Requirements 1.1, 1.3, 1.6, 1.7**

  - [x] 2.5 Write property test for CRUD method consistency
    - **Property 2: Consistency across CRUD methods**
    - **Validates: Requirements 1.4**

  - [x] 2.6 Write property test for no overload on non-computed entities
    - **Property 3: No overload for non-computed entities**
    - **Validates: Requirements 1.5**

- [x] 3. Checkpoint - Core generation logic
  - Ensure all tests pass, ask the user if questions arise.

- [x] 4. KeyInputMode integration for standard accessor methods
  - [x] 4.1 Add `KeyInputMode mode = KeyInputMode.Default` parameter to standard accessors
    - Modify `TableGenerator` Get, Delete, Update, ConditionCheck generation to call `ComputedOverloadEligibility.QualifiesForKeyInputMode()`
    - When eligible, append `KeyInputMode mode = KeyInputMode.Default` parameter after key parameters, before CancellationToken
    - Generated method body calls `KeyInputModeResolver.Resolve(mode, _table.Options)` once per invocation
    - Apply `KeyPrefixHelper.ApplyKeyPrefix` to each string key parameter that has a configured prefix using the resolved mode
    - Do NOT add the parameter when the entity qualifies for typed overloads (mutually exclusive)
    - Do NOT add the parameter when no string key has a configured prefix
    - _Requirements: 4.1, 4.2, 4.3, 4.4, 4.5, 4.6, 4.7, 4.8, 10.1, 10.2, 10.3, 10.4, 10.5_

  - [x] 4.2 Implement ambiguity detection to skip typed overloads silently
    - When `WouldBeAmbiguous()` returns true, skip typed overload generation (no diagnostic)
    - Fall through to KeyInputMode eligibility check instead
    - Ensure signature comparison excludes optional parameters with default values
    - _Requirements: 8.1, 8.2, 8.3, 8.4_

  - [x] 4.3 Write property test for KeyInputMode eligibility
    - **Property 7: KeyInputMode eligibility**
    - **Validates: Requirements 4.1, 4.2, 4.7, 6.1, 6.3, 7.1, 7.3, 10.1, 10.2, 11.6**

  - [x] 4.4 Write property test for ambiguity detection
    - **Property 8: Ambiguity detection**
    - **Validates: Requirements 8.1, 8.2, 8.3, 8.4**

- [x] 5. Table-level and convenience method propagation
  - [x] 5.1 Propagate typed overloads to table-level methods
    - Generate table-level typed overloads (e.g., `table.Get(int year, int month, int day, string sK)`) that delegate to entity accessor typed overloads
    - Apply same eligibility rules — only generate when entity accessor has typed overloads
    - _Requirements: 6.1, 6.2_

  - [x] 5.2 Propagate KeyInputMode to table-level overloads
    - Add `KeyInputMode mode = KeyInputMode.Default` parameter to table-level Get, Delete, Update, ConditionCheck under same eligibility conditions as Requirement 4
    - Table-level method passes `mode` through to entity accessor unchanged
    - _Requirements: 6.1, 6.2, 6.3_

  - [x] 5.3 Propagate KeyInputMode to convenience async methods
    - Add `KeyInputMode mode = KeyInputMode.Default` to `GetAsync`, `DeleteAsync` convenience methods when eligible
    - Position after existing optional parameters (KeyCondition) and before CancellationToken
    - Pass `mode` through to underlying builder method
    - Apply same logic to FluentResults variants (`GetAsyncResult`, `DeleteAsyncResult`) when applicable
    - _Requirements: 7.1, 7.2, 7.3_

  - [x] 5.4 Write property test for parameter type and name resolution
    - **Property 4: Parameter type and name resolution**
    - **Validates: Requirements 2.1, 2.2, 2.4, 2.5**

  - [x] 5.5 Write property test for delegation to Keys.Build methods
    - **Property 5: Delegation to Keys.Build methods with Raw bypass**
    - **Validates: Requirements 3.1, 3.2, 3.3, 3.4, 5.1, 5.2**

- [x] 6. Checkpoint - Full generation logic complete
  - Ensure all tests pass, ask the user if questions arise.

- [x] 7. Backward compatibility and standard overload preservation
  - [x] 7.1 Verify existing overloads remain intact
    - Ensure existing `(string)` and `(string, string)` accessor overloads are never removed or modified
    - When typed overloads are generated, the standard string overload remains without KeyInputMode parameter (Req 11.6)
    - When KeyInputMode is added, the default value of `KeyInputMode.Default` resolves to `Auto` preserving existing behavior
    - _Requirements: 11.1, 11.2, 11.3, 11.4, 11.5, 11.6_

  - [x] 7.2 Write property test for standard overload preservation
    - **Property 9: Standard overload preservation (backward compatibility)**
    - **Validates: Requirements 11.1, 11.5**

- [x] 8. Unit tests — source generator output verification
  - [x] 8.1 Write unit tests for computed PK only (no SK) scenario
    - Construct `EntityModel` with computed PK (≥2 source properties), no SK
    - Verify generated output contains typed overload with PK source params only
    - _Requirements: 1.7, 13.1_

  - [x] 8.2 Write unit tests for computed SK only (simple PK) scenario
    - Construct `EntityModel` with simple string PK + computed SK (≥2 sources)
    - Verify typed overload has PK string + SK source property params
    - _Requirements: 1.2, 13.2_

  - [x] 8.3 Write unit tests for both keys computed scenario
    - Construct `EntityModel` with both PK and SK computed
    - Verify single typed overload with all PK source params followed by all SK source params
    - _Requirements: 1.3, 13.3_

  - [x] 8.4 Write unit tests for prefix + computed key scenario
    - Construct `EntityModel` with computed key that also has a configured prefix
    - Verify typed overload delegates to `Keys.BuildPk(...)` / `Keys.BuildSk(...)`
    - _Requirements: 9.1, 13.4_

  - [x] 8.5 Write unit tests for non-string source property types
    - Construct `EntityModel` with `int`, `DateTime`, `Guid`, enum source properties in computed keys
    - Verify typed overload parameter types match source property types
    - _Requirements: 2.2, 2.3, 13.5_

  - [x] 8.6 Write unit tests for KeyInputMode generation on string key with prefix
    - Construct `EntityModel` with string key + prefix, no computed key
    - Verify generated accessor includes `KeyInputMode mode = KeyInputMode.Default` parameter
    - Verify no KeyInputMode when no prefix exists
    - Verify no KeyInputMode when typed overload is generated
    - _Requirements: 4.1, 4.7, 13.6_

  - [x] 8.7 Write unit tests for ambiguity detection
    - Construct `EntityModel` where computed key has all-string source properties matching existing overload signature
    - Verify no typed overload is generated (silent skip)
    - Verify entity falls through to KeyInputMode eligibility if applicable
    - _Requirements: 8.1, 8.2, 8.3, 13.7_

  - [x] 8.8 Write unit tests for table-level and convenience method propagation
    - Verify table-level typed overloads delegate to entity accessor
    - Verify table-level KeyInputMode parameter pass-through
    - Verify GetAsync/DeleteAsync convenience methods get KeyInputMode when eligible
    - _Requirements: 6.1, 6.2, 6.3, 7.1, 7.2_

  - [x] 8.9 Write unit tests for non-string key KeyInputMode exclusion
    - Construct `EntityModel` with non-string key types (int, Guid, enum)
    - Verify KeyInputMode is NOT added for non-string keys
    - Verify it IS added when a string key with prefix coexists with a non-string key
    - _Requirements: 10.1, 10.2, 10.3, 10.4, 10.5_

- [x] 9. Checkpoint - Unit tests complete
  - Ensure all tests pass, ask the user if questions arise.

- [x] 10. Re-enable disabled non-string key tests
  - [x] 10.1 Re-enable and fix deferred tests in `NonStringKeyAccessorBugExplorationTests.cs`
    - Remove `Skip` attribute from `EnumSortKey_DefaultSerialization_ShouldUseSetKeyWithStringAttributeValue`
    - Remove `Skip` attribute from `EnumSortKey_IntegerSerializationFormat_ShouldUseSetKeyWithFormattedAttributeValue`
    - Remove `Skip` attribute from `BothKeysNonString_IntPkAndEnumSk_ShouldUseSetKey`
    - Update test expectations if needed to account for KeyInputMode parameter or typed overload behavior
    - Ensure all three tests pass
    - _Requirements: 12.1, 12.2, 12.3, 12.4_

- [x] 11. Integration tests — runtime behavior verification
  - [x] 11.1 Create integration test entities with computed keys
    - Define test entities in integration test project with various computed key configurations (PK only, SK only, both, with prefix, with non-string types)
    - These entities use the source generator to produce real generated code at compile time
    - _Requirements: 14.1, 14.7_

  - [x] 11.2 Write integration tests for typed overload key equivalence
    - Instantiate generated table with mocked `IAmazonDynamoDB` (NSubstitute)
    - Invoke typed parameter overload, capture DynamoDB request
    - Invoke standard overload with manually built keys (`Entity.Keys.BuildPk(...)`) for same component values
    - Assert partition key and sort key `AttributeValue` entries are identical between both paths
    - _Requirements: 14.1, 3.5, 9.3_

  - [x] 11.3 Write integration tests for KeyInputMode.Auto behavior
    - Test with value already starting with prefix+separator — assert no double-prefix
    - Test with value NOT starting with prefix — assert prefix is applied
    - _Requirements: 14.2_

  - [x] 11.4 Write integration tests for KeyInputMode.Raw behavior
    - Test with entity that has configured prefix
    - Assert key value in captured request is identical to input (no prefix applied)
    - _Requirements: 14.3_

  - [x] 11.5 Write integration tests for KeyInputMode.Value behavior
    - Test that prefix is always prepended regardless of whether input already contains it
    - _Requirements: 14.4_

  - [x] 11.6 Write integration tests for default KeyInputMode (backward compatibility)
    - Invoke accessor without specifying KeyInputMode, passing pre-prefixed values from `Entity.Keys.Pk(...)`
    - Assert key value passes through unchanged (Auto mode detects existing prefix)
    - _Requirements: 14.5_

  - [x] 11.7 Write integration tests for no-prefix key with KeyInputMode
    - Test that no transformation is applied regardless of mode when key has no prefix
    - _Requirements: 14.6_

  - [x] 11.8 Write property test for path equivalence (round-trip)
    - **Property 6: Path equivalence (round-trip)**
    - **Validates: Requirements 3.5, 9.3**

- [x] 12. Checkpoint - Integration tests complete
  - Ensure all tests pass, ask the user if questions arise.

- [x] 13. API consistency tests
  - [x] 13.1 Add computed key entities to ApiConsistencyTests
    - Define entities with computed keys (various configurations) in `Oproto.FluentDynamoDb.ApiConsistencyTests/Entities/`
    - Define entities with string key + prefix (no computed) for KeyInputMode testing
    - _Requirements: 11.5_

  - [x] 13.2 Write API surface compile tests for typed overloads
    - Add tests in `SingleEntityTables/` verifying typed overload method signatures compile
    - Test Get, Delete, Update, ConditionCheck typed overload calls
    - Test table-level typed overload calls
    - _Requirements: 1.1, 1.3, 1.4, 1.6, 6.1_

  - [x] 13.3 Write API surface compile tests for KeyInputMode
    - Add tests verifying `KeyInputMode` parameter is accepted on standard overloads
    - Test Get, Delete, Update, ConditionCheck with explicit `KeyInputMode.Auto`, `KeyInputMode.Raw`, `KeyInputMode.Value`
    - Test GetAsync, DeleteAsync convenience methods with KeyInputMode
    - _Requirements: 4.1, 7.1, 7.3_

- [x] 14. Documentation updates
  - [x] 14.1 Create or update documentation for typed parameter overloads in `/docs`
    - Add code examples for Get, Update, Delete with typed parameter overloads demonstrating computed key usage
    - Show before (manual `Keys.BuildPk()`) and after (typed overload) patterns
    - Explain when typed overloads are generated vs. when they are skipped
    - _Requirements: 15.1_

  - [x] 14.2 Create or update documentation for KeyInputMode in `/docs`
    - Add code examples for each `KeyInputMode` value (`Auto`, `Value`, `Raw`)
    - Explain when the parameter appears and its default behavior
    - Show interaction with prefix configuration
    - _Requirements: 15.2_

  - [x] 14.3 Update `CHANGELOG.md`
    - Add entry in `[Unreleased]` section under `### Added`
    - Describe new convenience overloads for computed keys
    - Describe new `KeyInputMode` parameter on accessor methods
    - _Requirements: 15.3_

  - [x] 14.4 Update `docs/DOCUMENTATION_CHANGELOG.md`
    - Add entries for each new or modified documentation page
    - Include file path and summary of content added
    - _Requirements: 15.4_

- [x] 15. Final checkpoint
  - Ensure all tests pass, ask the user if questions arise.

## Notes

- Tasks marked with `*` are optional and can be skipped for faster MVP
- Each task references specific requirements for traceability
- Checkpoints ensure incremental validation
- Property tests validate universal correctness properties from the design document
- Unit tests verify source generator output strings
- Integration tests verify runtime behavior with mocked DynamoDB client
- API consistency tests verify compile-time API surface correctness
- Remember to run `dotnet build-server shutdown` when modifying the source generator before rebuilding
- Use `ConfigureAwait(false)` on all library async calls (not applicable to source generator which runs at compile time)
- FsCheck.Xunit is used for property-based tests (needs to be added to test project if not present)

## Task Dependency Graph

```json
{
  "waves": [
    { "id": 0, "tasks": ["1.1", "1.2", "1.3"] },
    { "id": 1, "tasks": ["2.1"] },
    { "id": 2, "tasks": ["2.2", "2.3"] },
    { "id": 3, "tasks": ["2.4", "2.5", "2.6", "4.1", "4.2"] },
    { "id": 4, "tasks": ["4.3", "4.4", "5.1", "5.2", "5.3"] },
    { "id": 5, "tasks": ["5.4", "5.5", "7.1"] },
    { "id": 6, "tasks": ["7.2", "8.1", "8.2", "8.3", "8.4", "8.5", "8.6", "8.7", "8.8", "8.9"] },
    { "id": 7, "tasks": ["10.1"] },
    { "id": 8, "tasks": ["11.1", "13.1"] },
    { "id": 9, "tasks": ["11.2", "11.3", "11.4", "11.5", "11.6", "11.7", "11.8", "13.2", "13.3"] },
    { "id": 10, "tasks": ["14.1", "14.2", "14.3", "14.4"] }
  ]
}
```
