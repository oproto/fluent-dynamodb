using Amazon.DynamoDBv2;
using Oproto.FluentDynamoDb.Requests;

namespace Oproto.FluentDynamoDb.Storage;

/// <summary>
/// Interface defining core DynamoDB table operations
/// </summary>
public interface IDynamoDbTable
{
    /// <summary>
    /// The DynamoDB client instance
    /// </summary>
    IAmazonDynamoDB DynamoDbClient { get; }
    
    /// <summary>
    /// The table name
    /// </summary>
    string Name { get; }
    
    /// <summary>
    /// Get item request builder
    /// </summary>
    GetItemRequestBuilder Get { get; }
    
    /// <summary>
    /// Put item request builder
    /// </summary>
    PutItemRequestBuilder Put { get; }
    
    /// <summary>
    /// Update item request builder
    /// </summary>
    UpdateItemRequestBuilder Update { get; }
    
    /// <summary>
    /// Query request builder
    /// </summary>
    QueryRequestBuilder Query { get; }
    
    /// <summary>
    /// Delete item request builder
    /// </summary>
    DeleteItemRequestBuilder Delete { get; }
}