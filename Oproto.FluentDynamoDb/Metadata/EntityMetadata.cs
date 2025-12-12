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

    /// <summary>
    /// Gets or sets whether this entity requires write operations to be performed within a transaction.
    /// When true, Put, Update, Delete, and BatchWrite operations will throw
    /// <see cref="InvalidOperationException"/> unless performed within a TransactWrite operation.
    /// </summary>
    public bool RequiresWriteTransaction { get; set; }

    /// <summary>
    /// Gets or sets the DynamoDB attribute name for the partition key.
    /// </summary>
    public string PartitionKeyAttributeName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the expected attribute type for the partition key (S, N, B).
    /// </summary>
    public string PartitionKeyAttributeType { get; set; } = "S";

    /// <summary>
    /// Gets or sets the DynamoDB attribute name for the sort key.
    /// Null if the table doesn't have a sort key.
    /// </summary>
    public string? SortKeyAttributeName { get; set; }

    /// <summary>
    /// Gets or sets the expected attribute type for the sort key (S, N, B).
    /// Null if the table doesn't have a sort key.
    /// </summary>
    public string? SortKeyAttributeType { get; set; }

    /// <summary>
    /// Gets or sets the TTL attribute name if TTL is configured.
    /// Null if TTL is not configured for this entity.
    /// </summary>
    public string? TtlAttributeName { get; set; }
}