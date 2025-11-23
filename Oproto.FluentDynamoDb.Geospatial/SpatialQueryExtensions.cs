using Amazon.DynamoDBv2.Model;
using Oproto.FluentDynamoDb.Attributes;
using Oproto.FluentDynamoDb.Geospatial.GeoHash;
using Oproto.FluentDynamoDb.Geospatial.H3;
using Oproto.FluentDynamoDb.Geospatial.S2;
using Oproto.FluentDynamoDb.Pagination;
using Oproto.FluentDynamoDb.Requests;
using Oproto.FluentDynamoDb.Storage;

namespace Oproto.FluentDynamoDb.Geospatial;

/// <summary>
/// Extension methods for performing spatial queries on DynamoDB tables.
/// </summary>
/// <remarks>
/// <para>
/// This implementation provides the core structure for spatial queries with support for
/// GeoHash, S2, and H3 spatial indices. It handles non-paginated proximity queries by
/// executing all cell queries in parallel for maximum performance.
/// </para>
/// <para>
/// <strong>Current Implementation Status (Task 10.3):</strong>
/// </para>
/// <list type="bullet">
/// <item>
/// <description>✅ Non-paginated proximity queries (radius-based)</description>
/// </item>
/// <item>
/// <description>❌ Paginated proximity queries (task 10.4 - not yet implemented)</description>
/// </item>
/// <item>
/// <description>❌ Bounding box queries (tasks 10.5 and 10.6 - not yet implemented)</description>
/// </item>
/// </list>
/// <para>
/// <strong>Integration Requirements:</strong>
/// </para>
/// <list type="bullet">
/// <item>
/// <description>Deserialization requires integration with source generator code</description>
/// </item>
/// <item>
/// <description>Distance calculation requires entity metadata to extract GeoLocation properties</description>
/// </item>
/// <item>
/// <description>Primary key extraction requires entity metadata for proper deduplication</description>
/// </item>
/// </list>
/// </remarks>
public static class SpatialQueryExtensions
{
    /// <summary>
    /// Performs a proximity query to find items within a specified radius of a center point.
    /// </summary>
    /// <typeparam name="TEntity">The entity type being queried.</typeparam>
    /// <param name="table">The DynamoDB table to query.</param>
    /// <param name="spatialIndexType">The type of spatial index used (GeoHash, S2, or H3).</param>
    /// <param name="precision">The precision/resolution level for the spatial index.</param>
    /// <param name="center">The center point of the search area.</param>
    /// <param name="radiusKilometers">The search radius in kilometers.</param>
    /// <param name="queryBuilder">A function that configures the query for each cell. Receives the query builder, cell value, and pagination configuration.</param>
    /// <param name="pageSize">Optional page size for pagination. If null, returns all results in non-paginated mode.</param>
    /// <param name="continuationToken">Optional continuation token for resuming paginated queries.</param>
    /// <param name="maxCells">Maximum number of cells to query. Default is 100.</param>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    /// <returns>A spatial query response containing items, continuation token, and query statistics.</returns>
    /// <remarks>
    /// <para>
    /// <strong>Non-Paginated Mode (pageSize = null) - IMPLEMENTED:</strong>
    /// </para>
    /// <list type="bullet">
    /// <item>
    /// <description>Queries all cells in parallel using Task.WhenAll for maximum performance</description>
    /// </item>
    /// <item>
    /// <description>Returns all results sorted by distance (closest first)</description>
    /// </item>
    /// <item>
    /// <description>Best for small result sets where you need all data immediately</description>
    /// </item>
    /// </list>
    /// <para>
    /// <strong>Paginated Mode (pageSize > 0) - NOT YET IMPLEMENTED (Task 10.4):</strong>
    /// </para>
    /// <list type="bullet">
    /// <item>
    /// <description>Will query cells sequentially in spiral order (closest to farthest)</description>
    /// </item>
    /// <item>
    /// <description>Will return up to pageSize items per request</description>
    /// </item>
    /// <item>
    /// <description>Will provide continuation token for fetching next page</description>
    /// </item>
    /// </list>
    /// </remarks>
    public static Task<SpatialQueryResponse<TEntity>> SpatialQueryAsync<TEntity>(
        this DynamoDbTableBase table,
        SpatialIndexType spatialIndexType,
        int precision,
        GeoLocation center,
        double radiusKilometers,
        Func<QueryRequestBuilder<TEntity>, string, IPaginationRequest, QueryRequestBuilder<TEntity>> queryBuilder,
        int? pageSize = null,
        SpatialContinuationToken? continuationToken = null,
        int maxCells = 100,
        CancellationToken cancellationToken = default)
        where TEntity : class
    {
        if (pageSize == null)
        {
            return SpatialQueryRadiusNonPaginatedAsync(
                table, spatialIndexType, precision, center, radiusKilometers,
                queryBuilder, maxCells, cancellationToken);
        }
        else
        {
            return SpatialQueryRadiusPaginatedAsync(
                table, spatialIndexType, precision, center, radiusKilometers,
                queryBuilder, pageSize.Value, continuationToken, maxCells, cancellationToken);
        }
    }

