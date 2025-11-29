using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using Oproto.FluentDynamoDb.Logging;
using Oproto.FluentDynamoDb.Requests.Interfaces;
using Oproto.FluentDynamoDb.Storage;

namespace Oproto.FluentDynamoDb.Requests;

/// <summary>
/// Fluent builder for composing DynamoDB BatchGetItem operations.
/// Accepts existing get request builders and extracts batch-compatible settings.
/// </summary>
/// <example>
/// <code>
/// var response = await DynamoDbBatch.Get
///     .Add(table.Get(pk1, sk1))
///     .Add(table.Get(pk2, sk2))
///     .Add(table2.Get(pk3, sk3))
///     .ExecuteAsync();
/// </code>
/// </example>
public class BatchGetBuilder
{
    private readonly Dictionary<string, KeysAndAttributes> _requestItems = new();
    private readonly List<string> _tableOrder = new();
    private readonly List<Dictionary<string, AttributeValue>> _requestedKeys = new();
    private IAmazonDynamoDB? _client;
    private IAmazonDynamoDB? _explicitClient;
    private ReturnConsumedCapacity? _returnConsumedCapacity;
    private IDynamoDbLogger _logger = NoOpLogger.Instance;

    /// <summary>
    /// Adds a get operation to the batch.
    /// Preserves projection expressions, attribute name mappings, and consistent read settings.
    /// </summary>
    /// <typeparam name="TEntity">The entity type being retrieved.</typeparam>
    /// <param name="builder">The get request builder containing the operation configuration.</param>
    /// <returns>The builder instance for method chaining.</returns>
    /// <example>
    /// <code>
    /// .Add(table.Get(pk, sk).WithProjection("name, email"))
    /// </code>
    /// </example>
    public BatchGetBuilder Add<TEntity>(GetItemRequestBuilder<TEntity> builder)
        where TEntity : class
    {
        InferClientIfNeeded(builder);
        
        var getBuilder = (ITransactableGetBuilder)builder;
        var tableName = getBuilder.GetTableName();
        var key = getBuilder.GetKey();
        var projectionExpression = getBuilder.GetProjectionExpression();
        var expressionAttributeNames = getBuilder.GetExpressionAttributeNames();
        var consistentRead = getBuilder.GetConsistentRead();
        
        // Track table order for response deserialization
        if (!_requestItems.ContainsKey(tableName))
        {
            _requestItems[tableName] = new KeysAndAttributes
            {
                Keys = new List<Dictionary<string, AttributeValue>>(),
                ProjectionExpression = projectionExpression,
                ExpressionAttributeNames = expressionAttributeNames,
                ConsistentRead = consistentRead
            };
        }
        
        // Track each item's table for maintaining order
        _tableOrder.Add(tableName);
        
        // Track the requested key for matching response items
        _requestedKeys.Add(key);
        
        // Add key to the batch
        _requestItems[tableName].Keys.Add(key);
        
        return this;
    }

    /// <summary>
    /// Explicitly sets the DynamoDB client to use for this batch operation.
    /// When specified, this client takes precedence over clients inferred from request builders.
    /// </summary>
    /// <param name="client">The DynamoDB client to use for executing the batch operation.</param>
    /// <returns>The builder instance for method chaining.</returns>
    /// <example>
    /// <code>
    /// .WithClient(myCustomClient)
    /// </code>
    /// </example>
    public BatchGetBuilder WithClient(IAmazonDynamoDB client)
    {
        _explicitClient = client;
        return this;
    }

    /// <summary>
    /// Configures the batch operation to return consumed capacity information.
    /// </summary>
    /// <param name="consumedCapacity">The level of consumed capacity information to return.</param>
    /// <returns>The builder instance for method chaining.</returns>
    /// <example>
    /// <code>
    /// .ReturnConsumedCapacity(ReturnConsumedCapacity.TOTAL)
    /// </code>
    /// </example>
    public BatchGetBuilder ReturnConsumedCapacity(ReturnConsumedCapacity consumedCapacity)
    {
        _returnConsumedCapacity = consumedCapacity;
        return this;
    }

    /// <summary>
    /// Sets the logger to use for diagnostic information.
    /// </summary>
    /// <param name="logger">The logger instance to use.</param>
    /// <returns>The builder instance for method chaining.</returns>
    /// <example>
    /// <code>
    /// .WithLogger(myLogger)
    /// </code>
    /// </example>
    public BatchGetBuilder WithLogger(IDynamoDbLogger logger)
    {
        _logger = logger ?? NoOpLogger.Instance;
        return this;
    }

