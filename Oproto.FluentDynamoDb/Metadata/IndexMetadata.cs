namespace Oproto.FluentDynamoDb.Metadata;

/// <summary>
/// Metadata about a secondary index (GSI or LSI) in a DynamoDB table.
/// </summary>
public class IndexMetadata
{
    /// <summary>
    /// Gets or sets the name of the secondary index.
    /// </summary>
    public string IndexName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the type of index (GSI or LSI).
    /// Defaults to GlobalSecondaryIndex for backward compatibility.
    /// </summary>
    public IndexType IndexType { get; set; } = IndexType.GlobalSecondaryIndex;

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

    /// <summary>
    /// Gets or sets the DynamoDB attribute name for the partition key.
    /// </summary>
    public string PartitionKeyAttributeName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the expected attribute type for the partition key (S, N, B).
    /// </summary>
    public string PartitionKeyAttributeType { get; set; } = "S";

    /// <summary>
    /// Gets or sets the DynamoDB attribute name for the sort key.
    /// Null if the index doesn't have a sort key.
    /// </summary>
    public string? SortKeyAttributeName { get; set; }

    /// <summary>
    /// Gets or sets the expected attribute type for the sort key (S, N, B).
    /// Null if the index doesn't have a sort key.
    /// </summary>
    public string? SortKeyAttributeType { get; set; }

    /// <summary>
    /// Gets or sets the projection type for this index.
    /// Defaults to All for backward compatibility.
    /// </summary>
    public ProjectionType ProjectionType { get; set; } = ProjectionType.All;

    /// <summary>
    /// Gets or sets whether a projection model is defined for this index.
    /// </summary>
    public bool HasProjectionModel { get; set; }
}