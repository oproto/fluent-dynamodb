using Amazon.DynamoDBv2.Model;

namespace Oproto.FluentDynamoDb.Requests.Interfaces;

/// <summary>
/// Marker interface indicating a builder can be used in transaction/batch delete operations.
/// Provides internal methods to extract request data for transaction and batch composition.
/// </summary>
public interface ITransactableDeleteBuilder
{
    /// <summary>
    /// Gets the table name for the delete operation.
    /// </summary>
    /// <returns>The DynamoDB table name.</returns>
    internal string GetTableName();

    /// <summary>
    /// Gets the key identifying the item to delete.
    /// </summary>
    /// <returns>Dictionary of key attribute names to attribute values.</returns>
    internal Dictionary<string, AttributeValue> GetKey();

    /// <summary>
    /// Gets the condition expression for the delete operation, if specified.
    /// </summary>
    /// <returns>The condition expression string, or null if not specified.</returns>
    internal string? GetConditionExpression();

    /// <summary>
    /// Gets the expression attribute name mappings for the delete operation.
    /// </summary>
    /// <returns>Dictionary of placeholder names to actual attribute names, or null if none.</returns>
    internal Dictionary<string, string>? GetExpressionAttributeNames();

    /// <summary>
    /// Gets the expression attribute value mappings for the delete operation.
    /// </summary>
    /// <returns>Dictionary of placeholder names to attribute values, or null if none.</returns>
    internal Dictionary<string, AttributeValue>? GetExpressionAttributeValues();
}
