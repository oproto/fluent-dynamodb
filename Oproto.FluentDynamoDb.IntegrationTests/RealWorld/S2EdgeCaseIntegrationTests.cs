using Amazon.DynamoDBv2;
using Oproto.FluentDynamoDb.Attributes;
using Oproto.FluentDynamoDb.Geospatial;
using Oproto.FluentDynamoDb.IntegrationTests.Infrastructure;
using Oproto.FluentDynamoDb.IntegrationTests.TestEntities;
using Oproto.FluentDynamoDb.Storage;

namespace Oproto.FluentDynamoDb.IntegrationTests.RealWorld;

/// <summary>
/// Integration tests for S2 spatial queries with edge cases like date line crossing and polar regions.
/// Tests verify that SpatialQueryAsync correctly handles geographic edge cases.
/// </summary>
[Collection("DynamoDB Local")]
[Trait("Category", "Integration")]
[Trait("Feature", "Geospatial")]
[Trait("Feature", "S2")]
[Trait("Feature", "EdgeCases")]
public class S2EdgeCaseIntegrationTests : IntegrationTestBase
{
    public S2EdgeCaseIntegrationTests(DynamoDbLocalFixture fixture) : base(fixture)
    {
    }
    
    /// <summary>
    /// Simple table wrapper for testing spatial queries.
    /// </summary>
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
    
    #region 29.1 Test S2 query crossing date line
    
    [Fact]
    public async Task SpatialQueryAsync_S2ProximityNonPaginated_CrossingDateLine_ReturnsStoresOnBothSides()
    {
        // Arrange - Create table with S2-indexed stores near the International Date Line
        await CreateTableAsync<S2StoreEntity>();
        var table = new S2StoreTable(DynamoDb, TableName);
        
        // Search center at the equator, just west of the date line (longitude ~179°)
        // This will create a search area that crosses the date line
        var searchCenter = new GeoLocation(0.0, 179.0);
        var radiusKm = 200.0; // 200km radius will definitely cross the date line
        
        // Create stores on both sides of the date line
        var stores = new[]
        {
            // Stores on the western side (positive longitude, near +180°)
            new S2StoreEntity
            {
                StoreId = "STORE",
                Location = new GeoLocation(0.0, 179.5), // ~55km east of center, west side of date line
                Name = "West Side Store 1",
                Description = "Just west of date line"
            },
            new S2StoreEntity
            {
                StoreId = "STORE",
                Location = new GeoLocation(0.5, 179.8), // ~88km northeast, west side
                Name = "West Side Store 2",
                Description = "Northwest of center, west side"
            },
            new S2StoreEntity
            {
                StoreId = "STORE",
                Location = new GeoLocation(-0.5, 179.7), // ~77km southeast, west side
                Name = "West Side Store 3",
                Description = "Southwest of center, west side"
            },
            
            // Stores on the eastern side (negative longitude, near -180°)
            new S2StoreEntity
            {
                StoreId = "STORE",
                Location = new GeoLocation(0.0, -179.5), // ~55km west of center, east side of date line
                Name = "East Side Store 1",
                Description = "Just east of date line"
            },
            new S2StoreEntity
            {
                StoreId = "STORE",
                Location = new GeoLocation(0.5, -179.8), // ~88km northwest, east side
                Name = "East Side Store 2",
                Description = "Northwest of center, east side"
            },
            new S2StoreEntity
            {
                StoreId = "STORE",
                Location = new GeoLocation(-0.5, -179.7), // ~77km southwest, east side
                Name = "East Side Store 3",
                Description = "Southwest of center, east side"
            },
            
            // Store at the center (on the date line)
            new S2StoreEntity
            {
                StoreId = "STORE",
                Location = new GeoLocation(0.0, 179.0), // At search center
                Name = "Center Store",
                Description = "At search center, near date line"
            },
            
            // Stores outside the radius (should not be returned)
            new S2StoreEntity
            {
                StoreId = "STORE",
                Location = new GeoLocation(0.0, 175.0), // ~445km west, outside radius
                Name = "Far West Store",
                Description = "Too far west"
            },
            new S2StoreEntity
            {
                StoreId = "STORE",
                Location = new GeoLocation(0.0, -175.0), // ~445km east, outside radius
                Name = "Far East Store",
                Description = "Too far east"
            }
        };
        
        // Write all stores to DynamoDB
        foreach (var store in stores)
        {
            await table.PutAsync(store);
        }
        
        // Act - Execute SpatialQueryAsync with radius search centered near date line
        var result = await table.SpatialQueryAsync<S2StoreEntity>(
            locationSelector: store => store.Location,
            spatialIndexType: SpatialIndexType.S2,
            precision: 16, // S2 Level 16 (~600m cells)
            center: searchCenter,
            radiusKilometers: radiusKm,
            queryBuilder: (query, cell, pagination) => query
                .Where<S2StoreEntity>(x => x.StoreId == "STORE" && x.Location == cell),
            pageSize: null // Non-paginated mode - query all cells in parallel
        );
        
        // Assert - Verify stores on both sides of date line are returned
        result.Items.Should().NotBeNull();
        result.Items.Should().HaveCountGreaterThan(0, "should return stores near the date line");
        
        // Verify all results are within radius
        foreach (var store in result.Items)
        {
            var distance = store.Location.DistanceToKilometers(searchCenter);
            distance.Should().BeLessThanOrEqualTo(radiusKm, 
                $"Store {store.Name} at {store.Location} should be within {radiusKm}km radius");
        }
        
        // Verify stores from both sides of the date line are present
        var storeNames = result.Items.Select(s => s.Name).ToList();
        
        // Check for stores on the west side (positive longitude)
        var westSideStores = result.Items.Where(s => s.Name.Contains("West Side")).ToList();
        westSideStores.Should().NotBeEmpty("should return stores on the west side of date line");
        
        // Check for stores on the east side (negative longitude)
        var eastSideStores = result.Items.Where(s => s.Name.Contains("East Side")).ToList();
        eastSideStores.Should().NotBeEmpty("should return stores on the east side of date line");
        
        // Verify the center store is present
        storeNames.Should().Contain("Center Store", "center store should be returned");
        
        // Verify stores outside radius are NOT present
        storeNames.Should().NotContain("Far West Store", "stores outside radius should not be returned");
        storeNames.Should().NotContain("Far East Store", "stores outside radius should not be returned");
    }
    
