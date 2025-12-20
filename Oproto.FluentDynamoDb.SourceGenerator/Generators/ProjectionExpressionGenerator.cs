using Oproto.FluentDynamoDb.SourceGenerator.Models;
using System.Text;

namespace Oproto.FluentDynamoDb.SourceGenerator.Generators;

/// <summary>
/// Generates projection expression strings and metadata for projection models.
/// </summary>
internal static class ProjectionExpressionGenerator
{
    /// <summary>
    /// Generates a projection expression string for a projection model.
    /// Maps property names to DynamoDB attribute names and includes discriminator if present.
    /// </summary>
    /// <param name="projection">The projection model to generate expression for.</param>
    /// <returns>A comma-separated projection expression string (e.g., "id, amount, created_date, entity_type").</returns>
    /// <example>
    /// For a projection with properties: Id, Amount, Status
    /// And discriminator property: EntityType
    /// Returns: "id, amount, status, entity_type"
    /// </example>
    public static string GenerateProjectionExpression(ProjectionModel projection)
    {
        var attributeNames = new List<string>();
        
        // Add all projection properties
        foreach (var property in projection.Properties)
        {
            if (!string.IsNullOrEmpty(property.AttributeName))
            {
                attributeNames.Add(property.AttributeName);
            }
        }
        
        // Include entity-level discriminator property if configured
        var discriminatorProp = DiscriminatorCodeGenerator.GetDiscriminatorPropertyName(projection.Discriminator);
        if (!string.IsNullOrEmpty(discriminatorProp) && !attributeNames.Contains(discriminatorProp))
        {
            attributeNames.Add(discriminatorProp);
        }
        
        // Include GSI-specific discriminator if different from entity discriminator
        var gsiDiscriminatorProp = DiscriminatorCodeGenerator.GetDiscriminatorPropertyName(projection.GsiDiscriminator);
        if (!string.IsNullOrEmpty(gsiDiscriminatorProp) && 
            gsiDiscriminatorProp != discriminatorProp &&
            !attributeNames.Contains(gsiDiscriminatorProp))
        {
            attributeNames.Add(gsiDiscriminatorProp);
        }
        
        // Join with comma and space for readability
        return string.Join(", ", attributeNames);
    }
    
