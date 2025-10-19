using Oproto.FluentDynamoDb.SourceGenerator.Models;
using System.Text;

namespace Oproto.FluentDynamoDb.SourceGenerator.Performance;

/// <summary>
/// Optimized code generator that produces high-performance mapping code with minimal allocations.
/// Focuses on reducing string concatenations, object allocations, and improving runtime performance.
/// </summary>
public static class OptimizedCodeGenerator
{
    private static readonly string[] CommonUsings = new[]
    {
        "using System;",
        "using System.Collections.Generic;",
        "using System.Linq;",
        "using System.Diagnostics.CodeAnalysis;",
        "using System.Runtime.CompilerServices;",
        "using Amazon.DynamoDBv2.Model;",
        "using Oproto.FluentDynamoDb.Storage;",
        "using Oproto.FluentDynamoDb.Attributes;"
    };

    /// <summary>
    /// Generates optimized ToDynamoDb method with minimal allocations and maximum performance.
    /// </summary>
    public static void GenerateOptimizedToDynamoDbMethod(StringBuilder sb, EntityModel entity)
    {
        sb.AppendLine();
        sb.AppendLine("        /// <summary>");
        sb.AppendLine("        /// High-performance conversion from entity to DynamoDB AttributeValue dictionary.");
        sb.AppendLine("        /// Optimized for minimal allocations and maximum throughput.");
        sb.AppendLine("        /// </summary>");
        sb.AppendLine("        [MethodImpl(MethodImplOptions.AggressiveInlining)]");
        sb.AppendLine($"        public static Dictionary<string, AttributeValue> ToDynamoDb<TSelf>(TSelf entity) where TSelf : IDynamoDbEntity");
        sb.AppendLine("        {");
        sb.AppendLine($"            if (entity is not {entity.ClassName} typedEntity)");
        sb.AppendLine($"                throw new ArgumentException($\"Expected {entity.ClassName}, got {{entity.GetType().Name}}\", nameof(entity));");
        sb.AppendLine();

        // Pre-compute capacity to avoid dictionary resizing
        var attributeCount = entity.Properties.Count(p => p.HasAttributeMapping);
        sb.AppendLine($"            // Pre-allocate dictionary with exact capacity to avoid resizing");
        sb.AppendLine($"            var item = new Dictionary<string, AttributeValue>({attributeCount});");
        sb.AppendLine();

        // Generate computed key logic with optimized string operations
        var computedProperties = entity.Properties.Where(p => p.IsComputed).ToArray();
        if (computedProperties.Length > 0)
        {
            sb.AppendLine("            // Compute composite keys with optimized string operations");
            foreach (var computedProperty in computedProperties)
            {
                GenerateOptimizedComputedKeyLogic(sb, computedProperty);
            }
            sb.AppendLine();
        }

        // Generate optimized property mappings
        foreach (var property in entity.Properties.Where(p => p.HasAttributeMapping))
        {
            GenerateOptimizedPropertyToAttributeValue(sb, property);
        }

        sb.AppendLine();
        sb.AppendLine("            return item;");
        sb.AppendLine("        }");
    }

    /// <summary>
    /// Generates optimized FromDynamoDb method with efficient type conversions and minimal boxing.
    /// </summary>
    public static void GenerateOptimizedFromDynamoDbMethod(StringBuilder sb, EntityModel entity)
    {
        sb.AppendLine();
        sb.AppendLine("        /// <summary>");
        sb.AppendLine("        /// High-performance conversion from DynamoDB item to entity with minimal boxing and allocations.");
        sb.AppendLine("        /// </summary>");
        sb.AppendLine("        [MethodImpl(MethodImplOptions.AggressiveInlining)]");
        sb.AppendLine($"        public static TSelf FromDynamoDb<TSelf>(Dictionary<string, AttributeValue> item) where TSelf : IDynamoDbEntity");
        sb.AppendLine("        {");
        sb.AppendLine($"            if (typeof(TSelf) != typeof({entity.ClassName}))");
        sb.AppendLine($"                throw new ArgumentException($\"Expected {entity.ClassName}, got {{typeof(TSelf).Name}}\");");
        sb.AppendLine();

        // Use object initializer for better performance
        sb.AppendLine($"            var entity = new {entity.ClassName}();");
        sb.AppendLine();

        // Generate optimized property mappings with minimal string operations
        foreach (var property in entity.Properties.Where(p => p.HasAttributeMapping))
        {
            GenerateOptimizedPropertyFromAttributeValue(sb, property, entity);
        }

        // Generate optimized extracted key logic
        var extractedProperties = entity.Properties.Where(p => p.IsExtracted).ToArray();
        if (extractedProperties.Length > 0)
        {
            sb.AppendLine();
            sb.AppendLine("            // Extract component properties with optimized string operations");
            foreach (var extractedProperty in extractedProperties)
            {
                GenerateOptimizedExtractedKeyLogic(sb, extractedProperty);
            }
        }

        sb.AppendLine();
        sb.AppendLine("            return (TSelf)(object)entity;");
        sb.AppendLine("        }");
    }

