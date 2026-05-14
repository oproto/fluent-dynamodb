using Amazon.DynamoDBv2.Model;

namespace Oproto.FluentDynamoDb.Requests;

/// <summary>
/// Contains response metadata from a DeleteItem operation.
/// Populated after executing DeleteAsync().
/// </summary>
public class DeleteItemOperationResponse
{
    /// <summary>
    /// Gets the consumed capacity from the DeleteItem execution.
    /// Null if ReturnConsumedCapacity was not set.
    /// </summary>
    public ConsumedCapacity? ConsumedCapacity { get; internal set; }

    /// <summary>
    /// Gets the response metadata from the DeleteItem execution.
    /// </summary>
    public Amazon.Runtime.ResponseMetadata? ResponseMetadata { get; internal set; }

    /// <summary>
    /// Gets the item collection metrics from the DeleteItem execution.
    /// Only populated if ReturnItemCollectionMetrics was set.
    /// </summary>
    public ItemCollectionMetrics? ItemCollectionMetrics { get; internal set; }
}
