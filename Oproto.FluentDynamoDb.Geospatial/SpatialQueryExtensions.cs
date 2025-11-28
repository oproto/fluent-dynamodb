using Amazon.DynamoDBv2.Model;
using Oproto.FluentDynamoDb.Attributes;
using Oproto.FluentDynamoDb.Geospatial.GeoHash;
using Oproto.FluentDynamoDb.Geospatial.H3;
using Oproto.FluentDynamoDb.Geospatial.S2;
using Oproto.FluentDynamoDb.Pagination;
using Oproto.FluentDynamoDb.Requests;
using Oproto.FluentDynamoDb.Requests.Extensions;
using Oproto.FluentDynamoDb.Storage; // Still needed for IDynamoDbEntity, DynamoDbTableBase, DynamoDbIndex

namespace Oproto.FluentDynamoDb.Geospatial;

/// <summary>
/// Extension methods for performing spatial queries on DynamoDB tables and indexes.
/// </summary>
/// <remarks>
/// <para>
/// This implementation provides comprehensive spatial query support for GeoHash, S2, and H3 spatial indices.
/// It supports both paginated and non-paginated queries for proximity (radius-based) and bounding box searches.
/// </para>
/// <para>
/// <strong>Supported Query Types:</strong>
/// </para>
/// <list type="bullet">
/// <item>
/// <description><strong>Proximity Queries:</strong> Find items within a specified radius of a center point</description>
/// </item>
/// <item>
/// <description><strong>Bounding Box Queries:</strong> Find items within a rectangular geographic area</description>
/// </item>
/// <item>
/// <description><strong>Custom Cell List Queries:</strong> Query using pre-computed cells from external libraries</description>
/// </item>
/// </list>
/// <para>
/// <strong>Query Modes:</strong>
/// </para>
/// <list type="bullet">
/// <item>
/// <description><strong>Non-Paginated (pageSize = null):</strong> Executes all cell queries in parallel for maximum performance. Best for small result sets.</description>
/// </item>
/// <item>
/// <description><strong>Paginated (pageSize > 0):</strong> Executes cell queries sequentially in spiral order (closest to farthest). Best for large result sets and memory efficiency.</description>
/// </item>
/// </list>
/// </remarks>
public static class SpatialQueryExtensions
{
    #region Table Extension Methods - Proximity Queries

    /// <summary>
    /// Performs a proximity query to find items within a specified radius of a center point.
    /// </summary>
    /// <typeparam name="TEntity">The entity type being queried.</typeparam>
    /// <param name="table">The DynamoDB table to query.</param>
    /// <param name="locationSelector">A function that extracts the GeoLocation from an entity.</param>
    /// <param name="spatialIndexType">The type of spatial index used (GeoHash, S2, or H3).</param>
    /// <param name="precision">The precision/resolution level for the spatial index.</param>
    /// <param name="center">The center point of the search area.</param>
    /// <param name="radiusKilometers">The search radius in kilometers.</param>
    /// <param name="queryBuilder">A function that configures the query for each cell.</param>
    /// <param name="pageSize">Optional page size for pagination. If null, returns all results.</param>
    /// <param name="continuationToken">Optional continuation token for resuming paginated queries.</param>
    /// <param name="maxCells">Maximum number of cells to query. Default is 100.</param>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    /// <returns>A spatial query response containing items, continuation token, and query statistics.</returns>
    public static Task<SpatialQueryResponse<TEntity>> SpatialQueryAsync<TEntity>(
        this DynamoDbTableBase table,
        Func<TEntity, GeoLocation> locationSelector,
        SpatialIndexType spatialIndexType,
        int precision,
        GeoLocation center,
        double radiusKilometers,
        Func<QueryRequestBuilder<TEntity>, string, IPaginationRequest, QueryRequestBuilder<TEntity>> queryBuilder,
        int? pageSize = null,
        SpatialContinuationToken? continuationToken = null,
        int maxCells = 100,
        CancellationToken cancellationToken = default)
        where TEntity : class, IDynamoDbEntity
    {
        // Compute cells based on spatial index type
        List<string> cells = ComputeCellsForRadius(spatialIndexType, precision, center, radiusKilometers, maxCells);
        
        // Delegate to the core implementation with query factory
        return SpatialQueryCoreAsync(
            createQuery: () => table.Query<TEntity>(),
            locationSelector: locationSelector,
            cells: cells,
            queryBuilder: queryBuilder,
            center: center,
            radiusKilometers: radiusKilometers,
            pageSize: pageSize,
            continuationToken: continuationToken,
            cancellationToken: cancellationToken);
    }

