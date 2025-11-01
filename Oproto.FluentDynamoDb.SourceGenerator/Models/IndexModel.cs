namespace Oproto.FluentDynamoDb.SourceGenerator.Models;

/// <summary>
/// Represents a Global Secondary Index model.
/// </summary>
internal class IndexModel
{
    /// <summary>
    /// Gets or sets the name of the Global Secondary Index.
    /// </summary>
    public string IndexName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the partition key property name for this GSI.
    /// </summary>
    public string PartitionKeyProperty { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the sort key property name for this GSI, if any.
    /// </summary>
    public string? SortKeyProperty { get; set; }

    /// <summary>
    /// Gets or sets the properties projected in this GSI.
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
    /// Gets a value indicating whether this GSI has a sort key.
    /// </summary>
    public bool HasSortKey => !string.IsNullOrEmpty(SortKeyProperty);

    /// <summary>
    /// Gets a value indicating whether this GSI has custom key formatting.
    /// </summary>
    public bool HasCustomKeyFormat => !string.IsNullOrEmpty(PartitionKeyFormat) || !string.IsNullOrEmpty(SortKeyFormat);

    /// <summary>
    /// Gets or sets the GSI-specific discriminator configuration.
    /// Overrides the entity-level discriminator for queries on this GSI.
    /// </summary>
    public DiscriminatorConfig? GsiDiscriminator { get; set; }
}