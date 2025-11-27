# Design Document

## Overview

This design extends the Oproto.FluentDynamoDb.Geospatial library to support multiple spatial indexing systems beyond the existing GeoHash implementation. The design adds support for Google S2 geometry cells and Uber H3 hexagonal hierarchical spatial indexing, while maintaining backward compatibility with existing GeoHash functionality.

The key architectural decision is to create a pluggable spatial index system where different encoding strategies can be selected via attribute configuration. The design also introduces multi-field serialization, allowing developers to store full-resolution coordinates alongside spatial indices for exact location preservation.

### Plus Codes Evaluation

After evaluation, **Plus Codes (Open Location Codes) will NOT be supported** in this implementation for the following reasons:

1. **Not Optimized for Range Queries**: Plus Codes are designed for human-readable location sharing, not for efficient spatial range queries. They do not form a continuous space-filling curve like GeoHash, S2, or H3.

2. **Poor BETWEEN Query Performance**: Plus Codes do not guarantee that nearby locations have lexicographically similar codes, making DynamoDB BETWEEN queries inefficient and potentially returning many false positives.

3. **Limited Spatial Properties**: Unlike GeoHash/S2/H3, Plus Codes lack hierarchical cell relationships and neighbor calculations that are essential for efficient spatial queries.

4. **Better Alternatives Available**: S2 and H3 provide superior spatial query performance while GeoHash offers simplicity. Plus Codes do not fill a unique niche in this context.

The documentation will explain this decision and recommend Plus Codes only for display/sharing purposes, not for spatial indexing.

## Architecture

### Spatial Index Type System

The design introduces a `SpatialIndexType` enumeration to represent the supported spatial indexing systems:

```csharp
public enum SpatialIndexType
{
    GeoHash = 0,  // Default for backward compatibility
    S2 = 1,
    H3 = 2
}
```

### Attribute Configuration

The `DynamoDbAttributeAttribute` will be extended with new properties:

```csharp
public class DynamoDbAttributeAttribute : Attribute
{
    // Existing properties...
    public int GeoHashPrecision { get; set; }
    
    // New properties
    public SpatialIndexType SpatialIndexType { get; set; } = SpatialIndexType.GeoHash;
    public int S2Level { get; set; } = 0;  // 0 means use default (16)
    public int H3Resolution { get; set; } = 0;  // 0 means use default (9)
}
```

### Multi-Field Serialization Approach

Instead of automatically creating suffixed attributes, developers can explicitly define separate properties for latitude and longitude if they want full-resolution storage:

**Option 1: Separate Properties (Recommended)**

```csharp
[DynamoDbTable("stores")]
public partial class Store
{
    [PartitionKey]
    [DynamoDbAttribute("pk")]
    public string StoreId { get; set; }
    
    // Spatial index for queries
    [DynamoDbAttribute("location_geohash", SpatialIndexType = SpatialIndexType.GeoHash)]
    public GeoLocation Location { get; set; }
    
    // Optional: Store full coordinates separately
    [DynamoDbAttribute("location_lat")]
    public double LocationLatitude => Location.Latitude;
    
    [DynamoDbAttribute("location_lon")]
    public double LocationLongitude => Location.Longitude;
}
```

**Option 2: Computed Properties**

The source generator can recognize computed properties (properties with only getters that reference other properties) and automatically serialize them:

```csharp
[DynamoDbTable("stores")]
public partial class Store
{
    [PartitionKey]
    [DynamoDbAttribute("pk")]
    public string StoreId { get; set; }
    
    [DynamoDbAttribute("location", SpatialIndexType = SpatialIndexType.S2)]
    public GeoLocation Location { get; set; }
    
    // Source generator recognizes these as computed from Location
    [DynamoDbAttribute("lat")]
    public double Latitude => Location.Latitude;
    
    [DynamoDbAttribute("lon")]
    public double Longitude => Location.Longitude;
}
```

**Option 3: Explicit Coordinate Storage Attribute**

For developers who want automatic coordinate storage with custom naming:

```csharp
// New attribute for explicit coordinate storage
[AttributeUsage(AttributeTargets.Property)]
public class StoreCoordinatesAttribute : Attribute
{
    public string LatitudeAttributeName { get; set; }
    public string LongitudeAttributeName { get; set; }
}

// Usage
[DynamoDbTable("stores")]
public partial class Store
{
    [DynamoDbAttribute("location", SpatialIndexType = SpatialIndexType.H3)]
    [StoreCoordinates(LatitudeAttributeName = "lat", LongitudeAttributeName = "lon")]
    public GeoLocation Location { get; set; }
}
```

This approach:
- ✅ Gives developers full control over attribute naming
- ✅ Doesn't impose naming conventions
- ✅ Works with existing DynamoDB schemas
- ✅ Allows mixing spatial indices with coordinate storage
- ✅ Supports multiple GeoLocation properties with different storage strategies

### Directory Structure

The geospatial library will be organized as follows:

```
Oproto.FluentDynamoDb.Geospatial/
├── GeoLocation.cs (existing)
├── GeoBoundingBox.cs (existing)
├── SpatialIndexType.cs (new)
├── GeoHash/
│   ├── GeoHashCell.cs (existing)
│   ├── GeoHashEncoder.cs (existing)
│   ├── GeoHashExtensions.cs (existing)
│   ├── GeoHashQueryExtensions.cs (existing)
│   └── GeoHashBoundingBoxExtensions.cs (existing)
├── S2/
│   ├── S2Cell.cs (new)
│   ├── S2Encoder.cs (new)
│   ├── S2Extensions.cs (new)
│   ├── S2QueryExtensions.cs (new)
│   └── S2BoundingBoxExtensions.cs (new)
└── H3/
    ├── H3Cell.cs (new)
    ├── H3Encoder.cs (new)
    ├── H3Extensions.cs (new)
    ├── H3QueryExtensions.cs (new)
    └── H3BoundingBoxExtensions.cs (new)
```

## Components and Interfaces

### 0. GeoLocation Enhancement for Query Support

To enable natural lambda expression syntax in spatial queries, the `GeoLocation` struct is enhanced to store and expose the spatial index value:

```csharp
public readonly struct GeoLocation : IEquatable<GeoLocation>
{
    /// <summary>
    /// Gets the latitude in degrees. Valid range is -90 to 90.
    /// </summary>
    public double Latitude { get; }

    /// <summary>
    /// Gets the longitude in degrees. Valid range is -180 to 180.
    /// </summary>
    public double Longitude { get; }
    
    /// <summary>
    /// Gets the spatial index value (GeoHash/S2 token/H3 index) if this location
    /// was deserialized from DynamoDB. Returns null if the location was created
    /// directly from coordinates.
    /// </summary>
    public string? SpatialIndex { get; }

    /// <summary>
    /// Initializes a new instance from coordinates only.
    /// SpatialIndex will be null.
    /// </summary>
    public GeoLocation(double latitude, double longitude)
    {
        Latitude = latitude;
        Longitude = longitude;
        SpatialIndex = null;
    }
    
    /// <summary>
    /// Initializes a new instance from coordinates and spatial index.
    /// Used by the source generator during deserialization.
    /// </summary>
    public GeoLocation(double latitude, double longitude, string? spatialIndex)
    {
        Latitude = latitude;
        Longitude = longitude;
        SpatialIndex = spatialIndex;
    }
    
    /// <summary>
    /// Implicit cast to string returns the SpatialIndex value.
    /// Enables natural comparison syntax: x.Location == cell
    /// </summary>
    public static implicit operator string?(GeoLocation location) => location.SpatialIndex;
    
    /// <summary>
    /// Equality operator for comparing GeoLocation to spatial index string.
    /// Enables lambda expressions: x.Location == cell
    /// </summary>
    public static bool operator ==(GeoLocation location, string? spatialIndex)
        => location.SpatialIndex == spatialIndex;
    
    public static bool operator !=(GeoLocation location, string? spatialIndex)
        => location.SpatialIndex != spatialIndex;
    
    // Reverse order operators
    public static bool operator ==(string? spatialIndex, GeoLocation location)
        => location.SpatialIndex == spatialIndex;
    
    public static bool operator !=(string? spatialIndex, GeoLocation location)
        => location.SpatialIndex != spatialIndex;
    
    // ... existing methods (DistanceToMeters, Equals, etc.)
}
```

**Key Design Points:**

1. **SpatialIndex Property**: Stores the original spatial index value (GeoHash/S2 token/H3 index) when deserialized from DynamoDB
2. **Dual Constructors**: 
   - `GeoLocation(lat, lon)` - For creating locations from coordinates (SpatialIndex = null)
   - `GeoLocation(lat, lon, spatialIndex)` - For deserialization (preserves the spatial index)
3. **Implicit Cast**: Allows `GeoLocation` to be used as a string in comparisons
4. **Equality Operators**: Enable natural syntax `x.Location == cell` in lambda expressions
5. **No Recalculation**: The spatial index is stored, not recalculated, for efficiency

**Source Generator Integration:**

When deserializing a `GeoLocation` property with a spatial index, the source generator will:

```csharp
// Read the spatial index from DynamoDB
var s2Token = item["location"].S;

// Decode to coordinates
var (lat, lon) = S2Encoder.Decode(s2Token);

// Create GeoLocation with the spatial index preserved
entity.Location = new GeoLocation(lat, lon, s2Token);
```

