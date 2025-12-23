using Oproto.FluentDynamoDb.SourceGenerator.Models;
using System.Text;

namespace Oproto.FluentDynamoDb.SourceGenerator.Generators;

/// <summary>
/// Generates Keys Only projection records for indexes with ProjectionType = KeysOnly.
/// These records contain only the key attributes (GSI/LSI keys + base table keys).
/// </summary>
internal static class KeysOnlyProjectionGenerator
{
    /// <summary>
    /// Generates a Keys Only projection record for an index.
    /// The record is generated as a nested type within the table class.
    /// </summary>
    /// <param name="sb">The StringBuilder to append to.</param>
    /// <param name="entity">The entity model containing the index.</param>
    /// <param name="index">The index model requiring Keys Only projection.</param>
    /// <param name="tableClassName">The table class name for the parent table.</param>
    public static void GenerateKeysOnlyProjectionRecord(
        StringBuilder sb,
        EntityModel entity,
        IndexModel index,
        string tableClassName)
    {
        var projectionName = $"{index.ResolvedPropertyName}KeysProjection";
        var indexType = index.IsGsi ? "GSI" : "LSI";
        
        // Collect all key properties for this projection
        var keyProperties = CollectKeyProperties(entity, index);
        
        // Generate projection expression
        var projectionExpression = string.Join(", ", keyProperties.Select(k => k.AttributeName));
        
        sb.AppendLine();
        sb.AppendLine($"    /// <summary>");
        sb.AppendLine($"    /// Keys-only projection for the {index.IndexName} index.");
        sb.AppendLine($"    /// Contains the {indexType} keys ({GetIndexKeyDescription(index)}) and the base table keys ({GetTableKeyDescription(entity)}).");
        sb.AppendLine($"    /// </summary>");
        sb.AppendLine($"    /// <remarks>");
        sb.AppendLine($"    /// This is a read-only projection that implements <see cref=\"IReadOnlyEntity{{TSelf}}\"/>.");
        sb.AppendLine($"    /// It can be used with QueryRequestBuilder to retrieve only key attributes from the index.");
        sb.AppendLine($"    /// The GetPartitionKey() and GetSortKey() methods return base table keys for entity lookup.");
        sb.AppendLine($"    /// </remarks>");
        sb.AppendLine($"    public sealed record {projectionName} : IReadOnlyEntity");
        sb.AppendLine("    {");
        
        // Generate properties for each key
        foreach (var keyProp in keyProperties)
        {
            GenerateKeyProperty(sb, keyProp);
        }
        
        // Generate ProjectionExpression static property
        sb.AppendLine();
        sb.AppendLine($"        /// <summary>");
        sb.AppendLine($"        /// Gets the projection expression for this projection type.");
        sb.AppendLine($"        /// </summary>");
        sb.AppendLine($"        public static string ProjectionExpression => \"{projectionExpression}\";");
        
        // Generate FromDynamoDb method (non-generic)
        GenerateFromDynamoDbMethod(sb, projectionName, keyProperties);
        
        // Generate FromDynamoDb<TSelf> method (required by IReadOnlyEntity)
        GenerateGenericFromDynamoDbMethod(sb, projectionName, keyProperties);
        
        // Generate GetPartitionKey static method (required by IReadOnlyEntity)
        GenerateGetPartitionKeyMethod(sb, entity);
        
        // Generate GetEntityMetadata static method (required by IEntityMetadataProvider)
        GenerateGetEntityMetadataMethod(sb, entity, index, projectionName, keyProperties);
        
        // Generate instance methods for key access
        GenerateInstanceKeyMethods(sb, entity);
        
        sb.AppendLine("    }");
    }

