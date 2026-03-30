using FluentResults;
using Oproto.FluentDynamoDb.Attributes;
using Oproto.FluentDynamoDb.Entities;
using Oproto.FluentDynamoDb.Geospatial;
using Oproto.FluentDynamoDb.Pagination;
using Oproto.FluentDynamoDb.Requests;
using Oproto.FluentDynamoDb.Storage;

namespace Oproto.FluentDynamoDb.FluentResults;

/// <summary>
/// FluentResults extensions for geospatial query operations.
/// These extensions wrap the SpatialQueryAsync methods to return Result&lt;T&gt; instead of throwing exceptions.
/// </summary>
public static class FluentResultsGeospatialExtensions
{
    #region Table Extension Methods - Proximity Queries

    /// <summary>
    /// Performs a proximity query to find items within a specified radius of a center point, returning a Result.
    /// This method wraps SpatialQueryAsync with try/catch and returns a Result instead of throwing exceptions.
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
    /// <returns>A Result containing the spatial query response or error details.</returns>
    public static async Task<Result<SpatialQueryResponse<TEntity>>> SpatialQueryAsyncResult<TEntity>(
        this IDynamoDbTable table,
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
        try
        {
            var response = await table.SpatialQueryAsync(
                locationSelector,
                spatialIndexType,
                precision,
                center,
                radiusKilometers,
                queryBuilder,
                pageSize,
                continuationToken,
                maxCells,
                cancellationToken);
            return Result.Ok(response);
        }
        catch (OperationCanceledException)
        {
            // Re-throw cancellation exceptions as they should not be wrapped
            throw;
        }
        catch (ArgumentException ex) when (ex.Message.Contains("latitude", StringComparison.OrdinalIgnoreCase) ||
                                           ex.Message.Contains("longitude", StringComparison.OrdinalIgnoreCase))
        {
            return Result.Fail<SpatialQueryResponse<TEntity>>(new InvalidCoordinatesError(
                ex.Message,
                latitude: center.Latitude,
                longitude: center.Longitude,
                innerException: ex));
        }
        catch (ArgumentException ex) when (ex.Message.Contains("radius", StringComparison.OrdinalIgnoreCase))
        {
            return Result.Fail<SpatialQueryResponse<TEntity>>(new SpatialQueryError(
                ex.Message,
                latitude: center.Latitude,
                longitude: center.Longitude,
                radiusKilometers: radiusKilometers,
                spatialIndexType: spatialIndexType.ToString(),
                precision: precision,
                innerException: ex));
        }
        catch (Exception ex)
        {
            return Result.Fail<SpatialQueryResponse<TEntity>>(DynamoDbErrors.FromException(ex));
        }
    }

    #endregion

    #region Table Extension Methods - Bounding Box Queries

    /// <summary>
    /// Performs a bounding box query to find items within a rectangular geographic area, returning a Result.
    /// This method wraps SpatialQueryAsync with try/catch and returns a Result instead of throwing exceptions.
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
    /// <returns>A Result containing the spatial query response or error details.</returns>
    public static async Task<Result<SpatialQueryResponse<TEntity>>> SpatialQueryAsyncResult<TEntity>(
        this IDynamoDbTable table,
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
        try
        {
            var response = await table.SpatialQueryAsync(
                locationSelector,
                spatialIndexType,
                precision,
                boundingBox,
                queryBuilder,
                pageSize,
                continuationToken,
                maxCells,
                cancellationToken);
            return Result.Ok(response);
        }
        catch (OperationCanceledException)
        {
            // Re-throw cancellation exceptions as they should not be wrapped
            throw;
        }
        catch (ArgumentException ex) when (ex.Message.Contains("bounding", StringComparison.OrdinalIgnoreCase) ||
                                           ex.Message.Contains("latitude", StringComparison.OrdinalIgnoreCase) ||
                                           ex.Message.Contains("longitude", StringComparison.OrdinalIgnoreCase))
        {
            return Result.Fail<SpatialQueryResponse<TEntity>>(new InvalidBoundingBoxError(
                ex.Message,
                southwestLatitude: boundingBox.Southwest.Latitude,
                southwestLongitude: boundingBox.Southwest.Longitude,
                northeastLatitude: boundingBox.Northeast.Latitude,
                northeastLongitude: boundingBox.Northeast.Longitude,
                innerException: ex));
        }
        catch (Exception ex)
        {
            return Result.Fail<SpatialQueryResponse<TEntity>>(DynamoDbErrors.FromException(ex));
        }
    }

    #endregion

    #region Table Extension Methods - Custom Cell List Queries

    /// <summary>
    /// Performs a spatial query using a pre-computed list of cells, returning a Result.
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
    /// <returns>A Result containing the spatial query response or error details.</returns>
    public static async Task<Result<SpatialQueryResponse<TEntity>>> SpatialQueryAsyncResult<TEntity>(
        this IDynamoDbTable table,
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
        try
        {
            var response = await table.SpatialQueryAsync(
                locationSelector,
                cells,
                queryBuilder,
                center,
                radiusKilometers,
                pageSize,
                continuationToken,
                cancellationToken);
            return Result.Ok(response);
        }
        catch (OperationCanceledException)
        {
            // Re-throw cancellation exceptions as they should not be wrapped
            throw;
        }
        catch (Exception ex)
        {
            return Result.Fail<SpatialQueryResponse<TEntity>>(DynamoDbErrors.FromException(ex));
        }
    }

