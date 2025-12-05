using Oproto.FluentDynamoDb.SourceGenerator.Models;
using System.Text;

namespace Oproto.FluentDynamoDb.SourceGenerator.Generators;

/// <summary>
/// Generates stream conversion methods for entities with [GenerateStreamConversion] attribute.
/// Creates FromDynamoDbStream and FromStreamImage methods that deserialize Lambda AttributeValue dictionaries.
/// </summary>
/// <remarks>
/// <para><strong>Architecture:</strong></para>
/// <para>
/// StreamMapperGenerator creates conversion methods specifically for DynamoDB Streams processing:
/// - FromDynamoDbStream: Converts Lambda AttributeValue dictionaries to C# entities
/// - FromStreamImage: Helper method that selects NewImage or OldImage from StreamRecord
/// </para>
/// <para><strong>Key Differences from Standard Mapping:</strong></para>
/// <list type="bullet">
/// <item><description>Uses Amazon.Lambda.DynamoDBEvents.AttributeValue instead of SDK AttributeValue</description></item>
/// <item><description>Handles encryption/decryption using IFieldEncryptor</description></item>
/// <item><description>Validates discriminators when configured</description></item>
/// <item><description>Returns null for null input (stream records may have null images)</description></item>
/// </list>
/// </remarks>
internal static class StreamMapperGenerator
{
    /// <summary>
    /// Generates stream conversion methods for an entity.
    /// Only generates if GenerateStreamConversion is true.
    /// </summary>
    /// <param name="entity">The entity model to generate stream conversion for.</param>
    /// <returns>The generated C# source code, or empty string if not applicable.</returns>
    public static string GenerateStreamConversion(EntityModel entity)
    {
        if (!entity.GenerateStreamConversion)
        {
            return string.Empty;
        }

        var sb = new StringBuilder();

        // File header with auto-generated comment, nullable directive, timestamp, and version
        FileHeaderGenerator.GenerateFileHeader(sb);

        // All necessary using statements
        sb.AppendLine("using System;");
        sb.AppendLine("using System.Collections.Generic;");
        sb.AppendLine("using Amazon.Lambda.DynamoDBEvents;");
        sb.AppendLine("using Oproto.FluentDynamoDb.Storage;");
        
        // Add encryption support if needed
        var hasEncryptedProperties = entity.Properties.Any(p => p.Security?.IsEncrypted == true);
        if (hasEncryptedProperties)
        {
            sb.AppendLine("using Oproto.FluentDynamoDb.Logging;");
        }
        
        sb.AppendLine();

        // Namespace declaration
        sb.AppendLine($"namespace {entity.Namespace}");
        sb.AppendLine("{");

        // XML documentation
        sb.AppendLine($"    /// <summary>");
        sb.AppendLine($"    /// Stream conversion methods for {entity.ClassName}.");
        sb.AppendLine($"    /// Provides deserialization from DynamoDB Stream events using Lambda AttributeValue types.");
        sb.AppendLine($"    /// </summary>");

        // Partial class declaration
        sb.AppendLine($"    public partial class {entity.ClassName}");
        sb.AppendLine("    {");

        // Generate FromDynamoDbStream method
        GenerateFromDynamoDbStreamMethod(sb, entity);

        // Generate FromStreamImage helper method
        GenerateFromStreamImageMethod(sb, entity);

        // Closing braces for class and namespace
        sb.AppendLine("    }");
        sb.AppendLine("}");

        return sb.ToString();
    }

