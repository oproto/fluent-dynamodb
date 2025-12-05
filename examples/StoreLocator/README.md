# StoreLocator Example

This example demonstrates geospatial queries with FluentDynamoDb, comparing three different spatial indexing approaches: GeoHash, S2, and H3.

## Project Structure

```
StoreLocator/
├── Data/
│   └── StoreSeedData.cs           # 52 predefined store locations in SF Bay Area
├── Entities/
│   ├── StoreGeoHash.cs            # Store entity with GeoHash spatial indexing
│   ├── StoreS2.cs                 # Store entity with S2 spatial indexing
│   ├── StoreH3.cs                 # Store entity with H3 spatial indexing
│   ├── StoresGeohashTable.cs      # Table class for GeoHash-indexed stores
│   ├── StoresS2Table.cs           # Table class for S2-indexed stores
│   └── StoresH3Table.cs           # Table class for H3-indexed stores
├── Program.cs                      # Interactive console application
├── README.md
└── StoreLocator.csproj
```

## Features Demonstrated

- **Geospatial Indexing**: Three different spatial index types for location-based queries
- **Adaptive Precision**: Automatic selection of precision level based on search radius
- **Index Comparison**: Side-by-side comparison of query efficiency across index types
- **Multi-Precision GSIs**: Using Global Secondary Indexes for different precision levels
- **Generated Entity Accessors**: Type-safe access via `table.Stores` property

## Spatial Index Types

### GeoHash
- Simple base-32 encoding using Z-order curve
- Good general-purpose choice with wide tool support
- Single precision level (7 = ~76m cells)

### S2 (Google)
- Spherical geometry using Hilbert curve
- Better area uniformity, especially near poles
- Multi-precision with GSIs (Level 14, 12, 10)
- Adaptive precision selection based on search radius

### H3 (Uber)
- Hexagonal hierarchical spatial index
- Uniform neighbor distances (6 equidistant neighbors)
- Better coverage with fewer cells for circular searches
- Multi-resolution with GSIs (Resolution 9, 7, 5)

## When to Use Each Index Type

| Index Type | Best For | Advantages | Considerations |
|------------|----------|------------|----------------|
| **GeoHash** | Simple proximity searches | Easy to understand, single query | Less efficient for large radii |
| **S2** | Global applications | Uniform cells worldwide, no pole distortion | More complex cell covering |
| **H3** | Ride-sharing, delivery | Hexagonal cells, uniform neighbors | Requires more storage for multi-resolution |

## Table Schema

All three entity types use the same key design pattern:

| Attribute | Type | Description |
|-----------|------|-------------|
| `pk` | String | Partition Key - StoreId (unique identifier) |
| `sk` | String | Sort Key - Category (e.g., "retail") |
| GSI PK | String | Spatial cell identifier for proximity queries |
| GSI SK | String | StoreId (for uniqueness within cell) |

### GeoHash Table (`stores-geohash`)
- Main Table: PK=`pk` (StoreId), SK=`sk` (Category)
- GSI `geohash-index`: PK=`geohash_cell`, SK=`pk`

### S2 Table (`stores-s2`)
- Main Table: PK=`pk` (StoreId), SK=`sk` (Category)
- GSI `s2-index-fine`: PK=`s2_cell_l14`, SK=`pk` (~284m cells)
- GSI `s2-index-medium`: PK=`s2_cell_l12`, SK=`pk` (~1.1km cells)
- GSI `s2-index-coarse`: PK=`s2_cell_l10`, SK=`pk` (~4.5km cells)

### H3 Table (`stores-h3`)
- Main Table: PK=`pk` (StoreId), SK=`sk` (Category)
- GSI `h3-index-fine`: PK=`h3_cell_r9`, SK=`pk` (~174m cells)
- GSI `h3-index-medium`: PK=`h3_cell_r7`, SK=`pk` (~1.2km cells)
- GSI `h3-index-coarse`: PK=`h3_cell_r5`, SK=`pk` (~8.5km cells)

## Entity Definition

Entities use `[DynamoDbTable]` with geospatial attributes for automatic spatial cell computation:

