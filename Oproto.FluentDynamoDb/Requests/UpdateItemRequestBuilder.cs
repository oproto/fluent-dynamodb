using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using Oproto.FluentDynamoDb.Logging;
using Oproto.FluentDynamoDb.Requests.Interfaces;

namespace Oproto.FluentDynamoDb.Requests;

/// <summary>
/// Fluent builder for DynamoDB UpdateItem operations.
/// UpdateItem modifies existing items or creates them if they don't exist (upsert behavior).
/// Use update expressions to specify which attributes to modify and how to modify them.
/// </summary>
/// <typeparam name="TEntity">The entity type being updated.</typeparam>
/// <example>
/// <code>
/// // Update specific attributes
/// var response = await table.Update&lt;Transaction&gt;()
///     .WithKey("id", "123")
///     .Set("SET #name = :name, #status = :status")
///     .WithAttribute("#name", "name")
///     .WithAttribute("#status", "status")
///     .WithValue(":name", "John Doe")
///     .WithValue(":status", "ACTIVE")
///     .ExecuteAsync();
/// 
/// // Conditional update
/// var response = await table.Update&lt;Transaction&gt;()
///     .WithKey("id", "123")
///     .Set("SET #count = #count + :inc")
///     .Where("attribute_exists(id)")
///     .WithAttribute("#count", "count")
///     .WithValue(":inc", 1)
///     .ExecuteAsync();
/// </code>
/// </example>
public class UpdateItemRequestBuilder<TEntity> :
    IWithKey<UpdateItemRequestBuilder<TEntity>>, IWithConditionExpression<UpdateItemRequestBuilder<TEntity>>, IWithAttributeNames<UpdateItemRequestBuilder<TEntity>>, IWithAttributeValues<UpdateItemRequestBuilder<TEntity>>, IWithUpdateExpression<UpdateItemRequestBuilder<TEntity>>
    where TEntity : class
{
    /// <summary>
    /// Initializes a new instance of the UpdateItemRequestBuilder.
    /// </summary>
    /// <param name="dynamoDbClient">The DynamoDB client to use for executing the request.</param>
    /// <param name="logger">Optional logger for operation diagnostics.</param>
    public UpdateItemRequestBuilder(IAmazonDynamoDB dynamoDbClient, IDynamoDbLogger? logger = null)
    {
        _dynamoDbClient = dynamoDbClient;
        _logger = logger ?? NoOpLogger.Instance;
    }

    private UpdateItemRequest _req = new();
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
    public UpdateItemRequestBuilder<TEntity> SetConditionExpression(string expression)
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
    public UpdateItemRequestBuilder<TEntity> SetKey(Action<Dictionary<string, AttributeValue>> keyAction)
    {
        if (_req.Key == null) _req.Key = new();
        keyAction(_req.Key);
        return this;
    }

    /// <summary>
    /// Gets the builder instance for method chaining.
    /// </summary>
    public UpdateItemRequestBuilder<TEntity> Self => this;

    public UpdateItemRequestBuilder<TEntity> ForTable(string tableName)
    {
        _req.TableName = tableName;
        return this;
    }





    /// <summary>
    /// Sets the update expression on the builder.
    /// </summary>
    /// <param name="expression">The processed update expression to set.</param>
    /// <returns>The builder instance for method chaining.</returns>
    public UpdateItemRequestBuilder<TEntity> SetUpdateExpression(string expression)
    {
        _req.UpdateExpression = expression;
        return this;
    }






    /// <summary>
    /// Specifies which values to return in the response.
    /// </summary>
    /// <param name="returnValue">The return value option (NONE, ALL_OLD, UPDATED_OLD, ALL_NEW, UPDATED_NEW).</param>
    /// <returns>The builder instance for method chaining.</returns>
    public UpdateItemRequestBuilder<TEntity> ReturnValues(ReturnValue returnValue)
    {
        _req.ReturnValues = returnValue;
        return this;
    }

    public UpdateItemRequestBuilder<TEntity> ReturnUpdatedNewValues()
    {
        _req.ReturnValues = ReturnValue.UPDATED_NEW;
        return this;
    }

    public UpdateItemRequestBuilder<TEntity> ReturnUpdatedOldValues()
    {
        _req.ReturnValues = ReturnValue.UPDATED_OLD;
        return this;
    }

    public UpdateItemRequestBuilder<TEntity> ReturnAllNewValues()
    {
        _req.ReturnValues = ReturnValue.ALL_NEW;
        return this;
    }

    public UpdateItemRequestBuilder<TEntity> ReturnAllOldValues()
    {
        _req.ReturnValues = ReturnValue.ALL_OLD;
        return this;
    }

    public UpdateItemRequestBuilder<TEntity> ReturnNone()
    {
        _req.ReturnValues = ReturnValue.NONE;
        return this;
    }

    public UpdateItemRequestBuilder<TEntity> ReturnTotalConsumedCapacity()
    {
        _req.ReturnConsumedCapacity = Amazon.DynamoDBv2.ReturnConsumedCapacity.TOTAL;
        return this;
    }

    public UpdateItemRequestBuilder<TEntity> ReturnConsumedCapacity(ReturnConsumedCapacity consumedCapacity)
    {
        _req.ReturnConsumedCapacity = consumedCapacity;
        return this;
    }

    public UpdateItemRequestBuilder<TEntity> ReturnItemCollectionMetrics()
    {
        _req.ReturnItemCollectionMetrics = Amazon.DynamoDBv2.ReturnItemCollectionMetrics.SIZE;
        return this;
    }

    public UpdateItemRequestBuilder<TEntity> ReturnOldValuesOnConditionCheckFailure()
    {
        _req.ReturnValuesOnConditionCheckFailure = Amazon.DynamoDBv2.ReturnValuesOnConditionCheckFailure.ALL_OLD;
        return this;
    }

    public UpdateItemRequest ToUpdateItemRequest()
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
    /// Executes the UpdateItem operation asynchronously and returns the raw AWS SDK UpdateItemResponse.
    /// This is the Advanced API method that does NOT populate DynamoDbOperationContext.
    /// For most use cases, prefer the Primary API extension method UpdateAsync() which populates context.
    /// </summary>
    /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
    /// <returns>A task representing the asynchronous operation, containing the raw UpdateItemResponse from AWS SDK.</returns>
    public async Task<UpdateItemResponse> ToDynamoDbResponseAsync(CancellationToken cancellationToken = default)
    {
        var request = ToUpdateItemRequest();
        
        #if !DISABLE_DYNAMODB_LOGGING
        _logger?.LogInformation(LogEventIds.ExecutingUpdate,
            "Executing UpdateItem on table {TableName}. UpdateExpression: {UpdateExpression}, Condition: {ConditionExpression}",
            request.TableName ?? "Unknown", 
            request.UpdateExpression ?? "None", 
            request.ConditionExpression ?? "None");
        
        if (_logger?.IsEnabled(LogLevel.Trace) == true && _attrV.AttributeValues.Count > 0)
        {
            _logger.LogTrace(LogEventIds.ExecutingUpdate,
                "UpdateItem parameters: {ParameterCount} values",
                _attrV.AttributeValues.Count);
        }
        #endif
        
        try
        {
            var response = await _dynamoDbClient.UpdateItemAsync(request, cancellationToken);
            
            #if !DISABLE_DYNAMODB_LOGGING
            _logger?.LogInformation(LogEventIds.OperationComplete,
                "UpdateItem completed. ConsumedCapacity: {ConsumedCapacity}",
                response.ConsumedCapacity?.CapacityUnits ?? 0);
            #endif
            
            return response;
        }
        catch (Exception ex)
        {
            #if !DISABLE_DYNAMODB_LOGGING
            _logger?.LogError(LogEventIds.DynamoDbOperationError, ex,
                "UpdateItem failed on table {TableName}",
                request.TableName ?? "Unknown");
            #endif
            throw;
        }
    }
}