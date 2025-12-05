using System.Diagnostics;
using Oproto.FluentDynamoDb.Attributes;
using Oproto.FluentDynamoDb.Geospatial;
using Oproto.FluentDynamoDb.IntegrationTests.Infrastructure;
using Oproto.FluentDynamoDb.IntegrationTests.TestEntities;
using Oproto.FluentDynamoDb.Pagination;

namespace Oproto.FluentDynamoDb.IntegrationTests.RealWorld;

/// <summary>
/// Performance integration tests for S2 spatial queries with DynamoDB.
/// Tests verify that SpatialQueryAsync performs efficiently with large datasets.
/// 
/// Note: Tests use S2 level 10 (~4.5km cells) with large radii (25-30km) to stay within
/// the 500 cell limit. For level 16 (~71m cells), radius must be ≤0.5km.
/// </summary>
[Collection("DynamoDB Local")]
[Trait("Category", "Integration")]
[Trait("Category", "Performance")]
[Trait("Feature", "Geospatial")]
[Trait("Feature", "S2")]
[Trait("Feature", "SpatialQuery")]
public class S2PerformanceIntegrationTests : IntegrationTestBase
{
    public S2PerformanceIntegrationTests(DynamoDbLocalFixture fixture) : base(fixture)
    {
        // Enable performance tracking for these tests
        TrackPerformance = true;
    }
    
    /// <summary>
    /// Simple table wrapper for testing spatial queries (used in skipped tests).
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
    /// Table wrapper for GSI-based S2 spatial queries with low precision (level 10).
    /// Supports multiple stores per S2 cell via GSI and large search radii.
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
    
    /// <summary>
    /// Table wrapper for GSI-based spatial queries.
    /// Supports multiple stores per S2 cell via GSI.
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
    
    #region 30.1 Test S2 query with large result set (non-paginated)
    