This applies to all three spatial index types (GeoHash, S2, H3) and works with both single-field and multi-field (coordinate storage) modes.

**Expression Translation:**

The ExpressionTranslator will recognize both forms:
- `x.Location == cell` (implicit cast)
- `x.Location.SpatialIndex == cell` (explicit property access)

Both translate to the same DynamoDB expression: `location = :cell`

### 1. Spatial Index Encoders

Each spatial index type will have its own encoder class following the pattern established by `GeoHashEncoder`:

#### S2Encoder

```csharp
internal static class S2Encoder
{
    // Encodes lat/lon to S2 cell token at specified level (0-30)
    public static string Encode(double latitude, double longitude, int level);
    
    // Decodes S2 cell token to center point coordinates
    public static (double Latitude, double Longitude) Decode(string s2Token);
    
    // Decodes S2 cell token to bounding box
    public static (double MinLat, double MaxLat, double MinLon, double MaxLon) DecodeBounds(string s2Token);
    
    // Gets the 8 neighboring S2 cells
    public static string[] GetNeighbors(string s2Token);
}
```

#### H3Encoder

```csharp
internal static class H3Encoder
{
    // Encodes lat/lon to H3 cell index at specified resolution (0-15)
    public static string Encode(double latitude, double longitude, int resolution);
    
    // Decodes H3 cell index to center point coordinates
    public static (double Latitude, double Longitude) Decode(string h3Index);
    
    // Decodes H3 cell index to bounding box (hexagon vertices)
    public static (double MinLat, double MaxLat, double MinLon, double MaxLon) DecodeBounds(string h3Index);
    
    // Gets the 6 neighboring H3 cells (hexagons have 6 neighbors)
    public static string[] GetNeighbors(string h3Index);
}
```

### 2. Cell Structures

Each spatial index type will have a cell structure similar to `GeoHashCell`:

#### S2Cell

```csharp
public readonly struct S2Cell
{
    public string Token { get; }
    public int Level { get; }
    public GeoBoundingBox Bounds { get; }
    
    public S2Cell(string token);
    public S2Cell(GeoLocation location, int level);
    
    public S2Cell[] GetNeighbors();
    public S2Cell GetParent();
    public S2Cell[] GetChildren();
}
```

#### H3Cell

```csharp
public readonly struct H3Cell
{
    public string Index { get; }
    public int Resolution { get; }
    public GeoBoundingBox Bounds { get; }
    
    public H3Cell(string index);
    public H3Cell(GeoLocation location, int resolution);
    
    public H3Cell[] GetNeighbors();  // Returns 6 neighbors for hexagons
    public H3Cell GetParent();
    public H3Cell[] GetChildren();   // Returns 7 children for H3
}
```

### 3. Extension Methods

Each spatial index type will provide extension methods for `GeoLocation`:

#### S2Extensions

```csharp
public static class S2Extensions
{
    public static string ToS2Token(this GeoLocation location, int level = 16);
    public static GeoLocation FromS2Token(string s2Token);
    public static S2Cell ToS2Cell(this GeoLocation location, int level = 16);
}
```

#### H3Extensions

```csharp
public static class H3Extensions
{
    public static string ToH3Index(this GeoLocation location, int resolution = 9);
    public static GeoLocation FromH3Index(string h3Index);
    public static H3Cell ToH3Cell(this GeoLocation location, int resolution = 9);
}
```

### 4. Spatial Query API

The library will provide a `SpatialQueryAsync` method that handles multi-cell queries for S2 and H3 (and optimized single-query for GeoHash):

#### SpatialQueryAsync Signature

```csharp
public static class SpatialQueryExtensions
{
    // Proximity query with distance
    public static Task<SpatialQueryResponse<TEntity>> SpatialQueryAsync<TEntity>(
        this DynamoDbTableBase table,
        Func<TEntity, GeoLocation> locationSelector,
        SpatialIndexType spatialIndexType,
        int precision,
        GeoLocation center,
        double radiusKilometers,
        Func<QueryRequestBuilder<TEntity>, string, IPaginationRequest, QueryRequestBuilder<TEntity>> queryBuilder,
        int? pageSize = null,
        SpatialContinuationToken? continuationToken = null,
        int maxCells = 100,
        CancellationToken cancellationToken = default)
        where TEntity : class, IDynamoDbEntity;
    
    // Bounding box query
    public static Task<SpatialQueryResponse<TEntity>> SpatialQueryAsync<TEntity>(
        this DynamoDbTableBase table,
        Func<TEntity, GeoLocation> locationSelector,
        SpatialIndexType spatialIndexType,
        int precision,
        GeoBoundingBox boundingBox,
        Func<QueryRequestBuilder<TEntity>, string, IPaginationRequest, QueryRequestBuilder<TEntity>> queryBuilder,
        int? pageSize = null,
        SpatialContinuationToken? continuationToken = null,
        int maxCells = 100,
        CancellationToken cancellationToken = default)
        where TEntity : class, IDynamoDbEntity;
}
```

**Important Design Decision: Location Selector Parameter**

The API uses `Func<TEntity, GeoLocation> locationSelector` instead of `string spatialAttributeName` for the following reasons:

1. **Type Safety**: The lambda expression `entity => entity.Location` is compile-time checked, ensuring the property exists
2. **AOT Compatibility**: No reflection needed - the lambda is just a property accessor
3. **Explicit and Clear**: Users specify exactly which property contains the location data
4. **Works with Current Architecture**: Since `QueryRequestBuilder<TEntity>.ToListAsync()` returns a single concrete type, the lambda approach is perfect

**Future Consideration**: For multi-entity table support (querying heterogeneous entity types), an overload accepting `string spatialAttributeName` could be added that uses entity metadata to extract location values. However, this is not needed for the current single-entity-type query model.

#### Query Builder Lambda

The query builder lambda receives three parameters:
1. **QueryRequestBuilder<TEntity>** - The query builder to configure
2. **string cellValue** - The current spatial cell value (GeoHash/S2 token/H3 index)
3. **IPaginationRequest** - The pagination configuration for this cell

Example usage:
```csharp
var results = await table.SpatialQueryAsync(
    locationSelector: store => store.Location,  // Lambda extracts the location
    spatialIndexType: SpatialIndexType.S2,
    precision: 16,
    center: new GeoLocation(37.7749, -122.4194),
    radiusKilometers: 5,
    queryBuilder: (query, cell, pagination) => query
        // Lambda expression: x.Location == cell works due to implicit cast
        // The GeoLocation.SpatialIndex property is compared to the cell token
        .Where<Store>(x => x.PartitionKey == "STORE" && x.Location == cell)
        .Paginate(pagination),
    pageSize: 50
);
```

**Alternative Query Expressions:**

The query builder supports multiple expression styles:

```csharp
// 1. Lambda with implicit cast (recommended - type-safe)
.Where<Store>(x => x.PartitionKey == "STORE" && x.Location == cell)

// 2. Lambda with explicit property access
.Where<Store>(x => x.PartitionKey == "STORE" && x.Location.SpatialIndex == cell)

// 3. Format string (works without GeoLocation enhancement)
.Where("pk = {0} AND location = {1}", "STORE", cell)

// 4. Plain text with parameters (works without GeoLocation enhancement)
.Where("pk = :pk AND location = :loc")
    .WithValue(":pk", "STORE")
    .WithValue(":loc", cell)
```

All four approaches produce the same DynamoDB query, but lambda expressions provide compile-time type safety.

#### SpatialQueryResponse

```csharp
public class SpatialQueryResponse<TEntity>
{
    public List<TEntity> Items { get; set; }
    public SpatialContinuationToken? ContinuationToken { get; set; }
    public int TotalCellsQueried { get; set; }
    public int TotalItemsScanned { get; set; }
}
```

#### SpatialContinuationToken

```csharp
public class SpatialContinuationToken
{
    // Index of the current cell in the spiral-ordered cell list
    public int CellIndex { get; set; }
    
    // DynamoDB's LastEvaluatedKey for pagination within the current cell
    // Null if the cell is exhausted and we should move to the next cell
    public string? LastEvaluatedKey { get; set; }
    
    // Serialization methods for passing between requests
    public string ToBase64();
    public static SpatialContinuationToken FromBase64(string token);
}
```

### 5. Cell Covering Computation

Each spatial index type will provide methods to compute cell coverings for spatial queries:

#### S2CellCovering

```csharp
public static class S2CellCovering
{
    // Gets the list of S2 cells that cover a circular area
    public static List<string> GetCellsForRadius(
        GeoLocation center, 
        double radiusKilometers, 
        int level,
        int maxCells = 100);
    
    // Gets the list of S2 cells that cover a bounding box
    public static List<string> GetCellsForBoundingBox(
        GeoBoundingBox bbox, 
        int level,
        int maxCells = 100);
}
```

#### H3CellCovering

```csharp
public static class H3CellCovering
{
    // Gets the list of H3 cells that cover a circular area
    public static List<string> GetCellsForRadius(
        GeoLocation center, 
        double radiusKilometers, 
        int resolution,
        int maxCells = 100);
    
    // Gets the list of H3 cells that cover a bounding box
    public static List<string> GetCellsForBoundingBox(
        GeoBoundingBox bbox, 
        int resolution,
        int maxCells = 100);
}
```

#### GeoHashCellCovering

