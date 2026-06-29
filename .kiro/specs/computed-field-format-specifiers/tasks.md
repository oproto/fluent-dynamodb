# Implementation Plan: Computed Field Format Specifiers

## Overview

This plan implements support for .NET format specifiers (e.g., `{0:yyyy-MM-dd}`, `{0:D4}`, `{0:G}`) in computed field format strings. The implementation spans the source generator (EntityAnalyzer, KeysGenerator, MapperGenerator) and runtime (UpdateExpressionTranslator), plus a new `FormatSpecifierHelper` utility duplicated across both projects. The approach is incremental: utility first, then source generator fixes, then runtime fix, then source property Format fallback, then tests, then documentation.

## Tasks

- [x] 1. Create FormatSpecifierHelper utility class
  - [x] 1.1 Create `FormatSpecifierHelper` in `Oproto.FluentDynamoDb.SourceGenerator/Utilities/`
    - Create `FormatSpecifierHelper.cs` as an `internal static class`
    - Implement `HasAnyFormatSpecifier(string? format)` returning bool using compiled regex `\{(\d+):([^}]+)\}`
    - Implement `HasFormatSpecifierForIndex(string? format, int index)` returning bool
    - Implement `GetIndicesWithFormatSpecifiers(string? format)` returning `HashSet<int>`
    - Include XML documentation consistent with other utilities in the project
    - _Requirements: 3.1, 3.2, 4.1, 5.4_

  - [x] 1.2 Create `FormatSpecifierHelper` in `Oproto.FluentDynamoDb/Utilities/`
    - Duplicate the same `FormatSpecifierHelper.cs` implementation for the runtime project
    - Ensure namespace is `Oproto.FluentDynamoDb.Utilities`
    - Mark as `internal static` class with identical logic to the source generator copy
    - _Requirements: 4.1, 5.4_

- [x] 2. Fix EntityAnalyzer discriminator pattern and validation
  - [x] 2.1 Update `EntityAnalyzer.DeriveDiscriminatorPattern` regex
    - Change regex from `@"\{\d+\}"` to `@"\{\d+(?::[^}]*)?\}"`
    - This handles both `{N}` and `{N:format}` placeholders including specifiers with colons (e.g., `{0:HH:mm:ss}`)
    - Verify null is returned when pattern starts with `*`
    - _Requirements: 1.1, 1.2, 1.3, 1.4, 1.5_

  - [x] 2.2 Fix `EntityAnalyzer.ValidateComputedKeyFormat` index parsing
    - Extract numeric index portion before the first colon in placeholder text
    - Replace the `!placeholderText.Contains(':')` special-case with proper parsing
    - Validate that parsed index is a non-negative integer
    - Emit diagnostic for invalid placeholder format when index portion is not parseable
    - _Requirements: 2.1, 2.2, 2.3, 2.4, 2.5, 7.1, 7.2, 7.3, 7.4, 7.5_

- [x] 3. Checkpoint - Ensure build succeeds after EntityAnalyzer changes
  - Ensure all tests pass, ask the user if questions arise.
  - Run `dotnet build-server shutdown` then `dotnet build` to verify source generator changes compile.

- [x] 4. Fix KeysGenerator pre-stringification bypass
  - [x] 4.1 Update `KeysGenerator.GenerateComputedKeyBuilder` to conditionally bypass pre-stringification
    - Use `FormatSpecifierHelper.GetIndicesWithFormatSpecifiers(computedKey.Format)` to determine which indices have format specifiers
    - For indices with specifiers: emit `(object){parameterName}` instead of `GetValueExpression(...)` result
    - For indices without specifiers: continue using existing `GetValueExpression()` logic
    - When any format specifiers present: use `string.Format(System.Globalization.CultureInfo.InvariantCulture, ...)` overload
    - When no format specifiers: use existing `string.Format(...)` without culture (backwards compatible)
    - _Requirements: 3.1, 3.2, 3.3, 3.4, 3.5, 5.4_

