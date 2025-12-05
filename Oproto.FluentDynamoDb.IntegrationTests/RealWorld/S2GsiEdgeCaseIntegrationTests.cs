using Oproto.FluentDynamoDb.Attributes;
using Oproto.FluentDynamoDb.Geospatial;
using Oproto.FluentDynamoDb.IntegrationTests.Infrastructure;
using Oproto.FluentDynamoDb.IntegrationTests.TestEntities;
using Oproto.FluentDynamoDb.Pagination;

namespace Oproto.FluentDynamoDb.IntegrationTests.RealWorld;

/// <summary>
/// Integration tests for S2 spatial queries with edge cases like date line crossing and polar regions.
/// Uses GSI-based spatial indexing with very low precision (level 8) for large-area searches (200km radius).
/// Level 8 cells are ~18km, which allows 200km radius searches within the 500 cell limit.
/// </summary>
[Collection("DynamoDB Local")]
[Trait("Category", "Integration")]
[Trait("Feature", "Geospatial")]
[Trait("Feature", "S2")]
[Trait("Feature", "EdgeCases")]
[Trait("Feature", "GSI")]
public class S2GsiEdgeCaseIntegrationTests : IntegrationTestBase
{
    private const string GsiName = "s2-location-index";
    private const string GsiPartitionKeyAttribute = "s2_cell";
    private const string GsiSortKeyAttribute = "pk";
    
    // S2 level 8 = ~18km cells, appropriate for 200km radius searches (requires ~380 cells)
    private const int Precision = 8;
    
    public S2GsiEdgeCaseIntegrationTests(DynamoDbLocalFixture fixture) : base(fixture)
    {
    }
    
    /// <summary>
    /// Table wrapper for GSI-based spatial queries with very low precision (level 8).
    /// </summary>
    private class S2StoreGsiTable : DynamoDbTableBase
    {
        public DynamoDbIndex LocationIndex { get; }
        
        public S2StoreGsiTable(IAmazonDynamoDB client, string tableName) 
            : base(client, tableName)
        {
            LocationIndex = new DynamoDbIndex(this, GsiName);
        }
        
        public async Task PutAsync(S2StoreWithGsiVeryLowPrecisionEntity entity)
        {
            var item = S2StoreWithGsiVeryLowPrecisionEntity.ToDynamoDb(entity);
            await DynamoDbClient.PutItemAsync(Name, item);
        }
    }

    
    #region Date Line Crossing Tests
    
