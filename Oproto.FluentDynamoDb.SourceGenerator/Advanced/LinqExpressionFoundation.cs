using Oproto.FluentDynamoDb.SourceGenerator.Models;
using System.Text;

namespace Oproto.FluentDynamoDb.SourceGenerator.Advanced;

/// <summary>
/// Generates foundation code for future LINQ expression support.
/// Provides metadata and infrastructure needed for AOT-compatible LINQ translation.
/// </summary>
internal static class LinqExpressionFoundation
{
    /// <summary>
    /// Generates LINQ expression foundation code for an entity.
    /// </summary>
    public static void GenerateLinqFoundation(StringBuilder sb, EntityModel entity)
    {
        sb.AppendLine();
        sb.AppendLine("        #region LINQ Expression Foundation");
        sb.AppendLine();

        // Generate expression metadata
        GenerateExpressionMetadata(sb, entity);

        // Generate property accessors
        GeneratePropertyAccessors(sb, entity);

        // Generate query capabilities
        GenerateQueryCapabilities(sb, entity);

        // Generate index optimization metadata
        GenerateIndexOptimization(sb, entity);

        sb.AppendLine("        #endregion");
    }

    /// <summary>
    /// Generates expression metadata for LINQ support.
    /// </summary>
    private static void GenerateExpressionMetadata(StringBuilder sb, EntityModel entity)
    {
        sb.AppendLine("        /// <summary>");
        sb.AppendLine("        /// Expression metadata for LINQ query translation.");
        sb.AppendLine("        /// </summary>");
        sb.AppendLine("        public static class ExpressionMetadata");
        sb.AppendLine("        {");

        // Generate property expression mappings
        sb.AppendLine("            /// <summary>");
        sb.AppendLine("            /// Maps property names to DynamoDB attribute names for expression translation.");
        sb.AppendLine("            /// </summary>");
        sb.AppendLine("            public static readonly Dictionary<string, string> PropertyToAttributeMap = new()");
        sb.AppendLine("            {");

        foreach (var property in entity.Properties.Where(p => p.HasAttributeMapping))
        {
            sb.AppendLine($"                {{ nameof({entity.ClassName}.{property.PropertyName}), \"{property.AttributeName}\" }},");
        }

        sb.AppendLine("            };");
        sb.AppendLine();

        // Generate type mappings
        sb.AppendLine("            /// <summary>");
        sb.AppendLine("            /// Maps property types to DynamoDB type information.");
        sb.AppendLine("            /// </summary>");
        sb.AppendLine("            public static readonly Dictionary<string, DynamoDbTypeInfo> PropertyTypeMap = new()");
        sb.AppendLine("            {");

        foreach (var property in entity.Properties.Where(p => p.HasAttributeMapping))
        {
            var dynamoDbType = GetDynamoDbType(property.PropertyType);
            var isNumeric = IsNumericType(property.PropertyType);
            sb.AppendLine($"                {{ nameof({entity.ClassName}.{property.PropertyName}), new DynamoDbTypeInfo {{ Type = \"{dynamoDbType}\", IsNumeric = {isNumeric.ToString().ToLower()}, IsCollection = {property.IsCollection.ToString().ToLower()} }} }},");
        }

        sb.AppendLine("            };");
        sb.AppendLine("        }");
        sb.AppendLine();
    }

    /// <summary>
    /// Generates property accessors for efficient property access in LINQ expressions.
    /// </summary>
    private static void GeneratePropertyAccessors(StringBuilder sb, EntityModel entity)
    {
        sb.AppendLine("        /// <summary>");
        sb.AppendLine("        /// High-performance property accessors for LINQ expression evaluation.");
        sb.AppendLine("        /// </summary>");
        sb.AppendLine("        public static class PropertyAccessors");
        sb.AppendLine("        {");

        foreach (var property in entity.Properties)
        {
            var propertyType = GetBaseType(property.PropertyType);

            // Generate getter
            sb.AppendLine($"            /// <summary>");
            sb.AppendLine($"            /// Gets the {property.PropertyName} property value.");
            sb.AppendLine($"            /// </summary>");
            sb.AppendLine($"            public static {propertyType} Get{property.PropertyName}({entity.ClassName} entity)");
            sb.AppendLine("            {");
            sb.AppendLine($"                return entity.{property.PropertyName};");
            sb.AppendLine("            }");
            sb.AppendLine();

            // Generate setter
            sb.AppendLine($"            /// <summary>");
            sb.AppendLine($"            /// Sets the {property.PropertyName} property value.");
            sb.AppendLine($"            /// </summary>");
            sb.AppendLine($"            public static void Set{property.PropertyName}({entity.ClassName} entity, {propertyType} value)");
            sb.AppendLine("            {");
            sb.AppendLine($"                entity.{property.PropertyName} = value;");
            sb.AppendLine("            }");
            sb.AppendLine();
        }

        sb.AppendLine("        }");
        sb.AppendLine();
    }

