using Amazon.DynamoDBv2.Model;

namespace Oproto.FluentDynamoDb.Requests.Interfaces;

/// <summary>
/// Marker interface indicating a builder can be used in transaction/batch update operations.
/// Provides internal methods to extract request data for transaction and batch composition.
/// </summary>
public interface ITransactableUpdateBuilder
{
    /// <summary>
    /// Gets the table name for the update operation.
    /// </summary>
    /// <returns>The DynamoDB table name.</returns>
    internal string GetTableName();

    /// <summary>
    /// Gets the key identifying the item to update.
    /// </summary>
    /// <returns>Dictionary of key attribute names to attribute values.</returns>
    internal Dictionary<string, AttributeValue> GetKey();

    /// <summary>
    /// Gets the update expression for the update operation.
    /// </summary>
    /// <returns>The update expression string.</returns>
    internal string GetUpdateExpression();

    /// <summary>
    /// Gets the condition expression for the update operation, if specified.
    /// </summary>
    /// <returns>The condition expression string, or null if not specified.</returns>
    internal string? GetConditionExpression();

    /// <summary>
    /// Gets the expression attribute name mappings for the update operation.
    /// </summary>
    /// <returns>Dictionary of placeholder names to actual attribute names, or null if none.</returns>
    internal Dictionary<string, string>? GetExpressionAttributeNames();

    /// <summary>
    /// Gets the expression attribute value mappings for the update operation.
    /// </summary>
    /// <returns>Dictionary of placeholder names to attribute values, or null if none.</returns>
    internal Dictionary<string, AttributeValue>? GetExpressionAttributeValues();

    /// <summary>
    /// Encrypts parameters that require encryption before executing the transaction.
    /// This method should be called before building the final transaction request.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token for the async operation.</param>
    /// <returns>A task representing the asynchronous encryption operation.</returns>
    internal Task EncryptParametersIfNeededAsync(CancellationToken cancellationToken);
}
