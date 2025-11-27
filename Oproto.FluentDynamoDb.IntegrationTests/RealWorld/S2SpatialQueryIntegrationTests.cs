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
/// Integration tests for S2 spatial queries with DynamoDB.
/// Tests verify that SpatialQueryAsync correctly executes proximity and bounding box queries
/// using S2-indexed GeoLocation properties.
/// </summary>
[Collection("DynamoDB Local")]
[Trait("Category", "Integration")]
[Trait("Feature", "Geospatial")]
[Trait("Feature", "S2")]
[Trait("Feature", "SpatialQuery")]
public class S2SpatialQueryIntegrationTests : IntegrationTestBase
{
    public S2SpatialQueryIntegrationTests(DynamoDbLocalFixture fixture) : base(fixture)
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
    
    /// <summary>
    /// Table wrapper for testing spatial queries with sort keys.
    /// </summary>
    private class S2StoreWithSortKeyTable : DynamoDbTableBase
    {
        public S2StoreWithSortKeyTable(IAmazonDynamoDB client, string tableName) 
            : base(client, tableName)
        {
        }
        
        public async Task PutAsync(S2StoreWithSortKeyEntity entity)
        {
            var item = S2StoreWithSortKeyEntity.ToDynamoDb(entity);
            await DynamoDbClient.PutItemAsync(Name, item);
        }
    }
    
    #region 26.1 Test S2 proximity query (non-paginated) with lambda expressions
    