- [x] 5. Fix MapperGenerator for InvariantCulture and source property Format injection
  - [x] 5.1 Update `MapperGenerator.GenerateComputedKeyLogic` to add InvariantCulture
    - Use `FormatSpecifierHelper.HasAnyFormatSpecifier(computedKey.Format)` to detect format specifiers
    - When specifiers present: emit `string.Format(System.Globalization.CultureInfo.InvariantCulture, ...)` 
    - When no specifiers: keep existing `string.Format(...)` for backwards compatibility
    - _Requirements: 5.1, 5.2, 5.3, 5.4_

  - [x] 5.2 Update `MapperGenerator.ComputeFormatString` for source property Format injection
    - Add source property `Format` lookup when building effective format string
    - For each placeholder without an explicit format specifier: check if the source property at that index has a non-null, non-empty `DynamoDbAttribute.Format`
    - If source property has Format: inject it into the placeholder (e.g., `{0}` → `{0:yyyy-MM-dd}`)
    - If source property has empty string Format: treat as null, leave placeholder unchanged
    - Do NOT override explicit format specifiers already in the computed format string
    - _Requirements: 6.1, 6.2, 6.3, 6.4, 6.5, 6.6_

- [x] 6. Checkpoint - Ensure build succeeds after generator changes
  - Ensure all tests pass, ask the user if questions arise.
  - Run `dotnet build-server shutdown` then `dotnet build` to verify source generator changes compile.

- [x] 7. Fix UpdateExpressionTranslator runtime recomputation
  - [x] 7.1 Update `UpdateExpressionTranslator.ValidateAndProcessComputedFields` for typed value preservation
    - Use `FormatSpecifierHelper.HasAnyFormatSpecifier(cf.Format)` to detect format specifiers
    - When specifiers present: pass typed values (boxed to object) without `.ToString()`, use `CultureInfo.InvariantCulture`
    - When no specifiers: preserve existing `.ToString()` behavior for backwards compatibility
    - Substitute empty string for null source values in both paths
    - _Requirements: 4.1, 4.2, 4.3, 4.4, 5.1, 5.4, 5.5_

- [x] 8. Checkpoint - Ensure build and tests pass after all code changes
  - Ensure all tests pass, ask the user if questions arise.
  - Run `dotnet build-server shutdown` then `dotnet test` to verify everything compiles and existing tests still pass.

- [x] 9. Unit tests for FormatSpecifierHelper
  - [x] 9.1 Create `FormatSpecifierHelperTests` in `Oproto.FluentDynamoDb.UnitTests/Utility/`
    - Test `HasAnyFormatSpecifier` returns true for `{0:yyyy-MM-dd}#{1}`
    - Test `HasAnyFormatSpecifier` returns false for `{0}#{1}`
    - Test `HasAnyFormatSpecifier` returns false for null/empty
    - Test `HasFormatSpecifierForIndex` returns true for correct index
    - Test `HasFormatSpecifierForIndex` returns false for index without specifier
    - Test `GetIndicesWithFormatSpecifiers` returns correct set for mixed formats
    - Test format specifiers with colons in them (e.g., `{0:HH:mm:ss}`)
    - _Requirements: 3.1, 3.2, 4.1, 5.4_

  - [x] 9.2 Write property tests for FormatSpecifierHelper
    - **Property 1: Discriminator Pattern Replaces All Placeholders** (applied to helper detection)
    - **Validates: Requirements 1.1, 1.2, 1.3**
    - Generate random format strings with 1-5 placeholders (with and without specifiers), verify `GetIndicesWithFormatSpecifiers` returns all indices that have specifiers
    - Create in `Oproto.FluentDynamoDb.UnitTests/Utility/FormatSpecifierHelperPropertyTests.cs`