    [Fact]
    public async Task SpatialQueryAsync_S2ProximityNonPaginated_CrossingDateLine_ReturnsStoresOnBothSides()
    {
        // Arrange - Create table with GSI for S2-indexed stores at low precision
        await CreateTableWithGsiAsync<S2StoreWithGsiVeryLowPrecisionEntity>(GsiName, GsiPartitionKeyAttribute, GsiSortKeyAttribute);
        var table = new S2StoreGsiTable(DynamoDb, TableName);
        
        // Search center at the equator, very close to the date line (longitude ~179.5°)
        // With 100km radius, this will cross the date line and include stores on both sides
        var searchCenter = new GeoLocation(0.0, 179.5);
        var radiusKm = 100.0; // 100km radius will cross the date line
        
        // Create stores on both sides of the date line with unique IDs
        var stores = new[]
        {
            // West side stores (positive longitude, near +180°)
            new S2StoreWithGsiVeryLowPrecisionEntity
            {
                StoreId = "STORE-W1",
                Category = "retail",
                Location = new GeoLocation(0.0, 179.5),
                Name = "West Side Store 1",
                Description = "Just west of date line"
            },
            new S2StoreWithGsiVeryLowPrecisionEntity
            {
                StoreId = "STORE-W2",
                Category = "retail",
                Location = new GeoLocation(0.5, 179.8),
                Name = "West Side Store 2",
                Description = "Northwest of center, west side"
            },
            new S2StoreWithGsiVeryLowPrecisionEntity
            {
                StoreId = "STORE-W3",
                Category = "retail",
                Location = new GeoLocation(-0.5, 179.7),
                Name = "West Side Store 3",
                Description = "Southwest of center, west side"
            },
            
            // East side stores (negative longitude, near -180°)
            new S2StoreWithGsiVeryLowPrecisionEntity
            {
                StoreId = "STORE-E1",
                Category = "retail",
                Location = new GeoLocation(0.0, -179.5),
                Name = "East Side Store 1",
                Description = "Just east of date line"
            },
            new S2StoreWithGsiVeryLowPrecisionEntity
            {
                StoreId = "STORE-E2",
                Category = "retail",
                Location = new GeoLocation(0.5, -179.8),
                Name = "East Side Store 2",
                Description = "Northwest of center, east side"
            },
            new S2StoreWithGsiVeryLowPrecisionEntity
            {
                StoreId = "STORE-E3",
                Category = "retail",
                Location = new GeoLocation(-0.5, -179.7),
                Name = "East Side Store 3",
                Description = "Southwest of center, east side"
            },
            
            // Center store
            new S2StoreWithGsiVeryLowPrecisionEntity
            {
                StoreId = "STORE-CENTER",
                Category = "retail",
                Location = new GeoLocation(0.0, 179.5),
                Name = "Center Store",
                Description = "At search center"
            },
            
            // Stores outside radius
            new S2StoreWithGsiVeryLowPrecisionEntity
            {
                StoreId = "STORE-FAR-W",
                Category = "retail",
                Location = new GeoLocation(0.0, 175.0),
                Name = "Far West Store",
                Description = "Too far west"
            },
            new S2StoreWithGsiVeryLowPrecisionEntity
            {
                StoreId = "STORE-FAR-E",
                Category = "retail",
                Location = new GeoLocation(0.0, -175.0),
                Name = "Far East Store",
                Description = "Too far east"
            }
        };
        
        foreach (var store in stores)
        {
            await table.PutAsync(store);
        }
        
        // Act - Execute SpatialQueryAsync on the GSI
        var result = await table.LocationIndex.SpatialQueryAsync<S2StoreWithGsiVeryLowPrecisionEntity>(
            locationSelector: store => store.Location,
            spatialIndexType: SpatialIndexType.S2,
            precision: Precision,
            center: searchCenter,
            radiusKilometers: radiusKm,
            queryBuilder: (query, cell, pagination) => query
                .Where("s2_cell = {0}", cell),
            pageSize: null
        );
        
        // Assert
        result.Items.Should().NotBeNull();
        result.Items.Should().HaveCountGreaterThan(0, "should return stores near the date line");
        
        // Verify all results are within radius
        foreach (var store in result.Items)
        {
            var distance = store.Location.DistanceToKilometers(searchCenter);
            distance.Should().BeLessThanOrEqualTo(radiusKm, 
                $"Store {store.Name} should be within {radiusKm}km radius");
        }
        
        // Verify stores from both sides of the date line are present
        var storeNames = result.Items.Select(s => s.Name).ToList();
        var westSideStores = result.Items.Where(s => s.Name.Contains("West Side")).ToList();
        var eastSideStores = result.Items.Where(s => s.Name.Contains("East Side")).ToList();
        
        westSideStores.Should().NotBeEmpty("should return stores on the west side of date line");
        eastSideStores.Should().NotBeEmpty("should return stores on the east side of date line");
        storeNames.Should().Contain("Center Store");
        
        // Verify stores outside radius are NOT present
        storeNames.Should().NotContain("Far West Store");
        storeNames.Should().NotContain("Far East Store");
    }
    