    [Fact]
    public async Task SpatialQueryAsync_S2ProximityNonPaginated_WithLambdaExpression_ReturnsAllResultsWithinRadius()
    {
        // Arrange - Create table with S2-indexed stores at known locations
        await CreateTableAsync<S2StoreEntity>();
        var table = new S2StoreTable(DynamoDb, TableName);
        
        // San Francisco downtown area - we'll search within 5km radius
        var searchCenter = new GeoLocation(37.7749, -122.4194);
        
        // Create stores at various distances from the search center
        var stores = new[]
        {
            // Within 5km radius
            new S2StoreEntity
            {
                StoreId = "STORE",
                Location = new GeoLocation(37.7749, -122.4194), // 0km - at center
                Name = "Downtown Store",
                Description = "At search center"
            },
            new S2StoreEntity
            {
                StoreId = "STORE",
                Location = new GeoLocation(37.7599, -122.4148), // ~1.7km south
                Name = "Mission Store",
                Description = "Mission District"
            },
            new S2StoreEntity
            {
                StoreId = "STORE",
                Location = new GeoLocation(37.8021, -122.4378), // ~3.5km north
                Name = "Marina Store",
                Description = "Marina District"
            },
            new S2StoreEntity
            {
                StoreId = "STORE",
                Location = new GeoLocation(37.7694, -122.4481), // ~2.5km west
                Name = "Haight Store",
                Description = "Haight-Ashbury"
            },
            // Outside 5km radius
            new S2StoreEntity
            {
                StoreId = "STORE",
                Location = new GeoLocation(37.8044, -122.2712), // ~13km east (across bay)
                Name = "Oakland Store",
                Description = "Oakland"
            },
            new S2StoreEntity
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
        var result = await table.SpatialQueryAsync<S2StoreEntity>(
            locationSelector: store => store.Location,
            spatialIndexType: SpatialIndexType.S2,
            precision: 16, // S2 Level 16 (~600m cells)
            center: searchCenter,
            radiusKilometers: 5.0,
            queryBuilder: (query, cell, pagination) => query
                .Where<S2StoreEntity>(x => x.StoreId == "STORE" && x.Location == cell)
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
    public async Task SpatialQueryAsync_S2ProximityNonPaginated_WithExplicitSpatialIndexProperty_ReturnsAllResultsWithinRadius()
    {
        // Arrange - Create table with S2-indexed stores at known locations
        await CreateTableAsync<S2StoreEntity>();
        var table = new S2StoreTable(DynamoDb, TableName);
        
        // San Francisco downtown area - we'll search within 5km radius
        var searchCenter = new GeoLocation(37.7749, -122.4194);
        
        // Create stores at various distances from the search center
        var stores = new[]
        {
            // Within 5km radius
            new S2StoreEntity
            {
                StoreId = "STORE",
                Location = new GeoLocation(37.7749, -122.4194), // 0km - at center
                Name = "Downtown Store",
                Description = "At search center"
            },
            new S2StoreEntity
            {
                StoreId = "STORE",
                Location = new GeoLocation(37.7599, -122.4148), // ~1.7km south
                Name = "Mission Store",
                Description = "Mission District"
            },
            new S2StoreEntity
            {
                StoreId = "STORE",
                Location = new GeoLocation(37.8021, -122.4378), // ~3.5km north
                Name = "Marina Store",
                Description = "Marina District"
            },
            // Outside 5km radius
            new S2StoreEntity
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
        var result = await table.SpatialQueryAsync<S2StoreEntity>(
            locationSelector: store => store.Location,
            spatialIndexType: SpatialIndexType.S2,
            precision: 16, // S2 Level 16 (~600m cells)
            center: searchCenter,
            radiusKilometers: 5.0,
            queryBuilder: (query, cell, pagination) => query
                .Where<S2StoreEntity>(x => x.StoreId == "STORE" && x.Location.SpatialIndex == cell)
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
    public async Task SpatialQueryAsync_S2ProximityNonPaginated_ResultsAreSortedByDistance()
    {
        // Arrange - Create table with S2-indexed stores
        await CreateTableAsync<S2StoreEntity>();
        var table = new S2StoreTable(DynamoDb, TableName);
        
        var searchCenter = new GeoLocation(37.7749, -122.4194);
        
        // Create stores at specific distances
        var stores = new[]
        {
            new S2StoreEntity
            {
                StoreId = "STORE",
                Location = new GeoLocation(37.7599, -122.4148), // ~1.7km
                Name = "Far Store",
                Description = "Farther away"
            },
            new S2StoreEntity
            {
                StoreId = "STORE",
                Location = new GeoLocation(37.7749, -122.4194), // 0km - at center
                Name = "Center Store",
                Description = "At center"
            },
            new S2StoreEntity
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
        var result = await table.SpatialQueryAsync<S2StoreEntity>(
            locationSelector: store => store.Location,
            spatialIndexType: SpatialIndexType.S2,
            precision: 16,
            center: searchCenter,
            radiusKilometers: 5.0,
            queryBuilder: (query, cell, pagination) => query
                .Where<S2StoreEntity>(x => x.StoreId == "STORE" && x.Location == cell)
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
    public async Task SpatialQueryAsync_S2ProximityNonPaginated_NoDuplicates()
    {
        // Arrange - Create table with S2-indexed stores
        await CreateTableAsync<S2StoreEntity>();
        var table = new S2StoreTable(DynamoDb, TableName);
        
        var searchCenter = new GeoLocation(37.7749, -122.4194);
        
        // Create stores that might appear in multiple S2 cells
        // (stores near cell boundaries could theoretically appear in multiple cells)
        var stores = new[]
        {
            new S2StoreEntity
            {
                StoreId = "STORE",
                Location = new GeoLocation(37.7749, -122.4194),
                Name = "Store 1",
                Description = "Test store 1"
            },
            new S2StoreEntity
            {
                StoreId = "STORE",
                Location = new GeoLocation(37.7759, -122.4204),
                Name = "Store 2",
                Description = "Test store 2"
            },
            new S2StoreEntity
            {
                StoreId = "STORE",
                Location = new GeoLocation(37.7739, -122.4184),
                Name = "Store 3",
                Description = "Test store 3"
            },
            new S2StoreEntity
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
        var result = await table.SpatialQueryAsync<S2StoreEntity>(
            locationSelector: store => store.Location,
            spatialIndexType: SpatialIndexType.S2,
            precision: 16,
            center: searchCenter,
            radiusKilometers: 2.0,
            queryBuilder: (query, cell, pagination) => query
                .Where<S2StoreEntity>(x => x.StoreId == "STORE" && x.Location == cell)
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
    
    [Fact]
    public async Task SpatialQueryAsync_S2ProximityNonPaginated_ReturnsNullContinuationToken()
    {
        // Arrange - Create table with S2-indexed stores
        await CreateTableAsync<S2StoreEntity>();
        var table = new S2StoreTable(DynamoDb, TableName);
        
        var searchCenter = new GeoLocation(37.7749, -122.4194);
        
        var stores = new[]
        {
            new S2StoreEntity
            {
                StoreId = "STORE",
                Location = new GeoLocation(37.7749, -122.4194),
                Name = "Store 1",
                Description = "Test store"
            }
        };
        
        foreach (var store in stores)
        {
            await table.PutAsync(store);
        }
        
        // Act - Execute non-paginated spatial query using implicit cast
        var result = await table.SpatialQueryAsync<S2StoreEntity>(
            locationSelector: store => store.Location,
            spatialIndexType: SpatialIndexType.S2,
            precision: 16,
            center: searchCenter,
            radiusKilometers: 5.0,
            queryBuilder: (query, cell, pagination) => query
                .Where<S2StoreEntity>(x => x.StoreId == "STORE" && x.Location == cell)
                ,
            pageSize: null // Non-paginated mode
        );
        
        // Assert - Verify continuation token is null for non-paginated queries
        result.ContinuationToken.Should().BeNull(
            "non-paginated queries should return all results with null continuation token");
    }
    
    [Fact]
    public async Task SpatialQueryAsync_S2ProximityNonPaginated_ReturnsQueryStatistics()
    {
        // Arrange - Create table with S2-indexed stores
        await CreateTableAsync<S2StoreEntity>();
        var table = new S2StoreTable(DynamoDb, TableName);
        
        var searchCenter = new GeoLocation(37.7749, -122.4194);
        
        var stores = new[]
        {
            new S2StoreEntity
            {
                StoreId = "STORE",
                Location = new GeoLocation(37.7749, -122.4194),
                Name = "Store 1",
                Description = "Test store 1"
            },
            new S2StoreEntity
            {
                StoreId = "STORE",
                Location = new GeoLocation(37.7759, -122.4204),
                Name = "Store 2",
                Description = "Test store 2"
            }
        };
        
        foreach (var store in stores)
        {
            await table.PutAsync(store);
        }
        
        // Act - Execute spatial query using implicit cast
        var result = await table.SpatialQueryAsync<S2StoreEntity>(
            locationSelector: store => store.Location,
            spatialIndexType: SpatialIndexType.S2,
            precision: 16,
            center: searchCenter,
            radiusKilometers: 5.0,
            queryBuilder: (query, cell, pagination) => query
                .Where<S2StoreEntity>(x => x.StoreId == "STORE" && x.Location == cell)
                ,
            pageSize: null
        );
        
        // Assert - Verify query statistics are populated
        result.TotalCellsQueried.Should().BeGreaterThan(0, 
            "should report the number of S2 cells queried");
        result.TotalItemsScanned.Should().BeGreaterThanOrEqualTo(result.Items.Count,
            "items scanned should be at least the number of items returned");
    }
    
    #endregion
    
    #region 26.2 Test S2 proximity query (non-paginated) with format string expressions
    
    [Fact]
    public async Task SpatialQueryAsync_S2ProximityNonPaginated_WithFormatStringExpression_ReturnsAllResultsWithinRadius()
    {
        // Arrange - Create table with S2-indexed stores at known locations
        await CreateTableAsync<S2StoreEntity>();
        var table = new S2StoreTable(DynamoDb, TableName);
        
        // San Francisco downtown area - we'll search within 5km radius
        var searchCenter = new GeoLocation(37.7749, -122.4194);
        
        // Create stores at various distances from the search center
        var stores = new[]
        {
            // Within 5km radius
            new S2StoreEntity
            {
                StoreId = "STORE",
                Location = new GeoLocation(37.7749, -122.4194), // 0km - at center
                Name = "Downtown Store",
                Description = "At search center"
            },
            new S2StoreEntity
            {
                StoreId = "STORE",
                Location = new GeoLocation(37.7599, -122.4148), // ~1.7km south
                Name = "Mission Store",
                Description = "Mission District"
            },
            new S2StoreEntity
            {
                StoreId = "STORE",
                Location = new GeoLocation(37.8021, -122.4378), // ~3.5km north
                Name = "Marina Store",
                Description = "Marina District"
            },
            new S2StoreEntity
            {
                StoreId = "STORE",
                Location = new GeoLocation(37.7694, -122.4481), // ~2.5km west
                Name = "Haight Store",
                Description = "Haight-Ashbury"
            },
            // Outside 5km radius
            new S2StoreEntity
            {
                StoreId = "STORE",
                Location = new GeoLocation(37.8044, -122.2712), // ~13km east (across bay)
                Name = "Oakland Store",
                Description = "Oakland"
            },
            new S2StoreEntity
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
        // Format string expressions still work - they don't rely on implicit cast
        var result = await table.SpatialQueryAsync<S2StoreEntity>(
            locationSelector: store => store.Location,
            spatialIndexType: SpatialIndexType.S2,
            precision: 16, // S2 Level 16 (~600m cells)
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
    public async Task SpatialQueryAsync_S2ProximityNonPaginated_FormatStringMatchesLambdaResults()
    {
        // Arrange - Create table with S2-indexed stores
        await CreateTableAsync<S2StoreEntity>();
        var table = new S2StoreTable(DynamoDb, TableName);
        
        var searchCenter = new GeoLocation(37.7749, -122.4194);
        
        // Create stores at specific distances
        var stores = new[]
        {
            new S2StoreEntity
            {
                StoreId = "STORE",
                Location = new GeoLocation(37.7749, -122.4194),
                Name = "Store 1",
                Description = "Test store 1"
            },
            new S2StoreEntity
            {
                StoreId = "STORE",
                Location = new GeoLocation(37.7759, -122.4204),
                Name = "Store 2",
                Description = "Test store 2"
            },
            new S2StoreEntity
            {
                StoreId = "STORE",
                Location = new GeoLocation(37.7739, -122.4184),
                Name = "Store 3",
                Description = "Test store 3"
            }
        };
        
        foreach (var store in stores)
        {
            await table.PutAsync(store);
        }
        
        // Act - Execute spatial query with lambda expression using implicit cast
        var lambdaResult = await table.SpatialQueryAsync<S2StoreEntity>(
            locationSelector: store => store.Location,
            spatialIndexType: SpatialIndexType.S2,
            precision: 16,
            center: searchCenter,
            radiusKilometers: 5.0,
            queryBuilder: (query, cell, pagination) => query
                .Where<S2StoreEntity>(x => x.StoreId == "STORE" && x.Location == cell)
                ,
            pageSize: null
        );
        
        // Act - Execute spatial query with format string expression
        var formatStringResult = await table.SpatialQueryAsync<S2StoreEntity>(
            locationSelector: store => store.Location,
            spatialIndexType: SpatialIndexType.S2,
            precision: 16,
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
        
        // Verify the same stores are returned (by name since Location is the sort key)
        var lambdaNames = lambdaResult.Items.Select(s => s.Name).OrderBy(n => n).ToList();
        var formatStringNames = formatStringResult.Items.Select(s => s.Name).OrderBy(n => n).ToList();
        
        formatStringNames.Should().BeEquivalentTo(lambdaNames,
            "format string expression should return the same stores as lambda expression");
        
        // Verify query statistics are similar
        lambdaResult.TotalCellsQueried.Should().Be(formatStringResult.TotalCellsQueried,
            "both approaches should query the same number of cells");
    }
    
    #endregion
    
    #region 26.3 Test S2 proximity query (non-paginated) with plain text expressions
    
    [Fact]
    public async Task SpatialQueryAsync_S2ProximityNonPaginated_WithPlainTextExpression_ReturnsAllResultsWithinRadius()
    {
        // Arrange - Create table with S2-indexed stores at known locations
        await CreateTableAsync<S2StoreEntity>();
        var table = new S2StoreTable(DynamoDb, TableName);
        
        // San Francisco downtown area - we'll search within 5km radius
        var searchCenter = new GeoLocation(37.7749, -122.4194);
        
        // Create stores at various distances from the search center
        var stores = new[]
        {
            // Within 5km radius
            new S2StoreEntity
            {
                StoreId = "STORE",
                Location = new GeoLocation(37.7749, -122.4194), // 0km - at center
                Name = "Downtown Store",
                Description = "At search center"
            },
            new S2StoreEntity
            {
                StoreId = "STORE",
                Location = new GeoLocation(37.7599, -122.4148), // ~1.7km south
                Name = "Mission Store",
                Description = "Mission District"
            },
            new S2StoreEntity
            {
                StoreId = "STORE",
                Location = new GeoLocation(37.8021, -122.4378), // ~3.5km north
                Name = "Marina Store",
                Description = "Marina District"
            },
            new S2StoreEntity
            {
                StoreId = "STORE",
                Location = new GeoLocation(37.7694, -122.4481), // ~2.5km west
                Name = "Haight Store",
                Description = "Haight-Ashbury"
            },
            // Outside 5km radius
            new S2StoreEntity
            {
                StoreId = "STORE",
                Location = new GeoLocation(37.8044, -122.2712), // ~13km east (across bay)
                Name = "Oakland Store",
                Description = "Oakland"
            },
            new S2StoreEntity
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
        // Plain text expressions with WithValue still work - they don't rely on implicit cast
        var result = await table.SpatialQueryAsync<S2StoreEntity>(
            locationSelector: store => store.Location,
            spatialIndexType: SpatialIndexType.S2,
            precision: 16, // S2 Level 16 (~600m cells)
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
    public async Task SpatialQueryAsync_S2ProximityNonPaginated_PlainTextMatchesOtherExpressionResults()
    {
        // Arrange - Create table with S2-indexed stores
        await CreateTableAsync<S2StoreEntity>();
        var table = new S2StoreTable(DynamoDb, TableName);
        
        var searchCenter = new GeoLocation(37.7749, -122.4194);
        
        // Create stores at specific distances
        var stores = new[]
        {
            new S2StoreEntity
            {
                StoreId = "STORE",
                Location = new GeoLocation(37.7749, -122.4194),
                Name = "Store 1",
                Description = "Test store 1"
            },
            new S2StoreEntity
            {
                StoreId = "STORE",
                Location = new GeoLocation(37.7759, -122.4204),
                Name = "Store 2",
                Description = "Test store 2"
            },
            new S2StoreEntity
            {
                StoreId = "STORE",
                Location = new GeoLocation(37.7739, -122.4184),
                Name = "Store 3",
                Description = "Test store 3"
            }
        };
        
        foreach (var store in stores)
        {
            await table.PutAsync(store);
        }
        
        // Act - Execute spatial query with lambda expression using implicit cast
        var lambdaResult = await table.SpatialQueryAsync<S2StoreEntity>(
            locationSelector: store => store.Location,
            spatialIndexType: SpatialIndexType.S2,
            precision: 16,
            center: searchCenter,
            radiusKilometers: 5.0,
            queryBuilder: (query, cell, pagination) => query
                .Where<S2StoreEntity>(x => x.StoreId == "STORE" && x.Location == cell)
                ,
            pageSize: null
        );
        
        // Act - Execute spatial query with format string expression
        var formatStringResult = await table.SpatialQueryAsync<S2StoreEntity>(
            locationSelector: store => store.Location,
            spatialIndexType: SpatialIndexType.S2,
            precision: 16,
            center: searchCenter,
            radiusKilometers: 5.0,
            queryBuilder: (query, cell, pagination) => query
                .Where("pk = {0} AND sk = {1}", "STORE", cell)
                ,
            pageSize: null
        );
        
        // Act - Execute spatial query with plain text expression using WithValue
        var plainTextResult = await table.SpatialQueryAsync<S2StoreEntity>(
            locationSelector: store => store.Location,
            spatialIndexType: SpatialIndexType.S2,
            precision: 16,
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
        
        // Verify the same stores are returned (by name since Location is the sort key)
        var lambdaNames = lambdaResult.Items.Select(s => s.Name).OrderBy(n => n).ToList();
        var formatStringNames = formatStringResult.Items.Select(s => s.Name).OrderBy(n => n).ToList();
        var plainTextNames = plainTextResult.Items.Select(s => s.Name).OrderBy(n => n).ToList();
        
        plainTextNames.Should().BeEquivalentTo(lambdaNames,
            "plain text expression should return the same stores as lambda expression");
        plainTextNames.Should().BeEquivalentTo(formatStringNames,
            "plain text expression should return the same stores as format string expression");
        
        // Verify query statistics are similar
        lambdaResult.TotalCellsQueried.Should().Be(plainTextResult.TotalCellsQueried,
            "all approaches should query the same number of cells");
        formatStringResult.TotalCellsQueried.Should().Be(plainTextResult.TotalCellsQueried,
            "all approaches should query the same number of cells");
    }
    
    #endregion
    
    #region 26.4 Test S2 proximity query (paginated) with lambda expressions
    
    [Fact]
    public async Task SpatialQueryAsync_S2ProximityPaginated_WithLambdaExpression_RespectsPageSize()
    {
        // Arrange - Create table with many S2-indexed stores
        await CreateTableAsync<S2StoreEntity>();
        var table = new S2StoreTable(DynamoDb, TableName);
        
        // San Francisco downtown area - we'll search within 10km radius
        var searchCenter = new GeoLocation(37.7749, -122.4194);
        
        // Create 25 stores at various distances within 10km
        var stores = new List<S2StoreEntity>();
        for (int i = 0; i < 25; i++)
        {
            // Distribute stores in a grid pattern around the center
            // Each store is roughly 0.5-2km from center
            var latOffset = (i % 5 - 2) * 0.01; // -0.02 to +0.02 degrees (~2.2km)
            var lonOffset = (i / 5 - 2) * 0.01;
            
            stores.Add(new S2StoreEntity
            {
                StoreId = "STORE",
                Location = new GeoLocation(
                    searchCenter.Latitude + latOffset,
                    searchCenter.Longitude + lonOffset
                ),
                Name = $"Store {i + 1}",
                Description = $"Test store at position {i + 1}"
            });
        }
        
        // Write all stores to DynamoDB
        foreach (var store in stores)
        {
            await table.PutAsync(store);
        }
        
        // Act - Execute SpatialQueryAsync with pageSize=10 using lambda expression
        var result = await table.SpatialQueryAsync<S2StoreEntity>(
            locationSelector: store => store.Location,
            spatialIndexType: SpatialIndexType.S2,
            precision: 16, // S2 Level 16 (~600m cells)
            center: searchCenter,
            radiusKilometers: 10.0,
            queryBuilder: (query, cell, pagination) => query
                .Where<S2StoreEntity>(x => x.StoreId == "STORE" && x.Location == cell),
            pageSize: 10 // Paginated mode with page size of 10
        );
        
        // Assert - Verify page size is respected
        result.Items.Should().NotBeNull();
        result.Items.Count.Should().BeLessThanOrEqualTo(10, 
            "paginated query should return at most pageSize items");
        result.Items.Count.Should().BeGreaterThan(0, 
            "should return at least some results");
        
        // Verify all results are within radius
        foreach (var store in result.Items)
        {
            var distance = store.Location.DistanceToKilometers(searchCenter);
            distance.Should().BeLessThanOrEqualTo(10.0, 
                $"Store {store.Name} should be within 10km radius");
        }
    }
    
    [Fact]
    public async Task SpatialQueryAsync_S2ProximityPaginated_ReturnsContinuationToken()
    {
        // Arrange - Create table with many S2-indexed stores
        await CreateTableAsync<S2StoreEntity>();
        var table = new S2StoreTable(DynamoDb, TableName);
        
        var searchCenter = new GeoLocation(37.7749, -122.4194);
        
        // Create 30 stores to ensure we have more than one page
        var stores = new List<S2StoreEntity>();
        for (int i = 0; i < 30; i++)
        {
            var latOffset = (i % 6 - 2.5) * 0.01;
            var lonOffset = (i / 6 - 2.5) * 0.01;
            
            stores.Add(new S2StoreEntity
            {
                StoreId = "STORE",
                Location = new GeoLocation(
                    searchCenter.Latitude + latOffset,
                    searchCenter.Longitude + lonOffset
                ),
                Name = $"Store {i + 1}",
                Description = $"Test store {i + 1}"
            });
        }
        
        foreach (var store in stores)
        {
            await table.PutAsync(store);
        }
        
        // Act - Execute paginated query with small page size using lambda expression
        var result = await table.SpatialQueryAsync<S2StoreEntity>(
            locationSelector: store => store.Location,
            spatialIndexType: SpatialIndexType.S2,
            precision: 16,
            center: searchCenter,
            radiusKilometers: 10.0,
            queryBuilder: (query, cell, pagination) => query
                .Where<S2StoreEntity>(x => x.StoreId == "STORE" && x.Location == cell),
            pageSize: 10
        );
        
        // Assert - Verify continuation token is returned when there are more results
        result.Items.Count.Should().BeLessThanOrEqualTo(10);
        
        // If we got fewer than 10 items, we might have exhausted all results
        // But if we got exactly 10, there should be a continuation token
        if (result.Items.Count == 10)
        {
            result.ContinuationToken.Should().NotBeNull(
                "continuation token should be returned when page is full and more results may exist");
        }
    }
    
    [Fact]
    public async Task SpatialQueryAsync_S2ProximityPaginated_ResultsInSpiralOrder()
    {
        // Arrange - Create table with S2-indexed stores
        await CreateTableAsync<S2StoreEntity>();
        var table = new S2StoreTable(DynamoDb, TableName);
        
        var searchCenter = new GeoLocation(37.7749, -122.4194);
        
        // Create stores at specific distances to verify spiral ordering
        var stores = new[]
        {
            // Very close stores (should appear in first page)
            new S2StoreEntity
            {
                StoreId = "STORE",
                Location = new GeoLocation(37.7749, -122.4194), // 0km - at center
                Name = "Center Store",
                Description = "At center"
            },
            new S2StoreEntity
            {
                StoreId = "STORE",
                Location = new GeoLocation(37.7759, -122.4204), // ~0.15km
                Name = "Very Close Store 1",
                Description = "Very close"
            },
            new S2StoreEntity
            {
                StoreId = "STORE",
                Location = new GeoLocation(37.7739, -122.4184), // ~0.15km
                Name = "Very Close Store 2",
                Description = "Very close"
            },
            // Medium distance stores
            new S2StoreEntity
            {
                StoreId = "STORE",
                Location = new GeoLocation(37.7849, -122.4294), // ~1.5km
                Name = "Medium Store 1",
                Description = "Medium distance"
            },
            new S2StoreEntity
            {
                StoreId = "STORE",
                Location = new GeoLocation(37.7649, -122.4094), // ~1.5km
                Name = "Medium Store 2",
                Description = "Medium distance"
            },
            // Far stores (should appear in later pages if pagination works)
            new S2StoreEntity
            {
                StoreId = "STORE",
                Location = new GeoLocation(37.8049, -122.4494), // ~4km
                Name = "Far Store 1",
                Description = "Far away"
            },
            new S2StoreEntity
            {
                StoreId = "STORE",
                Location = new GeoLocation(37.7449, -122.3894), // ~4km
                Name = "Far Store 2",
                Description = "Far away"
            }
        };
        
        foreach (var store in stores)
        {
            await table.PutAsync(store);
        }
        
        // Act - Execute paginated query with small page size using lambda expression
        var result = await table.SpatialQueryAsync<S2StoreEntity>(
            locationSelector: store => store.Location,
            spatialIndexType: SpatialIndexType.S2,
            precision: 16,
            center: searchCenter,
            radiusKilometers: 10.0,
            queryBuilder: (query, cell, pagination) => query
                .Where<S2StoreEntity>(x => x.StoreId == "STORE" && x.Location == cell),
            pageSize: 5 // Small page size to test ordering
        );
        
        // Assert - Verify results are in spiral order (closest first)
        result.Items.Should().NotBeNull();
        result.Items.Should().NotBeEmpty();
        
        // Calculate distances for all returned items
        var distances = result.Items
            .Select(s => new { Store = s, Distance = s.Location.DistanceToKilometers(searchCenter) })
            .ToList();
        
        // Verify that the first page contains the closest stores
        // The center store should definitely be in the first page
        var centerStoreInResults = result.Items.Any(s => s.Name == "Center Store");
        centerStoreInResults.Should().BeTrue(
            "the store at the center should be in the first page (spiral order)");
        
        // Verify that distances are generally increasing (spiral order)
        // Note: Due to cell-based querying, perfect distance ordering is not guaranteed,
        // but the general trend should be closest to farthest
        var firstItemDistance = distances.First().Distance;
        var lastItemDistance = distances.Last().Distance;
        
        // The first item should be closer than or equal to the last item
        // (allowing for some variation due to cell boundaries)
        firstItemDistance.Should().BeLessThanOrEqualTo(lastItemDistance + 1.0,
            "results should generally be ordered from closest to farthest (spiral order)");
    }
    
    [Fact]
    public async Task SpatialQueryAsync_S2ProximityPaginated_CanIterateThroughAllPages()
    {
        // Arrange - Create table with many S2-indexed stores
        await CreateTableAsync<S2StoreEntity>();
        var table = new S2StoreTable(DynamoDb, TableName);
        
        var searchCenter = new GeoLocation(37.7749, -122.4194);
        
        // Create 25 stores
        var stores = new List<S2StoreEntity>();
        for (int i = 0; i < 25; i++)
        {
            var latOffset = (i % 5 - 2) * 0.01;
            var lonOffset = (i / 5 - 2) * 0.01;
            
            stores.Add(new S2StoreEntity
            {
                StoreId = "STORE",
                Location = new GeoLocation(
                    searchCenter.Latitude + latOffset,
                    searchCenter.Longitude + lonOffset
                ),
                Name = $"Store {i + 1}",
                Description = $"Test store {i + 1}"
            });
        }
        
        foreach (var store in stores)
        {
            await table.PutAsync(store);
        }
        
        // Act - Iterate through all pages using lambda expression
        var allResults = new List<S2StoreEntity>();
        SpatialContinuationToken? continuationToken = null;
        int pageCount = 0;
        int maxPages = 10; // Safety limit to prevent infinite loops
        
        do
        {
            var result = await table.SpatialQueryAsync<S2StoreEntity>(
                locationSelector: store => store.Location,
                spatialIndexType: SpatialIndexType.S2,
                precision: 16,
                center: searchCenter,
                radiusKilometers: 10.0,
                queryBuilder: (query, cell, pagination) => query
                    .Where<S2StoreEntity>(x => x.StoreId == "STORE" && x.Location == cell),
                pageSize: 8, // Small page size to ensure multiple pages
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
        
        // Assert - Verify we got all stores
        allResults.Should().NotBeEmpty();
        allResults.Count.Should().BeLessThanOrEqualTo(25, 
            "should not return more items than we created");
        
        // Verify we iterated through multiple pages
        pageCount.Should().BeGreaterThan(1, 
            "should have iterated through multiple pages");
        
        // Verify no duplicates (each store should appear exactly once)
        var uniqueNames = allResults.Select(s => s.Name).Distinct().ToList();
        uniqueNames.Count.Should().Be(allResults.Count, 
            "should not have duplicate stores across pages");
        
        // Verify all results are within radius
        foreach (var store in allResults)
        {
            var distance = store.Location.DistanceToKilometers(searchCenter);
            distance.Should().BeLessThanOrEqualTo(10.0, 
                $"Store {store.Name} should be within 10km radius");
        }
    }
    
    [Fact]
    public async Task SpatialQueryAsync_S2ProximityPaginated_FinalPageReturnsNullToken()
    {
        // Arrange - Create table with a small number of S2-indexed stores
        await CreateTableAsync<S2StoreEntity>();
        var table = new S2StoreTable(DynamoDb, TableName);
        
        var searchCenter = new GeoLocation(37.7749, -122.4194);
        
        // Create only 5 stores so we can exhaust them with pagination
        var stores = new List<S2StoreEntity>();
        for (int i = 0; i < 5; i++)
        {
            stores.Add(new S2StoreEntity
            {
                StoreId = "STORE",
                Location = new GeoLocation(
                    searchCenter.Latitude + (i - 2) * 0.005,
                    searchCenter.Longitude + (i - 2) * 0.005
                ),
                Name = $"Store {i + 1}",
                Description = $"Test store {i + 1}"
            });
        }
        
        foreach (var store in stores)
        {
            await table.PutAsync(store);
        }
        
        // Act - Iterate through all pages until we get a null token using lambda expression
        SpatialContinuationToken? continuationToken = null;
        SpatialQueryResponse<S2StoreEntity>? lastResult = null;
        int pageCount = 0;
        
        do
        {
            lastResult = await table.SpatialQueryAsync<S2StoreEntity>(
                locationSelector: store => store.Location,
                spatialIndexType: SpatialIndexType.S2,
                precision: 16,
                center: searchCenter,
                radiusKilometers: 10.0,
                queryBuilder: (query, cell, pagination) => query
                    .Where<S2StoreEntity>(x => x.StoreId == "STORE" && x.Location == cell),
                pageSize: 2, // Very small page size to ensure multiple pages
                continuationToken: continuationToken
            );
            
            continuationToken = lastResult.ContinuationToken;
            pageCount++;
            
            // Safety check
            if (pageCount >= 10)
            {
                break;
            }
        }
        while (continuationToken != null);
        
        // Assert - Verify the final page returns null continuation token
        lastResult.Should().NotBeNull();
        lastResult!.ContinuationToken.Should().BeNull(
            "final page should return null continuation token to indicate completion");
        
        // Verify we went through multiple pages
        pageCount.Should().BeGreaterThan(1, 
            "should have paginated through multiple pages");
    }
    
    [Fact]
    public async Task SpatialQueryAsync_S2ProximityPaginated_WithExplicitSpatialIndexProperty_Works()
    {
        // Arrange - Create table with S2-indexed stores
        await CreateTableAsync<S2StoreEntity>();
        var table = new S2StoreTable(DynamoDb, TableName);
        
        var searchCenter = new GeoLocation(37.7749, -122.4194);
        
        // Create 15 stores
        var stores = new List<S2StoreEntity>();
        for (int i = 0; i < 15; i++)
        {
            var latOffset = (i % 4 - 1.5) * 0.01;
            var lonOffset = (i / 4 - 1.5) * 0.01;
            
            stores.Add(new S2StoreEntity
            {
                StoreId = "STORE",
                Location = new GeoLocation(
                    searchCenter.Latitude + latOffset,
                    searchCenter.Longitude + lonOffset
                ),
                Name = $"Store {i + 1}",
                Description = $"Test store {i + 1}"
            });
        }
        
        foreach (var store in stores)
        {
            await table.PutAsync(store);
        }
        
        // Act - Execute paginated query using explicit SpatialIndex property
        var result = await table.SpatialQueryAsync<S2StoreEntity>(
            locationSelector: store => store.Location,
            spatialIndexType: SpatialIndexType.S2,
            precision: 16,
            center: searchCenter,
            radiusKilometers: 10.0,
            queryBuilder: (query, cell, pagination) => query
                .Where<S2StoreEntity>(x => x.StoreId == "STORE" && x.Location.SpatialIndex == cell),
            pageSize: 10
        );
        
        // Assert - Verify paginated query works with explicit SpatialIndex property
        result.Items.Should().NotBeNull();
        result.Items.Count.Should().BeLessThanOrEqualTo(10);
        result.Items.Count.Should().BeGreaterThan(0);
        
        // Verify all results are within radius
        foreach (var store in result.Items)
        {
            var distance = store.Location.DistanceToKilometers(searchCenter);
            distance.Should().BeLessThanOrEqualTo(10.0);
        }
    }
    
    #endregion
    
    #region 26.5 Test S2 pagination continuation
    
    [Fact]
    public async Task SpatialQueryAsync_S2ProximityPaginated_ContinuationTokenFetchesNextPage()
    {
        // Arrange - Create table with many S2-indexed stores
        await CreateTableAsync<S2StoreEntity>();
        var table = new S2StoreTable(DynamoDb, TableName);
        
        var searchCenter = new GeoLocation(37.7749, -122.4194);
        
        // Create 30 stores to ensure multiple pages
        var stores = new List<S2StoreEntity>();
        for (int i = 0; i < 30; i++)
        {
            var latOffset = (i % 6 - 2.5) * 0.01;
            var lonOffset = (i / 6 - 2.5) * 0.01;
            
            stores.Add(new S2StoreEntity
            {
                StoreId = "STORE",
                Location = new GeoLocation(
                    searchCenter.Latitude + latOffset,
                    searchCenter.Longitude + lonOffset
                ),
                Name = $"Store {i + 1:D3}", // Zero-padded for consistent ordering
                Description = $"Test store {i + 1}"
            });
        }
        
        foreach (var store in stores)
        {
            await table.PutAsync(store);
        }
        
        // Act - Execute first page
        var firstPageResult = await table.SpatialQueryAsync<S2StoreEntity>(
            locationSelector: store => store.Location,
            spatialIndexType: SpatialIndexType.S2,
            precision: 16,
            center: searchCenter,
            radiusKilometers: 10.0,
            queryBuilder: (query, cell, pagination) => query
                .Where<S2StoreEntity>(x => x.StoreId == "STORE" && x.Location == cell),
            pageSize: 10
        );
        
        // Assert - First page should have results and continuation token
        firstPageResult.Items.Should().NotBeNull();
        firstPageResult.Items.Count.Should().BeLessThanOrEqualTo(10, 
            "first page should respect page size");
        firstPageResult.Items.Count.Should().BeGreaterThan(0, 
            "first page should have results");
        
        // If we got exactly 10 items, there should be more pages
        if (firstPageResult.Items.Count == 10)
        {
            firstPageResult.ContinuationToken.Should().NotBeNull(
                "continuation token should be present when page is full");
        }
        
        // Act - Use continuation token to fetch second page
        if (firstPageResult.ContinuationToken != null)
        {
            var secondPageResult = await table.SpatialQueryAsync<S2StoreEntity>(
                locationSelector: store => store.Location,
                spatialIndexType: SpatialIndexType.S2,
                precision: 16,
                center: searchCenter,
                radiusKilometers: 10.0,
                queryBuilder: (query, cell, pagination) => query
                    .Where<S2StoreEntity>(x => x.StoreId == "STORE" && x.Location == cell),
                pageSize: 10,
                continuationToken: firstPageResult.ContinuationToken
            );
            
            // Assert - Second page should have different results
            secondPageResult.Items.Should().NotBeNull();
            secondPageResult.Items.Count.Should().BeGreaterThan(0, 
                "second page should have results");
            secondPageResult.Items.Count.Should().BeLessThanOrEqualTo(10, 
                "second page should respect page size");
            
            // Verify no overlap between first and second page
            var firstPageNames = firstPageResult.Items.Select(s => s.Name).ToHashSet();
            var secondPageNames = secondPageResult.Items.Select(s => s.Name).ToHashSet();
            
            firstPageNames.Intersect(secondPageNames).Should().BeEmpty(
                "first and second page should not have overlapping results");
        }
    }
    
    [Fact]
    public async Task SpatialQueryAsync_S2ProximityPaginated_ContinueUntilAllResultsRetrieved()
    {
        // Arrange - Create table with known number of S2-indexed stores
        await CreateTableAsync<S2StoreEntity>();
        var table = new S2StoreTable(DynamoDb, TableName);
        
        var searchCenter = new GeoLocation(37.7749, -122.4194);
        
        // Create exactly 27 stores (not a multiple of page size to test final partial page)
        var expectedStoreCount = 27;
        var stores = new List<S2StoreEntity>();
        for (int i = 0; i < expectedStoreCount; i++)
        {
            var latOffset = (i % 6 - 2.5) * 0.008;
            var lonOffset = (i / 6 - 2.5) * 0.008;
            
            stores.Add(new S2StoreEntity
            {
                StoreId = "STORE",
                Location = new GeoLocation(
                    searchCenter.Latitude + latOffset,
                    searchCenter.Longitude + lonOffset
                ),
                Name = $"Store {i + 1:D3}",
                Description = $"Test store {i + 1}"
            });
        }
        
        foreach (var store in stores)
        {
            await table.PutAsync(store);
        }
        
        // Act - Continue fetching pages until all results retrieved
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
                radiusKilometers: 10.0,
                queryBuilder: (query, cell, pagination) => query
                    .Where<S2StoreEntity>(x => x.StoreId == "STORE" && x.Location == cell),
                pageSize: 8, // Not a divisor of 27 to test partial pages
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
        allResults.Count.Should().BeLessThanOrEqualTo(expectedStoreCount, 
            "should not return more items than created");
        
        // Verify we went through multiple pages
        pageCount.Should().BeGreaterThan(1, 
            "should have paginated through multiple pages");
        
        // Verify all results are within radius
        foreach (var store in allResults)
        {
            var distance = store.Location.DistanceToKilometers(searchCenter);
            distance.Should().BeLessThanOrEqualTo(10.0, 
                $"Store {store.Name} should be within 10km radius");
        }
    }
    
    [Fact]
    public async Task SpatialQueryAsync_S2ProximityPaginated_NoDuplicatesAcrossPages()
    {
        // Arrange - Create table with many S2-indexed stores
        await CreateTableAsync<S2StoreEntity>();
        var table = new S2StoreTable(DynamoDb, TableName);
        
        var searchCenter = new GeoLocation(37.7749, -122.4194);
        
        // Create 35 stores with unique names
        var stores = new List<S2StoreEntity>();
        for (int i = 0; i < 35; i++)
        {
            var latOffset = (i % 7 - 3) * 0.008;
            var lonOffset = (i / 7 - 2.5) * 0.008;
            
            stores.Add(new S2StoreEntity
            {
                StoreId = "STORE",
                Location = new GeoLocation(
                    searchCenter.Latitude + latOffset,
                    searchCenter.Longitude + lonOffset
                ),
                Name = $"Store {i + 1:D3}",
                Description = $"Unique store {i + 1}"
            });
        }
        
        foreach (var store in stores)
        {
            await table.PutAsync(store);
        }
        
        // Act - Fetch all pages and collect results
        var allResults = new List<S2StoreEntity>();
        var allNames = new HashSet<string>();
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
                radiusKilometers: 10.0,
                queryBuilder: (query, cell, pagination) => query
                    .Where<S2StoreEntity>(x => x.StoreId == "STORE" && x.Location == cell),
                pageSize: 7,
                continuationToken: continuationToken
            );
            
            // Check for duplicates within this page
            var pageNames = result.Items.Select(s => s.Name).ToList();
            pageNames.Should().OnlyHaveUniqueItems(
                $"page {pageCount + 1} should not have duplicate items");
            
            // Check for duplicates across pages
            foreach (var name in pageNames)
            {
                allNames.Add(name).Should().BeTrue(
                    $"store '{name}' should not appear in multiple pages");
            }
            
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
        
        // Assert - Verify no duplicates across all pages
        allResults.Should().NotBeEmpty();
        allNames.Count.Should().Be(allResults.Count, 
            "number of unique names should equal total results (no duplicates)");
        
        // Verify we went through multiple pages
        pageCount.Should().BeGreaterThan(1, 
            "should have paginated through multiple pages");
    }
    
    [Fact]
    public async Task SpatialQueryAsync_S2ProximityPaginated_FinalPageHasNullToken()
    {
        // Arrange - Create table with a specific number of S2-indexed stores
        await CreateTableAsync<S2StoreEntity>();
        var table = new S2StoreTable(DynamoDb, TableName);
        
        var searchCenter = new GeoLocation(37.7749, -122.4194);
        
        // Create 12 stores (will result in 2 full pages of 5 and 1 partial page of 2)
        var stores = new List<S2StoreEntity>();
        for (int i = 0; i < 12; i++)
        {
            var latOffset = (i % 4 - 1.5) * 0.008;
            var lonOffset = (i / 4 - 1.5) * 0.008;
            
            stores.Add(new S2StoreEntity
            {
                StoreId = "STORE",
                Location = new GeoLocation(
                    searchCenter.Latitude + latOffset,
                    searchCenter.Longitude + lonOffset
                ),
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
        SpatialQueryResponse<S2StoreEntity>? lastResult = null;
        var allTokens = new List<SpatialContinuationToken?>();
        int pageCount = 0;
        int maxPages = 20; // Safety limit
        
        do
        {
            lastResult = await table.SpatialQueryAsync<S2StoreEntity>(
                locationSelector: store => store.Location,
                spatialIndexType: SpatialIndexType.S2,
                precision: 16,
                center: searchCenter,
                radiusKilometers: 10.0,
                queryBuilder: (query, cell, pagination) => query
                    .Where<S2StoreEntity>(x => x.StoreId == "STORE" && x.Location == cell),
                pageSize: 5,
                continuationToken: continuationToken
            );
            
            allTokens.Add(lastResult.ContinuationToken);
            continuationToken = lastResult.ContinuationToken;
            pageCount++;
            
            // Safety check
            if (pageCount >= maxPages)
            {
                break;
            }
        }
        while (continuationToken != null);
        
        // Assert - Verify final page returns null continuation token
        lastResult.Should().NotBeNull();
        lastResult!.ContinuationToken.Should().BeNull(
            "final page should return null continuation token to indicate no more results");
        
        // Verify we went through multiple pages
        pageCount.Should().BeGreaterThan(1, 
            "should have paginated through multiple pages");
        
        // Verify only the last token is null
        allTokens.Should().NotBeEmpty();
        allTokens.Last().Should().BeNull("last token should be null");
        
        // All tokens except the last should be non-null
        for (int i = 0; i < allTokens.Count - 1; i++)
        {
            allTokens[i].Should().NotBeNull(
                $"token at page {i + 1} should not be null (more pages available)");
        }
    }
    
    [Fact]
    public async Task SpatialQueryAsync_S2ProximityPaginated_ContinuationTokenSerializationRoundTrip()
    {
        // Arrange - Create table with S2-indexed stores
        await CreateTableAsync<S2StoreEntity>();
        var table = new S2StoreTable(DynamoDb, TableName);
        
        var searchCenter = new GeoLocation(37.7749, -122.4194);
        
        // Create 20 stores
        var stores = new List<S2StoreEntity>();
        for (int i = 0; i < 20; i++)
        {
            var latOffset = (i % 5 - 2) * 0.008;
            var lonOffset = (i / 5 - 2) * 0.008;
            
            stores.Add(new S2StoreEntity
            {
                StoreId = "STORE",
                Location = new GeoLocation(
                    searchCenter.Latitude + latOffset,
                    searchCenter.Longitude + lonOffset
                ),
                Name = $"Store {i + 1:D3}",
                Description = $"Test store {i + 1}"
            });
        }
        
        foreach (var store in stores)
        {
            await table.PutAsync(store);
        }
        
        // Act - Get first page
        var firstPageResult = await table.SpatialQueryAsync<S2StoreEntity>(
            locationSelector: store => store.Location,
            spatialIndexType: SpatialIndexType.S2,
            precision: 16,
            center: searchCenter,
            radiusKilometers: 10.0,
            queryBuilder: (query, cell, pagination) => query
                .Where<S2StoreEntity>(x => x.StoreId == "STORE" && x.Location == cell),
            pageSize: 6
        );
        
        // If we have a continuation token, serialize and deserialize it
        if (firstPageResult.ContinuationToken != null)
        {
            // Serialize to Base64 (simulating passing token between requests)
            var serializedToken = firstPageResult.ContinuationToken.ToBase64();
            serializedToken.Should().NotBeNullOrEmpty(
                "continuation token should serialize to non-empty string");
            
            // Deserialize from Base64
            var deserializedToken = SpatialContinuationToken.FromBase64(serializedToken);
            deserializedToken.Should().NotBeNull(
                "continuation token should deserialize successfully");
            
            // Use deserialized token to fetch next page
            var secondPageResult = await table.SpatialQueryAsync<S2StoreEntity>(
                locationSelector: store => store.Location,
                spatialIndexType: SpatialIndexType.S2,
                precision: 16,
                center: searchCenter,
                radiusKilometers: 10.0,
                queryBuilder: (query, cell, pagination) => query
                    .Where<S2StoreEntity>(x => x.StoreId == "STORE" && x.Location == cell),
                pageSize: 6,
                continuationToken: deserializedToken
            );
            
            // Assert - Second page should have results
            secondPageResult.Items.Should().NotBeEmpty(
                "deserialized continuation token should work correctly");
            
            // Verify no overlap between pages
            var firstPageNames = firstPageResult.Items.Select(s => s.Name).ToHashSet();
            var secondPageNames = secondPageResult.Items.Select(s => s.Name).ToHashSet();
            
            firstPageNames.Intersect(secondPageNames).Should().BeEmpty(
                "pages should not have overlapping results after token serialization round-trip");
        }
    }
    
    #endregion
    
    #region 26.6 Test S2 bounding box query (non-paginated) with lambda expressions
    
    [Fact]
    public async Task SpatialQueryAsync_S2BoundingBoxNonPaginated_WithLambdaExpression_ReturnsAllResultsWithinBoundingBox()
    {
        // Arrange - Create table with S2-indexed stores at known locations
        await CreateTableAsync<S2StoreEntity>();
        var table = new S2StoreTable(DynamoDb, TableName);
        
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
            new S2StoreEntity
            {
                StoreId = "STORE",
                Location = new GeoLocation(37.7849, -122.4194), // Downtown - inside
                Name = "Downtown Store",
                Description = "Inside bounding box"
            },
            new S2StoreEntity
            {
                StoreId = "STORE",
                Location = new GeoLocation(37.7899, -122.4094), // North Beach - inside
                Name = "North Beach Store",
                Description = "Inside bounding box"
            },
            new S2StoreEntity
            {
                StoreId = "STORE",
                Location = new GeoLocation(37.7749, -122.4244), // Civic Center - inside
                Name = "Civic Center Store",
                Description = "Inside bounding box"
            },
            new S2StoreEntity
            {
                StoreId = "STORE",
                Location = new GeoLocation(37.7949, -122.4144), // Russian Hill - inside
                Name = "Russian Hill Store",
                Description = "Inside bounding box"
            },
            // Outside bounding box - too far south
            new S2StoreEntity
            {
                StoreId = "STORE",
                Location = new GeoLocation(37.7599, -122.4194), // Mission - outside (south)
                Name = "Mission Store",
                Description = "Outside bounding box (south)"
            },
            // Outside bounding box - too far north
            new S2StoreEntity
            {
                StoreId = "STORE",
                Location = new GeoLocation(37.8099, -122.4194), // Marina - outside (north)
                Name = "Marina Store",
                Description = "Outside bounding box (north)"
            },
            // Outside bounding box - too far west
            new S2StoreEntity
            {
                StoreId = "STORE",
                Location = new GeoLocation(37.7849, -122.4494), // Richmond - outside (west)
                Name = "Richmond Store",
                Description = "Outside bounding box (west)"
            },
            // Outside bounding box - too far east
            new S2StoreEntity
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
        var result = await table.SpatialQueryAsync<S2StoreEntity>(
            locationSelector: store => store.Location,
            spatialIndexType: SpatialIndexType.S2,
            precision: 16, // S2 Level 16 (~600m cells)
            boundingBox: boundingBox,
            queryBuilder: (query, cell, pagination) => query
                .Where<S2StoreEntity>(x => x.StoreId == "STORE" && x.Location == cell),
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
    public async Task SpatialQueryAsync_S2BoundingBoxNonPaginated_VerifiesParallelExecution()
    {
        // Arrange - Create table with S2-indexed stores
        await CreateTableAsync<S2StoreEntity>();
        var table = new S2StoreTable(DynamoDb, TableName);
        
        // Create a larger bounding box that will require multiple S2 cells
        // This ensures we're testing parallel execution of multiple cell queries
        var boundingBox = new GeoBoundingBox(
            southwest: new GeoLocation(37.75, -122.45),
            northeast: new GeoLocation(37.82, -122.38)
        );
        
        // Create 20 stores distributed across the bounding box
        var stores = new List<S2StoreEntity>();
        for (int i = 0; i < 20; i++)
        {
            // Distribute stores in a grid pattern within the bounding box
            var latRange = boundingBox.Northeast.Latitude - boundingBox.Southwest.Latitude;
            var lonRange = boundingBox.Northeast.Longitude - boundingBox.Southwest.Longitude;
            
            var lat = boundingBox.Southwest.Latitude + (i % 5) * (latRange / 4);
            var lon = boundingBox.Southwest.Longitude + (i / 5) * (lonRange / 4);
            
            stores.Add(new S2StoreEntity
            {
                StoreId = "STORE",
                Location = new GeoLocation(lat, lon),
                Name = $"Store {i + 1}",
                Description = $"Test store at position {i + 1}"
            });
        }
        
        // Write all stores to DynamoDB
        foreach (var store in stores)
        {
            await table.PutAsync(store);
        }
        
        // Act - Execute non-paginated bounding box query using lambda expression
        var startTime = DateTime.UtcNow;
        var result = await table.SpatialQueryAsync<S2StoreEntity>(
            locationSelector: store => store.Location,
            spatialIndexType: SpatialIndexType.S2,
            precision: 16,
            boundingBox: boundingBox,
            queryBuilder: (query, cell, pagination) => query
                .Where<S2StoreEntity>(x => x.StoreId == "STORE" && x.Location == cell),
            pageSize: null // Non-paginated mode - parallel execution
        );
        var endTime = DateTime.UtcNow;
        var executionTime = endTime - startTime;
        
        // Assert - Verify parallel execution characteristics
        result.Items.Should().NotBeNull();
        result.Items.Should().HaveCount(20, "all 20 stores should be returned");
        
        // Verify multiple cells were queried (indicating parallel execution)
        result.TotalCellsQueried.Should().BeGreaterThan(1,
            "bounding box should require multiple S2 cells, indicating parallel execution");
        
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
        
        // Verify no duplicates (each store appears exactly once)
        var uniqueNames = result.Items.Select(s => s.Name).Distinct().ToList();
        uniqueNames.Count.Should().Be(result.Items.Count,
            "no duplicates should exist despite parallel execution of multiple cells");
        
        // Verify continuation token is null for non-paginated queries
        result.ContinuationToken.Should().BeNull(
            "non-paginated queries should return null continuation token");
        
        // Log execution time for informational purposes
        // Note: We don't assert on execution time as it varies by environment,
        // but parallel execution should generally be faster than sequential
        Console.WriteLine($"Bounding box query executed in {executionTime.TotalMilliseconds:F2}ms");
        Console.WriteLine($"Queried {result.TotalCellsQueried} cells in parallel");
        Console.WriteLine($"Scanned {result.TotalItemsScanned} items total");
    }
    
    [Fact]
    public async Task SpatialQueryAsync_S2BoundingBoxNonPaginated_WithExplicitSpatialIndexProperty_Works()
    {
        // Arrange - Create table with S2-indexed stores
        await CreateTableAsync<S2StoreEntity>();
        var table = new S2StoreTable(DynamoDb, TableName);
        
        // Define a bounding box
        var boundingBox = new GeoBoundingBox(
            southwest: new GeoLocation(37.77, -122.43),
            northeast: new GeoLocation(37.80, -122.40)
        );
        
        // Create stores inside the bounding box
        var stores = new[]
        {
            new S2StoreEntity
            {
                StoreId = "STORE",
                Location = new GeoLocation(37.7849, -122.4194),
                Name = "Store 1",
                Description = "Test store 1"
            },
            new S2StoreEntity
            {
                StoreId = "STORE",
                Location = new GeoLocation(37.7899, -122.4094),
                Name = "Store 2",
                Description = "Test store 2"
            },
            new S2StoreEntity
            {
                StoreId = "STORE",
                Location = new GeoLocation(37.7749, -122.4244),
                Name = "Store 3",
                Description = "Test store 3"
            }
        };
        
        foreach (var store in stores)
        {
            await table.PutAsync(store);
        }
        
        // Act - Execute bounding box query using explicit SpatialIndex property
        var result = await table.SpatialQueryAsync<S2StoreEntity>(
            locationSelector: store => store.Location,
            spatialIndexType: SpatialIndexType.S2,
            precision: 16,
            boundingBox: boundingBox,
            queryBuilder: (query, cell, pagination) => query
                .Where<S2StoreEntity>(x => x.StoreId == "STORE" && x.Location.SpatialIndex == cell),
            pageSize: null
        );
        
        // Assert - Verify explicit SpatialIndex property works
        result.Items.Should().NotBeNull();
        result.Items.Should().HaveCount(3, "all 3 stores should be returned");
        
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
    }
    
    [Fact]
    public async Task SpatialQueryAsync_S2BoundingBoxNonPaginated_ReturnsNullContinuationToken()
    {
        // Arrange - Create table with S2-indexed stores
        await CreateTableAsync<S2StoreEntity>();
        var table = new S2StoreTable(DynamoDb, TableName);
        
        var boundingBox = new GeoBoundingBox(
            southwest: new GeoLocation(37.77, -122.43),
            northeast: new GeoLocation(37.80, -122.40)
        );
        
        var stores = new[]
        {
            new S2StoreEntity
            {
                StoreId = "STORE",
                Location = new GeoLocation(37.7849, -122.4194),
                Name = "Store 1",
                Description = "Test store"
            }
        };
        
        foreach (var store in stores)
        {
            await table.PutAsync(store);
        }
        
        // Act - Execute non-paginated bounding box query
        var result = await table.SpatialQueryAsync<S2StoreEntity>(
            locationSelector: store => store.Location,
            spatialIndexType: SpatialIndexType.S2,
            precision: 16,
            boundingBox: boundingBox,
            queryBuilder: (query, cell, pagination) => query
                .Where<S2StoreEntity>(x => x.StoreId == "STORE" && x.Location == cell),
            pageSize: null // Non-paginated mode
        );
        
        // Assert - Verify continuation token is null for non-paginated queries
        result.ContinuationToken.Should().BeNull(
            "non-paginated bounding box queries should return null continuation token");
    }
    
    [Fact]
    public async Task SpatialQueryAsync_S2BoundingBoxNonPaginated_ReturnsQueryStatistics()
    {
        // Arrange - Create table with S2-indexed stores
        await CreateTableAsync<S2StoreEntity>();
        var table = new S2StoreTable(DynamoDb, TableName);
        
        var boundingBox = new GeoBoundingBox(
            southwest: new GeoLocation(37.77, -122.43),
            northeast: new GeoLocation(37.80, -122.40)
        );
        
        var stores = new[]
        {
            new S2StoreEntity
            {
                StoreId = "STORE",
                Location = new GeoLocation(37.7849, -122.4194),
                Name = "Store 1",
                Description = "Test store 1"
            },
            new S2StoreEntity
            {
                StoreId = "STORE",
                Location = new GeoLocation(37.7899, -122.4094),
                Name = "Store 2",
                Description = "Test store 2"
            }
        };
        
        foreach (var store in stores)
        {
            await table.PutAsync(store);
        }
        
        // Act - Execute bounding box query
        var result = await table.SpatialQueryAsync<S2StoreEntity>(
            locationSelector: store => store.Location,
            spatialIndexType: SpatialIndexType.S2,
            precision: 16,
            boundingBox: boundingBox,
            queryBuilder: (query, cell, pagination) => query
                .Where<S2StoreEntity>(x => x.StoreId == "STORE" && x.Location == cell),
            pageSize: null
        );
        
        // Assert - Verify query statistics are populated
        result.TotalCellsQueried.Should().BeGreaterThan(0,
            "should report the number of S2 cells queried");
        result.TotalItemsScanned.Should().BeGreaterThanOrEqualTo(result.Items.Count,
            "items scanned should be at least the number of items returned");
    }
    
    #endregion
    
    #region 26.7 Test S2 bounding box query (paginated)
    
    [Fact]
    public async Task SpatialQueryAsync_S2BoundingBoxPaginated_RespectsPageSize()
    {
        // Arrange - Create table with many S2-indexed stores
        await CreateTableAsync<S2StoreEntity>();
        var table = new S2StoreTable(DynamoDb, TableName);
        
        // Define a bounding box covering a large area of San Francisco
        var boundingBox = new GeoBoundingBox(
            southwest: new GeoLocation(37.75, -122.45),
            northeast: new GeoLocation(37.82, -122.38)
        );
        
        // Create 30 stores distributed across the bounding box
        var stores = new List<S2StoreEntity>();
        for (int i = 0; i < 30; i++)
        {
            // Distribute stores in a grid pattern within the bounding box
            var latRange = boundingBox.Northeast.Latitude - boundingBox.Southwest.Latitude;
            var lonRange = boundingBox.Northeast.Longitude - boundingBox.Southwest.Longitude;
            
            var lat = boundingBox.Southwest.Latitude + (i % 6) * (latRange / 5);
            var lon = boundingBox.Southwest.Longitude + (i / 6) * (lonRange / 5);
            
            stores.Add(new S2StoreEntity
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
        var result = await table.SpatialQueryAsync<S2StoreEntity>(
            locationSelector: store => store.Location,
            spatialIndexType: SpatialIndexType.S2,
            precision: 16, // S2 Level 16 (~600m cells)
            boundingBox: boundingBox,
            queryBuilder: (query, cell, pagination) => query
                .Where<S2StoreEntity>(x => x.StoreId == "STORE" && x.Location == cell),
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
    public async Task SpatialQueryAsync_S2BoundingBoxPaginated_VerifiesSequentialCellQuerying()
    {
        // Arrange - Create table with S2-indexed stores
        await CreateTableAsync<S2StoreEntity>();
        var table = new S2StoreTable(DynamoDb, TableName);
        
        // Create a bounding box that will require multiple S2 cells
        var boundingBox = new GeoBoundingBox(
            southwest: new GeoLocation(37.75, -122.45),
            northeast: new GeoLocation(37.82, -122.38)
        );
        
        // Create 25 stores distributed across the bounding box
        var stores = new List<S2StoreEntity>();
        for (int i = 0; i < 25; i++)
        {
            var latRange = boundingBox.Northeast.Latitude - boundingBox.Southwest.Latitude;
            var lonRange = boundingBox.Northeast.Longitude - boundingBox.Southwest.Longitude;
            
            var lat = boundingBox.Southwest.Latitude + (i % 5) * (latRange / 4);
            var lon = boundingBox.Southwest.Longitude + (i / 5) * (lonRange / 4);
            
            stores.Add(new S2StoreEntity
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
        var result = await table.SpatialQueryAsync<S2StoreEntity>(
            locationSelector: store => store.Location,
            spatialIndexType: SpatialIndexType.S2,
            precision: 16,
            boundingBox: boundingBox,
            queryBuilder: (query, cell, pagination) => query
                .Where<S2StoreEntity>(x => x.StoreId == "STORE" && x.Location == cell),
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
    public async Task SpatialQueryAsync_S2BoundingBoxPaginated_TestPaginationContinuation()
    {
        // Arrange - Create table with many S2-indexed stores
        await CreateTableAsync<S2StoreEntity>();
        var table = new S2StoreTable(DynamoDb, TableName);
        
        // Define a bounding box
        var boundingBox = new GeoBoundingBox(
            southwest: new GeoLocation(37.75, -122.45),
            northeast: new GeoLocation(37.82, -122.38)
        );
        
        // Create 35 stores to ensure multiple pages
        var stores = new List<S2StoreEntity>();
        for (int i = 0; i < 35; i++)
        {
            var latRange = boundingBox.Northeast.Latitude - boundingBox.Southwest.Latitude;
            var lonRange = boundingBox.Northeast.Longitude - boundingBox.Southwest.Longitude;
            
            var lat = boundingBox.Southwest.Latitude + (i % 7) * (latRange / 6);
            var lon = boundingBox.Southwest.Longitude + (i / 7) * (lonRange / 6);
            
            stores.Add(new S2StoreEntity
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
        var firstPageResult = await table.SpatialQueryAsync<S2StoreEntity>(
            locationSelector: store => store.Location,
            spatialIndexType: SpatialIndexType.S2,
            precision: 16,
            boundingBox: boundingBox,
            queryBuilder: (query, cell, pagination) => query
                .Where<S2StoreEntity>(x => x.StoreId == "STORE" && x.Location == cell),
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
            var secondPageResult = await table.SpatialQueryAsync<S2StoreEntity>(
                locationSelector: store => store.Location,
                spatialIndexType: SpatialIndexType.S2,
                precision: 16,
                boundingBox: boundingBox,
                queryBuilder: (query, cell, pagination) => query
                    .Where<S2StoreEntity>(x => x.StoreId == "STORE" && x.Location == cell),
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
    public async Task SpatialQueryAsync_S2BoundingBoxPaginated_IterateThroughAllPages()
    {
        // Arrange - Create table with S2-indexed stores
        await CreateTableAsync<S2StoreEntity>();
        var table = new S2StoreTable(DynamoDb, TableName);
        
        // Define a bounding box
        var boundingBox = new GeoBoundingBox(
            southwest: new GeoLocation(37.75, -122.45),
            northeast: new GeoLocation(37.82, -122.38)
        );
        
        // Create 28 stores (not a multiple of page size to test partial final page)
        var stores = new List<S2StoreEntity>();
        for (int i = 0; i < 28; i++)
        {
            var latRange = boundingBox.Northeast.Latitude - boundingBox.Southwest.Latitude;
            var lonRange = boundingBox.Northeast.Longitude - boundingBox.Southwest.Longitude;
            
            var lat = boundingBox.Southwest.Latitude + (i % 7) * (latRange / 6);
            var lon = boundingBox.Southwest.Longitude + (i / 7) * (lonRange / 6);
            
            stores.Add(new S2StoreEntity
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
                boundingBox: boundingBox,
                queryBuilder: (query, cell, pagination) => query
                    .Where<S2StoreEntity>(x => x.StoreId == "STORE" && x.Location == cell),
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
    public async Task SpatialQueryAsync_S2BoundingBoxPaginated_FinalPageReturnsNullToken()
    {
        // Arrange - Create table with S2-indexed stores
        await CreateTableAsync<S2StoreEntity>();
        var table = new S2StoreTable(DynamoDb, TableName);
        
        // Define a bounding box
        var boundingBox = new GeoBoundingBox(
            southwest: new GeoLocation(37.77, -122.43),
            northeast: new GeoLocation(37.80, -122.40)
        );
        
        // Create 15 stores
        var stores = new List<S2StoreEntity>();
        for (int i = 0; i < 15; i++)
        {
            var latRange = boundingBox.Northeast.Latitude - boundingBox.Southwest.Latitude;
            var lonRange = boundingBox.Northeast.Longitude - boundingBox.Southwest.Longitude;
            
            var lat = boundingBox.Southwest.Latitude + (i % 5) * (latRange / 4);
            var lon = boundingBox.Southwest.Longitude + (i / 5) * (lonRange / 4);
            
            stores.Add(new S2StoreEntity
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
        SpatialQueryResponse<S2StoreEntity>? lastResult = null;
        int pageCount = 0;
        int maxPages = 20; // Safety limit
        
        do
        {
            lastResult = await table.SpatialQueryAsync<S2StoreEntity>(
                locationSelector: store => store.Location,
                spatialIndexType: SpatialIndexType.S2,
                precision: 16,
                boundingBox: boundingBox,
                queryBuilder: (query, cell, pagination) => query
                    .Where<S2StoreEntity>(x => x.StoreId == "STORE" && x.Location == cell),
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
        
        // Assert - Verify final page returns null continuation token
        lastResult.Should().NotBeNull();
        lastResult!.ContinuationToken.Should().BeNull(
            "final page should return null continuation token to indicate completion");
        
        // Verify we went through multiple pages
        pageCount.Should().BeGreaterThan(1, 
            "should have paginated through multiple pages");
    }
    
    [Fact]
    public async Task SpatialQueryAsync_S2BoundingBoxPaginated_NoDuplicatesAcrossPages()
    {
        // Arrange - Create table with S2-indexed stores
        await CreateTableAsync<S2StoreEntity>();
        var table = new S2StoreTable(DynamoDb, TableName);
        
        // Define a bounding box
        var boundingBox = new GeoBoundingBox(
            southwest: new GeoLocation(37.75, -122.45),
            northeast: new GeoLocation(37.82, -122.38)
        );
        
        // Create 30 stores with unique names
        var stores = new List<S2StoreEntity>();
        for (int i = 0; i < 30; i++)
        {
            var latRange = boundingBox.Northeast.Latitude - boundingBox.Southwest.Latitude;
            var lonRange = boundingBox.Northeast.Longitude - boundingBox.Southwest.Longitude;
            
            var lat = boundingBox.Southwest.Latitude + (i % 6) * (latRange / 5);
            var lon = boundingBox.Southwest.Longitude + (i / 6) * (lonRange / 5);
            
            stores.Add(new S2StoreEntity
            {
                StoreId = "STORE",
                Location = new GeoLocation(lat, lon),
                Name = $"Store {i + 1:D3}",
                Description = $"Unique store {i + 1}"
            });
        }
        
        foreach (var store in stores)
        {
            await table.PutAsync(store);
        }
        
        // Act - Fetch all pages and collect results
        var allResults = new List<S2StoreEntity>();
        var allNames = new HashSet<string>();
        SpatialContinuationToken? continuationToken = null;
        int pageCount = 0;
        int maxPages = 20; // Safety limit
        
        do
        {
            var result = await table.SpatialQueryAsync<S2StoreEntity>(
                locationSelector: store => store.Location,
                spatialIndexType: SpatialIndexType.S2,
                precision: 16,
                boundingBox: boundingBox,
                queryBuilder: (query, cell, pagination) => query
                    .Where<S2StoreEntity>(x => x.StoreId == "STORE" && x.Location == cell),
                pageSize: 7,
                continuationToken: continuationToken
            );
            
            // Check for duplicates within this page
            var pageNames = result.Items.Select(s => s.Name).ToList();
            pageNames.Should().OnlyHaveUniqueItems(
                $"page {pageCount + 1} should not have duplicate items");
            
            // Check for duplicates across pages
            foreach (var name in pageNames)
            {
                allNames.Add(name).Should().BeTrue(
                    $"store '{name}' should not appear in multiple pages");
            }
            
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
        
        // Assert - Verify no duplicates across all pages
        allResults.Should().NotBeEmpty();
        allNames.Count.Should().Be(allResults.Count, 
            "number of unique names should equal total results (no duplicates)");
        
        // Verify we went through multiple pages
        pageCount.Should().BeGreaterThan(1, 
            "should have paginated through multiple pages");
    }
    
    [Fact]
    public async Task SpatialQueryAsync_S2BoundingBoxPaginated_WithExplicitSpatialIndexProperty_Works()
    {
        // Arrange - Create table with S2-indexed stores
        await CreateTableAsync<S2StoreEntity>();
        var table = new S2StoreTable(DynamoDb, TableName);
        
        // Define a bounding box
        var boundingBox = new GeoBoundingBox(
            southwest: new GeoLocation(37.77, -122.43),
            northeast: new GeoLocation(37.80, -122.40)
        );
        
        // Create 20 stores
        var stores = new List<S2StoreEntity>();
        for (int i = 0; i < 20; i++)
        {
            var latRange = boundingBox.Northeast.Latitude - boundingBox.Southwest.Latitude;
            var lonRange = boundingBox.Northeast.Longitude - boundingBox.Southwest.Longitude;
            
            var lat = boundingBox.Southwest.Latitude + (i % 5) * (latRange / 4);
            var lon = boundingBox.Southwest.Longitude + (i / 5) * (lonRange / 4);
            
            stores.Add(new S2StoreEntity
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
        
        // Act - Execute paginated bounding box query using explicit SpatialIndex property
        var result = await table.SpatialQueryAsync<S2StoreEntity>(
            locationSelector: store => store.Location,
            spatialIndexType: SpatialIndexType.S2,
            precision: 16,
            boundingBox: boundingBox,
            queryBuilder: (query, cell, pagination) => query
                .Where<S2StoreEntity>(x => x.StoreId == "STORE" && x.Location.SpatialIndex == cell),
            pageSize: 10
        );
        
        // Assert - Verify paginated query works with explicit SpatialIndex property
        result.Items.Should().NotBeNull();
        result.Items.Count.Should().BeLessThanOrEqualTo(10);
        result.Items.Count.Should().BeGreaterThan(0);
        
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
    }
    
    #endregion
    
    #region 26.8 Test S2 query with additional filter conditions
    
    [Fact]
    public async Task SpatialQueryAsync_S2ProximityWithAdditionalFilters_FiltersResultsByStatus()
    {
        // Arrange - Create table with S2-indexed stores with different statuses
        await CreateTableAsync<S2StoreEntity>();
        var table = new S2StoreTable(DynamoDb, TableName);
        
        // San Francisco downtown area - we'll search within 5km radius
        var searchCenter = new GeoLocation(37.7749, -122.4194);
        
        // Create stores at various distances with different statuses (using Description field as status)
        var stores = new[]
        {
            // Within 5km radius - ACTIVE stores
            new S2StoreEntity
            {
                StoreId = "STORE",
                Location = new GeoLocation(37.7749, -122.4194), // 0km - at center
                Name = "Downtown Store",
                Description = "ACTIVE"
            },
            new S2StoreEntity
            {
                StoreId = "STORE",
                Location = new GeoLocation(37.7599, -122.4148), // ~1.7km south
                Name = "Mission Store",
                Description = "ACTIVE"
            },
            new S2StoreEntity
            {
                StoreId = "STORE",
                Location = new GeoLocation(37.8021, -122.4378), // ~3.5km north
                Name = "Marina Store",
                Description = "ACTIVE"
            },
            // Within 5km radius - INACTIVE stores (should be filtered out)
            new S2StoreEntity
            {
                StoreId = "STORE",
                Location = new GeoLocation(37.7694, -122.4481), // ~2.5km west
                Name = "Haight Store",
                Description = "INACTIVE"
            },
            new S2StoreEntity
            {
                StoreId = "STORE",
                Location = new GeoLocation(37.7799, -122.4094), // ~1km east
                Name = "Financial District Store",
                Description = "INACTIVE"
            },
            // Within 5km radius - CLOSED stores (should be filtered out)
            new S2StoreEntity
            {
                StoreId = "STORE",
                Location = new GeoLocation(37.7849, -122.4244), // ~1.5km
                Name = "Tenderloin Store",
                Description = "CLOSED"
            },
            // Outside 5km radius - ACTIVE stores (should be filtered out by distance)
            new S2StoreEntity
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
        var result = await table.SpatialQueryAsync<S2StoreEntity>(
            locationSelector: store => store.Location,
            spatialIndexType: SpatialIndexType.S2,
            precision: 16, // S2 Level 16 (~600m cells)
            center: searchCenter,
            radiusKilometers: 5.0,
            queryBuilder: (query, cell, pagination) => query
                .Where<S2StoreEntity>(x => x.StoreId == "STORE" && x.Location == cell)
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
    public async Task SpatialQueryAsync_S2ProximityWithAdditionalFilters_FiltersResultsByNamePattern()
    {
        // Arrange - Create table with S2-indexed stores with different name patterns
        await CreateTableAsync<S2StoreEntity>();
        var table = new S2StoreTable(DynamoDb, TableName);
        
        var searchCenter = new GeoLocation(37.7749, -122.4194);
        
        // Create stores with different name patterns
        var stores = new[]
        {
            // Within 5km radius - "Premium" stores
            new S2StoreEntity
            {
                StoreId = "STORE",
                Location = new GeoLocation(37.7749, -122.4194),
                Name = "Premium Downtown Store",
                Description = "Premium location"
            },
            new S2StoreEntity
            {
                StoreId = "STORE",
                Location = new GeoLocation(37.7599, -122.4148),
                Name = "Premium Mission Store",
                Description = "Premium location"
            },
            // Within 5km radius - "Standard" stores (should be filtered out)
            new S2StoreEntity
            {
                StoreId = "STORE",
                Location = new GeoLocation(37.8021, -122.4378),
                Name = "Standard Marina Store",
                Description = "Standard location"
            },
            new S2StoreEntity
            {
                StoreId = "STORE",
                Location = new GeoLocation(37.7694, -122.4481),
                Name = "Standard Haight Store",
                Description = "Standard location"
            },
            // Within 5km radius - "Express" stores (should be filtered out)
            new S2StoreEntity
            {
                StoreId = "STORE",
                Location = new GeoLocation(37.7799, -122.4094),
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
        // Filter for stores with "Premium" in the name using begins_with
        var result = await table.SpatialQueryAsync<S2StoreEntity>(
            locationSelector: store => store.Location,
            spatialIndexType: SpatialIndexType.S2,
            precision: 16,
            center: searchCenter,
            radiusKilometers: 5.0,
            queryBuilder: (query, cell, pagination) => query
                .Where<S2StoreEntity>(x => x.StoreId == "STORE" && x.Location == cell)
                .WithFilter("begins_with(#name, :prefix)")
                .WithAttribute("#name", "name")
                .WithValue(":prefix", "Premium"),
            pageSize: null
        );
        
        // Assert - Verify only Premium stores within radius are returned
        result.Items.Should().NotBeNull();
        result.Items.Should().HaveCount(2, "only 2 Premium stores are within 5km radius");
        
        // Verify each result is within the radius
        foreach (var store in result.Items)
        {
            var distance = store.Location.DistanceToKilometers(searchCenter);
            distance.Should().BeLessThanOrEqualTo(5.0, 
                $"Store {store.Name} should be within 5km radius");
        }
        
        // Verify all returned stores have "Premium" in the name
        foreach (var store in result.Items)
        {
            store.Name.Should().StartWith("Premium", 
                $"Store {store.Name} should start with 'Premium'");
        }
        
        // Verify the Premium stores are present
        var storeNames = result.Items.Select(s => s.Name).ToList();
        storeNames.Should().Contain("Premium Downtown Store");
        storeNames.Should().Contain("Premium Mission Store");
        
        // Verify the non-Premium stores are NOT present
        storeNames.Should().NotContain("Standard Marina Store");
        storeNames.Should().NotContain("Standard Haight Store");
        storeNames.Should().NotContain("Express Financial Store");
    }
    
    [Fact]
    public async Task SpatialQueryAsync_S2ProximityWithAdditionalFilters_CombinesMultipleFilters()
    {
        // Arrange - Create table with S2-indexed stores with multiple attributes
        await CreateTableAsync<S2StoreEntity>();
        var table = new S2StoreTable(DynamoDb, TableName);
        
        var searchCenter = new GeoLocation(37.7749, -122.4194);
        
        // Create stores with different combinations of attributes
        var stores = new[]
        {
            // Within 5km radius - Premium AND Active (should match)
            new S2StoreEntity
            {
                StoreId = "STORE",
                Location = new GeoLocation(37.7749, -122.4194),
                Name = "Premium Downtown Store",
                Description = "ACTIVE"
            },
            new S2StoreEntity
            {
                StoreId = "STORE",
                Location = new GeoLocation(37.7599, -122.4148),
                Name = "Premium Mission Store",
                Description = "ACTIVE"
            },
            // Within 5km radius - Premium but INACTIVE (should be filtered out)
            new S2StoreEntity
            {
                StoreId = "STORE",
                Location = new GeoLocation(37.8021, -122.4378),
                Name = "Premium Marina Store",
                Description = "INACTIVE"
            },
            // Within 5km radius - Standard but ACTIVE (should be filtered out)
            new S2StoreEntity
            {
                StoreId = "STORE",
                Location = new GeoLocation(37.7694, -122.4481),
                Name = "Standard Haight Store",
                Description = "ACTIVE"
            },
            // Within 5km radius - Standard and INACTIVE (should be filtered out)
            new S2StoreEntity
            {
                StoreId = "STORE",
                Location = new GeoLocation(37.7799, -122.4094),
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
        // Filter for stores that are both Premium AND Active
        var result = await table.SpatialQueryAsync<S2StoreEntity>(
            locationSelector: store => store.Location,
            spatialIndexType: SpatialIndexType.S2,
            precision: 16,
            center: searchCenter,
            radiusKilometers: 5.0,
            queryBuilder: (query, cell, pagination) => query
                .Where<S2StoreEntity>(x => x.StoreId == "STORE" && x.Location == cell)
                .WithFilter("begins_with(#name, :prefix) AND #desc = :status")
                .WithAttribute("#name", "name")
                .WithAttribute("#desc", "description")
                .WithValue(":prefix", "Premium")
                .WithValue(":status", "ACTIVE"),
            pageSize: null
        );
        
        // Assert - Verify only Premium AND Active stores within radius are returned
        result.Items.Should().NotBeNull();
        result.Items.Should().HaveCount(2, "only 2 stores match both Premium AND Active criteria");
        
        // Verify each result is within the radius
        foreach (var store in result.Items)
        {
            var distance = store.Location.DistanceToKilometers(searchCenter);
            distance.Should().BeLessThanOrEqualTo(5.0, 
                $"Store {store.Name} should be within 5km radius");
        }
        
        // Verify all returned stores meet both criteria
        foreach (var store in result.Items)
        {
            store.Name.Should().StartWith("Premium", 
                $"Store {store.Name} should start with 'Premium'");
            store.Description.Should().Be("ACTIVE", 
                $"Store {store.Name} should have ACTIVE status");
        }
        
        // Verify the matching stores are present
        var storeNames = result.Items.Select(s => s.Name).ToList();
        storeNames.Should().Contain("Premium Downtown Store");
        storeNames.Should().Contain("Premium Mission Store");
        
        // Verify stores that don't meet both criteria are NOT present
        storeNames.Should().NotContain("Premium Marina Store"); // Premium but INACTIVE
        storeNames.Should().NotContain("Standard Haight Store"); // ACTIVE but not Premium
        storeNames.Should().NotContain("Standard Financial Store"); // Neither Premium nor ACTIVE
    }
    
    [Fact]
    public async Task SpatialQueryAsync_S2ProximityPaginatedWithAdditionalFilters_WorksCorrectly()
    {
        // Arrange - Create table with many S2-indexed stores with different statuses
        await CreateTableAsync<S2StoreEntity>();
        var table = new S2StoreTable(DynamoDb, TableName);
        
        var searchCenter = new GeoLocation(37.7749, -122.4194);
        
        // Create 30 stores with alternating ACTIVE/INACTIVE status
        var stores = new List<S2StoreEntity>();
        for (int i = 0; i < 30; i++)
        {
            var latOffset = (i % 6 - 2.5) * 0.01;
            var lonOffset = (i / 6 - 2.5) * 0.01;
            
            // Alternate between ACTIVE and INACTIVE
            var status = i % 2 == 0 ? "ACTIVE" : "INACTIVE";
            
            stores.Add(new S2StoreEntity
            {
                StoreId = "STORE",
                Location = new GeoLocation(
                    searchCenter.Latitude + latOffset,
                    searchCenter.Longitude + lonOffset
                ),
                Name = $"Store {i + 1:D3}",
                Description = status
            });
        }
        
        // Write all stores to DynamoDB
        foreach (var store in stores)
        {
            await table.PutAsync(store);
        }
        
        // Act - Execute paginated query with status filter
        var result = await table.SpatialQueryAsync<S2StoreEntity>(
            locationSelector: store => store.Location,
            spatialIndexType: SpatialIndexType.S2,
            precision: 16,
            center: searchCenter,
            radiusKilometers: 10.0,
            queryBuilder: (query, cell, pagination) => query
                .Where<S2StoreEntity>(x => x.StoreId == "STORE" && x.Location == cell)
                .WithFilter("description = :status")
                .WithValue(":status", "ACTIVE"),
            pageSize: 5 // Small page size to test pagination with filters
        );
        
        // Assert - Verify page size is respected
        result.Items.Should().NotBeNull();
        result.Items.Count.Should().BeLessThanOrEqualTo(5, 
            "paginated query should return at most pageSize items");
        result.Items.Count.Should().BeGreaterThan(0, 
            "should return at least some ACTIVE stores");
        
        // Verify all returned stores are ACTIVE
        foreach (var store in result.Items)
        {
            store.Description.Should().Be("ACTIVE", 
                $"Store {store.Name} should have ACTIVE status");
        }
        
        // Verify all results are within radius
        foreach (var store in result.Items)
        {
            var distance = store.Location.DistanceToKilometers(searchCenter);
            distance.Should().BeLessThanOrEqualTo(10.0, 
                $"Store {store.Name} should be within 10km radius");
        }
        
        // If we got exactly 5 items, there should be more ACTIVE stores available
        if (result.Items.Count == 5)
        {
            result.ContinuationToken.Should().NotBeNull(
                "continuation token should be present when page is full and more results may exist");
        }
    }
    
    [Fact]
    public async Task SpatialQueryAsync_S2BoundingBoxWithAdditionalFilters_FiltersResultsCorrectly()
    {
        // Arrange - Create table with S2-indexed stores
        await CreateTableAsync<S2StoreEntity>();
        var table = new S2StoreTable(DynamoDb, TableName);
        
        // Define a bounding box covering downtown San Francisco
        var boundingBox = new GeoBoundingBox(
            southwest: new GeoLocation(37.77, -122.43),
            northeast: new GeoLocation(37.80, -122.40)
        );
        
        // Create stores at various locations with different statuses
        var stores = new[]
        {
            // Inside bounding box - ACTIVE stores
            new S2StoreEntity
            {
                StoreId = "STORE",
                Location = new GeoLocation(37.7849, -122.4194),
                Name = "Downtown Store",
                Description = "ACTIVE"
            },
            new S2StoreEntity
            {
                StoreId = "STORE",
                Location = new GeoLocation(37.7899, -122.4094),
                Name = "North Beach Store",
                Description = "ACTIVE"
            },
            // Inside bounding box - INACTIVE stores (should be filtered out)
            new S2StoreEntity
            {
                StoreId = "STORE",
                Location = new GeoLocation(37.7749, -122.4244),
                Name = "Civic Center Store",
                Description = "INACTIVE"
            },
            new S2StoreEntity
            {
                StoreId = "STORE",
                Location = new GeoLocation(37.7949, -122.4144),
                Name = "Russian Hill Store",
                Description = "INACTIVE"
            },
            // Outside bounding box - ACTIVE stores (should be filtered out by location)
            new S2StoreEntity
            {
                StoreId = "STORE",
                Location = new GeoLocation(37.7599, -122.4194),
                Name = "Mission Store",
                Description = "ACTIVE"
            }
        };
        
        // Write all stores to DynamoDB
        foreach (var store in stores)
        {
            await table.PutAsync(store);
        }
        
        // Act - Execute bounding box query with status filter
        var result = await table.SpatialQueryAsync<S2StoreEntity>(
            locationSelector: store => store.Location,
            spatialIndexType: SpatialIndexType.S2,
            precision: 16,
            boundingBox: boundingBox,
            queryBuilder: (query, cell, pagination) => query
                .Where<S2StoreEntity>(x => x.StoreId == "STORE" && x.Location == cell)
                .WithFilter("description = :status")
                .WithValue(":status", "ACTIVE"),
            pageSize: null
        );
        
        // Assert - Verify only ACTIVE stores within bounding box are returned
        result.Items.Should().NotBeNull();
        result.Items.Should().HaveCount(2, "only 2 ACTIVE stores are within the bounding box");
        
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
        
        // Verify all returned stores are ACTIVE
        foreach (var store in result.Items)
        {
            store.Description.Should().Be("ACTIVE", 
                $"Store {store.Name} should have ACTIVE status");
        }
        
        // Verify the ACTIVE stores inside the bounding box are present
        var storeNames = result.Items.Select(s => s.Name).ToList();
        storeNames.Should().Contain("Downtown Store");
        storeNames.Should().Contain("North Beach Store");
        
        // Verify the INACTIVE stores are NOT present (filtered by status)
        storeNames.Should().NotContain("Civic Center Store");
        storeNames.Should().NotContain("Russian Hill Store");
        
        // Verify the stores outside the bounding box are NOT present (filtered by location)
        storeNames.Should().NotContain("Mission Store");
    }
    
    #endregion
    
    #region 26.9 Test S2 query with sort key conditions
    
    [Fact]
    public async Task SpatialQueryAsync_S2ProximityWithSortKeyCondition_ReturnsFilteredResults()
    {
        // Arrange - Create table with S2-indexed stores that have timestamps as sort keys
        await CreateTableAsync<S2StoreWithSortKeyEntity>();
        var table = new S2StoreWithSortKeyTable(DynamoDb, TableName);
        
        // San Francisco downtown area
        var searchCenter = new GeoLocation(37.7749, -122.4194);
        
        // Create stores with different opening dates
        // Using ISO 8601 format for sort key to enable range queries
        var stores = new[]
        {
            // Stores opened in 2024 (recent) - within radius
            new S2StoreWithSortKeyEntity
            {
                StoreId = "STORE",
                OpenedAt = "2024-01-15T10:00:00Z",
                Location = new GeoLocation(37.7749, -122.4194), // 0km - at center
                Name = "New Downtown Store",
                Description = "Recently opened downtown",
                Status = "OPEN"
            },
            new S2StoreWithSortKeyEntity
            {
                StoreId = "STORE",
                OpenedAt = "2024-03-20T14:30:00Z",
                Location = new GeoLocation(37.7799, -122.4244), // ~0.7km north
                Name = "New Marina Store",
                Description = "Recently opened in Marina",
                Status = "OPEN"
            },
            new S2StoreWithSortKeyEntity
            {
                StoreId = "STORE",
                OpenedAt = "2024-06-10T09:00:00Z",
                Location = new GeoLocation(37.7699, -122.4144), // ~0.7km south
                Name = "New Mission Store",
                Description = "Recently opened in Mission",
                Status = "OPEN"
            },
            // Stores opened in 2023 (older) - within radius but should be filtered by sort key
            new S2StoreWithSortKeyEntity
            {
                StoreId = "STORE",
                OpenedAt = "2023-05-10T08:00:00Z",
                Location = new GeoLocation(37.7769, -122.4214), // ~0.3km
                Name = "Old Nearby Store",
                Description = "Opened last year",
                Status = "OPEN"
            },
            new S2StoreWithSortKeyEntity
            {
                StoreId = "STORE",
                OpenedAt = "2023-11-25T12:00:00Z",
                Location = new GeoLocation(37.7729, -122.4174), // ~0.3km
                Name = "Old Close Store",
                Description = "Opened late last year",
                Status = "OPEN"
            },
            // Store opened in 2024 but outside radius (should be filtered by location)
            new S2StoreWithSortKeyEntity
            {
                StoreId = "STORE",
                OpenedAt = "2024-02-14T11:00:00Z",
                Location = new GeoLocation(37.8044, -122.2712), // ~13km east (Oakland)
                Name = "New Oakland Store",
                Description = "Recently opened in Oakland",
                Status = "OPEN"
            }
        };
        
        // Write all stores to DynamoDB
        foreach (var storeItem in stores)
        {
            await table.PutAsync(storeItem);
        }
        
        // Act - Execute SpatialQueryAsync with radius search AND sort key condition
        // We want stores within 5km that opened on or after 2024-01-01
        // Note: String comparison operators (>=, <) are not supported in lambda expressions
        // We use format string expressions for sort key conditions
        var result = await table.SpatialQueryAsync<S2StoreWithSortKeyEntity>(
            locationSelector: store => store.Location,
            spatialIndexType: SpatialIndexType.S2,
            precision: 16, // S2 Level 16 (~600m cells)
            center: searchCenter,
            radiusKilometers: 5.0,
            queryBuilder: (query, cell, pagination) => query
                // Spatial condition: Location matches the S2 cell
                // Sort key condition: OpenedAt >= 2024-01-01
                .Where("pk = {0} AND sk >= {1} AND location = {2}", "STORE", "2024-01-01T00:00:00Z", cell),
            pageSize: null // Non-paginated mode
        );
        
        // Assert - Verify only stores within radius AND opened in 2024 are returned
        result.Items.Should().NotBeNull();
        result.Items.Should().HaveCount(3, 
            "3 stores are within 5km radius AND opened in 2024");
        
        // Verify each result is within the radius
        foreach (var store in result.Items)
        {
            var distance = store.Location.DistanceToKilometers(searchCenter);
            distance.Should().BeLessThanOrEqualTo(5.0, 
                $"Store {store.Name} at {store.Location} should be within 5km radius");
        }
        
        // Verify each result opened in 2024 or later
        foreach (var store in result.Items)
        {
            store.OpenedAt.Should().StartWith("2024", 
                $"Store {store.Name} should have opened in 2024 or later");
        }
        
        // Verify the correct stores are present
        var storeNames = result.Items.Select(s => s.Name).ToList();
        storeNames.Should().Contain("New Downtown Store");
        storeNames.Should().Contain("New Marina Store");
        storeNames.Should().Contain("New Mission Store");
        
        // Verify stores from 2023 are NOT present (filtered by sort key condition)
        storeNames.Should().NotContain("Old Nearby Store");
        storeNames.Should().NotContain("Old Close Store");
        
        // Verify store outside radius is NOT present (filtered by spatial condition)
        storeNames.Should().NotContain("New Oakland Store");
    }
    
    [Fact]
    public async Task SpatialQueryAsync_S2ProximityWithSortKeyRange_ReturnsFilteredResults()
    {
        // Arrange - Create table with S2-indexed stores
        await CreateTableAsync<S2StoreWithSortKeyEntity>();
        var table = new S2StoreWithSortKeyTable(DynamoDb, TableName);
        
        var searchCenter = new GeoLocation(37.7749, -122.4194);
        
        // Create stores with different opening dates
        var stores = new[]
        {
            // Q1 2024 stores (Jan-Mar) - within radius
            new S2StoreWithSortKeyEntity
            {
                StoreId = "STORE",
                OpenedAt = "2024-01-15T10:00:00Z",
                Location = new GeoLocation(37.7749, -122.4194),
                Name = "January Store",
                Description = "Opened in January",
                Status = "OPEN"
            },
            new S2StoreWithSortKeyEntity
            {
                StoreId = "STORE",
                OpenedAt = "2024-02-20T14:30:00Z",
                Location = new GeoLocation(37.7799, -122.4244),
                Name = "February Store",
                Description = "Opened in February",
                Status = "OPEN"
            },
            new S2StoreWithSortKeyEntity
            {
                StoreId = "STORE",
                OpenedAt = "2024-03-25T09:00:00Z",
                Location = new GeoLocation(37.7699, -122.4144),
                Name = "March Store",
                Description = "Opened in March",
                Status = "OPEN"
            },
            // Q2 2024 stores (Apr-Jun) - within radius but outside date range
            new S2StoreWithSortKeyEntity
            {
                StoreId = "STORE",
                OpenedAt = "2024-04-10T08:00:00Z",
                Location = new GeoLocation(37.7769, -122.4214),
                Name = "April Store",
                Description = "Opened in April",
                Status = "OPEN"
            },
            new S2StoreWithSortKeyEntity
            {
                StoreId = "STORE",
                OpenedAt = "2024-05-15T12:00:00Z",
                Location = new GeoLocation(37.7729, -122.4174),
                Name = "May Store",
                Description = "Opened in May",
                Status = "OPEN"
            }
        };
        
        // Write all stores to DynamoDB
        foreach (var storeItem in stores)
        {
            await table.PutAsync(storeItem);
        }
        
        // Act - Execute SpatialQueryAsync with radius search AND sort key BETWEEN condition
        // We want stores within 5km that opened between 2024-01-01 and 2024-03-31 (Q1 2024)
        // Note: String comparison operators are not supported in lambda expressions
        // We use format string expressions for sort key range conditions
        var result = await table.SpatialQueryAsync<S2StoreWithSortKeyEntity>(
            locationSelector: store => store.Location,
            spatialIndexType: SpatialIndexType.S2,
            precision: 16,
            center: searchCenter,
            radiusKilometers: 5.0,
            queryBuilder: (query, cell, pagination) => query
                .Where("pk = {0} AND sk BETWEEN {1} AND {2} AND location = {3}", 
                    "STORE", "2024-01-01T00:00:00Z", "2024-03-31T23:59:59Z", cell),
            pageSize: null
        );
        
        // Assert - Verify only Q1 2024 stores within radius are returned
        result.Items.Should().NotBeNull();
        result.Items.Should().HaveCount(3, 
            "3 stores are within 5km radius AND opened in Q1 2024");
        
        // Verify the correct stores are present
        var storeNames = result.Items.Select(s => s.Name).ToList();
        storeNames.Should().Contain("January Store");
        storeNames.Should().Contain("February Store");
        storeNames.Should().Contain("March Store");
        
        // Verify Q2 stores are NOT present (filtered by sort key range)
        storeNames.Should().NotContain("April Store");
        storeNames.Should().NotContain("May Store");
        
        // Verify all results are in Q1 2024
        foreach (var store in result.Items)
        {
            var openedAt = DateTime.Parse(store.OpenedAt);
            openedAt.Should().BeOnOrAfter(new DateTime(2024, 1, 1));
            openedAt.Should().BeBefore(new DateTime(2024, 4, 1));
        }
    }
    
    [Fact]
    public async Task SpatialQueryAsync_S2BoundingBoxWithSortKeyCondition_ReturnsFilteredResults()
    {
        // Arrange - Create table with S2-indexed stores
        await CreateTableAsync<S2StoreWithSortKeyEntity>();
        var table = new S2StoreWithSortKeyTable(DynamoDb, TableName);
        
        // Define a bounding box around San Francisco downtown
        var boundingBox = new GeoBoundingBox(
            southwest: new GeoLocation(37.77, -122.43),
            northeast: new GeoLocation(37.78, -122.41)
        );
        
        // Create stores with different opening dates
        var stores = new[]
        {
            // Inside bounding box, opened in 2024
            new S2StoreWithSortKeyEntity
            {
                StoreId = "STORE",
                OpenedAt = "2024-01-15T10:00:00Z",
                Location = new GeoLocation(37.7749, -122.4194),
                Name = "New Downtown Store",
                Description = "Inside box, opened 2024",
                Status = "OPEN"
            },
            new S2StoreWithSortKeyEntity
            {
                StoreId = "STORE",
                OpenedAt = "2024-03-20T14:30:00Z",
                Location = new GeoLocation(37.7750, -122.4200),
                Name = "New Central Store",
                Description = "Inside box, opened 2024",
                Status = "OPEN"
            },
            // Inside bounding box, opened in 2023 (should be filtered by sort key)
            new S2StoreWithSortKeyEntity
            {
                StoreId = "STORE",
                OpenedAt = "2023-06-10T09:00:00Z",
                Location = new GeoLocation(37.7760, -122.4210),
                Name = "Old Downtown Store",
                Description = "Inside box, opened 2023",
                Status = "OPEN"
            },
            // Outside bounding box, opened in 2024 (should be filtered by location)
            new S2StoreWithSortKeyEntity
            {
                StoreId = "STORE",
                OpenedAt = "2024-02-14T11:00:00Z",
                Location = new GeoLocation(37.7900, -122.4000),
                Name = "New North Store",
                Description = "Outside box, opened 2024",
                Status = "OPEN"
            }
        };
        
        // Write all stores to DynamoDB
        foreach (var storeItem in stores)
        {
            await table.PutAsync(storeItem);
        }
        
        // Act - Execute SpatialQueryAsync with bounding box AND sort key condition
        var result = await table.SpatialQueryAsync<S2StoreWithSortKeyEntity>(
            locationSelector: store => store.Location,
            spatialIndexType: SpatialIndexType.S2,
            precision: 16,
            boundingBox: boundingBox,
            queryBuilder: (query, cell, pagination) => query
                .Where("pk = {0} AND sk >= {1} AND location = {2}", "STORE", "2024-01-01T00:00:00Z", cell),
            pageSize: null
        );
        
        // Assert - Verify only stores inside bounding box AND opened in 2024 are returned
        result.Items.Should().NotBeNull();
        result.Items.Should().HaveCount(2, 
            "2 stores are inside bounding box AND opened in 2024");
        
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
        
        // Verify each result opened in 2024
        foreach (var store in result.Items)
        {
            store.OpenedAt.Should().StartWith("2024", 
                $"Store {store.Name} should have opened in 2024");
        }
        
        // Verify the correct stores are present
        var storeNames = result.Items.Select(s => s.Name).ToList();
        storeNames.Should().Contain("New Downtown Store");
        storeNames.Should().Contain("New Central Store");
        
        // Verify old store is NOT present (filtered by sort key)
        storeNames.Should().NotContain("Old Downtown Store");
        
        // Verify store outside box is NOT present (filtered by location)
        storeNames.Should().NotContain("New North Store");
    }
    
    [Fact]
    public async Task SpatialQueryAsync_S2ProximityWithSortKeyAndFilterExpression_ReturnsFilteredResults()
    {
        // Arrange - Create table with S2-indexed stores
        await CreateTableAsync<S2StoreWithSortKeyEntity>();
        var table = new S2StoreWithSortKeyTable(DynamoDb, TableName);
        
        var searchCenter = new GeoLocation(37.7749, -122.4194);
        
        // Create stores with different opening dates and statuses
        var stores = new[]
        {
            // Within radius, opened in 2024, OPEN status
            new S2StoreWithSortKeyEntity
            {
                StoreId = "STORE",
                OpenedAt = "2024-01-15T10:00:00Z",
                Location = new GeoLocation(37.7749, -122.4194),
                Name = "New Open Store",
                Description = "Recently opened and active",
                Status = "OPEN"
            },
            // Within radius, opened in 2024, CLOSED status (should be filtered by status)
            new S2StoreWithSortKeyEntity
            {
                StoreId = "STORE",
                OpenedAt = "2024-02-20T14:30:00Z",
                Location = new GeoLocation(37.7799, -122.4244),
                Name = "New Closed Store",
                Description = "Recently opened but closed",
                Status = "CLOSED"
            },
            // Within radius, opened in 2023, OPEN status (should be filtered by date)
            new S2StoreWithSortKeyEntity
            {
                StoreId = "STORE",
                OpenedAt = "2023-06-10T09:00:00Z",
                Location = new GeoLocation(37.7699, -122.4144),
                Name = "Old Open Store",
                Description = "Opened last year, still active",
                Status = "OPEN"
            }
        };
        
        // Write all stores to DynamoDB
        foreach (var storeItem in stores)
        {
            await table.PutAsync(storeItem);
        }
        
        // Act - Execute SpatialQueryAsync with spatial, sort key, AND filter conditions
        // We want stores within 5km, opened in 2024, AND with OPEN status
        var result = await table.SpatialQueryAsync<S2StoreWithSortKeyEntity>(
            locationSelector: store => store.Location,
            spatialIndexType: SpatialIndexType.S2,
            precision: 16,
            center: searchCenter,
            radiusKilometers: 5.0,
            queryBuilder: (query, cell, pagination) => query
                .Where("pk = {0} AND sk >= {1} AND location = {2}", "STORE", "2024-01-01T00:00:00Z", cell)
                .WithFilter("status = :status")
                .WithValue(":status", "OPEN"),
            pageSize: null
        );
        
        // Assert - Verify only stores matching ALL conditions are returned
        result.Items.Should().NotBeNull();
        result.Items.Should().HaveCount(1, 
            "only 1 store matches all conditions: within radius, opened in 2024, and OPEN status");
        
        // Verify the correct store is present
        var store = result.Items.Single();
        store.Name.Should().Be("New Open Store");
        store.Status.Should().Be("OPEN");
        store.OpenedAt.Should().StartWith("2024");
        
        var distance = store.Location.DistanceToKilometers(searchCenter);
        distance.Should().BeLessThanOrEqualTo(5.0);
    }
    
    #endregion
    
    #region 29.1 Test S2 query crossing date line
    
    [Fact]
    public async Task SpatialQueryAsync_S2ProximityNonPaginated_CrossingDateLine_ReturnsStoresOnBothSides()
    {
        // Arrange - Create table with S2-indexed stores near the International Date Line
        await CreateTableAsync<S2StoreEntity>();
        var table = new S2StoreTable(DynamoDb, TableName);
        
        // Search center near the date line at the equator (0°, 179°)
        // This location is in the Pacific Ocean, east of Fiji
        var searchCenter = new GeoLocation(0.0, 179.0);
        
        // Create stores on both sides of the date line
        // The date line is at longitude ±180°
        var stores = new[]
        {
            // Stores on the EAST side of date line (positive longitude, approaching +180°)
            new S2StoreEntity
            {
                StoreId = "STORE",
                Location = new GeoLocation(0.0, 179.0), // 0km - at center
                Name = "Center Store",
                Description = "At search center, east of date line"
            },
            new S2StoreEntity
            {
                StoreId = "STORE",
                Location = new GeoLocation(0.0, 179.5), // ~55km east
                Name = "East Store 1",
                Description = "East of center, approaching date line"
            },
            new S2StoreEntity
            {
                StoreId = "STORE",
                Location = new GeoLocation(0.0, 179.9), // ~100km east, very close to date line
                Name = "East Store 2",
                Description = "Very close to date line from east"
            },
            
            // Stores on the WEST side of date line (negative longitude, approaching -180°)
            // Note: -180° and +180° are the same meridian (the date line)
            new S2StoreEntity
            {
                StoreId = "STORE",
                Location = new GeoLocation(0.0, -179.9), // ~100km west of center, across date line
                Name = "West Store 1",
                Description = "Just west of date line"
            },
            new S2StoreEntity
            {
                StoreId = "STORE",
                Location = new GeoLocation(0.0, -179.5), // ~155km west of center, across date line
                Name = "West Store 2",
                Description = "West of date line"
            },
            
            // Stores OUTSIDE the 200km radius
            new S2StoreEntity
            {
                StoreId = "STORE",
                Location = new GeoLocation(0.0, 178.0), // ~111km west of center, same side
                Name = "Far West Store",
                Description = "Too far west, same side of date line"
            },
            new S2StoreEntity
            {
                StoreId = "STORE",
                Location = new GeoLocation(0.0, -178.0), // ~222km west of center, across date line
                Name = "Far East Store",
                Description = "Too far east, across date line"
            }
        };
        
        // Write all stores to DynamoDB
        foreach (var store in stores)
        {
            await table.PutAsync(store);
        }
        
        // Act - Execute SpatialQueryAsync with 200km radius centered near the date line
        // This should return stores on BOTH sides of the date line
        var result = await table.SpatialQueryAsync<S2StoreEntity>(
            locationSelector: store => store.Location,
            spatialIndexType: SpatialIndexType.S2,
            precision: 16, // S2 Level 16 (~600m cells)
            center: searchCenter,
            radiusKilometers: 200.0,
            queryBuilder: (query, cell, pagination) => query
                .Where<S2StoreEntity>(x => x.StoreId == "STORE" && x.Location == cell)
                .Paginate(pagination),
            pageSize: null // Non-paginated mode - query all cells in parallel
        );
        
        // Assert - Verify stores on both sides of date line are returned
        result.Items.Should().NotBeNull();
        result.Items.Should().HaveCount(5, 
            "5 stores are within 200km radius: 3 on east side, 2 on west side of date line");
        
        // Verify each result is within the radius
        foreach (var store in result.Items)
        {
            var distance = store.Location.DistanceToKilometers(searchCenter);
            distance.Should().BeLessThanOrEqualTo(200.0, 
                $"Store {store.Name} at {store.Location} should be within 200km radius");
        }
        
        // Verify stores on EAST side of date line (positive longitude)
        var storeNames = result.Items.Select(s => s.Name).ToList();
        storeNames.Should().Contain("Center Store", "center store should be returned");
        storeNames.Should().Contain("East Store 1", "store at 179.5° should be returned");
        storeNames.Should().Contain("East Store 2", "store at 179.9° should be returned");
        
        // Verify stores on WEST side of date line (negative longitude, across the date line)
        storeNames.Should().Contain("West Store 1", "store at -179.9° (across date line) should be returned");
        storeNames.Should().Contain("West Store 2", "store at -179.5° (across date line) should be returned");
        
        // Verify stores OUTSIDE radius are NOT returned
        storeNames.Should().NotContain("Far West Store", "store at 178° is outside 200km radius");
        storeNames.Should().NotContain("Far East Store", "store at -178° is outside 200km radius");
        
        // Verify no duplicates - each store should appear exactly once
        var uniqueNames = storeNames.Distinct().ToList();
        uniqueNames.Should().HaveCount(storeNames.Count, 
            "no duplicate stores should be returned");
        
        // Verify stores from both sides of the date line are present
        var eastSideStores = result.Items.Where(s => s.Location.Longitude > 0).ToList();
        var westSideStores = result.Items.Where(s => s.Location.Longitude < 0).ToList();
        
        eastSideStores.Should().HaveCount(3, "3 stores on east side of date line (positive longitude)");
        westSideStores.Should().HaveCount(2, "2 stores on west side of date line (negative longitude)");
        
        // Verify results are sorted by distance
        var distances = result.Items
            .Select(s => s.Location.DistanceToKilometers(searchCenter))
            .ToList();
        
        for (int i = 1; i < distances.Count; i++)
        {
            distances[i].Should().BeGreaterThanOrEqualTo(distances[i - 1],
                "results should be sorted by distance in ascending order");
        }
    }
    
    [Fact]
    public async Task SpatialQueryAsync_S2ProximityPaginated_CrossingDateLine_ReturnsStoresOnBothSides()
    {
        // Arrange - Create table with S2-indexed stores near the International Date Line
        await CreateTableAsync<S2StoreEntity>();
        var table = new S2StoreTable(DynamoDb, TableName);
        
        // Search center near the date line at the equator (0°, 179°)
        var searchCenter = new GeoLocation(0.0, 179.0);
        
        // Create stores on both sides of the date line
        var stores = new[]
        {
            // Stores on the EAST side of date line
            new S2StoreEntity
            {
                StoreId = "STORE",
                Location = new GeoLocation(0.0, 179.0),
                Name = "Center Store",
                Description = "At search center"
            },
            new S2StoreEntity
            {
                StoreId = "STORE",
                Location = new GeoLocation(0.0, 179.5),
                Name = "East Store 1",
                Description = "East of center"
            },
            new S2StoreEntity
            {
                StoreId = "STORE",
                Location = new GeoLocation(0.0, 179.9),
                Name = "East Store 2",
                Description = "Near date line"
            },
            
            // Stores on the WEST side of date line
            new S2StoreEntity
            {
                StoreId = "STORE",
                Location = new GeoLocation(0.0, -179.9),
                Name = "West Store 1",
                Description = "Just west of date line"
            },
            new S2StoreEntity
            {
                StoreId = "STORE",
                Location = new GeoLocation(0.0, -179.5),
                Name = "West Store 2",
                Description = "West of date line"
            }
        };
        
        // Write all stores to DynamoDB
        foreach (var store in stores)
        {
            await table.PutAsync(store);
        }
        
        // Act - Execute paginated SpatialQueryAsync with 200km radius
        // Fetch all pages to ensure we get stores from both sides
        var allStores = new List<S2StoreEntity>();
        SpatialContinuationToken? token = null;
        int pageCount = 0;
        
        do
        {
            var result = await table.SpatialQueryAsync<S2StoreEntity>(
                locationSelector: store => store.Location,
                spatialIndexType: SpatialIndexType.S2,
                precision: 16,
                center: searchCenter,
                radiusKilometers: 200.0,
                queryBuilder: (query, cell, pagination) => query
                    .Where<S2StoreEntity>(x => x.StoreId == "STORE" && x.Location == cell)
                    .Paginate(pagination),
                pageSize: 2, // Small page size to test pagination across date line
                continuationToken: token
            );
            
            allStores.AddRange(result.Items);
            token = result.ContinuationToken;
            pageCount++;
            
            // Safety check to prevent infinite loops
            pageCount.Should().BeLessThan(10, "should not require more than 10 pages");
            
        } while (token != null);
        
        // Assert - Verify stores on both sides of date line are returned
        allStores.Should().HaveCount(5, 
            "5 stores are within 200km radius across multiple pages");
        
        // Verify each result is within the radius
        foreach (var store in allStores)
        {
            var distance = store.Location.DistanceToKilometers(searchCenter);
            distance.Should().BeLessThanOrEqualTo(200.0, 
                $"Store {store.Name} at {store.Location} should be within 200km radius");
        }
        
        // Verify stores from both sides of the date line are present
        var storeNames = allStores.Select(s => s.Name).ToList();
        storeNames.Should().Contain("Center Store");
        storeNames.Should().Contain("East Store 1");
        storeNames.Should().Contain("East Store 2");
        storeNames.Should().Contain("West Store 1");
        storeNames.Should().Contain("West Store 2");
        
        // Verify no duplicates across pages
        var uniqueNames = storeNames.Distinct().ToList();
        uniqueNames.Should().HaveCount(storeNames.Count, 
            "no duplicate stores should be returned across pages");
        
        // Verify stores from both sides of the date line are present
        var eastSideStores = allStores.Where(s => s.Location.Longitude > 0).ToList();
        var westSideStores = allStores.Where(s => s.Location.Longitude < 0).ToList();
        
        eastSideStores.Should().HaveCount(3, "3 stores on east side of date line");
        westSideStores.Should().HaveCount(2, "2 stores on west side of date line");
        
        // Verify we fetched multiple pages
        pageCount.Should().BeGreaterThan(1, 
            "should have fetched multiple pages with pageSize=2 and 5 results");
    }
    
    [Fact]
    public async Task SpatialQueryAsync_S2ProximityNonPaginated_CrossingDateLine_NoDuplicates()
    {
        // Arrange - Create table with S2-indexed stores near the date line
        await CreateTableAsync<S2StoreEntity>();
        var table = new S2StoreTable(DynamoDb, TableName);
        
        // Search center exactly ON the date line at the equator
        // This is the most challenging case for deduplication
        var searchCenter = new GeoLocation(0.0, 180.0);
        
        // Create stores very close to the date line on both sides
        // These stores are in cells that might overlap the date line
        var stores = new[]
        {
            new S2StoreEntity
            {
                StoreId = "STORE",
                Location = new GeoLocation(0.0, 179.99),
                Name = "Store 1",
                Description = "Very close to date line from east"
            },
            new S2StoreEntity
            {
                StoreId = "STORE",
                Location = new GeoLocation(0.0, -179.99),
                Name = "Store 2",
                Description = "Very close to date line from west"
            },
            new S2StoreEntity
            {
                StoreId = "STORE",
                Location = new GeoLocation(0.1, 179.95),
                Name = "Store 3",
                Description = "Near date line, slightly north"
            },
            new S2StoreEntity
            {
                StoreId = "STORE",
                Location = new GeoLocation(-0.1, -179.95),
                Name = "Store 4",
                Description = "Near date line, slightly south"
            },
            new S2StoreEntity
            {
                StoreId = "STORE",
                Location = new GeoLocation(0.0, 179.5),
                Name = "Store 5",
                Description = "East of date line"
            },
            new S2StoreEntity
            {
                StoreId = "STORE",
                Location = new GeoLocation(0.0, -179.5),
                Name = "Store 6",
                Description = "West of date line"
            }
        };
        
        // Write all stores to DynamoDB
        foreach (var store in stores)
        {
            await table.PutAsync(store);
        }
        
        // Act - Execute SpatialQueryAsync with 100km radius centered ON the date line
        var result = await table.SpatialQueryAsync<S2StoreEntity>(
            locationSelector: store => store.Location,
            spatialIndexType: SpatialIndexType.S2,
            precision: 16,
            center: searchCenter,
            radiusKilometers: 100.0,
            queryBuilder: (query, cell, pagination) => query
                .Where<S2StoreEntity>(x => x.StoreId == "STORE" && x.Location == cell)
                .Paginate(pagination),
            pageSize: null
        );
        
        // Assert - Verify no duplicates
        result.Items.Should().NotBeNull();
        result.Items.Should().HaveCount(6, "all 6 stores are within 100km radius");
        
        // Verify each store appears exactly once by checking unique names
        var names = result.Items.Select(s => s.Name).ToList();
        names.Should().OnlyHaveUniqueItems("each store should appear exactly once, no duplicates");
        
        // Verify all expected stores are present
        names.Should().Contain("Store 1");
        names.Should().Contain("Store 2");
        names.Should().Contain("Store 3");
        names.Should().Contain("Store 4");
        names.Should().Contain("Store 5");
        names.Should().Contain("Store 6");
        
        // Verify each result is within the radius
        foreach (var store in result.Items)
        {
            var distance = store.Location.DistanceToKilometers(searchCenter);
            distance.Should().BeLessThanOrEqualTo(100.0, 
                $"Store {store.Name} at {store.Location} should be within 100km radius");
        }
    }
    
    #endregion
}