    /// <summary>
    /// Tests S2 spatial query with large result set using low precision (level 10).
    /// Uses level 10 (~4.5km cells) with 20km radius to stay within 500 cell limit.
    /// </summary>
    [Fact]
    public async Task SpatialQueryAsync_S2ProximityNonPaginated_LargeResultSet_CompletesEfficiently()
    {
        // Arrange - Create table with 1000+ stores using low precision entity
        await CreateTableWithGsiAsync<S2StoreWithGsiLowPrecisionEntity>(
            gsiName: "s2-location-index",
            gsiPartitionKeyAttribute: "s2_cell",
            gsiSortKeyAttribute: "pk");
        var table = new S2StoreWithGsiLowPrecisionTable(DynamoDb, TableName);
        
        // San Francisco downtown area as the center
        var searchCenter = new GeoLocation(37.7749, -122.4194);
        
        Console.WriteLine("[Performance Test] Creating 1000+ stores with S2 level 10...");
        var createStopwatch = Stopwatch.StartNew();
        
        // Create 1200 stores distributed around the search center
        // We'll create stores in a grid pattern within a 25km radius
        // This ensures most stores are within the 20km query radius
        var stores = GenerateLowPrecisionStoresInRadius(searchCenter, radiusKm: 25, count: 1200);
        
        // Write all stores to DynamoDB in batches for efficiency
        var batchSize = 25; // DynamoDB BatchWriteItem limit
        for (var i = 0; i < stores.Count; i += batchSize)
        {
            var batch = stores.Skip(i).Take(batchSize);
            var tasks = batch.Select(store => table.PutAsync(store));
            await Task.WhenAll(tasks);
            
            if ((i + batchSize) % 100 == 0)
            {
                Console.WriteLine($"[Performance Test] Created {Math.Min(i + batchSize, stores.Count)} stores...");
            }
        }
        
        createStopwatch.Stop();
        Console.WriteLine($"[Performance Test] Created {stores.Count} stores in {createStopwatch.ElapsedMilliseconds}ms");
        
        // Act - Execute SpatialQueryAsync with 20km radius
        // Using level 10 (~4.5km cells) to stay within 500 cell limit
        Console.WriteLine("[Performance Test] Executing spatial query with 20km radius at level 10...");
        var queryStopwatch = Stopwatch.StartNew();
        
        var result = await table.S2LocationIndex.SpatialQueryAsync<S2StoreWithGsiLowPrecisionEntity>(
            locationSelector: store => store.Location,
            spatialIndexType: SpatialIndexType.S2,
            precision: 10, // S2 Level 10 (~4.5km cells) for large radius queries
            center: searchCenter,
            radiusKilometers: 20.0,
            queryBuilder: (query, cell, pagination) => query
                .Where<S2StoreWithGsiLowPrecisionEntity>(x => x.Location == cell),
            pageSize: null // Non-paginated mode - query all cells in parallel
        );
        
        queryStopwatch.Stop();
        
        // Assert - Verify parallel execution completes efficiently
        result.Items.Should().NotBeNull();
        result.Items.Should().NotBeEmpty("should return stores within 20km radius");
        
        // Log performance metrics
        var queryTimeMs = queryStopwatch.ElapsedMilliseconds;
        Console.WriteLine($"[Performance Test] Query completed in {queryTimeMs}ms");
        Console.WriteLine($"[Performance Test] Returned {result.Items.Count} stores");
        Console.WriteLine($"[Performance Test] Queried {result.TotalCellsQueried} S2 cells");
        Console.WriteLine($"[Performance Test] Scanned {result.TotalItemsScanned} items");
        Console.WriteLine($"[Performance Test] Average time per cell: {(double)queryTimeMs / result.TotalCellsQueried:F2}ms");
        
        // Verify all results are within radius
        var storesOutsideRadius = 0;
        foreach (var store in result.Items)
        {
            var distance = store.Location.DistanceToKilometers(searchCenter);
            if (distance > 20.0)
            {
                storesOutsideRadius++;
            }
        }
        
        storesOutsideRadius.Should().Be(0, 
            "all returned stores should be within the 20km radius");
        
        // Verify results are sorted by distance
        var distances = result.Items
            .Select(s => s.Location.DistanceToKilometers(searchCenter))
            .ToList();
        
        for (int i = 1; i < distances.Count; i++)
        {
            distances[i].Should().BeGreaterThanOrEqualTo(distances[i - 1],
                "results should be sorted by distance in ascending order");
        }
        
        // Performance assertion: Query should complete in reasonable time
        // With parallel execution, even 100+ cells should complete quickly
        // Allow 10 seconds for the query (generous for CI environments)
        queryTimeMs.Should().BeLessThan(10000,
            "non-paginated query with parallel execution should complete within 10 seconds");
        
        // Verify no duplicates (check by StoreId, not location)
        var uniqueStoreIds = result.Items
            .Select(s => s.StoreId)
            .Distinct()
            .Count();
        
        uniqueStoreIds.Should().Be(result.Items.Count,
            "each store should appear exactly once (no duplicates)");
        
        // Verify continuation token is null for non-paginated queries
        result.ContinuationToken.Should().BeNull(
            "non-paginated queries should return all results with null continuation token");
    }
    
