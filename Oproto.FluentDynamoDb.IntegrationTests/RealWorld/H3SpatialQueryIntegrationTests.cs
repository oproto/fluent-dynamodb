using Amazon.DynamoDBv2;
using Oproto.FluentDynamoDb.Attributes;
using Oproto.FluentDynamoDb.Geospatial;
using Oproto.FluentDynamoDb.Geospatial.H3;
using Oproto.FluentDynamoDb.IntegrationTests.Infrastructure;
using Oproto.FluentDynamoDb.IntegrationTests.TestEntities;
using Oproto.FluentDynamoDb.Storage;

namespace Oproto.FluentDynamoDb.IntegrationTests.RealWorld;

/// <summary>
/// Integration tests for H3 spatial queries with DynamoDB.
/// Tests verify that SpatialQueryAsync correctly executes proximity and bounding box queries
/// using H3-indexed GeoLocation properties.
/// </summary>
[Collection("DynamoDB Local")]
[Trait("Category", "Integration")]
[Trait("Feature", "Geospatial")]
[Trait("Feature", "H3")]
[Trait("Feature", "SpatialQuery")]
public class H3SpatialQueryIntegrationTests : IntegrationTestBase
{
    public H3SpatialQueryIntegrationTests(DynamoDbLocalFixture fixture) : base(fixture)
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
    
    #region 27.1 Test H3 proximity query (non-paginated) with lambda expressions
    