    private void InferClientIfNeeded(object builder)
    {
        if (_client == null && _explicitClient == null)
        {
            // Extract client from builder using reflection or internal accessor
            _client = ExtractClientFromBuilder(builder);
        }
        else if (_explicitClient == null)
        {
            // Verify all builders use the same client
            var builderClient = ExtractClientFromBuilder(builder);
            if (!ReferenceEquals(builderClient, _client))
            {
                throw new InvalidOperationException(
                    "All request builders in a batch must use the same DynamoDB client instance. " +
                    "Use WithClient() to explicitly specify a client if needed.");
            }
        }
    }

    private IAmazonDynamoDB? ExtractClientFromBuilder(object builder)
    {
        // Use IHasDynamoDbClient interface to get the client without reflection
        if (builder is IHasDynamoDbClient clientProvider)
        {
            return clientProvider.GetDynamoDbClient();
        }
        
        throw new InvalidOperationException(
            $"Builder type {builder.GetType().Name} does not implement IHasDynamoDbClient");
    }

    /// <summary>
    /// Executes the batch get operation using the specified or inferred client.
    /// Client precedence: parameter > WithClient() > inferred from first builder.
    /// </summary>
    /// <param name="client">Optional DynamoDB client to use for execution (highest precedence).</param>
    /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
    /// <returns>A task representing the asynchronous operation, containing the BatchGetResponse wrapper.</returns>
    /// <exception cref="InvalidOperationException">Thrown when no client is available, batch is empty, or batch exceeds 100 operations.</exception>
    /// <example>
    /// <code>
    /// var response = await DynamoDbBatch.Get
    ///     .Add(table.Get(pk1, sk1))
    ///     .Add(table.Get(pk2, sk2))
    ///     .ExecuteAsync();
    /// 
    /// var user = response.GetItem&lt;User&gt;(0);
    /// </code>
    /// </example>
    public async Task<BatchGetResponse> ExecuteAsync(
        IAmazonDynamoDB? client = null,
        CancellationToken cancellationToken = default)
    {
        // Calculate total operations across all tables
        var totalOperations = _requestItems.Values.Sum(keysAndAttrs => keysAndAttrs.Keys.Count);
        
        // Check for empty batch first
        if (totalOperations == 0)
        {
            throw new InvalidOperationException(
                "Batch contains no operations. Add at least one operation using Add().");
        }

        // Determine effective client (parameter > explicit > inferred)
        var effectiveClient = client ?? _explicitClient ?? _client;
        
        if (effectiveClient == null)
        {
            throw new InvalidOperationException(
                "No DynamoDB client specified. Either pass a client to ExecuteAsync(), " +
                "call WithClient(), or add at least one request builder to infer the client.");
        }

        if (totalOperations > 100)
        {
            throw new InvalidOperationException(
                $"Batch contains {totalOperations} operations, but DynamoDB supports a maximum of 100 operations per batch. " +
                "Consider splitting your operations into multiple batches or using chunking logic.");
        }

        // Log operation counts per table at Information level
        if (_logger.IsEnabled(LogLevel.Information))
        {
            var tableDetails = string.Join(", ", _requestItems.Select(kvp => 
                $"{kvp.Key}: {kvp.Value.Keys.Count} gets"));
            
            _logger.LogInformation(
                LogEventIds.ExecutingBatchGet,
                "Executing batch get with {TotalOperations} operations across {TableCount} tables. Details: {TableDetails}",
                totalOperations, _requestItems.Count, tableDetails);
        }

        // Log detailed operation breakdown at Trace level
        if (_logger.IsEnabled(LogLevel.Trace))
        {
            foreach (var kvp in _requestItems)
            {
                var hasProjection = !string.IsNullOrEmpty(kvp.Value.ProjectionExpression);
                var consistentRead = kvp.Value.ConsistentRead;
                
                _logger.LogTrace(
                    LogEventIds.OperationBreakdown,
                    "Table {TableName}: {GetCount} get operations, Projection: {HasProjection}, ConsistentRead: {ConsistentRead}",
                    kvp.Key, kvp.Value.Keys.Count, hasProjection, consistentRead);
            }
        }

        var request = new BatchGetItemRequest
        {
            RequestItems = _requestItems,
            ReturnConsumedCapacity = _returnConsumedCapacity
        };

        try
        {
            var response = await effectiveClient.BatchGetItemAsync(request, cancellationToken);
            
            if (response == null)
            {
                throw new InvalidOperationException("DynamoDB client returned null response");
            }
            
            // Log unprocessed keys at Warning level
            if (response.UnprocessedKeys != null && response.UnprocessedKeys.Count > 0)
            {
                var unprocessedCount = response.UnprocessedKeys.Values.Sum(ka => ka.Keys?.Count ?? 0);
                _logger.LogWarning(
                    LogEventIds.UnprocessedItems,
                    "Batch get completed with {UnprocessedCount} unprocessed keys across {TableCount} tables. Consider implementing retry logic.",
                    unprocessedCount, response.UnprocessedKeys.Count);
            }
            else if (_logger.IsEnabled(LogLevel.Information))
            {
                _logger.LogInformation(
                    LogEventIds.OperationComplete,
                    "Batch get completed successfully with {OperationCount} operations across {TableCount} tables",
                    totalOperations, _requestItems.Count);
            }
            
            // Wrap response with table order and requested keys for proper deserialization
            return new BatchGetResponse(response, _tableOrder, _requestedKeys);
        }
        catch (Exception ex)
        {
            // Log error with operation details
            _logger.LogError(
                LogEventIds.DynamoDbOperationError,
                ex,
                "Batch get failed with {TotalOperations} operations across {TableCount} tables. Error: {ErrorMessage}",
                totalOperations, _requestItems.Count, ex.Message);
            
            throw;
        }
    }

