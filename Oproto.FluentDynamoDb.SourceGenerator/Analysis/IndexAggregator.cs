using Microsoft.CodeAnalysis;
using Oproto.FluentDynamoDb.SourceGenerator.Diagnostics;
using Oproto.FluentDynamoDb.SourceGenerator.Models;

namespace Oproto.FluentDynamoDb.SourceGenerator.Analysis;

/// <summary>
/// Aggregates index definitions from multiple entities sharing the same table.
/// Detects conflicts and resolves the final property name for generated index accessors.
/// </summary>
internal class IndexAggregator
{
    private readonly List<Diagnostic> _diagnostics = new();

    /// <summary>
    /// Gets the diagnostics collected during aggregation.
    /// </summary>
    public IReadOnlyList<Diagnostic> Diagnostics => _diagnostics;

    /// <summary>
    /// Aggregates index definitions from multiple entities sharing the same table.
    /// </summary>
    /// <param name="entities">The entities sharing the same table.</param>
    /// <returns>A list of aggregated index models with conflict detection.</returns>
    public List<AggregatedIndexModel> AggregateIndexes(List<EntityModel> entities)
    {
        _diagnostics.Clear();

        if (entities == null || entities.Count == 0)
        {
            return new List<AggregatedIndexModel>();
        }

        // Group all indexes by their DynamoDB index name
        var indexesByName = new Dictionary<string, AggregatedIndexModel>(StringComparer.OrdinalIgnoreCase);

        foreach (var entity in entities)
        {
            foreach (var index in entity.Indexes)
            {
                if (!indexesByName.TryGetValue(index.IndexName, out var aggregatedIndex))
                {
                    // First occurrence - capture configuration from this entity
                    aggregatedIndex = new AggregatedIndexModel
                    {
                        DynamoDbIndexName = index.IndexName,
                        Type = index.IndexType,
                        PartitionKeyProperty = index.PartitionKeyProperty,
                        SortKeyProperty = index.SortKeyProperty,
                        GsiDiscriminator = index.GsiDiscriminator,
                        FirstDefiningEntityName = entity.ClassName
                    };
                    indexesByName[index.IndexName] = aggregatedIndex;
                }
                else
                {
                    // Subsequent occurrence - validate configuration matches
                    ValidateIndexConfiguration(aggregatedIndex, index, entity);
                }

                // Add the entity to the referencing entities list
                if (!aggregatedIndex.ReferencingEntities.Contains(entity))
                {
                    aggregatedIndex.ReferencingEntities.Add(entity);
                }

                // Track custom names
                if (!string.IsNullOrEmpty(index.CustomName))
                {
                    if (aggregatedIndex.CustomPropertyName == null)
                    {
                        // First custom name specified
                        aggregatedIndex.CustomPropertyName = index.CustomName;
                    }
                    else if (aggregatedIndex.CustomPropertyName != index.CustomName)
                    {
                        // Conflict: different custom names specified
                        aggregatedIndex.HasConflict = true;
                        if (!aggregatedIndex.ConflictingNames.Contains(aggregatedIndex.CustomPropertyName))
                        {
                            aggregatedIndex.ConflictingNames.Add(aggregatedIndex.CustomPropertyName);
                        }
                        if (!aggregatedIndex.ConflictingNames.Contains(index.CustomName))
                        {
                            aggregatedIndex.ConflictingNames.Add(index.CustomName);
                        }
                    }
                    else
                    {
                        // Same custom name specified by multiple entities (redundant)
                        aggregatedIndex.HasRedundantSpecification = true;
                    }
                }
            }
        }

        // Resolve property names and report diagnostics
        foreach (var aggregatedIndex in indexesByName.Values)
        {
            ResolvePropertyName(aggregatedIndex);
            ReportDiagnostics(aggregatedIndex, entities);
        }

        return indexesByName.Values.ToList();
    }

    /// <summary>
    /// Resolves the final property name for an aggregated index.
    /// </summary>
    private void ResolvePropertyName(AggregatedIndexModel aggregatedIndex)
    {
        if (!string.IsNullOrEmpty(aggregatedIndex.CustomPropertyName) && !aggregatedIndex.HasConflict)
        {
            // Use the custom name if specified and no conflict
            aggregatedIndex.ResolvedPropertyName = aggregatedIndex.CustomPropertyName;
        }
        else
        {
            // Derive from DynamoDB index name using PascalCase conversion
            aggregatedIndex.ResolvedPropertyName = DerivePropertyName(aggregatedIndex.DynamoDbIndexName);
        }
    }

