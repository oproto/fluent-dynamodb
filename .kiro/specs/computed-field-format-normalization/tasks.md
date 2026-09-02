# Implementation Plan: Computed Field Format Normalization

## Overview

This implementation normalizes all computed field configurations into a single `Format` string at compile time in the source generator, eliminating redundant runtime fields (`Separator`, `Prefix`, `PrefixSeparator`) from `ComputedFieldMetadata`. The runtime `UpdateExpressionTranslator` is updated to use `string.Format(format, values)` exclusively, aligning with the existing Put and Key builder paths.

## Tasks

- [x] 1. Simplify ComputedFieldMetadata runtime model
  - [x] 1.1 Remove Separator, Prefix, and PrefixSeparator properties from ComputedFieldMetadata and add Format property
    - Modify `Oproto.FluentDynamoDb/Metadata/ComputedFieldMetadata.cs`
    - Remove `Separator` (string), `Prefix` (string?), `PrefixSeparator` (string?) properties
    - Add `Format` property of type `string` with default value `"{0}"`
    - Keep `SourceProperties` property unchanged
    - Update XML documentation comments
    - _Requirements: 2.1, 2.2, 2.3_

- [x] 2. Add ComputeFormatString helper to MapperGenerator
  - [x] 2.1 Implement the ComputeFormatString static helper method
    - Add `internal static string ComputeFormatString(ComputedKeyModel computedKey, KeyFormatModel? keyFormat)` to `Oproto.FluentDynamoDb.SourceGenerator/Generators/MapperGenerator.cs`
    - If `computedKey.HasCustomFormat` is true, return `computedKey.Format` directly
    - Otherwise, build format by interleaving `computedKey.Separator` between positional placeholders `{0}`, `{1}`, ..., `{N-1}`
    - If `keyFormat` has a non-empty `Prefix`, prepend `prefix + keySeparator` to the generated placeholders
    - _Requirements: 1.1, 1.2, 1.3, 1.4, 1.5, 1.6_

  - [x] 2.2 Write property test: Format Generation Round-Trip (No Prefix)
    - **Property 1: Format Generation Round-Trip (No Prefix)**
    - **Validates: Requirements 1.1, 1.2, 4.4, 7.1**
    - Create `Oproto.FluentDynamoDb.UnitTests/SourceGenerator/ComputeFormatStringPropertyTests.cs`
    - Use FsCheck.Xunit with `[Property(MaxTest = 100)]`
    - For any separator and N source values (N ≥ 1), verify `string.Format(ComputeFormatString(...), values) == string.Join(separator, values)`

  - [x] 2.3 Write property test: Format Generation Round-Trip (With Prefix)
    - **Property 2: Format Generation Round-Trip (With Prefix)**
    - **Validates: Requirements 1.3, 7.2**
    - For any prefix, keySeparator, computedSeparator, and N source values, verify `string.Format(generatedFormat, values) == prefix + keySeparator + string.Join(computedSeparator, values)`

  - [x] 2.4 Write property test: Explicit Format Pass-Through
    - **Property 3: Explicit Format Pass-Through**
    - **Validates: Requirements 1.4, 1.5, 7.3**
    - For any valid format string with exactly N placeholders ({0} through {N-1}), verify the generator emits it unchanged

  - [x] 2.5 Write property test: Placeholder Count Invariant
    - **Property 4: Placeholder Count Invariant**
    - **Validates: Requirements 1.6, 2.4**
    - For any computed field configuration, verify the generated format string contains exactly N sequential placeholders where N = source property count

- [x] 3. Update MapperGenerator metadata emission
  - [x] 3.1 Modify MapperGenerator to emit Format instead of Separator/Prefix/PrefixSeparator
    - In `Oproto.FluentDynamoDb.SourceGenerator/Generators/MapperGenerator.cs`, locate the `ComputedFieldMetadata` emission block
    - Call `ComputeFormatString(computedKey, property.KeyFormat)` to get the format string
    - Replace emission of `Separator`, `Prefix`, `PrefixSeparator` assignments with a single `Format = "{escapedFormatString}"` assignment
    - Use existing `EscapeString` utility for proper C# string literal escaping
    - _Requirements: 6.1, 6.2, 6.3_

  - [x] 3.2 Write property test: String Escaping Correctness
    - **Property 6: String Escaping Correctness**
    - **Validates: Requirements 6.1, 6.3**
    - For any format string containing backslash, double-quote, or literal curly braces, verify the escaped string literal evaluates to the original format string at runtime

- [x] 4. Checkpoint - Ensure source generator compiles and tests pass
  - Ensure all tests pass, ask the user if questions arise.

- [x] 5. Update UpdateExpressionTranslator to use string.Format
  - [x] 5.1 Replace string.Join + prefix logic with string.Format in ValidateAndProcessComputedFields
    - Modify `Oproto.FluentDynamoDb/Expressions/UpdateExpressionTranslator.cs`
    - In the `ValidateAndProcessComputedFields` method, replace the `string.Join` + prefix concatenation block
    - Build `object[]` array from source property values (substituting `string.Empty` for null)
    - Call `string.Format(cf.Format, parts)` to produce the recomputed value
    - Remove all references to `cf.Separator`, `cf.Prefix`, `cf.PrefixSeparator`
    - _Requirements: 3.1, 3.2, 3.3, 3.4_

  - [x] 5.2 Write unit tests for UpdateExpressionTranslator format normalization
    - Create `Oproto.FluentDynamoDb.UnitTests/Expressions/UpdateExpressionTranslator_FormatNormalizationTests.cs`
    - Test: separator-based config produces correct recomputed value via string.Format
    - Test: explicit format produces correct recomputed value
    - Test: null source value substitutes string.Empty
    - Test: multi-source with prefix produces correct value
    - _Requirements: 3.1, 3.3, 3.4, 5.1, 5.2, 5.3_

  - [x] 5.3 Write property test: Cross-Operation Consistency
    - **Property 5: Cross-Operation Consistency**
    - **Validates: Requirements 3.1, 3.3, 5.1, 5.2, 5.4**
    - For any computed field configuration and ordered set of source values, verify the Update recomputation path produces byte-for-byte identical output to `string.Format(format, values)`