    /// <summary>
    /// Generates optimized computed key logic using StringBuilder for complex concatenations.
    /// </summary>
    private static void GenerateOptimizedComputedKeyLogic(StringBuilder sb, PropertyModel computedProperty)
    {
        var computedKey = computedProperty.ComputedKey!;
        var propertyName = computedProperty.PropertyName;

        if (computedKey.HasCustomFormat)
        {
            // Use string interpolation for better performance than string.Format
            var formatArgs = string.Join(", ", computedKey.SourceProperties.Select(sp => $"typedEntity.{sp}"));
            sb.AppendLine($"            typedEntity.{propertyName} = $\"{computedKey.Format.Replace("{", "{{").Replace("}", "}}")}\";");
        }
        else if (computedKey.SourceProperties.Length <= 2)
        {
            // Direct concatenation for simple cases (faster than StringBuilder)
            var sourceValues = string.Join($" + \"{computedKey.Separator}\" + ", 
                computedKey.SourceProperties.Select(sp => $"typedEntity.{sp}"));
            sb.AppendLine($"            typedEntity.{propertyName} = {sourceValues};");
        }
        else
        {
            // Use StringBuilder for efficient multi-part key construction
            sb.AppendLine($"            // Use StringBuilder for efficient multi-part key construction");
            sb.AppendLine($"            var {propertyName.ToLowerInvariant()}Builder = new StringBuilder({EstimateKeyLength(computedKey)});");
            
            for (int i = 0; i < computedKey.SourceProperties.Length; i++)
            {
                var sourceProperty = computedKey.SourceProperties[i];
                if (i > 0)
                {
                    sb.AppendLine($"            {propertyName.ToLowerInvariant()}Builder.Append(\"{computedKey.Separator}\");");
                }
                sb.AppendLine($"            {propertyName.ToLowerInvariant()}Builder.Append(typedEntity.{sourceProperty});");
            }
            
            sb.AppendLine($"            typedEntity.{propertyName} = {propertyName.ToLowerInvariant()}Builder.ToString();");
        }
    }

    /// <summary>
    /// Generates optimized property to AttributeValue conversion with type-specific optimizations.
    /// </summary>
    private static void GenerateOptimizedPropertyToAttributeValue(StringBuilder sb, PropertyModel property)
    {
        var attributeName = property.AttributeName;
        var propertyName = property.PropertyName;
        
        if (property.IsCollection)
        {
            GenerateOptimizedCollectionPropertyToAttributeValue(sb, property);
            return;
        }
        
        // Use direct assignment for non-nullable properties to avoid unnecessary checks
        if (property.IsNullable)
        {
            sb.AppendLine($"            if (typedEntity.{propertyName} != null)");
            sb.AppendLine("            {");
            sb.AppendLine($"                item[\"{attributeName}\"] = {GetOptimizedToAttributeValueExpression(property, $"typedEntity.{propertyName}")};");
            sb.AppendLine("            }");
        }
        else
        {
            sb.AppendLine($"            item[\"{attributeName}\"] = {GetOptimizedToAttributeValueExpression(property, $"typedEntity.{propertyName}")};");
        }
    }

