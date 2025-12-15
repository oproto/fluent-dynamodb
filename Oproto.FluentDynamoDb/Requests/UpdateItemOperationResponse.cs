using Amazon.DynamoDBv2.Model;

namespace Oproto.FluentDynamoDb.Requests;

/// <summary>
/// Contains response metadata from an UpdateItem operation.
/// Populated after executing UpdateAsync().
/// </summary>
public class UpdateItemOperationResponse
{
    /// <summary>
    /// Gets the consumed capacity from the UpdateItem execution.
    /// Null if ReturnConsumedCapacity was not set.
    /// </summary>
    public ConsumedCapacity? ConsumedCapacity { get; internal set; }

    /// <summary>
    /// Gets the response metadata from the UpdateItem execution.
    /// </summary>
    public Amazon.Runtime.ResponseMetadata? ResponseMetadata { get; internal set; }

    /// <summary>
    /// Gets the item collection metrics from the UpdateItem execution.
    /// Only populated if ReturnItemCollectionMetrics was set.
    /// </summary>
    public ItemCollectionMetrics? ItemCollectionMetrics { get; internal set; }
}
