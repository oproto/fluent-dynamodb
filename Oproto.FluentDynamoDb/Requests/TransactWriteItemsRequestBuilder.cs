using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using Oproto.FluentDynamoDb.Logging;
using Oproto.FluentDynamoDb.Storage;

namespace Oproto.FluentDynamoDb.Requests;

/// <summary>
/// Fluent builder for DynamoDB TransactWriteItems operations.
/// Allows you to perform multiple write operations (Put, Update, Delete, ConditionCheck) 
/// across multiple tables as a single atomic transaction. All operations succeed or all fail.
/// </summary>
/// <example>
/// <code>
/// var response = await new TransactWriteItemsRequestBuilder(dynamoDbClient)
///     .Put(userTable, put => put
///         .WithItem(userItem)
///         .Where("attribute_not_exists(id)"))
///     .Update(accountTable, update => update
///         .WithKey("id", accountId)
///         .Set("SET balance = balance - :amount")
///         .WithValue(":amount", 100))
///     .ExecuteAsync();
/// </code>
/// </example>
public class TransactWriteItemsRequestBuilder
{
    private readonly IAmazonDynamoDB _dynamoDbClient;
    private readonly IDynamoDbLogger _logger;
    private readonly TransactWriteItemsRequest _req = new() { TransactItems = new List<TransactWriteItem>() };

    /// <summary>
    /// Initializes a new instance of the TransactWriteItemsRequestBuilder.
    /// </summary>
    /// <param name="dynamoDbClient">The DynamoDB client to use for executing the transaction.</param>
    /// <param name="logger">Optional logger for operation diagnostics.</param>
    public TransactWriteItemsRequestBuilder(IAmazonDynamoDB dynamoDbClient, IDynamoDbLogger? logger = null)
    {
        _dynamoDbClient = dynamoDbClient;
        _logger = logger ?? NoOpLogger.Instance;
    }

    /// <summary>
    /// Gets the DynamoDB client instance used by this builder.
    /// </summary>
    /// <returns>The IAmazonDynamoDB client instance used by this builder.</returns>
    internal IAmazonDynamoDB GetDynamoDbClient() => _dynamoDbClient;

    public TransactWriteItemsRequestBuilder WithClientRequestToken(string token)
    {
        _req.ClientRequestToken = token;
        return this;
    }

    public TransactWriteItemsRequestBuilder ReturnTotalConsumedCapacity()
    {
        _req.ReturnConsumedCapacity = Amazon.DynamoDBv2.ReturnConsumedCapacity.TOTAL;
        return this;
    }

    public TransactWriteItemsRequestBuilder ReturnConsumedCapacity(ReturnConsumedCapacity consumedCapacity)
    {
        _req.ReturnConsumedCapacity = consumedCapacity;
        return this;
    }

    public TransactWriteItemsRequestBuilder ReturnItemCollectionMetrics()
    {
        _req.ReturnItemCollectionMetrics = Amazon.DynamoDBv2.ReturnItemCollectionMetrics.SIZE;
        return this;
    }

    /// <summary>
    /// Adds a ConditionCheck operation to the transaction.
    /// ConditionCheck operations verify that conditions are met without modifying any data.
    /// If the condition fails, the entire transaction is rolled back.
    /// </summary>
    /// <param name="table">The table containing the item to check.</param>
    /// <param name="builderExpression">An action that configures the condition check.</param>
    /// <returns>The builder instance for method chaining.</returns>
    /// <example>
    /// <code>
    /// .CheckCondition(inventoryTable, check => check
    ///     .WithKey("productId", productId)
    ///     .Where("#quantity >= :required")
    ///     .WithAttribute("#quantity", "quantity")
    ///     .WithValue(":required", requiredQuantity))
    /// </code>
    /// </example>
    public TransactWriteItemsRequestBuilder CheckCondition(DynamoDbTableBase table,
        Action<TransactConditionCheckBuilder> builderExpression)
    {
        TransactConditionCheckBuilder builder = new(table.Name);
        builderExpression(builder);
        _req.TransactItems.Add(builder.ToWriteItem());
        return this;
    }

    /// <summary>
    /// Adds a Delete operation to the transaction.
    /// Delete operations remove items from the table by their primary key.
    /// </summary>
    /// <param name="table">The table containing the item to delete.</param>
    /// <param name="builderExpression">An action that configures the delete operation.</param>
    /// <returns>The builder instance for method chaining.</returns>
    /// <example>
    /// <code>
    /// .Delete(userTable, delete => delete
    ///     .WithKey("id", userId)
    ///     .Where("attribute_exists(id)"))
    /// </code>
    /// </example>
    public TransactWriteItemsRequestBuilder Delete(DynamoDbTableBase table,
        Action<TransactDeleteBuilder> builderExpression)
    {
        TransactDeleteBuilder builder = new(table.Name);
        builderExpression(builder);
        _req.TransactItems.Add(builder.ToWriteItem());
        return this;
    }

