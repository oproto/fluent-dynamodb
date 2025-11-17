# GeoHash Precision Guide

This guide helps you choose the right GeoHash precision level for your geospatial queries in DynamoDB.

## Understanding GeoHash Precision

GeoHash precision determines the size of the geographic cell that a hash represents. Higher precision means smaller cells and more accurate location representation, but also impacts query efficiency and storage.

## Precision Levels Reference

| Precision | Cell Width | Cell Height | Approximate Area | Example Use Case |
|-----------|------------|-------------|------------------|------------------|
| 1 | ±2500 km | ±2500 km | 5000 km × 5000 km | Continental-scale queries |
| 2 | ±630 km | ±630 km | 1260 km × 1260 km | Country or large region queries |
| 3 | ±78 km | ±156 km | 156 km × 156 km | State/province or large city area |
| 4 | ±20 km | ±20 km | 40 km × 40 km | Metropolitan area queries |
| 5 | ±2.4 km | ±4.9 km | 4.9 km × 4.9 km | City district or neighborhood |
| **6** | **±0.61 km** | **±0.61 km** | **1.2 km × 1.2 km** | **Default - Urban district** |
| 7 | ±0.076 km | ±0.153 km | 152 m × 152 m | Street or block level |
| 8 | ±0.019 km | ±0.019 km | 38 m × 38 m | Building or venue level |
| 9 | ±4.8 m | ±4.8 m | 9.6 m × 9.6 m | Precise location (room level) |
| 10 | ±1.2 m | ±0.6 m | 1.2 m × 1.2 m | Very precise (parking spot) |
| 11 | ±0.149 m | ±0.149 m | 30 cm × 30 cm | Sub-meter precision |
| 12 | ±0.037 m | ±0.037 m | 7.4 cm × 7.4 cm | Centimeter precision |

## Choosing the Right Precision

### Common Scenarios

#### Precision 4-5: City-Wide Services
**Use Case**: Food delivery, ride-sharing city zones, weather services

```csharp
[DynamoDbAttribute("location", GeoHashPrecision = 5)]
public GeoLocation Location { get; set; }
```

**Characteristics**:
- Cell size: ~2-5 km
- Good for: Broad city-area queries
- Query efficiency: Excellent (few cells to scan)
- Accuracy: Suitable for zone-based services

**Example**: Finding all delivery zones in a city
```csharp
var cityCenter = new GeoLocation(37.7749, -122.4194);
var zones = await table.Query
    .Where<Zone>(x => x.Location.WithinDistanceKilometers(cityCenter, 20))
    .ExecuteAsync();
```

#### Precision 6-7: Neighborhood Services (Recommended Default)
**Use Case**: Store locators, restaurant finders, local services

```csharp
[DynamoDbAttribute("location", GeoHashPrecision = 6)]  // or 7
public GeoLocation Location { get; set; }
```

**Characteristics**:
- Cell size: ~150m - 1.2 km
- Good for: Most location-based applications
- Query efficiency: Very good
- Accuracy: Suitable for "nearby" searches

**Example**: Finding nearby stores
```csharp
var userLocation = new GeoLocation(37.7749, -122.4194);
var nearbyStores = await storeTable.Query
    .Where<Store>(x => x.Location.WithinDistanceKilometers(userLocation, 5))
    .ExecuteAsync();
```

#### Precision 8-9: Precise Location Services
**Use Case**: Asset tracking, parking spot finders, precise POI

```csharp
[DynamoDbAttribute("location", GeoHashPrecision = 8)]
public GeoLocation Location { get; set; }
```

**Characteristics**:
- Cell size: ~5-40 m
- Good for: Building-level or precise location queries
- Query efficiency: Good (more cells to scan)
- Accuracy: High precision

**Example**: Finding available parking spots
```csharp
var parkingLot = new GeoLocation(37.7749, -122.4194);
var availableSpots = await spotTable.Query
    .Where<ParkingSpot>(x => x.Location.WithinDistanceMeters(parkingLot, 100))
    .ExecuteAsync();
```

#### Precision 10-12: High-Precision Tracking
**Use Case**: Indoor positioning, drone tracking, surveying

```csharp
[DynamoDbAttribute("location", GeoHashPrecision = 10)]
public GeoLocation Location { get; set; }
```

**Characteristics**:
- Cell size: <1 m
- Good for: Sub-meter precision requirements
- Query efficiency: Lower (many cells to scan)
- Accuracy: Very high precision

**Example**: Indoor asset tracking
```csharp
var warehouseZone = new GeoLocation(37.7749, -122.4194);
var assets = await assetTable.Query
    .Where<Asset>(x => x.Location.WithinDistanceMeters(warehouseZone, 10))
    .ExecuteAsync();
```

## Trade-offs and Considerations

### Query Efficiency vs. Accuracy

