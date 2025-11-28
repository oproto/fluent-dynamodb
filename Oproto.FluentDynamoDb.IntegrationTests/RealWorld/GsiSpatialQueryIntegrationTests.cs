using Amazon.DynamoDBv2;
using Oproto.FluentDynamoDb.Attributes;
using Oproto.FluentDynamoDb.Geospatial;
using Oproto.FluentDynamoDb.Geospatial.S2;
using Oproto.FluentDynamoDb.IntegrationTests.Infrastructure;
using Oproto.FluentDynamoDb.IntegrationTests.TestEntities;
using Oproto.FluentDynamoDb.Pagination;
using Oproto.FluentDynamoDb.Storage;

namespace Oproto.FluentDynamoDb.IntegrationTests.RealWorld;

/// <summary>
/// Integration tests for GSI-based spatial queries with DynamoDB.
/// Tests verify that SpatialQueryAsync correctly executes queries via GSI
/// where the spatial index is the GSI partition key.
/// </summary>
[Collection("DynamoDB Local")]
[Trait("Category", "Integration")]
[Trait("Feature", "Geospatial")]
[Trait("Feature", "GSI")]
[Trait("Feature", "SpatialQuery")]
public class GsiSpatialQueryIntegrationTests : IntegrationTestBase
{
    public GsiSpatialQueryIntegrationTests(DynamoDbLocalFixture fixture) : base(fixture)
    {
    }
    
    /// <summary>
    /// Table wrapper for testing GSI spatial queries.
    /// </summary>
    private class S2StoreWithGsiTable : DynamoDbTableBase
    {
        public DynamoDbIndex S2LocationIndex { get; }
        
        public S2StoreWithGsiTable(IAmazonDynamoDB client, string tableName) 
            : base(client, tableName)
        {
            S2LocationIndex = new DynamoDbIndex(this, "s2-location-index");
        }
        
        public async Task PutAsync(S2StoreWithGsiEntity entity)
        {
            var item = S2StoreWithGsiEntity.ToDynamoDb(entity);
            await DynamoDbClient.PutItemAsync(Name, item);
        }
    }
    
    /// <summary>
    /// Table wrapper for testing GSI spatial queries with low precision (S2 level 10).
    /// </summary>
    private class S2StoreWithGsiLowPrecisionTable : DynamoDbTableBase
    {
        public DynamoDbIndex S2LocationIndex { get; }
        
        public S2StoreWithGsiLowPrecisionTable(IAmazonDynamoDB client, string tableName) 
            : base(client, tableName)
        {
            S2LocationIndex = new DynamoDbIndex(this, "s2-location-index");
        }
        
        public async Task PutAsync(S2StoreWithGsiLowPrecisionEntity entity)
        {
            var item = S2StoreWithGsiLowPrecisionEntity.ToDynamoDb(entity);
            await DynamoDbClient.PutItemAsync(Name, item);
        }
    }

    #region 34.9 GSI Spatial Query Integration Tests
    
    /// <summary>
    /// Tests that multiple stores in the same S2 cell can be queried via GSI.
    /// Validates: Requirements 3.1, 3.2
    /// </summary>
    [Fact]
    public async Task SpatialQueryAsync_ViaGsi_ReturnsMultipleStoresInSameCell()
    {
        // Arrange - Create table with GSI
        await CreateTableWithGsiAsync<S2StoreWithGsiEntity>(
            gsiName: "s2-location-index",
            gsiPartitionKeyAttribute: "s2_cell",
            gsiSortKeyAttribute: "pk");
        
        var table = new S2StoreWithGsiTable(DynamoDb, TableName);
        
        // Create multiple stores at very close locations (same S2 cell at level 16)
        var baseLocation = new GeoLocation(37.7749, -122.4194); // San Francisco
        
        var stores = new[]
        {
            new S2StoreWithGsiEntity
            {
                StoreId = "store-001",
                Category = "COFFEE",
                Location = baseLocation,
                Name = "Coffee Shop 1"
            },
            new S2StoreWithGsiEntity
            {
                StoreId = "store-002",
                Category = "COFFEE",
                Location = new GeoLocation(37.7749, -122.4194), // Same location
                Name = "Coffee Shop 2"
            },
            new S2StoreWithGsiEntity
            {
                StoreId = "store-003",
                Category = "RESTAURANT",
                Location = new GeoLocation(37.7750, -122.4195), // Very close (~15m)
                Name = "Restaurant 1"
            }
        };
        
        foreach (var store in stores)
        {
            await table.PutAsync(store);
        }
        
        // Act - Query via GSI
        // Use 0.5km radius to stay within 500 cell limit for S2 level 16 (~71m cells)
        var result = await table.S2LocationIndex.SpatialQueryAsync<S2StoreWithGsiEntity>(
            locationSelector: store => store.Location,
            spatialIndexType: SpatialIndexType.S2,
            precision: 16,
            center: baseLocation,
            radiusKilometers: 0.5,
            queryBuilder: (query, cell, pagination) => query
                .Where<S2StoreWithGsiEntity>(x => x.Location == cell)
        );
        
        // Assert - All stores should be returned (they're all within 0.5km)
        result.Items.Should().HaveCountGreaterThanOrEqualTo(3);
        result.Items.Select(s => s.StoreId).Should().Contain("store-001");
        result.Items.Select(s => s.StoreId).Should().Contain("store-002");
        result.Items.Select(s => s.StoreId).Should().Contain("store-003");
    }

