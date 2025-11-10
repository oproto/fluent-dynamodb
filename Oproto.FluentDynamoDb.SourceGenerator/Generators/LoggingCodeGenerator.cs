using System.Text;

namespace Oproto.FluentDynamoDb.SourceGenerator.Generators;

/// <summary>
/// Generates logging code snippets for entity mapping operations.
/// Supports conditional compilation directives to allow logging to be disabled in production builds.
/// </summary>
/// <remarks>
/// <para><strong>Purpose:</strong></para>
/// <para>
/// LoggingCodeGenerator provides reusable methods for generating logging code throughout
/// the source generator. All logging code is wrapped in conditional compilation directives
/// (#if !DISABLE_DYNAMODB_LOGGING) to allow zero-overhead production builds.
/// </para>
/// <para><strong>Design Principles:</strong></para>
/// <list type="bullet">
/// <item><description>Conditional Compilation: All logging wrapped in #if !DISABLE_DYNAMODB_LOGGING</description></item>
/// <item><description>Null-Safe: Uses null-conditional operators (logger?.Method) for safety</description></item>
/// <item><description>Performance: Checks IsEnabled before expensive parameter evaluation</description></item>
/// <item><description>Structured Logging: Uses message templates with parameters for structured logging</description></item>
/// </list>
/// </remarks>
internal static class LoggingCodeGenerator
{
    /// <summary>
    /// Generates entry logging code for ToDynamoDb method.
    /// Logs at Trace level with entity type information.
    /// </summary>
    /// <param name="entityTypeName">The name of the entity type being mapped.</param>
    /// <returns>Generated logging code wrapped in conditional compilation directives.</returns>
    public static string GenerateToDynamoDbEntryLogging(string entityTypeName)
    {
        var sb = new StringBuilder();
        
        sb.AppendLine("            #if !DISABLE_DYNAMODB_LOGGING");
        sb.AppendLine("            logger?.LogTrace(LogEventIds.MappingToDynamoDbStart,");
        sb.AppendLine($"                \"Starting ToDynamoDb mapping for {{EntityType}}\",");
        sb.AppendLine($"                \"{entityTypeName}\");");
        sb.AppendLine("            #endif");
        
        return sb.ToString();
    }

    /// <summary>
    /// Generates exit logging code for ToDynamoDb method.
    /// Logs at Trace level with entity type and attribute count.
    /// </summary>
    /// <param name="entityTypeName">The name of the entity type being mapped.</param>
    /// <param name="itemVariableName">The name of the variable containing the DynamoDB item dictionary.</param>
    /// <returns>Generated logging code wrapped in conditional compilation directives.</returns>
    public static string GenerateToDynamoDbExitLogging(string entityTypeName, string itemVariableName = "item")
    {
        var sb = new StringBuilder();
        
        sb.AppendLine("            #if !DISABLE_DYNAMODB_LOGGING");
        sb.AppendLine("            logger?.LogTrace(LogEventIds.MappingToDynamoDbComplete,");
        sb.AppendLine($"                \"Completed ToDynamoDb mapping for {{EntityType}} with {{AttributeCount}} attributes\",");
        sb.AppendLine($"                \"{entityTypeName}\", {itemVariableName}.Count);");
        sb.AppendLine("            #endif");
        
        return sb.ToString();
    }

    /// <summary>
    /// Generates entry logging code for FromDynamoDb method.
    /// Logs at Trace level with entity type and attribute count.
    /// </summary>
    /// <param name="entityTypeName">The name of the entity type being mapped.</param>
    /// <param name="itemVariableName">The name of the variable containing the DynamoDB item dictionary.</param>
    /// <returns>Generated logging code wrapped in conditional compilation directives.</returns>
    public static string GenerateFromDynamoDbEntryLogging(string entityTypeName, string itemVariableName = "item")
    {
        var sb = new StringBuilder();
        
        sb.AppendLine("            #if !DISABLE_DYNAMODB_LOGGING");
        sb.AppendLine("            logger?.LogTrace(LogEventIds.MappingFromDynamoDbStart,");
        sb.AppendLine($"                \"Starting FromDynamoDb mapping for {{EntityType}} with {{AttributeCount}} attributes\",");
        sb.AppendLine($"                \"{entityTypeName}\", {itemVariableName}.Count);");
        sb.AppendLine("            #endif");
        
        return sb.ToString();
    }