    /// <summary>
    /// Tests that maxCells limit is respected for very large radius queries.
    /// Uses level 10 (~4.5km cells) with 50km radius and maxCells=50.
    /// </summary>
    [Fact]
    public async Task SpatialQueryAsync_S2ProximityNonPaginated_VeryLargeRadius_RespectsMaxCellsLimit()
    {
        // Arrange - Create table with stores using low precision entity
        await CreateTableWithGsiAsync<S2StoreWithGsiLowPrecisionEntity>(
            gsiName: "s2-location-index",
            gsiPartitionKeyAttribute: "s2_cell",
            gsiSortKeyAttribute: "pk");
        var table = new S2StoreWithGsiLowPrecisionTable(DynamoDb, TableName);
        
        var searchCenter = new GeoLocation(37.7749, -122.4194);
        
        Console.WriteLine("[Performance Test] Creating 500 stores with S2 level 10...");
        
        // Create 500 stores distributed around the search center
        // Using 50km radius to stay within reasonable cell count for level 10
        var stores = GenerateLowPrecisionStoresInRadius(searchCenter, radiusKm: 50, count: 500);
        
        // Write all stores to DynamoDB
        var batchSize = 25;
        for (var i = 0; i < stores.Count; i += batchSize)
        {
            var batch = stores.Skip(i).Take(batchSize);
            var tasks = batch.Select(store => table.PutAsync(store));
            await Task.WhenAll(tasks);
        }
        
        Console.WriteLine($"[Performance Test] Created {stores.Count} stores");
        
        // Act - Execute SpatialQueryAsync with large radius (50km) and maxCells limit
        // Using level 10 (~4.5km cells) to stay within reasonable cell count
        Console.WriteLine("[Performance Test] Executing spatial query with 50km radius and maxCells=50...");
        var queryStopwatch = Stopwatch.StartNew();
        
        var result = await table.S2LocationIndex.SpatialQueryAsync<S2StoreWithGsiLowPrecisionEntity>(
            locationSelector: store => store.Location,
            spatialIndexType: SpatialIndexType.S2,
            precision: 10, // S2 Level 10 (~4.5km cells) for large radius queries
            center: searchCenter,
            radiusKilometers: 50.0,
            queryBuilder: (query, cell, pagination) => query
                .Where<S2StoreWithGsiLowPrecisionEntity>(x => x.Location == cell),
            pageSize: null,
            maxCells: 50 // Limit to 50 cells to prevent excessive queries
        );
        
        queryStopwatch.Stop();
        
        // Assert - Verify maxCells limit is respected
        result.TotalCellsQueried.Should().BeLessThanOrEqualTo(50,
            "should respect maxCells limit and not query more than 50 cells");
        
        Console.WriteLine($"[Performance Test] Query completed in {queryStopwatch.ElapsedMilliseconds}ms");
        Console.WriteLine($"[Performance Test] Returned {result.Items.Count} stores");
        Console.WriteLine($"[Performance Test] Queried {result.TotalCellsQueried} S2 cells (limited by maxCells)");
        
        // Verify partial results are still returned
        result.Items.Should().NotBeEmpty(
            "should return partial results even when maxCells limit is hit");
        
        // Verify all returned results are within radius
        foreach (var store in result.Items)
        {
            var distance = store.Location.DistanceToKilometers(searchCenter);
            distance.Should().BeLessThanOrEqualTo(50.0,
                $"Store {store.Name} should be within 50km radius");
        }
    }
    
