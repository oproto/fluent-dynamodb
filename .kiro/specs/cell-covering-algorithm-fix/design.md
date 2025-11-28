# Design Document

## Overview

This design addresses a critical bug in the H3 spatial indexing implementation where only 1 cell is returned for radius queries that should return many cells. The bug was discovered during performance testing when a 30km radius query returned far fewer stores than expected.

### Root Cause Analysis

Through diagnostic logging, we identified that the bug is in `H3Encoder.GetNeighbors()` in `H3Encoder.cs`.

**The Cell Covering Algorithm is Correct:**
The ring expansion algorithm in `H3CellCovering.GetCellsForBoundingBoxInternal()` works as follows:
1. Start with the center cell
2. Get neighbors of all cells in the current ring using `H3Encoder.GetNeighbors()`
3. Check if each neighbor intersects the search area
4. Add intersecting neighbors to the next ring
5. Repeat until no new cells are added or maxCells is reached

**Evidence the Algorithm Works:**
- S2 implementation uses the identical algorithm and works perfectly
- S2 returns 100 cells for a 30km radius query (correct behavior)
- The center cell encoding/decoding works correctly for H3

**The Actual Bug - H3Encoder.GetNeighbors():**

Diagnostic logging revealed:
```
Center Cell Index: 89283082803ffff
Center Cell Decoded: (37.773515, -122.418271) ✓ CORRECT

Neighbor 1 Index: 890208610d...
Neighbor 1 Decoded: (76.704034, -99.808654) ✗ WRONG - 4468 km away!
```

The center cell `89283082803ffff` (San Francisco) encodes and decodes correctly. However, `GetNeighbors()` returns cell indices like `890208610d...` which are:
- Completely different from the center cell index
- Located thousands of kilometers away (Northern Canada)
- NOT adjacent neighbors at all

**Why This Breaks Everything:**
1. Ring expansion calls `GetNeighbors()` on the center cell
2. GetNeighbors() returns unrelated cells from different parts of the world
3. These "neighbors" are thousands of km away, so they fail the intersection check
4. No cells are added to the next ring
5. Ring expansion terminates immediately with only 1 cell

### The Fix

Fix the `H3Encoder.GetNeighbors()` method to return the correct neighbor cell indices. The method should return the 6 hexagonal neighbors (or 5 for pentagon cells) that are actually adjacent to the input cell.