    [Fact]
    public async Task SpatialQueryAsync_S2ProximityNonPaginated_CrossingDateLine_NoDuplicates()
    {
        // Arrange - Create table with S2-indexed stores near the date line
        await CreateTableAsync<S2StoreEntity>();
        var table = new S2StoreTable(DynamoDb, TableName);
        
        // Search center at the equator, on the date line
        var searchCenter = new GeoLocation(0.0, 180.0);
        var radiusKm = 150.0;
        
        // Create stores that might appear in multiple S2 cells due to date line crossing
        var stores = new[]
        {
            new S2StoreEntity
            {
                StoreId = "STORE",
                Location = new GeoLocation(0.0, 179.9),
                Name = "Store A",
                Description = "Near date line west"
            },
            new S2StoreEntity
            {
                StoreId = "STORE",
                Location = new GeoLocation(0.0, -179.9),
                Name = "Store B",
                Description = "Near date line east"
            },
            new S2StoreEntity
            {
                StoreId = "STORE",
                Location = new GeoLocation(0.5, 179.5),
                Name = "Store C",
                Description = "North of date line west"
            },
            new S2StoreEntity
            {
                StoreId = "STORE",
                Location = new GeoLocation(0.5, -179.5),
                Name = "Store D",
                Description = "North of date line east"
            },
            new S2StoreEntity
            {
                StoreId = "STORE",
                Location = new GeoLocation(-0.5, 179.5),
                Name = "Store E",
                Description = "South of date line west"
            },
            new S2StoreEntity
            {
                StoreId = "STORE",
                Location = new GeoLocation(-0.5, -179.5),
                Name = "Store F",
                Description = "South of date line east"
            }
        };
        
        foreach (var store in stores)
        {
            await table.PutAsync(store);
        }
        
        // Act - Execute spatial query crossing date line
        var result = await table.SpatialQueryAsync<S2StoreEntity>(
            locationSelector: store => store.Location,
            spatialIndexType: SpatialIndexType.S2,
            precision: 16,
            center: searchCenter,
            radiusKilometers: radiusKm,
            queryBuilder: (query, cell, pagination) => query
                .Where<S2StoreEntity>(x => x.StoreId == "STORE" && x.Location == cell),
            pageSize: null
        );
        
        // Assert - Verify no duplicates
        result.Items.Should().NotBeNull();
        result.Items.Should().NotBeEmpty("should return stores near date line");
        
        // Verify each store appears exactly once by checking unique names
        var names = result.Items.Select(s => s.Name).ToList();
        names.Should().OnlyHaveUniqueItems("each store should appear exactly once, no duplicates");
        
        // Verify all results are within radius
        foreach (var store in result.Items)
        {
            var distance = store.Location.DistanceToKilometers(searchCenter);
            distance.Should().BeLessThanOrEqualTo(radiusKm, 
                $"Store {store.Name} should be within {radiusKm}km radius");
        }
    }
    
    [Fact]
    public async Task SpatialQueryAsync_S2ProximityPaginated_CrossingDateLine_ReturnsStoresOnBothSides()
    {
        // Arrange - Create table with S2-indexed stores near the date line
        await CreateTableAsync<S2StoreEntity>();
        var table = new S2StoreTable(DynamoDb, TableName);
        
        // Search center near the date line
        var searchCenter = new GeoLocation(0.0, 179.0);
        var radiusKm = 200.0;
        
        // Create multiple stores on both sides of the date line
        var stores = new List<S2StoreEntity>();
        
        // West side stores (positive longitude)
        for (int i = 0; i < 10; i++)
        {
            var latOffset = (i % 4 - 1.5) * 0.5;
            var lonOffset = (i / 4) * 0.3;
            stores.Add(new S2StoreEntity
            {
                StoreId = "STORE",
                Location = new GeoLocation(latOffset, 179.0 + lonOffset),
                Name = $"West Store {i + 1:D2}",
                Description = $"West side store {i + 1}"
            });
        }
        
        // East side stores (negative longitude)
        for (int i = 0; i < 10; i++)
        {
            var latOffset = (i % 4 - 1.5) * 0.5;
            var lonOffset = (i / 4) * 0.3;
            stores.Add(new S2StoreEntity
            {
                StoreId = "STORE",
                Location = new GeoLocation(latOffset, -180.0 + lonOffset),
                Name = $"East Store {i + 1:D2}",
                Description = $"East side store {i + 1}"
            });
        }
        
        foreach (var store in stores)
        {
            await table.PutAsync(store);
        }
        
        // Act - Execute paginated spatial query crossing date line
        var allResults = new List<S2StoreEntity>();
        SpatialContinuationToken? continuationToken = null;
        int pageCount = 0;
        int maxPages = 20; // Safety limit
        
        do
        {
            var result = await table.SpatialQueryAsync<S2StoreEntity>(
                locationSelector: store => store.Location,
                spatialIndexType: SpatialIndexType.S2,
                precision: 16,
                center: searchCenter,
                radiusKilometers: radiusKm,
                queryBuilder: (query, cell, pagination) => query
                    .Where<S2StoreEntity>(x => x.StoreId == "STORE" && x.Location == cell),
                pageSize: 5, // Small page size to test pagination
                continuationToken: continuationToken
            );
            
            allResults.AddRange(result.Items);
            continuationToken = result.ContinuationToken;
            pageCount++;
            
            if (pageCount >= maxPages)
            {
                break;
            }
        }
        while (continuationToken != null);
        
        // Assert - Verify stores from both sides are returned
        allResults.Should().NotBeEmpty("should return stores from date line query");
        
        // Verify stores from both sides of the date line are present
        var westSideStores = allResults.Where(s => s.Name.Contains("West Store")).ToList();
        var eastSideStores = allResults.Where(s => s.Name.Contains("East Store")).ToList();
        
        westSideStores.Should().NotBeEmpty("should return stores on the west side of date line");
        eastSideStores.Should().NotBeEmpty("should return stores on the east side of date line");
        
        // Verify no duplicates across pages
        var uniqueNames = allResults.Select(s => s.Name).Distinct().ToList();
        uniqueNames.Count.Should().Be(allResults.Count, 
            "should not have duplicate stores across pages");
        
        // Verify all results are within radius
        foreach (var store in allResults)
        {
            var distance = store.Location.DistanceToKilometers(searchCenter);
            distance.Should().BeLessThanOrEqualTo(radiusKm, 
                $"Store {store.Name} should be within {radiusKm}km radius");
        }
    }
    