    #endregion

    #region Table Extension Methods - Bounding Box Queries

    /// <summary>
    /// Performs a bounding box query to find items within a rectangular geographic area.
    /// </summary>
    /// <typeparam name="TEntity">The entity type being queried.</typeparam>
    /// <param name="table">The DynamoDB table to query.</param>
    /// <param name="locationSelector">A function that extracts the GeoLocation from an entity.</param>
    /// <param name="spatialIndexType">The type of spatial index used (GeoHash, S2, or H3).</param>
    /// <param name="precision">The precision/resolution level for the spatial index.</param>
    /// <param name="boundingBox">The rectangular geographic area to search within.</param>
    /// <param name="queryBuilder">A function that configures the query for each cell.</param>
    /// <param name="pageSize">Optional page size for pagination. If null, returns all results.</param>
    /// <param name="continuationToken">Optional continuation token for resuming paginated queries.</param>
    /// <param name="maxCells">Maximum number of cells to query. Default is 100.</param>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    /// <returns>A spatial query response containing items, continuation token, and query statistics.</returns>
    public static Task<SpatialQueryResponse<TEntity>> SpatialQueryAsync<TEntity>(
        this DynamoDbTableBase table,
        Func<TEntity, GeoLocation> locationSelector,
        SpatialIndexType spatialIndexType,
        int precision,
        GeoBoundingBox boundingBox,
        Func<QueryRequestBuilder<TEntity>, string, IPaginationRequest, QueryRequestBuilder<TEntity>> queryBuilder,
        int? pageSize = null,
        SpatialContinuationToken? continuationToken = null,
        int maxCells = 100,
        CancellationToken cancellationToken = default)
        where TEntity : class, IDynamoDbEntity
    {
        // Compute cells based on spatial index type
        List<string> cells = ComputeCellsForBoundingBox(spatialIndexType, precision, boundingBox, maxCells);
        
        // Use bounding box center for distance calculations in paginated mode
        var center = boundingBox.Center;
        
        // Delegate to the core implementation with query factory
        return SpatialQueryCoreAsync(
            createQuery: () => table.Query<TEntity>(),
            locationSelector: locationSelector,
            cells: cells,
            queryBuilder: queryBuilder,
            center: center,
            radiusKilometers: null, // No radius filtering for bounding box queries
            pageSize: pageSize,
            continuationToken: continuationToken,
            cancellationToken: cancellationToken);
    }

    #endregion

    #region Table Extension Methods - Custom Cell List Queries