    /// <summary>
    /// Generates optimized collection property conversion using span operations where possible.
    /// </summary>
    private static void GenerateOptimizedCollectionPropertyToAttributeValue(StringBuilder sb, PropertyModel property)
    {
        var attributeName = property.AttributeName;
        var propertyName = property.PropertyName;
        var collectionElementType = GetCollectionElementType(property.PropertyType);
        var baseElementType = GetBaseType(collectionElementType);
        
        sb.AppendLine($"            // Optimized collection conversion for {propertyName}");
        sb.AppendLine($"            if (typedEntity.{propertyName} != null && typedEntity.{propertyName}.Count > 0)");
        sb.AppendLine("            {");
        
        if (baseElementType == "string")
        {
            // Optimized string set conversion
            sb.AppendLine($"                item[\"{attributeName}\"] = new AttributeValue");
            sb.AppendLine("                {");
            sb.AppendLine($"                    SS = typedEntity.{propertyName} is List<string> list ? list : new List<string>(typedEntity.{propertyName})");
            sb.AppendLine("                };");
        }
        else if (IsNumericType(baseElementType))
        {
            // Optimized numeric set conversion with pre-allocated capacity
            sb.AppendLine($"                var numericStrings = new List<string>(typedEntity.{propertyName}.Count);");
            sb.AppendLine($"                foreach (var item in typedEntity.{propertyName})");
            sb.AppendLine("                {");
            sb.AppendLine($"                    numericStrings.Add({GetOptimizedNumericToString(baseElementType, "item")});");
            sb.AppendLine("                }");
            sb.AppendLine($"                item[\"{attributeName}\"] = new AttributeValue {{ NS = numericStrings }};");
        }
        else
        {
            // Optimized list conversion with pre-allocated capacity
            sb.AppendLine($"                var attributeList = new List<AttributeValue>(typedEntity.{propertyName}.Count);");
            sb.AppendLine($"                foreach (var item in typedEntity.{propertyName})");
            sb.AppendLine("                {");
            sb.AppendLine($"                    attributeList.Add({GetOptimizedToAttributeValueExpressionForCollectionElement(collectionElementType, "item")});");
            sb.AppendLine("                }");
            sb.AppendLine($"                item[\"{attributeName}\"] = new AttributeValue {{ L = attributeList }};");
        }
        
        sb.AppendLine("            }");
    }

    /// <summary>
    /// Generates optimized property from AttributeValue conversion with minimal boxing.
    /// </summary>
    private static void GenerateOptimizedPropertyFromAttributeValue(StringBuilder sb, PropertyModel property, EntityModel entity)
    {
        var attributeName = property.AttributeName;
        var propertyName = property.PropertyName;
        
        if (property.IsCollection)
        {
            GenerateOptimizedCollectionPropertyFromAttributeValue(sb, property, entity);
            return;
        }
        
        // Use TryGetValue pattern for better performance with error handling
        sb.AppendLine($"            if (item.TryGetValue(\"{attributeName}\", out var {propertyName.ToLowerInvariant()}Value))");
        sb.AppendLine("            {");
        sb.AppendLine("                try");
        sb.AppendLine("                {");
        sb.AppendLine($"                    entity.{propertyName} = {GetOptimizedFromAttributeValueExpression(property, $"{propertyName.ToLowerInvariant()}Value")};");
        sb.AppendLine("                }");
        sb.AppendLine("                catch (Exception ex)");
        sb.AppendLine("                {");
        sb.AppendLine($"                    throw DynamoDbMappingException.PropertyConversionFailed(");
        sb.AppendLine($"                        typeof({entity.ClassName}),");
        sb.AppendLine($"                        \"{propertyName}\",");
        sb.AppendLine($"                        {propertyName.ToLowerInvariant()}Value,");
        sb.AppendLine($"                        typeof({GetTypeForMetadata(property.PropertyType)}),");
        sb.AppendLine("                        ex);");
        sb.AppendLine("                }");
        sb.AppendLine("            }");
    }