    /// <summary>
    /// Generates metadata class containing projection information.
    /// Creates a static class with projection expression constant and property mappings.
    /// </summary>
    /// <param name="projection">The projection model to generate metadata for.</param>
    /// <returns>Generated C# source code for the metadata class.</returns>
    public static string GenerateProjectionMetadata(ProjectionModel projection)
    {
        var sb = new StringBuilder();
        
        // File header with auto-generated comment, nullable directive, timestamp, and version
        FileHeaderGenerator.GenerateFileHeader(sb);
        
        // Using statements
        sb.AppendLine("using System.Collections.Generic;");
        sb.AppendLine();
        
        // Namespace
        sb.AppendLine($"namespace {projection.Namespace}");
        sb.AppendLine("{");
        
        // Metadata class
        sb.AppendLine($"    /// <summary>");
        sb.AppendLine($"    /// Generated metadata for projection model {projection.ClassName}.");
        sb.AppendLine($"    /// Contains projection expression and property mapping information.");
        sb.AppendLine($"    /// </summary>");
        sb.AppendLine($"    internal static class {projection.ClassName}Metadata");
        sb.AppendLine("    {");
        
        // Projection expression constant
        var projectionExpression = GenerateProjectionExpression(projection);
        sb.AppendLine($"        /// <summary>");
        sb.AppendLine($"        /// The DynamoDB projection expression for {projection.ClassName}.");
        sb.AppendLine($"        /// </summary>");
        sb.AppendLine($"        public const string ProjectionExpression = \"{projectionExpression}\";");
        sb.AppendLine();
        
        // Source entity type
        sb.AppendLine($"        /// <summary>");
        sb.AppendLine($"        /// The source entity type that this projection derives from.");
        sb.AppendLine($"        /// </summary>");
        sb.AppendLine($"        public const string SourceEntityType = \"{projection.SourceEntityType}\";");
        sb.AppendLine();
        
        // Discriminator information if applicable
        if (projection.Discriminator != null && projection.Discriminator.IsValid)
        {
            sb.AppendLine($"        /// <summary>");
            sb.AppendLine($"        /// The discriminator property name for multi-entity queries.");
            sb.AppendLine($"        /// </summary>");
            sb.AppendLine($"        public const string DiscriminatorProperty = \"{projection.Discriminator.PropertyName}\";");
            sb.AppendLine();
            
            if (projection.Discriminator.Strategy == DiscriminatorStrategy.ExactMatch && 
                !string.IsNullOrEmpty(projection.Discriminator.ExactValue))
            {
                sb.AppendLine($"        /// <summary>");
                sb.AppendLine($"        /// The discriminator value for the source entity.");
                sb.AppendLine($"        /// </summary>");
                sb.AppendLine($"        public const string DiscriminatorValue = \"{projection.Discriminator.ExactValue}\";");
                sb.AppendLine();
            }
            else if (!string.IsNullOrEmpty(projection.Discriminator.Pattern))
            {
                sb.AppendLine($"        /// <summary>");
                sb.AppendLine($"        /// The discriminator pattern for the source entity.");
                sb.AppendLine($"        /// </summary>");
                sb.AppendLine($"        public const string DiscriminatorPattern = \"{projection.Discriminator.Pattern}\";");
                sb.AppendLine();
            }
        }
        
        // Property mappings dictionary
        sb.AppendLine($"        /// <summary>");
        sb.AppendLine($"        /// Maps projection property names to DynamoDB attribute names.");
        sb.AppendLine($"        /// </summary>");
        sb.AppendLine($"        public static readonly IReadOnlyDictionary<string, string> PropertyMappings = new Dictionary<string, string>");
        sb.AppendLine("        {");
        
        foreach (var property in projection.Properties)
        {
            if (!string.IsNullOrEmpty(property.AttributeName))
            {
                sb.AppendLine($"            {{ \"{property.PropertyName}\", \"{property.AttributeName}\" }},");
            }
        }
        
        sb.AppendLine("        };");
        
        // Close metadata class
        sb.AppendLine("    }");
        
        // Close namespace
        sb.AppendLine("}");
        
        return sb.ToString();
    }
    
