using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using Oproto.FluentDynamoDb.Context;
using Oproto.FluentDynamoDb.Entities;
using Oproto.FluentDynamoDb.Logging;

namespace Oproto.FluentDynamoDb.Requests;

/// <summary>
/// Builder for batch PartiQL operations.
/// Allows composing multiple PartiQL statements (SELECT, INSERT, UPDATE, DELETE) into a single batch.
/// </summary>
/// <remarks>
/// <para>
/// Unlike BatchWriteItem/BatchGetItem which have separate read/write operations,
/// DynamoDB's BatchExecuteStatement API handles all statement types in a single batch.
/// </para>
/// <para>
/// The builder follows the same patterns as BatchGetBuilder and TransactionGetBuilder:
/// <list type="bullet">
/// <item><description>ExecuteAsync() returns a BatchPartiQLResponse wrapper</description></item>
/// <item><description>ExecuteAndMapAsync&lt;T1&gt;() etc. for typed tuple results</description></item>
/// <item><description>Response wrapper with GetItem&lt;T&gt;(index) for accessing individual SELECT results</description></item>
/// </list>
/// </para>
/// </remarks>
/// <example>
/// <code>
/// // Batch SELECT operations
/// var response = await DynamoDbBatch.PartiQL
///     .Add(table.ExecutePartiQL&lt;User&gt;("SELECT * FROM Users WHERE pk = {0}", "USER#123"))
///     .Add(table.ExecutePartiQL&lt;Order&gt;("SELECT * FROM Orders WHERE pk = {0}", "ORDER#456"))
///     .ExecuteAsync();
/// 
/// var user = response.GetItem&lt;User&gt;(0);
/// var order = response.GetItem&lt;Order&gt;(1);
/// 
/// // Or use tuple convenience method
/// var (user, order) = await DynamoDbBatch.PartiQL
///     .Add(table.ExecutePartiQL&lt;User&gt;("SELECT * FROM Users WHERE pk = {0}", "USER#123"))
///     .Add(table.ExecutePartiQL&lt;Order&gt;("SELECT * FROM Orders WHERE pk = {0}", "ORDER#456"))
///     .ExecuteAndMapAsync&lt;User, Order&gt;();
/// 
/// // Mixed operations (SELECT + UPDATE/DELETE)
/// await DynamoDbBatch.PartiQL
///     .Add(table.ExecutePartiQL&lt;User&gt;("UPDATE Users SET name = {0} WHERE pk = {1}", "Jane", "USER#123"))
///     .Add(table.ExecutePartiQL&lt;User&gt;("DELETE FROM Users WHERE pk = {0}", "USER#456"))
///     .ExecuteAsync();
/// </code>
/// </example>
public class BatchPartiQLBuilder
{
    private readonly List<BatchStatementRequest> _statements = new();
    private IAmazonDynamoDB? _client;
    private IAmazonDynamoDB? _explicitClient;
    private FluentDynamoDbOptions? _options;
    private IDynamoDbLogger _logger = NoOpLogger.Instance;

    /// <summary>
    /// Adds a PartiQL statement builder to the batch.
    /// </summary>
    /// <typeparam name="TEntity">The entity type for the statement.</typeparam>
    /// <param name="builder">The PartiQL request builder containing the statement configuration.</param>
    /// <returns>The builder instance for method chaining.</returns>
    /// <example>
    /// <code>
    /// .Add(table.ExecutePartiQL&lt;User&gt;("SELECT * FROM Users WHERE pk = {0}", "USER#123"))
    /// </code>
    /// </example>
    public BatchPartiQLBuilder Add<TEntity>(PartiQLRequestBuilder<TEntity> builder)
        where TEntity : class, IDynamoDbEntity
    {
        ArgumentNullException.ThrowIfNull(builder);
        InferClientIfNeeded(builder);

        // Capture options from first builder that has them
        _options ??= builder.Options;

        var request = builder.ToRequest();
        _statements.Add(new BatchStatementRequest
        {
            Statement = request.Statement,
            Parameters = request.Parameters
        });

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
    public BatchPartiQLBuilder WithClient(IAmazonDynamoDB client)
    {
        _explicitClient = client;
        return this;
    }

    /// <summary>
    /// Sets the logger to use for diagnostic information.
    /// </summary>
    /// <param name="logger">The logger instance to use.</param>
    /// <returns>The builder instance for method chaining.</returns>
    public BatchPartiQLBuilder WithLogger(IDynamoDbLogger logger)
    {
        _logger = logger ?? NoOpLogger.Instance;
        return this;
    }

    private void InferClientIfNeeded<TEntity>(PartiQLRequestBuilder<TEntity> builder)
        where TEntity : class, IDynamoDbEntity
    {
        if (_client == null && _explicitClient == null)
        {
            _client = builder.GetDynamoDbClient();
        }
    }


    /// <summary>
    /// Executes all statements in the batch.
    /// Returns a response wrapper for accessing results.
    /// </summary>
    /// <param name="client">Optional DynamoDB client to use for execution (highest precedence).</param>
    /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
    /// <returns>A BatchPartiQLResponse wrapper for accessing results.</returns>
    /// <exception cref="InvalidOperationException">Thrown when no client is available or batch is empty.</exception>
    /// <example>
    /// <code>
    /// var response = await DynamoDbBatch.PartiQL
    ///     .Add(table.ExecutePartiQL&lt;User&gt;("SELECT * FROM Users WHERE pk = {0}", "USER#123"))
    ///     .ExecuteAsync();
    /// 
    /// var user = response.GetItem&lt;User&gt;(0);
    /// </code>
    /// </example>
    public async Task<BatchPartiQLResponse> ExecuteAsync(
        IAmazonDynamoDB? client = null,
        CancellationToken cancellationToken = default)
    {
        if (_statements.Count == 0)
        {
            throw new InvalidOperationException(
                "Batch contains no statements. Add at least one statement using Add().");
        }

        var effectiveClient = client ?? _explicitClient ?? _client;

        if (effectiveClient == null)
        {
            throw new InvalidOperationException(
                "No DynamoDB client specified. Either pass a client to ExecuteAsync(), " +
                "call WithClient(), or add at least one request builder to infer the client.");
        }

        if (_logger.IsEnabled(LogLevel.Information))
        {
            _logger.LogInformation(
                LogEventIds.ExecutingBatchGet,
                "Executing batch PartiQL with {StatementCount} statements",
                _statements.Count);
        }

        var request = new BatchExecuteStatementRequest
        {
            Statements = _statements
        };

        try
        {
            var response = await effectiveClient.BatchExecuteStatementAsync(request, cancellationToken).ConfigureAwait(false);

            // Populate operation context
            DynamoDbOperationContext.Current = new OperationContextData
            {
                OperationType = "BatchPartiQL",
                ResponseMetadata = response.ResponseMetadata,
                ConsumedCapacity = response.ConsumedCapacity?.FirstOrDefault()
            };
            DynamoDbOperationContextDiagnostics.RaiseContextAssigned(DynamoDbOperationContext.Current);

            if (_logger.IsEnabled(LogLevel.Information))
            {
                _logger.LogInformation(
                    LogEventIds.OperationComplete,
                    "Batch PartiQL completed successfully with {StatementCount} statements",
                    _statements.Count);
            }

            return new BatchPartiQLResponse(response, _options);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                LogEventIds.DynamoDbOperationError,
                ex,
                "Batch PartiQL failed with {StatementCount} statements. Error: {ErrorMessage}",
                _statements.Count, ex.Message);
            throw;
        }
    }

