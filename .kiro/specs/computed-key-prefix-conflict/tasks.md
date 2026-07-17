# Implementation Plan: Computed Key Prefix Conflict (FDDB125)

## Overview

Add FDDB125 error diagnostic to the source generator that fires when a property has both `[Computed]` and a `Prefix` on its `[PartitionKey]` or `[SortKey]` attribute. Update existing test entities that use the now-invalid pattern, then verify correctness via property-based and unit tests.

## Tasks

- [x] 1. Add FDDB125 DiagnosticDescriptor and validation check
  - [x] 1.1 Add FDDB125 DiagnosticDescriptor to DiagnosticDescriptors.cs
    - Add new `ComputedKeyPrefixConflict` static readonly DiagnosticDescriptor after FDDB124 in `Oproto.FluentDynamoDb.SourceGenerator/Diagnostics/DiagnosticDescriptors.cs`
    - Code: `"FDDB125"`
    - Title: `"Computed key property has redundant Prefix"`
    - Message format: `"Property '{0}' is a computed key with Prefix = \"{1}\" configured on its key attribute. Prefixes are not applied to computed keys — remove the Prefix and embed it in the [Computed] Format if the prefix should appear in the stored value"`
    - Category: `"DynamoDb"`
    - Severity: `DiagnosticSeverity.Error`
    - Enabled by default: `true`
    - Description: provide guidance about removing Prefix or using Format
    - Help link: `string.Format(DiagnosticHelpLinks.BaseUrlFormat, "FDDB125")`
    - Run `dotnet build-server shutdown` then `dotnet build` to verify compilation
    - _Requirements: 3.1, 3.2, 3.3, 3.4, 3.5_

  - [x] 1.2 Add validation check in EntityAnalyzer.ValidatePropertyModel()
    - In `Oproto.FluentDynamoDb.SourceGenerator/Analysis/EntityAnalyzer.cs`, add check after the existing FDDB121 block in `ValidatePropertyModel()`
    - Condition: `propertyModel.IsComputed && (propertyModel.IsPartitionKey || propertyModel.IsSortKey) && !string.IsNullOrEmpty(propertyModel.KeyFormat?.Prefix)`
    - Action: `ReportDiagnostic(DiagnosticDescriptors.ComputedKeyPrefixConflict, propertyModel.PropertyDeclaration?.GetLocation(), propertyModel.PropertyName, propertyModel.KeyFormat!.Prefix!)`
    - Do NOT return early — continue processing remaining properties (non-halting, per Requirement 1.3)
    - Run `dotnet build-server shutdown` then `dotnet build` to verify compilation
    - _Requirements: 1.1, 1.3, 2.1, 2.3_