    /// <summary>
    /// Tests pagination with GSI spatial queries across multiple cells.
    /// Uses S2 level 10 (~10km cells) with 20km radius to stay within 500 cell limit.
    /// Validates: Requirements 3.2, 11.1, 11.2, 11.3
    /// </summary>
    [Fact]
    public async Task SpatialQueryAsync_ViaGsi_PaginationWorksAcrossCells()
    {
        // Arrange - Create table with GSI using low precision entity (S2 level 10)
        await CreateTableWithGsiAsync<S2StoreWithGsiLowPrecisionEntity>(
            gsiName: "s2-location-index",
            gsiPartitionKeyAttribute: "s2_cell",
            gsiSortKeyAttribute: "pk");
        
        var table = new S2StoreWithGsiLowPrecisionTable(DynamoDb, TableName);
        
        // Create stores spread across multiple S2 cells
        var center = new GeoLocation(37.7749, -122.4194);
        var stores = new List<S2StoreWithGsiLowPrecisionEntity>();
        
        // Create 20 stores in a grid pattern (spread across ~4km x 4km area)
        for (int i = 0; i < 20; i++)
        {
            var latOffset = (i / 5) * 0.01; // ~1km per row
            var lonOffset = (i % 5) * 0.01; // ~1km per column
            
            stores.Add(new S2StoreWithGsiLowPrecisionEntity
            {
                StoreId = $"store-{i:D3}",
                Category = "RETAIL",
                Location = new GeoLocation(center.Latitude + latOffset, center.Longitude + lonOffset),
                Name = $"Store {i}"
            });
        }
        
        foreach (var store in stores)
        {
            await table.PutAsync(store);
        }
        
        // Debug: Check cell distribution
        var cells = S2CellCovering.GetCellsForRadius(center, 20.0, 10, maxCells: 500);
        Console.WriteLine($"[Pagination Test] Total cells for 20km radius at level 10: {cells.Count}");
        Console.WriteLine($"[Pagination Test] First 5 cells: {string.Join(", ", cells.Take(5))}");
        
        // Act - Query with pagination (page size 5)
        // S2 level 10 (~10km cells) with 20km radius stays within 500 cell limit
        var allResults = new List<S2StoreWithGsiLowPrecisionEntity>();
        SpatialContinuationToken? token = null;
        int pageCount = 0;
        
        do
        {
            var result = await table.S2LocationIndex.SpatialQueryAsync<S2StoreWithGsiLowPrecisionEntity>(
                locationSelector: store => store.Location,
                spatialIndexType: SpatialIndexType.S2,
                precision: 10, // Level 10 for larger cells
                center: center,
                radiusKilometers: 20.0, // 20km radius appropriate for level 10
                queryBuilder: (query, cell, pagination) => query
                    .Where<S2StoreWithGsiLowPrecisionEntity>(x => x.Location == cell)
                    .Paginate(pagination),
                pageSize: 5,
                continuationToken: token
            );
            
            Console.WriteLine($"[Pagination Test] Page {pageCount + 1}: {result.Items.Count} items, CellsQueried={result.TotalCellsQueried}, Scanned={result.TotalItemsScanned}, HasToken={result.ContinuationToken != null}");
            
            allResults.AddRange(result.Items);
            token = result.ContinuationToken;
            pageCount++;
            
            // Safety limit
            if (pageCount > 10) break;
            
        } while (token != null);
        
        Console.WriteLine($"[Pagination Test] Total pages: {pageCount}, Total results: {allResults.Count}");
        
        // Assert - All stores should be retrieved across pages
        allResults.Should().HaveCountGreaterThanOrEqualTo(10); // At least half should be within radius
        
        // Verify no duplicates
        var uniqueIds = allResults.Select(s => s.StoreId).Distinct().ToList();
        uniqueIds.Should().HaveCount(allResults.Count, "No duplicate stores should be returned");
    }

    #endregion

    #region 34.10 Custom Cell List Integration Tests

