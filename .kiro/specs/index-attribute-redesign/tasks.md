# Implementation Plan: Index Attribute Redesign

## Overview

Replace `[GlobalSecondaryIndex]` and `[LocalSecondaryIndex]` attributes with three new self-describing attributes: `[GsiPartitionKey]`, `[GsiSortKey]`, and `[LsiSortKey]`. The implementation follows an incremental approach — new attributes and models are created first (coexisting temporarily with old ones), then the source generator is updated, then old code is deleted, and finally all tests, examples, and documentation are updated.

## Tasks

- [ ] 1. Create new attribute classes
  - [ ] 1.1 Create `GsiPartitionKeyAttribute` in `Oproto.FluentDynamoDb/Attributes/GsiPartitionKeyAttribute.cs`
    - `[AttributeUsage(AttributeTargets.Property, AllowMultiple = true)]`
    - Required positional `indexName` constructor parameter
    - Optional `Name`, `ProjectionType` (default `All`), `DiscriminatorProperty`, `DiscriminatorValue`, `DiscriminatorPattern` properties
    - _Requirements: 1.1, 1.2, 1.3, 1.4, 1.5, 1.6_

  - [ ] 1.2 Create `GsiSortKeyAttribute` in `Oproto.FluentDynamoDb/Attributes/GsiSortKeyAttribute.cs`
    - `[AttributeUsage(AttributeTargets.Property, AllowMultiple = true)]`
    - Required positional `indexName` constructor parameter
    - Optional `Name`, `ProjectionType` (default `All`) properties
    - No discriminator properties (discriminator belongs on partition key)
    - _Requirements: 2.1, 2.2, 2.3, 2.4, 2.6_

  - [ ] 1.3 Create `LsiSortKeyAttribute` in `Oproto.FluentDynamoDb/Attributes/LsiSortKeyAttribute.cs`
    - `[AttributeUsage(AttributeTargets.Property, AllowMultiple = true)]`
    - Required positional `indexName` constructor parameter
    - Optional `Name`, `ProjectionType` (default `All`) properties
    - No discriminator properties (LSIs share base table PK)
    - _Requirements: 3.1, 3.2, 3.3, 3.4, 3.6_

- [ ] 2. Create new per-property models in the source generator
  - [ ] 2.1 Create `GsiPartitionKeyModel` in `Oproto.FluentDynamoDb.SourceGenerator/Models/GsiPartitionKeyModel.cs`
    - Properties: `IndexName`, `CustomName`, `ProjectionType` (default `All`), `Discriminator` (`DiscriminatorConfig?`)
    - _Requirements: 5.1_

  - [ ] 2.2 Create `GsiSortKeyModel` in `Oproto.FluentDynamoDb.SourceGenerator/Models/GsiSortKeyModel.cs`
    - Properties: `IndexName`, `CustomName`, `ProjectionType` (default `All`)
    - _Requirements: 5.2_

  - [ ] 2.3 Create `LsiSortKeyModel` in `Oproto.FluentDynamoDb.SourceGenerator/Models/LsiSortKeyModel.cs`
    - Properties: `IndexName`, `CustomName`, `ProjectionType` (default `All`)
    - _Requirements: 5.3_

- [ ] 3. Update EntityAnalyzer extraction methods
  - [ ] 3.1 Add `ExtractGsiPartitionKeyAttributes()` method in `Oproto.FluentDynamoDb.SourceGenerator/Analysis/EntityAnalyzer.cs`
    - Extract `GsiPartitionKeyAttribute` data into `GsiPartitionKeyModel[]`
    - Parse `IndexName`, `Name`, `ProjectionType`, and discriminator properties
    - _Requirements: 5.1_

  - [ ] 3.2 Add `ExtractGsiSortKeyAttributes()` method in `EntityAnalyzer.cs`
    - Extract `GsiSortKeyAttribute` data into `GsiSortKeyModel[]`
    - Parse `IndexName`, `Name`, `ProjectionType`
    - _Requirements: 5.2_

  - [ ] 3.3 Add `ExtractLsiSortKeyAttributes()` method in `EntityAnalyzer.cs`
    - Extract `LsiSortKeyAttribute` data into `LsiSortKeyModel[]`
    - Parse `IndexName`, `Name`, `ProjectionType`
    - _Requirements: 5.3_

  - [ ] 3.4 Wire new extraction methods into `ExtractPropertyModel()` in `EntityAnalyzer.cs`
    - Call the three new extraction methods where `ExtractGsiAttributes()` and `ExtractLsiAttributes()` are currently called
    - Populate the new model arrays on `PropertyModel`
    - _Requirements: 5.1, 5.2, 5.3_