    /// <summary>
    /// Generates exit logging code for FromDynamoDb method.
    /// Logs at Trace level with entity type.
    /// </summary>
    /// <param name="entityTypeName">The name of the entity type being mapped.</param>
    /// <returns>Generated logging code wrapped in conditional compilation directives.</returns>
    public static string GenerateFromDynamoDbExitLogging(string entityTypeName)
    {
        var sb = new StringBuilder();
        
        sb.AppendLine("            #if !DISABLE_DYNAMODB_LOGGING");
        sb.AppendLine("            logger?.LogTrace(LogEventIds.MappingFromDynamoDbComplete,");
        sb.AppendLine($"                \"Completed FromDynamoDb mapping for {{EntityType}}\",");
        sb.AppendLine($"                \"{entityTypeName}\");");
        sb.AppendLine("            #endif");
        
        return sb.ToString();
    }

    /// <summary>
    /// Generates property mapping logging code.
    /// Logs at Debug level with property name and type, checking IsEnabled first for performance.
    /// </summary>
    /// <param name="propertyName">The name of the property being mapped.</param>
    /// <param name="propertyType">The type of the property being mapped.</param>
    /// <param name="direction">The mapping direction ("ToDynamoDb" or "FromDynamoDb").</param>
    /// <returns>Generated logging code wrapped in conditional compilation directives.</returns>
    public static string GeneratePropertyMappingLogging(string propertyName, string propertyType, string direction = "ToDynamoDb")
    {
        var sb = new StringBuilder();
        
        sb.AppendLine("            #if !DISABLE_DYNAMODB_LOGGING");
        sb.AppendLine("            if (logger?.IsEnabled(LogLevel.Debug) == true)");
        sb.AppendLine("            {");
        sb.AppendLine("                logger.LogDebug(LogEventIds.MappingPropertyStart,");
        sb.AppendLine($"                    \"Mapping property {{PropertyName}} of type {{PropertyType}} ({direction})\",");
        sb.AppendLine($"                    \"{propertyName}\", \"{propertyType}\");");
        sb.AppendLine("            }");
        sb.AppendLine("            #endif");
        
        return sb.ToString();
    }

    /// <summary>
    /// Generates logging code for skipped properties (null or empty values).
    /// Logs at Debug level with property name and reason.
    /// </summary>
    /// <param name="propertyName">The name of the property being skipped.</param>
    /// <param name="reason">The reason the property is being skipped (e.g., "null value", "empty collection").</param>
    /// <returns>Generated logging code wrapped in conditional compilation directives.</returns>
    public static string GeneratePropertySkippedLogging(string propertyName, string reason)
    {
        var sb = new StringBuilder();
        
        sb.AppendLine("            #if !DISABLE_DYNAMODB_LOGGING");
        sb.AppendLine("            logger?.LogDebug(LogEventIds.MappingPropertySkipped,");
        sb.AppendLine($"                \"Skipping property {{PropertyName}}: {{Reason}}\",");
        sb.AppendLine($"                \"{propertyName}\", \"{reason}\");");
        sb.AppendLine("            #endif");
        
        return sb.ToString();
    }

    /// <summary>
    /// Generates logging code for Map (Dictionary) conversions.
    /// Logs at Debug level with property name and element count.
    /// </summary>
    /// <param name="propertyName">The name of the Map property being converted.</param>
    /// <param name="elementCountExpression">Expression that evaluates to the element count.</param>
    /// <param name="direction">The conversion direction ("ToDynamoDb" or "FromDynamoDb").</param>
    /// <returns>Generated logging code wrapped in conditional compilation directives.</returns>
    public static string GenerateMapConversionLogging(string propertyName, string elementCountExpression, string direction = "ToDynamoDb")
    {
        var sb = new StringBuilder();
        
        sb.AppendLine("            #if !DISABLE_DYNAMODB_LOGGING");
        sb.AppendLine("            if (logger?.IsEnabled(LogLevel.Debug) == true)");
        sb.AppendLine("            {");
        sb.AppendLine("                logger.LogDebug(LogEventIds.ConvertingMap,");
        sb.AppendLine($"                    \"Converting {{PropertyName}} to Map with {{ElementCount}} elements ({direction})\",");
        sb.AppendLine($"                    \"{propertyName}\", {elementCountExpression});");
        sb.AppendLine("            }");
        sb.AppendLine("            #endif");
        
        return sb.ToString();
    }

