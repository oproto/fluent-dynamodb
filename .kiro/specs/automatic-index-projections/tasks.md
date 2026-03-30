# Implementation Plan: Automatic Index Projections

## Overview

This implementation adds automatic projection type support for GSI/LSI indexes, including:
1. `ProjectionType` property on index attributes
2. Automatic entity projection for single-entity tables
3. Auto-generated Keys Only projection records

## Tasks

- [x] 1. Add ProjectionType property to index attributes
  - [x] 1.1 Update `GlobalSecondaryIndexAttribute` with `ProjectionType` property
    - Add `public ProjectionType ProjectionType { get; set; } = ProjectionType.All;`
    - Add XML documentation explaining the property
    - _Requirements: 2.1_
  - [x] 1.2 Update `LocalSecondaryIndexAttribute` with `ProjectionType` property
    - Add `public ProjectionType ProjectionType { get; set; } = ProjectionType.All;`
    - Add XML documentation explaining the property
    - _Requirements: 2.2_
  - [x] 1.3 Write unit tests for attribute properties
    - Test default value is `ProjectionType.All`
    - Test setting different projection types
    - _Requirements: 2.1, 2.2_

- [x] 2. Update source generator to parse ProjectionType
  - [x] 2.1 Update `IndexModel` with `ProjectionType` property
    - Add `public ProjectionType ProjectionType { get; set; } = ProjectionType.All;`
    - Add `public bool RequiresKeysOnlyProjection => ProjectionType == ProjectionType.KeysOnly;`
    - _Requirements: 2.3, 2.4_
  - [x] 2.2 Update `EntityAnalyzer` to parse `ProjectionType` from attributes
    - Parse `ProjectionType` named argument from GSI and LSI attributes
    - Default to `ProjectionType.All` when not specified
    - _Requirements: 2.3, 2.4_
  - [x] 2.3 Update `MapperGenerator` to emit `ProjectionType` in `IndexMetadata`
    - Emit `ProjectionType = ProjectionType.{value}` in generated metadata
    - _Requirements: 4.1_
  - [x] 2.4 Write property test for ProjectionType propagation
    - **Property 5: ProjectionType propagates to metadata**
    - **Validates: Requirements 2.4, 4.1**

- [x] 3. Checkpoint - Ensure ProjectionType parsing works
  - Ensure all tests pass, ask the user if questions arise.

- [x] 4. Implement single-entity table detection
  - [x] 4.1 Add `IsSingleEntityTable` detection in `TableGenerator`
    - Check if only one entity references the table
    - Store result for use in index generation
    - _Requirements: 1.1, 1.3_
  - [x] 4.2 Update `GenerateIndexProperties` to use entity as default projection for single-entity tables
    - When single-entity AND no `[UseProjection]` AND `ProjectionType != KeysOnly`, use entity type
    - Generate `DynamoDbIndex<TEntity>` instead of `DynamoDbIndex`
    - _Requirements: 1.1, 1.2_
  - [x] 4.3 Update `GenerateConsolidatedIndexProperties` for multi-entity tables
    - Preserve existing behavior: generate `DynamoDbIndex` when no projection
    - _Requirements: 1.3, 6.2_
  - [x] 4.4 Write property test for single-entity table index generation
    - **Property 1: Single-entity table indexes use entity as default projection**
    - **Validates: Requirements 1.1, 1.2**
  - [x] 4.5 Write property test for multi-entity table index generation
    - **Property 2: Multi-entity table indexes use simple DynamoDbIndex**
    - **Validates: Requirements 1.3, 6.2**
    - **PBT Status: PASSED**

- [x] 5. Checkpoint - Ensure single-entity detection works
  - All property tests pass (4/4 single-entity, 4/4 multi-entity)
  - Ensure all tests pass, ask the user if questions arise.

- [x] 6. Implement Keys Only projection record generation
  - [x] 6.1 Create `KeysOnlyProjectionGenerator` class
    - Generate sealed record with key properties only
    - Include GSI/LSI keys AND base table keys
    - Implement `IReadOnlyEntity<TSelf>` interface
    - Generate `FromDynamoDb` method
    - Generate `ProjectionExpression` static property
    - _Requirements: 3.1, 3.2, 3.3, 3.4, 3.6, 3.7_
  - [x] 6.2 Integrate `KeysOnlyProjectionGenerator` into `TableGenerator`
    - Call generator when `index.RequiresKeysOnlyProjection` is true
    - Generate nested record within table class
    - _Requirements: 3.4, 3.5_
  - [x] 6.3 Update index property generation for Keys Only
    - Generate `DynamoDbIndex<{IndexPropertyName}KeysProjection>` when Keys Only
    - Pass projection expression to constructor
    - _Requirements: 3.5_
  - [x] 6.4 Update `IndexMetadata` generation for Keys Only
    - Set `HasProjectionModel = true`
    - Populate `ProjectedProperties` with all key attribute names
    - _Requirements: 4.2, 4.3_
  - [x] 6.5 Write property test for Keys Only projection generation
    - **Property 6: KeysOnly generates correct projection structure**
    - **Validates: Requirements 2.5, 3.1-3.9, 4.2, 4.3**
    - **PBT Status: PASSED (8/8 tests)**

- [x] 7. Checkpoint - Ensure Keys Only generation works
  - Ensure all tests pass, ask the user if questions arise.

- [x] 8. Add diagnostic warnings
  - [x] 8.1 Add FDDB070 warning for Include without properties
    - Emit when `ProjectionType = Include` but no `ProjectedProperties`
    - _Requirements: Error Handling_
  - [x] 8.2 Add FDDB072 warning for KeysOnly with UseProjection
    - Emit when both are specified, UseProjection takes precedence
    - _Requirements: Error Handling_
  - [x] 8.3 Write unit tests for diagnostic warnings
    - Test FDDB070 is emitted correctly
    - Test FDDB072 is emitted correctly
    - _Requirements: Error Handling_

- [x] 9. Update documentation
  - [x] 9.1 Update `fluentdynamodb.md` steering file
    - Add section on automatic entity projections for single-entity tables
    - Add examples of `ProjectionType` usage on index attributes
    - Add examples of Keys Only projection generation
    - Keep under 700 lines
    - _Requirements: 5.1, 5.2, 5.3_
  - [x] 9.2 Update `docs/core-features/GlobalSecondaryIndexes.md`
    - Document `ProjectionType` property
    - Document automatic entity projection behavior
    - Document Keys Only auto-generation
    - _Requirements: 5.4_
  - [x] 9.3 Update CHANGELOG.md
    - Add entry for automatic index projections feature
    - _Requirements: Documentation_
  - [x] 9.4 Truncate docs/DOCUMENTATION_CHANGELOG.md
    - Note that external documentation is synchronized
    - Add entry for current document changes
    - _Requirements: Documentation_

- [x] 10. Final checkpoint - Ensure all tests pass
  - Ensure all tests pass, ask the user if questions arise.

## Notes

- All tasks are required for comprehensive coverage
- Each task references specific requirements for traceability
- Property tests validate universal correctness properties
- Unit tests validate specific examples and edge cases
- Checkpoints ensure incremental validation
