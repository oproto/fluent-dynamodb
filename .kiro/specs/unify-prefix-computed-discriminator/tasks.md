# Implementation Plan: Unify Prefix, Computed Key Format, and Discriminator

## Overview

This implementation adds a new analysis phase in `EntityAnalyzer` that computes normalized key formats for non-computed keys, derives discriminator patterns from all key formats, populates the existing `DiscriminatorConfig` and `IndexModel.GsiDiscriminator` structures, and emits compile-time diagnostics (FDDB100–FDDB103) when definitions conflict. The existing `MatchesEntity` code generation requires no changes since auto-derivation populates the same infrastructure.

## Tasks

- [x] 1. Extend PropertyModel and DiscriminatorConfig with new properties
  - [x] 1.1 Add NormalizedKeyFormat and DerivedDiscriminatorPattern to PropertyModel
    - Modify `Oproto.FluentDynamoDb.SourceGenerator/Models/PropertyModel.cs`
    - Add `public string? NormalizedKeyFormat { get; set; }` — nullable, null for non-key properties
    - Add `public string? DerivedDiscriminatorPattern { get; set; }` — nullable, null when format is `"{0}"` or property is not a key
    - Add XML documentation comments as specified in design
    - _Requirements: 11.1, 11.2_

  - [x] 1.2 Add IsAutoDerived flag to DiscriminatorConfig
    - Modify `Oproto.FluentDynamoDb.SourceGenerator/Models/DiscriminatorConfig.cs`
    - Add `public bool IsAutoDerived { get; set; }` with default `false`
    - Add XML documentation comment explaining it distinguishes auto-derived from explicit discriminators
    - _Requirements: 2.6, 5.6, 6.1_

- [x] 2. Implement key format normalization for non-computed keys
  - [x] 2.1 Add ComputeNonComputedKeyFormat helper method to EntityAnalyzer
    - Add `private static string ComputeNonComputedKeyFormat(KeyFormatModel? keyFormat)` to `Oproto.FluentDynamoDb.SourceGenerator/Analysis/EntityAnalyzer.cs`
    - If keyFormat is null or Prefix is null/empty, return `"{0}"`
    - Otherwise return `$"{keyFormat.Prefix}{keyFormat.Separator}{{0}}"` (Separator defaults to `"#"` when null)
    - _Requirements: 1.1, 1.2, 1.3, 1.5_

  - [x] 2.2 Add ComputeNormalizedKeyFormats orchestration method to EntityAnalyzer
    - Add `private void ComputeNormalizedKeyFormats(EntityModel entity)` to EntityAnalyzer
    - For each property that is PK or SK: if property has ComputedKey, use `MapperGenerator.ComputeFormatString(computedKey, keyFormat)`; otherwise call `ComputeNonComputedKeyFormat(keyFormat)`
    - Store result in `property.NormalizedKeyFormat`
    - _Requirements: 1.1, 1.2, 1.3, 1.4, 11.1, 11.4_

  - [x] 2.3 Write property test: Non-Computed Key Format Derivation (Property 1)
    - **Property 1: Non-Computed Key Format Derivation**
    - **Validates: Requirements 1.1, 1.2, 1.5**
    - Create `Oproto.FluentDynamoDb.UnitTests/SourceGenerator/KeyFormatNormalizationPropertyTests.cs`
    - Use FsCheck.Xunit with `[Property(MaxTest = 100)]`
    - For any non-empty prefix and any separator (including empty), verify `string.Format(derivedFormat, value) == prefix + separator + value`

  - [x] 2.4 Write unit tests for ComputeNonComputedKeyFormat
    - Test: Prefix="ORDER", Separator="#" produces `"ORDER#{0}"`
    - Test: Prefix="USER", Separator="_" produces `"USER_{0}"`
    - Test: Prefix="A", Separator="" produces `"A{0}"`
    - Test: Prefix=null produces `"{0}"`
    - Test: Prefix="" produces `"{0}"`
    - _Requirements: 1.1, 1.2, 1.3, 1.5_

