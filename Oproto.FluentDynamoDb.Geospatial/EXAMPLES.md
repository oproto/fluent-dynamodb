# Geospatial Usage Examples

This document provides comprehensive examples of using the Oproto.FluentDynamoDb.Geospatial package for location-based queries in DynamoDB.

## Table of Contents

- [Basic Setup](#basic-setup)
- [Working with GeoLocation](#working-with-geolocation)
- [Distance Calculations](#distance-calculations)
- [Lambda Expression Queries](#lambda-expression-queries)
- [Manual Query Patterns](#manual-query-patterns)
- [Bounding Box Queries](#bounding-box-queries)
- [Advanced Scenarios](#advanced-scenarios)
- [Real-World Examples](#real-world-examples)

## Basic Setup

### Entity Definition

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
    
    [DynamoDbAttribute("address")]
    public string Address { get; set; }
}
```

### Table Initialization

```csharp
using Amazon.DynamoDBv2;
using Oproto.FluentDynamoDb.Storage;

var dynamoClient = new AmazonDynamoDBClient();
var storeTable = new StoreTable(dynamoClient);
```

## Working with GeoLocation

### Creating Locations

```csharp
using Oproto.FluentDynamoDb.Geospatial;

// Create from coordinates
var sanFrancisco = new GeoLocation(37.7749, -122.4194);
var newYork = new GeoLocation(40.7128, -74.0060);
var london = new GeoLocation(51.5074, -0.1278);
var tokyo = new GeoLocation(35.6762, 139.6503);

// Validation is automatic
try
{
    var invalid = new GeoLocation(100, 200); // Throws ArgumentOutOfRangeException
}
catch (ArgumentOutOfRangeException ex)
{
    Console.WriteLine($"Invalid location: {ex.Message}");
}
```

### GeoHash Encoding and Decoding

```csharp
using Oproto.FluentDynamoDb.Geospatial.GeoHash;

// Encode to GeoHash with default precision (6)
string hash = sanFrancisco.ToGeoHash();
Console.WriteLine(hash); // "9q8yy9"

// Encode with specific precision
string preciseHash = sanFrancisco.ToGeoHash(9);
Console.WriteLine(preciseHash); // "9q8yy9r0v"

// Decode from GeoHash
var decoded = GeoLocation.FromGeoHash("9q8yy9");
Console.WriteLine($"Lat: {decoded.Latitude}, Lon: {decoded.Longitude}");
// Output: Lat: 37.77490234375, Lon: -122.41943359375
```

### Location Validation

```csharp
var location = new GeoLocation(37.7749, -122.4194);

if (location.IsValid())
{
    Console.WriteLine("Location is valid");
}

// ToString for debugging
Console.WriteLine(location.ToString()); // "37.7749,-122.4194"
```

## Distance Calculations

### All Distance Units

```csharp
var sanFrancisco = new GeoLocation(37.7749, -122.4194);
var newYork = new GeoLocation(40.7128, -74.0060);

// Calculate distance in meters
double distanceMeters = sanFrancisco.DistanceToMeters(newYork);
Console.WriteLine($"Distance: {distanceMeters:N0} meters");
// Output: Distance: 4,130,000 meters

// Calculate distance in kilometers
double distanceKm = sanFrancisco.DistanceToKilometers(newYork);
Console.WriteLine($"Distance: {distanceKm:N2} km");
// Output: Distance: 4,130.00 km

// Calculate distance in miles
double distanceMiles = sanFrancisco.DistanceToMiles(newYork);
Console.WriteLine($"Distance: {distanceMiles:N2} miles");
// Output: Distance: 2,566.00 miles
```

### Sorting by Distance

```csharp
var userLocation = new GeoLocation(37.7749, -122.4194);
var stores = await GetAllStores();

// Sort by distance (closest first)
var sortedStores = stores
    .OrderBy(s => s.Location.DistanceToKilometers(userLocation))
    .ToList();

foreach (var store in sortedStores.Take(5))
{
    var distance = store.Location.DistanceToKilometers(userLocation);
    Console.WriteLine($"{store.Name}: {distance:N2} km away");
}
```

### Finding Closest Location

```csharp
var userLocation = new GeoLocation(37.7749, -122.4194);
var stores = await GetAllStores();

var closestStore = stores
    .OrderBy(s => s.Location.DistanceToMeters(userLocation))
    .FirstOrDefault();

if (closestStore != null)
{
    var distance = closestStore.Location.DistanceToMeters(userLocation);
    Console.WriteLine($"Closest store: {closestStore.Name} ({distance:N0}m away)");
}
```

## Lambda Expression Queries

### Proximity Queries with Meters

```csharp
using Oproto.FluentDynamoDb.Geospatial.GeoHash;

var userLocation = new GeoLocation(37.7749, -122.4194);

// Find stores within 5000 meters (5 km)
var nearbyStores = await storeTable.Query
    .Where<Store>(x => x.Location.WithinDistanceMeters(userLocation, 5000))
    .ExecuteAsync();

Console.WriteLine($"Found {nearbyStores.Count} stores within 5000 meters");
```

### Proximity Queries with Kilometers

```csharp
var userLocation = new GeoLocation(37.7749, -122.4194);

// Find stores within 5 kilometers
var nearbyStores = await storeTable.Query
    .Where<Store>(x => x.Location.WithinDistanceKilometers(userLocation, 5))
    .ExecuteAsync();

// Post-filter for exact circular distance and sort
var exactResults = nearbyStores
    .Where(s => s.Location.DistanceToKilometers(userLocation) <= 5)
    .OrderBy(s => s.Location.DistanceToKilometers(userLocation))
    .ToList();

foreach (var store in exactResults)
{
    var distance = store.Location.DistanceToKilometers(userLocation);
    Console.WriteLine($"{store.Name}: {distance:N2} km");
}
```

### Proximity Queries with Miles

```csharp
var userLocation = new GeoLocation(37.7749, -122.4194);

// Find stores within 3.1 miles (approximately 5 km)
var nearbyStores = await storeTable.Query
    .Where<Store>(x => x.Location.WithinDistanceMiles(userLocation, 3.1))
    .ExecuteAsync();

// Post-filter and sort by miles
var exactResults = nearbyStores
    .Where(s => s.Location.DistanceToMiles(userLocation) <= 3.1)
    .OrderBy(s => s.Location.DistanceToMiles(userLocation))
    .ToList();

foreach (var store in exactResults)
{
    var distance = store.Location.DistanceToMiles(userLocation);
    Console.WriteLine($"{store.Name}: {distance:N2} miles");
}
```

### Combining with Other Conditions

```csharp
var userLocation = new GeoLocation(37.7749, -122.4194);

// Find nearby stores that are also open
var openNearbyStores = await storeTable.Query
    .Where<Store>(x => 
        x.Location.WithinDistanceKilometers(userLocation, 5) &&
        x.IsOpen == true)
    .ExecuteAsync();
```

## Manual Query Patterns

### Basic Manual Query

```csharp
using Oproto.FluentDynamoDb.Geospatial.GeoHash;

var center = new GeoLocation(37.7749, -122.4194);

// Create bounding box from center and distance
var bbox = GeoBoundingBox.FromCenterAndDistanceKilometers(center, 5);

// Get GeoHash range for the bounding box
var (minHash, maxHash) = bbox.GetGeoHashRange(7);

// Execute query with manual BETWEEN expression
var stores = await storeTable.Query
    .Where("location BETWEEN :minHash AND :maxHash")
    .WithValue(":minHash", minHash)
    .WithValue(":maxHash", maxHash)
    .ExecuteAsync();

Console.WriteLine($"Found {stores.Count} stores");
```

### Manual Query with Different Units

```csharp
var center = new GeoLocation(37.7749, -122.4194);

// Using meters
var bboxMeters = GeoBoundingBox.FromCenterAndDistanceMeters(center, 5000);
var (minHashM, maxHashM) = bboxMeters.GetGeoHashRange(7);

// Using kilometers
var bboxKm = GeoBoundingBox.FromCenterAndDistanceKilometers(center, 5);
var (minHashK, maxHashK) = bboxKm.GetGeoHashRange(7);

// Using miles
var bboxMiles = GeoBoundingBox.FromCenterAndDistanceMiles(center, 3.1);
var (minHashMi, maxHashMi) = bboxMiles.GetGeoHashRange(7);

// All three produce equivalent results
Console.WriteLine($"Meters: {minHashM} - {maxHashM}");
Console.WriteLine($"Kilometers: {minHashK} - {maxHashK}");
Console.WriteLine($"Miles: {minHashMi} - {maxHashMi}");
```

### Manual Query with Custom Precision

```csharp
var center = new GeoLocation(37.7749, -122.4194);
var bbox = GeoBoundingBox.FromCenterAndDistanceKilometers(center, 5);

// Use different precision levels
var (minHash5, maxHash5) = bbox.GetGeoHashRange(5);
var (minHash7, maxHash7) = bbox.GetGeoHashRange(7);
var (minHash9, maxHash9) = bbox.GetGeoHashRange(9);

Console.WriteLine($"Precision 5: {minHash5} - {maxHash5}");
Console.WriteLine($"Precision 7: {minHash7} - {maxHash7}");
Console.WriteLine($"Precision 9: {minHash9} - {maxHash9}");
```

## Bounding Box Queries

### Rectangular Area Query

```csharp
// Define a rectangular area
var southwest = new GeoLocation(37.7, -122.5);
var northeast = new GeoLocation(37.8, -122.4);

// Query using lambda expression
var storesInArea = await storeTable.Query
    .Where<Store>(x => x.Location.WithinBoundingBox(southwest, northeast))
    .ExecuteAsync();

Console.WriteLine($"Found {storesInArea.Count} stores in the area");
```

### Bounding Box from Center and Distance

```csharp
var center = new GeoLocation(37.7749, -122.4194);

// Create bounding box with different units
var bboxKm = GeoBoundingBox.FromCenterAndDistanceKilometers(center, 5);
var bboxMeters = GeoBoundingBox.FromCenterAndDistanceMeters(center, 5000);
var bboxMiles = GeoBoundingBox.FromCenterAndDistanceMiles(center, 3.1);

// Query using the bounding box
var stores = await storeTable.Query
    .Where<Store>(x => x.Location.WithinBoundingBox(
        bboxKm.Southwest, 
        bboxKm.Northeast))
    .ExecuteAsync();
```

### Checking if Location is in Bounding Box

```csharp
var bbox = GeoBoundingBox.FromCenterAndDistanceKilometers(
    new GeoLocation(37.7749, -122.4194), 
    5);

var testLocation = new GeoLocation(37.78, -122.42);

if (bbox.Contains(testLocation))
{
    Console.WriteLine("Location is within the bounding box");
}
else
{
    Console.WriteLine("Location is outside the bounding box");
}
```

### Getting Bounding Box Properties

```csharp
var center = new GeoLocation(37.7749, -122.4194);
var bbox = GeoBoundingBox.FromCenterAndDistanceKilometers(center, 5);

Console.WriteLine($"Southwest: {bbox.Southwest}");
Console.WriteLine($"Northeast: {bbox.Northeast}");
Console.WriteLine($"Center: {bbox.Center}");

// Calculate bounding box dimensions
var width = bbox.Southwest.DistanceToKilometers(
    new GeoLocation(bbox.Southwest.Latitude, bbox.Northeast.Longitude));
var height = bbox.Southwest.DistanceToKilometers(
    new GeoLocation(bbox.Northeast.Latitude, bbox.Southwest.Longitude));

Console.WriteLine($"Bounding box: {width:N2} km × {height:N2} km");
```

## Advanced Scenarios

### Working with GeoHash Cells

```csharp
using Oproto.FluentDynamoDb.Geospatial.GeoHash;

var location = new GeoLocation(37.7749, -122.4194);

// Create a GeoHash cell
var cell = location.ToGeoHashCell(7);

Console.WriteLine($"Hash: {cell.Hash}");
Console.WriteLine($"Precision: {cell.Precision}");
Console.WriteLine($"Bounds: SW={cell.Bounds.Southwest}, NE={cell.Bounds.Northeast}");

// Get parent cell (lower precision)
var parent = cell.GetParent();
Console.WriteLine($"Parent hash: {parent.Hash} (precision {parent.Precision})");

// Get child cells (higher precision)
var children = cell.GetChildren();
Console.WriteLine($"Number of children: {children.Length}"); // 32 children

// Get neighboring cells
var neighbors = cell.GetNeighbors();
Console.WriteLine($"Number of neighbors: {neighbors.Length}"); // 8 neighbors
```

### Querying Neighbor Cells for Boundary Cases

```csharp
var location = new GeoLocation(37.7749, -122.4194);
var cell = location.ToGeoHashCell(7);

// Get all cells to query (center + neighbors)
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
var radiusKm = 5.0;
var uniqueResults = allResults
    .Distinct()
    .Where(s => s.Location.DistanceToKilometers(location) <= radiusKm)
    .OrderBy(s => s.Location.DistanceToKilometers(location))
    .ToList();

Console.WriteLine($"Found {uniqueResults.Count} unique stores within {radiusKm} km");
```

### Hierarchical Queries (Multi-Precision)

```csharp
var location = new GeoLocation(37.7749, -122.4194);

// Start with broad search (low precision)
var broadCell = location.ToGeoHashCell(5);
var broadResults = await QueryByGeoHashPrefix(broadCell.Hash);

if (broadResults.Count > 100)
{
    // Too many results, narrow down with higher precision
    var narrowCell = location.ToGeoHashCell(7);
    var narrowResults = await QueryByGeoHashPrefix(narrowCell.Hash);
    return narrowResults;
}

return broadResults;
```

### Pagination with Distance Sorting

```csharp
var userLocation = new GeoLocation(37.7749, -122.4194);
var pageSize = 10;
var allResults = new List<Store>();
string lastEvaluatedKey = null;

// Fetch all results with pagination
do
{
    var page = await storeTable.Query
        .Where<Store>(x => x.Location.WithinDistanceKilometers(userLocation, 5))
        .Take(pageSize)
        .StartFrom(lastEvaluatedKey)
        .ExecuteAsync();
    
    allResults.AddRange(page.Items);
    lastEvaluatedKey = page.LastEvaluatedKey;
} while (lastEvaluatedKey != null);

// Sort all results by distance
var sortedResults = allResults
    .OrderBy(s => s.Location.DistanceToKilometers(userLocation))
    .ToList();

// Return first page of sorted results
var firstPage = sortedResults.Take(pageSize).ToList();
```

## Real-World Examples

### Store Locator

```csharp
public class StoreLocatorService
{
    private readonly StoreTable _storeTable;
    
    public async Task<List<StoreResult>> FindNearbyStores(
        double latitude, 
        double longitude, 
        double radiusKm,
        int maxResults = 10)
    {
        var userLocation = new GeoLocation(latitude, longitude);
        
        // Query with bounding box
        var candidates = await _storeTable.Query
            .Where<Store>(x => x.Location.WithinDistanceKilometers(userLocation, radiusKm))
            .ExecuteAsync();
        
        // Post-filter for exact circular distance and sort
        var results = candidates
            .Where(s => s.Location.DistanceToKilometers(userLocation) <= radiusKm)
            .OrderBy(s => s.Location.DistanceToKilometers(userLocation))
            .Take(maxResults)
            .Select(s => new StoreResult
            {
                StoreId = s.StoreId,
                Name = s.Name,
                Address = s.Address,
                DistanceKm = s.Location.DistanceToKilometers(userLocation),
                Location = s.Location
            })
            .ToList();
        
        return results;
    }
}

public class StoreResult
{
    public string StoreId { get; set; }
    public string Name { get; set; }
    public string Address { get; set; }
    public double DistanceKm { get; set; }
    public GeoLocation Location { get; set; }
}
```

### Delivery Zone Checker

```csharp
public class DeliveryService
{
    private readonly DeliveryZoneTable _zoneTable;
    
    public async Task<bool> IsLocationInDeliveryZone(
        double latitude, 
        double longitude)
    {
        var location = new GeoLocation(latitude, longitude);
        
        // Query nearby zones
        var zones = await _zoneTable.Query
            .Where<DeliveryZone>(x => x.Location.WithinDistanceKilometers(location, 10))
            .ExecuteAsync();
        
        // Check if location is within any zone's radius
        foreach (var zone in zones)
        {
            var distance = location.DistanceToKilometers(zone.Location);
            if (distance <= zone.RadiusKm)
            {
                return true;
            }
        }
        
        return false;
    }
    
    public async Task<DeliveryZone> FindDeliveryZone(
        double latitude, 
        double longitude)
    {
        var location = new GeoLocation(latitude, longitude);
        
        var zones = await _zoneTable.Query
            .Where<DeliveryZone>(x => x.Location.WithinDistanceKilometers(location, 10))
            .ExecuteAsync();
        
        return zones
            .Where(z => location.DistanceToKilometers(z.Location) <= z.RadiusKm)
            .OrderBy(z => location.DistanceToKilometers(z.Location))
            .FirstOrDefault();
    }
}
```

### Ride-Sharing Driver Matching

```csharp
public class RideSharingService
{
    private readonly DriverTable _driverTable;
    
    public async Task<List<Driver>> FindAvailableDrivers(
        double pickupLat,
        double pickupLon,
        double maxDistanceKm = 5)
    {
        var pickupLocation = new GeoLocation(pickupLat, pickupLon);
        
        // Find drivers within range
        var nearbyDrivers = await _driverTable.Query
            .Where<Driver>(x => 
                x.Location.WithinDistanceKilometers(pickupLocation, maxDistanceKm) &&
                x.IsAvailable == true)
            .ExecuteAsync();
        
        // Sort by distance and return closest drivers
        return nearbyDrivers
            .Where(d => d.Location.DistanceToKilometers(pickupLocation) <= maxDistanceKm)
            .OrderBy(d => d.Location.DistanceToKilometers(pickupLocation))
            .Take(5)
            .ToList();
    }
    
    public async Task<Driver> FindClosestDriver(
        double pickupLat,
        double pickupLon)
    {
        var pickupLocation = new GeoLocation(pickupLat, pickupLon);
        
        var drivers = await FindAvailableDrivers(pickupLat, pickupLon, 10);
        
        return drivers
            .OrderBy(d => d.Location.DistanceToKilometers(pickupLocation))
            .FirstOrDefault();
    }
}
```

### Restaurant Finder with Filters

```csharp
public class RestaurantFinderService
{
    private readonly RestaurantTable _restaurantTable;
    
    public async Task<List<Restaurant>> FindRestaurants(
        double latitude,
        double longitude,
        double radiusMiles,
        string cuisine = null,
        decimal? minRating = null)
    {
        var userLocation = new GeoLocation(latitude, longitude);
        
        // Query nearby restaurants
        var candidates = await _restaurantTable.Query
            .Where<Restaurant>(x => x.Location.WithinDistanceMiles(userLocation, radiusMiles))
            .ExecuteAsync();
        
        // Apply filters
        var filtered = candidates
            .Where(r => r.Location.DistanceToMiles(userLocation) <= radiusMiles);
        
        if (!string.IsNullOrEmpty(cuisine))
        {
            filtered = filtered.Where(r => r.Cuisine == cuisine);
        }
        
        if (minRating.HasValue)
        {
            filtered = filtered.Where(r => r.Rating >= minRating.Value);
        }
        
        // Sort by distance
        return filtered
            .OrderBy(r => r.Location.DistanceToMiles(userLocation))
            .ToList();
    }
}
```

### Asset Tracking

```csharp
public class AssetTrackingService
{
    private readonly AssetTable _assetTable;
    
    public async Task<List<Asset>> FindAssetsInArea(
        double centerLat,
        double centerLon,
        double radiusMeters)
    {
        var center = new GeoLocation(centerLat, centerLon);
        
        // High precision for accurate asset tracking
        var assets = await _assetTable.Query
            .Where<Asset>(x => x.Location.WithinDistanceMeters(center, radiusMeters))
            .ExecuteAsync();
        
        return assets
            .Where(a => a.Location.DistanceToMeters(center) <= radiusMeters)
            .OrderBy(a => a.Location.DistanceToMeters(center))
            .ToList();
    }
    
    public async Task<Dictionary<string, int>> GetAssetCountByZone(
        List<GeoLocation> zoneCenters,
        double zoneRadiusMeters)
    {
        var counts = new Dictionary<string, int>();
        
        foreach (var (center, index) in zoneCenters.Select((c, i) => (c, i)))
        {
            var assets = await FindAssetsInArea(
                center.Latitude,
                center.Longitude,
                zoneRadiusMeters);
            
            counts[$"Zone_{index + 1}"] = assets.Count;
        }
        
        return counts;
    }
}
```

## Performance Tips

### 1. Choose Appropriate Precision

```csharp
// For city-wide searches, use lower precision
[DynamoDbAttribute("location", GeoHashPrecision = 5)]
public GeoLocation Location { get; set; }

// For neighborhood searches, use medium precision
[DynamoDbAttribute("location", GeoHashPrecision = 6)]
public GeoLocation Location { get; set; }

// For precise tracking, use higher precision
[DynamoDbAttribute("location", GeoHashPrecision = 9)]
public GeoLocation Location { get; set; }
```

### 2. Always Post-Filter for Exact Distances

```csharp
// Query returns rectangular bounding box
var candidates = await storeTable.Query
    .Where<Store>(x => x.Location.WithinDistanceKilometers(center, 5))
    .ExecuteAsync();

// Post-filter for exact circular distance
var exactResults = candidates
    .Where(s => s.Location.DistanceToKilometers(center) <= 5)
    .ToList();
```

### 3. Cache Frequently Used GeoHash Values

```csharp
private readonly Dictionary<string, string> _geoHashCache = new();

private string GetCachedGeoHash(GeoLocation location, int precision)
{
    var key = $"{location.Latitude},{location.Longitude},{precision}";
    
    if (!_geoHashCache.TryGetValue(key, out var hash))
    {
        hash = location.ToGeoHash(precision);
        _geoHashCache[key] = hash;
    }
    
    return hash;
}
```

## See Also

- [README.md](README.md) - Getting started guide
- [PRECISION_GUIDE.md](PRECISION_GUIDE.md) - Choosing the right precision
- [LIMITATIONS.md](LIMITATIONS.md) - Known limitations and edge cases


## S2 and H3 Examples

### Entity Definition with S2

```csharp
using Oproto.FluentDynamoDb.Attributes;
using Oproto.FluentDynamoDb.Geospatial;

[DynamoDbTable("stores")]
public partial class Store
{
    [PartitionKey]
    [DynamoDbAttribute("pk")]
    public string StoreId { get; set; }
    
    // S2 spatial index for queries
    [DynamoDbAttribute("location", SpatialIndexType = SpatialIndexType.S2, S2Level = 16)]
    public GeoLocation Location { get; set; }
    
    // Optional: Store exact coordinates
    [DynamoDbAttribute("location_lat")]
    public double LocationLatitude => Location.Latitude;
    
    [DynamoDbAttribute("location_lon")]
    public double LocationLongitude => Location.Longitude;
    
    [DynamoDbAttribute("name")]
    public string Name { get; set; }
}
```

### Entity Definition with H3

```csharp
[DynamoDbTable("delivery_zones")]
public partial class DeliveryZone
{
    [PartitionKey]
    [DynamoDbAttribute("pk")]
    public string ZoneId { get; set; }
    
    // H3 spatial index for queries
    [DynamoDbAttribute("center", SpatialIndexType = SpatialIndexType.H3, H3Resolution = 9)]
    public GeoLocation Center { get; set; }
    
    [DynamoDbAttribute("name")]
    public string Name { get; set; }
    
    [DynamoDbAttribute("radius_km")]
    public double RadiusKm { get; set; }
}
```

### Non-Paginated Proximity Query (Fastest)

```csharp
using Oproto.FluentDynamoDb.Geospatial;

// Find ALL stores within 5km - fastest approach
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

Console.WriteLine($"Found {result.Items.Count} stores");
Console.WriteLine($"Queried {result.TotalCellsQueried} cells in parallel");
Console.WriteLine($"Scanned {result.TotalItemsScanned} items");

// Results are automatically sorted by distance from center
foreach (var store in result.Items.Take(10))
{
    var distance = store.Location.DistanceToKilometers(center);
    Console.WriteLine($"{store.Name}: {distance:F2}km away");
}
```

### Paginated Proximity Query (Memory Efficient)

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
Console.WriteLine($"Queried {result.TotalCellsQueried} cells");
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

### Paginated Query - Fetching All Pages

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
    
    Console.WriteLine($"Page {pageNumber}: {result.Items.Count} items");
    pageNumber++;
} while (token != null);

Console.WriteLine($"Total stores found: {allStores.Count}");
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

### Working with S2 Cells

```csharp
using Oproto.FluentDynamoDb.Geospatial.S2;

var location = new GeoLocation(37.7749, -122.4194);

// Convert to S2 cell
var cell = location.ToS2Cell(level: 16);
Console.WriteLine($"S2 Token: {cell.Token}");
Console.WriteLine($"Level: {cell.Level}");
Console.WriteLine($"Bounds: {cell.Bounds}");

// Get neighboring cells (8 neighbors for S2)
var neighbors = cell.GetNeighbors();
Console.WriteLine($"Found {neighbors.Length} neighbors");

foreach (var neighbor in neighbors)
{
    Console.WriteLine($"  Neighbor: {neighbor.Token}");
}

// Get parent cell (lower precision)
var parent = cell.GetParent();
Console.WriteLine($"Parent token: {parent.Token} (level {parent.Level})");

// Get child cells (4 children for S2)
var children = cell.GetChildren();
Console.WriteLine($"Found {children.Length} children");

foreach (var child in children)
{
    Console.WriteLine($"  Child: {child.Token} (level {child.Level})");
}
```

### Working with H3 Cells

```csharp
using Oproto.FluentDynamoDb.Geospatial.H3;

var location = new GeoLocation(37.7749, -122.4194);

// Convert to H3 cell
var cell = location.ToH3Cell(resolution: 9);
Console.WriteLine($"H3 Index: {cell.Index}");
Console.WriteLine($"Resolution: {cell.Resolution}");
Console.WriteLine($"Bounds: {cell.Bounds}");

// Get neighboring cells (6 neighbors for hexagons, 5 for pentagons)
var neighbors = cell.GetNeighbors();
Console.WriteLine($"Found {neighbors.Length} neighbors");

foreach (var neighbor in neighbors)
{
    Console.WriteLine($"  Neighbor: {neighbor.Index}");
}

// Get parent cell (lower resolution)
var parent = cell.GetParent();
Console.WriteLine($"Parent index: {parent.Index} (resolution {parent.Resolution})");

// Get child cells (7 children for H3 aperture-7)
var children = cell.GetChildren();
Console.WriteLine($"Found {children.Length} children");

foreach (var child in children)
{
    Console.WriteLine($"  Child: {child.Index} (resolution {child.Resolution})");
}
```

### Encoding and Decoding with All Three Systems

```csharp
using Oproto.FluentDynamoDb.Geospatial.GeoHash;
using Oproto.FluentDynamoDb.Geospatial.S2;
using Oproto.FluentDynamoDb.Geospatial.H3;

var location = new GeoLocation(37.7749, -122.4194);

// Encode with all three systems
var geohash = location.ToGeoHash(precision: 7);
var s2Token = location.ToS2Token(level: 16);
var h3Index = location.ToH3Index(resolution: 9);

Console.WriteLine($"Original: {location}");
Console.WriteLine($"GeoHash: {geohash}");
Console.WriteLine($"S2 Token: {s2Token}");
Console.WriteLine($"H3 Index: {h3Index}");

// Decode back to locations
var fromGeoHash = GeoLocation.FromGeoHash(geohash);
var fromS2 = GeoLocation.FromS2Token(s2Token);
var fromH3 = GeoLocation.FromH3Index(h3Index);

// Calculate precision loss
Console.WriteLine($"\nPrecision Loss:");
Console.WriteLine($"GeoHash: {location.DistanceToMeters(fromGeoHash):F2}m");
Console.WriteLine($"S2: {location.DistanceToMeters(fromS2):F2}m");
Console.WriteLine($"H3: {location.DistanceToMeters(fromH3):F2}m");
```

### Query with Additional Filters

```csharp
var center = new GeoLocation(37.7749, -122.4194);

// Find nearby stores that are also open
var result = await storeTable.SpatialQueryAsync(
    spatialAttributeName: "location",
    center: center,
    radiusKilometers: 5,
    queryBuilder: (query, cell, pagination) => query
        .Where<Store>(x => 
            x.PartitionKey == "STORE" && 
            x.Location == cell &&
            x.IsOpen == true)
        .Paginate(pagination),
    pageSize: null
);

Console.WriteLine($"Found {result.Items.Count} open stores within 5km");
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

Console.WriteLine($"Found {result.Items.Count} grocery stores within 5km");
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
        items = result.Items.Select(s => new
        {
            id = s.StoreId,
            name = s.Name,
            location = new { lat = s.Location.Latitude, lon = s.Location.Longitude },
            distance = s.Location.DistanceToKilometers(center)
        }),
        nextToken = result.ContinuationToken?.ToBase64(),
        hasMore = result.ContinuationToken != null,
        totalCellsQueried = result.TotalCellsQueried,
        totalItemsScanned = result.TotalItemsScanned
    });
}
```

### Real-World: Store Locator with S2

```csharp
public class StoreLocatorService
{
    private readonly StoreTable _storeTable;
    
    public async Task<StoreLocatorResponse> FindNearbyStores(
        double latitude, 
        double longitude, 
        double radiusKm,
        int maxResults = 10)
    {
        var userLocation = new GeoLocation(latitude, longitude);
        
        // Use non-paginated mode for fast response
        var result = await _storeTable.SpatialQueryAsync(
            spatialAttributeName: "location",
            center: userLocation,
            radiusKilometers: radiusKm,
            queryBuilder: (query, cell, pagination) => query
                .Where<Store>(x => x.PartitionKey == "STORE" && x.Location == cell)
                .Paginate(pagination),
            pageSize: null
        );
        
        // Results are already sorted by distance
        var stores = result.Items
            .Take(maxResults)
            .Select(s => new StoreResult
            {
                StoreId = s.StoreId,
                Name = s.Name,
                DistanceKm = s.Location.DistanceToKilometers(userLocation),
                Location = s.Location
            })
            .ToList();
        
        return new StoreLocatorResponse
        {
            Stores = stores,
            TotalFound = result.Items.Count,
            CellsQueried = result.TotalCellsQueried,
            QueryTimeMs = result.QueryTimeMs
        };
    }
}
```

### Real-World: Delivery Zone Checker with H3

```csharp
public class DeliveryService
{
    private readonly DeliveryZoneTable _zoneTable;
    
    public async Task<DeliveryZoneInfo> CheckDeliveryAvailability(
        double latitude, 
        double longitude)
    {
        var location = new GeoLocation(latitude, longitude);
        
        // Query nearby zones (H3 provides excellent coverage)
        var result = await _zoneTable.SpatialQueryAsync(
            spatialAttributeName: "center",
            center: location,
            radiusKilometers: 10,
            queryBuilder: (query, cell, pagination) => query
                .Where<DeliveryZone>(x => x.PartitionKey == "ZONE" && x.Center == cell)
                .Paginate(pagination),
            pageSize: null
        );
        
        // Find the zone that contains this location
        var zone = result.Items
            .Where(z => location.DistanceToKilometers(z.Center) <= z.RadiusKm)
            .OrderBy(z => location.DistanceToKilometers(z.Center))
            .FirstOrDefault();
        
        if (zone != null)
        {
            return new DeliveryZoneInfo
            {
                IsAvailable = true,
                ZoneName = zone.Name,
                DistanceFromCenter = location.DistanceToKilometers(zone.Center)
            };
        }
        
        return new DeliveryZoneInfo
        {
            IsAvailable = false
        };
    }
}
```

### Real-World: Asset Tracking with Multiple Precision Levels

```csharp
public class AssetTrackingService
{
    private readonly AssetTable _assetTable;
    
    // Store assets at multiple precision levels for different query types
    [DynamoDbTable("assets")]
    public partial class Asset
    {
        [PartitionKey]
        [DynamoDbAttribute("pk")]
        public string AssetId { get; set; }
        
        // Low precision for regional queries (50-100km)
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
    
    public async Task<List<Asset>> FindAssets(
        double latitude,
        double longitude,
        double radiusKm)
    {
        var center = new GeoLocation(latitude, longitude);
        
        // Choose appropriate precision based on radius
        var attributeName = radiusKm switch
        {
            <= 10 => "location_local",   // S2 Level 16
            <= 50 => "location_city",    // S2 Level 14
            _ => "location_region"       // S2 Level 12
        };
        
        var result = await _assetTable.SpatialQueryAsync(
            spatialAttributeName: attributeName,
            center: center,
            radiusKilometers: radiusKm,
            queryBuilder: (query, cell, pagination) => query
                .Where<Asset>(x => x.PartitionKey == "ASSET" && x.Location == cell)
                .Paginate(pagination),
            pageSize: null
        );
        
        return result.Items;
    }
}
```

### Performance Monitoring

```csharp
public async Task<SpatialQueryResponse<Store>> SearchStoresWithMonitoring(
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
        "Spatial query: " +
        "Radius={Radius}km, " +
        "PageSize={PageSize}, " +
        "Cells={Cells}, " +
        "Scanned={Scanned}, " +
        "Returned={Returned}, " +
        "Efficiency={Efficiency:F1}%, " +
        "Latency={Latency}ms",
        radiusKm,
        pageSize?.ToString() ?? "null",
        result.TotalCellsQueried,
        result.TotalItemsScanned,
        result.Items.Count,
        efficiency,
        stopwatch.ElapsedMilliseconds
    );
    
    // Alert on potential issues
    if (result.TotalCellsQueried >= 100)
    {
        _logger.LogWarning(
            "High cell count! Consider reducing precision or radius."
        );
    }
    
    return result;
}
```

## Precision Selection Examples

### Example 1: Choosing Precision for 5km Radius

```csharp
// Calculate cell count for different precisions
var radiusKm = 5.0;

// S2 Level 14 (~6km cells)
var cellCountL14 = Math.PI * Math.Pow(radiusKm / 6.0, 2);
Console.WriteLine($"S2 Level 14: ~{cellCountL14:F0} cells"); // ~2 cells ✅

// S2 Level 16 (~1.5km cells)
var cellCountL16 = Math.PI * Math.Pow(radiusKm / 1.5, 2);
Console.WriteLine($"S2 Level 16: ~{cellCountL16:F0} cells"); // ~35 cells ✅

// S2 Level 18 (~400m cells)
var cellCountL18 = Math.PI * Math.Pow(radiusKm / 0.4, 2);
Console.WriteLine($"S2 Level 18: ~{cellCountL18:F0} cells"); // ~490 cells ⚠️

// Recommendation: Use Level 16 for good balance
[DynamoDbAttribute("location", SpatialIndexType = SpatialIndexType.S2, S2Level = 16)]
public GeoLocation Location { get; set; }
```

### Example 2: Query Explosion Scenario

```csharp
// ❌ BAD: 50km radius with S2 Level 16 (~1.5km cells)
var radiusKm = 50.0;
var cellSize = 1.5;
var cellCount = Math.PI * Math.Pow(radiusKm / cellSize, 2);
Console.WriteLine($"Cell count: ~{cellCount:F0}"); // ~3,490 cells 🚫

// This will hit maxCells limit (default 100)
// Results will be INCOMPLETE!

// ✅ GOOD: 50km radius with S2 Level 12 (~25km cells)
cellSize = 25.0;
cellCount = Math.PI * Math.Pow(radiusKm / cellSize, 2);
Console.WriteLine($"Cell count: ~{cellCount:F0}"); // ~13 cells ✅

// Recommendation: Use Level 12 for large radius
[DynamoDbAttribute("location", SpatialIndexType = SpatialIndexType.S2, S2Level = 12)]
public GeoLocation Location { get; set; }
```

### Example 3: Adaptive Precision Based on Radius

```csharp
public async Task<List<Store>> SearchStoresAdaptive(
    GeoLocation center,
    double radiusKm)
{
    // Choose precision based on radius to avoid query explosion
    var (attributeName, level) = radiusKm switch
    {
        <= 5 => ("location_precise", 18),   // ~400m cells
        <= 10 => ("location_local", 16),    // ~1.5km cells
        <= 50 => ("location_city", 14),     // ~6km cells
        _ => ("location_region", 12)        // ~25km cells
    };
    
    Console.WriteLine($"Using {attributeName} (S2 Level {level}) for {radiusKm}km radius");
    
    var result = await _storeTable.SpatialQueryAsync(
        spatialAttributeName: attributeName,
        center: center,
        radiusKilometers: radiusKm,
        queryBuilder: (query, cell, pagination) => query
            .Where<Store>(x => x.PartitionKey == "STORE" && x.Location == cell)
            .Paginate(pagination),
        pageSize: null
    );
    
    return result.Items;
}
```

## See Also

- [README.md](README.md) - Getting started guide
- [S2_H3_USAGE_GUIDE.md](S2_H3_USAGE_GUIDE.md) - Choosing between index types
- [PRECISION_GUIDE.md](PRECISION_GUIDE.md) - Precision selection and query explosion
- [PERFORMANCE_GUIDE.md](PERFORMANCE_GUIDE.md) - Query optimization
- [COORDINATE_STORAGE_GUIDE.md](COORDINATE_STORAGE_GUIDE.md) - Storing exact coordinates
