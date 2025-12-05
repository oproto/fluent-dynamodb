using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using Oproto.FluentDynamoDb.Logging;
using Oproto.FluentDynamoDb.Requests.Interfaces;

namespace Oproto.FluentDynamoDb.Requests;

/// <summary>
/// Fluent builder for DynamoDB Scan operations.
/// 
/// WARNING: Scan operations read every item in a table or index and can be very expensive.
/// Use Query operations instead whenever possible. Scan should only be used for:
/// - Data migration or ETL processes
/// - Analytics on small tables
/// - Operations where you truly need to examine every item
/// 
/// Performance Considerations:
/// - Scan operations consume read capacity for every item examined, not just returned items
/// - Large tables will require multiple scan operations due to 1MB response limits
/// - Consider using parallel scans for large tables to improve throughput
/// - Always use filter expressions to reduce data transfer, though this doesn't reduce consumed capacity
/// </summary>
/// <typeparam name="TEntity">The entity type being scanned.</typeparam>
/// <example>
/// <code>
/// // Basic scan with filter
/// var response = await table.Scan&lt;Transaction&gt;()
///     .WithFilter("#status = :active")
///     .WithAttribute("#status", "status")
///     .WithValue(":active", "ACTIVE")
///     .Take(100)
///     .ExecuteAsync();
/// 
/// // Parallel scan for large tables
/// var segment1Task = table.Scan&lt;Transaction&gt;()
///     .WithSegment(0, 4)  // Segment 0 of 4 total segments
///     .ExecuteAsync();
/// </code>
/// </example>
public class ScanRequestBuilder<TEntity> :
    IWithAttributeNames<ScanRequestBuilder<TEntity>>, IWithAttributeValues<ScanRequestBuilder<TEntity>>, IWithFilterExpression<ScanRequestBuilder<TEntity>>, IHasDynamoDbClient
    where TEntity : class
{
    /// <summary>
    /// Initializes a new instance of the ScanRequestBuilder.
    /// </summary>
    /// <param name="dynamoDbClient">The DynamoDB client to use for executing the request.</param>
    /// <param name="options">Configuration options including logger, hydrator registry, etc. If null, uses sensible defaults.</param>
    public ScanRequestBuilder(IAmazonDynamoDB dynamoDbClient, FluentDynamoDbOptions? options = null)
    {
        _dynamoDbClient = dynamoDbClient;
        _options = options ?? new FluentDynamoDbOptions();
        _logger = _options.Logger;
    }

    private ScanRequest _req = new ScanRequest() { ConsistentRead = false };
    private IAmazonDynamoDB _dynamoDbClient;
    private readonly IDynamoDbLogger _logger;
    private readonly FluentDynamoDbOptions _options;
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
    public IAmazonDynamoDB GetDynamoDbClient() => _dynamoDbClient;

    /// <summary>
    /// Gets the FluentDynamoDbOptions for extension method access.
    /// This is used by Primary API extension methods to access the hydrator registry.
    /// </summary>
    /// <returns>The FluentDynamoDbOptions instance used by this builder.</returns>
    public FluentDynamoDbOptions GetOptions() => _options;

    /// <summary>
    /// Replaces the DynamoDB client used for executing this request.
    /// Used for tenant-specific STS credential scenarios where different clients
    /// are needed for different tenants or security contexts.
    /// </summary>
    /// <param name="client">The scoped DynamoDB client to use.</param>
    /// <returns>This builder instance for method chaining.</returns>
    public ScanRequestBuilder<TEntity> WithClient(IAmazonDynamoDB client)
    {
        _dynamoDbClient = client;
        return this;
    }

    /// <summary>
    /// Sets the filter expression on the builder.
    /// If a filter expression already exists, combines them with AND logic.
    /// </summary>
    /// <param name="expression">The processed filter expression to set.</param>
    /// <returns>The builder instance for method chaining.</returns>
    public ScanRequestBuilder<TEntity> SetFilterExpression(string expression)
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
    public ScanRequestBuilder<TEntity> Self => this;

    /// <summary>
    /// Specifies the table name for the scan operation.
    /// </summary>
    /// <param name="tableName">The name of the DynamoDB table to scan.</param>
    /// <returns>The builder instance for method chaining.</returns>
    public ScanRequestBuilder<TEntity> ForTable(string tableName)
    {
        _req.TableName = tableName;
        return this;
    }



    /// <summary>
    /// Specifies which attributes to retrieve from each item.
    /// This reduces network traffic and can improve performance.
    /// </summary>
    /// <param name="projectionExpression">The projection expression specifying which attributes to retrieve.</param>
    /// <returns>The builder instance for method chaining.</returns>
    /// <example>
    /// <code>
    /// .WithProjection("#id, #name, #status")
    /// </code>
    /// </example>
    public ScanRequestBuilder<TEntity> WithProjection(string projectionExpression)
    {
        _req.ProjectionExpression = projectionExpression;
        _req.Select = Select.SPECIFIC_ATTRIBUTES;
        return this;
    }

    /// <summary>
    /// Specifies a secondary index to scan instead of the main table.
    /// </summary>
    /// <param name="indexName">The name of the global or local secondary index to scan.</param>
    /// <returns>The builder instance for method chaining.</returns>
    public ScanRequestBuilder<TEntity> UsingIndex(string indexName)
    {
        _req.IndexName = indexName;
        return this;
    }

    /// <summary>
    /// Limits the number of items examined during the scan operation.
    /// Note: This limits items examined, not items returned. Filtering may result in fewer returned items.
    /// </summary>
    /// <param name="limit">The maximum number of items to examine.</param>
    /// <returns>The builder instance for method chaining.</returns>
    public ScanRequestBuilder<TEntity> Take(int limit)
    {
        _req.Limit = limit;
        return this;
    }

    /// <summary>
    /// Specifies where to start the scan operation for pagination.
    /// Use the LastEvaluatedKey from a previous scan response.
    /// </summary>
    /// <param name="exclusiveStartKey">The key to start scanning from (exclusive).</param>
    /// <returns>The builder instance for method chaining.</returns>
    public ScanRequestBuilder<TEntity> StartAt(Dictionary<string, AttributeValue> exclusiveStartKey)
    {
        _req.ExclusiveStartKey = exclusiveStartKey;
        return this;
    }

    /// <summary>
    /// Enables strongly consistent reads for the scan operation.
    /// Note: Consistent reads consume twice the read capacity and are not supported on global secondary indexes.
    /// </summary>
    /// <returns>The builder instance for method chaining.</returns>
    public ScanRequestBuilder<TEntity> UsingConsistentRead()
    {
        _req.ConsistentRead = true;
        return this;
    }

    /// <summary>
    /// Configures parallel scanning by specifying which segment this scan should process.
    /// Use this to improve throughput on large tables by running multiple scan operations in parallel.
    /// </summary>
    /// <param name="segment">The segment number for this scan (0-based).</param>
    /// <param name="totalSegments">The total number of segments to divide the table into.</param>
    /// <returns>The builder instance for method chaining.</returns>
    /// <example>
    /// <code>
    /// // Scan segment 0 of 4 total segments
    /// .WithSegment(0, 4)
    /// </code>
    /// </example>
    public ScanRequestBuilder<TEntity> WithSegment(int segment, int totalSegments)
    {
        _req.Segment = segment;
        _req.TotalSegments = totalSegments;
        return this;
    }

    /// <summary>
    /// Configures the scan to return only the count of items, not the items themselves.
    /// This is more efficient when you only need to know how many items match your criteria.
    /// </summary>
    /// <returns>The builder instance for method chaining.</returns>
    public ScanRequestBuilder<TEntity> Count()
    {
        _req.Select = Select.COUNT;
        return this;
    }

    /// <summary>
    /// Configures the scan operation to return total consumed capacity information.
    /// Useful for monitoring and optimizing DynamoDB usage costs.
    /// </summary>
    /// <returns>The builder instance for method chaining.</returns>
    public ScanRequestBuilder<TEntity> ReturnTotalConsumedCapacity()
    {
        _req.ReturnConsumedCapacity = Amazon.DynamoDBv2.ReturnConsumedCapacity.TOTAL;
        return this;
    }

    /// <summary>
    /// Configures the scan operation to return consumed capacity information for indexes.
    /// </summary>
    /// <returns>The builder instance for method chaining.</returns>
    public ScanRequestBuilder<TEntity> ReturnIndexConsumedCapacity()
    {
        _req.ReturnConsumedCapacity = Amazon.DynamoDBv2.ReturnConsumedCapacity.INDEXES;
        return this;
    }

    /// <summary>
    /// Configures the scan operation to return consumed capacity information.
    /// </summary>
    /// <param name="consumedCapacity">The level of consumed capacity information to return.</param>
    /// <returns>The builder instance for method chaining.</returns>
    public ScanRequestBuilder<TEntity> ReturnConsumedCapacity(ReturnConsumedCapacity consumedCapacity)
    {
        _req.ReturnConsumedCapacity = consumedCapacity;
        return this;
    }





    /// <summary>
    /// Builds and returns the configured ScanRequest.
    /// </summary>
    /// <returns>A configured ScanRequest ready for execution.</returns>
    public ScanRequest ToScanRequest()
    {
        if (_attrN.AttributeNames.Count > 0)
        {
            _req.ExpressionAttributeNames = _attrN.AttributeNames;
        }
        
        if (_attrV.AttributeValues.Count > 0)
        {
            _req.ExpressionAttributeValues = _attrV.AttributeValues;
        }
        else if (_req.ExpressionAttributeValues == null)
        {
            _req.ExpressionAttributeValues = new Dictionary<string, AttributeValue>();
        }
        return _req;
    }

    /// <summary>
    /// Executes the Scan operation asynchronously and returns the raw AWS SDK ScanResponse.
    /// This is the Advanced API method that does NOT populate DynamoDbOperationContext.
    /// For most use cases, prefer the Primary API extension methods like ToListAsync() which populate context.
    /// 
    /// Performance Warning: This operation can be expensive on large tables.
    /// Consider the following best practices:
    /// - Use Query instead of Scan whenever possible
    /// - Implement pagination for large result sets
    /// - Use parallel scanning for better throughput on large tables
    /// - Monitor consumed capacity to avoid unexpected costs
    /// </summary>
    /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
    /// <returns>A task representing the asynchronous operation, containing the raw ScanResponse from AWS SDK.</returns>
    /// <exception cref="ResourceNotFoundException">Thrown when the specified table or index doesn't exist.</exception>
    public async Task<ScanResponse> ToDynamoDbResponseAsync(CancellationToken cancellationToken = default)
    {
        var request = ToScanRequest();
        
        #if !DISABLE_DYNAMODB_LOGGING
        _logger?.LogInformation(LogEventIds.ExecutingQuery,
            "Executing Scan on table {TableName}. Index: {IndexName}, Filter: {FilterExpression}, Segment: {Segment}/{TotalSegments}",
            request.TableName ?? "Unknown", 
            request.IndexName ?? "None", 
            request.FilterExpression ?? "None",
            (object?)request.Segment ?? "N/A",
            (object?)request.TotalSegments ?? "N/A");
        
        if (_logger?.IsEnabled(LogLevel.Trace) == true && _attrV.AttributeValues.Count > 0)
        {
            _logger.LogTrace(LogEventIds.ExecutingQuery,
                "Scan parameters: {ParameterCount} values",
                _attrV.AttributeValues.Count);
        }
        #endif
        
        try
        {
            var response = await _dynamoDbClient.ScanAsync(request, cancellationToken);
            
            #if !DISABLE_DYNAMODB_LOGGING
#pragma warning disable CS8601 // Possible null reference assignment - boxing value types to object[]
            _logger?.LogInformation(LogEventIds.OperationComplete,
                "Scan completed. ItemCount: {ItemCount}, ScannedCount: {ScannedCount}, ConsumedCapacity: {ConsumedCapacity}",
                new object[] { response.Count, response.ScannedCount, response.ConsumedCapacity?.CapacityUnits ?? 0 });
#pragma warning restore CS8601
            #endif
            
            return response;
        }
        catch (Exception ex)
        {
            #if !DISABLE_DYNAMODB_LOGGING
            _logger?.LogError(LogEventIds.DynamoDbOperationError, ex,
                "Scan failed on table {TableName}",
                request.TableName ?? "Unknown");
            #endif
            throw;
        }
    }
}