namespace Oproto.FluentDynamoDb.SourceGenerator.Models;

/// <summary>
/// Represents key formatting information for partition and sort keys.
/// </summary>
public class KeyFormatModel
{
    /// <summary>
    /// Gets or sets the prefix for the key value.
    /// </summary>
    public string? Prefix { get; set; }

    /// <summary>
    /// Gets or sets the separator used when combining key components.
    /// </summary>
    public string Separator { get; set; } = "#";

    /// <summary>
    /// Gets a value indicating whether this key has formatting rules.
    /// </summary>
    public bool HasFormatting => !string.IsNullOrEmpty(Prefix) || Separator != "#";
}