- [x] 6. Add FDDB090 diagnostic for placeholder count mismatch
  - [x] 6.1 Add DiagnosticDescriptor and validation logic for FDDB090
    - Add `ComputedFormatPlaceholderMismatch` descriptor to `Oproto.FluentDynamoDb.SourceGenerator/Analysis/DiagnosticDescriptors.cs` (or equivalent location)
    - Code: `FDDB090`, Severity: Error, Category: `DynamoDb`
    - Message format: `"Computed property '{0}' has format '{1}' with {2} placeholders but {3} source properties"`
    - In `EntityAnalyzer.cs`, when an explicit Format is specified, count placeholders and compare to source property count
    - Emit FDDB090 error diagnostic on mismatch
    - _Requirements: 1.7, 2.4_

  - [x] 6.2 Write unit test for FDDB090 diagnostic
    - Test that a ComputedAttribute with Format="{0}#{1}#{2}" and only 2 source properties triggers FDDB090
    - Test that a matching placeholder count does not trigger the diagnostic
    - _Requirements: 1.7_

- [x] 7. Checkpoint - Full build and test pass
  - Ensure all tests pass, ask the user if questions arise.

- [x] 8. Integration and backwards compatibility tests
  - [x] 8.1 Write integration test: Update produces same result as Put
    - Test end-to-end: configure entity with Separator-based computed field, put entity, update source properties, verify computed field value matches what Put produced
    - _Requirements: 5.1, 5.2_

  - [x] 8.2 Write backwards compatibility test: existing Separator configs produce identical values
    - Test that entities using Separator="#", Separator="_", and Separator with key Prefix all produce byte-for-byte identical computed values after the refactoring
    - Test explicit Format="TENANT#{0}#USER#{1}#" with concrete values produces "TENANT#tenantValue#USER#userValue#"
    - _Requirements: 4.4, 5.3_

- [x] 9. Update documentation
  - [x] 9.1 Update docs/ folder with computed field format normalization documentation
    - Add or update documentation in `/Users/dguisinger/git/oproto-fluent-dynamodb/docs/` explaining the internal change
    - Document that `ComputedFieldMetadata` now uses `Format` instead of `Separator`/`Prefix`/`PrefixSeparator`
    - Note that the user-facing `ComputedAttribute` API is unchanged
    - Include examples of generated format strings for common configurations
    - _Requirements: 4.1, 4.2, 4.3, 4.5_

  - [x] 9.2 Update docs/DOCUMENTATION_CHANGELOG.md
    - Add entry to `/Users/dguisinger/git/oproto-fluent-dynamodb/docs/DOCUMENTATION_CHANGELOG.md`
    - Document the before/after change for `ComputedFieldMetadata` (removed Separator/Prefix/PrefixSeparator, added Format)
    - Follow the entry format: Date, File Path, Before Pattern, After Pattern, Reason
    - _Requirements: 2.3, 6.2_

  - [x] 9.3 Update CHANGELOG.md
    - Add entry to `/Users/dguisinger/git/oproto-fluent-dynamodb/CHANGELOG.md`
    - Under "Changed": ComputedFieldMetadata simplified to use Format string instead of Separator/Prefix/PrefixSeparator
    - Under "Added": FDDB090 diagnostic for format placeholder count mismatch
    - Under "Removed": Separator, Prefix, PrefixSeparator properties from ComputedFieldMetadata (internal breaking change)
    - Follow Keep a Changelog conventions
    - _Requirements: 1.7, 2.3_

- [x] 10. Final checkpoint - Full build and all tests pass
  - Ensure all tests pass, ask the user if questions arise.

## Notes

- Tasks marked with `*` are optional and can be skipped for faster MVP
- Each task references specific requirements for traceability
- Checkpoints ensure incremental validation
- Property tests validate universal correctness properties from the design document using FsCheck
- Unit tests validate specific examples and edge cases
- The `ComputedAttribute` user-facing API is completely unchanged — this is an internal refactoring
- The source generator must be restarted (`dotnet build-server shutdown`) after modifications to pick up changes

## Task Dependency Graph

```json
{
  "waves": [
    { "id": 0, "tasks": ["1.1"] },
    { "id": 1, "tasks": ["2.1"] },
    { "id": 2, "tasks": ["2.2", "2.3", "2.4", "2.5", "3.1"] },
    { "id": 3, "tasks": ["3.2", "5.1"] },
    { "id": 4, "tasks": ["5.2", "5.3", "6.1"] },
    { "id": 5, "tasks": ["6.2"] },
    { "id": 6, "tasks": ["8.1", "8.2"] },
    { "id": 7, "tasks": ["9.1", "9.2", "9.3"] }
  ]
}
```