    /// <summary>
    /// Adds a Put operation to the transaction.
    /// Put operations create new items or completely replace existing items.
    /// </summary>
    /// <param name="table">The table to put the item into.</param>
    /// <param name="builderExpression">An action that configures the put operation.</param>
    /// <returns>The builder instance for method chaining.</returns>
    /// <example>
    /// <code>
    /// .Put(userTable, put => put
    ///     .WithItem(userItem)
    ///     .Where("attribute_not_exists(id)"))
    /// </code>
    /// </example>
    public TransactWriteItemsRequestBuilder Put(DynamoDbTableBase table, Action<TransactPutBuilder> builderExpression)
    {
        TransactPutBuilder builder = new(table.Name);
        builderExpression(builder);
        _req.TransactItems.Add(builder.ToWriteItem());
        return this;
    }

    /// <summary>
    /// Adds an Update operation to the transaction.
    /// Update operations modify existing items or create them if they don't exist.
    /// </summary>
    /// <param name="table">The table containing the item to update.</param>
    /// <param name="builderExpression">An action that configures the update operation.</param>
    /// <returns>The builder instance for method chaining.</returns>
    /// <example>
    /// <code>
    /// .Update(accountTable, update => update
    ///     .WithKey("id", accountId)
    ///     .Set("SET balance = balance - :amount")
    ///     .WithValue(":amount", transferAmount))
    /// </code>
    /// </example>
    public TransactWriteItemsRequestBuilder Update(DynamoDbTableBase table,
        Action<TransactUpdateBuilder> builderExpression)
    {
        TransactUpdateBuilder builder = new TransactUpdateBuilder(table.Name);
        builderExpression(builder);
        _req.TransactItems.Add(builder.ToWriteItem());
        return this;
    }

    public TransactWriteItemsRequestBuilder AddTransactItem(TransactWriteItem item)
    {
        _req.TransactItems.Add(item);
        return this;
    }

    public TransactWriteItemsRequest ToTransactWriteItemsRequest()
    {
        return _req;
    }

    /// <summary>
    /// Executes the transaction asynchronously using the configured operations.
    /// All operations in the transaction succeed or all fail atomically.
    /// </summary>
    /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
    /// <returns>A task representing the asynchronous operation, containing the TransactWriteItemsResponse.</returns>
    /// <exception cref="TransactionCanceledException">Thrown when the transaction is canceled due to a condition check failure or conflict.</exception>
    /// <exception cref="ValidationException">Thrown when the transaction contains invalid operations.</exception>
    /// <exception cref="ProvisionedThroughputExceededException">Thrown when the request rate is too high.</exception>
    public async Task<TransactWriteItemsResponse> ExecuteAsync(CancellationToken cancellationToken = default)
    {
        var request = ToTransactWriteItemsRequest();
        
        #if !DISABLE_DYNAMODB_LOGGING
        _logger?.LogInformation(LogEventIds.ExecutingTransaction,
            "Executing TransactWriteItems with {ItemCount} operations",
            request.TransactItems?.Count ?? 0);
        
        if (_logger?.IsEnabled(LogLevel.Trace) == true && request.TransactItems != null)
        {
            var putCount = request.TransactItems.Count(i => i.Put != null);
            var updateCount = request.TransactItems.Count(i => i.Update != null);
            var deleteCount = request.TransactItems.Count(i => i.Delete != null);
            var checkCount = request.TransactItems.Count(i => i.ConditionCheck != null);
            
            _logger.LogTrace(LogEventIds.ExecutingTransaction,
                "Transaction operations: Put={PutCount}, Update={UpdateCount}, Delete={DeleteCount}, Check={CheckCount}",
                putCount, updateCount, deleteCount, checkCount);
        }
        #endif
        
        try
        {
            var response = await _dynamoDbClient.TransactWriteItemsAsync(request, cancellationToken);
            
            #if !DISABLE_DYNAMODB_LOGGING
            var totalCapacity = response.ConsumedCapacity?.Sum(c => c.CapacityUnits) ?? 0;
            _logger?.LogInformation(LogEventIds.OperationComplete,
                "TransactWriteItems completed. TotalConsumedCapacity: {ConsumedCapacity}",
                totalCapacity);
            #endif
            
            return response;
        }
        catch (Exception ex)
        {
            #if !DISABLE_DYNAMODB_LOGGING
            _logger?.LogError(LogEventIds.DynamoDbOperationError, ex,
                "TransactWriteItems failed with {ItemCount} operations",
                request.TransactItems?.Count ?? 0);
            #endif
            throw;
        }
    }
}