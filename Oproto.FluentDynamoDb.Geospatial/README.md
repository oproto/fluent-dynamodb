# Oproto.FluentDynamoDb.Geospatial

Geospatial query support for Oproto.FluentDynamoDb with multiple spatial indexing systems. This package enables efficient location-based queries in DynamoDB with type-safe APIs and seamless integration with the FluentDynamoDb library.

## Features

- **Multiple Spatial Index Types**: Choose between GeoHash, S2, and H3 based on your needs
- **GeoLocation Type**: Type-safe geographic coordinates with validation
- **Distance Calculations**: Built-in Haversine formula for accurate distance calculations in meters, kilometers, and miles
- **Flexible Encoding**: GeoHash, S2 (Google), and H3 (Uber) spatial indexing systems
- **Lambda Expression Queries**: Type-safe proximity and bounding box queries
- **Bounding Box Support**: Create rectangular geographic areas for queries
- **Coordinate Storage**: Optional full-resolution coordinate storage alongside spatial indices
- **Pagination Support**: Efficient paginated queries with spiral ordering
- **AOT Compatible**: Works with Native AOT compilation
- **Zero External Dependencies**: Custom implementations with no external geospatial libraries

## Installation

```bash
dotnet add package Oproto.FluentDynamoDb.Geospatial
```

Or add to your `.csproj` file:

```xml
<PackageReference Include="Oproto.FluentDynamoDb.Geospatial" Version="1.0.0" />
```

## Quick Start

### 1. Define Your Entity

Choose your spatial index type based on your needs:

**Option A: GeoHash (Simple, Fast)**
```csharp
using Oproto.FluentDynamoDb.Attributes;
using Oproto.FluentDynamoDb.Geospatial;

[DynamoDbTable("stores")]
public partial class Store
{
    [PartitionKey]
    [DynamoDbAttribute("pk")]
    public string StoreId { get; set; }
    
    [DynamoDbAttribute("location", GeoHashPrecision = 7)]
    public GeoLocation Location { get; set; }
    
    [DynamoDbAttribute("name")]
    public string Name { get; set; }
}
```

**Option B: S2 (Global Coverage, Better at Poles)**
```csharp
[DynamoDbTable("stores")]
public partial class Store
{
    [PartitionKey]
    [DynamoDbAttribute("pk")]
    public string StoreId { get; set; }
    
    [DynamoDbAttribute("location", SpatialIndexType = SpatialIndexType.S2, S2Level = 16)]
    public GeoLocation Location { get; set; }
    
    [DynamoDbAttribute("name")]
    public string Name { get; set; }
}
```

**Option C: H3 (Hexagonal, Most Uniform Coverage)**
```csharp
[DynamoDbTable("stores")]
public partial class Store
{
    [PartitionKey]
    [DynamoDbAttribute("pk")]
    public string StoreId { get; set; }
    
    [DynamoDbAttribute("location", SpatialIndexType = SpatialIndexType.H3, H3Resolution = 9)]
    public GeoLocation Location { get; set; }
    
    [DynamoDbAttribute("name")]
    public string Name { get; set; }
}
```

### 2. Create and Store Locations

```csharp
using Oproto.FluentDynamoDb.Geospatial;

var store = new Store
{
    StoreId = "STORE#123",
    Location = new GeoLocation(37.7749, -122.4194), // San Francisco
    Name = "Downtown Store"
};

await storeTable.PutAsync(store);
```

### 3. Query by Proximity

**GeoHash (Legacy Lambda Expression API):**
```csharp
using Oproto.FluentDynamoDb.Geospatial.GeoHash;

// Find stores within 5 kilometers
var center = new GeoLocation(37.7749, -122.4194);
var nearbyStores = await storeTable.Query
    .Where<Store>(x => x.Location.WithinDistanceKilometers(center, 5))
    .ExecuteAsync();

// Sort by actual distance
var sortedStores = nearbyStores
    .OrderBy(s => s.Location.DistanceToKilometers(center))
    .ToList();
```

**S2 and H3 (New SpatialQueryAsync API with Lambda Expressions):**
```csharp
using Oproto.FluentDynamoDb.Geospatial;

// Find ALL stores within 5km (non-paginated - fastest)
var center = new GeoLocation(37.7749, -122.4194);
var result = await storeTable.SpatialQueryAsync(
    spatialAttributeName: "location",
    center: center,
    radiusKilometers: 5,
    queryBuilder: (query, cell, pagination) => query
        // Lambda expression: x.Location == cell works due to implicit cast
        // The GeoLocation.SpatialIndex property is compared to the cell token
        .Where<Store>(x => x.PartitionKey == "STORE" && x.Location == cell)
        .Paginate(pagination),
    pageSize: null  // No pagination - queries all cells in parallel
);

// Results are automatically sorted by distance
foreach (var store in result.Items)
{
    var distance = store.Location.DistanceToKilometers(center);
    Console.WriteLine($"{store.Name}: {distance:F2}km away");
}
```