- [x] 10. Unit tests for EntityAnalyzer format specifier fixes
  - [x] 10.1 Create `EntityAnalyzer_FormatSpecifierTests` in `Oproto.FluentDynamoDb.SourceGenerator.UnitTests/Analysis/`
    - Test `DeriveDiscriminatorPattern` with `{0:yyyy-MM-dd}#{1}` produces `*#*`
    - Test `DeriveDiscriminatorPattern` with `{0:D4}#{1}` produces `*#*`
    - Test `DeriveDiscriminatorPattern` with `{0:HH:mm:ss}#{1}` produces `*#*` (colons in specifier)
    - Test `DeriveDiscriminatorPattern` with `{0}#{1}` still produces `*#*` (backwards compat)
    - Test `DeriveDiscriminatorPattern` returns null when pattern starts with `*`
    - Test `ValidateComputedKeyFormat` correctly counts 2 placeholders for `{0:yyyy-MM-dd}#{1}` with 2 source properties
    - Test `ValidateComputedKeyFormat` emits FDDB090 for `{0:D4}` with 2 source properties
    - Test `ValidateComputedKeyFormat` handles repeated indices `{0:D4}#{0:G}#{1}` (distinct count = 2)
    - Test `ValidateComputedKeyFormat` emits diagnostic for `{abc:format}`
    - _Requirements: 1.1, 1.2, 1.3, 1.4, 1.5, 2.1, 2.2, 2.3, 2.4, 2.5, 7.1, 7.2, 7.3, 7.4, 7.5_

  - [x] 10.2 Write property tests for discriminator pattern derivation
    - **Property 1: Discriminator Pattern Replaces All Placeholders**
    - **Validates: Requirements 1.1, 1.2, 1.3, 1.4**
    - Generate format strings with random separators, 1-5 placeholders, random format specifiers; verify all placeholders replaced with `*` and separators preserved
    - Create in `Oproto.FluentDynamoDb.SourceGenerator.UnitTests/Analysis/EntityAnalyzer_FormatSpecifierPropertyTests.cs`

  - [x] 10.3 Write property tests for placeholder count extraction
    - **Property 3: Placeholder Count Extraction Correctness**
    - **Validates: Requirements 2.1, 2.4, 7.5**
    - Generate format strings with mixed specifiers, verify placeholder count equals `max(index) + 1`
    - Add to `Oproto.FluentDynamoDb.SourceGenerator.UnitTests/Analysis/EntityAnalyzer_FormatSpecifierPropertyTests.cs`

  - [x] 10.4 Write property tests for invalid placeholder index detection
    - **Property 5: Invalid Placeholder Index Detection**
    - **Validates: Requirements 2.5, 7.2**
    - Generate placeholders with non-numeric index portions, verify diagnostic emitted
    - Add to `Oproto.FluentDynamoDb.SourceGenerator.UnitTests/Analysis/EntityAnalyzer_FormatSpecifierPropertyTests.cs`

- [x] 11. Unit tests for KeysGenerator and MapperGenerator format specifier handling
  - [x] 11.1 Create `KeysGenerator_FormatSpecifierTests` in `Oproto.FluentDynamoDb.SourceGenerator.UnitTests/Generators/`
    - Test generated code for `{0:yyyy-MM-dd}#{1}` emits `(object)eventDate` for index 0 and pre-stringified for index 1
    - Test generated code for `{0:D4}#{1}` emits `(object)priority` for index 0
    - Test generated code for `{0}#{1}` uses `GetValueExpression` for all indices (backwards compat)
    - Test `CultureInfo.InvariantCulture` is included when format specifiers are present
    - Test `CultureInfo.InvariantCulture` is NOT included when no format specifiers
    - _Requirements: 3.1, 3.2, 3.3, 3.4, 3.5, 5.4_

  - [x] 11.2 Create `MapperGenerator_FormatSpecifierTests` in `Oproto.FluentDynamoDb.SourceGenerator.UnitTests/Generators/`
    - Test `ComputeFormatString` injects source property Format into placeholder when no explicit specifier
    - Test `ComputeFormatString` does NOT override explicit format specifier with source property Format
    - Test `ComputeFormatString` treats empty string Format same as null (no injection)
    - Test `GenerateComputedKeyLogic` emits `CultureInfo.InvariantCulture` when specifiers present
    - Test `GenerateComputedKeyLogic` does not emit `CultureInfo.InvariantCulture` when no specifiers
    - _Requirements: 5.1, 5.4, 6.1, 6.2, 6.3, 6.5, 6.6_

  - [x] 11.3 Write property tests for typed value preservation
    - **Property 6: Typed Value Preservation for Format Specifier Indices**
    - **Validates: Requirements 3.1, 3.2, 3.5**
    - Generate format strings, verify indices with specifiers produce `(object)` cast in emitted code
    - Create in `Oproto.FluentDynamoDb.SourceGenerator.UnitTests/Generators/KeysGenerator_FormatSpecifierPropertyTests.cs`

  - [x] 11.4 Write property tests for source property format injection
    - **Property 10: Source Property Format Injection**
    - **Validates: Requirements 6.1, 6.2, 6.3, 6.6**
    - Generate combinations of source properties with/without Format and computed format strings with/without explicit specifiers, verify injection logic
    - Create in `Oproto.FluentDynamoDb.SourceGenerator.UnitTests/Generators/MapperGenerator_FormatSpecifierPropertyTests.cs`

  - [x] 11.5 Write property tests for explicit specifier precedence
    - **Property 11: Explicit Specifier Precedence Over Source Property Format**
    - **Validates: Requirements 3.5, 6.2**
    - Generate cases with both explicit and source Format, verify explicit always wins
    - Add to `Oproto.FluentDynamoDb.SourceGenerator.UnitTests/Generators/MapperGenerator_FormatSpecifierPropertyTests.cs`