    /// <summary>
    /// Generates FromDynamoDb method for a projection model.
    /// Creates a partial class with static method to hydrate projection from DynamoDB response.
    /// Also generates IReadOnlyEntity implementation methods (GetPartitionKey, GetEntityMetadata).
    /// </summary>
    /// <param name="projection">The projection model to generate method for.</param>
    /// <returns>Generated C# source code for the partial class with FromDynamoDb method.</returns>
    public static string GenerateFromDynamoDbMethod(ProjectionModel projection)
    {
        var sb = new StringBuilder();
        
        // File header with auto-generated comment, nullable directive, timestamp, and version
        FileHeaderGenerator.GenerateFileHeader(sb);
        
        // Using statements
        sb.AppendLine("using System;");
        sb.AppendLine("using System.Collections.Generic;");
        sb.AppendLine("using System.IO;");
        sb.AppendLine("using Amazon.DynamoDBv2.Model;");
        sb.AppendLine("using Oproto.FluentDynamoDb.Entities;");
        sb.AppendLine("using Oproto.FluentDynamoDb.Metadata;");
        sb.AppendLine();
        
        // Namespace
        sb.AppendLine($"namespace {projection.Namespace}");
        sb.AppendLine("{");
        
        // Partial class - implement IProjectionModel or IDiscriminatedProjection AND IReadOnlyEntity
        sb.AppendLine($"    /// <summary>");
        sb.AppendLine($"    /// Generated implementation for projection model {projection.ClassName}.");
        sb.AppendLine($"    /// Provides automatic mapping from DynamoDB AttributeValue dictionaries.");
        sb.AppendLine($"    /// Implements both IProjectionModel and IReadOnlyEntity for QueryRequestBuilder compatibility.");
        sb.AppendLine($"    /// </summary>");
        
        // Determine which interface to implement based on discriminator presence
        var hasDiscriminator = projection.Discriminator != null && projection.Discriminator.IsValid;
        var projectionInterface = hasDiscriminator 
            ? $"IDiscriminatedProjection<{projection.ClassName}>"
            : $"IProjectionModel<{projection.ClassName}>";
        
        // Implement both IProjectionModel (or IDiscriminatedProjection) and IReadOnlyEntity
        sb.AppendLine($"    public partial class {projection.ClassName} : {projectionInterface}, IReadOnlyEntity");
        sb.AppendLine("    {");
        
        // Static ProjectionExpression property (required by IProjectionModel)
        var projectionExpr = GenerateProjectionExpression(projection);
        sb.AppendLine("        /// <summary>");
        sb.AppendLine("        /// Gets the DynamoDB projection expression for this model.");
        sb.AppendLine("        /// </summary>");
        sb.AppendLine($"        public static string ProjectionExpression => \"{projectionExpr}\";");
        sb.AppendLine();
        
        // Add discriminator properties if applicable (required by IDiscriminatedProjection)
        if (hasDiscriminator)
        {
            sb.AppendLine("        /// <summary>");
            sb.AppendLine("        /// Gets the discriminator property name in DynamoDB.");
            sb.AppendLine("        /// </summary>");
            sb.AppendLine($"        public static string? DiscriminatorProperty => \"{projection.Discriminator!.PropertyName}\";");
            sb.AppendLine();
            
            var discriminatorValue = projection.Discriminator.Strategy == DiscriminatorStrategy.ExactMatch 
                ? projection.Discriminator.ExactValue 
                : null;
            sb.AppendLine("        /// <summary>");
            sb.AppendLine("        /// Gets the expected discriminator value for this projection type.");
            sb.AppendLine("        /// </summary>");
            if (!string.IsNullOrEmpty(discriminatorValue))
            {
                sb.AppendLine($"        public static string? DiscriminatorValue => \"{discriminatorValue}\";");
            }
            else
            {
                sb.AppendLine($"        public static string? DiscriminatorValue => null;");
            }
            sb.AppendLine();
        }
        
        // FromDynamoDb method
        sb.AppendLine("        /// <summary>");
        sb.AppendLine("        /// Converts a DynamoDB item to a projection model instance.");
        sb.AppendLine("        /// Handles nullable properties and missing attributes gracefully.");
        sb.AppendLine("        /// </summary>");
        sb.AppendLine("        /// <param name=\"item\">The DynamoDB item to map from.</param>");
        sb.AppendLine($"        /// <returns>A mapped {projection.ClassName} instance.</returns>");
        sb.AppendLine("        /// <exception cref=\"DynamoDbMappingException\">Thrown when mapping fails due to data conversion issues.</exception>");
        sb.AppendLine($"        public static {projection.ClassName} FromDynamoDb(Dictionary<string, AttributeValue> item)");
        sb.AppendLine("        {");
        sb.AppendLine("            if (item == null)");
        sb.AppendLine("                throw new ArgumentNullException(nameof(item));");
        sb.AppendLine();
        sb.AppendLine("            try");
        sb.AppendLine("            {");
        
        // Generate discriminator validation if applicable
        if (projection.Discriminator != null && projection.Discriminator.IsValid)
        {
            var validationCode = DiscriminatorCodeGenerator.GenerateDiscriminatorValidation(
                projection.Discriminator, 
                projection.ClassName);
            sb.Append(validationCode);
        }
        
        sb.AppendLine($"                var projection = new {projection.ClassName}();");
        sb.AppendLine();
        
        // Generate property mappings
        foreach (var property in projection.Properties)
        {
            GenerateProjectionPropertyMapping(sb, property, projection);
        }
        
        sb.AppendLine("                return projection;");
        sb.AppendLine("            }");
        sb.AppendLine("            catch (Exception ex) when (ex is not DynamoDbMappingException)");
        sb.AppendLine("            {");
        sb.AppendLine("                throw DynamoDbMappingException.ProjectionMappingFailed(");
        sb.AppendLine($"                    typeof({projection.ClassName}),");
        sb.AppendLine($"                    \"{projection.SourceEntityType}\",");
        sb.AppendLine("                    ex);");
        sb.AppendLine("            }");
        sb.AppendLine("        }");
        sb.AppendLine();
        
        // Generate IReadOnlyEntity implementation methods
        GenerateReadOnlyEntityMethods(sb, projection);
        
        // Generate the generic FromDynamoDb<TSelf> method required by IReadOnlyEntity
        GenerateGenericFromDynamoDbMethod(sb, projection);
        
        // Close class
        sb.AppendLine("    }");
        
        // Close namespace
        sb.AppendLine("}");
        
        return sb.ToString();
    }
    
