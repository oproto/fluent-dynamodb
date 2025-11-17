# Oproto.FluentDynamoDb.Geospatial

Geospatial query support for Oproto.FluentDynamoDb using GeoHash encoding. This package enables efficient location-based queries in DynamoDB with type-safe APIs and seamless integration with the FluentDynamoDb library.

## Features

- **GeoLocation Type**: Type-safe geographic coordinates with validation
- **Distance Calculations**: Built-in Haversine formula for accurate distance calculations in meters, kilometers, and miles
- **GeoHash Encoding**: Efficient encoding/decoding for DynamoDB storage and queries
- **Lambda Expression Queries**: Type-safe proximity and bounding box queries
- **Bounding Box Support**: Create rectangular geographic areas for queries
- **AOT Compatible**: Works with Native AOT compilation
- **Zero External Dependencies**: Custom GeoHash implementation with no external geospatial libraries

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

### 2. Create and Store Locations

```csharp
using Oproto.FluentDynamoDb.Geospatial;

var store = new Store
{
    StoreId = "STORE#123",
    Location = new GeoLocation(37.7749, -122.4194), // San Francisco
    Name = "Downtown Store"
};

await storeTable.Put.Item(store).ExecuteAsync();
```

### 3. Query by Proximity

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

## Basic Usage Examples

### Working with GeoLocation

```csharp
// Create a location
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

## GeoHash Precision Guide

Choose the right precision level for your use case:

| Precision | Cell Size | Accuracy | Use Case |
|-----------|-----------|----------|----------|
| 1 | ±2500 km | Continental | Country-level queries |
| 2 | ±630 km | Regional | State/province queries |
| 3 | ±78 km | City | Large city queries |
| 4 | ±20 km | District | City queries |
| 5 | ±2.4 km | Neighborhood | Neighborhood queries |
| **6** | **±0.61 km** | **District** | **Default - Most common** |
| 7 | ±0.076 km | Street | Street-level queries |
| 8 | ±0.019 km | Building | Building-level queries |
| 9 | ±4.8 m | Precise | Precise location queries |
| 10-12 | <1 m | Very Precise | Sub-meter precision |

**Recommendation**: Use precision 6-7 for most applications. Higher precision increases storage and may reduce query efficiency.

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

## Documentation

For comprehensive documentation, examples, and advanced usage patterns, see:
- [Full Documentation](https://github.com/yourusername/Oproto.FluentDynamoDb/docs)
- [API Reference](https://github.com/yourusername/Oproto.FluentDynamoDb/docs/api)
- [Precision Guide](https://github.com/yourusername/Oproto.FluentDynamoDb/docs/geospatial/precision-guide.md)
- [Usage Examples](https://github.com/yourusername/Oproto.FluentDynamoDb/docs/geospatial/examples.md)

## Requirements

- .NET 8.0 or later
- Oproto.FluentDynamoDb 1.0.0 or later
- AOT compatible

## License

[Your License Here]

## Contributing

Contributions are welcome! Please see [CONTRIBUTING.md](../CONTRIBUTING.md) for details.
