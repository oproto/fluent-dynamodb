# Implementation Plan

- [x] 1. Add diagnostic logging to understand the bug
  - [x] 1.1 Add logging to H3CellCovering.GetCellsForBoundingBoxInternal
  - [x] 1.2 Add logging to S2CellCovering.GetCellsForBoundingBoxInternal
  - [x] 1.3 Run the failing performance tests with logging enabled
    
    **FINDINGS:** The bug is in **H3Encoder.GetNeighbors()** - it returns completely wrong neighbor cell indices (cells thousands of km away instead of adjacent cells). The cell covering algorithm, encoding, and decoding all work correctly.

- [x] 2. Port H3 reference implementation's neighbor calculation
  - [x] 2.1 Review GetNeighbors() method in H3Encoder.cs
    - Understand the current neighbor calculation algorithm
    - Identify where it's generating wrong cell indices
    - Compare with S2Encoder.GetNeighbors() which works correctly
    - Review H3 reference implementation if needed
    - _Requirements: 1.1, 1.2, 1.3_
  
  - [x] 2.2 Initial fix attempt using geographic approximation (INCOMPLETE)
    - Attempted to fix using decode/offset/encode approach
    - This approach fails neighbor symmetry tests
    - Geographic approximation is fundamentally flawed for H3
    - _Requirements: 1.1, 1.2, 1.3, 2.1, 2.3_
  
  - [x] 2.3 Port missing H3 direction and transition lookup tables
    - ✅ ALREADY EXISTS: UnitVectors array (direction constants)
    - ✅ ALREADY EXISTS: IJKRotate60cw, IJKRotate60ccw functions
    - ✅ ALREADY EXISTS: BaseCellDataTable with 122 base cells
    - ✅ ALREADY EXISTS: IsPentagon helper function
    - ❌ MISSING: NEW_DIGIT_II lookup table (7×7 array for Class II digit transitions)
    - ❌ MISSING: NEW_ADJUSTMENT_II lookup table (7×7 array for Class II direction adjustments)
    - ❌ MISSING: NEW_DIGIT_III lookup table (7×7 array for Class III digit transitions)
    - ❌ MISSING: NEW_ADJUSTMENT_III lookup table (7×7 array for Class III direction adjustments)
    - Reference: h3/src/h3lib/lib/algos.c lines 50-150
    - _Requirements: 1.2, 2.1_
  
  - [x] 2.4 Port base cell neighbor lookup tables
    - ✅ ALREADY EXISTS: BaseCellDataTable (contains face, IJK, isPentagon, cwOffsetFaces)
    - ❌ MISSING: baseCellNeighbors array (122 base cells × 7 directions → neighbor base cell)
    - ❌ MISSING: baseCellNeighbor60CCWRots array (122 base cells × 7 directions → rotation count)
    - ❌ MISSING: _isBaseCellPolarPentagon helper function
    - ❌ MISSING: _baseCellIsCwOffset helper function
    - Reference: h3/src/h3lib/lib/baseCells.c
    - _Requirements: 1.2, 2.1_
  
  - [x] 2.5 Port H3 index manipulation helper functions
    - ❌ MISSING: _h3LeadingNonZeroDigit function (finds first non-zero digit in index)
    - ❌ MISSING: H3_GET_INDEX_DIGIT function (extracts digit at specific resolution)
    - ❌ MISSING: H3_SET_INDEX_DIGIT function (sets digit at specific resolution)
    - ❌ MISSING: H3_GET_BASE_CELL function (extracts base cell from index)
    - ❌ MISSING: H3_SET_BASE_CELL function (sets base cell in index)
    - ❌ MISSING: H3_GET_RESOLUTION function (extracts resolution from index)
    - ❌ MISSING: isResolutionClassIII helper (determines if resolution is Class III)
    - ❌ MISSING: _h3RotatePent60ccw for pentagon-specific rotations
    - Reference: h3/src/h3lib/include/h3Index.h and h3/src/h3lib/lib/h3Index.c
    - _Requirements: 1.2, 2.1_
  
  - [x] 2.6 Implement h3NeighborRotations function
    - Port the core h3NeighborRotations function from algos.c
    - This function calculates a single neighbor in a given direction
    - Handles base cell transitions correctly
    - Handles pentagon cells and deleted K-axes subsequence
    - Handles coordinate rotations across face boundaries
    - Returns E_PENTAGON error for invalid pentagon directions
    - Reference: h3/src/h3lib/lib/algos.c lines 449-598
    - _Requirements: 1.1, 1.2, 1.3, 2.1, 2.3_
  
  - [x] 2.7 Rewrite GetNeighbors() to use h3NeighborRotations
    - Replace geographic approximation with proper IJK-based calculation
    - Loop through 6 directions (or 5 for pentagons)
    - Call h3NeighborRotations for each direction
    - Handle E_PENTAGON errors (skip that direction for pentagons)
    - Return array of neighbor indices
    - Ensure neighbor symmetry property holds
    - _Requirements: 1.1, 1.2, 1.3, 2.1, 2.3_
    
    **STATUS:** GetNeighbors() implementation is complete and correct. However, h3NeighborRotations (task 2.6) has a bug that causes it to fail for resolutions > 1. Works correctly for resolution 0-1, but returns false for all directions at resolution 9+.

  - [x] 2.8 Debug and fix h3NeighborRotations for higher resolutions
    - **ISSUE:** h3NeighborRotations works for resolution 0-1 but fails for resolution 9+
    - Add diagnostic logging to h3NeighborRotations to trace execution
    - Test with resolution 1 (working) vs resolution 9 (failing) to identify where logic diverges
    - Focus on the resolution traversal loop (working backwards from target resolution to 0)
    - Check digit extraction and transition table lookups
    - Verify NEW_DIGIT_II/III and NEW_ADJUSTMENT_II/III tables are correct
    - Compare with H3 reference implementation algos.c lines 449-598
    - Ensure all edge cases are handled (Class II vs Class III resolutions)
    - Test fix with cells at resolutions 0, 1, 5, 9, 12, 15
    - _Requirements: 1.1, 1.2, 1.3, 2.1, 2.3_

