using Oproto.FluentDynamoDb.SourceGenerator.Models;

namespace Oproto.FluentDynamoDb.SourceGenerator.Analysis;

/// <summary>
/// Strategy for creating projection metadata by inheriting from source entity metadata.
/// Projections inherit key metadata (table name, partition key, sort key) from their source entity
/// but filter attributes to only those included in the projection.
/// </summary>
internal static class MetadataInheritanceStrategy
{
    /// <summary>
    /// Creates projection metadata by inheriting from source entity and filtering to projected attributes.
    /// </summary>
    /// <param name="sourceEntity">The source entity model to inherit metadata from.</param>
    /// <param name="projection">The projection model containing the projected properties.</param>
    /// <returns>A new ProjectionMetadata instance with inherited and filtered metadata.</returns>
    public static ProjectionMetadata CreateProjectionMetadata(EntityModel sourceEntity, ProjectionModel projection)
    {
        if (sourceEntity == null)
            throw new ArgumentNullException(nameof(sourceEntity));
        if (projection == null)
            throw new ArgumentNullException(nameof(projection));

        // Get the set of projected attribute names for filtering
        var projectedAttributeNames = new HashSet<string>(StringComparer.Ordinal);
        foreach (var prop in projection.Properties)
        {
            if (!string.IsNullOrEmpty(prop.AttributeName))
            {
                projectedAttributeNames.Add(prop.AttributeName);
            }
        }

        // Inherit core table information from source entity
        var metadata = new ProjectionMetadata
        {
            // Inherit table name from source entity
            TableName = sourceEntity.TableName,
            
            // Inherit partition key metadata
            PartitionKeyAttributeName = sourceEntity.PartitionKeyProperty?.AttributeName ?? string.Empty,
            PartitionKeyAttributeType = GetAttributeType(sourceEntity.PartitionKeyProperty?.PropertyType),
            
            // Inherit sort key metadata if applicable
            SortKeyAttributeName = sourceEntity.SortKeyProperty?.AttributeName,
            SortKeyAttributeType = sourceEntity.SortKeyProperty != null 
                ? GetAttributeType(sourceEntity.SortKeyProperty.PropertyType) 
                : null,
            
            // Inherit discriminator metadata if applicable
            Discriminator = sourceEntity.Discriminator,
            
            // Filter properties to only projected ones
            Properties = sourceEntity.Properties
                .Where(p => projectedAttributeNames.Contains(p.AttributeName))
                .Select(CreateProjectionPropertyMetadata)
                .ToArray(),
            
            // Inherit indexes that are relevant to the projection
            Indexes = sourceEntity.Indexes
                .Where(idx => IsIndexRelevantToProjection(idx, projectedAttributeNames))
                .Select(CreateProjectionIndexMetadata)
                .ToArray(),
            
            // Projections are read-only - exclude write-specific metadata
            RequiresWriteTransaction = false,
            IsMultiItemEntity = false,
            
            // Store source entity reference for delegation
            SourceEntityClassName = sourceEntity.ClassName,
            SourceEntityNamespace = sourceEntity.Namespace
        };

        return metadata;
    }

    /// <summary>
    /// Determines if an index is relevant to a projection based on projected attributes.
    /// An index is relevant if its key properties are included in the projection.
    /// </summary>
    private static bool IsIndexRelevantToProjection(IndexModel index, HashSet<string> projectedAttributeNames)
    {
        // Check if the index's partition key property is projected
        // Note: We check by property name, not attribute name, since IndexModel uses property names
        return !string.IsNullOrEmpty(index.PartitionKeyProperty);
    }

    /// <summary>
    /// Creates a projection property metadata from a source property model.
    /// </summary>
    private static ProjectionPropertyMetadata CreateProjectionPropertyMetadata(PropertyModel sourceProperty)
    {
        return new ProjectionPropertyMetadata
        {
            PropertyName = sourceProperty.PropertyName,
            AttributeName = sourceProperty.AttributeName,
            PropertyType = sourceProperty.PropertyType,
            IsPartitionKey = sourceProperty.IsPartitionKey,
            IsSortKey = sourceProperty.IsSortKey,
            IsNullable = sourceProperty.IsNullable,
            IsCollection = sourceProperty.IsCollection,
            Format = sourceProperty.Format,
            // Projections don't need key format info for write operations
            KeyFormat = null
        };
    }

    /// <summary>
    /// Creates a projection index metadata from a source index model.
    /// </summary>
    private static ProjectionIndexMetadata CreateProjectionIndexMetadata(IndexModel sourceIndex)
    {
        return new ProjectionIndexMetadata
        {
            IndexName = sourceIndex.IndexName,
            IndexType = sourceIndex.IndexType,
            PartitionKeyProperty = sourceIndex.PartitionKeyProperty,
            SortKeyProperty = sourceIndex.SortKeyProperty
        };
    }

    /// <summary>
    /// Gets the DynamoDB attribute type (S, N, B) for a C# property type.
    /// </summary>
    private static string GetAttributeType(string? propertyType)
    {
        if (string.IsNullOrEmpty(propertyType))
            return "S";

        // Normalize the type name
        var normalizedType = propertyType.TrimEnd('?');
        
        return normalizedType switch
        {
            "int" or "Int32" or "System.Int32" => "N",
            "long" or "Int64" or "System.Int64" => "N",
            "double" or "Double" or "System.Double" => "N",
            "float" or "Single" or "System.Single" => "N",
            "decimal" or "Decimal" or "System.Decimal" => "N",
            "byte[]" or "System.Byte[]" => "B",
            _ => "S"
        };
    }
}
