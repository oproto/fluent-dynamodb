using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using Oproto.FluentDynamoDb.Entities;
using Oproto.FluentDynamoDb.Logging;
using Oproto.FluentDynamoDb.Requests;
using Oproto.FluentDynamoDb.Requests.Extensions;

namespace Oproto.FluentDynamoDb.Storage;

/// <summary>
/// A table class that provides generic entity operations for any DynamoDB table.
/// Use this when you need to work with multiple entity types on a single table
/// without using the source generator.
/// </summary>
/// <remarks>
/// <para>
/// GenericTable is useful for:
/// <list type="bullet">
/// <item><description>Testing scenarios where you need generic entity access without source generation</description></item>
/// <item><description>Ad-hoc table access in integration tests</description></item>
/// <item><description>Working with multiple entity types on a single-table design without source generation</description></item>
/// </list>
/// </para>
/// <para>
/// For production code, prefer using the source generator with [DynamoDbTable] attribute
/// which generates type-safe, optimized table classes.
/// </para>
/// <para>
/// This class is internal and not part of the public API. It is exposed to test projects
/// via InternalsVisibleTo for testing purposes only.
/// </para>
/// </remarks>
internal class GenericTable : IDynamoDbTable
{
    private readonly IDynamoDbLogger _logger;

    /// <summary>
    /// Gets the DynamoDB client used for operations.
    /// </summary>
    public IAmazonDynamoDB DynamoDbClient { get; }

