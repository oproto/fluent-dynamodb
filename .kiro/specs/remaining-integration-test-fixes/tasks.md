# Implementation Plan

- [x] 1. Fix reserved keyword issues in test entities
  - [x] 1.1 Rename S2StoreWithSortKeyEntity attributes
    - Change `[DynamoDbAttribute("location", ...)]` to `[DynamoDbAttribute("loc", ...)]`
    - Change `[DynamoDbAttribute("status")]` to `[DynamoDbAttribute("store_status")]`
    - _Requirements: 1.1, 1.2_
  
  - [x] 1.2 Update integration tests that use S2StoreWithSortKeyEntity
    - Update format string expressions from `location` to `loc`
    - Update format string expressions from `status` to `store_status`
    - _Requirements: 1.3_
  
  - [x] 1.3 Run affected integration tests to verify fix
    - Run tests with filter `S2ProximityWithSortKey`
    - Run tests with filter `S2ProximityWithAdditionalFilters`
    - Run tests with filter `S2BoundingBoxWithSortKey`
    - _Requirements: 1.3_

- [x] 2. Fix test code bugs
  - [x] 2.1 Fix longitude wrapping in H3EdgeCaseIntegrationTests
    - Update `SpatialQueryAsync_H3ProximityPaginated_NearSouthPole_ReturnsStoresWithinRadius`
    - Change `var lon = lonIdx * 45.0;` to wrap values > 180 to negative range
    - _Requirements: 3.5_
  
  - [x] 2.2 Review other polar region tests for similar issues
    - Check `SpatialQueryAsync_H3ProximityNonPaginated_NearSouthPole_*` tests
    - Check `SpatialQueryAsync_S2ProximityNonPaginated_NearNorthPole_*` tests
    - Fix any similar longitude wrapping issues
    - _Requirements: 3.5_
  
  - [x] 2.3 Run polar region tests to verify fixes
    - Run tests with filter `NearSouthPole`
    - Run tests with filter `NearNorthPole`
    - _Requirements: 3.4, 3.5_

- [x] 3. Checkpoint - Verify reserved keyword and test bug fixes
  - Ensure all tests pass, ask the user if questions arise.

- [x] 4. Investigate date line crossing issues
  - [x] 4.1 Add diagnostic logging to cell covering algorithms
    - Add logging to `H3CellCovering.GetCellsForRadius()` to show computed cells
    - Add logging to `S2CellCovering.GetCellsForRadius()` to show computed cells
    - Run date line crossing tests with logging enabled
    - _Requirements: 2.1, 2.2_
  
  - [x] 4.2 Analyze cell covering behavior near date line
    - Verify `GeoBoundingBox.CrossesDateLine()` returns true for date line queries
    - Verify `GeoBoundingBox.SplitAtDateLine()` produces correct western and eastern boxes
    - Check if cell covering is called for both split boxes
    - _Requirements: 2.1, 2.2_
  
  - [x] 4.3 Fix date line handling in cell covering algorithms
    - Ensure cell coverings are computed for both sides of date line
    - Ensure cells are deduplicated after merging
    - _Requirements: 2.1, 2.2, 2.3_
  
  - [x] 4.4 Run date line crossing tests to verify fix
    - Run tests with filter `CrossingDateLine`
    - _Requirements: 2.4, 2.5_

- [x] 5. Fix unit tests with inappropriate radius/level combinations
  - [x] 5.1 Fix SpatialQueryArchitectureTests
    - Update `CustomCellList_CanBeUsedForSpatialQueries` to use level 10 with 5km radius or level 16 with 0.5km radius
    - Update `CellComputation_ForRadius_ReturnsValidCells` S2 case to use level 10 instead of level 16
    - Update `CellComputation_ForBoundingBox_ReturnsValidCells` S2 case to use level 10 instead of level 16
    - Update `S2CellComputation_ReturnsCellsSortedByDistance` to use level 10 with 5km radius or level 16 with 0.5km radius
    - _Requirements: 6.1, 6.2, 6.7_
  
  - [x] 5.2 Fix DiagnosticTest
    - Update `DiagnoseDateLineCrossing` to use level 8 with 200km radius (already uses level 10)
    - Update `DiagnoseTestStoreCells` to use level 10 with 5km radius or level 16 with 0.5km radius
    - Update `DiagnoseS2CellCovering` to use level 10 with 5km radius or level 16 with 0.5km radius
    - _Requirements: 6.1, 6.2, 6.7_
  
  - [x] 5.3 Fix CombinedDatelinePoleTests
    - Update `GetCellsForRadius_EquatorNearDateline_ReturnsValidCells` to use level 8 with 100km radius instead of level 10
    - _Requirements: 6.3, 6.7_
  
  - [x] 5.4 Fix PoleHandlingTests
    - Update `S2CellCovering_NearPole_ProducesReasonableCellCount` level 14 and 16 cases to use smaller radius or lower level
    - _Requirements: 6.1, 6.2, 6.7_
  
  - [x] 5.5 Fix DatelineCellCoveringTests
    - Update `S2CellCovering_WithDatelineCrossingRadius_ReturnsUniqueCells` to use level 8 with 200km radius
    - Update `S2CellCovering_WithDatelineCrossingRadius_CellsAreSortedByDistance` to use level 8 with 200km radius
    - _Requirements: 6.3, 6.7_
  
  - [x] 5.6 Fix SpatialQueryPropertyTests
    - Update `BoundingBoxQuery_RespectsMaxCellsLimit_S2` to use level 8 instead of level 10 for 50km radius
    - _Requirements: 6.3, 6.7_
  
  - [x] 5.7 Fix H3CellPropertyTests
    - Update `GetChildren_ReturnsCorrectCountAndLevel` to handle edge cases where children count may be 0
    - _Requirements: 6.4, 6.5, 6.6_
  
  - [x] 5.8 Run unit tests to verify fixes
    - Run `dotnet test Oproto.FluentDynamoDb.Geospatial.UnitTests`
    - Verify all 13 previously failing tests now pass
    - _Requirements: 6.1, 6.2, 6.3, 6.4, 6.5, 6.6, 6.7_

