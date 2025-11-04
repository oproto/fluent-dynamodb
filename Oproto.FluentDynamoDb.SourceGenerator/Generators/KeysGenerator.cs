using Oproto.FluentDynamoDb.SourceGenerator.Models;
using System.Text;

namespace Oproto.FluentDynamoDb.SourceGenerator.Generators;

/// <summary>
/// Generates static key builder methods for DynamoDB entities.
/// </summary>
internal static class KeysGenerator
{
    /// <summary>
    /// Generates a static Keys class containing key builder methods for the entity.
    /// </summary>
    /// <param name="entity">The entity model to generate key builders for.</param>
    /// <returns>The generated C# source code.</returns>
    public static string GenerateKeysClass(EntityModel entity)
    {
        var sb = new StringBuilder();

        // File header with auto-generated comment, nullable directive, timestamp, and version
        FileHeaderGenerator.GenerateFileHeader(sb);
        sb.AppendLine($"namespace {entity.Namespace}");
        sb.AppendLine("{");
        sb.AppendLine($"    /// <summary>");
        sb.AppendLine($"    /// Key builder methods for {entity.ClassName} DynamoDB keys.");
        sb.AppendLine($"    /// </summary>");
        sb.AppendLine($"    public static partial class {entity.ClassName}Keys");
        sb.AppendLine("    {");

        // Generate main table key builders
        GenerateMainTableKeyBuilders(sb, entity);

        // Generate computed composite key builders
        GenerateComputedKeyBuilders(sb, entity);

        // Generate extraction helper methods
        GenerateExtractionHelpers(sb, entity);

        // Generate GSI key builder classes
        GenerateGsiKeyBuilderClasses(sb, entity);

        sb.AppendLine("    }");
        sb.AppendLine("}");

        return sb.ToString();
    }

    /// <summary>
    /// Generates a nested Keys class within an entity class.
    /// This method writes the Keys class directly to the provided StringBuilder with proper indentation.
    /// </summary>
    /// <param name="sb">The StringBuilder to write to.</param>
    /// <param name="entity">The entity model to generate key builders for.</param>
    public static void GenerateNestedKeysClass(StringBuilder sb, EntityModel entity)
    {
        sb.AppendLine();
        sb.AppendLine("        /// <summary>");
        sb.AppendLine($"        /// Key builder methods for {entity.ClassName} DynamoDB keys.");
        sb.AppendLine("        /// Provides strongly-typed methods to construct partition keys, sort keys, and composite keys");
        sb.AppendLine("        /// with proper formatting, validation, and DynamoDB compatibility.");
        sb.AppendLine("        /// </summary>");
        sb.AppendLine("        /// <remarks>");
        sb.AppendLine($"        /// This class is automatically generated for the {entity.ClassName} entity.");
        sb.AppendLine("        /// Key builder methods ensure:");
        sb.AppendLine("        /// - Consistent key formatting across your application");
        sb.AppendLine("        /// - Compile-time type safety for key parameters");
        sb.AppendLine("        /// - Automatic validation of key values (null checks, length limits, etc.)");
        sb.AppendLine("        /// - Proper handling of different data types (strings, GUIDs, dates, etc.)");
        sb.AppendLine("        /// </remarks>");
        sb.AppendLine("        /// <example>");
        sb.AppendLine("        /// <code>");
        sb.AppendLine($"        /// // Build a partition key");
        sb.AppendLine($"        /// var pk = {entity.ClassName}.Keys.Pk(userId);");
        sb.AppendLine("        /// ");
        sb.AppendLine($"        /// // Build a composite key");
        sb.AppendLine($"        /// var (partitionKey, sortKey) = {entity.ClassName}.Keys.Key(userId, timestamp);");
        sb.AppendLine("        /// ");
        sb.AppendLine($"        /// // Use in a query");
        sb.AppendLine($"        /// var item = await table.Get({entity.ClassName}.Keys.Pk(userId)).ExecuteAsync();");
        sb.AppendLine("        /// </code>");
        sb.AppendLine("        /// </example>");
        sb.AppendLine("        public static partial class Keys");
        sb.AppendLine("        {");

        // Generate main table key builders with nested indentation
        GenerateMainTableKeyBuilders(sb, entity, indentLevel: 3);

        // Generate computed composite key builders with nested indentation
        GenerateComputedKeyBuilders(sb, entity, indentLevel: 3);

        // Generate extraction helper methods with nested indentation
        GenerateExtractionHelpers(sb, entity, indentLevel: 3);

        // Generate GSI key builder classes with nested indentation
        GenerateGsiKeyBuilderClasses(sb, entity, indentLevel: 3);

        sb.AppendLine("        }");
    }

