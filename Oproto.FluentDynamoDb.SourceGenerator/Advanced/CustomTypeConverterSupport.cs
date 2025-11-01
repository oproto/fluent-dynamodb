using Oproto.FluentDynamoDb.SourceGenerator.Models;
using System.Text;

namespace Oproto.FluentDynamoDb.SourceGenerator.Advanced;

/// <summary>
/// Provides support for custom type converters in generated entity mapping code.
/// Enables extensible type conversion for complex or custom types.
/// </summary>
internal static class CustomTypeConverterSupport
{
    /// <summary>
    /// Generates custom type converter support code for entities with complex types.
    /// </summary>
    public static void GenerateCustomConverterSupport(StringBuilder sb, EntityModel entity)
    {
        var customTypeProperties = entity.Properties
            .Where(p => RequiresCustomConverter(p.PropertyType))
            .ToArray();

        if (customTypeProperties.Length == 0)
            return;

        sb.AppendLine();
        sb.AppendLine("        #region Custom Type Converter Support");
        sb.AppendLine();

        // Generate converter interface
        GenerateConverterInterface(sb);

        // Generate converter registry
        GenerateConverterRegistry(sb, customTypeProperties);

        // Generate converter methods
        GenerateConverterMethods(sb, customTypeProperties);

        // Generate dictionary conversion helpers
        GenerateDictionaryConversionHelpers(sb);

        sb.AppendLine("        #endregion");
    }

    /// <summary>
    /// Generates the custom type converter interface.
    /// </summary>
    private static void GenerateConverterInterface(StringBuilder sb)
    {
        sb.AppendLine("        /// <summary>");
        sb.AppendLine("        /// Interface for custom type converters that handle complex type conversions.");
        sb.AppendLine("        /// </summary>");
        sb.AppendLine("        public interface ICustomTypeConverter<T>");
        sb.AppendLine("        {");
        sb.AppendLine("            AttributeValue ToAttributeValue(T value);");
        sb.AppendLine("            T FromAttributeValue(AttributeValue attributeValue);");
        sb.AppendLine("        }");
        sb.AppendLine();
    }

    /// <summary>
    /// Generates the converter registry for managing custom converters.
    /// </summary>
    private static void GenerateConverterRegistry(StringBuilder sb, PropertyModel[] customTypeProperties)
    {
        sb.AppendLine("        /// <summary>");
        sb.AppendLine("        /// Registry for custom type converters.");
        sb.AppendLine("        /// </summary>");
        sb.AppendLine("        private static readonly Dictionary<Type, object> _customConverters = new()");
        sb.AppendLine("        {");

        foreach (var property in customTypeProperties)
        {
            var converterType = GetDefaultConverterType(property.PropertyType);
            if (converterType != null)
            {
                sb.AppendLine($"            {{ typeof({GetBaseType(property.PropertyType)}), new {converterType}() }},");
            }
        }

        sb.AppendLine("        };");
        sb.AppendLine();

        // Generate converter registration method
        sb.AppendLine("        /// <summary>");
        sb.AppendLine("        /// Registers a custom converter for a specific type.");
        sb.AppendLine("        /// </summary>");
        sb.AppendLine("        public static void RegisterConverter<T>(ICustomTypeConverter<T> converter)");
        sb.AppendLine("        {");
        sb.AppendLine("            _customConverters[typeof(T)] = converter;");
        sb.AppendLine("        }");
        sb.AppendLine();
    }

    /// <summary>
    /// Generates converter methods for custom types.
    /// </summary>
    private static void GenerateConverterMethods(StringBuilder sb, PropertyModel[] customTypeProperties)
    {
        foreach (var property in customTypeProperties)
        {
            GenerateConverterMethodsForType(sb, property);
        }
    }