    /// <summary>
    /// Generates logging code for Set (HashSet) conversions.
    /// Logs at Debug level with property name, set type, and element count.
    /// </summary>
    /// <param name="propertyName">The name of the Set property being converted.</param>
    /// <param name="setType">The type of set (e.g., "String Set", "Number Set", "Binary Set").</param>
    /// <param name="elementCountExpression">Expression that evaluates to the element count.</param>
    /// <param name="direction">The conversion direction ("ToDynamoDb" or "FromDynamoDb").</param>
    /// <returns>Generated logging code wrapped in conditional compilation directives.</returns>
    public static string GenerateSetConversionLogging(string propertyName, string setType, string elementCountExpression, string direction = "ToDynamoDb")
    {
        var sb = new StringBuilder();
        
        sb.AppendLine("            #if !DISABLE_DYNAMODB_LOGGING");
        sb.AppendLine("            if (logger?.IsEnabled(LogLevel.Debug) == true)");
        sb.AppendLine("            {");
        sb.AppendLine("                logger.LogDebug(LogEventIds.ConvertingSet,");
        sb.AppendLine($"                    \"Converting {{PropertyName}} to {{SetType}} with {{ElementCount}} elements ({direction})\",");
        sb.AppendLine($"                    \"{propertyName}\", \"{setType}\", {elementCountExpression});");
        sb.AppendLine("            }");
        sb.AppendLine("            #endif");
        
        return sb.ToString();
    }

    /// <summary>
    /// Generates logging code for List conversions.
    /// Logs at Debug level with property name and element count.
    /// </summary>
    /// <param name="propertyName">The name of the List property being converted.</param>
    /// <param name="elementCountExpression">Expression that evaluates to the element count.</param>
    /// <param name="direction">The conversion direction ("ToDynamoDb" or "FromDynamoDb").</param>
    /// <returns>Generated logging code wrapped in conditional compilation directives.</returns>
    public static string GenerateListConversionLogging(string propertyName, string elementCountExpression, string direction = "ToDynamoDb")
    {
        var sb = new StringBuilder();
        
        sb.AppendLine("            #if !DISABLE_DYNAMODB_LOGGING");
        sb.AppendLine("            if (logger?.IsEnabled(LogLevel.Debug) == true)");
        sb.AppendLine("            {");
        sb.AppendLine("                logger.LogDebug(LogEventIds.ConvertingList,");
        sb.AppendLine($"                    \"Converting {{PropertyName}} to List with {{ElementCount}} elements ({direction})\",");
        sb.AppendLine($"                    \"{propertyName}\", {elementCountExpression});");
        sb.AppendLine("            }");
        sb.AppendLine("            #endif");
        
        return sb.ToString();
    }

    /// <summary>
    /// Generates logging code for TTL (Time-To-Live) conversions.
    /// Logs at Debug level with property name and conversion direction.
    /// </summary>
    /// <param name="propertyName">The name of the TTL property being converted.</param>
    /// <param name="direction">The conversion direction ("ToDynamoDb" or "FromDynamoDb").</param>
    /// <returns>Generated logging code wrapped in conditional compilation directives.</returns>
    public static string GenerateTtlConversionLogging(string propertyName, string direction = "ToDynamoDb")
    {
        var sb = new StringBuilder();
        
        sb.AppendLine("            #if !DISABLE_DYNAMODB_LOGGING");
        sb.AppendLine("            if (logger?.IsEnabled(LogLevel.Debug) == true)");
        sb.AppendLine("            {");
        sb.AppendLine("                logger.LogDebug(LogEventIds.ConvertingTtl,");
        sb.AppendLine($"                    \"Converting TTL property {{PropertyName}} ({direction})\",");
        sb.AppendLine($"                    \"{propertyName}\");");
        sb.AppendLine("            }");
        sb.AppendLine("            #endif");
        
        return sb.ToString();
    }