    /// <summary>
    /// Generates query capabilities metadata for LINQ optimization.
    /// </summary>
    private static void GenerateQueryCapabilities(StringBuilder sb, EntityModel entity)
    {
        sb.AppendLine("        /// <summary>");
        sb.AppendLine("        /// Query capabilities and optimization metadata for LINQ support.");
        sb.AppendLine("        /// </summary>");
        sb.AppendLine("        public static class QueryCapabilities");
        sb.AppendLine("        {");

        // Generate key properties
        var partitionKeyProperty = entity.Properties.FirstOrDefault(p => p.IsPartitionKey);
        var sortKeyProperty = entity.Properties.FirstOrDefault(p => p.IsSortKey);

        sb.AppendLine("            /// <summary>");
        sb.AppendLine("            /// Primary key information for query optimization.");
        sb.AppendLine("            /// </summary>");
        sb.AppendLine("            public static readonly KeyInfo PrimaryKey = new()");
        sb.AppendLine("            {");
        sb.AppendLine($"                PartitionKeyProperty = \"{partitionKeyProperty?.PropertyName ?? ""}\",");
        sb.AppendLine($"                SortKeyProperty = \"{sortKeyProperty?.PropertyName ?? ""}\",");
        sb.AppendLine($"                PartitionKeyAttribute = \"{partitionKeyProperty?.AttributeName ?? ""}\",");
        sb.AppendLine($"                SortKeyAttribute = \"{sortKeyProperty?.AttributeName ?? ""}\"");
        sb.AppendLine("            };");
        sb.AppendLine();

        // Generate queryable properties
        var queryableProperties = entity.Properties.Where(p => p.Queryable != null).ToArray();
        if (queryableProperties.Length > 0)
        {
            sb.AppendLine("            /// <summary>");
            sb.AppendLine("            /// Properties that support efficient querying operations.");
            sb.AppendLine("            /// </summary>");
            sb.AppendLine("            public static readonly Dictionary<string, QueryablePropertyInfo> QueryableProperties = new()");
            sb.AppendLine("            {");

            foreach (var property in queryableProperties)
            {
                var operations = string.Join(", ", property.Queryable!.SupportedOperations.Select(op => $"DynamoDbOperation.{op}"));
                var indexes = property.Queryable.AvailableInIndexes != null
                    ? string.Join(", ", property.Queryable.AvailableInIndexes.Select(idx => $"\"{idx}\""))
                    : "";

                sb.AppendLine($"                {{ \"{property.PropertyName}\", new QueryablePropertyInfo");
                sb.AppendLine("                {");
                sb.AppendLine($"                    PropertyName = \"{property.PropertyName}\",");
                sb.AppendLine($"                    AttributeName = \"{property.AttributeName}\",");
                sb.AppendLine($"                    SupportedOperations = new[] {{ {operations} }},");
                sb.AppendLine($"                    AvailableInIndexes = new[] {{ {indexes} }}");
                sb.AppendLine("                }},");
            }

            sb.AppendLine("            };");
        }
        else
        {
            sb.AppendLine("            /// <summary>");
            sb.AppendLine("            /// Properties that support efficient querying operations.");
            sb.AppendLine("            /// </summary>");
            sb.AppendLine("            public static readonly Dictionary<string, QueryablePropertyInfo> QueryableProperties = new();");
        }

        sb.AppendLine("        }");
        sb.AppendLine();
    }