```csharp
public static class GeoHashCellCovering
{
    // Gets the GeoHash range for a circular area (single BETWEEN query)
    public static (string MinHash, string MaxHash) GetRangeForRadius(
        GeoLocation center, 
        double radiusKilometers, 
        int precision);
    
    // Gets the GeoHash range for a bounding box (single BETWEEN query)
    public static (string MinHash, string MaxHash) GetRangeForBoundingBox(
        GeoBoundingBox bbox, 
        int precision);
}
```

## Spatial Query Implementation

### Why Different Approaches for Different Index Types

**GeoHash**: Forms a continuous lexicographic space-filling curve. Nearby locations have similar prefixes, allowing efficient single BETWEEN queries.

**S2 and H3**: Use hierarchical cell structures that don't form continuous lexicographic ranges. A bounding box or radius may span multiple non-contiguous cells, requiring multiple discrete queries.

### Two Query Modes

#### Non-Paginated Mode (pageSize = null)
- **Goal**: Return all results as fast as possible
- **Strategy**: Query all cells in parallel using `Task.WhenAll`
- **Performance**: Optimal - all queries execute simultaneously
- **Use case**: When you need all results and can handle them in memory

#### Paginated Mode (pageSize > 0)
- **Goal**: Return consistent page sizes with predictable memory usage
- **Strategy**: Query cells sequentially in spiral order (closest to farthest)
- **Performance**: Slower than non-paginated, but memory-efficient
- **Use case**: Large result sets, infinite scroll, API responses

### Spiral Ordering

For paginated queries, cells are ordered by distance from the search center:

```
Cell ordering for radius search:
  Cell 0: Center cell (contains the search point)
  Cells 1-6/8: Immediate neighbors (first ring)
  Cells 7-18/24: Second ring
  Cells 19-42/48: Third ring
  ... and so on
```

**Benefits:**
- Most relevant results (closest to center) appear in early pages
- Early termination: if user only views first page, we only query nearby cells
- Better UX: "Stores near you" shows closest stores first
- Predictable performance: each page queries similar number of cells

### SpatialQueryAsync Algorithm

#### Non-Paginated Mode

1. **Determine Spatial Index Type**: Read entity metadata to determine GeoHash/S2/H3
2. **Compute Cell Covering**: 
   - For GeoHash: Compute min/max range (single query)
   - For S2/H3: Compute list of cells that cover the area
3. **Execute All Queries in Parallel**:
   - For each cell, invoke query builder lambda with cell value
   - Execute all queries in parallel using `Task.WhenAll`
4. **Merge and Deduplicate Results**:
   - Combine results from all queries
   - Deduplicate by primary key (items may appear in multiple cells)
5. **Post-Filter and Sort**:
   - Calculate exact distance from center to each result
   - Filter out items outside the exact radius
   - Sort by distance (closest first)
6. **Return All Results**: No continuation token

#### Paginated Mode

1. **Determine Spatial Index Type**: Read entity metadata
2. **Compute Cell Covering and Sort by Distance**:
   - For S2/H3: Compute list of cells, sort by distance from center (spiral order)
   - For GeoHash: Compute min/max range (single query, no sorting needed)
3. **Resume from Continuation Token** (if provided):
   - Start from cell at `CellIndex`
   - Use `LastEvaluatedKey` to resume within that cell
4. **Query Cells Sequentially**:
   - Query one cell at a time in spiral order
   - Collect results until `pageSize` reached
   - If cell exhausted (no LastEvaluatedKey), move to next cell
   - If `pageSize` reached mid-cell, stop and save position
5. **Generate Continuation Token**:
   - If stopped mid-cell: store `CellIndex` + `LastEvaluatedKey`
   - If stopped between cells: store next `CellIndex`
   - If all cells complete: return null token
6. **Post-Filter Results**:
   - Calculate exact distance from center
   - Filter out items outside exact radius
   - Results are already roughly sorted by distance (due to spiral ordering)

### Pagination Strategy

The continuation token contains:
- **CellIndex**: Which cell in the spiral-ordered covering we're currently querying
- **LastEvaluatedKey**: DynamoDB's pagination key within the current cell (if mid-cell)

**Example pagination flow:**
```
Request 1: pageSize=50
  - Query cell 0 (center): 30 items
  - Query cell 1: 25 items (total: 55, over limit)
  - Return: 50 items (30 from cell 0, 20 from cell 1)
  - Token: { CellIndex: 1, LastEvaluatedKey: "..." }

Request 2: pageSize=50, token from above
  - Resume cell 1 from LastEvaluatedKey: 5 items
  - Query cell 2: 40 items (total: 45)
  - Query cell 3: 10 items (total: 55, over limit)
  - Return: 50 items (5 from cell 1, 40 from cell 2, 5 from cell 3)
  - Token: { CellIndex: 3, LastEvaluatedKey: "..." }

Request 3: pageSize=50, token from above
  - Resume cell 3 from LastEvaluatedKey: 5 items
  - Query cell 4: 20 items (total: 25)
  - Query cell 5: 15 items (total: 40)
  - Query cell 6: 8 items (total: 48, last cell)
  - Return: 48 items
  - Token: null (all cells complete)
```

### Post-Filtering

Since cell coverings are approximations (especially for circular radius queries), results should be post-filtered:
1. Calculate exact distance from center to each result
2. Filter out items outside the exact radius
3. For non-paginated mode: sort by distance
4. For paginated mode: results are already roughly sorted due to spiral ordering

This post-filtering happens in-memory after collecting results from cells.

## Spatial Query Architecture

### Layered Architecture

The spatial query implementation uses a layered architecture to support both table and GSI queries without code duplication, and to allow users to provide custom cell lists:

```
┌─────────────────────────────────────────────────────────────────────────────┐
│                           PUBLIC API LAYER                                   │
├─────────────────────────────────────────────────────────────────────────────┤
│  Table.SpatialQueryAsync(center, radius, ...)                               │
│  Index.SpatialQueryAsync(center, radius, ...)                               │
│       │                                                                      │
│       ▼                                                                      │
│  Calculates cells using S2/H3/GeoHash covering                              │
│       │                                                                      │
├───────┼─────────────────────────────────────────────────────────────────────┤
│       │                                                                      │
│  Table.SpatialQueryAsync(cells[], ...)    ◄── User provides custom cells    │
│  Index.SpatialQueryAsync(cells[], ...)                                       │
│       │                                                                      │
│       ▼                                                                      │
├─────────────────────────────────────────────────────────────────────────────┤
│                        SHARED CORE IMPLEMENTATION                            │
├─────────────────────────────────────────────────────────────────────────────┤
│  SpatialQueryCore.ExecuteAsync(querySource, cells[], ...)                   │
│       - Handles pagination vs parallel execution                             │
│       - Handles spiral ordering (when center provided)                       │
│       - Handles result merging and distance filtering                        │
│       - Works with any query source (table or index)                         │
└─────────────────────────────────────────────────────────────────────────────┘
```

### Query Source Abstraction

Both `DynamoDbTableBase.Query<TEntity>()` and `DynamoDbIndex.Query<TEntity>()` return `QueryRequestBuilder<TEntity>`. The only difference is which one creates the builder.

The shared implementation simply accepts a factory function:

```csharp
// Core implementation signature
private static async Task<SpatialQueryResponse<TEntity>> SpatialQueryCoreAsync<TEntity>(
    Func<QueryRequestBuilder<TEntity>> createQuery,  // Factory to create query builder
    // ... other parameters
)

// Table extension calls:
SpatialQueryCoreAsync(() => table.Query<TEntity>(), ...)

// Index extension calls:
SpatialQueryCoreAsync(() => index.Query<TEntity>(), ...)
```

No interface or delegate type needed - just a simple `Func<QueryRequestBuilder<TEntity>>`. This is the minimal abstraction required.

### Custom Cell List Support

Users can provide their own list of cells for advanced use cases:

```csharp
/// <summary>
/// Performs a spatial query using a pre-computed list of cells.
/// Use this when you have custom cell computation logic (e.g., H3 k-ring, polyfill).
/// </summary>
public static Task<SpatialQueryResponse<TEntity>> SpatialQueryAsync<TEntity>(
    this DynamoDbTableBase table,
    Func<TEntity, GeoLocation> locationSelector,
    IEnumerable<string> cells,
    Func<QueryRequestBuilder<TEntity>, string, IPaginationRequest, QueryRequestBuilder<TEntity>> queryBuilder,
    GeoLocation? center = null,  // Optional: for distance sorting
    double? radiusKilometers = null,  // Optional: for distance filtering
    int? pageSize = null,
    SpatialContinuationToken? continuationToken = null,
    CancellationToken cancellationToken = default)
    where TEntity : class, IDynamoDbEntity;
```

**Use Cases for Custom Cell Lists:**

1. **H3 K-Ring**: Use H3's k-ring function for hexagonal ring queries
2. **H3 Polyfill**: Use H3's polyfill for polygon-based queries
3. **Custom Algorithms**: Implement specialized cell selection logic
4. **Third-Party Libraries**: Use external S2/H3 libraries for advanced features

**Example: Using H3 K-Ring from External Library**

```csharp
// Using a third-party H3 library for k-ring
var centerCell = H3Index.FromLatLng(center.Latitude, center.Longitude, resolution: 9);
var cells = centerCell.KRing(k: 2);  // Get 2-ring of hexagons

var result = await table.SpatialQueryAsync(
    locationSelector: store => store.Location,
    cells: cells.Select(c => c.ToString()),
    queryBuilder: (query, cell, pagination) => query
        .Where<Store>(x => x.PartitionKey == "STORE" && x.Location == cell)
        .Paginate(pagination),
    center: center,  // For distance sorting
    radiusKilometers: 5.0,  // For distance filtering
    pageSize: 50
);
```

