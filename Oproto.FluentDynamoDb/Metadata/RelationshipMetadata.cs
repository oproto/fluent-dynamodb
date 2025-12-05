namespace Oproto.FluentDynamoDb.Metadata;

/// <summary>
/// Metadata about a related entity relationship.
/// </summary>
public class RelationshipMetadata
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
    /// Gets or sets the type of the related entity.
    /// </summary>
    public Type? EntityType { get; set; }

    /// <summary>
    /// Gets or sets whether this relationship represents a collection of entities.
    /// </summary>
    public bool IsCollection { get; set; }

    /// <summary>
    /// Gets or sets whether this relationship is required or optional.
    /// </summary>
    public bool IsRequired { get; set; }
}