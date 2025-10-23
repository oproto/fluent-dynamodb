namespace Oproto.FluentDynamoDb.SourceGenerator.Models;

/// <summary>
/// Represents a Global Secondary Index definition aggregated across multiple entities.
/// Used during source generation to create index properties on table classes.
/// </summary>
public class GsiDefinition
{
    /// <summary>
    /// Gets or sets the GSI name.
    /// </summary>
    public string IndexName { get; set; } = string.Empty;
    
    /// <summary>
    /// Gets or sets the entity types that use this GSI.
    /// Multiple entities can share the same GSI name.
    /// </summary>
    public List<string> EntityTypes { get; set; } = new();
    
    /// <summary>
    /// Gets or sets the optional projection type constraint.
    /// When set, indicates that this GSI uses [UseProjection] attribute.
    /// </summary>
    public string? ProjectionType { get; set; }
    
    /// <summary>
    /// Gets or sets the projection expression for this GSI (if UseProjection is specified).
    /// Example: "id, amount, status, entity_type"
    /// </summary>
    public string? ProjectionExpression { get; set; }
    
    /// <summary>
    /// Gets or sets the partition key property name.
    /// </summary>
    public string? PartitionKeyProperty { get; set; }
    
    /// <summary>
    /// Gets or sets the sort key property name.
    /// </summary>
    public string? SortKeyProperty { get; set; }
}