- [x] 3. Implement discriminator pattern derivation
  - [x] 3.1 Add DeriveDiscriminatorPattern helper method to EntityAnalyzer
    - Add `internal static string? DeriveDiscriminatorPattern(string normalizedKeyFormat)` to EntityAnalyzer
    - Replace all `{N}` placeholders with `*` using `Regex.Replace(format, @"\{\d+\}", "*")`
    - If the resulting pattern is just `"*"` or starts with `*` (no useful fixed prefix), return null
    - Otherwise return the pattern
    - _Requirements: 2.1, 2.2, 2.3, 2.4, 2.5_

  - [x] 3.2 Add DeriveDiscriminatorPatterns orchestration method to EntityAnalyzer
    - Add `private void DeriveDiscriminatorPatterns(EntityModel entity)` to EntityAnalyzer
    - For each property with non-null `NormalizedKeyFormat`, call `DeriveDiscriminatorPattern` and store result in `property.DerivedDiscriminatorPattern`
    - _Requirements: 2.1, 11.2_

  - [x] 3.3 Write property test: Discriminator Pattern Derivation from Format (Property 2)
    - **Property 2: Discriminator Pattern Derivation from Format**
    - **Validates: Requirements 2.1, 2.2, 2.3, 2.4**
    - Create `Oproto.FluentDynamoDb.UnitTests/SourceGenerator/DiscriminatorDerivationPropertyTests.cs`
    - For any format string with N placeholders interleaved with literal segments, verify derived pattern == format with every `{N}` replaced by `*`

  - [x] 3.4 Write unit tests for DeriveDiscriminatorPattern
    - Test: `"ORDER#{0}"` → `"ORDER#*"`
    - Test: `"TENANT#{0}#USER#{1}"` → `"TENANT#*#USER#*"`
    - Test: `"TENANT#{0}#USER#{1}#"` → `"TENANT#*#USER#*#"`
    - Test: `"{0}"` → null (trivial)
    - Test: `"{0}#{1}"` → null (starts with wildcard, no useful discrimination)
    - _Requirements: 2.1, 2.2, 2.3, 2.4, 2.5_

- [x] 4. Checkpoint - Ensure source generator compiles and core derivation tests pass
  - Ensure all tests pass, ask the user if questions arise.

- [x] 5. Implement discriminator selection and auto-derivation
  - [x] 5.1 Add ApplyAutoDerivedDiscriminator method to EntityAnalyzer
    - Add `private void ApplyAutoDerivedDiscriminator(EntityModel entity)` to EntityAnalyzer
    - If entity already has a valid explicit discriminator, return (do not override)
    - Try sort key first: if SK property has non-null `DerivedDiscriminatorPattern`, create and assign `DiscriminatorConfig` with `IsAutoDerived = true`
    - Fall back to partition key: if PK property has non-null `DerivedDiscriminatorPattern`, create and assign
    - Use `DiscriminatorAnalyzer.DeterminePatternStrategy(pattern)` to set the strategy
    - _Requirements: 2.6, 2.7, 2.8, 2.9, 7.1, 7.3, 7.4_

  - [x] 5.2 Add ApplyAutoDerivedGsiDiscriminator method to EntityAnalyzer
    - Add `private void ApplyAutoDerivedGsiDiscriminator(EntityModel entity)` to EntityAnalyzer
    - For each GSI in `entity.Indexes` where `IsGsi == true` and `GsiDiscriminator == null`:
    - Find the property with a `GsiPartitionKeys` entry matching the index name
    - If that property has non-null `DerivedDiscriminatorPattern`, populate `index.GsiDiscriminator` with `IsAutoDerived = true`
    - _Requirements: 9.1, 9.5, 9.6_

  - [x] 5.3 Write property test: Discriminator Selection Priority (Property 3)
    - **Property 3: Discriminator Selection Priority**
    - **Validates: Requirements 2.6, 2.8, 2.9**
    - Create `Oproto.FluentDynamoDb.UnitTests/SourceGenerator/DiscriminatorSelectionPropertyTests.cs`
    - For any entity without explicit discriminator where SK has a non-null derived pattern, verify entity's auto-derived discriminator uses SK's attribute name and pattern

  - [x] 5.4 Write unit tests for discriminator selection
    - Test: SK preferred over PK when both have patterns
    - Test: Falls back to PK when SK pattern is null
    - Test: Explicit discriminator not overridden by auto-derived
    - Test: No discriminator when both PK and SK are trivial (`"{0}"`)
    - _Requirements: 2.6, 2.7, 2.8, 2.9, 7.1, 7.3_

  - [x] 5.5 Write property test: GSI Discriminator Auto-Derivation (Property 9)
    - **Property 9: GSI Discriminator Auto-Derivation**
    - **Validates: Requirements 9.1, 9.5, 9.6**
    - Create `Oproto.FluentDynamoDb.UnitTests/SourceGenerator/GsiDiscriminatorDerivationPropertyTests.cs`
    - For any GSI PK property with non-null derived pattern and no explicit GsiDiscriminator, verify IndexModel.GsiDiscriminator is populated with correct property name and pattern

  - [x] 5.6 Write unit tests for GSI discriminator auto-derivation
    - Test: GSI PK with prefix auto-derives discriminator
    - Test: GSI PK without prefix (trivial pattern) does not populate GsiDiscriminator
    - Test: Explicit GsiDiscriminator not overridden
    - _Requirements: 9.1, 9.5, 9.6_