- [x] 3. Fix and strengthen existing H3NeighborTests
  - [x] 3.1 Fix GetNeighbors_NeighborsAreAdjacent test
    - Replace weak Euclidean distance check (5 degrees) with actual geographic distance
    - Use proper distance calculation: `GeoLocation.DistanceToKilometers()`
    - Set threshold based on cell size: neighbors should be within 2 * cellSize
    - Add test case for resolution 9 in San Francisco (the failing case we found)
    - **This test should FAIL with current buggy code, then PASS after fix**
    - _Requirements: 1.2, 2.1, 2.3_
  
  - [x] 3.2 Add test for specific failing case
    - Test cell `89283082803ffff` (San Francisco, resolution 9)
    - Verify all 6 neighbors decode to locations near San Francisco
    - Verify no neighbors are thousands of km away
    - _Requirements: 1.1, 1.2, 1.3_
  
  - [x] 3.3 Add test for neighbor symmetry
    - Test if B is neighbor of A, then A is neighbor of B
    - Test with multiple resolutions and locations
    - _Requirements: 1.2_

- [x] 4. Write property test for GetNeighbors()
  - [x] 4.1 Write property test for neighbor adjacency
    - **Property 0: H3 GetNeighbors returns adjacent cells**
    - Generate random H3 cell indices at various resolutions
    - Call GetNeighbors() on each
    - Decode all returned neighbors
    - Verify each neighbor is within 2 * cellSize distance
    - Verify neighbor count is 6 (or 5 for pentagons)
    - _Requirements: 1.1, 1.2, 1.3, 2.1, 2.3_

- [x] 5. Verify the fix with integration tests
  - [x] 5.1 Run H3 performance test and verify it passes
    - Run H3PerformanceIntegrationTests.SpatialQueryAsync_H3ProximityNonPaginated_LargeResultSet_CompletesEfficiently
    - Verify that many cells are returned (not just 1)
    - Verify that all stores within 30km are found
    - Verify query completes in reasonable time
    - _Requirements: 1.1, 1.5, 4.1, 4.2, 5.1, 5.2_
  
  - [x] 5.2 Write property test for H3 cell count scaling
    - **Property 7: H3 cell count scales with area**
    - Generate random center points
    - Compute cell covering for radius R and 2R
    - Verify cells(2R) ≈ 4 * cells(R) (within 50% tolerance)
    - _Requirements: 4.1, 4.2, 4.3_
  
  - [x] 5.3 Write property test for H3 multiple cells for large radius
    - **Property 2: Cell covering returns multiple cells for large radius**
    - Generate random center points
    - Use radius = 20 * cellSize
    - Verify result contains > 1 cell
    - **STATUS:** ✅ FIXED - H3 cell covering now returns 100 cells for 30km radius (was returning only 1)
    - _Requirements: 1.1, 1.5_