- [x] 12. Unit tests for UpdateExpressionTranslator format specifier handling
  - [x] 12.1 Create `UpdateExpressionTranslator_FormatSpecifierTests` in `Oproto.FluentDynamoDb.UnitTests/Expressions/`
    - Test recomputation with `{0:yyyy-MM-dd}#{1}` and DateTime 2024-03-15 + "CategoryA" produces `2024-03-15#CategoryA`
    - Test recomputation with `{0:D4}#{1}` and int 42 + "Name" produces `0042#Name`
    - Test recomputation with `{0:G}#{1}` and enum Active + "id123" produces `Active#id123`
    - Test recomputation with `{0}#{1}` (no specifiers) still calls `.ToString()` on values (backwards compat)
    - Test null source value with format specifiers produces empty string substitution
    - Test recomputation uses InvariantCulture (verify with culture-sensitive format like `{0:N2}`)
    - _Requirements: 4.1, 4.2, 4.3, 4.4, 5.1, 5.2, 5.3, 5.4, 5.5_

  - [x] 12.2 Write property tests for update recomputation
    - **Property 8: Update Recomputation Produces Correct Formatted Output**
    - **Validates: Requirements 4.1, 4.2, 5.4**
    - Generate typed values + format strings with specifiers, verify output matches `string.Format(CultureInfo.InvariantCulture, format, typedValues)`
    - Create in `Oproto.FluentDynamoDb.UnitTests/Expressions/UpdateExpressionTranslator_FormatSpecifierPropertyTests.cs`

- [x] 13. Integration tests for cross-operation consistency
  - [x] 13.1 Create end-to-end integration test: DateOnly with format specifiers
    - Define entity with `[Computed("EventDate", "Category", Format = "{0:yyyy-MM-dd}#{1}")]`
    - Run through source generator to produce code
    - Verify Keys builder, Put mapper, and Update recomputation all produce `2024-03-15#electronics` for same inputs
    - Create in `Oproto.FluentDynamoDb.SourceGenerator.UnitTests/Integration/FormatSpecifierIntegrationTests.cs`
    - _Requirements: 5.1, 5.4_

  - [x] 13.2 Create end-to-end integration test: Int zero-padding with format specifiers
    - Define entity with `[Computed("Priority", "Name", Format = "{0:D4}#{1}")]`
    - Verify all three paths produce `0042#TaskName` for int 42 + "TaskName"
    - Add to `Oproto.FluentDynamoDb.SourceGenerator.UnitTests/Integration/FormatSpecifierIntegrationTests.cs`
    - _Requirements: 5.2_

  - [x] 13.3 Create end-to-end integration test: Source property Format fallback
    - Define entity with `[DynamoDbAttribute("date", Format = "yyyy-MM-dd")]` on source property and `[Computed("EventDate", "Category")]` without explicit format
    - Verify effective format becomes `{0:yyyy-MM-dd}#{1}` and all paths produce correct output
    - Add to `Oproto.FluentDynamoDb.SourceGenerator.UnitTests/Integration/FormatSpecifierIntegrationTests.cs`
    - _Requirements: 6.1, 6.4, 6.5_

  - [x] 13.4 Create end-to-end integration test: Discriminator pattern with format specifiers
    - Define multi-entity table where entities use format specifiers in computed keys
    - Verify entity type resolution still works correctly (discriminator patterns are derived correctly)
    - Add to `Oproto.FluentDynamoDb.SourceGenerator.UnitTests/Integration/FormatSpecifierIntegrationTests.cs`
    - _Requirements: 1.1, 1.5, 5.6_

  - [x] 13.5 Write property tests for cross-operation consistency
    - **Property 9: Cross-Operation Consistency**
    - **Validates: Requirements 5.1, 5.2, 5.3, 5.4**
    - Compare outputs of all three operation paths with same format string and typed inputs
    - Create in `Oproto.FluentDynamoDb.SourceGenerator.UnitTests/Integration/FormatSpecifierConsistencyPropertyTests.cs`