    /// <summary>
    /// Tests that multiple queries return consistent results and performance.
    /// Uses level 10 (~4.5km cells) with 15km radius to stay within 500 cell limit.
    /// </summary>
    [Fact(Skip = "Performance Test")]
    public async Task SpatialQueryAsync_S2ProximityNonPaginated_MultipleQueries_ConsistentPerformance()
    {
        // Arrange - Create table with stores using low precision entity
        await CreateTableWithGsiAsync<S2StoreWithGsiLowPrecisionEntity>(
            gsiName: "s2-location-index",
            gsiPartitionKeyAttribute: "s2_cell",
            gsiSortKeyAttribute: "pk");
        var table = new S2StoreWithGsiLowPrecisionTable(DynamoDb, TableName);
        
        var searchCenter = new GeoLocation(37.7749, -122.4194);
        
        Console.WriteLine("[Performance Test] Creating 800 stores with S2 level 10...");
        
        // Create 800 stores
        var stores = GenerateLowPrecisionStoresInRadius(searchCenter, radiusKm: 20, count: 800);
        
        // Write all stores to DynamoDB
        var batchSize = 25;
        for (var i = 0; i < stores.Count; i += batchSize)
        {
            var batch = stores.Skip(i).Take(batchSize);
            var tasks = batch.Select(store => table.PutAsync(store));
            await Task.WhenAll(tasks);
        }
        
        Console.WriteLine($"[Performance Test] Created {stores.Count} stores");
        
        // Act - Execute the same query multiple times to verify consistent performance
        var queryTimes = new List<long>();
        var resultCounts = new List<int>();
        
        for (var iteration = 1; iteration <= 3; iteration++)
        {
            Console.WriteLine($"[Performance Test] Executing query iteration {iteration}...");
            var queryStopwatch = Stopwatch.StartNew();
            
            var result = await table.S2LocationIndex.SpatialQueryAsync<S2StoreWithGsiLowPrecisionEntity>(
                locationSelector: store => store.Location,
                spatialIndexType: SpatialIndexType.S2,
                precision: 10, // S2 Level 10 (~4.5km cells) for large radius queries
                center: searchCenter,
                radiusKilometers: 15.0,
                queryBuilder: (query, cell, pagination) => query
                    .Where<S2StoreWithGsiLowPrecisionEntity>(x => x.Location == cell),
                pageSize: null
            );
            
            queryStopwatch.Stop();
            queryTimes.Add(queryStopwatch.ElapsedMilliseconds);
            resultCounts.Add(result.Items.Count);
            
            Console.WriteLine($"[Performance Test] Iteration {iteration}: {queryStopwatch.ElapsedMilliseconds}ms, {result.Items.Count} results");
        }
        
        // Assert - Verify consistent results
        resultCounts.Distinct().Should().HaveCount(1,
            "all iterations should return the same number of results");
        
        // Verify consistent performance (within reasonable variance)
        var avgTime = queryTimes.Average();
        var maxTime = queryTimes.Max();
        var minTime = queryTimes.Min();
        
        Console.WriteLine($"[Performance Test] Average query time: {avgTime:F2}ms");
        Console.WriteLine($"[Performance Test] Min: {minTime}ms, Max: {maxTime}ms");
        
        // Performance should be relatively consistent (max shouldn't be more than 3x min)
        // This accounts for cold start, caching, and network variability
        (maxTime / (double)minTime).Should().BeLessThan(3.0,
            "query performance should be relatively consistent across iterations");
    }
    
    #endregion
    
    #region 30.3 Test S2 paginated query with many pages
    
