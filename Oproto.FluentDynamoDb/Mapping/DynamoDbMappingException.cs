// Suppress AOT warnings for JSON serialization in exception ToString() - this is for debugging only
// and doesn't affect runtime behavior in AOT scenarios
#pragma warning disable IL2026 // RequiresUnreferencedCode - JSON serialization in exception details
#pragma warning disable IL3050 // RequiresDynamicCode - JSON serialization in exception details

using Amazon.DynamoDBv2.Model;
using System.Text.Json;

namespace Oproto.FluentDynamoDb.Mapping;

/// <summary>
/// Exception thrown when entity mapping operations fail.
/// Provides detailed context information for debugging mapping issues.
/// </summary>
public class DynamoDbMappingException : Exception
{
    /// <summary>
    /// Gets the entity type that was being mapped when the error occurred.
    /// </summary>
    public Type? EntityType { get; }

    /// <summary>
    /// Gets the DynamoDB item that caused the mapping failure.
    /// </summary>
    public Dictionary<string, AttributeValue>? DynamoDbItem { get; }

    /// <summary>
    /// Gets the property name that caused the mapping failure, if applicable.
    /// </summary>
    public string? PropertyName { get; }

    /// <summary>
    /// Gets the operation that was being performed when the error occurred.
    /// </summary>
    public MappingOperation Operation { get; }

    /// <summary>
    /// Gets additional context information about the mapping failure.
    /// </summary>
    public Dictionary<string, object> Context { get; }

    /// <summary>
    /// Initializes a new instance of the DynamoDbMappingException class.
    /// </summary>
    public DynamoDbMappingException() : this("Entity mapping operation failed.")
    {
    }

    /// <summary>
    /// Initializes a new instance of the DynamoDbMappingException class with a specified error message.
    /// </summary>
    /// <param name="message">The message that describes the error.</param>
    public DynamoDbMappingException(string message) : base(message)
    {
        Context = new Dictionary<string, object>();
        Operation = MappingOperation.Unknown;
    }

    /// <summary>
    /// Initializes a new instance of the DynamoDbMappingException class with a specified error message and inner exception.
    /// </summary>
    /// <param name="message">The message that describes the error.</param>
    /// <param name="innerException">The exception that is the cause of the current exception.</param>
    public DynamoDbMappingException(string message, Exception innerException) : base(message, innerException)
    {
        Context = new Dictionary<string, object>();
        Operation = MappingOperation.Unknown;
    }

    /// <summary>
    /// Initializes a new instance of the DynamoDbMappingException class with detailed context information.
    /// </summary>
    /// <param name="message">The message that describes the error.</param>
    /// <param name="entityType">The entity type being mapped.</param>
    /// <param name="operation">The mapping operation being performed.</param>
    /// <param name="dynamoDbItem">The DynamoDB item that caused the failure.</param>
    /// <param name="propertyName">The property name that caused the failure, if applicable.</param>
    /// <param name="innerException">The exception that is the cause of the current exception.</param>
    public DynamoDbMappingException(
        string message,
        Type? entityType = null,
        MappingOperation operation = MappingOperation.Unknown,
        Dictionary<string, AttributeValue>? dynamoDbItem = null,
        string? propertyName = null,
        Exception? innerException = null) : base(message, innerException)
    {
        EntityType = entityType;
        Operation = operation;
        DynamoDbItem = dynamoDbItem;
        PropertyName = propertyName;
        Context = new Dictionary<string, object>();
    }

    /// <summary>
    /// Creates a detailed error message with context information for debugging.
    /// </summary>
    /// <returns>A formatted error message with context details.</returns>
    /// <remarks>
    /// This method provides additional context information beyond the base exception.
    /// JSON serialization is wrapped in try-catch to handle any serialization failures gracefully.
    /// </remarks>
    public override string ToString()
    {
        var details = new List<string> { base.ToString() };

        if (EntityType != null)
        {
            details.Add($"Entity Type: {EntityType.FullName}");
        }

        if (Operation != MappingOperation.Unknown)
        {
            details.Add($"Operation: {Operation}");
        }

        if (!string.IsNullOrEmpty(PropertyName))
        {
            details.Add($"Property: {PropertyName}");
        }

        if (DynamoDbItem != null)
        {
            try
            {
                var itemJson = JsonSerializer.Serialize(
                    DynamoDbItem.ToDictionary(
                        kvp => kvp.Key,
                        kvp => ConvertAttributeValueToObject(kvp.Value)
                    ),
                    new JsonSerializerOptions { WriteIndented = true }
                );
                details.Add($"DynamoDB Item:\n{itemJson}");
            }
            catch (Exception ex)
            {
                details.Add($"DynamoDB Item: [Failed to serialize: {ex.Message}]");
            }
        }

        if (Context.Count > 0)
        {
            try
            {
                var contextJson = JsonSerializer.Serialize(Context, new JsonSerializerOptions { WriteIndented = true });
                details.Add($"Additional Context:\n{contextJson}");
            }
            catch (Exception ex)
            {
                details.Add($"Additional Context: [Failed to serialize: {ex.Message}]");
            }
        }

        return string.Join("\n\n", details);
    }