- [x] 2. Update existing test entities to remove invalid Prefix + Computed configurations
  - [x] 2.1 Remove Prefix from ComputedPkWithPrefixTestEntity
    - In `Oproto.FluentDynamoDb.UnitTests/Properties/ComputedKeyExclusionPropertyTests.cs`, find `ComputedPkWithPrefixTestEntity` class definition
    - Change `[PartitionKey(Prefix = "EVT")]` to `[PartitionKey]` on the computed PK property
    - Verify the entity still compiles and tests that reference it still make sense (the behavioral assertion — computed keys don't receive prefix — is now trivially satisfied by having no prefix configured)
    - _Requirements: 5.1, 5.3_

  - [x] 2.2 Remove Prefix from NonComputedPkComputedSkTestEntity
    - In `Oproto.FluentDynamoDb.UnitTests/Requests/PutKeyPrefix/PutComputedAndGsiIntegrationTests.cs`, find `NonComputedPkComputedSkTestEntity` class definition
    - Change `[SortKey(Prefix = "LOC")]` to `[SortKey]` on the computed SK property
    - Update assertions in `PutAsync_NonComputedPk_ComputedSk_OnlyPkGetsPrefix` and related tests to reflect the removed prefix on the computed SK (the computed SK value should still pass through unchanged; the non-computed PK should still get its prefix applied)
    - _Requirements: 5.2, 5.4_

  - [x] 2.3 Verify existing tests pass after entity updates
    - Run `dotnet build-server shutdown` then `dotnet test --filter "ComputedKeyExclusion|PutComputedAndGsi"` to confirm existing property-based tests and integration tests pass
    - The behavioral property (computed keys pass through unchanged) remains the same
    - _Requirements: 5.3, 5.4_

- [x] 3. Checkpoint - Ensure build and existing tests pass
  - Run `dotnet build-server shutdown` then `dotnet test` for full test suite
  - Ensure all tests pass, ask the user if questions arise.

- [x] 4. Write unit tests for FDDB125
  - [x] 4.1 Write descriptor structure unit test
    - In `Oproto.FluentDynamoDb.SourceGenerator.UnitTests`, create or add to an appropriate test file
    - Verify FDDB125 descriptor has: code "FDDB125", severity Error, category "DynamoDb", isEnabledByDefault true, helpLinkUri contains "FDDB125"
    - _Requirements: 3.1, 3.2, 3.3, 3.4, 3.5_

  - [x] 4.2 Write non-halting unit test
    - Create test entity with 2+ computed key properties each having a Prefix
    - Run source generator and verify both FDDB125 diagnostics are emitted (not just the first)
    - Confirms the analyzer continues processing after reporting FDDB125
    - _Requirements: 1.3_

  - [x] 4.3 Write no-false-positive unit test for non-key computed property
    - Create test entity with `[Computed]` on a property that is NOT `[PartitionKey]` or `[SortKey]`
    - Run source generator and verify FDDB125 is NOT emitted
    - _Requirements: 4.4_

  - [x] 4.4 Write property-based test for Property 1 (computed key + prefix always emits FDDB125)
    - **Property 1: Computed key with prefix always emits FDDB125**
    - **Validates: Requirements 1.1, 1.2, 2.1, 2.2, 2.3**
    - Use FsCheck to generate random non-empty prefix strings and property names
    - Construct entity source with computed key + prefix (both with and without explicit Format on `[Computed]`)
    - Run source generator analyzer
    - Verify FDDB125 is emitted with Error severity
    - Verify diagnostic message contains the property name and the configured prefix value
    - Minimum 100 iterations
    - _Requirements: 1.1, 1.2, 2.1, 2.2, 2.3_

  - [x] 4.5 Write property-based test for Property 2 (no false positives)
    - **Property 2: No false positives for non-conflicting configurations**
    - **Validates: Requirements 4.1, 4.2, 4.3, 4.4**
    - Use FsCheck to generate random entities in one of four categories: (a) key with prefix but NOT computed, (b) computed key with no prefix, (c) computed key with empty/null prefix, (d) computed non-key property
    - Run source generator analyzer
    - Verify FDDB125 is NOT emitted in any of these cases
    - Minimum 100 iterations
    - _Requirements: 4.1, 4.2, 4.3, 4.4_

- [x] 5. Final checkpoint - Ensure all tests pass
  - Run `dotnet build-server shutdown` then `dotnet test` for full test suite
  - Ensure all tests pass, ask the user if questions arise.

## Notes

- Tasks marked with `*` are optional and can be skipped for faster MVP
- Remember to run `dotnet build-server shutdown` before builds when source generator changes are made
- The main risk is updating existing test entities (2.1, 2.2) — the behavioral properties they validate remain the same, but assertions referencing the removed prefix may need updating
- Property tests validate universal correctness properties from the design document
- Unit tests validate specific examples and edge cases
- Each task references specific requirements for traceability

## Task Dependency Graph

```json
{
  "waves": [
    { "id": 0, "tasks": ["1.1"] },
    { "id": 1, "tasks": ["1.2"] },
    { "id": 2, "tasks": ["2.1", "2.2"] },
    { "id": 3, "tasks": ["2.3"] },
    { "id": 4, "tasks": ["4.1", "4.2", "4.3"] },
    { "id": 5, "tasks": ["4.4", "4.5"] }
  ]
}
```