    /// <summary>
    /// Executes the batch and deserializes a single item.
    /// Convenience method for batches with one get operation.
    /// </summary>
    /// <typeparam name="T1">The entity type for the first item.</typeparam>
    /// <param name="client">Optional DynamoDB client to use for execution.</param>
    /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
    /// <returns>The deserialized entity, or null if missing.</returns>
    /// <example>
    /// <code>
    /// var user = await DynamoDbBatch.Get
    ///     .Add(userTable.Get(userId))
    ///     .ExecuteAndMapAsync&lt;User&gt;();
    /// </code>
    /// </example>
    public async Task<T1?> ExecuteAndMapAsync<T1>(
        IAmazonDynamoDB? client = null,
        CancellationToken cancellationToken = default)
        where T1 : class, IDynamoDbEntity
    {
        var response = await ExecuteAsync(client, cancellationToken);
        return response.GetItem<T1>(0);
    }

    /// <summary>
    /// Executes the batch and deserializes two items.
    /// Convenience method for batches with two get operations.
    /// </summary>
    /// <typeparam name="T1">The entity type for the first item.</typeparam>
    /// <typeparam name="T2">The entity type for the second item.</typeparam>
    /// <param name="client">Optional DynamoDB client to use for execution.</param>
    /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
    /// <returns>A tuple containing the deserialized entities (nulls for missing items).</returns>
    /// <example>
    /// <code>
    /// var (user, order) = await DynamoDbBatch.Get
    ///     .Add(userTable.Get(userId))
    ///     .Add(orderTable.Get(orderId))
    ///     .ExecuteAndMapAsync&lt;User, Order&gt;();
    /// </code>
    /// </example>
    public async Task<(T1?, T2?)> ExecuteAndMapAsync<T1, T2>(
        IAmazonDynamoDB? client = null,
        CancellationToken cancellationToken = default)
        where T1 : class, IDynamoDbEntity
        where T2 : class, IDynamoDbEntity
    {
        var response = await ExecuteAsync(client, cancellationToken);
        return (response.GetItem<T1>(0), response.GetItem<T2>(1));
    }

    /// <summary>
    /// Executes the batch and deserializes three items.
    /// Convenience method for batches with three get operations.
    /// </summary>
    /// <typeparam name="T1">The entity type for the first item.</typeparam>
    /// <typeparam name="T2">The entity type for the second item.</typeparam>
    /// <typeparam name="T3">The entity type for the third item.</typeparam>
    /// <param name="client">Optional DynamoDB client to use for execution.</param>
    /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
    /// <returns>A tuple containing the deserialized entities (nulls for missing items).</returns>
    /// <example>
    /// <code>
    /// var (user, order, payment) = await DynamoDbBatch.Get
    ///     .Add(userTable.Get(userId))
    ///     .Add(orderTable.Get(orderId))
    ///     .Add(paymentTable.Get(paymentId))
    ///     .ExecuteAndMapAsync&lt;User, Order, Payment&gt;();
    /// </code>
    /// </example>
    public async Task<(T1?, T2?, T3?)> ExecuteAndMapAsync<T1, T2, T3>(
        IAmazonDynamoDB? client = null,
        CancellationToken cancellationToken = default)
        where T1 : class, IDynamoDbEntity
        where T2 : class, IDynamoDbEntity
        where T3 : class, IDynamoDbEntity
    {
        var response = await ExecuteAsync(client, cancellationToken);
        return (
            response.GetItem<T1>(0), 
            response.GetItem<T2>(1), 
            response.GetItem<T3>(2)
        );
    }