    /// <summary>
    /// Generates logging code for JSON blob serialization/deserialization.
    /// Logs at Debug level with property name, type, and serializer.
    /// </summary>
    /// <param name="propertyName">The name of the JSON blob property.</param>
    /// <param name="propertyType">The type of the property being serialized/deserialized.</param>
    /// <param name="serializerType">The JSON serializer being used (e.g., "SystemTextJson", "NewtonsoftJson").</param>
    /// <param name="direction">The operation direction ("Serialization" or "Deserialization").</param>
    /// <returns>Generated logging code wrapped in conditional compilation directives.</returns>
    public static string GenerateJsonBlobLogging(string propertyName, string propertyType, string serializerType, string direction = "Serialization")
    {
        var sb = new StringBuilder();
        
        sb.AppendLine("            #if !DISABLE_DYNAMODB_LOGGING");
        sb.AppendLine("            if (logger?.IsEnabled(LogLevel.Debug) == true)");
        sb.AppendLine("            {");
        sb.AppendLine("                logger.LogDebug(LogEventIds.ConvertingJsonBlob,");
        sb.AppendLine($"                    \"JSON {direction} for {{PropertyName}} of type {{PropertyType}} using {{SerializerType}}\",");
        sb.AppendLine($"                    \"{propertyName}\", \"{propertyType}\", \"{serializerType}\");");
        sb.AppendLine("            }");
        sb.AppendLine("            #endif");
        
        return sb.ToString();
    }

    /// <summary>
    /// Generates logging code for blob reference operations.
    /// Logs at Debug level with property name, reference key, and provider type.
    /// </summary>
    /// <param name="propertyName">The name of the blob reference property.</param>
    /// <param name="referenceKeyExpression">Expression that evaluates to the blob reference key.</param>
    /// <param name="operation">The blob operation ("Store" or "Retrieve").</param>
    /// <returns>Generated logging code wrapped in conditional compilation directives.</returns>
    public static string GenerateBlobReferenceLogging(string propertyName, string referenceKeyExpression, string operation = "Store")
    {
        var sb = new StringBuilder();
        
        sb.AppendLine("            #if !DISABLE_DYNAMODB_LOGGING");
        sb.AppendLine("            if (logger?.IsEnabled(LogLevel.Debug) == true)");
        sb.AppendLine("            {");
        sb.AppendLine("                logger.LogDebug(LogEventIds.ConvertingBlobReference,");
        sb.AppendLine($"                    \"{operation} blob reference for {{PropertyName}} with key {{ReferenceKey}}\",");
        sb.AppendLine($"                    \"{propertyName}\", {referenceKeyExpression});");
        sb.AppendLine("            }");
        sb.AppendLine("            #endif");
        
        return sb.ToString();
    }

    /// <summary>
    /// Generates logging code for format string application during serialization.
    /// Logs at Debug level with property name, format string, and property type.
    /// </summary>
    /// <param name="propertyName">The name of the property having format applied.</param>
    /// <param name="formatString">The format string being applied.</param>
    /// <param name="propertyType">The type of the property.</param>
    /// <returns>Generated logging code wrapped in conditional compilation directives.</returns>
    public static string GenerateFormatStringApplicationLogging(string propertyName, string formatString, string propertyType)
    {
        var sb = new StringBuilder();
        
        sb.AppendLine("            #if !DISABLE_DYNAMODB_LOGGING");
        sb.AppendLine("            if (logger?.IsEnabled(LogLevel.Debug) == true)");
        sb.AppendLine("            {");
        sb.AppendLine("                logger.LogDebug(LogEventIds.ApplyingFormatString,");
        sb.AppendLine($"                    \"Applying format string '{{FormatString}}' to property {{PropertyName}} of type {{PropertyType}}\",");
        sb.AppendLine($"                    \"{formatString}\", \"{propertyName}\", \"{propertyType}\");");
        sb.AppendLine("            }");
        sb.AppendLine("            #endif");
        
        return sb.ToString();
    }

    /// <summary>
    /// Generates logging code for parsing formatted values during deserialization.
    /// Logs at Debug level with property name, format string, and property type.
    /// </summary>
    /// <param name="propertyName">The name of the property being parsed.</param>
    /// <param name="formatString">The format string being used for parsing.</param>
    /// <param name="propertyType">The type of the property.</param>
    /// <returns>Generated logging code wrapped in conditional compilation directives.</returns>
    public static string GenerateFormatStringParsingLogging(string propertyName, string formatString, string propertyType)
    {
        var sb = new StringBuilder();
        
        sb.AppendLine("            #if !DISABLE_DYNAMODB_LOGGING");
        sb.AppendLine("            if (logger?.IsEnabled(LogLevel.Debug) == true)");
        sb.AppendLine("            {");
        sb.AppendLine("                logger.LogDebug(LogEventIds.ParsingFormattedValue,");
        sb.AppendLine($"                    \"Parsing formatted value for property {{PropertyName}} of type {{PropertyType}} using format '{{FormatString}}'\",");
        sb.AppendLine($"                    \"{propertyName}\", \"{propertyType}\", \"{formatString}\");");
        sb.AppendLine("            }");
        sb.AppendLine("            #endif");
        
        return sb.ToString();
    }

