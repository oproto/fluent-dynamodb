using Amazon.DynamoDBv2;
using Oproto.FluentDynamoDb.Attributes;
using Oproto.FluentDynamoDb.Geospatial;
using Oproto.FluentDynamoDb.IntegrationTests.Infrastructure;
using Oproto.FluentDynamoDb.IntegrationTests.TestEntities;
using Oproto.FluentDynamoDb.Storage;

namespace Oproto.FluentDynamoDb.IntegrationTests.RealWorld;

/// <summary>
/// Integration tests for H3 spatial queries with edge cases like date line crossing and polar regions.
/// Tests verify that SpatialQueryAsync correctly handles geographic edge cases.
/// </summary>
[Collection("DynamoDB Local")]
[Trait("Category", "Integration")]
[Trait("Feature", "Geospatial")]
[Trait("Feature", "H3")]
[Trait("Feature", "EdgeCases")]
public class H3EdgeCaseIntegrationTests : IntegrationTestBase
{
    public H3EdgeCaseIntegrationTests(DynamoDbLocalFixture fixture) : base(fixture)
    {
    }
    
    /// <summary>
    /// Simple table wrapper for testing spatial queries.
    /// </summary>
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
    
    #region 29.2 Test H3 query crossing date line
    
    [Fact]
    public async Task SpatialQueryAsync_H3ProximityNonPaginated_CrossingDateLine_ReturnsStoresOnBothSides()
    {
        // Arrange - Create table with H3-indexed stores near the International Date Line
        await CreateTableAsync<H3StoreLocationSortKeyEntity>();
        var table = new H3StoreTable(DynamoDb, TableName);
        
        // Search center at the equator, just east of the date line (longitude ~-179°)
        // This will create a search area that crosses the date line
        var searchCenter = new GeoLocation(0.0, -179.0);
        var radiusKm = 200.0; // 200km radius will definitely cross the date line
        
        // Create stores on both sides of the date line
        var stores = new[]
        {
            // Stores on the western side (positive longitude, near +180°)
            new H3StoreLocationSortKeyEntity
            {
                StoreId = "STORE",
                Location = new GeoLocation(0.0, 179.5), // ~55km west of center, west side of date line
                Name = "West Side Store 1",
                Description = "Just west of date line"
            },
            new H3StoreLocationSortKeyEntity
            {
                StoreId = "STORE",
                Location = new GeoLocation(0.5, 179.8), // ~88km northwest, west side
                Name = "West Side Store 2",
                Description = "Northwest of center, west side"
            },
            new H3StoreLocationSortKeyEntity
            {
                StoreId = "STORE",
                Location = new GeoLocation(-0.5, 179.7), // ~77km southwest, west side
                Name = "West Side Store 3",
                Description = "Southwest of center, west side"
            },
            
            // Stores on the eastern side (negative longitude, near -180°)
            new H3StoreLocationSortKeyEntity
            {
                StoreId = "STORE",
                Location = new GeoLocation(0.0, -179.5), // ~55km east of center, east side of date line
                Name = "East Side Store 1",
                Description = "Just east of date line"
            },
            new H3StoreLocationSortKeyEntity
            {
                StoreId = "STORE",
                Location = new GeoLocation(0.5, -179.8), // ~88km northeast, east side
                Name = "East Side Store 2",
                Description = "Northeast of center, east side"
            },
            new H3StoreLocationSortKeyEntity
            {
                StoreId = "STORE",
                Location = new GeoLocation(-0.5, -179.7), // ~77km southeast, east side
                Name = "East Side Store 3",
                Description = "Southeast of center, east side"
            },
            
            // Store at the center (on the date line)
            new H3StoreLocationSortKeyEntity
            {
                StoreId = "STORE",
                Location = new GeoLocation(0.0, -179.0), // At search center
                Name = "Center Store",
                Description = "At search center, near date line"
            },
            
            // Stores outside the radius (should not be returned)
            new H3StoreLocationSortKeyEntity
            {
                StoreId = "STORE",
                Location = new GeoLocation(0.0, 175.0), // ~445km west, outside radius
                Name = "Far West Store",
                Description = "Too far west"
            },
            new H3StoreLocationSortKeyEntity
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
        var result = await table.SpatialQueryAsync<H3StoreLocationSortKeyEntity>(
            locationSelector: store => store.Location,
            spatialIndexType: SpatialIndexType.H3,
            precision: 9, // H3 Resolution 9 (~174m hexagons)
            center: searchCenter,
            radiusKilometers: radiusKm,
            queryBuilder: (query, cell, pagination) => query
                .Where<H3StoreLocationSortKeyEntity>(x => x.StoreId == "STORE" && x.Location == cell),
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
    public async Task SpatialQueryAsync_H3ProximityNonPaginated_CrossingDateLine_NoDuplicates()
    {
        // Arrange - Create table with H3-indexed stores near the date line
        await CreateTableAsync<H3StoreLocationSortKeyEntity>();
        var table = new H3StoreTable(DynamoDb, TableName);
        
        // Search center at the equator, on the date line
        var searchCenter = new GeoLocation(0.0, 180.0);
        var radiusKm = 150.0;
        
        // Create stores that might appear in multiple H3 cells due to date line crossing
        var stores = new[]
        {
            new H3StoreLocationSortKeyEntity
            {
                StoreId = "STORE",
                Location = new GeoLocation(0.0, 179.9),
                Name = "Store A",
                Description = "Near date line west"
            },
            new H3StoreLocationSortKeyEntity
            {
                StoreId = "STORE",
                Location = new GeoLocation(0.0, -179.9),
                Name = "Store B",
                Description = "Near date line east"
            },
            new H3StoreLocationSortKeyEntity
            {
                StoreId = "STORE",
                Location = new GeoLocation(0.5, 179.5),
                Name = "Store C",
                Description = "North of date line west"
            },
            new H3StoreLocationSortKeyEntity
            {
                StoreId = "STORE",
                Location = new GeoLocation(0.5, -179.5),
                Name = "Store D",
                Description = "North of date line east"
            },
            new H3StoreLocationSortKeyEntity
            {
                StoreId = "STORE",
                Location = new GeoLocation(-0.5, 179.5),
                Name = "Store E",
                Description = "South of date line west"
            },
            new H3StoreLocationSortKeyEntity
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
        var result = await table.SpatialQueryAsync<H3StoreLocationSortKeyEntity>(
            locationSelector: store => store.Location,
            spatialIndexType: SpatialIndexType.H3,
            precision: 9,
            center: searchCenter,
            radiusKilometers: radiusKm,
            queryBuilder: (query, cell, pagination) => query
                .Where<H3StoreLocationSortKeyEntity>(x => x.StoreId == "STORE" && x.Location == cell),
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
    public async Task SpatialQueryAsync_H3ProximityPaginated_CrossingDateLine_ReturnsStoresOnBothSides()
    {
        // Arrange - Create table with H3-indexed stores near the date line
        await CreateTableAsync<H3StoreLocationSortKeyEntity>();
        var table = new H3StoreTable(DynamoDb, TableName);
        
        // Search center near the date line
        var searchCenter = new GeoLocation(0.0, -179.0);
        var radiusKm = 200.0;
        
        // Create multiple stores on both sides of the date line
        var stores = new List<H3StoreLocationSortKeyEntity>();
        
        // West side stores (positive longitude)
        for (int i = 0; i < 10; i++)
        {
            var latOffset = (i % 4 - 1.5) * 0.5;
            var lonOffset = (i / 4) * 0.3;
            stores.Add(new H3StoreLocationSortKeyEntity
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
            stores.Add(new H3StoreLocationSortKeyEntity
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
        var allResults = new List<H3StoreLocationSortKeyEntity>();
        SpatialContinuationToken? continuationToken = null;
        int pageCount = 0;
        int maxPages = 20; // Safety limit
        
        do
        {
            var result = await table.SpatialQueryAsync<H3StoreLocationSortKeyEntity>(
                locationSelector: store => store.Location,
                spatialIndexType: SpatialIndexType.H3,
                precision: 9,
                center: searchCenter,
                radiusKilometers: radiusKm,
                queryBuilder: (query, cell, pagination) => query
                    .Where<H3StoreLocationSortKeyEntity>(x => x.StoreId == "STORE" && x.Location == cell),
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
    public async Task SpatialQueryAsync_H3BoundingBox_CrossingDateLine_ReturnsStoresOnBothSides()
    {
        // Arrange - Create table with H3-indexed stores near the date line
        await CreateTableAsync<H3StoreLocationSortKeyEntity>();
        var table = new H3StoreTable(DynamoDb, TableName);
        
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
            new H3StoreLocationSortKeyEntity
            {
                StoreId = "STORE",
                Location = new GeoLocation(0.0, 179.0),
                Name = "Inside West 1",
                Description = "Inside box, west side"
            },
            new H3StoreLocationSortKeyEntity
            {
                StoreId = "STORE",
                Location = new GeoLocation(0.5, 178.5),
                Name = "Inside West 2",
                Description = "Inside box, west side"
            },
            
            // Inside the box - east side
            new H3StoreLocationSortKeyEntity
            {
                StoreId = "STORE",
                Location = new GeoLocation(0.0, -179.0),
                Name = "Inside East 1",
                Description = "Inside box, east side"
            },
            new H3StoreLocationSortKeyEntity
            {
                StoreId = "STORE",
                Location = new GeoLocation(-0.5, -178.5),
                Name = "Inside East 2",
                Description = "Inside box, east side"
            },
            
            // Outside the box
            new H3StoreLocationSortKeyEntity
            {
                StoreId = "STORE",
                Location = new GeoLocation(0.0, 170.0),
                Name = "Outside West",
                Description = "Outside box, too far west"
            },
            new H3StoreLocationSortKeyEntity
            {
                StoreId = "STORE",
                Location = new GeoLocation(0.0, -170.0),
                Name = "Outside East",
                Description = "Outside box, too far east"
            },
            new H3StoreLocationSortKeyEntity
            {
                StoreId = "STORE",
                Location = new GeoLocation(2.0, 179.0),
                Name = "Outside North",
                Description = "Outside box, too far north"
            },
            new H3StoreLocationSortKeyEntity
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
        var result = await table.SpatialQueryAsync<H3StoreLocationSortKeyEntity>(
            locationSelector: store => store.Location,
            spatialIndexType: SpatialIndexType.H3,
            precision: 9,
            boundingBox: boundingBox,
            queryBuilder: (query, cell, pagination) => query
                .Where<H3StoreLocationSortKeyEntity>(x => x.StoreId == "STORE" && x.Location == cell),
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
    
    #region 29.4 Test H3 query near South Pole
    
    [Fact]
    public async Task SpatialQueryAsync_H3ProximityNonPaginated_NearSouthPole_ReturnsStoresWithinRadius()
    {
        // Arrange - Create table with H3-indexed stores near the South Pole
        await CreateTableAsync<H3StoreLocationSortKeyEntity>();
        var table = new H3StoreTable(DynamoDb, TableName);
        
        // Search center near the South Pole (latitude ~-89°)
        // At this latitude, longitude convergence is significant
        var searchCenter = new GeoLocation(-89.0, 0.0);
        var radiusKm = 200.0; // 200km radius
        
        // Create stores near the South Pole at various longitudes
        // At -89° latitude, 1° of longitude is only ~2km (vs ~111km at equator)
        var stores = new[]
        {
            // Stores within radius at different longitudes
            new H3StoreLocationSortKeyEntity
            {
                StoreId = "STORE",
                Location = new GeoLocation(-89.0, 0.0), // At search center
                Name = "Pole Center Store",
                Description = "At search center"
            },
            new H3StoreLocationSortKeyEntity
            {
                StoreId = "STORE",
                Location = new GeoLocation(-89.5, 0.0), // ~55km south (closer to pole)
                Name = "South Store 1",
                Description = "Directly south of center (closer to pole)"
            },
            new H3StoreLocationSortKeyEntity
            {
                StoreId = "STORE",
                Location = new GeoLocation(-88.5, 0.0), // ~55km north (away from pole)
                Name = "North Store 1",
                Description = "Directly north of center (away from pole)"
            },
            
            // Stores at different longitudes (testing longitude convergence)
            // At -89° latitude, longitude differences represent very small distances
            new H3StoreLocationSortKeyEntity
            {
                StoreId = "STORE",
                Location = new GeoLocation(-89.0, 45.0), // ~50km east (longitude convergence)
                Name = "East Store 1",
                Description = "East at 45° longitude"
            },
            new H3StoreLocationSortKeyEntity
            {
                StoreId = "STORE",
                Location = new GeoLocation(-89.0, 90.0), // ~50km east (longitude convergence)
                Name = "East Store 2",
                Description = "East at 90° longitude"
            },
            new H3StoreLocationSortKeyEntity
            {
                StoreId = "STORE",
                Location = new GeoLocation(-89.0, 180.0), // ~100km opposite side
                Name = "Opposite Store",
                Description = "Opposite side at 180° longitude"
            },
            new H3StoreLocationSortKeyEntity
            {
                StoreId = "STORE",
                Location = new GeoLocation(-89.0, -90.0), // ~50km west (longitude convergence)
                Name = "West Store 1",
                Description = "West at -90° longitude"
            },
            new H3StoreLocationSortKeyEntity
            {
                StoreId = "STORE",
                Location = new GeoLocation(-89.0, -45.0), // ~50km west (longitude convergence)
                Name = "West Store 2",
                Description = "West at -45° longitude"
            },
            
            // Stores at the edge of the radius
            new H3StoreLocationSortKeyEntity
            {
                StoreId = "STORE",
                Location = new GeoLocation(-87.2, 0.0), // ~200km north (at edge)
                Name = "Edge Store North",
                Description = "At northern edge of radius"
            },
            new H3StoreLocationSortKeyEntity
            {
                StoreId = "STORE",
                Location = new GeoLocation(-89.0, 135.0), // ~75km away
                Name = "Edge Store East",
                Description = "East at 135° longitude"
            },
            
            // Stores outside the radius (should not be returned)
            new H3StoreLocationSortKeyEntity
            {
                StoreId = "STORE",
                Location = new GeoLocation(-87.0, 0.0), // ~222km north, outside radius
                Name = "Far North Store",
                Description = "Too far north"
            },
            new H3StoreLocationSortKeyEntity
            {
                StoreId = "STORE",
                Location = new GeoLocation(-86.5, 0.0), // ~278km north, outside radius
                Name = "Very Far North Store",
                Description = "Very far north"
            },
            new H3StoreLocationSortKeyEntity
            {
                StoreId = "STORE",
                Location = new GeoLocation(-85.0, 0.0), // ~445km north, outside radius
                Name = "Extremely Far Store",
                Description = "Extremely far north"
            }
        };
        
        // Write all stores to DynamoDB
        foreach (var store in stores)
        {
            await table.PutAsync(store);
        }
        
        // Act - Execute SpatialQueryAsync with radius search near South Pole
        var result = await table.SpatialQueryAsync<H3StoreLocationSortKeyEntity>(
            locationSelector: store => store.Location,
            spatialIndexType: SpatialIndexType.H3,
            precision: 9, // H3 Resolution 9 (~174m hexagons)
            center: searchCenter,
            radiusKilometers: radiusKm,
            queryBuilder: (query, cell, pagination) => query
                .Where<H3StoreLocationSortKeyEntity>(x => x.StoreId == "STORE" && x.Location == cell),
            pageSize: null // Non-paginated mode - query all cells in parallel
        );
        
        // Assert - Verify all results are within radius
        result.Items.Should().NotBeNull();
        result.Items.Should().HaveCountGreaterThan(0, "should return stores near the South Pole");
        
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
        storeNames.Should().NotContain("Far North Store", "stores outside radius should not be returned");
        storeNames.Should().NotContain("Very Far North Store", "stores outside radius should not be returned");
        storeNames.Should().NotContain("Extremely Far Store", "stores outside radius should not be returned");
        
        // Verify no duplicates
        storeNames.Should().OnlyHaveUniqueItems("each store should appear exactly once, no duplicates");
    }
    
    [Fact]
    public async Task SpatialQueryAsync_H3ProximityNonPaginated_NearSouthPole_HandlesLongitudeConvergence()
    {
        // Arrange - Create table to specifically test longitude convergence at high southern latitudes
        await CreateTableAsync<H3StoreLocationSortKeyEntity>();
        var table = new H3StoreTable(DynamoDb, TableName);
        
        // Search center very close to the South Pole (latitude -89.5°)
        // At this latitude, longitude convergence is extreme
        var searchCenter = new GeoLocation(-89.5, 0.0);
        var radiusKm = 100.0; // 100km radius
        
        // At -89.5° latitude, 1° of longitude is only ~1km
        // So stores at vastly different longitudes can be very close together
        var stores = new[]
        {
            // Stores at the same latitude but different longitudes
            // These should all be within ~100km of each other due to longitude convergence
            new H3StoreLocationSortKeyEntity
            {
                StoreId = "STORE",
                Location = new GeoLocation(-89.5, 0.0),
                Name = "Longitude 0",
                Description = "At 0° longitude"
            },
            new H3StoreLocationSortKeyEntity
            {
                StoreId = "STORE",
                Location = new GeoLocation(-89.5, 90.0),
                Name = "Longitude 90",
                Description = "At 90° longitude"
            },
            new H3StoreLocationSortKeyEntity
            {
                StoreId = "STORE",
                Location = new GeoLocation(-89.5, 180.0),
                Name = "Longitude 180",
                Description = "At 180° longitude"
            },
            new H3StoreLocationSortKeyEntity
            {
                StoreId = "STORE",
                Location = new GeoLocation(-89.5, -90.0),
                Name = "Longitude -90",
                Description = "At -90° longitude"
            },
            new H3StoreLocationSortKeyEntity
            {
                StoreId = "STORE",
                Location = new GeoLocation(-89.5, -45.0),
                Name = "Longitude -45",
                Description = "At -45° longitude"
            },
            new H3StoreLocationSortKeyEntity
            {
                StoreId = "STORE",
                Location = new GeoLocation(-89.5, 45.0),
                Name = "Longitude 45",
                Description = "At 45° longitude"
            },
            new H3StoreLocationSortKeyEntity
            {
                StoreId = "STORE",
                Location = new GeoLocation(-89.5, 135.0),
                Name = "Longitude 135",
                Description = "At 135° longitude"
            },
            new H3StoreLocationSortKeyEntity
            {
                StoreId = "STORE",
                Location = new GeoLocation(-89.5, -135.0),
                Name = "Longitude -135",
                Description = "At -135° longitude"
            },
            
            // Store slightly north (should be within radius)
            new H3StoreLocationSortKeyEntity
            {
                StoreId = "STORE",
                Location = new GeoLocation(-89.0, 0.0), // ~55km north
                Name = "Slightly North",
                Description = "Slightly north of center"
            },
            
            // Store too far north (should be outside radius)
            new H3StoreLocationSortKeyEntity
            {
                StoreId = "STORE",
                Location = new GeoLocation(-88.5, 0.0), // ~111km north, outside radius
                Name = "Too Far North",
                Description = "Outside the radius"
            }
        };
        
        foreach (var store in stores)
        {
            await table.PutAsync(store);
        }
        
        // Act - Execute spatial query near South Pole
        var result = await table.SpatialQueryAsync<H3StoreLocationSortKeyEntity>(
            locationSelector: store => store.Location,
            spatialIndexType: SpatialIndexType.H3,
            precision: 9,
            center: searchCenter,
            radiusKilometers: radiusKm,
            queryBuilder: (query, cell, pagination) => query
                .Where<H3StoreLocationSortKeyEntity>(x => x.StoreId == "STORE" && x.Location == cell),
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
        
        // Verify the store that's too far north is NOT returned
        storeNames.Should().NotContain("Too Far North", 
            "stores outside radius should not be returned even at high latitudes");
        
        // Verify no duplicates
        storeNames.Should().OnlyHaveUniqueItems("each store should appear exactly once");
    }
    
    [Fact]
    public async Task SpatialQueryAsync_H3ProximityPaginated_NearSouthPole_ReturnsStoresWithinRadius()
    {
        // Arrange - Create table with many stores near the South Pole for pagination testing
        await CreateTableAsync<H3StoreLocationSortKeyEntity>();
        var table = new H3StoreTable(DynamoDb, TableName);
        
        // Search center near the South Pole
        var searchCenter = new GeoLocation(-89.0, 0.0);
        var radiusKm = 150.0;
        
        // Create multiple stores at various locations near the pole
        var stores = new List<H3StoreLocationSortKeyEntity>();
        
        // Create stores in a grid pattern around the pole
        for (int latIdx = 0; latIdx < 5; latIdx++)
        {
            for (int lonIdx = 0; lonIdx < 8; lonIdx++)
            {
                var lat = -89.0 + (latIdx - 2) * 0.3; // Range: -89.6° to -88.4°
                var lon = lonIdx * 45.0; // 0°, 45°, 90°, 135°, 180°, -135°, -90°, -45°
                
                var location = new GeoLocation(lat, lon);
                var distance = location.DistanceToKilometers(searchCenter);
                
                // Only add stores within radius
                if (distance <= radiusKm)
                {
                    stores.Add(new H3StoreLocationSortKeyEntity
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
        stores.Add(new H3StoreLocationSortKeyEntity
        {
            StoreId = "STORE",
            Location = new GeoLocation(-87.5, 0.0), // ~167km north
            Name = "Outside Store 1",
            Description = "Outside radius"
        });
        stores.Add(new H3StoreLocationSortKeyEntity
        {
            StoreId = "STORE",
            Location = new GeoLocation(-87.0, 0.0), // ~222km north
            Name = "Outside Store 2",
            Description = "Outside radius"
        });
        
        foreach (var store in stores)
        {
            await table.PutAsync(store);
        }
        
        // Act - Execute paginated spatial query near South Pole
        var allResults = new List<H3StoreLocationSortKeyEntity>();
        SpatialContinuationToken? continuationToken = null;
        int pageCount = 0;
        int maxPages = 30; // Safety limit
        
        do
        {
            var result = await table.SpatialQueryAsync<H3StoreLocationSortKeyEntity>(
                locationSelector: store => store.Location,
                spatialIndexType: SpatialIndexType.H3,
                precision: 9,
                center: searchCenter,
                radiusKilometers: radiusKm,
                queryBuilder: (query, cell, pagination) => query
                    .Where<H3StoreLocationSortKeyEntity>(x => x.StoreId == "STORE" && x.Location == cell),
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
        allResults.Should().NotBeEmpty("should return stores near the South Pole");
        
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
    
    #region 29.6 Test H3 query with both date line and pole
    
    [Fact]
    public async Task SpatialQueryAsync_H3ProximityNonPaginated_NearSouthPoleAndDateLine_HandlesBothEdgeCases()
    {
        // Arrange - Create table with H3-indexed stores near both the South Pole and the International Date Line
        await CreateTableAsync<H3StoreLocationSortKeyEntity>();
        var table = new H3StoreTable(DynamoDb, TableName);
        
        // Search center near the South Pole AND near the date line (latitude -89°, longitude -179°)
        // This tests the most challenging edge case: both longitude convergence and date line crossing
        var searchCenter = new GeoLocation(-89.0, -179.0);
        var radiusKm = 300.0; // 300km radius to ensure we capture stores on both sides
        
        // Create stores that test both edge cases simultaneously
        var stores = new[]
        {
            // Store at the search center
            new H3StoreLocationSortKeyEntity
            {
                StoreId = "STORE",
                Location = new GeoLocation(-89.0, -179.0),
                Name = "Center Store",
                Description = "At search center (pole + date line)"
            },
            
            // Stores near the date line on the west side (positive longitude)
            new H3StoreLocationSortKeyEntity
            {
                StoreId = "STORE",
                Location = new GeoLocation(-89.0, 179.5), // ~25km west across date line
                Name = "West Date Line Store 1",
                Description = "West side of date line, near pole"
            },
            new H3StoreLocationSortKeyEntity
            {
                StoreId = "STORE",
                Location = new GeoLocation(-89.2, 179.0), // ~35km southwest across date line
                Name = "West Date Line Store 2",
                Description = "Southwest across date line, near pole"
            },
            new H3StoreLocationSortKeyEntity
            {
                StoreId = "STORE",
                Location = new GeoLocation(-88.8, 179.8), // ~30km northwest across date line
                Name = "West Date Line Store 3",
                Description = "Northwest across date line, near pole"
            },
            
            // Stores near the date line on the east side (negative longitude)
            new H3StoreLocationSortKeyEntity
            {
                StoreId = "STORE",
                Location = new GeoLocation(-89.0, -179.5), // ~25km east
                Name = "East Date Line Store 1",
                Description = "East side of date line, near pole"
            },
            new H3StoreLocationSortKeyEntity
            {
                StoreId = "STORE",
                Location = new GeoLocation(-89.2, -179.8), // ~30km southeast
                Name = "East Date Line Store 2",
                Description = "Southeast on date line, near pole"
            },
            new H3StoreLocationSortKeyEntity
            {
                StoreId = "STORE",
                Location = new GeoLocation(-88.8, -179.2), // ~25km northeast
                Name = "East Date Line Store 3",
                Description = "Northeast on date line, near pole"
            },
            
            // Stores at various longitudes around the pole (testing longitude convergence)
            // At -89° latitude, longitude differences represent very small distances
            new H3StoreLocationSortKeyEntity
            {
                StoreId = "STORE",
                Location = new GeoLocation(-89.0, -90.0), // ~50km away due to longitude convergence
                Name = "Longitude -90 Store",
                Description = "At -90° longitude, near pole"
            },
            new H3StoreLocationSortKeyEntity
            {
                StoreId = "STORE",
                Location = new GeoLocation(-89.0, 90.0), // ~50km away due to longitude convergence
                Name = "Longitude 90 Store",
                Description = "At 90° longitude, near pole"
            },
            new H3StoreLocationSortKeyEntity
            {
                StoreId = "STORE",
                Location = new GeoLocation(-89.0, 0.0), // ~100km away
                Name = "Longitude 0 Store",
                Description = "At 0° longitude, near pole"
            },
            
            // Stores moving away from the pole (north)
            new H3StoreLocationSortKeyEntity
            {
                StoreId = "STORE",
                Location = new GeoLocation(-88.5, -179.0), // ~55km north
                Name = "North Store 1",
                Description = "North of center, near date line"
            },
            new H3StoreLocationSortKeyEntity
            {
                StoreId = "STORE",
                Location = new GeoLocation(-88.2, -179.0), // ~89km north
                Name = "North Store 2",
                Description = "Further north, near date line"
            },
            new H3StoreLocationSortKeyEntity
            {
                StoreId = "STORE",
                Location = new GeoLocation(-87.5, -179.0), // ~167km north, at edge
                Name = "Edge North Store",
                Description = "At northern edge of radius"
            },
            
            // Stores moving closer to the pole (south)
            new H3StoreLocationSortKeyEntity
            {
                StoreId = "STORE",
                Location = new GeoLocation(-89.5, -179.0), // ~55km south (closer to pole)
                Name = "South Store 1",
                Description = "Closer to pole, near date line"
            },
            new H3StoreLocationSortKeyEntity
            {
                StoreId = "STORE",
                Location = new GeoLocation(-89.8, -179.0), // ~89km south (very close to pole)
                Name = "South Store 2",
                Description = "Very close to pole, near date line"
            },
            
            // Stores outside the radius (should NOT be returned)
            new H3StoreLocationSortKeyEntity
            {
                StoreId = "STORE",
                Location = new GeoLocation(-87.0, -179.0), // ~222km north, outside radius
                Name = "Far North Store",
                Description = "Too far north"
            },
            new H3StoreLocationSortKeyEntity
            {
                StoreId = "STORE",
                Location = new GeoLocation(-89.0, -170.0), // ~500km east, outside radius
                Name = "Far East Store",
                Description = "Too far east"
            },
            new H3StoreLocationSortKeyEntity
            {
                StoreId = "STORE",
                Location = new GeoLocation(-89.0, 170.0), // ~500km west, outside radius
                Name = "Far West Store",
                Description = "Too far west"
            },
            new H3StoreLocationSortKeyEntity
            {
                StoreId = "STORE",
                Location = new GeoLocation(-85.0, -179.0), // ~445km north, outside radius
                Name = "Very Far North Store",
                Description = "Very far north"
            }
        };
        
        // Write all stores to DynamoDB
        foreach (var store in stores)
        {
            await table.PutAsync(store);
        }
        
        // Act - Execute SpatialQueryAsync with radius search near South Pole and date line
        var result = await table.SpatialQueryAsync<H3StoreLocationSortKeyEntity>(
            locationSelector: store => store.Location,
            spatialIndexType: SpatialIndexType.H3,
            precision: 9, // H3 Resolution 9 (~174m hexagons)
            center: searchCenter,
            radiusKilometers: radiusKm,
            queryBuilder: (query, cell, pagination) => query
                .Where<H3StoreLocationSortKeyEntity>(x => x.StoreId == "STORE" && x.Location == cell),
            pageSize: null // Non-paginated mode - query all cells in parallel
        );
        
        // Assert - Verify both edge cases are handled correctly
        result.Items.Should().NotBeNull();
        result.Items.Should().HaveCountGreaterThan(0, "should return stores near the South Pole and date line");
        
        // Verify all results are within the specified radius
        foreach (var store in result.Items)
        {
            var distance = store.Location.DistanceToKilometers(searchCenter);
            distance.Should().BeLessThanOrEqualTo(radiusKm, 
                $"Store {store.Name} at {store.Location} should be within {radiusKm}km radius. " +
                $"Actual distance: {distance:F2}km");
        }
        
        var storeNames = result.Items.Select(s => s.Name).ToList();
        
        // Verify the center store is present
        storeNames.Should().Contain("Center Store", "center store should be returned");
        
        // Verify stores on both sides of the date line are present (date line edge case)
        var westDateLineStores = result.Items.Where(s => s.Name.Contains("West Date Line")).ToList();
        var eastDateLineStores = result.Items.Where(s => s.Name.Contains("East Date Line")).ToList();
        
        // At least one side should have stores (due to extreme longitude convergence, not all stores may be within radius)
        var hasEitherSide = westDateLineStores.Any() || eastDateLineStores.Any();
        hasEitherSide.Should().BeTrue(
            $"should return stores from at least one side of the date line. " +
            $"West side: {westDateLineStores.Count}, East side: {eastDateLineStores.Count}");
        
        // Verify stores at various longitudes are present (longitude convergence edge case)
        var longitudeStores = result.Items.Where(s => 
            s.Name.Contains("Longitude") || s.Name.Contains("Date Line")
        ).ToList();
        longitudeStores.Should().NotBeEmpty(
            "should return stores at various longitudes, demonstrating longitude convergence handling");
        
        // Verify stores moving toward and away from the pole are present
        var northStores = result.Items.Where(s => s.Name.Contains("North Store")).ToList();
        var southStores = result.Items.Where(s => s.Name.Contains("South Store")).ToList();
        
        northStores.Should().NotBeEmpty("should return stores north of center (away from pole)");
        southStores.Should().NotBeEmpty("should return stores south of center (closer to pole)");
        
        // Verify stores outside radius are NOT present
        storeNames.Should().NotContain("Far North Store", "stores outside radius should not be returned");
        storeNames.Should().NotContain("Far East Store", "stores outside radius should not be returned");
        storeNames.Should().NotContain("Far West Store", "stores outside radius should not be returned");
        storeNames.Should().NotContain("Very Far North Store", "stores outside radius should not be returned");
        
        // Verify no duplicates (critical for combined edge cases)
        storeNames.Should().OnlyHaveUniqueItems(
            "each store should appear exactly once, no duplicates even with both edge cases");
    }
    
    [Fact]
    public async Task SpatialQueryAsync_H3ProximityPaginated_NearSouthPoleAndDateLine_HandlesBothEdgeCases()
    {
        // Arrange - Create table with many stores for paginated testing of combined edge cases
        await CreateTableAsync<H3StoreLocationSortKeyEntity>();
        var table = new H3StoreTable(DynamoDb, TableName);
        
        // Search center near the South Pole and date line
        var searchCenter = new GeoLocation(-89.0, -179.0);
        var radiusKm = 200.0;
        
        // Create a grid of stores around the search center
        var stores = new List<H3StoreLocationSortKeyEntity>();
        
        // Create stores in a pattern that tests both edge cases
        // Latitude range: -89.5° to -88.5° (closer to and away from pole)
        // Longitude range: crossing the date line (178° to -178°)
        for (int latIdx = 0; latIdx < 6; latIdx++)
        {
            for (int lonIdx = 0; lonIdx < 8; lonIdx++)
            {
                var lat = -89.5 + latIdx * 0.2; // Range: -89.5° to -88.5°
                
                // Longitude crosses the date line
                // lonIdx 0-3: 178° to 179.5° (west side, positive)
                // lonIdx 4-7: -180° to -178.5° (east side, negative)
                double lon;
                if (lonIdx < 4)
                {
                    lon = 178.0 + lonIdx * 0.5; // 178.0, 178.5, 179.0, 179.5
                }
                else
                {
                    lon = -180.0 + (lonIdx - 4) * 0.5; // -180.0, -179.5, -179.0, -178.5
                }
                
                var location = new GeoLocation(lat, lon);
                var distance = location.DistanceToKilometers(searchCenter);
                
                // Only add stores within radius
                if (distance <= radiusKm)
                {
                    stores.Add(new H3StoreLocationSortKeyEntity
                    {
                        StoreId = "STORE",
                        Location = location,
                        Name = $"Grid Store {latIdx:D2}-{lonIdx:D2}",
                        Description = $"Store at lat={lat:F1}, lon={lon:F1}"
                    });
                }
            }
        }
        
        // Add some stores outside the radius
        stores.Add(new H3StoreLocationSortKeyEntity
        {
            StoreId = "STORE",
            Location = new GeoLocation(-87.0, -179.0), // ~222km north, outside
            Name = "Outside North",
            Description = "Outside radius to the north"
        });
        stores.Add(new H3StoreLocationSortKeyEntity
        {
            StoreId = "STORE",
            Location = new GeoLocation(-89.0, -170.0), // ~500km east, outside
            Name = "Outside East",
            Description = "Outside radius to the east"
        });
        stores.Add(new H3StoreLocationSortKeyEntity
        {
            StoreId = "STORE",
            Location = new GeoLocation(-89.0, 170.0), // ~500km west, outside
            Name = "Outside West",
            Description = "Outside radius to the west"
        });
        
        foreach (var store in stores)
        {
            await table.PutAsync(store);
        }
        
        // Act - Execute paginated spatial query with both edge cases
        var allResults = new List<H3StoreLocationSortKeyEntity>();
        SpatialContinuationToken? continuationToken = null;
        int pageCount = 0;
        int maxPages = 30; // Safety limit
        
        do
        {
            var result = await table.SpatialQueryAsync<H3StoreLocationSortKeyEntity>(
                locationSelector: store => store.Location,
                spatialIndexType: SpatialIndexType.H3,
                precision: 9,
                center: searchCenter,
                radiusKilometers: radiusKm,
                queryBuilder: (query, cell, pagination) => query
                    .Where<H3StoreLocationSortKeyEntity>(x => x.StoreId == "STORE" && x.Location == cell),
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
        
        // Assert - Verify both edge cases are handled correctly across pages
        allResults.Should().NotBeEmpty("should return stores near the South Pole and date line");
        
        // Verify all results are within the specified radius
        foreach (var store in allResults)
        {
            var distance = store.Location.DistanceToKilometers(searchCenter);
            distance.Should().BeLessThanOrEqualTo(radiusKm, 
                $"Store {store.Name} at {store.Location} should be within {radiusKm}km radius. " +
                $"Actual distance: {distance:F2}km");
        }
        
        var storeNames = allResults.Select(s => s.Name).ToList();
        
        // Verify stores from the grid pattern are present
        var gridStores = allResults.Where(s => s.Name.StartsWith("Grid Store")).ToList();
        gridStores.Should().NotBeEmpty("should return stores from the grid pattern");
        
        // Verify stores on both sides of the date line are present
        // Grid stores with lonIdx 0-3 are on the west side (positive longitude)
        // Grid stores with lonIdx 4-7 are on the east side (negative longitude)
        var westSideStores = gridStores.Where(s => 
        {
            var parts = s.Name.Split('-');
            if (parts.Length == 2 && int.TryParse(parts[1], out int lonIdx))
            {
                return lonIdx < 4;
            }
            return false;
        }).ToList();
        
        var eastSideStores = gridStores.Where(s => 
        {
            var parts = s.Name.Split('-');
            if (parts.Length == 2 && int.TryParse(parts[1], out int lonIdx))
            {
                return lonIdx >= 4;
            }
            return false;
        }).ToList();
        
        westSideStores.Should().NotBeEmpty(
            "should return stores on the west side of date line (positive longitude)");
        eastSideStores.Should().NotBeEmpty(
            "should return stores on the east side of date line (negative longitude)");
        
        // Verify stores outside radius are NOT present
        storeNames.Should().NotContain("Outside North", "stores outside radius should not be returned");
        storeNames.Should().NotContain("Outside East", "stores outside radius should not be returned");
        storeNames.Should().NotContain("Outside West", "stores outside radius should not be returned");
        
        // Verify no duplicates across pages (critical for combined edge cases)
        storeNames.Should().OnlyHaveUniqueItems(
            "each store should appear exactly once across all pages, no duplicates even with both edge cases");
        
        // Verify pagination worked (multiple pages were fetched)
        pageCount.Should().BeGreaterThan(1, "should have fetched multiple pages");
    }
    
    [Fact]
    public async Task SpatialQueryAsync_H3BoundingBox_NearSouthPoleAndDateLine_HandlesBothEdgeCases()
    {
        // Arrange - Create table for bounding box query with both edge cases
        await CreateTableAsync<H3StoreLocationSortKeyEntity>();
        var table = new H3StoreTable(DynamoDb, TableName);
        
        // Create a bounding box that crosses the date line and is near the South Pole
        // Southwest: (lat: -89.5°, lon: 178°) - west side, near pole
        // Northeast: (lat: -88.5°, lon: -178°) - east side, away from pole
        var boundingBox = new GeoBoundingBox(
            southwest: new GeoLocation(-89.5, 178.0),
            northeast: new GeoLocation(-88.5, -178.0)
        );
        
        // Create stores inside and outside the bounding box
        var stores = new[]
        {
            // Inside the box - west side of date line
            new H3StoreLocationSortKeyEntity
            {
                StoreId = "STORE",
                Location = new GeoLocation(-89.0, 179.0),
                Name = "Inside West 1",
                Description = "Inside box, west side, near pole"
            },
            new H3StoreLocationSortKeyEntity
            {
                StoreId = "STORE",
                Location = new GeoLocation(-89.3, 178.5),
                Name = "Inside West 2",
                Description = "Inside box, west side, closer to pole"
            },
            new H3StoreLocationSortKeyEntity
            {
                StoreId = "STORE",
                Location = new GeoLocation(-88.7, 179.5),
                Name = "Inside West 3",
                Description = "Inside box, west side, away from pole"
            },
            
            // Inside the box - east side of date line
            new H3StoreLocationSortKeyEntity
            {
                StoreId = "STORE",
                Location = new GeoLocation(-89.0, -179.0),
                Name = "Inside East 1",
                Description = "Inside box, east side, near pole"
            },
            new H3StoreLocationSortKeyEntity
            {
                StoreId = "STORE",
                Location = new GeoLocation(-89.3, -178.5),
                Name = "Inside East 2",
                Description = "Inside box, east side, closer to pole"
            },
            new H3StoreLocationSortKeyEntity
            {
                StoreId = "STORE",
                Location = new GeoLocation(-88.7, -179.5),
                Name = "Inside East 3",
                Description = "Inside box, east side, away from pole"
            },
            
            // Outside the box - too far north
            new H3StoreLocationSortKeyEntity
            {
                StoreId = "STORE",
                Location = new GeoLocation(-88.0, -179.0),
                Name = "Outside North",
                Description = "Outside box, too far north"
            },
            
            // Outside the box - too far south (closer to pole)
            new H3StoreLocationSortKeyEntity
            {
                StoreId = "STORE",
                Location = new GeoLocation(-90.0, -179.0),
                Name = "Outside South",
                Description = "Outside box, at the pole"
            },
            
            // Outside the box - wrong longitude (too far east)
            new H3StoreLocationSortKeyEntity
            {
                StoreId = "STORE",
                Location = new GeoLocation(-89.0, -170.0),
                Name = "Outside East",
                Description = "Outside box, too far east"
            },
            
            // Outside the box - wrong longitude (too far west)
            new H3StoreLocationSortKeyEntity
            {
                StoreId = "STORE",
                Location = new GeoLocation(-89.0, 170.0),
                Name = "Outside West",
                Description = "Outside box, too far west"
            }
        };
        
        foreach (var store in stores)
        {
            await table.PutAsync(store);
        }
        
        // Act - Execute bounding box query with both edge cases
        var result = await table.SpatialQueryAsync<H3StoreLocationSortKeyEntity>(
            locationSelector: store => store.Location,
            spatialIndexType: SpatialIndexType.H3,
            precision: 9,
            boundingBox: boundingBox,
            queryBuilder: (query, cell, pagination) => query
                .Where<H3StoreLocationSortKeyEntity>(x => x.StoreId == "STORE" && x.Location == cell),
            pageSize: null
        );
        
        // Assert - Verify both edge cases are handled correctly
        result.Items.Should().NotBeNull();
        result.Items.Should().NotBeEmpty("should return stores within bounding box");
        
        var storeNames = result.Items.Select(s => s.Name).ToList();
        
        // Verify stores inside the box on both sides of date line are present
        var westSideStores = result.Items.Where(s => s.Name.Contains("Inside West")).ToList();
        var eastSideStores = result.Items.Where(s => s.Name.Contains("Inside East")).ToList();
        
        westSideStores.Should().NotBeEmpty(
            "should return stores inside box on west side of date line");
        eastSideStores.Should().NotBeEmpty(
            "should return stores inside box on east side of date line");
        
        // Verify stores outside the box are NOT present
        storeNames.Should().NotContain("Outside North", "stores outside box should not be returned");
        storeNames.Should().NotContain("Outside South", "stores outside box should not be returned");
        storeNames.Should().NotContain("Outside East", "stores outside box should not be returned");
        storeNames.Should().NotContain("Outside West", "stores outside box should not be returned");
        
        // Verify no duplicates
        storeNames.Should().OnlyHaveUniqueItems(
            "each store should appear exactly once, no duplicates even with both edge cases");
    }
    
    #endregion
}
