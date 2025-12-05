# Design Document

## Overview

This design addresses two issues in the StoreLocator example application:

1. **GeoHash Query Fix**: Replace the incorrect use of `SpatialQueryAsync` with the proper lambda expression approach using `WithinDistanceKilometers`, which translates to a DynamoDB BETWEEN query.

2. **GSI Validation**: Add startup validation to detect missing GSIs and provide a mechanism to recreate tables with the correct index definitions.

## Architecture

The fix involves minimal architectural changes:

```
┌─────────────────────────────────────────────────────────────────┐
│                    StoreLocator Application                      │
├─────────────────────────────────────────────────────────────────┤
│  Program.cs                                                      │
│  ├── EnsureTablesExistAsync() - Creates tables with GSIs        │
│  ├── ValidateGSIsAsync() - NEW: Validates GSI existence         │
│  └── RecreateTablesAsync() - NEW: Deletes and recreates tables  │
├─────────────────────────────────────────────────────────────────┤
│  StoreGeoHashTable.cs                                           │
│  └── FindStoresNearbyAsync()                                    │
│      BEFORE: Uses SpatialQueryAsync (wrong for GeoHash)         │
│      AFTER:  Uses Query with WithinDistanceKilometers lambda    │
├─────────────────────────────────────────────────────────────────┤
│  StoreS2Table.cs / StoreH3Table.cs                              │
│  └── FindStoresNearbyAsync()                                    │
│      (No changes - SpatialQueryAsync is correct for S2/H3)      │
└─────────────────────────────────────────────────────────────────┘
```

## Components and Interfaces

### StoreGeoHashTable Changes

The `FindStoresNearbyAsync` method will be rewritten to use the lambda expression approach:

```csharp
public async Task<List<(StoreGeoHash Store, double DistanceKm)>> FindStoresNearbyAsync(
    GeoLocation center,
    double radiusKilometers)
{
    LastQueryCount = 1; // GeoHash always uses a single BETWEEN query
    
    var results = await LocationIndex.Query<StoreGeoHash>()
        .Where(x => x.Location.WithinDistanceKilometers(center, radiusKilometers))
        .ToListAsync();
    
    // Post-filter by exact distance (BETWEEN returns rectangular approximation)
    return results
        .Select(store => (Store: store, DistanceKm: store.Location.DistanceToKilometers(center)))
        .Where(x => x.DistanceKm <= radiusKilometers)
        .OrderBy(x => x.DistanceKm)
        .ToList();
}
```

### GSI Validation in Program.cs

New methods to validate and recreate tables:

```csharp
async Task<bool> ValidateGSIsAsync()
{
    var issues = new List<string>();
    
    // Check S2 table GSIs
    var s2Description = await client.DescribeTableAsync(StoreS2Table.TableName);
    var s2Gsis = s2Description.Table.GlobalSecondaryIndexes?.Select(g => g.IndexName).ToList() ?? new();
    if (!s2Gsis.Contains("s2-index-fine")) issues.Add("S2 table missing s2-index-fine");
    if (!s2Gsis.Contains("s2-index-medium")) issues.Add("S2 table missing s2-index-medium");
    if (!s2Gsis.Contains("s2-index-coarse")) issues.Add("S2 table missing s2-index-coarse");
    
    // Check H3 table GSIs
    var h3Description = await client.DescribeTableAsync(StoreH3Table.TableName);
    var h3Gsis = h3Description.Table.GlobalSecondaryIndexes?.Select(g => g.IndexName).ToList() ?? new();
    if (!h3Gsis.Contains("h3-index-fine")) issues.Add("H3 table missing h3-index-fine");
    if (!h3Gsis.Contains("h3-index-medium")) issues.Add("H3 table missing h3-index-medium");
    if (!h3Gsis.Contains("h3-index-coarse")) issues.Add("H3 table missing h3-index-coarse");
    
    // Check GeoHash table GSI
    var geoHashDescription = await client.DescribeTableAsync(StoreGeoHashTable.TableName);
    var geoHashGsis = geoHashDescription.Table.GlobalSecondaryIndexes?.Select(g => g.IndexName).ToList() ?? new();
    if (!geoHashGsis.Contains("geohash-index")) issues.Add("GeoHash table missing geohash-index");
    
    if (issues.Count > 0)
    {
        ConsoleHelpers.ShowWarning("Missing GSIs detected:");
        foreach (var issue in issues)
            Console.WriteLine($"  - {issue}");
        return false;
    }
    
    return true;
}

async Task RecreateTablesAsync()
{
    // Delete existing tables
    await DeleteTableIfExistsAsync(StoreGeoHashTable.TableName);
    await DeleteTableIfExistsAsync(StoreS2Table.TableName);
    await DeleteTableIfExistsAsync(StoreH3Table.TableName);
    
    // Recreate with correct GSIs
    await EnsureTablesExistAsync();
}
```

## Data Models

No changes to data models. The existing entity definitions are correct:

- `StoreGeoHash`: Single GeoHash index at precision 7
- `StoreS2`: Three S2 indices at levels 14, 12, 10
- `StoreH3`: Three H3 indices at resolutions 9, 7, 5

## Correctness Properties

*A property is a characteristic or behavior that should hold true across all valid executions of a system-essentially, a formal statement about what the system should do. Properties serve as the bridge between human-readable specifications and machine-verifiable correctness guarantees.*

Based on the prework analysis, the following correctness properties are identified:

### Property 1: GeoHash Search Returns Correct Results Within Radius

*For any* GeoHash search with a center point and radius, all returned stores should be within the exact circular radius from the center, and results should be sorted by ascending distance.

**Validates: Requirements 1.3, 1.4**

### Property 2: GeoHash Query Count Is Always One

*For any* GeoHash spatial search regardless of search radius, the query count should always be exactly 1, since GeoHash uses a single BETWEEN query rather than multiple discrete cell queries.

**Validates: Requirements 3.3**

## Error Handling

### Missing GSI Detection

When a required GSI is missing:
1. The application will catch the `ResourceNotFoundException` or check the table description
2. Display a clear message indicating which GSIs are missing
3. Offer the user the option to recreate tables

### Table Recreation

When recreating tables:
1. Warn the user that all data will be lost
2. Require explicit confirmation
3. Delete tables sequentially to avoid rate limiting
4. Wait for table deletion to complete before recreation
5. Create tables with all required GSIs

## Testing Strategy

### Unit Tests

Unit tests will verify:
- `StoreGeoHashTable.FindStoresNearbyAsync` returns correct results
- Results are properly filtered by exact distance
- Results are sorted by distance
- Query count is always 1 for GeoHash

### Property-Based Tests

Property-based tests using FsCheck will verify:
- **Property 1**: For random store locations and search parameters, all returned stores are within the radius and sorted by distance
- **Property 2**: For any search parameters, GeoHash query count is always 1

The property-based testing library for this project is **FsCheck** (already used in the codebase).

Each property-based test will:
- Run a minimum of 100 iterations
- Be tagged with a comment referencing the correctness property: `**Feature: storelocator-query-fixes, Property {number}: {property_text}**`

### Integration Tests

Integration tests will verify:
- GSI validation correctly detects missing indexes
- Table recreation creates all required GSIs
- End-to-end search functionality works for all three index types