    [Fact]
    public async Task SpatialQueryAsync_S2ProximityNonPaginated_CrossingDateLine_NoDuplicates()
    {
        // Arrange
        await CreateTableWithGsiAsync<S2StoreWithGsiVeryLowPrecisionEntity>(GsiName, GsiPartitionKeyAttribute, GsiSortKeyAttribute);
        var table = new S2StoreGsiTable(DynamoDb, TableName);
        
        var searchCenter = new GeoLocation(0.0, 180.0);
        var radiusKm = 150.0;
        
        var stores = new[]
        {
            new S2StoreWithGsiVeryLowPrecisionEntity { StoreId = "STORE-A", Category = "retail", Location = new GeoLocation(0.0, 179.9), Name = "Store A" },
            new S2StoreWithGsiVeryLowPrecisionEntity { StoreId = "STORE-B", Category = "retail", Location = new GeoLocation(0.0, -179.9), Name = "Store B" },
            new S2StoreWithGsiVeryLowPrecisionEntity { StoreId = "STORE-C", Category = "retail", Location = new GeoLocation(0.5, 179.5), Name = "Store C" },
            new S2StoreWithGsiVeryLowPrecisionEntity { StoreId = "STORE-D", Category = "retail", Location = new GeoLocation(0.5, -179.5), Name = "Store D" },
            new S2StoreWithGsiVeryLowPrecisionEntity { StoreId = "STORE-E", Category = "retail", Location = new GeoLocation(-0.5, 179.5), Name = "Store E" },
            new S2StoreWithGsiVeryLowPrecisionEntity { StoreId = "STORE-F", Category = "retail", Location = new GeoLocation(-0.5, -179.5), Name = "Store F" }
        };
        
        foreach (var store in stores)
        {
            await table.PutAsync(store);
        }
        
        // Act
        var result = await table.LocationIndex.SpatialQueryAsync<S2StoreWithGsiVeryLowPrecisionEntity>(
            locationSelector: store => store.Location,
            spatialIndexType: SpatialIndexType.S2,
            precision: Precision,
            center: searchCenter,
            radiusKilometers: radiusKm,
            queryBuilder: (query, cell, pagination) => query
                .Where("s2_cell = {0}", cell),
            pageSize: null
        );
        
        // Assert - Verify no duplicates
        result.Items.Should().NotBeEmpty("should return stores near date line");
        var storeIds = result.Items.Select(s => s.StoreId).ToList();
        storeIds.Should().OnlyHaveUniqueItems("each store should appear exactly once");
    }

    
    [Fact]
    public async Task SpatialQueryAsync_S2BoundingBox_CrossingDateLine_ReturnsStoresOnBothSides()
    {
        // Arrange
        await CreateTableWithGsiAsync<S2StoreWithGsiVeryLowPrecisionEntity>(GsiName, GsiPartitionKeyAttribute, GsiSortKeyAttribute);
        var table = new S2StoreGsiTable(DynamoDb, TableName);
        
        // Bounding box crossing the date line: SW=(lat:-1, lon:178) to NE=(lat:1, lon:-178)
        var boundingBox = new GeoBoundingBox(
            southwest: new GeoLocation(-1.0, 178.0),
            northeast: new GeoLocation(1.0, -178.0)
        );
        
        var stores = new[]
        {
            // Inside the box - west side
            new S2StoreWithGsiVeryLowPrecisionEntity { StoreId = "IN-W1", Category = "retail", Location = new GeoLocation(0.0, 179.0), Name = "Inside West 1" },
            new S2StoreWithGsiVeryLowPrecisionEntity { StoreId = "IN-W2", Category = "retail", Location = new GeoLocation(0.5, 178.5), Name = "Inside West 2" },
            
            // Inside the box - east side
            new S2StoreWithGsiVeryLowPrecisionEntity { StoreId = "IN-E1", Category = "retail", Location = new GeoLocation(0.0, -179.0), Name = "Inside East 1" },
            new S2StoreWithGsiVeryLowPrecisionEntity { StoreId = "IN-E2", Category = "retail", Location = new GeoLocation(-0.5, -178.5), Name = "Inside East 2" },
            
            // Outside the box
            new S2StoreWithGsiVeryLowPrecisionEntity { StoreId = "OUT-W", Category = "retail", Location = new GeoLocation(0.0, 170.0), Name = "Outside West" },
            new S2StoreWithGsiVeryLowPrecisionEntity { StoreId = "OUT-E", Category = "retail", Location = new GeoLocation(0.0, -170.0), Name = "Outside East" },
            new S2StoreWithGsiVeryLowPrecisionEntity { StoreId = "OUT-N", Category = "retail", Location = new GeoLocation(2.0, 179.0), Name = "Outside North" },
            new S2StoreWithGsiVeryLowPrecisionEntity { StoreId = "OUT-S", Category = "retail", Location = new GeoLocation(-2.0, -179.0), Name = "Outside South" }
        };
        
        foreach (var store in stores)
        {
            await table.PutAsync(store);
        }
        
        // Act
        var result = await table.LocationIndex.SpatialQueryAsync<S2StoreWithGsiVeryLowPrecisionEntity>(
            locationSelector: store => store.Location,
            spatialIndexType: SpatialIndexType.S2,
            precision: Precision,
            boundingBox: boundingBox,
            queryBuilder: (query, cell, pagination) => query
                .Where("s2_cell = {0}", cell),
            pageSize: null
        );
        
        // Assert
        result.Items.Should().NotBeEmpty("should return stores within bounding box");
        
        var storeNames = result.Items.Select(s => s.Name).ToList();
        var westSideStores = result.Items.Where(s => s.Name.Contains("Inside West")).ToList();
        var eastSideStores = result.Items.Where(s => s.Name.Contains("Inside East")).ToList();
        
        westSideStores.Should().NotBeEmpty("should return stores inside box on west side");
        eastSideStores.Should().NotBeEmpty("should return stores inside box on east side");
        
        storeNames.Should().NotContain("Outside West");
        storeNames.Should().NotContain("Outside East");
        storeNames.Should().NotContain("Outside North");
        storeNames.Should().NotContain("Outside South");
        
        storeNames.Should().OnlyHaveUniqueItems("each store should appear exactly once");
    }
    