    #endregion

    #region Index Extension Methods - Proximity Queries

    /// <summary>
    /// Performs a proximity query on a GSI to find items within a specified radius of a center point, returning a Result.
    /// This method wraps SpatialQueryAsync with try/catch and returns a Result instead of throwing exceptions.
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
    /// <returns>A Result containing the spatial query response or error details.</returns>
    public static async Task<Result<SpatialQueryResponse<TEntity>>> SpatialQueryAsyncResult<TEntity>(
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
        try
        {
            var response = await index.SpatialQueryAsync(
                locationSelector,
                spatialIndexType,
                precision,
                center,
                radiusKilometers,
                queryBuilder,
                pageSize,
                continuationToken,
                maxCells,
                cancellationToken);
            return Result.Ok(response);
        }
        catch (OperationCanceledException)
        {
            // Re-throw cancellation exceptions as they should not be wrapped
            throw;
        }
        catch (ArgumentException ex) when (ex.Message.Contains("latitude", StringComparison.OrdinalIgnoreCase) ||
                                           ex.Message.Contains("longitude", StringComparison.OrdinalIgnoreCase))
        {
            return Result.Fail<SpatialQueryResponse<TEntity>>(new InvalidCoordinatesError(
                ex.Message,
                latitude: center.Latitude,
                longitude: center.Longitude,
                innerException: ex));
        }
        catch (ArgumentException ex) when (ex.Message.Contains("radius", StringComparison.OrdinalIgnoreCase))
        {
            return Result.Fail<SpatialQueryResponse<TEntity>>(new SpatialQueryError(
                ex.Message,
                latitude: center.Latitude,
                longitude: center.Longitude,
                radiusKilometers: radiusKilometers,
                spatialIndexType: spatialIndexType.ToString(),
                precision: precision,
                innerException: ex));
        }
        catch (Exception ex)
        {
            return Result.Fail<SpatialQueryResponse<TEntity>>(DynamoDbErrors.FromException(ex));
        }
    }

    #endregion

    #region Index Extension Methods - Bounding Box Queries

    /// <summary>
    /// Performs a bounding box query on a GSI to find items within a rectangular geographic area, returning a Result.
    /// This method wraps SpatialQueryAsync with try/catch and returns a Result instead of throwing exceptions.
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
    /// <returns>A Result containing the spatial query response or error details.</returns>
    public static async Task<Result<SpatialQueryResponse<TEntity>>> SpatialQueryAsyncResult<TEntity>(
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
        try
        {
            var response = await index.SpatialQueryAsync(
                locationSelector,
                spatialIndexType,
                precision,
                boundingBox,
                queryBuilder,
                pageSize,
                continuationToken,
                maxCells,
                cancellationToken);
            return Result.Ok(response);
        }
        catch (OperationCanceledException)
        {
            // Re-throw cancellation exceptions as they should not be wrapped
            throw;
        }
        catch (ArgumentException ex) when (ex.Message.Contains("bounding", StringComparison.OrdinalIgnoreCase) ||
                                           ex.Message.Contains("latitude", StringComparison.OrdinalIgnoreCase) ||
                                           ex.Message.Contains("longitude", StringComparison.OrdinalIgnoreCase))
        {
            return Result.Fail<SpatialQueryResponse<TEntity>>(new InvalidBoundingBoxError(
                ex.Message,
                southwestLatitude: boundingBox.Southwest.Latitude,
                southwestLongitude: boundingBox.Southwest.Longitude,
                northeastLatitude: boundingBox.Northeast.Latitude,
                northeastLongitude: boundingBox.Northeast.Longitude,
                innerException: ex));
        }
        catch (Exception ex)
        {
            return Result.Fail<SpatialQueryResponse<TEntity>>(DynamoDbErrors.FromException(ex));
        }
    }

    #endregion

    #region Index Extension Methods - Custom Cell List Queries

    /// <summary>
    /// Performs a spatial query on a GSI using a pre-computed list of cells, returning a Result.
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
    /// <returns>A Result containing the spatial query response or error details.</returns>
    public static async Task<Result<SpatialQueryResponse<TEntity>>> SpatialQueryAsyncResult<TEntity>(
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
        try
        {
            var response = await index.SpatialQueryAsync(
                locationSelector,
                cells,
                queryBuilder,
                center,
                radiusKilometers,
                pageSize,
                continuationToken,
                cancellationToken);
            return Result.Ok(response);
        }
        catch (OperationCanceledException)
        {
            // Re-throw cancellation exceptions as they should not be wrapped
            throw;
        }
        catch (Exception ex)
        {
            return Result.Fail<SpatialQueryResponse<TEntity>>(DynamoDbErrors.FromException(ex));
        }
    }

    #endregion
}