**No changes needed to:**
- H3CellCovering algorithm (it's working correctly)
- S2CellCovering algorithm (it's working correctly)
- Encoding/decoding logic (it's working correctly)

## Architecture

### Current Architecture (Buggy)

```
GetCellsForRadius(center, radius, resolution, maxCells)
  ↓
GetCellsForBoundingBox(bbox, center, resolution, maxCells)
  ↓
GetCellsForBoundingBoxInternal(bbox, center, resolution, maxCells)
  ↓
Ring expansion calls H3Encoder.GetNeighbors()
  ↓
GetNeighbors() returns WRONG cell indices (bug is here!)
  ↓
Wrong neighbors fail intersection check
  ↓
Ring expansion terminates with only 1 cell
```

### Fixed Architecture

```
GetCellsForRadius(center, radius, resolution, maxCells)
  ↓
GetCellsForBoundingBox(bbox, center, resolution, maxCells)
  ↓
GetCellsForBoundingBoxInternal(bbox, center, resolution, maxCells)
  ↓
Ring expansion calls H3Encoder.GetNeighbors()
  ↓
GetNeighbors() returns CORRECT neighbor cell indices (fixed!)
  ↓
Correct neighbors pass intersection check
  ↓
Ring expansion continues until area is covered

## Components and Interfaces

### H3Encoder (To Be Fixed)

```csharp
internal static class H3Encoder
{
    // Existing methods - working correctly
    public static string Encode(double latitude, double longitude, int resolution);
    public static (double Latitude, double Longitude) Decode(string h3Index);
    
    // BUGGY METHOD - needs to be fixed
    public static string[] GetNeighbors(string h3Index);
    // Currently returns wrong cell indices
    // Should return the 6 adjacent hexagonal neighbors (or 5 for pentagons)
}
```

**The Fix:** Investigate and correct the GetNeighbors() implementation to return the actual adjacent neighbor cells.

### H3CellCovering (No Changes Needed)

```csharp
public static class H3CellCovering
{
    // Public API - working correctly, no changes needed
    public static List<string> GetCellsForRadius(
        GeoLocation center,
        double radiusKilometers,
        int resolution,
        int maxCells = 100);
    
    public static List<string> GetCellsForBoundingBox(
        GeoBoundingBox boundingBox,
        int resolution,
        int maxCells = 100);
    
    // Internal method - working correctly, no changes needed
    private static List<(string Index, double Distance)> GetCellsForBoundingBoxInternal(
        GeoBoundingBox boundingBox,
        GeoLocation center,
        int resolution,
        int maxCells);
    
    // Helper methods - working correctly
    private static double GetApproximateCellSizeKm(int resolution);
    private static double GetBoundingBoxRadiusKm(GeoBoundingBox bbox);
}
```

## Data Models

No changes to data models.

## Correctness Properties

*A property is a characteristic or behavior that should hold true across all valid executions of a system-essentially, a formal statement about what the system should do. Properties serve as the bridge between human-readable specifications and machine-verifiable correctness guarantees.*

### Property 0: H3 GetNeighbors returns adjacent cells

*For any* valid H3 cell index, when calling GetNeighbors(), all returned neighbor cell indices should decode to locations that are adjacent to the input cell (within 2 * cellSize distance).

**Validates: Requirements 1.1, 1.2, 1.3** (This is the root cause fix)

### Property 1: Cell covering completeness for radius queries

*For any* center point, radius, and precision level, when computing a cell covering for a radius query, all cells whose centers are within radius + cellSize from the center should be included in the result (up to maxCells limit).

**Validates: Requirements 1.1, 1.2, 1.5, 2.1, 2.3**

### Property 2: Cell covering returns multiple cells for large radius

*For any* center point and radius > 10 * cellSize, when computing a cell covering, the result should contain more than 1 cell (unless maxCells = 1).

**Validates: Requirements 1.1, 1.5**

### Property 3: Cells are sorted by distance

*For any* cell covering result, the cells should be sorted by distance from the center point in ascending order (closest first).

**Validates: Requirements 1.4**

### Property 4: Cell covering respects maxCells limit

*For any* cell covering computation, the number of returned cells should be less than or equal to maxCells.

**Validates: Requirements 3.4, 4.4**

### Property 5: Cell covering excludes distant cells

*For any* center point, radius, and precision level, when computing a cell covering for a radius query, all cells whose centers are more than radius + cellSize from the center should be excluded from the result.

**Validates: Requirements 2.2, 2.4**

### Property 6: S2 cell count scales with area

*For any* center point and S2 level, when doubling the radius, the number of returned cells should increase by approximately 4x (area scales with radius²), subject to maxCells limit.

**Validates: Requirements 3.1, 3.2, 3.3**

### Property 7: H3 cell count scales with area

*For any* center point and H3 resolution, when doubling the radius, the number of returned cells should increase by approximately 4x (area scales with radius²), subject to maxCells limit.

**Validates: Requirements 4.1, 4.2, 4.3**

### Property 8: Bounding box covering completeness

*For any* bounding box and precision level, when computing a cell covering for a bounding box query, all cells whose centers are within the bounding box (expanded by cellSize) should be included in the result (up to maxCells limit).

**Validates: Requirements 2.2, 2.3**

### Property 9: Date line handling preserves completeness

*For any* search area that crosses the International Date Line, the cell covering should include cells from both sides of the date line, with no duplicates.

**Validates: Requirements 6.1**

### Property 10: Small radius returns few cells

*For any* center point and radius < cellSize, when computing a cell covering, the result should contain a small number of cells (typically 1-7 for the center and immediate neighbors).

**Validates: Requirements 3.5, 4.5, 6.3**

## Error Handling

### Validation Errors

The fixed implementation maintains existing validation:
- `ArgumentOutOfRangeException` for invalid level/resolution (S2: 0-30, H3: 0-15)
- `ArgumentOutOfRangeException` for invalid maxCells (< 1)

### Edge Cases

The fixed implementation handles:
- **Very small radius**: Returns at least the center cell
- **Very large radius**: Returns up to maxCells cells, sorted by distance
- **Date line crossing**: Splits query and deduplicates cells
- **Polar regions**: Handles longitude convergence correctly
- **maxCells reached**: Stops expansion and returns closest cells

## Testing Strategy

### Unit Tests

Unit tests will verify the H3Encoder.GetNeighbors() fix:

1. **GetNeighbors returns correct count**:
   - Test that hexagonal cells return 6 neighbors
   - Test that pentagon cells return 5 neighbors

2. **GetNeighbors returns adjacent cells**:
   - Test that all returned neighbors are within 2 * cellSize distance
   - Test that neighbors are actually adjacent (share an edge)
   - Test with cells at various locations (equator, poles, date line)

3. **GetNeighbors consistency**:
   - Test that if B is a neighbor of A, then A is a neighbor of B
   - Test that neighbors don't include the input cell itself
   - Test that there are no duplicate neighbors

### Property-Based Tests

Property-based tests will verify correctness properties across many random inputs:

1. **Property 0: H3 GetNeighbors returns adjacent cells** (PBT) - **PRIMARY FIX**
   - Generate random H3 cell indices at various resolutions
   - Call GetNeighbors() on each
   - Decode all returned neighbors
   - Verify each neighbor is within 2 * cellSize distance from the input cell
   - Verify neighbor count is 6 (or 5 for pentagons)

2. **Property 2: Multiple cells for large radius** (PBT)
   - Generate random center points
   - Use radius = 20 * cellSize
   - Verify result contains > 1 cell (this will pass once GetNeighbors is fixed)

3. **Property 7: H3 cell count scales with area** (PBT)
   - Generate random center points
   - Compute cell covering for radius R and 2R
   - Verify cells(2R) ≈ 4 * cells(R) (within tolerance, accounting for maxCells)

### Integration Tests

Integration tests will verify the fix works end-to-end with DynamoDB:

1. **30km radius query returns many cells** (the original failing test):
   - Create stores in a 30km radius
   - Execute SpatialQueryAsync with 30km radius using H3
   - Verify many stores are returned (not just 1)
   - Verify all returned stores are within 30km
   - **This test will pass once GetNeighbors() is fixed**

2. **H3 matches S2 behavior**:
   - Run identical queries with both H3 and S2
   - Verify both return similar numbers of cells
   - Verify both return similar numbers of results

3. **Different H3 resolutions**:
   - Test H3 resolutions 5, 7, 9
   - Verify cell counts match expectations from requirements

## Implementation Details

### The Fix: H3Encoder.GetNeighbors()

The GetNeighbors() method needs to be investigated and corrected. The current implementation is returning cell indices that are not adjacent to the input cell.

**Investigation needed:**
1. Review the H3 neighbor calculation algorithm
2. Check if there's an issue with coordinate system transformations
3. Verify the IJK/FaceIJK neighbor logic
4. Compare with the H3 reference implementation

**Expected behavior:**
- Input: H3 cell index (e.g., `89283082803ffff`)
- Output: Array of 6 neighbor indices (or 5 for pentagons)
- Each neighbor should decode to a location adjacent to the input cell (within ~2 * cellSize)

**Current buggy behavior:**
- Input: `89283082803ffff` (San Francisco)
- Output: Indices like `890208610d...` 
- These decode to locations thousands of km away (Northern Canada)

### No Changes Needed

The following components are working correctly and require no changes:
- H3CellCovering.GetCellsForBoundingBoxInternal() - ring expansion algorithm is correct
- H3Encoder.Encode() - encoding works correctly
- H3Encoder.Decode() - decoding works correctly
- S2CellCovering - entire S2 implementation works correctly

```csharp
private static List<(string Token, double Distance)> GetCellsForBoundingBoxInternal(
    GeoBoundingBox boundingBox,
    GeoLocation center,
    int level,
    int maxCells)
{
    var cellSizeKm = GetApproximateCellSizeKm(level);
    
    // Expand the bounding box by cellSize to catch boundary cells
    var expandedBbox = new GeoBoundingBox(
        new GeoLocation(
            Math.Max(-90, boundingBox.Southwest.Latitude - cellSizeKm / 111.32),
            Math.Max(-180, boundingBox.Southwest.Longitude - cellSizeKm / (111.32 * Math.Cos(boundingBox.Southwest.Latitude * Math.PI / 180)))),
        new GeoLocation(
            Math.Min(90, boundingBox.Northeast.Latitude + cellSizeKm / 111.32),
            Math.Min(180, boundingBox.Northeast.Longitude + cellSizeKm / (111.32 * Math.Cos(boundingBox.Northeast.Latitude * Math.PI / 180)))));
    
    var cellSet = new HashSet<string>();
    var cellsWithDistance = new List<(string Token, double Distance)>();
    
    // Start with the center cell
    var centerToken = S2Encoder.Encode(center.Latitude, center.Longitude, level);
    cellSet.Add(centerToken);
    var centerDistance = center.DistanceToKilometers(center);
    cellsWithDistance.Add((centerToken, centerDistance));
    
    // Expand rings until we cover the bounding box or hit maxCells
    var currentRing = new HashSet<string> { centerToken };
    var visited = new HashSet<string> { centerToken };
    
    while (cellSet.Count < maxCells)
    {
        var nextRing = new HashSet<string>();
        
        foreach (var cellToken in currentRing)
        {
            var neighbors = S2Encoder.GetNeighbors(cellToken);
            
            foreach (var neighbor in neighbors)
            {
                if (visited.Contains(neighbor))
                    continue;
                
                visited.Add(neighbor);
                
                // Decode neighbor to get its center point
                var (lat, lon) = S2Encoder.Decode(neighbor);
                var neighborLocation = new GeoLocation(lat, lon);
                
                // Check if this cell intersects the expanded bounding box
                if (expandedBbox.Contains(neighborLocation))
                {
                    cellSet.Add(neighbor);
                    var distance = center.DistanceToKilometers(neighborLocation);
                    cellsWithDistance.Add((neighbor, distance));
                    nextRing.Add(neighbor);
                    
                    if (cellSet.Count >= maxCells)
                        break;
                }
            }
            
            if (cellSet.Count >= maxCells)
                break;
        }
        
        // If no new cells were added, we've covered the area
        if (nextRing.Count == 0)
            break;
        
        currentRing = nextRing;
    }
    
    // Sort by distance and return
    return cellsWithDistance
        .OrderBy(x => x.Distance)
        .Take(maxCells)
        .ToList();
}
```

**Key Improvement**: Properly expand the bounding box by converting cellSize (km) to degrees, accounting for latitude-dependent longitude scaling.

## Migration Strategy

This is a bug fix, not a breaking change. The public API remains unchanged:
- `GetCellsForRadius(...)` - same signature
- `GetCellsForBoundingBox(...)` - same signature

Existing code will automatically benefit from the fix without any changes.

## Performance Considerations

### Before Fix
- Only 1 cell returned for 30km radius
- Very fast (no ring expansion)
- **Incorrect results**

### After Fix
- Correct number of cells returned
- Ring expansion continues until area is covered
- Still efficient due to:
  - HashSet for O(1) visited checks
  - Early termination at maxCells
  - Distance-based filtering (no expensive geometric operations)

### Expected Performance
- 30km radius, S2 level 16: ~400-600 cells, < 50ms
- 30km radius, H3 resolution 9: ~100 cells (maxCells limit), < 20ms
- 100km radius, S2 level 14: ~100 cells (maxCells limit), < 30ms

The fix will be slower than the buggy version (which only returned 1 cell), but still fast enough for production use.