### GSI Support

The spatial query extensions support both main table queries and GSI queries:

#### Table Query (Main Table)

```csharp
// Query the main table
var result = await table.SpatialQueryAsync<Store>(
    locationSelector: store => store.Location,
    spatialIndexType: SpatialIndexType.S2,
    precision: 16,
    center: center,
    radiusKilometers: 5.0,
    queryBuilder: (query, cell, pagination) => query
        .Where<Store>(x => x.PartitionKey == "STORE" && x.Location == cell)
        .Paginate(pagination)
);
```

#### GSI Query (Global Secondary Index)

```csharp
// Define a GSI where the spatial index is the partition key
public class StoreTable : DynamoDbTableBase
{
    public DynamoDbIndex LocationIndex => new DynamoDbIndex(this, "location-index");
}

// Query via GSI - allows multiple stores per cell
var result = await table.LocationIndex.SpatialQueryAsync<Store>(
    locationSelector: store => store.Location,
    spatialIndexType: SpatialIndexType.S2,
    precision: 16,
    center: center,
    radiusKilometers: 5.0,
    queryBuilder: (query, cell, pagination) => query
        .Where<Store>(x => x.Location == cell)  // GSI PK is the cell
        .Paginate(pagination)
);
```

#### When to Use GSI for Spatial Queries

**Use Main Table When:**
- Location is the sort key (one item per cell per partition)
- You always query with a known partition key
- Simple access patterns

**Use GSI When:**
- You need multiple items per cell (e.g., many stores in same area)
- You want to query by location without knowing the partition key
- You need to support large-scale pagination tests (1000+ items)

#### GSI Design Pattern for Spatial Queries

```csharp
[DynamoDbTable("stores")]
public partial class StoreWithGsi
{
    // Main table: PK=StoreId (unique), SK=Category
    [PartitionKey]
    [DynamoDbAttribute("pk")]
    public string StoreId { get; set; }
    
    [SortKey]
    [DynamoDbAttribute("sk")]
    public string Category { get; set; }
    
    // GSI: PK=S2Cell, SK=StoreId
    // This allows multiple stores per cell
    [DynamoDbAttribute("s2cell", SpatialIndexType = SpatialIndexType.S2, S2Level = 16)]
    public GeoLocation Location { get; set; }
    
    [DynamoDbAttribute("name")]
    public string Name { get; set; }
}

// GSI Definition (in DynamoDB):
// - GSI Name: "location-index"
// - GSI PK: "s2cell" (the S2 token)
// - GSI SK: "pk" (the StoreId, for uniqueness)
```

### Shared Core Implementation

The core implementation is simply a refactoring of the existing private methods to accept a query factory instead of a table:

```csharp
// Before (current implementation)
private static async Task<SpatialQueryResponse<TEntity>> SpatialQueryRadiusNonPaginatedAsync<TEntity>(
    DynamoDbTableBase table,  // Tied to table
    // ... other params
)
{
    foreach (var cellValue in cells)
    {
        var query = table.Query<TEntity>();  // Creates query from table
        // ...
    }
}

// After (refactored)
private static async Task<SpatialQueryResponse<TEntity>> SpatialQueryRadiusNonPaginatedAsync<TEntity>(
    Func<QueryRequestBuilder<TEntity>> createQuery,  // Factory function
    // ... other params
)
{
    foreach (var cellValue in cells)
    {
        var query = createQuery();  // Creates query from factory
        // ...
    }
}
```

The public extension methods become thin wrappers:

```csharp
// Table extension
public static Task<SpatialQueryResponse<TEntity>> SpatialQueryAsync<TEntity>(
    this DynamoDbTableBase table, ...)
{
    return SpatialQueryRadiusNonPaginatedAsync(
        () => table.Query<TEntity>(),  // Pass factory
        ...);
}

// Index extension
public static Task<SpatialQueryResponse<TEntity>> SpatialQueryAsync<TEntity>(
    this DynamoDbIndex index, ...)
{
    return SpatialQueryRadiusNonPaginatedAsync(
        () => index.Query<TEntity>(),  // Pass factory
        ...);
}
```

**Benefits:**

1. **Minimal Change**: Just change parameter type from `DynamoDbTableBase` to `Func<QueryRequestBuilder<TEntity>>`
2. **No New Types**: No interfaces or delegate types needed
3. **Same Logic**: All existing pagination, spiral ordering, and filtering logic stays the same
4. **Easy to Test**: Can pass mock query factories for unit testing

## Dateline and Pole Handling

### International Date Line (±180° Longitude)

The International Date Line presents a challenge because longitude wraps from +180° to -180°. A bounding box that crosses the date line (e.g., from 170°E to -170°E) cannot be represented with the simple constraint `west <= east`.

#### Solution: Split Query Approach

When a bounding box crosses the date line, we split it into two separate bounding boxes:

**Original (crosses dateline):**
- Southwest: (lat, 170°)
- Northeast: (lat, -170°)

**Split into two:**
1. **Western box**: Southwest: (lat, 170°), Northeast: (lat, 180°)
2. **Eastern box**: Southwest: (lat, -180°), Northeast: (lat, -170°)

**Algorithm:**
```csharp
public static bool CrossesDateLine(GeoBoundingBox bbox)
{
    return bbox.Southwest.Longitude > bbox.Northeast.Longitude;
}

public static (GeoBoundingBox western, GeoBoundingBox eastern) SplitAtDateLine(GeoBoundingBox bbox)
{
    var western = new GeoBoundingBox(
        new GeoLocation(bbox.Southwest.Latitude, bbox.Southwest.Longitude),
        new GeoLocation(bbox.Northeast.Latitude, 180.0));
    
    var eastern = new GeoBoundingBox(
        new GeoLocation(bbox.Southwest.Latitude, -180.0),
        new GeoLocation(bbox.Northeast.Latitude, bbox.Northeast.Longitude));
    
    return (western, eastern);
}
```

**Query Execution:**
1. Detect if bounding box crosses date line
2. If yes, split into two bounding boxes
3. Compute cell coverings for both boxes
4. Execute queries for all cells (deduplicate cells that appear in both)
5. Merge and deduplicate results by primary key

**Cell Deduplication:**
Cells near the date line may appear in both coverings. We use a `HashSet<string>` to track unique cell tokens/indices before querying.

### Polar Regions (±90° Latitude)

Near the poles, longitude becomes increasingly meaningless as all meridians converge. At exactly ±90°, longitude is undefined.

#### Challenges:

1. **Longitude Convergence**: At high latitudes, a small change in longitude represents a very small distance
2. **Bounding Box Distortion**: A "square" bounding box in lat/lon coordinates becomes highly distorted near poles
3. **Cell Behavior**: S2 and H3 cells behave differently near poles due to their projection methods

#### Solution: Pole-Aware Bounding Box Creation

When creating a bounding box from a center point and radius near the poles:

```csharp
public static GeoBoundingBox FromCenterAndDistanceMeters(GeoLocation center, double distanceMeters)
{
    const double metersPerDegreeLat = 111320.0;
    
    // Calculate latitude offset
    var latOffset = distanceMeters / metersPerDegreeLat;
    
    // Calculate longitude offset (varies by latitude)
    var metersPerDegreeLon = metersPerDegreeLat * Math.Cos(DegreesToRadians(center.Latitude));
    
    // Near poles, longitude offset becomes very large or infinite
    // Clamp to reasonable values
    var lonOffset = metersPerDegreeLon > 0 ? distanceMeters / metersPerDegreeLon : 180.0;
    
    // Clamp longitude offset to prevent wrapping past ±180
    lonOffset = Math.Min(lonOffset, 180.0);
    
    // Calculate corners with clamping
    var swLat = Math.Max(-90, center.Latitude - latOffset);
    var swLon = Math.Max(-180, center.Longitude - lonOffset);
    var neLat = Math.Min(90, center.Latitude + latOffset);
    var neLon = Math.Min(180, center.Longitude + lonOffset);
    
    // Special case: if we're at a pole or the search radius covers a pole
    if (neLat >= 90 || swLat <= -90)
    {
        // At the pole, longitude is meaningless - use full longitude range
        swLon = -180;
        neLon = 180;
    }
    
    // Check if longitude wraps around (crosses date line)
    if (swLon > neLon)
    {
        // This will be handled by the date line splitting logic
    }
    
    return new GeoBoundingBox(
        new GeoLocation(swLat, swLon),
        new GeoLocation(neLat, neLon));
}
```

#### Solution: Pole-Aware Cell Covering

When computing cell coverings near poles:

1. **Detect Polar Queries**: Check if center latitude is > 85° or < -85°, or if bounding box extends beyond these thresholds
2. **Use Lower Precision**: Near poles, use lower precision levels to avoid excessive cell counts due to longitude convergence
3. **Full Longitude Range**: If the bounding box touches a pole, expand longitude to full range (-180 to 180)
4. **Deduplicate Cells**: Cells near poles may be duplicated due to longitude wrapping

```csharp
private static bool IsNearPole(GeoLocation location, double thresholdLatitude = 85.0)
{
    return Math.Abs(location.Latitude) > thresholdLatitude;
}

private static bool BoundingBoxIncludesPole(GeoBoundingBox bbox)
{
    return bbox.Northeast.Latitude >= 90 || bbox.Southwest.Latitude <= -90;
}
```

