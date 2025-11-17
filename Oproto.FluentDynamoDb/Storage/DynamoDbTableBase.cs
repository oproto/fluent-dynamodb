using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using Oproto.FluentDynamoDb.Logging;
using Oproto.FluentDynamoDb.Requests;

namespace Oproto.FluentDynamoDb.Storage;

/// <summary>
/// Base implementation for DynamoDB table abstraction
/// </summary>
public abstract class DynamoDbTableBase : IDynamoDbTable
{
    /// <summary>
    /// Initializes a new instance of the DynamoDbTableBase class.
    /// </summary>
    /// <param name="client">The DynamoDB client.</param>
    /// <param name="tableName">The name of the table.</param>
    public DynamoDbTableBase(IAmazonDynamoDB client, string tableName)
        : this(client, tableName, null)
    {
    }

    /// <summary>
    /// Initializes a new instance of the DynamoDbTableBase class with optional logger.
    /// </summary>
    /// <param name="client">The DynamoDB client.</param>
    /// <param name="tableName">The name of the table.</param>
    /// <param name="logger">Optional logger for DynamoDB operations. If null, uses a no-op logger.</param>
    public DynamoDbTableBase(IAmazonDynamoDB client, string tableName, IDynamoDbLogger? logger)
        : this(client, tableName, logger, null)
    {
    }

    /// <summary>
    /// Initializes a new instance of the DynamoDbTableBase class with optional logger and field encryptor.
    /// </summary>
    /// <param name="client">The DynamoDB client.</param>
    /// <param name="tableName">The name of the table.</param>
    /// <param name="logger">Optional logger for DynamoDB operations. If null, uses a no-op logger.</param>
    /// <param name="fieldEncryptor">Optional field encryptor for encrypting sensitive properties. If null, encryption is disabled.</param>
    public DynamoDbTableBase(IAmazonDynamoDB client, string tableName, IDynamoDbLogger? logger, IFieldEncryptor? fieldEncryptor)
    {
        DynamoDbClient = client;
        Name = tableName;
        Logger = logger ?? NoOpLogger.Instance;
        FieldEncryptor = fieldEncryptor;
    }

    public IAmazonDynamoDB DynamoDbClient { get; private init; }
    public string Name { get; private init; }
    
    /// <summary>
    /// Gets the logger for DynamoDB operations.
    /// </summary>
    protected IDynamoDbLogger Logger { get; private init; }

    /// <summary>
    /// Gets the field encryptor for encrypting and decrypting sensitive properties.
    /// Returns null if encryption is not configured for this table.
    /// </summary>
    protected IFieldEncryptor? FieldEncryptor { get; private init; }

    /// <summary>
    /// Gets the field encryptor for this table.
    /// This method is used internally by transaction builders to access the encryptor.
    /// </summary>
    /// <returns>The field encryptor, or null if encryption is not configured.</returns>
    internal IFieldEncryptor? GetFieldEncryptor() => FieldEncryptor;

    /// <summary>
    /// Gets the current encryption context identifier, checking both operation-specific and ambient contexts.
    /// This context is used by the field encryptor to determine the appropriate encryption key.
    /// </summary>
    /// <returns>The current encryption context identifier, or null if not set.</returns>
    /// <remarks>
    /// The encryption context can be set using DynamoDbOperationContext.EncryptionContextId or per-operation
    /// using WithEncryptionContext() on request builders. The per-operation context takes
    /// precedence over the ambient context.
    /// </remarks>
    protected string? GetEncryptionContext()
    {
        // Check for operation-specific context first (set via WithEncryptionContext extension)
        var operationContext = Requests.Extensions.EncryptionExtensions.GetOperationContext();
        if (operationContext != null)
        {
            return operationContext;
        }

        // Fall back to ambient context from unified context
        return DynamoDbOperationContext.EncryptionContextId;
    }

    /// <summary>
    /// Creates a new Query operation builder for this table.
    /// Use this to query items using the primary key or a secondary index.
    /// </summary>
    /// <returns>A QueryRequestBuilder configured for this table.</returns>
    /// <example>
    /// <code>
    /// // Manual query configuration
    /// var results = await table.Query&lt;MyEntity&gt;()
    ///     .Where("pk = {0}", "USER#123")
    ///     .ExecuteAsync();
    /// 
    /// // Or use the expression overload
    /// var results = await table.Query("pk = {0}", "USER#123").ExecuteAsync();
    /// </code>
    /// </example>
    public QueryRequestBuilder<TEntity> Query<TEntity>() where TEntity : class => 
        new QueryRequestBuilder<TEntity>(DynamoDbClient, Logger).ForTable(Name);
    
