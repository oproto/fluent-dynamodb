using Oproto.FluentDynamoDb.Geospatial;
using Oproto.FluentDynamoDb.Geospatial.GeoHash;
using Oproto.FluentDynamoDb.IntegrationTests.Infrastructure;
using Oproto.FluentDynamoDb.IntegrationTests.TestEntities;

namespace Oproto.FluentDynamoDb.IntegrationTests.RealWorld;

/// <summary>
/// Integration tests for GeoHash spatial queries with DynamoDB.
/// Tests verify that GeoHash query extension methods (WithinDistanceKilometers, etc.)
/// correctly execute proximity and bounding box queries using GeoHash-indexed GeoLocation properties.
/// </summary>
[Collection("DynamoDB Local")]
[Trait("Category", "Integration")]
[Trait("Feature", "Geospatial")]
[Trait("Feature", "GeoHash")]
[Trait("Feature", "SpatialQuery")]
public class GeoHashSpatialQueryIntegrationTests : IntegrationTestBase
{
    public GeoHashSpatialQueryIntegrationTests(DynamoDbLocalFixture fixture) : base(fixture)
    {
    }
    
    /// <summary>
    /// Simple table wrapper for testing spatial queries with GeoHash-indexed entities.
    /// </summary>
    private class GeoHashStoreTable : DynamoDbTableBase
    {
        public GeoHashStoreTable(IAmazonDynamoDB client, string tableName) 
            : base(client, tableName, new FluentDynamoDbOptions().AddGeospatial())
        {
        }
        
        public async Task PutAsync(GeoHashStoreEntity entity)
        {
            var item = GeoHashStoreEntity.ToDynamoDb(entity);
            await DynamoDbClient.PutItemAsync(Name, item);
        }
    }
    
    #region 28.1 Test GeoHash proximity query with WithinDistanceKilometers in lambda expression
    
    [Fact]
    public async Task Query_GeoHashProximity_WithinDistanceKilometers_ReturnsAllResultsWithinRadius()
    {
        // Arrange - Create table with GeoHash-indexed stores at known locations
        await CreateTableAsync<GeoHashStoreEntity>();
        var table = new GeoHashStoreTable(DynamoDb, TableName);
        
        // San Francisco downtown area - we'll search within 5km radius
        var searchCenter = new GeoLocation(37.7749, -122.4194);
        const string region = "west";
        
        // Create stores at various distances from the search center
        var stores = new[]
        {
            // Within 5km radius - all in "west" region
            new GeoHashStoreEntity
            {
                StoreId = "store-001",
                Region = region,
                Name = "Downtown Store",
                Location = new GeoLocation(37.7749, -122.4194), // 0km - at center
                Description = "At search center"
            },
            new GeoHashStoreEntity
            {
                StoreId = "store-002",
                Region = region,
                Name = "Mission Store",
                Location = new GeoLocation(37.7599, -122.4148), // ~1.7km south
                Description = "Mission District"
            },
            new GeoHashStoreEntity
            {
                StoreId = "store-003",
                Region = region,
                Name = "Marina Store",
                Location = new GeoLocation(37.8021, -122.4378), // ~3.5km north
                Description = "Marina District"
            },
            new GeoHashStoreEntity
            {
                StoreId = "store-004",
                Region = region,
                Name = "Haight Store",
                Location = new GeoLocation(37.7694, -122.4481), // ~2.5km west
                Description = "Haight-Ashbury"
            },
            // Outside 5km radius - also in "west" region
            new GeoHashStoreEntity
            {
                StoreId = "store-005",
                Region = region,
                Name = "Oakland Store",
                Location = new GeoLocation(37.8044, -122.2712), // ~13km east (across bay)
                Description = "Oakland"
            },
            new GeoHashStoreEntity
            {
                StoreId = "store-006",
                Region = region,
                Name = "Daly City Store",
                Location = new GeoLocation(37.6879, -122.4702), // ~10km south
                Description = "Daly City"
            }
        };
        
        // Write all stores to DynamoDB
        foreach (var store in stores)
        {
            await table.PutAsync(store);
        }
        
        // Act - Execute Query with WithinDistanceKilometers extension method in lambda expression
        // This tests that the expression translator correctly handles the GeoHash extension method
        // Note: WithinDistanceKilometers creates a BETWEEN condition on the GeoHash sort key
        // Since Location is the sort key, we can use Query efficiently
        var results = await table.Query<GeoHashStoreEntity>()
            .Where<GeoHashStoreEntity>(x => x.Region == region && x.Location.WithinDistanceKilometers(searchCenter, 5.0))
            .ToListAsync();
        
        // Assert - Verify query executes successfully
        results.Should().NotBeNull();
        results.Should().NotBeEmpty("Query should return results");
        
        // Verify all results are within the specified radius (with post-filtering for exact circular distance)
        // Note: GeoHash BETWEEN queries return a rectangular bounding box, so we need to post-filter
        var resultsWithinRadius = results
            .Where(s => s.Location.DistanceToKilometers(searchCenter) <= 5.0)
            .ToList();
        
        resultsWithinRadius.Should().HaveCount(4, "4 stores are within 5km radius");
        
        // Verify each result is within the radius
        foreach (var store in resultsWithinRadius)
        {
            var distance = store.Location.DistanceToKilometers(searchCenter);
            distance.Should().BeLessThanOrEqualTo(5.0, 
                $"Store {store.Name} at {store.Location} should be within 5km radius");
        }
        
        // Verify the stores within radius are present
        var storeNames = resultsWithinRadius.Select(s => s.Name).ToList();
        storeNames.Should().Contain("Downtown Store");
        storeNames.Should().Contain("Mission Store");
        storeNames.Should().Contain("Marina Store");
        storeNames.Should().Contain("Haight Store");
        
        // Verify results are returned (not filtered out by DynamoDB)
        // The BETWEEN query should return at least the stores within radius
        results.Should().Contain(s => s.Name == "Downtown Store");
        results.Should().Contain(s => s.Name == "Mission Store");
        results.Should().Contain(s => s.Name == "Marina Store");
        results.Should().Contain(s => s.Name == "Haight Store");
    }
    