### Combined Dateline and Pole Handling

When a query involves both the date line and polar regions:

1. **Check for pole inclusion first**: If bounding box includes a pole, expand longitude to full range
2. **Check for date line crossing**: If longitude range is not full and crosses date line, split the query
3. **Compute cell coverings**: For each resulting bounding box
4. **Deduplicate cells**: Merge cell lists and remove duplicates
5. **Execute queries**: Query all unique cells
6. **Deduplicate results**: Merge results and remove duplicates by primary key

### GeoBoundingBox Constructor Update

The current constructor throws an exception when `southwest.Longitude > northeast.Longitude`. This needs to be updated to allow date line crossing:

```csharp
public GeoBoundingBox(GeoLocation southwest, GeoLocation northeast)
{
    if (southwest.Latitude > northeast.Latitude)
    {
        throw new ArgumentException(
            "Southwest corner latitude must be less than or equal to northeast corner latitude",
            nameof(southwest));
    }

    // Allow longitude wrapping for date line crossing
    // We'll detect and handle this in the query logic
    // No validation needed here - any longitude combination is valid
    
    Southwest = southwest;
    Northeast = northeast;
}
```

### Testing Strategy for Edge Cases

**Unit Tests:**
- Bounding box creation at various latitudes (0°, 45°, 85°, 89°, 90°)
- Bounding box creation with various longitudes (0°, 90°, 170°, 179°, -179°, -170°)
- Date line crossing detection
- Bounding box splitting at date line
- Cell covering computation near date line
- Cell covering computation near poles

**Property-Based Tests:**
- For any location and radius, bounding box should contain the center point
- For any bounding box crossing date line, split boxes should cover the same area
- For any cell covering, all cells should be unique
- For any query result, all items should be within the specified radius (post-filtering)

**Integration Tests:**
- Query with center at (0°, 179°) and 200km radius (crosses date line)
- Query with center at (89°, 0°) and 200km radius (near North Pole)
- Query with center at (-89°, 0°) and 200km radius (near South Pole)
- Query with center at (89°, 179°) and 200km radius (both date line and pole)

## Data Models

### Spatial Index Configuration

The source generator will extract spatial index configuration from attributes:

```csharp
internal class SpatialIndexConfig
{
    public SpatialIndexType IndexType { get; set; }
    public int Precision { get; set; }  // GeoHash precision, S2 level, or H3 resolution
    public string AttributeName { get; set; }
    
    // Optional coordinate storage (from StoreCoordinatesAttribute)
    public string? LatitudeAttributeName { get; set; }
    public string? LongitudeAttributeName { get; set; }
    public bool HasCoordinateStorage => LatitudeAttributeName != null && LongitudeAttributeName != null;
}
```

### Default Precision Levels

The system will use the following defaults when precision is not specified:

| Index Type | Default Precision | Cell Size | Rationale |
|------------|------------------|-----------|-----------|
| GeoHash | 6 | ~610m × 610m | Existing default, good for neighborhood queries |
| S2 | 16 | ~600m × 600m | Similar to GeoHash 6, balanced accuracy |
| H3 | 9 | ~174m edge length | Slightly more precise, hexagonal uniformity |

## Data Models

### Serialization Modes

#### Single-Field Mode (Default)

When no coordinate storage is configured:

```json
{
  "location": "9q8yy9r"  // Just the spatial index
}
```

Deserialization returns the center point of the spatial index cell.

#### Multi-Field Mode with Separate Properties

When using separate properties for coordinates:

```csharp
[DynamoDbAttribute("location", SpatialIndexType = SpatialIndexType.S2)]
public GeoLocation Location { get; set; }

[DynamoDbAttribute("lat")]
public double Latitude => Location.Latitude;

[DynamoDbAttribute("lon")]
public double Longitude => Location.Longitude;
```

Serialized as:

```json
{
  "location": "89c25985",     // S2 token for queries
  "lat": 37.7749,             // Full precision latitude
  "lon": -122.4194            // Full precision longitude
}
```

During deserialization:
1. Read the spatial index value from the `location` attribute
2. If `lat` and `lon` exist, reconstruct `GeoLocation` from them (exact coordinates) **and include the spatial index**
3. If only `location` exists, decode from spatial index (cell center) **and include the spatial index**
4. The source generator handles this automatically based on property configuration

**Generated Deserialization Code (Single-Field Mode):**
```csharp
// Read and preserve the spatial index
var s2Token = item["location"].S;
var (lat, lon) = S2Encoder.Decode(s2Token);
entity.Location = new GeoLocation(lat, lon, s2Token); // Spatial index preserved!
```

**Generated Deserialization Code (Multi-Field Mode):**
```csharp
// Read spatial index and coordinates
var s2Token = item["location"].S;
var lat = double.Parse(item["lat"].N);
var lon = double.Parse(item["lon"].N);
entity.Location = new GeoLocation(lat, lon, s2Token); // Exact coords + spatial index!
```

This ensures that `GeoLocation.SpatialIndex` is always populated when deserializing from DynamoDB, enabling efficient query comparisons without recalculation.

#### Multi-Field Mode with StoreCoordinatesAttribute

When using the explicit attribute:

```csharp
[DynamoDbAttribute("location_hash", SpatialIndexType = SpatialIndexType.H3)]
[StoreCoordinates(LatitudeAttributeName = "location_lat", LongitudeAttributeName = "location_lon")]
public GeoLocation Location { get; set; }
```

Serialized as:

```json
{
  "location_hash": "8928308280fffff",  // H3 index for queries
  "location_lat": 37.7749,             // Full precision latitude
  "location_lon": -122.4194            // Full precision longitude
}
```

The source generator creates both serialization and deserialization code that handles all three fields atomically.

## Correctn
ess Properties

*A property is a characteristic or behavior that should hold true across all valid executions of a system-essentially, a formal statement about what the system should do. Properties serve as the bridge between human-readable specifications and machine-verifiable correctness guarantees.*

Property 1: S2 encoding produces valid cell tokens
*For any* valid GeoLocation and S2 level (0-30), encoding the location to an S2 cell token should produce a valid S2 token that can be decoded back to coordinates
**Validates: Requirements 1.2**

Property 2: H3 encoding produces valid cell indices
*For any* valid GeoLocation and H3 resolution (0-15), encoding the location to an H3 cell index should produce a valid H3 index that can be decoded back to coordinates
**Validates: Requirements 1.3**

Property 3: Non-paginated queries execute all cells in parallel
*For any* spatial query with pageSize = null, all cell queries should execute in parallel using Task.WhenAll
**Validates: Requirements 3.1**

Property 4: Paginated queries execute cells sequentially in spiral order
*For any* spatial query with pageSize > 0, cells should be queried sequentially starting from the center cell and moving outward
**Validates: Requirements 3.2**

Property 5: S2 cell covering is sorted by distance from center
*For any* S2-indexed proximity query, the cell covering should be sorted by distance from the search center (spiral order)
**Validates: Requirements 3.3**

Property 6: H3 cell covering is sorted by distance from center
*For any* H3-indexed proximity query, the cell covering should be sorted by distance from the search center (spiral order)
**Validates: Requirements 3.4**

Property 7: GeoHash queries execute single BETWEEN query
*For any* GeoHash-indexed property and proximity query, SpatialQueryAsync should compute the GeoHash range and execute a single BETWEEN query
**Validates: Requirements 3.5**

Property 8: Query builder lambda receives correct cell value
*For any* spatial query, the query builder lambda should receive the appropriate cell value (GeoHash/S2 token/H3 index) for each cell being queried
**Validates: Requirements 12.2**

Property 9: Spatial query results are deduplicated by primary key
*For any* non-paginated spatial query that returns items from multiple cells, duplicate items should be removed based on primary key
**Validates: Requirements 3.1**

Property 10: S2 bounding box queries compute correct cell coverings
*For any* bounding box and S2 level, the S2 cell covering should include all locations within the bounding box
**Validates: Requirements 4.1**

Property 11: H3 bounding box queries compute correct cell coverings
*For any* bounding box and H3 resolution, the H3 cell covering should include all locations within the bounding box
**Validates: Requirements 4.2**

Property 12: Large bounding boxes are limited to prevent excessive queries
*For any* very large bounding box, the system should limit the number of cells in the covering to a reasonable maximum
**Validates: Requirements 4.4**

Property 13: Cell coverings use configured precision
*For any* GeoLocation property with configured precision, cell coverings should use that precision level
**Validates: Requirements 4.5**

Property 22: Pagination limits results to page size
*For any* spatial query with a page size, the response should contain at most that many items
**Validates: Requirements 11.1**

Property 23: Continuation token contains cell index and LastEvaluatedKey
*For any* paginated spatial query that stops mid-cell, the continuation token should contain both the cell index and DynamoDB's LastEvaluatedKey
**Validates: Requirements 11.2**

Property 24: Continuation token enables resumption from correct position
*For any* spatial query with a continuation token, the query should resume from the exact cell and key position indicated by the token
**Validates: Requirements 11.3, 11.4**

Property 25: Completed queries return null continuation token
*For any* spatial query where all cells are fully processed, the continuation token should be null
**Validates: Requirements 11.5**

Property 26: Query builder lambda receives all required parameters
*For any* spatial query, the query builder lambda should receive the query builder, cell value, and pagination configuration
**Validates: Requirements 12.1, 12.2**