- [ ] 4. Add validation and diagnostic logic
  - [ ] 4.1 Add new diagnostic descriptors (DYNDB120–DYNDB127) in the source generator diagnostics
    - DYNDB120: GSI sort key without partition key
    - DYNDB121: Duplicate GSI partition keys for same index
    - DYNDB122: Duplicate GSI sort keys for same index
    - DYNDB123: Duplicate LSI sort keys for same index
    - DYNDB124–126: Empty/whitespace index names for each attribute type
    - DYNDB127: Same index name used as both GSI and LSI
    - _Requirements: 8.1, 8.2, 8.3, 8.4, 8.5, 8.6, 8.7, 8.8_

  - [ ] 4.2 Implement `ValidateIndexAttributes()` method in `EntityAnalyzer.cs`
    - Check empty/whitespace index names (DYNDB124–126)
    - Check duplicate partition keys per GSI (DYNDB121)
    - Check duplicate sort keys per GSI (DYNDB122)
    - Check duplicate sort keys per LSI (DYNDB123)
    - Check GSI sort key without partition key (DYNDB120)
    - Check GSI/LSI type conflict on same index name (DYNDB127)
    - Wire into entity analysis pipeline after `ExtractIndexes()`
    - _Requirements: 8.1, 8.2, 8.3, 8.4, 8.5, 8.6, 8.7, 8.8_

- [ ] 5. Update PropertyModel and ExtractIndexes
  - [ ] 5.1 Update `PropertyModel` in `Oproto.FluentDynamoDb.SourceGenerator/Models/PropertyModel.cs`
    - Replace `GlobalSecondaryIndexModel[] GlobalSecondaryIndexes` with `GsiPartitionKeyModel[] GsiPartitionKeys`, `GsiSortKeyModel[] GsiSortKeys`
    - Replace `LocalSecondaryIndexModel[] LocalSecondaryIndexes` with `LsiSortKeyModel[] LsiSortKeys`
    - Update `IsPartOfGsi` to check `GsiPartitionKeys.Length > 0 || GsiSortKeys.Length > 0`
    - Update `IsPartOfLsi` to check `LsiSortKeys.Length > 0`
    - _Requirements: 5.1, 5.2, 5.3_

  - [ ] 5.2 Update `ExtractIndexes()` in `EntityAnalyzer.cs`
    - Iterate `GsiPartitionKeys` to create/populate `IndexModel` entries (PK role)
    - Iterate `GsiSortKeys` to populate `IndexModel` entries (SK role), with PK values taking precedence for `Name`/`ProjectionType`
    - Iterate `LsiSortKeys` to create LSI `IndexModel` entries inheriting base table PK
    - Remove old iteration over `GlobalSecondaryIndexes` and `LocalSecondaryIndexes`
    - _Requirements: 2.5, 3.5, 5.4, 5.5_

  - [ ] 5.3 Update any other `EntityAnalyzer` methods that reference old model arrays
    - Update `FindIndexKeyProperty()` and similar helper methods to use new model arrays
    - _Requirements: 5.1, 5.2, 5.3_

- [ ] 6. Checkpoint — Build and verify source generator compiles
  - Ensure `dotnet build` succeeds for the source generator project
  - Run `dotnet build-server shutdown` to clear cached generators
  - Ensure all tests pass, ask the user if questions arise.