    /// <summary>
    /// Collects all key properties needed for the Keys Only projection.
    /// For GSI: GSI partition key, GSI sort key (if any), base table partition key, base table sort key (if any)
    /// For LSI: Base table partition key, LSI sort key, base table sort key (if different from LSI sort key)
    /// </summary>
    private static List<KeyPropertyInfo> CollectKeyProperties(EntityModel entity, IndexModel index)
    {
        var keyProperties = new List<KeyPropertyInfo>();
        var addedAttributes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        
        // Get base table keys
        var tablePk = entity.PartitionKeyProperty;
        var tableSk = entity.SortKeyProperty;
        
        if (index.IsGsi)
        {
            // For GSI: Add GSI keys first, then base table keys
            
            // GSI Partition Key
            var gsiPkProp = entity.Properties.FirstOrDefault(p => p.PropertyName == index.PartitionKeyProperty);
            if (gsiPkProp != null && !string.IsNullOrEmpty(gsiPkProp.AttributeName))
            {
                keyProperties.Add(new KeyPropertyInfo
                {
                    PropertyName = gsiPkProp.PropertyName,
                    AttributeName = gsiPkProp.AttributeName,
                    PropertyType = gsiPkProp.PropertyType,
                    IsGsiKey = true,
                    IsPartitionKey = true,
                    Description = "GSI partition key"
                });
                addedAttributes.Add(gsiPkProp.AttributeName);
            }
            
            // GSI Sort Key (if any)
            if (index.HasSortKey && !string.IsNullOrEmpty(index.SortKeyProperty))
            {
                var gsiSkProp = entity.Properties.FirstOrDefault(p => p.PropertyName == index.SortKeyProperty);
                if (gsiSkProp != null && !string.IsNullOrEmpty(gsiSkProp.AttributeName) && 
                    !addedAttributes.Contains(gsiSkProp.AttributeName))
                {
                    keyProperties.Add(new KeyPropertyInfo
                    {
                        PropertyName = gsiSkProp.PropertyName,
                        AttributeName = gsiSkProp.AttributeName,
                        PropertyType = gsiSkProp.PropertyType,
                        IsGsiKey = true,
                        IsSortKey = true,
                        Description = "GSI sort key"
                    });
                    addedAttributes.Add(gsiSkProp.AttributeName);
                }
            }
            
            // Base table Partition Key
            if (tablePk != null && !string.IsNullOrEmpty(tablePk.AttributeName) && 
                !addedAttributes.Contains(tablePk.AttributeName))
            {
                keyProperties.Add(new KeyPropertyInfo
                {
                    PropertyName = tablePk.PropertyName,
                    AttributeName = tablePk.AttributeName,
                    PropertyType = tablePk.PropertyType,
                    IsTableKey = true,
                    IsPartitionKey = true,
                    Description = "Base table partition key"
                });
                addedAttributes.Add(tablePk.AttributeName);
            }
            
            // Base table Sort Key (if any)
            if (tableSk != null && !string.IsNullOrEmpty(tableSk.AttributeName) && 
                !addedAttributes.Contains(tableSk.AttributeName))
            {
                keyProperties.Add(new KeyPropertyInfo
                {
                    PropertyName = tableSk.PropertyName,
                    AttributeName = tableSk.AttributeName,
                    PropertyType = tableSk.PropertyType,
                    IsTableKey = true,
                    IsSortKey = true,
                    Description = "Base table sort key"
                });
                addedAttributes.Add(tableSk.AttributeName);
            }
        }
        else // LSI
        {
            // For LSI: Base table partition key, LSI sort key, base table sort key (if different)
            
            // Base table Partition Key (LSIs share partition key with base table)
            if (tablePk != null && !string.IsNullOrEmpty(tablePk.AttributeName))
            {
                keyProperties.Add(new KeyPropertyInfo
                {
                    PropertyName = tablePk.PropertyName,
                    AttributeName = tablePk.AttributeName,
                    PropertyType = tablePk.PropertyType,
                    IsTableKey = true,
                    IsPartitionKey = true,
                    Description = "Base table partition key"
                });
                addedAttributes.Add(tablePk.AttributeName);
            }
            
            // LSI Sort Key
            if (!string.IsNullOrEmpty(index.SortKeyProperty))
            {
                var lsiSkProp = entity.Properties.FirstOrDefault(p => p.PropertyName == index.SortKeyProperty);
                if (lsiSkProp != null && !string.IsNullOrEmpty(lsiSkProp.AttributeName) && 
                    !addedAttributes.Contains(lsiSkProp.AttributeName))
                {
                    keyProperties.Add(new KeyPropertyInfo
                    {
                        PropertyName = lsiSkProp.PropertyName,
                        AttributeName = lsiSkProp.AttributeName,
                        PropertyType = lsiSkProp.PropertyType,
                        IsLsiKey = true,
                        IsSortKey = true,
                        Description = "LSI sort key"
                    });
                    addedAttributes.Add(lsiSkProp.AttributeName);
                }
            }
            
            // Base table Sort Key (if different from LSI sort key)
            if (tableSk != null && !string.IsNullOrEmpty(tableSk.AttributeName) && 
                !addedAttributes.Contains(tableSk.AttributeName))
            {
                keyProperties.Add(new KeyPropertyInfo
                {
                    PropertyName = tableSk.PropertyName,
                    AttributeName = tableSk.AttributeName,
                    PropertyType = tableSk.PropertyType,
                    IsTableKey = true,
                    IsSortKey = true,
                    Description = "Base table sort key"
                });
                addedAttributes.Add(tableSk.AttributeName);
            }
        }
        
        return keyProperties;
    }