Property 27: Dateline crossing is detected correctly
*For any* bounding box where southwest longitude > northeast longitude, the system should detect it as crossing the International Date Line
**Validates: Requirements 13.1**

Property 28: Dateline-crossing bounding boxes are split correctly
*For any* bounding box that crosses the date line, splitting it should produce two bounding boxes that together cover the same area without overlap
**Validates: Requirements 13.1**

Property 29: Dateline queries deduplicate cells
*For any* query that crosses the date line, cells that appear in both western and eastern coverings should be deduplicated before querying
**Validates: Requirements 13.2, 13.5**

Property 30: Polar bounding boxes clamp latitude correctly
*For any* bounding box that would extend beyond ±90° latitude, the latitude should be clamped to valid ranges
**Validates: Requirements 13.3**

Property 31: Polar queries handle longitude convergence
*For any* query centered at latitude > 85° or < -85°, the cell covering should account for longitude convergence and avoid excessive cell counts
**Validates: Requirements 13.4**

Property 32: Query results are deduplicated by primary key
*For any* spatial query (including dateline and polar queries), duplicate items should be removed based on primary key before returning results
**Validates: Requirements 13.5**

Property 13: Single-field serialization round-trip preserves cell
*For any* GeoLocation serialized in single-field mode, deserializing should return a location within the same spatial index cell, and the SpatialIndex property should contain the original spatial index value
**Validates: Requirements 5.4, 5.5**

Property 33: Deserialized GeoLocation preserves spatial index
*For any* GeoLocation deserialized from DynamoDB, the SpatialIndex property should contain the original spatial index value (GeoHash/S2 token/H3 index) from the database
**Validates: Requirements 5.4, 5.5, 12.1, 12.2**

Property 34: GSI queries produce same results as table queries
*For any* spatial query, executing via DynamoDbIndex should produce the same results as executing via DynamoDbTableBase when querying the same data
**Validates: Requirements 3.1, 3.2**

Property 35: Custom cell list queries use provided cells
*For any* spatial query with a custom cell list, the query should use exactly the provided cells without computing a cell covering
**Validates: Requirements 3.1, 3.2**

Property 36: Custom cell list with center sorts by distance
*For any* spatial query with a custom cell list and a center point, results should be sorted by distance from the center
**Validates: Requirements 3.1, 3.2**

Property 37: Shared core produces consistent results
*For any* spatial query, the shared core implementation should produce identical results regardless of whether called from table or index extensions
**Validates: Requirements 3.1, 3.2**

Property 14: Coordinate storage creates separate attributes
*For any* GeoLocation with StoreCoordinatesAttribute, the serialized data should contain the spatial index attribute plus separate latitude and longitude attributes
**Validates: Requirements 6.1, 6.2**

Property 15: Coordinate deserialization preserves exact values
*For any* GeoLocation serialized with coordinate storage, deserializing should return the exact original coordinates, not the cell center
**Validates: Requirements 6.3**

Property 16: Single-field mode stores only spatial index
*For any* GeoLocation without coordinate storage, the serialized data should contain only one attribute: the spatial index
**Validates: Requirements 6.4**

Property 17: ToS2Cell returns valid S2Cell
*For any* GeoLocation and S2 level, calling ToS2Cell should return an S2Cell with a valid token at the specified level
**Validates: Requirements 8.1**

Property 18: ToH3Cell returns valid H3Cell
*For any* GeoLocation and H3 resolution, calling ToH3Cell should return an H3Cell with a valid index at the specified resolution
**Validates: Requirements 8.2**

Property 19: GetNeighbors returns correct count and level
*For any* S2Cell or H3Cell, calling GetNeighbors should return all adjacent cells (8 for S2, 6 for H3) at the same precision level
**Validates: Requirements 8.3**

Property 20: GetParent returns cell at lower precision
*For any* S2Cell or H3Cell with precision > 0, calling GetParent should return a cell at precision - 1
**Validates: Requirements 8.4**

Property 21: GetChildren returns correct count and level
*For any* S2Cell or H3Cell below maximum precision, calling GetChildren should return all child cells (4 for S2, 7 for H3) at precision + 1
**Validates: Requirements 8.5**

## Error Handling

### Validation Errors

The system will validate spatial index configuration at multiple levels:

1. **Attribute Level**: The `DynamoDbAttributeAttribute` will validate precision ranges when properties are set:
   - S2Level: 0-30 (0 means use default 16)
   - H3Resolution: 0-15 (0 means use default 9)
   - GeoHashPrecision: 0-12 (0 means use default 6)

2. **Source Generator Level**: The source generator will produce compile-time diagnostics for:
   - Invalid spatial index type configurations
   - Out-of-range precision values
   - Conflicting configuration (e.g., specifying both S2Level and H3Resolution)
   - Missing required dependencies (S2/H3 libraries not referenced)

3. **Runtime Level**: The encoders will validate inputs:
   - Invalid latitude/longitude ranges
   - Invalid spatial index tokens/indices during decoding
   - Null or empty strings

### Error Messages

All error messages will be clear and actionable:

```csharp
// Attribute validation
throw new ArgumentOutOfRangeException(
    nameof(S2Level),
    value,
    "S2 level must be between 0 (default) and 30. " +
    "Common values: 10 (city), 16 (neighborhood), 20 (building)");

// Source generator diagnostic
context.ReportDiagnostic(Diagnostic.Create(
    new DiagnosticDescriptor(
        "FDB001",
        "Invalid spatial index configuration",
        "Property '{0}' specifies both S2Level and H3Resolution. " +
        "Only one spatial index type can be configured per property.",
        "FluentDynamoDb.Geospatial",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true),
    location,
    propertyName));

// Runtime validation
throw new ArgumentException(
    $"Invalid S2 cell token '{token}'. S2 tokens must be 16-character hexadecimal strings.",
    nameof(token));
```

### Graceful Degradation

When coordinate storage is configured but only the spatial index field exists in the data:

1. The system will fall back to decoding from the spatial index
2. The returned GeoLocation will be the cell center, not exact coordinates
3. No error is thrown - this allows reading data written before coordinate storage was added

## Testing Strategy

### Unit Testing

Unit tests will verify specific behaviors and edge cases:

1. **Encoder Tests**:
   - Valid encoding/decoding for known locations
   - Boundary conditions (poles, date line, equator)
   - Invalid input handling
   - Precision level validation

2. **Cell Structure Tests**:
   - Cell creation from tokens/indices
   - Neighbor calculation correctness
   - Parent/child relationships
   - Boundary cases (minimum/maximum precision)

3. **Serialization Tests**:
   - Single-field mode serialization/deserialization
   - Coordinate storage with separate properties
   - Coordinate storage with StoreCoordinatesAttribute
   - Fallback when coordinates are missing

4. **Source Generator Tests**:
   - Code generation for each spatial index type
   - Diagnostic generation for invalid configurations
   - Precision level handling
   - Coordinate storage code generation (separate properties and StoreCoordinatesAttribute)

5. **Expression Translator Tests**:
   - Query expression generation for each index type
   - Distance unit conversions
   - Bounding box query translation
   - Parameter value generation

### Property-Based Testing

Property-based tests will verify universal properties across many inputs using **FsCheck** (the existing PBT library for .NET):

**Configuration**: Each property-based test will run a minimum of 100 iterations to ensure thorough coverage of the random input space.

**Test Tagging**: Each property-based test will be tagged with a comment explicitly referencing the correctness property from this design document using the format: `// Feature: s2-h3-geospatial-support, Property {number}: {property_text}`

1. **Encoding Round-Trip Properties**:
   - Property 1: S2 encoding/decoding round-trip
   - Property 2: H3 encoding/decoding round-trip
   - Property 12: Single-field serialization round-trip

2. **Coordinate Storage Properties**:
   - Property 13: Separate attributes created
   - Property 14: Exact coordinate preservation
   - Property 15: Single attribute in single-field mode

3. **Query Translation Properties**:
   - Property 3: S2 BETWEEN query generation
   - Property 4: H3 BETWEEN query generation
   - Property 5: Distance unit equivalence
   - Property 6: Index type identification
   - Property 7: DynamoDB-compatible parameters

4. **Bounding Box Properties**:
   - Property 8: S2 cell covering completeness
   - Property 9: H3 cell covering completeness
   - Property 10: Large bounding box limits
   - Property 11: Configured precision usage

5. **Cell Operation Properties**:
   - Property 17: ToS2Cell validity
   - Property 18: ToH3Cell validity
   - Property 19: Neighbor count and level
   - Property 20: Parent level correctness
   - Property 21: Children count and level

### Integration Testing

Integration tests will verify end-to-end functionality with actual DynamoDB:

1. **Query Execution Tests**:
   - Proximity queries with each spatial index type
   - Bounding box queries with each spatial index type
   - Multi-field serialization with real DynamoDB operations
   - Query result accuracy (post-filtering verification)

2. **Performance Tests**:
   - Query performance comparison between index types
   - Serialization/deserialization performance
   - Large dataset query efficiency

3. **Compatibility Tests**:
   - Reading data with different spatial index types
   - Reading single-field data when coordinate storage is configured
   - Backward compatibility with existing GeoHash data

## Implementation Notes

### Zero External Dependencies Approach

Following the pattern established with GeoHash, we will implement S2 and H3 encoders **without external dependencies**. This maintains consistency with the library's philosophy and avoids dependency bloat.

