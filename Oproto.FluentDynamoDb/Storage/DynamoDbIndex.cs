using Oproto.FluentDynamoDb.Requests;

namespace Oproto.FluentDynamoDb.Storage;

/// <summary>
/// Represents a DynamoDB Global Secondary Index (GSI) or Local Secondary Index (LSI).
/// Provides a convenient way to query indexes with the correct table and index configuration.
/// </summary>
/// <example>
/// <code>
/// // Define an index in your table class
/// public DynamoDbIndex StatusIndex => new DynamoDbIndex(this, "StatusIndex");
/// 
/// // Query the index
/// var results = await table.StatusIndex.Query
///     .Where("gsi1pk = :status")
///     .WithValue(":status", "ACTIVE")
///     .ExecuteAsync();
/// 
/// // Define an index with projection expression
/// public DynamoDbIndex StatusIndexWithProjection => 
///     new DynamoDbIndex(this, "StatusIndex", "id, amount, status");
/// 
/// // Query with auto-applied projection
/// var results = await table.StatusIndexWithProjection.Query
///     .Where("gsi1pk = :status")
///     .WithValue(":status", "ACTIVE")
///     .ExecuteAsync();
/// </code>
/// </example>
public class DynamoDbIndex
{
    private readonly DynamoDbTableBase _table;
    private readonly string? _projectionExpression;

    /// <summary>
    /// Initializes a new instance of the DynamoDbIndex.
    /// </summary>
    /// <param name="table">The parent table that contains this index.</param>
    /// <param name="indexName">The name of the index as defined in DynamoDB.</param>
    public DynamoDbIndex(DynamoDbTableBase table, string indexName)
    {
        _table = table;
        Name = indexName;
        _projectionExpression = null;
    }

    /// <summary>
    /// Initializes a new instance of the DynamoDbIndex with a projection expression.
    /// The projection expression will be automatically applied to all queries through this index.
    /// </summary>
    /// <param name="table">The parent table that contains this index.</param>
    /// <param name="indexName">The name of the index as defined in DynamoDB.</param>
    /// <param name="projectionExpression">The projection expression to automatically apply to queries.</param>
    /// <example>
    /// <code>
    /// // Define an index with projection
    /// public DynamoDbIndex StatusIndex => 
    ///     new DynamoDbIndex(this, "StatusIndex", "id, amount, status, entity_type");
    /// 
    /// // Projection is automatically applied
    /// var results = await table.StatusIndex.Query
    ///     .Where("status = :status")
    ///     .WithValue(":status", "ACTIVE")
    ///     .ExecuteAsync();
    /// </code>
    /// </example>
    public DynamoDbIndex(DynamoDbTableBase table, string indexName, string projectionExpression)
    {
        _table = table;
        Name = indexName;
        _projectionExpression = projectionExpression;
    }

    /// <summary>
    /// Gets the name of the index.
    /// </summary>
    public string Name { get; private init; }

    /// <summary>
    /// Gets a query builder pre-configured to query this specific index.
    /// The builder is automatically configured with the correct table name and index name.
    /// If a projection expression was specified in the constructor, it will be automatically applied.
    /// 
    /// Note: When querying an index, you must use the index's key schema in your key condition expression.
    /// Global Secondary Indexes (GSI) do not support consistent reads.
    /// </summary>
    /// <example>
    /// <code>
    /// // Query a GSI with its own key schema
    /// var results = await myIndex.Query
    ///     .Where("gsi1pk = :pk AND begins_with(gsi1sk, :prefix)")
    ///     .WithValue(":pk", "STATUS#ACTIVE")
    ///     .WithValue(":prefix", "USER#")
    ///     .ExecuteAsync();
    /// 
    /// // Manual projection override (takes precedence over auto-projection)
    /// var results = await myIndex.Query
    ///     .Where("gsi1pk = :pk")
    ///     .WithValue(":pk", "STATUS#ACTIVE")
    ///     .WithProjection("id, amount") // Overrides automatic projection
    ///     .ExecuteAsync();
    /// </code>
    /// </example>
    public QueryRequestBuilder Query
    {
        get
        {
            var builder = new QueryRequestBuilder(_table.DynamoDbClient)
                .ForTable(_table.Name)
                .UsingIndex(Name);

            // Auto-apply projection if configured
            if (!string.IsNullOrEmpty(_projectionExpression))
            {
                builder = builder.WithProjection(_projectionExpression);
            }

            return builder;
        }
    }
}

/// <summary>
/// Generic DynamoDB index that automatically applies projection for the specified type.
/// TDefault specifies the default projection/entity type for this index.
/// This type's projection is auto-applied but can be overridden with ToListAsync&lt;TOther&gt;().
/// </summary>
/// <typeparam name="TDefault">
/// The default projection/entity type for this index.
/// This type's projection is auto-applied but can be overridden with ToListAsync&lt;TOther&gt;().
/// </typeparam>
/// <example>
/// <code>
/// // Define a generic index with projection type
/// public DynamoDbIndex&lt;TransactionSummary&gt; StatusIndex => 
///     new DynamoDbIndex&lt;TransactionSummary&gt;(
///         this, 
///         "StatusIndex", 
///         "id, amount, status, entity_type");
/// 
/// // Query using default type
/// var summaries = await table.StatusIndex.QueryAsync(q => 
///     q.Where("status = :status").WithValue(":status", "ACTIVE"));
/// 
/// // Override to use different projection type
/// var minimal = await table.StatusIndex.QueryAsync&lt;MinimalTransaction&gt;(q => 
///     q.Where("status = :status").WithValue(":status", "ACTIVE"));
/// </code>
/// </example>
public class DynamoDbIndex<TDefault> where TDefault : class, new()
{
    private readonly DynamoDbTableBase _table;
    private readonly string _indexName;
    private readonly string? _projectionExpression;