- [x] 6. Fix integration tests with inappropriate radius/level combinations
  - [x] 6.1 Fix S2GsiSpatialQueryIntegrationTests
    - Tests use S2 level 16 (~71m cells) with 5-20km radius - need to reduce radius to 0.5km or use level 10
    - Update all tests to use radius ≤ 0.5km for level 16, or switch to low precision entity
    - _Requirements: 6.1, 6.2, 6.7_
  
  - [x] 6.2 Fix H3GsiSpatialQueryIntegrationTests
    - Tests use H3 resolution 9 (~175m cells) with 5-20km radius - need to reduce radius to 1km or use resolution 5
    - Update all tests to use radius ≤ 1km for resolution 9, or switch to low precision entity
    - _Requirements: 6.4, 6.5, 6.7_
  
  - [x] 6.3 Fix H3PerformanceIntegrationTests
    - Update tests to use appropriate radius/resolution combinations
    - _Requirements: 6.4, 6.5, 6.7_
  
  - [x] 6.4 Run integration tests to verify fixes (FAILED - 83 tests failing)
    - Run `dotnet test Oproto.FluentDynamoDb.IntegrationTests --filter "S2|H3"`
    - _Requirements: 6.1, 6.2, 6.3, 6.4, 6.5, 6.6, 6.7_
  
  - [x] 6.5 Fix H3GsiEdgeCaseIntegrationTests radius/resolution mismatch
    - Tests use resolution 5 (~8km cells) with 200km radius - requires ~664 cells (max 500)
    - Reduce radius from 200km to 50km for all edge case tests (date line, polar regions)
    - Update store placement to be within the reduced radius
    - _Requirements: 6.6, 6.7_
  
  - [x] 6.6 Fix H3SpatialQueryIntegrationTests radius/resolution mismatch
    - Tests use resolution 9 (~175m cells) with 5km radius - requires ~996 cells (max 500)
    - Reduce radius from 5km to 1km for all tests, OR
    - Create a new low-precision entity with resolution 5 and update tests to use it
    - Update store placement to be within the reduced radius
    - _Requirements: 6.4, 6.5, 6.7_
  
  - [x] 6.7 Fix S2SpatialQueryIntegrationTests radius/level mismatch
    - Remember if any test is using a cell as the sort key of the actual table, the test is invalid.  We have moved to GSI-based tests
    - Tests use level 16 (~71m cells) with 5km radius - requires ~15,594 cells (max 500)
    - Reduce radius from 5km to 0.5km for all tests, OR
    - Create a new low-precision entity with level 10 and update tests to use it
    - Update store placement to be within the reduced radius
    - _Requirements: 6.1, 6.2, 6.7_
  
  - [x] 6.8 Fix S2PerformanceIntegrationTests radius/level mismatch (4 tests failing)
    - Tests use S2 level 16 (~71m cells) with 25-100km radius - requires 389,850 to 6,237,594 cells (max 500)
    - `LargeResultSet_CompletesEfficiently`: level 16 with 30km radius → change to level 10 or reduce radius to 0.5km
    - `VeryLargeRadius_RespectsMaxCellsLimit`: level 16 with 100km radius → change to level 8 or reduce radius
    - `MultipleQueries_ConsistentPerformance`: level 16 with 25km radius → change to level 10 or reduce radius to 0.5km
    - `ManyPages_SequentialExecutionWorksCorrectly`: level 16 with 30km radius → change to level 10 or reduce radius to 0.5km
    - _Requirements: 6.1, 6.2, 6.7_
  
  - [x] 6.9 Fix S2GsiEdgeCaseIntegrationTests radius/level mismatch (5 tests failing)
    - Tests use S2 level 10 (~4.5km cells) with 150-200km radius - requires 3,427 to 6,092 cells (max 500)
    - `CrossingDateLine_ReturnsStoresOnBothSides`: level 10 with 200km radius → change to level 8 (18km cells)
    - `CrossingDateLine_NoDuplicates`: level 10 with 150km radius → change to level 8
    - `NearNorthPole_ReturnsStoresWithinRadius`: level 10 with 200km radius → change to level 8
    - `NearSouthPole_ReturnsStoresWithinRadius`: level 10 with 200km radius → change to level 8
    - `S2ProximityPaginated_NearSouthPole_ReturnsStoresWithinRadius`: level 10 with 200km radius → change to level 8
    - Update the Precision constant from 10 to 8 in the test class
    - Update store placement to account for larger cell size
    - _Requirements: 6.3, 6.7_
  
  - [x] 6.10 Fix H3PerformanceIntegrationTests if any remaining failures (1 test potentially failing)
    - `LargeResultSet_CompletesEfficiently`: verify resolution 5 with 30km radius is within limits
    - _Requirements: 6.4, 6.5, 6.7_
  
  - [x] 6.11 Run integration tests to verify all fixes
    - Run `dotnet test Oproto.FluentDynamoDb.IntegrationTests --filter "S2|H3"`
    - Verify all tests pass
    - _Requirements: 6.1, 6.2, 6.3, 6.4, 6.5, 6.6, 6.7_

