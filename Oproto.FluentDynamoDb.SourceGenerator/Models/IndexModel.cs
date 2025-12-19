namespace Oproto.FluentDynamoDb.SourceGenerator.Models;

/// <summary>
/// Represents a secondary index model (GSI or LSI).
/// </summary>
internal class IndexModel
{
    /// <summary>
    /// Gets or sets the name of the secondary index (the DynamoDB index name).
    /// </summary>
    public string IndexName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the custom C# property name specified via the Name property on the attribute.
    /// If not specified, this will be null.
    /// </summary>
    public string? CustomName { get; set; }

    /// <summary>
    /// Gets or sets the resolved C# property name for the generated index accessor.
    /// This is either the <see cref="CustomName"/> if specified, or a PascalCase conversion
    /// of the <see cref="IndexName"/>.
    /// </summary>
    public string ResolvedPropertyName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the type of index (GSI or LSI).
    /// Defaults to GlobalSecondaryIndex for backward compatibility.
    /// </summary>
    public IndexType IndexType { get; set; } = IndexType.GlobalSecondaryIndex;

    /// <summary>
    /// Gets or sets the partition key property name for this index.
    /// For LSIs, this is inherited from the base table.
    /// </summary>
    public string PartitionKeyProperty { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the sort key property name for this index, if any.
    /// </summary>
    public string? SortKeyProperty { get; set; }

    /// <summary>
    /// Gets or sets the properties projected in this index.
    /// </summary>
    public string[] ProjectedProperties { get; set; } = Array.Empty<string>();

    /// <summary>
    /// Gets or sets the key format for the partition key, if any.
    /// </summary>
    public string? PartitionKeyFormat { get; set; }

    /// <summary>
    /// Gets or sets the key format for the sort key, if any.
    /// </summary>
    public string? SortKeyFormat { get; set; }

    /// <summary>
    /// Gets a value indicating whether this index has a sort key.
    /// </summary>
    public bool HasSortKey => !string.IsNullOrEmpty(SortKeyProperty);

    /// <summary>
    /// Gets a value indicating whether this index has custom key formatting.
    /// </summary>
    public bool HasCustomKeyFormat => !string.IsNullOrEmpty(PartitionKeyFormat) || !string.IsNullOrEmpty(SortKeyFormat);

    /// <summary>
    /// Gets or sets the GSI-specific discriminator configuration.
    /// Overrides the entity-level discriminator for queries on this GSI.
    /// Only applicable for GSIs.
    /// </summary>
    public DiscriminatorConfig? GsiDiscriminator { get; set; }

    /// <summary>
    /// Gets a value indicating whether this is a Global Secondary Index.
    /// </summary>
    public bool IsGsi => IndexType == IndexType.GlobalSecondaryIndex;

    /// <summary>
    /// Gets a value indicating whether this is a Local Secondary Index.
    /// </summary>
    public bool IsLsi => IndexType == IndexType.LocalSecondaryIndex;
}