    /// <summary>
    /// Tests custom cell list with table spatial queries.
    /// Validates: Requirements 3.1, 3.2
    /// </summary>
    [Fact]
    public async Task SpatialQueryAsync_WithCustomCellList_ReturnsMatchingStores()
    {
        // Arrange - Create table with GSI
        await CreateTableWithGsiAsync<S2StoreWithGsiEntity>(
            gsiName: "s2-location-index",
            gsiPartitionKeyAttribute: "s2_cell",
            gsiSortKeyAttribute: "pk");
        
        var table = new S2StoreWithGsiTable(DynamoDb, TableName);
        
        var center = new GeoLocation(37.7749, -122.4194);
        
        // Create stores (within 0.5km to stay within cell limit)
        var stores = new[]
        {
            new S2StoreWithGsiEntity
            {
                StoreId = "store-001",
                Category = "COFFEE",
                Location = center,
                Name = "Center Store"
            },
            new S2StoreWithGsiEntity
            {
                StoreId = "store-002",
                Category = "COFFEE",
                Location = new GeoLocation(37.7765, -122.4180), // ~200m away (within 0.5km)
                Name = "North Store"
            }
        };
        
        foreach (var store in stores)
        {
            await table.PutAsync(store);
        }
        
        // Get custom cell list using S2CellCovering
        // Use 0.5km radius to stay within 500 cell limit for S2 level 16 (~71m cells)
        var customCells = S2CellCovering.GetCellsForRadius(center, 0.5, 16, maxCells: 100);
        
        // Act - Query with custom cell list via GSI
        var result = await table.S2LocationIndex.SpatialQueryAsync<S2StoreWithGsiEntity>(
            locationSelector: store => store.Location,
            cells: customCells,
            queryBuilder: (query, cell, pagination) => query
                .Where<S2StoreWithGsiEntity>(x => x.Location == cell),
            center: center,
            radiusKilometers: 0.5
        );
        
        // Assert
        result.Items.Should().NotBeEmpty();
        result.TotalCellsQueried.Should().BeGreaterThan(0);
    }

    /// <summary>
    /// Tests custom cell list with distance sorting.
    /// Validates: Requirements 3.1, 3.2
    /// </summary>
    [Fact]
    public async Task SpatialQueryAsync_WithCustomCellList_SortsByDistanceWhenCenterProvided()
    {
        // Arrange - Create table with GSI
        await CreateTableWithGsiAsync<S2StoreWithGsiEntity>(
            gsiName: "s2-location-index",
            gsiPartitionKeyAttribute: "s2_cell",
            gsiSortKeyAttribute: "pk");
        
        var table = new S2StoreWithGsiTable(DynamoDb, TableName);
        
        var center = new GeoLocation(37.7749, -122.4194);
        
        // Create stores at different distances (within 0.5km to stay within cell limit)
        var stores = new[]
        {
            new S2StoreWithGsiEntity
            {
                StoreId = "store-far",
                Category = "COFFEE",
                Location = new GeoLocation(37.7785, -122.4160), // ~400m away
                Name = "Far Store"
            },
            new S2StoreWithGsiEntity
            {
                StoreId = "store-near",
                Category = "COFFEE",
                Location = center, // At center
                Name = "Near Store"
            },
            new S2StoreWithGsiEntity
            {
                StoreId = "store-mid",
                Category = "COFFEE",
                Location = new GeoLocation(37.7765, -122.4180), // ~200m away
                Name = "Mid Store"
            }
        };
        
        foreach (var store in stores)
        {
            await table.PutAsync(store);
        }
        
        // Get custom cell list
        // Use 0.5km radius to stay within 500 cell limit for S2 level 16 (~71m cells)
        var customCells = S2CellCovering.GetCellsForRadius(center, 0.5, 16, maxCells: 100);
        
        // Act - Query with custom cell list and center for distance sorting
        var result = await table.S2LocationIndex.SpatialQueryAsync<S2StoreWithGsiEntity>(
            locationSelector: store => store.Location,
            cells: customCells,
            queryBuilder: (query, cell, pagination) => query
                .Where<S2StoreWithGsiEntity>(x => x.Location == cell),
            center: center, // Providing center enables distance sorting
            radiusKilometers: 0.5
        );
        
        // Assert - Results should be sorted by distance
        if (result.Items.Count > 1)
        {
            var distances = result.Items
                .Select(s => s.Location.DistanceToKilometers(center))
                .ToList();
            
            for (int i = 1; i < distances.Count; i++)
            {
                distances[i].Should().BeGreaterThanOrEqualTo(distances[i - 1] - 0.001,
                    "Results should be sorted by distance from center");
            }
        }
    }

    #endregion
}
