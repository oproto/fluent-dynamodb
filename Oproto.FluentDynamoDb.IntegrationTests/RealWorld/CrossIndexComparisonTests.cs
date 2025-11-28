using System.Diagnostics;
using Amazon.DynamoDBv2;
using Oproto.FluentDynamoDb.Attributes;
using Oproto.FluentDynamoDb.Geospatial;
using Oproto.FluentDynamoDb.IntegrationTests.Infrastructure;
using Oproto.FluentDynamoDb.IntegrationTests.TestEntities;
using Oproto.FluentDynamoDb.Storage;

namespace Oproto.FluentDynamoDb.IntegrationTests.RealWorld;

/// <summary>
/// Cross-index comparison tests for S2 and H3 spatial indices.
/// These tests compare behavior, precision, and performance across different spatial index types.
/// Note: GeoHash uses a different query pattern (BETWEEN) so is tested separately.
/// </summary>
[Collection("DynamoDB Local")]
[Trait("Category", "Integration")]
[Trait("Feature", "Geospatial")]
[Trait("Feature", "CrossIndex")]
public class CrossIndexComparisonTests : IntegrationTestBase
{
    public CrossIndexComparisonTests(DynamoDbLocalFixture fixture) : base(fixture)
    {
    }
    
    #region Table Wrappers
    
    private class S2StoreTable : DynamoDbTableBase
    {
        public S2StoreTable(IAmazonDynamoDB client, string tableName) 
            : base(client, tableName)
        {
        }
        
        public async Task PutAsync(S2StoreEntity entity)
        {
            var item = S2StoreEntity.ToDynamoDb(entity);
            await DynamoDbClient.PutItemAsync(Name, item);
        }
    }
    
    private class H3StoreTable : DynamoDbTableBase
    {
        public H3StoreTable(IAmazonDynamoDB client, string tableName) 
            : base(client, tableName)
        {
        }
        
        public async Task PutAsync(H3StoreLocationSortKeyEntity entity)
        {
            var item = H3StoreLocationSortKeyEntity.ToDynamoDb(entity);
            await DynamoDbClient.PutItemAsync(Name, item);
        }
    }
    
    #endregion

    
    #region 31.1 Test same location with different index types
    
    /// <summary>
    /// Task 31.1: Test same location with different index types.
    /// Stores the same location using S2 and H3, then queries each
    /// to verify both return the location correctly.
    /// </summary>
    [Fact]
    public async Task SameLocation_StoredWithDifferentIndexTypes_AllReturnCorrectly()
    {
        // Arrange - Define test locations
        var testLocations = new[]
        {
            new GeoLocation(37.7749, -122.4194),  // San Francisco
            new GeoLocation(40.7128, -74.0060),   // New York
            new GeoLocation(51.5074, -0.1278),    // London
            new GeoLocation(-33.8688, 151.2093),  // Sydney
            new GeoLocation(35.6762, 139.6503),   // Tokyo
        };
        
        Console.WriteLine("[Cross-Index Test 31.1] Testing same locations with S2 and H3...");
        
        foreach (var location in testLocations)
        {
            Console.WriteLine($"\n[Cross-Index Test 31.1] Testing location: ({location.Latitude:F4}, {location.Longitude:F4})");
            
            // Test S2
            var s2Result = await TestS2StorageAndRetrieval(location);
            
            // Test H3
            var h3Result = await TestH3StorageAndRetrieval(location);
            
            // Both should find the location
            s2Result.Found.Should().BeTrue("S2 should find the stored location");
            h3Result.Found.Should().BeTrue("H3 should find the stored location");
            
            Console.WriteLine($"  S2: distance={s2Result.DistanceMeters:F1}m, cells={s2Result.CellsQueried}");
            Console.WriteLine($"  H3: distance={h3Result.DistanceMeters:F1}m, cells={h3Result.CellsQueried}");
        }
    }
    
    private async Task<(bool Found, double DistanceMeters, int CellsQueried)> TestS2StorageAndRetrieval(GeoLocation location)
    {
        // Delete existing table if it exists before creating a new one
        try { await DynamoDb.DeleteTableAsync(TableName); await Task.Delay(500); } catch { }
        await CreateTableAsync<S2StoreEntity>();
        var table = new S2StoreTable(DynamoDb, TableName);
        
        var entity = new S2StoreEntity
        {
            StoreId = "STORE",
            Location = location,
            Name = "Test Store",
            Description = "S2 test"
        };
        
        await table.PutAsync(entity);
        
        // Use 0.5km radius to stay within 500 cell limit for S2 level 16 (~71m cells)
        var result = await table.SpatialQueryAsync<S2StoreEntity>(
            locationSelector: s => s.Location,
            spatialIndexType: SpatialIndexType.S2,
            precision: 16,
            center: location,
            radiusKilometers: 0.5,
            queryBuilder: (query, cell, pagination) => query
                .Where<S2StoreEntity>(x => x.StoreId == "STORE" && x.Location == cell),
            pageSize: null
        );
        
        if (result.Items.Count == 0)
            return (false, 0, result.TotalCellsQueried);
        
        var retrieved = result.Items[0];
        var distance = retrieved.Location.DistanceToKilometers(location) * 1000; // Convert to meters
        
        return (true, distance, result.TotalCellsQueried);
    }
    