    /// <summary>
    /// Executes the batch and deserializes four items.
    /// Convenience method for batches with four get operations.
    /// </summary>
    public async Task<(T1?, T2?, T3?, T4?)> ExecuteAndMapAsync<T1, T2, T3, T4>(
        IAmazonDynamoDB? client = null,
        CancellationToken cancellationToken = default)
        where T1 : class, IDynamoDbEntity
        where T2 : class, IDynamoDbEntity
        where T3 : class, IDynamoDbEntity
        where T4 : class, IDynamoDbEntity
    {
        var response = await ExecuteAsync(client, cancellationToken);
        return (
            response.GetItem<T1>(0), 
            response.GetItem<T2>(1), 
            response.GetItem<T3>(2),
            response.GetItem<T4>(3)
        );
    }

    /// <summary>
    /// Executes the batch and deserializes five items.
    /// Convenience method for batches with five get operations.
    /// </summary>
    public async Task<(T1?, T2?, T3?, T4?, T5?)> ExecuteAndMapAsync<T1, T2, T3, T4, T5>(
        IAmazonDynamoDB? client = null,
        CancellationToken cancellationToken = default)
        where T1 : class, IDynamoDbEntity
        where T2 : class, IDynamoDbEntity
        where T3 : class, IDynamoDbEntity
        where T4 : class, IDynamoDbEntity
        where T5 : class, IDynamoDbEntity
    {
        var response = await ExecuteAsync(client, cancellationToken);
        return (
            response.GetItem<T1>(0), 
            response.GetItem<T2>(1), 
            response.GetItem<T3>(2),
            response.GetItem<T4>(3),
            response.GetItem<T5>(4)
        );
    }

    /// <summary>
    /// Executes the batch and deserializes six items.
    /// Convenience method for batches with six get operations.
    /// </summary>
    public async Task<(T1?, T2?, T3?, T4?, T5?, T6?)> ExecuteAndMapAsync<T1, T2, T3, T4, T5, T6>(
        IAmazonDynamoDB? client = null,
        CancellationToken cancellationToken = default)
        where T1 : class, IDynamoDbEntity
        where T2 : class, IDynamoDbEntity
        where T3 : class, IDynamoDbEntity
        where T4 : class, IDynamoDbEntity
        where T5 : class, IDynamoDbEntity
        where T6 : class, IDynamoDbEntity
    {
        var response = await ExecuteAsync(client, cancellationToken);
        return (
            response.GetItem<T1>(0), 
            response.GetItem<T2>(1), 
            response.GetItem<T3>(2),
            response.GetItem<T4>(3),
            response.GetItem<T5>(4),
            response.GetItem<T6>(5)
        );
    }

    /// <summary>
    /// Executes the batch and deserializes seven items.
    /// Convenience method for batches with seven get operations.
    /// </summary>
    public async Task<(T1?, T2?, T3?, T4?, T5?, T6?, T7?)> ExecuteAndMapAsync<T1, T2, T3, T4, T5, T6, T7>(
        IAmazonDynamoDB? client = null,
        CancellationToken cancellationToken = default)
        where T1 : class, IDynamoDbEntity
        where T2 : class, IDynamoDbEntity
        where T3 : class, IDynamoDbEntity
        where T4 : class, IDynamoDbEntity
        where T5 : class, IDynamoDbEntity
        where T6 : class, IDynamoDbEntity
        where T7 : class, IDynamoDbEntity
    {
        var response = await ExecuteAsync(client, cancellationToken);
        return (
            response.GetItem<T1>(0), 
            response.GetItem<T2>(1), 
            response.GetItem<T3>(2),
            response.GetItem<T4>(3),
            response.GetItem<T5>(4),
            response.GetItem<T6>(5),
            response.GetItem<T7>(6)
        );
    }

    /// <summary>
    /// Executes the batch and deserializes eight items.
    /// Convenience method for batches with eight get operations.
    /// </summary>
    public async Task<(T1?, T2?, T3?, T4?, T5?, T6?, T7?, T8?)> ExecuteAndMapAsync<T1, T2, T3, T4, T5, T6, T7, T8>(
        IAmazonDynamoDB? client = null,
        CancellationToken cancellationToken = default)
        where T1 : class, IDynamoDbEntity
        where T2 : class, IDynamoDbEntity
        where T3 : class, IDynamoDbEntity
        where T4 : class, IDynamoDbEntity
        where T5 : class, IDynamoDbEntity
        where T6 : class, IDynamoDbEntity
        where T7 : class, IDynamoDbEntity
        where T8 : class, IDynamoDbEntity
    {
        var response = await ExecuteAsync(client, cancellationToken);
        return (
            response.GetItem<T1>(0), 
            response.GetItem<T2>(1), 
            response.GetItem<T3>(2),
            response.GetItem<T4>(3),
            response.GetItem<T5>(4),
            response.GetItem<T6>(5),
            response.GetItem<T7>(6),
            response.GetItem<T8>(7)
        );
    }
}
