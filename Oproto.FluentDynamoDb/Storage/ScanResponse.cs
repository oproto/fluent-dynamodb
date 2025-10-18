using Amazon.DynamoDBv2.Model;
using Amazon.Runtime;

namespace Oproto.FluentDynamoDb.Storage;

/// <summary>
/// Strongly-typed response for Scan operations.
/// </summary>
/// <typeparam name="T">The entity type that implements IDynamoDbEntity.</typeparam>
public class ScanResponse<T> where T : class, IDynamoDbEntity
{
    /// <summary>
    /// Gets or sets the collection of mapped entity items.
    /// For multi-item entities, items with the same partition key are grouped into single entities.
    /// </summary>
    public IList<T> Items { get; set; } = new List<T>();

    /// <summary>
    /// Gets or sets the last evaluated key for pagination.
    /// Use this value as the ExclusiveStartKey for the next scan to continue pagination.
    /// </summary>
    public Dictionary<string, AttributeValue>? LastEvaluatedKey { get; set; }

    /// <summary>
    /// Gets or sets the consumed capacity information for the operation.
    /// </summary>
    public ConsumedCapacity? ConsumedCapacity { get; set; }

    /// <summary>
    /// Gets or sets the number of entities returned (after grouping for multi-item entities).
    /// </summary>
    public int Count { get; set; }

    /// <summary>
    /// Gets or sets the number of items examined during the scan operation.
    /// This may be higher than Count due to filtering and multi-item entity grouping.
    /// </summary>
    public int ScannedCount { get; set; }

    /// <summary>
    /// Gets or sets the response metadata from DynamoDB.
    /// </summary>
    public ResponseMetadata? ResponseMetadata { get; set; }
}