    /// <summary>
    /// Generates index optimization metadata for query planning.
    /// </summary>
    private static void GenerateIndexOptimization(StringBuilder sb, EntityModel entity)
    {
        sb.AppendLine("        /// <summary>");
        sb.AppendLine("        /// Index optimization metadata for efficient query planning.");
        sb.AppendLine("        /// </summary>");
        sb.AppendLine("        public static class IndexOptimization");
        sb.AppendLine("        {");

        if (entity.Indexes.Length > 0)
        {
            sb.AppendLine("            /// <summary>");
            sb.AppendLine("            /// Available indexes for query optimization.");
            sb.AppendLine("            /// </summary>");
            sb.AppendLine("            public static readonly Dictionary<string, IndexInfo> AvailableIndexes = new()");
            sb.AppendLine("            {");

            foreach (var index in entity.Indexes)
            {
                sb.AppendLine($"                {{ \"{index.IndexName}\", new IndexInfo");
                sb.AppendLine("                {");
                sb.AppendLine($"                    IndexName = \"{index.IndexName}\",");
                sb.AppendLine($"                    PartitionKeyProperty = \"{index.PartitionKeyProperty}\",");
                sb.AppendLine($"                    SortKeyProperty = \"{index.SortKeyProperty ?? ""}\",");
                sb.AppendLine($"                    ProjectedProperties = new[] {{ {string.Join(", ", index.ProjectedProperties.Select(p => $"\"{p}\""))} }}");
                sb.AppendLine("                }},");
            }

            sb.AppendLine("            };");
        }
        else
        {
            sb.AppendLine("            /// <summary>");
            sb.AppendLine("            /// Available indexes for query optimization.");
            sb.AppendLine("            /// </summary>");
            sb.AppendLine("            public static readonly Dictionary<string, IndexInfo> AvailableIndexes = new();");
        }

        sb.AppendLine();

        // Generate index selection logic
        sb.AppendLine("            /// <summary>");
        sb.AppendLine("            /// Selects the best index for a given query pattern.");
        sb.AppendLine("            /// </summary>");
        sb.AppendLine("            public static string? SelectBestIndex(string[] queryProperties)");
        sb.AppendLine("            {");
        sb.AppendLine("                // Simple index selection logic - can be enhanced for complex scenarios");
        sb.AppendLine("                foreach (var index in AvailableIndexes.Values)");
        sb.AppendLine("                {");
        sb.AppendLine("                    if (queryProperties.Contains(index.PartitionKeyProperty))");
        sb.AppendLine("                    {");
        sb.AppendLine("                        return index.IndexName;");
        sb.AppendLine("                    }");
        sb.AppendLine("                }");
        sb.AppendLine("                return null; // Use main table");
        sb.AppendLine("            }");

        sb.AppendLine("        }");
        sb.AppendLine();

        // Generate supporting types
        GenerateLinqSupportingTypes(sb);
    }

    /// <summary>
    /// Generates supporting types for LINQ expression foundation.
    /// </summary>
    private static void GenerateLinqSupportingTypes(StringBuilder sb)
    {
        sb.AppendLine("        /// <summary>");
        sb.AppendLine("        /// DynamoDB type information for LINQ expression translation.");
        sb.AppendLine("        /// </summary>");
        sb.AppendLine("        public class DynamoDbTypeInfo");
        sb.AppendLine("        {");
        sb.AppendLine("            public string Type { get; set; } = string.Empty;");
        sb.AppendLine("            public bool IsNumeric { get; set; }");
        sb.AppendLine("            public bool IsCollection { get; set; }");
        sb.AppendLine("        }");
        sb.AppendLine();

        sb.AppendLine("        /// <summary>");
        sb.AppendLine("        /// Key information for query optimization.");
        sb.AppendLine("        /// </summary>");
        sb.AppendLine("        public class KeyInfo");
        sb.AppendLine("        {");
        sb.AppendLine("            public string PartitionKeyProperty { get; set; } = string.Empty;");
        sb.AppendLine("            public string SortKeyProperty { get; set; } = string.Empty;");
        sb.AppendLine("            public string PartitionKeyAttribute { get; set; } = string.Empty;");
        sb.AppendLine("            public string SortKeyAttribute { get; set; } = string.Empty;");
        sb.AppendLine("        }");
        sb.AppendLine();

        sb.AppendLine("        /// <summary>");
        sb.AppendLine("        /// Queryable property information for LINQ optimization.");
        sb.AppendLine("        /// </summary>");
        sb.AppendLine("        public class QueryablePropertyInfo");
        sb.AppendLine("        {");
        sb.AppendLine("            public string PropertyName { get; set; } = string.Empty;");
        sb.AppendLine("            public string AttributeName { get; set; } = string.Empty;");
        sb.AppendLine("            public DynamoDbOperation[] SupportedOperations { get; set; } = Array.Empty<DynamoDbOperation>();");
        sb.AppendLine("            public string[] AvailableInIndexes { get; set; } = Array.Empty<string>();");
        sb.AppendLine("        }");
        sb.AppendLine();

        sb.AppendLine("        /// <summary>");
        sb.AppendLine("        /// Index information for query planning.");
        sb.AppendLine("        /// </summary>");
        sb.AppendLine("        public class IndexInfo");
        sb.AppendLine("        {");
        sb.AppendLine("            public string IndexName { get; set; } = string.Empty;");
        sb.AppendLine("            public string PartitionKeyProperty { get; set; } = string.Empty;");
        sb.AppendLine("            public string SortKeyProperty { get; set; } = string.Empty;");
        sb.AppendLine("            public string[] ProjectedProperties { get; set; } = Array.Empty<string>();");
        sb.AppendLine("        }");
    }

    /// <summary>
    /// Gets the DynamoDB type for a property type.
    /// </summary>
    private static string GetDynamoDbType(string propertyType)
    {
        var baseType = GetBaseType(propertyType);

        return baseType switch
        {
            "string" => "S",
            "int" or "long" or "double" or "float" or "decimal" or "byte" or "short" => "N",
            "bool" => "BOOL",
            "byte[]" => "B",
            _ when baseType.StartsWith("List<string>") => "SS",
            _ when baseType.StartsWith("List<") && IsNumericType(baseType) => "NS",
            _ when baseType.StartsWith("List<") => "L",
            _ => "S"
        };
    }

    /// <summary>
    /// Determines if a type is numeric.
    /// </summary>
    private static bool IsNumericType(string typeName)
    {
        var baseType = GetBaseType(typeName);
        var numericTypes = new[]
        {
            "int", "long", "double", "float", "decimal", "byte", "short", "uint", "ulong", "ushort",
            "System.Int32", "System.Int64", "System.Double", "System.Single", "System.Decimal",
            "System.Byte", "System.Int16", "System.UInt32", "System.UInt64", "System.UInt16"
        };
        return numericTypes.Contains(baseType);
    }

    /// <summary>
    /// Gets the base type without nullable annotation.
    /// </summary>
    private static string GetBaseType(string typeName)
    {
        return typeName.TrimEnd('?');
    }
}