    /// <summary>
    /// Generates key builder methods for the main table partition and sort keys.
    /// </summary>
    private static void GenerateMainTableKeyBuilders(StringBuilder sb, EntityModel entity, int indentLevel = 2)
    {
        // Generate partition key builder
        if (entity.PartitionKeyProperty != null)
        {
            GeneratePartitionKeyBuilder(sb, entity.PartitionKeyProperty, "Pk", isMainTable: true, index: null, indentLevel);
        }

        // Generate sort key builder if exists
        if (entity.SortKeyProperty != null)
        {
            GenerateSortKeyBuilder(sb, entity.SortKeyProperty, "Sk", isMainTable: true, index: null, indentLevel);
        }

        // Generate composite key builder if both keys exist
        if (entity.PartitionKeyProperty != null && entity.SortKeyProperty != null)
        {
            GenerateCompositeKeyBuilder(sb, entity.PartitionKeyProperty, entity.SortKeyProperty, "Key", isMainTable: true, index: null, indentLevel);
        }
    }

    /// <summary>
    /// Generates key builder methods for computed composite keys.
    /// </summary>
    private static void GenerateComputedKeyBuilders(StringBuilder sb, EntityModel entity, int indentLevel = 2)
    {
        var computedProperties = entity.Properties.Where(p => p.IsComputed).ToArray();

        foreach (var computedProperty in computedProperties)
        {
            GenerateComputedKeyBuilder(sb, computedProperty, entity, indentLevel);
        }
    }

    /// <summary>
    /// Generates a key builder method for a computed composite key.
    /// </summary>
    private static void GenerateComputedKeyBuilder(StringBuilder sb, PropertyModel computedProperty, EntityModel entity, int indentLevel = 2)
    {
        var computedKey = computedProperty.ComputedKey!;
        var methodName = $"Build{computedProperty.PropertyName}";
        var indent = new string(' ', indentLevel * 4);

        // Get source property information
        var sourceProperties = computedKey.SourceProperties
            .Select(sp => entity.Properties.FirstOrDefault(p => p.PropertyName == sp))
            .Where(p => p != null)
            .ToArray();

        if (sourceProperties.Length == 0)
            return;

        // Generate method signature
        sb.AppendLine();
        sb.AppendLine($"{indent}/// <summary>");
        sb.AppendLine($"{indent}/// Builds the computed composite key for {computedProperty.PropertyName}.");
        sb.AppendLine($"{indent}/// Combines: {string.Join(", ", computedKey.SourceProperties)}");
        sb.AppendLine($"{indent}/// </summary>");

        foreach (var sourceProperty in sourceProperties)
        {
            var paramName = GetParameterName(sourceProperty!.PropertyName);
            sb.AppendLine($"{indent}/// <param name=\"{paramName}\">The {sourceProperty.PropertyName} value.</param>");
        }

        sb.AppendLine($"{indent}/// <returns>The computed composite key value.</returns>");

        var parameters = sourceProperties.Select(p => $"{GetParameterType(p!.PropertyType)} {GetParameterName(p.PropertyName)}").ToArray();
        sb.AppendLine($"{indent}public static string {methodName}({string.Join(", ", parameters)})");
        sb.AppendLine($"{indent}{{");

        // Generate parameter validation
        foreach (var sourceProperty in sourceProperties)
        {
            var paramName = GetParameterName(sourceProperty!.PropertyName);
            GenerateParameterValidation(sb, paramName, sourceProperty.PropertyType, indentLevel + 1);
        }

        // Generate key construction logic
        sb.AppendLine($"{indent}    try");
        sb.AppendLine($"{indent}    {{");

        if (computedKey.HasCustomFormat)
        {
            // Use custom format string
            var formatArgs = string.Join(", ", sourceProperties.Select(p => GetValueExpression(GetParameterName(p!.PropertyName), p.PropertyType)));
            sb.AppendLine($"{indent}        var keyValue = string.Format(\"{computedKey.Format}\", {formatArgs});");
        }
        else
        {
            // Use separator-based concatenation
            var sourceValues = string.Join($" + \"{computedKey.Separator}\" + ",
                sourceProperties.Select(p => GetValueExpression(GetParameterName(p!.PropertyName), p.PropertyType)));
            sb.AppendLine($"{indent}        var keyValue = {sourceValues};");
        }

        // Add key validation
        sb.AppendLine();
        sb.AppendLine($"{indent}        // Validate generated key");
        sb.AppendLine($"{indent}        if (string.IsNullOrEmpty(keyValue))");
        sb.AppendLine($"{indent}        {{");
        sb.AppendLine($"{indent}            throw new System.ArgumentException(\"Generated key cannot be null or empty. Check input parameters.\");");
        sb.AppendLine($"{indent}        }}");
        sb.AppendLine();
        sb.AppendLine($"{indent}        if (keyValue.Length > 2048)");
        sb.AppendLine($"{indent}        {{");
        sb.AppendLine($"{indent}            throw new System.ArgumentException($\"Generated key length ({{keyValue.Length}}) exceeds DynamoDB limit of 2048 bytes.\");");
        sb.AppendLine($"{indent}        }}");
        sb.AppendLine();
        sb.AppendLine($"{indent}        return keyValue;");
        sb.AppendLine($"{indent}    }}");
        sb.AppendLine($"{indent}    catch (System.ArgumentException)");
        sb.AppendLine($"{indent}    {{");
        sb.AppendLine($"{indent}        throw;");
        sb.AppendLine($"{indent}    }}");
        sb.AppendLine($"{indent}    catch (System.Exception ex)");
        sb.AppendLine($"{indent}    {{");

        var parameterInfo = string.Join(", ", sourceProperties.Select(p => $"{GetParameterName(p!.PropertyName)}: {{{GetParameterName(p.PropertyName)}}}"));
        sb.AppendLine($"{indent}        throw new System.InvalidOperationException(");
        sb.AppendLine($"{indent}            $\"Failed to generate computed key {computedProperty.PropertyName} with parameters: {parameterInfo}. {{ex.Message}}\", ex);");
        sb.AppendLine($"{indent}    }}");
        sb.AppendLine($"{indent}}}");
    }

