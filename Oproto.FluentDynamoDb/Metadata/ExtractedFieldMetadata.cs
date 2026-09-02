namespace Oproto.FluentDynamoDb.Metadata;

/// <summary>
/// Metadata about an extracted property that derives its value from a computed field.
/// </summary>
public class ExtractedFieldMetadata
{
    /// <summary>
    /// Gets or sets the property name this extracted property derives from.
    /// </summary>
    public string SourceProperty { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the zero-based positional index in the source property's segments.
    /// </summary>
    public int Index { get; set; }
}
