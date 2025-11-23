# S2 and H3 Spatial Index Usage Guide

This guide helps you choose between GeoHash, S2, and H3 spatial indexing systems for your DynamoDB geospatial queries.

## Table of Contents

- [Overview](#overview)
- [Comparison Table](#comparison-table)
- [When to Use Each Index Type](#when-to-use-each-index-type)
- [Precision and Resolution Levels](#precision-and-resolution-levels)
- [Code Examples](#code-examples)
- [Performance Characteristics](#performance-characteristics)

## Overview

Oproto.FluentDynamoDb.Geospatial supports three spatial indexing systems:

- **GeoHash**: Simple Z-order curve encoding, good for general-purpose queries
- **S2**: Google's spherical geometry system with better area uniformity, especially near poles
- **H3**: Uber's hexagonal hierarchical system with uniform neighbor distances

All three systems encode geographic coordinates into sortable strings that enable efficient DynamoDB BETWEEN queries.

## Comparison Table

| Feature | GeoHash | S2 | H3 |
|---------|---------|----|----|
| **Cell Shape** | Rectangle | Square (on cube face) | Hexagon |
| **Precision Levels** | 1-12 | 0-30 | 0-15 |
| **Default Precision** | 6 (~610m) | 16 (~600m) | 7 (~1.2km edge) |
| **Area Uniformity** | Poor near poles | Good everywhere | Excellent everywhere |
| **Neighbor Count** | 8 | 8 | 6 (5 for pentagons) |
| **Children per Cell** | 32 | 4 | 7 |
| **Query Complexity** | Single BETWEEN | Multiple queries | Multiple queries |
| **Encoding Speed** | Fast | Fast | Moderate |
| **Best For** | Simple queries | Global coverage | Uniform coverage |
| **Pole Handling** | Poor | Good | Excellent |
| **Date Line Handling** | Requires special care | Good | Good |

## When to Use Each Index Type

### Use GeoHash When:

✅ **Simple queries** - You need straightforward proximity queries  
✅ **Low latency** - Single BETWEEN query is fastest  
✅ **Mid-latitudes** - Your data is primarily between ±60° latitude  
✅ **Backward compatibility** - Migrating from existing GeoHash systems  
✅ **Minimal complexity** - You want the simplest implementation

**Example Use Cases:**
- Store locators in continental US/Europe
- Restaurant finders in cities
- Delivery zone calculations
- Simple "near me" features

### Use S2 When:

✅ **Global coverage** - Your data spans the entire globe  
✅ **Polar regions** - You need accurate queries near poles  
✅ **Hierarchical queries** - You need parent/child cell relationships  
✅ **Area uniformity** - Consistent cell sizes matter  
✅ **Google ecosystem** - Integrating with other Google S2 systems

**Example Use Cases:**
- Global asset tracking
- Satellite data indexing
- Weather data queries
- Flight path analysis
- Arctic/Antarctic research data

### Use H3 When:

✅ **Uniform coverage** - You need the most uniform cell sizes  
✅ **Neighbor analysis** - Hexagonal neighbors are important  
✅ **Grid analysis** - You're doing spatial grid computations  
✅ **Uber ecosystem** - Integrating with other H3 systems  
✅ **Visual appeal** - Hexagons look better on maps

**Example Use Cases:**
- Ride-sharing zone definitions
- Delivery territory optimization
- Urban planning analysis
- Heatmap generation
- Coverage area visualization

## Precision and Resolution Levels

### GeoHash Precision Levels (1-12)

| Precision | Cell Size | Typical Use Case |
|-----------|-----------|------------------|
| 4 | ~20 km × 20 km | City-wide searches |
| 5 | ~2.4 km × 4.9 km | Neighborhood searches |
| **6** | **~610 m × 1.2 km** | **Default - District searches** |
| 7 | ~76 m × 153 m | Street-level searches |
| 8 | ~19 m × 38 m | Building-level searches |
| 9 | ~4.8 m × 4.8 m | Precise location tracking |

### S2 Levels (0-30)

| Level | Cell Size (approx) | Typical Use Case |
|-------|-------------------|------------------|
| 10 | ~100 km × 100 km | Regional searches |
| 12 | ~25 km × 25 km | City-wide searches |
| 14 | ~6 km × 6 km | District searches |
| **16** | **~1.5 km × 1.5 km** | **Default - Neighborhood searches** |
| 18 | ~400 m × 400 m | Street-level searches |
| 20 | ~100 m × 100 m | Building-level searches |
| 22 | ~25 m × 25 m | Precise location tracking |

### H3 Resolutions (0-15)

| Resolution | Hexagon Edge Length | Typical Use Case |
|------------|---------------------|------------------|
| 5 | ~8.5 km | City-wide searches |
| 6 | ~3.2 km | District searches |
| 7 | ~1.2 km | Neighborhood searches |
| 8 | ~460 m | Local area searches |
| **7** | **~1.2 km** | **Default - Neighborhood searches** |
| 10 | ~66 m | Building-level searches |
| 11 | ~25 m | Precise location tracking |

**Note**: H3 hexagons have more uniform area than GeoHash/S2 squares, making them ideal for grid-based analysis.

## Code Examples

### Basic Setup with S2

```csharp
using Oproto.FluentDynamoDb.Attributes;
using Oproto.FluentDynamoDb.Geospatial;

[DynamoDbTable("stores")]
public partial class Store
{
    [PartitionKey]
    [DynamoDbAttribute("pk")]
    public string StoreId { get; set; }
    
    // Use S2 with level 16 (~1.5km cells)
    [SortKey]
    [DynamoDbAttribute("location", SpatialIndexType = SpatialIndexType.S2, S2Level = 16)]
    public GeoLocation Location { get; set; }
    
    [DynamoDbAttribute("name")]
    public string Name { get; set; }
}
```

### Basic Setup with H3

```csharp
[DynamoDbTable("delivery_zones")]
public partial class DeliveryZone
{
    [PartitionKey]
    [DynamoDbAttribute("pk")]
    public string ZoneId { get; set; }
    
    // Use H3 with resolution 7 (~1.2km edge)
    [SortKey]
    [DynamoDbAttribute("center", SpatialIndexType = SpatialIndexType.H3, H3Resolution = 7)]
    public GeoLocation Center { get; set; }
    
    [DynamoDbAttribute("name")]
    public string Name { get; set; }
}
```

### Proximity Query with S2 (Non-Paginated)

```csharp
using Oproto.FluentDynamoDb.Geospatial;

// Find ALL stores within 5km - fastest approach
var center = new GeoLocation(37.7749, -122.4194); // San Francisco
var result = await storeTable.SpatialQueryAsync(
    spatialAttributeName: "location",
    center: center,
    radiusKilometers: 5,
    queryBuilder: (query, cell, pagination) => query
        .Where<Store>(x => x.PartitionKey == "STORE" && x.Location == cell)
        .Paginate(pagination),
    pageSize: null  // No pagination - queries all cells in parallel
);

Console.WriteLine($"Found {result.Items.Count} stores");
Console.WriteLine($"Queried {result.TotalCellsQueried} S2 cells in parallel");

// Results are automatically sorted by distance from center
foreach (var store in result.Items)
{
    var distance = store.Location.DistanceToKilometers(center);
    Console.WriteLine($"{store.Name}: {distance:F2} km away");
}
```

### Proximity Query with H3 (Paginated)

```csharp
// Find stores within 10km, paginated (50 per page)
var center = new GeoLocation(37.7749, -122.4194);
var result = await storeTable.SpatialQueryAsync(
    spatialAttributeName: "location",
    center: center,
    radiusKilometers: 10,
    queryBuilder: (query, cell, pagination) => query
        .Where<Store>(x => x.PartitionKey == "STORE" && x.Location == cell)
        .Paginate(pagination),
    pageSize: 50  // Paginated - queries cells sequentially in spiral order
);

Console.WriteLine($"Found {result.Items.Count} stores (page 1)");
Console.WriteLine($"Queried {result.TotalCellsQueried} H3 cells");
Console.WriteLine($"Has more results: {result.ContinuationToken != null}");

// Get next page if available
if (result.ContinuationToken != null)
{
    var nextPage = await storeTable.SpatialQueryAsync(
        spatialAttributeName: "location",
        center: center,
        radiusKilometers: 10,
        queryBuilder: (query, cell, pagination) => query
            .Where<Store>(x => x.PartitionKey == "STORE" && x.Location == cell)
            .Paginate(pagination),
        pageSize: 50,
        continuationToken: result.ContinuationToken
    );
    
    Console.WriteLine($"Found {nextPage.Items.Count} more stores (page 2)");
}
```

### Bounding Box Query with S2

```csharp
// Define a rectangular area
var southwest = new GeoLocation(37.7, -122.5);
var northeast = new GeoLocation(37.8, -122.4);
var bbox = new GeoBoundingBox(southwest, northeast);

var result = await storeTable.SpatialQueryAsync(
    spatialAttributeName: "location",
    boundingBox: bbox,
    queryBuilder: (query, cell, pagination) => query
        .Where<Store>(x => x.PartitionKey == "STORE" && x.Location == cell)
        .Paginate(pagination),
    pageSize: null  // Get all results
);

Console.WriteLine($"Found {result.Items.Count} stores in bounding box");
```

### Working with S2 Cells Directly

```csharp
using Oproto.FluentDynamoDb.Geospatial.S2;

var location = new GeoLocation(37.7749, -122.4194);

// Convert to S2 cell
var cell = location.ToS2Cell(level: 16);
Console.WriteLine($"S2 Token: {cell.Token}");
Console.WriteLine($"Level: {cell.Level}");
Console.WriteLine($"Bounds: {cell.Bounds}");

// Get neighboring cells
var neighbors = cell.GetNeighbors();
Console.WriteLine($"Found {neighbors.Length} neighbors"); // 8 neighbors

// Get parent cell (lower precision)
var parent = cell.GetParent();
Console.WriteLine($"Parent level: {parent.Level}"); // Level 15

// Get child cells (higher precision)
var children = cell.GetChildren();
Console.WriteLine($"Found {children.Length} children"); // 4 children
```

### Working with H3 Cells Directly

```csharp
using Oproto.FluentDynamoDb.Geospatial.H3;

var location = new GeoLocation(37.7749, -122.4194);

// Convert to H3 cell
var cell = location.ToH3Cell(resolution: 7);
Console.WriteLine($"H3 Index: {cell.Index}");
Console.WriteLine($"Resolution: {cell.Resolution}");
Console.WriteLine($"Bounds: {cell.Bounds}");

// Get neighboring cells (hexagons have 6 neighbors, pentagons have 5)
var neighbors = cell.GetNeighbors();
Console.WriteLine($"Found {neighbors.Length} neighbors"); // Usually 6

// Get parent cell (lower resolution)
var parent = cell.GetParent();
Console.WriteLine($"Parent resolution: {parent.Resolution}"); // Resolution 6

// Get child cells (higher resolution)
var children = cell.GetChildren();
Console.WriteLine($"Found {children.Length} children"); // 7 children (aperture-7)
```

### Comparing All Three Index Types

```csharp
var location = new GeoLocation(37.7749, -122.4194);

// Encode with all three systems
var geohash = location.ToGeoHash(precision: 7);
var s2Token = location.ToS2Token(level: 16);
var h3Index = location.ToH3Index(resolution: 7);

Console.WriteLine($"GeoHash: {geohash}");     // "9q8yy9r"
Console.WriteLine($"S2 Token: {s2Token}");    // "89c25985"
Console.WriteLine($"H3 Index: {h3Index}");    // "8928308280fffff"

// Decode back to locations
var fromGeoHash = GeoLocation.FromGeoHash(geohash);
var fromS2 = GeoLocation.FromS2Token(s2Token);
var fromH3 = GeoLocation.FromH3Index(h3Index);

// All should be close to the original location (within cell precision)
Console.WriteLine($"GeoHash distance: {location.DistanceToMeters(fromGeoHash):F2}m");
Console.WriteLine($"S2 distance: {location.DistanceToMeters(fromS2):F2}m");
Console.WriteLine($"H3 distance: {location.DistanceToMeters(fromH3):F2}m");
```

### Using with Global Secondary Index (GSI)

```csharp
[DynamoDbTable("stores")]
public partial class Store
{
    [PartitionKey]
    [DynamoDbAttribute("pk")]
    public string StoreId { get; set; }
    
    // GSI for querying by category and location
    [DynamoDbAttribute("gsi1pk")]
    public string Category { get; set; }
    
    [DynamoDbAttribute("gsi1sk", SpatialIndexType = SpatialIndexType.H3, H3Resolution = 7)]
    public GeoLocation Location { get; set; }
}

// Query by category and location using GSI
var center = new GeoLocation(37.7749, -122.4194);
var result = await storeTable.CategoryLocationIndex.SpatialQueryAsync(
    spatialAttributeName: "gsi1sk",
    center: center,
    radiusKilometers: 5,
    queryBuilder: (query, cell, pagination) => query
        .Where<Store>(x => x.Category == "GROCERY" && x.Location == cell)
        .Paginate(pagination),
    pageSize: 50
);

Console.WriteLine($"Found {result.Items.Count} grocery stores within 5km");
```

## Performance Characteristics

### Query Execution Modes

**Non-Paginated Mode (pageSize = null)**
- Queries all cells in parallel using `Task.WhenAll`
- Fastest approach for small to medium result sets
- Returns all results in a single response
- Higher memory usage (all results in memory)

**Paginated Mode (pageSize > 0)**
- Queries cells sequentially in spiral order (closest to farthest)
- Memory-efficient for large result sets
- Consistent page sizes
- Slower total time but better for user experience

### Latency Comparison

| Scenario | GeoHash | S2 | H3 |
|----------|---------|----|----|
| **5km radius, non-paginated** | ~50ms (1 query) | ~50ms (parallel) | ~50ms (parallel) |
| **5km radius, paginated** | ~50ms (1 query) | ~100-200ms (2-4 cells) | ~100-200ms (2-4 cells) |
| **50km radius, non-paginated** | ~50ms (1 query) | ~50ms (parallel) | ~50ms (parallel) |
| **50km radius, paginated** | ~50ms (1 query) | ~500-1000ms (10-20 cells) | ~500-1000ms (10-20 cells) |

**Key Takeaway**: GeoHash is fastest for single queries, but S2/H3 provide better accuracy and coverage uniformity.

### Cell Count Impact

The number of cells affects paginated query performance:

```
Approximate cell count = π × (radius / cellSize)²
```

**Example**: 10km radius with 1.5km cells (S2 Level 16)
```
cellCount ≈ π × (10 / 1.5)² ≈ 140 cells
```

For paginated queries, this means ~140 sequential DynamoDB queries. Choose precision wisely!

## Best Practices

### 1. Choose Appropriate Precision

Match precision to your typical search radius:

```csharp
// ✅ GOOD: 5km radius with S2 Level 14 (~6km cells)
[DynamoDbAttribute("location", SpatialIndexType = SpatialIndexType.S2, S2Level = 14)]

// ❌ BAD: 5km radius with S2 Level 20 (~100m cells)
// This creates ~2,500 cells - very slow for paginated queries!
[DynamoDbAttribute("location", SpatialIndexType = SpatialIndexType.S2, S2Level = 20)]
```

### 2. Use Non-Paginated for Small Areas

```csharp
// For small search areas, get all results at once
var result = await table.SpatialQueryAsync(
    center: center,
    radiusKilometers: 2,  // Small radius
    queryBuilder: ...,
    pageSize: null  // No pagination - fastest
);
```

### 3. Use Paginated for Large Areas or Unknown Result Sizes

```csharp
// For large areas or when result size is unknown
var result = await table.SpatialQueryAsync(
    center: center,
    radiusKilometers: 50,  // Large radius
    queryBuilder: ...,
    pageSize: 100  // Paginated - memory efficient
);
```

### 4. Monitor Cell Counts

```csharp
var result = await table.SpatialQueryAsync(...);
Console.WriteLine($"Queried {result.TotalCellsQueried} cells");
Console.WriteLine($"Scanned {result.TotalItemsScanned} items");

// If TotalCellsQueried is high (>100), consider:
// - Reducing search radius
// - Lowering precision level
// - Using non-paginated mode for parallel execution
```

### 5. Consider Multiple Precision Levels

For applications with varying search radii, store data at multiple precisions:

```csharp
public partial class Store
{
    // Low precision for city-wide searches
    [DynamoDbAttribute("location_city", SpatialIndexType = SpatialIndexType.S2, S2Level = 12)]
    public GeoLocation LocationCity => Location;
    
    // High precision for local searches
    [DynamoDbAttribute("location_local", SpatialIndexType = SpatialIndexType.S2, S2Level = 18)]
    public GeoLocation LocationLocal => Location;
}
```

## Migration Guide

### From GeoHash to S2

```csharp
// Before (GeoHash)
[DynamoDbAttribute("location", GeoHashPrecision = 7)]
public GeoLocation Location { get; set; }

// After (S2)
[DynamoDbAttribute("location", SpatialIndexType = SpatialIndexType.S2, S2Level = 18)]
public GeoLocation Location { get; set; }
```

**Note**: You'll need to re-encode existing data. S2 Level 18 (~400m) is roughly equivalent to GeoHash precision 7 (~76m).

### From GeoHash to H3

```csharp
// Before (GeoHash)
[DynamoDbAttribute("location", GeoHashPrecision = 7)]
public GeoLocation Location { get; set; }

// After (H3)
[DynamoDbAttribute("location", SpatialIndexType = SpatialIndexType.H3, H3Resolution = 10)]
public GeoLocation Location { get; set; }
```

**Note**: H3 Resolution 10 (~66m edge) is roughly equivalent to GeoHash precision 7 (~76m).

## Handling Edge Cases: Dateline and Poles

The library automatically handles two critical edge cases that can cause problems in geospatial queries:

### International Date Line (±180° Longitude)

Queries that cross the International Date Line (from 170°E to -170°E) are automatically handled by splitting the query into two separate bounding boxes.

**Example: Query near the dateline**

```csharp
// Query at the dateline - automatically handled
var center = new GeoLocation(0, 179); // Near dateline at equator
var result = await table.SpatialQueryAsync(
    spatialIndexType: SpatialIndexType.S2,
    precision: 14,
    center: center,
    radiusKilometers: 200,
    queryBuilder: (query, cell, pagination) => query
        .Where<Store>(x => x.PartitionKey == "STORE" && x.Location == cell)
        .Paginate(pagination),
    pageSize: 50
);

// The library automatically:
// 1. Detects that the bounding box crosses the dateline
// 2. Splits it into western (170° to 180°) and eastern (-180° to -170°) boxes
// 3. Computes cell coverings for both boxes
// 4. Deduplicates cells that appear in both coverings
// 5. Returns merged, deduplicated results
```

**What happens automatically:**
- Bounding boxes that cross the dateline are detected
- The query is split into two separate bounding boxes (western and eastern)
- Cell coverings are computed for both boxes
- Cells are deduplicated before querying
- Results are merged and deduplicated by primary key

**Performance impact:**
- Minimal - cell deduplication adds negligible overhead
- Query execution time is the same (cells are deduplicated before querying)

### Polar Regions (±90° Latitude)

Queries near the North or South poles are automatically handled with special logic for longitude convergence.

**Example: Query near the North Pole**

```csharp
// Query near the North Pole - automatically handled
var center = new GeoLocation(89, 0); // 1° from North Pole
var result = await table.SpatialQueryAsync(
    spatialIndexType: SpatialIndexType.S2,
    precision: 10, // Use lower precision near poles
    center: center,
    radiusKilometers: 200,
    queryBuilder: (query, cell, pagination) => query
        .Where<Store>(x => x.PartitionKey == "STORE" && x.Location == cell)
        .Paginate(pagination),
    pageSize: 50
);

// The library automatically:
// 1. Detects that the query is near a pole
// 2. Handles longitude convergence (longitude becomes meaningless at poles)
// 3. If the search area includes the pole, expands longitude to full range (-180° to 180°)
// 4. Returns correct results
```

**What happens automatically:**
- Latitude is clamped to valid range (-90° to 90°)
- If the bounding box includes a pole, longitude is expanded to full range
- Longitude convergence is handled correctly
- Cells are deduplicated to avoid excessive queries

**Performance considerations:**
- Near poles, use lower precision to avoid excessive cell counts
- Recommended: S2 Level 10-12 or H3 Resolution 5-6 for polar queries
- The library logs warnings if cell counts might be excessive

**Example: Query at the South Pole**

```csharp
// Query at the South Pole
var center = new GeoLocation(-89, 0); // 1° from South Pole
var result = await table.SpatialQueryAsync(
    spatialIndexType: SpatialIndexType.H3,
    precision: 5, // Lower resolution for polar regions
    center: center,
    radiusKilometers: 200,
    queryBuilder: (query, cell, pagination) => query
        .Where<Store>(x => x.PartitionKey == "STORE" && x.Location == cell)
        .Paginate(pagination),
    pageSize: 50
);
```

### Combined Edge Case: Dateline + Pole

Queries that involve both the dateline and polar regions are handled correctly.

**Example: Query near North Pole and dateline**

```csharp
// Query near both North Pole and dateline - automatically handled
var center = new GeoLocation(89, 179); // Near North Pole and dateline
var result = await table.SpatialQueryAsync(
    spatialIndexType: SpatialIndexType.S2,
    precision: 10,
    center: center,
    radiusKilometers: 200,
    queryBuilder: (query, cell, pagination) => query
        .Where<Store>(x => x.PartitionKey == "STORE" && x.Location == cell)
        .Paginate(pagination),
    pageSize: 50
);

// The library automatically:
// 1. Checks for pole inclusion first (expands longitude if needed)
// 2. Checks for dateline crossing (splits if needed)
// 3. Computes cell coverings for all resulting bounding boxes
// 4. Deduplicates cells across all coverings
// 5. Returns merged, deduplicated results
```

**Best practices for edge cases:**
- Use lower precision near poles (S2 Level 10-12, H3 Resolution 5-6)
- The library handles all edge cases automatically - no special code needed
- Results are always deduplicated by primary key
- Performance is optimized with cell deduplication

**What you don't need to worry about:**
- ✅ Dateline crossing detection - handled automatically
- ✅ Bounding box splitting - handled automatically
- ✅ Longitude wrapping - handled automatically
- ✅ Pole detection - handled automatically
- ✅ Longitude convergence - handled automatically
- ✅ Cell deduplication - handled automatically
- ✅ Result deduplication - handled automatically

## Troubleshooting

### Query Returns No Results

**Problem**: Spatial query returns empty results even though data exists.

**Solutions**:
1. Check that the spatial attribute name matches your entity definition
2. Verify the partition key condition in your query builder
3. Ensure the search radius is large enough
4. Check that data was encoded with the same spatial index type

### Query is Too Slow

**Problem**: Paginated queries take too long.

**Solutions**:
1. Reduce search radius
2. Lower precision level (larger cells = fewer queries)
3. Use non-paginated mode for small result sets
4. Consider caching frequently accessed areas

### Incomplete Results

**Problem**: Query doesn't return all expected results.

**Solutions**:
1. Check if you hit the maxCells limit (default 100)
2. Increase maxCells for non-paginated queries
3. Reduce precision to use larger cells
4. Verify post-filtering logic isn't too aggressive

## Additional Resources

- [Precision Selection Guide](PRECISION_GUIDE.md) - Detailed guide for choosing precision levels
- [Performance Guide](PERFORMANCE_GUIDE.md) - Query optimization and performance tuning
- [Coordinate Storage Guide](COORDINATE_STORAGE_GUIDE.md) - Storing full-resolution coordinates
- [API Reference](API_REFERENCE.md) - Complete API documentation
- [Examples](EXAMPLES.md) - More code examples and patterns

## Summary

- **GeoHash**: Best for simple queries in mid-latitudes
- **S2**: Best for global coverage and polar regions
- **H3**: Best for uniform coverage and grid analysis

Choose based on your specific requirements, and don't hesitate to experiment with different index types to find the best fit for your use case!
