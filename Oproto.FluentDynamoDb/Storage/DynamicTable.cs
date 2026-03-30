using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using Oproto.FluentDynamoDb.Entities;
using Oproto.FluentDynamoDb.Logging;
using Oproto.FluentDynamoDb.Requests;

namespace Oproto.FluentDynamoDb.Storage;

/// <summary>
/// A table class for schema-less access to any DynamoDB table.
/// Use this when you need to work with tables without defining entity classes.
/// </summary>
/// <remarks>
/// <para>
/// DynamicTable enables schema-less access to any DynamoDB table using <see cref="DynamicEntity"/>.
/// All attributes from DynamoDB items are stored in the <see cref="DynamicEntity.DynamicFields"/> collection.
/// </para>
/// <para>
/// This class is useful for:
/// <list type="bullet">
/// <item><description>Exploring unknown table schemas</description></item>
/// <item><description>Building migration tools</description></item>
/// <item><description>Working with tables that have no fixed schema</description></item>
/// <item><description>Accessing tables without defining entity classes</description></item>
/// </list>
/// </para>
/// <para>
/// When <see cref="KeyOptions"/> is configured, typed key methods (GetAsync, DeleteAsync, UpdateAsync)
/// are available that accept string or numeric parameters instead of raw AttributeValue objects.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// // Create a DynamicTable with string keys
/// var keyOptions = new DynamicTableKeyOptions
/// {
///     PartitionKeyName = "pk",
///     PartitionKeyType = ScalarAttributeType.S,
///     SortKeyName = "sk",
///     SortKeyType = ScalarAttributeType.S
/// };
/// var table = new DynamicTable(dynamoDbClient, "MyTable", keyOptions);
/// 
/// // Query using lambda expressions
/// var results = await table.Query()
///     .Where(x => x.DynamicFields["pk"] == "USER#123")
///     .ToListAsync();
/// 
/// // Get item using typed keys
/// var item = await table.GetAsync("USER#123", "PROFILE");
/// 
/// // Access fields from results
/// foreach (var entity in results)
/// {
///     var name = entity.DynamicFields.GetString("name");
///     var age = entity.DynamicFields.GetInt("age");
/// }
/// </code>
/// </example>
public class DynamicTable
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
    /// Gets the key configuration for this table.
    /// When configured, enables typed key methods (GetAsync, DeleteAsync, UpdateAsync).
    /// </summary>
    public DynamicTableKeyOptions? KeyOptions { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="DynamicTable"/> class.
    /// </summary>
    /// <param name="client">The DynamoDB client to use for operations.</param>
    /// <param name="tableName">The name of the DynamoDB table.</param>
    /// <param name="keyOptions">Optional key configuration. When provided, enables typed key methods.</param>
    /// <param name="options">Optional configuration options. If null, uses sensible defaults.</param>
    /// <exception cref="ArgumentNullException">Thrown when client or tableName is null.</exception>
    /// <exception cref="ArgumentException">Thrown when tableName is empty or whitespace.</exception>
    public DynamicTable(
        IAmazonDynamoDB client,
        string tableName,
        DynamicTableKeyOptions? keyOptions = null,
        FluentDynamoDbOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(client);
        ArgumentException.ThrowIfNullOrWhiteSpace(tableName);

        DynamoDbClient = client;
        Name = tableName;
        KeyOptions = keyOptions;
        Options = options ?? new FluentDynamoDbOptions();
        _logger = Options.Logger;
    }

    #region Query and Scan Operations

    /// <summary>
    /// Creates a new Query operation builder for this table.
    /// Use this to query items using the primary key or a secondary index.
    /// </summary>
    /// <returns>A QueryRequestBuilder configured for this table with DynamicEntity.</returns>
    /// <example>
    /// <code>
    /// // Query with lambda expression
    /// var results = await table.Query()
    ///     .Where(x => x.DynamicFields["pk"] == "USER#123")
    ///     .ToListAsync();
    /// 
    /// // Query with format string
    /// var results = await table.Query()
    ///     .Where("pk = {0}", "USER#123")
    ///     .ToListAsync();
    /// </code>
    /// </example>
    public QueryRequestBuilder<DynamicEntity> Query()
        => new QueryRequestBuilder<DynamicEntity>(DynamoDbClient, Options).ForTable(Name);

    /// <summary>
    /// Creates a new Scan operation builder for this table.
    /// </summary>
    /// <returns>A ScanRequestBuilder configured for this table with DynamicEntity.</returns>
    /// <remarks>
    /// <para>
    /// WARNING: Scan operations read every item in a table and can be very expensive.
    /// Use Query operations instead whenever possible.
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// // Scan with filter
    /// var results = await table.Scan()
    ///     .WithFilter(x => x.DynamicFields["status"] == "active")
    ///     .ToListAsync();
    /// </code>
    /// </example>
    public ScanRequestBuilder<DynamicEntity> Scan()
        => new ScanRequestBuilder<DynamicEntity>(DynamoDbClient, Options).ForTable(Name);

    #endregion

    #region Validation Helpers

    /// <summary>
    /// Validates that KeyOptions is configured before using typed key methods.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown when KeyOptions is not configured.</exception>
    private void ValidateKeyOptions()
    {
        if (KeyOptions == null)
        {
            throw new InvalidOperationException(
                "Key options must be configured to use typed key methods. " +
                "Use the constructor overload that accepts DynamicTableKeyOptions, " +
                "or use the GetAsync(AttributeValue, AttributeValue?) overload for raw key access.");
        }
    }

    /// <summary>
    /// Validates that KeyOptions is configured and includes a sort key.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown when KeyOptions is not configured or has no sort key.</exception>
    private void ValidateKeyOptionsWithSortKey()
    {
        ValidateKeyOptions();
        if (!KeyOptions!.HasSortKey)
        {
            throw new InvalidOperationException(
                "Sort key was provided but DynamicTableKeyOptions does not define a sort key. " +
                "Configure SortKeyName and SortKeyType in DynamicTableKeyOptions.");
        }
    }

    /// <summary>
    /// Creates an AttributeValue for a string key.
    /// </summary>
    private static AttributeValue CreateStringKey(string value) => new() { S = value };

    /// <summary>
    /// Creates an AttributeValue for a numeric key.
    /// </summary>
    private static AttributeValue CreateNumericKey(long value) => new() { N = value.ToString() };

    #endregion

    #region GetAsync Operations

    /// <summary>
    /// Gets an item by string partition key.
    /// Requires <see cref="KeyOptions"/> to be configured with a string partition key.
    /// </summary>
    /// <param name="partitionKey">The partition key value.</param>
    /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
    /// <returns>The DynamicEntity if found, or null if not found.</returns>
    /// <exception cref="InvalidOperationException">Thrown when KeyOptions is not configured.</exception>
    public async Task<DynamicEntity?> GetAsync(string partitionKey, CancellationToken cancellationToken = default)
    {
        ValidateKeyOptions();
        return await GetAsync(CreateStringKey(partitionKey), null, cancellationToken);
    }

    /// <summary>
    /// Gets an item by string partition key and string sort key.
    /// Requires <see cref="KeyOptions"/> to be configured with string keys.
    /// </summary>
    /// <param name="partitionKey">The partition key value.</param>
    /// <param name="sortKey">The sort key value.</param>
    /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
    /// <returns>The DynamicEntity if found, or null if not found.</returns>
    /// <exception cref="InvalidOperationException">Thrown when KeyOptions is not configured or has no sort key.</exception>
    public async Task<DynamicEntity?> GetAsync(string partitionKey, string sortKey, CancellationToken cancellationToken = default)
    {
        ValidateKeyOptionsWithSortKey();
        return await GetAsync(CreateStringKey(partitionKey), CreateStringKey(sortKey), cancellationToken);
    }

    /// <summary>
    /// Gets an item by numeric partition key.
    /// Requires <see cref="KeyOptions"/> to be configured with a numeric partition key.
    /// </summary>
    /// <param name="partitionKey">The partition key value.</param>
    /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
    /// <returns>The DynamicEntity if found, or null if not found.</returns>
    /// <exception cref="InvalidOperationException">Thrown when KeyOptions is not configured.</exception>
    public async Task<DynamicEntity?> GetAsync(long partitionKey, CancellationToken cancellationToken = default)
    {
        ValidateKeyOptions();
        return await GetAsync(CreateNumericKey(partitionKey), null, cancellationToken);
    }

    /// <summary>
    /// Gets an item by numeric partition key and numeric sort key.
    /// Requires <see cref="KeyOptions"/> to be configured with numeric keys.
    /// </summary>
    /// <param name="partitionKey">The partition key value.</param>
    /// <param name="sortKey">The sort key value.</param>
    /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
    /// <returns>The DynamicEntity if found, or null if not found.</returns>
    /// <exception cref="InvalidOperationException">Thrown when KeyOptions is not configured or has no sort key.</exception>
    public async Task<DynamicEntity?> GetAsync(long partitionKey, long sortKey, CancellationToken cancellationToken = default)
    {
        ValidateKeyOptionsWithSortKey();
        return await GetAsync(CreateNumericKey(partitionKey), CreateNumericKey(sortKey), cancellationToken);
    }

    /// <summary>
    /// Gets an item by raw AttributeValue keys.
    /// This method is always available regardless of KeyOptions configuration.
    /// </summary>
    /// <param name="partitionKey">The partition key as an AttributeValue.</param>
    /// <param name="sortKey">The optional sort key as an AttributeValue.</param>
    /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
    /// <returns>The DynamicEntity if found, or null if not found.</returns>
    public async Task<DynamicEntity?> GetAsync(
        AttributeValue partitionKey,
        AttributeValue? sortKey = null,
        CancellationToken cancellationToken = default)
    {
        var pkName = KeyOptions?.PartitionKeyName ?? "pk";
        var skName = KeyOptions?.SortKeyName ?? "sk";

        var key = new Dictionary<string, AttributeValue>
        {
            [pkName] = partitionKey
        };

        if (sortKey != null)
        {
            key[skName] = sortKey;
        }

        var request = new GetItemRequest
        {
            TableName = Name,
            Key = key
        };

        if (Options.DefaultConsistentRead.HasValue)
        {
            request.ConsistentRead = Options.DefaultConsistentRead.Value;
        }

        var response = await DynamoDbClient.GetItemAsync(request, cancellationToken);

        if (response.Item == null || response.Item.Count == 0)
        {
            return null;
        }

        return DynamicEntity.FromDynamoDb<DynamicEntity>(response.Item, Options);
    }

    #endregion

    #region DeleteAsync Operations

    /// <summary>
    /// Deletes an item by string partition key.
    /// Requires <see cref="KeyOptions"/> to be configured with a string partition key.
    /// </summary>
    /// <param name="partitionKey">The partition key value.</param>
    /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    /// <exception cref="InvalidOperationException">Thrown when KeyOptions is not configured.</exception>
    public async Task DeleteAsync(string partitionKey, CancellationToken cancellationToken = default)
    {
        ValidateKeyOptions();
        await DeleteAsync(CreateStringKey(partitionKey), null, cancellationToken);
    }

    /// <summary>
    /// Deletes an item by string partition key and string sort key.
    /// Requires <see cref="KeyOptions"/> to be configured with string keys.
    /// </summary>
    /// <param name="partitionKey">The partition key value.</param>
    /// <param name="sortKey">The sort key value.</param>
    /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    /// <exception cref="InvalidOperationException">Thrown when KeyOptions is not configured or has no sort key.</exception>
    public async Task DeleteAsync(string partitionKey, string sortKey, CancellationToken cancellationToken = default)
    {
        ValidateKeyOptionsWithSortKey();
        await DeleteAsync(CreateStringKey(partitionKey), CreateStringKey(sortKey), cancellationToken);
    }

    /// <summary>
    /// Deletes an item by numeric partition key.
    /// Requires <see cref="KeyOptions"/> to be configured with a numeric partition key.
    /// </summary>
    /// <param name="partitionKey">The partition key value.</param>
    /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    /// <exception cref="InvalidOperationException">Thrown when KeyOptions is not configured.</exception>
    public async Task DeleteAsync(long partitionKey, CancellationToken cancellationToken = default)
    {
        ValidateKeyOptions();
        await DeleteAsync(CreateNumericKey(partitionKey), null, cancellationToken);
    }

    /// <summary>
    /// Deletes an item by numeric partition key and numeric sort key.
    /// Requires <see cref="KeyOptions"/> to be configured with numeric keys.
    /// </summary>
    /// <param name="partitionKey">The partition key value.</param>
    /// <param name="sortKey">The sort key value.</param>
    /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    /// <exception cref="InvalidOperationException">Thrown when KeyOptions is not configured or has no sort key.</exception>
    public async Task DeleteAsync(long partitionKey, long sortKey, CancellationToken cancellationToken = default)
    {
        ValidateKeyOptionsWithSortKey();
        await DeleteAsync(CreateNumericKey(partitionKey), CreateNumericKey(sortKey), cancellationToken);
    }

    /// <summary>
    /// Deletes an item by raw AttributeValue keys.
    /// This method is always available regardless of KeyOptions configuration.
    /// </summary>
    /// <param name="partitionKey">The partition key as an AttributeValue.</param>
    /// <param name="sortKey">The optional sort key as an AttributeValue.</param>
    /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task DeleteAsync(
        AttributeValue partitionKey,
        AttributeValue? sortKey = null,
        CancellationToken cancellationToken = default)
    {
        var pkName = KeyOptions?.PartitionKeyName ?? "pk";
        var skName = KeyOptions?.SortKeyName ?? "sk";

        var key = new Dictionary<string, AttributeValue>
        {
            [pkName] = partitionKey
        };

        if (sortKey != null)
        {
            key[skName] = sortKey;
        }

        var request = new DeleteItemRequest
        {
            TableName = Name,
            Key = key
        };

        await DynamoDbClient.DeleteItemAsync(request, cancellationToken);
    }

    #endregion

    #region UpdateAsync Operations

    /// <summary>
    /// Creates an UpdateItem operation builder for an item identified by string partition key.
    /// Requires <see cref="KeyOptions"/> to be configured with a string partition key.
    /// </summary>
    /// <param name="partitionKey">The partition key value.</param>
    /// <returns>An UpdateItemRequestBuilder configured with the key.</returns>
    /// <exception cref="InvalidOperationException">Thrown when KeyOptions is not configured.</exception>
    public UpdateItemRequestBuilder<DynamicEntity> Update(string partitionKey)
    {
        ValidateKeyOptions();
        return CreateUpdateBuilder(CreateStringKey(partitionKey), null);
    }

    /// <summary>
    /// Creates an UpdateItem operation builder for an item identified by string partition key and sort key.
    /// Requires <see cref="KeyOptions"/> to be configured with string keys.
    /// </summary>
    /// <param name="partitionKey">The partition key value.</param>
    /// <param name="sortKey">The sort key value.</param>
    /// <returns>An UpdateItemRequestBuilder configured with the keys.</returns>
    /// <exception cref="InvalidOperationException">Thrown when KeyOptions is not configured or has no sort key.</exception>
    public UpdateItemRequestBuilder<DynamicEntity> Update(string partitionKey, string sortKey)
    {
        ValidateKeyOptionsWithSortKey();
        return CreateUpdateBuilder(CreateStringKey(partitionKey), CreateStringKey(sortKey));
    }

    /// <summary>
    /// Creates an UpdateItem operation builder for an item identified by numeric partition key.
    /// Requires <see cref="KeyOptions"/> to be configured with a numeric partition key.
    /// </summary>
    /// <param name="partitionKey">The partition key value.</param>
    /// <returns>An UpdateItemRequestBuilder configured with the key.</returns>
    /// <exception cref="InvalidOperationException">Thrown when KeyOptions is not configured.</exception>
    public UpdateItemRequestBuilder<DynamicEntity> Update(long partitionKey)
    {
        ValidateKeyOptions();
        return CreateUpdateBuilder(CreateNumericKey(partitionKey), null);
    }

    /// <summary>
    /// Creates an UpdateItem operation builder for an item identified by numeric partition key and sort key.
    /// Requires <see cref="KeyOptions"/> to be configured with numeric keys.
    /// </summary>
    /// <param name="partitionKey">The partition key value.</param>
    /// <param name="sortKey">The sort key value.</param>
    /// <returns>An UpdateItemRequestBuilder configured with the keys.</returns>
    /// <exception cref="InvalidOperationException">Thrown when KeyOptions is not configured or has no sort key.</exception>
    public UpdateItemRequestBuilder<DynamicEntity> Update(long partitionKey, long sortKey)
    {
        ValidateKeyOptionsWithSortKey();
        return CreateUpdateBuilder(CreateNumericKey(partitionKey), CreateNumericKey(sortKey));
    }

    /// <summary>
    /// Creates an UpdateItem operation builder for an item identified by raw AttributeValue keys.
    /// This method is always available regardless of KeyOptions configuration.
    /// </summary>
    /// <param name="partitionKey">The partition key as an AttributeValue.</param>
    /// <param name="sortKey">The optional sort key as an AttributeValue.</param>
    /// <returns>An UpdateItemRequestBuilder configured with the keys.</returns>
    public UpdateItemRequestBuilder<DynamicEntity> Update(AttributeValue partitionKey, AttributeValue? sortKey = null)
    {
        return CreateUpdateBuilder(partitionKey, sortKey);
    }

    /// <summary>
    /// Creates an UpdateItemRequestBuilder with the specified keys.
    /// </summary>
    private UpdateItemRequestBuilder<DynamicEntity> CreateUpdateBuilder(AttributeValue partitionKey, AttributeValue? sortKey)
    {
        var pkName = KeyOptions?.PartitionKeyName ?? "pk";
        var skName = KeyOptions?.SortKeyName ?? "sk";

        var builder = new UpdateItemRequestBuilder<DynamicEntity>(DynamoDbClient, Options)
            .ForTable(Name)
            .SetKey(key =>
            {
                key[pkName] = partitionKey;
                if (sortKey != null)
                {
                    key[skName] = sortKey;
                }
            });

        return builder;
    }

    #endregion

    #region PutAsync Operations

    /// <summary>
    /// Puts a DynamicEntity into the table.
    /// The entity's DynamicFields collection is serialized to DynamoDB attributes.
    /// </summary>
    /// <param name="entity">The DynamicEntity to put.</param>
    /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    /// <exception cref="ArgumentNullException">Thrown when entity is null.</exception>
    /// <example>
    /// <code>
    /// var entity = new DynamicEntity();
    /// entity.DynamicFields.SetString("pk", "USER#123");
    /// entity.DynamicFields.SetString("sk", "PROFILE");
    /// entity.DynamicFields.SetString("name", "John Doe");
    /// entity.DynamicFields.SetInt("age", 30);
    /// 
    /// await table.PutAsync(entity);
    /// </code>
    /// </example>
    public async Task PutAsync(DynamicEntity entity, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entity);

        var item = DynamicEntity.ToDynamoDb(entity, Options);

        var request = new PutItemRequest
        {
            TableName = Name,
            Item = item
        };

        await DynamoDbClient.PutItemAsync(request, cancellationToken);
    }

    #endregion

    #region PartiQL Methods

    /// <summary>
    /// Creates a PartiQL request builder for executing SQL-like queries.
    /// Supports format specifiers like {0:o} for ISO 8601 dates.
    /// </summary>
    /// <param name="statement">The PartiQL statement with format placeholders.</param>
    /// <param name="parameters">The parameter values to substitute for placeholders.</param>
    /// <returns>A PartiQLRequestBuilder configured with the statement for DynamicEntity.</returns>
    /// <example>
    /// <code>
    /// var items = await dynamicTable.ExecutePartiQL(
    ///     "SELECT * FROM MyTable WHERE pk = {0}",
    ///     "ITEM#789")
    ///     .ToListAsync();
    /// 
    /// foreach (var item in items)
    /// {
    ///     var name = item.DynamicFields.GetString("name");
    /// }
    /// 
    /// // INSERT/UPDATE/DELETE statements
    /// await dynamicTable.ExecutePartiQL(
    ///     "UPDATE MyTable SET name = {0} WHERE pk = {1}",
    ///     "Jane Doe", "ITEM#789")
    ///     .ExecuteAsync();
    /// </code>
    /// </example>
    public PartiQLRequestBuilder<DynamicEntity> ExecutePartiQL(
        string statement,
        params object[] parameters)
    {
        return new PartiQLRequestBuilder<DynamicEntity>(DynamoDbClient, Options)
            .WithStatement(statement, parameters);
    }

    #endregion

    #region Direct SDK Request Methods

    /// <summary>
    /// Creates a Query operation builder configured with a pre-built SDK request.
    /// Use this when you have an existing QueryRequest and want to leverage entity hydration.
    /// </summary>
    /// <param name="request">The pre-built QueryRequest.</param>
    /// <returns>A QueryRequestBuilder configured with the request.</returns>
    public QueryRequestBuilder<DynamicEntity> Query(QueryRequest request)
        => Query().WithRequest(request);

    /// <summary>
    /// Executes a pre-built QueryRequest and hydrates the results to DynamicEntity.
    /// </summary>
    /// <param name="request">The pre-built QueryRequest.</param>
    /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
    /// <returns>A list of hydrated DynamicEntity instances.</returns>
    public async Task<List<DynamicEntity>> QueryAsync(QueryRequest request, CancellationToken cancellationToken = default)
        => await Requests.Extensions.EntityExecuteAsyncExtensions.ToListAsync(Query(request), cancellationToken);

    /// <summary>
    /// Creates a Scan operation builder configured with a pre-built SDK request.
    /// Use this when you have an existing ScanRequest and want to leverage entity hydration.
    /// </summary>
    /// <param name="request">The pre-built ScanRequest.</param>
    /// <returns>A ScanRequestBuilder configured with the request.</returns>
    public ScanRequestBuilder<DynamicEntity> Scan(ScanRequest request)
        => Scan().WithRequest(request);

    /// <summary>
    /// Executes a pre-built ScanRequest and hydrates the results to DynamicEntity.
    /// </summary>
    /// <param name="request">The pre-built ScanRequest.</param>
    /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
    /// <returns>A list of hydrated DynamicEntity instances.</returns>
    public async Task<List<DynamicEntity>> ScanAsync(ScanRequest request, CancellationToken cancellationToken = default)
        => await Requests.Extensions.EntityExecuteAsyncExtensions.ToListAsync(Scan(request), cancellationToken);

    /// <summary>
    /// Executes a pre-built GetItemRequest and hydrates the result to DynamicEntity.
    /// </summary>
    /// <param name="request">The pre-built GetItemRequest.</param>
    /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
    /// <returns>The hydrated DynamicEntity or null if not found.</returns>
    public async Task<DynamicEntity?> GetAsync(GetItemRequest request, CancellationToken cancellationToken = default)
    {
        var response = await DynamoDbClient.GetItemAsync(request, cancellationToken);
        
        if (response.Item == null || response.Item.Count == 0)
            return null;
        
        return DynamicEntity.FromDynamoDb<DynamicEntity>(response.Item, Options);
    }

    /// <summary>
    /// Executes a pre-built PutItemRequest.
    /// </summary>
    /// <param name="request">The pre-built PutItemRequest.</param>
    /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task PutAsync(PutItemRequest request, CancellationToken cancellationToken = default)
        => await DynamoDbClient.PutItemAsync(request, cancellationToken);

    /// <summary>
    /// Executes a pre-built UpdateItemRequest.
    /// </summary>
    /// <param name="request">The pre-built UpdateItemRequest.</param>
    /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task UpdateAsync(UpdateItemRequest request, CancellationToken cancellationToken = default)
        => await DynamoDbClient.UpdateItemAsync(request, cancellationToken);

    /// <summary>
    /// Executes a pre-built DeleteItemRequest.
    /// </summary>
    /// <param name="request">The pre-built DeleteItemRequest.</param>
    /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task DeleteAsync(DeleteItemRequest request, CancellationToken cancellationToken = default)
        => await DynamoDbClient.DeleteItemAsync(request, cancellationToken);

    #endregion
}
