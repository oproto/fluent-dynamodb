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