- [x] 6. Fix H3 Pentagon Base Cell Decoding Bug
  - [x] 6.1 Understand the pentagon decoding bug
    - **BUG SUMMARY:** H3 cells encoded to pentagon base cells decode to completely wrong geographic locations (up to 12° error)
    - **AFFECTED BASE CELLS:** 4, 14, 24, 38, 49, 58, 63, 72, 83, 97, 107, 117 (12 pentagons out of 122 base cells)
    - **WORKING BASE CELLS:** All 110 hexagon base cells decode correctly (< 0.1° error)
    - **ROOT CAUSE:** When a point on face X is encoded to a pentagon base cell whose home face is Y, the decoding starts from face Y and the overage mechanism fails to transition back to face X
    - **EXAMPLE FAILING CASE:** 
      - Input: (14.9477°, 58.0997°) Arabian Sea
      - Encoded to: base cell 49 (pentagon, home face 14)
      - Original face: 0 (point is closest to face 0)
      - Decoded to: (7.0566°, 58.3633°) - 7.89° latitude error!
    - **TEST FILES CREATED:**
      - `H3/H3ArabianSeaDebugTest.cs` - traces encoding/decoding for failing case
      - `H3/H3GnomonicDebugTest.cs` - tests various locations, shows pattern
      - `H3/H3BaseCellDebugTest.cs` - confirms all failures are pentagon base cells
      - `H3/H3EncodingDebugTest.cs` - traces base cell selection
      - `H3/H3PentagonCenterTest.cs` - tests pentagon center decoding
    - _Requirements: 1.1, 1.2, 1.3_

  - [x] 6.2 Study H3 reference implementation's pentagon handling
    - **ENCODING (faceijk.c `_faceIjkToH3`):**
      - When base cell is pentagon, special rotation is applied
      - `_h3LeadingNonZeroDigit` checks for digit 1 (K-axis, invalid for pentagons)
      - `_baseCellIsCwOffset` determines rotation direction
      - `_h3RotatePent60ccw` rotates pentagon indices differently than hexagons
    - **DECODING (faceijk.c `_h3ToFaceIjk`):**
      - Starts from base cell's home FaceIJK
      - For pentagons with leading digit 5, rotates index 60° CW before traversing
      - Traverses digit path with DownAp7/DownAp7r based on resolution class
      - `_adjustOverageClassII` handles face transitions
      - Pentagon cells may need multiple overage adjustments
    - **KEY INSIGHT:** The rotation applied during encoding must be exactly reversed during decoding
    - Reference files:
      - h3/src/h3lib/lib/faceijk.c (lines 400-600 for encoding, 200-400 for decoding)
      - h3/src/h3lib/lib/algos.c (pentagon rotation helpers)
      - h3/src/h3lib/lib/baseCells.c (base cell data and helpers)
    - _Requirements: 1.2, 2.1_

  - [x] 6.3 Fix ParseH3IndexWithFace for pentagon base cells
    - **STATUS:** ✅ FIXED - Pentagon rotation was fixed by correcting digit rotation mappings
    - Fixed incorrect rotation mappings in `RotateH3Index60ccw`, `RotateH3Index60cw`, and `RotatePentagon60ccw`
    - All pentagon encoding/decoding now matches H3 reference library
    - _Requirements: 1.1, 1.2, 1.3, 2.1, 2.3_

  - [x] 6.4 Fix BuildH3IndexFromFaceIJK for pentagon base cells
    - **STATUS:** ✅ FIXED - Encoding rotation now matches reference implementation
    - Verified with H3.NET reference library comparison tests
    - _Requirements: 1.2, 2.1_

  - [x] 6.5 Verify AdjustOverageClassII handles pentagon face transitions
    - **STATUS:** ✅ VERIFIED - Pentagon face transitions work correctly after rotation fix
    - All 12 pentagon base cells encode/decode correctly
    - _Requirements: 1.2, 2.1_

  - [x] 6.6 Add comprehensive pentagon decoding tests
    - **STATUS:** ✅ COMPLETED - Added H3ReferenceComparisonTest.cs with comprehensive tests
    - Tests all 12 pentagon base cells at multiple resolutions
    - Tests round-trip encoding/decoding accuracy
    - All tests pass with < 0.1° error
    - _Requirements: 1.1, 1.2, 1.3, 2.1, 2.3_

  - [x] 6.7 Verify fix doesn't break hexagon base cells
    - **STATUS:** ✅ VERIFIED - All 55 H3 tests pass
    - San Francisco (hexagon base cell 20) works correctly
    - All locations in H3GnomonicDebugTest work correctly
    - Reference library comparison tests pass for all cell types
    - _Requirements: 1.1, 1.2, 1.3_

- [x] 7. Clean up and document
  - [x] 7.1 Remove diagnostic logging added in task 1
    - Remove Console.WriteLine statements from H3CellCovering
    - Remove Console.WriteLine statements from S2CellCovering
    - Keep useful comments explaining the algorithm
    - _Requirements: 8.1, 8.2, 8.3_
  
  - [x] 7.2 Update documentation for H3Encoder.GetNeighbors()
    - Add XML documentation explaining the method
    - Document that it returns 6 neighbors for hexagons, 5 for pentagons
    - Document expected behavior and edge cases
    - _Requirements: 8.1, 8.2, 8.4, 8.5_
  
  - [x] 7.3 Update documentation for H3Encoder.Decode()
    - Document pentagon base cell handling
    - Document expected accuracy for different base cell types
    - _Requirements: 8.1, 8.2, 8.4, 8.5_

- [x] 8. Final checkpoint - Ensure all tests pass
  - Run all H3Encoder unit tests
  - Run all H3CellCovering integration tests
  - Run all property-based tests
  - Verify no regressions in existing functionality
  - Ask the user if questions arise.
