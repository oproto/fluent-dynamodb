using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using Oproto.FluentDynamoDb.Logging;
using Oproto.FluentDynamoDb.Storage;

namespace Oproto.FluentDynamoDb.Requests;

/// <summary>
/// Fluent builder for DynamoDB TransactGetItems operations.
/// Allows you to retrieve multiple items from multiple tables in a single atomic read transaction.
/// All get operations are performed with snapshot isolation at the same point in time.
/// </summary>
/// <example>
/// <code>
/// var response = await new TransactGetItemsRequestBuilder(dynamoDbClient)
///     .Get(userTable, get => get
///         .WithKey("id", userId)
///         .WithProjection("#name, #email")
///         .WithAttribute("#name", "name")
///         .WithAttribute("#email", "email"))
///     .Get(accountTable, get => get
///         .WithKey("id", accountId))
///     .ExecuteAsync();
/// </code>
/// </example>
public class TransactGetItemsRequestBuilder
{
    /// <summary>
    /// Initializes a new instance of the TransactGetItemsRequestBuilder.
    /// </summary>
    /// <param name="dynamoDbClient">The DynamoDB client to use for executing the request.</param>
    /// <param name="logger">Optional logger for operation diagnostics.</param>
    public TransactGetItemsRequestBuilder(IAmazonDynamoDB dynamoDbClient, IDynamoDbLogger? logger = null)
    {
        _dynamoDbClient = dynamoDbClient;
        _logger = logger ?? NoOpLogger.Instance;
    }

    private readonly IAmazonDynamoDB _dynamoDbClient;
    private readonly IDynamoDbLogger _logger;
    private readonly TransactGetItemsRequest _req = new() { TransactItems = new List<TransactGetItem>() };


    public TransactGetItemsRequestBuilder ReturnConsumedCapacity(ReturnConsumedCapacity consumedCapacity)
    {
        _req.ReturnConsumedCapacity = consumedCapacity;
        return this;
    }

    /// <summary>
    /// Adds a Get operation to the transaction.
    /// Get operations retrieve items by their primary key with optional projection expressions.
    /// </summary>
    /// <param name="table">The table to get the item from.</param>
    /// <param name="builderExpression">An action that configures the get operation.</param>
    /// <returns>The builder instance for method chaining.</returns>
    /// <example>
    /// <code>
    /// .Get(userTable, get => get
    ///     .WithKey("id", userId)
    ///     .WithProjection("#name, #email, #status")
    ///     .WithAttribute("#name", "name")
    ///     .WithAttribute("#email", "email")
    ///     .WithAttribute("#status", "status"))
    /// </code>
    /// </example>
    public TransactGetItemsRequestBuilder Get(DynamoDbTableBase table,
        Action<TransactGetItemBuilder> builderExpression)
    {
        TransactGetItemBuilder builder = new(table.Name);
        builderExpression(builder);
        _req.TransactItems.Add(builder.ToGetItem());
        return this;
    }

    public TransactGetItemsRequestBuilder AddTransactItem(TransactGetItem item)
    {
        _req.TransactItems.Add(item);
        return this;
    }

    public TransactGetItemsRequest ToTransactGetItemsRequest()
    {
        return _req;
    }

    /// <summary>
    /// Executes the transaction asynchronously using the configured get operations.
    /// All get operations are performed atomically with snapshot isolation.
    /// </summary>
    /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
    /// <returns>A task representing the asynchronous operation, containing the TransactGetItemsResponse.</returns>
    /// <exception cref="ValidationException">Thrown when the transaction contains invalid operations.</exception>
    /// <exception cref="ProvisionedThroughputExceededException">Thrown when the request rate is too high.</exception>
    /// <exception cref="ResourceNotFoundException">Thrown when a specified table doesn't exist.</exception>
    public async Task<TransactGetItemsResponse> ExecuteAsync(CancellationToken cancellationToken = default)
    {
        var request = ToTransactGetItemsRequest();
        
        #if !DISABLE_DYNAMODB_LOGGING
        _logger?.LogInformation(LogEventIds.ExecutingTransaction,
            "Executing TransactGetItems with {ItemCount} get operations",
            request.TransactItems?.Count ?? 0);
        
        if (_logger?.IsEnabled(LogLevel.Trace) == true && request.TransactItems != null)
        {
            var tableGroups = request.TransactItems
                .GroupBy(i => i.Get?.TableName ?? "Unknown")
                .Select(g => new { Table = g.Key, Count = g.Count() });
            
            foreach (var group in tableGroups)
            {
                _logger.LogTrace(LogEventIds.ExecutingTransaction,
                    "TransactGetItems table {TableName}: {GetCount} operations",
                    group.Table, group.Count);
            }
        }
        #endif
        
        try
        {
            var response = await _dynamoDbClient.TransactGetItemsAsync(request, cancellationToken);
            
            #if !DISABLE_DYNAMODB_LOGGING
            var itemsRetrieved = response.Responses?.Count(r => r.Item != null && r.Item.Count > 0) ?? 0;
            var totalCapacity = response.ConsumedCapacity?.Sum(c => c.CapacityUnits) ?? 0;
            
            _logger?.LogInformation(LogEventIds.OperationComplete,
                "TransactGetItems completed. ItemsRetrieved: {ItemCount}, ConsumedCapacity: {ConsumedCapacity}",
                itemsRetrieved, totalCapacity);
            #endif
            
            return response;
        }
        catch (Exception ex)
        {
            #if !DISABLE_DYNAMODB_LOGGING
            _logger?.LogError(LogEventIds.DynamoDbOperationError, ex,
                "TransactGetItems failed with {ItemCount} operations",
                request.TransactItems?.Count ?? 0);
            #endif
            throw;
        }
    }
}