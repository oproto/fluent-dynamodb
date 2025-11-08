using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using Oproto.FluentDynamoDb.Logging;
using Oproto.FluentDynamoDb.Requests.Interfaces;

namespace Oproto.FluentDynamoDb.Requests;

/// <summary>
/// Fluent builder for DynamoDB DeleteItem operations.
/// Provides a type-safe way to construct delete requests with support for conditional deletes,
/// return values, and consumed capacity tracking.
/// </summary>
/// <typeparam name="TEntity">The entity type being deleted.</typeparam>
/// <example>
/// <code>
/// // Simple delete by primary key
/// await table.Delete&lt;Transaction&gt;()
///     .WithKey("id", "user123")
///     .ExecuteAsync();
/// 
/// // Conditional delete with return values
/// var response = await table.Delete&lt;Transaction&gt;()
///     .WithKey("pk", "USER", "sk", "user123")
///     .Where("attribute_exists(#status)")
///     .WithAttribute("#status", "status")
///     .ReturnAllOldValues()
///     .ExecuteAsync();
/// </code>
/// </example>
public class DeleteItemRequestBuilder<TEntity> :
    IWithKey<DeleteItemRequestBuilder<TEntity>>,
    IWithConditionExpression<DeleteItemRequestBuilder<TEntity>>,
    IWithAttributeNames<DeleteItemRequestBuilder<TEntity>>,
    IWithAttributeValues<DeleteItemRequestBuilder<TEntity>>
    where TEntity : class
{
    /// <summary>
    /// Initializes a new instance of the DeleteItemRequestBuilder.
    /// </summary>
    /// <param name="dynamoDbClient">The DynamoDB client to use for executing the request.</param>
    /// <param name="logger">Optional logger for operation diagnostics.</param>
    public DeleteItemRequestBuilder(IAmazonDynamoDB dynamoDbClient, IDynamoDbLogger? logger = null)
    {
        _dynamoDbClient = dynamoDbClient;
        _logger = logger ?? NoOpLogger.Instance;
    }

    private DeleteItemRequest _req = new();
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
    public DeleteItemRequestBuilder<TEntity> SetConditionExpression(string expression)
    {
        if (string.IsNullOrEmpty(_req.ConditionExpression))
        {
            _req.ConditionExpression = expression;
        }
        else
        {
            _req.ConditionExpression = $"({_req.ConditionExpression}) AND ({expression})";
        }
        return this;
    }

    /// <summary>
    /// Sets key values using a configuration action for extension method access.
    /// </summary>
    /// <param name="keyAction">An action that configures the key dictionary.</param>
    /// <returns>The builder instance for method chaining.</returns>
    public DeleteItemRequestBuilder<TEntity> SetKey(Action<Dictionary<string, AttributeValue>> keyAction)
    {
        if (_req.Key == null) _req.Key = new();
        keyAction(_req.Key);
        return this;
    }

    /// <summary>
    /// Gets the builder instance for method chaining.
    /// </summary>
    public DeleteItemRequestBuilder<TEntity> Self => this;

    /// <summary>
    /// Specifies the table name for the delete operation.
    /// </summary>
    /// <param name="tableName">The name of the DynamoDB table.</param>
    /// <returns>The builder instance for method chaining.</returns>
    public DeleteItemRequestBuilder<TEntity> ForTable(string tableName)
    {
        _req.TableName = tableName;
        return this;
    }









    /// <summary>
    /// Configures the delete operation to return all attributes of the deleted item as they appeared before deletion.
    /// Useful for audit trails or undo functionality.
    /// </summary>
    /// <returns>The builder instance for method chaining.</returns>
    public DeleteItemRequestBuilder<TEntity> ReturnAllOldValues()
    {
        _req.ReturnValues = ReturnValue.ALL_OLD;
        return this;
    }

    /// <summary>
    /// Configures the delete operation to return no item attributes (default behavior).
    /// This is the most efficient option when you don't need the deleted item's data.
    /// </summary>
    /// <returns>The builder instance for method chaining.</returns>
    public DeleteItemRequestBuilder<TEntity> ReturnNone()
    {
        _req.ReturnValues = ReturnValue.NONE;
        return this;
    }

    /// <summary>
    /// Configures the delete operation to return the total consumed capacity information.
    /// Useful for monitoring and optimizing DynamoDB usage costs.
    /// </summary>
    /// <returns>The builder instance for method chaining.</returns>
    public DeleteItemRequestBuilder<TEntity> ReturnTotalConsumedCapacity()
    {
        _req.ReturnConsumedCapacity = Amazon.DynamoDBv2.ReturnConsumedCapacity.TOTAL;
        return this;
    }

    /// <summary>
    /// Configures the delete operation to return consumed capacity information.
    /// </summary>
    /// <param name="consumedCapacity">The level of consumed capacity information to return.</param>
    /// <returns>The builder instance for method chaining.</returns>
    public DeleteItemRequestBuilder<TEntity> ReturnConsumedCapacity(ReturnConsumedCapacity consumedCapacity)
    {
        _req.ReturnConsumedCapacity = consumedCapacity;
        return this;
    }

    /// <summary>
    /// Configures the delete operation to return item collection metrics.
    /// Only applicable for tables with local secondary indexes.
    /// </summary>
    /// <returns>The builder instance for method chaining.</returns>
    public DeleteItemRequestBuilder<TEntity> ReturnItemCollectionMetrics()
    {
        _req.ReturnItemCollectionMetrics = Amazon.DynamoDBv2.ReturnItemCollectionMetrics.SIZE;
        return this;
    }

    /// <summary>
    /// Configures the delete operation to return the old item values when a condition check fails.
    /// Useful for debugging conditional delete failures.
    /// </summary>
    /// <returns>The builder instance for method chaining.</returns>
    public DeleteItemRequestBuilder<TEntity> ReturnOldValuesOnConditionCheckFailure()
    {
        _req.ReturnValuesOnConditionCheckFailure = Amazon.DynamoDBv2.ReturnValuesOnConditionCheckFailure.ALL_OLD;
        return this;
    }

    /// <summary>
    /// Builds and returns the configured DeleteItemRequest.
    /// </summary>
    /// <returns>A configured DeleteItemRequest ready for execution.</returns>
    public DeleteItemRequest ToDeleteItemRequest()
    {
        if (_attrN.AttributeNames.Count > 0)
        {
            _req.ExpressionAttributeNames = _attrN.AttributeNames;
        }
        else if (_req.ExpressionAttributeNames == null)
        {
            _req.ExpressionAttributeNames = new Dictionary<string, string>();
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
    /// Executes the DeleteItem operation asynchronously and returns the raw AWS SDK DeleteItemResponse.
    /// This is the Advanced API method that does NOT populate DynamoDbOperationContext.
    /// For most use cases, prefer the Primary API extension method DeleteAsync() which populates context.
    /// </summary>
    /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
    /// <returns>A task representing the asynchronous operation, containing the raw DeleteItemResponse from AWS SDK.</returns>
    /// <exception cref="ConditionalCheckFailedException">Thrown when a condition expression fails.</exception>
    /// <exception cref="ResourceNotFoundException">Thrown when the specified table doesn't exist.</exception>
    public async Task<DeleteItemResponse> ToDynamoDbResponseAsync(CancellationToken cancellationToken = default)
    {
        var request = ToDeleteItemRequest();
        
        #if !DISABLE_DYNAMODB_LOGGING
        _logger?.LogInformation(LogEventIds.ExecutingPutItem,
            "Executing DeleteItem on table {TableName}. Condition: {ConditionExpression}",
            request.TableName ?? "Unknown", 
            request.ConditionExpression ?? "None");
        
        if (_logger?.IsEnabled(LogLevel.Trace) == true && request.Key != null)
        {
            _logger.LogTrace(LogEventIds.ExecutingPutItem,
                "DeleteItem key attributes: {KeyCount}",
                request.Key.Count);
        }
        #endif
        
        try
        {
            var response = await _dynamoDbClient.DeleteItemAsync(request, cancellationToken);
            
            #if !DISABLE_DYNAMODB_LOGGING
            _logger?.LogInformation(LogEventIds.OperationComplete,
                "DeleteItem completed. ConsumedCapacity: {ConsumedCapacity}",
                response.ConsumedCapacity?.CapacityUnits ?? 0);
            #endif
            
            return response;
        }
        catch (Exception ex)
        {
            #if !DISABLE_DYNAMODB_LOGGING
            _logger?.LogError(LogEventIds.DynamoDbOperationError, ex,
                "DeleteItem failed on table {TableName}",
                request.TableName ?? "Unknown");
            #endif
            throw;
        }
    }
}