```csharp
[DynamoDbTable("stores-geohash", IsDefault = true)]
[GenerateEntityProperty(Name = "Stores")]
[Scannable]
public partial class StoreGeoHash
{
    // Main table partition key - unique store identifier
    [PartitionKey]
    [DynamoDbAttribute("pk")]
    public string StoreId { get; set; } = string.Empty;

    // Main table sort key - store category
    [SortKey]
    [DynamoDbAttribute("sk")]
    public string Category { get; set; } = "retail";

    // Location with GeoHash encoding - GSI partition key
    // GeoHashPrecision = 7 provides ~76m accuracy
    [GlobalSecondaryIndex("geohash-index", IsPartitionKey = true)]
    [DynamoDbAttribute("geohash_cell", GeoHashPrecision = 7)]
    [StoreCoordinates(LatitudeAttributeName = "lat", LongitudeAttributeName = "lon")]
    public GeoLocation Location { get; set; }

    [DynamoDbAttribute("store_name")]
    public string Name { get; set; } = string.Empty;

    [DynamoDbAttribute("address")]
    public string Address { get; set; } = string.Empty;
}
```

### Multi-Precision S2 Entity

For adaptive precision based on search radius, define multiple location properties at different S2 levels:

```csharp
[DynamoDbTable("stores-s2", IsDefault = true)]
[GenerateEntityProperty(Name = "Stores")]
[Scannable]
public partial class StoreS2
{
    [PartitionKey]
    [DynamoDbAttribute("pk")]
    public string StoreId { get; set; } = string.Empty;

    [SortKey]
    [DynamoDbAttribute("sk")]
    public string Category { get; set; } = "retail";

    // Fine precision (Level 14, ~284m) for radius ≤ 2km
    [GlobalSecondaryIndex("s2-index-fine", IsPartitionKey = true)]
    [DynamoDbAttribute("s2_cell_l14", SpatialIndexType = SpatialIndexType.S2, S2Level = 14)]
    [StoreCoordinates(LatitudeAttributeName = "lat", LongitudeAttributeName = "lon")]
    public GeoLocation Location { get; set; }

    // Medium precision (Level 12, ~1.1km) for radius 2-10km
    [GlobalSecondaryIndex("s2-index-medium", IsPartitionKey = true)]
    [DynamoDbAttribute("s2_cell_l12", SpatialIndexType = SpatialIndexType.S2, S2Level = 12)]
    public GeoLocation LocationMedium { get; set; }

    // Coarse precision (Level 10, ~4.5km) for radius > 10km
    [GlobalSecondaryIndex("s2-index-coarse", IsPartitionKey = true)]
    [DynamoDbAttribute("s2_cell_l10", SpatialIndexType = SpatialIndexType.S2, S2Level = 10)]
    public GeoLocation LocationCoarse { get; set; }

    [DynamoDbAttribute("store_name")]
    public string Name { get; set; } = string.Empty;

    [DynamoDbAttribute("address")]
    public string Address { get; set; } = string.Empty;
}
```

### Multi-Resolution H3 Entity

```csharp
[DynamoDbTable("stores-h3", IsDefault = true)]
[GenerateEntityProperty(Name = "Stores")]
[Scannable]
public partial class StoreH3
{
    [PartitionKey]
    [DynamoDbAttribute("pk")]
    public string StoreId { get; set; } = string.Empty;

    [SortKey]
    [DynamoDbAttribute("sk")]
    public string Category { get; set; } = "retail";

    // Fine resolution (Resolution 9, ~174m) for radius ≤ 2km
    [GlobalSecondaryIndex("h3-index-fine", IsPartitionKey = true)]
    [DynamoDbAttribute("h3_cell_r9", SpatialIndexType = SpatialIndexType.H3, H3Resolution = 9)]
    [StoreCoordinates(LatitudeAttributeName = "lat", LongitudeAttributeName = "lon")]
    public GeoLocation Location { get; set; }

    // Medium resolution (Resolution 7, ~1.2km) for radius 2-10km
    [GlobalSecondaryIndex("h3-index-medium", IsPartitionKey = true)]
    [DynamoDbAttribute("h3_cell_r7", SpatialIndexType = SpatialIndexType.H3, H3Resolution = 7)]
    public GeoLocation LocationMedium { get; set; }

    // Coarse resolution (Resolution 5, ~8.5km) for radius > 10km
    [GlobalSecondaryIndex("h3-index-coarse", IsPartitionKey = true)]
    [DynamoDbAttribute("h3_cell_r5", SpatialIndexType = SpatialIndexType.H3, H3Resolution = 5)]
    public GeoLocation LocationCoarse { get; set; }

    [DynamoDbAttribute("store_name")]
    public string Name { get; set; } = string.Empty;

    [DynamoDbAttribute("address")]
    public string Address { get; set; } = string.Empty;
}
```

## Code Examples

### Table Setup with Geospatial Support

```csharp
// Initialize table with geospatial provider
var geoHashTable = new StoresGeohashTable(client);
var s2Table = new StoresS2Table(client);
var h3Table = new StoresH3Table(client);
```