    /// <summary>
    /// Generates optimized collection property from AttributeValue conversion.
    /// </summary>
    private static void GenerateOptimizedCollectionPropertyFromAttributeValue(StringBuilder sb, PropertyModel property, EntityModel entity)
    {
        var attributeName = property.AttributeName;
        var propertyName = property.PropertyName;
        var collectionElementType = GetCollectionElementType(property.PropertyType);
        var baseElementType = GetBaseType(collectionElementType);
        
        sb.AppendLine($"            // Optimized collection conversion from DynamoDB for {propertyName}");
        sb.AppendLine($"            if (item.TryGetValue(\"{attributeName}\", out var {propertyName.ToLowerInvariant()}Value))");
        sb.AppendLine("            {");
        
        if (baseElementType == "string")
        {
            // Optimized string set conversion
            sb.AppendLine($"                entity.{propertyName} = {propertyName.ToLowerInvariant()}Value.SS?.Count > 0 ? ");
            sb.AppendLine($"                    new List<string>({propertyName.ToLowerInvariant()}Value.SS) : ");
            sb.AppendLine($"                    new List<string>();");
        }
        else if (IsNumericType(baseElementType))
        {
            // Optimized numeric set conversion with pre-allocated capacity
            sb.AppendLine($"                if ({propertyName.ToLowerInvariant()}Value.NS?.Count > 0)");
            sb.AppendLine("                {");
            sb.AppendLine($"                    var convertedNumbers = new List<{baseElementType}>({propertyName.ToLowerInvariant()}Value.NS.Count);");
            sb.AppendLine($"                    foreach (var numStr in {propertyName.ToLowerInvariant()}Value.NS)");
            sb.AppendLine("                    {");
            sb.AppendLine($"                        convertedNumbers.Add({GetOptimizedNumericParse(baseElementType, "numStr")});");
            sb.AppendLine("                    }");
            sb.AppendLine($"                    entity.{propertyName} = new List<{baseElementType}>(convertedNumbers);");
            sb.AppendLine("                }");
            sb.AppendLine("                else");
            sb.AppendLine("                {");
            sb.AppendLine($"                    entity.{propertyName} = new List<{baseElementType}>();");
            sb.AppendLine("                }");
        }
        else
        {
            // Optimized list conversion with pre-allocated capacity
            sb.AppendLine($"                if ({propertyName.ToLowerInvariant()}Value.L?.Count > 0)");
            sb.AppendLine("                {");
            sb.AppendLine($"                    var convertedItems = new List<{collectionElementType}>({propertyName.ToLowerInvariant()}Value.L.Count);");
            sb.AppendLine($"                    foreach (var listItem in {propertyName.ToLowerInvariant()}Value.L)");
            sb.AppendLine("                    {");
            sb.AppendLine($"                        convertedItems.Add({GetOptimizedFromAttributeValueExpressionForCollectionElement(collectionElementType, "listItem")});");
            sb.AppendLine("                    }");
            sb.AppendLine($"                    entity.{propertyName} = new List<{collectionElementType}>(convertedItems);");
            sb.AppendLine("                }");
            sb.AppendLine("                else");
            sb.AppendLine("                {");
            sb.AppendLine($"                    entity.{propertyName} = new List<{collectionElementType}>();");
            sb.AppendLine("                }");
        }
        
        sb.AppendLine("            }");
        sb.AppendLine("            else");
        sb.AppendLine("            {");
        sb.AppendLine($"                entity.{propertyName} = new List<{collectionElementType}>();");
        sb.AppendLine("            }");
    }

