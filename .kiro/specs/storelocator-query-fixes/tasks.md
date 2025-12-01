# Implementation Plan

- [x] 1. Fix StoreGeoHashTable to use lambda expression query
  - [x] 1.1 Update FindStoresNearbyAsync to use Query with WithinDistanceKilometers
    - Replace `SpatialQueryAsync` call with `LocationIndex.Query<StoreGeoHash>()`
    - Use `.Where(x => x.Location.WithinDistanceKilometers(center, radiusKilometers))`
    - Post-filter results by exact distance (BETWEEN returns rectangular approximation)
    - Sort results by distance
    - Set `LastQueryCount = 1` since GeoHash always uses single query
    - _Requirements: 1.1, 1.2, 1.3, 1.4, 3.1, 3.2, 3.3_
  - [x] 1.2 Write property test for GeoHash search correctness
    - **Property 1: GeoHash Search Returns Correct Results Within Radius**
    - **Validates: Requirements 1.3, 1.4**
  - [x] 1.3 Write property test for GeoHash query count
    - **Property 2: GeoHash Query Count Is Always One**
    - **Validates: Requirements 3.3**

- [x] 2. Add GSI validation to Program.cs
  - [x] 2.1 Implement ValidateGSIsAsync method
    - Check S2 table for s2-index-fine, s2-index-medium, s2-index-coarse GSIs
    - Check H3 table for h3-index-fine, h3-index-medium, h3-index-coarse GSIs
    - Check GeoHash table for geohash-index GSI
    - Return list of missing GSIs or empty if all present
    - _Requirements: 2.1_
  - [x] 2.2 Implement DeleteTableIfExistsAsync helper method
    - Delete table if it exists
    - Wait for deletion to complete
    - Handle ResourceNotFoundException gracefully
    - _Requirements: 2.3_
  - [x] 2.3 Implement RecreateTablesAsync method
    - Prompt user for confirmation (data will be lost)
    - Delete all three tables using DeleteTableIfExistsAsync
    - Call EnsureTablesExistAsync to recreate with correct GSIs
    - _Requirements: 2.3_
  - [x] 2.4 Update startup flow to validate GSIs
    - Call ValidateGSIsAsync after EnsureTablesExistAsync
    - If GSIs are missing, display warning and offer to recreate
    - If user confirms, call RecreateTablesAsync
    - _Requirements: 2.1, 2.2_

- [x] 3. Checkpoint - Ensure all tests pass
  - Ensure all tests pass, ask the user if questions arise.

- [ ] 4. Final Checkpoint - Ensure all tests pass
  - Ensure all tests pass, ask the user if questions arise.