    /// <summary>
    /// Creates a new Query operation builder with a key condition expression.
    /// Uses format string syntax for parameters: {0}, {1}, etc.
    /// </summary>
    /// <param name="keyConditionExpression">The key condition expression with format placeholders.</param>
    /// <param name="values">The values to substitute into the expression.</param>
    /// <returns>A QueryRequestBuilder configured with the key condition.</returns>
    /// <example>
    /// <code>
    /// // Simple partition key query
    /// var results = await table.Query("pk = {0}", "USER#123").ExecuteAsync();
    /// 
    /// // Composite key query
    /// var results = await table.Query("pk = {0} AND sk > {1}", "USER#123", "2024-01-01").ExecuteAsync();
    /// 
    /// // With begins_with
    /// var results = await table.Query("pk = {0} AND begins_with(sk, {1})", "USER#123", "ORDER#").ExecuteAsync();
    /// </code>
    /// </example>
    public QueryRequestBuilder<TEntity> Query<TEntity>(string keyConditionExpression, params object[] values) where TEntity : class
    {
        var builder = Query<TEntity>();
        return Requests.Extensions.WithConditionExpressionExtensions.Where(builder, keyConditionExpression, values);
    }
    
    /// <summary>
    /// Creates a new GetItem operation builder for this table.
    /// Base implementation provides parameterless version.
    /// Derived classes should override to provide key-specific overloads.
    /// </summary>
    /// <returns>A GetItemRequestBuilder configured for this table.</returns>
    /// <example>
    /// <code>
    /// // Manual key configuration
    /// var item = await table.Get&lt;MyEntity&gt;()
    ///     .WithKey("id", "123")
    ///     .WithProjection("name, email")
    ///     .ExecuteAsync();
    /// 
    /// // Or use derived class overload (if available)
    /// var item = await table.Get("123").ExecuteAsync();
    /// </code>
    /// </example>
    public virtual GetItemRequestBuilder<TEntity> Get<TEntity>() where TEntity : class => 
        new GetItemRequestBuilder<TEntity>(DynamoDbClient, Logger).ForTable(Name);
    
    /// <summary>
    /// Creates a new UpdateItem operation builder for this table.
    /// Base implementation provides parameterless version.
    /// Derived classes should override to provide key-specific overloads.
    /// </summary>
    /// <returns>An UpdateItemRequestBuilder configured for this table.</returns>
    public virtual UpdateItemRequestBuilder<TEntity> Update<TEntity>() where TEntity : class => 
        new UpdateItemRequestBuilder<TEntity>(DynamoDbClient, Logger)
            .ForTable(Name)
            .SetFieldEncryptor(FieldEncryptor);
    
    /// <summary>
    /// Creates a new DeleteItem operation builder for this table.
    /// Base implementation provides parameterless version.
    /// Derived classes should override to provide key-specific overloads.
    /// </summary>
    /// <returns>A DeleteItemRequestBuilder configured for this table.</returns>
    public virtual DeleteItemRequestBuilder<TEntity> Delete<TEntity>() where TEntity : class => 
        new DeleteItemRequestBuilder<TEntity>(DynamoDbClient, Logger).ForTable(Name);
    
    /// <summary>
    /// Creates a new PutItem operation builder for this table.
    /// </summary>
    /// <typeparam name="TEntity">The entity type to put.</typeparam>
    /// <returns>A PutItemRequestBuilder&lt;TEntity&gt; configured for this table.</returns>
    /// <example>
    /// <code>
    /// // Put an entity
    /// await table.Put&lt;MyEntity&gt;()
    ///     .WithItem(myEntity)
    ///     .ExecuteAsync();
    /// 
    /// // Put with condition
    /// await table.Put&lt;MyEntity&gt;()
    ///     .WithItem(myEntity)
    ///     .Where("attribute_not_exists(id)")
    ///     .ExecuteAsync();
    /// </code>
    /// </example>
    public PutItemRequestBuilder<TEntity> Put<TEntity>() where TEntity : class => 
        new PutItemRequestBuilder<TEntity>(DynamoDbClient, Logger).ForTable(Name);
    