    #region ExecuteAndMapAsync Overloads

    /// <summary>
    /// Executes the batch and deserializes a single SELECT result.
    /// </summary>
    /// <typeparam name="T1">The entity type for the first statement.</typeparam>
    /// <param name="client">Optional DynamoDB client to use for execution.</param>
    /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
    /// <returns>The deserialized entity, or null if not found.</returns>
    public async Task<T1?> ExecuteAndMapAsync<T1>(
        IAmazonDynamoDB? client = null,
        CancellationToken cancellationToken = default)
        where T1 : class, IDynamoDbEntity
    {
        var response = await ExecuteAsync(client, cancellationToken).ConfigureAwait(false);
        return response.GetItem<T1>(0);
    }

    /// <summary>
    /// Executes the batch and deserializes two SELECT results.
    /// </summary>
    public async Task<(T1?, T2?)> ExecuteAndMapAsync<T1, T2>(
        IAmazonDynamoDB? client = null,
        CancellationToken cancellationToken = default)
        where T1 : class, IDynamoDbEntity
        where T2 : class, IDynamoDbEntity
    {
        var response = await ExecuteAsync(client, cancellationToken).ConfigureAwait(false);
        return (response.GetItem<T1>(0), response.GetItem<T2>(1));
    }

    /// <summary>
    /// Executes the batch and deserializes three SELECT results.
    /// </summary>
    public async Task<(T1?, T2?, T3?)> ExecuteAndMapAsync<T1, T2, T3>(
        IAmazonDynamoDB? client = null,
        CancellationToken cancellationToken = default)
        where T1 : class, IDynamoDbEntity
        where T2 : class, IDynamoDbEntity
        where T3 : class, IDynamoDbEntity
    {
        var response = await ExecuteAsync(client, cancellationToken).ConfigureAwait(false);
        return (
            response.GetItem<T1>(0),
            response.GetItem<T2>(1),
            response.GetItem<T3>(2)
        );
    }

    /// <summary>
    /// Executes the batch and deserializes four SELECT results.
    /// </summary>
    public async Task<(T1?, T2?, T3?, T4?)> ExecuteAndMapAsync<T1, T2, T3, T4>(
        IAmazonDynamoDB? client = null,
        CancellationToken cancellationToken = default)
        where T1 : class, IDynamoDbEntity
        where T2 : class, IDynamoDbEntity
        where T3 : class, IDynamoDbEntity
        where T4 : class, IDynamoDbEntity
    {
        var response = await ExecuteAsync(client, cancellationToken).ConfigureAwait(false);
        return (
            response.GetItem<T1>(0),
            response.GetItem<T2>(1),
            response.GetItem<T3>(2),
            response.GetItem<T4>(3)
        );
    }

    /// <summary>
    /// Executes the batch and deserializes five SELECT results.
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
        var response = await ExecuteAsync(client, cancellationToken).ConfigureAwait(false);
        return (
            response.GetItem<T1>(0),
            response.GetItem<T2>(1),
            response.GetItem<T3>(2),
            response.GetItem<T4>(3),
            response.GetItem<T5>(4)
        );
    }

    /// <summary>
    /// Executes the batch and deserializes six SELECT results.
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
        var response = await ExecuteAsync(client, cancellationToken).ConfigureAwait(false);
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
    /// Executes the batch and deserializes seven SELECT results.
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
        var response = await ExecuteAsync(client, cancellationToken).ConfigureAwait(false);
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
    /// Executes the batch and deserializes eight SELECT results.
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
        var response = await ExecuteAsync(client, cancellationToken).ConfigureAwait(false);
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

    #endregion
}