**Understanding the Lambda Expression Syntax:**

The `x.Location == cell` syntax works because:
1. When a `GeoLocation` is deserialized from DynamoDB, it stores the original spatial index value (GeoHash/S2 token/H3 index) in the `SpatialIndex` property
2. `GeoLocation` has an implicit cast to `string?` that returns the `SpatialIndex` value
3. This enables natural comparison syntax in lambda expressions without explicitly accessing `.SpatialIndex`

**Alternative Query Expression Styles:**

```csharp
// 1. Implicit cast (recommended - most concise)
.Where<Store>(x => x.PartitionKey == "STORE" && x.Location == cell)

// 2. Explicit property access (also works)
.Where<Store>(x => x.PartitionKey == "STORE" && x.Location.SpatialIndex == cell)

// 3. Format string (works without lambda expressions)
.Where("pk = {0} AND location = {1}", "STORE", cell)

// 4. Plain text with parameters (works without lambda expressions)
.Where("pk = :pk AND location = :loc")
    .WithValue(":pk", "STORE")
    .WithValue(":loc", cell)
```

All four approaches produce the same DynamoDB query, but lambda expressions provide compile-time type safety.

## Basic Usage Examples

### Working with GeoLocation

```csharp
// Create a location from coordinates
var sanFrancisco = new GeoLocation(37.7749, -122.4194);
var newYork = new GeoLocation(40.7128, -74.0060);

// Calculate distances in different units
double distanceMeters = sanFrancisco.DistanceToMeters(newYork);      // ~4,130,000 meters
double distanceKm = sanFrancisco.DistanceToKilometers(newYork);      // ~4,130 km
double distanceMiles = sanFrancisco.DistanceToMiles(newYork);        // ~2,566 miles

// Encode to GeoHash
string hash = sanFrancisco.ToGeoHash(7); // "9q8yy9r"

// Decode from GeoHash
var location = GeoLocation.FromGeoHash("9q8yy9r");

// Understanding the SpatialIndex Property
// When created from coordinates, SpatialIndex is null
Console.WriteLine(sanFrancisco.SpatialIndex); // null

// When deserialized from DynamoDB, SpatialIndex contains the stored value
// This enables efficient query comparisons without recalculation
var store = await storeTable.GetAsync("STORE#123");
Console.WriteLine(store.Location.SpatialIndex); // "9q8yy9r" (or S2 token/H3 index)

// The implicit cast enables natural comparison syntax
if (store.Location == "9q8yy9r")
{
    Console.WriteLine("Location matches the expected cell");
}
```

### Proximity Queries (All Distance Units)

```csharp
var center = new GeoLocation(37.7749, -122.4194);

// Query with meters
var storesInMeters = await storeTable.Query
    .Where<Store>(x => x.Location.WithinDistanceMeters(center, 5000))
    .ExecuteAsync();

// Query with kilometers
var storesInKm = await storeTable.Query
    .Where<Store>(x => x.Location.WithinDistanceKilometers(center, 5))
    .ExecuteAsync();

// Query with miles
var storesInMiles = await storeTable.Query
    .Where<Store>(x => x.Location.WithinDistanceMiles(center, 3.1))
    .ExecuteAsync();
```

### Bounding Box Queries

```csharp
// Define a rectangular area
var southwest = new GeoLocation(37.7, -122.5);
var northeast = new GeoLocation(37.8, -122.4);

var storesInArea = await storeTable.Query
    .Where<Store>(x => x.Location.WithinBoundingBox(southwest, northeast))
    .ExecuteAsync();

// Or create from center and distance
var bbox = GeoBoundingBox.FromCenterAndDistanceKilometers(center, 5);
var storesInBbox = await storeTable.Query
    .Where<Store>(x => x.Location.WithinBoundingBox(bbox.Southwest, bbox.Northeast))
    .ExecuteAsync();
```

### Manual Query Pattern

```csharp
// For advanced scenarios
var center = new GeoLocation(37.7749, -122.4194);
var bbox = GeoBoundingBox.FromCenterAndDistanceKilometers(center, 5);
var (minHash, maxHash) = bbox.GetGeoHashRange(7);

var stores = await storeTable.Query
    .Where("location BETWEEN :minHash AND :maxHash")
    .WithValue(":minHash", minHash)
    .WithValue(":maxHash", maxHash)
    .ExecuteAsync();
```

## Choosing a Spatial Index Type

### Quick Comparison

