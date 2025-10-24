using Oproto.FluentDynamoDb.Requests;

namespace Oproto.FluentDynamoDb.Storage;

/// <summary>
/// Represents a DynamoDB Global Secondary Index (GSI) or Local Secondary Index (LSI).
/// Provides method-based access to query operations using expression strings.
/// </summary>
/// <example>
/// <code>
/// // Define an index in your table class
/// public DynamoDbIndex StatusIndex => new DynamoDbIndex(this, "StatusIndex");
/// 
/// // Query the index with manual configuration
/// var results = await table.StatusIndex.Query()
///     .Where("gsi1pk = {0}", "ACTIVE")
///     .ExecuteAsync();
/// 
/// // Query with expression string directly
/// var results = await table.StatusIndex.Query("gsi1pk = {0}", "ACTIVE").ExecuteAsync();
/// 
/// // Query with composite key
/// var results = await table.StatusIndex.Query("gsi1pk = {0} AND gsi1sk >= {1}", "ACTIVE", "2024-01-01").ExecuteAsync();
/// 
/// // Define an index with projection expression
/// public DynamoDbIndex StatusIndex => 
///     new DynamoDbIndex(this, "StatusIndex", "id, amount, status");
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
    /// var results = await table.StatusIndex.Query()
    ///     .Where("status = {0}", "ACTIVE")
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
    /// Creates a new Query operation builder for this index.
    /// Use this when you need to manually configure the query.
    /// </summary>
    /// <returns>A QueryRequestBuilder configured for this index.</returns>
    /// <example>
    /// <code>
    /// var results = await index.Query&lt;MyEntity&gt;()
    ///     .Where("gsi1pk = {0}", "STATUS#ACTIVE")
    ///     .ExecuteAsync();
    /// </code>
    /// </example>
    public QueryRequestBuilder<TEntity> Query<TEntity>() where TEntity : class
    {
        var builder = new QueryRequestBuilder<TEntity>(_table.DynamoDbClient)
            .ForTable(_table.Name)
            .UsingIndex(Name);

        if (!string.IsNullOrEmpty(_projectionExpression))
        {
            builder = builder.WithProjection(_projectionExpression);
        }

        return builder;
    }
    
    /// <summary>
    /// Creates a new Query operation builder with a key condition expression.
    /// Uses format string syntax for parameters: {0}, {1}, etc.
    /// </summary>
    /// <param name="keyConditionExpression">The key condition expression with format placeholders.</param>
    /// <param name="values">The values to substitute into the expression.</param>
    /// <returns>A QueryRequestBuilder configured with the key condition.</returns>
    /// <example>
    /// <code>
    /// // Simple partition key query
    /// var results = await index.Query("gsi1pk = {0}", "STATUS#ACTIVE").ExecuteAsync();
    /// 
    /// // Composite key query
    /// var results = await index.Query("gsi1pk = {0} AND gsi1sk > {1}", "STATUS#ACTIVE", "2024-01-01").ExecuteAsync();
    /// 
    /// // With begins_with
    /// var results = await index.Query("gsi1pk = {0} AND begins_with(gsi1sk, {1})", "STATUS#ACTIVE", "USER#").ExecuteAsync();
    /// </code>
    /// </example>
    public QueryRequestBuilder<TEntity> Query<TEntity>(string keyConditionExpression, params object[] values) where TEntity : class
    {
        return Requests.Extensions.WithConditionExpressionExtensions.Where(Query<TEntity>(), keyConditionExpression, values);
    }
}

/// <summary>
/// Generic DynamoDB index with a default projection type.
/// Maintained for backward compatibility but simplified to use method-based queries with expression strings.
/// </summary>
/// <typeparam name="TDefault">The default projection/entity type for this index.</typeparam>
/// <example>
/// <code>
/// // Define a generic index with projection type
/// public DynamoDbIndex&lt;TransactionSummary&gt; StatusIndex => 
///     new DynamoDbIndex&lt;TransactionSummary&gt;(
///         this, 
///         "StatusIndex", 
///         "id, amount, status, entity_type");
/// 
/// // Query using method-based API
/// var results = await table.StatusIndex.Query()
///     .Where("gsi1pk = {0}", "ACTIVE")
///     .ExecuteAsync();
/// 
/// // Query with expression string
/// var results = await table.StatusIndex.Query("gsi1pk = {0}", "ACTIVE").ExecuteAsync();
/// </code>
/// </example>
public class DynamoDbIndex<TDefault> where TDefault : class, new()
{
    private readonly DynamoDbIndex _innerIndex;

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
        _innerIndex = new DynamoDbIndex(table, indexName, projectionExpression);
    }
    


    /// <summary>
    /// Gets the index name.
    /// </summary>
    public string Name => _innerIndex.Name;

    /// <summary>
    /// Creates a new Query operation builder for this index.
    /// </summary>
    /// <returns>A QueryRequestBuilder configured for this index.</returns>
    /// <example>
    /// <code>
    /// var results = await table.StatusIndex.Query&lt;MyEntity&gt;()
    ///     .Where("gsi1pk = {0}", "ACTIVE")
    ///     .ExecuteAsync();
    /// </code>
    /// </example>
    public QueryRequestBuilder<TEntity> Query<TEntity>() where TEntity : class => _innerIndex.Query<TEntity>();
    
    /// <summary>
    /// Creates a new Query operation builder with a key condition expression.
    /// Uses format string syntax for parameters: {0}, {1}, etc.
    /// </summary>
    /// <param name="keyConditionExpression">The key condition expression with format placeholders.</param>
    /// <param name="values">The values to substitute into the expression.</param>
    /// <returns>A QueryRequestBuilder configured with the key condition.</returns>
    /// <example>
    /// <code>
    /// // Simple partition key query
    /// var results = await index.Query("gsi1pk = {0}", "STATUS#ACTIVE").ExecuteAsync();
    /// 
    /// // Composite key query
    /// var results = await index.Query("gsi1pk = {0} AND gsi1sk > {1}", "STATUS#ACTIVE", "2024-01-01").ExecuteAsync();
    /// </code>
    /// </example>
    public QueryRequestBuilder Query(string keyConditionExpression, params object[] values) => 
        _innerIndex.Query(keyConditionExpression, values);

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
    ///     q.Where("status = {0}", "ACTIVE"));
    /// </code>
    /// </example>
    public async Task<List<TDefault>> QueryAsync(
        Action<QueryRequestBuilder> configure,
        CancellationToken cancellationToken = default)
    {
        var builder = Query();
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
    ///     q.Where("status = {0}", "ACTIVE"));
    /// 
    /// // Override to use different projection
    /// var minimal = await table.StatusIndex.QueryAsync&lt;MinimalTransaction&gt;(q => 
    ///     q.Where("status = {0}", "ACTIVE"));
    /// </code>
    /// </example>
    public async Task<List<TResult>> QueryAsync<TResult>(
        Action<QueryRequestBuilder> configure,
        CancellationToken cancellationToken = default)
        where TResult : class, new()
    {
        var builder = Query();
        configure(builder);
        
        // Note: This will be enhanced in future tasks to use ToListAsync<TResult>()
        // For now, we execute the query and return an empty list as a placeholder
        var response = await builder.ExecuteAsync(cancellationToken);
        
        // TODO: Implement proper hydration in task 6
        // This is a placeholder implementation
        return new List<TResult>();
    }
}