    /// <summary>
    /// Generates error logging code for mapping failures.
    /// Logs at Error level with entity type, property name, and exception.
    /// </summary>
    /// <param name="entityTypeName">The name of the entity type where the error occurred.</param>
    /// <param name="propertyName">The name of the property that failed to map (optional).</param>
    /// <param name="exceptionVariableName">The name of the exception variable.</param>
    /// <returns>Generated logging code wrapped in conditional compilation directives.</returns>
    public static string GenerateMappingErrorLogging(string entityTypeName, string propertyName, string exceptionVariableName = "ex")
    {
        var sb = new StringBuilder();
        
        sb.AppendLine("            #if !DISABLE_DYNAMODB_LOGGING");
        if (!string.IsNullOrEmpty(propertyName))
        {
            sb.AppendLine("            logger?.LogError(LogEventIds.MappingError, ex,");
            sb.AppendLine($"                \"Failed to map property {{PropertyName}} for {{EntityType}}\",");
            sb.AppendLine($"                \"{propertyName}\", \"{entityTypeName}\");");
        }
        else
        {
            sb.AppendLine("            logger?.LogError(LogEventIds.MappingError, ex,");
            sb.AppendLine($"                \"Failed to map {{EntityType}}\",");
            sb.AppendLine($"                \"{entityTypeName}\");");
        }
        sb.AppendLine("            #endif");
        
        return sb.ToString();
    }

    /// <summary>
    /// Generates error logging code for type conversion failures.
    /// Logs at Error level with property name, source type, target type, and exception.
    /// </summary>
    /// <param name="propertyName">The name of the property that failed to convert.</param>
    /// <param name="sourceType">The source type being converted from.</param>
    /// <param name="targetType">The target type being converted to.</param>
    /// <param name="exceptionVariableName">The name of the exception variable.</param>
    /// <returns>Generated logging code wrapped in conditional compilation directives.</returns>
    public static string GenerateConversionErrorLogging(string propertyName, string sourceType, string targetType, string exceptionVariableName = "ex")
    {
        var sb = new StringBuilder();
        
        sb.AppendLine("            #if !DISABLE_DYNAMODB_LOGGING");
        sb.AppendLine($"            logger?.LogError(LogEventIds.ConversionError, {exceptionVariableName},");
        sb.AppendLine($"                \"Failed to convert {{PropertyName}} from {{SourceType}} to {{TargetType}}\",");
        sb.AppendLine($"                \"{propertyName}\", \"{sourceType}\", \"{targetType}\");");
        sb.AppendLine("            #endif");
        
        return sb.ToString();
    }

    /// <summary>
    /// Generates error logging code for JSON serialization failures.
    /// Logs at Error level with property name, type, serializer, and exception.
    /// </summary>
    /// <param name="propertyName">The name of the property that failed to serialize/deserialize.</param>
    /// <param name="propertyType">The type of the property.</param>
    /// <param name="serializerType">The JSON serializer being used.</param>
    /// <param name="exceptionVariableName">The name of the exception variable.</param>
    /// <returns>Generated logging code wrapped in conditional compilation directives.</returns>
    public static string GenerateJsonSerializationErrorLogging(string propertyName, string propertyType, string serializerType, string exceptionVariableName = "ex")
    {
        var sb = new StringBuilder();
        
        sb.AppendLine("            #if !DISABLE_DYNAMODB_LOGGING");
        sb.AppendLine($"            logger?.LogError(LogEventIds.JsonSerializationError, {exceptionVariableName},");
        sb.AppendLine($"                \"JSON serialization failed for {{PropertyName}} of type {{PropertyType}} using {{SerializerType}}\",");
        sb.AppendLine($"                \"{propertyName}\", \"{propertyType}\", \"{serializerType}\");");
        sb.AppendLine("            #endif");
        
        return sb.ToString();
    }