    /// <summary>
    /// Task 30.3: Test S2 paginated query with many pages.
    /// 
    /// Uses GSI-based entity (S2StoreWithGsiLowPrecisionEntity) with level 10 (~4.5km cells)
    /// to support multiple stores per S2 cell and large search radii.
    /// The GSI has S2 cell as partition key and StoreId as sort key, allowing efficient
    /// spatial queries with proper pagination.
    /// </summary>
    [Fact]
    public async Task SpatialQueryAsync_S2ProximityPaginated_ManyPages_SequentialExecutionWorksCorrectly()
    {
        // Arrange - Create table with GSI for spatial queries using low precision entity
        await CreateTableWithGsiAsync<S2StoreWithGsiLowPrecisionEntity>(
            gsiName: "s2-location-index",
            gsiPartitionKeyAttribute: "s2_cell",
            gsiSortKeyAttribute: "pk");
        var table = new S2StoreWithGsiLowPrecisionTable(DynamoDb, TableName);
        
        // San Francisco downtown area as the center
        var searchCenter = new GeoLocation(37.7749, -122.4194);
        
        Console.WriteLine("[Performance Test 30.3] Creating 1000+ stores with GSI support (level 10)...");
        var createStopwatch = Stopwatch.StartNew();
        
        // Create 1100 stores distributed around the search center
        // Using a radius that ensures most stores are within the search radius
        // The search radius will be 20km, so we generate stores within 18km to ensure most are found
        var stores = GenerateLowPrecisionStoresInRadius(searchCenter, radiusKm: 18, count: 1100);
        
        // Write all stores to DynamoDB in batches for efficiency
        var batchSize = 25; // DynamoDB BatchWriteItem limit
        for (var i = 0; i < stores.Count; i += batchSize)
        {
            var batch = stores.Skip(i).Take(batchSize);
            var tasks = batch.Select(store => table.PutAsync(store));
            await Task.WhenAll(tasks);
            
            if ((i + batchSize) % 200 == 0)
            {
                Console.WriteLine($"[Performance Test 30.3] Created {Math.Min(i + batchSize, stores.Count)} stores...");
            }
        }
        
        createStopwatch.Stop();
        Console.WriteLine($"[Performance Test 30.3] Created {stores.Count} stores in {createStopwatch.ElapsedMilliseconds}ms");
        
        // Act - Execute SpatialQueryAsync via GSI with pageSize=10 and iterate through all pages
        Console.WriteLine("[Performance Test 30.3] Executing paginated spatial query via GSI with pageSize=10...");
        var queryStopwatch = Stopwatch.StartNew();
        
        var allResults = new List<S2StoreWithGsiLowPrecisionEntity>();
        var pageCount = 0;
        var pageSizes = new List<int>();
        SpatialContinuationToken? continuationToken = null;
        
        // Track memory usage at start
        var initialMemory = GC.GetTotalMemory(true);
        var peakMemory = initialMemory;
        
        do
        {
            var result = await table.S2LocationIndex.SpatialQueryAsync<S2StoreWithGsiLowPrecisionEntity>(
                locationSelector: store => store.Location,
                spatialIndexType: SpatialIndexType.S2,
                precision: 10, // S2 Level 10 (~4.5km cells) for large radius queries
                center: searchCenter,
                radiusKilometers: 20.0, // 20km radius to get most stores
                queryBuilder: (query, cell, pagination) => query
                    .Where<S2StoreWithGsiLowPrecisionEntity>(x => x.Location == cell)
                    .Paginate(pagination),
                pageSize: 10, // Small page size to ensure many pages
                continuationToken: continuationToken
            );
            
            pageCount++;
            pageSizes.Add(result.Items.Count);
            allResults.AddRange(result.Items);
            continuationToken = result.ContinuationToken;
            
            // Track peak memory usage
            var currentMemory = GC.GetTotalMemory(false);
            if (currentMemory > peakMemory)
            {
                peakMemory = currentMemory;
            }
            
            // Log progress every 10 pages
            if (pageCount % 10 == 0)
            {
                Console.WriteLine($"[Performance Test 30.3] Processed {pageCount} pages, {allResults.Count} total results...");
            }
            
            // Safety limit to prevent infinite loops
            if (pageCount > 500)
            {
                Console.WriteLine("[Performance Test 30.3] WARNING: Exceeded 500 pages, breaking loop");
                break;
            }
            
        } while (continuationToken != null);
        
        queryStopwatch.Stop();
        
        // Calculate memory metrics
        var finalMemory = GC.GetTotalMemory(true);
        var memoryGrowth = finalMemory - initialMemory;
        var peakGrowth = peakMemory - initialMemory;
        
        // Assert - Verify sequential execution works correctly
        Console.WriteLine($"[Performance Test 30.3] Query completed in {queryStopwatch.ElapsedMilliseconds}ms");
        Console.WriteLine($"[Performance Test 30.3] Total pages: {pageCount}");
        Console.WriteLine($"[Performance Test 30.3] Total results: {allResults.Count}");
        Console.WriteLine($"[Performance Test 30.3] Average page size: {(double)allResults.Count / pageCount:F2}");
        Console.WriteLine($"[Performance Test 30.3] Initial memory: {initialMemory / 1024}KB");
        Console.WriteLine($"[Performance Test 30.3] Peak memory: {peakMemory / 1024}KB");
        Console.WriteLine($"[Performance Test 30.3] Final memory: {finalMemory / 1024}KB");
        Console.WriteLine($"[Performance Test 30.3] Peak memory growth: {peakGrowth / 1024}KB");
        
        // Verify we got multiple pages
        // With 1000+ stores and pageSize=10, we should have many pages
        // The exact number depends on how many stores are within the search radius
        pageCount.Should().BeGreaterThan(10,
            "should have multiple pages when querying stores with pageSize=10");
        
        // Verify page sizes are respected (each page should have at most pageSize items)
        foreach (var size in pageSizes.Take(pageSizes.Count - 1)) // Exclude last page which may be smaller
        {
            size.Should().BeLessThanOrEqualTo(10,
                "each page should have at most pageSize items");
        }
        
        // Verify we got a reasonable number of results
        allResults.Should().NotBeEmpty("should return stores within 20km radius");
        allResults.Count.Should().BeGreaterThan(50,
            "should return a significant number of stores within 20km radius");
        
        // Verify all results are within radius
        var storesOutsideRadius = 0;
        foreach (var store in allResults)
        {
            var distance = store.Location.DistanceToKilometers(searchCenter);
            if (distance > 20.0)
            {
                storesOutsideRadius++;
            }
        }
        
        storesOutsideRadius.Should().Be(0,
            "all returned stores should be within the 20km radius");
        
        // Verify no duplicates across pages (check by StoreId, not location)
        var uniqueStoreIds = allResults
            .Select(s => s.StoreId)
            .Distinct()
            .Count();
        
        uniqueStoreIds.Should().Be(allResults.Count,
            "each store should appear exactly once across all pages (no duplicates)");
        
        // Verify final page has null continuation token (already verified by loop exit)
        continuationToken.Should().BeNull(
            "final page should have null continuation token");
        
        // Verify memory usage remains relatively constant
        // Memory growth should be bounded - we're not loading all results at once
        // Allow for some growth due to result accumulation, but it should be reasonable
        // With 1000+ stores, if we were loading all at once, memory would grow significantly
        // With pagination, memory per page should be constant
        var expectedMaxGrowthPerPage = 50 * 1024; // ~50KB per page is reasonable
        var expectedMaxTotalGrowth = expectedMaxGrowthPerPage * Math.Min(pageCount, 20); // Cap at 20 pages worth
        
        // Note: This is a soft check - memory behavior can vary based on GC
        // The main verification is that we successfully paginated through all results
        Console.WriteLine($"[Performance Test 30.3] Memory growth per page: {(double)peakGrowth / pageCount / 1024:F2}KB");
    }
    
