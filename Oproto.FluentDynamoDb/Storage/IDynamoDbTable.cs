using Amazon.DynamoDBv2;
using Oproto.FluentDynamoDb.Requests;

namespace Oproto.FluentDynamoDb.Storage;

/// <summary>
/// Interface defining core DynamoDB table operations.
/// This interface provides access to the most commonly used DynamoDB operations:
/// Get, Put, Update, Query, and Delete. Scan operations are intentionally excluded
/// and must be explicitly enabled using the [Scannable] attribute on table classes.
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
    /// Creates a new Query operation builder for this table.
    /// Use this to query items using the primary key or a secondary index.
    /// </summary>
    /// <returns>A QueryRequestBuilder configured for this table.</returns>
    QueryRequestBuilder Query();

    /// <summary>
    /// Creates a new Query operation builder with a key condition expression.
    /// Uses format string syntax for parameters: {0}, {1}, etc.
    /// </summary>
    /// <param name="keyConditionExpression">The key condition expression with format placeholders.</param>
    /// <param name="values">The values to substitute into the expression.</param>
    /// <returns>A QueryRequestBuilder configured with the key condition.</returns>
    QueryRequestBuilder Query(string keyConditionExpression, params object[] values);

    /// <summary>
    /// Creates a new GetItem operation builder for this table.
    /// </summary>
    /// <returns>A GetItemRequestBuilder configured for this table.</returns>
    GetItemRequestBuilder Get();

    /// <summary>
    /// Creates a new UpdateItem operation builder for this table.
    /// </summary>
    /// <returns>An UpdateItemRequestBuilder configured for this table.</returns>
    UpdateItemRequestBuilder Update();

    /// <summary>
    /// Creates a new DeleteItem operation builder for this table.
    /// </summary>
    /// <returns>A DeleteItemRequestBuilder configured for this table.</returns>
    DeleteItemRequestBuilder Delete();

    /// <summary>
    /// Creates a new PutItem operation builder for this table.
    /// </summary>
    /// <returns>A PutItemRequestBuilder configured for this table.</returns>
    PutItemRequestBuilder Put();
}