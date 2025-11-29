# Geospatial Support

This guide explains how to configure and use geospatial features in FluentDynamoDb. The geospatial package provides support for location-based queries using GeoHash, S2, and H3 spatial indexing systems.

## Installation

Install the geospatial package:

```bash
dotnet add package Oproto.FluentDynamoDb.Geospatial
```

Or add to your `.csproj` file:

```xml
<PackageReference Include="Oproto.FluentDynamoDb.Geospatial" Version="1.0.0" />
```

## Configuration

Geospatial features require explicit configuration using `FluentDynamoDbOptions`. Use the `AddGeospatial()` extension method to enable geospatial support:

```csharp
using Oproto.FluentDynamoDb;

var options = new FluentDynamoDbOptions()
    .AddGeospatial();

var table = new LocationsTable(client, "locations", options);
```

### Error Without Configuration

If you attempt to use geospatial features without calling `AddGeospatial()`, you'll receive an error:

> "Geospatial features require configuration. Add the Oproto.FluentDynamoDb.Geospatial package and call options.AddGeospatial() when creating your table."

This error indicates that:
1. The geospatial package may not be installed
2. The `AddGeospatial()` method was not called when creating the options
3. The options were not passed to the table constructor

### Combining with Other Features

Chain `AddGeospatial()` with other configuration methods:

```csharp
using Oproto.FluentDynamoDb;
using Oproto.FluentDynamoDb.Logging.Extensions;

var options = new FluentDynamoDbOptions()
    .WithLogger(loggerFactory.ToDynamoDbLogger<LocationsTable>())
    .AddGeospatial();

var table = new LocationsTable(client, "locations", options);
```

## Defining Geospatial Entities

### GeoHash (Simple, Fast)

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

### S2 (Global Coverage, Better at Poles)

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

### H3 (Hexagonal, Most Uniform Coverage)

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

## Basic Usage

### Creating and Storing Locations

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

### Working with GeoLocation

```csharp
// Create a location from coordinates
var sanFrancisco = new GeoLocation(37.7749, -122.4194);
var newYork = new GeoLocation(40.7128, -74.0060);

// Calculate distances in different units
double distanceMeters = sanFrancisco.DistanceToMeters(newYork);
double distanceKm = sanFrancisco.DistanceToKilometers(newYork);
double distanceMiles = sanFrancisco.DistanceToMiles(newYork);

// Encode to GeoHash
string hash = sanFrancisco.ToGeoHash(7); // "9q8yy9r"

// Decode from GeoHash
var location = GeoLocation.FromGeoHash("9q8yy9r");
```

## Proximity Queries

### GeoHash Queries (Lambda Expression API)

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

### S2 and H3 Queries (SpatialQueryAsync API)

```csharp
using Oproto.FluentDynamoDb.Geospatial;

var center = new GeoLocation(37.7749, -122.4194);
var result = await storeTable.SpatialQueryAsync(
    spatialAttributeName: "location",
    center: center,
    radiusKilometers: 5,
    queryBuilder: (query, cell, pagination) => query
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

### Distance Units

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

## Bounding Box Queries

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

## Choosing a Spatial Index Type

### Quick Comparison

| Feature | GeoHash | S2 | H3 |
|---------|---------|----|----|
| Cell Shape | Rectangle | Square | Hexagon |
| Precision Levels | 1-12 | 0-30 | 0-15 |
| Default | 6 (~610m) | 16 (~1.5km) | 9 (~174m) |
| Query Type | Single BETWEEN | Multiple queries | Multiple queries |
| Best For | Simple queries | Global coverage | Uniform coverage |
| Pole Handling | Poor | Good | Excellent |

### When to Use Each

**Use GeoHash when:**
- Simple proximity queries
- Low latency critical (single query)
- Mid-latitude locations
- Backward compatibility needed

**Use S2 when:**
- Global coverage needed
- Polar regions important
- Hierarchical queries
- Area uniformity matters

**Use H3 when:**
- Most uniform coverage needed
- Hexagonal neighbors important
- Grid analysis required
- Visual appeal matters

## Paginated Queries

For large result sets, use pagination:

```csharp
var result = await storeTable.SpatialQueryAsync(
    spatialAttributeName: "location",
    center: center,
    radiusKilometers: 10,
    queryBuilder: (query, cell, pagination) => query
        .Where<Store>(x => x.PartitionKey == "STORE" && x.Location == cell)
        .Paginate(pagination),
    pageSize: 50
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

## Custom Geospatial Provider

For testing or custom implementations, use a custom provider:

```csharp
var options = new FluentDynamoDbOptions()
    .AddGeospatial(new MyCustomGeospatialProvider());
```

## Limitations

### DynamoDB Query Patterns

1. **Rectangular Queries Only**: DynamoDB BETWEEN queries create rectangular bounding boxes, not circles. Use post-filtering with `DistanceTo()` methods for exact circular queries.

2. **No Native Distance Sorting**: DynamoDB cannot sort by distance from a point. Retrieve results and sort in memory using `DistanceTo()` methods.

3. **Single Range Query**: DynamoDB supports one BETWEEN condition per query. Boundary cases may require querying neighbor cells.

### Edge Cases

- **Poles**: GeoHash precision decreases near poles due to longitude convergence
- **Date Line**: Queries crossing the international date line (±180°) require special handling
- **Cell Boundaries**: Locations near cell boundaries may require querying neighbor cells

## See Also

- [Configuration Guide](../core-features/Configuration.md) - Central configuration documentation
- [Geospatial Package README](../../Oproto.FluentDynamoDb.Geospatial/README.md) - Complete API reference
- [S2 and H3 Usage Guide](../../Oproto.FluentDynamoDb.Geospatial/S2_H3_USAGE_GUIDE.md) - Choosing between index types
- [Precision Guide](../../Oproto.FluentDynamoDb.Geospatial/PRECISION_GUIDE.md) - Choosing precision levels