- [ ] 7. Write property-based tests for new extraction logic
  - [ ]* 7.1 Write property test: Extraction preserves all configuration values
    - **Property 1: Index attribute extraction preserves all configuration values**
    - **Validates: Requirements 1.1, 2.1, 3.1, 5.1, 5.2, 5.3**
    - Create `IndexAttributeExtractionPropertyTests` in `Oproto.FluentDynamoDb.SourceGenerator.UnitTests/Analysis/`
    - Use FsCheck with random index names, optional Name/ProjectionType/Discriminator

  - [ ]* 7.2 Write property test: GSI PK and SK combine into single IndexModel
    - **Property 2: GSI partition key and sort key combination**
    - **Validates: Requirements 5.4, 5.5**

  - [ ]* 7.3 Write property test: GsiPartitionKey takes precedence over GsiSortKey
    - **Property 3: GsiPartitionKey takes precedence over GsiSortKey for shared settings**
    - **Validates: Requirements 2.5**

  - [ ]* 7.4 Write property test: Multi-index property produces independent IndexModels
    - **Property 4: Multi-index property produces independent IndexModels**
    - **Validates: Requirements 6.1, 6.2, 6.3, 6.4, 6.5**

  - [ ]* 7.5 Write property test: LSI inherits base table partition key
    - **Property 5: LSI inherits base table partition key**
    - **Validates: Requirements 3.5**

  - [ ]* 7.6 Write property test: Duplicate and missing key diagnostics
    - **Property 6: Duplicate and missing key diagnostics**
    - **Validates: Requirements 8.1, 8.2, 8.3, 8.4**

  - [ ]* 7.7 Write property test: Empty index name diagnostics
    - **Property 7: Empty index name diagnostics**
    - **Validates: Requirements 8.5, 8.6, 8.7**

  - [ ]* 7.8 Write property test: GSI/LSI type conflict detection
    - **Property 8: GSI/LSI type conflict detection**
    - **Validates: Requirements 8.8**

- [ ] 8. Write unit tests for new attributes and diagnostics
  - [ ]* 8.1 Write unit tests for `GsiPartitionKeyAttribute` in `Oproto.FluentDynamoDb.UnitTests/Attributes/GsiPartitionKeyAttributeTests.cs`
    - Test constructor sets IndexName, defaults for optional properties, AllowMultiple = true
    - _Requirements: 1.1, 1.2, 1.3, 1.4, 1.5, 1.6_

  - [ ]* 8.2 Write unit tests for `GsiSortKeyAttribute` in `Oproto.FluentDynamoDb.UnitTests/Attributes/GsiSortKeyAttributeTests.cs`
    - Test constructor sets IndexName, defaults for optional properties, AllowMultiple = true
    - _Requirements: 2.1, 2.2, 2.3, 2.4, 2.6_

  - [ ]* 8.3 Write unit tests for `LsiSortKeyAttribute` in `Oproto.FluentDynamoDb.UnitTests/Attributes/LsiSortKeyAttributeTests.cs`
    - Test constructor sets IndexName, defaults for optional properties, AllowMultiple = true
    - _Requirements: 3.1, 3.2, 3.3, 3.4, 3.6_

  - [ ]* 8.4 Write unit tests for diagnostic validation in `Oproto.FluentDynamoDb.SourceGenerator.UnitTests/Analysis/IndexDiagnosticTests.cs`
    - Test each diagnostic (DYNDB120–127) fires for the correct misconfiguration
    - Test valid configurations produce no diagnostics
    - _Requirements: 8.1, 8.2, 8.3, 8.4, 8.5, 8.6, 8.7, 8.8_

- [ ] 9. Checkpoint — Ensure all new tests pass
  - Run `dotnet test` across all test projects
  - Ensure all tests pass, ask the user if questions arise.

- [ ] 10. Delete old attribute classes and old models
  - [ ] 10.1 Delete `Oproto.FluentDynamoDb/Attributes/GlobalSecondaryIndexAttribute.cs`
    - _Requirements: 4.1_

  - [ ] 10.2 Delete `Oproto.FluentDynamoDb/Attributes/LocalSecondaryIndexAttribute.cs`
    - _Requirements: 4.2_

  - [ ] 10.3 Delete `Oproto.FluentDynamoDb.SourceGenerator/Models/GlobalSecondaryIndexModel.cs`
    - _Requirements: 4.1_

  - [ ] 10.4 Delete `Oproto.FluentDynamoDb.SourceGenerator/Models/LocalSecondaryIndexModel.cs`
    - _Requirements: 4.2_

  - [ ] 10.5 Remove old `ExtractGsiAttributes()` and `ExtractLsiAttributes()` methods from `EntityAnalyzer.cs`
    - _Requirements: 4.1, 4.2_

