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

}