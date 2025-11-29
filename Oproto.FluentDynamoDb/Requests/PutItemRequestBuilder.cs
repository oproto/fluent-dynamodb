using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using Oproto.FluentDynamoDb.Logging;
using Oproto.FluentDynamoDb.Requests.Interfaces;
using Oproto.FluentDynamoDb.Storage;

namespace Oproto.FluentDynamoDb.Requests;

/// <summary>
/// Fluent builder for DynamoDB PutItem operations.
/// PutItem creates a new item or completely replaces an existing item with the same primary key.
/// Use conditional expressions to prevent overwriting existing items when needed.
/// </summary>
/// <typeparam name="TEntity">The entity type being put into DynamoDB.</typeparam>
/// <example>
/// <code>
/// // Put an entity
/// var response = await table.Put&lt;MyEntity&gt;()
///     .WithItem(myEntity)
///     .ExecuteAsync();
/// 
/// // Put with raw attributes
/// var response = await table.Put&lt;MyEntity&gt;()
///     .WithItem(new Dictionary&lt;string, AttributeValue&gt;
///     {
///         ["id"] = new AttributeValue { S = "123" },
///         ["name"] = new AttributeValue { S = "John Doe" },
///         ["email"] = new AttributeValue { S = "john@example.com" }
///     })
///     .ExecuteAsync();
/// 
/// // Conditional put (only if item doesn't exist)
/// var response = await table.Put&lt;MyEntity&gt;()
///     .WithItem(myEntity)
///     .Where("attribute_not_exists(id)")
///     .ExecuteAsync();
/// </code>
/// </example>
public class PutItemRequestBuilder<TEntity> : IWithAttributeNames<PutItemRequestBuilder<TEntity>>, IWithAttributeValues<PutItemRequestBuilder<TEntity>>,
    IWithConditionExpression<PutItemRequestBuilder<TEntity>>, ITransactablePutBuilder, IHasDynamoDbClient
    where TEntity : class
{
    /// <summary>
    /// Initializes a new instance of the PutItemRequestBuilder.
    /// </summary>
    /// <param name="dynamoDbClient">The DynamoDB client to use for executing the request.</param>
    /// <param name="options">Configuration options including logger, hydrator registry, etc. If null, uses sensible defaults.</param>
    public PutItemRequestBuilder(IAmazonDynamoDB dynamoDbClient, FluentDynamoDbOptions? options = null)
    {
        _dynamoDbClient = dynamoDbClient;
        _options = options ?? new FluentDynamoDbOptions();
        _logger = _options.Logger;
    }

    private PutItemRequest _req = new PutItemRequest();
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
    public PutItemRequestBuilder<TEntity> WithClient(IAmazonDynamoDB client)
    {
        _dynamoDbClient = client;
        return this;
    }

    /// <summary>
    /// Sets the condition expression on the builder.
    /// If a condition expression already exists, combines them with AND logic.
    /// </summary>
    /// <param name="expression">The processed condition expression to set.</param>
    /// <returns>The builder instance for method chaining.</returns>
    public PutItemRequestBuilder<TEntity> SetConditionExpression(string expression)
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
    /// Gets the builder instance for method chaining.
    /// </summary>
    public PutItemRequestBuilder<TEntity> Self => this;

    public PutItemRequestBuilder<TEntity> ForTable(string tableName)
    {
        _req.TableName = tableName;
        return this;
    }








    /// <summary>
    /// Specifies which values to return in the response.
    /// </summary>
    /// <param name="returnValue">The return value option (NONE, ALL_OLD, UPDATED_OLD, ALL_NEW, UPDATED_NEW).</param>
    /// <returns>The builder instance for method chaining.</returns>
    public PutItemRequestBuilder<TEntity> ReturnValues(ReturnValue returnValue)
    {
        _req.ReturnValues = returnValue;
        return this;
    }

    public PutItemRequestBuilder<TEntity> ReturnUpdatedNewValues()
    {
        _req.ReturnValues = ReturnValue.UPDATED_NEW;
        return this;
    }

    public PutItemRequestBuilder<TEntity> ReturnUpdatedOldValues()
    {
        _req.ReturnValues = ReturnValue.UPDATED_OLD;
        return this;
    }

    public PutItemRequestBuilder<TEntity> ReturnAllNewValues()
    {
        _req.ReturnValues = ReturnValue.ALL_NEW;
        return this;
    }

    public PutItemRequestBuilder<TEntity> ReturnAllOldValues()
    {
        _req.ReturnValues = ReturnValue.ALL_OLD;
        return this;
    }

    public PutItemRequestBuilder<TEntity> ReturnNone()
    {
        _req.ReturnValues = ReturnValue.NONE;
        return this;
    }

    public PutItemRequestBuilder<TEntity> ReturnTotalConsumedCapacity()
    {
        _req.ReturnConsumedCapacity = Amazon.DynamoDBv2.ReturnConsumedCapacity.TOTAL;
        return this;
    }

    public PutItemRequestBuilder<TEntity> ReturnConsumedCapacity(ReturnConsumedCapacity consumedCapacity)
    {
        _req.ReturnConsumedCapacity = consumedCapacity;
        return this;
    }

    public PutItemRequestBuilder<TEntity> ReturnItemCollectionMetrics()
    {
        _req.ReturnItemCollectionMetrics = Amazon.DynamoDBv2.ReturnItemCollectionMetrics.SIZE;
        return this;
    }

    public PutItemRequestBuilder<TEntity> ReturnOldValuesOnConditionCheckFailure()
    {
        _req.ReturnValuesOnConditionCheckFailure = Amazon.DynamoDBv2.ReturnValuesOnConditionCheckFailure.ALL_OLD;
        return this;
    }

    /// <summary>
    /// Sets the item to put using an entity instance.
    /// The entity must implement IDynamoDbEntity for automatic mapping.
    /// </summary>
    /// <typeparam name="T">The entity type that implements IDynamoDbEntity.</typeparam>
    /// <param name="entity">The entity instance to put.</param>
    /// <returns>The builder instance for method chaining.</returns>
    /// <example>
    /// <code>
    /// var myEntity = new MyEntity { Id = "123", Name = "John" };
    /// await table.Put&lt;MyEntity&gt;()
    ///     .WithItem(myEntity)
    ///     .ExecuteAsync();
    /// </code>
    /// </example>
    public PutItemRequestBuilder<TEntity> WithItem<T>(T entity) where T : class, TEntity, IDynamoDbEntity
    {
        _req.Item = T.ToDynamoDb(entity, _logger);
        return this;
    }

    /// <summary>
    /// Sets the item to put using a raw DynamoDB attribute dictionary.
    /// Use this for backward compatibility or when working with raw attributes.
    /// </summary>
    /// <param name="item">The DynamoDB attribute dictionary.</param>
    /// <returns>The builder instance for method chaining.</returns>
    public PutItemRequestBuilder<TEntity> WithItem(Dictionary<string, AttributeValue> item)
    {
        _req.Item = item;
        return this;
    }

    /// <summary>
    /// Sets the item to put using a custom mapper function.
    /// </summary>
    /// <typeparam name="TItemType">The type of the item to map.</typeparam>
    /// <param name="item">The item instance.</param>
    /// <param name="modelMapper">Function to convert the item to DynamoDB attributes.</param>
    /// <returns>The builder instance for method chaining.</returns>
    public PutItemRequestBuilder<TEntity> WithItem<TItemType>(TItemType item, Func<TItemType, Dictionary<string, AttributeValue>> modelMapper)
    {
        _req.Item = modelMapper(item);
        return this;
    }

    public PutItemRequest ToPutItemRequest()
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

    // ITransactablePutBuilder implementation
    string ITransactablePutBuilder.GetTableName() => _req.TableName;
    Dictionary<string, AttributeValue> ITransactablePutBuilder.GetItem() => _req.Item;
    string? ITransactablePutBuilder.GetConditionExpression() => _req.ConditionExpression;
    Dictionary<string, string>? ITransactablePutBuilder.GetExpressionAttributeNames() => 
        _attrN.AttributeNames.Count > 0 ? _attrN.AttributeNames : null;
    Dictionary<string, AttributeValue>? ITransactablePutBuilder.GetExpressionAttributeValues() => 
        _attrV.AttributeValues.Count > 0 ? _attrV.AttributeValues : null;

    /// <summary>
    /// Executes the PutItem operation asynchronously and returns the raw AWS SDK PutItemResponse.
    /// This is the Advanced API method that does NOT populate DynamoDbOperationContext.
    /// For most use cases, prefer the Primary API extension method PutAsync() which populates context.
    /// </summary>
    /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
    /// <returns>A task representing the asynchronous operation, containing the raw PutItemResponse from AWS SDK.</returns>
    public async Task<PutItemResponse> ToDynamoDbResponseAsync(CancellationToken cancellationToken = default)
    {
        var request = ToPutItemRequest();
        
        #if !DISABLE_DYNAMODB_LOGGING
        _logger?.LogInformation(LogEventIds.ExecutingPutItem,
            "Executing PutItem on table {TableName}. Condition: {ConditionExpression}",
            request.TableName ?? "Unknown", 
            request.ConditionExpression ?? "None");
        
        if (_logger?.IsEnabled(LogLevel.Trace) == true && request.Item != null)
        {
            _logger.LogTrace(LogEventIds.ExecutingPutItem,
                "PutItem attributes: {AttributeCount}",
                request.Item.Count);
        }
        #endif
        
        try
        {
            var response = await _dynamoDbClient.PutItemAsync(request, cancellationToken);
            
            #if !DISABLE_DYNAMODB_LOGGING
            _logger?.LogInformation(LogEventIds.OperationComplete,
                "PutItem completed. ConsumedCapacity: {ConsumedCapacity}",
                response.ConsumedCapacity?.CapacityUnits ?? 0);
            #endif
            
            return response;
        }
        catch (Exception ex)
        {
            #if !DISABLE_DYNAMODB_LOGGING
            _logger?.LogError(LogEventIds.DynamoDbOperationError, ex,
                "PutItem failed on table {TableName}",
                request.TableName ?? "Unknown");
            #endif
            throw;
        }
    }
}