- [ ] 11. Update all existing tests that reference old attributes/models
  - [ ] 11.1 Update `Oproto.FluentDynamoDb.UnitTests/Attributes/GlobalSecondaryIndexAttributeTests.cs` — rewrite or delete to test new attributes
    - _Requirements: 4.1, 14.2_

  - [ ] 11.2 Update `Oproto.FluentDynamoDb.UnitTests/Attributes/LocalSecondaryIndexAttributeTests.cs` — rewrite or delete to test new attributes
    - _Requirements: 4.2, 14.2_

  - [ ] 11.3 Update source generator unit tests that construct `GlobalSecondaryIndexModel` / `LocalSecondaryIndexModel` directly
    - Files: `PropertyModelTests.cs`, `FieldsGeneratorTests.cs`, `KeysGeneratorTests.cs`, `MapperGeneratorTests.cs`, `LsiMetadataGenerationTests.cs`, `KeysOnlyProjectionPropertyTests.cs`, `SingleEntityIndexProjectionPropertyTests.cs`, `MultiEntityIndexProjectionPropertyTests.cs`, `ProjectionTypePropertyTests.cs`
    - Replace old model construction with `GsiPartitionKeyModel`, `GsiSortKeyModel`, `LsiSortKeyModel`
    - _Requirements: 5.1, 5.2, 5.3, 14.2_

  - [ ] 11.4 Update `EntityAnalyzerTests.cs` — change inline entity source code from `[GlobalSecondaryIndex]` to `[GsiPartitionKey]`/`[GsiSortKey]`/`[LsiSortKey]`
    - _Requirements: 5.1, 5.2, 5.3, 14.2_

  - [ ] 11.5 Update `SchemaValidationPropertyTests.cs` and `SchemaValidatorPropertyTests.cs` — update comments and any attribute references
    - _Requirements: 11.1, 11.2, 14.2_

  - [ ] 11.6 Update `TableCreatorPropertyTests.cs` — update any attribute references in comments/doc strings
    - _Requirements: 9.1, 9.2, 14.2_

- [ ] 12. Checkpoint — Full test suite passes after deletion
  - Run `dotnet build-server shutdown` then `dotnet test` across all projects
  - Ensure all tests pass, ask the user if questions arise.

- [ ] 13. Update example projects
  - [ ] 13.1 Update `examples/StoreLocator/Entities/StoreGeoHash.cs` — replace `[GlobalSecondaryIndex("geohash-index", IsPartitionKey = true)]` with `[GsiPartitionKey("geohash-index")]`
    - _Requirements: 14.1_

  - [ ] 13.2 Update `examples/StoreLocator/Entities/StoreS2.cs` — replace all `[GlobalSecondaryIndex(..., IsPartitionKey = true)]` with `[GsiPartitionKey(...)]` for s2-index-fine, s2-index-medium, s2-index-coarse
    - _Requirements: 14.1_

  - [ ] 13.3 Update `examples/StoreLocator/Entities/StoreH3.cs` — replace all `[GlobalSecondaryIndex(..., IsPartitionKey = true)]` with `[GsiPartitionKey(...)]` for h3-index-fine, h3-index-medium, h3-index-coarse
    - _Requirements: 14.1_

  - [ ] 13.4 Update `examples/StoreLocator/Entities/StoresS2Table.cs` and `StoresH3Table.cs` — update comments referencing `[GlobalSecondaryIndex]` to `[GsiPartitionKey]`
    - _Requirements: 14.1_

  - [ ] 13.5 Update `examples/Examples.Tests/StoreLocator/SpatialSearchPropertyTests.cs` — replace `GlobalSecondaryIndexAttribute` reflection references with `GsiPartitionKeyAttribute`
    - _Requirements: 14.2_

  - [ ] 13.6 Update any other example entity files that use `[GlobalSecondaryIndex]` or `[LocalSecondaryIndex]`
    - Check `examples/OperationSamples/`, `examples/TodoList/`, `examples/InvoiceManager/` for GSI/LSI usage
    - _Requirements: 14.1_