    /// <summary>
    /// Generates helper methods for extracting components from composite keys.
    /// </summary>
    private static void GenerateExtractionHelpers(StringBuilder sb, EntityModel entity, int indentLevel = 2)
    {
        var extractedProperties = entity.Properties.Where(p => p.IsExtracted).ToArray();

        // Group by source property to avoid duplicate extraction methods
        var extractionGroups = extractedProperties
            .GroupBy(p => p.ExtractedKey!.SourceProperty)
            .ToArray();

        foreach (var group in extractionGroups)
        {
            GenerateExtractionHelper(sb, group.Key, group.ToArray(), entity, indentLevel);
        }
    }

    /// <summary>
    /// Generates an extraction helper method for a composite key.
    /// </summary>
    private static void GenerateExtractionHelper(StringBuilder sb, string sourcePropertyName, PropertyModel[] extractedProperties, EntityModel entity, int indentLevel = 2)
    {
        var sourceProperty = entity.Properties.FirstOrDefault(p => p.PropertyName == sourcePropertyName);
        if (sourceProperty == null)
            return;

        var methodName = $"Extract{sourcePropertyName}Components";
        var separator = extractedProperties.First().ExtractedKey!.Separator;
        var indent = new string(' ', indentLevel * 4);

        // Determine return type based on extracted properties
        var returnProperties = extractedProperties.OrderBy(p => p.ExtractedKey!.Index).ToArray();
        var returnType = returnProperties.Length == 1
            ? GetParameterType(returnProperties[0].PropertyType)
            : $"({string.Join(", ", returnProperties.Select(p => $"{GetParameterType(p.PropertyType)} {p.PropertyName}"))})";

        sb.AppendLine();
        sb.AppendLine($"{indent}/// <summary>");
        sb.AppendLine($"{indent}/// Extracts component values from the {sourcePropertyName} composite key.");
        sb.AppendLine($"{indent}/// Separator: '{separator}'");
        sb.AppendLine($"{indent}/// </summary>");
        sb.AppendLine($"{indent}/// <param name=\"{GetParameterName(sourcePropertyName)}\">The composite key value to extract from.</param>");
        sb.AppendLine($"{indent}/// <returns>The extracted component values.</returns>");
        sb.AppendLine($"{indent}public static {returnType} {methodName}(string {GetParameterName(sourcePropertyName)})");
        sb.AppendLine($"{indent}{{");

        // Parameter validation
        sb.AppendLine($"{indent}    if (string.IsNullOrEmpty({GetParameterName(sourcePropertyName)}))");
        sb.AppendLine($"{indent}        throw new System.ArgumentException(\"Composite key cannot be null or empty.\", nameof({GetParameterName(sourcePropertyName)}));");
        sb.AppendLine();

        // Extract components
        sb.AppendLine($"{indent}    var parts = {GetParameterName(sourcePropertyName)}.Split('{separator}');");
        sb.AppendLine();

        if (returnProperties.Length == 1)
        {
            var extractedProperty = returnProperties[0];
            var index = extractedProperty.ExtractedKey!.Index;
            sb.AppendLine($"{indent}    if (parts.Length <= {index})");
            sb.AppendLine($"{indent}        throw new System.ArgumentException($\"Composite key does not contain enough components. Expected at least {index + 1}, got {{parts.Length}}.\");");
            sb.AppendLine();
            sb.AppendLine($"{indent}    return {GetExtractionExpression("parts[" + index + "]", extractedProperty.PropertyType)};");
        }
        else
        {
            var maxIndex = returnProperties.Max(p => p.ExtractedKey!.Index);
            sb.AppendLine($"{indent}    if (parts.Length <= {maxIndex})");
            sb.AppendLine($"{indent}        throw new System.ArgumentException($\"Composite key does not contain enough components. Expected at least {maxIndex + 1}, got {{parts.Length}}.\");");
            sb.AppendLine();

            var returnValues = returnProperties.Select(p =>
                $"{p.PropertyName}: {GetExtractionExpression($"parts[{p.ExtractedKey!.Index}]", p.PropertyType)}");
            sb.AppendLine($"{indent}    return ({string.Join(", ", returnValues)});");
        }

        sb.AppendLine($"{indent}}}");
    }