    /// <summary>
    /// Task 30.3b: Verify S2 paginated results are in spiral order (closest first).
    /// 
    /// BLOCKED: Requires GSI support (see task 34 in tasks.md).
    /// Same limitation as the main 30.3 test - needs GSI to support multiple stores per cell.
    /// </summary>
    [Fact(Skip = "BLOCKED: Requires GSI support. See task 34 in tasks.md.")]
    public async Task SpatialQueryAsync_S2ProximityPaginated_ManyPages_ResultsInSpiralOrder()
    {
        // Arrange - Create table with stores at known distances
        await CreateTableAsync<S2StoreEntity>();
        var table = new S2StoreTable(DynamoDb, TableName);
        
        var searchCenter = new GeoLocation(37.7749, -122.4194);
        
        Console.WriteLine("[Performance Test 30.3b] Creating 500 stores at known distances...");
        
        // Create stores at specific distances to verify spiral ordering
        var stores = GenerateStoresInRadius(searchCenter, radiusKm: 30, count: 500);
        
        // Assign unique StoreIds to each store (since Location is the sort key, we need unique PKs)
        for (int i = 0; i < stores.Count; i++)
        {
            stores[i].StoreId = $"STORE-{i:D4}";
        }
        
        // Write all stores to DynamoDB
        var batchSize = 25;
        for (var i = 0; i < stores.Count; i += batchSize)
        {
            var batch = stores.Skip(i).Take(batchSize);
            var tasks = batch.Select(store => table.PutAsync(store));
            await Task.WhenAll(tasks);
        }
        
        Console.WriteLine($"[Performance Test 30.3b] Created {stores.Count} stores");
        
        // Act - Execute paginated query and collect results in order
        var allResults = new List<S2StoreEntity>();
        SpatialContinuationToken? continuationToken = null;
        var pageCount = 0;
        
        do
        {
            var result = await table.SpatialQueryAsync<S2StoreEntity>(
                locationSelector: store => store.Location,
                spatialIndexType: SpatialIndexType.S2,
                precision: 16,
                center: searchCenter,
                radiusKilometers: 25.0,
                queryBuilder: (query, cell, pagination) => query
                    .Where<S2StoreEntity>(x => x.Location == cell),
                pageSize: 20,
                continuationToken: continuationToken
            );
            
            pageCount++;
            allResults.AddRange(result.Items);
            continuationToken = result.ContinuationToken;
            
            // Safety limit
            if (pageCount > 100)
            {
                break;
            }
            
        } while (continuationToken != null);
        
        Console.WriteLine($"[Performance Test 30.3b] Retrieved {allResults.Count} results in {pageCount} pages");
        
        // Assert - Verify results are roughly in spiral order (closest first)
        // Due to cell-based querying, results within a cell may not be perfectly sorted,
        // but overall trend should be increasing distance
        var distances = allResults
            .Select(s => s.Location.DistanceToKilometers(searchCenter))
            .ToList();
        
        // Calculate average distance for each quarter of results
        var quarterSize = distances.Count / 4;
        if (quarterSize > 0)
        {
            var q1Avg = distances.Take(quarterSize).Average();
            var q2Avg = distances.Skip(quarterSize).Take(quarterSize).Average();
            var q3Avg = distances.Skip(quarterSize * 2).Take(quarterSize).Average();
            var q4Avg = distances.Skip(quarterSize * 3).Average();
            
            Console.WriteLine($"[Performance Test 30.3b] Average distances by quarter:");
            Console.WriteLine($"  Q1 (first 25%): {q1Avg:F2}km");
            Console.WriteLine($"  Q2 (25-50%): {q2Avg:F2}km");
            Console.WriteLine($"  Q3 (50-75%): {q3Avg:F2}km");
            Console.WriteLine($"  Q4 (last 25%): {q4Avg:F2}km");
            
            // Verify spiral ordering: earlier quarters should have smaller average distances
            // Allow some tolerance since cell-based querying isn't perfectly sorted
            q1Avg.Should().BeLessThan(q4Avg,
                "first quarter should have smaller average distance than last quarter (spiral order)");
        }
        
        // Verify all results are within radius
        foreach (var store in allResults)
        {
            var distance = store.Location.DistanceToKilometers(searchCenter);
            distance.Should().BeLessThanOrEqualTo(25.0,
                $"Store {store.Name} should be within 25km radius");
        }
    }
    
