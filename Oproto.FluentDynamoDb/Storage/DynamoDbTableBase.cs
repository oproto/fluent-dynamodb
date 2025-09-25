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
    
    public GetItemRequestBuilder Get => new GetItemRequestBuilder(DynamoDbClient).ForTable(Name);
    public UpdateItemRequestBuilder Update => new UpdateItemRequestBuilder(DynamoDbClient).ForTable(Name);
    public QueryRequestBuilder Query => new QueryRequestBuilder(DynamoDbClient).ForTable(Name);
    public PutItemRequestBuilder Put => new PutItemRequestBuilder(DynamoDbClient).ForTable(Name);
    public DeleteItemRequestBuilder Delete => new DeleteItemRequestBuilder(DynamoDbClient).ForTable(Name);
    
    /// <summary>
    /// Returns a scannable interface that provides access to scan operations.
    /// This method implements intentional friction to discourage accidental scan usage.
    /// </summary>
    /// <returns>An interface that provides scan functionality while maintaining access to all core operations</returns>
    public IScannableDynamoDbTable AsScannable()
    {
        return new ScannableDynamoDbTable(this);
    }
}