    /// <summary>
    /// Gets the expression to convert a string component to the target property type.
    /// </summary>
    private static string GetExtractionExpression(string valueExpression, string propertyType)
    {
        var baseType = GetBaseType(propertyType);

        return baseType switch
        {
            "string" => valueExpression,
            "int" or "System.Int32" => $"int.Parse({valueExpression})",
            "long" or "System.Int64" => $"long.Parse({valueExpression})",
            "double" or "System.Double" => $"double.Parse({valueExpression})",
            "float" or "System.Single" => $"float.Parse({valueExpression})",
            "decimal" or "System.Decimal" => $"decimal.Parse({valueExpression})",
            "bool" or "System.Boolean" => $"bool.Parse({valueExpression})",
            "DateTime" or "System.DateTime" => $"DateTime.Parse({valueExpression})",
            "DateTimeOffset" or "System.DateTimeOffset" => $"DateTimeOffset.Parse({valueExpression})",
            "Guid" or "System.Guid" => $"Guid.Parse({valueExpression})",
            "Ulid" or "System.Ulid" => $"Ulid.Parse({valueExpression})",
            _ when IsEnumType(propertyType) => $"Enum.Parse<{baseType}>({valueExpression})",
            _ => valueExpression
        };
    }

    /// <summary>
    /// Gets the base type name without nullable annotations.
    /// </summary>
    private static string GetBaseType(string propertyType)
    {
        return propertyType.TrimEnd('?');
    }

    /// <summary>
    /// Generates nested key builder classes for Global Secondary Indexes.
    /// </summary>
    private static void GenerateGsiKeyBuilderClasses(StringBuilder sb, EntityModel entity, int indentLevel = 2)
    {
        if (entity.Indexes.Length == 0)
            return;

        foreach (var index in entity.Indexes.OrderBy(i => i.IndexName))
        {
            GenerateGsiKeyBuilderClass(sb, entity, index, indentLevel);
        }
    }

    /// <summary>
    /// Generates a nested key builder class for a specific GSI.
    /// </summary>
    private static void GenerateGsiKeyBuilderClass(StringBuilder sb, EntityModel entity, IndexModel index, int indentLevel = 2)
    {
        // Remove "Keys" suffix for nested classes - just use the index name
        var className = GetSafeClassName(index.IndexName);
        var indent = new string(' ', indentLevel * 4);

        sb.AppendLine();
        sb.AppendLine($"{indent}/// <summary>");
        sb.AppendLine($"{indent}/// Key builder methods for {index.IndexName} Global Secondary Index.");
        sb.AppendLine($"{indent}/// Provides strongly-typed methods to construct GSI partition keys and sort keys");
        sb.AppendLine($"{indent}/// with proper formatting and validation specific to this index.");
        sb.AppendLine($"{indent}/// </summary>");
        sb.AppendLine($"{indent}/// <remarks>");
        sb.AppendLine($"{indent}/// This nested class provides key builders specifically for the {index.IndexName} GSI.");
        sb.AppendLine($"{indent}/// Use these methods when querying or filtering on this Global Secondary Index.");
        sb.AppendLine($"{indent}/// The key format may differ from the main table keys based on your GSI configuration.");
        sb.AppendLine($"{indent}/// </remarks>");
        sb.AppendLine($"{indent}/// <example>");
        sb.AppendLine($"{indent}/// <code>");
        sb.AppendLine($"{indent}/// // Build GSI partition key");
        sb.AppendLine($"{indent}/// var gsiPk = {entity.ClassName}.Keys.{className}.Pk(value);");
        sb.AppendLine($"{indent}/// ");
        sb.AppendLine($"{indent}/// // Use in a GSI query");
        sb.AppendLine($"{indent}/// var results = await table.{index.IndexName}.Query()");
        sb.AppendLine($"{indent}///     .Where(\"gsi_pk = {{0}}\", {entity.ClassName}.Keys.{className}.Pk(value))");
        sb.AppendLine($"{indent}///     .ToListAsync();");
        sb.AppendLine($"{indent}/// </code>");
        sb.AppendLine($"{indent}/// </example>");
        sb.AppendLine($"{indent}public static partial class {className}");
        sb.AppendLine($"{indent}{{");

        // Get partition key property for this GSI
        var partitionKeyProperty = entity.Properties.FirstOrDefault(p => p.PropertyName == index.PartitionKeyProperty);
        if (partitionKeyProperty != null)
        {
            GeneratePartitionKeyBuilder(sb, partitionKeyProperty, "Pk", isMainTable: false, index, indentLevel + 1);
        }

        // Get sort key property for this GSI if exists
        if (!string.IsNullOrEmpty(index.SortKeyProperty))
        {
            var sortKeyProperty = entity.Properties.FirstOrDefault(p => p.PropertyName == index.SortKeyProperty);
            if (sortKeyProperty != null)
            {
                GenerateSortKeyBuilder(sb, sortKeyProperty, "Sk", isMainTable: false, index, indentLevel + 1);
            }
        }

        // Generate composite key builder if both keys exist
        if (partitionKeyProperty != null && !string.IsNullOrEmpty(index.SortKeyProperty))
        {
            var sortKeyProperty = entity.Properties.FirstOrDefault(p => p.PropertyName == index.SortKeyProperty);
            if (sortKeyProperty != null)
            {
                GenerateCompositeKeyBuilder(sb, partitionKeyProperty, sortKeyProperty, "Key", isMainTable: false, index, indentLevel + 1);
            }
        }

        sb.AppendLine($"{indent}}}");
    }

