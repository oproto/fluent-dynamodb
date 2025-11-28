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
/// Integration tests for S2 spatial queries using GSI-based spatial indexing.
/// This approach properly supports multiple items per S2 cell by using:
/// - Main table: PK=StoreId (unique), SK=Category
/// - GSI (s2-location-index): PK=S2Cell, SK=StoreId
/// </summary>
[Collection("DynamoDB Local")]
[Trait("Category", "Integration")]
[Trait("Feature", "Geospatial")]
[Trait("Feature", "S2")]
[Trait("Feature", "SpatialQuery")]
[Trait("Feature", "GSI")]
public class S2GsiSpatialQueryIntegrationTests : IntegrationTestBase
{
    private const string GsiName = "s2-location-index";
    private const string GsiPartitionKeyAttribute = "s2_cell";
    private const string GsiSortKeyAttribute = "pk";
    
    public S2GsiSpatialQueryIntegrationTests(DynamoDbLocalFixture fixture) : base(fixture)
    {
    }
    
    /// <summary>
    /// Table wrapper for GSI-based spatial queries.
    /// </summary>
    private class S2StoreGsiTable : DynamoDbTableBase
    {
        public DynamoDbIndex LocationIndex { get; }
        
        public S2StoreGsiTable(IAmazonDynamoDB client, string tableName) 
            : base(client, tableName)
        {
            LocationIndex = new DynamoDbIndex(this, GsiName);
        }
        
        public async Task PutAsync(S2StoreWithGsiEntity entity)
        {
            var item = S2StoreWithGsiEntity.ToDynamoDb(entity);
            await DynamoDbClient.PutItemAsync(Name, item);
        }
    }
    
    #region Proximity Query Tests
    
    [Fact]
    public async Task SpatialQueryAsync_S2ProximityNonPaginated_ReturnsAllResultsWithinRadius()
    {
        // Arrange - Create table with GSI for S2-indexed stores
        await CreateTableWithGsiAsync<S2StoreWithGsiEntity>(GsiName, GsiPartitionKeyAttribute, GsiSortKeyAttribute);
        var table = new S2StoreGsiTable(DynamoDb, TableName);
        
        // San Francisco downtown area - search within 0.5km radius (appropriate for S2 level 16 ~71m cells)
        var searchCenter = new GeoLocation(37.7749, -122.4194);
        
        // Create stores at various distances - all within 0.5km to ensure they're in the cell covering
        var stores = new[]
        {
            // Within 0.5km radius - clustered very close together
            new S2StoreWithGsiEntity
            {
                StoreId = "STORE-001",
                Category = "retail",
                Location = new GeoLocation(37.7749, -122.4194), // 0km - at center
                Name = "Downtown Store",
                Description = "At search center"
            },
            new S2StoreWithGsiEntity
            {
                StoreId = "STORE-002",
                Category = "retail",
                Location = new GeoLocation(37.7752, -122.4197), // ~0.04km
                Name = "Mission Store",
                Description = "Mission District"
            },
            new S2StoreWithGsiEntity
            {
                StoreId = "STORE-003",
                Category = "retail",
                Location = new GeoLocation(37.7746, -122.4191), // ~0.04km
                Name = "Marina Store",
                Description = "Marina District"
            },
            new S2StoreWithGsiEntity
            {
                StoreId = "STORE-004",
                Category = "retail",
                Location = new GeoLocation(37.7755, -122.4200), // ~0.08km
                Name = "Haight Store",
                Description = "Haight-Ashbury"
            },
            // Outside 0.5km radius
            new S2StoreWithGsiEntity
            {
                StoreId = "STORE-005",
                Category = "retail",
                Location = new GeoLocation(37.7849, -122.4094), // ~1.4km northeast
                Name = "Oakland Store",
                Description = "Oakland"
            },
            new S2StoreWithGsiEntity
            {
                StoreId = "STORE-006",
                Category = "retail",
                Location = new GeoLocation(37.7649, -122.4294), // ~1.4km southwest
                Name = "Daly City Store",
                Description = "Daly City"
            }
        };
        
        // Write all stores to DynamoDB
        foreach (var store in stores)
        {
            await table.PutAsync(store);
        }
        
        // Act - Execute SpatialQueryAsync on the GSI
        var result = await table.LocationIndex.SpatialQueryAsync<S2StoreWithGsiEntity>(
            locationSelector: store => store.Location,
            spatialIndexType: SpatialIndexType.S2,
            precision: 16,
            center: searchCenter,
            radiusKilometers: 0.5,
            queryBuilder: (query, cell, pagination) => query
                .Where("s2_cell = {0}", cell),
            pageSize: null
        );
        
        // Assert - Verify all results are within radius
        result.Items.Should().NotBeNull();
        result.Items.Should().HaveCount(4, "4 stores are within 0.5km radius");
        
        // Verify each result is within the radius
        foreach (var store in result.Items)
        {
            var distance = store.Location.DistanceToKilometers(searchCenter);
            distance.Should().BeLessThanOrEqualTo(0.5, 
                $"Store {store.Name} should be within 0.5km radius");
        }
        
        // Verify the stores within radius are present
        var storeNames = result.Items.Select(s => s.Name).ToList();
        storeNames.Should().Contain("Downtown Store");
        storeNames.Should().Contain("Mission Store");
        storeNames.Should().Contain("Marina Store");
        storeNames.Should().Contain("Haight Store");
        
        // Verify the stores outside radius are NOT present
        storeNames.Should().NotContain("Oakland Store");
        storeNames.Should().NotContain("Daly City Store");
    }
    
