using System;

namespace Oproto.FluentDynamoDb.Attributes;

/// <summary>
/// Marks a property as a related entity that should be automatically populated
/// based on sort key patterns when querying.
/// </summary>
[AttributeUsage(AttributeTargets.Property)]
public class RelatedEntityAttribute : Attribute
{
    /// <summary>
    /// Gets the sort key pattern used to identify related entities.
    /// Supports wildcards like "audit#*" or exact matches like "summary".
    /// When <c>null</c>, the pattern is inferred from the child entity's
    /// <c>DerivedDiscriminatorPattern</c> at compile time.
    /// </summary>
    public string? SortKeyPattern { get; }

    /// <summary>
    /// Gets or sets the type of the related entity.
    /// If not specified, the property type will be used.
    /// </summary>
    public Type? EntityType { get; set; }

    /// <summary>
    /// Initializes a new instance of the <see cref="RelatedEntityAttribute"/> class
    /// with no explicit pattern. The source generator infers the sort key matching
    /// pattern from the child entity's key metadata.
    /// </summary>
    public RelatedEntityAttribute()
    {
        SortKeyPattern = null;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="RelatedEntityAttribute"/> class
    /// with an explicit sort key pattern.
    /// </summary>
    /// <param name="sortKeyPattern">The sort key pattern to match related entities.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="sortKeyPattern"/> is <c>null</c>.</exception>
    public RelatedEntityAttribute(string sortKeyPattern)
    {
        ArgumentNullException.ThrowIfNull(sortKeyPattern);
        SortKeyPattern = sortKeyPattern;
    }
}
