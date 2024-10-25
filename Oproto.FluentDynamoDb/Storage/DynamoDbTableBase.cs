using Amazon.DynamoDBv2;
using Oproto.FluentDynamoDb.Requests;

namespace Oproto.FluentDynamoDb.Storage;

/// <summary>
/// Base implementation for DynamoDB table abstraction
/// </summary>
public abstract class DynamoDbTableBase
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
}