    /// <summary>
    /// Generates the IReadOnlyEntity interface implementation methods for a projection.
    /// This includes GetPartitionKey (delegating to source entity) and GetEntityMetadata.
    /// </summary>
    private static void GenerateReadOnlyEntityMethods(StringBuilder sb, ProjectionModel projection)
    {
        // Generate GetPartitionKey method that delegates to source entity
        sb.AppendLine("        // ===== IReadOnlyEntity Implementation =====");
        sb.AppendLine();
        sb.AppendLine("        /// <summary>");
        sb.AppendLine("        /// Extracts the partition key value from a DynamoDB item.");
        sb.AppendLine($"        /// Delegates to source entity: {projection.SourceEntityType}.");
        sb.AppendLine("        /// </summary>");
        sb.AppendLine("        /// <param name=\"item\">The DynamoDB item.</param>");
        sb.AppendLine("        /// <returns>The partition key value.</returns>");
        sb.AppendLine("        public static string GetPartitionKey(Dictionary<string, AttributeValue> item)");
        sb.AppendLine("        {");
        
        // If we have inherited metadata with partition key info, use it directly
        if (projection.InheritedMetadata != null && !string.IsNullOrEmpty(projection.InheritedMetadata.PartitionKeyAttributeName))
        {
            var pkAttrName = projection.InheritedMetadata.PartitionKeyAttributeName;
            sb.AppendLine($"            // Extract partition key from attribute '{pkAttrName}'");
            sb.AppendLine($"            if (item.TryGetValue(\"{pkAttrName}\", out var pkAttr))");
            sb.AppendLine("            {");
            sb.AppendLine("                return pkAttr.S ?? pkAttr.N ?? string.Empty;");
            sb.AppendLine("            }");
            sb.AppendLine("            return string.Empty;");
        }
        else
        {
            // Delegate to source entity's GetPartitionKey method
            sb.AppendLine($"            // Delegate to source entity's GetPartitionKey method");
            sb.AppendLine($"            return {projection.SourceEntityType}.GetPartitionKey(item);");
        }
        
        sb.AppendLine("        }");
        sb.AppendLine();
        
        // Generate GetEntityMetadata method
        sb.AppendLine("        /// <summary>");
        sb.AppendLine("        /// Gets the entity metadata for this projection.");
        sb.AppendLine($"        /// Metadata is inherited from source entity: {projection.SourceEntityType}.");
        sb.AppendLine("        /// </summary>");
        sb.AppendLine("        /// <returns>The entity metadata.</returns>");
        sb.AppendLine("        public static EntityMetadata GetEntityMetadata()");
        sb.AppendLine("        {");
        
        // If we have inherited metadata, generate it inline
        if (projection.InheritedMetadata != null)
        {
            GenerateInlineEntityMetadata(sb, projection);
        }
        else
        {
            // Delegate to source entity and modify for projection
            sb.AppendLine($"            // Get metadata from source entity and modify for projection");
            sb.AppendLine($"            var sourceMetadata = {projection.SourceEntityType}.GetEntityMetadata();");
            sb.AppendLine("            return new EntityMetadata");
            sb.AppendLine("            {");
            sb.AppendLine("                TableName = sourceMetadata.TableName,");
            sb.AppendLine("                PartitionKeyAttributeName = sourceMetadata.PartitionKeyAttributeName,");
            sb.AppendLine("                PartitionKeyAttributeType = sourceMetadata.PartitionKeyAttributeType,");
            sb.AppendLine("                SortKeyAttributeName = sourceMetadata.SortKeyAttributeName,");
            sb.AppendLine("                SortKeyAttributeType = sourceMetadata.SortKeyAttributeType,");
            sb.AppendLine("                // Projections are read-only - exclude write-specific metadata");
            sb.AppendLine("                RequiresWriteTransaction = false,");
            sb.AppendLine("                IsMultiItemEntity = false,");
            sb.AppendLine("                Properties = Array.Empty<PropertyMetadata>(),");
            sb.AppendLine("                Indexes = Array.Empty<IndexMetadata>(),");
            sb.AppendLine("                Relationships = Array.Empty<RelationshipMetadata>()");
            sb.AppendLine("            };");
        }
        
        sb.AppendLine("        }");
    }
    