    /// <summary>
    /// Generates converter methods for a specific property type.
    /// </summary>
    private static void GenerateConverterMethodsForType(StringBuilder sb, PropertyModel property)
    {
        var baseType = GetBaseType(property.PropertyType);
        var methodSuffix = baseType.Replace(".", "").Replace("<", "").Replace(">", "").Replace(",", "");

        // To AttributeValue converter
        sb.AppendLine($"        /// <summary>");
        sb.AppendLine($"        /// Converts {baseType} to AttributeValue using custom converter.");
        sb.AppendLine($"        /// </summary>");
        sb.AppendLine($"        private static AttributeValue Convert{methodSuffix}ToAttributeValue({baseType} value)");
        sb.AppendLine("        {");
        sb.AppendLine($"            if (_customConverters.TryGetValue(typeof({baseType}), out var converter))");
        sb.AppendLine("            {");
        sb.AppendLine($"                return ((ICustomTypeConverter<{baseType}>)converter).ToAttributeValue(value);");
        sb.AppendLine("            }");
        sb.AppendLine();
        sb.AppendLine($"            // Fallback to default conversion");
        sb.AppendLine($"            return {GetDefaultToAttributeValueConversion(property, "value")};");
        sb.AppendLine("        }");
        sb.AppendLine();

        // From AttributeValue converter
        sb.AppendLine($"        /// <summary>");
        sb.AppendLine($"        /// Converts AttributeValue to {baseType} using custom converter.");
        sb.AppendLine($"        /// </summary>");
        sb.AppendLine($"        private static {baseType} Convert{methodSuffix}FromAttributeValue(AttributeValue attributeValue)");
        sb.AppendLine("        {");
        sb.AppendLine($"            if (_customConverters.TryGetValue(typeof({baseType}), out var converter))");
        sb.AppendLine("            {");
        sb.AppendLine($"                return ((ICustomTypeConverter<{baseType}>)converter).FromAttributeValue(attributeValue);");
        sb.AppendLine("            }");
        sb.AppendLine();
        sb.AppendLine($"            // Fallback to default conversion");
        sb.AppendLine($"            return {GetDefaultFromAttributeValueConversion(property, "attributeValue")};");
        sb.AppendLine("        }");
        sb.AppendLine();
    }

    /// <summary>
    /// Determines if a property type requires a custom converter.
    /// </summary>
    private static bool RequiresCustomConverter(string propertyType)
    {
        var baseType = GetBaseType(propertyType);

        return baseType switch
        {
            "Uri" or "System.Uri" => true,
            "TimeSpan" or "System.TimeSpan" => true,
            "Version" or "System.Version" => true,
            _ when baseType.StartsWith("Dictionary<") => true,
            _ when baseType.StartsWith("HashSet<") => true,
            _ when baseType.StartsWith("SortedSet<") => true,

            _ => false
        };
    }

    /// <summary>
    /// Gets the default converter type for a property type.
    /// </summary>
    private static string? GetDefaultConverterType(string propertyType)
    {
        var baseType = GetBaseType(propertyType);

        return baseType switch
        {
            "Uri" or "System.Uri" => "UriConverter",
            "TimeSpan" or "System.TimeSpan" => "TimeSpanConverter",
            "Version" or "System.Version" => "VersionConverter",
            _ when baseType.StartsWith("Dictionary<") => "DictionaryConverter",
            _ => null
        };
    }

    /// <summary>
    /// Gets the default to AttributeValue conversion for fallback scenarios.
    /// </summary>
    private static string GetDefaultToAttributeValueConversion(PropertyModel property, string valueExpression)
    {
        var baseType = GetBaseType(property.PropertyType);

        return baseType switch
        {
            "Uri" or "System.Uri" => $"new AttributeValue {{ S = {valueExpression}?.ToString() ?? string.Empty }}",
            "TimeSpan" or "System.TimeSpan" => $"new AttributeValue {{ S = {valueExpression}.ToString() }}",
            "Version" or "System.Version" => $"new AttributeValue {{ S = {valueExpression}?.ToString() ?? string.Empty }}",
            _ when baseType.StartsWith("Dictionary<") => $"new AttributeValue {{ M = ConvertDictionaryToAttributeValueMap({valueExpression}) }}",
            _ => $"new AttributeValue {{ S = {valueExpression}?.ToString() ?? string.Empty }}"
        };
    }

    /// <summary>
    /// Gets the default from AttributeValue conversion for fallback scenarios.
    /// </summary>
    private static string GetDefaultFromAttributeValueConversion(PropertyModel property, string valueExpression)
    {
        var baseType = GetBaseType(property.PropertyType);

        return baseType switch
        {
            "Uri" or "System.Uri" => $"new Uri({valueExpression}.S)",
            "TimeSpan" or "System.TimeSpan" => $"TimeSpan.Parse({valueExpression}.S)",
            "Version" or "System.Version" => $"new Version({valueExpression}.S)",
            _ when baseType.StartsWith("Dictionary<") => $"ConvertAttributeValueMapToDictionary<{baseType}>({valueExpression}.M)",
            _ => $"{valueExpression}.S"
        };
    }

