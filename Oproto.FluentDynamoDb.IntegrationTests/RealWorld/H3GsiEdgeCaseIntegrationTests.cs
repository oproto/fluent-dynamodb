using Oproto.FluentDynamoDb.Attributes;
using Oproto.FluentDynamoDb.Geospatial;
using Oproto.FluentDynamoDb.IntegrationTests.Infrastructure;
using Oproto.FluentDynamoDb.IntegrationTests.TestEntities;
using Oproto.FluentDynamoDb.Pagination;

namespace Oproto.FluentDynamoDb.IntegrationTests.RealWorld;

/// <summary>
/// Integration tests for H3 spatial queries with edge cases like date line crossing and polar regions.
/// Uses GSI-based spatial indexing with low precision (resolution 5, ~8km cells) for edge case searches.
/// Uses 50km radius to stay within the 500 cell limit for H3 resolution 5.
/// </summary>
[Collection("DynamoDB Local")]
[Trait("Category", "Integration")]
[Trait("Feature", "Geospatial")]
[Trait("Feature", "H3")]
[Trait("Feature", "EdgeCases")]
[Trait("Feature", "GSI")]
public class H3GsiEdgeCaseIntegrationTests : IntegrationTestBase
{
    private const string GsiName = "h3-location-index";
    private const string GsiPartitionKeyAttribute = "h3_cell";
    private const string GsiSortKeyAttribute = "pk";
    
    // H3 resolution 5 = ~8km cells, appropriate for 50km radius searches (stays within 500 cell limit)
    private const int Precision = 5;
    
    public H3GsiEdgeCaseIntegrationTests(DynamoDbLocalFixture fixture) : base(fixture)
    {
    }
    
    /// <summary>
    /// Table wrapper for GSI-based spatial queries with low precision.
    /// </summary>
    private class H3StoreGsiTable : DynamoDbTableBase
    {
        public DynamoDbIndex LocationIndex { get; }
        
        public H3StoreGsiTable(IAmazonDynamoDB client, string tableName) 
            : base(client, tableName)
        {
            LocationIndex = new DynamoDbIndex(this, GsiName);
        }
        
        public async Task PutAsync(H3StoreWithGsiLowPrecisionEntity entity)
        {
            var item = H3StoreWithGsiLowPrecisionEntity.ToDynamoDb(entity);
            await DynamoDbClient.PutItemAsync(Name, item);
        }
    }
    
    #region Date Line Crossing Tests
    
