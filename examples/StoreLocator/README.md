# StoreLocator Example

This example demonstrates geospatial queries with FluentDynamoDb, comparing three different spatial indexing approaches: GeoHash, S2, and H3.

## Features Demonstrated

- **Geospatial Indexing**: Three different spatial index types for location-based queries
- **Adaptive Precision**: Automatic selection of precision level based on search radius
- **Index Comparison**: Side-by-side comparison of query efficiency across index types
- **Multi-Precision GSIs**: Using Global Secondary Indexes for different precision levels

## Spatial Index Types

### GeoHash
- Simple base-32 encoding using Z-order curve
- Good general-purpose choice with wide tool support
- Uses BETWEEN queries on sort key for range searches
- Single precision level (7 = ~76m cells)

### S2 (Google)
- Spherical geometry using Hilbert curve
- Better area uniformity, especially near poles
- Multi-precision with GSIs (Level 16, 14, 12)
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

### Composite Sort Keys

All entities use composite sort keys (`{spatial_index}#{storeId}`) to:
- Enable spatial range queries using `begins_with`
- Ensure uniqueness when multiple stores share the same cell
- Support efficient prefix-based queries

### Adaptive Precision Selection

S2 and H3 tables automatically select precision based on search radius:

**S2 Levels:**
- Level 16 (~1.5km cells): Searches under 2km
- Level 14 (~6km cells): Searches 2-10km
- Level 12 (~25km cells): Searches over 10km

**H3 Resolutions:**
- Resolution 9 (~174m edge): Searches under 1km
- Resolution 7 (~1.2km edge): Searches 1-5km
- Resolution 5 (~8km edge): Searches over 5km

### Query Efficiency

The comparison feature shows:
- **Results**: Number of stores found within radius
- **Queries**: Number of DynamoDB queries executed
- **Time**: Total execution time
- **Precision**: The precision level used

## Code Examples

### Lambda Expression Queries (Preferred)

```csharp
// Query using begins_with for composite sort key
var results = await Query<StoreS2>()
    .Where(x => x.Pk == "STORE" && x.Sk.StartsWith(cellToken))
    .ToListAsync();
```

### Format String Queries (Alternative)

```csharp
// Query using format string approach
var results = await Query<StoreGeoHash>()
    .Where("pk = {0}", "STORE")
    .WithFilter("sk BETWEEN {0} AND {1}", minHash, maxHash)
    .ToListAsync();
```

## Table Schemas

### stores-geohash
- PK: `pk` (String) = "STORE"
- SK: `sk` (String) = `{geohash}#{storeId}`

### stores-s2
- PK: `pk` (String) = "STORE"
- SK: `sk` (String) = `{s2_l16}#{storeId}`
- GSI `gsi-s2-l14`: PK=`pk`, SK=`sk_l14`
- GSI `gsi-s2-l12`: PK=`pk`, SK=`sk_l12`

### stores-h3
- PK: `pk` (String) = "STORE"
- SK: `sk` (String) = `{h3_r9}#{storeId}`
- GSI `gsi-h3-r7`: PK=`pk`, SK=`sk_r7`
- GSI `gsi-h3-r5`: PK=`pk`, SK=`sk_r5`

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
