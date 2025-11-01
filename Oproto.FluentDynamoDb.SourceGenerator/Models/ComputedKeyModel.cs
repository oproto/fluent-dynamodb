namespace Oproto.FluentDynamoDb.SourceGenerator.Models;

/// <summary>
/// Represents computed key information for a property.
/// </summary>
internal class ComputedKeyModel
{
    /// <summary>
    /// Gets or sets the names of the source properties used to compute this property's value.
    /// </summary>
    public string[] SourceProperties { get; set; } = Array.Empty<string>();

    /// <summary>
    /// Gets or sets the format string used to combine source property values.
    /// If null, properties are concatenated with the separator.
    /// </summary>
    public string? Format { get; set; }

    /// <summary>
    /// Gets or sets the separator used between source property values when no format is specified.
    /// </summary>
    public string Separator { get; set; } = "#";

    /// <summary>
    /// Gets a value indicating whether this computed key uses a custom format.
    /// </summary>
    public bool HasCustomFormat => !string.IsNullOrEmpty(Format);
}