    /// <summary>
    /// Initializes a new instance of the DynamoDbIndex&lt;TDefault&gt;.
    /// </summary>
    /// <param name="table">The parent table that contains this index.</param>
    /// <param name="indexName">The name of the index as defined in DynamoDB.</param>
    /// <param name="projectionExpression">Optional projection expression to automatically apply to queries.</param>
    /// <example>
    /// <code>
    /// // Generic index with projection
    /// public DynamoDbIndex&lt;TransactionSummary&gt; StatusIndex => 
    ///     new DynamoDbIndex&lt;TransactionSummary&gt;(
    ///         this, 
    ///         "StatusIndex", 
    ///         "id, amount, status");
    /// 
    /// // Generic index without projection (defaults to all fields)
    /// public DynamoDbIndex&lt;Transaction&gt; Gsi1 => 
    ///     new DynamoDbIndex&lt;Transaction&gt;(this, "Gsi1");
    /// </code>
    /// </example>
    public DynamoDbIndex(
        DynamoDbTableBase table,
        string indexName,
        string? projectionExpression = null)
    {
        _table = table;
        _indexName = indexName;
        _projectionExpression = projectionExpression;
    }

    /// <summary>
    /// Gets the index name.
    /// </summary>
    public string Name => _indexName;

    /// <summary>
    /// Gets a query builder pre-configured with projection expression.
    /// The projection is automatically applied unless manually overridden.
    /// </summary>
    /// <example>
    /// <code>
    /// // Use the Query property directly
    /// var results = await table.StatusIndex.Query
    ///     .Where("status = :status")
    ///     .WithValue(":status", "ACTIVE")
    ///     .ExecuteAsync();
    /// 
    /// // Manual projection override
    /// var results = await table.StatusIndex.Query
    ///     .Where("status = :status")
    ///     .WithValue(":status", "ACTIVE")
    ///     .WithProjection("id, amount") // Overrides automatic projection
    ///     .ExecuteAsync();
    /// </code>
    /// </example>
    public QueryRequestBuilder Query
    {
        get
        {
            var builder = new QueryRequestBuilder(_table.DynamoDbClient)
                .ForTable(_table.Name)
                .UsingIndex(_indexName);

            // Auto-apply projection if available
            if (!string.IsNullOrEmpty(_projectionExpression))
            {
                builder = builder.WithProjection(_projectionExpression);
            }

            return builder;
        }
    }

    /// <summary>
    /// Executes query and returns results as TDefault (the index's default type).
    /// </summary>
    /// <param name="configure">Action to configure the query builder.</param>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    /// <returns>A list of items of type TDefault.</returns>
    /// <example>
    /// <code>
    /// // Query using default type
    /// var summaries = await table.StatusIndex.QueryAsync(q => 
    ///     q.Where("status = :status").WithValue(":status", "ACTIVE"));
    /// </code>
    /// </example>
    public async Task<List<TDefault>> QueryAsync(
        Action<QueryRequestBuilder> configure,
        CancellationToken cancellationToken = default)
    {
        var builder = Query;
        configure(builder);
        
        // Note: This will be enhanced in future tasks to use ToListAsync<TDefault>()
        // For now, we execute the query and return an empty list as a placeholder
        var response = await builder.ExecuteAsync(cancellationToken);
        
        // TODO: Implement proper hydration in task 6
        // This is a placeholder implementation
        return new List<TDefault>();
    }

    /// <summary>
    /// Executes query and returns results as TResult (overriding the default type).
    /// Useful when the same GSI is used by multiple entity types.
    /// </summary>
    /// <typeparam name="TResult">The result type to return.</typeparam>
    /// <param name="configure">Action to configure the query builder.</param>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    /// <returns>A list of items of type TResult.</returns>
    /// <example>
    /// <code>
    /// // Index default is TransactionSummary
    /// var summaries = await table.StatusIndex.QueryAsync&lt;TransactionSummary&gt;(q => 
    ///     q.Where("status = :s").WithValue(":s", "ACTIVE"));
    /// 
    /// // Override to use different projection
    /// var minimal = await table.StatusIndex.QueryAsync&lt;MinimalTransaction&gt;(q => 
    ///     q.Where("status = :s").WithValue(":s", "ACTIVE"));
    /// </code>
    /// </example>
    public async Task<List<TResult>> QueryAsync<TResult>(
        Action<QueryRequestBuilder> configure,
        CancellationToken cancellationToken = default)
        where TResult : class, new()
    {
        var builder = Query;
        configure(builder);
        
        // Note: This will be enhanced in future tasks to use ToListAsync<TResult>()
        // For now, we execute the query and return an empty list as a placeholder
        var response = await builder.ExecuteAsync(cancellationToken);
        
        // TODO: Implement proper hydration in task 6
        // This is a placeholder implementation
        return new List<TResult>();
    }
}