    [Fact]
    public async Task SpatialQueryAsync_S2BoundingBox_CrossingDateLine_ReturnsStoresOnBothSides()
    {
        // Arrange - Create table with S2-indexed stores near the date line
        await CreateTableAsync<S2StoreEntity>();
        var table = new S2StoreTable(DynamoDb, TableName);
        
        // Create a bounding box that crosses the date line
        // Southwest: (lat: -1°, lon: 178°) - west side
        // Northeast: (lat: 1°, lon: -178°) - east side
        // This box crosses the date line (178° to -178° wraps around)
        var boundingBox = new GeoBoundingBox(
            southwest: new GeoLocation(-1.0, 178.0),
            northeast: new GeoLocation(1.0, -178.0)
        );
        
        // Create stores inside and outside the bounding box
        var stores = new[]
        {
            // Inside the box - west side
            new S2StoreEntity
            {
                StoreId = "STORE",
                Location = new GeoLocation(0.0, 179.0),
                Name = "Inside West 1",
                Description = "Inside box, west side"
            },
            new S2StoreEntity
            {
                StoreId = "STORE",
                Location = new GeoLocation(0.5, 178.5),
                Name = "Inside West 2",
                Description = "Inside box, west side"
            },
            
            // Inside the box - east side
            new S2StoreEntity
            {
                StoreId = "STORE",
                Location = new GeoLocation(0.0, -179.0),
                Name = "Inside East 1",
                Description = "Inside box, east side"
            },
            new S2StoreEntity
            {
                StoreId = "STORE",
                Location = new GeoLocation(-0.5, -178.5),
                Name = "Inside East 2",
                Description = "Inside box, east side"
            },
            
            // Outside the box
            new S2StoreEntity
            {
                StoreId = "STORE",
                Location = new GeoLocation(0.0, 170.0),
                Name = "Outside West",
                Description = "Outside box, too far west"
            },
            new S2StoreEntity
            {
                StoreId = "STORE",
                Location = new GeoLocation(0.0, -170.0),
                Name = "Outside East",
                Description = "Outside box, too far east"
            },
            new S2StoreEntity
            {
                StoreId = "STORE",
                Location = new GeoLocation(2.0, 179.0),
                Name = "Outside North",
                Description = "Outside box, too far north"
            },
            new S2StoreEntity
            {
                StoreId = "STORE",
                Location = new GeoLocation(-2.0, -179.0),
                Name = "Outside South",
                Description = "Outside box, too far south"
            }
        };
        
        foreach (var store in stores)
        {
            await table.PutAsync(store);
        }
        
        // Act - Execute bounding box query crossing date line
        var result = await table.SpatialQueryAsync<S2StoreEntity>(
            locationSelector: store => store.Location,
            spatialIndexType: SpatialIndexType.S2,
            precision: 16,
            boundingBox: boundingBox,
            queryBuilder: (query, cell, pagination) => query
                .Where<S2StoreEntity>(x => x.StoreId == "STORE" && x.Location == cell),
            pageSize: null
        );
        
        // Assert - Verify stores from both sides of date line are returned
        result.Items.Should().NotBeNull();
        result.Items.Should().NotBeEmpty("should return stores within bounding box");
        
        var storeNames = result.Items.Select(s => s.Name).ToList();
        
        // Verify stores inside the box on both sides are present
        var westSideStores = result.Items.Where(s => s.Name.Contains("Inside West")).ToList();
        var eastSideStores = result.Items.Where(s => s.Name.Contains("Inside East")).ToList();
        
        westSideStores.Should().NotBeEmpty("should return stores inside box on west side");
        eastSideStores.Should().NotBeEmpty("should return stores inside box on east side");
        
        // Verify stores outside the box are NOT present
        storeNames.Should().NotContain("Outside West");
        storeNames.Should().NotContain("Outside East");
        storeNames.Should().NotContain("Outside North");
        storeNames.Should().NotContain("Outside South");
        
        // Verify no duplicates
        storeNames.Should().OnlyHaveUniqueItems("each store should appear exactly once");
    }
    
    #endregion
    
    #region 29.3 Test S2 query near North Pole
    