    /// <summary>
    /// Performs a bounding box query to find items within a rectangular geographic area.
    /// </summary>
    /// <typeparam name="TEntity">The entity type being queried.</typeparam>
    /// <param name="table">The DynamoDB table to query.</param>
    /// <param name="spatialIndexType">The type of spatial index used (GeoHash, S2, or H3).</param>
    /// <param name="precision">The precision/resolution level for the spatial index.</param>
    /// <param name="boundingBox">The rectangular geographic area to search within.</param>
    /// <param name="queryBuilder">A function that configures the query for each cell. Receives the query builder, cell value, and pagination configuration.</param>
    /// <param name="pageSize">Optional page size for pagination. If null, returns all results in non-paginated mode.</param>
    /// <param name="continuationToken">Optional continuation token for resuming paginated queries.</param>
    /// <param name="maxCells">Maximum number of cells to query. Default is 100.</param>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    /// <returns>A spatial query response containing items, continuation token, and query statistics.</returns>
    /// <remarks>
    /// <para>
    /// <strong>Non-Paginated Mode (pageSize = null) - IMPLEMENTED (Task 10.5):</strong>
    /// </para>
    /// <list type="bullet">
    /// <item>
    /// <description>Queries all cells in parallel using Task.WhenAll for maximum performance</description>
    /// </item>
    /// <item>
    /// <description>Returns all results within the bounding box</description>
    /// </item>
    /// <item>
    /// <description>Best for small result sets where you need all data immediately</description>
    /// </item>
    /// </list>
    /// <para>
    /// <strong>Paginated Mode (pageSize > 0) - NOT YET IMPLEMENTED (Task 10.6):</strong>
    /// </para>
    /// <list type="bullet">
    /// <item>
    /// <description>Will query cells sequentially in spiral order (closest to farthest from center)</description>
    /// </item>
    /// <item>
    /// <description>Will return up to pageSize items per request</description>
    /// </item>
    /// <item>
    /// <description>Will provide continuation token for fetching next page</description>
    /// </item>
    /// </list>
    /// </remarks>
    public static Task<SpatialQueryResponse<TEntity>> SpatialQueryAsync<TEntity>(
        this DynamoDbTableBase table,
        SpatialIndexType spatialIndexType,
        int precision,
        GeoBoundingBox boundingBox,
        Func<QueryRequestBuilder<TEntity>, string, IPaginationRequest, QueryRequestBuilder<TEntity>> queryBuilder,
        int? pageSize = null,
        SpatialContinuationToken? continuationToken = null,
        int maxCells = 100,
        CancellationToken cancellationToken = default)
        where TEntity : class
    {
        if (pageSize == null)
        {
            return SpatialQueryBoundingBoxNonPaginatedAsync(
                table, spatialIndexType, precision, boundingBox,
                queryBuilder, maxCells, cancellationToken);
        }
        else
        {
            return SpatialQueryBoundingBoxPaginatedAsync(
                table, spatialIndexType, precision, boundingBox,
                queryBuilder, pageSize.Value, continuationToken, maxCells, cancellationToken);
        }
    }