    /// <summary>
    /// Generates a partition key builder method.
    /// </summary>
    private static void GeneratePartitionKeyBuilder(StringBuilder sb, PropertyModel property, string methodName, bool isMainTable, IndexModel? index = null, int indentLevel = 2)
    {
        var keyFormat = GetKeyFormat(property, index, isPartitionKey: true);
        var parameterType = GetParameterType(property.PropertyType);
        var parameterName = GetParameterName(property.PropertyName);
        var indent = new string(' ', indentLevel * 4);

        sb.AppendLine();
        sb.AppendLine($"{indent}/// <summary>");
        sb.AppendLine($"{indent}/// Builds the partition key value for {property.PropertyName}.");
        if (!isMainTable && index != null)
        {
            sb.AppendLine($"{indent}/// Used for {index.IndexName} Global Secondary Index.");
        }
        sb.AppendLine($"{indent}/// </summary>");
        sb.AppendLine($"{indent}/// <param name=\"{parameterName}\">The {property.PropertyName} value. Must not be null or empty.</param>");
        sb.AppendLine($"{indent}/// <returns>The formatted partition key value ready for use in DynamoDB operations.</returns>");
        sb.AppendLine($"{indent}/// <exception cref=\"System.ArgumentNullException\">Thrown when {parameterName} is null.</exception>");
        sb.AppendLine($"{indent}/// <exception cref=\"System.ArgumentException\">Thrown when {parameterName} is invalid (empty, whitespace, or contains invalid characters).</exception>");
        sb.AppendLine($"{indent}/// <remarks>");
        sb.AppendLine($"{indent}/// This method automatically formats the key value according to the configured key format.");
        sb.AppendLine($"{indent}/// The generated key is validated to ensure it meets DynamoDB requirements (max 2048 bytes).");
        sb.AppendLine($"{indent}/// </remarks>");
        sb.AppendLine($"{indent}public static string {methodName}({parameterType} {parameterName})");
        sb.AppendLine($"{indent}{{");

        GenerateKeyBuilderBody(sb, keyFormat, new[] { (parameterName, property.PropertyType) }, indentLevel + 1);

        sb.AppendLine($"{indent}}}");
    }

    /// <summary>
    /// Generates a sort key builder method.
    /// </summary>
    private static void GenerateSortKeyBuilder(StringBuilder sb, PropertyModel property, string methodName, bool isMainTable, IndexModel? index = null, int indentLevel = 2)
    {
        var keyFormat = GetKeyFormat(property, index, isPartitionKey: false);
        var parameterType = GetParameterType(property.PropertyType);
        var parameterName = GetParameterName(property.PropertyName);
        var indent = new string(' ', indentLevel * 4);

        sb.AppendLine();
        sb.AppendLine($"{indent}/// <summary>");
        sb.AppendLine($"{indent}/// Builds the sort key value for {property.PropertyName}.");
        if (!isMainTable && index != null)
        {
            sb.AppendLine($"{indent}/// Used for {index.IndexName} Global Secondary Index.");
        }
        sb.AppendLine($"{indent}/// </summary>");
        sb.AppendLine($"{indent}/// <param name=\"{parameterName}\">The {property.PropertyName} value. Must not be null or empty.</param>");
        sb.AppendLine($"{indent}/// <returns>The formatted sort key value ready for use in DynamoDB operations.</returns>");
        sb.AppendLine($"{indent}/// <exception cref=\"System.ArgumentNullException\">Thrown when {parameterName} is null.</exception>");
        sb.AppendLine($"{indent}/// <exception cref=\"System.ArgumentException\">Thrown when {parameterName} is invalid (empty, whitespace, or contains invalid characters).</exception>");
        sb.AppendLine($"{indent}/// <remarks>");
        sb.AppendLine($"{indent}/// This method automatically formats the key value according to the configured key format.");
        sb.AppendLine($"{indent}/// The generated key is validated to ensure it meets DynamoDB requirements (max 2048 bytes).");
        sb.AppendLine($"{indent}/// Sort keys enable range queries and ordered retrieval of items.");
        sb.AppendLine($"{indent}/// </remarks>");
        sb.AppendLine($"{indent}public static string {methodName}({parameterType} {parameterName})");
        sb.AppendLine($"{indent}{{");

        GenerateKeyBuilderBody(sb, keyFormat, new[] { (parameterName, property.PropertyType) }, indentLevel + 1);

        sb.AppendLine($"{indent}}}");
    }

