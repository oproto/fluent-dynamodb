using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using Oproto.FluentDynamoDb.Logging;
using Oproto.FluentDynamoDb.Requests.Interfaces;
using Oproto.FluentDynamoDb.Storage;

namespace Oproto.FluentDynamoDb.Requests;

/// <summary>
/// Fluent builder for composing DynamoDB TransactGetItems operations.
/// Accepts existing get request builders and extracts transaction-compatible settings.
/// </summary>
/// <example>
/// <code>
/// var response = await DynamoDbTransactions.Get
///     .Add(table.Get(pk, sk))
///     .Add(table2.Get(pk2, sk2).WithProjection("name, email"))
///     .ExecuteAsync();
/// </code>
/// </example>
public class TransactionGetBuilder
{
    private readonly List<TransactGetItem> _items = new();
    private IAmazonDynamoDB? _client;
    private IAmazonDynamoDB? _explicitClient;
    private ReturnConsumedCapacity? _returnConsumedCapacity;
    private IDynamoDbLogger _logger = NoOpLogger.Instance;
    private FluentDynamoDbOptions? _options;

    /// <summary>
    /// Adds a get operation to the transaction.
    /// </summary>
    /// <typeparam name="TEntity">The entity type being retrieved.</typeparam>
    /// <param name="builder">The get request builder containing the operation configuration.</param>
    /// <returns>The builder instance for method chaining.</returns>
    /// <example>
    /// <code>
    /// .Add(table.Get(pk, sk).WithProjection("name, email"))
    /// </code>
    /// </example>
    public TransactionGetBuilder Add<TEntity>(GetItemRequestBuilder<TEntity> builder)
        where TEntity : class
    {
        InferClientIfNeeded(builder);
        
        // Capture options from first builder that has them
        if (_options == null && builder is IHasDynamoDbClient clientProvider)
        {
            _options = clientProvider.GetOptions();
        }
        
        var item = new TransactGetItem
        {
            Get = new Get
            {
                TableName = ((ITransactableGetBuilder)builder).GetTableName(),
                Key = ((ITransactableGetBuilder)builder).GetKey(),
                ProjectionExpression = ((ITransactableGetBuilder)builder).GetProjectionExpression(),
                ExpressionAttributeNames = ((ITransactableGetBuilder)builder).GetExpressionAttributeNames()
            }
        };
        
        _items.Add(item);
        return this;
    }

    /// <summary>
    /// Explicitly sets the DynamoDB client to use for this transaction.
    /// When specified, this client takes precedence over clients inferred from request builders.
    /// </summary>
    /// <param name="client">The DynamoDB client to use for executing the transaction.</param>
    /// <returns>The builder instance for method chaining.</returns>
    /// <example>
    /// <code>
    /// .WithClient(myCustomClient)
    /// </code>
    /// </example>
    public TransactionGetBuilder WithClient(IAmazonDynamoDB client)
    {
        _explicitClient = client;
        return this;
    }