    /// <summary>
    /// Performs a spatial query using a pre-computed list of cells.
    /// Use this when you have custom cell computation logic (e.g., H3 k-ring, polyfill).
    /// </summary>
    /// <typeparam name="TEntity">The entity type being queried.</typeparam>
    /// <param name="table">The DynamoDB table to query.</param>
    /// <param name="locationSelector">A function that extracts the GeoLocation from an entity.</param>
    /// <param name="cells">The pre-computed list of spatial cells to query.</param>
    /// <param name="queryBuilder">A function that configures the query for each cell.</param>
    /// <param name="center">Optional center point for distance sorting. If null, no distance sorting is applied.</param>
    /// <param name="radiusKilometers">Optional radius for distance filtering. If null, no distance filtering is applied.</param>
    /// <param name="pageSize">Optional page size for pagination. If null, returns all results.</param>
    /// <param name="continuationToken">Optional continuation token for resuming paginated queries.</param>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    /// <returns>A spatial query response containing items, continuation token, and query statistics.</returns>
    /// <example>
    /// <code>
    /// // Using H3 k-ring from external library
    /// var centerCell = H3Index.FromLatLng(center.Latitude, center.Longitude, resolution: 9);
    /// var cells = centerCell.KRing(k: 2).Select(c => c.ToString());
    /// 
    /// var result = await table.SpatialQueryAsync(
    ///     locationSelector: store => store.Location,
    ///     cells: cells,
    ///     queryBuilder: (query, cell, pagination) => query
    ///         .Where&lt;Store&gt;(x => x.PartitionKey == "STORE" &amp;&amp; x.Location == cell)
    ///         .Paginate(pagination),
    ///     center: center,
    ///     radiusKilometers: 5.0,
    ///     pageSize: 50
    /// );
    /// </code>
    /// </example>
    public static Task<SpatialQueryResponse<TEntity>> SpatialQueryAsync<TEntity>(
        this DynamoDbTableBase table,
        Func<TEntity, GeoLocation> locationSelector,
        IEnumerable<string> cells,
        Func<QueryRequestBuilder<TEntity>, string, IPaginationRequest, QueryRequestBuilder<TEntity>> queryBuilder,
        GeoLocation? center = null,
        double? radiusKilometers = null,
        int? pageSize = null,
        SpatialContinuationToken? continuationToken = null,
        CancellationToken cancellationToken = default)
        where TEntity : class, IDynamoDbEntity
    {
        return SpatialQueryCoreAsync(
            createQuery: () => table.Query<TEntity>(),
            locationSelector: locationSelector,
            cells: cells.ToList(),
            queryBuilder: queryBuilder,
            center: center,
            radiusKilometers: radiusKilometers,
            pageSize: pageSize,
            continuationToken: continuationToken,
            cancellationToken: cancellationToken);
    }

    #endregion

    #region Index Extension Methods - Proximity Queries

    /// <summary>
    /// Performs a proximity query on a GSI to find items within a specified radius of a center point.
    /// </summary>
    /// <typeparam name="TEntity">The entity type being queried.</typeparam>
    /// <param name="index">The DynamoDB index to query.</param>
    /// <param name="locationSelector">A function that extracts the GeoLocation from an entity.</param>
    /// <param name="spatialIndexType">The type of spatial index used (GeoHash, S2, or H3).</param>
    /// <param name="precision">The precision/resolution level for the spatial index.</param>
    /// <param name="center">The center point of the search area.</param>
    /// <param name="radiusKilometers">The search radius in kilometers.</param>
    /// <param name="queryBuilder">A function that configures the query for each cell.</param>
    /// <param name="pageSize">Optional page size for pagination. If null, returns all results.</param>
    /// <param name="continuationToken">Optional continuation token for resuming paginated queries.</param>
    /// <param name="maxCells">Maximum number of cells to query. Default is 100.</param>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    /// <returns>A spatial query response containing items, continuation token, and query statistics.</returns>
    public static Task<SpatialQueryResponse<TEntity>> SpatialQueryAsync<TEntity>(
        this DynamoDbIndex index,
        Func<TEntity, GeoLocation> locationSelector,
        SpatialIndexType spatialIndexType,
        int precision,
        GeoLocation center,
        double radiusKilometers,
        Func<QueryRequestBuilder<TEntity>, string, IPaginationRequest, QueryRequestBuilder<TEntity>> queryBuilder,
        int? pageSize = null,
        SpatialContinuationToken? continuationToken = null,
        int maxCells = 100,
        CancellationToken cancellationToken = default)
        where TEntity : class, IDynamoDbEntity
    {
        List<string> cells = ComputeCellsForRadius(spatialIndexType, precision, center, radiusKilometers, maxCells);
        
        return SpatialQueryCoreAsync(
            createQuery: () => index.Query<TEntity>(),
            locationSelector: locationSelector,
            cells: cells,
            queryBuilder: queryBuilder,
            center: center,
            radiusKilometers: radiusKilometers,
            pageSize: pageSize,
            continuationToken: continuationToken,
            cancellationToken: cancellationToken);
    }

