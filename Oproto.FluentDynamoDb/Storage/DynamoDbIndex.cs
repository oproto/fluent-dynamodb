using System.Linq.Expressions;
using Oproto.FluentDynamoDb.Entities;
using Oproto.FluentDynamoDb.Requests;
using Oproto.FluentDynamoDb.Requests.Extensions;

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
    private readonly IDynamoDbTable _table;
    private readonly string? _projectionExpression;

    /// <summary>
    /// Initializes a new instance of the DynamoDbIndex.
    /// </summary>
    /// <param name="table">The parent table that contains this index.</param>
    /// <param name="indexName">The name of the index as defined in DynamoDB.</param>
    public DynamoDbIndex(IDynamoDbTable table, string indexName)
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
    public DynamoDbIndex(IDynamoDbTable table, string indexName, string projectionExpression)
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
    public QueryRequestBuilder<TEntity> Query<TEntity>() 
        where TEntity : class, IReadOnlyEntity
    {
        var options = _table.GetOptions();
        var builder = new QueryRequestBuilder<TEntity>(_table.DynamoDbClient, options)
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
    public QueryRequestBuilder<TEntity> Query<TEntity>(string keyConditionExpression, params object[] values) 
        where TEntity : class, IReadOnlyEntity
    {
        return Requests.Extensions.WithConditionExpressionExtensions.Where(Query<TEntity>(), keyConditionExpression, values);
    }
    
    /// <summary>
    /// Creates a new Query operation builder with a LINQ expression for the key condition.
    /// </summary>
    /// <typeparam name="TEntity">The entity type to query.</typeparam>
    /// <param name="keyCondition">The LINQ expression representing the key condition.</param>
    /// <returns>A QueryRequestBuilder configured with the key condition.</returns>
    /// <example>
    /// <code>
    /// // Lambda expression query
    /// var results = await index.Query&lt;MyEntity&gt;(x => x.Gsi1Pk == "STATUS#ACTIVE")
    ///     .ToListAsync();
    /// 
    /// // Composite key with lambda
    /// var results = await index.Query&lt;MyEntity&gt;(x => x.Gsi1Pk == "STATUS#ACTIVE" &amp;&amp; x.Gsi1Sk.StartsWith("USER#"))
    ///     .ToListAsync();
    /// </code>
    /// </example>
    public QueryRequestBuilder<TEntity> Query<TEntity>(Expression<Func<TEntity, bool>> keyCondition) 
        where TEntity : class, IReadOnlyEntity
    {
        return Query<TEntity>().Where(keyCondition);
    }
    
    /// <summary>
    /// Creates a new Query operation builder with LINQ expressions for both key condition and filter.
    /// </summary>
    /// <typeparam name="TEntity">The entity type to query.</typeparam>
    /// <param name="keyCondition">The LINQ expression representing the key condition.</param>
    /// <param name="filterCondition">The LINQ expression representing the filter condition.</param>
    /// <returns>A QueryRequestBuilder configured with both key condition and filter.</returns>
    /// <example>
    /// <code>
    /// // Query with key condition and filter
    /// var results = await index.Query&lt;MyEntity&gt;(
    ///         x => x.Gsi1Pk == "STATUS#ACTIVE",
    ///         x => x.Amount > 100)
    ///     .ToListAsync();
    /// </code>
    /// </example>
    public QueryRequestBuilder<TEntity> Query<TEntity>(
        Expression<Func<TEntity, bool>> keyCondition,
        Expression<Func<TEntity, bool>> filterCondition) 
        where TEntity : class, IReadOnlyEntity
    {
        return Query<TEntity>().Where(keyCondition).WithFilter(filterCondition);
    }
}