    private static void GenerateFromDynamoDbStreamMethod(StringBuilder sb, EntityModel entity)
    {
        var hasEncryptedProperties = entity.Properties.Any(p => p.Security?.IsEncrypted == true);
        
        sb.AppendLine();
        sb.AppendLine("        /// <summary>");
        sb.AppendLine("        /// Converts a DynamoDB Stream image (Lambda AttributeValue dictionary) to an entity instance.");
        sb.AppendLine("        /// This method is specifically designed for processing DynamoDB Stream events in AWS Lambda.");
        sb.AppendLine("        /// </summary>");
        sb.AppendLine("        /// <param name=\"item\">The Lambda AttributeValue dictionary from a stream record image.</param>");
        
        if (hasEncryptedProperties)
        {
            sb.AppendLine("        /// <param name=\"fieldEncryptor\">Optional field encryptor for decrypting encrypted properties.</param>");
        }
        
        sb.AppendLine("        /// <returns>The deserialized entity instance, or null if the input is null.</returns>");
        
        if (hasEncryptedProperties)
        {
            sb.AppendLine($"        public static {entity.ClassName}? FromDynamoDbStream(");
            sb.AppendLine("            Dictionary<string, DynamoDBEvent.AttributeValue>? item,");
            sb.AppendLine("            IFieldEncryptor? fieldEncryptor = null)");
        }
        else
        {
            sb.AppendLine($"        public static {entity.ClassName}? FromDynamoDbStream(Dictionary<string, DynamoDBEvent.AttributeValue>? item)");
        }
        
        sb.AppendLine("        {");
        sb.AppendLine("            if (item == null) return null;");
        sb.AppendLine();
        sb.AppendLine($"            var entity = new {entity.ClassName}();");
        sb.AppendLine();

        // Generate property mappings
        foreach (var property in entity.Properties.Where(p => p.HasAttributeMapping && !p.IsComputed))
        {
            GeneratePropertyFromStreamAttributeValue(sb, property, entity);
        }

        // Generate discriminator validation if configured
        if (entity.Discriminator != null)
        {
            GenerateDiscriminatorValidation(sb, entity);
        }

        sb.AppendLine();
        sb.AppendLine("            return entity;");
        sb.AppendLine("        }");
    }

    private static void GenerateFromStreamImageMethod(StringBuilder sb, EntityModel entity)
    {
        var hasEncryptedProperties = entity.Properties.Any(p => p.Security?.IsEncrypted == true);
        
        sb.AppendLine();
        sb.AppendLine("        /// <summary>");
        sb.AppendLine("        /// Helper method to convert a StreamRecord to an entity by selecting the appropriate image.");
        sb.AppendLine("        /// </summary>");
        sb.AppendLine("        /// <param name=\"streamRecord\">The DynamoDB stream record.</param>");
        sb.AppendLine("        /// <param name=\"useNewImage\">True to use NewImage, false to use OldImage.</param>");
        
        if (hasEncryptedProperties)
        {
            sb.AppendLine("        /// <param name=\"fieldEncryptor\">Optional field encryptor for decrypting encrypted properties.</param>");
        }
        
        sb.AppendLine("        /// <returns>The deserialized entity instance, or null if the selected image is null.</returns>");
        
        if (hasEncryptedProperties)
        {
            sb.AppendLine($"        public static {entity.ClassName}? FromStreamImage(");
            sb.AppendLine("            DynamoDBEvent.StreamRecord streamRecord,");
            sb.AppendLine("            bool useNewImage,");
            sb.AppendLine("            IFieldEncryptor? fieldEncryptor = null)");
        }
        else
        {
            sb.AppendLine($"        public static {entity.ClassName}? FromStreamImage(");
            sb.AppendLine("            DynamoDBEvent.StreamRecord streamRecord,");
            sb.AppendLine("            bool useNewImage)");
        }
        
        sb.AppendLine("        {");
        sb.AppendLine("            var image = useNewImage ? streamRecord.NewImage : streamRecord.OldImage;");
        
        if (hasEncryptedProperties)
        {
            sb.AppendLine("            return FromDynamoDbStream(image, fieldEncryptor);");
        }
        else
        {
            sb.AppendLine("            return FromDynamoDbStream(image);");
        }
        
        sb.AppendLine("        }");
    }

    private static void GeneratePropertyFromStreamAttributeValue(StringBuilder sb, PropertyModel property, EntityModel entity)
    {
        var attributeName = property.AttributeName;
        var propertyName = property.PropertyName;
        var varName = propertyName.ToLowerInvariant() + "Value";

        // Handle encrypted properties
        if (property.Security?.IsEncrypted == true)
        {
            GenerateEncryptedPropertyFromStream(sb, property, entity);
            return;
        }

        // Handle extracted properties (skip - they're computed from other properties)
        if (property.IsExtracted)
        {
            return;
        }

        sb.AppendLine($"            // Map {propertyName} from stream attribute");
        sb.AppendLine($"            if (item.TryGetValue(\"{attributeName}\", out var {varName}))");
        sb.AppendLine("            {");

        // Generate conversion based on property type
        GenerateStreamAttributeValueConversion(sb, property, varName);

        sb.AppendLine("            }");
    }

