using System;

namespace Oproto.FluentDynamoDb.Attributes;

/// <summary>
/// Specifies that a property value should be computed from other properties before mapping to DynamoDB.
/// Used for creating composite keys or derived values from source properties.
/// </summary>
[AttributeUsage(AttributeTargets.Property)]
public class ComputedAttribute : Attribute
{
    /// <summary>
    /// Gets the names of the source properties used to compute this property's value.
    /// </summary>
    public string[] SourceProperties { get; }

    /// <summary>
    /// Gets or sets the format string used to combine source property values.
    /// Uses standard .NET string formatting (e.g., "{0}#{1}" for two properties).
    /// If not specified, properties are concatenated with the separator.
    /// </summary>
    public string? Format { get; set; }

    /// <summary>
    /// Gets or sets the separator used between source property values when no format is specified.
    /// Default is "#".
    /// </summary>
    public string Separator { get; set; } = "#";

    /// <summary>
    /// Initializes a new instance of the <see cref="ComputedAttribute"/> class.
    /// </summary>
    /// <param name="sourceProperties">The names of the source properties used to compute this property's value.</param>
    /// <exception cref="ArgumentException">Thrown when no source properties are provided.</exception>
    public ComputedAttribute(params string[] sourceProperties)
    {
        if (sourceProperties == null || sourceProperties.Length == 0)
            throw new ArgumentException("At least one source property must be specified.", nameof(sourceProperties));

        SourceProperties = sourceProperties;
    }
}
