using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using Oproto.FluentDynamoDb.Logging;

namespace Oproto.FluentDynamoDb.Requests;

/// <summary>
/// Fluent builder for DynamoDB BatchWriteItem operations.
/// Allows putting and deleting multiple items across one or more tables in a single request,
/// improving performance and reducing API calls compared to individual write operations.
/// 
/// Performance Considerations:
/// - BatchWriteItem can process up to 25 put or delete requests per batch
/// - Each item can be up to 400KB in size
/// - Operations are processed in parallel, improving throughput
/// - Unprocessed items may be returned if the request exceeds capacity limits
/// - No conditional writes are supported in batch operations
/// </summary>
/// <example>
/// <code>
/// // Write items to multiple tables
/// var response = await new BatchWriteItemRequestBuilder(client)
///     .WriteToTable("Users", builder => builder
///         .PutItem(new Dictionary&lt;string, AttributeValue&gt; 
///         {
///             ["id"] = new AttributeValue("user1"),
///             ["name"] = new AttributeValue("John Doe")
///         })
///         .DeleteItem("id", "user2"))
///     .WriteToTable("Orders", builder => builder
///         .PutItem(orderData, order => MapOrderToAttributes(order)))
///     .ExecuteAsync();
/// </code>
/// </example>
public class BatchWriteItemRequestBuilder
{
    private readonly IAmazonDynamoDB _dynamoDbClient;
    private readonly IDynamoDbLogger _logger;
    private readonly BatchWriteItemRequest _req = new() { RequestItems = new Dictionary<string, List<WriteRequest>>() };

    /// <summary>
    /// Initializes a new instance of the BatchWriteItemRequestBuilder.
    /// </summary>
    /// <param name="dynamoDbClient">The DynamoDB client to use for executing the request.</param>
    /// <param name="logger">Optional logger for operation diagnostics.</param>
    public BatchWriteItemRequestBuilder(IAmazonDynamoDB dynamoDbClient, IDynamoDbLogger? logger = null)
    {
        _dynamoDbClient = dynamoDbClient;
        _logger = logger ?? NoOpLogger.Instance;
    }

    /// <summary>
    /// Gets the DynamoDB client instance used by this builder.
    /// </summary>
    /// <returns>The IAmazonDynamoDB client instance used by this builder.</returns>
    internal IAmazonDynamoDB GetDynamoDbClient() => _dynamoDbClient;

    /// <summary>
    /// Adds write operations (put or delete) for a specific table.
    /// You can call this method multiple times to write to different tables in the same batch.
    /// You can also call it multiple times for the same table to add more operations.
    /// </summary>
    /// <param name="tableName">The name of the table to write to.</param>
    /// <param name="builderAction">An action to configure the write operations for this table.</param>
    /// <returns>The builder instance for method chaining.</returns>
    /// <example>
    /// <code>
    /// .WriteToTable("Users", builder => builder
    ///     .PutItem(userData)
    ///     .DeleteItem("id", "user123"))
    /// </code>
    /// </example>
    public BatchWriteItemRequestBuilder WriteToTable(string tableName, Action<BatchWriteItemBuilder> builderAction)
    {
        var builder = new BatchWriteItemBuilder(tableName);
        builderAction(builder);

        var writeRequests = builder.ToWriteRequests();
        if (writeRequests.Count > 0)
        {
            if (!_req.RequestItems.ContainsKey(tableName))
            {
                _req.RequestItems[tableName] = new List<WriteRequest>();
            }
            _req.RequestItems[tableName].AddRange(writeRequests);
        }

        return this;
    }

    /// <summary>
    /// Configures the batch write operation to return total consumed capacity information.
    /// Useful for monitoring and optimizing DynamoDB usage costs.
    /// </summary>
    /// <returns>The builder instance for method chaining.</returns>
    public BatchWriteItemRequestBuilder ReturnTotalConsumedCapacity()
    {
        _req.ReturnConsumedCapacity = Amazon.DynamoDBv2.ReturnConsumedCapacity.TOTAL;
        return this;
    }

    /// <summary>
    /// Configures the batch write operation to return consumed capacity information.
    /// </summary>
    /// <param name="consumedCapacity">The level of consumed capacity information to return.</param>
    /// <returns>The builder instance for method chaining.</returns>
    public BatchWriteItemRequestBuilder ReturnConsumedCapacity(ReturnConsumedCapacity consumedCapacity)
    {
        _req.ReturnConsumedCapacity = consumedCapacity;
        return this;
    }

