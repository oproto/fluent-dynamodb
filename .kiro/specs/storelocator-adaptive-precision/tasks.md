# Implementation Plan

- [x] 1. Update StoreS2 entity with multi-precision spatial indices
  - [x] 1.1 Update StoreS2.cs to add LocationMedium and LocationCoarse properties
    - Add `LocationMedium` property with S2Level=12 and GSI `s2-index-medium`
    - Add `LocationCoarse` property with S2Level=10 and GSI `s2-index-coarse`
    - Rename existing GSI from `s2-index` to `s2-index-fine`
    - Update attribute name from `s2_cell` to `s2_cell_l14`
    - _Requirements: 3.1, 3.3_
  - [x] 1.2 Write property test for S2 multi-precision storage
    - **Property 3: S2 Multi-Precision Storage**
    - **Validates: Requirements 3.1**

- [x] 2. Update StoreH3 entity with multi-precision spatial indices
  - [x] 2.1 Update StoreH3.cs to add LocationMedium and LocationCoarse properties
    - Add `LocationMedium` property with H3Resolution=7 and GSI `h3-index-medium`
    - Add `LocationCoarse` property with H3Resolution=5 and GSI `h3-index-coarse`
    - Rename existing GSI from `h3-index` to `h3-index-fine`
    - Update attribute name from `h3_cell` to `h3_cell_r9`
    - _Requirements: 3.2, 3.4_
  - [x] 2.2 Write property test for H3 multi-precision storage
    - **Property 4: H3 Multi-Precision Storage**
    - **Validates: Requirements 3.2**

- [x] 3. Update StoreS2Table with adaptive precision selection
  - [x] 3.1 Update StoreS2Table.cs with multiple index references and precision selection
    - Add FineIndex, MediumIndex, CoarseIndex properties
    - Add LastCellSize property to track cell size for display
    - Implement SelectPrecision method that returns (index, level, cellSize, cellAttribute)
    - Update FindStoresNearbyAsync to use SelectPrecision and query the appropriate index
    - Update AddStoreAsync to populate all three location properties
    - _Requirements: 2.1, 1.1, 1.2, 1.3_
  - [x] 3.2 Write property test for S2 precision selection
    - **Property 1: S2 Precision Selection**
    - **Validates: Requirements 2.1**

- [x] 4. Update StoreH3Table with adaptive precision selection
  - [x] 4.1 Update StoreH3Table.cs with multiple index references and precision selection
    - Add FineIndex, MediumIndex, CoarseIndex properties
    - Add LastCellSize property to track cell size for display
    - Implement SelectPrecision method that returns (index, resolution, cellSize, cellAttribute)
    - Update FindStoresNearbyAsync to use SelectPrecision and query the appropriate index
    - Update AddStoreAsync to populate all three location properties
    - _Requirements: 2.2, 1.1, 1.2, 1.3_
  - [x] 4.2 Write property test for H3 precision selection
    - **Property 2: H3 Precision Selection**
    - **Validates: Requirements 2.2**

- [x] 5. Update Program.cs table creation and display
  - [x] 5.1 Update EnsureTablesExistAsync to create all GSIs
    - Update S2 table creation to include s2-index-fine, s2-index-medium, s2-index-coarse GSIs
    - Update H3 table creation to include h3-index-fine, h3-index-medium, h3-index-coarse GSIs
    - _Requirements: 3.3, 3.4_
  - [x] 5.2 Update display methods to show precision level and cell size
    - Update DisplaySearchResults to show cell size in addition to precision level
    - Update CompareAllAsync to show cell size for each index type
    - _Requirements: 1.4, 2.3_

- [x] 6. Checkpoint - Ensure all tests pass
  - Ensure all tests pass, ask the user if questions arise.

- [x] 7. Write integration test for search completion
  - [x] 7.1 Write property test for search completion without cell limit errors
    - **Property 5: Search Completes Without Cell Limit Error**
    - **Validates: Requirements 1.1, 1.2, 1.3**

- [x] 8. Final Checkpoint - Ensure all tests pass
  - Ensure all tests pass, ask the user if questions arise.