    #endregion
    
    #region North Pole Tests
    
    [Fact]
    public async Task SpatialQueryAsync_S2ProximityNonPaginated_NearNorthPole_ReturnsStoresWithinRadius()
    {
        // Arrange
        await CreateTableWithGsiAsync<S2StoreWithGsiVeryLowPrecisionEntity>(GsiName, GsiPartitionKeyAttribute, GsiSortKeyAttribute);
        var table = new S2StoreGsiTable(DynamoDb, TableName);
        
        // Search center near the North Pole (latitude ~89°)
        var searchCenter = new GeoLocation(89.0, 0.0);
        var radiusKm = 100.0;
        
        // At 89° latitude, longitude convergence is significant
        var stores = new[]
        {
            new S2StoreWithGsiVeryLowPrecisionEntity { StoreId = "POLE-CENTER", Category = "retail", Location = new GeoLocation(89.0, 0.0), Name = "Pole Center Store" },
            new S2StoreWithGsiVeryLowPrecisionEntity { StoreId = "POLE-N1", Category = "retail", Location = new GeoLocation(89.5, 0.0), Name = "North Store 1" },
            new S2StoreWithGsiVeryLowPrecisionEntity { StoreId = "POLE-S1", Category = "retail", Location = new GeoLocation(88.5, 0.0), Name = "South Store 1" },
            
            // Stores at different longitudes (testing longitude convergence)
            new S2StoreWithGsiVeryLowPrecisionEntity { StoreId = "POLE-E1", Category = "retail", Location = new GeoLocation(89.0, 45.0), Name = "East Store 1" },
            new S2StoreWithGsiVeryLowPrecisionEntity { StoreId = "POLE-E2", Category = "retail", Location = new GeoLocation(89.0, 90.0), Name = "East Store 2" },
            new S2StoreWithGsiVeryLowPrecisionEntity { StoreId = "POLE-OPP", Category = "retail", Location = new GeoLocation(89.0, 180.0), Name = "Opposite Store" },
            new S2StoreWithGsiVeryLowPrecisionEntity { StoreId = "POLE-W1", Category = "retail", Location = new GeoLocation(89.0, -90.0), Name = "West Store 1" },
            new S2StoreWithGsiVeryLowPrecisionEntity { StoreId = "POLE-W2", Category = "retail", Location = new GeoLocation(89.0, -45.0), Name = "West Store 2" },
            
            // Stores outside the radius (100km = ~0.9° latitude)
            new S2StoreWithGsiVeryLowPrecisionEntity { StoreId = "FAR-S1", Category = "retail", Location = new GeoLocation(88.0, 0.0), Name = "Far South Store" },
            new S2StoreWithGsiVeryLowPrecisionEntity { StoreId = "FAR-S2", Category = "retail", Location = new GeoLocation(87.5, 0.0), Name = "Very Far South Store" },
            new S2StoreWithGsiVeryLowPrecisionEntity { StoreId = "FAR-S3", Category = "retail", Location = new GeoLocation(87.0, 0.0), Name = "Extremely Far Store" }
        };
        
        foreach (var store in stores)
        {
            await table.PutAsync(store);
        }
        
        // Act
        var result = await table.LocationIndex.SpatialQueryAsync<S2StoreWithGsiVeryLowPrecisionEntity>(
            locationSelector: store => store.Location,
            spatialIndexType: SpatialIndexType.S2,
            precision: Precision,
            center: searchCenter,
            radiusKilometers: radiusKm,
            queryBuilder: (query, cell, pagination) => query
                .Where("s2_cell = {0}", cell),
            pageSize: null
        );
        
        // Assert
        result.Items.Should().HaveCountGreaterThan(0, "should return stores near the North Pole");
        
        foreach (var store in result.Items)
        {
            var distance = store.Location.DistanceToKilometers(searchCenter);
            distance.Should().BeLessThanOrEqualTo(radiusKm, 
                $"Store {store.Name} should be within {radiusKm}km radius. Actual: {distance:F2}km");
        }
        
        var storeNames = result.Items.Select(s => s.Name).ToList();
        storeNames.Should().Contain("Pole Center Store");
        
        // Stores at different longitudes should be present (longitude convergence test)
        var eastWestStores = result.Items.Where(s => 
            s.Name.Contains("East Store") || s.Name.Contains("West Store") || s.Name.Contains("Opposite Store")
        ).ToList();
        eastWestStores.Should().NotBeEmpty("should return stores at various longitudes");
        
        // Verify stores outside radius are NOT present
        storeNames.Should().NotContain("Far South Store");
        storeNames.Should().NotContain("Very Far South Store");
        storeNames.Should().NotContain("Extremely Far Store");
        
        storeNames.Should().OnlyHaveUniqueItems("each store should appear exactly once");
    }
    