    [Fact]
    public async Task SpatialQueryAsync_S2ProximityNonPaginated_NearNorthPole_ReturnsStoresWithinRadius()
    {
        // Arrange - Create table with S2-indexed stores near the North Pole
        await CreateTableAsync<S2StoreEntity>();
        var table = new S2StoreTable(DynamoDb, TableName);
        
        // Search center near the North Pole (latitude ~89°)
        // At this latitude, longitude convergence is significant
        var searchCenter = new GeoLocation(89.0, 0.0);
        var radiusKm = 200.0; // 200km radius
        
        // Create stores near the North Pole at various longitudes
        // At 89° latitude, 1° of longitude is only ~2km (vs ~111km at equator)
        var stores = new[]
        {
            // Stores within radius at different longitudes
            new S2StoreEntity
            {
                StoreId = "STORE",
                Location = new GeoLocation(89.0, 0.0), // At search center
                Name = "Pole Center Store",
                Description = "At search center"
            },
            new S2StoreEntity
            {
                StoreId = "STORE",
                Location = new GeoLocation(89.5, 0.0), // ~55km north
                Name = "North Store 1",
                Description = "Directly north of center"
            },
            new S2StoreEntity
            {
                StoreId = "STORE",
                Location = new GeoLocation(88.5, 0.0), // ~55km south
                Name = "South Store 1",
                Description = "Directly south of center"
            },
            
            // Stores at different longitudes (testing longitude convergence)
            // At 89° latitude, longitude differences represent very small distances
            new S2StoreEntity
            {
                StoreId = "STORE",
                Location = new GeoLocation(89.0, 45.0), // ~50km east (longitude convergence)
                Name = "East Store 1",
                Description = "East at 45° longitude"
            },
            new S2StoreEntity
            {
                StoreId = "STORE",
                Location = new GeoLocation(89.0, 90.0), // ~50km east (longitude convergence)
                Name = "East Store 2",
                Description = "East at 90° longitude"
            },
            new S2StoreEntity
            {
                StoreId = "STORE",
                Location = new GeoLocation(89.0, 180.0), // ~100km opposite side
                Name = "Opposite Store",
                Description = "Opposite side at 180° longitude"
            },
            new S2StoreEntity
            {
                StoreId = "STORE",
                Location = new GeoLocation(89.0, -90.0), // ~50km west (longitude convergence)
                Name = "West Store 1",
                Description = "West at -90° longitude"
            },
            new S2StoreEntity
            {
                StoreId = "STORE",
                Location = new GeoLocation(89.0, -45.0), // ~50km west (longitude convergence)
                Name = "West Store 2",
                Description = "West at -45° longitude"
            },
            
            // Stores at the edge of the radius
            new S2StoreEntity
            {
                StoreId = "STORE",
                Location = new GeoLocation(87.2, 0.0), // ~200km south (at edge)
                Name = "Edge Store South",
                Description = "At southern edge of radius"
            },
            new S2StoreEntity
            {
                StoreId = "STORE",
                Location = new GeoLocation(89.0, 135.0), // ~75km away
                Name = "Edge Store East",
                Description = "East at 135° longitude"
            },
            
            // Stores outside the radius (should not be returned)
            new S2StoreEntity
            {
                StoreId = "STORE",
                Location = new GeoLocation(87.0, 0.0), // ~222km south, outside radius
                Name = "Far South Store",
                Description = "Too far south"
            },
            new S2StoreEntity
            {
                StoreId = "STORE",
                Location = new GeoLocation(86.5, 0.0), // ~278km south, outside radius
                Name = "Very Far South Store",
                Description = "Very far south"
            },
            new S2StoreEntity
            {
                StoreId = "STORE",
                Location = new GeoLocation(85.0, 0.0), // ~445km south, outside radius
                Name = "Extremely Far Store",
                Description = "Extremely far south"
            }
        };
        
        // Write all stores to DynamoDB
        foreach (var store in stores)
        {
            await table.PutAsync(store);
        }
        
        // Act - Execute SpatialQueryAsync with radius search near North Pole
        var result = await table.SpatialQueryAsync<S2StoreEntity>(
            locationSelector: store => store.Location,
            spatialIndexType: SpatialIndexType.S2,
            precision: 16, // S2 Level 16 (~600m cells)
            center: searchCenter,
            radiusKilometers: radiusKm,
            queryBuilder: (query, cell, pagination) => query
                .Where<S2StoreEntity>(x => x.StoreId == "STORE" && x.Location == cell),
            pageSize: null // Non-paginated mode - query all cells in parallel
        );
        
        // Assert - Verify all results are within radius
        result.Items.Should().NotBeNull();
        result.Items.Should().HaveCountGreaterThan(0, "should return stores near the North Pole");
        
        // Verify all results are within the specified radius
        foreach (var store in result.Items)
        {
            var distance = store.Location.DistanceToKilometers(searchCenter);
            distance.Should().BeLessThanOrEqualTo(radiusKm, 
                $"Store {store.Name} at {store.Location} should be within {radiusKm}km radius. " +
                $"Actual distance: {distance:F2}km");
        }
        
        // Verify stores at various longitudes are returned (testing longitude convergence handling)
        var storeNames = result.Items.Select(s => s.Name).ToList();
        
        // Center store should be present
        storeNames.Should().Contain("Pole Center Store", "center store should be returned");
        
        // Stores at different longitudes should be present (longitude convergence test)
        var eastWestStores = result.Items.Where(s => 
            s.Name.Contains("East Store") || s.Name.Contains("West Store") || s.Name.Contains("Opposite Store")
        ).ToList();
        eastWestStores.Should().NotBeEmpty(
            "should return stores at various longitudes, demonstrating longitude convergence handling");
        
        // Verify stores outside radius are NOT present
        storeNames.Should().NotContain("Far South Store", "stores outside radius should not be returned");
        storeNames.Should().NotContain("Very Far South Store", "stores outside radius should not be returned");
        storeNames.Should().NotContain("Extremely Far Store", "stores outside radius should not be returned");
        
        // Verify no duplicates
        storeNames.Should().OnlyHaveUniqueItems("each store should appear exactly once, no duplicates");
    }
    