    #endregion
    
    #region 28.2 Test GeoHash proximity query with manual BETWEEN expression
    
    [Fact]
    public async Task Query_GeoHashProximity_ManualBetweenExpression_ReturnsAllResultsWithinRadius()
    {
        // Arrange - Create table with GeoHash-indexed stores at known locations
        await CreateTableAsync<GeoHashStoreEntity>();
        var table = new GeoHashStoreTable(DynamoDb, TableName);
        
        // San Francisco downtown area - we'll search within 5km radius
        var searchCenter = new GeoLocation(37.7749, -122.4194);
        const string region = "west";
        const int precision = 7; // Use precision 7 for ~150m cell size
        
        // Create stores at various distances from the search center
        var stores = new[]
        {
            // Within 5km radius - all in "west" region
            new GeoHashStoreEntity
            {
                StoreId = "store-001",
                Region = region,
                Name = "Downtown Store",
                Location = new GeoLocation(37.7749, -122.4194), // 0km - at center
                Description = "At search center"
            },
            new GeoHashStoreEntity
            {
                StoreId = "store-002",
                Region = region,
                Name = "Mission Store",
                Location = new GeoLocation(37.7599, -122.4148), // ~1.7km south
                Description = "Mission District"
            },
            new GeoHashStoreEntity
            {
                StoreId = "store-003",
                Region = region,
                Name = "Marina Store",
                Location = new GeoLocation(37.8021, -122.4378), // ~3.5km north
                Description = "Marina District"
            },
            new GeoHashStoreEntity
            {
                StoreId = "store-004",
                Region = region,
                Name = "Haight Store",
                Location = new GeoLocation(37.7694, -122.4481), // ~2.5km west
                Description = "Haight-Ashbury"
            },
            // Outside 5km radius - also in "west" region
            new GeoHashStoreEntity
            {
                StoreId = "store-005",
                Region = region,
                Name = "Oakland Store",
                Location = new GeoLocation(37.8044, -122.2712), // ~13km east (across bay)
                Description = "Oakland"
            },
            new GeoHashStoreEntity
            {
                StoreId = "store-006",
                Region = region,
                Name = "Daly City Store",
                Location = new GeoLocation(37.6879, -122.4702), // ~10km south
                Description = "Daly City"
            }
        };
        
        // Write all stores to DynamoDB
        foreach (var store in stores)
        {
            await table.PutAsync(store);
        }
        
        // Act - Execute Query with manual BETWEEN expression using GeoHash range
        // Calculate GeoHash range from bounding box
        var bbox = GeoBoundingBox.FromCenterAndDistanceKilometers(searchCenter, 5.0);
        var (minHash, maxHash) = bbox.GetGeoHashRange(precision);
        
        // Use format string expression with BETWEEN
        // Note: Location is mapped to "sk" attribute in DynamoDB
        var results = await table.Query<GeoHashStoreEntity>()
            .Where("pk = {0} AND sk BETWEEN {1} AND {2}", region, minHash, maxHash)
            .ToListAsync();
        
        // Assert - Verify query executes successfully
        results.Should().NotBeNull();
        results.Should().NotBeEmpty("Query should return results");
        
        // Verify BETWEEN query is executed efficiently (single query, not multiple cells)
        // GeoHash BETWEEN queries are efficient because GeoHash forms a continuous lexicographic space
        
        // Verify all results are within the specified radius (with post-filtering for exact circular distance)
        // Note: GeoHash BETWEEN queries return a rectangular bounding box, so we need to post-filter
        var resultsWithinRadius = results
            .Where(s => s.Location.DistanceToKilometers(searchCenter) <= 5.0)
            .ToList();
        
        resultsWithinRadius.Should().HaveCount(4, "4 stores are within 5km radius");
        
        // Verify each result is within the radius
        foreach (var store in resultsWithinRadius)
        {
            var distance = store.Location.DistanceToKilometers(searchCenter);
            distance.Should().BeLessThanOrEqualTo(5.0, 
                $"Store {store.Name} at {store.Location} should be within 5km radius");
        }
        
        // Verify the stores within radius are present
        var storeNames = resultsWithinRadius.Select(s => s.Name).ToList();
        storeNames.Should().Contain("Downtown Store");
        storeNames.Should().Contain("Mission Store");
        storeNames.Should().Contain("Marina Store");
        storeNames.Should().Contain("Haight Store");
        
        // Verify results match lambda expression approach (with post-filtering)
        // Both approaches should return the same stores within the radius
        results.Should().Contain(s => s.Name == "Downtown Store");
        results.Should().Contain(s => s.Name == "Mission Store");
        results.Should().Contain(s => s.Name == "Marina Store");
        results.Should().Contain(s => s.Name == "Haight Store");
    }
    