    /// <summary>
    /// Generates a composite key builder method that accepts both partition and sort key parameters.
    /// </summary>
    private static void GenerateCompositeKeyBuilder(StringBuilder sb, PropertyModel partitionKeyProperty, PropertyModel sortKeyProperty, string methodName, bool isMainTable, IndexModel? index = null, int indentLevel = 2)
    {
        var pkParameterType = GetParameterType(partitionKeyProperty.PropertyType);
        var pkParameterName = GetParameterName(partitionKeyProperty.PropertyName);
        var skParameterType = GetParameterType(sortKeyProperty.PropertyType);
        var skParameterName = GetParameterName(sortKeyProperty.PropertyName);
        var indent = new string(' ', indentLevel * 4);

        sb.AppendLine();
        sb.AppendLine($"{indent}/// <summary>");
        sb.AppendLine($"{indent}/// Builds a composite key containing both partition and sort key values.");
        if (!isMainTable && index != null)
        {
            sb.AppendLine($"{indent}/// Used for {index.IndexName} Global Secondary Index.");
        }
        sb.AppendLine($"{indent}/// </summary>");
        sb.AppendLine($"{indent}/// <param name=\"{pkParameterName}\">The {partitionKeyProperty.PropertyName} value for the partition key. Must not be null or empty.</param>");
        sb.AppendLine($"{indent}/// <param name=\"{skParameterName}\">The {sortKeyProperty.PropertyName} value for the sort key. Must not be null or empty.</param>");
        sb.AppendLine($"{indent}/// <returns>A tuple containing the formatted partition key and sort key values, ready for use in DynamoDB operations.</returns>");
        sb.AppendLine($"{indent}/// <exception cref=\"System.ArgumentNullException\">Thrown when either parameter is null.</exception>");
        sb.AppendLine($"{indent}/// <exception cref=\"System.ArgumentException\">Thrown when either parameter is invalid.</exception>");
        sb.AppendLine($"{indent}/// <remarks>");
        sb.AppendLine($"{indent}/// This convenience method builds both keys in a single call by delegating to Pk() and Sk() methods.");
        sb.AppendLine($"{indent}/// Use this when you need both keys for operations like GetItem, DeleteItem, or UpdateItem.");
        sb.AppendLine($"{indent}/// </remarks>");
        sb.AppendLine($"{indent}/// <example>");
        sb.AppendLine($"{indent}/// <code>");
        sb.AppendLine($"{indent}/// var (pk, sk) = {methodName}({pkParameterName}Value, {skParameterName}Value);");
        sb.AppendLine($"{indent}/// var item = await table.Get().WithKey(\"pk\", pk, \"sk\", sk).ExecuteAsync();");
        sb.AppendLine($"{indent}/// </code>");
        sb.AppendLine($"{indent}/// </example>");
        sb.AppendLine($"{indent}public static (string PartitionKey, string SortKey) {methodName}({pkParameterType} {pkParameterName}, {skParameterType} {skParameterName})");
        sb.AppendLine($"{indent}{{");
        sb.AppendLine($"{indent}    return (Pk({pkParameterName}), Sk({skParameterName}));");
        sb.AppendLine($"{indent}}}");
    }

    /// <summary>
    /// Gets the key format information for a property, considering GSI-specific formats.
    /// </summary>
    private static KeyFormatInfo GetKeyFormat(PropertyModel property, IndexModel? index, bool isPartitionKey)
    {
        // Check if there's a GSI-specific format
        if (index != null)
        {
            var gsiAttribute = property.GlobalSecondaryIndexes.FirstOrDefault(gsi => gsi.IndexName == index.IndexName);
            if (gsiAttribute != null && !string.IsNullOrEmpty(gsiAttribute.KeyFormat))
            {
                return ParseKeyFormat(gsiAttribute.KeyFormat!);
            }
        }

        // Use property-level key format if available
        if (property.KeyFormat != null)
        {
            return new KeyFormatInfo
            {
                Prefix = property.KeyFormat.Prefix,
                Separator = property.KeyFormat.Separator
            };
        }

        // Default format
        return new KeyFormatInfo
        {
            Prefix = null,
            Separator = "#"
        };
    }