    [Fact]
    public async Task SpatialQueryAsync_S2ProximityNonPaginated_ResultsAreSortedByDistance()
    {
        // Arrange
        await CreateTableWithGsiAsync<S2StoreWithGsiEntity>(GsiName, GsiPartitionKeyAttribute, GsiSortKeyAttribute);
        var table = new S2StoreGsiTable(DynamoDb, TableName);
        
        var searchCenter = new GeoLocation(37.7749, -122.4194);
        
        // Use stores within 0.5km radius (appropriate for S2 level 16 ~71m cells)
        var stores = new[]
        {
            new S2StoreWithGsiEntity
            {
                StoreId = "STORE-FAR",
                Category = "retail",
                Location = new GeoLocation(37.7755, -122.4200), // ~0.08km
                Name = "Far Store"
            },
            new S2StoreWithGsiEntity
            {
                StoreId = "STORE-CENTER",
                Category = "retail",
                Location = new GeoLocation(37.7749, -122.4194), // 0km
                Name = "Center Store"
            },
            new S2StoreWithGsiEntity
            {
                StoreId = "STORE-NEAR",
                Category = "retail",
                Location = new GeoLocation(37.7751, -122.4196), // ~0.03km
                Name = "Near Store"
            }
        };
        
        foreach (var store in stores)
        {
            await table.PutAsync(store);
        }
        
        // Act
        var result = await table.LocationIndex.SpatialQueryAsync<S2StoreWithGsiEntity>(
            locationSelector: store => store.Location,
            spatialIndexType: SpatialIndexType.S2,
            precision: 16,
            center: searchCenter,
            radiusKilometers: 0.5,
            queryBuilder: (query, cell, pagination) => query
                .Where("s2_cell = {0}", cell),
            pageSize: null
        );
        
        // Assert - Verify results are sorted by distance
        result.Items.Should().HaveCount(3);
        
        var distances = result.Items
            .Select(s => new { Store = s, Distance = s.Location.DistanceToKilometers(searchCenter) })
            .ToList();
        
        // Verify distances are in ascending order
        for (int i = 1; i < distances.Count; i++)
        {
            distances[i].Distance.Should().BeGreaterThanOrEqualTo(distances[i - 1].Distance,
                "results should be sorted by distance in ascending order");
        }
    }
    
