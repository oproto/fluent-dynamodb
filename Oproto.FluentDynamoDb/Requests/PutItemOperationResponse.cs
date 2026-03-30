using Amazon.DynamoDBv2.Model;

namespace Oproto.FluentDynamoDb.Requests;

/// <summary>
/// Contains response metadata from a PutItem operation.
/// Populated after executing PutAsync().
/// </summary>
public class PutItemOperationResponse
{
    /// <summary>
    /// Gets the consumed capacity from the PutItem execution.
    /// Null if ReturnConsumedCapacity was not set.
    /// </summary>
    public ConsumedCapacity? ConsumedCapacity { get; internal set; }

    /// <summary>
    /// Gets the response metadata from the PutItem execution.
    /// </summary>
    public Amazon.Runtime.ResponseMetadata? ResponseMetadata { get; internal set; }

    /// <summary>
    /// Gets the item collection metrics from the PutItem execution.
    /// Only populated if ReturnItemCollectionMetrics was set.
    /// </summary>
    public ItemCollectionMetrics? ItemCollectionMetrics { get; internal set; }
}
