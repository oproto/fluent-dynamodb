# Geospatial Limitations and Edge Cases

This document describes the known limitations, edge cases, and workarounds when using geospatial queries with DynamoDB and GeoHash encoding.

## Table of Contents

- [DynamoDB Query Pattern Limitations](#dynamodb-query-pattern-limitations)
- [GeoHash Algorithm Limitations](#geohash-algorithm-limitations)
- [Geographic Edge Cases](#geographic-edge-cases)
- [Performance Considerations](#performance-considerations)
- [Workarounds and Best Practices](#workarounds-and-best-practices)

## DynamoDB Query Pattern Limitations

### 1. Rectangular Queries Only (Not Circular)

**Limitation**: DynamoDB's BETWEEN operator creates rectangular bounding boxes, not circular areas.

**Impact**: When you query for locations within a certain distance (e.g., 5km radius), the query returns all locations within a rectangular bounding box, which includes locations outside the circular radius.

**Example**:
```csharp
var center = new GeoLocation(37.7749, -122.4194);

// This query returns a RECTANGULAR area, not a circle
var results = await storeTable.Query
    .Where<Store>(x => x.Location.WithinDistanceKilometers(center, 5))
    .ExecuteAsync();

// Some results may be > 5km away (in the corners of the rectangle)
```

**Visual Representation**:
```
┌─────────────────────┐
│         ╱───╲       │  Rectangle: DynamoDB query results
│       ╱       ╲     │  Circle: Actual 5km radius
│      │    ●    │    │  ● = Center point
│       ╲       ╱     │  Corners contain locations > 5km away
│         ╲───╱       │
└─────────────────────┘
```

**Workaround**: Always post-filter results for exact circular distance:

```csharp
var center = new GeoLocation(37.7749, -122.4194);
var radiusKm = 5.0;

// Query with bounding box (rectangular)
var candidates = await storeTable.Query
    .Where<Store>(x => x.Location.WithinDistanceKilometers(center, radiusKm))
    .ExecuteAsync();

// Post-filter for exact circular distance
var exactResults = candidates
    .Where(s => s.Location.DistanceToKilometers(center) <= radiusKm)
    .OrderBy(s => s.Location.DistanceToKilometers(center))
    .ToList();
```

**Over-Retrieval**: Rectangular queries typically retrieve 20-30% more items than needed. The exact percentage depends on the query area and data distribution.

### 2. No Native Distance-Based Sorting

**Limitation**: DynamoDB cannot sort query results by distance from a point.

**Impact**: You cannot retrieve "the 10 closest stores" directly from DynamoDB. You must retrieve all candidates and sort in memory.

**Example of What Doesn't Work**:
```csharp
// This is NOT possible with DynamoDB
var closestStores = await storeTable.Query
    .Where<Store>(x => x.Location.WithinDistanceKilometers(center, 10))
    .OrderBy(x => x.Location.DistanceToKilometers(center))  // ❌ Not supported
    .Take(10)
    .ExecuteAsync();
```

**Workaround**: Retrieve all candidates and sort in memory:

```csharp
var center = new GeoLocation(37.7749, -122.4194);

// Retrieve all candidates within search radius
var candidates = await storeTable.Query
    .Where<Store>(x => x.Location.WithinDistanceKilometers(center, 10))
    .ExecuteAsync();

// Sort in memory and take top 10
var closestStores = candidates
    .OrderBy(s => s.Location.DistanceToKilometers(center))
    .Take(10)
    .ToList();
```

**Performance Impact**: For large result sets, this requires retrieving and processing all candidates in memory. Consider using a smaller search radius or implementing pagination strategies.

### 3. Pagination with Distance Sorting is Complex

**Limitation**: DynamoDB's pagination (LastEvaluatedKey) doesn't work well with distance-based sorting.

**Impact**: You cannot efficiently paginate through results sorted by distance without retrieving all results first.

**Problem**:
```csharp
// Page 1: Get first 10 results
var page1 = await storeTable.Query
    .Where<Store>(x => x.Location.WithinDistanceKilometers(center, 10))
    .Take(10)
    .ExecuteAsync();

// ❌ Page 2 results are NOT the next 10 closest stores
// They're just the next 10 items from DynamoDB's perspective
var page2 = await storeTable.Query
    .Where<Store>(x => x.Location.WithinDistanceKilometers(center, 10))
    .Take(10)
    .StartFrom(page1.LastEvaluatedKey)
    .ExecuteAsync();
```

**Workaround 1**: Retrieve all results and paginate in memory:

```csharp
// Fetch all results
var allResults = new List<Store>();
string lastKey = null;

do
{
    var page = await storeTable.Query
        .Where<Store>(x => x.Location.WithinDistanceKilometers(center, 10))
        .Take(100)
        .StartFrom(lastKey)
        .ExecuteAsync();
    
    allResults.AddRange(page.Items);
    lastKey = page.LastEvaluatedKey;
} while (lastKey != null);

// Sort and paginate in memory
var sortedResults = allResults
    .OrderBy(s => s.Location.DistanceToKilometers(center))
    .ToList();

var page1 = sortedResults.Skip(0).Take(10).ToList();
var page2 = sortedResults.Skip(10).Take(10).ToList();
```

**Workaround 2**: Use client-side pagination with offset/limit:

```csharp
public async Task<List<Store>> GetStoresPage(
    GeoLocation center,
    double radiusKm,
    int pageNumber,
    int pageSize)
{
    // Retrieve all results (consider caching)
    var allResults = await GetAllStoresInRadius(center, radiusKm);
    
    // Sort by distance
    var sorted = allResults
        .OrderBy(s => s.Location.DistanceToKilometers(center))
        .ToList();
    
    // Return requested page
    return sorted
        .Skip((pageNumber - 1) * pageSize)
        .Take(pageSize)
        .ToList();
}
```

### 4. Single Range Query Limitation

**Limitation**: DynamoDB supports only one BETWEEN condition per query.

**Impact**: You cannot efficiently query multiple non-contiguous geographic areas in a single query.

**Example of What Doesn't Work**:
```csharp
// ❌ Cannot query multiple separate areas in one query
var results = await storeTable.Query
    .Where("location BETWEEN :min1 AND :max1 OR location BETWEEN :min2 AND :max2")
    .ExecuteAsync();
```

**Workaround**: Execute multiple queries and combine results:

```csharp
var location1 = new GeoLocation(37.7749, -122.4194);
var location2 = new GeoLocation(40.7128, -74.0060);

// Query each area separately
var results1 = await storeTable.Query
    .Where<Store>(x => x.Location.WithinDistanceKilometers(location1, 5))
    .ExecuteAsync();

var results2 = await storeTable.Query
    .Where<Store>(x => x.Location.WithinDistanceKilometers(location2, 5))
    .ExecuteAsync();

// Combine and deduplicate
var allResults = results1.Concat(results2).Distinct().ToList();
```

## GeoHash Algorithm Limitations

### 1. Cell Boundary Issues

**Limitation**: Locations near GeoHash cell boundaries may be missed if you only query a single cell.

**Impact**: A location just outside a cell boundary won't be returned, even if it's very close to the query center.

**Example**:
```
Cell A    │    Cell B
          │
    ●     │  ○
  (query) │  (store)
          │
```

The store in Cell B won't be returned if you only query Cell A, even though it might be closer than stores within Cell A.

**Workaround**: Query neighbor cells for critical applications:

```csharp
var location = new GeoLocation(37.7749, -122.4194);
var cell = location.ToGeoHashCell(7);

// Get all cells to query (center + 8 neighbors)
var allCells = new[] { cell }.Concat(cell.GetNeighbors()).ToArray();

var allResults = new List<Store>();

foreach (var c in allCells)
{
    var results = await storeTable.Query
        .Where("location = :hash")
        .WithValue(":hash", c.Hash)
        .ExecuteAsync();
    
    allResults.AddRange(results);
}

// Deduplicate and filter by actual distance
var uniqueResults = allResults
    .Distinct()
    .Where(s => s.Location.DistanceToKilometers(location) <= 5)
    .ToList();
```

### 2. Precision Trade-offs

**Limitation**: GeoHash precision is a trade-off between accuracy and query efficiency.

**Impact**:
- **Lower precision**: Faster queries but less accurate, may return many irrelevant results
- **Higher precision**: More accurate but slower queries, may require querying multiple cells

**Example**:

| Precision | Cell Size | Query Speed | Accuracy | Over-Retrieval |
|-----------|-----------|-------------|----------|----------------|
| 5 | ~2.4 km | Very Fast | Low | High (50-100%) |
| 6 | ~0.61 km | Fast | Medium | Medium (20-40%) |
| 7 | ~0.15 km | Medium | High | Low (10-20%) |
| 8 | ~0.04 km | Slow | Very High | Very Low (<10%) |

**Recommendation**: Use precision 6-7 for most applications. See [PRECISION_GUIDE.md](PRECISION_GUIDE.md) for detailed guidance.

### 3. Non-Uniform Cell Sizes

**Limitation**: GeoHash cells are not perfectly square and vary in size based on latitude.

**Impact**: 
- Cells near the equator are more square
- Cells near the poles are more elongated (longitude dimension shrinks)
- Query accuracy varies by latitude

**Example**:
```
Equator (0°):     Pole (90°):
┌────────┐        ┌┐
│        │        ││  (same precision,
│        │        ││   different shape)
└────────┘        └┘
```

**Workaround**: Use slightly larger search radii at higher latitudes, or use lower precision for polar regions.

## Geographic Edge Cases

### 1. International Date Line (±180° Longitude)

**Limitation**: Queries crossing the international date line require special handling.

**Impact**: A bounding box that crosses the date line (e.g., from 179° to -179°) won't work correctly with standard BETWEEN queries.

**Example**:
```csharp
// This bounding box crosses the date line
var southwest = new GeoLocation(20, 179);   // 179° E
var northeast = new GeoLocation(30, -179);  // 179° W

// ❌ Standard query won't work correctly
var results = await storeTable.Query
    .Where<Store>(x => x.Location.WithinBoundingBox(southwest, northeast))
    .ExecuteAsync();
```

**Workaround**: Split the query into two parts:

```csharp
// Query 1: From 179° to 180°
var results1 = await storeTable.Query
    .Where<Store>(x => x.Location.WithinBoundingBox(
        new GeoLocation(20, 179),
        new GeoLocation(30, 180)))
    .ExecuteAsync();

// Query 2: From -180° to -179°
var results2 = await storeTable.Query
    .Where<Store>(x => x.Location.WithinBoundingBox(
        new GeoLocation(20, -180),
        new GeoLocation(30, -179)))
    .ExecuteAsync();

// Combine results
var allResults = results1.Concat(results2).ToList();
```

### 2. Polar Regions (±90° Latitude)

**Limitation**: GeoHash precision decreases significantly near the poles due to longitude convergence.

**Impact**: 
- All longitude lines converge at the poles
- GeoHash cells become very elongated
- Distance calculations may be less accurate

**Example**:
```
At 89° latitude, a GeoHash cell might be:
- 1 km in latitude dimension
- 100 km in longitude dimension
```

**Workaround**: 
- Use lower precision (4-5) for polar queries
- Consider alternative encoding schemes (S2, H3) for polar applications
- Increase search radius to compensate for cell elongation

```csharp
// For polar regions, use lower precision
[DynamoDbAttribute("location", GeoHashPrecision = 4)]
public GeoLocation Location { get; set; }
```

### 3. Prime Meridian (0° Longitude)

**Limitation**: Similar to the date line, but less problematic.

**Impact**: Queries crossing the prime meridian work correctly, but be aware of positive/negative longitude values.

**Example**:
```csharp
// This works correctly (crosses prime meridian)
var southwest = new GeoLocation(50, -5);   // 5° W
var northeast = new GeoLocation(52, 5);    // 5° E

var results = await storeTable.Query
    .Where<Store>(x => x.Location.WithinBoundingBox(southwest, northeast))
    .ExecuteAsync();
```

**Note**: No special handling needed for prime meridian crossings.

### 4. Equator (0° Latitude)

**Limitation**: None - the equator is handled correctly.

**Impact**: GeoHash cells are most uniform near the equator.

**Recommendation**: Equatorial regions are ideal for GeoHash-based queries.

## Performance Considerations

### 1. Large Search Radii

**Limitation**: Very large search radii (>50km) can result in scanning many items.

**Impact**: 
- Increased query time
- Higher DynamoDB read costs
- More items to post-filter

**Example**:
```csharp
// Large radius query - may scan thousands of items
var results = await storeTable.Query
    .Where<Store>(x => x.Location.WithinDistanceKilometers(center, 100))
    .ExecuteAsync();
```

**Workaround**: 
- Use lower precision for large-area queries
- Consider hierarchical queries (start broad, narrow down)
- Implement result limits

```csharp
// Use lower precision for large areas
var bbox = GeoBoundingBox.FromCenterAndDistanceKilometers(center, 100);
var (minHash, maxHash) = bbox.GetGeoHashRange(5);  // Lower precision

var results = await storeTable.Query
    .Where("location BETWEEN :min AND :max")
    .WithValue(":min", minHash)
    .WithValue(":max", maxHash)
    .Take(100)  // Limit results
    .ExecuteAsync();
```

### 2. Dense Data Regions

**Limitation**: Areas with many locations (e.g., city centers) can result in large result sets.

**Impact**: 
- More items to retrieve and process
- Higher memory usage
- Slower post-filtering

**Workaround**: 
- Use higher precision to narrow results
- Implement pagination
- Add additional filters (e.g., rating, category)

```csharp
// Add filters to reduce result set
var results = await storeTable.Query
    .Where<Store>(x => 
        x.Location.WithinDistanceKilometers(center, 5) &&
        x.Rating >= 4.0 &&
        x.IsOpen == true)
    .ExecuteAsync();
```

### 3. Sparse Data Regions

**Limitation**: Areas with few locations may require very large search radii.

**Impact**: 
- May need to query very large areas
- Increased query costs
- May not find any results

**Workaround**: 
- Start with a reasonable radius and expand if needed
- Use hierarchical search (start broad, narrow down)

```csharp
public async Task<List<Store>> FindNearestStores(
    GeoLocation location,
    int maxResults = 10)
{
    var radii = new[] { 5, 10, 25, 50, 100 };  // km
    
    foreach (var radius in radii)
    {
        var results = await storeTable.Query
            .Where<Store>(x => x.Location.WithinDistanceKilometers(location, radius))
            .ExecuteAsync();
        
        if (results.Count >= maxResults)
        {
            return results
                .OrderBy(s => s.Location.DistanceToKilometers(location))
                .Take(maxResults)
                .ToList();
        }
    }
    
    // Return whatever we found
    return new List<Store>();
}
```

## Workarounds and Best Practices

### 1. Always Post-Filter for Exact Distances

```csharp
// ✅ ALWAYS do this
var candidates = await storeTable.Query
    .Where<Store>(x => x.Location.WithinDistanceKilometers(center, 5))
    .ExecuteAsync();

var exactResults = candidates
    .Where(s => s.Location.DistanceToKilometers(center) <= 5)
    .ToList();
```

### 2. Use Appropriate Precision

```csharp
// ✅ Match precision to use case
[DynamoDbAttribute("location", GeoHashPrecision = 6)]  // City-wide
[DynamoDbAttribute("location", GeoHashPrecision = 8)]  // Building-level
```

### 3. Implement Result Limits

```csharp
// ✅ Limit results to prevent over-retrieval
var results = await storeTable.Query
    .Where<Store>(x => x.Location.WithinDistanceKilometers(center, 10))
    .Take(100)  // Limit to 100 items
    .ExecuteAsync();
```

### 4. Cache Frequently Used Queries

```csharp
// ✅ Cache results for hot locations
private readonly IMemoryCache _cache;

public async Task<List<Store>> GetNearbyStores(GeoLocation location)
{
    var cacheKey = $"stores_{location.ToGeoHash(6)}";
    
    if (_cache.TryGetValue(cacheKey, out List<Store> cached))
    {
        return cached;
    }
    
    var results = await QueryStores(location);
    _cache.Set(cacheKey, results, TimeSpan.FromMinutes(5));
    
    return results;
}
```

### 5. Monitor Query Performance

```csharp
// ✅ Track query metrics
var stopwatch = Stopwatch.StartNew();

var results = await storeTable.Query
    .Where<Store>(x => x.Location.WithinDistanceKilometers(center, 5))
    .ExecuteAsync();

stopwatch.Stop();

_logger.LogInformation(
    "Geospatial query: {Count} results in {Ms}ms, {Precision} precision",
    results.Count,
    stopwatch.ElapsedMilliseconds,
    7);
```

## Summary

### Key Limitations

1. **Rectangular queries only** - Always post-filter for circular distances
2. **No native distance sorting** - Sort in memory after retrieval
3. **Pagination complexity** - Retrieve all results for distance-sorted pagination
4. **Cell boundary issues** - Query neighbor cells for critical applications
5. **Precision trade-offs** - Balance accuracy vs. query efficiency
6. **Date line handling** - Split queries that cross ±180° longitude
7. **Polar region accuracy** - Use lower precision near poles

### Best Practices

1. Always post-filter for exact circular distances
2. Use precision 6-7 for most applications
3. Implement result limits to prevent over-retrieval
4. Cache frequently used queries
5. Monitor query performance and costs
6. Test with your actual data distribution
7. Consider alternative encoding schemes (S2, H3) for specialized use cases

### When to Use Alternatives

Consider alternative solutions if you need:
- **True circular queries** without post-filtering
- **Native distance sorting** in the database
- **Efficient pagination** with distance sorting
- **High accuracy near poles** (consider S2 or H3)
- **Complex spatial operations** (intersections, unions, etc.)

For most location-based applications, GeoHash with DynamoDB provides an excellent balance of simplicity, performance, and accuracy.

## See Also

- [README.md](README.md) - Getting started guide
- [PRECISION_GUIDE.md](PRECISION_GUIDE.md) - Choosing the right precision
- [EXAMPLES.md](EXAMPLES.md) - Usage examples and patterns
