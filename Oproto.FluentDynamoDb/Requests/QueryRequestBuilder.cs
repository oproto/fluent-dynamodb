using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using Oproto.FluentDynamoDb.Logging;
using Oproto.FluentDynamoDb.Requests.Interfaces;

namespace Oproto.FluentDynamoDb.Requests;

/// <summary>
/// Fluent builder for DynamoDB Query operations.
/// Query operations efficiently retrieve items using the primary key and optional sort key conditions.
/// This is the preferred method for retrieving multiple items when you know the primary key.
/// Query operations are much more efficient than Scan operations and should be used whenever possible.
/// </summary>
/// <typeparam name="TEntity">The entity type being queried.</typeparam>
/// <example>
/// <code>
/// // Query items with a specific primary key
/// var response = await table.Query&lt;Transaction&gt;()
///     .Where("pk = :pk")
///     .WithValue(":pk", "USER#123")
///     .ExecuteAsync();
/// 
/// // Query with sort key condition and filter
/// var response = await table.Query&lt;Transaction&gt;()
///     .Where("pk = :pk AND begins_with(sk, :prefix)")
///     .WithFilter("#status = :status")
///     .WithValue(":pk", "USER#123")
///     .WithValue(":prefix", "ORDER#")
///     .WithValue(":status", "ACTIVE")
///     .WithAttribute("#status", "status")
///     .Take(10)
///     .ExecuteAsync();
/// </code>
/// </example>
public class QueryRequestBuilder<TEntity> :
    IWithAttributeNames<QueryRequestBuilder<TEntity>>, IWithConditionExpression<QueryRequestBuilder<TEntity>>, IWithAttributeValues<QueryRequestBuilder<TEntity>>, IWithFilterExpression<QueryRequestBuilder<TEntity>>
    where TEntity : class
{
    /// <summary>
    /// Initializes a new instance of the QueryRequestBuilder.
    /// </summary>
    /// <param name="dynamoDbClient">The DynamoDB client to use for executing the request.</param>
    /// <param name="logger">Optional logger for operation diagnostics.</param>
    public QueryRequestBuilder(IAmazonDynamoDB dynamoDbClient, IDynamoDbLogger? logger = null)
    {
        _dynamoDbClient = dynamoDbClient;
        _logger = logger ?? NoOpLogger.Instance;
    }

    private QueryRequest _req = new QueryRequest() { ExclusiveStartKey = new Dictionary<string, AttributeValue>() };
    private readonly IAmazonDynamoDB _dynamoDbClient;
    private readonly IDynamoDbLogger _logger;
    private readonly AttributeValueInternal _attrV = new AttributeValueInternal();
    private readonly AttributeNameInternal _attrN = new AttributeNameInternal();

    /// <summary>
    /// Gets the internal attribute value helper for extension method access.
    /// </summary>
    /// <returns>The AttributeValueInternal instance used by this builder.</returns>
    public AttributeValueInternal GetAttributeValueHelper() => _attrV;

    /// <summary>
    /// Gets the internal attribute name helper for extension method access.
    /// </summary>
    /// <returns>The AttributeNameInternal instance used by this builder.</returns>
    public AttributeNameInternal GetAttributeNameHelper() => _attrN;

    /// <summary>
    /// Gets the DynamoDB client for extension method access.
    /// This is used by Primary API extension methods to call AWS SDK directly.
    /// </summary>
    /// <returns>The IAmazonDynamoDB client instance used by this builder.</returns>
    internal IAmazonDynamoDB GetDynamoDbClient() => _dynamoDbClient;

    /// <summary>
    /// Sets the condition expression on the builder.
    /// If a condition expression already exists, combines them with AND logic.
    /// </summary>
    /// <param name="expression">The processed condition expression to set.</param>
    /// <returns>The builder instance for method chaining.</returns>
    public QueryRequestBuilder<TEntity> SetConditionExpression(string expression)
    {
        if (string.IsNullOrEmpty(_req.KeyConditionExpression))
        {
            _req.KeyConditionExpression = expression;
        }
        else
        {
            _req.KeyConditionExpression = $"({_req.KeyConditionExpression}) AND ({expression})";
        }
        return this;
    }

    /// <summary>
    /// Sets the filter expression on the builder.
    /// If a filter expression already exists, combines them with AND logic.
    /// </summary>
    /// <param name="expression">The processed filter expression to set.</param>
    /// <returns>The builder instance for method chaining.</returns>
    public QueryRequestBuilder<TEntity> SetFilterExpression(string expression)
    {
        if (string.IsNullOrEmpty(_req.FilterExpression))
        {
            _req.FilterExpression = expression;
        }
        else
        {
            _req.FilterExpression = $"({_req.FilterExpression}) AND ({expression})";
        }
        return this;
    }

    /// <summary>
    /// Gets the builder instance for method chaining.
    /// </summary>
    public QueryRequestBuilder<TEntity> Self => this;

    /// <summary>
    /// Specifies the name of the table to query.
    /// </summary>
    /// <param name="tableName">The name of the DynamoDB table.</param>
    /// <returns>The builder instance for method chaining.</returns>
    public QueryRequestBuilder<TEntity> ForTable(string tableName)
    {
        _req.TableName = tableName;
        return this;
    }

    /// <summary>
    /// Limits the number of items to evaluate (not necessarily the number of items returned).
    /// DynamoDB will stop evaluating items once this limit is reached, even if the filter expression
    /// hasn't been applied to all items. Use this for pagination and to control consumed capacity.
    /// </summary>
    /// <param name="limit">The maximum number of items to evaluate.</param>
    /// <returns>The builder instance for method chaining.</returns>
    public QueryRequestBuilder<TEntity> Take(int limit)
    {
        _req.Limit = limit;
        return this;
    }

    /// <summary>
    /// Configures the query to return only the count of items that match the query conditions,
    /// rather than the items themselves. This is more efficient when you only need to know
    /// how many items match your criteria.
    /// </summary>
    /// <returns>The builder instance for method chaining.</returns>
    public QueryRequestBuilder<TEntity> Count()
    {
        _req.Select = Select.COUNT;
        return this;
    }

    /// <summary>
    /// Enables strongly consistent reads for this query operation.
    /// By default, DynamoDB uses eventually consistent reads which are faster and consume less capacity,
    /// but may not reflect the most recent write operations. Use consistent reads when you need
    /// the most up-to-date data, but be aware this consumes twice the read capacity.
    /// Note: Consistent reads are not supported on Global Secondary Indexes.
    /// </summary>
    /// <returns>The builder instance for method chaining.</returns>
    public QueryRequestBuilder<TEntity> UsingConsistentRead()
    {
        _req.ConsistentRead = true;
        return this;
    }



    /// <summary>
    /// Specifies a Global Secondary Index (GSI) or Local Secondary Index (LSI) to query.
    /// When querying an index, the key condition must use the index's key schema.
    /// </summary>
    /// <param name="indexName">The name of the index to query.</param>
    /// <returns>The builder instance for method chaining.</returns>
    public QueryRequestBuilder<TEntity> UsingIndex(string indexName)
    {
        _req.IndexName = indexName;
        _indexName = indexName;
        return this;
    }
    
    private string? _indexName;
    
    /// <summary>
    /// Gets the name of the index being queried, if any.
    /// Used by expression translation to validate GSI key properties.
    /// </summary>
    /// <returns>The index name, or null if querying the main table.</returns>
    public string? GetIndexName() => _indexName;

    /// <summary>
    /// Specifies which attributes to retrieve using a projection expression.
    /// This can significantly reduce the amount of data transferred and improve performance.
    /// Use attribute name parameters (e.g., "#name") for reserved words.
    /// </summary>
    /// <param name="projectionExpression">A string that identifies the attributes to retrieve.</param>
    /// <returns>The builder instance for method chaining.</returns>
    /// <example>
    /// <code>
    /// .WithProjection("#name, email, #status, createdAt")
    /// .WithAttribute("#name", "name")
    /// .WithAttribute("#status", "status")
    /// </code>
    /// </example>
    public QueryRequestBuilder<TEntity> WithProjection(string projectionExpression)
    {
        _req.ProjectionExpression = projectionExpression;
        _req.Select = Select.SPECIFIC_ATTRIBUTES;
        return this;
    }

    /// <summary>
    /// Specifies the starting point for pagination by providing the last evaluated key from a previous query.
    /// This is used to continue querying from where the previous operation left off.
    /// </summary>
    /// <param name="exclusiveStartKey">The primary key of the item where the previous query stopped.</param>
    /// <returns>The builder instance for method chaining.</returns>
    public QueryRequestBuilder<TEntity> StartAt(Dictionary<string, AttributeValue> exclusiveStartKey)
    {
        _req.ExclusiveStartKey = exclusiveStartKey;
        return this;
    }







    /// <summary>
    /// Configures the response to include total consumed capacity information.
    /// This is useful for monitoring and optimizing read capacity usage across tables and indexes.
    /// </summary>
    /// <returns>The builder instance for method chaining.</returns>
    public QueryRequestBuilder<TEntity> ReturnTotalConsumedCapacity()
    {
        _req.ReturnConsumedCapacity = Amazon.DynamoDBv2.ReturnConsumedCapacity.TOTAL;
        return this;
    }

    /// <summary>
    /// Configures the response to include consumed capacity information for indexes only.
    /// This is useful when querying indexes and you want to monitor index-specific capacity usage.
    /// </summary>
    /// <returns>The builder instance for method chaining.</returns>
    public QueryRequestBuilder<TEntity> ReturnIndexConsumedCapacity()
    {
        _req.ReturnConsumedCapacity = Amazon.DynamoDBv2.ReturnConsumedCapacity.INDEXES;
        return this;
    }

    /// <summary>
    /// Configures the level of consumed capacity information to return in the response.
    /// </summary>
    /// <param name="consumedCapacity">The level of consumed capacity information to return.</param>
    /// <returns>The builder instance for method chaining.</returns>
    public QueryRequestBuilder<TEntity> ReturnConsumedCapacity(ReturnConsumedCapacity consumedCapacity)
    {
        _req.ReturnConsumedCapacity = consumedCapacity;
        return this;
    }

    /// <summary>
    /// Configures the query to return items in ascending order by sort key.
    /// This is the default behavior for Query operations.
    /// </summary>
    /// <returns>The builder instance for method chaining.</returns>
    public QueryRequestBuilder<TEntity> OrderAscending()
    {
        _req.ScanIndexForward = true;
        return this;
    }

    /// <summary>
    /// Configures the query to return items in descending order by sort key.
    /// This is useful when you want the most recent items first (assuming sort key represents time).
    /// </summary>
    /// <returns>The builder instance for method chaining.</returns>
    public QueryRequestBuilder<TEntity> OrderDescending()
    {
        _req.ScanIndexForward = false;
        return this;
    }

    /// <summary>
    /// Configures the sort order for query results.
    /// </summary>
    /// <param name="ascending">True for ascending order (default), false for descending order.</param>
    /// <returns>The builder instance for method chaining.</returns>
    public QueryRequestBuilder<TEntity> ScanIndexForward(bool ascending = true)
    {
        _req.ScanIndexForward = ascending;
        return this;
    }

    /// <summary>
    /// Builds and returns the configured QueryRequest.
    /// This method is typically used for advanced scenarios where you need direct access to the request object.
    /// </summary>
    /// <returns>A configured QueryRequest ready for execution.</returns>
    public QueryRequest ToQueryRequest()
    {
        if (_attrN.AttributeNames.Count > 0)
        {
            _req.ExpressionAttributeNames = _attrN.AttributeNames;
        }
        if (_attrV.AttributeValues.Count > 0)
        {
            _req.ExpressionAttributeValues = _attrV.AttributeValues;
        }
        return _req;
    }

    /// <summary>
    /// Executes the Query operation asynchronously and returns the raw AWS SDK QueryResponse.
    /// This is the Advanced API method that does NOT populate DynamoDbOperationContext.
    /// For most use cases, prefer the Primary API extension methods like ToListAsync() which populate context.
    /// Query operations are efficient and should be preferred over Scan operations whenever possible.
    /// </summary>
    /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
    /// <returns>A task representing the asynchronous operation, containing the raw QueryResponse from AWS SDK.</returns>
    /// <exception cref="ResourceNotFoundException">Thrown when the specified table or index doesn't exist.</exception>
    /// <exception cref="ProvisionedThroughputExceededException">Thrown when the request rate is too high.</exception>
    /// <exception cref="ValidationException">Thrown when the key condition expression is invalid.</exception>
    public async Task<QueryResponse> ToDynamoDbResponseAsync(CancellationToken cancellationToken = default)
    {
        var request = ToQueryRequest();
        
        #if !DISABLE_DYNAMODB_LOGGING
        _logger?.LogInformation(LogEventIds.ExecutingQuery,
            "Executing Query on table {TableName}. Index: {IndexName}, KeyCondition: {KeyCondition}, Filter: {FilterExpression}",
            request.TableName ?? "Unknown", 
            request.IndexName ?? "None", 
            request.KeyConditionExpression ?? "None", 
            request.FilterExpression ?? "None");
        
        if (_logger?.IsEnabled(LogLevel.Trace) == true && _attrV.AttributeValues.Count > 0)
        {
            _logger.LogTrace(LogEventIds.ExecutingQuery,
                "Query parameters: {ParameterCount} values",
                _attrV.AttributeValues.Count);
        }
        #endif
        
        try
        {
            var response = await _dynamoDbClient.QueryAsync(request, cancellationToken);
            
            #if !DISABLE_DYNAMODB_LOGGING
            _logger?.LogInformation(LogEventIds.OperationComplete,
                "Query completed. ItemCount: {ItemCount}, ConsumedCapacity: {ConsumedCapacity}",
                response.Count, 
                response.ConsumedCapacity?.CapacityUnits ?? 0);
            #endif
            
            return response;
        }
        catch (Exception ex)
        {
            #if !DISABLE_DYNAMODB_LOGGING
            _logger?.LogError(LogEventIds.DynamoDbOperationError, ex,
                "Query failed on table {TableName}",
                request.TableName ?? "Unknown");
            #endif
            throw;
        }
    }
}