    private static void GenerateKeyProperty(StringBuilder sb, KeyPropertyInfo keyProp)
    {
        sb.AppendLine();
        sb.AppendLine($"        /// <summary>");
        sb.AppendLine($"        /// Gets or sets the {keyProp.Description} value.");
        sb.AppendLine($"        /// </summary>");
        sb.AppendLine($"        [DynamoDbAttribute(\"{keyProp.AttributeName}\")]");
        sb.AppendLine($"        public {keyProp.PropertyType} {keyProp.PropertyName} {{ get; init; }} = {GetDefaultValue(keyProp.PropertyType)};");
    }

    private static void GenerateFromDynamoDbMethod(StringBuilder sb, string projectionName, List<KeyPropertyInfo> keyProperties)
    {
        sb.AppendLine();
        sb.AppendLine($"        /// <summary>");
        sb.AppendLine($"        /// Creates an instance from DynamoDB attributes.");
        sb.AppendLine($"        /// </summary>");
        sb.AppendLine($"        /// <param name=\"attributes\">The DynamoDB item attributes.</param>");
        sb.AppendLine($"        /// <param name=\"options\">Optional configuration options.</param>");
        sb.AppendLine($"        /// <returns>A new {projectionName} instance.</returns>");
        sb.AppendLine($"        public static {projectionName} FromDynamoDb(");
        sb.AppendLine($"            Dictionary<string, AttributeValue> attributes,");
        sb.AppendLine($"            FluentDynamoDbOptions? options = null)");
        sb.AppendLine("        {");
        sb.AppendLine($"            return new {projectionName}");
        sb.AppendLine("            {");
        
        foreach (var keyProp in keyProperties)
        {
            var conversion = GetAttributeValueConversion(keyProp);
            sb.AppendLine($"                {keyProp.PropertyName} = attributes.TryGetValue(\"{keyProp.AttributeName}\", out var {keyProp.PropertyName.ToLower()}Attr) ? {conversion} : {GetDefaultValue(keyProp.PropertyType)},");
        }
        
        sb.AppendLine("            };");
        sb.AppendLine("        }");
    }

