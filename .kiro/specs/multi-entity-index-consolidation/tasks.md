# Implementation Plan: Multi-Entity Index Consolidation

## Overview

This implementation extends the existing `IndexAggregator` to detect configuration conflicts (partition key, sort key, index type) and integrates it into `TableGenerator` to consolidate indexes from all entities in a multi-entity table. The existing infrastructure for name conflict detection (FDDB050-052) is already in place.

## Tasks

- [x] 1. Extend AggregatedIndexModel for configuration tracking
  - [x] 1.1 Add configuration fields to AggregatedIndexModel
    - Add `PartitionKeyProperty`, `SortKeyProperty`, `GsiDiscriminator` fields
    - Add `HasConfigurationConflict` flag and `ConfigurationConflictDetails` list
    - _Requirements: 2.1, 2.2, 2.3, 4.1_

- [x] 2. Add new diagnostic descriptors for configuration conflicts
  - [x] 2.1 Add FDDB053 for conflicting partition keys
    - Add diagnostic descriptor with entity names and conflicting property names
    - _Requirements: 2.1, 2.4_
  - [x] 2.2 Add FDDB054 for conflicting sort keys
    - Add diagnostic descriptor with entity names and conflicting property names
    - _Requirements: 2.2, 2.4_
  - [x] 2.3 Add FDDB055 for conflicting index types (GSI vs LSI)
    - Add diagnostic descriptor with entity names and conflicting types
    - _Requirements: 2.3, 2.4_

- [x] 3. Extend IndexAggregator for configuration validation
  - [x] 3.1 Implement configuration capture on first occurrence
    - Store partition key, sort key, and type from first entity defining the index
    - _Requirements: 1.1, 4.1, 4.2_
  - [x] 3.2 Implement ValidateIndexConfiguration method
    - Compare subsequent index definitions against captured configuration
    - Set HasConfigurationConflict and populate ConfigurationConflictDetails
    - _Requirements: 2.1, 2.2, 2.3_
  - [x] 3.3 Implement configuration conflict diagnostic reporting
    - Report FDDB053, FDDB054, FDDB055 diagnostics with entity names
    - _Requirements: 2.4_
  - [x] 3.4 Write property tests for configuration conflict detection
    - **Property 1: Conflicting partition key diagnostic emission**
    - **Validates: Requirements 2.1, 2.4**
  - [x] 3.5 Write property tests for conflicting sort keys
    - **Property 2: Conflicting sort key diagnostic emission**
    - **Validates: Requirements 2.2, 2.4**
  - [x] 3.6 Write property tests for conflicting index types
    - **Property 3: Conflicting index type diagnostic emission**
    - **Validates: Requirements 2.3, 2.4**

- [x] 4. Checkpoint - Ensure all IndexAggregator tests pass
  - Ensure all tests pass, ask the user if questions arise.

- [x] 5. Integrate IndexAggregator into TableGenerator for multi-entity tables
  - [x] 5.1 Replace single-entity index generation with consolidated approach
    - Call IndexAggregator.AggregateIndexes with all entities
    - Report diagnostics from aggregator to context
    - _Requirements: 1.1, 1.2, 1.3_
  - [x] 5.2 Implement GenerateConsolidatedIndexProperties method
    - Generate index properties from aggregated indexes (not single entity)
    - Only generate if no configuration conflicts exist
    - _Requirements: 1.1, 1.2, 1.3_
  - [x] 5.3 Update typed index class generation for consolidated indexes
    - Generate typed index classes for all consolidated indexes with projections
    - Preserve GSI discriminator and projection configurations
    - _Requirements: 4.1, 4.2, 4.3_
  - [x] 5.4 Write unit tests for consolidated index generation
    - Test indexes from multiple entities appear on generated table class
    - Test typed index classes are generated for all indexes with projections
    - _Requirements: 6.1, 6.2_

- [x] 6. Checkpoint - Ensure TableGenerator integration tests pass
  - All 699 source generator tests pass.

- [x] 7. Backward compatibility verification
  - [x] 7.1 Verify single-entity table generation unchanged
    - Existing single-entity tables compile and function without modification
    - _Requirements: 5.1, 5.2_
  - [x] 7.2 Write backward compatibility tests
    - Existing tests in ConsolidatedIndexGenerationTests.SingleEntityTable_GeneratesIndexPropertiesAsExpected
    - Integration tests in SingleEntityTableTests and MultiEntityTableTests all pass (23 tests)
    - _Requirements: 5.3, 6.3_

- [x] 8. Documentation updates
  - [x] 8.1 Update CHANGELOG.md with new functionality
    - Added multi-entity index consolidation feature entry
    - _Requirements: 7.1_
  - [x] 8.2 Update docs/DOCUMENTATION_CHANGELOG.md
    - Added entry for external documentation synchronization
    - _Requirements: 7.2_
  - [x] 8.3 Update fluentdynamodb.md steering document
    - Added "Multi-Entity Index Consolidation" section with rules table
    - _Requirements: 7.3_
  - [x] 8.4 Update docs folder with multi-entity index guidance
    - Added "Index Consolidation" section to docs/advanced-topics/MultiEntityTables.md
    - _Requirements: 7.4_

- [x] 9. Final checkpoint - Ensure all tests pass
  - All 699 source generator unit tests pass.

## Notes

- The existing IndexAggregator already handles name conflicts (FDDB050-052) - this implementation extends it for configuration conflicts
- Each task references specific requirements for traceability
- Checkpoints ensure incremental validation
- Property tests validate universal correctness properties
- Unit tests validate specific examples and edge cases