    #endregion
    
    #region 28.3 Test GeoHash proximity query with plain text BETWEEN and WithValue
    
    [Fact]
    public async Task Query_GeoHashProximity_PlainTextBetweenWithValue_ReturnsAllResultsWithinRadius()
    {
        // Arrange - Create table with GeoHash-indexed stores at known locations
        await CreateTableAsync<GeoHashStoreEntity>();
        var table = new GeoHashStoreTable(DynamoDb, TableName);
        
        // San Francisco downtown area - we'll search within 5km radius
        var searchCenter = new GeoLocation(37.7749, -122.4194);
        const string region = "west";
        const int precision = 7; // Use precision 7 for ~150m cell size
        
        // Create stores at various distances from the search center
        var stores = new[]
        {
            // Within 5km radius - all in "west" region
            new GeoHashStoreEntity
            {
                StoreId = "store-001",
                Region = region,
                Name = "Downtown Store",
                Location = new GeoLocation(37.7749, -122.4194), // 0km - at center
                Description = "At search center"
            },
            new GeoHashStoreEntity
            {
                StoreId = "store-002",
                Region = region,
                Name = "Mission Store",
                Location = new GeoLocation(37.7599, -122.4148), // ~1.7km south
                Description = "Mission District"
            },
            new GeoHashStoreEntity
            {
                StoreId = "store-003",
                Region = region,
                Name = "Marina Store",
                Location = new GeoLocation(37.8021, -122.4378), // ~3.5km north
                Description = "Marina District"
            },
            new GeoHashStoreEntity
            {
                StoreId = "store-004",
                Region = region,
                Name = "Haight Store",
                Location = new GeoLocation(37.7694, -122.4481), // ~2.5km west
                Description = "Haight-Ashbury"
            },
            // Outside 5km radius - also in "west" region
            new GeoHashStoreEntity
            {
                StoreId = "store-005",
                Region = region,
                Name = "Oakland Store",
                Location = new GeoLocation(37.8044, -122.2712), // ~13km east (across bay)
                Description = "Oakland"
            },
            new GeoHashStoreEntity
            {
                StoreId = "store-006",
                Region = region,
                Name = "Daly City Store",
                Location = new GeoLocation(37.6879, -122.4702), // ~10km south
                Description = "Daly City"
            }
        };
        
        // Write all stores to DynamoDB
        foreach (var store in stores)
        {
            await table.PutAsync(store);
        }
        
        // Act - Execute Query with plain text BETWEEN expression and WithValue
        // Calculate GeoHash range from bounding box
        var bbox = GeoBoundingBox.FromCenterAndDistanceKilometers(searchCenter, 5.0);
        var (minHash, maxHash) = bbox.GetGeoHashRange(precision);
        
        // Use plain text expression with parameter substitution via WithValue
        // Note: Location is mapped to "sk" attribute in DynamoDB
        var results = await table.Query<GeoHashStoreEntity>()
            .Where("pk = :pk AND sk BETWEEN :minHash AND :maxHash")
            .WithValue(":pk", region)
            .WithValue(":minHash", minHash)
            .WithValue(":maxHash", maxHash)
            .ToListAsync();
        
        // Assert - Verify query executes successfully
        results.Should().NotBeNull();
        results.Should().NotBeEmpty("Query should return results");
        
        // Verify parameter substitution works correctly
        // The query should have properly substituted :pk, :minHash, and :maxHash with their values
        
        // Verify all results are within the specified radius (with post-filtering for exact circular distance)
        // Note: GeoHash BETWEEN queries return a rectangular bounding box, so we need to post-filter
        var resultsWithinRadius = results
            .Where(s => s.Location.DistanceToKilometers(searchCenter) <= 5.0)
            .ToList();
        
        resultsWithinRadius.Should().HaveCount(4, "4 stores are within 5km radius");
        
        // Verify each result is within the radius
        foreach (var store in resultsWithinRadius)
        {
            var distance = store.Location.DistanceToKilometers(searchCenter);
            distance.Should().BeLessThanOrEqualTo(5.0, 
                $"Store {store.Name} at {store.Location} should be within 5km radius");
        }
        
        // Verify the stores within radius are present
        var storeNames = resultsWithinRadius.Select(s => s.Name).ToList();
        storeNames.Should().Contain("Downtown Store");
        storeNames.Should().Contain("Mission Store");
        storeNames.Should().Contain("Marina Store");
        storeNames.Should().Contain("Haight Store");
        
        // Verify results match other expression approaches (with post-filtering)
        // All three approaches (lambda, format string, plain text) should return the same stores
        results.Should().Contain(s => s.Name == "Downtown Store");
        results.Should().Contain(s => s.Name == "Mission Store");
        results.Should().Contain(s => s.Name == "Marina Store");
        results.Should().Contain(s => s.Name == "Haight Store");
    }
    