    [Fact]
    public async Task SpatialQueryAsync_S2ProximityNonPaginated_NoDuplicates()
    {
        // Arrange
        await CreateTableWithGsiAsync<S2StoreWithGsiEntity>(GsiName, GsiPartitionKeyAttribute, GsiSortKeyAttribute);
        var table = new S2StoreGsiTable(DynamoDb, TableName);
        
        var searchCenter = new GeoLocation(37.7749, -122.4194);
        
        // Create stores at various locations - all very close to center within 0.5km radius (appropriate for S2 level 16)
        var stores = new[]
        {
            new S2StoreWithGsiEntity { StoreId = "STORE-1", Category = "retail", Location = new GeoLocation(37.7749, -122.4194), Name = "Store 1" },
            new S2StoreWithGsiEntity { StoreId = "STORE-2", Category = "retail", Location = new GeoLocation(37.7752, -122.4197), Name = "Store 2" },
            new S2StoreWithGsiEntity { StoreId = "STORE-3", Category = "retail", Location = new GeoLocation(37.7746, -122.4191), Name = "Store 3" },
            new S2StoreWithGsiEntity { StoreId = "STORE-4", Category = "retail", Location = new GeoLocation(37.7751, -122.4195), Name = "Store 4" }
        };
        
        foreach (var store in stores)
        {
            await table.PutAsync(store);
        }
        
        // Act - Use 0.5km radius (appropriate for S2 level 16 ~71m cells)
        var result = await table.LocationIndex.SpatialQueryAsync<S2StoreWithGsiEntity>(
            locationSelector: store => store.Location,
            spatialIndexType: SpatialIndexType.S2,
            precision: 16,
            center: searchCenter,
            radiusKilometers: 0.5,
            queryBuilder: (query, cell, pagination) => query
                .Where("s2_cell = {0}", cell),
            pageSize: null
        );
        
        // Assert - Verify no duplicates (stores should appear exactly once)
        var storeIds = result.Items.Select(s => s.StoreId).ToList();
        storeIds.Should().OnlyHaveUniqueItems("each store should appear exactly once");
        
        // Verify we got all 4 stores
        result.Items.Should().HaveCount(4, "all 4 stores should be found within 0.5km radius");
    }

    
    [Fact]
    public async Task SpatialQueryAsync_S2ProximityNonPaginated_ReturnsQueryStatistics()
    {
        // Arrange
        await CreateTableWithGsiAsync<S2StoreWithGsiEntity>(GsiName, GsiPartitionKeyAttribute, GsiSortKeyAttribute);
        var table = new S2StoreGsiTable(DynamoDb, TableName);
        
        var searchCenter = new GeoLocation(37.7749, -122.4194);
        
        // Use stores within 0.5km radius (appropriate for S2 level 16 ~71m cells)
        var stores = new[]
        {
            new S2StoreWithGsiEntity { StoreId = "STORE-1", Category = "retail", Location = new GeoLocation(37.7749, -122.4194), Name = "Store 1" },
            new S2StoreWithGsiEntity { StoreId = "STORE-2", Category = "retail", Location = new GeoLocation(37.7752, -122.4197), Name = "Store 2" }
        };
        
        foreach (var store in stores)
        {
            await table.PutAsync(store);
        }
        
        // Act
        var result = await table.LocationIndex.SpatialQueryAsync<S2StoreWithGsiEntity>(
            locationSelector: store => store.Location,
            spatialIndexType: SpatialIndexType.S2,
            precision: 16,
            center: searchCenter,
            radiusKilometers: 0.5,
            queryBuilder: (query, cell, pagination) => query
                .Where("s2_cell = {0}", cell),
            pageSize: null
        );
        
        // Assert
        result.TotalCellsQueried.Should().BeGreaterThan(0, "should report the number of S2 cells queried");
        result.TotalItemsScanned.Should().BeGreaterThanOrEqualTo(result.Items.Count);
    }
    
    #endregion
    
    #region Paginated Query Tests
    
