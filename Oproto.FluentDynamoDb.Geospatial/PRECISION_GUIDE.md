# Precision and Resolution Selection Guide

This guide helps you choose the optimal precision/resolution level for your spatial queries to balance accuracy with performance.

## Table of Contents

- [Understanding Cell Sizes](#understanding-cell-sizes)
- [Query Explosion Warning](#query-explosion-warning)
- [Cell Count Formula](#cell-count-formula)
- [Decision Matrix](#decision-matrix)
- [Real-World Scenarios](#real-world-scenarios)
- [MaxCells Limit](#maxcells-limit)
- [Trade-offs](#trade-offs)

## Understanding Cell Sizes

Spatial indices divide the Earth into cells at different precision levels. Higher precision = smaller cells = more accurate queries but more DynamoDB queries.

### GeoHash Precision Levels (1-12)

| Precision | Cell Width | Cell Height | Area | Example Use Case |
|-----------|------------|-------------|------|------------------|
| 1 | ±2500 km | ±5000 km | Continental | Country-level |
| 2 | ±630 km | ±1250 km | Regional | State/province |
| 3 | ±78 km | ±156 km | Large city | Metropolitan area |
| 4 | ±20 km | ±39 km | City | City-wide search |
| 5 | ±2.4 km | ±4.9 km | Neighborhood | District search |
| **6** | **±610 m** | **±1.2 km** | **~0.73 km²** | **Default - Local area** |
| 7 | ±76 m | ±153 m | ~0.012 km² | Street-level |
| 8 | ±19 m | ±38 m | ~0.0007 km² | Building-level |
| 9 | ±4.8 m | ±4.8 m | ~0.00002 km² | Precise location |
| 10 | ±1.2 m | ±0.6 m | Sub-meter | Very precise |
| 11 | ±0.149 m | ±0.149 m | Centimeter | Extremely precise |
| 12 | ±0.037 m | ±0.019 m | Millimeter | Maximum precision |

### S2 Levels (0-30)

| Level | Cell Size (approx) | Area | Example Use Case |
|-------|-------------------|------|------------------|
| 0 | ~85,000 km | Entire face | Global |
| 5 | ~2,700 km | Continental | Continent |
| 8 | ~340 km | Regional | Large region |
| 10 | ~100 km | ~10,000 km² | Metropolitan area |
| 12 | ~25 km | ~625 km² | City-wide |
| 14 | ~6 km | ~36 km² | District |
| **16** | **~1.5 km** | **~2.25 km²** | **Default - Neighborhood** |
| 18 | ~400 m | ~0.16 km² | Street-level |
| 20 | ~100 m | ~0.01 km² | Building-level |
| 22 | ~25 m | ~0.0006 km² | Precise location |
| 24 | ~6 m | ~0.00004 km² | Very precise |
| 26 | ~1.5 m | Sub-meter | Extremely precise |
| 28 | ~40 cm | Centimeter | Maximum precision |
| 30 | ~10 cm | Millimeter | Ultra-precise |

### H3 Resolutions (0-15)

| Resolution | Hexagon Edge | Area | Example Use Case |
|------------|--------------|------|------------------|
| 0 | ~1,108 km | ~4,357,449 km² | Global |
| 1 | ~418 km | ~609,788 km² | Continental |
| 2 | ~158 km | ~86,801 km² | Regional |
| 3 | ~60 km | ~12,393 km² | Large region |
| 4 | ~23 km | ~1,770 km² | Metropolitan |
| 5 | ~8.5 km | ~253 km² | City-wide |
| 6 | ~3.2 km | ~36 km² | District |
| **7** | **~1.2 km** | **~5.2 km²** | **Default - Neighborhood** |
| 8 | ~460 m | ~0.74 km² | Local area |
| 9 | ~174 m | ~0.11 km² | Street-level |
| 10 | ~66 m | ~0.015 km² | Building-level |
| 11 | ~25 m | ~0.002 km² | Precise location |
| 12 | ~9.4 m | ~0.0003 km² | Very precise |
| 13 | ~3.5 m | ~0.00004 km² | Extremely precise |
| 14 | ~1.3 m | Sub-meter | Maximum precision |
| 15 | ~0.5 m | Centimeter | Ultra-precise |

## Query Explosion Warning

⚠️ **CRITICAL**: Choosing too high a precision for your search radius can cause "query explosion" - generating thousands of cells that make queries extremely slow or incomplete.

### The Problem

DynamoDB spatial queries work by:
1. Computing which cells cover your search area
2. Querying each cell individually
3. Merging and deduplicating results

**If you use small cells with a large radius, you'll need to query MANY cells!**

### Cell Count Formula

The approximate number of cells needed to cover a circular area:

```
cellCount ≈ π × (radius / cellSize)²
```

**Example Calculations:**

**Good Example** - 10km radius with S2 Level 14 (~6km cells):
```
cellCount ≈ π × (10 / 6)² ≈ π × 2.78 ≈ 8.7 cells ✅
```

**Bad Example** - 10km radius with S2 Level 18 (~400m cells):
```
cellCount ≈ π × (10000 / 400)² ≈ π × 625 ≈ 1,963 cells 🚫
```

### Query Explosion Examples

#### Example 1: 10km Radius with Different Precisions

**GeoHash:**
| Precision | Cell Size | Cell Count | Status |
|-----------|-----------|------------|--------|
| 4 | ~20 km | 1 cell | ✅ Excellent |
| 5 | ~2.4 km | ~55 cells | ✅ Good |
| 6 | ~610 m | ~350 cells | ⚠️ Warning - will hit maxCells limit |
| 7 | ~76 m | ~22,000 cells | 🚫 Query explosion! |

**S2:**
| Level | Cell Size | Cell Count | Status |
|-------|-----------|------------|--------|
| 12 | ~25 km | 2 cells | ✅ Excellent |
| 14 | ~6 km | 11 cells | ✅ Excellent |
| 16 | ~1.5 km | 175 cells | ⚠️ Warning - will hit maxCells limit |
| 18 | ~400 m | 2,500 cells | 🚫 Query explosion! |
| 20 | ~100 m | 40,000 cells | 🚫 Extreme query explosion! |

**H3:**
| Resolution | Cell Edge | Cell Count | Status |
|------------|-----------|------------|--------|
| 5 | ~8.5 km | 6 cells | ✅ Excellent |
| 6 | ~3.2 km | 40 cells | ✅ Good |
| 7 | ~1.2 km | 280 cells | ⚠️ Warning - will hit maxCells limit |
| 8 | ~460 m | 1,900 cells | 🚫 Query explosion! |
| 9 | ~174 m | 13,000 cells | 🚫 Extreme query explosion! |

#### Example 2: 50km Radius with Different Precisions

**S2:**
| Level | Cell Size | Cell Count | Status |
|-------|-----------|------------|--------|
| 10 | ~100 km | 1 cell | ✅ Excellent |
| 12 | ~25 km | 16 cells | ✅ Excellent |
| 14 | ~6 km | 275 cells | ⚠️ Warning - will hit maxCells limit |
| 16 | ~1.5 km | 4,400 cells | 🚫 Query explosion! |
| 18 | ~400 m | 62,500 cells | 🚫 Extreme query explosion! |

**H3:**
| Resolution | Cell Edge | Cell Count | Status |
|------------|-----------|------------|--------|
| 4 | ~23 km | 20 cells | ✅ Excellent |
| 5 | ~8.5 km | 140 cells | ⚠️ Warning - will hit maxCells limit |
| 6 | ~3.2 km | 1,000 cells | 🚫 Query explosion! |
| 7 | ~1.2 km | 7,000 cells | 🚫 Extreme query explosion! |

### What Happens During Query Explosion?

1. **MaxCells Limit Hit**: Default limit is 100 cells
   - Only the first 100 cells are queried
   - **Your results are INCOMPLETE** - you're missing data!
   - No error is thrown - you just get partial results

2. **Slow Paginated Queries**: If you increase maxCells
   - Each cell is queried sequentially in paginated mode
   - 1,000 cells = 1,000 sequential DynamoDB queries
   - At ~50ms per query = 50 seconds total!

3. **High Costs**: More queries = higher DynamoDB costs
   - 1,000 queries = 1,000 read capacity units consumed
   - Can quickly exhaust provisioned capacity

## Decision Matrix

Use this matrix to choose the right precision for your search radius:

### For GeoHash

| Search Radius | Recommended Precision | Cell Count | Notes |
|---------------|----------------------|------------|-------|
| < 1 km | 6-7 | 1-10 | Excellent |
| 1-5 km | 5-6 | 10-50 | Good |
| 5-10 km | 4-5 | 20-100 | Acceptable |
| 10-50 km | 3-4 | 50-200 | Use with caution |
| > 50 km | 2-3 | 100+ | Consider S2/H3 |

### For S2

| Search Radius | Recommended Level | Cell Count | Notes |
|---------------|------------------|------------|-------|
| < 1 km | 18-20 | 1-10 | Excellent |
| 1-5 km | 16-18 | 10-50 | Good |
| 5-10 km | 14-16 | 20-100 | Acceptable |
| 10-50 km | 12-14 | 50-200 | Good |
| 50-100 km | 10-12 | 100-300 | Use with caution |
| > 100 km | 8-10 | 200+ | Consider lower precision |

### For H3

| Search Radius | Recommended Resolution | Cell Count | Notes |
|---------------|----------------------|------------|-------|
| < 1 km | 10-11 | 1-10 | Excellent |
| 1-5 km | 8-10 | 10-50 | Good |
| 5-10 km | 7-8 | 20-100 | Acceptable |
| 10-50 km | 5-7 | 50-200 | Good |
| 50-100 km | 4-5 | 100-300 | Use with caution |
| > 100 km | 2-4 | 200+ | Consider lower resolution |

### Quick Reference Rules

**Rule of Thumb**: Cell size should be **20-50% of your search radius**

```
Optimal cell size ≈ radius / 3
```

**Examples:**
- 10km radius → ~3km cells → S2 Level 14 or H3 Resolution 6
- 5km radius → ~1.5km cells → S2 Level 16 or H3 Resolution 7
- 1km radius → ~300m cells → S2 Level 18 or H3 Resolution 9

## Real-World Scenarios

### Scenario 1: Restaurant Finder App

**Requirements:**
- Users search for restaurants within 5km
- Need accurate results
- Fast response time important

**Recommendation:**
```csharp
// S2 Level 16 (~1.5km cells)
[DynamoDbAttribute("location", SpatialIndexType = SpatialIndexType.S2, S2Level = 16)]
public GeoLocation Location { get; set; }

// Cell count: π × (5 / 1.5)² ≈ 35 cells ✅
// Query time: ~50ms (non-paginated, parallel)
```

**Why this works:**
- 35 cells is well under the 100 cell limit
- All queries execute in parallel
- Results are accurate within 1.5km
- Fast response time

### Scenario 2: Delivery Zone Management

**Requirements:**
- Define delivery zones for drivers
- Zones are typically 10-20km radius
- Need uniform coverage for visualization

**Recommendation:**
```csharp
// H3 Resolution 7 (~1.2km hexagons)
[DynamoDbAttribute("zone_center", SpatialIndexType = SpatialIndexType.H3, H3Resolution = 7)]
public GeoLocation ZoneCenter { get; set; }

// For 15km radius: π × (15 / 1.2)² ≈ 620 cells
// This will hit maxCells limit!
```

**Better approach - use lower resolution:**
```csharp
// H3 Resolution 6 (~3.2km hexagons)
[DynamoDbAttribute("zone_center", SpatialIndexType = SpatialIndexType.H3, H3Resolution = 6)]
public GeoLocation ZoneCenter { get; set; }

// For 15km radius: π × (15 / 3.2)² ≈ 70 cells ✅
// Query time: ~50ms (non-paginated, parallel)
```

### Scenario 3: Global Asset Tracking

**Requirements:**
- Track assets worldwide (including polar regions)
- Queries vary from 1km to 100km radius
- Need consistent performance

**Recommendation - Multiple Precision Levels:**
```csharp
public partial class Asset
{
    // Low precision for large area queries (50-100km)
    [DynamoDbAttribute("location_region", SpatialIndexType = SpatialIndexType.S2, S2Level = 12)]
    public GeoLocation LocationRegion => Location;
    
    // Medium precision for city queries (10-50km)
    [DynamoDbAttribute("location_city", SpatialIndexType = SpatialIndexType.S2, S2Level = 14)]
    public GeoLocation LocationCity => Location;
    
    // High precision for local queries (1-10km)
    [DynamoDbAttribute("location_local", SpatialIndexType = SpatialIndexType.S2, S2Level = 16)]
    public GeoLocation LocationLocal => Location;
    
    [DynamoDbAttribute("location")]
    public GeoLocation Location { get; set; }
}

// Query logic chooses appropriate attribute based on radius
var attributeName = radiusKm switch
{
    <= 10 => "location_local",   // S2 Level 16
    <= 50 => "location_city",    // S2 Level 14
    _ => "location_region"       // S2 Level 12
};
```

### Scenario 4: Store Locator with Variable Radius

**Requirements:**
- Users can search 1km, 5km, 10km, or 25km
- Most searches are 5km
- Need to optimize for common case

**Recommendation:**
```csharp
// Optimize for 5km searches (most common)
[DynamoDbAttribute("location", SpatialIndexType = SpatialIndexType.S2, S2Level = 16)]
public GeoLocation Location { get; set; }

// Cell counts:
// 1km: π × (1 / 1.5)² ≈ 2 cells ✅ Excellent
// 5km: π × (5 / 1.5)² ≈ 35 cells ✅ Good
// 10km: π × (10 / 1.5)² ≈ 140 cells ⚠️ Will hit maxCells limit
// 25km: π × (25 / 1.5)² ≈ 870 cells 🚫 Query explosion
```

**Handle large radius queries differently:**
```csharp
public async Task<List<Store>> SearchStores(GeoLocation center, double radiusKm)
{
    if (radiusKm > 10)
    {
        // For large radius, use bounding box instead
        var bbox = GeoBoundingBox.FromCenterAndDistanceKilometers(center, radiusKm);
        var result = await table.SpatialQueryAsync(
            spatialAttributeName: "location",
            boundingBox: bbox,
            queryBuilder: ...,
            pageSize: null
        );
        
        // Post-filter by exact distance
        return result.Items
            .Where(s => s.Location.DistanceToKilometers(center) <= radiusKm)
            .OrderBy(s => s.Location.DistanceToKilometers(center))
            .ToList();
    }
    else
    {
        // For small radius, use proximity query
        var result = await table.SpatialQueryAsync(
            spatialAttributeName: "location",
            center: center,
            radiusKilometers: radiusKm,
            queryBuilder: ...,
            pageSize: null
        );
        
        return result.Items;
    }
}
```

### Scenario 5: Real-Time Vehicle Tracking

**Requirements:**
- Track vehicles in real-time
- Need precise locations (within 50m)
- Queries are typically 2-3km radius

**Recommendation:**
```csharp
// H3 Resolution 10 (~66m hexagons)
[DynamoDbAttribute("location", SpatialIndexType = SpatialIndexType.H3, H3Resolution = 10)]
public GeoLocation Location { get; set; }

// For 2km radius: π × (2000 / 66)² ≈ 2,900 cells 🚫
// This is query explosion!
```

**Better approach - use lower resolution:**
```csharp
// H3 Resolution 9 (~174m hexagons)
[DynamoDbAttribute("location", SpatialIndexType = SpatialIndexType.H3, H3Resolution = 9)]
public GeoLocation Location { get; set; }

// For 2km radius: π × (2000 / 174)² ≈ 415 cells ⚠️
// Still too many! Will hit maxCells limit
```

**Best approach - use even lower resolution:**
```csharp
// H3 Resolution 8 (~460m hexagons)
[DynamoDbAttribute("location", SpatialIndexType = SpatialIndexType.H3, H3Resolution = 8)]
public GeoLocation Location { get; set; }

// For 2km radius: π × (2000 / 460)² ≈ 60 cells ✅
// Query time: ~50ms (non-paginated, parallel)
// Accuracy: ±460m (acceptable for vehicle tracking)
```

## MaxCells Limit

### What is MaxCells?

The `maxCells` parameter limits the number of cells queried to prevent query explosion. Default is **100 cells**.

### When MaxCells is Reached

**Non-Paginated Mode:**
```csharp
var result = await table.SpatialQueryAsync(
    center: center,
    radiusKilometers: 10,
    queryBuilder: ...,
    pageSize: null,  // Non-paginated
    maxCells: 100    // Default
);

// If 200 cells are needed:
// - Only first 100 cells are queried
// - Results are INCOMPLETE
// - No error is thrown
// - You're missing ~50% of the data!
```

**Paginated Mode:**
```csharp
var result = await table.SpatialQueryAsync(
    center: center,
    radiusKilometers: 10,
    queryBuilder: ...,
    pageSize: 50,    // Paginated
    maxCells: 100    // Default
);

// If 200 cells are needed:
// - Queries cells sequentially until pageSize reached
// - May stop before reaching maxCells
// - Continuation token allows resuming
// - Eventually all cells will be queried across multiple pages
```

### Adjusting MaxCells

You can increase maxCells, but be aware of the trade-offs:

```csharp
// Increase maxCells for non-paginated queries
var result = await table.SpatialQueryAsync(
    center: center,
    radiusKilometers: 20,
    queryBuilder: ...,
    pageSize: null,
    maxCells: 500    // Increased from 100
);

// Trade-offs:
// ✅ More complete results
// ❌ More parallel queries (higher memory usage)
// ❌ Higher DynamoDB costs
// ❌ Potential timeout issues
```

### Monitoring MaxCells

Always monitor if you're hitting the limit:

```csharp
var result = await table.SpatialQueryAsync(...);

if (result.TotalCellsQueried >= maxCells)
{
    Console.WriteLine($"⚠️ WARNING: Hit maxCells limit!");
    Console.WriteLine($"Results may be incomplete.");
    Console.WriteLine($"Consider:");
    Console.WriteLine($"  - Reducing search radius");
    Console.WriteLine($"  - Lowering precision level");
    Console.WriteLine($"  - Increasing maxCells (with caution)");
}
```

## Trade-offs

### Higher Precision (Smaller Cells)

**Advantages:**
- ✅ More accurate results
- ✅ Less post-filtering needed
- ✅ Better for small search areas

**Disadvantages:**
- ❌ More cells to query
- ❌ Slower paginated queries
- ❌ Higher DynamoDB costs
- ❌ Risk of hitting maxCells limit
- ❌ Incomplete results if limit is hit

### Lower Precision (Larger Cells)

**Advantages:**
- ✅ Fewer cells to query
- ✅ Faster queries
- ✅ Lower DynamoDB costs
- ✅ Better for large search areas
- ✅ Less likely to hit maxCells limit

**Disadvantages:**
- ❌ Less accurate results
- ❌ More post-filtering needed
- ❌ May return items outside search area

### The Sweet Spot

**Optimal Configuration:**
- Cell size ≈ radius / 3
- Cell count: 20-50 cells
- Query time: ~50ms
- Accuracy: Within 1-2 cell sizes

**Example:**
```csharp
// 5km radius with S2 Level 16 (~1.5km cells)
// Cell count: ~35 cells
// Accuracy: ±1.5km
// Query time: ~50ms
// Perfect balance! ✅
```

## Calculation Tool

Use this formula to estimate cell count before deploying:

```csharp
public static int EstimateCellCount(double radiusKm, double cellSizeKm)
{
    return (int)Math.Ceiling(Math.PI * Math.Pow(radiusKm / cellSizeKm, 2));
}

// Example usage:
var cellCount = EstimateCellCount(radiusKm: 10, cellSizeKm: 1.5);
Console.WriteLine($"Estimated cells: {cellCount}");

if (cellCount > 100)
{
    Console.WriteLine("⚠️ WARNING: Will hit maxCells limit!");
    Console.WriteLine($"Recommend using larger cells.");
    
    // Calculate recommended cell size
    var recommendedCellSize = radiusKm / 3;
    Console.WriteLine($"Recommended cell size: ~{recommendedCellSize:F1}km");
}
```

## Summary

### Key Takeaways

1. **Cell size should be 20-50% of search radius**
2. **Target 20-50 cells for optimal performance**
3. **Watch out for query explosion with high precision + large radius**
4. **Monitor TotalCellsQueried in production**
5. **Consider multiple precision levels for varying search radii**
6. **MaxCells limit (default 100) prevents runaway queries**
7. **Non-paginated mode is fastest but uses more memory**
8. **Paginated mode is slower but memory-efficient**

### Quick Decision Guide

**Small search area (< 5km):**
- Use high precision (S2 Level 16-18, H3 Resolution 9-10)
- Non-paginated mode for speed
- ~10-50 cells

**Medium search area (5-20km):**
- Use medium precision (S2 Level 14-16, H3 Resolution 7-8)
- Non-paginated or paginated depending on result size
- ~50-100 cells

**Large search area (> 20km):**
- Use low precision (S2 Level 12-14, H3 Resolution 5-7)
- Paginated mode recommended
- ~100-200 cells (watch maxCells limit!)

**When in doubt, start with defaults and adjust based on monitoring!**

## Additional Resources

- [S2 and H3 Usage Guide](S2_H3_USAGE_GUIDE.md) - Choosing between index types
- [Performance Guide](PERFORMANCE_GUIDE.md) - Query optimization
- [Examples](EXAMPLES.md) - Code examples for different scenarios
