namespace Oproto.FluentDynamoDb.Metadata;

/// <summary>
/// Metadata about a computed field that reconstructs a value from multiple source properties using a format string.
/// </summary>
public class ComputedFieldMetadata
{
    /// <summary>
    /// Gets or sets the ordered list of source property names that compose this computed field.
    /// </summary>
    public string[] SourceProperties { get; set; } = Array.Empty<string>();

    /// <summary>
    /// Gets or sets the format string used to reconstruct the computed value via string.Format().
    /// Always non-null at runtime. Contains positional placeholders {0} through {N-1}.
    /// </summary>
    public string Format { get; set; } = "{0}";
}
