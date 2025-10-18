namespace Oproto.FluentDynamoDb.SourceGenerator.Models;

/// <summary>
/// Represents a related entity relationship model.
/// </summary>
public class RelationshipModel
{
    /// <summary>
    /// Gets or sets the property name that holds the related entity.
    /// </summary>
    public string PropertyName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the sort key pattern used to identify related entities.
    /// </summary>
    public string SortKeyPattern { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the type name of the related entity.
    /// </summary>
    public string? EntityType { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether this relationship represents a collection.
    /// </summary>
    public bool IsCollection { get; set; }

    /// <summary>
    /// Gets or sets the property type as a string.
    /// </summary>
    public string PropertyType { get; set; } = string.Empty;

    /// <summary>
    /// Gets a value indicating whether this relationship uses wildcard matching.
    /// </summary>
    public bool IsWildcardPattern => SortKeyPattern.Contains('*');

    /// <summary>
    /// Gets a value indicating whether this relationship has a specific entity type.
    /// </summary>
    public bool HasSpecificEntityType => !string.IsNullOrEmpty(EntityType);
}