    #endregion

    
    #region South Pole Tests
    
    [Fact]
    public async Task SpatialQueryAsync_S2ProximityNonPaginated_NearSouthPole_ReturnsStoresWithinRadius()
    {
        // Arrange
        await CreateTableWithGsiAsync<S2StoreWithGsiVeryLowPrecisionEntity>(GsiName, GsiPartitionKeyAttribute, GsiSortKeyAttribute);
        var table = new S2StoreGsiTable(DynamoDb, TableName);
        
        // Search center near the South Pole (latitude ~-89°)
        var searchCenter = new GeoLocation(-89.0, 0.0);
        var radiusKm = 100.0;
        
        var stores = new[]
        {
            new S2StoreWithGsiVeryLowPrecisionEntity { StoreId = "SPOLE-CENTER", Category = "retail", Location = new GeoLocation(-89.0, 0.0), Name = "South Pole Center" },
            new S2StoreWithGsiVeryLowPrecisionEntity { StoreId = "SPOLE-N1", Category = "retail", Location = new GeoLocation(-88.5, 0.0), Name = "North Store 1" },
            new S2StoreWithGsiVeryLowPrecisionEntity { StoreId = "SPOLE-S1", Category = "retail", Location = new GeoLocation(-89.5, 0.0), Name = "South Store 1" },
            
            // Stores at different longitudes (testing longitude convergence)
            new S2StoreWithGsiVeryLowPrecisionEntity { StoreId = "SPOLE-E1", Category = "retail", Location = new GeoLocation(-89.0, 45.0), Name = "East Store 1" },
            new S2StoreWithGsiVeryLowPrecisionEntity { StoreId = "SPOLE-E2", Category = "retail", Location = new GeoLocation(-89.0, 90.0), Name = "East Store 2" },
            new S2StoreWithGsiVeryLowPrecisionEntity { StoreId = "SPOLE-OPP", Category = "retail", Location = new GeoLocation(-89.0, 180.0), Name = "Opposite Store" },
            new S2StoreWithGsiVeryLowPrecisionEntity { StoreId = "SPOLE-W1", Category = "retail", Location = new GeoLocation(-89.0, -90.0), Name = "West Store 1" },
            new S2StoreWithGsiVeryLowPrecisionEntity { StoreId = "SPOLE-W2", Category = "retail", Location = new GeoLocation(-89.0, -45.0), Name = "West Store 2" },
            
            // Stores outside the radius (100km = ~0.9° latitude)
            new S2StoreWithGsiVeryLowPrecisionEntity { StoreId = "FAR-N1", Category = "retail", Location = new GeoLocation(-88.0, 0.0), Name = "Far North Store" },
            new S2StoreWithGsiVeryLowPrecisionEntity { StoreId = "FAR-N2", Category = "retail", Location = new GeoLocation(-87.5, 0.0), Name = "Very Far North Store" }
        };
        
        foreach (var store in stores)
        {
            await table.PutAsync(store);
        }
        
        // Act
        var result = await table.LocationIndex.SpatialQueryAsync<S2StoreWithGsiVeryLowPrecisionEntity>(
            locationSelector: store => store.Location,
            spatialIndexType: SpatialIndexType.S2,
            precision: Precision,
            center: searchCenter,
            radiusKilometers: radiusKm,
            queryBuilder: (query, cell, pagination) => query
                .Where("s2_cell = {0}", cell),
            pageSize: null
        );
        
        // Assert
        result.Items.Should().HaveCountGreaterThan(0, "should return stores near the South Pole");
        
        foreach (var store in result.Items)
        {
            var distance = store.Location.DistanceToKilometers(searchCenter);
            distance.Should().BeLessThanOrEqualTo(radiusKm, 
                $"Store {store.Name} should be within {radiusKm}km radius. Actual: {distance:F2}km");
        }
        
        var storeNames = result.Items.Select(s => s.Name).ToList();
        storeNames.Should().Contain("South Pole Center");
        
        // Verify stores outside radius are NOT present
        storeNames.Should().NotContain("Far North Store");
        storeNames.Should().NotContain("Very Far North Store");
        
        storeNames.Should().OnlyHaveUniqueItems("each store should appear exactly once");
    }
    
