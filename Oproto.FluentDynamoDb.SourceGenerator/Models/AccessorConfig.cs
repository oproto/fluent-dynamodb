namespace Oproto.FluentDynamoDb.SourceGenerator.Models;

/// <summary>
/// Represents configuration for operation method generation on entity accessors.
/// Controls which DynamoDB operations are generated and their visibility.
/// </summary>
internal class AccessorConfig
{
    /// <summary>
    /// Gets or sets the operations to configure.
    /// Can be a single operation or multiple operations combined with flags.
    /// </summary>
    public TableOperation Operations { get; set; } = TableOperation.All;

    /// <summary>
    /// Gets or sets a value indicating whether to generate the specified operations.
    /// Default is true. Set to false to suppress generation of specific operations.
    /// </summary>
    public bool Generate { get; set; } = true;

    /// <summary>
    /// Gets or sets the visibility modifier for the operation methods.
    /// Default is Public.
    /// </summary>
    public AccessModifier Modifier { get; set; } = AccessModifier.Public;
}