    #endregion

    #region Index Extension Methods - Bounding Box Queries

    /// <summary>
    /// Performs a bounding box query on a GSI to find items within a rectangular geographic area.
    /// </summary>
    /// <typeparam name="TEntity">The entity type being queried.</typeparam>
    /// <param name="index">The DynamoDB index to query.</param>
    /// <param name="locationSelector">A function that extracts the GeoLocation from an entity.</param>
    /// <param name="spatialIndexType">The type of spatial index used (GeoHash, S2, or H3).</param>
    /// <param name="precision">The precision/resolution level for the spatial index.</param>
    /// <param name="boundingBox">The rectangular geographic area to search within.</param>
    /// <param name="queryBuilder">A function that configures the query for each cell.</param>
    /// <param name="pageSize">Optional page size for pagination. If null, returns all results.</param>
    /// <param name="continuationToken">Optional continuation token for resuming paginated queries.</param>
    /// <param name="maxCells">Maximum number of cells to query. Default is 100.</param>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    /// <returns>A spatial query response containing items, continuation token, and query statistics.</returns>
    public static Task<SpatialQueryResponse<TEntity>> SpatialQueryAsync<TEntity>(
        this DynamoDbIndex index,
        Func<TEntity, GeoLocation> locationSelector,
        SpatialIndexType spatialIndexType,
        int precision,
        GeoBoundingBox boundingBox,
        Func<QueryRequestBuilder<TEntity>, string, IPaginationRequest, QueryRequestBuilder<TEntity>> queryBuilder,
        int? pageSize = null,
        SpatialContinuationToken? continuationToken = null,
        int maxCells = 100,
        CancellationToken cancellationToken = default)
        where TEntity : class, IDynamoDbEntity
    {
        List<string> cells = ComputeCellsForBoundingBox(spatialIndexType, precision, boundingBox, maxCells);
        var center = boundingBox.Center;
        
        return SpatialQueryCoreAsync(
            createQuery: () => index.Query<TEntity>(),
            locationSelector: locationSelector,
            cells: cells,
            queryBuilder: queryBuilder,
            center: center,
            radiusKilometers: null,
            pageSize: pageSize,
            continuationToken: continuationToken,
            cancellationToken: cancellationToken);
    }

    #endregion

    #region Index Extension Methods - Custom Cell List Queries

    /// <summary>
    /// Performs a spatial query on a GSI using a pre-computed list of cells.
    /// Use this when you have custom cell computation logic (e.g., H3 k-ring, polyfill).
    /// </summary>
    /// <typeparam name="TEntity">The entity type being queried.</typeparam>
    /// <param name="index">The DynamoDB index to query.</param>
    /// <param name="locationSelector">A function that extracts the GeoLocation from an entity.</param>
    /// <param name="cells">The pre-computed list of spatial cells to query.</param>
    /// <param name="queryBuilder">A function that configures the query for each cell.</param>
    /// <param name="center">Optional center point for distance sorting. If null, no distance sorting is applied.</param>
    /// <param name="radiusKilometers">Optional radius for distance filtering. If null, no distance filtering is applied.</param>
    /// <param name="pageSize">Optional page size for pagination. If null, returns all results.</param>
    /// <param name="continuationToken">Optional continuation token for resuming paginated queries.</param>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    /// <returns>A spatial query response containing items, continuation token, and query statistics.</returns>
    public static Task<SpatialQueryResponse<TEntity>> SpatialQueryAsync<TEntity>(
        this DynamoDbIndex index,
        Func<TEntity, GeoLocation> locationSelector,
        IEnumerable<string> cells,
        Func<QueryRequestBuilder<TEntity>, string, IPaginationRequest, QueryRequestBuilder<TEntity>> queryBuilder,
        GeoLocation? center = null,
        double? radiusKilometers = null,
        int? pageSize = null,
        SpatialContinuationToken? continuationToken = null,
        CancellationToken cancellationToken = default)
        where TEntity : class, IDynamoDbEntity
    {
        return SpatialQueryCoreAsync(
            createQuery: () => index.Query<TEntity>(),
            locationSelector: locationSelector,
            cells: cells.ToList(),
            queryBuilder: queryBuilder,
            center: center,
            radiusKilometers: radiusKilometers,
            pageSize: pageSize,
            continuationToken: continuationToken,
            cancellationToken: cancellationToken);
    }

