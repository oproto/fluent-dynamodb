# Coordinate Storage Options Guide

This guide explains the different ways to store geographic coordinates in DynamoDB and when to use each approach.

## Table of Contents

- [Overview](#overview)
- [Storage Modes](#storage-modes)
- [Trade-offs](#trade-offs)
- [Code Examples](#code-examples)
- [Fallback Behavior](#fallback-behavior)
- [Migration Strategies](#migration-strategies)
- [Best Practices](#best-practices)

## Overview

When storing geographic locations in DynamoDB, you have three options:

1. **Single-Field Mode** - Store only the spatial index (GeoHash/S2/H3)
2. **Separate Properties Mode** - Define separate properties for latitude and longitude
3. **StoreCoordinatesAttribute Mode** - Use an attribute to automatically store coordinates

Each mode has different trade-offs between storage size, precision, and query flexibility.

## Storage Modes

### Mode 1: Single-Field Mode (Default)

**What it stores:**
- Only the spatial index (GeoHash/S2 token/H3 index)

**DynamoDB representation:**
```json
{
  "pk": "STORE#123",
  "location": "9q8yy9r"  // Just the GeoHash
}
```

**Code:**
```csharp
[DynamoDbTable("stores")]
public partial class Store
{
    [PartitionKey]
    [DynamoDbAttribute("pk")]
    public string StoreId { get; set; }
    
    // Single-field mode - only stores the spatial index
    [DynamoDbAttribute("location", GeoHashPrecision = 7)]
    public GeoLocation Location { get; set; }
}
```

**Deserialization behavior:**
- Decodes the spatial index to get the **center point** of the cell
- Returns approximate coordinates (within cell precision)
- **Not exact** - precision depends on cell size

**When to use:**
- Storage efficiency is critical
- Exact coordinates not needed
- Only using for spatial queries
- Backward compatibility with existing GeoHash data

**Advantages:**
- ✅ Minimal storage (single attribute)
- ✅ Simplest configuration
- ✅ Fastest writes (one attribute)

**Disadvantages:**
- ❌ Loses precision (returns cell center)
- ❌ Cannot reconstruct exact coordinates
- ❌ Precision depends on cell size

### Mode 2: Separate Properties Mode

**What it stores:**
- Spatial index for queries
- Separate latitude attribute
- Separate longitude attribute

**DynamoDB representation:**
```json
{
  "pk": "STORE#123",
  "location": "9q8yy9r",      // Spatial index for queries
  "location_lat": 37.7749,    // Full precision latitude
  "location_lon": -122.4194   // Full precision longitude
}
```

**Code:**
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
    
    // Separate properties for full precision
    [DynamoDbAttribute("location_lat")]
    public double LocationLatitude => Location.Latitude;
    
    [DynamoDbAttribute("location_lon")]
    public double LocationLongitude => Location.Longitude;
}
```

**Deserialization behavior:**
- Checks if latitude and longitude attributes exist
- If yes: reconstructs GeoLocation from exact coordinates
- If no: falls back to decoding spatial index (cell center)

**When to use:**
- Need exact coordinates
- Want full control over attribute names
- Migrating from existing schema with separate lat/lon fields
- Need to query by latitude or longitude independently

**Advantages:**
- ✅ Preserves exact coordinates
- ✅ Full control over attribute names
- ✅ Can query lat/lon independently
- ✅ Works with existing schemas

**Disadvantages:**
- ❌ More storage (three attributes)
- ❌ Slightly slower writes (three attributes)
- ❌ More verbose configuration

### Mode 3: StoreCoordinatesAttribute Mode

**What it stores:**
- Spatial index for queries
- Latitude attribute (custom name)
- Longitude attribute (custom name)

**DynamoDB representation:**
```json
{
  "pk": "STORE#123",
  "location_hash": "89c25985",  // S2 token for queries
  "lat": 37.7749,               // Full precision latitude
  "lon": -122.4194              // Full precision longitude
}
```

**Code:**
```csharp
using Oproto.FluentDynamoDb.Attributes;
using Oproto.FluentDynamoDb.Geospatial;

[DynamoDbTable("stores")]
public partial class Store
{
    [PartitionKey]
    [DynamoDbAttribute("pk")]
    public string StoreId { get; set; }
    
    // Automatic coordinate storage with custom names
    [DynamoDbAttribute("location_hash", SpatialIndexType = SpatialIndexType.S2, S2Level = 16)]
    [StoreCoordinates(LatitudeAttributeName = "lat", LongitudeAttributeName = "lon")]
    public GeoLocation Location { get; set; }
}
```

**Deserialization behavior:**
- Same as Separate Properties Mode
- Checks for latitude and longitude attributes
- Falls back to spatial index if not found

**When to use:**
- Need exact coordinates
- Want automatic coordinate storage
- Prefer explicit configuration
- Custom attribute naming required

**Advantages:**
- ✅ Preserves exact coordinates
- ✅ Automatic serialization/deserialization
- ✅ Custom attribute names
- ✅ Explicit configuration

**Disadvantages:**
- ❌ More storage (three attributes)
- ❌ Slightly slower writes (three attributes)
- ❌ Requires additional attribute

## Trade-offs

### Storage Size Comparison

**Single-Field Mode:**
```json
{
  "location": "9q8yy9r"  // ~7-16 bytes
}
Total: ~7-16 bytes
```

**Multi-Field Mode (Separate Properties or StoreCoordinates):**
```json
{
  "location": "9q8yy9r",    // ~7-16 bytes
  "location_lat": 37.7749,  // ~8 bytes (double)
  "location_lon": -122.4194 // ~8 bytes (double)
}
Total: ~23-32 bytes
```

**Storage overhead: ~16 bytes per item**

For 1 million items:
- Single-field: ~7-16 MB
- Multi-field: ~23-32 MB
- **Difference: ~16 MB** (negligible for most use cases)

### Precision Comparison

**Single-Field Mode (GeoHash precision 7):**
- Cell size: ~76m × 153m
- Precision loss: ±38m latitude, ±76m longitude
- Example: (37.7749, -122.4194) → (37.7750, -122.4195)
- **Error: ~50-100 meters**

**Single-Field Mode (S2 Level 16):**
- Cell size: ~1.5km × 1.5km
- Precision loss: ±750m
- Example: (37.7749, -122.4194) → (37.7800, -122.4200)
- **Error: ~500-1000 meters**

**Multi-Field Mode:**
- Stores full double precision (15-17 significant digits)
- Precision: ~1 millimeter
- Example: (37.7749, -122.4194) → (37.7749, -122.4194)
- **Error: ~0 meters** (exact)

### Performance Comparison

| Operation | Single-Field | Multi-Field | Difference |
|-----------|--------------|-------------|------------|
| **Write** | ~10ms | ~12ms | +20% |
| **Read** | ~10ms | ~10ms | Same |
| **Storage** | 7-16 bytes | 23-32 bytes | +100% |
| **Precision** | ±50-1000m | ±0m | Exact |

**Key insight**: Multi-field mode adds minimal overhead but provides exact coordinates.

## Code Examples

### Example 1: Single-Field Mode (Default)

```csharp
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

// Create and store
var store = new Store
{
    StoreId = "STORE#123",
    Location = new GeoLocation(37.7749, -122.4194),
    Name = "Downtown Store"
};

await storeTable.Put.Item(store).ExecuteAsync();

// DynamoDB stores:
// {
//   "pk": "STORE#123",
//   "location": "9q8yy9r",
//   "name": "Downtown Store"
// }

// Retrieve
var retrieved = await storeTable.Get
    .WithKey("STORE#123")
    .ExecuteAsync();

// retrieved.Location is approximately (37.775, -122.420)
// Not exact! Returns cell center
Console.WriteLine($"Original: {store.Location}");
Console.WriteLine($"Retrieved: {retrieved.Location}");
Console.WriteLine($"Error: {store.Location.DistanceToMeters(retrieved.Location):F2}m");
// Output: Error: ~75.00m
```

### Example 2: Separate Properties Mode

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
    
    // Separate properties for exact coordinates
    [DynamoDbAttribute("location_lat")]
    public double LocationLatitude => Location.Latitude;
    
    [DynamoDbAttribute("location_lon")]
    public double LocationLongitude => Location.Longitude;
    
    [DynamoDbAttribute("name")]
    public string Name { get; set; }
}

// Create and store
var store = new Store
{
    StoreId = "STORE#123",
    Location = new GeoLocation(37.7749, -122.4194),
    Name = "Downtown Store"
};

await storeTable.Put.Item(store).ExecuteAsync();

// DynamoDB stores:
// {
//   "pk": "STORE#123",
//   "location": "89c25985",      // S2 token
//   "location_lat": 37.7749,     // Exact latitude
//   "location_lon": -122.4194,   // Exact longitude
//   "name": "Downtown Store"
// }

// Retrieve
var retrieved = await storeTable.Get
    .WithKey("STORE#123")
    .ExecuteAsync();

// retrieved.Location is EXACT (37.7749, -122.4194)
Console.WriteLine($"Original: {store.Location}");
Console.WriteLine($"Retrieved: {retrieved.Location}");
Console.WriteLine($"Error: {store.Location.DistanceToMeters(retrieved.Location):F2}m");
// Output: Error: 0.00m (exact!)
```

### Example 3: StoreCoordinatesAttribute Mode

```csharp
[DynamoDbTable("stores")]
public partial class Store
{
    [PartitionKey]
    [DynamoDbAttribute("pk")]
    public string StoreId { get; set; }
    
    // Automatic coordinate storage with custom names
    [DynamoDbAttribute("location_hash", SpatialIndexType = SpatialIndexType.H3, H3Resolution = 9)]
    [StoreCoordinates(LatitudeAttributeName = "lat", LongitudeAttributeName = "lon")]
    public GeoLocation Location { get; set; }
    
    [DynamoDbAttribute("name")]
    public string Name { get; set; }
}

// Create and store
var store = new Store
{
    StoreId = "STORE#123",
    Location = new GeoLocation(37.7749, -122.4194),
    Name = "Downtown Store"
};

await storeTable.Put.Item(store).ExecuteAsync();

// DynamoDB stores:
// {
//   "pk": "STORE#123",
//   "location_hash": "8928308280fffff",  // H3 index
//   "lat": 37.7749,                      // Exact latitude
//   "lon": -122.4194,                    // Exact longitude
//   "name": "Downtown Store"
// }

// Retrieve
var retrieved = await storeTable.Get
    .WithKey("STORE#123")
    .ExecuteAsync();

// retrieved.Location is EXACT (37.7749, -122.4194)
Console.WriteLine($"Original: {store.Location}");
Console.WriteLine($"Retrieved: {retrieved.Location}");
Console.WriteLine($"Error: {store.Location.DistanceToMeters(retrieved.Location):F2}m");
// Output: Error: 0.00m (exact!)
```

### Example 4: Querying with Multi-Field Storage

```csharp
// Spatial queries work the same regardless of storage mode
var center = new GeoLocation(37.7749, -122.4194);

var result = await storeTable.SpatialQueryAsync(
    spatialAttributeName: "location",  // Uses spatial index
    center: center,
    radiusKilometers: 5,
    queryBuilder: (query, cell, pagination) => query
        .Where<Store>(x => x.PartitionKey == "STORE" && x.Location == cell)
        .Paginate(pagination),
    pageSize: null
);

// Results have exact coordinates (if using multi-field mode)
foreach (var store in result.Items)
{
    var distance = store.Location.DistanceToKilometers(center);
    Console.WriteLine($"{store.Name}: {distance:F3}km away");
    Console.WriteLine($"  Exact location: {store.Location}");
}
```

### Example 5: Querying by Latitude or Longitude

With separate properties, you can query by lat/lon independently:

```csharp
// Only possible with separate properties mode
var storesInLatRange = await storeTable.Query
    .Where("location_lat BETWEEN :minLat AND :maxLat")
    .WithValue(":minLat", 37.7)
    .WithValue(":maxLat", 37.8)
    .ExecuteAsync();

// Or query by longitude
var storesInLonRange = await storeTable.Query
    .Where("location_lon BETWEEN :minLon AND :maxLon")
    .WithValue(":minLon", -122.5)
    .WithValue(":maxLon", -122.4)
    .ExecuteAsync();
```

## Fallback Behavior

The library automatically handles missing coordinate attributes:

### Scenario 1: Reading Old Data (Single-Field) with New Schema (Multi-Field)

```csharp
// Old data in DynamoDB (single-field):
// {
//   "pk": "STORE#123",
//   "location": "9q8yy9r"
// }

// New schema (multi-field):
[DynamoDbAttribute("location", GeoHashPrecision = 7)]
public GeoLocation Location { get; set; }

[DynamoDbAttribute("location_lat")]
public double LocationLatitude => Location.Latitude;

[DynamoDbAttribute("location_lon")]
public double LocationLongitude => Location.Longitude;

// Retrieve old data
var store = await storeTable.Get.WithKey("STORE#123").ExecuteAsync();

// Deserialization:
// 1. Checks for location_lat and location_lon
// 2. Not found, so falls back to decoding "location"
// 3. Returns cell center: (37.775, -122.420)

Console.WriteLine($"Location: {store.Location}");
// Output: Location: (37.775, -122.420) [approximate]
```

### Scenario 2: Reading New Data (Multi-Field) with Old Schema (Single-Field)

```csharp
// New data in DynamoDB (multi-field):
// {
//   "pk": "STORE#123",
//   "location": "9q8yy9r",
//   "location_lat": 37.7749,
//   "location_lon": -122.4194
// }

// Old schema (single-field):
[DynamoDbAttribute("location", GeoHashPrecision = 7)]
public GeoLocation Location { get; set; }

// Retrieve new data
var store = await storeTable.Get.WithKey("STORE#123").ExecuteAsync();

// Deserialization:
// 1. Only knows about "location" attribute
// 2. Decodes "location" to get cell center
// 3. Ignores location_lat and location_lon (not in schema)

Console.WriteLine($"Location: {store.Location}");
// Output: Location: (37.775, -122.420) [approximate]
// Exact coordinates are ignored!
```

### Scenario 3: Partial Data (Missing Coordinates)

```csharp
// Corrupted data in DynamoDB:
// {
//   "pk": "STORE#123",
//   "location": "9q8yy9r",
//   "location_lat": 37.7749
//   // location_lon is missing!
// }

// Schema (multi-field):
[DynamoDbAttribute("location", GeoHashPrecision = 7)]
public GeoLocation Location { get; set; }

[DynamoDbAttribute("location_lat")]
public double LocationLatitude => Location.Latitude;

[DynamoDbAttribute("location_lon")]
public double LocationLongitude => Location.Longitude;

// Retrieve
var store = await storeTable.Get.WithKey("STORE#123").ExecuteAsync();

// Deserialization:
// 1. Checks for location_lat and location_lon
// 2. location_lon is missing, so falls back to decoding "location"
// 3. Returns cell center: (37.775, -122.420)

Console.WriteLine($"Location: {store.Location}");
// Output: Location: (37.775, -122.420) [approximate]
```

### Fallback Rules

1. **Both lat and lon exist** → Use exact coordinates
2. **Either lat or lon missing** → Fall back to spatial index
3. **Spatial index missing** → Throw exception
4. **All missing** → Throw exception

## Migration Strategies

### Strategy 1: Gradual Migration (Recommended)

**Step 1**: Add coordinate properties to schema
```csharp
// Add coordinate properties (doesn't break existing data)
[DynamoDbAttribute("location", GeoHashPrecision = 7)]
public GeoLocation Location { get; set; }

[DynamoDbAttribute("location_lat")]
public double LocationLatitude => Location.Latitude;

[DynamoDbAttribute("location_lon")]
public double LocationLongitude => Location.Longitude;
```

**Step 2**: New writes automatically include coordinates
```csharp
// New items automatically get all three attributes
var store = new Store
{
    StoreId = "STORE#456",
    Location = new GeoLocation(37.7749, -122.4194)
};

await storeTable.Put.Item(store).ExecuteAsync();
// Stores: location, location_lat, location_lon
```

**Step 3**: Old data still works (fallback to spatial index)
```csharp
// Old items only have "location" attribute
var oldStore = await storeTable.Get.WithKey("STORE#123").ExecuteAsync();
// Returns approximate coordinates (cell center)
```

**Step 4**: Backfill old data (optional)
```csharp
// Scan all items and update with exact coordinates
var allStores = await storeTable.Scan.ExecuteAsync();

foreach (var store in allStores)
{
    // Re-save to add coordinate attributes
    await storeTable.Put.Item(store).ExecuteAsync();
}
```

### Strategy 2: Big Bang Migration

**Step 1**: Export all data
```bash
aws dynamodb scan --table-name stores > stores_backup.json
```

**Step 2**: Update schema
```csharp
[DynamoDbAttribute("location", GeoHashPrecision = 7)]
public GeoLocation Location { get; set; }

[DynamoDbAttribute("location_lat")]
public double LocationLatitude => Location.Latitude;

[DynamoDbAttribute("location_lon")]
public double LocationLongitude => Location.Longitude;
```

**Step 3**: Transform and re-import data
```csharp
// Read backup
var stores = JsonSerializer.Deserialize<List<Store>>(backupJson);

// Re-save all items (adds coordinate attributes)
foreach (var store in stores)
{
    await storeTable.Put.Item(store).ExecuteAsync();
}
```

### Strategy 3: Dual-Write Migration

**Step 1**: Write to both old and new attributes
```csharp
// Temporarily write to both formats
var item = new Dictionary<string, AttributeValue>
{
    ["pk"] = new AttributeValue("STORE#123"),
    ["location"] = new AttributeValue(location.ToGeoHash(7)),
    ["location_lat"] = new AttributeValue { N = location.Latitude.ToString() },
    ["location_lon"] = new AttributeValue { N = location.Longitude.ToString() }
};

await dynamoDb.PutItemAsync("stores", item);
```

**Step 2**: Verify new attributes work
```csharp
// Test reading with new schema
var store = await storeTable.Get.WithKey("STORE#123").ExecuteAsync();
Assert.Equal(37.7749, store.Location.Latitude);
```

**Step 3**: Switch to new schema
```csharp
// Update entity definition to use coordinate properties
[DynamoDbAttribute("location", GeoHashPrecision = 7)]
public GeoLocation Location { get; set; }

[DynamoDbAttribute("location_lat")]
public double LocationLatitude => Location.Latitude;

[DynamoDbAttribute("location_lon")]
public double LocationLongitude => Location.Longitude;
```

## Best Practices

### 1. Choose Based on Requirements

**Use Single-Field Mode when:**
- Storage efficiency is critical
- Exact coordinates not needed
- Only using for spatial queries
- Precision loss is acceptable

**Use Multi-Field Mode when:**
- Need exact coordinates
- Displaying locations on maps
- Calculating precise distances
- Compliance requires exact data

### 2. Consider Storage Costs

For 1 million items:
- Single-field: ~$0.0001/month (negligible)
- Multi-field: ~$0.0003/month (negligible)

**Conclusion**: Storage cost difference is negligible. Choose based on precision needs, not cost.

### 3. Document Your Choice

```csharp
/// <summary>
/// Store location.
/// 
/// Storage mode: Multi-field (exact coordinates)
/// - location: S2 token for spatial queries
/// - location_lat: Exact latitude (full precision)
/// - location_lon: Exact longitude (full precision)
/// 
/// Rationale: Need exact coordinates for compliance and precise distance calculations.
/// </summary>
[DynamoDbAttribute("location", SpatialIndexType = SpatialIndexType.S2, S2Level = 16)]
public GeoLocation Location { get; set; }

[DynamoDbAttribute("location_lat")]
public double LocationLatitude => Location.Latitude;

[DynamoDbAttribute("location_lon")]
public double LocationLongitude => Location.Longitude;
```

### 4. Test Fallback Behavior

```csharp
[Fact]
public async Task Location_WithoutCoordinates_FallsBackToSpatialIndex()
{
    // Create item with only spatial index
    var item = new Dictionary<string, AttributeValue>
    {
        ["pk"] = new AttributeValue("STORE#123"),
        ["location"] = new AttributeValue("9q8yy9r")
        // No location_lat or location_lon
    };
    
    await dynamoDb.PutItemAsync("stores", item);
    
    // Retrieve with multi-field schema
    var store = await storeTable.Get.WithKey("STORE#123").ExecuteAsync();
    
    // Should fall back to decoding spatial index
    Assert.NotNull(store.Location);
    Assert.InRange(store.Location.Latitude, 37.7, 37.8);
    Assert.InRange(store.Location.Longitude, -122.5, -122.4);
}
```

### 5. Monitor Precision Loss

```csharp
public async Task<Store> GetStoreWithPrecisionCheck(string storeId)
{
    var store = await storeTable.Get.WithKey(storeId).ExecuteAsync();
    
    // Check if we have exact coordinates
    var hasExactCoordinates = store.LocationLatitude != 0 && store.LocationLongitude != 0;
    
    if (!hasExactCoordinates)
    {
        _logger.LogWarning(
            "Store {StoreId} has approximate coordinates (cell center). " +
            "Consider backfilling with exact coordinates.",
            storeId
        );
    }
    
    return store;
}
```

## Summary

### Quick Decision Guide

**Need exact coordinates?**
- Yes → Use Multi-Field Mode (Separate Properties or StoreCoordinates)
- No → Use Single-Field Mode

**Have existing schema with lat/lon?**
- Yes → Use Separate Properties Mode
- No → Use StoreCoordinatesAttribute Mode

**Storage cost a concern?**
- No → Use Multi-Field Mode (cost difference is negligible)
- Yes → Use Single-Field Mode (but consider precision loss)

### Key Takeaways

1. **Single-field mode loses precision** (returns cell center)
2. **Multi-field mode preserves exact coordinates** (±0 meters)
3. **Storage overhead is minimal** (~16 bytes per item)
4. **Fallback behavior handles missing coordinates** gracefully
5. **Migration is straightforward** (gradual or big bang)
6. **Choose based on precision needs**, not storage cost

### Recommended Default

For most applications, use **Multi-Field Mode** (Separate Properties):
- Preserves exact coordinates
- Minimal storage overhead
- Flexible attribute naming
- Graceful fallback for old data

```csharp
// Recommended default configuration
[DynamoDbAttribute("location", SpatialIndexType = SpatialIndexType.S2, S2Level = 16)]
public GeoLocation Location { get; set; }

[DynamoDbAttribute("location_lat")]
public double LocationLatitude => Location.Latitude;

[DynamoDbAttribute("location_lon")]
public double LocationLongitude => Location.Longitude;
```

**When in doubt, store exact coordinates. Storage is cheap, but lost precision is forever!**
