using Oproto.FluentDynamoDb.Attributes;

namespace Oproto.FluentDynamoDb.Metadata;

/// <summary>
/// Comprehensive metadata about a DynamoDB entity for future LINQ expression support.
/// </summary>
public class EntityMetadata
{
    /// <summary>
    /// Gets or sets the DynamoDB table name.
    /// </summary>
    public string TableName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the entity discriminator for multi-type tables.
    /// </summary>
    public string? EntityDiscriminator { get; set; }

    /// <summary>
    /// Gets or sets metadata for all properties in the entity.
    /// </summary>
    public PropertyMetadata[] Properties { get; set; } = Array.Empty<PropertyMetadata>();

    /// <summary>
    /// Gets or sets metadata for all Global Secondary Indexes.
    /// </summary>
    public IndexMetadata[] Indexes { get; set; } = Array.Empty<IndexMetadata>();

    /// <summary>
    /// Gets or sets metadata for related entity relationships.
    /// </summary>
    public RelationshipMetadata[] Relationships { get; set; } = Array.Empty<RelationshipMetadata>();

    /// <summary>
    /// Gets or sets whether this entity spans multiple DynamoDB items.
    /// </summary>
    public bool IsMultiItemEntity { get; set; }
}