    #endregion

    #region Core Implementation

    /// <summary>
    /// Core implementation for spatial queries. Handles both paginated and non-paginated modes.
    /// </summary>
    /// <typeparam name="TEntity">The entity type being queried.</typeparam>
    /// <param name="createQuery">Factory function to create a QueryRequestBuilder.</param>
    /// <param name="locationSelector">A function that extracts the GeoLocation from an entity.</param>
    /// <param name="cells">The list of spatial cells to query.</param>
    /// <param name="queryBuilder">A function that configures the query for each cell.</param>
    /// <param name="center">Optional center point for distance sorting and filtering.</param>
    /// <param name="radiusKilometers">Optional radius for distance filtering.</param>
    /// <param name="pageSize">Optional page size for pagination.</param>
    /// <param name="continuationToken">Optional continuation token for resuming paginated queries.</param>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    /// <returns>A spatial query response containing items, continuation token, and query statistics.</returns>
    private static Task<SpatialQueryResponse<TEntity>> SpatialQueryCoreAsync<TEntity>(
        Func<QueryRequestBuilder<TEntity>> createQuery,
        Func<TEntity, GeoLocation> locationSelector,
        List<string> cells,
        Func<QueryRequestBuilder<TEntity>, string, IPaginationRequest, QueryRequestBuilder<TEntity>> queryBuilder,
        GeoLocation? center,
        double? radiusKilometers,
        int? pageSize,
        SpatialContinuationToken? continuationToken,
        CancellationToken cancellationToken)
        where TEntity : class, IDynamoDbEntity
    {
        if (pageSize == null)
        {
            return SpatialQueryNonPaginatedCoreAsync(
                createQuery, locationSelector, cells, queryBuilder, center, radiusKilometers, cancellationToken);
        }
        else
        {
            return SpatialQueryPaginatedCoreAsync(
                createQuery, locationSelector, cells, queryBuilder, center, radiusKilometers,
                pageSize.Value, continuationToken, cancellationToken);
        }
    }

    /// <summary>
    /// Non-paginated core implementation: executes all cell queries in parallel.
    /// </summary>
    private static async Task<SpatialQueryResponse<TEntity>> SpatialQueryNonPaginatedCoreAsync<TEntity>(
        Func<QueryRequestBuilder<TEntity>> createQuery,
        Func<TEntity, GeoLocation> locationSelector,
        List<string> cells,
        Func<QueryRequestBuilder<TEntity>, string, IPaginationRequest, QueryRequestBuilder<TEntity>> queryBuilder,
        GeoLocation? center,
        double? radiusKilometers,
        CancellationToken cancellationToken)
        where TEntity : class, IDynamoDbEntity
    {
        // Execute ALL queries in parallel using Task.WhenAll
        var queryTasks = new List<Task<List<TEntity>>>();
        
        foreach (var cellValue in cells)
        {
            var query = createQuery();
            // For non-paginated mode, we don't use pagination within each cell query
            var pagination = new PaginationRequest(pageSize: int.MaxValue, paginationToken: string.Empty);
            query = queryBuilder(query, cellValue, pagination);
            queryTasks.Add(query.ToListAsync(cancellationToken));
        }

        var responses = await Task.WhenAll(queryTasks);

        // Merge results from all cells
        // Note: Deduplication is not needed because each DynamoDB item has a unique primary key.
        // An item can only have ONE location value, so it cannot appear in multiple cells.
        var allItems = new List<TEntity>();
        int totalScanned = 0;

        foreach (var entityList in responses)
        {
            totalScanned += entityList.Count;
            allItems.AddRange(entityList);
        }

        // Post-filter and sort results if center point is provided
        List<TEntity> filtered;
        if (center.HasValue)
        {
            var query = allItems
                .Select(item => new { Item = item, Distance = CalculateDistance(item, center.Value, locationSelector) });
            
            if (radiusKilometers.HasValue)
            {
                query = query.Where(x => x.Distance <= radiusKilometers.Value);
            }
            
            filtered = query.OrderBy(x => x.Distance).Select(x => x.Item).ToList();
        }
        else
        {
            filtered = allItems;
        }

        return new SpatialQueryResponse<TEntity>
        {
            Items = filtered,
            ContinuationToken = null,
            TotalCellsQueried = cells.Count,
            TotalItemsScanned = totalScanned
        };
    }

