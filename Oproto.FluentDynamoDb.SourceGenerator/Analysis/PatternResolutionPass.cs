using Microsoft.CodeAnalysis;
using Oproto.FluentDynamoDb.SourceGenerator.Diagnostics;
using Oproto.FluentDynamoDb.SourceGenerator.Models;

namespace Oproto.FluentDynamoDb.SourceGenerator.Analysis;

/// <summary>
/// Resolves bare [RelatedEntity] patterns by looking up child entity metadata
/// within the same table group. This pass runs after all entities have been
/// individually analyzed and grouped by table name, but before overlap analysis
/// and code generation.
/// </summary>
internal static class PatternResolutionPass
{
    /// <summary>
    /// Resolves bare [RelatedEntity] patterns by looking up child entity metadata
    /// within the same table group. Modifies <see cref="RelationshipModel.SortKeyPattern"/>
    /// in place and returns diagnostics for unresolvable patterns.
    /// </summary>
    /// <param name="entitiesByTable">All entities grouped by table name.</param>
    /// <returns>List of diagnostics to report.</returns>
    public static List<Diagnostic> Resolve(Dictionary<string, List<EntityModel>> entitiesByTable)
    {
        var diagnostics = new List<Diagnostic>();

        foreach (var tableGroup in entitiesByTable)
        {
            var tableName = tableGroup.Key;
            var tableEntities = tableGroup.Value;

            foreach (var entity in tableEntities)
            {
                foreach (var relationship in entity.Relationships)
                {
                    // Skip relationships with explicit patterns (already resolved)
                    if (!string.IsNullOrEmpty(relationship.SortKeyPattern))
                        continue;

                    ResolveRelationship(entity, relationship, tableName, tableEntities, entitiesByTable, diagnostics);
                }
            }
        }

        return diagnostics;
    }

    /// <summary>
    /// Resolves a single bare [RelatedEntity] relationship by finding the child entity
    /// in the same table group and copying its DerivedDiscriminatorPattern.
    /// </summary>
    private static void ResolveRelationship(
        EntityModel parent,
        RelationshipModel relationship,
        string parentTableName,
        List<EntityModel> sameTableEntities,
        Dictionary<string, List<EntityModel>> allEntitiesByTable,
        List<Diagnostic> diagnostics)
    {
        // Step 1: Resolve child entity type name
        var childTypeName = ResolveChildTypeName(relationship);
        if (string.IsNullOrEmpty(childTypeName))
        {
            // Cannot determine child type — emit FDDB130 with empty type name
            diagnostics.Add(Diagnostic.Create(
                DiagnosticDescriptors.BareRelatedEntityUnresolvedType,
                Location.None,
                relationship.PropertyName,
                parent.ClassName,
                "(unknown)"));
            return;
        }

        // Step 2: Search for child entity in same table group
        var childEntity = FindEntityByClassName(sameTableEntities, childTypeName);

        if (childEntity == null)
        {
            // Step 2b: Check if child exists in a different table group
            var (foundInOtherTable, otherTableName) = FindEntityInOtherTables(
                allEntitiesByTable, parentTableName, childTypeName);

            if (foundInOtherTable)
            {
                // Step 4: Found in different table → emit FDDB132
                diagnostics.Add(Diagnostic.Create(
                    DiagnosticDescriptors.BareRelatedEntityTableMismatch,
                    Location.None,
                    relationship.PropertyName,
                    parent.ClassName,
                    parentTableName,
                    childTypeName,
                    otherTableName!));
            }
            else
            {
                // Step 3: Not found anywhere → emit FDDB130
                diagnostics.Add(Diagnostic.Create(
                    DiagnosticDescriptors.BareRelatedEntityUnresolvedType,
                    Location.None,
                    relationship.PropertyName,
                    parent.ClassName,
                    childTypeName));
            }

            return;
        }

        // Step 5: Check child entity's sort key DerivedDiscriminatorPattern
        var sortKeyProperty = childEntity.SortKeyProperty;
        if (sortKeyProperty == null || sortKeyProperty.DerivedDiscriminatorPattern == null)
        {
            // Trivial sort key (NormalizedKeyFormat is "{0}") or no sort key → emit FDDB131
            diagnostics.Add(Diagnostic.Create(
                DiagnosticDescriptors.BareRelatedEntityTrivialSortKey,
                Location.None,
                relationship.PropertyName,
                parent.ClassName,
                childTypeName));
            return;
        }

        // Step 6: Success — resolve the pattern
        relationship.SortKeyPattern = sortKeyProperty.DerivedDiscriminatorPattern;
        relationship.IsPatternInferred = true;

        if (string.IsNullOrWhiteSpace(relationship.EntityType))
        {
            relationship.EntityType = childEntity.ClassName;
        }
    }