    /// <summary>
    /// Non-paginated proximity query: executes all cell queries in parallel.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This method implements the non-paginated mode for proximity queries as specified in task 10.3.
    /// It follows the algorithm defined in the design document:
    /// </para>
    /// <list type="number">
    /// <item>
    /// <description>Detect spatial index type and compute cell covering</description>
    /// </item>
    /// <item>
    /// <description>Execute ALL queries in parallel using Task.WhenAll</description>
    /// </item>
    /// <item>
    /// <description>Merge results and deduplicate by primary key</description>
    /// </item>
    /// <item>
    /// <description>Post-filter results by exact distance and sort by distance</description>
    /// </item>
    /// <item>
    /// <description>Return all results with null continuation token</description>
    /// </item>
    /// </list>
    /// <para>
    /// <strong>Validates Requirements:</strong> 3.1, 3.3, 3.4, 3.5, 12.1, 12.2, 12.3, 12.4, 12.5
    /// </para>
    /// <para>
    /// <strong>Validates Properties:</strong> Property 3 (Non-paginated queries execute all cells in parallel),
    /// Property 9 (Spatial query results are deduplicated by primary key)
    /// </para>
    /// </remarks>
    private static async Task<SpatialQueryResponse<TEntity>> SpatialQueryRadiusNonPaginatedAsync<TEntity>(
        DynamoDbTableBase table,
        SpatialIndexType spatialIndexType,
        int precision,
        GeoLocation center,
        double radiusKilometers,
        Func<QueryRequestBuilder<TEntity>, string, IPaginationRequest, QueryRequestBuilder<TEntity>> queryBuilder,
        int maxCells,
        CancellationToken cancellationToken)
        where TEntity : class
    {
        // Step 1: Compute cell covering based on spatial index type
        // For GeoHash: single BETWEEN query (Property 7)
        // For S2: sorted by distance from center (Property 5)
        // For H3: sorted by distance from center (Property 6)
        List<string> cells = spatialIndexType switch
        {
            SpatialIndexType.GeoHash => GetGeoHashCells(center, radiusKilometers, precision),
            SpatialIndexType.S2 => S2CellCovering.GetCellsForRadius(center, radiusKilometers, precision, maxCells),
            SpatialIndexType.H3 => H3CellCovering.GetCellsForRadius(center, radiusKilometers, precision, maxCells),
            _ => throw new ArgumentException($"Unsupported spatial index type: {spatialIndexType}")
        };

        // Step 2: Execute ALL queries in parallel using Task.WhenAll (Property 3)
        var queryTasks = new List<Task<QueryResponse>>();
        
        foreach (var cellValue in cells)
        {
            var query = table.Query<TEntity>();
            // For non-paginated mode, we don't use pagination within each cell query
            // We pass a dummy pagination request since the lambda expects it
            var pagination = new PaginationRequest(pageSize: int.MaxValue, paginationToken: string.Empty);
            // Invoke query builder lambda with (query, cellValue, pagination) (Property 8, Property 26)
            query = queryBuilder(query, cellValue, pagination);
            queryTasks.Add(query.ToDynamoDbResponseAsync(cancellationToken));
        }

        var responses = await Task.WhenAll(queryTasks);

        // Step 3: Merge results and deduplicate by primary key (Property 9)
        var allItems = new List<TEntity>();
        var seenKeys = new HashSet<string>();
        int totalScanned = 0;

        foreach (var response in responses)
        {
            totalScanned += response.Count ?? 0;
            foreach (var item in response.Items)
            {
                var key = GenerateItemKey(item);
                if (!seenKeys.Contains(key))
                {
                    seenKeys.Add(key);
                    var entity = DeserializeItem<TEntity>(item);
                    allItems.Add(entity);
                }
            }
        }

        // Step 4: Post-filter results by exact distance and sort by distance (closest first)
        var filtered = allItems
            .Select(item => new { Item = item, Distance = CalculateDistance(item, center) })
            .Where(x => x.Distance <= radiusKilometers)
            .OrderBy(x => x.Distance)
            .Select(x => x.Item)
            .ToList();

        // Step 5: Return all results with null continuation token (Property 25)
        return new SpatialQueryResponse<TEntity>
        {
            Items = filtered,
            ContinuationToken = null, // Non-paginated queries return null token
            TotalCellsQueried = cells.Count,
            TotalItemsScanned = totalScanned
        };
    }

    /// <summary>
    /// Non-paginated bounding box query: executes all cell queries in parallel.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This method implements the non-paginated mode for bounding box queries as specified in task 10.5.
    /// It follows the algorithm defined in the design document:
    /// </para>
    /// <list type="number">
    /// <item>
    /// <description>Detect spatial index type and compute cell covering for the bounding box</description>
    /// </item>
    /// <item>
    /// <description>Execute ALL queries in parallel using Task.WhenAll</description>
    /// </item>
    /// <item>
    /// <description>Merge results and deduplicate by primary key</description>
    /// </item>
    /// <item>
    /// <description>Return all results with null continuation token</description>
    /// </item>
    /// </list>
    /// <para>
    /// <strong>Validates Requirements:</strong> 4.1, 4.2, 4.4, 4.5, 12.1, 12.2, 12.3, 12.4, 12.5
    /// </para>
    /// <para>
    /// <strong>Validates Properties:</strong> Property 3 (Non-paginated queries execute all cells in parallel),
    /// Property 9 (Spatial query results are deduplicated by primary key), Property 10 (S2 bounding box queries compute correct cell coverings),
    /// Property 11 (H3 bounding box queries compute correct cell coverings), Property 12 (Large bounding boxes are limited to prevent excessive queries),
    /// Property 13 (Cell coverings use configured precision)
    /// </para>
    /// </remarks>
    private static async Task<SpatialQueryResponse<TEntity>> SpatialQueryBoundingBoxNonPaginatedAsync<TEntity>(
        DynamoDbTableBase table,
        SpatialIndexType spatialIndexType,
        int precision,
        GeoBoundingBox boundingBox,
        Func<QueryRequestBuilder<TEntity>, string, IPaginationRequest, QueryRequestBuilder<TEntity>> queryBuilder,
        int maxCells,
        CancellationToken cancellationToken)
        where TEntity : class
    {
        // Step 1: Compute cell covering based on spatial index type
        // For GeoHash: single BETWEEN query
        // For S2: cell covering for bounding box (Property 10, Property 13)
        // For H3: cell covering for bounding box (Property 11, Property 13)
        // All coverings are limited by maxCells (Property 12)
        List<string> cells = spatialIndexType switch
        {
            SpatialIndexType.GeoHash => GetGeoHashCellsForBoundingBox(boundingBox, precision),
            SpatialIndexType.S2 => S2CellCovering.GetCellsForBoundingBox(boundingBox, precision, maxCells),
            SpatialIndexType.H3 => H3CellCovering.GetCellsForBoundingBox(boundingBox, precision, maxCells),
            _ => throw new ArgumentException($"Unsupported spatial index type: {spatialIndexType}")
        };

        // Step 2: Execute ALL queries in parallel using Task.WhenAll (Property 3)
        var queryTasks = new List<Task<QueryResponse>>();
        
        foreach (var cellValue in cells)
        {
            var query = table.Query<TEntity>();
            // For non-paginated mode, we don't use pagination within each cell query
            // We pass a dummy pagination request since the lambda expects it
            var pagination = new PaginationRequest(pageSize: int.MaxValue, paginationToken: string.Empty);
            // Invoke query builder lambda with (query, cellValue, pagination) (Property 8, Property 26)
            query = queryBuilder(query, cellValue, pagination);
            queryTasks.Add(query.ToDynamoDbResponseAsync(cancellationToken));
        }

        var responses = await Task.WhenAll(queryTasks);

        // Step 3: Merge results and deduplicate by primary key (Property 9)
        var allItems = new List<TEntity>();
        var seenKeys = new HashSet<string>();
        int totalScanned = 0;

        foreach (var response in responses)
        {
            totalScanned += response.Count ?? 0;
            foreach (var item in response.Items)
            {
                var key = GenerateItemKey(item);
                if (!seenKeys.Contains(key))
                {
                    seenKeys.Add(key);
                    var entity = DeserializeItem<TEntity>(item);
                    allItems.Add(entity);
                }
            }
        }

        // Step 4: Return all results with null continuation token (Property 25)
        // Note: For bounding box queries, we don't post-filter by distance since
        // the bounding box is the filter criteria
        return new SpatialQueryResponse<TEntity>
        {
            Items = allItems,
            ContinuationToken = null, // Non-paginated queries return null token
            TotalCellsQueried = cells.Count,
            TotalItemsScanned = totalScanned
        };
    }