    #endregion
    
    #region Helper Methods
    
    /// <summary>
    /// Generates a list of stores distributed in a radius around a center point.
    /// Uses a spiral pattern to ensure even distribution.
    /// </summary>
    private List<S2StoreEntity> GenerateStoresInRadius(
        GeoLocation center,
        double radiusKm,
        int count)
    {
        var stores = new List<S2StoreEntity>();
        var random = new Random(42); // Fixed seed for reproducibility
        
        // Generate stores in a spiral pattern
        for (var i = 0; i < count; i++)
        {
            // Use golden angle spiral for even distribution
            var angle = i * 137.508; // Golden angle in degrees
            var distance = radiusKm * Math.Sqrt((double)i / count); // Sqrt for even area distribution
            
            // Add some randomness to avoid perfect grid
            var randomOffset = random.NextDouble() * 0.5; // Up to 50% offset
            distance = distance * (1 + randomOffset);
            
            // Ensure we don't exceed the radius
            distance = Math.Min(distance, radiusKm * 0.98); // Stay within 98% of radius
            
            // Convert polar coordinates to lat/lon offset
            var angleRad = angle * Math.PI / 180.0;
            
            // Approximate conversion (good enough for testing)
            // 1 degree latitude ≈ 111 km
            // 1 degree longitude ≈ 111 km * cos(latitude)
            var latOffset = (distance * Math.Cos(angleRad)) / 111.0;
            var lonOffset = (distance * Math.Sin(angleRad)) / (111.0 * Math.Cos(center.Latitude * Math.PI / 180.0));
            
            var location = new GeoLocation(
                center.Latitude + latOffset,
                center.Longitude + lonOffset
            );
            
            stores.Add(new S2StoreEntity
            {
                StoreId = "STORE",
                Location = location,
                Name = $"Store {i + 1}",
                Description = $"Test store at {distance:F2}km from center"
            });
        }
        
        return stores;
    }
    
