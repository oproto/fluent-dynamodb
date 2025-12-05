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
        sb.AppendLine("using Oproto.FluentDynamoDb.Storage;");
        sb.AppendLine();
        
        // Namespace
        sb.AppendLine($"namespace {projection.Namespace}");
        sb.AppendLine("{");
        
        // Partial class - implement IProjectionModel or IDiscriminatedProjection
        sb.AppendLine($"    /// <summary>");
        sb.AppendLine($"    /// Generated implementation for projection model {projection.ClassName}.");
        sb.AppendLine($"    /// Provides automatic mapping from DynamoDB AttributeValue dictionaries.");
        sb.AppendLine($"    /// </summary>");
        
        // Determine which interface to implement based on discriminator presence
        var hasDiscriminator = projection.Discriminator != null && projection.Discriminator.IsValid;
        var interfaceType = hasDiscriminator 
            ? $"IDiscriminatedProjection<{projection.ClassName}>"
            : $"IProjectionModel<{projection.ClassName}>";
        
        sb.AppendLine($"    public partial class {projection.ClassName} : {interfaceType}");
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
        
        // Close class
        sb.AppendLine("    }");
        
        // Close namespace
        sb.AppendLine("}");
        
        return sb.ToString();
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