- [x] 6. Implement conflict detection diagnostics
  - [x] 6.1 Add FDDB100–FDDB103 diagnostic descriptors
    - Modify `Oproto.FluentDynamoDb.SourceGenerator/Diagnostics/DiagnosticDescriptors.cs`
    - Add `PrefixFormatConflict` (FDDB100, Error): prefix conflicts with explicit computed format
    - Add `DiscriminatorKeyFormatConflict` (FDDB101, Error): explicit discriminator pattern conflicts with derived
    - Add `OverlappingAutoDerivedPatterns` (FDDB102, Warning): overlapping auto-derived patterns
    - Add `RedundantExplicitDiscriminator` (FDDB103, Info): explicit pattern matches derived
    - All in category "DynamoDb", enabled by default
    - _Requirements: 3.2, 4.2, 5.2, 6.2_

  - [x] 6.2 Implement ValidatePrefixFormatConsistency (FDDB100)
    - Add `private void ValidatePrefixFormatConsistency(EntityModel entity)` to EntityAnalyzer
    - For each key property with non-empty Prefix and a ComputedKey with HasCustomFormat:
    - Check if format starts with `"{Prefix}{Separator}"` (ordinal comparison)
    - Emit FDDB100 diagnostic if it doesn't match
    - _Requirements: 3.1, 3.4, 3.5, 3.6, 3.7_

  - [x] 6.3 Implement ValidateExplicitVsDerivedDiscriminator (FDDB101)
    - Add `private void ValidateExplicitVsDerivedDiscriminator(EntityModel entity)` to EntityAnalyzer
    - If entity has non-auto-derived discriminator with a Pattern (not ExactMatch):
    - Find the key property whose DynamoDbAttribute name matches DiscriminatorProperty
    - If that property's DerivedDiscriminatorPattern is not null and differs from the explicit pattern, emit FDDB101
    - _Requirements: 4.1, 4.4, 4.5_

  - [x] 6.4 Implement DetectRedundantExplicitDiscriminator (FDDB103)
    - Add `private void DetectRedundantExplicitDiscriminator(EntityModel entity)` to EntityAnalyzer
    - If entity has non-auto-derived discriminator that is not ExactMatch:
    - Find the key property whose DynamoDbAttribute name matches DiscriminatorProperty
    - If that property's DerivedDiscriminatorPattern exactly matches the explicit pattern, emit FDDB103
    - _Requirements: 6.1, 6.4, 6.5, 6.6_

  - [x] 6.5 Enhance PatternOverlapAnalyzer for FDDB102
    - Modify `Oproto.FluentDynamoDb.SourceGenerator/Analysis/PatternOverlapAnalyzer.cs`
    - In the overlap comparison loop, after detecting overlap with different specificity:
    - If both entities have `IsAutoDerived == true`, emit FDDB102 warning
    - Existing exclusion guard logic continues unchanged
    - _Requirements: 5.1, 5.3, 5.4, 5.6, 5.7_

  - [x] 6.6 Write property test: FDDB100 Conflict Detection (Property 4)
    - **Property 4: FDDB100 Conflict Detection**
    - **Validates: Requirements 3.1, 3.4, 3.5, 3.6, 3.7**
    - Create `Oproto.FluentDynamoDb.UnitTests/SourceGenerator/FDDB100ConflictPropertyTests.cs`
    - For any key with non-empty Prefix and explicit Format, verify FDDB100 is emitted iff Format does not start with `"{Prefix}{Separator}"`

  - [x] 6.7 Write property test: FDDB101 Conflict Detection (Property 5)
    - **Property 5: FDDB101 Conflict Detection**
    - **Validates: Requirements 4.1, 4.4, 4.5**
    - Create `Oproto.FluentDynamoDb.UnitTests/SourceGenerator/FDDB101ConflictPropertyTests.cs`
    - For any entity with explicit DiscriminatorPattern matching a key property's attribute name, verify FDDB101 is emitted iff the explicit pattern differs from the derived pattern (and derived is not null)

  - [x] 6.8 Write property test: FDDB103 Redundancy Detection (Property 6)
    - **Property 6: FDDB103 Redundancy Detection**
    - **Validates: Requirements 6.1, 6.4, 6.6**
    - Create `Oproto.FluentDynamoDb.UnitTests/SourceGenerator/FDDB103RedundancyPropertyTests.cs`
    - For any entity with explicit DiscriminatorPattern (not DiscriminatorValue) matching a key property's attribute name, verify FDDB103 is emitted iff the explicit pattern exactly matches the derived pattern

  - [x] 6.9 Write property test: FDDB102 Emission Constraint (Property 8)
    - **Property 8: FDDB102 Emission Constraint**
    - **Validates: Requirements 5.1, 5.6**
    - Create `Oproto.FluentDynamoDb.UnitTests/SourceGenerator/FDDB102OverlapPropertyTests.cs`
    - For any pair of overlapping patterns with different specificity, verify FDDB102 is emitted only when both are auto-derived

  - [x] 6.10 Write unit tests for diagnostic emissions
    - Test: FDDB100 emitted when Prefix="ORDER" but Format="TENANT#{0}"
    - Test: FDDB100 not emitted when Prefix matches format start
    - Test: FDDB100 not emitted when no prefix or no custom format
    - Test: FDDB101 emitted when explicit pattern != derived pattern on same attribute
    - Test: FDDB101 not emitted when derived is null (trivial key)
    - Test: FDDB102 emitted for auto-derived overlap, not for explicit overlap
    - Test: FDDB103 emitted when explicit matches derived exactly
    - Test: FDDB103 not emitted for DiscriminatorValue (exact match)
    - _Requirements: 3.1, 3.5, 3.6, 3.7, 4.1, 4.5, 5.1, 5.6, 6.1, 6.6_

