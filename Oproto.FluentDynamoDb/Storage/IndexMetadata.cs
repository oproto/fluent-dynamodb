namespace Oproto.FluentDynamoDb.Storage;

/// <summary>
/// Metadata about a Global Secondary Index in a DynamoDB table.
/// </summary>
public class IndexMetadata
{
    /// <summary>
    /// Gets or sets the name of the Global Secondary Index.
    /// </summary>
    public string IndexName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the property name that serves as the partition key for this index.
    /// </summary>
    public string PartitionKeyProperty { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the property name that serves as the sort key for this index.
    /// Null if the index doesn't have a sort key.
    /// </summary>
    public string? SortKeyProperty { get; set; }

    /// <summary>
    /// Gets or sets the properties that are projected into this index.
    /// Empty array means all attributes are projected.
    /// </summary>
    public string[] ProjectedProperties { get; set; } = Array.Empty<string>();

    /// <summary>
    /// Gets or sets the key format pattern for composite keys in this index.
    /// </summary>
    public string? KeyFormat { get; set; }
}