    [Fact]
    public async Task SpatialQueryAsync_S2ProximityNonPaginated_NearNorthPole_HandlesLongitudeConvergence()
    {
        // Arrange - Create table to specifically test longitude convergence at high latitudes
        await CreateTableAsync<S2StoreEntity>();
        var table = new S2StoreTable(DynamoDb, TableName);
        
        // Search center very close to the North Pole (latitude 89.5°)
        // At this latitude, longitude convergence is extreme
        var searchCenter = new GeoLocation(89.5, 0.0);
        var radiusKm = 100.0; // 100km radius
        
        // At 89.5° latitude, 1° of longitude is only ~1km
        // So stores at vastly different longitudes can be very close together
        var stores = new[]
        {
            // Stores at the same latitude but different longitudes
            // These should all be within ~100km of each other due to longitude convergence
            new S2StoreEntity
            {
                StoreId = "STORE",
                Location = new GeoLocation(89.5, 0.0),
                Name = "Longitude 0",
                Description = "At 0° longitude"
            },
            new S2StoreEntity
            {
                StoreId = "STORE",
                Location = new GeoLocation(89.5, 90.0),
                Name = "Longitude 90",
                Description = "At 90° longitude"
            },
            new S2StoreEntity
            {
                StoreId = "STORE",
                Location = new GeoLocation(89.5, 180.0),
                Name = "Longitude 180",
                Description = "At 180° longitude"
            },
            new S2StoreEntity
            {
                StoreId = "STORE",
                Location = new GeoLocation(89.5, -90.0),
                Name = "Longitude -90",
                Description = "At -90° longitude"
            },
            new S2StoreEntity
            {
                StoreId = "STORE",
                Location = new GeoLocation(89.5, -45.0),
                Name = "Longitude -45",
                Description = "At -45° longitude"
            },
            new S2StoreEntity
            {
                StoreId = "STORE",
                Location = new GeoLocation(89.5, 45.0),
                Name = "Longitude 45",
                Description = "At 45° longitude"
            },
            new S2StoreEntity
            {
                StoreId = "STORE",
                Location = new GeoLocation(89.5, 135.0),
                Name = "Longitude 135",
                Description = "At 135° longitude"
            },
            new S2StoreEntity
            {
                StoreId = "STORE",
                Location = new GeoLocation(89.5, -135.0),
                Name = "Longitude -135",
                Description = "At -135° longitude"
            },
            
            // Store slightly south (should be within radius)
            new S2StoreEntity
            {
                StoreId = "STORE",
                Location = new GeoLocation(89.0, 0.0), // ~55km south
                Name = "Slightly South",
                Description = "Slightly south of center"
            },
            
            // Store too far south (should be outside radius)
            new S2StoreEntity
            {
                StoreId = "STORE",
                Location = new GeoLocation(88.5, 0.0), // ~111km south, outside radius
                Name = "Too Far South",
                Description = "Outside the radius"
            }
        };
        
        foreach (var store in stores)
        {
            await table.PutAsync(store);
        }
        
        // Act - Execute spatial query near North Pole
        var result = await table.SpatialQueryAsync<S2StoreEntity>(
            locationSelector: store => store.Location,
            spatialIndexType: SpatialIndexType.S2,
            precision: 16,
            center: searchCenter,
            radiusKilometers: radiusKm,
            queryBuilder: (query, cell, pagination) => query
                .Where<S2StoreEntity>(x => x.StoreId == "STORE" && x.Location == cell),
            pageSize: null
        );
        
        // Assert - Verify longitude convergence is handled correctly
        result.Items.Should().NotBeNull();
        result.Items.Should().NotBeEmpty("should return stores near the pole");
        
        // Verify all results are within radius
        foreach (var store in result.Items)
        {
            var distance = store.Location.DistanceToKilometers(searchCenter);
            distance.Should().BeLessThanOrEqualTo(radiusKm, 
                $"Store {store.Name} at {store.Location} should be within {radiusKm}km radius. " +
                $"Actual distance: {distance:F2}km");
        }
        
        // Verify stores at vastly different longitudes are returned
        // This demonstrates that longitude convergence is handled correctly
        var storeNames = result.Items.Select(s => s.Name).ToList();
        
        // Should have stores at multiple different longitudes
        var longitudeStores = result.Items.Where(s => s.Name.StartsWith("Longitude")).ToList();
        longitudeStores.Should().HaveCountGreaterThan(3, 
            "should return stores at multiple different longitudes, demonstrating longitude convergence handling");
        
        // Verify the store that's too far south is NOT returned
        storeNames.Should().NotContain("Too Far South", 
            "stores outside radius should not be returned even at high latitudes");
        
        // Verify no duplicates
        storeNames.Should().OnlyHaveUniqueItems("each store should appear exactly once");
    }
    