    /// <summary>
    /// Generates inline EntityMetadata from the projection's inherited metadata.
    /// </summary>
    private static void GenerateInlineEntityMetadata(StringBuilder sb, ProjectionModel projection)
    {
        var metadata = projection.InheritedMetadata!;
        
        sb.AppendLine("            return new EntityMetadata");
        sb.AppendLine("            {");
        sb.AppendLine($"                TableName = \"{EscapeString(metadata.TableName)}\",");
        sb.AppendLine($"                PartitionKeyAttributeName = \"{EscapeString(metadata.PartitionKeyAttributeName)}\",");
        sb.AppendLine($"                PartitionKeyAttributeType = \"{EscapeString(metadata.PartitionKeyAttributeType)}\",");
        
        if (!string.IsNullOrEmpty(metadata.SortKeyAttributeName))
        {
            sb.AppendLine($"                SortKeyAttributeName = \"{EscapeString(metadata.SortKeyAttributeName)}\",");
            sb.AppendLine($"                SortKeyAttributeType = \"{EscapeString(metadata.SortKeyAttributeType ?? "S")}\",");
        }
        else
        {
            sb.AppendLine("                SortKeyAttributeName = null,");
            sb.AppendLine("                SortKeyAttributeType = null,");
        }
        
        // Projections are read-only - exclude write-specific metadata
        sb.AppendLine("                RequiresWriteTransaction = false,");
        sb.AppendLine("                IsMultiItemEntity = false,");
        
        // Generate property metadata array
        if (metadata.Properties.Length > 0)
        {
            sb.AppendLine("                Properties = new PropertyMetadata[]");
            sb.AppendLine("                {");
            foreach (var prop in metadata.Properties)
            {
                sb.AppendLine("                    new PropertyMetadata");
                sb.AppendLine("                    {");
                sb.AppendLine($"                        PropertyName = \"{EscapeString(prop.PropertyName)}\",");
                sb.AppendLine($"                        AttributeName = \"{EscapeString(prop.AttributeName)}\",");
                sb.AppendLine($"                        PropertyType = {GetTypeOfExpression(prop.PropertyType)},");
                sb.AppendLine($"                        IsPartitionKey = {prop.IsPartitionKey.ToString().ToLower()},");
                sb.AppendLine($"                        IsSortKey = {prop.IsSortKey.ToString().ToLower()},");
                sb.AppendLine($"                        IsNullable = {prop.IsNullable.ToString().ToLower()}");
                sb.AppendLine("                    },");
            }
            sb.AppendLine("                },");
        }
        else
        {
            sb.AppendLine("                Properties = Array.Empty<PropertyMetadata>(),");
        }
        
        sb.AppendLine("                Indexes = Array.Empty<IndexMetadata>(),");
        sb.AppendLine("                Relationships = Array.Empty<RelationshipMetadata>()");
        sb.AppendLine("            };");
    }
    