    private static void GenerateStreamAttributeValueConversion(StringBuilder sb, PropertyModel property, string varName)
    {
        var propertyName = property.PropertyName;
        var propertyType = property.PropertyType;
        var baseType = GetBaseType(propertyType);

        // Handle different attribute types
        if (IsStringType(baseType))
        {
            sb.AppendLine($"                if ({varName}.S != null)");
            sb.AppendLine($"                    entity.{propertyName} = {varName}.S;");
        }
        else if (IsNumericType(baseType))
        {
            sb.AppendLine($"                if ({varName}.N != null)");
            sb.AppendLine($"                    entity.{propertyName} = {GetNumericConversion(baseType, varName + ".N")};");
        }
        else if (IsBoolType(baseType))
        {
            sb.AppendLine($"                if ({varName}.BOOL.HasValue)");
            sb.AppendLine($"                    entity.{propertyName} = {varName}.BOOL.Value;");
        }
        else if (IsBinaryType(baseType))
        {
            sb.AppendLine($"                if ({varName}.B != null)");
            sb.AppendLine($"                    entity.{propertyName} = {varName}.B.ToArray();");
        }
        else if (IsListType(baseType))
        {
            GenerateListConversion(sb, property, varName);
        }
        else if (IsSetType(baseType))
        {
            GenerateSetConversion(sb, property, varName);
        }
        else if (IsDictionaryType(baseType))
        {
            GenerateMapConversion(sb, property, varName);
        }
        else
        {
            // Complex type - assume it's stored as JSON string
            sb.AppendLine($"                if ({varName}.S != null)");
            sb.AppendLine($"                    entity.{propertyName} = System.Text.Json.JsonSerializer.Deserialize<{baseType}>({varName}.S);");
        }
    }

    private static void GenerateEncryptedPropertyFromStream(StringBuilder sb, PropertyModel property, EntityModel entity)
    {
        var attributeName = property.AttributeName;
        var propertyName = property.PropertyName;
        var varName = propertyName.ToLowerInvariant() + "Value";

        sb.AppendLine($"            // Decrypt encrypted property {propertyName}");
        sb.AppendLine($"            if (item.TryGetValue(\"{attributeName}\", out var {varName}) && {varName}.S != null)");
        sb.AppendLine("            {");
        sb.AppendLine("                if (fieldEncryptor == null)");
        sb.AppendLine("                {");
        sb.AppendLine($"                    throw new InvalidOperationException(");
        sb.AppendLine($"                        \"Property {propertyName} is encrypted but no IFieldEncryptor was provided. \" +");
        sb.AppendLine($"                        \"Pass an IFieldEncryptor to FromDynamoDbStream to decrypt encrypted properties.\");");
        sb.AppendLine("                }");
        sb.AppendLine();
        sb.AppendLine("                var context = new FieldEncryptionContext");
        sb.AppendLine("                {");
        sb.AppendLine($"                    EntityType = typeof({entity.ClassName}),");
        sb.AppendLine($"                    PropertyName = \"{propertyName}\",");
        sb.AppendLine("                    EncryptionContextId = DynamoDbOperationContext.EncryptionContextId");
        sb.AppendLine("                };");
        sb.AppendLine();
        sb.AppendLine($"                var decrypted = fieldEncryptor.Decrypt({varName}.S, context);");
        sb.AppendLine($"                entity.{propertyName} = decrypted;");
        sb.AppendLine("            }");
    }

