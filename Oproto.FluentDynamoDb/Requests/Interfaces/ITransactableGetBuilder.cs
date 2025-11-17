using Amazon.DynamoDBv2.Model;

namespace Oproto.FluentDynamoDb.Requests.Interfaces;

/// <summary>
/// Marker interface indicating a builder can be used in transaction/batch get operations.
/// Provides internal methods to extract request data for transaction and batch composition.
/// </summary>
public interface ITransactableGetBuilder
{
    /// <summary>
    /// Gets the table name for the get operation.
    /// </summary>
    /// <returns>The DynamoDB table name.</returns>
    internal string GetTableName();

    /// <summary>
    /// Gets the key identifying the item to retrieve.
    /// </summary>
    /// <returns>Dictionary of key attribute names to attribute values.</returns>
    internal Dictionary<string, AttributeValue> GetKey();

    /// <summary>
    /// Gets the projection expression for the get operation, if specified.
    /// </summary>
    /// <returns>The projection expression string, or null if not specified.</returns>
    internal string? GetProjectionExpression();

    /// <summary>
    /// Gets the expression attribute name mappings for the get operation.
    /// </summary>
    /// <returns>Dictionary of placeholder names to actual attribute names, or null if none.</returns>
    internal Dictionary<string, string>? GetExpressionAttributeNames();

    /// <summary>
    /// Gets the consistent read setting for the get operation.
    /// </summary>
    /// <returns>True if consistent read is enabled, false otherwise.</returns>
    internal bool GetConsistentRead();
}