### CRUD Operations with Generated Entity Accessors

```csharp
// PREFERRED: Using generated entity accessor PutAsync method
var store = new StoreGeoHash
{
    StoreId = "SF001",
    Name = "Union Square Market",
    Address = "333 Post St, San Francisco, CA",
    Location = new GeoLocation(37.7879, -122.4074),
    Category = "retail"
};
await geoHashTable.Stores.PutAsync(store);

// Scan all stores using generated accessor
var allStores = await geoHashTable.Stores.Scan().ToListAsync();

// Delete a store
await geoHashTable.Stores.DeleteAsync(storeId, category);
```

### Spatial Queries with SpatialQueryAsync Extension

```csharp
// PREFERRED: Using SpatialQueryAsync extension method
var center = new GeoLocation(37.7879, -122.4074);
var radiusKm = 5.0;

var result = await geoHashTable.SpatialQueryAsync<StoreGeoHash>(
    locationSelector: store => store.Location,
    spatialIndexType: SpatialIndexType.GeoHash,
    precision: 7,
    center: center,
    radiusKilometers: radiusKm,
    queryBuilder: (query, cell, pagination) => query
        .Where($"geohash_cell BETWEEN {0} AND {1}", cell.Split(':')[0], cell.Split(':')[1])
);

// Results include distance calculation
var nearbyStores = result.Items
    .Select(store => (Store: store, Distance: store.Location.DistanceToKilometers(center)))
    .OrderBy(r => r.Distance)
    .ToList();
```

### Adaptive Precision S2 Query

```csharp
// Select appropriate S2 level based on search radius
var s2Level = radiusKm switch
{
    <= 2.0 => 14,   // Fine precision
    <= 10.0 => 12,  // Medium precision
    _ => 10         // Coarse precision
};

var (indexName, cellAttribute) = s2Level switch
{
    14 => ("s2-index-fine", "s2_cell_l14"),
    12 => ("s2-index-medium", "s2_cell_l12"),
    _ => ("s2-index-coarse", "s2_cell_l10")
};

var result = await s2Table.SpatialQueryAsync<StoreS2>(
    locationSelector: store => store.Location,
    spatialIndexType: SpatialIndexType.S2,
    precision: s2Level,
    center: center,
    radiusKilometers: radiusKm,
    queryBuilder: (query, cell, pagination) => query
        .UsingIndex(indexName)
        .Where($"{cellAttribute} = {{0}}", cell)
);
```

## Running the Example

### Prerequisites

1. DynamoDB Local running on port 8000:
   ```bash
   java -Djava.library.path=./DynamoDBLocal_lib -jar DynamoDBLocal.jar -sharedDb
   ```

2. .NET 8.0 SDK installed

### Run the Application

```bash
cd examples/StoreLocator
dotnet run
```

### Menu Options

1. **Seed Store Data**: Populates 52 stores in the San Francisco Bay Area
2. **Search with GeoHash**: Proximity search using GeoHash indexing
3. **Search with S2**: Proximity search using S2 indexing with adaptive precision
4. **Search with H3**: Proximity search using H3 indexing with adaptive resolution
5. **Compare All Index Types**: Side-by-side comparison of all three approaches
6. **Clear All Data**: Removes all store data from all tables

## Key Concepts

### Adaptive Precision Selection

S2 and H3 tables automatically select precision based on search radius:

**S2 Levels:**
- Level 14 (~284m cells): Searches under 2km
- Level 12 (~1.1km cells): Searches 2-10km
- Level 10 (~4.5km cells): Searches over 10km

**H3 Resolutions:**
- Resolution 9 (~174m edge): Searches under 2km
- Resolution 7 (~1.2km edge): Searches 2-10km
- Resolution 5 (~8.5km edge): Searches over 10km

### Query Efficiency

The comparison feature shows:
- **Results**: Number of stores found within radius
- **Queries**: Number of DynamoDB queries executed
- **Time**: Total execution time
- **Precision**: The precision level used

## Seed Data

The example includes 52 predefined store locations across the San Francisco Bay Area:
- San Francisco (10 stores)
- Oakland (7 stores)
- Berkeley (5 stores)
- San Jose (6 stores)
- Palo Alto / Mountain View (5 stores)
- Fremont / Union City (4 stores)
- Walnut Creek / Concord (4 stores)
- San Mateo / Redwood City (4 stores)
- Daly City / South San Francisco (2 stores)
- Hayward / San Leandro (4 stores)

Bay Area bounding box: (37.2, -122.6) to (37.9, -121.8)
