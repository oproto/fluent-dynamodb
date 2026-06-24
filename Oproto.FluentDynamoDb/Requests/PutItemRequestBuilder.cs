using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using Oproto.FluentDynamoDb.Entities;
using Oproto.FluentDynamoDb.Logging;
using Oproto.FluentDynamoDb.Providers.BlobStorage;
using Oproto.FluentDynamoDb.Requests.Interfaces;

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
/// await table.Put&lt;MyEntity&gt;()
///     .WithItem(myEntity)
///     .PutAsync();
/// 
/// // Put with raw attributes
/// await table.Put&lt;MyEntity&gt;()
///     .WithItem(new Dictionary&lt;string, AttributeValue&gt;
///     {
///         ["id"] = new AttributeValue { S = "123" },
///         ["name"] = new AttributeValue { S = "John Doe" },
///         ["email"] = new AttributeValue { S = "john@example.com" }
///     })
///     .PutAsync();
/// 
/// // Conditional put with return values (use ToDynamoDbResponseAsync to access response.Attributes)
/// var response = await table.Put&lt;MyEntity&gt;()
///     .WithItem(myEntity)
///     .Where("attribute_not_exists(id)")
///     .ReturnAllOldValues()
///     .ToDynamoDbResponseAsync();
/// </code>
/// </example>
public class PutItemRequestBuilder<TEntity> : IWithAttributeNames<PutItemRequestBuilder<TEntity>>, IWithAttributeValues<PutItemRequestBuilder<TEntity>>,
    IWithConditionExpression<PutItemRequestBuilder<TEntity>>, ITransactablePutBuilder, IHasDynamoDbClient
    where TEntity : class, IDynamoDbEntity
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
        
        // Apply default options
        if (_options.DefaultReturnConsumedCapacity is { } defaultConsumedCapacity)
        {
            _req.ReturnConsumedCapacity = defaultConsumedCapacity;
        }
        if (_options.DefaultReturnItemCollectionMetrics is { } defaultItemCollectionMetrics)
        {
            _req.ReturnItemCollectionMetrics = defaultItemCollectionMetrics;
        }
        if (_options.DefaultReturnValues is { } defaultReturnValues)
        {
            _req.ReturnValues = defaultReturnValues;
        }
    }

    private PutItemRequest _req = new PutItemRequest();
    private IAmazonDynamoDB _dynamoDbClient;
    private readonly IDynamoDbLogger _logger;
    private readonly FluentDynamoDbOptions _options;
    private readonly AttributeValueInternal _attrV = new AttributeValueInternal();
    private readonly AttributeNameInternal _attrN = new AttributeNameInternal();
    private TEntity? _entity;
    private bool _hasDeferredEntity;
    private KeyCondition _keyCondition = KeyCondition.None;
    private KeyInputMode _keyInputMode = KeyInputMode.Default;

    /// <summary>
    /// Gets the response metadata from the most recent PutItem execution.
    /// This is populated by Primary API methods (PutAsync) after execution.
    /// Null if the operation hasn't been executed yet.
    /// </summary>
    public PutItemOperationResponse? Response { get; internal set; }

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
    /// Gets whether this builder has a deferred entity that requires async serialization.
    /// When true, the entity must be serialized via the hydrator registry before building the request.
    /// </summary>
    public bool HasDeferredEntity => _hasDeferredEntity;

    /// <summary>
    /// Gets the deferred entity that needs async serialization, or null if no entity is deferred.
    /// </summary>
    /// <returns>The deferred entity, or null.</returns>
    public TEntity? GetDeferredEntity() => _hasDeferredEntity ? _entity : null;

    /// <summary>
    /// Sets the serialized item dictionary after async serialization has been resolved.
    /// Called by PutAsync and other async execution methods after resolving deferred serialization.
    /// </summary>
    /// <param name="item">The serialized DynamoDB attribute dictionary.</param>
    public void SetResolvedItem(Dictionary<string, AttributeValue> item)
    {
        _req.Item = item;
        _hasDeferredEntity = false;
    }

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
    /// If the expression is empty or whitespace (e.g., all conditional clauses evaluated to skip),
    /// the method returns without setting the condition, allowing the operation to proceed unconditionally.
    /// </summary>
    /// <param name="expression">The processed condition expression to set.</param>
    /// <returns>The builder instance for method chaining.</returns>
    public PutItemRequestBuilder<TEntity> SetConditionExpression(string expression)
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
    /// Gets the builder instance for method chaining.
    /// </summary>
    public PutItemRequestBuilder<TEntity> Self => this;

    /// <summary>
    /// Adds a condition that the item must already exist (all key attributes must exist).
    /// Equivalent to <c>WithKeyCondition(KeyCondition.MustExist)</c>.
    /// </summary>
    /// <returns>The builder instance for method chaining.</returns>
    /// <remarks>
    /// <para>For simple key entities: generates <c>attribute_exists(pk)</c></para>
    /// <para>For composite key entities: generates <c>attribute_exists(pk) AND attribute_exists(sk)</c></para>
    /// </remarks>
    /// <example>
    /// <code>
    /// // Replace existing item only (fail if not exists)
    /// await table.Users.Put(user).IfExists().PutAsync();
    /// </code>
    /// </example>
    public PutItemRequestBuilder<TEntity> IfExists()
    {
        _keyCondition = KeyCondition.MustExist;
        return this;
    }

    /// <summary>
    /// Adds a condition that the item must not already exist (key attributes must not exist).
    /// Equivalent to <c>WithKeyCondition(KeyCondition.MustNotExist)</c>.
    /// </summary>
    /// <returns>The builder instance for method chaining.</returns>
    /// <remarks>
    /// <para>For simple key entities: generates <c>attribute_not_exists(pk)</c></para>
    /// <para>For composite key entities: generates <c>attribute_not_exists(pk) AND attribute_not_exists(sk)</c></para>
    /// </remarks>
    /// <example>
    /// <code>
    /// // Create only (fail if exists)
    /// await table.Users.Put(user).IfNotExists().PutAsync();
    /// </code>
    /// </example>
    public PutItemRequestBuilder<TEntity> IfNotExists()
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
    /// await table.Users.Put(user).WithKeyCondition(KeyCondition.MustNotExist).PutAsync();
    /// </code>
    /// </example>
    public PutItemRequestBuilder<TEntity> WithKeyCondition(KeyCondition condition)
    {
        _keyCondition = condition;
        return this;
    }

    /// <summary>
    /// Overrides the KeyInputMode used for prefix application during Put serialization.
    /// When not called, KeyInputMode.Default is used (resolved from FluentDynamoDbOptions.DefaultKeyInputMode).
    /// </summary>
    /// <param name="mode">The KeyInputMode to use for this Put operation.</param>
    /// <returns>The builder instance for method chaining.</returns>
    /// <example>
    /// <code>
    /// // Pass key values through unchanged (no prefix application)
    /// await table.Users.Put(user).WithKeyMode(KeyInputMode.Raw).PutAsync();
    /// 
    /// // Always prepend configured prefix
    /// await table.Users.Put(user).WithKeyMode(KeyInputMode.Value).PutAsync();
    /// </code>
    /// </example>
    public PutItemRequestBuilder<TEntity> WithKeyMode(KeyInputMode mode)
    {
        _keyInputMode = mode;
        return this;
    }

    /// <summary>
    /// Gets the KeyInputMode configured for this builder.
    /// Used by extension methods to propagate the mode to serialization.
    /// </summary>
    /// <returns>The configured KeyInputMode.</returns>
    public KeyInputMode GetKeyInputMode() => _keyInputMode;

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

    public PutItemRequestBuilder<TEntity> ForTable(string tableName)
    {
        _req.TableName = tableName;
        return this;
    }

    /// <summary>
    /// Configures the builder with a pre-built PutItemRequest.
    /// This replaces any previously configured request state.
    /// Use this when you have an existing SDK request object and want to leverage
    /// the library's execution and context population capabilities.
    /// </summary>
    /// <param name="request">The pre-built PutItemRequest.</param>
    /// <returns>The builder instance for method chaining.</returns>
    /// <exception cref="ArgumentNullException">Thrown when request is null.</exception>
    /// <example>
    /// <code>
    /// var sdkRequest = new PutItemRequest
    /// {
    ///     TableName = "Users",
    ///     Item = new Dictionary&lt;string, AttributeValue&gt;
    ///     {
    ///         ["pk"] = new AttributeValue { S = "USER#123" },
    ///         ["sk"] = new AttributeValue { S = "PROFILE" },
    ///         ["name"] = new AttributeValue { S = "John Doe" }
    ///     },
    ///     ConditionExpression = "attribute_not_exists(pk)"
    /// };
    /// 
    /// // Use builder pattern for metadata access
    /// var builder = table.Put&lt;User&gt;().WithRequest(sdkRequest);
    /// await builder.PutAsync();
    /// var capacity = builder.ConsumedCapacity;
    /// </code>
    /// </example>
    public PutItemRequestBuilder<TEntity> WithRequest(PutItemRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        _req = request;
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
    /// The entity is automatically mapped to DynamoDB attributes using the generated mapper.
    /// For entities with a registered hydrator (e.g., encrypted entities), serialization is
    /// deferred to async execution time. For non-encrypted entities, serialization happens
    /// synchronously at configuration time.
    /// </summary>
    /// <param name="entity">The entity instance to put.</param>
    /// <returns>The builder instance for method chaining.</returns>
    /// <example>
    /// <code>
    /// var myEntity = new MyEntity { Id = "123", Name = "John" };
    /// await table.Put&lt;MyEntity&gt;()
    ///     .WithItem(myEntity)
    ///     .PutAsync();
    /// </code>
    /// </example>
    public PutItemRequestBuilder<TEntity> WithItem(TEntity entity)
    {
        _entity = entity;

        // Check if the entity has a hydrator registered (indicating async serialization is needed,
        // e.g., for encrypted entities). If so, defer serialization to async execution time.
        var hydrator = _options.HydratorRegistry?.GetHydrator<TEntity>();
        if (hydrator != null)
        {
            // Defer serialization — async execution (PutAsync) will resolve via hydrator
            _hasDeferredEntity = true;
            return this;
        }

        // No hydrator registered — perform synchronous serialization.
        // Pass _keyInputMode to the new overload for prefix application.
        try
        {
            _req.Item = TEntity.ToDynamoDb(entity, _options, _keyInputMode);
        }
        catch (NotSupportedException)
        {
            // Entity requires async serialization (e.g., encryption) — defer to PutAsync
            _hasDeferredEntity = true;
        }

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
        // Apply key condition before building the request
        ApplyKeyCondition();

        // If the entity requires async serialization (deferred) and hasn't been resolved yet,
        // throw to indicate that async execution is required
        if (_hasDeferredEntity && _req.Item == null)
        {
            throw new InvalidOperationException(
                $"Entity '{typeof(TEntity).Name}' requires async serialization (e.g., encryption). " +
                "Use PutAsync() or resolve the deferred entity via the hydrator registry before calling ToPutItemRequest().");
        }
        
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
    Dictionary<string, AttributeValue> ITransactablePutBuilder.GetItem()
    {
        if (_hasDeferredEntity && _req.Item == null)
        {
            throw new InvalidOperationException(
                $"Entity '{typeof(TEntity).Name}' requires async serialization (e.g., encryption). " +
                "Resolve the deferred entity via the hydrator registry before calling GetItem().");
        }
        return _req.Item;
    }
    string? ITransactablePutBuilder.GetConditionExpression()
    {
        // Apply key condition before returning the condition expression
        // This ensures key conditions are included when the builder is used in transactions
        ApplyKeyCondition();
        return _req.ConditionExpression;
    }
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
    /// <exception cref="InvalidOperationException">
    /// Thrown when the entity type is marked with [RequireWriteTransaction] attribute.
    /// Use DynamoDbTransactions.Write() to perform transactional writes for such entities.
    /// </exception>
    public async Task<PutItemResponse> ToDynamoDbResponseAsync(CancellationToken cancellationToken = default)
    {
        if (TEntity.RequiresWriteTransaction)
        {
            throw new InvalidOperationException(
                $"Entity '{typeof(TEntity).Name}' is marked with [RequireWriteTransaction] and cannot be modified " +
                "outside of a transaction. Use DynamoDbTransactions.Write() to perform this operation.");
        }
        
        // Resolve deferred async serialization for encrypted entities before building the request
        if (_hasDeferredEntity)
        {
            var hydrator = _options.HydratorRegistry?.GetHydrator<TEntity>();
            if (hydrator != null && _entity != null)
            {
                var blobProvider = _options.BlobStorageProvider;
                var item = await hydrator.SerializeAsync(_entity, blobProvider, _options, _keyInputMode, cancellationToken).ConfigureAwait(false);
                SetResolvedItem(item);
            }
        }
        
        var request = ToPutItemRequest();
        
        // Check if we have an entity with blob storage properties and a strategy configured
        if (_entity != null && _options.BlobStorageStrategy != null && 
            BlobStorageHelper.HasBlobStorageProperties<TEntity>())
        {
            return await ExecuteWithBlobStorageAsync(request, cancellationToken).ConfigureAwait(false);
        }
        
        return await ExecuteDynamoDbOperationAsync(request, cancellationToken).ConfigureAwait(false);
    }

    private async Task<PutItemResponse> ExecuteWithBlobStorageAsync(
        PutItemRequest request, 
        CancellationToken cancellationToken)
    {
        return await BlobStorageHelper.ExecuteWithBlobStrategyAsync<TEntity, PutItemResponse>(
            _entity!,
            request.Item,
            _options,
            async () => await ExecuteDynamoDbOperationAsync(request, cancellationToken).ConfigureAwait(false),
            cancellationToken).ConfigureAwait(false);
    }

    private async Task<PutItemResponse> ExecuteDynamoDbOperationAsync(
        PutItemRequest request, 
        CancellationToken cancellationToken)
    {
        if (_logger?.IsEnabled(LogLevel.Information) == true)
        {
            _logger.LogInformation(LogEventIds.ExecutingPutItem,
                "Executing PutItem on table {TableName}. Condition: {ConditionExpression}",
                request.TableName ?? "Unknown", 
                request.ConditionExpression ?? "None");
        }
        
        if (_logger?.IsEnabled(LogLevel.Trace) == true && request.Item != null)
        {
            _logger.LogTrace(LogEventIds.ExecutingPutItem,
                "PutItem attributes: {AttributeCount}",
                request.Item.Count);
        }
        
        try
        {
            var response = await _dynamoDbClient.PutItemAsync(request, cancellationToken).ConfigureAwait(false);
            
            if (_logger?.IsEnabled(LogLevel.Information) == true)
            {
                _logger.LogInformation(LogEventIds.OperationComplete,
                    "PutItem completed. ConsumedCapacity: {ConsumedCapacity}",
                    response.ConsumedCapacity?.CapacityUnits ?? 0);
            }
            
            return response;
        }
        catch (Exception ex)
        {
            _logger?.LogError(LogEventIds.DynamoDbOperationError, ex,
                "PutItem failed on table {TableName}",
                request.TableName ?? "Unknown");
            throw;
        }
    }
}