    [Fact]
    public async Task SpatialQueryAsync_H3ProximityNonPaginated_CrossingDateLine_ReturnsStoresOnBothSides()
    {
        // Arrange
        await CreateTableWithGsiAsync<H3StoreWithGsiLowPrecisionEntity>(GsiName, GsiPartitionKeyAttribute, GsiSortKeyAttribute);
        var table = new H3StoreGsiTable(DynamoDb, TableName);
        
        // H3 resolution 5 (~8km cells) with 50km radius stays within 500 cell limit
        var searchCenter = new GeoLocation(0.0, 179.8);
        var radiusKm = 50.0;
        
        var stores = new[]
        {
            // West side stores (within 50km of search center at 179.8)
            new H3StoreWithGsiLowPrecisionEntity { StoreId = "STORE-W1", Category = "retail", Location = new GeoLocation(0.0, 179.9), Name = "West Side Store 1" },
            new H3StoreWithGsiLowPrecisionEntity { StoreId = "STORE-W2", Category = "retail", Location = new GeoLocation(0.1, 179.95), Name = "West Side Store 2" },
            new H3StoreWithGsiLowPrecisionEntity { StoreId = "STORE-W3", Category = "retail", Location = new GeoLocation(-0.1, 179.85), Name = "West Side Store 3" },
            
            // East side stores (across date line, within 50km)
            new H3StoreWithGsiLowPrecisionEntity { StoreId = "STORE-E1", Category = "retail", Location = new GeoLocation(0.0, -179.95), Name = "East Side Store 1" },
            new H3StoreWithGsiLowPrecisionEntity { StoreId = "STORE-E2", Category = "retail", Location = new GeoLocation(0.1, -179.9), Name = "East Side Store 2" },
            new H3StoreWithGsiLowPrecisionEntity { StoreId = "STORE-E3", Category = "retail", Location = new GeoLocation(-0.1, -179.85), Name = "East Side Store 3" },
            
            // Center store
            new H3StoreWithGsiLowPrecisionEntity { StoreId = "STORE-CENTER", Category = "retail", Location = new GeoLocation(0.0, 179.8), Name = "Center Store" },
            
            // Stores outside radius (more than 50km away)
            new H3StoreWithGsiLowPrecisionEntity { StoreId = "STORE-FAR-W", Category = "retail", Location = new GeoLocation(0.0, 179.0), Name = "Far West Store" },
            new H3StoreWithGsiLowPrecisionEntity { StoreId = "STORE-FAR-E", Category = "retail", Location = new GeoLocation(0.0, -179.0), Name = "Far East Store" }
        };
        
        foreach (var store in stores)
        {
            await table.PutAsync(store);
        }
        
        // Act
        var result = await table.LocationIndex.SpatialQueryAsync<H3StoreWithGsiLowPrecisionEntity>(
            locationSelector: store => store.Location,
            spatialIndexType: SpatialIndexType.H3,
            precision: Precision,
            center: searchCenter,
            radiusKilometers: radiusKm,
            queryBuilder: (query, cell, pagination) => query
                .Where("h3_cell = {0}", cell),
            pageSize: null
        );
        
        // Assert
        result.Items.Should().HaveCountGreaterThan(0, "should return stores near the date line");
        
        foreach (var store in result.Items)
        {
            var distance = store.Location.DistanceToKilometers(searchCenter);
            distance.Should().BeLessThanOrEqualTo(radiusKm, 
                $"Store {store.Name} should be within {radiusKm}km radius");
        }
        
        var storeNames = result.Items.Select(s => s.Name).ToList();
        var westSideStores = result.Items.Where(s => s.Name.Contains("West Side")).ToList();
        var eastSideStores = result.Items.Where(s => s.Name.Contains("East Side")).ToList();
        
        westSideStores.Should().NotBeEmpty("should return stores on the west side of date line");
        eastSideStores.Should().NotBeEmpty("should return stores on the east side of date line");
        
        storeNames.Should().NotContain("Far West Store");
        storeNames.Should().NotContain("Far East Store");
    }

    
    [Fact]
    public async Task SpatialQueryAsync_H3ProximityNonPaginated_CrossingDateLine_NoDuplicates()
    {
        // Arrange
        await CreateTableWithGsiAsync<H3StoreWithGsiLowPrecisionEntity>(GsiName, GsiPartitionKeyAttribute, GsiSortKeyAttribute);
        var table = new H3StoreGsiTable(DynamoDb, TableName);
        
        // H3 resolution 5 (~8km cells) with 50km radius stays within 500 cell limit
        var searchCenter = new GeoLocation(0.0, 180.0);
        var radiusKm = 50.0;
        
        var stores = new[]
        {
            // All stores within 50km of the date line (180°)
            new H3StoreWithGsiLowPrecisionEntity { StoreId = "STORE-A", Category = "retail", Location = new GeoLocation(0.0, 179.95), Name = "Store A" },
            new H3StoreWithGsiLowPrecisionEntity { StoreId = "STORE-B", Category = "retail", Location = new GeoLocation(0.0, -179.95), Name = "Store B" },
            new H3StoreWithGsiLowPrecisionEntity { StoreId = "STORE-C", Category = "retail", Location = new GeoLocation(0.15, 179.9), Name = "Store C" },
            new H3StoreWithGsiLowPrecisionEntity { StoreId = "STORE-D", Category = "retail", Location = new GeoLocation(0.15, -179.9), Name = "Store D" },
            new H3StoreWithGsiLowPrecisionEntity { StoreId = "STORE-E", Category = "retail", Location = new GeoLocation(-0.15, 179.9), Name = "Store E" },
            new H3StoreWithGsiLowPrecisionEntity { StoreId = "STORE-F", Category = "retail", Location = new GeoLocation(-0.15, -179.9), Name = "Store F" }
        };
        
        foreach (var store in stores)
        {
            await table.PutAsync(store);
        }
        
        // Act
        var result = await table.LocationIndex.SpatialQueryAsync<H3StoreWithGsiLowPrecisionEntity>(
            locationSelector: store => store.Location,
            spatialIndexType: SpatialIndexType.H3,
            precision: Precision,
            center: searchCenter,
            radiusKilometers: radiusKm,
            queryBuilder: (query, cell, pagination) => query
                .Where("h3_cell = {0}", cell),
            pageSize: null
        );
        
        // Assert
        result.Items.Should().NotBeEmpty("should return stores near date line");
        var storeIds = result.Items.Select(s => s.StoreId).ToList();
        storeIds.Should().OnlyHaveUniqueItems("each store should appear exactly once");
    }
    