- [x] 7. Fix remaining radius/level mismatch tests (6 tests failing)
  - [x] 7.1 Fix CrossIndexComparisonTests radius/level mismatch (3 tests failing)
    - `PrecisionComparison_HigherPrecisionMoreAccurate`: DELETED - test premise was flawed (can't query at different precision than stored)
    - `SameLocation_StoredWithDifferentIndexTypes_AllReturnCorrectly`: S2 level 16 with 0.5km radius
    - `SpatialQuery_PerformanceComparison_DocumentsCharacteristics`: S2 level 16 with 0.5km radius
    - _Requirements: 6.1, 6.2, 6.7_
  
  - [x] 7.2 Fix GsiSpatialQueryIntegrationTests radius/level mismatch (3 tests failing)
    - `SpatialQueryAsync_ViaGsi_ReturnsMultipleStoresInSameCell`: S2 level 16 with 0.5km radius
    - `SpatialQueryAsync_WithCustomCellList_ReturnsMatchingStores`: S2 level 16 with 0.5km radius
    - `SpatialQueryAsync_WithCustomCellList_SortsByDistanceWhenCenterProvided`: S2 level 16 with 0.5km radius
    - _Requirements: 6.1, 6.2, 6.7_
  
  - [x] 7.3 Run integration tests to verify all fixes
    - Run `dotnet test Oproto.FluentDynamoDb.IntegrationTests --filter "CrossIndex|GsiSpatial"`
    - All 15 tests pass
    - _Requirements: 6.1, 6.2, 6.7_

- [x] 8. Checkpoint - Verify unit test and pagination fixes
  - All 108 geospatial integration tests pass (1 skipped)

- [x] 9. Investigate remaining failures
  - [x] 9.1 Run full integration test suite
    - All geospatial integration tests pass
    - No remaining failures to categorize
    - _Requirements: 5.1, 5.2, 5.3_
  
  - [x] 9.2 Fix remaining issues
    - No remaining issues to fix
    - _Requirements: 5.1, 5.2, 5.3_

- [ ] 10. Write property tests for correctness properties
  - [ ]* 9.1 Write property test for lambda expression attribute name generation
    - **Property 1: Lambda expressions generate attribute name placeholders**
    - **Validates: Requirements 1.4**
  
  - [ ]* 9.2 Write property test for date line cell coverings
    - **Property 2: Date line cell coverings include both sides**
    - **Validates: Requirements 2.1, 2.2**
  
  - [ ]* 9.3 Write property test for cell deduplication
    - **Property 3: Cell coverings have no duplicates**
    - **Validates: Requirements 2.3, 2.5**
  
  - [ ]* 9.4 Write property test for polar bounding box latitude clamping
    - **Property 4: Polar bounding boxes clamp latitude**
    - **Validates: Requirements 3.2**
  
  - [ ]* 9.5 Write property test for polar bounding box longitude expansion
    - **Property 5: Polar bounding boxes expand longitude when appropriate**
    - **Validates: Requirements 3.3**
  
  - [ ]* 9.6 Write property test for coordinate validity
    - **Property 6: Cell coverings produce valid coordinates**
    - **Validates: Requirements 3.5**
  
  - [ ]* 9.7 Write property test for pagination completeness
    - **Property 7: Pagination returns all results exactly once**
    - **Validates: Requirements 4.1, 4.2, 4.3, 4.4, 4.5**

- [ ] 11. Final Checkpoint - Ensure all tests pass
  - Ensure all tests pass, ask the user if questions arise.
