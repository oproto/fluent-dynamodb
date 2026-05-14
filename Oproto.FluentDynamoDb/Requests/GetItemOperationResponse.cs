using Amazon.DynamoDBv2.Model;

namespace Oproto.FluentDynamoDb.Requests;

/// <summary>
/// Contains response metadata from a GetItem operation.
/// Populated after executing GetItemAsync().
/// </summary>
public class GetItemOperationResponse
{
    /// <summary>
    /// Gets the consumed capacity from the GetItem execution.
    /// Null if ReturnConsumedCapacity was not set.
    /// </summary>
    public ConsumedCapacity? ConsumedCapacity { get; internal set; }

    /// <summary>
    /// Gets the response metadata from the GetItem execution.
    /// </summary>
    public Amazon.Runtime.ResponseMetadata? ResponseMetadata { get; internal set; }
}