| Feature | GeoHash | S2 | H3 |
|---------|---------|----|----|
| **Cell Shape** | Rectangle | Square | Hexagon |
| **Precision Levels** | 1-12 | 0-30 | 0-15 |
| **Default** | 6 (~610m) | 16 (~1.5km) | 9 (~174m) |
| **Query Type** | Single BETWEEN | Multiple queries | Multiple queries |
| **Best For** | Simple queries | Global coverage | Uniform coverage |
| **Pole Handling** | Poor | Good | Excellent |

### When to Use Each

**Use GeoHash when:**
- ✅ Simple proximity queries
- ✅ Low latency critical (single query)
- ✅ Mid-latitude locations
- ✅ Backward compatibility needed

**Use S2 when:**
- ✅ Global coverage needed
- ✅ Polar regions important
- ✅ Hierarchical queries
- ✅ Area uniformity matters

**Use H3 when:**
- ✅ Most uniform coverage needed
- ✅ Hexagonal neighbors important
- ✅ Grid analysis required
- ✅ Visual appeal matters

### Precision Guide

**GeoHash:**
| Precision | Cell Size | Use Case |
|-----------|-----------|----------|
| 5 | ~2.4 km | Neighborhood |
| **6** | **~610 m** | **Default - District** |
| 7 | ~76 m | Street-level |
| 8 | ~19 m | Building-level |

**S2:**
| Level | Cell Size | Use Case |
|-------|-----------|----------|
| 14 | ~6 km | District |
| **16** | **~1.5 km** | **Default - Neighborhood** |
| 18 | ~400 m | Street-level |
| 20 | ~100 m | Building-level |

**H3:**
| Resolution | Cell Edge | Use Case |
|------------|-----------|----------|
| 7 | ~1.2 km | Neighborhood |
| 8 | ~460 m | Local area |
| **9** | **~174 m** | **Default - Street-level** |
| 10 | ~66 m | Building-level |

**⚠️ Warning**: Higher precision = more cells = slower paginated queries. See [Precision Guide](PRECISION_GUIDE.md) for details.

## Important Limitations

### DynamoDB Query Patterns

1. **Rectangular Queries Only**: DynamoDB BETWEEN queries create rectangular bounding boxes, not circles
   - Results may include locations outside the circular distance
   - Use post-filtering with `DistanceTo()` methods for exact circular queries

2. **No Native Distance Sorting**: DynamoDB cannot sort by distance from a point
   - Retrieve results and sort in memory using `DistanceTo()` methods
   - Pagination with distance sorting requires custom implementation

3. **Single Range Query**: DynamoDB supports one BETWEEN condition per query
   - Boundary cases may require querying neighbor cells
   - Multiple non-contiguous areas require multiple queries

### Edge Cases

- **Poles**: GeoHash precision decreases near poles due to longitude convergence
- **Date Line**: Queries crossing the international date line (±180°) require special handling
- **Cell Boundaries**: Locations near GeoHash cell boundaries may require querying neighbor cells for complete results

## Performance Considerations

- **Encoding/Decoding**: Very fast (<1 microsecond), O(precision) complexity
- **Query Efficiency**: Lower precision = fewer items scanned but less accurate
- **Memory**: All types are readonly structs with minimal heap allocations
- **Caching**: Consider caching frequently used GeoHash values for hot paths

## Understanding the SpatialIndex Property

The `GeoLocation` struct includes a `SpatialIndex` property that stores the original spatial index value (GeoHash/S2 token/H3 index) when deserialized from DynamoDB. This enables efficient spatial queries using natural lambda expression syntax.

### When is SpatialIndex Populated?

**SpatialIndex is NULL when:**
- Creating a location directly from coordinates: `new GeoLocation(37.7749, -122.4194)`
- Using extension methods: `location.ToGeoHash(7)` returns a string, not a GeoLocation with SpatialIndex

**SpatialIndex is POPULATED when:**
- Deserializing from DynamoDB (source generator automatically includes it)
- Using `FromGeoHash()`, `FromS2Token()`, or `FromH3Index()` methods
- The value matches what's stored in the DynamoDB attribute

### Implicit Cast Behavior

`GeoLocation` has an implicit cast to `string?` that returns the `SpatialIndex` value:

```csharp
GeoLocation location = await GetLocationFromDynamoDB();

// These are equivalent:
string? index1 = location.SpatialIndex;  // Explicit property access
string? index2 = location;               // Implicit cast

// Both return the same value (e.g., "9q8yy9r" for GeoHash)
```

### Using in Lambda Expressions

The implicit cast enables natural comparison syntax in spatial queries:

```csharp
// ✅ Recommended: Implicit cast (most concise)
.Where<Store>(x => x.Location == cell)

// ✅ Also works: Explicit property access
.Where<Store>(x => x.Location.SpatialIndex == cell)

// Both compile to the same DynamoDB expression: location = :cell
```

### Equality Operators

`GeoLocation` provides equality operators for comparing with spatial index strings:

```csharp
var location = await GetLocationFromDynamoDB();
string cell = "9q8yy9r";

// All of these work:
if (location == cell) { }                    // Implicit cast
if (cell == location) { }                    // Reverse order
if (location.SpatialIndex == cell) { }       // Explicit property
if (location != cell) { }                    // Inequality
```

### Why This Matters

Without the `SpatialIndex` property, spatial queries would need to:
1. Recalculate the spatial index for every comparison (slow)
2. Use string-based expressions instead of lambda expressions (no type safety)

With the `SpatialIndex` property:
1. ✅ No recalculation needed - value is preserved from DynamoDB
2. ✅ Type-safe lambda expressions work naturally
3. ✅ Compile-time checking of property names
4. ✅ IntelliSense support in IDEs

### Example: Complete Flow

```csharp
// 1. Create and store a location
var newStore = new Store
{
    StoreId = "STORE#123",
    Location = new GeoLocation(37.7749, -122.4194), // SpatialIndex is null
    Name = "Downtown Store"
};
await storeTable.PutAsync(newStore);
// DynamoDB now contains: { "location": "9q8yy9r", ... }

// 2. Query using spatial index
var center = new GeoLocation(37.7749, -122.4194);
var result = await storeTable.SpatialQueryAsync(
    spatialAttributeName: "location",
    center: center,
    radiusKilometers: 5,
    queryBuilder: (query, cell, pagination) => query
        // cell = "9q8yy9r" (or similar)
        // x.Location == cell works because SpatialIndex is populated during deserialization
        .Where<Store>(x => x.PartitionKey == "STORE" && x.Location == cell)
        .Paginate(pagination)
);

// 3. Retrieved locations have SpatialIndex populated
foreach (var store in result.Items)
{
    Console.WriteLine($"Store: {store.Name}");
    Console.WriteLine($"Coordinates: {store.Location.Latitude}, {store.Location.Longitude}");
    Console.WriteLine($"Spatial Index: {store.Location.SpatialIndex}"); // "9q8yy9r"
    
    // Can compare directly
    if (store.Location == "9q8yy9r")
    {
        Console.WriteLine("This is the exact cell we queried!");
    }
}
```

## Advanced Features

### Paginated Queries

For large result sets, use pagination:

```csharp
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
Console.WriteLine($"Has more: {result.ContinuationToken != null}");

// Get next page
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
}
```

### Storing Exact Coordinates

Preserve full-resolution coordinates alongside spatial indices:

```csharp
[DynamoDbTable("stores")]
public partial class Store
{
    [PartitionKey]
    [DynamoDbAttribute("pk")]
    public string StoreId { get; set; }
    
    // Spatial index for queries
    [DynamoDbAttribute("location", SpatialIndexType = SpatialIndexType.S2, S2Level = 16)]
    public GeoLocation Location { get; set; }
    
    // Store exact coordinates
    [DynamoDbAttribute("location_lat")]
    public double LocationLatitude => Location.Latitude;
    
    [DynamoDbAttribute("location_lon")]
    public double LocationLongitude => Location.Longitude;
}

// Deserialization automatically uses exact coordinates if available
// Falls back to spatial index (cell center) if coordinates are missing
```

### Working with Cells Directly

```csharp
using Oproto.FluentDynamoDb.Geospatial.S2;
using Oproto.FluentDynamoDb.Geospatial.H3;

var location = new GeoLocation(37.7749, -122.4194);

// S2 cells
var s2Cell = location.ToS2Cell(level: 16);
var s2Neighbors = s2Cell.GetNeighbors();  // 8 neighbors
var s2Parent = s2Cell.GetParent();        // Level 15
var s2Children = s2Cell.GetChildren();    // 4 children

// H3 cells
var h3Cell = location.ToH3Cell(resolution: 9);
var h3Neighbors = h3Cell.GetNeighbors();  // 6 neighbors (hexagons)
var h3Parent = h3Cell.GetParent();        // Resolution 8
var h3Children = h3Cell.GetChildren();    // 7 children
```

## Documentation

For comprehensive documentation, examples, and advanced usage patterns, see:
- [S2 and H3 Usage Guide](S2_H3_USAGE_GUIDE.md) - Choosing between index types
- [Precision Selection Guide](PRECISION_GUIDE.md) - Choosing precision levels and avoiding query explosion
- [Performance Guide](PERFORMANCE_GUIDE.md) - Query optimization and performance tuning
- [Coordinate Storage Guide](COORDINATE_STORAGE_GUIDE.md) - Storing full-resolution coordinates
- [Examples](EXAMPLES.md) - More code examples and patterns

## Requirements

- .NET 8.0 or later
- Oproto.FluentDynamoDb 1.0.0 or later
- AOT compatible

## License

[Your License Here]

## Contributing

Contributions are welcome! Please see [CONTRIBUTING.md](../CONTRIBUTING.md) for details.