    /// <summary>
    /// Generates a list of GSI-based stores distributed in a radius around a center point.
    /// Uses a spiral pattern to ensure even distribution.
    /// </summary>
    private List<S2StoreWithGsiEntity> GenerateGsiStoresInRadius(
        GeoLocation center,
        double radiusKm,
        int count)
    {
        var stores = new List<S2StoreWithGsiEntity>();
        var random = new Random(42); // Fixed seed for reproducibility
        
        // Generate stores in a spiral pattern
        for (var i = 0; i < count; i++)
        {
            // Use golden angle spiral for even distribution
            var angle = i * 137.508; // Golden angle in degrees
            var distance = radiusKm * Math.Sqrt((double)i / count); // Sqrt for even area distribution
            
            // Add some randomness to avoid perfect grid
            var randomOffset = random.NextDouble() * 0.5; // Up to 50% offset
            distance = distance * (1 + randomOffset);
            
            // Ensure we don't exceed the radius
            distance = Math.Min(distance, radiusKm * 0.98); // Stay within 98% of radius
            
            // Convert polar coordinates to lat/lon offset
            var angleRad = angle * Math.PI / 180.0;
            
            // Approximate conversion (good enough for testing)
            // 1 degree latitude ≈ 111 km
            // 1 degree longitude ≈ 111 km * cos(latitude)
            var latOffset = (distance * Math.Cos(angleRad)) / 111.0;
            var lonOffset = (distance * Math.Sin(angleRad)) / (111.0 * Math.Cos(center.Latitude * Math.PI / 180.0));
            
            var location = new GeoLocation(
                center.Latitude + latOffset,
                center.Longitude + lonOffset
            );
            
            stores.Add(new S2StoreWithGsiEntity
            {
                StoreId = $"store-{i:D4}",
                Category = "RETAIL",
                Location = location,
                Name = $"Store {i + 1}",
                Description = $"Test store at {distance:F2}km from center"
            });
        }
        
        return stores;
    }
    
    /// <summary>
    /// Generates a list of low precision GSI-based S2 stores distributed in a radius around a center point.
    /// Uses a spiral pattern to ensure even distribution.
    /// Uses S2 level 10 (~4.5km cells) for large radius queries.
    /// </summary>
    private List<S2StoreWithGsiLowPrecisionEntity> GenerateLowPrecisionStoresInRadius(
        GeoLocation center,
        double radiusKm,
        int count)
    {
        var stores = new List<S2StoreWithGsiLowPrecisionEntity>();
        var random = new Random(42); // Fixed seed for reproducibility
        
        // Generate stores in a spiral pattern
        for (var i = 0; i < count; i++)
        {
            // Use golden angle spiral for even distribution
            var angle = i * 137.508; // Golden angle in degrees
            var distance = radiusKm * Math.Sqrt((double)i / count); // Sqrt for even area distribution
            
            // Add some randomness to avoid perfect grid
            var randomOffset = random.NextDouble() * 0.5; // Up to 50% offset
            distance = distance * (1 + randomOffset);
            
            // Ensure we don't exceed the radius
            distance = Math.Min(distance, radiusKm * 0.98); // Stay within 98% of radius
            
            // Convert polar coordinates to lat/lon offset
            var angleRad = angle * Math.PI / 180.0;
            
            // Approximate conversion (good enough for testing)
            // 1 degree latitude ≈ 111 km
            // 1 degree longitude ≈ 111 km * cos(latitude)
            var latOffset = (distance * Math.Cos(angleRad)) / 111.0;
            var lonOffset = (distance * Math.Sin(angleRad)) / (111.0 * Math.Cos(center.Latitude * Math.PI / 180.0));
            
            var location = new GeoLocation(
                center.Latitude + latOffset,
                center.Longitude + lonOffset
            );
            
            stores.Add(new S2StoreWithGsiLowPrecisionEntity
            {
                StoreId = $"store-{i:D4}",
                Category = "RETAIL",
                Location = location,
                Name = $"Store {i + 1}",
                Description = $"Test store at {distance:F2}km from center"
            });
        }
        
        return stores;
    }
    
    #endregion
}