    /// <summary>
    /// Generates optimized extracted key logic using ReadOnlySpan for string operations.
    /// </summary>
    private static void GenerateOptimizedExtractedKeyLogic(StringBuilder sb, PropertyModel extractedProperty)
    {
        var extractedKey = extractedProperty.ExtractedKey!;
        var propertyName = extractedProperty.PropertyName;
        var sourceProperty = extractedKey.SourceProperty;
        var index = extractedKey.Index;
        var separator = extractedKey.Separator;

        sb.AppendLine($"            // Optimized key extraction for {propertyName}");
        sb.AppendLine($"            if (!string.IsNullOrEmpty(entity.{sourceProperty}))");
        sb.AppendLine("            {");
        
        if (separator.Length == 1)
        {
            // Use ReadOnlySpan for single character separators (most common case)
            sb.AppendLine($"                var keySpan = entity.{sourceProperty}.AsSpan();");
            sb.AppendLine($"                var separatorIndex = 0;");
            sb.AppendLine($"                var currentIndex = 0;");
            sb.AppendLine($"                while (currentIndex < keySpan.Length && separatorIndex < {index})");
            sb.AppendLine("                {");
            sb.AppendLine($"                    if (keySpan[currentIndex] == separator[0])");
            sb.AppendLine("                    {");
            sb.AppendLine("                        separatorIndex++;");
            sb.AppendLine("                    }");
            sb.AppendLine("                    currentIndex++;");
            sb.AppendLine("                }");
            sb.AppendLine($"                if (separatorIndex == {index} && currentIndex < keySpan.Length)");
            sb.AppendLine("                {");
            sb.AppendLine($"                    var endIndex = keySpan.Slice(currentIndex).IndexOf(separator[0]);");
            sb.AppendLine("                    var componentSpan = endIndex >= 0 ? keySpan.Slice(currentIndex, endIndex) : keySpan.Slice(currentIndex);");
            sb.AppendLine($"                    entity.{propertyName} = {GetOptimizedSpanConversion(extractedProperty.PropertyType, "componentSpan")};");
            sb.AppendLine("                }");
        }
        else
        {
            // Fallback to string.Split for multi-character separators
            sb.AppendLine($"                var parts = entity.{sourceProperty}.Split(new[] {{ \"{separator}\" }}, StringSplitOptions.None);");
            sb.AppendLine($"                if (parts.Length > {index})");
            sb.AppendLine("                {");
            sb.AppendLine($"                    entity.{propertyName} = {GetOptimizedStringConversion(extractedProperty.PropertyType, $"parts[{index}]")};");
            sb.AppendLine("                }");
        }
        
        sb.AppendLine("            }");
    }

    /// <summary>
    /// Gets optimized AttributeValue creation expression with minimal allocations.
    /// </summary>
    private static string GetOptimizedToAttributeValueExpression(PropertyModel property, string valueExpression)
    {
        var baseType = GetBaseType(property.PropertyType);
        
        return baseType switch
        {
            "string" => $"new AttributeValue {{ S = {valueExpression} }}",
            "int" or "System.Int32" => $"new AttributeValue {{ N = {valueExpression}.ToString() }}",
            "long" or "System.Int64" => $"new AttributeValue {{ N = {valueExpression}.ToString() }}",
            "double" or "System.Double" => $"new AttributeValue {{ N = {valueExpression}.ToString(\"G17\") }}", // Preserve precision
            "float" or "System.Single" => $"new AttributeValue {{ N = {valueExpression}.ToString(\"G9\") }}", // Preserve precision
            "decimal" or "System.Decimal" => $"new AttributeValue {{ N = {valueExpression}.ToString() }}",
            "bool" or "System.Boolean" => $"new AttributeValue {{ BOOL = {valueExpression} }}",
            "DateTime" or "System.DateTime" => $"new AttributeValue {{ S = {valueExpression}.ToString(\"O\") }}", // ISO 8601
            "DateTimeOffset" or "System.DateTimeOffset" => $"new AttributeValue {{ S = {valueExpression}.ToString(\"O\") }}",
            "Guid" or "System.Guid" => $"new AttributeValue {{ S = {valueExpression}.ToString(\"D\") }}", // Standard format
            "Ulid" => $"new AttributeValue {{ S = {valueExpression}.ToString() }}",
            "byte[]" or "System.Byte[]" => $"new AttributeValue {{ B = new MemoryStream({valueExpression}) }}",
            _ when IsEnumType(property.PropertyType) => $"new AttributeValue {{ S = {valueExpression}.ToString() }}",
            _ => $"new AttributeValue {{ S = {valueExpression}?.ToString() ?? \"\" }}"
        };
    }

    /// <summary>
    /// Gets optimized AttributeValue to property conversion with minimal boxing.
    /// </summary>
    private static string GetOptimizedFromAttributeValueExpression(PropertyModel property, string valueExpression)
    {
        var baseType = GetBaseType(property.PropertyType);
        
        return baseType switch
        {
            "string" => $"{valueExpression}.S",
            "int" or "System.Int32" => $"int.Parse({valueExpression}.N)",
            "long" or "System.Int64" => $"long.Parse({valueExpression}.N)",
            "double" or "System.Double" => $"double.Parse({valueExpression}.N)",
            "float" or "System.Single" => $"float.Parse({valueExpression}.N)",
            "decimal" or "System.Decimal" => $"decimal.Parse({valueExpression}.N)",
            "bool" or "System.Boolean" => $"{valueExpression}.BOOL",
            "DateTime" or "System.DateTime" => $"DateTime.Parse({valueExpression}.S)",
            "DateTimeOffset" or "System.DateTimeOffset" => $"DateTimeOffset.Parse({valueExpression}.S)",
            "Guid" or "System.Guid" => $"Guid.Parse({valueExpression}.S)",
            "Ulid" => $"Ulid.Parse({valueExpression}.S)",
            "byte[]" or "System.Byte[]" => $"{valueExpression}.B.ToArray()",
            _ when IsEnumType(property.PropertyType) => $"Enum.Parse<{baseType}>({valueExpression}.S)",
            _ => $"{valueExpression}.S"
        };
    }