    [Fact]
    public async Task SpatialQueryAsync_S2ProximityPaginated_RespectsPageSize()
    {
        // Arrange
        await CreateTableWithGsiAsync<S2StoreWithGsiEntity>(GsiName, GsiPartitionKeyAttribute, GsiSortKeyAttribute);
        var table = new S2StoreGsiTable(DynamoDb, TableName);
        
        var searchCenter = new GeoLocation(37.7749, -122.4194);
        
        // Create multiple stores within 0.5km radius (appropriate for S2 level 16 ~71m cells)
        for (int i = 0; i < 10; i++)
        {
            var store = new S2StoreWithGsiEntity
            {
                StoreId = $"STORE-{i:D3}",
                Category = "retail",
                Location = new GeoLocation(37.7749 + (i * 0.0001), -122.4194 + (i * 0.0001)), // ~11m apart
                Name = $"Store {i}"
            };
            await table.PutAsync(store);
        }
        
        // Act - Request page size of 3
        var result = await table.LocationIndex.SpatialQueryAsync<S2StoreWithGsiEntity>(
            locationSelector: store => store.Location,
            spatialIndexType: SpatialIndexType.S2,
            precision: 16,
            center: searchCenter,
            radiusKilometers: 0.5,
            queryBuilder: (query, cell, pagination) => query
                .Where("s2_cell = {0}", cell),
            pageSize: 3
        );
        
        // Assert
        result.Items.Count.Should().BeLessThanOrEqualTo(3, "should respect page size");
    }
    
    [Fact]
    public async Task SpatialQueryAsync_S2ProximityPaginated_ReturnsContinuationToken()
    {
        // Arrange
        await CreateTableWithGsiAsync<S2StoreWithGsiEntity>(GsiName, GsiPartitionKeyAttribute, GsiSortKeyAttribute);
        var table = new S2StoreGsiTable(DynamoDb, TableName);
        
        var searchCenter = new GeoLocation(37.7749, -122.4194);
        
        // Create multiple stores within 0.5km radius (appropriate for S2 level 16 ~71m cells)
        for (int i = 0; i < 20; i++)
        {
            var store = new S2StoreWithGsiEntity
            {
                StoreId = $"STORE-{i:D3}",
                Category = "retail",
                Location = new GeoLocation(37.7749 + (i * 0.0002), -122.4194 + (i * 0.0002)), // ~22m apart
                Name = $"Store {i}"
            };
            await table.PutAsync(store);
        }
        
        // Act - Request small page size
        var result = await table.LocationIndex.SpatialQueryAsync<S2StoreWithGsiEntity>(
            locationSelector: store => store.Location,
            spatialIndexType: SpatialIndexType.S2,
            precision: 16,
            center: searchCenter,
            radiusKilometers: 0.5,
            queryBuilder: (query, cell, pagination) => query
                .Where("s2_cell = {0}", cell),
            pageSize: 5
        );
        
        // Assert - Should have continuation token if more results exist
        if (result.Items.Count == 5)
        {
            result.ContinuationToken.Should().NotBeNull("should return continuation token when more results exist");
        }
    }
    
