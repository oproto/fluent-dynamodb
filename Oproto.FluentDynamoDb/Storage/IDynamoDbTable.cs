using Amazon.DynamoDBv2;
using Oproto.FluentDynamoDb.Requests;

namespace Oproto.FluentDynamoDb.Storage;

/// <summary>
/// Interface defining core DynamoDB table operations.
/// This interface provides access to the most commonly used DynamoDB operations:
/// Get, Put, Update, Query, and Delete. Scan operations are intentionally excluded
/// and are only available through the IScannableDynamoDbTable interface.
/// </summary>
public interface IDynamoDbTable
{
    /// <summary>
    /// Gets the DynamoDB client instance used for executing operations.
    /// </summary>
    IAmazonDynamoDB DynamoDbClient { get; }
    
    /// <summary>
    /// Gets the name of the DynamoDB table.
    /// </summary>
    string Name { get; }
    
    /// <summary>
    /// Gets a builder for GetItem operations to retrieve individual items by their primary key.
    /// </summary>
    GetItemRequestBuilder Get { get; }
    
    /// <summary>
    /// Gets a builder for PutItem operations to create new items or completely replace existing items.
    /// </summary>
    PutItemRequestBuilder Put { get; }
    
    /// <summary>
    /// Gets a builder for UpdateItem operations to modify existing items or create them if they don't exist.
    /// </summary>
    UpdateItemRequestBuilder Update { get; }
    
    /// <summary>
    /// Gets a builder for Query operations to efficiently retrieve items using the primary key and optional sort key conditions.
    /// This is the preferred method for retrieving multiple items when you know the primary key.
    /// </summary>
    QueryRequestBuilder Query { get; }
    
    /// <summary>
    /// Gets a builder for DeleteItem operations to remove individual items by their primary key.
    /// Supports conditional deletes and return value options.
    /// </summary>
    DeleteItemRequestBuilder Delete { get; }
}