    /// <summary>
    /// Configures the transaction to return consumed capacity information.
    /// </summary>
    /// <param name="consumedCapacity">The level of consumed capacity information to return.</param>
    /// <returns>The builder instance for method chaining.</returns>
    /// <example>
    /// <code>
    /// .ReturnConsumedCapacity(ReturnConsumedCapacity.TOTAL)
    /// </code>
    /// </example>
    public TransactionGetBuilder ReturnConsumedCapacity(ReturnConsumedCapacity consumedCapacity)
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
    public TransactionGetBuilder WithLogger(IDynamoDbLogger logger)
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
                    "All request builders in a transaction must use the same DynamoDB client instance. " +
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
    /// Executes the transaction using the specified or inferred client.
    /// Client precedence: parameter > WithClient() > inferred from first builder.
    /// </summary>
    /// <param name="client">Optional DynamoDB client to use for execution (highest precedence).</param>
    /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
    /// <returns>A task representing the asynchronous operation, containing the TransactionGetResponse wrapper.</returns>
    /// <exception cref="InvalidOperationException">Thrown when no client is available or transaction is empty.</exception>
    /// <exception cref="ValidationException">Thrown when transaction exceeds 100 operations.</exception>
    /// <example>
    /// <code>
    /// var response = await DynamoDbTransactions.Get
    ///     .Add(table.Get(pk, sk))
    ///     .Add(table2.Get(pk2, sk2))
    ///     .ExecuteAsync();
    /// 
    /// var user = response.GetItem&lt;User&gt;(0);
    /// var order = response.GetItem&lt;Order&gt;(1);
    /// </code>
    /// </example>
    public async Task<TransactionGetResponse> ExecuteAsync(
        IAmazonDynamoDB? client = null,
        CancellationToken cancellationToken = default)
    {
        // Check for empty transaction first
        if (_items.Count == 0)
        {
            throw new InvalidOperationException(
                "Transaction contains no operations. Add at least one operation using Add().");
        }

        // Determine effective client (parameter > explicit > inferred)
        var effectiveClient = client ?? _explicitClient ?? _client;
        
        if (effectiveClient == null)
        {
            throw new InvalidOperationException(
                "No DynamoDB client specified. Either pass a client to ExecuteAsync(), " +
                "call WithClient(), or add at least one request builder to infer the client.");
        }

        if (_items.Count > 100)
        {
            throw new InvalidOperationException(
                $"Transaction contains {_items.Count} operations, but DynamoDB supports a maximum of 100 operations per transaction.");
        }

        // Log operation count before execution
        if (_logger.IsEnabled(LogLevel.Information))
        {
            _logger.LogInformation(
                LogEventIds.ExecutingTransactionGet,
                "Executing transaction get with {OperationCount} get operations",
                _items.Count);
        }

        var request = new TransactGetItemsRequest
        {
            TransactItems = _items,
            ReturnConsumedCapacity = _returnConsumedCapacity
        };

        try
        {
            var response = await effectiveClient.TransactGetItemsAsync(request, cancellationToken);
            
            if (response == null)
            {
                throw new InvalidOperationException("DynamoDB client returned null response");
            }
            
            // Log successful completion with consumed capacity
            if (_logger.IsEnabled(LogLevel.Information))
            {
                if (response.ConsumedCapacity != null && response.ConsumedCapacity.Count > 0)
                {
                    var totalCapacity = response.ConsumedCapacity.Sum(c => c.CapacityUnits ?? 0);
                    _logger.LogInformation(
                        LogEventIds.ConsumedCapacity,
                        "Transaction get completed successfully. Total consumed capacity: {TotalCapacity} units across {TableCount} tables",
                        totalCapacity, response.ConsumedCapacity.Count);
                }
                else
                {
                    _logger.LogInformation(
                        LogEventIds.OperationComplete,
                        "Transaction get completed successfully with {OperationCount} operations",
                        _items.Count);
                }
            }
            
            return new TransactionGetResponse(response, _options);
        }
        catch (Exception ex)
        {
            // Log error with operation details
            _logger.LogError(
                LogEventIds.DynamoDbOperationError,
                ex,
                "Transaction get failed with {OperationCount} operations. Error: {ErrorMessage}",
                _items.Count, ex.Message);
            
            throw;
        }
    }

    /// <summary>
    /// Executes the transaction and deserializes a single item.
    /// Convenience method for transactions with one get operation.
    /// </summary>
    /// <typeparam name="T1">The entity type for the first item.</typeparam>
    /// <param name="client">Optional DynamoDB client to use for execution.</param>
    /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
    /// <returns>The deserialized entity, or null if missing.</returns>
    /// <example>
    /// <code>
    /// var user = await DynamoDbTransactions.Get
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
    /// Executes the transaction and deserializes two items.
    /// Convenience method for transactions with two get operations.
    /// </summary>
    /// <typeparam name="T1">The entity type for the first item.</typeparam>
    /// <typeparam name="T2">The entity type for the second item.</typeparam>
    /// <param name="client">Optional DynamoDB client to use for execution.</param>
    /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
    /// <returns>A tuple containing the deserialized entities (nulls for missing items).</returns>
    /// <example>
    /// <code>
    /// var (user, order) = await DynamoDbTransactions.Get
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
    /// Executes the transaction and deserializes three items.
    /// Convenience method for transactions with three get operations.
    /// </summary>
    /// <typeparam name="T1">The entity type for the first item.</typeparam>
    /// <typeparam name="T2">The entity type for the second item.</typeparam>
    /// <typeparam name="T3">The entity type for the third item.</typeparam>
    /// <param name="client">Optional DynamoDB client to use for execution.</param>
    /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
    /// <returns>A tuple containing the deserialized entities (nulls for missing items).</returns>
    /// <example>
    /// <code>
    /// var (user, order, payment) = await DynamoDbTransactions.Get
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
    /// Executes the transaction and deserializes four items.
    /// Convenience method for transactions with four get operations.
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
    /// Executes the transaction and deserializes five items.
    /// Convenience method for transactions with five get operations.
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
    /// Executes the transaction and deserializes six items.
    /// Convenience method for transactions with six get operations.
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
    /// Executes the transaction and deserializes seven items.
    /// Convenience method for transactions with seven get operations.
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
    /// Executes the transaction and deserializes eight items.
    /// Convenience method for transactions with eight get operations.
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