    /// <summary>
    /// Resolves the child entity type name from the relationship model,
    /// trying EntityType, then ResolvedEntityType, then parsing PropertyType.
    /// </summary>
    private static string? ResolveChildTypeName(RelationshipModel relationship)
    {
        // Priority 1: Explicit EntityType (from named argument)
        if (!string.IsNullOrWhiteSpace(relationship.EntityType))
            return ExtractSimpleTypeName(relationship.EntityType);

        // Priority 2: ResolvedEntityType (from property type inference in EntityAnalyzer)
        if (!string.IsNullOrWhiteSpace(relationship.ResolvedEntityType))
            return ExtractSimpleTypeName(relationship.ResolvedEntityType);

        // Priority 3: Parse from PropertyType string as fallback
        if (!string.IsNullOrWhiteSpace(relationship.PropertyType))
            return ParseTypeNameFromPropertyType(relationship.PropertyType);

        return null;
    }

    /// <summary>
    /// Extracts the simple (unqualified) type name from a potentially fully-qualified type name.
    /// For example, "MyNamespace.OrderLine" → "OrderLine".
    /// </summary>
    private static string ExtractSimpleTypeName(string? typeName)
    {
        if (string.IsNullOrWhiteSpace(typeName))
            return string.Empty;

        // Strip nullable suffix
        var name = typeName.TrimEnd('?');

        // Handle fully qualified names — take the last segment
        var lastDot = name.LastIndexOf('.');
        if (lastDot >= 0)
            name = name.Substring(lastDot + 1);

        return name;
    }

    /// <summary>
    /// Parses a child entity type name from a PropertyType string.
    /// Handles patterns like:
    ///   "List&lt;OrderLine&gt;" or "System.Collections.Generic.List&lt;OrderLine&gt;" → "OrderLine"
    ///   "OrderLine?" → "OrderLine"
    ///   "OrderLine" → "OrderLine"
    /// </summary>
    private static string? ParseTypeNameFromPropertyType(string propertyType)
    {
        if (string.IsNullOrWhiteSpace(propertyType))
            return null;

        var type = propertyType.Trim();

        // Check for generic type: extract the type argument between < and >
        var angleBracketStart = type.IndexOf('<');
        if (angleBracketStart >= 0)
        {
            var angleBracketEnd = type.LastIndexOf('>');
            if (angleBracketEnd > angleBracketStart)
            {
                var typeArgument = type.Substring(angleBracketStart + 1, angleBracketEnd - angleBracketStart - 1).Trim();
                return ExtractSimpleTypeName(typeArgument.TrimEnd('?'));
            }
        }

        // Strip nullable suffix
        type = type.TrimEnd('?');

        // Extract simple name
        return ExtractSimpleTypeName(type);
    }

    /// <summary>
    /// Finds an EntityModel by ClassName within a list of entities.
    /// Matches on simple class name (not fully qualified).
    /// </summary>
    private static EntityModel? FindEntityByClassName(List<EntityModel> entities, string className)
    {
        return entities.FirstOrDefault(e =>
            string.Equals(e.ClassName, className, StringComparison.Ordinal));
    }

    /// <summary>
    /// Searches all table groups except the specified one for an entity with the given class name.
    /// Returns whether it was found and in which table.
    /// </summary>
    private static (bool Found, string? TableName) FindEntityInOtherTables(
        Dictionary<string, List<EntityModel>> allEntitiesByTable,
        string excludeTableName,
        string className)
    {
        foreach (var tableGroup in allEntitiesByTable)
        {
            if (string.Equals(tableGroup.Key, excludeTableName, StringComparison.Ordinal))
                continue;

            var match = FindEntityByClassName(tableGroup.Value, className);
            if (match != null)
                return (true, tableGroup.Key);
        }

        return (false, null);
    }
}