- [x] 7. Checkpoint - Ensure all diagnostics compile and tests pass
  - Ensure all tests pass, ask the user if questions arise.

- [x] 8. Wire analysis phases into EntityAnalyzer.AnalyzeEntity
  - [x] 8.1 Integrate new analysis methods into EntityAnalyzer.AnalyzeEntity ordering
    - Modify `Oproto.FluentDynamoDb.SourceGenerator/Analysis/EntityAnalyzer.cs`
    - After `ExtractProperties`, `ExtractIndexes`, `ExtractRelationships` and before `ValidateEntityModel`:
    - Add calls in order: `ComputeNormalizedKeyFormats(entityModel)` → `DeriveDiscriminatorPatterns(entityModel)` → `ValidatePrefixFormatConsistency(entityModel)` → `ApplyAutoDerivedDiscriminator(entityModel)` → `ApplyAutoDerivedGsiDiscriminator(entityModel)` → `ValidateExplicitVsDerivedDiscriminator(entityModel)` → `DetectRedundantExplicitDiscriminator(entityModel)`
    - _Requirements: 11.4_

  - [x] 8.2 Write property test: NormalizedKeyFormat Population Completeness (Property 10)
    - **Property 10: NormalizedKeyFormat Population Completeness**
    - **Validates: Requirements 11.1, 11.4**
    - Create `Oproto.FluentDynamoDb.UnitTests/SourceGenerator/KeyFormatPopulationPropertyTests.cs`
    - For any entity analyzed by EntityAnalyzer, verify every property annotated with PartitionKey or SortKey has non-null NormalizedKeyFormat after analysis

- [x] 9. Checkpoint - Full build pass with integrated analysis phases
  - Ensure all tests pass, ask the user if questions arise.

