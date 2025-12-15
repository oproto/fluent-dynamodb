using Amazon.DynamoDBv2.Model;

namespace Oproto.FluentDynamoDb.Requests;

/// <summary>
/// Contains response metadata from a Query operation.
/// Populated after executing ToListAsync(), ToCompositeEntityAsync(), or similar methods.
/// </summary>
public class QueryOperationResponse
{
    /// <summary>
    /// Gets the last evaluated key from the query execution.
    /// Use this for pagination to continue from where the previous query left off.
    /// Null if there are no more pages.
    /// </summary>
    public Dictionary<string, AttributeValue>? LastEvaluatedKey { get; internal set; }

    /// <summary>
    /// Gets the number of items evaluated (before filtering) from the query execution.
    /// </summary>
    public int? ScannedCount { get; internal set; }

    /// <summary>
    /// Gets the number of items returned from the query execution.
    /// </summary>
    public int? ResultCount { get; internal set; }

    /// <summary>
    /// Gets the consumed capacity from the query execution.
    /// Null if ReturnConsumedCapacity was not set.
    /// </summary>
    public ConsumedCapacity? ConsumedCapacity { get; internal set; }

    /// <summary>
    /// Gets the response metadata from the query execution.
    /// </summary>
    public Amazon.Runtime.ResponseMetadata? ResponseMetadata { get; internal set; }

    /// <summary>
    /// Indicates whether there are more pages of results available.
    /// </summary>
    public bool HasMorePages => LastEvaluatedKey != null && LastEvaluatedKey.Count > 0;
}