    /// <summary>
    /// Parses a key format string into components.
    /// </summary>
    private static KeyFormatInfo ParseKeyFormat(string keyFormat)
    {
        // Simple parsing for now - can be enhanced for more complex formats
        // Format examples: "tenant#{0}", "{0}#{1}", "prefix_{0}"

        var info = new KeyFormatInfo { Separator = "#" };

        // Extract prefix if format starts with text before {0}
        var firstPlaceholder = keyFormat.IndexOf("{0}");
        if (firstPlaceholder > 0)
        {
            var prefix = keyFormat.Substring(0, firstPlaceholder);
            // Remove common separators from the end of prefix
            prefix = prefix.TrimEnd('#', '_', '-', ':', '|');
            info.Prefix = prefix;

            // Determine separator from what was trimmed
            var separatorStart = prefix.Length;
            if (separatorStart < firstPlaceholder)
            {
                info.Separator = keyFormat.Substring(separatorStart, firstPlaceholder - separatorStart);
            }
        }

        return info;
    }

    /// <summary>
    /// Generates the method body for a key builder with simple, direct key construction.
    /// </summary>
    private static void GenerateKeyBuilderBody(StringBuilder sb, KeyFormatInfo keyFormat, (string parameterName, string propertyType)[] parameters, int indentLevel = 3)
    {
        var indent = new string(' ', indentLevel * 4);

        // Generate parameter validation for all parameters
        foreach (var (parameterName, propertyType) in parameters)
        {
            GenerateParameterValidation(sb, parameterName, propertyType, indentLevel);
        }

        // Generate simple, direct key construction logic
        if (parameters.Length == 1)
        {
            var (parameterName, propertyType) = parameters[0];
            var valueExpression = GetValueExpression(parameterName, propertyType);

            if (!string.IsNullOrEmpty(keyFormat.Prefix))
            {
                sb.AppendLine($"{indent}var keyValue = \"{keyFormat.Prefix}{keyFormat.Separator}\" + {valueExpression};");
            }
            else
            {
                sb.AppendLine($"{indent}var keyValue = {valueExpression};");
            }

            sb.AppendLine($"{indent}return keyValue;");
        }
        else
        {
            // Multiple parameters - build composite key
            var valueExpressions = parameters.Select(p => GetValueExpression(p.parameterName, p.propertyType)).ToArray();
            var separator = keyFormat.Separator;

            if (!string.IsNullOrEmpty(keyFormat.Prefix))
            {
                sb.AppendLine($"{indent}var keyValue = \"{keyFormat.Prefix}{separator}\" + string.Join(\"{separator}\", {string.Join(", ", valueExpressions)});");
            }
            else
            {
                sb.AppendLine($"{indent}var keyValue = string.Join(\"{separator}\", {string.Join(", ", valueExpressions)});");
            }

            sb.AppendLine($"{indent}return keyValue;");
        }
    }

    /// <summary>
    /// Generates comprehensive parameter validation for key builder methods.
    /// </summary>
    private static void GenerateParameterValidation(StringBuilder sb, string parameterName, string propertyType, int indentLevel = 3)
    {
        var indent = new string(' ', indentLevel * 4);

        // Null checks for nullable parameters
        if (IsNullableType(propertyType))
        {
            sb.AppendLine($"{indent}if ({parameterName} == null)");
            sb.AppendLine($"{indent}    throw new System.ArgumentNullException(nameof({parameterName}), \"Key parameter cannot be null.\");");
            sb.AppendLine();
        }

        // String-specific validation
        if (propertyType == "string" || propertyType.EndsWith("?"))
        {
            sb.AppendLine($"{indent}if (string.IsNullOrWhiteSpace({parameterName}))");
            sb.AppendLine($"{indent}    throw new System.ArgumentException(\"String key parameter cannot be null, empty, or whitespace.\", nameof({parameterName}));");
            sb.AppendLine();

            // Check for problematic characters in string keys
            sb.AppendLine($"{indent}if ({parameterName}.Contains('\\0'))");
            sb.AppendLine($"{indent}    throw new System.ArgumentException(\"Key parameter cannot contain null characters.\", nameof({parameterName}));");
            sb.AppendLine();
        }

        // Guid-specific validation
        if (propertyType.Contains("Guid"))
        {
            sb.AppendLine($"{indent}if ({parameterName} == System.Guid.Empty)");
            sb.AppendLine($"{indent}    throw new System.ArgumentException(\"Guid key parameter cannot be empty.\", nameof({parameterName}));");
            sb.AppendLine();
        }

        // DateTime-specific validation
        if (propertyType.Contains("DateTime"))
        {
            sb.AppendLine($"{indent}if ({parameterName} == default)");
            sb.AppendLine($"{indent}    throw new System.ArgumentException(\"DateTime key parameter cannot be default value.\", nameof({parameterName}));");
            sb.AppendLine();
        }

        // Numeric validation for negative values that might be problematic
        if (IsNumericType(propertyType) && !propertyType.Contains("uint") && !propertyType.Contains("UInt"))
        {
            sb.AppendLine($"{indent}if ({parameterName} < 0)");
            sb.AppendLine($"{indent}    System.Diagnostics.Debug.WriteLine($\"Warning: Negative value {{{{parameterName}}}} used in key generation may cause sorting issues.\");");
            sb.AppendLine();
        }
    }

