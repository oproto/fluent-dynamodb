namespace Oproto.FluentDynamoDb.Geospatial;

/// <summary>
/// Represents the response from a spatial query operation.
/// </summary>
/// <typeparam name="TEntity">The type of entity returned in the query results.</typeparam>
public class SpatialQueryResponse<TEntity>
{
    /// <summary>
    /// Gets or sets the list of items returned by the query.
    /// </summary>
    /// <remarks>
    /// Items are deduplicated by primary key and filtered by exact distance from the search center.
    /// For non-paginated queries, items are sorted by distance (closest first).
    /// For paginated queries, items are roughly sorted by distance due to spiral ordering.
    /// </remarks>
    public List<TEntity> Items { get; set; } = new();

    /// <summary>
    /// Gets or sets the continuation token for retrieving the next page of results.
    /// </summary>
    /// <remarks>
    /// Null if all results have been returned (query is complete).
    /// Non-null if more results are available. Pass this token to the next query to resume pagination.
    /// </remarks>
    public SpatialContinuationToken? ContinuationToken { get; set; }

    /// <summary>
    /// Gets or sets the total number of cells queried to produce these results.
    /// </summary>
    /// <remarks>
    /// For non-paginated queries, this is the total number of cells in the covering.
    /// For paginated queries, this is the number of cells queried so far (may be less than total).
    /// Useful for monitoring query performance and identifying problematic queries.
    /// </remarks>
    public int TotalCellsQueried { get; set; }

    /// <summary>
    /// Gets or sets the total number of items scanned across all cells.
    /// </summary>
    /// <remarks>
    /// This may be higher than the number of items returned due to:
    /// - Deduplication (items appearing in multiple cells)
    /// - Post-filtering (items outside the exact radius)
    /// - Pagination limits
    /// Useful for understanding query efficiency and DynamoDB read capacity consumption.
    /// </remarks>
    public int TotalItemsScanned { get; set; }
}