    /// <summary>
    /// Gets the typeof() expression for a property type string.
    /// </summary>
    private static string GetTypeOfExpression(string propertyType)
    {
        if (string.IsNullOrEmpty(propertyType))
            return "typeof(object)";
        
        // Handle nullable types
        var isNullable = propertyType.EndsWith("?");
        var baseType = propertyType.TrimEnd('?');
        
        // Map common type names to their typeof expressions
        var typeExpression = baseType switch
        {
            "string" or "String" or "System.String" => "typeof(string)",
            "int" or "Int32" or "System.Int32" => "typeof(int)",
            "long" or "Int64" or "System.Int64" => "typeof(long)",
            "double" or "Double" or "System.Double" => "typeof(double)",
            "float" or "Single" or "System.Single" => "typeof(float)",
            "decimal" or "Decimal" or "System.Decimal" => "typeof(decimal)",
            "bool" or "Boolean" or "System.Boolean" => "typeof(bool)",
            "DateTime" or "System.DateTime" => "typeof(DateTime)",
            "DateTimeOffset" or "System.DateTimeOffset" => "typeof(DateTimeOffset)",
            "Guid" or "System.Guid" => "typeof(Guid)",
            "byte[]" or "System.Byte[]" => "typeof(byte[])",
            _ => $"typeof({baseType})"
        };
        
        return typeExpression;
    }
    
    /// <summary>
    /// Escapes a string for use in generated code.
    /// </summary>
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
    /// Generates the generic FromDynamoDb&lt;TSelf&gt; method required by IReadOnlyEntity interface.
    /// This method delegates to the non-generic FromDynamoDb method.
    /// </summary>
    private static void GenerateGenericFromDynamoDbMethod(StringBuilder sb, ProjectionModel projection)
    {
        sb.AppendLine();
        sb.AppendLine("        /// <summary>");
        sb.AppendLine("        /// Creates an entity instance from a single DynamoDB item.");
        sb.AppendLine("        /// Required by IReadOnlyEntity interface.");
        sb.AppendLine("        /// </summary>");
        sb.AppendLine("        /// <typeparam name=\"TSelf\">The entity type implementing this interface.</typeparam>");
        sb.AppendLine("        /// <param name=\"item\">The DynamoDB item as an AttributeValue dictionary.</param>");
        sb.AppendLine("        /// <param name=\"options\">Optional configuration options. Not used for projections.</param>");
        sb.AppendLine("        /// <returns>The mapped projection instance.</returns>");
        sb.AppendLine("        /// <exception cref=\"ArgumentException\">Thrown when the type parameter doesn't match the projection type.</exception>");
        sb.AppendLine("        public static TSelf FromDynamoDb<TSelf>(Dictionary<string, AttributeValue> item, FluentDynamoDbOptions? options = null) where TSelf : IReadOnlyEntity");
        sb.AppendLine("        {");
        sb.AppendLine($"            if (typeof(TSelf) != typeof({projection.ClassName}))");
        sb.AppendLine("            {");
        sb.AppendLine($"                throw new ArgumentException($\"Type parameter must be {projection.ClassName}, but was {{typeof(TSelf).Name}}\", nameof(TSelf));");
        sb.AppendLine("            }");
        sb.AppendLine();
        sb.AppendLine("            // Delegate to the non-generic FromDynamoDb method");
        sb.AppendLine($"            return (TSelf)(object)FromDynamoDb(item);");
        sb.AppendLine("        }");
    }
    
    /// <summary>
    /// Generates property mapping code for a single projection property.
    /// Handles nullable properties and missing attributes.
    /// </summary>
    private static void GenerateProjectionPropertyMapping(StringBuilder sb, ProjectionPropertyModel property, ProjectionModel projection)
    {
        var attributeName = property.AttributeName;
        var propertyName = property.PropertyName;
        var propertyType = property.PropertyType;
        
        if (string.IsNullOrEmpty(attributeName))
            return;
        
        // Check if attribute exists in item
        sb.AppendLine($"                // Map {propertyName} from attribute '{attributeName}'");
        sb.AppendLine($"                if (item.TryGetValue(\"{attributeName}\", out var {propertyName.ToLower()}Attr))");
        sb.AppendLine("                {");
        sb.AppendLine("                    try");
        sb.AppendLine("                    {");
        
        // Generate conversion based on property type
        var conversionCode = GenerateAttributeValueConversion(property, $"{propertyName.ToLower()}Attr");
        sb.AppendLine($"                        projection.{propertyName} = {conversionCode};");
        
        sb.AppendLine("                    }");
        sb.AppendLine("                    catch (Exception ex)");
        sb.AppendLine("                    {");
        sb.AppendLine("                        throw DynamoDbMappingException.PropertyConversionFailed(");
        sb.AppendLine($"                            typeof({projection.ClassName}),");
        sb.AppendLine($"                            \"{propertyName}\",");
        sb.AppendLine($"                            {propertyName.ToLower()}Attr,");
        sb.AppendLine($"                            typeof({GetTypeForConversion(propertyType)}),");
        sb.AppendLine("                            ex);");
        sb.AppendLine("                    }");
        sb.AppendLine("                }");
        
        // Handle missing attributes for non-nullable properties
        if (!property.IsNullable)
        {
            sb.AppendLine("                else");
            sb.AppendLine("                {");
            sb.AppendLine("                    throw DynamoDbMappingException.RequiredAttributeMissing(");
            sb.AppendLine($"                        typeof({projection.ClassName}),");
            sb.AppendLine($"                        \"{attributeName}\",");
            sb.AppendLine($"                        \"{propertyName}\");");
            sb.AppendLine("                }");
        }
        
        sb.AppendLine();
    }
    