    [Fact]
    public async Task SpatialQueryAsync_S2ProximityPaginated_NearNorthPole_ReturnsStoresWithinRadius()
    {
        // Arrange - Create table with many stores near the North Pole for pagination testing
        await CreateTableAsync<S2StoreEntity>();
        var table = new S2StoreTable(DynamoDb, TableName);
        
        // Search center near the North Pole
        var searchCenter = new GeoLocation(89.0, 0.0);
        var radiusKm = 150.0;
        
        // Create multiple stores at various locations near the pole
        var stores = new List<S2StoreEntity>();
        
        // Create stores in a grid pattern around the pole
        for (int latIdx = 0; latIdx < 5; latIdx++)
        {
            for (int lonIdx = 0; lonIdx < 8; lonIdx++)
            {
                var lat = 89.0 + (latIdx - 2) * 0.3; // Range: 88.4° to 89.6°
                var lon = lonIdx * 45.0; // 0°, 45°, 90°, 135°, 180°, -135°, -90°, -45°
                
                var location = new GeoLocation(lat, lon);
                var distance = location.DistanceToKilometers(searchCenter);
                
                // Only add stores within radius
                if (distance <= radiusKm)
                {
                    stores.Add(new S2StoreEntity
                    {
                        StoreId = "STORE",
                        Location = location,
                        Name = $"Pole Store {latIdx:D2}-{lonIdx:D2}",
                        Description = $"Store at lat={lat:F1}, lon={lon:F1}"
                    });
                }
            }
        }
        
        // Add some stores outside the radius
        stores.Add(new S2StoreEntity
        {
            StoreId = "STORE",
            Location = new GeoLocation(87.5, 0.0), // ~167km south
            Name = "Outside Store 1",
            Description = "Outside radius"
        });
        stores.Add(new S2StoreEntity
        {
            StoreId = "STORE",
            Location = new GeoLocation(87.0, 0.0), // ~222km south
            Name = "Outside Store 2",
            Description = "Outside radius"
        });
        
        foreach (var store in stores)
        {
            await table.PutAsync(store);
        }
        
        // Act - Execute paginated spatial query near North Pole
        var allResults = new List<S2StoreEntity>();
        SpatialContinuationToken? continuationToken = null;
        int pageCount = 0;
        int maxPages = 30; // Safety limit
        
        do
        {
            var result = await table.SpatialQueryAsync<S2StoreEntity>(
                locationSelector: store => store.Location,
                spatialIndexType: SpatialIndexType.S2,
                precision: 16,
                center: searchCenter,
                radiusKilometers: radiusKm,
                queryBuilder: (query, cell, pagination) => query
                    .Where<S2StoreEntity>(x => x.StoreId == "STORE" && x.Location == cell),
                pageSize: 5, // Small page size to test pagination
                continuationToken: continuationToken
            );
            
            allResults.AddRange(result.Items);
            continuationToken = result.ContinuationToken;
            pageCount++;
            
            if (pageCount >= maxPages)
            {
                break;
            }
        }
        while (continuationToken != null);
        
        // Assert - Verify all results are within radius
        allResults.Should().NotBeEmpty("should return stores near the North Pole");
        
        // Verify all results are within the specified radius
        foreach (var store in allResults)
        {
            var distance = store.Location.DistanceToKilometers(searchCenter);
            distance.Should().BeLessThanOrEqualTo(radiusKm, 
                $"Store {store.Name} at {store.Location} should be within {radiusKm}km radius. " +
                $"Actual distance: {distance:F2}km");
        }
        
        // Verify stores outside radius are NOT present
        var storeNames = allResults.Select(s => s.Name).ToList();
        storeNames.Should().NotContain("Outside Store 1", "stores outside radius should not be returned");
        storeNames.Should().NotContain("Outside Store 2", "stores outside radius should not be returned");
        
        // Verify no duplicates across pages
        storeNames.Should().OnlyHaveUniqueItems("each store should appear exactly once across all pages");
        
        // Verify stores at various longitudes are present (longitude convergence test)
        var poleStores = allResults.Where(s => s.Name.StartsWith("Pole Store")).ToList();
        poleStores.Should().NotBeEmpty("should return stores from the grid pattern");
        
        // Verify pagination worked (multiple pages were fetched)
        pageCount.Should().BeGreaterThan(1, "should have fetched multiple pages");
    }
    
    #endregion
    
    #region 29.5 Test S2 query with both date line and pole
    
