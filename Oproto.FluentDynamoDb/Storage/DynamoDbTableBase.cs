using Amazon.DynamoDBv2;
using Oproto.FluentDynamoDb.Requests;

namespace Oproto.FluentDynamoDb.Storage;

/// <summary>
/// Base implementation for DynamoDB table abstraction
/// </summary>
public abstract class DynamoDbTableBase : IDynamoDbTable
{
    public DynamoDbTableBase(IAmazonDynamoDB client, string tableName)
    {
        DynamoDbClient = client;
        Name = tableName;
    }
    
    public IAmazonDynamoDB DynamoDbClient { get; private init; }
    public string Name { get; private init; }
    
    /// <summary>
    /// Gets a builder for GetItem operations on this table.
    /// </summary>
    public GetItemRequestBuilder Get => new GetItemRequestBuilder(DynamoDbClient).ForTable(Name);
    
    /// <summary>
    /// Gets a builder for UpdateItem operations on this table.
    /// </summary>
    public UpdateItemRequestBuilder Update => new UpdateItemRequestBuilder(DynamoDbClient).ForTable(Name);
    
    /// <summary>
    /// Gets a builder for Query operations on this table.
    /// </summary>
    public QueryRequestBuilder Query => new QueryRequestBuilder(DynamoDbClient).ForTable(Name);
    
    /// <summary>
    /// Gets a builder for PutItem operations on this table.
    /// </summary>
    public PutItemRequestBuilder Put => new PutItemRequestBuilder(DynamoDbClient).ForTable(Name);
    
    /// <summary>
    /// Gets a builder for DeleteItem operations on this table.
    /// Use this to delete individual items by their primary key, with optional condition expressions.
    /// </summary>
    public DeleteItemRequestBuilder Delete => new DeleteItemRequestBuilder(DynamoDbClient).ForTable(Name);
    
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