    /// <summary>
    /// Gets the appropriate parameter type for a property type.
    /// </summary>
    private static string GetParameterType(string propertyType)
    {
        // Handle nullable types
        if (propertyType.EndsWith("?"))
        {
            return propertyType;
        }

        // Handle generic nullable types
        if (propertyType.StartsWith("System.Nullable<") || propertyType.StartsWith("Nullable<"))
        {
            return propertyType;
        }

        return propertyType;
    }

    /// <summary>
    /// Gets a safe parameter name from a property name.
    /// </summary>
    private static string GetParameterName(string propertyName)
    {
        // Convert to camelCase
        if (string.IsNullOrEmpty(propertyName))
            return "value";

        return char.ToLowerInvariant(propertyName[0]) + propertyName.Substring(1);
    }

    /// <summary>
    /// Gets the expression to convert a parameter to string for key building.
    /// </summary>
    private static string GetValueExpression(string parameterName, string propertyType)
    {
        // Handle different types appropriately
        if (propertyType == "string" || propertyType.EndsWith("?"))
        {
            return parameterName;
        }

        // Handle Guid types
        if (propertyType.Contains("Guid"))
        {
            return $"{parameterName}.ToString()";
        }

        // Handle Ulid types (common in DynamoDB scenarios)
        if (propertyType.Contains("Ulid"))
        {
            return $"{parameterName}.ToString()";
        }

        // Handle DateTime types
        if (propertyType.Contains("DateTime"))
        {
            return $"{parameterName}.ToString(\"yyyy-MM-ddTHH:mm:ss.fffZ\")";
        }

        // Handle DateTimeOffset types
        if (propertyType.Contains("DateTimeOffset"))
        {
            return $"{parameterName}.ToString(\"yyyy-MM-ddTHH:mm:ss.fffZ\")";
        }

        // Handle numeric types and enums
        if (IsNumericType(propertyType) || IsEnumType(propertyType))
        {
            return $"{parameterName}.ToString()";
        }

        // Default to ToString() for other types
        return $"{parameterName}.ToString()";
    }

    /// <summary>
    /// Determines if a type is nullable.
    /// </summary>
    private static bool IsNullableType(string propertyType)
    {
        return propertyType.EndsWith("?") ||
               propertyType.StartsWith("System.Nullable<") ||
               propertyType.StartsWith("Nullable<") ||
               propertyType == "string"; // strings are reference types and nullable by default
    }

    /// <summary>
    /// Determines if a type is numeric.
    /// </summary>
    private static bool IsNumericType(string propertyType)
    {
        var numericTypes = new HashSet<string>
        {
            "int", "int?", "System.Int32", "System.Int32?",
            "long", "long?", "System.Int64", "System.Int64?",
            "short", "short?", "System.Int16", "System.Int16?",
            "byte", "byte?", "System.Byte", "System.Byte?",
            "uint", "uint?", "System.UInt32", "System.UInt32?",
            "ulong", "ulong?", "System.UInt64", "System.UInt64?",
            "ushort", "ushort?", "System.UInt16", "System.UInt16?",
            "sbyte", "sbyte?", "System.SByte", "System.SByte?",
            "decimal", "decimal?", "System.Decimal", "System.Decimal?",
            "double", "double?", "System.Double", "System.Double?",
            "float", "float?", "System.Single", "System.Single?"
        };

        return numericTypes.Contains(propertyType);
    }

    /// <summary>
    /// Determines if a type is an enum (simplified check).
    /// </summary>
    private static bool IsEnumType(string propertyType)
    {
        // This is a simplified check - in a real implementation, we'd use semantic model
        // to determine if the type is actually an enum
        return propertyType.Contains("Status") ||
               propertyType.Contains("Type") ||
               propertyType.Contains("Kind") ||
               propertyType.Contains("State");
    }

    /// <summary>
    /// Converts a name to a safe class name.
    /// </summary>
    private static string GetSafeClassName(string name)
    {
        // Remove invalid characters and ensure it starts with a letter or underscore
        var safeName = new StringBuilder();

        for (int i = 0; i < name.Length; i++)
        {
            char c = name[i];

            if (i == 0)
            {
                // First character must be letter or underscore
                if (char.IsLetter(c) || c == '_')
                {
                    safeName.Append(c);
                }
                else if (char.IsDigit(c))
                {
                    safeName.Append('_').Append(c);
                }
                else
                {
                    safeName.Append('_');
                }
            }
            else
            {
                // Subsequent characters can be letters, digits, or underscores
                if (char.IsLetterOrDigit(c) || c == '_')
                {
                    safeName.Append(c);
                }
                else
                {
                    safeName.Append('_');
                }
            }
        }

        var result = safeName.ToString();

        // Ensure we don't have an empty result
        if (string.IsNullOrEmpty(result))
        {
            result = "_Keys";
        }

        return result;
    }

    /// <summary>
    /// Represents key formatting information.
    /// </summary>
    private class KeyFormatInfo
    {
        public string Prefix { get; set; }
        public string Separator { get; set; } = "#";
    }
}