    [Fact]
    public async Task SpatialQueryAsync_H3BoundingBox_CrossingDateLine_ReturnsStoresOnBothSides()
    {
        // Arrange
        await CreateTableWithGsiAsync<H3StoreWithGsiLowPrecisionEntity>(GsiName, GsiPartitionKeyAttribute, GsiSortKeyAttribute);
        var table = new H3StoreGsiTable(DynamoDb, TableName);
        
        // Smaller bounding box (~50km wide) to stay within 500 cell limit for H3 resolution 5
        var boundingBox = new GeoBoundingBox(
            southwest: new GeoLocation(-0.25, 179.75),
            northeast: new GeoLocation(0.25, -179.75)
        );
        
        var stores = new[]
        {
            // Inside the box (within ~50km of date line)
            new H3StoreWithGsiLowPrecisionEntity { StoreId = "IN-W1", Category = "retail", Location = new GeoLocation(0.0, 179.9), Name = "Inside West 1" },
            new H3StoreWithGsiLowPrecisionEntity { StoreId = "IN-W2", Category = "retail", Location = new GeoLocation(0.1, 179.8), Name = "Inside West 2" },
            new H3StoreWithGsiLowPrecisionEntity { StoreId = "IN-E1", Category = "retail", Location = new GeoLocation(0.0, -179.9), Name = "Inside East 1" },
            new H3StoreWithGsiLowPrecisionEntity { StoreId = "IN-E2", Category = "retail", Location = new GeoLocation(-0.1, -179.8), Name = "Inside East 2" },
            
            // Outside the box
            new H3StoreWithGsiLowPrecisionEntity { StoreId = "OUT-W", Category = "retail", Location = new GeoLocation(0.0, 179.0), Name = "Outside West" },
            new H3StoreWithGsiLowPrecisionEntity { StoreId = "OUT-E", Category = "retail", Location = new GeoLocation(0.0, -179.0), Name = "Outside East" }
        };
        
        foreach (var store in stores)
        {
            await table.PutAsync(store);
        }
        
        // Act
        var result = await table.LocationIndex.SpatialQueryAsync<H3StoreWithGsiLowPrecisionEntity>(
            locationSelector: store => store.Location,
            spatialIndexType: SpatialIndexType.H3,
            precision: Precision,
            boundingBox: boundingBox,
            queryBuilder: (query, cell, pagination) => query
                .Where("h3_cell = {0}", cell),
            pageSize: null
        );
        
        // Assert
        result.Items.Should().NotBeEmpty("should return stores within bounding box");
        
        var storeNames = result.Items.Select(s => s.Name).ToList();
        storeNames.Should().Contain(n => n.Contains("Inside West"));
        storeNames.Should().Contain(n => n.Contains("Inside East"));
        storeNames.Should().NotContain("Outside West");
        storeNames.Should().NotContain("Outside East");
    }
    
    #endregion
    
    #region North Pole Tests
    
