namespace Oproto.FluentDynamoDb.SourceGenerator.Models;

/// <summary>
/// Represents extracted key information for a property.
/// </summary>
internal class ExtractedKeyModel
{
    /// <summary>
    /// Gets or sets the name of the source property containing the composite key.
    /// </summary>
    public string SourceProperty { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the zero-based index of the component to extract from the composite key.
    /// </summary>
    public int Index { get; set; }

    /// <summary>
    /// Gets or sets the separator used to split the composite key.
    /// </summary>
    public string Separator { get; set; } = "#";
}