    private static string GetOptimizedToAttributeValueExpressionForCollectionElement(string elementType, string valueExpression)
    {
        var baseType = GetBaseType(elementType);
        
        return baseType switch
        {
            "string" => $"new AttributeValue {{ S = {valueExpression} }}",
            "int" or "System.Int32" => $"new AttributeValue {{ N = {valueExpression}.ToString() }}",
            "long" or "System.Int64" => $"new AttributeValue {{ N = {valueExpression}.ToString() }}",
            "double" or "System.Double" => $"new AttributeValue {{ N = {valueExpression}.ToString(\"G17\") }}",
            "float" or "System.Single" => $"new AttributeValue {{ N = {valueExpression}.ToString(\"G9\") }}",
            "decimal" or "System.Decimal" => $"new AttributeValue {{ N = {valueExpression}.ToString() }}",
            "bool" or "System.Boolean" => $"new AttributeValue {{ BOOL = {valueExpression} }}",
            "DateTime" or "System.DateTime" => $"new AttributeValue {{ S = {valueExpression}.ToString(\"O\") }}",
            "DateTimeOffset" or "System.DateTimeOffset" => $"new AttributeValue {{ S = {valueExpression}.ToString(\"O\") }}",
            "Guid" or "System.Guid" => $"new AttributeValue {{ S = {valueExpression}.ToString(\"D\") }}",
            "Ulid" => $"new AttributeValue {{ S = {valueExpression}.ToString() }}",
            _ when IsEnumType(elementType) => $"new AttributeValue {{ S = {valueExpression}.ToString() }}",
            _ => $"new AttributeValue {{ S = {valueExpression}?.ToString() ?? \"\" }}"
        };
    }

    private static string GetOptimizedFromAttributeValueExpressionForCollectionElement(string elementType, string valueExpression)
    {
        var baseType = GetBaseType(elementType);
        
        return baseType switch
        {
            "string" => $"{valueExpression}.S",
            "int" or "System.Int32" => $"int.Parse({valueExpression}.N)",
            "long" or "System.Int64" => $"long.Parse({valueExpression}.N)",
            "double" or "System.Double" => $"double.Parse({valueExpression}.N)",
            "float" or "System.Single" => $"float.Parse({valueExpression}.N)",
            "decimal" or "System.Decimal" => $"decimal.Parse({valueExpression}.N)",
            "bool" or "System.Boolean" => $"{valueExpression}.BOOL",
            "DateTime" or "System.DateTime" => $"DateTime.Parse({valueExpression}.S)",
            "DateTimeOffset" or "System.DateTimeOffset" => $"DateTimeOffset.Parse({valueExpression}.S)",
            "Guid" or "System.Guid" => $"Guid.Parse({valueExpression}.S)",
            "Ulid" => $"Ulid.Parse({valueExpression}.S)",
            _ when IsEnumType(elementType) => $"Enum.Parse<{baseType}>({valueExpression}.S)",
            _ => $"{valueExpression}.S"
        };
    }

    private static string GetOptimizedNumericToString(string numericType, string valueExpression)
    {
        return numericType switch
        {
            "double" or "System.Double" => $"{valueExpression}.ToString(\"G17\")",
            "float" or "System.Single" => $"{valueExpression}.ToString(\"G9\")",
            _ => $"{valueExpression}.ToString()"
        };
    }

