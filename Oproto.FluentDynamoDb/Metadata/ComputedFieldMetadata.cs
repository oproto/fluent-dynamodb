namespace Oproto.FluentDynamoDb.Metadata;

/// <summary>
/// Metadata about a computed field that concatenates multiple source property values.
/// </summary>
public class ComputedFieldMetadata
{
    /// <summary>
    /// Gets or sets the ordered list of source property names that compose this computed field.
    /// </summary>
    public string[] SourceProperties { get; set; } = Array.Empty<string>();

    /// <summary>
    /// Gets or sets the separator used between source values during concatenation.
    /// </summary>
    public string Separator { get; set; } = "#";

    /// <summary>
    /// Gets or sets the optional prefix prepended to the computed value.
    /// Null if no prefix is configured.
    /// </summary>
    public string? Prefix { get; set; }

    /// <summary>
    /// Gets or sets the separator between the prefix and the computed value.
    /// </summary>
    public string? PrefixSeparator { get; set; }
}