    /// <summary>
    /// Generates the conversion code from AttributeValue to property type.
    /// </summary>
    private static string GenerateAttributeValueConversion(ProjectionPropertyModel property, string attrVarName)
    {
        var baseType = GetBaseType(property.PropertyType);
        var isNullable = property.IsNullable;
        
        return baseType switch
        {
            "string" or "System.String" => $"{attrVarName}.S",
            "int" or "System.Int32" => isNullable 
                ? $"int.Parse({attrVarName}.N)" 
                : $"int.Parse({attrVarName}.N)",
            "long" or "System.Int64" => isNullable 
                ? $"long.Parse({attrVarName}.N)" 
                : $"long.Parse({attrVarName}.N)",
            "double" or "System.Double" => isNullable 
                ? $"double.Parse({attrVarName}.N)" 
                : $"double.Parse({attrVarName}.N)",
            "float" or "System.Single" => isNullable 
                ? $"float.Parse({attrVarName}.N)" 
                : $"float.Parse({attrVarName}.N)",
            "decimal" or "System.Decimal" => isNullable 
                ? $"decimal.Parse({attrVarName}.N)" 
                : $"decimal.Parse({attrVarName}.N)",
            "bool" or "System.Boolean" => isNullable 
                ? $"{attrVarName}.BOOL" 
                : $"{attrVarName}.BOOL",
            "DateTime" or "System.DateTime" => isNullable 
                ? $"DateTime.Parse({attrVarName}.S)" 
                : $"DateTime.Parse({attrVarName}.S)",
            "DateTimeOffset" or "System.DateTimeOffset" => isNullable 
                ? $"DateTimeOffset.Parse({attrVarName}.S)" 
                : $"DateTimeOffset.Parse({attrVarName}.S)",
            "Guid" or "System.Guid" => isNullable 
                ? $"Guid.Parse({attrVarName}.S)" 
                : $"Guid.Parse({attrVarName}.S)",
            "Ulid" or "System.Ulid" => isNullable 
                ? $"Ulid.Parse({attrVarName}.S)" 
                : $"Ulid.Parse({attrVarName}.S)",
            "byte[]" or "System.Byte[]" => $"{attrVarName}.B.ToArray()",
            _ => $"{attrVarName}.S" // Default to string
        };
    }
    
    /// <summary>
    /// Gets the base type name without nullable annotation.
    /// </summary>
    private static string GetBaseType(string typeName)
    {
        return typeName.TrimEnd('?');
    }
    
    /// <summary>
    /// Gets the type name for conversion error messages.
    /// </summary>
    private static string GetTypeForConversion(string typeName)
    {
        // Remove nullable annotation for error messages
        return typeName.TrimEnd('?');
    }
    
    /// <summary>
    /// Gets the DynamoDB attribute name for the discriminator property.
    /// Typically "entity_type" or similar based on naming conventions.
    /// </summary>
    private static string GetDiscriminatorAttributeName(ProjectionModel projection)
    {
        // The discriminator property is typically stored as "entity_type" in DynamoDB
        // This follows the common naming convention used in the library
        return "entity_type";
    }
}