    private static string GetOptimizedNumericParse(string numericType, string valueExpression)
    {
        return numericType switch
        {
            "int" or "System.Int32" => $"int.Parse({valueExpression})",
            "long" or "System.Int64" => $"long.Parse({valueExpression})",
            "double" or "System.Double" => $"double.Parse({valueExpression})",
            "float" or "System.Single" => $"float.Parse({valueExpression})",
            "decimal" or "System.Decimal" => $"decimal.Parse({valueExpression})",
            "byte" or "System.Byte" => $"byte.Parse({valueExpression})",
            "short" or "System.Int16" => $"short.Parse({valueExpression})",
            "uint" or "System.UInt32" => $"uint.Parse({valueExpression})",
            "ulong" or "System.UInt64" => $"ulong.Parse({valueExpression})",
            "ushort" or "System.UInt16" => $"ushort.Parse({valueExpression})",
            _ => $"{valueExpression}"
        };
    }

    private static string GetOptimizedSpanConversion(string propertyType, string spanExpression)
    {
        var baseType = GetBaseType(propertyType);
        
        return baseType switch
        {
            "string" => $"{spanExpression}.ToString()",
            "int" or "System.Int32" => $"int.Parse({spanExpression})",
            "long" or "System.Int64" => $"long.Parse({spanExpression})",
            "double" or "System.Double" => $"double.Parse({spanExpression})",
            "float" or "System.Single" => $"float.Parse({spanExpression})",
            "decimal" or "System.Decimal" => $"decimal.Parse({spanExpression})",
            "Guid" or "System.Guid" => $"Guid.Parse({spanExpression})",
            "Ulid" => $"Ulid.Parse({spanExpression})",
            _ => $"{spanExpression}.ToString()"
        };
    }

    private static string GetOptimizedStringConversion(string propertyType, string stringExpression)
    {
        var baseType = GetBaseType(propertyType);
        
        return baseType switch
        {
            "string" => stringExpression,
            "int" or "System.Int32" => $"int.Parse({stringExpression})",
            "long" or "System.Int64" => $"long.Parse({stringExpression})",
            "double" or "System.Double" => $"double.Parse({stringExpression})",
            "float" or "System.Single" => $"float.Parse({stringExpression})",
            "decimal" or "System.Decimal" => $"decimal.Parse({stringExpression})",
            "Guid" or "System.Guid" => $"Guid.Parse({stringExpression})",
            "Ulid" => $"Ulid.Parse({stringExpression})",
            _ => stringExpression
        };
    }

    private static int EstimateKeyLength(ComputedKeyModel computedKey)
    {
        // Estimate key length for StringBuilder capacity
        var baseLength = computedKey.SourceProperties.Length * 20; // Average property length
        var separatorLength = (computedKey.SourceProperties.Length - 1) * computedKey.Separator.Length;
        return baseLength + separatorLength + 50; // Add buffer
    }

    private static string GetBaseType(string typeName)
    {
        return typeName.TrimEnd('?');
    }

    private static string GetCollectionElementType(string collectionType)
    {
        if (collectionType.StartsWith("List<") && collectionType.EndsWith(">"))
            return collectionType.Substring(5, collectionType.Length - 6);
        if (collectionType.StartsWith("IList<") && collectionType.EndsWith(">"))
            return collectionType.Substring(6, collectionType.Length - 7);
        if (collectionType.StartsWith("ICollection<") && collectionType.EndsWith(">"))
            return collectionType.Substring(12, collectionType.Length - 13);
        if (collectionType.StartsWith("IEnumerable<") && collectionType.EndsWith(">"))
            return collectionType.Substring(12, collectionType.Length - 13);
        return "object";
    }

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

    private static bool IsEnumType(string propertyType)
    {
        return propertyType.Contains("Status") || 
               propertyType.Contains("Type") || 
               propertyType.Contains("Kind") ||
               propertyType.Contains("State");
    }

    private static string GetTypeForMetadata(string typeName)
    {
        // For metadata, we need the actual type without nullable annotations
        var baseType = GetBaseType(typeName);
        
        // Convert common type aliases to full type names for typeof()
        return baseType switch
        {
            "string" => "string",
            "int" => "int",
            "long" => "long", 
            "double" => "double",
            "float" => "float",
            "decimal" => "decimal",
            "bool" => "bool",
            "byte[]" => "byte[]",
            _ => baseType
        };
    }
}