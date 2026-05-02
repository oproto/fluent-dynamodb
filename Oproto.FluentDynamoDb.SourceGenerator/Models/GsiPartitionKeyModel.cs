namespace Oproto.FluentDynamoDb.SourceGenerator.Models;

/// <summary>
/// Represents a <c>[GsiPartitionKey]</c> attribute on a property.
/// Captures the partition key role for a Global Secondary Index.
/// </summary>
internal class GsiPartitionKeyModel
{
    /// <summary>
    /// Gets or sets the name of the Global Secondary Index (the DynamoDB index name).
    /// </summary>
    public string IndexName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the custom C# property name specified via the Name property on the attribute.
    /// If not specified, this will be null.
    /// </summary>
    public string? CustomName { get; set; }

    /// <summary>
    /// Gets or sets the DynamoDB projection type for this index.
    /// Defaults to <see cref="ProjectionType.All"/>.
    /// </summary>
    public ProjectionType ProjectionType { get; set; } = ProjectionType.All;

    /// <summary>
    /// Gets or sets the GSI-specific discriminator configuration.
    /// </summary>
    public DiscriminatorConfig? Discriminator { get; set; }
}