    [Fact]
    public async Task SpatialQueryAsync_H3ProximityNonPaginated_WithLambdaExpression_ReturnsAllResultsWithinRadius()
    {
        // Arrange - Create table with H3-indexed stores at known locations
        await CreateTableAsync<H3StoreLocationSortKeyEntity>();
        var table = new H3StoreTable(DynamoDb, TableName);
        
        // San Francisco downtown area - we'll search within 5km radius
        var searchCenter = new GeoLocation(37.7749, -122.4194);
        
        // Create stores at various distances from the search center
        var stores = new[]
        {
            // Within 5km radius
            new H3StoreLocationSortKeyEntity
            {
                StoreId = "STORE",
                Location = new GeoLocation(37.7749, -122.4194), // 0km - at center
                Name = "Downtown Store",
                Description = "At search center"
            },
            new H3StoreLocationSortKeyEntity
            {
                StoreId = "STORE",
                Location = new GeoLocation(37.7599, -122.4148), // ~1.7km south
                Name = "Mission Store",
                Description = "Mission District"
            },
            new H3StoreLocationSortKeyEntity
            {
                StoreId = "STORE",
                Location = new GeoLocation(37.8021, -122.4378), // ~3.5km north
                Name = "Marina Store",
                Description = "Marina District"
            },
            new H3StoreLocationSortKeyEntity
            {
                StoreId = "STORE",
                Location = new GeoLocation(37.7694, -122.4481), // ~2.5km west
                Name = "Haight Store",
                Description = "Haight-Ashbury"
            },
            // Outside 5km radius
            new H3StoreLocationSortKeyEntity
            {
                StoreId = "STORE",
                Location = new GeoLocation(37.8044, -122.2712), // ~13km east (across bay)
                Name = "Oakland Store",
                Description = "Oakland"
            },
            new H3StoreLocationSortKeyEntity
            {
                StoreId = "STORE",
                Location = new GeoLocation(37.6879, -122.4702), // ~10km south
                Name = "Daly City Store",
                Description = "Daly City"
            }
        };
        
        // Write all stores to DynamoDB
        foreach (var store in stores)
        {
            await table.PutAsync(store);
        }
        
        // Act - Execute SpatialQueryAsync with radius search using lambda expression
        // Test the implicit cast: x.Location == cell compares GeoLocation.SpatialIndex to cell
        var result = await table.SpatialQueryAsync<H3StoreLocationSortKeyEntity>(
            locationSelector: store => store.Location,
            spatialIndexType: SpatialIndexType.H3,
            precision: 9, // H3 Resolution 9 (~174m hexagons)
            center: searchCenter,
            radiusKilometers: 5.0,
            queryBuilder: (query, cell, pagination) => query
                .Where<H3StoreLocationSortKeyEntity>(x => x.StoreId == "STORE" && x.Location == cell)
                ,
            pageSize: null // Non-paginated mode - query all cells in parallel
        );
        
        // Assert - Verify all results are within radius
        result.Items.Should().NotBeNull();
        result.Items.Should().HaveCount(4, "4 stores are within 5km radius");
        
        // Verify each result is within the radius
        foreach (var store in result.Items)
        {
            var distance = store.Location.DistanceToKilometers(searchCenter);
            distance.Should().BeLessThanOrEqualTo(5.0, 
                $"Store {store.Name} at {store.Location} should be within 5km radius");
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
    public async Task SpatialQueryAsync_H3ProximityNonPaginated_WithExplicitSpatialIndexProperty_ReturnsAllResultsWithinRadius()
    {
        // Arrange - Create table with H3-indexed stores at known locations
        await CreateTableAsync<H3StoreLocationSortKeyEntity>();
        var table = new H3StoreTable(DynamoDb, TableName);
        
        // San Francisco downtown area - we'll search within 5km radius
        var searchCenter = new GeoLocation(37.7749, -122.4194);
        
        // Create stores at various distances from the search center
        var stores = new[]
        {
            // Within 5km radius
            new H3StoreLocationSortKeyEntity
            {
                StoreId = "STORE",
                Location = new GeoLocation(37.7749, -122.4194), // 0km - at center
                Name = "Downtown Store",
                Description = "At search center"
            },
            new H3StoreLocationSortKeyEntity
            {
                StoreId = "STORE",
                Location = new GeoLocation(37.7599, -122.4148), // ~1.7km south
                Name = "Mission Store",
                Description = "Mission District"
            },
            new H3StoreLocationSortKeyEntity
            {
                StoreId = "STORE",
                Location = new GeoLocation(37.8021, -122.4378), // ~3.5km north
                Name = "Marina Store",
                Description = "Marina District"
            },
            // Outside 5km radius
            new H3StoreLocationSortKeyEntity
            {
                StoreId = "STORE",
                Location = new GeoLocation(37.8044, -122.2712), // ~13km east (across bay)
                Name = "Oakland Store",
                Description = "Oakland"
            }
        };
        
        // Write all stores to DynamoDB
        foreach (var store in stores)
        {
            await table.PutAsync(store);
        }
        
        // Act - Execute SpatialQueryAsync with radius search using explicit SpatialIndex property
        // Test the explicit property access: x.Location.SpatialIndex == cell
        var result = await table.SpatialQueryAsync<H3StoreLocationSortKeyEntity>(
            locationSelector: store => store.Location,
            spatialIndexType: SpatialIndexType.H3,
            precision: 9, // H3 Resolution 9 (~174m hexagons)
            center: searchCenter,
            radiusKilometers: 5.0,
            queryBuilder: (query, cell, pagination) => query
                .Where<H3StoreLocationSortKeyEntity>(x => x.StoreId == "STORE" && x.Location.SpatialIndex == cell)
                ,
            pageSize: null // Non-paginated mode - query all cells in parallel
        );
        
        // Assert - Verify all results are within radius
        result.Items.Should().NotBeNull();
        result.Items.Should().HaveCount(3, "3 stores are within 5km radius");
        
        // Verify each result is within the radius
        foreach (var store in result.Items)
        {
            var distance = store.Location.DistanceToKilometers(searchCenter);
            distance.Should().BeLessThanOrEqualTo(5.0, 
                $"Store {store.Name} at {store.Location} should be within 5km radius");
        }
        
        // Verify the stores within radius are present
        var storeNames = result.Items.Select(s => s.Name).ToList();
        storeNames.Should().Contain("Downtown Store");
        storeNames.Should().Contain("Mission Store");
        storeNames.Should().Contain("Marina Store");
        
        // Verify the stores outside radius are NOT present
        storeNames.Should().NotContain("Oakland Store");
    }
    
    [Fact]
    public async Task SpatialQueryAsync_H3ProximityNonPaginated_ResultsAreSortedByDistance()
    {
        // Arrange - Create table with H3-indexed stores
        await CreateTableAsync<H3StoreLocationSortKeyEntity>();
        var table = new H3StoreTable(DynamoDb, TableName);
        
        var searchCenter = new GeoLocation(37.7749, -122.4194);
        
        // Create stores at specific distances
        var stores = new[]
        {
            new H3StoreLocationSortKeyEntity
            {
                StoreId = "STORE",
                Location = new GeoLocation(37.7599, -122.4148), // ~1.7km
                Name = "Far Store",
                Description = "Farther away"
            },
            new H3StoreLocationSortKeyEntity
            {
                StoreId = "STORE",
                Location = new GeoLocation(37.7749, -122.4194), // 0km - at center
                Name = "Center Store",
                Description = "At center"
            },
            new H3StoreLocationSortKeyEntity
            {
                StoreId = "STORE",
                Location = new GeoLocation(37.7769, -122.4214), // ~0.3km
                Name = "Near Store",
                Description = "Very close"
            }
        };
        
        foreach (var store in stores)
        {
            await table.PutAsync(store);
        }
        
        // Act - Execute spatial query using implicit cast
        var result = await table.SpatialQueryAsync<H3StoreLocationSortKeyEntity>(
            locationSelector: store => store.Location,
            spatialIndexType: SpatialIndexType.H3,
            precision: 9,
            center: searchCenter,
            radiusKilometers: 5.0,
            queryBuilder: (query, cell, pagination) => query
                .Where<H3StoreLocationSortKeyEntity>(x => x.StoreId == "STORE" && x.Location == cell)
                ,
            pageSize: null
        );
        
        // Assert - Verify results are sorted by distance (closest first)
        result.Items.Should().HaveCount(3);
        
        // Calculate distances for verification
        var distances = result.Items
            .Select(s => new { Store = s, Distance = s.Location.DistanceToKilometers(searchCenter) })
            .ToList();
        
        // Verify sorted order
        distances[0].Store.Name.Should().Be("Center Store", "closest store should be first");
        distances[0].Distance.Should().BeLessThan(0.1);
        
        distances[1].Store.Name.Should().Be("Near Store", "second closest should be second");
        distances[1].Distance.Should().BeInRange(0.2, 0.4);
        
        distances[2].Store.Name.Should().Be("Far Store", "farthest should be last");
        distances[2].Distance.Should().BeInRange(1.5, 2.0);
        
        // Verify distances are in ascending order
        for (int i = 1; i < distances.Count; i++)
        {
            distances[i].Distance.Should().BeGreaterThanOrEqualTo(distances[i - 1].Distance,
                "results should be sorted by distance in ascending order");
        }
    }
    
    [Fact]
    public async Task SpatialQueryAsync_H3ProximityNonPaginated_NoDuplicates()
    {
        // Arrange - Create table with H3-indexed stores
        await CreateTableAsync<H3StoreLocationSortKeyEntity>();
        var table = new H3StoreTable(DynamoDb, TableName);
        
        var searchCenter = new GeoLocation(37.7749, -122.4194);
        
        // Create stores that might appear in multiple H3 cells
        // (stores near cell boundaries could theoretically appear in multiple cells)
        var stores = new[]
        {
            new H3StoreLocationSortKeyEntity
            {
                StoreId = "STORE",
                Location = new GeoLocation(37.7749, -122.4194),
                Name = "Store 1",
                Description = "Test store 1"
            },
            new H3StoreLocationSortKeyEntity
            {
                StoreId = "STORE",
                Location = new GeoLocation(37.7759, -122.4204),
                Name = "Store 2",
                Description = "Test store 2"
            },
            new H3StoreLocationSortKeyEntity
            {
                StoreId = "STORE",
                Location = new GeoLocation(37.7739, -122.4184),
                Name = "Store 3",
                Description = "Test store 3"
            },
            new H3StoreLocationSortKeyEntity
            {
                StoreId = "STORE",
                Location = new GeoLocation(37.7769, -122.4214),
                Name = "Store 4",
                Description = "Test store 4"
            }
        };
        
        foreach (var store in stores)
        {
            await table.PutAsync(store);
        }
        
        // Act - Execute spatial query using implicit cast
        var result = await table.SpatialQueryAsync<H3StoreLocationSortKeyEntity>(
            locationSelector: store => store.Location,
            spatialIndexType: SpatialIndexType.H3,
            precision: 9,
            center: searchCenter,
            radiusKilometers: 2.0,
            queryBuilder: (query, cell, pagination) => query
                .Where<H3StoreLocationSortKeyEntity>(x => x.StoreId == "STORE" && x.Location == cell)
                ,
            pageSize: null
        );
        
        // Assert - Verify no duplicates
        result.Items.Should().NotBeNull();
        result.Items.Should().HaveCount(4, "should return 4 unique stores");
        
        // Verify each store appears exactly once by checking unique names
        var names = result.Items.Select(s => s.Name).ToList();
        names.Should().OnlyHaveUniqueItems("each store should appear exactly once");
        
        // Verify all expected stores are present
        names.Should().Contain("Store 1");
        names.Should().Contain("Store 2");
        names.Should().Contain("Store 3");
        names.Should().Contain("Store 4");
    }
    
    #endregion
    
    #region 27.2 Test H3 proximity query (non-paginated) with format string expressions
    
    [Fact]
    public async Task SpatialQueryAsync_H3ProximityNonPaginated_WithFormatStringExpression_ReturnsAllResultsWithinRadius()
    {
        // Arrange - Create table with H3-indexed stores at known locations
        await CreateTableAsync<H3StoreLocationSortKeyEntity>();
        var table = new H3StoreTable(DynamoDb, TableName);
        
        // San Francisco downtown area - we'll search within 5km radius
        var searchCenter = new GeoLocation(37.7749, -122.4194);
        
        // Create stores at various distances from the search center
        var stores = new[]
        {
            // Within 5km radius
            new H3StoreLocationSortKeyEntity
            {
                StoreId = "STORE",
                Location = new GeoLocation(37.7749, -122.4194), // 0km - at center
                Name = "Downtown Store",
                Description = "At search center"
            },
            new H3StoreLocationSortKeyEntity
            {
                StoreId = "STORE",
                Location = new GeoLocation(37.7599, -122.4148), // ~1.7km south
                Name = "Mission Store",
                Description = "Mission District"
            },
            new H3StoreLocationSortKeyEntity
            {
                StoreId = "STORE",
                Location = new GeoLocation(37.8021, -122.4378), // ~3.5km north
                Name = "Marina Store",
                Description = "Marina District"
            },
            new H3StoreLocationSortKeyEntity
            {
                StoreId = "STORE",
                Location = new GeoLocation(37.7694, -122.4481), // ~2.5km west
                Name = "Haight Store",
                Description = "Haight-Ashbury"
            },
            // Outside 5km radius
            new H3StoreLocationSortKeyEntity
            {
                StoreId = "STORE",
                Location = new GeoLocation(37.8044, -122.2712), // ~13km east (across bay)
                Name = "Oakland Store",
                Description = "Oakland"
            },
            new H3StoreLocationSortKeyEntity
            {
                StoreId = "STORE",
                Location = new GeoLocation(37.6879, -122.4702), // ~10km south
                Name = "Daly City Store",
                Description = "Daly City"
            }
        };
        
        // Write all stores to DynamoDB
        foreach (var store in stores)
        {
            await table.PutAsync(store);
        }
        
        // Act - Execute SpatialQueryAsync with radius search using format string expression
        // Test format string syntax: query.Where("pk = {0} AND sk = {1}", "STORE", cell)
        var result = await table.SpatialQueryAsync<H3StoreLocationSortKeyEntity>(
            locationSelector: store => store.Location,
            spatialIndexType: SpatialIndexType.H3,
            precision: 9, // H3 Resolution 9 (~174m hexagons)
            center: searchCenter,
            radiusKilometers: 5.0,
            queryBuilder: (query, cell, pagination) => query
                .Where("pk = {0} AND sk = {1}", "STORE", cell)
                ,
            pageSize: null // Non-paginated mode - query all cells in parallel
        );
        
        // Assert - Verify all results are within radius
        result.Items.Should().NotBeNull();
        result.Items.Should().HaveCount(4, "4 stores are within 5km radius");
        
        // Verify each result is within the radius
        foreach (var store in result.Items)
        {
            var distance = store.Location.DistanceToKilometers(searchCenter);
            distance.Should().BeLessThanOrEqualTo(5.0, 
                $"Store {store.Name} at {store.Location} should be within 5km radius");
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
    public async Task SpatialQueryAsync_H3ProximityNonPaginated_FormatStringMatchesLambdaResults()
    {
        // Arrange - Create table with H3-indexed stores
        await CreateTableAsync<H3StoreLocationSortKeyEntity>();
        var table = new H3StoreTable(DynamoDb, TableName);
        
        var searchCenter = new GeoLocation(37.7749, -122.4194);
        
        // Create stores at various distances
        var stores = new[]
        {
            new H3StoreLocationSortKeyEntity
            {
                StoreId = "STORE",
                Location = new GeoLocation(37.7749, -122.4194),
                Name = "Store A",
                Description = "Test store A"
            },
            new H3StoreLocationSortKeyEntity
            {
                StoreId = "STORE",
                Location = new GeoLocation(37.7599, -122.4148),
                Name = "Store B",
                Description = "Test store B"
            },
            new H3StoreLocationSortKeyEntity
            {
                StoreId = "STORE",
                Location = new GeoLocation(37.8021, -122.4378),
                Name = "Store C",
                Description = "Test store C"
            },
            new H3StoreLocationSortKeyEntity
            {
                StoreId = "STORE",
                Location = new GeoLocation(37.8044, -122.2712), // Outside radius
                Name = "Store D",
                Description = "Test store D"
            }
        };
        
        foreach (var store in stores)
        {
            await table.PutAsync(store);
        }
        
        // Act - Execute query with lambda expression
        var lambdaResult = await table.SpatialQueryAsync<H3StoreLocationSortKeyEntity>(
            locationSelector: store => store.Location,
            spatialIndexType: SpatialIndexType.H3,
            precision: 9,
            center: searchCenter,
            radiusKilometers: 5.0,
            queryBuilder: (query, cell, pagination) => query
                .Where<H3StoreLocationSortKeyEntity>(x => x.StoreId == "STORE" && x.Location == cell)
                ,
            pageSize: null
        );
        
        // Act - Execute query with format string expression
        var formatStringResult = await table.SpatialQueryAsync<H3StoreLocationSortKeyEntity>(
            locationSelector: store => store.Location,
            spatialIndexType: SpatialIndexType.H3,
            precision: 9,
            center: searchCenter,
            radiusKilometers: 5.0,
            queryBuilder: (query, cell, pagination) => query
                .Where("pk = {0} AND sk = {1}", "STORE", cell)
                ,
            pageSize: null
        );
        
        // Assert - Verify both approaches return the same results
        lambdaResult.Items.Should().HaveCount(formatStringResult.Items.Count,
            "lambda and format string expressions should return the same number of results");
        
        // Verify the same stores are returned
        var lambdaNames = lambdaResult.Items.Select(s => s.Name).OrderBy(n => n).ToList();
        var formatStringNames = formatStringResult.Items.Select(s => s.Name).OrderBy(n => n).ToList();
        
        formatStringNames.Should().BeEquivalentTo(lambdaNames,
            "format string expression should return the same stores as lambda expression");
        
        // Verify both return the expected stores
        lambdaNames.Should().Contain("Store A");
        lambdaNames.Should().Contain("Store B");
        lambdaNames.Should().Contain("Store C");
        lambdaNames.Should().NotContain("Store D", "Store D is outside the radius");
        
        formatStringNames.Should().Contain("Store A");
        formatStringNames.Should().Contain("Store B");
        formatStringNames.Should().Contain("Store C");
        formatStringNames.Should().NotContain("Store D", "Store D is outside the radius");
    }
    
    #endregion
    
    #region 27.3 Test H3 proximity query (non-paginated) with plain text expressions
    
    [Fact]
    public async Task SpatialQueryAsync_H3ProximityNonPaginated_WithPlainTextExpression_ReturnsAllResultsWithinRadius()
    {
        // Arrange - Create table with H3-indexed stores at known locations
        await CreateTableAsync<H3StoreLocationSortKeyEntity>();
        var table = new H3StoreTable(DynamoDb, TableName);
        
        // San Francisco downtown area - we'll search within 5km radius
        var searchCenter = new GeoLocation(37.7749, -122.4194);
        
        // Create stores at various distances from the search center
        var stores = new[]
        {
            // Within 5km radius
            new H3StoreLocationSortKeyEntity
            {
                StoreId = "STORE",
                Location = new GeoLocation(37.7749, -122.4194), // 0km - at center
                Name = "Downtown Store",
                Description = "At search center"
            },
            new H3StoreLocationSortKeyEntity
            {
                StoreId = "STORE",
                Location = new GeoLocation(37.7599, -122.4148), // ~1.7km south
                Name = "Mission Store",
                Description = "Mission District"
            },
            new H3StoreLocationSortKeyEntity
            {
                StoreId = "STORE",
                Location = new GeoLocation(37.8021, -122.4378), // ~3.5km north
                Name = "Marina Store",
                Description = "Marina District"
            },
            new H3StoreLocationSortKeyEntity
            {
                StoreId = "STORE",
                Location = new GeoLocation(37.7694, -122.4481), // ~2.5km west
                Name = "Haight Store",
                Description = "Haight-Ashbury"
            },
            // Outside 5km radius
            new H3StoreLocationSortKeyEntity
            {
                StoreId = "STORE",
                Location = new GeoLocation(37.8044, -122.2712), // ~13km east (across bay)
                Name = "Oakland Store",
                Description = "Oakland"
            },
            new H3StoreLocationSortKeyEntity
            {
                StoreId = "STORE",
                Location = new GeoLocation(37.6879, -122.4702), // ~10km south
                Name = "Daly City Store",
                Description = "Daly City"
            }
        };
        
        // Write all stores to DynamoDB
        foreach (var store in stores)
        {
            await table.PutAsync(store);
        }
        
        // Act - Execute SpatialQueryAsync with radius search using plain text expression with WithValue
        // Test plain text syntax: query.Where("pk = :pk AND sk = :loc").WithValue(":pk", "STORE").WithValue(":loc", cell)
        var result = await table.SpatialQueryAsync<H3StoreLocationSortKeyEntity>(
            locationSelector: store => store.Location,
            spatialIndexType: SpatialIndexType.H3,
            precision: 9, // H3 Resolution 9 (~174m hexagons)
            center: searchCenter,
            radiusKilometers: 5.0,
            queryBuilder: (query, cell, pagination) => query
                .Where("pk = :pk AND sk = :loc")
                .WithValue(":pk", "STORE")
                .WithValue(":loc", cell)
                ,
            pageSize: null // Non-paginated mode - query all cells in parallel
        );
        
        // Assert - Verify all results are within radius
        result.Items.Should().NotBeNull();
        result.Items.Should().HaveCount(4, "4 stores are within 5km radius");
        
        // Verify each result is within the radius
        foreach (var store in result.Items)
        {
            var distance = store.Location.DistanceToKilometers(searchCenter);
            distance.Should().BeLessThanOrEqualTo(5.0, 
                $"Store {store.Name} at {store.Location} should be within 5km radius");
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
    public async Task SpatialQueryAsync_H3ProximityNonPaginated_PlainTextMatchesOtherExpressionResults()
    {
        // Arrange - Create table with H3-indexed stores
        await CreateTableAsync<H3StoreLocationSortKeyEntity>();
        var table = new H3StoreTable(DynamoDb, TableName);
        
        var searchCenter = new GeoLocation(37.7749, -122.4194);
        
        // Create stores at various distances
        var stores = new[]
        {
            new H3StoreLocationSortKeyEntity
            {
                StoreId = "STORE",
                Location = new GeoLocation(37.7749, -122.4194),
                Name = "Store A",
                Description = "Test store A"
            },
            new H3StoreLocationSortKeyEntity
            {
                StoreId = "STORE",
                Location = new GeoLocation(37.7599, -122.4148),
                Name = "Store B",
                Description = "Test store B"
            },
            new H3StoreLocationSortKeyEntity
            {
                StoreId = "STORE",
                Location = new GeoLocation(37.8021, -122.4378),
                Name = "Store C",
                Description = "Test store C"
            },
            new H3StoreLocationSortKeyEntity
            {
                StoreId = "STORE",
                Location = new GeoLocation(37.8044, -122.2712), // Outside radius
                Name = "Store D",
                Description = "Test store D"
            }
        };
        
        foreach (var store in stores)
        {
            await table.PutAsync(store);
        }
        
        // Act - Execute query with lambda expression
        var lambdaResult = await table.SpatialQueryAsync<H3StoreLocationSortKeyEntity>(
            locationSelector: store => store.Location,
            spatialIndexType: SpatialIndexType.H3,
            precision: 9,
            center: searchCenter,
            radiusKilometers: 5.0,
            queryBuilder: (query, cell, pagination) => query
                .Where<H3StoreLocationSortKeyEntity>(x => x.StoreId == "STORE" && x.Location == cell)
                ,
            pageSize: null
        );
        
        // Act - Execute query with format string expression
        var formatStringResult = await table.SpatialQueryAsync<H3StoreLocationSortKeyEntity>(
            locationSelector: store => store.Location,
            spatialIndexType: SpatialIndexType.H3,
            precision: 9,
            center: searchCenter,
            radiusKilometers: 5.0,
            queryBuilder: (query, cell, pagination) => query
                .Where("pk = {0} AND sk = {1}", "STORE", cell)
                ,
            pageSize: null
        );
        
        // Act - Execute query with plain text expression
        var plainTextResult = await table.SpatialQueryAsync<H3StoreLocationSortKeyEntity>(
            locationSelector: store => store.Location,
            spatialIndexType: SpatialIndexType.H3,
            precision: 9,
            center: searchCenter,
            radiusKilometers: 5.0,
            queryBuilder: (query, cell, pagination) => query
                .Where("pk = :pk AND sk = :loc")
                .WithValue(":pk", "STORE")
                .WithValue(":loc", cell)
                ,
            pageSize: null
        );
        
        // Assert - Verify all three approaches return the same results
        lambdaResult.Items.Should().HaveCount(plainTextResult.Items.Count,
            "lambda and plain text expressions should return the same number of results");
        formatStringResult.Items.Should().HaveCount(plainTextResult.Items.Count,
            "format string and plain text expressions should return the same number of results");
        
        // Verify the same stores are returned
        var lambdaNames = lambdaResult.Items.Select(s => s.Name).OrderBy(n => n).ToList();
        var formatStringNames = formatStringResult.Items.Select(s => s.Name).OrderBy(n => n).ToList();
        var plainTextNames = plainTextResult.Items.Select(s => s.Name).OrderBy(n => n).ToList();
        
        plainTextNames.Should().BeEquivalentTo(lambdaNames,
            "plain text expression should return the same stores as lambda expression");
        plainTextNames.Should().BeEquivalentTo(formatStringNames,
            "plain text expression should return the same stores as format string expression");
        
        // Verify all return the expected stores
        plainTextNames.Should().Contain("Store A");
        plainTextNames.Should().Contain("Store B");
        plainTextNames.Should().Contain("Store C");
        plainTextNames.Should().NotContain("Store D", "Store D is outside the radius");
    }
    
    #endregion
    
    #region 27.4 Test H3 proximity query (paginated) with lambda expressions
    
    [Fact]
    public async Task SpatialQueryAsync_H3ProximityPaginated_WithLambdaExpression_RespectsPageSize()
    {
        // Arrange - Create table with many H3-indexed stores
        await CreateTableAsync<H3StoreLocationSortKeyEntity>();
        var table = new H3StoreTable(DynamoDb, TableName);
        
        // San Francisco downtown area - we'll search within 10km radius
        var searchCenter = new GeoLocation(37.7749, -122.4194);
        
        // Create 30 stores at various distances within 10km
        var stores = new List<H3StoreLocationSortKeyEntity>();
        var random = new Random(42); // Fixed seed for reproducibility
        
        for (int i = 0; i < 30; i++)
        {
            // Generate random locations within ~10km radius
            // Using simple offset in degrees (rough approximation)
            var latOffset = (random.NextDouble() - 0.5) * 0.18; // ~10km in latitude
            var lonOffset = (random.NextDouble() - 0.5) * 0.18; // ~10km in longitude
            
            var location = new GeoLocation(
                searchCenter.Latitude + latOffset,
                searchCenter.Longitude + lonOffset
            );
            
            stores.Add(new H3StoreLocationSortKeyEntity
            {
                StoreId = "STORE",
                Location = location,
                Name = $"Store {i}",
                Description = $"Test store {i}"
            });
        }
        
        // Write all stores to DynamoDB
        foreach (var store in stores)
        {
            await table.PutAsync(store);
        }
        
        // Act - Execute SpatialQueryAsync with pageSize=10
        var result = await table.SpatialQueryAsync<H3StoreLocationSortKeyEntity>(
            locationSelector: store => store.Location,
            spatialIndexType: SpatialIndexType.H3,
            precision: 9, // H3 Resolution 9 (~174m hexagons)
            center: searchCenter,
            radiusKilometers: 10.0,
            queryBuilder: (query, cell, pagination) => query
                .Where<H3StoreLocationSortKeyEntity>(x => x.StoreId == "STORE" && x.Location == cell),
            pageSize: 10 // Request 10 items per page
        );
        
        // Assert - Verify page size is respected
        result.Items.Should().NotBeNull();
        result.Items.Count.Should().BeLessThanOrEqualTo(10, "page size should be respected");
        result.Items.Count.Should().BeGreaterThan(0, "should return at least some results");
        
        // Verify continuation token is returned (more results available)
        result.ContinuationToken.Should().NotBeNull("continuation token should be present when more results exist");
        
        // Verify all results are within radius
        foreach (var store in result.Items)
        {
            var distance = store.Location.DistanceToKilometers(searchCenter);
            distance.Should().BeLessThanOrEqualTo(10.0, 
                $"Store {store.Name} should be within 10km radius");
        }
        
        // Verify results are roughly sorted by distance (due to spiral ordering)
        // First result should be relatively close to center
        var firstDistance = result.Items[0].Location.DistanceToKilometers(searchCenter);
        firstDistance.Should().BeLessThan(5.0, "first result should be relatively close due to spiral ordering");
    }
    
    #endregion
    
    #region 27.5 Test H3 pagination continuation
    
    [Fact]
    public async Task SpatialQueryAsync_H3ProximityPaginated_ContinuationTokenRetrievesAllResults()
    {
        // Arrange - Create table with many H3-indexed stores
        await CreateTableAsync<H3StoreLocationSortKeyEntity>();
        var table = new H3StoreTable(DynamoDb, TableName);
        
        // San Francisco downtown area - we'll search within 10km radius
        var searchCenter = new GeoLocation(37.7749, -122.4194);
        
        // Create 50 stores at various distances within 10km to ensure multiple pages
        var stores = new List<H3StoreLocationSortKeyEntity>();
        var random = new Random(42); // Fixed seed for reproducibility
        
        for (int i = 0; i < 50; i++)
        {
            // Generate random locations within ~10km radius
            // Using simple offset in degrees (rough approximation)
            var latOffset = (random.NextDouble() - 0.5) * 0.18; // ~10km in latitude
            var lonOffset = (random.NextDouble() - 0.5) * 0.18; // ~10km in longitude
            
            var location = new GeoLocation(
                searchCenter.Latitude + latOffset,
                searchCenter.Longitude + lonOffset
            );
            
            stores.Add(new H3StoreLocationSortKeyEntity
            {
                StoreId = "STORE",
                Location = location,
                Name = $"Store {i}",
                Description = $"Test store {i}"
            });
        }
        
        // Write all stores to DynamoDB
        foreach (var store in stores)
        {
            await table.PutAsync(store);
        }
        
        // Act - Execute first page of H3 proximity query
        var allResults = new List<H3StoreLocationSortKeyEntity>();
        var continuationToken = (SpatialContinuationToken?)null;
        var pageCount = 0;
        var maxPages = 20; // Safety limit to prevent infinite loops
        
        do
        {
            pageCount++;
            
            // Execute query with continuation token
            var result = await table.SpatialQueryAsync<H3StoreLocationSortKeyEntity>(
                locationSelector: store => store.Location,
                spatialIndexType: SpatialIndexType.H3,
                precision: 9, // H3 Resolution 9 (~174m hexagons)
                center: searchCenter,
                radiusKilometers: 10.0,
                queryBuilder: (query, cell, pagination) => query
                    .Where<H3StoreLocationSortKeyEntity>(x => x.StoreId == "STORE" && x.Location == cell),
                pageSize: 10, // Small page size to ensure multiple pages
                continuationToken: continuationToken
            );
            
            // Collect results from this page
            allResults.AddRange(result.Items);
            
            // Update continuation token for next iteration
            continuationToken = result.ContinuationToken;
            
            // Verify page size is respected (except possibly last page)
            if (continuationToken != null)
            {
                result.Items.Count.Should().BeLessThanOrEqualTo(10, 
                    $"page {pageCount} should respect page size limit");
            }
            
            // Safety check to prevent infinite loops
            if (pageCount >= maxPages)
            {
                throw new InvalidOperationException(
                    $"Exceeded maximum page count ({maxPages}). Possible infinite loop in pagination.");
            }
            
        } while (continuationToken != null);
        
        // Assert - Verify all results retrieved
        allResults.Should().NotBeNull();
        allResults.Count.Should().BeGreaterThan(10, "should have retrieved multiple pages of results");
        allResults.Count.Should().BeLessThanOrEqualTo(50, "should not exceed total number of stores");
        
        // Verify no duplicates across pages
        var uniqueStoreNames = allResults.Select(s => s.Name).Distinct().ToList();
        uniqueStoreNames.Count.Should().Be(allResults.Count, 
            "each store should appear exactly once (no duplicates across pages)");
        
        // Verify all results are within radius
        foreach (var store in allResults)
        {
            var distance = store.Location.DistanceToKilometers(searchCenter);
            distance.Should().BeLessThanOrEqualTo(10.0, 
                $"Store {store.Name} should be within 10km radius");
        }
        
        // Verify null token on final page (already verified by loop exit condition)
        continuationToken.Should().BeNull("continuation token should be null on final page");
        
        // Verify we retrieved multiple pages
        pageCount.Should().BeGreaterThan(1, "should have retrieved multiple pages");
        
        // Log summary for debugging
        Console.WriteLine($"Retrieved {allResults.Count} stores across {pageCount} pages");
        Console.WriteLine($"Average page size: {allResults.Count / (double)pageCount:F1}");
    }
    
    [Fact]
    public async Task SpatialQueryAsync_H3ProximityPaginated_ContinuationTokenPreservesOrder()
    {
        // Arrange - Create table with H3-indexed stores
        await CreateTableAsync<H3StoreLocationSortKeyEntity>();
        var table = new H3StoreTable(DynamoDb, TableName);
        
        var searchCenter = new GeoLocation(37.7749, -122.4194);
        
        // Create 30 stores at various distances
        var stores = new List<H3StoreLocationSortKeyEntity>();
        var random = new Random(42); // Fixed seed for reproducibility
        
        for (int i = 0; i < 30; i++)
        {
            var latOffset = (random.NextDouble() - 0.5) * 0.18;
            var lonOffset = (random.NextDouble() - 0.5) * 0.18;
            
            var location = new GeoLocation(
                searchCenter.Latitude + latOffset,
                searchCenter.Longitude + lonOffset
            );
            
            stores.Add(new H3StoreLocationSortKeyEntity
            {
                StoreId = "STORE",
                Location = location,
                Name = $"Store {i}",
                Description = $"Test store {i}"
            });
        }
        
        foreach (var store in stores)
        {
            await table.PutAsync(store);
        }
        
        // Act - Retrieve all results in pages
        var allResults = new List<H3StoreLocationSortKeyEntity>();
        var continuationToken = (SpatialContinuationToken?)null;
        
        do
        {
            var result = await table.SpatialQueryAsync<H3StoreLocationSortKeyEntity>(
                locationSelector: store => store.Location,
                spatialIndexType: SpatialIndexType.H3,
                precision: 9,
                center: searchCenter,
                radiusKilometers: 10.0,
                queryBuilder: (query, cell, pagination) => query
                    .Where<H3StoreLocationSortKeyEntity>(x => x.StoreId == "STORE" && x.Location == cell),
                pageSize: 8,
                continuationToken: continuationToken
            );
            
            allResults.AddRange(result.Items);
            continuationToken = result.ContinuationToken;
            
        } while (continuationToken != null);
        
        // Assert - Verify results are roughly sorted by distance (spiral ordering)
        // Calculate distances for all results
        var distances = allResults
            .Select(s => s.Location.DistanceToKilometers(searchCenter))
            .ToList();
        
        // Verify first results are closer than later results (on average)
        var firstQuarterAvg = distances.Take(distances.Count / 4).Average();
        var lastQuarterAvg = distances.Skip(3 * distances.Count / 4).Average();
        
        firstQuarterAvg.Should().BeLessThan(lastQuarterAvg,
            "due to spiral ordering, earlier results should be closer on average than later results");
        
        // Verify no duplicates
        var uniqueNames = allResults.Select(s => s.Name).Distinct().Count();
        uniqueNames.Should().Be(allResults.Count, "no duplicates should exist across pages");
    }
    
    [Fact]
    public async Task SpatialQueryAsync_H3ProximityPaginated_EmptyResultsReturnNullToken()
    {
        // Arrange - Create table with H3-indexed stores
        await CreateTableAsync<H3StoreLocationSortKeyEntity>();
        var table = new H3StoreTable(DynamoDb, TableName);
        
        // San Francisco downtown area
        var searchCenter = new GeoLocation(37.7749, -122.4194);
        
        // Create stores far outside the search radius
        var stores = new[]
        {
            new H3StoreLocationSortKeyEntity
            {
                StoreId = "STORE",
                Location = new GeoLocation(40.7128, -74.0060), // New York - ~4000km away
                Name = "New York Store",
                Description = "Far away"
            },
            new H3StoreLocationSortKeyEntity
            {
                StoreId = "STORE",
                Location = new GeoLocation(51.5074, -0.1278), // London - ~8600km away
                Name = "London Store",
                Description = "Very far away"
            }
        };
        
        foreach (var store in stores)
        {
            await table.PutAsync(store);
        }
        
        // Act - Execute query with small radius (no results expected)
        var result = await table.SpatialQueryAsync<H3StoreLocationSortKeyEntity>(
            locationSelector: store => store.Location,
            spatialIndexType: SpatialIndexType.H3,
            precision: 9,
            center: searchCenter,
            radiusKilometers: 5.0, // Small radius - won't reach any stores
            queryBuilder: (query, cell, pagination) => query
                .Where<H3StoreLocationSortKeyEntity>(x => x.StoreId == "STORE" && x.Location == cell),
            pageSize: 10
        );
        
        // Assert - Verify empty results with null token
        result.Items.Should().BeEmpty("no stores should be within 5km radius");
        result.ContinuationToken.Should().BeNull("continuation token should be null when no results");
    }
    
    #endregion
    
    #region 27.6 Test H3 bounding box query (non-paginated) with lambda expressions
    
    [Fact]
    public async Task SpatialQueryAsync_H3BoundingBoxNonPaginated_WithLambdaExpression_ReturnsAllResultsWithinBoundingBox()
    {
        // Arrange - Create table with H3-indexed stores at known locations
        await CreateTableAsync<H3StoreLocationSortKeyEntity>();
        var table = new H3StoreTable(DynamoDb, TableName);
        
        // Define a bounding box covering downtown San Francisco
        // Southwest corner: 37.77, -122.43 (near Civic Center)
        // Northeast corner: 37.80, -122.40 (near North Beach)
        // This creates a roughly 3.3km x 2.7km box
        var boundingBox = new GeoBoundingBox(
            southwest: new GeoLocation(37.77, -122.43),
            northeast: new GeoLocation(37.80, -122.40)
        );
        
        // Create stores at various locations - some inside, some outside the bounding box
        var stores = new[]
        {
            // Inside bounding box
            new H3StoreLocationSortKeyEntity
            {
                StoreId = "STORE",
                Location = new GeoLocation(37.7849, -122.4194), // Downtown - inside
                Name = "Downtown Store",
                Description = "Inside bounding box"
            },
            new H3StoreLocationSortKeyEntity
            {
                StoreId = "STORE",
                Location = new GeoLocation(37.7899, -122.4094), // North Beach - inside
                Name = "North Beach Store",
                Description = "Inside bounding box"
            },
            new H3StoreLocationSortKeyEntity
            {
                StoreId = "STORE",
                Location = new GeoLocation(37.7749, -122.4244), // Civic Center - inside
                Name = "Civic Center Store",
                Description = "Inside bounding box"
            },
            new H3StoreLocationSortKeyEntity
            {
                StoreId = "STORE",
                Location = new GeoLocation(37.7949, -122.4144), // Russian Hill - inside
                Name = "Russian Hill Store",
                Description = "Inside bounding box"
            },
            // Outside bounding box - too far south
            new H3StoreLocationSortKeyEntity
            {
                StoreId = "STORE",
                Location = new GeoLocation(37.7599, -122.4194), // Mission - outside (south)
                Name = "Mission Store",
                Description = "Outside bounding box (south)"
            },
            // Outside bounding box - too far north
            new H3StoreLocationSortKeyEntity
            {
                StoreId = "STORE",
                Location = new GeoLocation(37.8099, -122.4194), // Marina - outside (north)
                Name = "Marina Store",
                Description = "Outside bounding box (north)"
            },
            // Outside bounding box - too far west
            new H3StoreLocationSortKeyEntity
            {
                StoreId = "STORE",
                Location = new GeoLocation(37.7849, -122.4494), // Richmond - outside (west)
                Name = "Richmond Store",
                Description = "Outside bounding box (west)"
            },
            // Outside bounding box - too far east
            new H3StoreLocationSortKeyEntity
            {
                StoreId = "STORE",
                Location = new GeoLocation(37.7849, -122.3894), // Embarcadero - outside (east)
                Name = "Embarcadero Store",
                Description = "Outside bounding box (east)"
            }
        };
        
        // Write all stores to DynamoDB
        foreach (var store in stores)
        {
            await table.PutAsync(store);
        }
        
        // Act - Execute SpatialQueryAsync with bounding box using lambda expression
        // Test the implicit cast: x.Location == cell compares GeoLocation.SpatialIndex to cell
        var result = await table.SpatialQueryAsync<H3StoreLocationSortKeyEntity>(
            locationSelector: store => store.Location,
            spatialIndexType: SpatialIndexType.H3,
            precision: 9, // H3 Resolution 9 (~174m hexagons)
            boundingBox: boundingBox,
            queryBuilder: (query, cell, pagination) => query
                .Where<H3StoreLocationSortKeyEntity>(x => x.StoreId == "STORE" && x.Location == cell),
            pageSize: null // Non-paginated mode - query all cells in parallel
        );
        
        // Assert - Verify all results are within bounding box
        result.Items.Should().NotBeNull();
        result.Items.Should().HaveCount(4, "4 stores are within the bounding box");
        
        // Verify each result is within the bounding box
        foreach (var store in result.Items)
        {
            var isInside = store.Location.Latitude >= boundingBox.Southwest.Latitude &&
                          store.Location.Latitude <= boundingBox.Northeast.Latitude &&
                          store.Location.Longitude >= boundingBox.Southwest.Longitude &&
                          store.Location.Longitude <= boundingBox.Northeast.Longitude;
            
            isInside.Should().BeTrue(
                $"Store {store.Name} at {store.Location} should be within bounding box {boundingBox}");
        }
        
        // Verify the stores inside the bounding box are present
        var storeNames = result.Items.Select(s => s.Name).ToList();
        storeNames.Should().Contain("Downtown Store");
        storeNames.Should().Contain("North Beach Store");
        storeNames.Should().Contain("Civic Center Store");
        storeNames.Should().Contain("Russian Hill Store");
        
        // Verify the stores outside the bounding box are NOT present
        storeNames.Should().NotContain("Mission Store");
        storeNames.Should().NotContain("Marina Store");
        storeNames.Should().NotContain("Richmond Store");
        storeNames.Should().NotContain("Embarcadero Store");
    }
    
    [Fact]
    public async Task SpatialQueryAsync_H3BoundingBoxNonPaginated_VerifiesParallelExecution()
    {
        // Arrange - Create table with H3-indexed stores
        await CreateTableAsync<H3StoreLocationSortKeyEntity>();
        var table = new H3StoreTable(DynamoDb, TableName);
        
        // Create a larger bounding box that will require multiple H3 cells
        // This ensures we're testing parallel execution of multiple cell queries
        var boundingBox = new GeoBoundingBox(
            southwest: new GeoLocation(37.75, -122.45),
            northeast: new GeoLocation(37.82, -122.38)
        );
        
        // Create 20 stores distributed across the bounding box
        var stores = new List<H3StoreLocationSortKeyEntity>();
        for (int i = 0; i < 20; i++)
        {
            // Distribute stores in a grid pattern within the bounding box
            var latRange = boundingBox.Northeast.Latitude - boundingBox.Southwest.Latitude;
            var lonRange = boundingBox.Northeast.Longitude - boundingBox.Southwest.Longitude;
            
            var lat = boundingBox.Southwest.Latitude + (i % 5) * (latRange / 4);
            var lon = boundingBox.Southwest.Longitude + (i / 5) * (lonRange / 4);
            
            stores.Add(new H3StoreLocationSortKeyEntity
            {
                StoreId = "STORE",
                Location = new GeoLocation(lat, lon),
                Name = $"Store {i}",
                Description = $"Test store {i}"
            });
        }
        
        // Write all stores to DynamoDB
        foreach (var store in stores)
        {
            await table.PutAsync(store);
        }
        
        // Act - Execute SpatialQueryAsync with bounding box
        var startTime = DateTime.UtcNow;
        
        var result = await table.SpatialQueryAsync<H3StoreLocationSortKeyEntity>(
            locationSelector: store => store.Location,
            spatialIndexType: SpatialIndexType.H3,
            precision: 9, // H3 Resolution 9 (~174m hexagons)
            boundingBox: boundingBox,
            queryBuilder: (query, cell, pagination) => query
                .Where<H3StoreLocationSortKeyEntity>(x => x.StoreId == "STORE" && x.Location == cell),
            pageSize: null // Non-paginated mode - query all cells in parallel
        );
        
        var elapsedTime = DateTime.UtcNow - startTime;
        
        // Assert - Verify all results are within bounding box
        result.Items.Should().NotBeNull();
        result.Items.Should().HaveCount(20, "all 20 stores should be within the bounding box");
        
        // Verify each result is within the bounding box
        foreach (var store in result.Items)
        {
            var isInside = store.Location.Latitude >= boundingBox.Southwest.Latitude &&
                          store.Location.Latitude <= boundingBox.Northeast.Latitude &&
                          store.Location.Longitude >= boundingBox.Southwest.Longitude &&
                          store.Location.Longitude <= boundingBox.Northeast.Longitude;
            
            isInside.Should().BeTrue(
                $"Store {store.Name} at {store.Location} should be within bounding box");
        }
        
        // Verify multiple cells were queried (indicates parallel execution)
        result.TotalCellsQueried.Should().BeGreaterThan(1, 
            "bounding box should require multiple H3 cells, indicating parallel execution");
        
        // Log execution details for debugging
        Console.WriteLine($"Queried {result.TotalCellsQueried} H3 cells in {elapsedTime.TotalMilliseconds:F0}ms");
        Console.WriteLine($"Retrieved {result.Items.Count} stores");
        Console.WriteLine($"Average time per cell: {elapsedTime.TotalMilliseconds / result.TotalCellsQueried:F1}ms");
        
        // Verify no duplicates
        var uniqueNames = result.Items.Select(s => s.Name).Distinct().Count();
        uniqueNames.Should().Be(20, "each store should appear exactly once (no duplicates)");
    }
    
    [Fact]
    public async Task SpatialQueryAsync_H3BoundingBoxNonPaginated_WithExplicitSpatialIndexProperty_ReturnsAllResultsWithinBoundingBox()
    {
        // Arrange - Create table with H3-indexed stores
        await CreateTableAsync<H3StoreLocationSortKeyEntity>();
        var table = new H3StoreTable(DynamoDb, TableName);
        
        // Define a bounding box
        var boundingBox = new GeoBoundingBox(
            southwest: new GeoLocation(37.77, -122.43),
            northeast: new GeoLocation(37.80, -122.40)
        );
        
        // Create stores - some inside, some outside
        var stores = new[]
        {
            new H3StoreLocationSortKeyEntity
            {
                StoreId = "STORE",
                Location = new GeoLocation(37.7849, -122.4194), // Inside
                Name = "Store A",
                Description = "Inside"
            },
            new H3StoreLocationSortKeyEntity
            {
                StoreId = "STORE",
                Location = new GeoLocation(37.7899, -122.4094), // Inside
                Name = "Store B",
                Description = "Inside"
            },
            new H3StoreLocationSortKeyEntity
            {
                StoreId = "STORE",
                Location = new GeoLocation(37.7599, -122.4194), // Outside (south)
                Name = "Store C",
                Description = "Outside"
            }
        };
        
        foreach (var store in stores)
        {
            await table.PutAsync(store);
        }
        
        // Act - Execute SpatialQueryAsync with explicit SpatialIndex property
        var result = await table.SpatialQueryAsync<H3StoreLocationSortKeyEntity>(
            locationSelector: store => store.Location,
            spatialIndexType: SpatialIndexType.H3,
            precision: 9,
            boundingBox: boundingBox,
            queryBuilder: (query, cell, pagination) => query
                .Where<H3StoreLocationSortKeyEntity>(x => x.StoreId == "STORE" && x.Location.SpatialIndex == cell),
            pageSize: null
        );
        
        // Assert - Verify only stores inside bounding box are returned
        result.Items.Should().HaveCount(2, "2 stores are within the bounding box");
        
        var storeNames = result.Items.Select(s => s.Name).ToList();
        storeNames.Should().Contain("Store A");
        storeNames.Should().Contain("Store B");
        storeNames.Should().NotContain("Store C", "Store C is outside the bounding box");
        
        // Verify each result is within the bounding box
        foreach (var store in result.Items)
        {
            var isInside = store.Location.Latitude >= boundingBox.Southwest.Latitude &&
                          store.Location.Latitude <= boundingBox.Northeast.Latitude &&
                          store.Location.Longitude >= boundingBox.Southwest.Longitude &&
                          store.Location.Longitude <= boundingBox.Northeast.Longitude;
            
            isInside.Should().BeTrue($"Store {store.Name} should be within bounding box");
        }
    }
    
    #endregion
    
    #region 27.7 Test H3 bounding box query (paginated)
    
    [Fact]
    public async Task SpatialQueryAsync_H3BoundingBoxPaginated_RespectsPageSize()
    {
        // Arrange - Create table with many H3-indexed stores
        await CreateTableAsync<H3StoreLocationSortKeyEntity>();
        var table = new H3StoreTable(DynamoDb, TableName);
        
        // Define a bounding box covering a large area of San Francisco
        var boundingBox = new GeoBoundingBox(
            southwest: new GeoLocation(37.75, -122.45),
            northeast: new GeoLocation(37.82, -122.38)
        );
        
        // Create 30 stores distributed across the bounding box
        var stores = new List<H3StoreLocationSortKeyEntity>();
        var latRange = boundingBox.Northeast.Latitude - boundingBox.Southwest.Latitude;
        var lonRange = boundingBox.Northeast.Longitude - boundingBox.Southwest.Longitude;
        
        for (int i = 0; i < 30; i++)
        {
            // Distribute stores in a grid pattern within the bounding box
            // Use modulo to create a grid: 6 columns x 5 rows
            var row = i / 6;  // 0-4
            var col = i % 6;  // 0-5
            
            // Calculate position within the bounding box (add small offset to ensure inside)
            var lat = boundingBox.Southwest.Latitude + (row + 0.5) * (latRange / 5.0);
            var lon = boundingBox.Southwest.Longitude + (col + 0.5) * (lonRange / 6.0);
            
            stores.Add(new H3StoreLocationSortKeyEntity
            {
                StoreId = "STORE",
                Location = new GeoLocation(lat, lon),
                Name = $"Store {i + 1:D3}",
                Description = $"Test store at position {i + 1}"
            });
        }
        
        // Write all stores to DynamoDB
        foreach (var store in stores)
        {
            await table.PutAsync(store);
        }
        
        // Act - Execute SpatialQueryAsync with bounding box and pageSize
        var result = await table.SpatialQueryAsync<H3StoreLocationSortKeyEntity>(
            locationSelector: store => store.Location,
            spatialIndexType: SpatialIndexType.H3,
            precision: 9, // H3 Resolution 9 (~174m hexagons)
            boundingBox: boundingBox,
            queryBuilder: (query, cell, pagination) => query
                .Where<H3StoreLocationSortKeyEntity>(x => x.StoreId == "STORE" && x.Location == cell),
            pageSize: 10 // Paginated mode with page size of 10
        );
        
        // Assert - Verify page size is respected
        result.Items.Should().NotBeNull();
        result.Items.Count.Should().BeLessThanOrEqualTo(10, 
            "paginated bounding box query should return at most pageSize items");
        result.Items.Count.Should().BeGreaterThan(0, 
            "should return at least some results");
        
        // Verify all results are within the bounding box
        foreach (var store in result.Items)
        {
            var isInside = store.Location.Latitude >= boundingBox.Southwest.Latitude &&
                          store.Location.Latitude <= boundingBox.Northeast.Latitude &&
                          store.Location.Longitude >= boundingBox.Southwest.Longitude &&
                          store.Location.Longitude <= boundingBox.Northeast.Longitude;
            
            isInside.Should().BeTrue(
                $"Store {store.Name} at {store.Location} should be within bounding box");
        }
    }
    
    [Fact]
    public async Task SpatialQueryAsync_H3BoundingBoxPaginated_VerifiesSequentialCellQuerying()
    {
        // Arrange - Create table with H3-indexed stores
        await CreateTableAsync<H3StoreLocationSortKeyEntity>();
        var table = new H3StoreTable(DynamoDb, TableName);
        
        // Create a bounding box that will require multiple H3 cells
        var boundingBox = new GeoBoundingBox(
            southwest: new GeoLocation(37.75, -122.45),
            northeast: new GeoLocation(37.82, -122.38)
        );
        
        // Create 25 stores distributed across the bounding box
        var stores = new List<H3StoreLocationSortKeyEntity>();
        for (int i = 0; i < 25; i++)
        {
            var latRange = boundingBox.Northeast.Latitude - boundingBox.Southwest.Latitude;
            var lonRange = boundingBox.Northeast.Longitude - boundingBox.Southwest.Longitude;
            
            var lat = boundingBox.Southwest.Latitude + (i % 5) * (latRange / 4);
            var lon = boundingBox.Southwest.Longitude + (i / 5) * (lonRange / 4);
            
            stores.Add(new H3StoreLocationSortKeyEntity
            {
                StoreId = "STORE",
                Location = new GeoLocation(lat, lon),
                Name = $"Store {i + 1:D3}",
                Description = $"Test store {i + 1}"
            });
        }
        
        foreach (var store in stores)
        {
            await table.PutAsync(store);
        }
        
        // Act - Execute paginated bounding box query
        var result = await table.SpatialQueryAsync<H3StoreLocationSortKeyEntity>(
            locationSelector: store => store.Location,
            spatialIndexType: SpatialIndexType.H3,
            precision: 9,
            boundingBox: boundingBox,
            queryBuilder: (query, cell, pagination) => query
                .Where<H3StoreLocationSortKeyEntity>(x => x.StoreId == "STORE" && x.Location == cell),
            pageSize: 8 // Small page size to ensure sequential cell querying
        );
        
        // Assert - Verify sequential cell querying characteristics
        result.Items.Should().NotBeNull();
        result.Items.Count.Should().BeLessThanOrEqualTo(8, 
            "should respect page size");
        
        // Verify that cells are queried (not all at once in parallel)
        // In paginated mode, we query cells sequentially until page size is reached
        result.TotalCellsQueried.Should().BeGreaterThan(0,
            "should have queried at least one cell");
        
        // The key difference from non-paginated mode is that we don't query ALL cells
        // We stop when we reach the page size, which is the sequential behavior
        
        // Verify all results are within the bounding box
        foreach (var store in result.Items)
        {
            var isInside = store.Location.Latitude >= boundingBox.Southwest.Latitude &&
                          store.Location.Latitude <= boundingBox.Northeast.Latitude &&
                          store.Location.Longitude >= boundingBox.Southwest.Longitude &&
                          store.Location.Longitude <= boundingBox.Northeast.Longitude;
            
            isInside.Should().BeTrue(
                $"Store {store.Name} should be within bounding box");
        }
        
        // Verify continuation token is present if page is full
        if (result.Items.Count == 8)
        {
            result.ContinuationToken.Should().NotBeNull(
                "continuation token should be present when page is full and more results may exist");
        }
    }
    
    [Fact]
    public async Task SpatialQueryAsync_H3BoundingBoxPaginated_TestPaginationContinuation()
    {
        // Arrange - Create table with many H3-indexed stores
        await CreateTableAsync<H3StoreLocationSortKeyEntity>();
        var table = new H3StoreTable(DynamoDb, TableName);
        
        // Define a bounding box
        var boundingBox = new GeoBoundingBox(
            southwest: new GeoLocation(37.75, -122.45),
            northeast: new GeoLocation(37.82, -122.38)
        );
        
        // Create 35 stores to ensure multiple pages
        var stores = new List<H3StoreLocationSortKeyEntity>();
        for (int i = 0; i < 35; i++)
        {
            var latRange = boundingBox.Northeast.Latitude - boundingBox.Southwest.Latitude;
            var lonRange = boundingBox.Northeast.Longitude - boundingBox.Southwest.Longitude;
            
            var lat = boundingBox.Southwest.Latitude + (i % 7) * (latRange / 6);
            var lon = boundingBox.Southwest.Longitude + (i / 7) * (lonRange / 6);
            
            stores.Add(new H3StoreLocationSortKeyEntity
            {
                StoreId = "STORE",
                Location = new GeoLocation(lat, lon),
                Name = $"Store {i + 1:D3}",
                Description = $"Test store {i + 1}"
            });
        }
        
        foreach (var store in stores)
        {
            await table.PutAsync(store);
        }
        
        // Act - Execute first page
        var firstPageResult = await table.SpatialQueryAsync<H3StoreLocationSortKeyEntity>(
            locationSelector: store => store.Location,
            spatialIndexType: SpatialIndexType.H3,
            precision: 9,
            boundingBox: boundingBox,
            queryBuilder: (query, cell, pagination) => query
                .Where<H3StoreLocationSortKeyEntity>(x => x.StoreId == "STORE" && x.Location == cell),
            pageSize: 10
        );
        
        // Assert - First page should have results
        firstPageResult.Items.Should().NotBeNull();
        firstPageResult.Items.Count.Should().BeLessThanOrEqualTo(10);
        firstPageResult.Items.Count.Should().BeGreaterThan(0);
        
        // If we got exactly 10 items, there should be more pages
        if (firstPageResult.Items.Count == 10)
        {
            firstPageResult.ContinuationToken.Should().NotBeNull(
                "continuation token should be present when page is full");
        }
        
        // Act - Use continuation token to fetch second page
        if (firstPageResult.ContinuationToken != null)
        {
            var secondPageResult = await table.SpatialQueryAsync<H3StoreLocationSortKeyEntity>(
                locationSelector: store => store.Location,
                spatialIndexType: SpatialIndexType.H3,
                precision: 9,
                boundingBox: boundingBox,
                queryBuilder: (query, cell, pagination) => query
                    .Where<H3StoreLocationSortKeyEntity>(x => x.StoreId == "STORE" && x.Location == cell),
                pageSize: 10,
                continuationToken: firstPageResult.ContinuationToken
            );
            
            // Assert - Second page should have different results
            secondPageResult.Items.Should().NotBeNull();
            secondPageResult.Items.Count.Should().BeGreaterThan(0);
            secondPageResult.Items.Count.Should().BeLessThanOrEqualTo(10);
            
            // Verify no overlap between first and second page
            var firstPageNames = firstPageResult.Items.Select(s => s.Name).ToHashSet();
            var secondPageNames = secondPageResult.Items.Select(s => s.Name).ToHashSet();
            
            firstPageNames.Intersect(secondPageNames).Should().BeEmpty(
                "first and second page should not have overlapping results");
            
            // Verify all results are within the bounding box
            foreach (var store in secondPageResult.Items)
            {
                var isInside = store.Location.Latitude >= boundingBox.Southwest.Latitude &&
                              store.Location.Latitude <= boundingBox.Northeast.Latitude &&
                              store.Location.Longitude >= boundingBox.Southwest.Longitude &&
                              store.Location.Longitude <= boundingBox.Northeast.Longitude;
                
                isInside.Should().BeTrue(
                    $"Store {store.Name} should be within bounding box");
            }
        }
    }
    
    [Fact]
    public async Task SpatialQueryAsync_H3BoundingBoxPaginated_IterateThroughAllPages()
    {
        // Arrange - Create table with H3-indexed stores
        await CreateTableAsync<H3StoreLocationSortKeyEntity>();
        var table = new H3StoreTable(DynamoDb, TableName);
        
        // Define a bounding box
        var boundingBox = new GeoBoundingBox(
            southwest: new GeoLocation(37.75, -122.45),
            northeast: new GeoLocation(37.82, -122.38)
        );
        
        // Create 28 stores (not a multiple of page size to test partial final page)
        var stores = new List<H3StoreLocationSortKeyEntity>();
        var latRange = boundingBox.Northeast.Latitude - boundingBox.Southwest.Latitude;
        var lonRange = boundingBox.Northeast.Longitude - boundingBox.Southwest.Longitude;
        
        for (int i = 0; i < 28; i++)
        {
            // Distribute stores in a grid pattern within the bounding box
            // Use modulo to create a grid: 7 columns x 4 rows
            var row = i / 7;  // 0-3
            var col = i % 7;  // 0-6
            
            // Calculate position within the bounding box (add small offset to ensure inside)
            var lat = boundingBox.Southwest.Latitude + (row + 0.5) * (latRange / 4.0);
            var lon = boundingBox.Southwest.Longitude + (col + 0.5) * (lonRange / 7.0);
            
            stores.Add(new H3StoreLocationSortKeyEntity
            {
                StoreId = "STORE",
                Location = new GeoLocation(lat, lon),
                Name = $"Store {i + 1:D3}",
                Description = $"Test store {i + 1}"
            });
        }
        
        foreach (var store in stores)
        {
            await table.PutAsync(store);
        }
        
        // Act - Iterate through all pages
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
                boundingBox: boundingBox,
                queryBuilder: (query, cell, pagination) => query
                    .Where<H3StoreLocationSortKeyEntity>(x => x.StoreId == "STORE" && x.Location == cell),
                pageSize: 9, // Not a divisor of 28 to test partial pages
                continuationToken: continuationToken
            );
            
            allResults.AddRange(result.Items);
            continuationToken = result.ContinuationToken;
            pageCount++;
            
            // Safety check
            if (pageCount >= maxPages)
            {
                break;
            }
        }
        while (continuationToken != null);
        
        // Assert - Verify all results retrieved
        allResults.Should().NotBeEmpty();
        allResults.Count.Should().BeLessThanOrEqualTo(28, 
            "should not return more items than created");
        
        // Verify we iterated through multiple pages
        pageCount.Should().BeGreaterThan(1, 
            "should have paginated through multiple pages");
        
        // Verify no duplicates across pages
        var uniqueNames = allResults.Select(s => s.Name).Distinct().ToList();
        uniqueNames.Count.Should().Be(allResults.Count, 
            "should not have duplicate stores across pages");
        
        // Verify all results are within the bounding box
        foreach (var store in allResults)
        {
            var isInside = store.Location.Latitude >= boundingBox.Southwest.Latitude &&
                          store.Location.Latitude <= boundingBox.Northeast.Latitude &&
                          store.Location.Longitude >= boundingBox.Southwest.Longitude &&
                          store.Location.Longitude <= boundingBox.Northeast.Longitude;
            
            isInside.Should().BeTrue(
                $"Store {store.Name} should be within bounding box");
        }
    }
    
    [Fact]
    public async Task SpatialQueryAsync_H3BoundingBoxPaginated_FinalPageReturnsNullToken()
    {
        // Arrange - Create table with H3-indexed stores
        await CreateTableAsync<H3StoreLocationSortKeyEntity>();
        var table = new H3StoreTable(DynamoDb, TableName);
        
        // Define a bounding box
        var boundingBox = new GeoBoundingBox(
            southwest: new GeoLocation(37.77, -122.43),
            northeast: new GeoLocation(37.80, -122.40)
        );
        
        // Create 15 stores
        var stores = new List<H3StoreLocationSortKeyEntity>();
        for (int i = 0; i < 15; i++)
        {
            var latRange = boundingBox.Northeast.Latitude - boundingBox.Southwest.Latitude;
            var lonRange = boundingBox.Northeast.Longitude - boundingBox.Southwest.Longitude;
            
            var lat = boundingBox.Southwest.Latitude + (i % 5) * (latRange / 4);
            var lon = boundingBox.Southwest.Longitude + (i / 5) * (lonRange / 4);
            
            stores.Add(new H3StoreLocationSortKeyEntity
            {
                StoreId = "STORE",
                Location = new GeoLocation(lat, lon),
                Name = $"Store {i + 1:D3}",
                Description = $"Test store {i + 1}"
            });
        }
        
        foreach (var store in stores)
        {
            await table.PutAsync(store);
        }
        
        // Act - Iterate through all pages until null token
        SpatialContinuationToken? continuationToken = null;
        SpatialQueryResponse<H3StoreLocationSortKeyEntity>? lastResult = null;
        int pageCount = 0;
        int maxPages = 20; // Safety limit
        
        do
        {
            lastResult = await table.SpatialQueryAsync<H3StoreLocationSortKeyEntity>(
                locationSelector: store => store.Location,
                spatialIndexType: SpatialIndexType.H3,
                precision: 9,
                boundingBox: boundingBox,
                queryBuilder: (query, cell, pagination) => query
                    .Where<H3StoreLocationSortKeyEntity>(x => x.StoreId == "STORE" && x.Location == cell),
                pageSize: 6,
                continuationToken: continuationToken
            );
            
            continuationToken = lastResult.ContinuationToken;
            pageCount++;
            
            // Safety check
            if (pageCount >= maxPages)
            {
                break;
            }
        }
        while (continuationToken != null);
        
        // Assert - Final page should have null continuation token
        lastResult.Should().NotBeNull();
        lastResult!.ContinuationToken.Should().BeNull(
            "final page should return null continuation token");
        
        // Verify we iterated through at least one page
        pageCount.Should().BeGreaterThan(0, 
            "should have retrieved at least one page");
    }
    
    #endregion
    
    #region 27.8 Test H3 query with additional filter conditions
    
    [Fact]
    public async Task SpatialQueryAsync_H3ProximityWithAdditionalFilters_FiltersResultsByStatus()
    {
        // Arrange - Create table with H3-indexed stores with different statuses
        await CreateTableAsync<H3StoreLocationSortKeyEntity>();
        var table = new H3StoreTable(DynamoDb, TableName);
        
        // San Francisco downtown area - we'll search within 5km radius
        var searchCenter = new GeoLocation(37.7749, -122.4194);
        
        // Create stores at various distances with different statuses (using Description field as status)
        // Using closer locations to ensure they're within the same H3 cell coverage
        var stores = new[]
        {
            // Within 5km radius - ACTIVE stores (very close to center)
            new H3StoreLocationSortKeyEntity
            {
                StoreId = "STORE",
                Location = new GeoLocation(37.7749, -122.4194), // 0km - at center
                Name = "Downtown Store",
                Description = "ACTIVE"
            },
            new H3StoreLocationSortKeyEntity
            {
                StoreId = "STORE",
                Location = new GeoLocation(37.7759, -122.4204), // ~150m northeast
                Name = "Mission Store",
                Description = "ACTIVE"
            },
            new H3StoreLocationSortKeyEntity
            {
                StoreId = "STORE",
                Location = new GeoLocation(37.7739, -122.4184), // ~150m southwest
                Name = "Marina Store",
                Description = "ACTIVE"
            },
            // Within 5km radius - INACTIVE stores (should be filtered out)
            new H3StoreLocationSortKeyEntity
            {
                StoreId = "STORE",
                Location = new GeoLocation(37.7769, -122.4214), // ~200m north
                Name = "Haight Store",
                Description = "INACTIVE"
            },
            new H3StoreLocationSortKeyEntity
            {
                StoreId = "STORE",
                Location = new GeoLocation(37.7729, -122.4174), // ~200m south
                Name = "Financial District Store",
                Description = "INACTIVE"
            },
            // Within 5km radius - CLOSED stores (should be filtered out)
            new H3StoreLocationSortKeyEntity
            {
                StoreId = "STORE",
                Location = new GeoLocation(37.7754, -122.4199), // ~50m
                Name = "Tenderloin Store",
                Description = "CLOSED"
            },
            // Outside 5km radius - ACTIVE stores (should be filtered out by distance)
            new H3StoreLocationSortKeyEntity
            {
                StoreId = "STORE",
                Location = new GeoLocation(37.8044, -122.2712), // ~13km east (across bay)
                Name = "Oakland Store",
                Description = "ACTIVE"
            }
        };
        
        // Write all stores to DynamoDB
        foreach (var store in stores)
        {
            await table.PutAsync(store);
        }
        
        // Act - Execute SpatialQueryAsync with radius search AND status filter
        // Add additional WHERE condition to filter by Description = "ACTIVE"
        var result = await table.SpatialQueryAsync<H3StoreLocationSortKeyEntity>(
            locationSelector: store => store.Location,
            spatialIndexType: SpatialIndexType.H3,
            precision: 9, // H3 Resolution 9 (~174m hexagons)
            center: searchCenter,
            radiusKilometers: 5.0,
            queryBuilder: (query, cell, pagination) => query
                .Where<H3StoreLocationSortKeyEntity>(x => x.StoreId == "STORE" && x.Location == cell)
                .WithFilter("description = :status")
                .WithValue(":status", "ACTIVE"),
            pageSize: null // Non-paginated mode
        );
        
        // Assert - Verify only ACTIVE stores within radius are returned
        result.Items.Should().NotBeNull();
        result.Items.Should().HaveCount(3, "only 3 ACTIVE stores are within 5km radius");
        
        // Verify each result is within the radius
        foreach (var store in result.Items)
        {
            var distance = store.Location.DistanceToKilometers(searchCenter);
            distance.Should().BeLessThanOrEqualTo(5.0, 
                $"Store {store.Name} at {store.Location} should be within 5km radius");
        }
        
        // Verify all returned stores have ACTIVE status
        foreach (var store in result.Items)
        {
            store.Description.Should().Be("ACTIVE", 
                $"Store {store.Name} should have ACTIVE status");
        }
        
        // Verify the ACTIVE stores within radius are present
        var storeNames = result.Items.Select(s => s.Name).ToList();
        storeNames.Should().Contain("Downtown Store");
        storeNames.Should().Contain("Mission Store");
        storeNames.Should().Contain("Marina Store");
        
        // Verify the INACTIVE/CLOSED stores are NOT present (filtered by status)
        storeNames.Should().NotContain("Haight Store");
        storeNames.Should().NotContain("Financial District Store");
        storeNames.Should().NotContain("Tenderloin Store");
        
        // Verify the stores outside radius are NOT present (filtered by distance)
        storeNames.Should().NotContain("Oakland Store");
    }
    
    [Fact]
    public async Task SpatialQueryAsync_H3ProximityWithAdditionalFilters_FiltersResultsByNamePattern()
    {
        // Arrange - Create table with H3-indexed stores with different name patterns
        await CreateTableAsync<H3StoreLocationSortKeyEntity>();
        var table = new H3StoreTable(DynamoDb, TableName);
        
        var searchCenter = new GeoLocation(37.7749, -122.4194);
        
        // Create stores with different name patterns (using closer locations)
        var stores = new[]
        {
            // Within 5km radius - "Premium" stores (very close to center)
            new H3StoreLocationSortKeyEntity
            {
                StoreId = "STORE",
                Location = new GeoLocation(37.7749, -122.4194),
                Name = "Premium Downtown Store",
                Description = "Premium location"
            },
            new H3StoreLocationSortKeyEntity
            {
                StoreId = "STORE",
                Location = new GeoLocation(37.7759, -122.4204), // ~150m northeast
                Name = "Premium Mission Store",
                Description = "Premium location"
            },
            // Within 5km radius - "Standard" stores (should be filtered out)
            new H3StoreLocationSortKeyEntity
            {
                StoreId = "STORE",
                Location = new GeoLocation(37.7739, -122.4184), // ~150m southwest
                Name = "Standard Marina Store",
                Description = "Standard location"
            },
            new H3StoreLocationSortKeyEntity
            {
                StoreId = "STORE",
                Location = new GeoLocation(37.7769, -122.4214), // ~200m north
                Name = "Standard Haight Store",
                Description = "Standard location"
            },
            // Within 5km radius - "Express" stores (should be filtered out)
            new H3StoreLocationSortKeyEntity
            {
                StoreId = "STORE",
                Location = new GeoLocation(37.7729, -122.4174), // ~200m south
                Name = "Express Financial Store",
                Description = "Express location"
            }
        };
        
        // Write all stores to DynamoDB
        foreach (var store in stores)
        {
            await table.PutAsync(store);
        }
        
        // Act - Execute SpatialQueryAsync with radius search AND name pattern filter
        // Use begins_with filter to find stores with names starting with "Premium"
        var result = await table.SpatialQueryAsync<H3StoreLocationSortKeyEntity>(
            locationSelector: store => store.Location,
            spatialIndexType: SpatialIndexType.H3,
            precision: 9, // H3 Resolution 9 (~174m hexagons)
            center: searchCenter,
            radiusKilometers: 5.0,
            queryBuilder: (query, cell, pagination) => query
                .Where<H3StoreLocationSortKeyEntity>(x => x.StoreId == "STORE" && x.Location == cell)
                .WithFilter("begins_with(#name, :prefix)")
                .WithAttribute("#name", "name")
                .WithValue(":prefix", "Premium"),
            pageSize: null // Non-paginated mode
        );
        
        // Assert - Verify only "Premium" stores within radius are returned
        result.Items.Should().NotBeNull();
        result.Items.Should().HaveCount(2, "only 2 Premium stores are within 5km radius");
        
        // Verify each result is within the radius
        foreach (var store in result.Items)
        {
            var distance = store.Location.DistanceToKilometers(searchCenter);
            distance.Should().BeLessThanOrEqualTo(5.0, 
                $"Store {store.Name} at {store.Location} should be within 5km radius");
        }
        
        // Verify all returned stores have "Premium" in their name
        foreach (var store in result.Items)
        {
            store.Name.Should().StartWith("Premium", 
                $"Store {store.Name} should start with 'Premium'");
        }
        
        // Verify the Premium stores within radius are present
        var storeNames = result.Items.Select(s => s.Name).ToList();
        storeNames.Should().Contain("Premium Downtown Store");
        storeNames.Should().Contain("Premium Mission Store");
        
        // Verify the non-Premium stores are NOT present (filtered by name pattern)
        storeNames.Should().NotContain("Standard Marina Store");
        storeNames.Should().NotContain("Standard Haight Store");
        storeNames.Should().NotContain("Express Financial Store");
    }
    
    [Fact]
    public async Task SpatialQueryAsync_H3ProximityWithAdditionalFilters_CombinesMultipleFilters()
    {
        // Arrange - Create table with H3-indexed stores with multiple attributes
        await CreateTableAsync<H3StoreLocationSortKeyEntity>();
        var table = new H3StoreTable(DynamoDb, TableName);
        
        var searchCenter = new GeoLocation(37.7749, -122.4194);
        
        // Create stores with different combinations of attributes (using closer locations)
        var stores = new[]
        {
            // Within 5km radius - Premium AND Active (should be returned)
            new H3StoreLocationSortKeyEntity
            {
                StoreId = "STORE",
                Location = new GeoLocation(37.7749, -122.4194),
                Name = "Premium Downtown Store",
                Description = "ACTIVE"
            },
            new H3StoreLocationSortKeyEntity
            {
                StoreId = "STORE",
                Location = new GeoLocation(37.7759, -122.4204), // ~150m northeast
                Name = "Premium Mission Store",
                Description = "ACTIVE"
            },
            // Within 5km radius - Premium but INACTIVE (should be filtered out)
            new H3StoreLocationSortKeyEntity
            {
                StoreId = "STORE",
                Location = new GeoLocation(37.7739, -122.4184), // ~150m southwest
                Name = "Premium Marina Store",
                Description = "INACTIVE"
            },
            // Within 5km radius - Standard but ACTIVE (should be filtered out)
            new H3StoreLocationSortKeyEntity
            {
                StoreId = "STORE",
                Location = new GeoLocation(37.7769, -122.4214), // ~200m north
                Name = "Standard Haight Store",
                Description = "ACTIVE"
            },
            // Within 5km radius - Standard and INACTIVE (should be filtered out)
            new H3StoreLocationSortKeyEntity
            {
                StoreId = "STORE",
                Location = new GeoLocation(37.7729, -122.4174), // ~200m south
                Name = "Standard Financial Store",
                Description = "INACTIVE"
            }
        };
        
        // Write all stores to DynamoDB
        foreach (var store in stores)
        {
            await table.PutAsync(store);
        }
        
        // Act - Execute SpatialQueryAsync with radius search AND multiple filters
        // Filter by both name pattern (Premium) AND status (ACTIVE)
        var result = await table.SpatialQueryAsync<H3StoreLocationSortKeyEntity>(
            locationSelector: store => store.Location,
            spatialIndexType: SpatialIndexType.H3,
            precision: 9, // H3 Resolution 9 (~174m hexagons)
            center: searchCenter,
            radiusKilometers: 5.0,
            queryBuilder: (query, cell, pagination) => query
                .Where<H3StoreLocationSortKeyEntity>(x => x.StoreId == "STORE" && x.Location == cell)
                .WithFilter("begins_with(#name, :prefix) AND description = :status")
                .WithAttribute("#name", "name")
                .WithValue(":prefix", "Premium")
                .WithValue(":status", "ACTIVE"),
            pageSize: null // Non-paginated mode
        );
        
        // Assert - Verify only Premium AND Active stores within radius are returned
        result.Items.Should().NotBeNull();
        result.Items.Should().HaveCount(2, "only 2 Premium AND Active stores are within 5km radius");
        
        // Verify each result is within the radius
        foreach (var store in result.Items)
        {
            var distance = store.Location.DistanceToKilometers(searchCenter);
            distance.Should().BeLessThanOrEqualTo(5.0, 
                $"Store {store.Name} at {store.Location} should be within 5km radius");
        }
        
        // Verify all returned stores meet both criteria
        foreach (var store in result.Items)
        {
            store.Name.Should().StartWith("Premium", 
                $"Store {store.Name} should start with 'Premium'");
            store.Description.Should().Be("ACTIVE", 
                $"Store {store.Name} should have ACTIVE status");
        }
        
        // Verify the correct stores are present
        var storeNames = result.Items.Select(s => s.Name).ToList();
        storeNames.Should().Contain("Premium Downtown Store");
        storeNames.Should().Contain("Premium Mission Store");
        
        // Verify stores that don't meet both criteria are NOT present
        storeNames.Should().NotContain("Premium Marina Store", "INACTIVE");
        storeNames.Should().NotContain("Standard Haight Store", "not Premium");
        storeNames.Should().NotContain("Standard Financial Store", "not Premium and INACTIVE");
    }
    
    [Fact]
    public async Task SpatialQueryAsync_H3ProximityWithAdditionalFilters_WorksWithPagination()
    {
        // Arrange - Create table with many H3-indexed stores with different statuses
        await CreateTableAsync<H3StoreLocationSortKeyEntity>();
        var table = new H3StoreTable(DynamoDb, TableName);
        
        var searchCenter = new GeoLocation(37.7749, -122.4194);
        
        // Create 30 stores - half ACTIVE, half INACTIVE
        var stores = new List<H3StoreLocationSortKeyEntity>();
        var random = new Random(42); // Fixed seed for reproducibility
        
        for (int i = 0; i < 30; i++)
        {
            // Generate random locations within ~10km radius
            var latOffset = (random.NextDouble() - 0.5) * 0.18;
            var lonOffset = (random.NextDouble() - 0.5) * 0.18;
            
            var location = new GeoLocation(
                searchCenter.Latitude + latOffset,
                searchCenter.Longitude + lonOffset
            );
            
            // Alternate between ACTIVE and INACTIVE
            var status = i % 2 == 0 ? "ACTIVE" : "INACTIVE";
            
            stores.Add(new H3StoreLocationSortKeyEntity
            {
                StoreId = "STORE",
                Location = location,
                Name = $"Store {i:D3}",
                Description = status
            });
        }
        
        // Write all stores to DynamoDB
        foreach (var store in stores)
        {
            await table.PutAsync(store);
        }
        
        // Act - Execute paginated SpatialQueryAsync with status filter
        var allResults = new List<H3StoreLocationSortKeyEntity>();
        var continuationToken = (SpatialContinuationToken?)null;
        var pageCount = 0;
        var maxPages = 20; // Safety limit
        
        do
        {
            pageCount++;
            
            var result = await table.SpatialQueryAsync<H3StoreLocationSortKeyEntity>(
                locationSelector: store => store.Location,
                spatialIndexType: SpatialIndexType.H3,
                precision: 9,
                center: searchCenter,
                radiusKilometers: 10.0,
                queryBuilder: (query, cell, pagination) => query
                    .Where<H3StoreLocationSortKeyEntity>(x => x.StoreId == "STORE" && x.Location == cell)
                    .WithFilter("description = :status")
                    .WithValue(":status", "ACTIVE"),
                pageSize: 5, // Small page size to ensure multiple pages
                continuationToken: continuationToken
            );
            
            allResults.AddRange(result.Items);
            continuationToken = result.ContinuationToken;
            
            // Verify page size is respected
            if (continuationToken != null)
            {
                result.Items.Count.Should().BeLessThanOrEqualTo(5, 
                    $"page {pageCount} should respect page size limit");
            }
            
            // Safety check
            if (pageCount >= maxPages)
            {
                break;
            }
        }
        while (continuationToken != null);
        
        // Assert - Verify all results are ACTIVE and within radius
        allResults.Should().NotBeEmpty();
        
        // Verify all returned stores have ACTIVE status
        foreach (var store in allResults)
        {
            store.Description.Should().Be("ACTIVE", 
                $"Store {store.Name} should have ACTIVE status");
            
            var distance = store.Location.DistanceToKilometers(searchCenter);
            distance.Should().BeLessThanOrEqualTo(10.0, 
                $"Store {store.Name} should be within 10km radius");
        }
        
        // Verify no duplicates across pages
        var uniqueNames = allResults.Select(s => s.Name).Distinct().Count();
        uniqueNames.Should().Be(allResults.Count, 
            "should not have duplicate stores across pages");
        
        // Verify we got roughly half the stores (only ACTIVE ones)
        // Allow some variance due to distance filtering
        allResults.Count.Should().BeGreaterThan(5, 
            "should have retrieved multiple ACTIVE stores");
        allResults.Count.Should().BeLessThan(30, 
            "should have filtered out INACTIVE stores");
        
        // Log summary
        Console.WriteLine($"Retrieved {allResults.Count} ACTIVE stores across {pageCount} pages");
    }
    
    #endregion
    
    #region 27.9 Test H3 query with sort key conditions
    
    /// <summary>
    /// Table wrapper for testing spatial queries with H3StoreEntity (which has a Region sort key).
    /// </summary>
    private class H3StoreWithRegionTable : DynamoDbTableBase
    {
        public H3StoreWithRegionTable(IAmazonDynamoDB client, string tableName) 
            : base(client, tableName)
        {
        }
        
        public async Task PutAsync(H3StoreEntity entity)
        {
            var item = H3StoreEntity.ToDynamoDb(entity);
            await DynamoDbClient.PutItemAsync(Name, item);
        }
    }
    
    [Fact]
    public async Task SpatialQueryAsync_H3ProximityWithSortKeyCondition_FiltersResultsByRegion()
    {
        // Arrange - Create table with H3-indexed stores with different regions (sort key)
        await CreateTableAsync<H3StoreEntity>();
        var table = new H3StoreWithRegionTable(DynamoDb, TableName);
        
        // San Francisco downtown area - we'll search within 5km radius
        var searchCenter = new GeoLocation(37.7749, -122.4194);
        
        // Create stores at various distances with different regions (sort key)
        // Using closer locations to ensure they're within the same H3 cell coverage
        var stores = new[]
        {
            // Within 5km radius - NORTH region (should be returned)
            new H3StoreEntity
            {
                StoreId = "STORE",
                Region = "NORTH",
                Location = new GeoLocation(37.7749, -122.4194), // 0km - at center
                Name = "Downtown North Store",
                Description = "North region store"
            },
            new H3StoreEntity
            {
                StoreId = "STORE",
                Region = "NORTH",
                Location = new GeoLocation(37.7759, -122.4204), // ~150m northeast
                Name = "Mission North Store",
                Description = "North region store"
            },
            new H3StoreEntity
            {
                StoreId = "STORE",
                Region = "NORTH",
                Location = new GeoLocation(37.7739, -122.4184), // ~150m southwest
                Name = "Marina North Store",
                Description = "North region store"
            },
            // Within 5km radius - SOUTH region (should be filtered out by sort key)
            new H3StoreEntity
            {
                StoreId = "STORE",
                Region = "SOUTH",
                Location = new GeoLocation(37.7769, -122.4214), // ~200m north
                Name = "Haight South Store",
                Description = "South region store"
            },
            new H3StoreEntity
            {
                StoreId = "STORE",
                Region = "SOUTH",
                Location = new GeoLocation(37.7729, -122.4174), // ~200m south
                Name = "Financial South Store",
                Description = "South region store"
            },
            // Within 5km radius - EAST region (should be filtered out by sort key)
            new H3StoreEntity
            {
                StoreId = "STORE",
                Region = "EAST",
                Location = new GeoLocation(37.7754, -122.4199), // ~50m
                Name = "Tenderloin East Store",
                Description = "East region store"
            },
            // Outside 5km radius - NORTH region (should be filtered out by distance)
            new H3StoreEntity
            {
                StoreId = "STORE",
                Region = "NORTH",
                Location = new GeoLocation(37.8044, -122.2712), // ~13km east (across bay)
                Name = "Oakland North Store",
                Description = "North region store"
            }
        };
        
        // Write all stores to DynamoDB
        foreach (var store in stores)
        {
            await table.PutAsync(store);
        }
        
        // Act - Execute SpatialQueryAsync with radius search AND sort key condition
        // Add sort key condition to filter by Region = "NORTH"
        // Note: Location is a regular attribute with spatial index (not part of key), Region is the sort key
        // The H3 hash (location) must be used in a filter expression, not in the key condition
        var result = await table.SpatialQueryAsync<H3StoreEntity>(
            locationSelector: store => store.Location,
            spatialIndexType: SpatialIndexType.H3,
            precision: 9, // H3 Resolution 9 (~174m hexagons)
            center: searchCenter,
            radiusKilometers: 5.0,
            queryBuilder: (query, cell, pagination) => query
                .Where("pk = :pk AND sk = :region")
                .WithValue(":pk", "STORE")
                .WithValue(":region", "NORTH")
                .WithFilter("#loc = :loc")
                .WithAttribute("#loc", "location")
                .WithValue(":loc", cell),
            pageSize: null // Non-paginated mode
        );
        
        // Assert - Verify only NORTH region stores within radius are returned
        result.Items.Should().NotBeNull();
        result.Items.Should().HaveCount(3, "only 3 NORTH region stores are within 5km radius");
        
        // Verify each result is within the radius
        foreach (var store in result.Items)
        {
            var distance = store.Location.DistanceToKilometers(searchCenter);
            distance.Should().BeLessThanOrEqualTo(5.0, 
                $"Store {store.Name} at {store.Location} should be within 5km radius");
        }
        
        // Verify all returned stores have NORTH region
        foreach (var store in result.Items)
        {
            store.Region.Should().Be("NORTH", 
                $"Store {store.Name} should have NORTH region");
        }
        
        // Verify the NORTH region stores within radius are present
        var storeNames = result.Items.Select(s => s.Name).ToList();
        storeNames.Should().Contain("Downtown North Store");
        storeNames.Should().Contain("Mission North Store");
        storeNames.Should().Contain("Marina North Store");
        
        // Verify the SOUTH/EAST region stores are NOT present (filtered by sort key)
        storeNames.Should().NotContain("Haight South Store");
        storeNames.Should().NotContain("Financial South Store");
        storeNames.Should().NotContain("Tenderloin East Store");
        
        // Verify the stores outside radius are NOT present (filtered by distance)
        storeNames.Should().NotContain("Oakland North Store");
    }
    
    [Fact]
    public async Task SpatialQueryAsync_H3ProximityWithSortKeyRange_FiltersResultsByRegionRange()
    {
        // Arrange - Create table with H3-indexed stores with different regions
        await CreateTableAsync<H3StoreEntity>();
        var table = new H3StoreWithRegionTable(DynamoDb, TableName);
        
        var searchCenter = new GeoLocation(37.7749, -122.4194);
        
        // Create stores with regions that sort lexicographically
        var stores = new[]
        {
            // Within 5km radius - Regions A-M (should be returned)
            new H3StoreEntity
            {
                StoreId = "STORE",
                Region = "ALPHA",
                Location = new GeoLocation(37.7749, -122.4194),
                Name = "Alpha Store",
                Description = "Alpha region"
            },
            new H3StoreEntity
            {
                StoreId = "STORE",
                Region = "BETA",
                Location = new GeoLocation(37.7759, -122.4204), // ~150m northeast
                Name = "Beta Store",
                Description = "Beta region"
            },
            new H3StoreEntity
            {
                StoreId = "STORE",
                Region = "GAMMA",
                Location = new GeoLocation(37.7739, -122.4184), // ~150m southwest
                Name = "Gamma Store",
                Description = "Gamma region"
            },
            // Within 5km radius - Regions N-Z (should be filtered out by sort key range)
            new H3StoreEntity
            {
                StoreId = "STORE",
                Region = "OMEGA",
                Location = new GeoLocation(37.7769, -122.4214), // ~200m north
                Name = "Omega Store",
                Description = "Omega region"
            },
            new H3StoreEntity
            {
                StoreId = "STORE",
                Region = "ZETA",
                Location = new GeoLocation(37.7729, -122.4174), // ~200m south
                Name = "Zeta Store",
                Description = "Zeta region"
            }
        };
        
        // Write all stores to DynamoDB
        foreach (var store in stores)
        {
            await table.PutAsync(store);
        }
        
        // Act - Execute SpatialQueryAsync with radius search AND sort key range condition
        // Use BETWEEN to filter regions from A to M (lexicographically)
        // The H3 hash (location) must be used in a filter expression, not in the key condition
        var result = await table.SpatialQueryAsync<H3StoreEntity>(
            locationSelector: store => store.Location,
            spatialIndexType: SpatialIndexType.H3,
            precision: 9,
            center: searchCenter,
            radiusKilometers: 5.0,
            queryBuilder: (query, cell, pagination) => query
                .Where("pk = :pk AND sk BETWEEN :minRegion AND :maxRegion")
                .WithValue(":pk", "STORE")
                .WithValue(":minRegion", "A")
                .WithValue(":maxRegion", "M")
                .WithFilter("#loc = :loc")
                .WithAttribute("#loc", "location")
                .WithValue(":loc", cell),
            pageSize: null
        );
        
        // Assert - Verify only stores with regions A-M within radius are returned
        result.Items.Should().NotBeNull();
        result.Items.Should().HaveCount(3, "only 3 stores with regions A-M are within 5km radius");
        
        // Verify each result is within the radius
        foreach (var store in result.Items)
        {
            var distance = store.Location.DistanceToKilometers(searchCenter);
            distance.Should().BeLessThanOrEqualTo(5.0, 
                $"Store {store.Name} at {store.Location} should be within 5km radius");
        }
        
        // Verify all returned stores have regions in the A-M range
        foreach (var store in result.Items)
        {
            store.Region.Should().Match(r => string.Compare(r, "A", StringComparison.Ordinal) >= 0 
                                           && string.Compare(r, "M", StringComparison.Ordinal) <= 0,
                $"Store {store.Name} should have region between A and M");
        }
        
        // Verify the A-M region stores are present
        var storeNames = result.Items.Select(s => s.Name).ToList();
        storeNames.Should().Contain("Alpha Store");
        storeNames.Should().Contain("Beta Store");
        storeNames.Should().Contain("Gamma Store");
        
        // Verify the N-Z region stores are NOT present (filtered by sort key range)
        storeNames.Should().NotContain("Omega Store");
        storeNames.Should().NotContain("Zeta Store");
    }
    
    [Fact]
    public async Task SpatialQueryAsync_H3ProximityWithSortKeyCondition_WorksWithPagination()
    {
        // Arrange - Create table with many H3-indexed stores with different regions
        await CreateTableAsync<H3StoreEntity>();
        var table = new H3StoreWithRegionTable(DynamoDb, TableName);
        
        var searchCenter = new GeoLocation(37.7749, -122.4194);
        
        // Create 30 stores - half NORTH region, half SOUTH region
        var stores = new List<H3StoreEntity>();
        var random = new Random(42); // Fixed seed for reproducibility
        
        for (int i = 0; i < 30; i++)
        {
            // Generate random locations within ~10km radius
            var latOffset = (random.NextDouble() - 0.5) * 0.18;
            var lonOffset = (random.NextDouble() - 0.5) * 0.18;
            
            var location = new GeoLocation(
                searchCenter.Latitude + latOffset,
                searchCenter.Longitude + lonOffset
            );
            
            // Alternate between NORTH and SOUTH regions
            var region = i % 2 == 0 ? "NORTH" : "SOUTH";
            
            stores.Add(new H3StoreEntity
            {
                StoreId = "STORE",
                Region = region,
                Location = location,
                Name = $"Store {i:D3}",
                Description = $"{region} region store"
            });
        }
        
        // Write all stores to DynamoDB
        foreach (var store in stores)
        {
            await table.PutAsync(store);
        }
        
        // Act - Execute paginated SpatialQueryAsync with sort key condition
        var allResults = new List<H3StoreEntity>();
        var continuationToken = (SpatialContinuationToken?)null;
        var pageCount = 0;
        var maxPages = 20; // Safety limit
        
        do
        {
            pageCount++;
            
            var result = await table.SpatialQueryAsync<H3StoreEntity>(
                locationSelector: store => store.Location,
                spatialIndexType: SpatialIndexType.H3,
                precision: 9,
                center: searchCenter,
                radiusKilometers: 10.0,
                queryBuilder: (query, cell, pagination) => query
                    .Where("pk = :pk AND sk = :region")
                    .WithValue(":pk", "STORE")
                    .WithValue(":region", "NORTH")
                    .WithFilter("#loc = :loc")
                    .WithAttribute("#loc", "location")
                    .WithValue(":loc", cell),
                pageSize: 5, // Small page size to ensure multiple pages
                continuationToken: continuationToken
            );
            
            allResults.AddRange(result.Items);
            continuationToken = result.ContinuationToken;
            
            // Verify page size is respected
            if (continuationToken != null)
            {
                result.Items.Count.Should().BeLessThanOrEqualTo(5, 
                    $"page {pageCount} should respect page size limit");
            }
            
            // Safety check
            if (pageCount >= maxPages)
            {
                break;
            }
        }
        while (continuationToken != null);
        
        // Assert - Verify all results are NORTH region and within radius
        allResults.Should().NotBeEmpty();
        
        // Verify all returned stores have NORTH region
        foreach (var store in allResults)
        {
            store.Region.Should().Be("NORTH", 
                $"Store {store.Name} should have NORTH region");
            
            var distance = store.Location.DistanceToKilometers(searchCenter);
            distance.Should().BeLessThanOrEqualTo(10.0, 
                $"Store {store.Name} should be within 10km radius");
        }
        
        // Verify no duplicates across pages
        var uniqueNames = allResults.Select(s => s.Name).Distinct().Count();
        uniqueNames.Should().Be(allResults.Count, 
            "should not have duplicate stores across pages");
        
        // Verify we got roughly half the stores (only NORTH region ones)
        // Allow some variance due to distance filtering
        allResults.Count.Should().BeGreaterThan(5, 
            "should have retrieved multiple NORTH region stores");
        allResults.Count.Should().BeLessThan(30, 
            "should have filtered out SOUTH region stores");
        
        // Log summary
        Console.WriteLine($"Retrieved {allResults.Count} NORTH region stores across {pageCount} pages");
    }
    
    [Fact]
    public async Task SpatialQueryAsync_H3ProximityWithSortKeyCondition_CombinesWithFilterExpression()
    {
        // Arrange - Create table with H3-indexed stores with regions and statuses
        await CreateTableAsync<H3StoreEntity>();
        var table = new H3StoreWithRegionTable(DynamoDb, TableName);
        
        var searchCenter = new GeoLocation(37.7749, -122.4194);
        
        // Create stores with different combinations of region (sort key) and status (filter)
        var stores = new[]
        {
            // Within 5km radius - NORTH region AND ACTIVE (should be returned)
            new H3StoreEntity
            {
                StoreId = "STORE",
                Region = "NORTH",
                Location = new GeoLocation(37.7749, -122.4194),
                Name = "Downtown North Active",
                Description = "ACTIVE"
            },
            new H3StoreEntity
            {
                StoreId = "STORE",
                Region = "NORTH",
                Location = new GeoLocation(37.7759, -122.4204), // ~150m northeast
                Name = "Mission North Active",
                Description = "ACTIVE"
            },
            // Within 5km radius - NORTH region but INACTIVE (should be filtered out by filter expression)
            new H3StoreEntity
            {
                StoreId = "STORE",
                Region = "NORTH",
                Location = new GeoLocation(37.7739, -122.4184), // ~150m southwest
                Name = "Marina North Inactive",
                Description = "INACTIVE"
            },
            // Within 5km radius - SOUTH region but ACTIVE (should be filtered out by sort key)
            new H3StoreEntity
            {
                StoreId = "STORE",
                Region = "SOUTH",
                Location = new GeoLocation(37.7769, -122.4214), // ~200m north
                Name = "Haight South Active",
                Description = "ACTIVE"
            },
            // Within 5km radius - SOUTH region and INACTIVE (should be filtered out by both)
            new H3StoreEntity
            {
                StoreId = "STORE",
                Region = "SOUTH",
                Location = new GeoLocation(37.7729, -122.4174), // ~200m south
                Name = "Financial South Inactive",
                Description = "INACTIVE"
            }
        };
        
        // Write all stores to DynamoDB
        foreach (var store in stores)
        {
            await table.PutAsync(store);
        }
        
        // Act - Execute SpatialQueryAsync with radius search, sort key condition, AND filter expression
        // Combine sort key condition (Region = NORTH) with filter expressions (H3 hash AND Description = ACTIVE)
        // The H3 hash (location) must be used in a filter expression, not in the key condition
        var result = await table.SpatialQueryAsync<H3StoreEntity>(
            locationSelector: store => store.Location,
            spatialIndexType: SpatialIndexType.H3,
            precision: 9,
            center: searchCenter,
            radiusKilometers: 5.0,
            queryBuilder: (query, cell, pagination) => query
                .Where("pk = :pk AND sk = :region")
                .WithValue(":pk", "STORE")
                .WithValue(":region", "NORTH")
                .WithFilter("#loc = :loc AND description = :status")
                .WithAttribute("#loc", "location")
                .WithValue(":loc", cell)
                .WithValue(":status", "ACTIVE"),
            pageSize: null
        );
        
        // Assert - Verify only NORTH region AND ACTIVE stores within radius are returned
        result.Items.Should().NotBeNull();
        result.Items.Should().HaveCount(2, "only 2 NORTH region AND ACTIVE stores are within 5km radius");
        
        // Verify each result is within the radius
        foreach (var store in result.Items)
        {
            var distance = store.Location.DistanceToKilometers(searchCenter);
            distance.Should().BeLessThanOrEqualTo(5.0, 
                $"Store {store.Name} at {store.Location} should be within 5km radius");
        }
        
        // Verify all returned stores meet both criteria
        foreach (var store in result.Items)
        {
            store.Region.Should().Be("NORTH", 
                $"Store {store.Name} should have NORTH region");
            store.Description.Should().Be("ACTIVE", 
                $"Store {store.Name} should have ACTIVE status");
        }
        
        // Verify the correct stores are present
        var storeNames = result.Items.Select(s => s.Name).ToList();
        storeNames.Should().Contain("Downtown North Active");
        storeNames.Should().Contain("Mission North Active");
        
        // Verify stores that don't meet both criteria are NOT present
        storeNames.Should().NotContain("Marina North Inactive", "INACTIVE");
        storeNames.Should().NotContain("Haight South Active", "SOUTH region");
        storeNames.Should().NotContain("Financial South Inactive", "SOUTH region and INACTIVE");
    }
    
    #endregion
}