    /// <summary>
    /// Paginated core implementation: executes cell queries sequentially in spiral order.
    /// </summary>
    private static async Task<SpatialQueryResponse<TEntity>> SpatialQueryPaginatedCoreAsync<TEntity>(
        Func<QueryRequestBuilder<TEntity>> createQuery,
        Func<TEntity, GeoLocation> locationSelector,
        List<string> cells,
        Func<QueryRequestBuilder<TEntity>, string, IPaginationRequest, QueryRequestBuilder<TEntity>> queryBuilder,
        GeoLocation? center,
        double? radiusKilometers,
        int pageSize,
        SpatialContinuationToken? continuationToken,
        CancellationToken cancellationToken)
        where TEntity : class, IDynamoDbEntity
    {
        // Resume from continuation token if provided
        int startCellIndex = continuationToken?.CellIndex ?? 0;
        string? lastEvaluatedKeyToken = continuationToken?.LastEvaluatedKey;

        // Query cells SEQUENTIALLY in spiral order until pageSize reached
        var allItems = new List<TEntity>();
        int totalScanned = 0;
        int cellsQueried = 0;
        SpatialContinuationToken? nextToken = null;

        for (int cellIndex = startCellIndex; cellIndex < cells.Count; cellIndex++)
        {
            var cellValue = cells[cellIndex];
            
            // Create pagination request for this cell
            var cellPagination = new PaginationRequest(
                pageSize: pageSize - allItems.Count,
                paginationToken: (cellIndex == startCellIndex && lastEvaluatedKeyToken != null) ? lastEvaluatedKeyToken : string.Empty
            );

            var query = createQuery();
            query = queryBuilder(query, cellValue, cellPagination);
            
            // Apply pagination limit - request only what we need to fill the page
            var itemsNeeded = pageSize - allItems.Count;
            query = query.Take(itemsNeeded);
            
            // If we have a LastEvaluatedKey from continuation token, deserialize and apply it
            if (cellIndex == startCellIndex && lastEvaluatedKeyToken != null)
            {
                var deserializedKey = DeserializeLastEvaluatedKey(lastEvaluatedKeyToken);
                query = query.StartAt(deserializedKey);
            }
            
            var entityList = await query.ToListAsync(cancellationToken);
            cellsQueried++;
            
            // Get LastEvaluatedKey and ScannedCount directly from the builder instance
            // This avoids AsyncLocal issues that can occur with DynamoDbOperationContext
            var cellLastEvaluatedKey = query.LastEvaluatedKey;
            var scannedCount = query.ScannedCount ?? entityList.Count;
            totalScanned += scannedCount;

            // Add all items from this batch (we requested exactly what we need)
            allItems.AddRange(entityList);

            // Check if we've filled the page
            if (allItems.Count >= pageSize)
            {
                // Determine if there are more items to fetch
                bool hasMoreInCell = cellLastEvaluatedKey != null && cellLastEvaluatedKey.Count > 0;
                
                if (hasMoreInCell)
                {
                    // More items exist in this cell - save position to continue here
                    nextToken = new SpatialContinuationToken
                    {
                        CellIndex = cellIndex,
                        LastEvaluatedKey = SerializeLastEvaluatedKey(cellLastEvaluatedKey)
                    };
                }
                else if (cellIndex + 1 < cells.Count)
                {
                    // Current cell is exhausted, but there are more cells to query
                    nextToken = new SpatialContinuationToken
                    {
                        CellIndex = cellIndex + 1,
                        LastEvaluatedKey = null
                    };
                }
                // else: no more items anywhere, nextToken stays null
                break;
            }

            lastEvaluatedKeyToken = null;
        }

        // Post-filter results by exact distance if center and radius are provided
        List<TEntity> filtered;
        if (center.HasValue && radiusKilometers.HasValue)
        {
            filtered = allItems
                .Select(item => new { Item = item, Distance = CalculateDistance(item, center.Value, locationSelector) })
                .Where(x => x.Distance <= radiusKilometers.Value)
                .Select(x => x.Item)
                .ToList();
        }
        else
        {
            filtered = allItems;
        }

        return new SpatialQueryResponse<TEntity>
        {
            Items = filtered,
            ContinuationToken = nextToken,
            TotalCellsQueried = cellsQueried,
            TotalItemsScanned = totalScanned
        };
    }