/// <summary>
/// Generic DynamoDB index with a default projection type.
/// Provides fluent query operations using the standard Query() method pattern.
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
/// // Query using fluent API - non-generic uses TDefault
/// var results = await table.StatusIndex.Query()
///     .Where("gsi1pk = {0}", "ACTIVE")
///     .ToListAsync();
/// 
/// // Query with explicit type parameter
/// var results = await table.StatusIndex.Query&lt;TransactionSummary&gt;()
///     .Where("gsi1pk = {0}", "ACTIVE")
///     .ToListAsync();
/// 
/// // Query with expression string shorthand
/// var results = await table.StatusIndex.Query("gsi1pk = {0}", "ACTIVE")
///     .ToListAsync();
/// </code>
/// </example>
public class DynamoDbIndex<TDefault> where TDefault : class, IReadOnlyEntity, new()
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
        IDynamoDbTable table,
        string indexName,
        string? projectionExpression = null)
    {
        _innerIndex = new DynamoDbIndex(table, indexName, projectionExpression!);
    }
    


    /// <summary>
    /// Gets the index name.
    /// </summary>
    public string Name => _innerIndex.Name;

    /// <summary>
    /// Creates a new Query operation builder for this index using the default type TDefault.
    /// This is the preferred method when querying with the index's default projection type.
    /// </summary>
    /// <returns>A QueryRequestBuilder configured for this index with TDefault as the entity type.</returns>
    /// <example>
    /// <code>
    /// // Non-generic Query() uses TDefault
    /// var results = await table.StatusIndex.Query()
    ///     .Where("gsi1pk = {0}", "ACTIVE")
    ///     .ToListAsync();
    /// </code>
    /// </example>
    public QueryRequestBuilder<TDefault> Query() => _innerIndex.Query<TDefault>();

    /// <summary>
    /// Creates a new Query operation builder with a key condition expression using the default type TDefault.
    /// Uses format string syntax for parameters: {0}, {1}, etc.
    /// </summary>
    /// <param name="keyConditionExpression">The key condition expression with format placeholders.</param>
    /// <param name="values">The values to substitute into the expression.</param>
    /// <returns>A QueryRequestBuilder configured with the key condition using TDefault as the entity type.</returns>
    /// <example>
    /// <code>
    /// // Non-generic Query with expression uses TDefault
    /// var results = await table.StatusIndex.Query("gsi1pk = {0}", "ACTIVE")
    ///     .ToListAsync();
    /// </code>
    /// </example>
    public QueryRequestBuilder<TDefault> Query(string keyConditionExpression, params object[] values) =>
        _innerIndex.Query<TDefault>(keyConditionExpression, values);

    /// <summary>
    /// Creates a new Query operation builder for this index with an explicit entity type.
    /// Use this when you need to query with a different type than TDefault.
    /// </summary>
    /// <returns>A QueryRequestBuilder configured for this index.</returns>
    /// <example>
    /// <code>
    /// var results = await table.StatusIndex.Query&lt;MyEntity&gt;()
    ///     .Where("gsi1pk = {0}", "ACTIVE")
    ///     .ExecuteAsync();
    /// </code>
    /// </example>
    public QueryRequestBuilder<TEntity> Query<TEntity>() 
        where TEntity : class, IReadOnlyEntity => _innerIndex.Query<TEntity>();
    
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
    public QueryRequestBuilder<TEntity> Query<TEntity>(string keyConditionExpression, params object[] values) 
        where TEntity : class, IReadOnlyEntity => 
        _innerIndex.Query<TEntity>(keyConditionExpression, values);
    
    /// <summary>
    /// Creates a new Query operation builder with a LINQ expression for the key condition using the default type TDefault.
    /// </summary>
    /// <param name="keyCondition">The LINQ expression representing the key condition.</param>
    /// <returns>A QueryRequestBuilder configured with the key condition using TDefault as the entity type.</returns>
    /// <example>
    /// <code>
    /// // Non-generic Query with lambda uses TDefault
    /// var results = await table.StatusIndex.Query(x => x.Status == "ACTIVE")
    ///     .ToListAsync();
    /// </code>
    /// </example>
    public QueryRequestBuilder<TDefault> Query(Expression<Func<TDefault, bool>> keyCondition) =>
        _innerIndex.Query<TDefault>(keyCondition);
    
    /// <summary>
    /// Creates a new Query operation builder with LINQ expressions for both key condition and filter using the default type TDefault.
    /// </summary>
    /// <param name="keyCondition">The LINQ expression representing the key condition.</param>
    /// <param name="filterCondition">The LINQ expression representing the filter condition.</param>
    /// <returns>A QueryRequestBuilder configured with both key condition and filter using TDefault as the entity type.</returns>
    /// <example>
    /// <code>
    /// // Non-generic Query with key condition and filter uses TDefault
    /// var results = await table.StatusIndex.Query(
    ///         x => x.Status == "ACTIVE",
    ///         x => x.Amount > 100)
    ///     .ToListAsync();
    /// </code>
    /// </example>
    public QueryRequestBuilder<TDefault> Query(
        Expression<Func<TDefault, bool>> keyCondition,
        Expression<Func<TDefault, bool>> filterCondition) =>
        _innerIndex.Query<TDefault>(keyCondition, filterCondition);
    
    /// <summary>
    /// Creates a new Query operation builder with a LINQ expression for the key condition.
    /// </summary>
    /// <typeparam name="TEntity">The entity type to query.</typeparam>
    /// <param name="keyCondition">The LINQ expression representing the key condition.</param>
    /// <returns>A QueryRequestBuilder configured with the key condition.</returns>
    /// <example>
    /// <code>
    /// // Lambda expression query with explicit type
    /// var results = await index.Query&lt;MyEntity&gt;(x => x.Gsi1Pk == "STATUS#ACTIVE")
    ///     .ToListAsync();
    /// </code>
    /// </example>
    public QueryRequestBuilder<TEntity> Query<TEntity>(Expression<Func<TEntity, bool>> keyCondition) 
        where TEntity : class, IReadOnlyEntity =>
        _innerIndex.Query<TEntity>(keyCondition);
    
    /// <summary>
    /// Creates a new Query operation builder with LINQ expressions for both key condition and filter.
    /// </summary>
    /// <typeparam name="TEntity">The entity type to query.</typeparam>
    /// <param name="keyCondition">The LINQ expression representing the key condition.</param>
    /// <param name="filterCondition">The LINQ expression representing the filter condition.</param>
    /// <returns>A QueryRequestBuilder configured with both key condition and filter.</returns>
    /// <example>
    /// <code>
    /// // Query with key condition and filter using explicit type
    /// var results = await index.Query&lt;MyEntity&gt;(
    ///         x => x.Gsi1Pk == "STATUS#ACTIVE",
    ///         x => x.Amount > 100)
    ///     .ToListAsync();
    /// </code>
    /// </example>
    public QueryRequestBuilder<TEntity> Query<TEntity>(
        Expression<Func<TEntity, bool>> keyCondition,
        Expression<Func<TEntity, bool>> filterCondition) 
        where TEntity : class, IReadOnlyEntity =>
        _innerIndex.Query<TEntity>(keyCondition, filterCondition);
}