    /// <summary>
    /// Paginated proximity query: executes cell queries sequentially in spiral order.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This method implements the paginated mode for proximity queries as specified in task 10.4.
    /// It follows the algorithm defined in the design document:
    /// </para>
    /// <list type="number">
    /// <item>
    /// <description>Detect spatial index type and compute cell covering sorted by distance from center (spiral order)</description>
    /// </item>
    /// <item>
    /// <description>Resume from continuation token if provided (start at CellIndex with LastEvaluatedKey)</description>
    /// </item>
    /// <item>
    /// <description>Query cells SEQUENTIALLY in spiral order until pageSize reached</description>
    /// </item>
    /// <item>
    /// <description>Invoke query builder lambda with (query, cellValue, pagination) for each cell</description>
    /// </item>
    /// <item>
    /// <description>Collect results until pageSize reached (may stop mid-cell)</description>
    /// </item>
    /// <item>
    /// <description>Generate continuation token with CellIndex and LastEvaluatedKey if more results exist</description>
    /// </item>
    /// <item>
    /// <description>Post-filter results by exact distance (already roughly sorted by spiral order)</description>
    /// </item>
    /// </list>
    /// <para>
    /// <strong>Validates Requirements:</strong> 3.2, 3.3, 3.4, 3.5, 11.1, 11.2, 11.3, 11.4, 11.5, 12.1, 12.2, 12.3, 12.4, 12.5
    /// </para>
    /// <para>
    /// <strong>Validates Properties:</strong> Property 4 (Paginated queries execute cells sequentially in spiral order),
    /// Property 5 (S2 cell covering is sorted by distance from center), Property 6 (H3 cell covering is sorted by distance from center),
    /// Property 22 (Pagination limits results to page size), Property 23 (Continuation token contains cell index and LastEvaluatedKey),
    /// Property 24 (Continuation token enables resumption from correct position), Property 25 (Completed queries return null continuation token)
    /// </para>
    /// </remarks>
    private static async Task<SpatialQueryResponse<TEntity>> SpatialQueryRadiusPaginatedAsync<TEntity>(
        DynamoDbTableBase table,
        SpatialIndexType spatialIndexType,
        int precision,
        GeoLocation center,
        double radiusKilometers,
        Func<QueryRequestBuilder<TEntity>, string, IPaginationRequest, QueryRequestBuilder<TEntity>> queryBuilder,
        int pageSize,
        SpatialContinuationToken? continuationToken,
        int maxCells,
        CancellationToken cancellationToken)
        where TEntity : class
    {
        // Step 1: Compute cell covering sorted by distance from center (spiral order)
        // For S2: sorted by distance from center (Property 5)
        // For H3: sorted by distance from center (Property 6)
        // For GeoHash: single BETWEEN query (Property 7)
        List<string> cells = spatialIndexType switch
        {
            SpatialIndexType.GeoHash => GetGeoHashCells(center, radiusKilometers, precision),
            SpatialIndexType.S2 => S2CellCovering.GetCellsForRadius(center, radiusKilometers, precision, maxCells),
            SpatialIndexType.H3 => H3CellCovering.GetCellsForRadius(center, radiusKilometers, precision, maxCells),
            _ => throw new ArgumentException($"Unsupported spatial index type: {spatialIndexType}")
        };

        // Step 2: Resume from continuation token if provided (Requirement 11.3, Property 24)
        int startCellIndex = continuationToken?.CellIndex ?? 0;
        string? lastEvaluatedKey = continuationToken?.LastEvaluatedKey;

        // Step 3: Query cells SEQUENTIALLY in spiral order until pageSize reached (Property 4)
        var allItems = new List<TEntity>();
        var seenKeys = new HashSet<string>();
        int totalScanned = 0;
        int cellsQueried = 0;
        SpatialContinuationToken? nextToken = null;

        for (int cellIndex = startCellIndex; cellIndex < cells.Count; cellIndex++)
        {
            var cellValue = cells[cellIndex];
            
            // Create pagination request for this cell
            // If we're resuming from a continuation token on this cell, use the LastEvaluatedKey
            // Otherwise, start from the beginning of the cell
            var cellPagination = new PaginationRequest(
                pageSize: pageSize - allItems.Count, // Remaining space in the page
                paginationToken: (cellIndex == startCellIndex && lastEvaluatedKey != null) ? lastEvaluatedKey : string.Empty
            );

            // Step 4: Invoke query builder lambda with (query, cellValue, pagination) for each cell (Property 8, Property 26)
            var query = table.Query<TEntity>();
            query = queryBuilder(query, cellValue, cellPagination);
            
            // Execute the query for this cell
            var response = await query.ToDynamoDbResponseAsync(cancellationToken);
            cellsQueried++;
            totalScanned += response.Count ?? 0;

            // Step 5: Collect results until pageSize reached (may stop mid-cell) (Requirement 11.1, Property 22)
            foreach (var item in response.Items)
            {
                // Deduplicate by primary key
                var key = GenerateItemKey(item);
                if (!seenKeys.Contains(key))
                {
                    seenKeys.Add(key);
                    var entity = DeserializeItem<TEntity>(item);
                    allItems.Add(entity);

                    // Check if we've reached the page size
                    if (allItems.Count >= pageSize)
                    {
                        break;
                    }
                }
            }

            // Step 6: Generate continuation token if more results exist (Requirement 11.2, Property 23)
            // Check if we've reached the page size
            if (allItems.Count >= pageSize)
            {
                // Check if there are more items in the current cell
                if (response.LastEvaluatedKey != null && response.LastEvaluatedKey.Count > 0)
                {
                    // Stopped mid-cell - save position within this cell
                    nextToken = new SpatialContinuationToken
                    {
                        CellIndex = cellIndex,
                        LastEvaluatedKey = SerializeLastEvaluatedKey(response.LastEvaluatedKey)
                    };
                }
                else if (cellIndex + 1 < cells.Count)
                {
                    // Current cell is exhausted, but there are more cells
                    nextToken = new SpatialContinuationToken
                    {
                        CellIndex = cellIndex + 1,
                        LastEvaluatedKey = null
                    };
                }
                // else: Last cell is exhausted, nextToken remains null (Property 25)
                
                break;
            }

            // If current cell is not exhausted but we haven't reached page size yet,
            // we need to continue with the next cell
            // Reset lastEvaluatedKey for the next cell
            lastEvaluatedKey = null;
        }

        // Step 7: Post-filter results by exact distance (already roughly sorted by spiral order) (Requirement 11.4)
        var filtered = allItems
            .Select(item => new { Item = item, Distance = CalculateDistance(item, center) })
            .Where(x => x.Distance <= radiusKilometers)
            .Select(x => x.Item)
            .ToList();

        return new SpatialQueryResponse<TEntity>
        {
            Items = filtered,
            ContinuationToken = nextToken, // Null if all cells complete (Property 25)
            TotalCellsQueried = cellsQueried,
            TotalItemsScanned = totalScanned
        };
    }

