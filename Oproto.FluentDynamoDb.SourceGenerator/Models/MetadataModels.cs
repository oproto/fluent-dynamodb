namespace Oproto.FluentDynamoDb.SourceGenerator.Models;

/// <summary>
/// Simplified EntityMetadata for source generator use (avoids circular dependencies).
/// </summary>
internal class EntityMetadata
{
    public string TableName { get; set; } = string.Empty;
    public string? EntityDiscriminator { get; set; }
    public PropertyMetadata[] Properties { get; set; } = Array.Empty<PropertyMetadata>();
    public IndexMetadata[] Indexes { get; set; } = Array.Empty<IndexMetadata>();
    public RelationshipMetadata[] Relationships { get; set; } = Array.Empty<RelationshipMetadata>();
    public bool IsMultiItemEntity { get; set; }
}

/// <summary>
/// Simplified PropertyMetadata for source generator use.
/// </summary>
internal class PropertyMetadata
{
    public string PropertyName { get; set; } = string.Empty;
    public string AttributeName { get; set; } = string.Empty;
    public Type PropertyType { get; set; } = typeof(object);
    public bool IsPartitionKey { get; set; }
    public bool IsSortKey { get; set; }
    public bool IsCollection { get; set; }
    public bool IsNullable { get; set; }
    public DynamoDbOperation[] SupportedOperations { get; set; } = Array.Empty<DynamoDbOperation>();
    public string[] AvailableInIndexes { get; set; } = Array.Empty<string>();
}

/// <summary>
/// Simplified IndexMetadata for source generator use.
/// </summary>
internal class IndexMetadata
{
    public string IndexName { get; set; } = string.Empty;
    public string PartitionKeyProperty { get; set; } = string.Empty;
    public string? SortKeyProperty { get; set; }
    public string[] ProjectedProperties { get; set; } = Array.Empty<string>();
}

/// <summary>
/// Simplified RelationshipMetadata for source generator use.
/// </summary>
internal class RelationshipMetadata
{
    public string PropertyName { get; set; } = string.Empty;
    public string SortKeyPattern { get; set; } = string.Empty;
    public Type? EntityType { get; set; }
    public bool IsCollection { get; set; }
}