    /// <summary>
    /// Generates helper methods for converting dictionaries to/from native DynamoDB Map types.
    /// </summary>
    private static void GenerateDictionaryConversionHelpers(StringBuilder sb)
    {
        sb.AppendLine();
        sb.AppendLine("        /// <summary>");
        sb.AppendLine("        /// Converts a Dictionary to DynamoDB Map (M) type using native AttributeValue types.");
        sb.AppendLine("        /// </summary>");
        sb.AppendLine("        private static Dictionary<string, AttributeValue> ConvertDictionaryToAttributeValueMap<T>(T dictionary)");
        sb.AppendLine("        {");
        sb.AppendLine("            if (dictionary is IDictionary<string, object> dict)");
        sb.AppendLine("            {");
        sb.AppendLine("                var result = new Dictionary<string, AttributeValue>();");
        sb.AppendLine("                foreach (var kvp in dict)");
        sb.AppendLine("                {");
        sb.AppendLine("                    result[kvp.Key] = ConvertObjectToAttributeValue(kvp.Value);");
        sb.AppendLine("                }");
        sb.AppendLine("                return result;");
        sb.AppendLine("            }");
        sb.AppendLine("            return new Dictionary<string, AttributeValue>();");
        sb.AppendLine("        }");
        sb.AppendLine();

        sb.AppendLine("        /// <summary>");
        sb.AppendLine("        /// Converts a DynamoDB Map (M) type to Dictionary using native AttributeValue types.");
        sb.AppendLine("        /// </summary>");
        sb.AppendLine("        private static T ConvertAttributeValueMapToDictionary<T>(Dictionary<string, AttributeValue> map)");
        sb.AppendLine("        {");
        sb.AppendLine("            if (typeof(T).IsAssignableFrom(typeof(Dictionary<string, object>)))");
        sb.AppendLine("            {");
        sb.AppendLine("                var result = new Dictionary<string, object>();");
        sb.AppendLine("                foreach (var kvp in map)");
        sb.AppendLine("                {");
        sb.AppendLine("                    result[kvp.Key] = ConvertAttributeValueToObject(kvp.Value);");
        sb.AppendLine("                }");
        sb.AppendLine("                return (T)(object)result;");
        sb.AppendLine("            }");
        sb.AppendLine("            return Activator.CreateInstance<T>();");
        sb.AppendLine("        }");
        sb.AppendLine();

        sb.AppendLine("        /// <summary>");
        sb.AppendLine("        /// Converts an object to AttributeValue using native DynamoDB types.");
        sb.AppendLine("        /// </summary>");
        sb.AppendLine("        private static AttributeValue ConvertObjectToAttributeValue(object value)");
        sb.AppendLine("        {");
        sb.AppendLine("            return value switch");
        sb.AppendLine("            {");
        sb.AppendLine("                string s => new AttributeValue { S = s },");
        sb.AppendLine("                int i => new AttributeValue { N = i.ToString() },");
        sb.AppendLine("                long l => new AttributeValue { N = l.ToString() },");
        sb.AppendLine("                double d => new AttributeValue { N = d.ToString() },");
        sb.AppendLine("                bool b => new AttributeValue { BOOL = b },");
        sb.AppendLine("                byte[] bytes => new AttributeValue { B = new MemoryStream(bytes) },");
        sb.AppendLine("                null => new AttributeValue { NULL = true },");
        sb.AppendLine("                _ => new AttributeValue { S = value.ToString() }");
        sb.AppendLine("            };");
        sb.AppendLine("        }");
        sb.AppendLine();

        sb.AppendLine("        /// <summary>");
        sb.AppendLine("        /// Converts an AttributeValue to object using native DynamoDB types.");
        sb.AppendLine("        /// </summary>");
        sb.AppendLine("        private static object ConvertAttributeValueToObject(AttributeValue attributeValue)");
        sb.AppendLine("        {");
        sb.AppendLine("            if (attributeValue.S != null) return attributeValue.S;");
        sb.AppendLine("            if (attributeValue.N != null) return double.Parse(attributeValue.N);");
        sb.AppendLine("            if (attributeValue.BOOL.HasValue) return attributeValue.BOOL.Value;");
        sb.AppendLine("            if (attributeValue.B != null) return attributeValue.B.ToArray();");
        sb.AppendLine("            if (attributeValue.SS?.Count > 0) return attributeValue.SS;");
        sb.AppendLine("            if (attributeValue.NS?.Count > 0) return attributeValue.NS;");
        sb.AppendLine("            if (attributeValue.L?.Count > 0) return attributeValue.L.Select(ConvertAttributeValueToObject).ToList();");
        sb.AppendLine("            if (attributeValue.M?.Count > 0) return attributeValue.M.ToDictionary(kvp => kvp.Key, kvp => ConvertAttributeValueToObject(kvp.Value));");
        sb.AppendLine("            return null;");
        sb.AppendLine("        }");
    }

    /// <summary>
    /// Gets the base type without nullable annotation.
    /// </summary>
    private static string GetBaseType(string typeName)
    {
        return typeName.TrimEnd('?');
    }
}