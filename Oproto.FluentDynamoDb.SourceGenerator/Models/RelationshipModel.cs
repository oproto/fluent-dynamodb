namespace Oproto.FluentDynamoDb.SourceGenerator.Models;

/// <summary>
/// Represents a related entity relationship model.
/// </summary>
internal class RelationshipModel
{
    /// <summary>
    /// Gets or sets the property name that holds the related entity.
    /// </summary>
    public string PropertyName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the sort key pattern used to identify related entities.
    /// Null when using bare [RelatedEntity] before pattern resolution.
    /// </summary>
    public string? SortKeyPattern { get; set; }

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
    /// Gets or sets a value indicating whether the sort key pattern was inferred
    /// from the child entity's DerivedDiscriminatorPattern (true) or explicitly
    /// provided via [RelatedEntity("pattern")] (false).
    /// </summary>
    public bool IsPatternInferred { get; set; }

    /// <summary>
    /// Gets or sets the fully resolved child entity type name, populated from
    /// property type extraction or from the explicit EntityType argument.
    /// </summary>
    public string? ResolvedEntityType { get; set; }

    /// <summary>
    /// Gets a value indicating whether this relationship uses wildcard matching.
    /// </summary>
    public bool IsWildcardPattern => SortKeyPattern?.Contains('*') ?? false;

    /// <summary>
    /// Gets a value indicating whether this relationship has a specific entity type.
    /// </summary>
    public bool HasSpecificEntityType => !string.IsNullOrWhiteSpace(EntityType);

    /// <summary>
    /// Gets or sets a value indicating whether the child entity type has its own [RelatedEntity] relationships.
    /// When true, recursive composite entity assembly is needed.
    /// </summary>
    public bool ChildEntityHasRelationships { get; set; }

    /// <summary>
    /// Gets or sets the relationships defined on the child entity type.
    /// Used for recursive composite entity assembly.
    /// </summary>
    public RelationshipModel[] ChildEntityRelationships { get; set; } = Array.Empty<RelationshipModel>();
}