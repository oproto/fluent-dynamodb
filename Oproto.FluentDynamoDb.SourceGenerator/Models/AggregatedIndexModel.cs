namespace Oproto.FluentDynamoDb.SourceGenerator.Models;

/// <summary>
/// Represents an aggregated index model that combines index definitions from multiple entities
/// sharing the same table. This model is used to detect conflicts and resolve the final
/// property name for generated index accessors.
/// </summary>
internal class AggregatedIndexModel
{
    /// <summary>
    /// Gets or sets the DynamoDB index name (e.g., "gsi1", "status-index").
    /// </summary>
    public string DynamoDbIndexName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the custom property name specified via the Name property on the attribute.
    /// This is null if no entity specifies a custom name, or contains the resolved name
    /// when one or more entities specify it.
    /// </summary>
    public string? CustomPropertyName { get; set; }

    /// <summary>
    /// Gets or sets the resolved C# property name for the generated index accessor.
    /// This is either the <see cref="CustomPropertyName"/> if specified, or a PascalCase
    /// conversion of the <see cref="DynamoDbIndexName"/>.
    /// </summary>
    public string ResolvedPropertyName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the list of entities that reference this index.
    /// </summary>
    public List<EntityModel> ReferencingEntities { get; set; } = new();

    /// <summary>
    /// Gets or sets the projection type name for this index, if any.
    /// </summary>
    public string? ProjectionTypeName { get; set; }

    /// <summary>
    /// Gets or sets the projection expression for this index, if any.
    /// </summary>
    public string? ProjectionExpression { get; set; }

    /// <summary>
    /// Gets or sets the type of index (GSI or LSI).
    /// </summary>
    public IndexType Type { get; set; } = IndexType.GlobalSecondaryIndex;

    /// <summary>
    /// Gets or sets a value indicating whether there is a conflict in the Name property
    /// across entities referencing this index.
    /// </summary>
    public bool HasConflict { get; set; }

    /// <summary>
    /// Gets or sets the list of conflicting custom names when multiple entities specify
    /// different Name values for this index.
    /// </summary>
    public List<string> ConflictingNames { get; set; } = new();

    /// <summary>
    /// Gets or sets a value indicating whether multiple entities specify the same Name
    /// (redundant specification).
    /// </summary>
    public bool HasRedundantSpecification { get; set; }

    /// <summary>
    /// Gets the number of entities that specify a custom Name for this index.
    /// </summary>
    public int CustomNameCount => ReferencingEntities.Count(e => 
        e.Indexes.Any(i => i.IndexName == DynamoDbIndexName && !string.IsNullOrEmpty(i.CustomName)));
}
