using System;

namespace Oproto.FluentDynamoDb.Attributes;

/// <summary>
/// Specifies that a property value should be extracted from a composite key property after mapping from DynamoDB.
/// Used for extracting component values from composite keys.
/// </summary>
[AttributeUsage(AttributeTargets.Property)]
public class ExtractedAttribute : Attribute
{
    /// <summary>
    /// Gets the name of the source property containing the composite key.
    /// </summary>
    public string SourceProperty { get; }

    /// <summary>
    /// Gets the zero-based index of the component to extract from the composite key.
    /// </summary>
    public int Index { get; }

    /// <summary>
    /// Gets or sets the separator used to split the composite key.
    /// Default is "#".
    /// </summary>
    public string Separator { get; set; } = "#";

    /// <summary>
    /// Initializes a new instance of the <see cref="ExtractedAttribute"/> class.
    /// </summary>
    /// <param name="sourceProperty">The name of the source property containing the composite key.</param>
    /// <param name="index">The zero-based index of the component to extract.</param>
    /// <exception cref="ArgumentException">Thrown when source property is null or empty, or index is negative.</exception>
    public ExtractedAttribute(string sourceProperty, int index)
    {
        if (string.IsNullOrEmpty(sourceProperty))
            throw new ArgumentException("Source property name cannot be null or empty.", nameof(sourceProperty));

        if (index < 0)
            throw new ArgumentException("Index must be non-negative.", nameof(index));

        SourceProperty = sourceProperty;
        Index = index;
    }
}