    /// <summary>
    /// Adds context information to the exception.
    /// </summary>
    /// <param name="key">The context key.</param>
    /// <param name="value">The context value.</param>
    /// <returns>This exception instance for method chaining.</returns>
    public DynamoDbMappingException WithContext(string key, object value)
    {
        Context[key] = value;
        return this;
    }

    /// <summary>
    /// Creates a mapping exception for property conversion failures.
    /// </summary>
    /// <param name="entityType">The entity type being mapped.</param>
    /// <param name="propertyName">The property that failed to convert.</param>
    /// <param name="attributeValue">The DynamoDB attribute value that couldn't be converted.</param>
    /// <param name="targetType">The target .NET type for conversion.</param>
    /// <param name="innerException">The underlying conversion exception.</param>
    /// <returns>A configured DynamoDbMappingException.</returns>
    public static DynamoDbMappingException PropertyConversionFailed(
        Type entityType,
        string propertyName,
        AttributeValue attributeValue,
        Type targetType,
        Exception innerException)
    {
        var message = $"Failed to convert DynamoDB attribute '{propertyName}' to {targetType.Name}. " +
                     $"Attribute type: {GetAttributeValueType(attributeValue)}, " +
                     $"Attribute value: {GetAttributeValueString(attributeValue)}";

        return new DynamoDbMappingException(
            message,
            entityType,
            MappingOperation.FromDynamoDb,
            propertyName: propertyName,
            innerException: innerException)
            .WithContext("TargetType", targetType.FullName ?? targetType.Name)
            .WithContext("AttributeType", GetAttributeValueType(attributeValue))
            .WithContext("AttributeValue", GetAttributeValueString(attributeValue));
    }

    /// <summary>
    /// Creates a mapping exception for entity construction failures.
    /// </summary>
    /// <param name="entityType">The entity type that failed to construct.</param>
    /// <param name="dynamoDbItem">The DynamoDB item being mapped.</param>
    /// <param name="innerException">The underlying construction exception.</param>
    /// <returns>A configured DynamoDbMappingException.</returns>
    public static DynamoDbMappingException EntityConstructionFailed(
        Type entityType,
        Dictionary<string, AttributeValue> dynamoDbItem,
        Exception innerException)
    {
        var message = $"Failed to construct entity of type {entityType.Name} from DynamoDB item. " +
                     $"Item contains {dynamoDbItem.Count} attributes.";

        return new DynamoDbMappingException(
            message,
            entityType,
            MappingOperation.FromDynamoDb,
            dynamoDbItem,
            innerException: innerException)
            .WithContext("AttributeCount", dynamoDbItem.Count)
            .WithContext("AttributeNames", dynamoDbItem.Keys.ToArray());
    }

    /// <summary>
    /// Creates a mapping exception for entity serialization failures.
    /// </summary>
    /// <param name="entityType">The entity type that failed to serialize.</param>
    /// <param name="entity">The entity instance being serialized.</param>
    /// <param name="propertyName">The property that caused the failure, if applicable.</param>
    /// <param name="innerException">The underlying serialization exception.</param>
    /// <returns>A configured DynamoDbMappingException.</returns>
    public static DynamoDbMappingException EntitySerializationFailed(
        Type entityType,
        object entity,
        string? propertyName = null,
        Exception? innerException = null)
    {
        var message = $"Failed to serialize entity of type {entityType.Name} to DynamoDB format.";
        if (!string.IsNullOrEmpty(propertyName))
        {
            message += $" Property '{propertyName}' caused the failure.";
        }

        return new DynamoDbMappingException(
            message,
            entityType,
            MappingOperation.ToDynamoDb,
            propertyName: propertyName,
            innerException: innerException)
            .WithContext("EntityInstance", entity.ToString() ?? "[null]");
    }

    /// <summary>
    /// Creates a mapping exception for key generation failures.
    /// </summary>
    /// <param name="entityType">The entity type for which key generation failed.</param>
    /// <param name="keyType">The type of key (partition or sort).</param>
    /// <param name="keyValue">The key value that caused the failure.</param>
    /// <param name="innerException">The underlying key generation exception.</param>
    /// <returns>A configured DynamoDbMappingException.</returns>
    public static DynamoDbMappingException KeyGenerationFailed(
        Type entityType,
        string keyType,
        object? keyValue,
        Exception innerException)
    {
        var message = $"Failed to generate {keyType} key for entity type {entityType.Name}. " +
                     $"Key value: {keyValue ?? "[null]"}";

        return new DynamoDbMappingException(
            message,
            entityType,
            MappingOperation.KeyGeneration,
            innerException: innerException)
            .WithContext("KeyType", keyType)
            .WithContext("KeyValue", keyValue?.ToString() ?? "[null]");
    }