#### S2 Implementation Complexity

**Feasibility**: Moderate - Implementing a minimal S2 encoder is achievable

**What We Need**:
1. **Hilbert Curve Mapping**: Convert lat/lon to a point on the unit sphere, then to a Hilbert curve position
2. **Cell ID Encoding**: Encode the Hilbert curve position as a 64-bit cell ID
3. **Token Generation**: Convert cell ID to a hexadecimal token string
4. **Level Handling**: Support 30 levels of precision (0-30)
5. **Neighbor Calculation**: Compute adjacent cells using bit manipulation

**Complexity Estimate**: ~500-800 lines of code for core functionality

**Key Algorithms**:
- Spherical coordinate to cube face projection (6 faces)
- UV coordinate transformation on each face
- Hilbert curve encoding/decoding
- Bit manipulation for cell IDs and neighbors

**Reference**: Google's S2 library is open source (Apache 2.0), so we can reference the algorithms without taking a dependency

#### H3 Implementation Complexity

**Feasibility**: Moderate to High - H3 is more complex due to hexagonal geometry

**What We Need**:
1. **Icosahedron Face Selection**: Map lat/lon to one of 20 icosahedron faces
2. **Hexagonal Grid Coordinates**: Convert to hexagonal grid coordinates on the face
3. **Index Encoding**: Encode face + coordinates as a 64-bit H3 index
4. **Resolution Handling**: Support 16 levels of resolution (0-15)
5. **Neighbor Calculation**: Compute 6 hexagonal neighbors (more complex than square grids)

**Complexity Estimate**: ~800-1200 lines of code for core functionality

**Key Algorithms**:
- Icosahedron face projection
- Hexagonal coordinate systems (axial or cube coordinates)
- Aperture 7 hexagonal grid hierarchy
- Hexagonal neighbor traversal
- Pentagon handling (12 pentagons at each resolution)

**Reference**: Uber's H3 library is open source (Apache 2.0), so we can reference the algorithms

#### Implementation Strategy

**Phase 1: S2 Implementation**
- Implement core S2 encoder with levels 0-30
- Focus on encoding, decoding, and neighbor calculation
- Skip advanced features (polygon covering, edge calculations)
- Estimated effort: 2-3 days

**Phase 2: H3 Implementation**
- Implement core H3 encoder with resolutions 0-15
- Focus on encoding, decoding, and neighbor calculation
- Handle pentagon edge cases
- Estimated effort: 3-5 days

**Phase 3: Optimization**
- Performance optimization (lookup tables, caching)
- Comprehensive testing against reference implementations
- Estimated effort: 2-3 days

#### Trade-offs

**Pros of Zero Dependencies**:
- Consistent with GeoHash implementation
- No external dependency management
- Full control over implementation
- Smaller package size
- No licensing concerns (we implement from scratch using public algorithms)

**Cons of Zero Dependencies**:
- More implementation effort
- Need to maintain our own implementation
- May not have all advanced features of full libraries
- Need thorough testing to ensure correctness

#### Validation Strategy

To ensure correctness without external dependencies:

1. **Test Against Known Values**: Use published test vectors from S2/H3 documentation
2. **Cross-Validation**: Compare our results with online S2/H3 converters
3. **Property-Based Testing**: Verify mathematical properties (e.g., neighbor relationships, parent-child consistency)
4. **Reference Implementation Comparison**: During development, compare against S2Geometry/H3.NET to validate algorithms

#### Minimal Feature Set

We will implement only the features needed for DynamoDB spatial queries:

**S2 Features**:
- ✅ Encode lat/lon to S2 cell token
- ✅ Decode S2 cell token to lat/lon (center point)
- ✅ Get cell bounds (bounding box)
- ✅ Get 8 neighbors
- ✅ Get parent cell
- ✅ Get 4 child cells
- ❌ Advanced: Polygon covering, edge calculations, distance on sphere

**H3 Features**:
- ✅ Encode lat/lon to H3 cell index
- ✅ Decode H3 cell index to lat/lon (center point)
- ✅ Get cell bounds (hexagon vertices)
- ✅ Get 6 neighbors (with pentagon handling)
- ✅ Get parent cell
- ✅ Get 7 child cells
- ❌ Advanced: K-ring, polygon filling, grid distance

This minimal feature set is sufficient for all DynamoDB spatial query use cases while keeping implementation complexity manageable.

### Performance Considerations

1. **Encoding Performance**: S2 and H3 encoding is typically faster than GeoHash due to more efficient algorithms
2. **Query Performance**: H3 hexagonal cells provide better coverage uniformity, potentially reducing false positives
3. **Storage**: All three index types use similar storage (strings), so storage costs are comparable
4. **Memory**: Cell structures are readonly structs to minimize heap allocations

### Backward Compatibility

The design maintains full backward compatibility:

1. Existing GeoHash-based code continues to work unchanged
2. Default spatial index type is GeoHash
3. No breaking changes to existing APIs
4. New functionality is additive only
5. Existing data can be read with new spatial index types (returns cell center)

## Performance Characteristics

### Query Mode Comparison

| Mode | Cell Execution | Latency | Memory | Use Case |
|------|---------------|---------|--------|----------|
| **Non-Paginated** | All cells in parallel | Lowest (1x cell query time) | High (all results) | Small result sets, need all data |
| **Paginated** | Sequential, spiral order | Higher (N× cell query time) | Low (one page) | Large result sets, infinite scroll |

### Cell Count Impact and Query Explosion

The number of cells in a covering depends on the ratio of search radius to cell size. This can **explode quickly** with poor precision choices.

#### Approximate Cell Count Formula

```
cellCount ≈ π × (radius / cellSize)²
```

This is an approximation because:
- Cells aren't perfect squares/hexagons
- Earth's curvature affects cell sizes
- Cell covering algorithms may include extra cells for complete coverage

#### S2 Cell Sizes by Level

| Level | Cell Size (approx) | 1km radius | 5km radius | 10km radius | 50km radius |
|-------|-------------------|------------|------------|-------------|-------------|
| 10 | ~100km | 1 cell | 1 cell | 1 cell | 1 cell |
| 12 | ~25km | 1 cell | 1 cell | 2 cells | 16 cells |
| 14 | ~6km | 1 cell | 3 cells | 11 cells | 275 cells |
| 16 | ~1.5km | 2 cells | 44 cells | 175 cells | **4,400 cells** ⚠️ |
| 18 | ~400m | 25 cells | 625 cells | **2,500 cells** ⚠️ | **62,500 cells** 🚫 |
| 20 | ~100m | 400 cells | **10,000 cells** 🚫 | **40,000 cells** 🚫 | **1M cells** 🚫 |

#### H3 Cell Sizes by Resolution

| Resolution | Cell Edge (approx) | 1km radius | 5km radius | 10km radius | 50km radius |
|------------|-------------------|------------|------------|-------------|-------------|
| 5 | ~8.5km | 1 cell | 2 cells | 6 cells | 140 cells |
| 6 | ~3.2km | 3 cells | 10 cells | 40 cells | **1,000 cells** ⚠️ |
| 7 | ~1.2km | 3 cells | 70 cells | 280 cells | **7,000 cells** 🚫 |
| 8 | ~460m | 20 cells | 480 cells | **1,900 cells** ⚠️ | **48,000 cells** 🚫 |
| 9 | ~174m | 130 cells | **3,300 cells** 🚫 | **13,000 cells** 🚫 | **325,000 cells** 🚫 |
| 10 | ~66m | 920 cells | **23,000 cells** 🚫 | **92,000 cells** 🚫 | **2.3M cells** 🚫 |

**Legend:**
- ✅ Good: < 100 cells
- ⚠️ Warning: 100-1000 cells (will hit maxCells limit, may be slow)
- 🚫 Bad: > 1000 cells (will definitely hit maxCells limit, very slow)

#### Query Explosion Examples

**Bad Example 1: Too much precision for large radius**
```csharp
// ❌ BAD: 50km radius with S2 Level 16 (~1.5km cells)
// Result: ~4,400 cells needed, will hit maxCells limit (100)
// Only covers ~2.3% of the area!
[DynamoDbAttribute("location", SpatialIndexType = SpatialIndexType.S2, S2Level = 16)]
public GeoLocation Location { get; set; }

var result = await table.SpatialQueryAsync(
    center: center,
    radiusKilometers: 50,  // Too large for this precision!
    ...
);
```

**Good Example 1: Appropriate precision for radius**
```csharp
// ✅ GOOD: 50km radius with S2 Level 12 (~25km cells)
// Result: ~16 cells, fast and complete coverage
[DynamoDbAttribute("location", SpatialIndexType = SpatialIndexType.S2, S2Level = 12)]
public GeoLocation Location { get; set; }

var result = await table.SpatialQueryAsync(
    center: center,
    radiusKilometers: 50,
    ...
);
```

**Bad Example 2: Too much precision for any reasonable radius**
```csharp
// ❌ BAD: H3 Resolution 10 (~66m cells) with 5km radius
// Result: ~23,000 cells needed, will hit maxCells limit
// Paginated queries will be extremely slow
[DynamoDbAttribute("location", SpatialIndexType = SpatialIndexType.H3, H3Resolution = 10)]
public GeoLocation Location { get; set; }
```