    [Fact]
    public async Task SpatialQueryAsync_H3ProximityNonPaginated_NearNorthPole_ReturnsStoresWithinRadius()
    {
        // Arrange
        await CreateTableWithGsiAsync<H3StoreWithGsiLowPrecisionEntity>(GsiName, GsiPartitionKeyAttribute, GsiSortKeyAttribute);
        var table = new H3StoreGsiTable(DynamoDb, TableName);
        
        // H3 resolution 5 (~8km cells) with 50km radius stays within 500 cell limit
        var searchCenter = new GeoLocation(89.5, 0.0);
        var radiusKm = 50.0;
        
        var stores = new[]
        {
            // Stores within 50km of search center at 89.5°N
            new H3StoreWithGsiLowPrecisionEntity { StoreId = "POLE-CENTER", Category = "retail", Location = new GeoLocation(89.5, 0.0), Name = "Pole Center Store" },
            new H3StoreWithGsiLowPrecisionEntity { StoreId = "POLE-N1", Category = "retail", Location = new GeoLocation(89.7, 0.0), Name = "North Store 1" },
            new H3StoreWithGsiLowPrecisionEntity { StoreId = "POLE-S1", Category = "retail", Location = new GeoLocation(89.3, 0.0), Name = "South Store 1" },
            new H3StoreWithGsiLowPrecisionEntity { StoreId = "POLE-E1", Category = "retail", Location = new GeoLocation(89.5, 45.0), Name = "East Store 1" },
            new H3StoreWithGsiLowPrecisionEntity { StoreId = "POLE-E2", Category = "retail", Location = new GeoLocation(89.5, 90.0), Name = "East Store 2" },
            new H3StoreWithGsiLowPrecisionEntity { StoreId = "POLE-W1", Category = "retail", Location = new GeoLocation(89.5, -90.0), Name = "West Store 1" },
            new H3StoreWithGsiLowPrecisionEntity { StoreId = "POLE-W2", Category = "retail", Location = new GeoLocation(89.5, -45.0), Name = "West Store 2" },
            
            // Outside radius (more than 50km away)
            new H3StoreWithGsiLowPrecisionEntity { StoreId = "FAR-S1", Category = "retail", Location = new GeoLocation(88.5, 0.0), Name = "Far South Store" },
            new H3StoreWithGsiLowPrecisionEntity { StoreId = "FAR-S2", Category = "retail", Location = new GeoLocation(88.0, 0.0), Name = "Very Far South Store" }
        };
        
        foreach (var store in stores)
        {
            await table.PutAsync(store);
        }
        
        // Act
        var result = await table.LocationIndex.SpatialQueryAsync<H3StoreWithGsiLowPrecisionEntity>(
            locationSelector: store => store.Location,
            spatialIndexType: SpatialIndexType.H3,
            precision: Precision,
            center: searchCenter,
            radiusKilometers: radiusKm,
            queryBuilder: (query, cell, pagination) => query
                .Where("h3_cell = {0}", cell),
            pageSize: null
        );
        
        // Assert
        result.Items.Should().HaveCountGreaterThan(0, "should return stores near the North Pole");
        
        foreach (var store in result.Items)
        {
            var distance = store.Location.DistanceToKilometers(searchCenter);
            distance.Should().BeLessThanOrEqualTo(radiusKm, 
                $"Store {store.Name} should be within {radiusKm}km radius");
        }
        
        var storeNames = result.Items.Select(s => s.Name).ToList();
        storeNames.Should().Contain("Pole Center Store");
        storeNames.Should().NotContain("Far South Store");
        storeNames.Should().NotContain("Very Far South Store");
    }
    
    #endregion

    
    #region South Pole Tests
    
    [Fact]
    public async Task SpatialQueryAsync_H3ProximityNonPaginated_NearSouthPole_ReturnsStoresWithinRadius()
    {
        // Arrange
        await CreateTableWithGsiAsync<H3StoreWithGsiLowPrecisionEntity>(GsiName, GsiPartitionKeyAttribute, GsiSortKeyAttribute);
        var table = new H3StoreGsiTable(DynamoDb, TableName);
        
        // H3 resolution 5 (~8km cells) with 50km radius stays within 500 cell limit
        var searchCenter = new GeoLocation(-89.5, 0.0);
        var radiusKm = 50.0;
        
        var stores = new[]
        {
            // Stores within 50km of search center at -89.5°S
            new H3StoreWithGsiLowPrecisionEntity { StoreId = "SPOLE-CENTER", Category = "retail", Location = new GeoLocation(-89.5, 0.0), Name = "South Pole Center" },
            new H3StoreWithGsiLowPrecisionEntity { StoreId = "SPOLE-N1", Category = "retail", Location = new GeoLocation(-89.3, 0.0), Name = "North Store 1" },
            new H3StoreWithGsiLowPrecisionEntity { StoreId = "SPOLE-S1", Category = "retail", Location = new GeoLocation(-89.7, 0.0), Name = "South Store 1" },
            new H3StoreWithGsiLowPrecisionEntity { StoreId = "SPOLE-E1", Category = "retail", Location = new GeoLocation(-89.5, 45.0), Name = "East Store 1" },
            new H3StoreWithGsiLowPrecisionEntity { StoreId = "SPOLE-E2", Category = "retail", Location = new GeoLocation(-89.5, 90.0), Name = "East Store 2" },
            new H3StoreWithGsiLowPrecisionEntity { StoreId = "SPOLE-W1", Category = "retail", Location = new GeoLocation(-89.5, -90.0), Name = "West Store 1" },
            new H3StoreWithGsiLowPrecisionEntity { StoreId = "SPOLE-W2", Category = "retail", Location = new GeoLocation(-89.5, -45.0), Name = "West Store 2" },
            
            // Outside radius (more than 50km away)
            new H3StoreWithGsiLowPrecisionEntity { StoreId = "FAR-N1", Category = "retail", Location = new GeoLocation(-88.5, 0.0), Name = "Far North Store" },
            new H3StoreWithGsiLowPrecisionEntity { StoreId = "FAR-N2", Category = "retail", Location = new GeoLocation(-88.0, 0.0), Name = "Very Far North Store" }
        };
        
        foreach (var store in stores)
        {
            await table.PutAsync(store);
        }
        
        // Act
        var result = await table.LocationIndex.SpatialQueryAsync<H3StoreWithGsiLowPrecisionEntity>(
            locationSelector: store => store.Location,
            spatialIndexType: SpatialIndexType.H3,
            precision: Precision,
            center: searchCenter,
            radiusKilometers: radiusKm,
            queryBuilder: (query, cell, pagination) => query
                .Where("h3_cell = {0}", cell),
            pageSize: null
        );
        
        // Assert
        result.Items.Should().HaveCountGreaterThan(0, "should return stores near the South Pole");
        
        foreach (var store in result.Items)
        {
            var distance = store.Location.DistanceToKilometers(searchCenter);
            distance.Should().BeLessThanOrEqualTo(radiusKm, 
                $"Store {store.Name} should be within {radiusKm}km radius");
        }
        
        var storeNames = result.Items.Select(s => s.Name).ToList();
        storeNames.Should().Contain("South Pole Center");
        storeNames.Should().NotContain("Far North Store");
        storeNames.Should().NotContain("Very Far North Store");
    }
    