    private async Task<(bool Found, double DistanceMeters, int CellsQueried)> TestH3StorageAndRetrieval(GeoLocation location)
    {
        // Delete existing table if it exists before creating a new one
        try { await DynamoDb.DeleteTableAsync(TableName); await Task.Delay(500); } catch { }
        await CreateTableAsync<H3StoreLocationSortKeyEntity>();
        var table = new H3StoreTable(DynamoDb, TableName);
        
        var entity = new H3StoreLocationSortKeyEntity
        {
            StoreId = "STORE",
            Location = location,
            Name = "Test Store",
            Description = "H3 test"
        };
        
        await table.PutAsync(entity);
        
        // Use 0.5km radius to stay within 500 cell limit for H3 resolution 9 (~175m cells)
        var result = await table.SpatialQueryAsync<H3StoreLocationSortKeyEntity>(
            locationSelector: s => s.Location,
            spatialIndexType: SpatialIndexType.H3,
            precision: 9,
            center: location,
            radiusKilometers: 0.5,
            queryBuilder: (query, cell, pagination) => query
                .Where<H3StoreLocationSortKeyEntity>(x => x.StoreId == "STORE" && x.Location == cell),
            pageSize: null
        );
        
        if (result.Items.Count == 0)
            return (false, 0, result.TotalCellsQueried);
        
        var retrieved = result.Items[0];
        var distance = retrieved.Location.DistanceToKilometers(location) * 1000; // Convert to meters
        
        return (true, distance, result.TotalCellsQueried);
    }
    
    #endregion
    
    #region 31.2 Test query performance comparison
    
    /// <summary>
    /// Task 31.2: Test query performance comparison.
    /// Executes the same spatial query with S2 and H3 and compares
    /// query times and cell counts.
    /// Uses 0.5km radius to stay within 500 cell limit for S2 level 16 (~71m cells).
    /// </summary>
    [Fact]
    public async Task SpatialQuery_PerformanceComparison_DocumentsCharacteristics()
    {
        var searchCenter = new GeoLocation(37.7749, -122.4194); // San Francisco
        var searchRadius = 0.5; // 0.5km radius to stay within 500 cell limit for S2 level 16
        var storeCount = 50; // Reduced store count for smaller radius
        
        Console.WriteLine("[Cross-Index Test 31.2] Performance comparison for spatial queries...");
        Console.WriteLine($"  Search center: ({searchCenter.Latitude:F4}, {searchCenter.Longitude:F4})");
        Console.WriteLine($"  Search radius: {searchRadius}km");
        Console.WriteLine($"  Store count: {storeCount}");
        
        // Test S2 performance
        var s2Results = await TestS2QueryPerformance(searchCenter, searchRadius, storeCount);
        
        // Test H3 performance
        var h3Results = await TestH3QueryPerformance(searchCenter, searchRadius, storeCount);
        
        // Log comparison
        Console.WriteLine("\n[Cross-Index Test 31.2] Performance Summary:");
        Console.WriteLine($"  S2: {s2Results.QueryTimeMs}ms, {s2Results.CellsQueried} cells, {s2Results.ResultCount} results");
        Console.WriteLine($"  H3: {h3Results.QueryTimeMs}ms, {h3Results.CellsQueried} cells, {h3Results.ResultCount} results");
        
        // Both should return results (exact counts may differ due to different cell geometries)
        // S2 and H3 have different cell shapes and sizes, so result counts can vary significantly
        s2Results.ResultCount.Should().BeGreaterThan(0, "S2 should return some results");
        h3Results.ResultCount.Should().BeGreaterThan(0, "H3 should return some results");
    }
    