    /// <summary>
    /// Creates a new ConditionCheck operation builder for this table.
    /// Condition checks verify conditions without modifying data and are used within transactions.
    /// </summary>
    /// <typeparam name="TEntity">The entity type to check.</typeparam>
    /// <returns>A ConditionCheckBuilder&lt;TEntity&gt; configured for this table.</returns>
    /// <example>
    /// <code>
    /// // Use in a transaction
    /// await DynamoDbTransactions.Write
    ///     .Add(table.ConditionCheck&lt;MyEntity&gt;()
    ///         .WithKey("id", "123")
    ///         .Where("attribute_exists(#status)")
    ///         .WithAttribute("#status", "status"))
    ///     .Add(table.Update&lt;MyEntity&gt;().WithKey("id", "456").Set(...))
    ///     .ExecuteAsync();
    /// </code>
    /// </example>
    public ConditionCheckBuilder<TEntity> ConditionCheck<TEntity>() where TEntity : class => 
        new ConditionCheckBuilder<TEntity>(DynamoDbClient, Name);
    
    /// <summary>
    /// Creates a new Scan operation builder for this table.
    /// Use this to scan all items in the table or apply filters.
    /// </summary>
    /// <returns>A ScanRequestBuilder configured for this table.</returns>
    /// <example>
    /// <code>
    /// // Basic scan
    /// var results = await table.Scan&lt;MyEntity&gt;()
    ///     .ExecuteAsync();
    /// 
    /// // Scan with filter
    /// var results = await table.Scan&lt;MyEntity&gt;()
    ///     .WithFilter("status = {0}", "ACTIVE")
    ///     .ExecuteAsync();
    /// </code>
    /// </example>
    public ScanRequestBuilder<TEntity> Scan<TEntity>() where TEntity : class => 
        new ScanRequestBuilder<TEntity>(DynamoDbClient, Logger).ForTable(Name);





    /// <summary>
    /// Express-route method that executes a PutItem operation and stores an entity in DynamoDB.
    /// This method combines Put() and PutAsync() into a single call for simple scenarios.
    /// </summary>
    /// <typeparam name="TEntity">The entity type that implements IDynamoDbEntity.</typeparam>
    /// <param name="entity">The entity instance to put.</param>
    /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    /// <exception cref="DynamoDbMappingException">Thrown when the operation fails.</exception>
    /// <example>
    /// <code>
    /// // Simple put operation
    /// await table.PutAsync(myEntity);
    /// </code>
    /// </example>
    public async Task PutAsync<TEntity>(
        TEntity entity,
        CancellationToken cancellationToken = default)
        where TEntity : class, IDynamoDbEntity
    {
        var builder = Put<TEntity>();
        builder = Requests.Extensions.EnhancedExecuteAsyncExtensions.WithItem(builder, entity);
        await Requests.Extensions.EnhancedExecuteAsyncExtensions.PutAsync(builder, cancellationToken);
    }

    /// <summary>
    /// Express-route method that executes a PutItem operation with a raw attribute dictionary.
    /// This method combines Put() and PutAsync() into a single call for simple scenarios.
    /// </summary>
    /// <typeparam name="TEntity">The entity type.</typeparam>
    /// <param name="item">The raw attribute dictionary to put.</param>
    /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    /// <exception cref="DynamoDbMappingException">Thrown when the operation fails.</exception>
    /// <example>
    /// <code>
    /// // Put with raw dictionary
    /// await table.PutAsync&lt;MyEntity&gt;(new Dictionary&lt;string, AttributeValue&gt;
    /// {
    ///     ["id"] = new AttributeValue { S = "123" },
    ///     ["name"] = new AttributeValue { S = "John" }
    /// });
    /// </code>
    /// </example>
    public async Task PutAsync<TEntity>(
        Dictionary<string, AttributeValue> item,
        CancellationToken cancellationToken = default)
        where TEntity : class
    {
        var builder = Put<TEntity>().WithItem(item);
        await Requests.Extensions.EnhancedExecuteAsyncExtensions.PutAsync(builder, cancellationToken);
    }





}