```
Lower Precision (1-5)
├── Pros: Fast queries, fewer DynamoDB reads, lower cost
├── Cons: Less accurate, larger bounding boxes
└── Best for: Broad area queries, city-level searches

Medium Precision (6-7) ⭐ RECOMMENDED
├── Pros: Good balance of speed and accuracy
├── Cons: None for most use cases
└── Best for: Most location-based applications

Higher Precision (8-12)
├── Pros: Very accurate, small bounding boxes
├── Cons: More DynamoDB reads, higher cost, slower queries
└── Best for: Precise location requirements
```

### Storage Considerations

GeoHash strings are stored as DynamoDB string attributes:

| Precision | Storage Size | Example |
|-----------|--------------|---------|
| 5 | 5 bytes | "9q8yy" |
| 6 | 6 bytes | "9q8yy9" |
| 7 | 7 bytes | "9q8yy9r" |
| 8 | 8 bytes | "9q8yy9r0" |

**Impact**: Minimal - the difference between precision levels is negligible for storage costs.

### Query Cost Considerations

Higher precision can increase query costs:

1. **More Cells to Query**: Higher precision means smaller cells, potentially requiring queries across multiple cells for the same geographic area
2. **Boundary Cases**: Locations near cell boundaries may require querying neighbor cells
3. **DynamoDB Read Units**: More cells = more items scanned = higher read costs

**Example**: A 5km radius query might span:
- Precision 5: 1-4 cells
- Precision 6: 4-9 cells
- Precision 7: 9-25 cells
- Precision 8: 25-100 cells

## Best Practices

### 1. Start with Precision 6 or 7

The default precision of 6 is a good starting point for most applications:

```csharp
[DynamoDbAttribute("location", GeoHashPrecision = 6)]
public GeoLocation Location { get; set; }
```

### 2. Match Precision to Query Radius

Choose precision based on your typical query radius:

| Typical Query Radius | Recommended Precision |
|---------------------|----------------------|
| > 10 km | 4-5 |
| 1-10 km | 6 |
| 100m - 1 km | 7 |
| 10-100 m | 8 |
| < 10 m | 9-10 |

### 3. Consider Your Data Distribution

- **Sparse data** (few locations): Use higher precision for accuracy
- **Dense data** (many locations): Use lower precision to reduce query complexity

### 4. Test with Your Data

Benchmark different precision levels with your actual data:

```csharp
// Test query performance at different precisions
var precisions = new[] { 5, 6, 7, 8 };
foreach (var precision in precisions)
{
    var stopwatch = Stopwatch.StartNew();
    var results = await QueryWithPrecision(precision);
    stopwatch.Stop();
    
    Console.WriteLine($"Precision {precision}: {stopwatch.ElapsedMilliseconds}ms, {results.Count} results");
}
```

### 5. Use Post-Filtering for Exact Distances

GeoHash queries return rectangular bounding boxes. For circular queries, post-filter:

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

## Advanced Scenarios

### Variable Precision by Use Case

Use different precision levels for different entity types:

```csharp
// Stores: Medium precision for neighborhood searches
[DynamoDbTable("stores")]
public partial class Store
{
    [DynamoDbAttribute("location", GeoHashPrecision = 6)]
    public GeoLocation Location { get; set; }
}

// Parking spots: High precision for exact location
[DynamoDbTable("parking-spots")]
public partial class ParkingSpot
{
    [DynamoDbAttribute("location", GeoHashPrecision = 9)]
    public GeoLocation Location { get; set; }
}

// Delivery zones: Low precision for broad areas
[DynamoDbTable("delivery-zones")]
public partial class DeliveryZone
{
    [DynamoDbAttribute("location", GeoHashPrecision = 5)]
    public GeoLocation Location { get; set; }
}
```

### Handling Boundary Cases

For critical applications, query neighbor cells to avoid missing results:

```csharp
var location = new GeoLocation(37.7749, -122.4194);
var cell = location.ToGeoHashCell(7);
var neighbors = cell.GetNeighbors();

// Query all cells (center + 8 neighbors)
var allCells = new[] { cell }.Concat(neighbors);
var allResults = new List<Store>();

foreach (var c in allCells)
{
    var results = await storeTable.Query
        .Where($"location = :hash")
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

## Performance Benchmarks

Typical performance characteristics (based on 10,000 locations):

| Precision | Avg Query Time | Items Scanned | Accuracy |
|-----------|---------------|---------------|----------|
| 5 | 15ms | 50-200 | ±2.4 km |
| 6 | 20ms | 100-400 | ±0.61 km |
| 7 | 30ms | 200-800 | ±0.15 km |
| 8 | 50ms | 400-1600 | ±0.04 km |

*Note: Actual performance depends on data distribution, DynamoDB configuration, and query patterns.*

## Summary

- **Default to precision 6-7** for most applications
- **Lower precision (4-5)** for city-wide or regional queries
- **Higher precision (8-10)** for building-level or precise tracking
- **Always post-filter** for exact circular distance queries
- **Test with your data** to find the optimal precision
- **Consider query costs** when choosing higher precision levels

For more information, see:
- [README.md](README.md) - Getting started guide
- [EXAMPLES.md](EXAMPLES.md) - Usage examples
- [LIMITATIONS.md](LIMITATIONS.md) - Known limitations and edge cases