    private static void GenerateDiscriminatorValidation(StringBuilder sb, EntityModel entity)
    {
        var discriminator = entity.Discriminator!;
        var property = entity.Properties.FirstOrDefault(p => p.AttributeName == discriminator.PropertyName);
        
        if (property == null)
        {
            return;
        }

        sb.AppendLine();
        sb.AppendLine("            // Validate discriminator");
        sb.AppendLine($"            if (item.TryGetValue(\"{discriminator.PropertyName}\", out var discriminatorValue) && discriminatorValue.S != null)");
        sb.AppendLine("            {");

        if (!string.IsNullOrEmpty(discriminator.Pattern))
        {
            // Pattern matching
            var pattern = discriminator.Pattern;
            if (pattern.StartsWith("*") && pattern.EndsWith("*"))
            {
                // Contains
                var value = pattern.Trim('*');
                sb.AppendLine($"                if (!discriminatorValue.S.Contains(\"{value}\"))");
            }
            else if (pattern.StartsWith("*"))
            {
                // EndsWith
                var value = pattern.TrimStart('*');
                sb.AppendLine($"                if (!discriminatorValue.S.EndsWith(\"{value}\"))");
            }
            else if (pattern.EndsWith("*"))
            {
                // StartsWith
                var value = pattern.TrimEnd('*');
                sb.AppendLine($"                if (!discriminatorValue.S.StartsWith(\"{value}\"))");
            }
            else
            {
                // Exact match
                sb.AppendLine($"                if (discriminatorValue.S != \"{pattern}\")");
            }
        }
        else if (!string.IsNullOrEmpty(discriminator.ExactValue))
        {
            // Exact value match
            sb.AppendLine($"                if (discriminatorValue.S != \"{discriminator.ExactValue}\")");
        }

        sb.AppendLine("                {");
        sb.AppendLine("                    throw new DiscriminatorMismatchException(");
        sb.AppendLine($"                        \"{discriminator.ExactValue ?? discriminator.Pattern}\",");
        sb.AppendLine("                        discriminatorValue.S,");
        sb.AppendLine($"                        \"{discriminator.PropertyName}\");");
        sb.AppendLine("                }");
        sb.AppendLine("            }");
    }

    private static void GenerateListConversion(StringBuilder sb, PropertyModel property, string varName)
    {
        var propertyName = property.PropertyName;
        var propertyType = property.PropertyType;
        var elementType = GetListElementType(propertyType);

        sb.AppendLine($"                if ({varName}.L != null)");
        sb.AppendLine("                {");
        sb.AppendLine($"                    var list = new List<{elementType}>();");
        sb.AppendLine($"                    foreach (var item in {varName}.L)");
        sb.AppendLine("                    {");

        if (IsStringType(elementType))
        {
            sb.AppendLine("                        if (item.S != null) list.Add(item.S);");
        }
        else if (IsNumericType(elementType))
        {
            sb.AppendLine($"                        if (item.N != null) list.Add({GetNumericConversion(elementType, "item.N")});");
        }
        else if (IsBoolType(elementType))
        {
            sb.AppendLine("                        if (item.BOOL.HasValue) list.Add(item.BOOL.Value);");
        }

        sb.AppendLine("                    }");
        sb.AppendLine($"                    entity.{propertyName} = list;");
        sb.AppendLine("                }");
    }

    private static void GenerateSetConversion(StringBuilder sb, PropertyModel property, string varName)
    {
        var propertyName = property.PropertyName;
        var propertyType = property.PropertyType;
        var elementType = GetSetElementType(propertyType);

        if (IsStringType(elementType))
        {
            sb.AppendLine($"                if ({varName}.SS != null)");
            sb.AppendLine($"                    entity.{propertyName} = new HashSet<{elementType}>({varName}.SS);");
        }
        else if (IsNumericType(elementType))
        {
            sb.AppendLine($"                if ({varName}.NS != null)");
            sb.AppendLine("                {");
            sb.AppendLine($"                    var set = new HashSet<{elementType}>();");
            sb.AppendLine($"                    foreach (var item in {varName}.NS)");
            sb.AppendLine($"                        set.Add({GetNumericConversion(elementType, "item")});");
            sb.AppendLine($"                    entity.{propertyName} = set;");
            sb.AppendLine("                }");
        }
        else if (IsBinaryType(elementType))
        {
            sb.AppendLine($"                if ({varName}.BS != null)");
            sb.AppendLine("                {");
            sb.AppendLine($"                    var set = new HashSet<{elementType}>();");
            sb.AppendLine($"                    foreach (var item in {varName}.BS)");
            sb.AppendLine("                        set.Add(item.ToArray());");
            sb.AppendLine($"                    entity.{propertyName} = set;");
            sb.AppendLine("                }");
        }
    }