    /// <summary>
    /// Creates a mapping exception for projection mapping failures.
    /// </summary>
    /// <param name="projectionType">The projection type that failed to map.</param>
    /// <param name="sourceEntityType">The source entity type name.</param>
    /// <param name="innerException">The underlying mapping exception.</param>
    /// <returns>A configured DynamoDbMappingException.</returns>
    public static DynamoDbMappingException ProjectionMappingFailed(
        Type projectionType,
        string sourceEntityType,
        Exception innerException)
    {
        var message = $"Failed to map DynamoDB item to projection type {projectionType.Name}. " +
                     $"Source entity: {sourceEntityType}";

        return new DynamoDbMappingException(
            message,
            projectionType,
            MappingOperation.FromDynamoDb,
            innerException: innerException)
            .WithContext("ProjectionType", projectionType.FullName ?? projectionType.Name)
            .WithContext("SourceEntityType", sourceEntityType);
    }

    /// <summary>
    /// Creates a mapping exception for required attribute missing.
    /// </summary>
    /// <param name="entityType">The entity type being mapped.</param>
    /// <param name="attributeName">The missing attribute name.</param>
    /// <param name="propertyName">The property name that requires the attribute.</param>
    /// <returns>A configured DynamoDbMappingException.</returns>
    public static DynamoDbMappingException RequiredAttributeMissing(
        Type entityType,
        string attributeName,
        string propertyName)
    {
        var message = $"Required attribute '{attributeName}' is missing from DynamoDB item. " +
                     $"Property '{propertyName}' on type {entityType.Name} is not nullable and requires this attribute.";

        return new DynamoDbMappingException(
            message,
            entityType,
            MappingOperation.FromDynamoDb,
            propertyName: propertyName)
            .WithContext("AttributeName", attributeName)
            .WithContext("PropertyName", propertyName);
    }

    private static string GetAttributeValueType(AttributeValue attributeValue)
    {
        if (attributeValue.S != null) return "String";
        if (attributeValue.N != null) return "Number";
        if (attributeValue.B != null) return "Binary";
        if (attributeValue.SS?.Count > 0) return "StringSet";
        if (attributeValue.NS?.Count > 0) return "NumberSet";
        if (attributeValue.BS?.Count > 0) return "BinarySet";
        if (attributeValue.M?.Count > 0) return "Map";
        if (attributeValue.L?.Count > 0) return "List";
        if (attributeValue.NULL == true) return "Null";
        if (attributeValue.BOOL != null) return "Boolean";
        return "Unknown";
    }

    private static string GetAttributeValueString(AttributeValue attributeValue)
    {
        try
        {
            if (attributeValue.S != null) return $"\"{attributeValue.S}\"";
            if (attributeValue.N != null) return attributeValue.N;
            if (attributeValue.BOOL != null) return attributeValue.BOOL.ToString()!;
            if (attributeValue.NULL == true) return "null";
            if (attributeValue.B != null) return $"[Binary data: {attributeValue.B.Length} bytes]";
            if (attributeValue.SS?.Count > 0) return $"[StringSet: {attributeValue.SS.Count} items]";
            if (attributeValue.NS?.Count > 0) return $"[NumberSet: {attributeValue.NS.Count} items]";
            if (attributeValue.BS?.Count > 0) return $"[BinarySet: {attributeValue.BS.Count} items]";
            if (attributeValue.M?.Count > 0) return $"[Map: {attributeValue.M.Count} attributes]";
            if (attributeValue.L?.Count > 0) return $"[List: {attributeValue.L.Count} items]";
            return "[Unknown]";
        }
        catch
        {
            return "[Error getting value]";
        }
    }

    private static object ConvertAttributeValueToObject(AttributeValue attributeValue)
    {
        if (attributeValue.S != null) return attributeValue.S;
        if (attributeValue.N != null) return attributeValue.N;
        if (attributeValue.BOOL != null) return attributeValue.BOOL;
        if (attributeValue.NULL == true) return "null";
        if (attributeValue.SS?.Count > 0) return attributeValue.SS;
        if (attributeValue.NS?.Count > 0) return attributeValue.NS;
        if (attributeValue.BS?.Count > 0) return "[Binary data]";
        if (attributeValue.M?.Count > 0) return attributeValue.M.ToDictionary(kvp => kvp.Key, kvp => ConvertAttributeValueToObject(kvp.Value));
        if (attributeValue.L?.Count > 0) return attributeValue.L.Select(ConvertAttributeValueToObject).ToArray();
        return "[Unknown]";
    }
}

/// <summary>
/// Represents the type of mapping operation that was being performed when an error occurred.
/// </summary>
public enum MappingOperation
{
    /// <summary>
    /// Unknown or unspecified operation.
    /// </summary>
    Unknown,

    /// <summary>
    /// Converting from DynamoDB AttributeValue dictionary to entity.
    /// </summary>
    FromDynamoDb,

    /// <summary>
    /// Converting from entity to DynamoDB AttributeValue dictionary.
    /// </summary>
    ToDynamoDb,

    /// <summary>
    /// Generating partition or sort keys.
    /// </summary>
    KeyGeneration,

    /// <summary>
    /// Validating entity structure or configuration.
    /// </summary>
    Validation,

    /// <summary>
    /// Multi-item entity grouping and reconstruction.
    /// </summary>
    MultiItemMapping,

    /// <summary>
    /// Related entity mapping and population.
    /// </summary>
    RelatedEntityMapping
}