    [Fact]
    public async Task SpatialQueryAsync_S2ProximityPaginated_CanIterateThroughAllPages()
    {
        // Arrange
        await CreateTableWithGsiAsync<S2StoreWithGsiEntity>(GsiName, GsiPartitionKeyAttribute, GsiSortKeyAttribute);
        var table = new S2StoreGsiTable(DynamoDb, TableName);
        
        var searchCenter = new GeoLocation(37.7749, -122.4194);
        
        // Create stores within 0.5km radius (appropriate for S2 level 16 ~71m cells)
        var expectedStoreIds = new HashSet<string>();
        for (int i = 0; i < 15; i++)
        {
            var storeId = $"STORE-{i:D3}";
            expectedStoreIds.Add(storeId);
            var store = new S2StoreWithGsiEntity
            {
                StoreId = storeId,
                Category = "retail",
                Location = new GeoLocation(37.7749 + (i * 0.0002), -122.4194 + (i * 0.0002)), // ~22m apart
                Name = $"Store {i}"
            };
            await table.PutAsync(store);
        }
        
        // Act - Iterate through all pages
        var allItems = new List<S2StoreWithGsiEntity>();
        SpatialContinuationToken? continuationToken = null;
        var pageCount = 0;
        const int maxPages = 20;
        
        do
        {
            var result = await table.LocationIndex.SpatialQueryAsync<S2StoreWithGsiEntity>(
                locationSelector: store => store.Location,
                spatialIndexType: SpatialIndexType.S2,
                precision: 16,
                center: searchCenter,
                radiusKilometers: 0.5,
                queryBuilder: (query, cell, pagination) => query
                    .Where("s2_cell = {0}", cell),
                pageSize: 5,
                continuationToken: continuationToken
            );
            
            allItems.AddRange(result.Items);
            continuationToken = result.ContinuationToken;
            pageCount++;
        } while (continuationToken != null && pageCount < maxPages);
        
        // Assert - Should have retrieved all stores
        var retrievedStoreIds = allItems.Select(s => s.StoreId).ToHashSet();
        retrievedStoreIds.Should().BeEquivalentTo(expectedStoreIds, "should retrieve all stores across pages");
    }
    
    #endregion
    
    #region Bounding Box Query Tests
    
    [Fact]
    public async Task SpatialQueryAsync_S2BoundingBoxNonPaginated_ReturnsAllResultsWithinBoundingBox()
    {
        // Arrange
        await CreateTableWithGsiAsync<S2StoreWithGsiEntity>(GsiName, GsiPartitionKeyAttribute, GsiSortKeyAttribute);
        var table = new S2StoreGsiTable(DynamoDb, TableName);
        
        // Define small bounding box (~0.5km x 0.5km) appropriate for S2 level 16 (~71m cells)
        var boundingBox = new GeoBoundingBox(
            southwest: new GeoLocation(37.7725, -122.4220),
            northeast: new GeoLocation(37.7775, -122.4170)
        );
        
        var stores = new[]
        {
            // Inside bounding box
            new S2StoreWithGsiEntity { StoreId = "STORE-IN-1", Category = "retail", Location = new GeoLocation(37.7749, -122.4194), Name = "Downtown" },
            new S2StoreWithGsiEntity { StoreId = "STORE-IN-2", Category = "retail", Location = new GeoLocation(37.7740, -122.4200), Name = "West SF" },
            new S2StoreWithGsiEntity { StoreId = "STORE-IN-3", Category = "retail", Location = new GeoLocation(37.7760, -122.4185), Name = "North SF" },
            // Outside bounding box
            new S2StoreWithGsiEntity { StoreId = "STORE-OUT-1", Category = "retail", Location = new GeoLocation(37.7700, -122.4194), Name = "South" },
            new S2StoreWithGsiEntity { StoreId = "STORE-OUT-2", Category = "retail", Location = new GeoLocation(37.7749, -122.4100), Name = "East" }
        };
        
        foreach (var store in stores)
        {
            await table.PutAsync(store);
        }
        
        // Act
        var result = await table.LocationIndex.SpatialQueryAsync<S2StoreWithGsiEntity>(
            locationSelector: store => store.Location,
            spatialIndexType: SpatialIndexType.S2,
            precision: 16,
            boundingBox: boundingBox,
            queryBuilder: (query, cell, pagination) => query
                .Where("s2_cell = {0}", cell),
            pageSize: null
        );
        
        // Assert
        result.Items.Should().HaveCount(3, "3 stores are inside the bounding box");
        
        var storeNames = result.Items.Select(s => s.Name).ToList();
        storeNames.Should().Contain("Downtown");
        storeNames.Should().Contain("West SF");
        storeNames.Should().Contain("North SF");
        storeNames.Should().NotContain("South");
        storeNames.Should().NotContain("East");
    }
    
    #endregion
}