    private static void GenerateMapConversion(StringBuilder sb, PropertyModel property, string varName)
    {
        var propertyName = property.PropertyName;
        var propertyType = property.PropertyType;

        // Simple Dictionary<string, string> case
        if (propertyType.Contains("Dictionary<string, string>"))
        {
            sb.AppendLine($"                if ({varName}.M != null)");
            sb.AppendLine("                {");
            sb.AppendLine("                    var dict = new Dictionary<string, string>();");
            sb.AppendLine($"                    foreach (var kvp in {varName}.M)");
            sb.AppendLine("                    {");
            sb.AppendLine("                        if (kvp.Value.S != null)");
            sb.AppendLine("                            dict[kvp.Key] = kvp.Value.S;");
            sb.AppendLine("                    }");
            sb.AppendLine($"                    entity.{propertyName} = dict;");
            sb.AppendLine("                }");
        }
    }

    // Helper methods for type checking
    private static string GetBaseType(string type)
    {
        // Remove nullable suffix
        if (type.EndsWith("?"))
        {
            type = type.TrimEnd('?');
        }

        // Remove generic List/HashSet/Dictionary wrappers
        if (type.StartsWith("List<") || type.StartsWith("System.Collections.Generic.List<"))
        {
            return type;
        }

        return type;
    }

    private static bool IsStringType(string type)
    {
        return type == "string" || type == "String" || type == "System.String";
    }

    private static bool IsNumericType(string type)
    {
        return type is "int" or "Int32" or "System.Int32" or
                        "long" or "Int64" or "System.Int64" or
                        "decimal" or "Decimal" or "System.Decimal" or
                        "double" or "Double" or "System.Double" or
                        "float" or "Single" or "System.Single" or
                        "short" or "Int16" or "System.Int16" or
                        "byte" or "Byte" or "System.Byte";
    }

    private static bool IsBoolType(string type)
    {
        return type == "bool" || type == "Boolean" || type == "System.Boolean";
    }

    private static bool IsBinaryType(string type)
    {
        return type == "byte[]" || type == "Byte[]" || type == "System.Byte[]";
    }

    private static bool IsListType(string type)
    {
        return type.StartsWith("List<") || type.StartsWith("System.Collections.Generic.List<") ||
               type.StartsWith("IList<") || type.StartsWith("System.Collections.Generic.IList<");
    }

    private static bool IsSetType(string type)
    {
        return type.StartsWith("HashSet<") || type.StartsWith("System.Collections.Generic.HashSet<") ||
               type.StartsWith("ISet<") || type.StartsWith("System.Collections.Generic.ISet<");
    }

    private static bool IsDictionaryType(string type)
    {
        return type.StartsWith("Dictionary<") || type.StartsWith("System.Collections.Generic.Dictionary<") ||
               type.StartsWith("IDictionary<") || type.StartsWith("System.Collections.Generic.IDictionary<");
    }

    private static string GetListElementType(string type)
    {
        var start = type.IndexOf('<') + 1;
        var end = type.LastIndexOf('>');
        return type.Substring(start, end - start);
    }

    private static string GetSetElementType(string type)
    {
        var start = type.IndexOf('<') + 1;
        var end = type.LastIndexOf('>');
        return type.Substring(start, end - start);
    }

    private static string GetNumericConversion(string type, string value)
    {
        return type switch
        {
            "int" or "Int32" or "System.Int32" => $"int.Parse({value})",
            "long" or "Int64" or "System.Int64" => $"long.Parse({value})",
            "decimal" or "Decimal" or "System.Decimal" => $"decimal.Parse({value})",
            "double" or "Double" or "System.Double" => $"double.Parse({value})",
            "float" or "Single" or "System.Single" => $"float.Parse({value})",
            "short" or "Int16" or "System.Int16" => $"short.Parse({value})",
            "byte" or "Byte" or "System.Byte" => $"byte.Parse({value})",
            _ => $"Convert.ChangeType({value}, typeof({type}))"
        };
    }
}