    [Fact]
    public async Task SpatialQueryAsync_H3ProximityPaginated_NearSouthPole_ReturnsStoresWithinRadius()
    {
        // Arrange
        await CreateTableWithGsiAsync<H3StoreWithGsiLowPrecisionEntity>(GsiName, GsiPartitionKeyAttribute, GsiSortKeyAttribute);
        var table = new H3StoreGsiTable(DynamoDb, TableName);
        
        // H3 resolution 5 (~8km cells) with 50km radius stays within 500 cell limit
        var searchCenter = new GeoLocation(-89.5, 0.0);
        var radiusKm = 50.0;
        
        // Create stores at various longitudes around the South Pole (within 50km)
        var stores = new List<H3StoreWithGsiLowPrecisionEntity>();
        for (int lonIdx = 0; lonIdx < 8; lonIdx++)
        {
            var lon = lonIdx * 45.0;
            if (lon > 180) lon -= 360; // Wrap to valid range
            
            stores.Add(new H3StoreWithGsiLowPrecisionEntity
            {
                StoreId = $"SPOLE-{lonIdx:D2}",
                Category = "retail",
                Location = new GeoLocation(-89.5, lon),
                Name = $"South Pole Store {lonIdx + 1}"
            });
        }
        
        foreach (var store in stores)
        {
            await table.PutAsync(store);
        }
        
        // Act - Paginated query
        var allResults = new List<H3StoreWithGsiLowPrecisionEntity>();
        SpatialContinuationToken? continuationToken = null;
        int pageCount = 0;
        const int maxPages = 20;
        
        do
        {
            var result = await table.LocationIndex.SpatialQueryAsync<H3StoreWithGsiLowPrecisionEntity>(
                locationSelector: store => store.Location,
                spatialIndexType: SpatialIndexType.H3,
                precision: Precision,
                center: searchCenter,
                radiusKilometers: radiusKm,
                queryBuilder: (query, cell, pagination) => query
                    .Where("h3_cell = {0}", cell),
                pageSize: 3,
                continuationToken: continuationToken
            );
            
            allResults.AddRange(result.Items);
            continuationToken = result.ContinuationToken;
            pageCount++;
        } while (continuationToken != null && pageCount < maxPages);
        
        // Assert
        allResults.Should().NotBeEmpty("should return stores from South Pole query");
        
        foreach (var store in allResults)
        {
            var distance = store.Location.DistanceToKilometers(searchCenter);
            distance.Should().BeLessThanOrEqualTo(radiusKm, 
                $"Store {store.Name} should be within {radiusKm}km radius");
        }
        
        var storeIds = allResults.Select(s => s.StoreId).ToList();
        storeIds.Should().OnlyHaveUniqueItems("should not have duplicate stores across pages");
    }
    
    #endregion
}