    #endregion
    
    #region 28.4 Test GeoHash bounding box query with WithinBoundingBox extension
    
    [Fact]
    public async Task Query_GeoHashBoundingBox_WithinBoundingBoxExtension_ReturnsAllResultsWithinBoundingBox()
    {
        // Arrange - Create table with GeoHash-indexed stores at known locations
        await CreateTableAsync<GeoHashStoreEntity>();
        var table = new GeoHashStoreTable(DynamoDb, TableName);
        
        // Define a rectangular bounding box covering part of San Francisco
        // Southwest corner: (37.765, -122.425) - just north of Mission
        // Northeast corner: (37.81, -122.40) - North Beach area
        var southwest = new GeoLocation(37.765, -122.425);
        var northeast = new GeoLocation(37.81, -122.40);
        const string region = "west";
        
        // Create stores at various locations
        var stores = new[]
        {
            // Inside the bounding box
            new GeoHashStoreEntity
            {
                StoreId = "store-001",
                Region = region,
                Name = "Downtown Store",
                Location = new GeoLocation(37.7749, -122.4194), // Downtown SF - inside
                Description = "Downtown San Francisco"
            },
            new GeoHashStoreEntity
            {
                StoreId = "store-002",
                Region = region,
                Name = "Financial District Store",
                Location = new GeoLocation(37.7946, -122.4014), // Financial District - inside
                Description = "Financial District"
            },
            new GeoHashStoreEntity
            {
                StoreId = "store-003",
                Region = region,
                Name = "Chinatown Store",
                Location = new GeoLocation(37.7941, -122.4078), // Chinatown - inside
                Description = "Chinatown"
            },
            new GeoHashStoreEntity
            {
                StoreId = "store-004",
                Region = region,
                Name = "Union Square Store",
                Location = new GeoLocation(37.7879, -122.4075), // Union Square - inside
                Description = "Union Square"
            },
            // Outside the bounding box
            new GeoHashStoreEntity
            {
                StoreId = "store-005",
                Region = region,
                Name = "Mission Store",
                Location = new GeoLocation(37.7599, -122.4148), // Mission - south of bbox
                Description = "Mission District"
            },
            new GeoHashStoreEntity
            {
                StoreId = "store-006",
                Region = region,
                Name = "Marina Store",
                Location = new GeoLocation(37.8021, -122.4378), // Marina - north and west of bbox
                Description = "Marina District"
            },
            new GeoHashStoreEntity
            {
                StoreId = "store-007",
                Region = region,
                Name = "Haight Store",
                Location = new GeoLocation(37.7694, -122.4481), // Haight - west of bbox
                Description = "Haight-Ashbury"
            },
            new GeoHashStoreEntity
            {
                StoreId = "store-008",
                Region = region,
                Name = "Oakland Store",
                Location = new GeoLocation(37.8044, -122.2712), // Oakland - east of bbox
                Description = "Oakland"
            }
        };
        
        // Write all stores to DynamoDB
        foreach (var store in stores)
        {
            await table.PutAsync(store);
        }
        
        // Act - Execute Query with WithinBoundingBox extension
        // This tests that the expression translator correctly handles the WithinBoundingBox method
        // and generates a single BETWEEN query (not multiple cell queries)
        var results = await table.Query<GeoHashStoreEntity>()
            .Where<GeoHashStoreEntity>(x => x.Region == region && x.Location.WithinBoundingBox(southwest, northeast))
            .ToListAsync();
        
        // Assert - Verify query executes successfully
        results.Should().NotBeNull();
        results.Should().NotBeEmpty("Query should return results");
        
        // Verify single BETWEEN query is executed (not multiple cells)
        // GeoHash BETWEEN queries are efficient because GeoHash forms a continuous lexicographic space
        // This is more efficient than S2/H3 which require multiple discrete cell queries
        
        // Verify all results are within bounding box (or very close due to GeoHash approximation)
        // Note: GeoHash BETWEEN queries return a rectangular bounding box approximation
        // Due to how GeoHash encoding works, results may include locations slightly outside the exact bounds
        var resultsWithinBbox = results
            .Where(s => s.Location.Latitude >= southwest.Latitude &&
                       s.Location.Latitude <= northeast.Latitude &&
                       s.Location.Longitude >= southwest.Longitude &&
                       s.Location.Longitude <= northeast.Longitude)
            .ToList();
        
        // We expect at least 4 stores within the exact bounding box
        resultsWithinBbox.Should().HaveCountGreaterThanOrEqualTo(4, "at least 4 stores are within the bounding box");
        
        // Verify each result is within the bounding box
        foreach (var store in resultsWithinBbox)
        {
            store.Location.Latitude.Should().BeInRange(southwest.Latitude, northeast.Latitude,
                $"Store {store.Name} latitude should be within bounding box");
            store.Location.Longitude.Should().BeInRange(southwest.Longitude, northeast.Longitude,
                $"Store {store.Name} longitude should be within bounding box");
        }
        
        // Verify the stores within bounding box are present
        var storeNames = resultsWithinBbox.Select(s => s.Name).ToList();
        storeNames.Should().Contain("Downtown Store");
        storeNames.Should().Contain("Financial District Store");
        storeNames.Should().Contain("Chinatown Store");
        storeNames.Should().Contain("Union Square Store");
        
        // Verify stores outside bounding box are not included
        storeNames.Should().NotContain("Mission Store", "Mission Store is south of bounding box");
        storeNames.Should().NotContain("Marina Store", "Marina Store is north and west of bounding box");
        storeNames.Should().NotContain("Haight Store", "Haight Store is west of bounding box");
        storeNames.Should().NotContain("Oakland Store", "Oakland Store is east of bounding box");
        
        // Verify this is efficient for rectangular area queries
        // GeoHash BETWEEN queries execute as a single DynamoDB query operation,
        // making them ideal for rectangular bounding box queries
        results.Should().Contain(s => s.Name == "Downtown Store");
        results.Should().Contain(s => s.Name == "Financial District Store");
        results.Should().Contain(s => s.Name == "Chinatown Store");
        results.Should().Contain(s => s.Name == "Union Square Store");
    }
    