    /// <summary>
    /// Paginated bounding box query: executes cell queries sequentially in spiral order.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This method implements the paginated mode for bounding box queries as specified in task 10.6.
    /// It follows the algorithm defined in the design document:
    /// </para>
    /// <list type="number">
    /// <item>
    /// <description>Detect spatial index type and compute cell covering sorted by distance from bounding box center (spiral order)</description>
    /// </item>
    /// <item>
    /// <description>Resume from continuation token if provided (start at CellIndex with LastEvaluatedKey)</description>
    /// </item>
    /// <item>
    /// <description>Query cells SEQUENTIALLY in spiral order until pageSize reached</description>
    /// </item>
    /// <item>
    /// <description>Invoke query builder lambda with (query, cellValue, pagination) for each cell</description>
    /// </item>
    /// <item>
    /// <description>Collect results until pageSize reached (may stop mid-cell)</description>
    /// </item>
    /// <item>
    /// <description>Generate continuation token with CellIndex and LastEvaluatedKey if more results exist</description>
    /// </item>
    /// </list>
    /// <para>
    /// <strong>Validates Requirements:</strong> 4.1, 4.2, 4.3, 4.4, 4.5, 11.1, 11.2, 11.3, 11.4, 11.5, 12.1, 12.2, 12.3, 12.4, 12.5
    /// </para>
    /// <para>
    /// <strong>Validates Properties:</strong> Property 4 (Paginated queries execute cells sequentially in spiral order),
    /// Property 10 (S2 bounding box queries compute correct cell coverings), Property 11 (H3 bounding box queries compute correct cell coverings),
    /// Property 12 (Large bounding boxes are limited to prevent excessive queries), Property 13 (Cell coverings use configured precision),
    /// Property 22 (Pagination limits results to page size), Property 23 (Continuation token contains cell index and LastEvaluatedKey),
    /// Property 24 (Continuation token enables resumption from correct position), Property 25 (Completed queries return null continuation token)
    /// </para>
    /// </remarks>
    private static async Task<SpatialQueryResponse<TEntity>> SpatialQueryBoundingBoxPaginatedAsync<TEntity>(
        DynamoDbTableBase table,
        SpatialIndexType spatialIndexType,
        int precision,
        GeoBoundingBox boundingBox,
        Func<QueryRequestBuilder<TEntity>, string, IPaginationRequest, QueryRequestBuilder<TEntity>> queryBuilder,
        int pageSize,
        SpatialContinuationToken? continuationToken,
        int maxCells,
        CancellationToken cancellationToken)
        where TEntity : class
    {
        // Step 1: Compute cell covering sorted by distance from bounding box center (spiral order)
        // For S2: cell covering for bounding box sorted by distance from center (Property 10, Property 13)
        // For H3: cell covering for bounding box sorted by distance from center (Property 11, Property 13)
        // For GeoHash: single BETWEEN query (Property 7)
        // All coverings are limited by maxCells (Property 12)
        List<string> cells = spatialIndexType switch
        {
            SpatialIndexType.GeoHash => GetGeoHashCellsForBoundingBox(boundingBox, precision),
            SpatialIndexType.S2 => S2CellCovering.GetCellsForBoundingBox(boundingBox, precision, maxCells),
            SpatialIndexType.H3 => H3CellCovering.GetCellsForBoundingBox(boundingBox, precision, maxCells),
            _ => throw new ArgumentException($"Unsupported spatial index type: {spatialIndexType}")
        };

        // Step 2: Resume from continuation token if provided (Requirement 11.3, Property 24)
        int startCellIndex = continuationToken?.CellIndex ?? 0;
        string? lastEvaluatedKey = continuationToken?.LastEvaluatedKey;

        // Step 3: Query cells SEQUENTIALLY in spiral order until pageSize reached (Property 4)
        var allItems = new List<TEntity>();
        var seenKeys = new HashSet<string>();
        int totalScanned = 0;
        int cellsQueried = 0;
        SpatialContinuationToken? nextToken = null;

        for (int cellIndex = startCellIndex; cellIndex < cells.Count; cellIndex++)
        {
            var cellValue = cells[cellIndex];
            
            // Create pagination request for this cell
            // If we're resuming from a continuation token on this cell, use the LastEvaluatedKey
            // Otherwise, start from the beginning of the cell
            var cellPagination = new PaginationRequest(
                pageSize: pageSize - allItems.Count, // Remaining space in the page
                paginationToken: (cellIndex == startCellIndex && lastEvaluatedKey != null) ? lastEvaluatedKey : string.Empty
            );

            // Step 4: Invoke query builder lambda with (query, cellValue, pagination) for each cell (Property 8, Property 26)
            var query = table.Query<TEntity>();
            query = queryBuilder(query, cellValue, cellPagination);
            
            // Execute the query for this cell
            var response = await query.ToDynamoDbResponseAsync(cancellationToken);
            cellsQueried++;
            totalScanned += response.Count ?? 0;

            // Step 5: Collect results until pageSize reached (may stop mid-cell) (Requirement 11.1, Property 22)
            foreach (var item in response.Items)
            {
                // Deduplicate by primary key
                var key = GenerateItemKey(item);
                if (!seenKeys.Contains(key))
                {
                    seenKeys.Add(key);
                    var entity = DeserializeItem<TEntity>(item);
                    allItems.Add(entity);

                    // Check if we've reached the page size
                    if (allItems.Count >= pageSize)
                    {
                        break;
                    }
                }
            }

            // Step 6: Generate continuation token if more results exist (Requirement 11.2, Property 23)
            // Check if we've reached the page size
            if (allItems.Count >= pageSize)
            {
                // Check if there are more items in the current cell
                if (response.LastEvaluatedKey != null && response.LastEvaluatedKey.Count > 0)
                {
                    // Stopped mid-cell - save position within this cell
                    nextToken = new SpatialContinuationToken
                    {
                        CellIndex = cellIndex,
                        LastEvaluatedKey = SerializeLastEvaluatedKey(response.LastEvaluatedKey)
                    };
                }
                else if (cellIndex + 1 < cells.Count)
                {
                    // Current cell is exhausted, but there are more cells
                    nextToken = new SpatialContinuationToken
                    {
                        CellIndex = cellIndex + 1,
                        LastEvaluatedKey = null
                    };
                }
                // else: Last cell is exhausted, nextToken remains null (Property 25)
                
                break;
            }

            // If current cell is not exhausted but we haven't reached page size yet,
            // we need to continue with the next cell
            // Reset lastEvaluatedKey for the next cell
            lastEvaluatedKey = null;
        }

        // Step 7: Return results with continuation token
        // Note: For bounding box queries, we don't post-filter by distance since
        // the bounding box is the filter criteria
        return new SpatialQueryResponse<TEntity>
        {
            Items = allItems,
            ContinuationToken = nextToken, // Null if all cells complete (Property 25)
            TotalCellsQueried = cellsQueried,
            TotalItemsScanned = totalScanned
        };
    }

