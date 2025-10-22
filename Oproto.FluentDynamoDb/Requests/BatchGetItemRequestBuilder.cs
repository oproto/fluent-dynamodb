using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using Oproto.FluentDynamoDb.Logging;

namespace Oproto.FluentDynamoDb.Requests;

/// <summary>
/// Fluent builder for DynamoDB BatchGetItem operations.
/// Allows retrieving multiple items from one or more tables in a single request,
/// improving performance and reducing API calls compared to individual GetItem operations.
/// 
/// Performance Considerations:
/// - BatchGetItem can retrieve up to 100 items or 16MB of data per request
/// - Items are retrieved in parallel, improving throughput
/// - Unprocessed keys may be returned if the request exceeds capacity limits
/// - Use consistent reads carefully as they consume twice the read capacity
/// </summary>
/// <example>
/// <code>
/// // Get items from multiple tables
/// var response = await new BatchGetItemRequestBuilder(client)
///     .GetFromTable("Users", builder => builder
///         .WithKey("id", "user1")
///         .WithKey("id", "user2")
///         .WithProjection("#name, #email")
///         .WithAttribute("#name", "name")
///         .WithAttribute("#email", "email"))
///     .GetFromTable("Orders", builder => builder
///         .WithKey("orderId", "order123")
///         .UsingConsistentRead())
///     .ExecuteAsync();
/// </code>
/// </example>
public class BatchGetItemRequestBuilder
{
    private readonly IAmazonDynamoDB _dynamoDbClient;
    private readonly IDynamoDbLogger _logger;
    private readonly BatchGetItemRequest _req = new();

    /// <summary>
    /// Initializes a new instance of the BatchGetItemRequestBuilder.
    /// </summary>
    /// <param name="dynamoDbClient">The DynamoDB client to use for executing the request.</param>
    /// <param name="logger">Optional logger for operation diagnostics.</param>
    public BatchGetItemRequestBuilder(IAmazonDynamoDB dynamoDbClient, IDynamoDbLogger? logger = null)
    {
        _dynamoDbClient = dynamoDbClient;
        _logger = logger ?? NoOpLogger.Instance;
        _req.RequestItems = new Dictionary<string, KeysAndAttributes>();
    }

    /// <summary>
    /// Adds items to retrieve from a specific table.
    /// You can call this method multiple times to retrieve items from different tables in the same batch.
    /// </summary>
    /// <param name="tableName">The name of the table to retrieve items from.</param>
    /// <param name="builderAction">An action to configure which items to retrieve from this table.</param>
    /// <returns>The builder instance for method chaining.</returns>
    /// <example>
    /// <code>
    /// .GetFromTable("Users", builder => builder
    ///     .WithKey("id", "user1")
    ///     .WithKey("id", "user2")
    ///     .WithProjection("#name, #email"))
    /// </code>
    /// </example>
    public BatchGetItemRequestBuilder GetFromTable(string tableName, Action<BatchGetItemBuilder> builderAction)
    {
        var builder = new BatchGetItemBuilder(tableName);
        builderAction(builder);
        _req.RequestItems[tableName] = builder.ToKeysAndAttributes();
        return this;
    }

    /// <summary>
    /// Configures the batch get operation to return consumed capacity information.
    /// </summary>
    /// <param name="consumedCapacity">The level of consumed capacity information to return.</param>
    /// <returns>The builder instance for method chaining.</returns>
    public BatchGetItemRequestBuilder ReturnConsumedCapacity(ReturnConsumedCapacity consumedCapacity)
    {
        _req.ReturnConsumedCapacity = consumedCapacity;
        return this;
    }

    /// <summary>
    /// Builds and returns the configured BatchGetItemRequest.
    /// </summary>
    /// <returns>A configured BatchGetItemRequest ready for execution.</returns>
    public BatchGetItemRequest ToBatchGetItemRequest()
    {
        return _req;
    }

    /// <summary>
    /// Executes the batch get operation asynchronously.
    /// 
    /// Note: Check the UnprocessedKeys property in the response to handle any items
    /// that couldn't be processed due to capacity limits or other constraints.
    /// </summary>
    /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
    /// <returns>A task representing the asynchronous operation, containing the batch get response.</returns>
    /// <exception cref="ResourceNotFoundException">Thrown when one of the specified tables doesn't exist.</exception>
    public async Task<BatchGetItemResponse> ExecuteAsync(CancellationToken cancellationToken = default)
    {
        var request = ToBatchGetItemRequest();
        
        #if !DISABLE_DYNAMODB_LOGGING
        var totalKeys = request.RequestItems?.Sum(kvp => kvp.Value.Keys?.Count ?? 0) ?? 0;
        _logger?.LogInformation(LogEventIds.ExecutingGetItem,
            "Executing BatchGetItem across {TableCount} tables with {TotalKeys} keys",
            request.RequestItems?.Count ?? 0,
            totalKeys);
        
        if (_logger?.IsEnabled(LogLevel.Trace) == true && request.RequestItems != null)
        {
            foreach (var table in request.RequestItems.Keys)
            {
                _logger.LogTrace(LogEventIds.ExecutingGetItem,
                    "BatchGetItem table {TableName}: {KeyCount} keys",
                    table, request.RequestItems[table].Keys?.Count ?? 0);
            }
        }
        #endif
        
        try
        {
            var response = await _dynamoDbClient.BatchGetItemAsync(request, cancellationToken);
            
            #if !DISABLE_DYNAMODB_LOGGING
            var totalItems = response.Responses?.Sum(kvp => kvp.Value?.Count ?? 0) ?? 0;
            var unprocessedCount = response.UnprocessedKeys?.Sum(kvp => kvp.Value.Keys?.Count ?? 0) ?? 0;
            var totalCapacity = response.ConsumedCapacity?.Sum(c => c.CapacityUnits) ?? 0;
            
            _logger?.LogInformation(LogEventIds.OperationComplete,
                "BatchGetItem completed. ItemsRetrieved: {ItemCount}, UnprocessedKeys: {UnprocessedCount}, ConsumedCapacity: {ConsumedCapacity}",
                totalItems, unprocessedCount, totalCapacity);
            #endif
            
            return response;
        }
        catch (Exception ex)
        {
            #if !DISABLE_DYNAMODB_LOGGING
            _logger?.LogError(LogEventIds.DynamoDbOperationError, ex,
                "BatchGetItem failed across {TableCount} tables",
                request.RequestItems?.Count ?? 0);
            #endif
            throw;
        }
    }
}