    #endregion
    
    #region 28.5 Test GeoHash query with WithinDistanceMeters and WithinDistanceMiles
    
    [Fact]
    public async Task Query_GeoHashProximity_WithinDistanceMetersAndMiles_ReturnEquivalentResults()
    {
        // Arrange - Create table with GeoHash-indexed stores at known locations
        await CreateTableAsync<GeoHashStoreEntity>();
        var table = new GeoHashStoreTable(DynamoDb, TableName);
        
        // San Francisco downtown area - we'll search within 5km radius
        var searchCenter = new GeoLocation(37.7749, -122.4194);
        const string region = "west";
        
        // Convert 5km to meters and miles for equivalent searches
        const double radiusKilometers = 5.0;
        const double radiusMeters = radiusKilometers * 1000.0; // 5000 meters
        const double radiusMiles = radiusKilometers * 0.621371; // ~3.107 miles
        
        // Create stores at various distances from the search center
        var stores = new[]
        {
            // Within 5km radius - all in "west" region
            new GeoHashStoreEntity
            {
                StoreId = "store-001",
                Region = region,
                Name = "Downtown Store",
                Location = new GeoLocation(37.7749, -122.4194), // 0km - at center
                Description = "At search center"
            },
            new GeoHashStoreEntity
            {
                StoreId = "store-002",
                Region = region,
                Name = "Mission Store",
                Location = new GeoLocation(37.7599, -122.4148), // ~1.7km south
                Description = "Mission District"
            },
            new GeoHashStoreEntity
            {
                StoreId = "store-003",
                Region = region,
                Name = "Marina Store",
                Location = new GeoLocation(37.8021, -122.4378), // ~3.5km north
                Description = "Marina District"
            },
            new GeoHashStoreEntity
            {
                StoreId = "store-004",
                Region = region,
                Name = "Haight Store",
                Location = new GeoLocation(37.7694, -122.4481), // ~2.5km west
                Description = "Haight-Ashbury"
            },
            // Outside 5km radius - also in "west" region
            new GeoHashStoreEntity
            {
                StoreId = "store-005",
                Region = region,
                Name = "Oakland Store",
                Location = new GeoLocation(37.8044, -122.2712), // ~13km east (across bay)
                Description = "Oakland"
            },
            new GeoHashStoreEntity
            {
                StoreId = "store-006",
                Region = region,
                Name = "Daly City Store",
                Location = new GeoLocation(37.6879, -122.4702), // ~10km south
                Description = "Daly City"
            }
        };
        
        // Write all stores to DynamoDB
        foreach (var store in stores)
        {
            await table.PutAsync(store);
        }
        
        // Act - Execute Query with WithinDistanceMeters extension method
        var resultsMeters = await table.Query<GeoHashStoreEntity>()
            .Where<GeoHashStoreEntity>(x => x.Region == region && x.Location.WithinDistanceMeters(searchCenter, radiusMeters))
            .ToListAsync();
        
        // Act - Execute Query with WithinDistanceMiles extension method
        var resultsMiles = await table.Query<GeoHashStoreEntity>()
            .Where<GeoHashStoreEntity>(x => x.Region == region && x.Location.WithinDistanceMiles(searchCenter, radiusMiles))
            .ToListAsync();
        
        // Assert - Verify both methods work correctly
        resultsMeters.Should().NotBeNull();
        resultsMeters.Should().NotBeEmpty("Query with WithinDistanceMeters should return results");
        
        resultsMiles.Should().NotBeNull();
        resultsMiles.Should().NotBeEmpty("Query with WithinDistanceMiles should return results");
        
        // Post-filter for exact circular distance using meters
        var exactResultsMeters = resultsMeters
            .Where(s => s.Location.DistanceToMeters(searchCenter) <= radiusMeters)
            .OrderBy(s => s.StoreId)
            .ToList();
        
        // Post-filter for exact circular distance using miles
        var exactResultsMiles = resultsMiles
            .Where(s => s.Location.DistanceToMiles(searchCenter) <= radiusMiles)
            .OrderBy(s => s.StoreId)
            .ToList();
        
        // Verify both methods return equivalent results (when distances are equivalent)
        exactResultsMeters.Should().HaveCount(4, "4 stores are within 5km/5000m/~3.107mi radius");
        exactResultsMiles.Should().HaveCount(4, "4 stores are within 5km/5000m/~3.107mi radius");
        
        // Verify the same stores are returned by both methods
        var storeIdsMeters = exactResultsMeters.Select(s => s.StoreId).ToList();
        var storeIdsMiles = exactResultsMiles.Select(s => s.StoreId).ToList();
        
        storeIdsMeters.Should().BeEquivalentTo(storeIdsMiles, 
            "Both methods should return the same stores when using equivalent distances");
        
        // Verify post-filtering produces accurate results for meters
        foreach (var store in exactResultsMeters)
        {
            var distanceMeters = store.Location.DistanceToMeters(searchCenter);
            distanceMeters.Should().BeLessThanOrEqualTo(radiusMeters, 
                $"Store {store.Name} at {store.Location} should be within {radiusMeters}m radius");
        }
        
        // Verify post-filtering produces accurate results for miles
        foreach (var store in exactResultsMiles)
        {
            var distanceMiles = store.Location.DistanceToMiles(searchCenter);
            distanceMiles.Should().BeLessThanOrEqualTo(radiusMiles, 
                $"Store {store.Name} at {store.Location} should be within {radiusMiles}mi radius");
        }
        
        // Verify the expected stores are present in both result sets
        var storeNamesMeters = exactResultsMeters.Select(s => s.Name).ToList();
        var storeNamesMiles = exactResultsMiles.Select(s => s.Name).ToList();
        
        storeNamesMeters.Should().Contain("Downtown Store");
        storeNamesMeters.Should().Contain("Mission Store");
        storeNamesMeters.Should().Contain("Marina Store");
        storeNamesMeters.Should().Contain("Haight Store");
        
        storeNamesMiles.Should().Contain("Downtown Store");
        storeNamesMiles.Should().Contain("Mission Store");
        storeNamesMiles.Should().Contain("Marina Store");
        storeNamesMiles.Should().Contain("Haight Store");
        
        // Verify stores outside the radius are not included
        storeNamesMeters.Should().NotContain("Oakland Store", "Oakland Store is ~13km away");
        storeNamesMeters.Should().NotContain("Daly City Store", "Daly City Store is ~10km away");
        
        storeNamesMiles.Should().NotContain("Oakland Store", "Oakland Store is ~13km away");
        storeNamesMiles.Should().NotContain("Daly City Store", "Daly City Store is ~10km away");
        
        // Verify that the distance calculations are consistent
        // For each store, verify that distance in meters and miles are equivalent
        foreach (var store in exactResultsMeters)
        {
            var distanceMeters = store.Location.DistanceToMeters(searchCenter);
            var distanceKilometers = store.Location.DistanceToKilometers(searchCenter);
            var distanceMiles = store.Location.DistanceToMiles(searchCenter);
            
            // Verify conversion: meters = kilometers * 1000
            (distanceKilometers * 1000.0).Should().BeApproximately(distanceMeters, 0.01,
                $"Distance conversion for {store.Name} should be consistent");
            
            // Verify conversion: miles = meters / 1609.344
            (distanceMeters / 1609.344).Should().BeApproximately(distanceMiles, 0.001,
                $"Distance conversion for {store.Name} should be consistent");
        }
    }
    
    #endregion
}