    /// <summary>
    /// Validates that an index definition matches the captured configuration from the first entity.
    /// Sets HasConfigurationConflict and populates ConfigurationConflictDetails if conflicts are found.
    /// </summary>
    /// <param name="aggregatedIndex">The aggregated index with captured configuration.</param>
    /// <param name="index">The index definition from the current entity.</param>
    /// <param name="entity">The entity containing the index definition.</param>
    private void ValidateIndexConfiguration(AggregatedIndexModel aggregatedIndex, IndexModel index, EntityModel entity)
    {
        // Check partition key conflict
        if (!string.Equals(aggregatedIndex.PartitionKeyProperty, index.PartitionKeyProperty, StringComparison.Ordinal))
        {
            aggregatedIndex.HasConfigurationConflict = true;
            aggregatedIndex.ConfigurationConflictDetails.Add(
                $"PK:{aggregatedIndex.PartitionKeyProperty ?? "(none)"}:{aggregatedIndex.FirstDefiningEntityName}:{index.PartitionKeyProperty ?? "(none)"}:{entity.ClassName}");
        }

        // Check sort key conflict (both null is OK, both same value is OK)
        var aggregatedSortKey = aggregatedIndex.SortKeyProperty ?? string.Empty;
        var indexSortKey = index.SortKeyProperty ?? string.Empty;
        if (!string.Equals(aggregatedSortKey, indexSortKey, StringComparison.Ordinal))
        {
            aggregatedIndex.HasConfigurationConflict = true;
            var firstSortKey = string.IsNullOrEmpty(aggregatedIndex.SortKeyProperty) ? "(none)" : aggregatedIndex.SortKeyProperty;
            var secondSortKey = string.IsNullOrEmpty(index.SortKeyProperty) ? "(none)" : index.SortKeyProperty;
            aggregatedIndex.ConfigurationConflictDetails.Add(
                $"SK:{firstSortKey}:{aggregatedIndex.FirstDefiningEntityName}:{secondSortKey}:{entity.ClassName}");
        }

        // Check index type conflict (GSI vs LSI)
        if (aggregatedIndex.Type != index.IndexType)
        {
            aggregatedIndex.HasConfigurationConflict = true;
            aggregatedIndex.ConfigurationConflictDetails.Add(
                $"TYPE:{aggregatedIndex.Type}:{aggregatedIndex.FirstDefiningEntityName}:{index.IndexType}:{entity.ClassName}");
        }
    }

    /// <summary>
    /// Derives a valid C# property name from a DynamoDB index name using PascalCase conversion.
    /// </summary>
    /// <param name="indexName">The DynamoDB index name.</param>
    /// <returns>A valid C# property name.</returns>
    public static string DerivePropertyName(string indexName)
    {
        if (string.IsNullOrEmpty(indexName))
        {
            return "Index";
        }

        // Split by common separators (hyphens, underscores)
        var parts = indexName.Split(new[] { '-', '_' }, StringSplitOptions.RemoveEmptyEntries);
        
        // Convert each part to PascalCase
        var result = string.Concat(parts.Select(part =>
        {
            if (string.IsNullOrEmpty(part))
            {
                return string.Empty;
            }
            
            // Capitalize first letter, keep rest as-is (handles already PascalCase parts)
            return char.ToUpperInvariant(part[0]) + part.Substring(1);
        }));

        // Ensure the result is a valid C# identifier
        if (string.IsNullOrEmpty(result))
        {
            return "Index";
        }

        // If starts with a digit, prefix with underscore
        if (char.IsDigit(result[0]))
        {
            result = "_" + result;
        }

        return result;
    }

    /// <summary>
    /// Reports diagnostics for index conflicts and redundant specifications.
    /// </summary>
    private void ReportDiagnostics(AggregatedIndexModel aggregatedIndex, List<EntityModel> entities)
    {
        var location = GetLocationForIndex(aggregatedIndex, entities);

        // Report name conflicts (FDDB050)
        if (aggregatedIndex.HasConflict && aggregatedIndex.ConflictingNames.Count >= 2)
        {
            _diagnostics.Add(Diagnostic.Create(
                DiagnosticDescriptors.ConflictingIndexNames,
                location,
                aggregatedIndex.DynamoDbIndexName,
                aggregatedIndex.ConflictingNames[0],
                aggregatedIndex.ConflictingNames[1]));
        }
        else if (aggregatedIndex.HasRedundantSpecification && aggregatedIndex.CustomNameCount > 1)
        {
            // Report FDDB052 - Redundant name specification
            _diagnostics.Add(Diagnostic.Create(
                DiagnosticDescriptors.RedundantIndexNameSpecification,
                location,
                aggregatedIndex.DynamoDbIndexName,
                aggregatedIndex.CustomPropertyName ?? string.Empty));
        }

        // Report configuration conflicts (FDDB053, FDDB054, FDDB055)
        if (aggregatedIndex.HasConfigurationConflict)
        {
            ReportConfigurationConflictDiagnostics(aggregatedIndex, location);
        }
    }

