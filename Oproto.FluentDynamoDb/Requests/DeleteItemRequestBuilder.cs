using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using Oproto.FluentDynamoDb.Entities;
using Oproto.FluentDynamoDb.Logging;
using Oproto.FluentDynamoDb.Providers.BlobStorage;
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
///     .DeleteAsync();
/// 
/// // Conditional delete with return values (use ToDynamoDbResponseAsync to access response.Attributes)
/// var response = await table.Delete&lt;Transaction&gt;()
///     .WithKey("pk", "USER", "sk", "user123")
///     .Where("attribute_exists(#status)")
///     .WithAttribute("#status", "status")
///     .ReturnAllOldValues()
///     .ToDynamoDbResponseAsync();
/// </code>
/// </example>
public class DeleteItemRequestBuilder<TEntity> :
    IWithKey<DeleteItemRequestBuilder<TEntity>>,
    IWithConditionExpression<DeleteItemRequestBuilder<TEntity>>,
    IWithAttributeNames<DeleteItemRequestBuilder<TEntity>>,
    IWithAttributeValues<DeleteItemRequestBuilder<TEntity>>,
    ITransactableDeleteBuilder,
    IHasDynamoDbClient
    where TEntity : class, IDynamoDbEntity
{
    /// <summary>
    /// Initializes a new instance of the DeleteItemRequestBuilder.
    /// </summary>
    /// <param name="dynamoDbClient">The DynamoDB client to use for executing the request.</param>
    /// <param name="options">Configuration options including logger, hydrator registry, etc. If null, uses sensible defaults.</param>
    public DeleteItemRequestBuilder(IAmazonDynamoDB dynamoDbClient, FluentDynamoDbOptions? options = null)
    {
        _dynamoDbClient = dynamoDbClient;
        _options = options ?? new FluentDynamoDbOptions();
        _logger = _options.Logger;
        
        // Apply default options
        if (_options.DefaultReturnConsumedCapacity is { } defaultConsumedCapacity)
        {
            _req.ReturnConsumedCapacity = defaultConsumedCapacity;
        }
        if (_options.DefaultReturnItemCollectionMetrics is { } defaultItemCollectionMetrics)
        {
            _req.ReturnItemCollectionMetrics = defaultItemCollectionMetrics;
        }
        // Note: DeleteItemRequest only supports NONE and ALL_OLD for ReturnValues
        // We apply the default only if it's a valid value for delete operations
        if (_options.DefaultReturnValues is { } defaultReturnValues && 
            (defaultReturnValues == ReturnValue.NONE || 
             defaultReturnValues == ReturnValue.ALL_OLD))
        {
            _req.ReturnValues = defaultReturnValues;
        }
    }

    private DeleteItemRequest _req = new();
    private IAmazonDynamoDB _dynamoDbClient;
    private readonly IDynamoDbLogger _logger;
    private readonly FluentDynamoDbOptions _options;
    private readonly AttributeValueInternal _attrV = new AttributeValueInternal();
    private readonly AttributeNameInternal _attrN = new AttributeNameInternal();
    private List<string>? _blobReferenceKeys;
    private KeyCondition _keyCondition = KeyCondition.None;

    /// <summary>
    /// Gets the response metadata from the most recent DeleteItem execution.
    /// This is populated by Primary API methods (DeleteAsync) after execution.
    /// Null if the operation hasn't been executed yet.
    /// </summary>
    public DeleteItemOperationResponse? Response { get; internal set; }

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
    public DeleteItemRequestBuilder<TEntity> WithClient(IAmazonDynamoDB client)
    {
        _dynamoDbClient = client;
        return this;
    }

    /// <summary>
    /// Sets the condition expression on the builder.
    /// If a condition expression already exists, combines them with AND logic.
    /// If the expression is empty or whitespace (e.g., all conditional clauses evaluated to skip),
    /// the method returns without setting the condition, allowing the operation to proceed unconditionally.
    /// </summary>
    /// <param name="expression">The processed condition expression to set.</param>
    /// <returns>The builder instance for method chaining.</returns>
    public DeleteItemRequestBuilder<TEntity> SetConditionExpression(string expression)
    {
        // Skip setting if expression is empty (all conditionals evaluated to skip)
        if (string.IsNullOrWhiteSpace(expression))
        {
            return this;
        }
        
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
    /// Adds a condition that the item must already exist (all key attributes must exist).
    /// Equivalent to <c>WithKeyCondition(KeyCondition.MustExist)</c>.
    /// Use this to ensure you're deleting an existing item.
    /// </summary>
    /// <returns>The builder instance for method chaining.</returns>
    /// <remarks>
    /// <para>For simple key entities: generates <c>attribute_exists(pk)</c></para>
    /// <para>For composite key entities: generates <c>attribute_exists(pk) AND attribute_exists(sk)</c></para>
    /// </remarks>
    /// <example>
    /// <code>
    /// // Delete only if exists (fail if not exists)
    /// await table.Users.Delete(userId).IfExists().DeleteAsync();
    /// </code>
    /// </example>
    public DeleteItemRequestBuilder<TEntity> IfExists()
    {
        _keyCondition = KeyCondition.MustExist;
        return this;
    }

    /// <summary>
    /// Adds a condition that the item must not already exist (key attributes must not exist).
    /// Equivalent to <c>WithKeyCondition(KeyCondition.MustNotExist)</c>.
    /// Note: This is rarely useful for delete operations but provided for API consistency.
    /// </summary>
    /// <returns>The builder instance for method chaining.</returns>
    /// <remarks>
    /// <para>For simple key entities: generates <c>attribute_not_exists(pk)</c></para>
    /// <para>For composite key entities: generates <c>attribute_not_exists(pk) AND attribute_not_exists(sk)</c></para>
    /// </remarks>
    public DeleteItemRequestBuilder<TEntity> IfNotExists()
    {
        _keyCondition = KeyCondition.MustNotExist;
        return this;
    }

    /// <summary>
    /// Sets the key condition for this operation.
    /// </summary>
    /// <param name="condition">The key condition to apply.</param>
    /// <returns>The builder instance for method chaining.</returns>
    /// <example>
    /// <code>
    /// // Using enum directly
    /// await table.Users.Delete(userId).WithKeyCondition(KeyCondition.MustExist).DeleteAsync();
    /// </code>
    /// </example>
    public DeleteItemRequestBuilder<TEntity> WithKeyCondition(KeyCondition condition)
    {
        _keyCondition = condition;
        return this;
    }

    /// <summary>
    /// Applies the key condition to the request's condition expression.
    /// Called internally during request building.
    /// </summary>
    private void ApplyKeyCondition()
    {
        if (_keyCondition == KeyCondition.None) return;

        var metadata = TEntity.GetEntityMetadata();
        var pkAttrName = metadata.PartitionKeyAttributeName;
        var skAttrName = metadata.SortKeyAttributeName;

        string condition;
        if (_keyCondition == KeyCondition.MustExist)
        {
            condition = string.IsNullOrEmpty(skAttrName)
                ? $"attribute_exists({pkAttrName})"
                : $"attribute_exists({pkAttrName}) AND attribute_exists({skAttrName})";
        }
        else // MustNotExist
        {
            condition = string.IsNullOrEmpty(skAttrName)
                ? $"attribute_not_exists({pkAttrName})"
                : $"attribute_not_exists({pkAttrName}) AND attribute_not_exists({skAttrName})";
        }

        // Combine with existing condition if present
        if (string.IsNullOrEmpty(_req.ConditionExpression))
        {
            _req.ConditionExpression = condition;
        }
        else
        {
            _req.ConditionExpression = $"({condition}) AND ({_req.ConditionExpression})";
        }
    }

    /// <summary>
    /// Sets the blob reference keys for cleanup after delete.
    /// Used internally when deleting entities with blob storage properties.
    /// </summary>
    /// <param name="referenceKeys">The blob reference keys to clean up after delete.</param>
    /// <returns>The builder instance for method chaining.</returns>
    internal DeleteItemRequestBuilder<TEntity> WithBlobReferenceKeys(List<string> referenceKeys)
    {
        _blobReferenceKeys = referenceKeys;
        return this;
    }

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
    /// Configures the builder with a pre-built DeleteItemRequest.
    /// This replaces any previously configured request state.
    /// Use this when you have an existing SDK request object and want to leverage
    /// the library's execution and context population capabilities.
    /// </summary>
    /// <param name="request">The pre-built DeleteItemRequest.</param>
    /// <returns>The builder instance for method chaining.</returns>
    /// <exception cref="ArgumentNullException">Thrown when request is null.</exception>
    /// <example>
    /// <code>
    /// var sdkRequest = new DeleteItemRequest
    /// {
    ///     TableName = "Users",
    ///     Key = new Dictionary&lt;string, AttributeValue&gt;
    ///     {
    ///         ["pk"] = new AttributeValue { S = "USER#123" },
    ///         ["sk"] = new AttributeValue { S = "PROFILE" }
    ///     },
    ///     ConditionExpression = "attribute_exists(pk)",
    ///     ReturnValues = ReturnValue.ALL_OLD
    /// };
    /// 
    /// // Use builder pattern for metadata access
    /// var builder = table.Delete&lt;User&gt;().WithRequest(sdkRequest);
    /// var deletedUser = await builder.DeleteAsync();
    /// var capacity = builder.ConsumedCapacity;
    /// </code>
    /// </example>
    public DeleteItemRequestBuilder<TEntity> WithRequest(DeleteItemRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        _req = request;
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
        // Apply key condition before building the request
        ApplyKeyCondition();
        
        if (_attrN.AttributeNames.Count > 0)
        {
            _req.ExpressionAttributeNames = _attrN.AttributeNames;
        }
        
        if (_attrV.AttributeValues.Count > 0)
        {
            _req.ExpressionAttributeValues = _attrV.AttributeValues;
        }
        // Note: Do NOT set an empty ExpressionAttributeValues dictionary
        // DynamoDB will reject requests with empty ExpressionAttributeValues
        return _req;
    }

    // ITransactableDeleteBuilder implementation
    string ITransactableDeleteBuilder.GetTableName() => _req.TableName;
    Dictionary<string, AttributeValue> ITransactableDeleteBuilder.GetKey() => _req.Key;
    string? ITransactableDeleteBuilder.GetConditionExpression()
    {
        // Apply key condition before returning the condition expression
        // This ensures key conditions are included when the builder is used in transactions
        ApplyKeyCondition();
        return _req.ConditionExpression;
    }
    Dictionary<string, string>? ITransactableDeleteBuilder.GetExpressionAttributeNames() => 
        _attrN.AttributeNames.Count > 0 ? _attrN.AttributeNames : null;
    Dictionary<string, AttributeValue>? ITransactableDeleteBuilder.GetExpressionAttributeValues() => 
        _attrV.AttributeValues.Count > 0 ? _attrV.AttributeValues : null;

    /// <summary>
    /// Executes the DeleteItem operation asynchronously and returns the raw AWS SDK DeleteItemResponse.
    /// This is the Advanced API method that does NOT populate DynamoDbOperationContext.
    /// For most use cases, prefer the Primary API extension method DeleteAsync() which populates context.
    /// </summary>
    /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
    /// <returns>A task representing the asynchronous operation, containing the raw DeleteItemResponse from AWS SDK.</returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the entity type is marked with [RequireWriteTransaction] attribute.
    /// Use DynamoDbTransactions.Write() to perform transactional writes for such entities.
    /// </exception>
    /// <exception cref="ConditionalCheckFailedException">Thrown when a condition expression fails.</exception>
    /// <exception cref="ResourceNotFoundException">Thrown when the specified table doesn't exist.</exception>
    public async Task<DeleteItemResponse> ToDynamoDbResponseAsync(CancellationToken cancellationToken = default)
    {
        if (TEntity.RequiresWriteTransaction)
        {
            throw new InvalidOperationException(
                $"Entity '{typeof(TEntity).Name}' is marked with [RequireWriteTransaction] and cannot be modified " +
                "outside of a transaction. Use DynamoDbTransactions.Write() to perform this operation.");
        }
        
        var request = ToDeleteItemRequest();
        
        // Check if we have blob reference keys to clean up
        if (_blobReferenceKeys != null && _blobReferenceKeys.Count > 0 && _options.BlobStorageStrategy != null)
        {
            return await ExecuteWithBlobStorageAsync(request, cancellationToken).ConfigureAwait(false);
        }
        
        return await ExecuteDynamoDbOperationAsync(request, cancellationToken).ConfigureAwait(false);
    }

    private async Task<DeleteItemResponse> ExecuteWithBlobStorageAsync(
        DeleteItemRequest request,
        CancellationToken cancellationToken)
    {
        return await BlobStorageHelper.ExecuteDeleteWithBlobStrategyAsync<TEntity, DeleteItemResponse>(
            _blobReferenceKeys!,
            _options,
            async () => await ExecuteDynamoDbOperationAsync(request, cancellationToken).ConfigureAwait(false),
            cancellationToken).ConfigureAwait(false);
    }

    private async Task<DeleteItemResponse> ExecuteDynamoDbOperationAsync(
        DeleteItemRequest request,
        CancellationToken cancellationToken)
    {
        if (_logger?.IsEnabled(LogLevel.Information) == true)
        {
            _logger.LogInformation(LogEventIds.ExecutingPutItem,
                "Executing DeleteItem on table {TableName}. Condition: {ConditionExpression}",
                request.TableName ?? "Unknown", 
                request.ConditionExpression ?? "None");
        }
        
        if (_logger?.IsEnabled(LogLevel.Trace) == true && request.Key != null)
        {
            _logger.LogTrace(LogEventIds.ExecutingPutItem,
                "DeleteItem key attributes: {KeyCount}",
                request.Key.Count);
        }
        
        try
        {
            var response = await _dynamoDbClient.DeleteItemAsync(request, cancellationToken).ConfigureAwait(false);
            
            if (_logger?.IsEnabled(LogLevel.Information) == true)
            {
                _logger.LogInformation(LogEventIds.OperationComplete,
                    "DeleteItem completed. ConsumedCapacity: {ConsumedCapacity}",
                    response.ConsumedCapacity?.CapacityUnits ?? 0);
            }
            
            return response;
        }
        catch (Exception ex)
        {
            _logger?.LogError(LogEventIds.DynamoDbOperationError, ex,
                "DeleteItem failed on table {TableName}",
                request.TableName ?? "Unknown");
            throw;
        }
    }
}