    [Fact]
    public async Task SpatialQueryAsync_S2ProximityPaginated_NearSouthPole_ReturnsStoresWithinRadius()
    {
        // Arrange
        await CreateTableWithGsiAsync<S2StoreWithGsiVeryLowPrecisionEntity>(GsiName, GsiPartitionKeyAttribute, GsiSortKeyAttribute);
        var table = new S2StoreGsiTable(DynamoDb, TableName);
        
        var searchCenter = new GeoLocation(-89.0, 0.0);
        var radiusKm = 100.0;
        
        // Create stores at various longitudes around the South Pole
        var stores = new List<S2StoreWithGsiVeryLowPrecisionEntity>();
        for (int lonIdx = 0; lonIdx < 8; lonIdx++)
        {
            var lon = lonIdx * 45.0;
            if (lon > 180) lon -= 360; // Wrap to valid range
            
            stores.Add(new S2StoreWithGsiVeryLowPrecisionEntity
            {
                StoreId = $"SPOLE-{lonIdx:D2}",
                Category = "retail",
                Location = new GeoLocation(-89.0, lon),
                Name = $"South Pole Store {lonIdx + 1}"
            });
        }
        
        foreach (var store in stores)
        {
            await table.PutAsync(store);
        }
        
        // Act - Paginated query
        var allResults = new List<S2StoreWithGsiVeryLowPrecisionEntity>();
        SpatialContinuationToken? continuationToken = null;
        int pageCount = 0;
        const int maxPages = 20;
        
        do
        {
            var result = await table.LocationIndex.SpatialQueryAsync<S2StoreWithGsiVeryLowPrecisionEntity>(
                locationSelector: store => store.Location,
                spatialIndexType: SpatialIndexType.S2,
                precision: Precision,
                center: searchCenter,
                radiusKilometers: radiusKm,
                queryBuilder: (query, cell, pagination) => query
                    .Where("s2_cell = {0}", cell),
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
