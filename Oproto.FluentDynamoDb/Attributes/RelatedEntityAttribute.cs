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
    /// </summary>
    public string SortKeyPattern { get; }

    /// <summary>
    /// Gets or sets the type of the related entity.
    /// If not specified, the property type will be used.
    /// </summary>
    public Type? EntityType { get; set; }

    /// <summary>
    /// Initializes a new instance of the RelatedEntityAttribute class.
    /// </summary>
    /// <param name="sortKeyPattern">The sort key pattern to match related entities.</param>
    public RelatedEntityAttribute(string sortKeyPattern)
    {
        SortKeyPattern = sortKeyPattern;
    }
}