    private static void GenerateGenericFromDynamoDbMethod(StringBuilder sb, string projectionName, List<KeyPropertyInfo> keyProperties)
    {
        sb.AppendLine();
        sb.AppendLine($"        /// <summary>");
        sb.AppendLine($"        /// Creates an entity instance from a single DynamoDB item.");
        sb.AppendLine($"        /// Required by IReadOnlyEntity interface.");
        sb.AppendLine($"        /// </summary>");
        sb.AppendLine($"        /// <typeparam name=\"TSelf\">The entity type implementing this interface.</typeparam>");
        sb.AppendLine($"        /// <param name=\"item\">The DynamoDB item as an AttributeValue dictionary.</param>");
        sb.AppendLine($"        /// <param name=\"options\">Optional configuration options.</param>");
        sb.AppendLine($"        /// <returns>The mapped projection instance.</returns>");
        sb.AppendLine($"        public static TSelf FromDynamoDb<TSelf>(Dictionary<string, AttributeValue> item, FluentDynamoDbOptions? options = null) where TSelf : IReadOnlyEntity");
        sb.AppendLine("        {");
        sb.AppendLine($"            if (typeof(TSelf) != typeof({projectionName}))");
        sb.AppendLine("            {");
        sb.AppendLine($"                throw new ArgumentException($\"Type parameter must be {projectionName}, but was {{typeof(TSelf).Name}}\", nameof(TSelf));");
        sb.AppendLine("            }");
        sb.AppendLine();
        sb.AppendLine($"            return (TSelf)(object)FromDynamoDb(item, options);");
        sb.AppendLine("        }");
    }

    private static void GenerateGetPartitionKeyMethod(StringBuilder sb, EntityModel entity)
    {
        var tablePk = entity.PartitionKeyProperty;
        var pkAttrName = tablePk?.AttributeName ?? "pk";
        
        sb.AppendLine();
        sb.AppendLine($"        /// <summary>");
        sb.AppendLine($"        /// Extracts the partition key value from a DynamoDB item.");
        sb.AppendLine($"        /// Returns the base table partition key for entity lookup.");
        sb.AppendLine($"        /// </summary>");
        sb.AppendLine($"        /// <param name=\"item\">The DynamoDB item.</param>");
        sb.AppendLine($"        /// <returns>The partition key value.</returns>");
        sb.AppendLine($"        public static string GetPartitionKey(Dictionary<string, AttributeValue> item)");
        sb.AppendLine("        {");
        sb.AppendLine($"            if (item.TryGetValue(\"{pkAttrName}\", out var pkAttr))");
        sb.AppendLine("            {");
        sb.AppendLine("                return pkAttr.S ?? pkAttr.N ?? string.Empty;");
        sb.AppendLine("            }");
        sb.AppendLine("            return string.Empty;");
        sb.AppendLine("        }");
    }