    #endregion

    #region Cell Computation Helpers

    /// <summary>
    /// Computes the list of cells for a radius query based on spatial index type.
    /// </summary>
    private static List<string> ComputeCellsForRadius(
        SpatialIndexType spatialIndexType,
        int precision,
        GeoLocation center,
        double radiusKilometers,
        int maxCells)
    {
        return spatialIndexType switch
        {
            SpatialIndexType.GeoHash => GetGeoHashCells(center, radiusKilometers, precision),
            SpatialIndexType.S2 => S2CellCovering.GetCellsForRadius(center, radiusKilometers, precision, maxCells),
            SpatialIndexType.H3 => H3CellCovering.GetCellsForRadius(center, radiusKilometers, precision, maxCells),
            _ => throw new ArgumentException($"Unsupported spatial index type: {spatialIndexType}")
        };
    }

    /// <summary>
    /// Computes the list of cells for a bounding box query based on spatial index type.
    /// </summary>
    private static List<string> ComputeCellsForBoundingBox(
        SpatialIndexType spatialIndexType,
        int precision,
        GeoBoundingBox boundingBox,
        int maxCells)
    {
        return spatialIndexType switch
        {
            SpatialIndexType.GeoHash => GetGeoHashCellsForBoundingBox(boundingBox, precision),
            SpatialIndexType.S2 => S2CellCovering.GetCellsForBoundingBox(boundingBox, precision, maxCells),
            SpatialIndexType.H3 => H3CellCovering.GetCellsForBoundingBox(boundingBox, precision, maxCells),
            _ => throw new ArgumentException($"Unsupported spatial index type: {spatialIndexType}")
        };
    }

    /// <summary>
    /// Gets the GeoHash range for a radius query.
    /// </summary>
    private static List<string> GetGeoHashCells(GeoLocation center, double radiusKilometers, int precision)
    {
        var (minHash, maxHash) = GeoHashCellCovering.GetRangeForRadius(center, radiusKilometers, precision);
        return new List<string> { $"{minHash}:{maxHash}" };
    }

    /// <summary>
    /// Gets the GeoHash range for a bounding box query.
    /// </summary>
    private static List<string> GetGeoHashCellsForBoundingBox(GeoBoundingBox boundingBox, int precision)
    {
        var (minHash, maxHash) = GeoHashCellCovering.GetRangeForBoundingBox(boundingBox, precision);
        return new List<string> { $"{minHash}:{maxHash}" };
    }

    #endregion

    #region Utility Methods

    /// <summary>
    /// Calculates the distance from an entity to a center point.
    /// </summary>
    private static double CalculateDistance<TEntity>(
        TEntity entity,
        GeoLocation center,
        Func<TEntity, GeoLocation> locationSelector)
        where TEntity : class, IDynamoDbEntity
    {
        var location = locationSelector(entity);
        return location.DistanceToKilometers(center);
    }

    /// <summary>
    /// Serializes DynamoDB's LastEvaluatedKey to a string for storage in continuation token.
    /// </summary>
    private static string SerializeLastEvaluatedKey(Dictionary<string, AttributeValue> lastEvaluatedKey)
    {
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

    #endregion
}