    private async Task<(long QueryTimeMs, int CellsQueried, int ResultCount)> TestS2QueryPerformance(
        GeoLocation center, double radiusKm, int storeCount)
    {
        // Delete existing table if it exists before creating a new one
        try { await DynamoDb.DeleteTableAsync(TableName); await Task.Delay(500); } catch { }
        await CreateTableAsync<S2StoreEntity>();
        var table = new S2StoreTable(DynamoDb, TableName);
        
        var stores = GenerateS2Stores(center, radiusKm * 0.9, storeCount);
        foreach (var store in stores)
        {
            await table.PutAsync(store);
        }
        
        var stopwatch = Stopwatch.StartNew();
        var result = await table.SpatialQueryAsync<S2StoreEntity>(
            locationSelector: s => s.Location,
            spatialIndexType: SpatialIndexType.S2,
            precision: 16,
            center: center,
            radiusKilometers: radiusKm,
            queryBuilder: (query, cell, pagination) => query
                .Where<S2StoreEntity>(x => x.StoreId == "STORE" && x.Location == cell),
            pageSize: null
        );
        stopwatch.Stop();
        
        return (stopwatch.ElapsedMilliseconds, result.TotalCellsQueried, result.Items.Count);
    }
    
    private async Task<(long QueryTimeMs, int CellsQueried, int ResultCount)> TestH3QueryPerformance(
        GeoLocation center, double radiusKm, int storeCount)
    {
        // Delete existing table if it exists before creating a new one
        try { await DynamoDb.DeleteTableAsync(TableName); await Task.Delay(500); } catch { }
        await CreateTableAsync<H3StoreLocationSortKeyEntity>();
        var table = new H3StoreTable(DynamoDb, TableName);
        
        var stores = GenerateH3Stores(center, radiusKm * 0.9, storeCount);
        foreach (var store in stores)
        {
            await table.PutAsync(store);
        }
        
        var stopwatch = Stopwatch.StartNew();
        var result = await table.SpatialQueryAsync<H3StoreLocationSortKeyEntity>(
            locationSelector: s => s.Location,
            spatialIndexType: SpatialIndexType.H3,
            precision: 9,
            center: center,
            radiusKilometers: radiusKm,
            queryBuilder: (query, cell, pagination) => query
                .Where<H3StoreLocationSortKeyEntity>(x => x.StoreId == "STORE" && x.Location == cell),
            pageSize: null
        );
        stopwatch.Stop();
        
        return (stopwatch.ElapsedMilliseconds, result.TotalCellsQueried, result.Items.Count);
    }
    
    #endregion

    
    // NOTE: PrecisionComparison_HigherPrecisionMoreAccurate test was removed because its premise was flawed.
    // The test tried to query at different precision levels than the data was stored at, which doesn't work.
    // Cell count behavior at different precision levels is already covered by unit tests.
    
    #region Helper Methods
    
    private List<S2StoreEntity> GenerateS2Stores(GeoLocation center, double radiusKm, int count)
    {
        var stores = new List<S2StoreEntity>();
        
        for (var i = 0; i < count; i++)
        {
            var angle = i * 137.508;
            var distance = radiusKm * Math.Sqrt((double)i / count);
            distance = Math.Min(distance, radiusKm * 0.98);
            
            var angleRad = angle * Math.PI / 180.0;
            var latOffset = (distance * Math.Cos(angleRad)) / 111.0;
            var lonOffset = (distance * Math.Sin(angleRad)) / (111.0 * Math.Cos(center.Latitude * Math.PI / 180.0));
            
            stores.Add(new S2StoreEntity
            {
                StoreId = "STORE",
                Location = new GeoLocation(center.Latitude + latOffset, center.Longitude + lonOffset),
                Name = $"Store {i + 1}",
                Description = $"Test store at {distance:F2}km"
            });
        }
        
        return stores;
    }
    
    private List<H3StoreLocationSortKeyEntity> GenerateH3Stores(GeoLocation center, double radiusKm, int count)
    {
        var stores = new List<H3StoreLocationSortKeyEntity>();
        
        for (var i = 0; i < count; i++)
        {
            var angle = i * 137.508;
            var distance = radiusKm * Math.Sqrt((double)i / count);
            distance = Math.Min(distance, radiusKm * 0.98);
            
            var angleRad = angle * Math.PI / 180.0;
            var latOffset = (distance * Math.Cos(angleRad)) / 111.0;
            var lonOffset = (distance * Math.Sin(angleRad)) / (111.0 * Math.Cos(center.Latitude * Math.PI / 180.0));
            
            stores.Add(new H3StoreLocationSortKeyEntity
            {
                StoreId = "STORE",
                Location = new GeoLocation(center.Latitude + latOffset, center.Longitude + lonOffset),
                Name = $"Store {i + 1}",
                Description = $"Test store at {distance:F2}km"
            });
        }
        
        return stores;
    }
    
    #endregion
}