    /// <summary>
    /// Generates error logging code for blob storage failures.
    /// Logs at Error level with reference key, provider type, operation, and exception.
    /// </summary>
    /// <param name="propertyName">The name of the blob reference property.</param>
    /// <param name="referenceKeyExpression">Expression that evaluates to the blob reference key.</param>
    /// <param name="operation">The blob operation that failed ("Store" or "Retrieve").</param>
    /// <param name="exceptionVariableName">The name of the exception variable.</param>
    /// <returns>Generated logging code wrapped in conditional compilation directives.</returns>
    public static string GenerateBlobStorageErrorLogging(string propertyName, string referenceKeyExpression, string operation, string exceptionVariableName = "ex")
    {
        var sb = new StringBuilder();
        
        sb.AppendLine("            #if !DISABLE_DYNAMODB_LOGGING");
        sb.AppendLine($"            logger?.LogError(LogEventIds.BlobStorageError, {exceptionVariableName},");
        sb.AppendLine($"                \"Blob storage {operation} failed for {{PropertyName}} with key {{ReferenceKey}}\",");
        sb.AppendLine($"                \"{propertyName}\", {referenceKeyExpression});");
        sb.AppendLine("            #endif");
        
        return sb.ToString();
    }

    /// <summary>
    /// Generates code to redact sensitive fields from a DynamoDB item before logging.
    /// Uses the SensitiveDataRedactor utility to replace sensitive values with [REDACTED].
    /// </summary>
    /// <param name="itemVariableName">The name of the variable containing the DynamoDB item dictionary.</param>
    /// <param name="sensitiveFieldsExpression">Expression that evaluates to a HashSet of sensitive field names (e.g., "SensitiveFields").</param>
    /// <param name="redactedVariableName">The name of the variable to store the redacted item (default: "redactedItem").</param>
    /// <returns>Generated code that creates a redacted copy of the item.</returns>
    /// <remarks>
    /// This method generates code that calls SensitiveDataRedactor.RedactSensitiveFields to create
    /// a copy of the item with sensitive values replaced. The redacted item can then be safely logged.
    /// </remarks>
    public static string GenerateItemRedactionCode(
        string itemVariableName,
        string sensitiveFieldsExpression,
        string redactedVariableName = "redactedItem")
    {
        var sb = new StringBuilder();
        
        sb.AppendLine($"            var {redactedVariableName} = SensitiveDataRedactor.RedactSensitiveFields(");
        sb.AppendLine($"                {itemVariableName},");
        sb.AppendLine($"                {sensitiveFieldsExpression});");
        
        return sb.ToString();
    }

    /// <summary>
    /// Generates logging code for DynamoDB items with sensitive field redaction.
    /// Logs at the specified level with the item data, automatically redacting sensitive fields.
    /// </summary>
    /// <param name="logLevel">The log level (e.g., "Debug", "Trace").</param>
    /// <param name="eventId">The event ID constant (e.g., "LogEventIds.ExecutingPutItem").</param>
    /// <param name="message">The log message template.</param>
    /// <param name="itemVariableName">The name of the variable containing the DynamoDB item.</param>
    /// <param name="sensitiveFieldsExpression">Expression that evaluates to a HashSet of sensitive field names.</param>
    /// <param name="additionalArgs">Additional arguments to include in the log message (optional).</param>
    /// <returns>Generated logging code with redaction wrapped in conditional compilation directives.</returns>
    public static string GenerateItemLoggingWithRedaction(
        string logLevel,
        string eventId,
        string message,
        string itemVariableName,
        string sensitiveFieldsExpression,
        params string[] additionalArgs)
    {
        var sb = new StringBuilder();
        
        sb.AppendLine("            #if !DISABLE_DYNAMODB_LOGGING");
        sb.AppendLine($"            if (logger?.IsEnabled(LogLevel.{logLevel}) == true)");
        sb.AppendLine("            {");
        
        // Generate redaction code
        sb.AppendLine($"                var redactedItem = SensitiveDataRedactor.RedactSensitiveFields(");
        sb.AppendLine($"                    {itemVariableName},");
        sb.AppendLine($"                    {sensitiveFieldsExpression});");
        sb.AppendLine();
        
        // Generate logging call
        sb.AppendLine($"                logger.Log{logLevel}({eventId},");
        sb.AppendLine($"                    \"{message}\",");
        
        // Add item as first argument
        sb.Append($"                    redactedItem");
        
        // Add additional arguments
        foreach (var arg in additionalArgs)
        {
            sb.Append($", {arg}");
        }
        
        sb.AppendLine(");");
        sb.AppendLine("            }");
        sb.AppendLine("            #endif");
        
        return sb.ToString();
    }
}
