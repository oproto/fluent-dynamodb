# Query Performance Guide

This guide explains the performance characteristics of spatial queries and provides best practices for optimization.

## Table of Contents

- [Query Execution Modes](#query-execution-modes)
- [Spiral Ordering](#spiral-ordering)
- [Latency Estimates](#latency-estimates)
- [Performance Comparison](#performance-comparison)
- [Optimization Best Practices](#optimization-best-practices)
- [Cost Considerations](#cost-considerations)
- [Monitoring and Debugging](#monitoring-and-debugging)

## Query Execution Modes

Spatial queries support two execution modes with different performance characteristics:

### Non-Paginated Mode (pageSize = null)

**How it works:**
1. Compute all cells that cover the search area
2. Execute ALL cell queries in parallel using `Task.WhenAll`
3. Merge results and deduplicate by primary key
4. Post-filter by exact distance
5. Sort by distance from center
6. Return all results in a single response

**Performance characteristics:**
- ⚡ **Fastest** - All queries execute simultaneously
- 📊 **Latency** = Single cell query time (~50ms)
- 💾 **Memory** = All results loaded into memory
- 🔄 **Parallelism** = N parallel DynamoDB queries (N = cell count)

**When to use:**
- Small to medium result sets (< 1000 items)
- You need all results at once
- Fast response time is critical
- Memory usage is not a concern
- You can handle all results in memory

**Example:**
```csharp
// Find ALL stores within 5km - fastest approach
var result = await storeTable.SpatialQueryAsync(
    spatialAttributeName: "location",
    center: center,
    radiusKilometers: 5,
    queryBuilder: (query, cell, pagination) => query
        .Where<Store>(x => x.PartitionKey == "STORE" && x.Location == cell)
        .Paginate(pagination),
    pageSize: null  // Non-paginated mode
);

// Typical performance:
// - 35 cells computed
// - 35 parallel DynamoDB queries
// - Total latency: ~50ms (single query time)
// - Memory: All results in memory
```

### Paginated Mode (pageSize > 0)

**How it works:**
1. Compute all cells that cover the search area
2. Sort cells by distance from center (spiral order)
3. Query cells SEQUENTIALLY in spiral order
4. Collect results until pageSize is reached
5. Generate continuation token if more results exist
6. Return one page of results

**Performance characteristics:**
- 🐌 **Slower** - Queries execute sequentially
- 📊 **Latency** = N × cell query time (~50ms × N)
- 💾 **Memory** = One page in memory
- 🔄 **Parallelism** = 1 query at a time
- 📄 **Pagination** = Consistent page sizes

**When to use:**
- Large result sets (> 1000 items)
- Memory-efficient processing required
- Infinite scroll UI patterns
- API responses with pagination
- You only need first N results

**Example:**
```csharp
// Find stores within 10km, paginated (50 per page)
var result = await storeTable.SpatialQueryAsync(
    spatialAttributeName: "location",
    center: center,
    radiusKilometers: 10,
    queryBuilder: (query, cell, pagination) => query
        .Where<Store>(x => x.PartitionKey == "STORE" && x.Location == cell)
        .Paginate(pagination),
    pageSize: 50  // Paginated mode
);

// Typical performance:
// - 140 cells computed
// - Queries cells sequentially until 50 items collected
// - Usually queries 2-4 cells for first page
// - First page latency: ~100-200ms (2-4 queries)
// - Memory: Only 50 items in memory
```

## Spiral Ordering

Paginated queries use **spiral ordering** - cells are queried from closest to farthest from the search center.

### Why Spiral Ordering Matters

**Without spiral ordering (random order):**
```
Query cells: [Cell 45, Cell 2, Cell 89, Cell 12, ...]
Results: Random mix of near and far locations
User experience: Poor - first page shows random locations
```

**With spiral ordering (closest first):**
```
Query cells: [Cell 0 (center), Cell 1, Cell 2, Cell 3, ...]
Results: Closest locations appear first
User experience: Excellent - first page shows nearest locations
```

### Spiral Order Visualization

```
Search center: ⭐
Cell numbers indicate query order:

        [7] [8] [9]
    [6] [1] [2] [3]
    [5] [⭐] [0] [4]
    [14][13][12][11]
        [15][16][17]

Cell 0: Contains search center
Cells 1-8: First ring (immediate neighbors)
Cells 9-24: Second ring
Cells 25-48: Third ring
... and so on
```

### Benefits of Spiral Ordering

1. **Better User Experience**
   - Most relevant results (closest) appear in first pages
   - Users typically only view first 1-2 pages
   - "Stores near you" shows actually near stores first

2. **Early Termination**
   - If user stops at page 1, we only queried nearby cells
   - No wasted queries on distant cells
   - Saves DynamoDB costs

3. **Predictable Performance**
   - Each page queries similar number of cells
   - Consistent latency per page
   - No surprising slow pages

4. **Roughly Sorted Results**
   - Results are already roughly sorted by distance
   - Less post-processing needed
   - Better for streaming results

### Example: Paginated Query with Spiral Ordering

```csharp
var center = new GeoLocation(37.7749, -122.4194);
var allStores = new List<Store>();
SpatialContinuationToken? token = null;
int pageNumber = 1;

do
{
    var result = await storeTable.SpatialQueryAsync(
        spatialAttributeName: "location",
        center: center,
        radiusKilometers: 10,
        queryBuilder: (query, cell, pagination) => query
            .Where<Store>(x => x.PartitionKey == "STORE" && x.Location == cell)
            .Paginate(pagination),
        pageSize: 50,
        continuationToken: token
    );
    
    allStores.AddRange(result.Items);
    token = result.ContinuationToken;
    
    // Show distance of first and last item in page
    if (result.Items.Any())
    {
        var firstDistance = result.Items.First().Location.DistanceToKilometers(center);
        var lastDistance = result.Items.Last().Location.DistanceToKilometers(center);
        
        Console.WriteLine($"Page {pageNumber}:");
        Console.WriteLine($"  Items: {result.Items.Count}");
        Console.WriteLine($"  Cells queried: {result.TotalCellsQueried}");
        Console.WriteLine($"  Distance range: {firstDistance:F2}km - {lastDistance:F2}km");
    }
    
    pageNumber++;
} while (token != null);

// Output example:
// Page 1:
//   Items: 50
//   Cells queried: 3
//   Distance range: 0.12km - 2.45km  ← Closest stores
// Page 2:
//   Items: 50
//   Cells queried: 4
//   Distance range: 2.38km - 4.89km  ← Medium distance
// Page 3:
//   Items: 42
//   Cells queried: 5
//   Distance range: 4.76km - 7.23km  ← Farther stores
```

## Latency Estimates

### Single Cell Query Baseline

A single DynamoDB query typically takes:
- **~50ms** - Average latency
- **~20ms** - Best case (small result set, good network)
- **~100ms** - Worst case (large result set, network issues)

All estimates below assume 50ms per query.

### Non-Paginated Mode Latency

**Formula:**
```
Total Latency = Single Query Time
              ≈ 50ms (regardless of cell count)
```

**Examples:**

| Scenario | Cell Count | Latency | Notes |
|----------|------------|---------|-------|
| 1km radius, S2 L18 | 2 cells | ~50ms | All queries parallel |
| 5km radius, S2 L16 | 35 cells | ~50ms | All queries parallel |
| 10km radius, S2 L14 | 11 cells | ~50ms | All queries parallel |
| 50km radius, S2 L12 | 16 cells | ~50ms | All queries parallel |

**Key insight**: Latency is constant regardless of cell count because all queries execute in parallel!

### Paginated Mode Latency

**Formula:**
```
First Page Latency = Cells Queried × Single Query Time
                   ≈ N × 50ms

Total Latency = Total Cells × Single Query Time
              ≈ Total Cells × 50ms
```

**Examples:**

| Scenario | Total Cells | Cells for Page 1 | First Page Latency | Total Latency |
|----------|-------------|------------------|-------------------|---------------|
| 5km radius, S2 L16, pageSize=50 | 35 | 2-4 | ~100-200ms | ~1,750ms |
| 10km radius, S2 L14, pageSize=50 | 11 | 2-3 | ~100-150ms | ~550ms |
| 10km radius, S2 L16, pageSize=50 | 140 | 3-5 | ~150-250ms | ~7,000ms |
| 50km radius, S2 L12, pageSize=100 | 16 | 3-4 | ~150-200ms | ~800ms |

**Key insights:**
- First page is fast (only queries nearby cells)
- Total time increases linearly with cell count
- Users typically only see first 1-2 pages

### GeoHash Performance

GeoHash uses a **single BETWEEN query** regardless of search radius:

| Scenario | Query Count | Latency | Notes |
|----------|-------------|---------|-------|
| Any radius, any precision | 1 query | ~50ms | Always single query |

**Trade-off**: GeoHash is fastest but less accurate (rectangular approximation of circular area).

## Performance Comparison

### By Index Type

| Index Type | Query Count | Latency (Non-Pag) | Latency (Pag, Page 1) | Accuracy |
|------------|-------------|-------------------|----------------------|----------|
| **GeoHash** | 1 | ~50ms | ~50ms | Good |
| **S2** | 10-50 | ~50ms | ~100-200ms | Excellent |
| **H3** | 10-50 | ~50ms | ~100-200ms | Excellent |

### By Search Radius (S2 Level 16)

| Radius | Cell Count | Non-Pag Latency | Pag First Page | Pag Total Time |
|--------|------------|-----------------|----------------|----------------|
| 1km | 2 | ~50ms | ~50ms | ~100ms |
| 5km | 35 | ~50ms | ~100-200ms | ~1,750ms |
| 10km | 140 | ~50ms | ~150-250ms | ~7,000ms |
| 25km | 870 | ~50ms* | ~200-400ms | ~43,500ms |
| 50km | 4,400 | ~50ms* | ~300-500ms | ~220,000ms |

*Assumes maxCells limit not hit. If hit, results are incomplete.

### By Precision Level (10km radius)

**S2:**
| Level | Cell Size | Cell Count | Non-Pag | Pag First Page | Pag Total |
|-------|-----------|------------|---------|----------------|-----------|
| 12 | ~25km | 2 | ~50ms | ~50ms | ~100ms |
| 14 | ~6km | 11 | ~50ms | ~100ms | ~550ms |
| 16 | ~1.5km | 140 | ~50ms | ~150-250ms | ~7,000ms |
| 18 | ~400m | 2,500 | ~50ms* | ~200-400ms | ~125,000ms |

**H3:**
| Resolution | Cell Edge | Cell Count | Non-Pag | Pag First Page | Pag Total |
|------------|-----------|------------|---------|----------------|-----------|
| 5 | ~8.5km | 6 | ~50ms | ~50ms | ~300ms |
| 7 | ~1.2km | 280 | ~50ms | ~150-250ms | ~14,000ms |
| 9 | ~174m | 13,000 | ~50ms* | ~300-500ms | ~650,000ms |

*Assumes maxCells limit not hit.

## Optimization Best Practices

### 1. Choose the Right Mode

**Use Non-Paginated When:**
- Result set is small (< 1000 items)
- You need all results at once
- Fast response time is critical
- Memory is not a constraint

```csharp
// ✅ Good: Small area, need all results
var result = await table.SpatialQueryAsync(
    center: center,
    radiusKilometers: 2,
    queryBuilder: ...,
    pageSize: null  // Non-paginated
);
```

**Use Paginated When:**
- Result set is large (> 1000 items)
- Memory efficiency is important
- Implementing infinite scroll
- Building paginated APIs

```csharp
// ✅ Good: Large area, paginated API
var result = await table.SpatialQueryAsync(
    center: center,
    radiusKilometers: 50,
    queryBuilder: ...,
    pageSize: 100  // Paginated
);
```

### 2. Match Precision to Radius

**Rule of thumb**: Cell size should be 20-50% of search radius

```csharp
// ✅ Good: 5km radius with ~1.5km cells
[DynamoDbAttribute("location", SpatialIndexType = SpatialIndexType.S2, S2Level = 16)]

// ❌ Bad: 5km radius with ~400m cells (too many cells!)
[DynamoDbAttribute("location", SpatialIndexType = SpatialIndexType.S2, S2Level = 18)]
```

### 3. Limit Search Radius

Smaller radius = fewer cells = faster queries

```csharp
// ✅ Good: Reasonable radius
var result = await table.SpatialQueryAsync(
    center: center,
    radiusKilometers: 10,  // Reasonable
    ...
);

// ❌ Bad: Very large radius
var result = await table.SpatialQueryAsync(
    center: center,
    radiusKilometers: 100,  // Too large!
    ...
);
```

### 4. Use Multiple Precision Levels

Store data at multiple precisions for different query types:

```csharp
public partial class Store
{
    // Low precision for large area queries
    [DynamoDbAttribute("location_region", SpatialIndexType = SpatialIndexType.S2, S2Level = 12)]
    public GeoLocation LocationRegion => Location;
    
    // High precision for local queries
    [DynamoDbAttribute("location_local", SpatialIndexType = SpatialIndexType.S2, S2Level = 16)]
    public GeoLocation LocationLocal => Location;
    
    [DynamoDbAttribute("location")]
    public GeoLocation Location { get; set; }
}

// Choose attribute based on radius
var attributeName = radiusKm <= 10 ? "location_local" : "location_region";
```

### 5. Implement Caching

Cache frequently accessed areas:

```csharp
private readonly IMemoryCache _cache;

public async Task<List<Store>> GetNearbyStores(GeoLocation center, double radiusKm)
{
    var cacheKey = $"stores:{center.Latitude:F3}:{center.Longitude:F3}:{radiusKm}";
    
    if (_cache.TryGetValue(cacheKey, out List<Store> cached))
    {
        return cached;
    }
    
    var result = await table.SpatialQueryAsync(
        center: center,
        radiusKilometers: radiusKm,
        queryBuilder: ...,
        pageSize: null
    );
    
    _cache.Set(cacheKey, result.Items, TimeSpan.FromMinutes(5));
    return result.Items;
}
```

### 6. Use Bounding Box for Large Areas

For very large areas, bounding box queries may be more efficient:

```csharp
public async Task<List<Store>> SearchLargeArea(GeoLocation center, double radiusKm)
{
    if (radiusKm > 50)
    {
        // Use bounding box for large areas
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
        // Use proximity query for smaller areas
        var result = await table.SpatialQueryAsync(
            center: center,
            radiusKilometers: radiusKm,
            queryBuilder: ...,
            pageSize: null
        );
        
        return result.Items;
    }
}
```

### 7. Monitor and Adjust

Always monitor query performance in production:

```csharp
var stopwatch = Stopwatch.StartNew();

var result = await table.SpatialQueryAsync(...);

stopwatch.Stop();

_logger.LogInformation(
    "Spatial query completed: " +
    "Radius={Radius}km, " +
    "CellsQueried={Cells}, " +
    "ItemsReturned={Items}, " +
    "Latency={Latency}ms",
    radiusKm,
    result.TotalCellsQueried,
    result.Items.Count,
    stopwatch.ElapsedMilliseconds
);

// Alert if performance is poor
if (result.TotalCellsQueried > 100)
{
    _logger.LogWarning(
        "High cell count detected! " +
        "Consider reducing precision or radius."
    );
}
```

## Cost Considerations

### DynamoDB Read Capacity Units (RCUs)

Each cell query consumes RCUs based on:
- Item size
- Number of items returned
- Consistency level (eventual vs strong)

**Formula:**
```
Total RCUs = Cell Count × RCUs per Query
```

**Example:**
```
10km radius with S2 Level 16:
- 140 cells
- ~5 RCUs per query (average)
- Total: 140 × 5 = 700 RCUs per search
```

### Cost Optimization Strategies

1. **Use Lower Precision**
   - Fewer cells = fewer queries = lower cost
   - Trade-off: Less accuracy

2. **Use Non-Paginated Mode**
   - Parallel queries complete faster
   - Reduces connection time
   - Same total RCUs but faster completion

3. **Implement Caching**
   - Cache popular search areas
   - Reduces repeated queries
   - Significant cost savings for hot paths

4. **Use Eventual Consistency**
   - Half the RCU cost vs strong consistency
   - Acceptable for most spatial queries

```csharp
var result = await table.SpatialQueryAsync(
    center: center,
    radiusKilometers: 10,
    queryBuilder: (query, cell, pagination) => query
        .Where<Store>(x => x.PartitionKey == "STORE" && x.Location == cell)
        .WithConsistentRead(false)  // Eventual consistency
        .Paginate(pagination),
    pageSize: null
);
```

5. **Limit Search Radius**
   - Smaller radius = fewer cells = lower cost
   - Encourage users to search smaller areas

### Cost Comparison

**Scenario: 1 million searches per day**

| Configuration | Cells per Search | RCUs per Search | Daily RCUs | Monthly Cost* |
|---------------|------------------|-----------------|------------|---------------|
| 5km, S2 L16 | 35 | 175 | 175M | $21.88 |
| 10km, S2 L16 | 140 | 700 | 700M | $87.50 |
| 10km, S2 L14 | 11 | 55 | 55M | $6.88 |
| 50km, S2 L12 | 16 | 80 | 80M | $10.00 |

*Assumes $0.25 per million RCUs (on-demand pricing)

**Key insight**: Precision level has huge impact on costs!

## Monitoring and Debugging

### Key Metrics to Track

1. **TotalCellsQueried**
   - How many cells were queried
   - High values indicate potential issues

2. **TotalItemsScanned**
   - How many items DynamoDB scanned
   - Helps identify inefficient queries

3. **Items.Count**
   - How many items were returned
   - Compare to TotalItemsScanned for efficiency

4. **Query Latency**
   - End-to-end query time
   - Track p50, p95, p99 percentiles

### Logging Example

```csharp
public async Task<SpatialQueryResponse<Store>> SearchStoresWithLogging(
    GeoLocation center,
    double radiusKm,
    int? pageSize = null)
{
    var stopwatch = Stopwatch.StartNew();
    
    var result = await _storeTable.SpatialQueryAsync(
        spatialAttributeName: "location",
        center: center,
        radiusKilometers: radiusKm,
        queryBuilder: (query, cell, pagination) => query
            .Where<Store>(x => x.PartitionKey == "STORE" && x.Location == cell)
            .Paginate(pagination),
        pageSize: pageSize
    );
    
    stopwatch.Stop();
    
    var efficiency = result.Items.Count / (double)result.TotalItemsScanned * 100;
    
    _logger.LogInformation(
        "Spatial query completed: " +
        "Center=({Lat},{Lon}), " +
        "Radius={Radius}km, " +
        "PageSize={PageSize}, " +
        "CellsQueried={Cells}, " +
        "ItemsScanned={Scanned}, " +
        "ItemsReturned={Returned}, " +
        "Efficiency={Efficiency:F1}%, " +
        "Latency={Latency}ms, " +
        "HasMore={HasMore}",
        center.Latitude,
        center.Longitude,
        radiusKm,
        pageSize?.ToString() ?? "null",
        result.TotalCellsQueried,
        result.TotalItemsScanned,
        result.Items.Count,
        efficiency,
        stopwatch.ElapsedMilliseconds,
        result.ContinuationToken != null
    );
    
    // Alert on potential issues
    if (result.TotalCellsQueried >= 100)
    {
        _logger.LogWarning(
            "High cell count detected! " +
            "Radius={Radius}km, Cells={Cells}. " +
            "Consider reducing precision or radius.",
            radiusKm,
            result.TotalCellsQueried
        );
    }
    
    if (efficiency < 50)
    {
        _logger.LogWarning(
            "Low query efficiency! " +
            "Scanned={Scanned}, Returned={Returned}, Efficiency={Efficiency:F1}%. " +
            "Consider adjusting precision.",
            result.TotalItemsScanned,
            result.Items.Count,
            efficiency
        );
    }
    
    return result;
}
```

### Performance Dashboard Metrics

Track these metrics in your monitoring dashboard:

```
Spatial Query Performance:
- Average latency: 125ms (p50), 250ms (p95), 500ms (p99)
- Average cells queried: 35
- Average items scanned: 150
- Average items returned: 45
- Query efficiency: 30%
- Queries hitting maxCells limit: 2.5%
- Total RCUs consumed: 1.2M per hour
```

### Troubleshooting Common Issues

**Issue: Queries are slow**
- Check TotalCellsQueried - if > 100, reduce precision or radius
- Check if using paginated mode - consider non-paginated for small areas
- Check network latency to DynamoDB

**Issue: Incomplete results**
- Check if hitting maxCells limit
- Increase maxCells or reduce precision
- Consider using lower precision level

**Issue: High costs**
- Check TotalCellsQueried - reduce precision if high
- Implement caching for popular areas
- Use eventual consistency
- Limit search radius

**Issue: Low efficiency (many scanned, few returned)**
- Precision too low (cells too large)
- Increase precision level
- Add additional filter conditions

## Summary

### Key Takeaways

1. **Non-paginated mode is fastest** (~50ms regardless of cell count)
2. **Paginated mode is memory-efficient** but slower (N × 50ms)
3. **Spiral ordering ensures best results appear first** in paginated mode
4. **Precision level has huge impact** on performance and cost
5. **Monitor TotalCellsQueried** to identify issues
6. **Cache frequently accessed areas** for cost savings
7. **Match precision to search radius** for optimal performance

### Quick Reference

| Scenario | Mode | Precision | Expected Performance |
|----------|------|-----------|---------------------|
| Small area, need all results | Non-paginated | High | ~50ms, low cost |
| Large area, need all results | Non-paginated | Low | ~50ms, medium cost |
| Small area, paginated API | Paginated | High | ~100-200ms first page |
| Large area, paginated API | Paginated | Low | ~100-200ms first page |
| Very large area | Bounding box | Low | ~50ms, post-filter |

### When in Doubt

Start with these defaults and adjust based on monitoring:
- **Radius < 10km**: S2 Level 16, non-paginated
- **Radius 10-50km**: S2 Level 14, paginated
- **Radius > 50km**: S2 Level 12, paginated or bounding box

**Always monitor and optimize based on your actual usage patterns!**