    [Fact]
    public async Task SpatialQueryAsync_S2ProximityNonPaginated_NorthPoleAndDateLine_HandlesBothEdgeCases()
    {
        // Arrange - Create table with S2-indexed stores near both North Pole and date line
        await CreateTableAsync<S2StoreEntity>();
        var table = new S2StoreTable(DynamoDb, TableName);
        
        // Search center at North Pole (89°) AND near the date line (179°)
        // This tests the most challenging edge case: both longitude convergence and date line crossing
        var searchCenter = new GeoLocation(89.0, 179.0);
        var radiusKm = 250.0; // 250km radius
        
        // At 89° latitude near the date line, we need to test:
        // 1. Longitude convergence (longitudes are very close together)
        // 2. Date line crossing (longitude wraps from +180° to -180°)
        // 3. No duplicate results from cells appearing in multiple regions
        
        var stores = new[]
        {
            // Store at search center
            new S2StoreEntity
            {
                StoreId = "STORE",
                Location = new GeoLocation(89.0, 179.0),
                Name = "Center Store",
                Description = "At search center (89°, 179°)"
            },
            
            // Stores on the western side of date line (positive longitude)
            // At 89° latitude, longitude differences represent very small distances
            new S2StoreEntity
            {
                StoreId = "STORE",
                Location = new GeoLocation(89.0, 179.5), // Close to search center
                Name = "West Side Store 1",
                Description = "West of date line, same latitude"
            },
            new S2StoreEntity
            {
                StoreId = "STORE",
                Location = new GeoLocation(89.2, 179.0), // North of search center
                Name = "West Side Store 2",
                Description = "West of date line, slightly north"
            },
            new S2StoreEntity
            {
                StoreId = "STORE",
                Location = new GeoLocation(88.8, 179.0), // South of search center
                Name = "West Side Store 3",
                Description = "West of date line, slightly south"
            },
            
            // Stores on the eastern side of date line (negative longitude)
            new S2StoreEntity
            {
                StoreId = "STORE",
                Location = new GeoLocation(89.0, -179.5), // Close to search center, other side
                Name = "East Side Store 1",
                Description = "East of date line, same latitude"
            },
            new S2StoreEntity
            {
                StoreId = "STORE",
                Location = new GeoLocation(89.2, -179.0), // North of search center, other side
                Name = "East Side Store 2",
                Description = "East of date line, slightly north"
            },
            new S2StoreEntity
            {
                StoreId = "STORE",
                Location = new GeoLocation(88.8, -179.0), // South of search center, other side
                Name = "East Side Store 3",
                Description = "East of date line, slightly south"
            },
            
            // Stores at various longitudes around the pole (testing longitude convergence)
            // At 89° latitude, these vastly different longitudes are actually close together
            new S2StoreEntity
            {
                StoreId = "STORE",
                Location = new GeoLocation(89.0, 0.0),
                Name = "Longitude 0 Store",
                Description = "At 0° longitude, near pole"
            },
            new S2StoreEntity
            {
                StoreId = "STORE",
                Location = new GeoLocation(89.0, 90.0),
                Name = "Longitude 90 Store",
                Description = "At 90° longitude, near pole"
            },
            new S2StoreEntity
            {
                StoreId = "STORE",
                Location = new GeoLocation(89.0, -90.0),
                Name = "Longitude -90 Store",
                Description = "At -90° longitude, near pole"
            },
            
            // Stores closer to the pole (higher latitude)
            new S2StoreEntity
            {
                StoreId = "STORE",
                Location = new GeoLocation(89.5, 179.0),
                Name = "Higher Latitude Store 1",
                Description = "Closer to pole, same longitude"
            },
            new S2StoreEntity
            {
                StoreId = "STORE",
                Location = new GeoLocation(89.5, -179.0),
                Name = "Higher Latitude Store 2",
                Description = "Closer to pole, opposite side of date line"
            },
            
            // Stores farther from the pole (lower latitude, within radius)
            new S2StoreEntity
            {
                StoreId = "STORE",
                Location = new GeoLocation(88.5, 179.0),
                Name = "Lower Latitude Store 1",
                Description = "Farther from pole, same longitude"
            },
            new S2StoreEntity
            {
                StoreId = "STORE",
                Location = new GeoLocation(88.5, -179.0),
                Name = "Lower Latitude Store 2",
                Description = "Farther from pole, opposite side of date line"
            },
            
            // Stores outside the radius (should NOT be returned)
            new S2StoreEntity
            {
                StoreId = "STORE",
                Location = new GeoLocation(87.0, 179.0),
                Name = "Far South Store 1",
                Description = "Too far south, same longitude"
            },
            new S2StoreEntity
            {
                StoreId = "STORE",
                Location = new GeoLocation(87.0, -179.0),
                Name = "Far South Store 2",
                Description = "Too far south, opposite side of date line"
            },
            new S2StoreEntity
            {
                StoreId = "STORE",
                Location = new GeoLocation(89.0, 170.0),
                Name = "Far West Store",
                Description = "Too far west"
            },
            new S2StoreEntity
            {
                StoreId = "STORE",
                Location = new GeoLocation(89.0, -170.0),
                Name = "Far East Store",
                Description = "Too far east"
            }
        };
        
        // Write all stores to DynamoDB
        foreach (var store in stores)
        {
            await table.PutAsync(store);
        }
        
        // Act - Execute SpatialQueryAsync with radius search at North Pole near date line
        var result = await table.SpatialQueryAsync<S2StoreEntity>(
            locationSelector: store => store.Location,
            spatialIndexType: SpatialIndexType.S2,
            precision: 16, // S2 Level 16 (~600m cells)
            center: searchCenter,
            radiusKilometers: radiusKm,
            queryBuilder: (query, cell, pagination) => query
                .Where<S2StoreEntity>(x => x.StoreId == "STORE" && x.Location == cell),
            pageSize: null // Non-paginated mode - query all cells in parallel
        );
        
        // Assert - Verify both edge cases are handled correctly
        result.Items.Should().NotBeNull();
        
        // The key test here is that the query completes successfully without errors
        // At such extreme latitudes near the date line, the S2 cell covering algorithm
        // handles the complex geometry correctly
        result.Should().NotBeNull("query should complete successfully at North Pole near date line");
        result.TotalCellsQueried.Should().BeGreaterThan(0, "should query at least one S2 cell");
        
        // If we got results, verify they are within radius
        if (result.Items.Count > 0)
        {
            foreach (var store in result.Items)
            {
                var distance = store.Location.DistanceToKilometers(searchCenter);
                distance.Should().BeLessThanOrEqualTo(radiusKm, 
                    $"Store {store.Name} at {store.Location} should be within {radiusKm}km radius. " +
                    $"Actual distance: {distance:F2}km");
            }
            
            var storeNames = result.Items.Select(s => s.Name).ToList();
            
            // Verify stores on both sides of the date line are present
            var westSideStores = result.Items.Where(s => s.Name.Contains("West Side")).ToList();
            var eastSideStores = result.Items.Where(s => s.Name.Contains("East Side")).ToList();
            
            // At least one side should have stores
            var hasEitherSide = westSideStores.Any() || eastSideStores.Any();
            hasEitherSide.Should().BeTrue(
                $"should return stores from at least one side of the date line. " +
                $"West side: {westSideStores.Count}, East side: {eastSideStores.Count}");
            
            // Verify stores at various longitudes are present (longitude convergence test)
            // At 89° latitude, stores at vastly different longitudes can be close together
            var longitudeStores = result.Items.Where(s => s.Name.Contains("Longitude")).ToList();
            longitudeStores.Should().NotBeEmpty(
                "should return stores at various longitudes, demonstrating longitude convergence handling");
            
            // Verify stores at different latitudes are present
            var higherLatStores = result.Items.Where(s => s.Name.Contains("Higher Latitude")).ToList();
            var lowerLatStores = result.Items.Where(s => s.Name.Contains("Lower Latitude")).ToList();
            
            higherLatStores.Should().NotBeEmpty(
                "should return stores closer to the pole");
            lowerLatStores.Should().NotBeEmpty(
                "should return stores farther from the pole but within radius");
            
            // Verify the center store is present
            storeNames.Should().Contain("Center Store", 
                "center store at (89°, 179°) should be returned");
            
            // Verify stores outside radius are NOT present
            storeNames.Should().NotContain("Far South Store 1", 
                "stores outside radius should not be returned");
            storeNames.Should().NotContain("Far South Store 2", 
                "stores outside radius should not be returned");
            storeNames.Should().NotContain("Far West Store", 
                "stores outside radius should not be returned");
            storeNames.Should().NotContain("Far East Store", 
                "stores outside radius should not be returned");
            
            // Verify no duplicates - this is critical for combined edge cases
            storeNames.Should().OnlyHaveUniqueItems(
                "each store should appear exactly once, no duplicates despite date line and pole edge cases");
        }
        
        // Additional verification: ensure we have a good distribution of stores
        result.Items.Count.Should().BeGreaterThan(5, 
            "should return multiple stores demonstrating both edge cases are handled");
    }
    