    /// <summary>
    /// Gets the name of the DynamoDB table.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Gets the configuration options for this table.
    /// </summary>
    public FluentDynamoDbOptions Options { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="GenericTable"/> class.
    /// </summary>
    /// <param name="client">The DynamoDB client to use for operations.</param>
    /// <param name="tableName">The name of the DynamoDB table.</param>
    /// <param name="options">Optional configuration options. If null, uses sensible defaults.</param>
    public GenericTable(
        IAmazonDynamoDB client,
        string tableName,
        FluentDynamoDbOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(client);
        ArgumentException.ThrowIfNullOrWhiteSpace(tableName);

        DynamoDbClient = client;
        Name = tableName;
        Options = options ?? new FluentDynamoDbOptions();
        _logger = Options.Logger;
    }


    #region Query Operations

    /// <summary>
    /// Creates a new Query operation builder for the specified entity type.
    /// </summary>
    /// <typeparam name="TEntity">The entity or projection type to query. Must implement IReadOnlyEntity.</typeparam>
    /// <returns>A QueryRequestBuilder configured for this table.</returns>
    public QueryRequestBuilder<TEntity> Query<TEntity>() where TEntity : class, IReadOnlyEntity
        => new QueryRequestBuilder<TEntity>(DynamoDbClient, Options).ForTable(Name);

    /// <summary>
    /// Creates a new Query operation builder with a format string key condition.
    /// </summary>
    /// <typeparam name="TEntity">The entity or projection type to query. Must implement IReadOnlyEntity.</typeparam>
    /// <param name="keyConditionExpression">The key condition expression with format placeholders.</param>
    /// <param name="values">The values to substitute for placeholders.</param>
    /// <returns>A QueryRequestBuilder configured with the key condition.</returns>
    public QueryRequestBuilder<TEntity> Query<TEntity>(string keyConditionExpression, params object[] values)
        where TEntity : class, IReadOnlyEntity
        => WithConditionExpressionExtensions.Where(Query<TEntity>(), keyConditionExpression, values);

    #endregion

    #region Scan Operations

    /// <summary>
    /// Creates a new Scan operation builder for the specified entity type.
    /// </summary>
    /// <typeparam name="TEntity">The entity or projection type to scan. Must implement IReadOnlyEntity.</typeparam>
    /// <returns>A ScanRequestBuilder configured for this table.</returns>
    /// <remarks>
    /// WARNING: Scan operations read every item in a table and can be very expensive.
    /// Use Query operations instead whenever possible.
    /// </remarks>
    public ScanRequestBuilder<TEntity> Scan<TEntity>() where TEntity : class, IReadOnlyEntity
        => new ScanRequestBuilder<TEntity>(DynamoDbClient, Options).ForTable(Name);

    #endregion

    #region Get Operations

    /// <summary>
    /// Creates a new GetItem operation builder for the specified entity type.
    /// </summary>
    /// <typeparam name="TEntity">The entity or projection type to get. Must implement IReadOnlyEntity.</typeparam>
    /// <returns>A GetItemRequestBuilder configured for this table.</returns>
    public GetItemRequestBuilder<TEntity> Get<TEntity>() where TEntity : class, IReadOnlyEntity
        => new GetItemRequestBuilder<TEntity>(DynamoDbClient, Options).ForTable(Name);

    /// <summary>
    /// Creates a GetItem operation builder configured with a pre-built SDK request.
    /// </summary>
    /// <typeparam name="TEntity">The entity or projection type to get. Must implement IReadOnlyEntity.</typeparam>
    /// <param name="request">The pre-built GetItemRequest.</param>
    /// <returns>A GetItemRequestBuilder configured with the request.</returns>
    public GetItemRequestBuilder<TEntity> Get<TEntity>(GetItemRequest request) where TEntity : class, IReadOnlyEntity
        => Get<TEntity>().WithRequest(request);

    /// <summary>
    /// Executes a pre-built GetItemRequest and hydrates the result.
    /// </summary>
    /// <typeparam name="TEntity">The entity type to get. Must implement IDynamoDbEntity for entity mapping.</typeparam>
    /// <param name="request">The pre-built GetItemRequest.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The hydrated entity or null if not found.</returns>
    public async Task<TEntity?> GetAsync<TEntity>(GetItemRequest request, CancellationToken cancellationToken = default)
        where TEntity : class, IDynamoDbEntity
        => await EntityExecuteAsyncExtensions.GetItemAsync(Get<TEntity>(request), cancellationToken).ConfigureAwait(false);

    #endregion

    #region Put Operations

    /// <summary>
    /// Creates a new PutItem operation builder for the specified entity type.
    /// </summary>
    /// <typeparam name="TEntity">The entity type to put.</typeparam>
    /// <returns>A PutItemRequestBuilder configured for this table.</returns>
    public PutItemRequestBuilder<TEntity> Put<TEntity>() where TEntity : class, IDynamoDbEntity, new()
        => new PutItemRequestBuilder<TEntity>(DynamoDbClient, Options).ForTable(Name);

    /// <summary>
    /// Creates a PutItem operation builder configured with a pre-built SDK request.
    /// </summary>
    /// <typeparam name="TEntity">The entity type to put.</typeparam>
    /// <param name="request">The pre-built PutItemRequest.</param>
    /// <returns>A PutItemRequestBuilder configured with the request.</returns>
    public PutItemRequestBuilder<TEntity> Put<TEntity>(PutItemRequest request) where TEntity : class, IDynamoDbEntity, new()
        => Put<TEntity>().WithRequest(request);

    /// <summary>
    /// Executes a pre-built PutItemRequest.
    /// </summary>
    /// <typeparam name="TEntity">The entity type to put.</typeparam>
    /// <param name="request">The pre-built PutItemRequest.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    public async Task PutAsync<TEntity>(PutItemRequest request, CancellationToken cancellationToken = default)
        where TEntity : class, IDynamoDbEntity, new()
        => await EntityExecuteAsyncExtensions.PutAsync(Put<TEntity>(request), cancellationToken).ConfigureAwait(false);

    /// <summary>
    /// Puts an entity into the table.
    /// </summary>
    /// <typeparam name="TEntity">The entity type to put.</typeparam>
    /// <param name="entity">The entity to put.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    public async Task PutAsync<TEntity>(TEntity entity, CancellationToken cancellationToken = default)
        where TEntity : class, IDynamoDbEntity, new()
        => await EntityExecuteAsyncExtensions.PutAsync(Put<TEntity>().WithItem(entity), cancellationToken).ConfigureAwait(false);

    #endregion

    #region Update Operations

    /// <summary>
    /// Creates a new UpdateItem operation builder for the specified entity type.
    /// </summary>
    /// <typeparam name="TEntity">The entity type to update.</typeparam>
    /// <returns>An UpdateItemRequestBuilder configured for this table.</returns>
    public UpdateItemRequestBuilder<TEntity> Update<TEntity>() where TEntity : class, IDynamoDbEntity, new()
        => new UpdateItemRequestBuilder<TEntity>(DynamoDbClient, Options).ForTable(Name);

    /// <summary>
    /// Creates an UpdateItem operation builder configured with a pre-built SDK request.
    /// </summary>
    /// <typeparam name="TEntity">The entity type to update.</typeparam>
    /// <param name="request">The pre-built UpdateItemRequest.</param>
    /// <returns>An UpdateItemRequestBuilder configured with the request.</returns>
    public UpdateItemRequestBuilder<TEntity> Update<TEntity>(UpdateItemRequest request) where TEntity : class, IDynamoDbEntity, new()
        => Update<TEntity>().WithRequest(request);

    /// <summary>
    /// Executes a pre-built UpdateItemRequest.
    /// </summary>
    /// <typeparam name="TEntity">The entity type to update.</typeparam>
    /// <param name="request">The pre-built UpdateItemRequest.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    public async Task UpdateAsync<TEntity>(UpdateItemRequest request, CancellationToken cancellationToken = default)
        where TEntity : class, IDynamoDbEntity, new()
        => await EntityExecuteAsyncExtensions.UpdateAsync(Update<TEntity>(request), cancellationToken).ConfigureAwait(false);

    #endregion

    #region Delete Operations

    /// <summary>
    /// Creates a new DeleteItem operation builder for the specified entity type.
    /// </summary>
    /// <typeparam name="TEntity">The entity type to delete.</typeparam>
    /// <returns>A DeleteItemRequestBuilder configured for this table.</returns>
    public DeleteItemRequestBuilder<TEntity> Delete<TEntity>() where TEntity : class, IDynamoDbEntity, new()
        => new DeleteItemRequestBuilder<TEntity>(DynamoDbClient, Options).ForTable(Name);

    /// <summary>
    /// Creates a DeleteItem operation builder configured with a pre-built SDK request.
    /// </summary>
    /// <typeparam name="TEntity">The entity type to delete.</typeparam>
    /// <param name="request">The pre-built DeleteItemRequest.</param>
    /// <returns>A DeleteItemRequestBuilder configured with the request.</returns>
    public DeleteItemRequestBuilder<TEntity> Delete<TEntity>(DeleteItemRequest request) where TEntity : class, IDynamoDbEntity, new()
        => Delete<TEntity>().WithRequest(request);

    /// <summary>
    /// Executes a pre-built DeleteItemRequest.
    /// </summary>
    /// <typeparam name="TEntity">The entity type to delete.</typeparam>
    /// <param name="request">The pre-built DeleteItemRequest.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    public async Task DeleteAsync<TEntity>(DeleteItemRequest request, CancellationToken cancellationToken = default)
        where TEntity : class, IDynamoDbEntity, new()
        => await EntityExecuteAsyncExtensions.DeleteAsync(Delete<TEntity>(request), cancellationToken).ConfigureAwait(false);

    #endregion

    #region ConditionCheck Operations

    /// <summary>
    /// Creates a new ConditionCheck operation builder for the specified entity type.
    /// Used in transactions to verify conditions without modifying data.
    /// </summary>
    /// <typeparam name="TEntity">The entity type to check.</typeparam>
    /// <returns>A ConditionCheckBuilder configured for this table.</returns>
    public ConditionCheckBuilder<TEntity> ConditionCheck<TEntity>() where TEntity : class
        => new ConditionCheckBuilder<TEntity>(DynamoDbClient, Name, Options);

    #endregion

    #region PartiQL Operations

    /// <summary>
    /// Creates a PartiQL request builder for executing SQL-like queries with a specific entity type.
    /// </summary>
    /// <typeparam name="TEntity">The entity type for result hydration.</typeparam>
    /// <param name="statement">The PartiQL statement with format placeholders.</param>
    /// <param name="parameters">The parameter values to substitute for placeholders.</param>
    /// <returns>A PartiQLRequestBuilder configured with the statement.</returns>
    public PartiQLRequestBuilder<TEntity> ExecutePartiQL<TEntity>(string statement, params object[] parameters)
        where TEntity : class, IDynamoDbEntity, new()
        => new PartiQLRequestBuilder<TEntity>(DynamoDbClient, Options).WithStatement(statement, parameters);

    /// <summary>
    /// Creates a PartiQL request builder for executing SQL-like queries with DynamicEntity.
    /// </summary>
    /// <param name="statement">The PartiQL statement with format placeholders.</param>
    /// <param name="parameters">The parameter values to substitute for placeholders.</param>
    /// <returns>A PartiQLRequestBuilder configured with the statement for DynamicEntity.</returns>
    public PartiQLRequestBuilder<DynamicEntity> ExecutePartiQL(string statement, params object[] parameters)
        => new PartiQLRequestBuilder<DynamicEntity>(DynamoDbClient, Options).WithStatement(statement, parameters);

    #endregion

    #region Direct SDK Request Methods

    /// <summary>
    /// Creates a Query operation builder configured with a pre-built SDK request.
    /// </summary>
    /// <typeparam name="TEntity">The entity or projection type to query. Must implement IReadOnlyEntity.</typeparam>
    /// <param name="request">The pre-built QueryRequest.</param>
    /// <returns>A QueryRequestBuilder configured with the request.</returns>
    public QueryRequestBuilder<TEntity> Query<TEntity>(QueryRequest request) where TEntity : class, IReadOnlyEntity
        => Query<TEntity>().WithRequest(request);

    /// <summary>
    /// Executes a pre-built QueryRequest and hydrates the results.
    /// </summary>
    /// <typeparam name="TEntity">The entity type to query. Must implement IDynamoDbEntity for entity mapping.</typeparam>
    /// <param name="request">The pre-built QueryRequest.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>A list of hydrated entities.</returns>
    public async Task<List<TEntity>> QueryAsync<TEntity>(QueryRequest request, CancellationToken cancellationToken = default)
        where TEntity : class, IDynamoDbEntity
        => await Requests.Extensions.EntityExecuteAsyncExtensions.ToListAsync(Query<TEntity>(request), cancellationToken).ConfigureAwait(false);

    /// <summary>
    /// Creates a Scan operation builder configured with a pre-built SDK request.
    /// </summary>
    /// <typeparam name="TEntity">The entity or projection type to scan. Must implement IReadOnlyEntity.</typeparam>
    /// <param name="request">The pre-built ScanRequest.</param>
    /// <returns>A ScanRequestBuilder configured with the request.</returns>
    public ScanRequestBuilder<TEntity> Scan<TEntity>(ScanRequest request) where TEntity : class, IReadOnlyEntity
        => Scan<TEntity>().WithRequest(request);

    /// <summary>
    /// Executes a pre-built ScanRequest and hydrates the results.
    /// </summary>
    /// <typeparam name="TEntity">The entity type to scan. Must implement IDynamoDbEntity for entity mapping.</typeparam>
    /// <param name="request">The pre-built ScanRequest.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>A list of hydrated entities.</returns>
    public async Task<List<TEntity>> ScanAsync<TEntity>(ScanRequest request, CancellationToken cancellationToken = default)
        where TEntity : class, IDynamoDbEntity
        => await Requests.Extensions.EntityExecuteAsyncExtensions.ToListAsync(Scan<TEntity>(request), cancellationToken).ConfigureAwait(false);

    #endregion
}
