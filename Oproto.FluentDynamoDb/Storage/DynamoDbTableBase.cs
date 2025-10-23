using Amazon.DynamoDBv2;
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
    internal IDynamoDbLogger Logger { get; private init; }

    /// <summary>
    /// Gets the field encryptor for encrypting and decrypting sensitive properties.
    /// Returns null if encryption is not configured for this table.
    /// </summary>
    protected IFieldEncryptor? FieldEncryptor { get; private init; }

    /// <summary>
    /// Gets the current encryption context identifier, checking both operation-specific and ambient contexts.
    /// This context is used by the field encryptor to determine the appropriate encryption key.
    /// </summary>
    /// <returns>The current encryption context identifier, or null if not set.</returns>
    /// <remarks>
    /// The encryption context can be set using EncryptionContext.Current or per-operation
    /// using WithEncryptionContext() on request builders. The per-operation context takes
    /// precedence over the ambient context.
    /// </remarks>
    protected string? GetEncryptionContext()
    {
        return EncryptionContext.GetEffectiveContext();
    }

    /// <summary>
    /// Creates a new Query operation builder for this table.
    /// Use this to query items using the primary key or a secondary index.
    /// </summary>
    /// <returns>A QueryRequestBuilder configured for this table.</returns>
    /// <example>
    /// <code>
    /// // Manual query configuration
    /// var results = await table.Query()
    ///     .Where("pk = {0}", "USER#123")
    ///     .ExecuteAsync();
    /// 
    /// // Or use the expression overload
    /// var results = await table.Query("pk = {0}", "USER#123").ExecuteAsync();
    /// </code>
    /// </example>
    public QueryRequestBuilder Query() => 
        new QueryRequestBuilder(DynamoDbClient, Logger).ForTable(Name);
    
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
    public QueryRequestBuilder Query(string keyConditionExpression, params object[] values)
    {
        var builder = Query();
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
    /// var item = await table.Get()
    ///     .WithKey("id", "123")
    ///     .WithProjection("name, email")
    ///     .ExecuteAsync();
    /// 
    /// // Or use derived class overload (if available)
    /// var item = await table.Get("123").ExecuteAsync();
    /// </code>
    /// </example>
    public virtual GetItemRequestBuilder Get() => 
        new GetItemRequestBuilder(DynamoDbClient, Logger).ForTable(Name);
    
    /// <summary>
    /// Creates a new UpdateItem operation builder for this table.
    /// Base implementation provides parameterless version.
    /// Derived classes should override to provide key-specific overloads.
    /// </summary>
    /// <returns>An UpdateItemRequestBuilder configured for this table.</returns>
    public virtual UpdateItemRequestBuilder Update() => 
        new UpdateItemRequestBuilder(DynamoDbClient, Logger).ForTable(Name);
    
    /// <summary>
    /// Creates a new DeleteItem operation builder for this table.
    /// Base implementation provides parameterless version.
    /// Derived classes should override to provide key-specific overloads.
    /// </summary>
    /// <returns>A DeleteItemRequestBuilder configured for this table.</returns>
    public virtual DeleteItemRequestBuilder Delete() => 
        new DeleteItemRequestBuilder(DynamoDbClient, Logger).ForTable(Name);
    
    /// <summary>
    /// Creates a new PutItem operation builder for this table.
    /// </summary>
    /// <returns>A PutItemRequestBuilder configured for this table.</returns>
    public PutItemRequestBuilder Put() => 
        new PutItemRequestBuilder(DynamoDbClient, Logger).ForTable(Name);

    /// <summary>
    /// Returns a scannable interface that provides access to scan operations.
    /// 
    /// This method implements intentional friction to discourage accidental scan usage.
    /// Scan operations are expensive and should only be used for legitimate use cases such as:
    /// - Data migration or ETL processes
    /// - Analytics on small tables  
    /// - Operations where you truly need to examine every item
    /// 
    /// Consider using Query operations instead whenever possible, as they are much more efficient.
    /// </summary>
    /// <returns>An interface that provides scan functionality while maintaining access to all core operations.</returns>
    /// <example>
    /// <code>
    /// // Access scan operations through the scannable interface
    /// var scannableTable = table.AsScannable();
    /// var results = await scannableTable.Scan
    ///     .WithFilter("#status = :active")
    ///     .WithAttribute("#status", "status")
    ///     .WithValue(":active", "ACTIVE")
    ///     .ExecuteAsync();
    /// 
    /// // Still access regular operations
    /// var item = await scannableTable.Get
    ///     .WithKey("id", "123")
    ///     .ExecuteAsync();
    /// </code>
    /// </example>
    public IScannableDynamoDbTable AsScannable()
    {
        return new ScannableDynamoDbTable(this);
    }
}