    [Fact]
    public async Task SpatialQueryAsync_S2ProximityPaginated_NorthPoleAndDateLine_HandlesBothEdgeCases()
    {
        // Arrange - Create table with many stores for paginated query testing
        await CreateTableAsync<S2StoreEntity>();
        var table = new S2StoreTable(DynamoDb, TableName);
        
        // Search center at North Pole near date line
        var searchCenter = new GeoLocation(89.0, 179.0);
        var radiusKm = 200.0;
        
        // Create a grid of stores around the search center
        var stores = new List<S2StoreEntity>();
        
        // Create stores in a pattern that covers both sides of the date line
        // and various latitudes near the pole
        for (int latIdx = 0; latIdx < 5; latIdx++)
        {
            for (int lonIdx = 0; lonIdx < 12; lonIdx++)
            {
                // Latitude: 88.5° to 89.5° (range of 1° around 89°)
                var lat = 88.5 + latIdx * 0.25;
                
                // Longitude: spread around 179° crossing the date line
                // Range from 177° to -177° (crossing date line)
                var lon = 177.0 + lonIdx * 2.0;
                if (lon > 180.0)
                {
                    lon = lon - 360.0; // Wrap to negative longitude
                }
                
                var location = new GeoLocation(lat, lon);
                var distance = location.DistanceToKilometers(searchCenter);
                
                // Only add stores within radius
                if (distance <= radiusKm)
                {
                    stores.Add(new S2StoreEntity
                    {
                        StoreId = "STORE",
                        Location = location,
                        Name = $"Grid Store {latIdx:D2}-{lonIdx:D2}",
                        Description = $"Store at lat={lat:F2}, lon={lon:F2}, distance={distance:F2}km"
                    });
                }
            }
        }
        
        // Add some stores outside the radius
        stores.Add(new S2StoreEntity
        {
            StoreId = "STORE",
            Location = new GeoLocation(87.0, 179.0),
            Name = "Outside Store 1",
            Description = "Too far south"
        });
        stores.Add(new S2StoreEntity
        {
            StoreId = "STORE",
            Location = new GeoLocation(87.0, -179.0),
            Name = "Outside Store 2",
            Description = "Too far south, other side of date line"
        });
        
        foreach (var store in stores)
        {
            await table.PutAsync(store);
        }
        
        // Act - Execute paginated spatial query at North Pole near date line
        var allResults = new List<S2StoreEntity>();
        SpatialContinuationToken? continuationToken = null;
        int pageCount = 0;
        int maxPages = 30; // Safety limit
        
        do
        {
            var result = await table.SpatialQueryAsync<S2StoreEntity>(
                locationSelector: store => store.Location,
                spatialIndexType: SpatialIndexType.S2,
                precision: 16,
                center: searchCenter,
                radiusKilometers: radiusKm,
                queryBuilder: (query, cell, pagination) => query
                    .Where<S2StoreEntity>(x => x.StoreId == "STORE" && x.Location == cell),
                pageSize: 5, // Small page size to test pagination
                continuationToken: continuationToken
            );
            
            allResults.AddRange(result.Items);
            continuationToken = result.ContinuationToken;
            pageCount++;
            
            if (pageCount >= maxPages)
            {
                break;
            }
        }
        while (continuationToken != null);
        
        // Assert - Verify both edge cases are handled correctly with pagination
        allResults.Should().NotBeEmpty(
            "should return stores from paginated query at North Pole near date line");
        
        // Verify all results are within the specified radius
        foreach (var store in allResults)
        {
            var distance = store.Location.DistanceToKilometers(searchCenter);
            distance.Should().BeLessThanOrEqualTo(radiusKm, 
                $"Store {store.Name} at {store.Location} should be within {radiusKm}km radius. " +
                $"Actual distance: {distance:F2}km");
        }
        
        var storeNames = allResults.Select(s => s.Name).ToList();
        
        // Verify stores from both sides of the date line are present
        var gridStores = allResults.Where(s => s.Name.StartsWith("Grid Store")).ToList();
        gridStores.Should().NotBeEmpty("should return stores from the grid pattern");
        
        // Check for stores on both sides of date line by examining longitudes
        var westSideStores = allResults.Where(s => s.Location.Longitude > 0).ToList();
        var eastSideStores = allResults.Where(s => s.Location.Longitude < 0).ToList();
        
        westSideStores.Should().NotBeEmpty(
            "should return stores with positive longitude (west side of date line)");
        eastSideStores.Should().NotBeEmpty(
            "should return stores with negative longitude (east side of date line)");
        
        // Verify stores outside radius are NOT present
        storeNames.Should().NotContain("Outside Store 1", 
            "stores outside radius should not be returned");
        storeNames.Should().NotContain("Outside Store 2", 
            "stores outside radius should not be returned");
        
        // Verify no duplicates across pages - critical for combined edge cases
        storeNames.Should().OnlyHaveUniqueItems(
            "each store should appear exactly once across all pages, no duplicates");
        
        // Verify pagination worked (multiple pages were fetched)
        pageCount.Should().BeGreaterThan(1, 
            "should have fetched multiple pages for this query");
        
        // Verify we have a good distribution of stores
        allResults.Count.Should().BeGreaterThan(5, 
            "should return multiple stores demonstrating both edge cases are handled");
    }
    
    #endregion
}
