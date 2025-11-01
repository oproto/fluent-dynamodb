namespace Oproto.FluentDynamoDb.SourceGenerator.Models;

/// <summary>
/// Represents configuration for entity accessor property generation.
/// Controls how the entity accessor property is generated on the table class.
/// </summary>
internal class EntityPropertyConfig
{
    /// <summary>
    /// Gets or sets the custom name for the entity accessor property.
    /// If null, the default name (pluralized entity class name) will be used.
    /// </summary>
    public string? Name { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether to generate the entity accessor property.
    /// Default is true.
    /// </summary>
    public bool Generate { get; set; } = true;

    /// <summary>
    /// Gets or sets the visibility modifier for the entity accessor property.
    /// Default is Public.
    /// </summary>
    public AccessModifier Modifier { get; set; } = AccessModifier.Public;
}
