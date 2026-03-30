namespace Oproto.FluentDynamoDb.SourceGenerator.Models;

/// <summary>
/// Represents metadata for a projection model, inherited from its source entity.
/// This metadata is used during code generation to create the IReadOnlyEntity implementation.
/// </summary>
internal class ProjectionMetadata
{
    /// <summary>
    /// Gets or sets the DynamoDB table name (inherited from source entity).
    /// </summary>
    public string TableName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the DynamoDB attribute name for the partition key (inherited from source entity).
    /// </summary>
    public string PartitionKeyAttributeName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the expected attribute type for the partition key (S, N, B).
    /// </summary>
    public string PartitionKeyAttributeType { get; set; } = "S";

    /// <summary>
    /// Gets or sets the DynamoDB attribute name for the sort key (inherited from source entity).
    /// Null if the table doesn't have a sort key.
    /// </summary>
    public string? SortKeyAttributeName { get; set; }

    /// <summary>
    /// Gets or sets the expected attribute type for the sort key (S, N, B).
    /// Null if the table doesn't have a sort key.
    /// </summary>
    public string? SortKeyAttributeType { get; set; }

    /// <summary>
    /// Gets or sets the discriminator configuration (inherited from source entity).
    /// </summary>
    public DiscriminatorConfig? Discriminator { get; set; }

    /// <summary>
    /// Gets or sets the properties included in the projection.
    /// Only properties that are part of the projection are included.
    /// </summary>
    public ProjectionPropertyMetadata[] Properties { get; set; } = Array.Empty<ProjectionPropertyMetadata>();

    /// <summary>
    /// Gets or sets the indexes relevant to the projection.
    /// </summary>
    public ProjectionIndexMetadata[] Indexes { get; set; } = Array.Empty<ProjectionIndexMetadata>();

    /// <summary>
    /// Gets or sets whether this entity requires write operations within a transaction.
    /// Always false for projections since they are read-only.
    /// </summary>
    public bool RequiresWriteTransaction { get; set; }

    /// <summary>
    /// Gets or sets whether this entity spans multiple DynamoDB items.
    /// Always false for projections.
    /// </summary>
    public bool IsMultiItemEntity { get; set; }

    /// <summary>
    /// Gets or sets the source entity class name for delegation.
    /// </summary>
    public string SourceEntityClassName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the source entity namespace for delegation.
    /// </summary>
    public string SourceEntityNamespace { get; set; } = string.Empty;

    /// <summary>
    /// Gets the fully qualified source entity type name.
    /// </summary>
    public string FullyQualifiedSourceEntityType => 
        string.IsNullOrEmpty(SourceEntityNamespace) 
            ? SourceEntityClassName 
            : $"{SourceEntityNamespace}.{SourceEntityClassName}";
}

/// <summary>
/// Represents metadata for a property in a projection.
/// </summary>
internal class ProjectionPropertyMetadata
{
    /// <summary>
    /// Gets or sets the C# property name.
    /// </summary>
    public string PropertyName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the DynamoDB attribute name.
    /// </summary>
    public string AttributeName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the C# property type.
    /// </summary>
    public string PropertyType { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets whether this property is the partition key.
    /// </summary>
    public bool IsPartitionKey { get; set; }

    /// <summary>
    /// Gets or sets whether this property is the sort key.
    /// </summary>
    public bool IsSortKey { get; set; }

    /// <summary>
    /// Gets or sets whether this property is nullable.
    /// </summary>
    public bool IsNullable { get; set; }

    /// <summary>
    /// Gets or sets whether this property is a collection type.
    /// </summary>
    public bool IsCollection { get; set; }

    /// <summary>
    /// Gets or sets the format string for value serialization.
    /// </summary>
    public string? Format { get; set; }

    /// <summary>
    /// Gets or sets the key format information.
    /// Null for projections since they don't need write-specific key formatting.
    /// </summary>
    public KeyFormatModel? KeyFormat { get; set; }
}

/// <summary>
/// Represents metadata for an index relevant to a projection.
/// </summary>
internal class ProjectionIndexMetadata
{
    /// <summary>
    /// Gets or sets the index name.
    /// </summary>
    public string IndexName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the index type (GSI or LSI).
    /// </summary>
    public IndexType IndexType { get; set; }

    /// <summary>
    /// Gets or sets the partition key property name.
    /// </summary>
    public string? PartitionKeyProperty { get; set; }

    /// <summary>
    /// Gets or sets the sort key property name.
    /// </summary>
    public string? SortKeyProperty { get; set; }
}