- [ ] 14. Update documentation
  - [ ] 14.1 Update `docs/advanced-topics/GlobalSecondaryIndexes.md` — replace all `[GlobalSecondaryIndex]`/`[LocalSecondaryIndex]` examples with `[GsiPartitionKey]`/`[GsiSortKey]`/`[LsiSortKey]`
    - _Requirements: 13.1_

  - [ ] 14.2 Update `docs/reference/AttributeReference.md` — replace `[GlobalSecondaryIndex]` and `[LocalSecondaryIndex]` sections with `[GsiPartitionKey]`, `[GsiSortKey]`, `[LsiSortKey]` sections
    - _Requirements: 13.1_

  - [ ] 14.3 Update `docs/QUICK_REFERENCE.md` — replace GSI/LSI attribute examples with new attribute syntax
    - _Requirements: 13.1_

  - [ ] 14.4 Update `docs/DeveloperGuide.md` — replace `[GlobalSecondaryIndex]` examples with new attributes
    - _Requirements: 13.1_

  - [ ] 14.5 Update `docs/reference/AdoptionGuide.md` — replace `[GlobalSecondaryIndex]` examples with new attributes
    - _Requirements: 13.1_

  - [ ] 14.6 Update `docs/reference/Troubleshooting.md` — replace `[GlobalSecondaryIndex]` examples with new attributes
    - _Requirements: 13.1_

  - [ ] 14.7 Update `docs/core-features/EntityDefinition.md` — replace any GSI/LSI attribute examples with new syntax
    - _Requirements: 13.1_

  - [ ] 14.8 Update `docs/getting-started/FirstEntity.md` and `docs/getting-started/SingleEntityTables.md` — replace any GSI/LSI attribute examples
    - _Requirements: 13.1_

  - [ ] 14.9 Update `docs/advanced-topics/MultiEntityTables.md` and `docs/advanced-topics/Discriminators.md` — replace GSI attribute examples
    - _Requirements: 13.1_

  - [ ] 14.10 Update `docs/advanced-topics/SchemaValidation.md` and `docs/advanced-topics/TableCreation.md` — replace GSI/LSI attribute examples and add new diagnostic codes DYNDB120–127
    - _Requirements: 13.1_

- [ ] 15. Update steering files
  - [ ] 15.1 Update `.kiro/steering/fluentdynamodb.md` — replace all `[GlobalSecondaryIndex]`/`[LocalSecondaryIndex]` examples with `[GsiPartitionKey]`/`[GsiSortKey]`/`[LsiSortKey]` in Entity Definition, Index Operations, Automatic Index Projections, and Multi-Entity Index Consolidation sections
    - _Requirements: 13.1_

  - [ ] 15.2 Update `.kiro/steering/entity-patterns.md` — add a new section documenting GSI/LSI attribute patterns with `[GsiPartitionKey]`, `[GsiSortKey]`, `[LsiSortKey]`
    - _Requirements: 13.2_

- [ ] 16. Update changelogs
  - [ ] 16.1 Add entry to `CHANGELOG.md` under `[Unreleased]` → `Changed` (breaking change)
    - Document removal of `[GlobalSecondaryIndex]` and `[LocalSecondaryIndex]`
    - Document new `[GsiPartitionKey]`, `[GsiSortKey]`, `[LsiSortKey]` attributes
    - Include migration examples (before/after)
    - Document new diagnostics DYNDB120–127
    - _Requirements: 13.3_

  - [ ] 16.2 Add entry to `docs/DOCUMENTATION_CHANGELOG.md`
    - Add dated entry documenting the attribute name changes across all affected documentation files
    - Include before/after code examples for each attribute change
    - Category: Pattern Update
    - Reason: Breaking API change — `[GlobalSecondaryIndex]`/`[LocalSecondaryIndex]` replaced with `[GsiPartitionKey]`/`[GsiSortKey]`/`[LsiSortKey]`
    - _Requirements: 13.1, 13.2, 13.3_

- [ ] 17. Final checkpoint — Full build and test suite
  - Run `dotnet build-server shutdown` then `dotnet build` then `dotnet test`
  - Verify all projects compile, all tests pass, no stale references to old attributes remain
  - Ensure all tests pass, ask the user if questions arise.

## Notes

- Tasks marked with `*` are optional and can be skipped for faster MVP
- Each task references specific requirements for traceability
- Checkpoints ensure incremental validation
- Property tests validate universal correctness properties from the design document
- The old attributes and models are deleted only after new ones are fully wired and tested (tasks 1–9 before task 10)
- After task 10, all existing tests must be updated (task 11) before the test suite will pass again
- Documentation tasks (14–16) should be done last to avoid churn from implementation changes