    /// <summary>
    /// Serializes DynamoDB's LastEvaluatedKey to a string for storage in continuation token.
    /// </summary>
    /// <remarks>
    /// Uses System.Text.Json for AOT compatibility.
    /// The LastEvaluatedKey is a dictionary of AttributeValue objects that needs to be
    /// serialized to a string format that can be stored in the continuation token.
    /// </remarks>
    private static string SerializeLastEvaluatedKey(Dictionary<string, AttributeValue> lastEvaluatedKey)
    {
        // Convert AttributeValue dictionary to a JSON-serializable format
        var serializable = new Dictionary<string, object>();
        foreach (var kvp in lastEvaluatedKey)
        {
            serializable[kvp.Key] = AttributeValueToSerializable(kvp.Value);
        }
        
        var json = System.Text.Json.JsonSerializer.Serialize(serializable);
        return Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(json));
    }

    /// <summary>
    /// Deserializes a LastEvaluatedKey string back to DynamoDB's AttributeValue dictionary.
    /// </summary>
    private static Dictionary<string, AttributeValue> DeserializeLastEvaluatedKey(string serialized)
    {
        var bytes = Convert.FromBase64String(serialized);
        var json = System.Text.Encoding.UTF8.GetString(bytes);
        var serializable = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, object>>(json)
            ?? throw new InvalidOperationException("Failed to deserialize LastEvaluatedKey");
        
        var result = new Dictionary<string, AttributeValue>();
        foreach (var kvp in serializable)
        {
            result[kvp.Key] = SerializableToAttributeValue(kvp.Value);
        }
        
        return result;
    }

    /// <summary>
    /// Converts an AttributeValue to a JSON-serializable object.
    /// </summary>
    private static object AttributeValueToSerializable(AttributeValue value)
    {
        if (value.S != null) return new { Type = "S", Value = value.S };
        if (value.N != null) return new { Type = "N", Value = value.N };
        if (value.BOOL == true) return new { Type = "BOOL", Value = true };
        if (value.NULL == true) return new { Type = "NULL" };
        // Add more types as needed
        throw new NotSupportedException($"AttributeValue type not supported for serialization");
    }

    /// <summary>
    /// Converts a JSON-serializable object back to an AttributeValue.
    /// </summary>
    private static AttributeValue SerializableToAttributeValue(object obj)
    {
        if (obj is System.Text.Json.JsonElement element)
        {
            var type = element.GetProperty("Type").GetString();
            return type switch
            {
                "S" => new AttributeValue { S = element.GetProperty("Value").GetString() },
                "N" => new AttributeValue { N = element.GetProperty("Value").GetString() },
                "BOOL" => new AttributeValue { BOOL = element.GetProperty("Value").GetBoolean() },
                "NULL" => new AttributeValue { NULL = true },
                _ => throw new NotSupportedException($"AttributeValue type {type} not supported")
            };
        }
        throw new NotSupportedException("Unexpected object type in deserialization");
    }

    /// <summary>
    /// Gets the GeoHash range for a radius query.
    /// </summary>
    /// <remarks>
    /// For GeoHash, we use a single BETWEEN query instead of multiple discrete queries.
    /// The returned string is in the format "minHash:maxHash" which the query builder
    /// will need to handle specially to create a BETWEEN condition.
    /// </remarks>
    private static List<string> GetGeoHashCells(GeoLocation center, double radiusKilometers, int precision)
    {
        var (minHash, maxHash) = GeoHashCellCovering.GetRangeForRadius(center, radiusKilometers, precision);
        // Return a special format that indicates this is a range query
        return new List<string> { $"{minHash}:{maxHash}" };
    }

    /// <summary>
    /// Gets the GeoHash range for a bounding box query.
    /// </summary>
    /// <remarks>
    /// For GeoHash, we use a single BETWEEN query instead of multiple discrete queries.
    /// The returned string is in the format "minHash:maxHash" which the query builder
    /// will need to handle specially to create a BETWEEN condition.
    /// </remarks>
    private static List<string> GetGeoHashCellsForBoundingBox(GeoBoundingBox boundingBox, int precision)
    {
        var (minHash, maxHash) = GeoHashCellCovering.GetRangeForBoundingBox(boundingBox, precision);
        // Return a special format that indicates this is a range query
        return new List<string> { $"{minHash}:{maxHash}" };
    }

    /// <summary>
    /// Generates a unique key for an item for deduplication purposes.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <strong>PLACEHOLDER IMPLEMENTATION:</strong> This is a simplified approach that hashes all attributes.
    /// </para>
    /// <para>
    /// <strong>TODO:</strong> In production, this should extract the actual primary key (partition key + sort key)
    /// from the item based on entity metadata from the source generator. This will require:
    /// </para>
    /// <list type="bullet">
    /// <item>
    /// <description>Access to EntityMetadata to identify which attributes are keys</description>
    /// </item>
    /// <item>
    /// <description>Proper handling of composite keys (partition + sort)</description>
    /// </item>
    /// <item>
    /// <description>Support for different key types (string, number, binary)</description>
    /// </item>
    /// </list>
    /// </remarks>
    private static string GenerateItemKey(Dictionary<string, AttributeValue> item)
    {
        // TODO: Implement proper primary key extraction using entity metadata
        var keyParts = item.OrderBy(kvp => kvp.Key)
            .Select(kvp => $"{kvp.Key}={AttributeValueToString(kvp.Value)}");
        return string.Join("|", keyParts);
    }

    /// <summary>
    /// Converts an AttributeValue to a string for key generation.
    /// </summary>
    private static string AttributeValueToString(AttributeValue value)
    {
        if (value.S != null) return value.S;
        if (value.N != null) return value.N;
        if (value.BOOL == true) return "true";
        if (value.NULL == true) return "null";
        // Add more types as needed
        return value.ToString() ?? "";
    }

    /// <summary>
    /// Deserializes a DynamoDB item to an entity.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <strong>PLACEHOLDER IMPLEMENTATION:</strong> This method throws NotImplementedException.
    /// </para>
    /// <para>
    /// <strong>TODO:</strong> In production, this should use the source generator's deserialization code.
    /// The source generator creates FromDynamoDb methods for each entity type that handle:
    /// </para>
    /// <list type="bullet">
    /// <item>
    /// <description>Type-safe deserialization of all properties</description>
    /// </item>
    /// <item>
    /// <description>Spatial index decoding (GeoHash/S2/H3 to GeoLocation)</description>
    /// </item>
    /// <item>
    /// <description>Coordinate storage fallback (lat/lon attributes)</description>
    /// </item>
    /// <item>
    /// <description>Field-level encryption/decryption</description>
    /// </item>
    /// <item>
    /// <description>Format string application</description>
    /// </item>
    /// </list>
    /// </remarks>
    private static TEntity DeserializeItem<TEntity>(Dictionary<string, AttributeValue> item)
        where TEntity : class
    {
        throw new NotImplementedException(
            "Deserialization requires integration with the source generator. " +
            "The source generator creates FromDynamoDb methods for each entity type. " +
            "This integration will be completed as part of the full spatial query implementation.");
    }

    /// <summary>
    /// Calculates the distance from an entity to a center point.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <strong>PLACEHOLDER IMPLEMENTATION:</strong> This method throws NotImplementedException.
    /// </para>
    /// <para>
    /// <strong>TODO:</strong> In production, this should:
    /// </para>
    /// <list type="bullet">
    /// <item>
    /// <description>Use entity metadata to identify which property contains the GeoLocation</description>
    /// </item>
    /// <item>
    /// <description>Extract the GeoLocation value from the entity using reflection or generated code</description>
    /// </item>
    /// <item>
    /// <description>Calculate the distance using GeoLocation.DistanceToKilometers(center)</description>
    /// </item>
    /// </list>
    /// <para>
    /// This requires access to EntityMetadata which contains PropertyMetadata for all properties,
    /// including information about which properties are GeoLocation types with spatial indices.
    /// </para>
    /// </remarks>
    private static double CalculateDistance<TEntity>(TEntity entity, GeoLocation center)
        where TEntity : class
    {
        throw new NotImplementedException(
            "Distance calculation requires integration with entity metadata. " +
            "This will extract the GeoLocation property from the entity and calculate distance. " +
            "This integration will be completed as part of the full spatial query implementation.");
    }
}