    private static void GenerateGetEntityMetadataMethod(
        StringBuilder sb, 
        EntityModel entity, 
        IndexModel index, 
        string projectionName,
        List<KeyPropertyInfo> keyProperties)
    {
        var tablePk = entity.PartitionKeyProperty;
        var tableSk = entity.SortKeyProperty;
        
        sb.AppendLine();
        sb.AppendLine($"        /// <summary>");
        sb.AppendLine($"        /// Gets the entity metadata for this projection.");
        sb.AppendLine($"        /// Metadata is derived from the source entity: {entity.ClassName}.");
        sb.AppendLine($"        /// </summary>");
        sb.AppendLine($"        /// <returns>The entity metadata.</returns>");
        sb.AppendLine($"        public static EntityMetadata GetEntityMetadata()");
        sb.AppendLine("        {");
        sb.AppendLine("            return new EntityMetadata");
        sb.AppendLine("            {");
        sb.AppendLine($"                TableName = \"{EscapeString(entity.TableName)}\",");
        sb.AppendLine($"                PartitionKeyAttributeName = \"{EscapeString(tablePk?.AttributeName ?? "pk")}\",");
        sb.AppendLine($"                PartitionKeyAttributeType = \"{GetAttributeType(tablePk?.PropertyType ?? "string")}\",");
        
        if (tableSk != null && !string.IsNullOrEmpty(tableSk.AttributeName))
        {
            sb.AppendLine($"                SortKeyAttributeName = \"{EscapeString(tableSk.AttributeName)}\",");
            sb.AppendLine($"                SortKeyAttributeType = \"{GetAttributeType(tableSk.PropertyType)}\",");
        }
        else
        {
            sb.AppendLine("                SortKeyAttributeName = null,");
            sb.AppendLine("                SortKeyAttributeType = null,");
        }
        
        sb.AppendLine("                RequiresWriteTransaction = false,");
        sb.AppendLine("                IsMultiItemEntity = false,");
        
        // Generate property metadata for key properties
        sb.AppendLine("                Properties = new PropertyMetadata[]");
        sb.AppendLine("                {");
        foreach (var keyProp in keyProperties)
        {
            sb.AppendLine("                    new PropertyMetadata");
            sb.AppendLine("                    {");
            sb.AppendLine($"                        PropertyName = \"{keyProp.PropertyName}\",");
            sb.AppendLine($"                        AttributeName = \"{keyProp.AttributeName}\",");
            sb.AppendLine($"                        PropertyType = typeof({GetTypeForMetadata(keyProp.PropertyType)}),");
            sb.AppendLine($"                        IsPartitionKey = {(keyProp.IsPartitionKey && keyProp.IsTableKey).ToString().ToLower()},");
            sb.AppendLine($"                        IsSortKey = {(keyProp.IsSortKey && keyProp.IsTableKey).ToString().ToLower()},");
            sb.AppendLine("                        IsNullable = false");
            sb.AppendLine("                    },");
        }
        sb.AppendLine("                },");
        
        sb.AppendLine("                Indexes = Array.Empty<IndexMetadata>(),");
        sb.AppendLine("                Relationships = Array.Empty<RelationshipMetadata>()");
        sb.AppendLine("            };");
        sb.AppendLine("        }");
    }

    private static void GenerateInstanceKeyMethods(StringBuilder sb, EntityModel entity)
    {
        var tablePk = entity.PartitionKeyProperty;
        var tableSk = entity.SortKeyProperty;
        
        // GetPartitionKey instance method
        sb.AppendLine();
        sb.AppendLine($"        /// <summary>");
        sb.AppendLine($"        /// Gets the base table partition key value for entity lookup.");
        sb.AppendLine($"        /// </summary>");
        sb.AppendLine($"        /// <returns>The partition key value.</returns>");
        sb.AppendLine($"        public string GetPartitionKey() => {tablePk?.PropertyName ?? "Pk"};");
        
        // GetSortKey instance method
        sb.AppendLine();
        sb.AppendLine($"        /// <summary>");
        sb.AppendLine($"        /// Gets the base table sort key value for entity lookup.");
        sb.AppendLine($"        /// </summary>");
        sb.AppendLine($"        /// <returns>The sort key value, or null if no sort key.</returns>");
        if (tableSk != null)
        {
            sb.AppendLine($"        public string? GetSortKey() => {tableSk.PropertyName};");
        }
        else
        {
            sb.AppendLine($"        public string? GetSortKey() => null;");
        }
    }

    private static string GetIndexKeyDescription(IndexModel index)
    {
        if (index.HasSortKey)
        {
            return $"{index.PartitionKeyAttribute}, {index.SortKeyAttribute}";
        }
        return index.PartitionKeyAttribute;
    }

    private static string GetTableKeyDescription(EntityModel entity)
    {
        var tablePk = entity.PartitionKeyProperty;
        var tableSk = entity.SortKeyProperty;
        
        if (tableSk != null)
        {
            return $"{tablePk?.AttributeName ?? "pk"}, {tableSk.AttributeName}";
        }
        return tablePk?.AttributeName ?? "pk";
    }