**Good Example 2: Balanced precision**
```csharp
// ✅ GOOD: H3 Resolution 7 (~1.2km cells) with 5km radius
// Result: ~70 cells, good balance of accuracy and performance
[DynamoDbAttribute("location", SpatialIndexType = SpatialIndexType.H3, H3Resolution = 7)]
public GeoLocation Location { get; set; }
```

### Pagination Performance

**Non-Paginated Mode:**
- All cells query in parallel
- Latency = single cell query time (~50ms)
- Total time: ~50ms regardless of cell count

**Paginated Mode:**
- Cells query sequentially in spiral order
- Latency = N × cell query time
- Total time for first page: ~50-200ms (1-4 cells typically)
- Total time for all pages: ~50ms × number of cells

**Why Sequential is Acceptable:**
1. **Spiral ordering**: Most users only see first page (closest results)
2. **Early termination**: If user stops at page 2, we only queried 2-4 cells
3. **Predictable**: Consistent latency per page
4. **Memory efficient**: Only one page in memory at a time

### Precision Selection Guide

Use this guide to choose the right precision/resolution for your use case:

#### By Use Case

| Use Case | Typical Radius | Recommended S2 Level | Recommended H3 Resolution |
|----------|---------------|---------------------|--------------------------|
| City-wide search | 20-50km | 12-14 | 5-6 |
| Neighborhood search | 5-10km | 14-16 | 6-7 |
| Local search | 1-5km | 16-18 | 7-8 |
| Precise tracking | 100m-1km | 18-20 | 8-9 |
| Asset tracking | < 100m | 20-22 | 9-10 |

#### Decision Matrix

**Step 1: Determine your maximum search radius**
- What's the largest radius users will search?
- Example: "Find stores within 10km"

**Step 2: Calculate cell count for different precisions**
```
cellCount ≈ π × (radius / cellSize)²
```

**Step 3: Choose precision where cellCount < 100**
- Target: 20-50 cells for optimal performance
- Maximum: 100 cells (maxCells limit)
- Avoid: > 100 cells (will be truncated, incomplete coverage)

**Example Calculation:**
```
Radius: 10km
S2 Level 14 (~6km cells): π × (10/6)² ≈ 11 cells ✅
S2 Level 16 (~1.5km cells): π × (10/1.5)² ≈ 175 cells ⚠️ (will hit limit)
S2 Level 18 (~400m cells): π × (10/0.4)² ≈ 2,500 cells 🚫 (way over limit)

Recommendation: Use S2 Level 14
```

### Optimization Tips

1. **Choose appropriate precision**: Use the decision matrix above - higher precision = more cells = slower paginated queries
2. **Use non-paginated for small areas**: If you know the result set is small (<100 items), skip pagination
3. **Limit search radius**: Smaller radius = fewer cells = faster queries
4. **Consider precision vs radius ratio**: 
   - Good: 5km radius with 1.5km cells (~44 cells)
   - Bad: 50km radius with 1.5km cells (~4,400 cells - will hit maxCells limit)
5. **Adjust maxCells if needed**: Default is 100, but you can increase it for non-paginated queries (at the cost of more parallel queries)
6. **Monitor cell counts**: Log `TotalCellsQueried` in production to identify problematic queries
7. **Consider multiple precision levels**: Store data at multiple precisions for different query types (e.g., Level 12 for city-wide, Level 16 for local)

## Usage Examples

### Basic Proximity Query with S2 (Non-Paginated)

```csharp
[DynamoDbTable("stores")]
public partial class Store
{
    [PartitionKey]
    [DynamoDbAttribute("pk")]
    public string PartitionKey { get; set; }
    
    [SortKey]
    [DynamoDbAttribute("location", SpatialIndexType = SpatialIndexType.S2, S2Level = 16)]
    public GeoLocation Location { get; set; }
    
    [DynamoDbAttribute("name")]
    public string Name { get; set; }
}

// Find ALL stores within 5km (no pagination - fastest)
var center = new GeoLocation(37.7749, -122.4194);
var result = await storeTable.SpatialQueryAsync(
    spatialAttributeName: "location",
    center: center,
    radiusKilometers: 5,
    queryBuilder: (query, cell, pagination) => query
        .Where<Store>(x => x.PartitionKey == "STORE" && x.Location == cell)
        .Paginate(pagination),
    pageSize: null  // No pagination - query all cells in parallel
);

Console.WriteLine($"Found {result.Items.Count} stores");
Console.WriteLine($"Queried {result.TotalCellsQueried} cells in parallel");
// Results are sorted by distance from center
```

### Basic Proximity Query with S2 (Paginated)

```csharp
// Find stores within 5km, paginated (50 per page)
var center = new GeoLocation(37.7749, -122.4194);
var result = await storeTable.SpatialQueryAsync(
    spatialAttributeName: "location",
    center: center,
    radiusKilometers: 5,
    queryBuilder: (query, cell, pagination) => query
        .Where<Store>(x => x.PartitionKey == "STORE" && x.Location == cell)
        .Paginate(pagination),
    pageSize: 50  // Paginated - queries cells sequentially in spiral order
);

Console.WriteLine($"Found {result.Items.Count} stores (page 1)");
Console.WriteLine($"Queried {result.TotalCellsQueried} cells");
Console.WriteLine($"Has more: {result.ContinuationToken != null}");
// Results are roughly sorted by distance (spiral ordering)
```

### Paginated Query with H3

```csharp
var center = new GeoLocation(37.7749, -122.4194);
var allStores = new List<Store>();
SpatialContinuationToken? token = null;

do
{
    var result = await storeTable.SpatialQueryAsync(
        spatialAttributeName: "location",
        center: center,
        radiusKilometers: 10,
        queryBuilder: (query, cell, pagination) => query
            .Where<Store>(x => x.PartitionKey == "STORE" && x.Location == cell)
            .Paginate(pagination),
        pageSize: 100,
        continuationToken: token
    );
    
    allStores.AddRange(result.Items);
    token = result.ContinuationToken;
    
    Console.WriteLine($"Fetched {result.Items.Count} items, total so far: {allStores.Count}");
} while (token != null);

Console.WriteLine($"Total stores found: {allStores.Count}");
```

### Bounding Box Query with Additional Filters

```csharp
var southwest = new GeoLocation(37.7, -122.5);
var northeast = new GeoLocation(37.8, -122.4);
var bbox = new GeoBoundingBox(southwest, northeast);

var result = await storeTable.SpatialQueryAsync(
    spatialAttributeName: "location",
    boundingBox: bbox,
    queryBuilder: (query, cell, pagination) => query
        .Where<Store>(x => 
            x.PartitionKey == "STORE" && 
            x.Location == cell &&
            x.IsOpen == true)
        .Paginate(pagination),
    pageSize: 50
);

// Results are already filtered by IsOpen and within the bounding box
foreach (var store in result.Items)
{
    Console.WriteLine($"{store.Name}: {store.Location}");
}
```

### Using with GSI (Global Secondary Index)

```csharp
[DynamoDbTable("stores")]
public partial class Store
{
    [PartitionKey]
    [DynamoDbAttribute("pk")]
    public string StoreId { get; set; }
    
    [DynamoDbAttribute("gsi1pk")]
    public string Category { get; set; }
    
    [DynamoDbAttribute("gsi1sk", SpatialIndexType = SpatialIndexType.H3, H3Resolution = 9)]
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
```

### Serializing Continuation Token for API Responses

```csharp
// In your API controller
public async Task<IActionResult> GetNearbyStores(
    double lat, 
    double lon, 
    double radiusKm,
    string? continuationToken = null)
{
    var center = new GeoLocation(lat, lon);
    var token = continuationToken != null 
        ? SpatialContinuationToken.FromBase64(continuationToken)
        : null;
    
    var result = await storeTable.SpatialQueryAsync(
        spatialAttributeName: "location",
        center: center,
        radiusKilometers: radiusKm,
        queryBuilder: (query, cell, pagination) => query
            .Where<Store>(x => x.PartitionKey == "STORE" && x.Location == cell)
            .Paginate(pagination),
        pageSize: 20,
        continuationToken: token
    );
    
    return Ok(new
    {
        items = result.Items,
        nextToken = result.ContinuationToken?.ToBase64(),
        hasMore = result.ContinuationToken != null
    });
}
```

## Open Questions

1. **Cell Covering Algorithm**: For bounding boxes, should we implement optimal cell covering (complex) or use a simpler approximation (faster but may include more cells)?
   - **Recommendation**: Start with simple approximation (compute min/max cells at configured precision), optimize later if needed

2. **Maximum Cell Count**: What should be the maximum number of cells in a bounding box covering? Too low limits query area, too high impacts performance.
   - **Recommendation**: Limit to 100 cells per query, log warning if exceeded

3. **Computed Property Recognition**: Should the source generator automatically recognize computed properties (getters that reference other properties) or require explicit attributes?
   - **Recommendation**: Recognize computed properties automatically for convenience, but also support StoreCoordinatesAttribute for explicit control

4. **Pentagon Handling in H3**: H3 has 12 pentagons per resolution. Should we handle them specially or treat them as hexagons with one missing neighbor?
   - **Recommendation**: Detect pentagons and return 5 neighbors instead of 6, document the edge case

5. **Precision Validation**: Should we validate that precision levels make sense for the query area (e.g., warn if using level 30 for a 100km radius)?
   - **Recommendation**: Add optional validation with warnings, but allow developers to override

These questions will be resolved during implementation based on performance testing and user feedback.