    /// <summary>
    /// Configures the batch write operation to return consumed capacity information for indexes.
    /// </summary>
    /// <returns>The builder instance for method chaining.</returns>
    public BatchWriteItemRequestBuilder ReturnIndexesConsumedCapacity()
    {
        _req.ReturnConsumedCapacity = Amazon.DynamoDBv2.ReturnConsumedCapacity.INDEXES;
        return this;
    }

    /// <summary>
    /// Configures the batch write operation to return item collection metrics.
    /// Only applicable for tables with local secondary indexes.
    /// </summary>
    /// <returns>The builder instance for method chaining.</returns>
    public BatchWriteItemRequestBuilder ReturnItemCollectionMetrics()
    {
        _req.ReturnItemCollectionMetrics = Amazon.DynamoDBv2.ReturnItemCollectionMetrics.SIZE;
        return this;
    }

    /// <summary>
    /// Configures the batch write operation to return item collection metrics.
    /// </summary>
    /// <param name="itemCollectionMetrics">The level of item collection metrics to return.</param>
    /// <returns>The builder instance for method chaining.</returns>
    public BatchWriteItemRequestBuilder ReturnItemCollectionMetrics(ReturnItemCollectionMetrics itemCollectionMetrics)
    {
        _req.ReturnItemCollectionMetrics = itemCollectionMetrics;
        return this;
    }

    /// <summary>
    /// Builds and returns the configured BatchWriteItemRequest.
    /// </summary>
    /// <returns>A configured BatchWriteItemRequest ready for execution.</returns>
    public BatchWriteItemRequest ToBatchWriteItemRequest()
    {
        return _req;
    }

    /// <summary>
    /// Executes the batch write operation asynchronously and returns the raw AWS SDK response (Advanced API).
    /// This method does NOT populate DynamoDbOperationContext. Use the Primary API extension methods for context population.
    /// 
    /// Note: Check the UnprocessedItems property in the response to handle any items
    /// that couldn't be processed due to capacity limits or other constraints.
    /// Consider implementing exponential backoff retry logic for unprocessed items.
    /// </summary>
    /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
    /// <returns>A task representing the asynchronous operation, containing the batch write response.</returns>
    /// <exception cref="ResourceNotFoundException">Thrown when one of the specified tables doesn't exist.</exception>
    public async Task<BatchWriteItemResponse> ToDynamoDbResponseAsync(CancellationToken cancellationToken = default)
    {
        var request = ToBatchWriteItemRequest();
        
        #if !DISABLE_DYNAMODB_LOGGING
        var totalRequests = request.RequestItems?.Sum(kvp => kvp.Value?.Count ?? 0) ?? 0;
        _logger?.LogInformation(LogEventIds.ExecutingPutItem,
            "Executing BatchWriteItem across {TableCount} tables with {TotalRequests} operations",
            request.RequestItems?.Count ?? 0,
            totalRequests);
        
        if (_logger?.IsEnabled(LogLevel.Trace) == true && request.RequestItems != null)
        {
            foreach (var table in request.RequestItems.Keys)
            {
                var putCount = request.RequestItems[table].Count(r => r.PutRequest != null);
                var deleteCount = request.RequestItems[table].Count(r => r.DeleteRequest != null);
                _logger.LogTrace(LogEventIds.ExecutingPutItem,
                    "BatchWriteItem table {TableName}: Put={PutCount}, Delete={DeleteCount}",
                    table, putCount, deleteCount);
            }
        }
        #endif
        
        try
        {
            var response = await _dynamoDbClient.BatchWriteItemAsync(request, cancellationToken);
            
            #if !DISABLE_DYNAMODB_LOGGING
            var unprocessedCount = response.UnprocessedItems?.Sum(kvp => kvp.Value?.Count ?? 0) ?? 0;
            var totalCapacity = response.ConsumedCapacity?.Sum(c => c.CapacityUnits) ?? 0;
            
            _logger?.LogInformation(LogEventIds.OperationComplete,
                "BatchWriteItem completed. UnprocessedItems: {UnprocessedCount}, ConsumedCapacity: {ConsumedCapacity}",
                unprocessedCount, totalCapacity);
            #endif
            
            return response;
        }
        catch (Exception ex)
        {
            #if !DISABLE_DYNAMODB_LOGGING
            _logger?.LogError(LogEventIds.DynamoDbOperationError, ex,
                "BatchWriteItem failed across {TableCount} tables",
                request.RequestItems?.Count ?? 0);
            #endif
            throw;
        }
    }
}