    /// <summary>
    /// Reports configuration conflict diagnostics (FDDB053, FDDB054, FDDB055).
    /// </summary>
    /// <param name="aggregatedIndex">The aggregated index with configuration conflicts.</param>
    /// <param name="location">The location for diagnostic reporting.</param>
    private void ReportConfigurationConflictDiagnostics(AggregatedIndexModel aggregatedIndex, Location location)
    {
        foreach (var detail in aggregatedIndex.ConfigurationConflictDetails)
        {
            // Parse the conflict detail format: "TYPE:value1:entity1:value2:entity2"
            var parts = detail.Split(':');
            if (parts.Length < 5)
            {
                continue;
            }

            var conflictType = parts[0];
            var value1 = parts[1];
            var entity1 = parts[2];
            var value2 = parts[3];
            var entity2 = parts[4];

            switch (conflictType)
            {
                case "PK":
                    _diagnostics.Add(Diagnostic.Create(
                        DiagnosticDescriptors.ConflictingIndexPartitionKey,
                        location,
                        aggregatedIndex.DynamoDbIndexName,
                        value1,
                        entity1,
                        value2,
                        entity2));
                    break;

                case "SK":
                    _diagnostics.Add(Diagnostic.Create(
                        DiagnosticDescriptors.ConflictingIndexSortKey,
                        location,
                        aggregatedIndex.DynamoDbIndexName,
                        value1,
                        entity1,
                        value2,
                        entity2));
                    break;

                case "TYPE":
                    _diagnostics.Add(Diagnostic.Create(
                        DiagnosticDescriptors.ConflictingIndexType,
                        location,
                        aggregatedIndex.DynamoDbIndexName,
                        value1,
                        entity1,
                        value2,
                        entity2));
                    break;
            }
        }
    }

    /// <summary>
    /// Gets the location for diagnostic reporting.
    /// </summary>
    private static Location GetLocationForIndex(AggregatedIndexModel aggregatedIndex, List<EntityModel> entities)
    {
        // Try to find the first entity that defines this index with a custom name
        foreach (var entity in aggregatedIndex.ReferencingEntities)
        {
            var index = entity.Indexes.FirstOrDefault(i => 
                i.IndexName == aggregatedIndex.DynamoDbIndexName && 
                !string.IsNullOrEmpty(i.CustomName));
            
            if (index != null && entity.ClassDeclaration != null)
            {
                return entity.ClassDeclaration.Identifier.GetLocation();
            }
        }

        // Fallback to first referencing entity
        var firstEntity = aggregatedIndex.ReferencingEntities.FirstOrDefault();
        return firstEntity?.ClassDeclaration?.Identifier.GetLocation() ?? Location.None;
    }

    /// <summary>
    /// Applies the resolved property names from aggregated indexes back to the entity indexes.
    /// This ensures all entities use the same resolved property name for shared indexes.
    /// </summary>
    /// <param name="entities">The entities to update.</param>
    /// <param name="aggregatedIndexes">The aggregated indexes with resolved names.</param>
    public static void ApplyResolvedNames(List<EntityModel> entities, List<AggregatedIndexModel> aggregatedIndexes)
    {
        var indexLookup = aggregatedIndexes.ToDictionary(
            ai => ai.DynamoDbIndexName, 
            ai => ai.ResolvedPropertyName,
            StringComparer.OrdinalIgnoreCase);

        foreach (var entity in entities)
        {
            foreach (var index in entity.Indexes)
            {
                if (indexLookup.TryGetValue(index.IndexName, out var resolvedName))
                {
                    index.ResolvedPropertyName = resolvedName;
                }
            }
        }
    }

    /// <summary>
    /// Validates index configurations and returns true if there are no conflicts.
    /// </summary>
    /// <param name="aggregatedIndexes">The aggregated indexes to validate.</param>
    /// <returns>True if all indexes are valid (no name or configuration conflicts), false otherwise.</returns>
    public static bool HasNoConflicts(List<AggregatedIndexModel> aggregatedIndexes)
    {
        return !aggregatedIndexes.Any(ai => ai.HasConflict || ai.HasConfigurationConflict);
    }
}