- [x] 14. Checkpoint - Ensure all tests pass
  - Ensure all tests pass, ask the user if questions arise.
  - Run `dotnet build-server shutdown` then `dotnet test` to verify full test suite.

- [x] 15. Documentation and changelog updates
  - [x] 15.1 Create `docs/core-features/ComputedFieldFormatSpecifiers.md`
    - Document format specifier support in computed field format strings
    - Include complete example with DateOnly: `[Computed("EventDate", "Category", Format = "{0:yyyy-MM-dd}#{1}")]` → `2024-03-15#electronics`
    - Include complete example with integers: `[Computed("Priority", "Name", Format = "{0:D4}#{1}")]` → `0042#TaskName`
    - Include complete example with enums: `[Computed("Status", "Id", Format = "{0:G}#{1}")]` → `Active#id123`
    - Document format specifier precedence: (1) explicit specifier in computed format, (2) source property DynamoDbAttribute.Format, (3) default ToString()
    - Include source property Format fallback example with `[DynamoDbAttribute("date", Format = "yyyy-MM-dd")]`
    - Document that `CultureInfo.InvariantCulture` is used for all format specifier paths
    - Note backwards compatibility: existing entities without format specifiers are unaffected
    - _Requirements: 8.1, 8.2, 8.3, 8.4_

  - [x] 15.2 Update `CHANGELOG.md` under `[Unreleased]`
    - Add `### Fixed` entries for: discriminator regex not matching `{N:format}` placeholders, false FDDB090 diagnostics with format specifiers, Keys builder pre-stringification ignoring format specifiers, Update recomputation pre-stringification ignoring format specifiers
    - Add `### Added` entries for: source property DynamoDbAttribute.Format fallback in computed fields, CultureInfo.InvariantCulture usage for format specifier paths
    - Include brief usage examples in changelog entries
    - _Requirements: 8.6_

  - [x] 15.3 Update `docs/DOCUMENTATION_CHANGELOG.md`
    - Add entry with implementation date
    - Category: "New Feature Documentation"
    - Reference the new `docs/core-features/ComputedFieldFormatSpecifiers.md` file
    - Include before/after examples showing format specifier usage
    - Reason: New format specifier support for computed field format strings
    - _Requirements: 8.5_

- [x] 16. Final checkpoint - Ensure all tests pass and documentation is complete
  - Ensure all tests pass, ask the user if questions arise.
  - Verify documentation files are well-formed markdown.
  - Verify CHANGELOG entries follow Keep a Changelog format.
  - Run `dotnet build-server shutdown` then `dotnet test` for final verification.

## Notes

- Tasks marked with `*` are optional and can be skipped for faster MVP
- Each task references specific requirements for traceability
- Checkpoints ensure incremental validation
- Property tests validate universal correctness properties from the design document
- Unit tests validate specific examples and edge cases
- The source generator project must be rebuilt with `dotnet build-server shutdown` after modifications
- `FormatSpecifierHelper` is intentionally duplicated between source generator and runtime projects because the source generator cannot reference the main library at compile time
- All format specifier changes are gated on presence of specifiers — existing entities without format specifiers produce identical output (backwards compatible)
- FsCheck is used for property-based testing, consistent with existing project test patterns

## Task Dependency Graph

```json
{
  "waves": [
    { "id": 0, "tasks": ["1.1", "1.2"] },
    { "id": 1, "tasks": ["2.1", "2.2"] },
    { "id": 2, "tasks": ["4.1"] },
    { "id": 3, "tasks": ["5.1", "5.2"] },
    { "id": 4, "tasks": ["7.1"] },
    { "id": 5, "tasks": ["9.1", "9.2", "10.1", "11.1", "11.2"] },
    { "id": 6, "tasks": ["10.2", "10.3", "10.4", "11.3", "11.4", "11.5", "12.1"] },
    { "id": 7, "tasks": ["12.2", "13.1", "13.2", "13.3", "13.4"] },
    { "id": 8, "tasks": ["13.5"] },
    { "id": 9, "tasks": ["15.1", "15.2", "15.3"] }
  ]
}
```