- [x] 10. Backwards compatibility and integration tests
  - [x] 10.1 Write property test: Backwards Compatibility of Explicit Discriminators (Property 7)
    - **Property 7: Backwards Compatibility of Explicit Discriminators**
    - **Validates: Requirements 10.5, 10.7**
    - Create `Oproto.FluentDynamoDb.UnitTests/SourceGenerator/BackwardsCompatibilityPropertyTests.cs`
    - For any entity with explicit DiscriminatorProperty and DiscriminatorPattern/DiscriminatorValue, verify MatchesEntity generates identical logic with and without auto-derivation

  - [x] 10.2 Write integration test: Existing entities with explicit discriminator produce same output
    - Verify existing entity definitions with explicit DiscriminatorProperty/DiscriminatorPattern compile with identical generated MatchesEntity behavior
    - _Requirements: 10.5, 10.7, 10.8_

  - [x] 10.3 Write integration test: New entity with prefix-only auto-derives correct MatchesEntity
    - Create entity with `[SortKey(Prefix = "ORDER")]` and no explicit discriminator
    - Verify MatchesEntity checks `item["sk"].S.StartsWith("ORDER#")`
    - _Requirements: 2.6, 8.1, 8.2_

  - [x] 10.4 Write integration test: Multi-entity table with overlapping patterns produces exclusion guards
    - Create two entities sharing a table where auto-derived patterns overlap
    - Verify FDDB102 warning is emitted
    - Verify exclusion guards are generated in the less-specific entity's MatchesEntity
    - _Requirements: 5.1, 5.3, 8.4_

  - [x] 10.5 Write integration test: Single-entity table derives pattern but no MatchesEntity change
    - Verify single-entity table with prefix has NormalizedKeyFormat and DerivedDiscriminatorPattern populated
    - Verify MatchesEntity behavior remains key-presence-only for single-entity tables
    - _Requirements: 2.10, 10.9_

- [x] 11. Update documentation and changelog
  - [x] 11.1 Update CHANGELOG.md
    - Add entry under "Added": Auto-derivation of discriminator patterns from key formats
    - Add entry under "Added": FDDB100, FDDB101, FDDB102, FDDB103 diagnostics
    - Add entry under "Added": `NormalizedKeyFormat` and `DerivedDiscriminatorPattern` on PropertyModel
    - Add entry under "Added": `IsAutoDerived` flag on DiscriminatorConfig
    - Note backwards compatibility: existing explicit discriminators unchanged
    - _Requirements: 10.1, 10.2, 10.3, 10.4, 10.5, 10.6_

  - [x] 11.2 Update docs/DOCUMENTATION_CHANGELOG.md
    - Document the new diagnostics FDDB100–FDDB103 with before/after patterns
    - Document that `DiscriminatorPattern` is now auto-derivable from key format
    - _Requirements: 6.2, 6.3_

- [x] 12. Final checkpoint - Full build and all tests pass
  - Ensure all tests pass, ask the user if questions arise.

## Notes

- Tasks marked with `*` are optional and can be skipped for faster MVP
- Each task references specific requirements for traceability
- Checkpoints ensure incremental validation
- Property tests validate universal correctness properties from the design document using FsCheck
- Unit tests validate specific examples and edge cases
- The source generator must be restarted (`dotnet build-server shutdown`) after modifications to pick up changes
- This feature depends on the completed "Computed Field Format Normalization" feature — `MapperGenerator.ComputeFormatString` is used for computed keys
- The existing `MatchesEntity` code generation path requires NO changes; auto-derivation populates the same `DiscriminatorConfig` structure
- FDDB102 is emitted by `PatternOverlapAnalyzer` (cross-entity analysis), not inside `EntityAnalyzer`

## Task Dependency Graph

```json
{
  "waves": [
    { "id": 0, "tasks": ["1.1", "1.2"] },
    { "id": 1, "tasks": ["2.1", "6.1"] },
    { "id": 2, "tasks": ["2.2", "2.3", "2.4"] },
    { "id": 3, "tasks": ["3.1"] },
    { "id": 4, "tasks": ["3.2", "3.3", "3.4"] },
    { "id": 5, "tasks": ["5.1", "5.2", "6.2", "6.3", "6.4"] },
    { "id": 6, "tasks": ["5.3", "5.4", "5.5", "5.6", "6.5"] },
    { "id": 7, "tasks": ["6.6", "6.7", "6.8", "6.9", "6.10"] },
    { "id": 8, "tasks": ["8.1"] },
    { "id": 9, "tasks": ["8.2", "10.1", "10.2", "10.3", "10.4", "10.5"] },
    { "id": 10, "tasks": ["11.1", "11.2"] }
  ]
}
```