    private static string GetDefaultValue(string propertyType)
    {
        var baseType = GetBaseType(propertyType);
        return baseType switch
        {
            "string" or "String" or "System.String" => "string.Empty",
            "int" or "Int32" or "System.Int32" => "0",
            "long" or "Int64" or "System.Int64" => "0L",
            "double" or "Double" or "System.Double" => "0.0",
            "decimal" or "Decimal" or "System.Decimal" => "0m",
            "bool" or "Boolean" or "System.Boolean" => "false",
            _ => "default!"
        };
    }

    private static string GetAttributeValueConversion(KeyPropertyInfo keyProp)
    {
        var baseType = GetBaseType(keyProp.PropertyType);
        var varName = $"{keyProp.PropertyName.ToLower()}Attr";
        
        return baseType switch
        {
            "string" or "String" or "System.String" => $"{varName}.S ?? string.Empty",
            "int" or "Int32" or "System.Int32" => $"int.Parse({varName}.N)",
            "long" or "Int64" or "System.Int64" => $"long.Parse({varName}.N)",
            "double" or "Double" or "System.Double" => $"double.Parse({varName}.N)",
            "decimal" or "Decimal" or "System.Decimal" => $"decimal.Parse({varName}.N)",
            "bool" or "Boolean" or "System.Boolean" => $"{varName}.BOOL",
            _ => $"{varName}.S ?? string.Empty"
        };
    }

    private static string GetBaseType(string propertyType)
    {
        if (string.IsNullOrEmpty(propertyType))
            return "string";
        
        return propertyType.TrimEnd('?');
    }

    private static string GetAttributeType(string propertyType)
    {
        var baseType = GetBaseType(propertyType);
        return baseType switch
        {
            "string" or "String" or "System.String" => "S",
            "int" or "Int32" or "System.Int32" => "N",
            "long" or "Int64" or "System.Int64" => "N",
            "double" or "Double" or "System.Double" => "N",
            "decimal" or "Decimal" or "System.Decimal" => "N",
            "bool" or "Boolean" or "System.Boolean" => "BOOL",
            _ => "S"
        };
    }

    private static string GetTypeForMetadata(string propertyType)
    {
        var baseType = GetBaseType(propertyType);
        return baseType switch
        {
            "string" or "String" or "System.String" => "string",
            "int" or "Int32" or "System.Int32" => "int",
            "long" or "Int64" or "System.Int64" => "long",
            "double" or "Double" or "System.Double" => "double",
            "decimal" or "Decimal" or "System.Decimal" => "decimal",
            "bool" or "Boolean" or "System.Boolean" => "bool",
            _ => "string"
        };
    }

    private static string EscapeString(string value)
    {
        if (string.IsNullOrEmpty(value))
            return string.Empty;
        
        return value
            .Replace("\\", "\\\\")
            .Replace("\"", "\\\"")
            .Replace("\n", "\\n")
            .Replace("\r", "\\r")
            .Replace("\t", "\\t");
    }

    /// <summary>
    /// Gets all key attribute names for an index (used for IndexMetadata.ProjectedProperties).
    /// </summary>
    public static string[] GetKeyAttributeNames(EntityModel entity, IndexModel index)
    {
        var keyProperties = CollectKeyProperties(entity, index);
        return keyProperties.Select(k => k.AttributeName).ToArray();
    }

    /// <summary>
    /// Internal class to hold key property information.
    /// </summary>
    private class KeyPropertyInfo
    {
        public string PropertyName { get; set; } = string.Empty;
        public string AttributeName { get; set; } = string.Empty;
        public string PropertyType { get; set; } = "string";
        public bool IsGsiKey { get; set; }
        public bool IsLsiKey { get; set; }
        public bool IsTableKey { get; set; }
        public bool IsPartitionKey { get; set; }
        public bool IsSortKey { get; set; }
        public string Description { get; set; } = string.Empty;
    }
}
