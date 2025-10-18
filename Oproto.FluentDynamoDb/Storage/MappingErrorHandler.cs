using Amazon.DynamoDBv2.Model;

namespace Oproto.FluentDynamoDb.Storage;

/// <summary>
/// Provides centralized error handling utilities for DynamoDB entity mapping operations.
/// This class helps generate consistent, detailed error messages for debugging mapping issues.
/// </summary>
public static class MappingErrorHandler
{
    /// <summary>
    /// Handles property conversion errors with detailed context information.
    /// </summary>
    /// <typeparam name="T">The target entity type.</typeparam>
    /// <param name="propertyName">The name of the property that failed to convert.</param>
    /// <param name="attributeValue">The DynamoDB attribute value that couldn't be converted.</param>
    /// <param name="targetType">The target .NET type for conversion.</param>
    /// <param name="innerException">The underlying conversion exception.</param>
    /// <returns>A configured DynamoDbMappingException with detailed context.</returns>
    public static DynamoDbMappingException HandlePropertyConversionError<T>(
        string propertyName,
        AttributeValue attributeValue,
        Type targetType,
        Exception innerException)
    {
        return DynamoDbMappingException.PropertyConversionFailed(
            typeof(T),
            propertyName,
            attributeValue,
            targetType,
            innerException);
    }

    /// <summary>
    /// Handles entity construction errors with detailed context information.
    /// </summary>
    /// <typeparam name="T">The target entity type.</typeparam>
    /// <param name="dynamoDbItem">The DynamoDB item that caused the construction failure.</param>
    /// <param name="innerException">The underlying construction exception.</param>
    /// <returns>A configured DynamoDbMappingException with detailed context.</returns>
    public static DynamoDbMappingException HandleEntityConstructionError<T>(
        Dictionary<string, AttributeValue> dynamoDbItem,
        Exception innerException)
    {
        return DynamoDbMappingException.EntityConstructionFailed(
            typeof(T),
            dynamoDbItem,
            innerException);
    }

    /// <summary>
    /// Handles entity serialization errors with detailed context information.
    /// </summary>
    /// <typeparam name="T">The source entity type.</typeparam>
    /// <param name="entity">The entity instance that failed to serialize.</param>
    /// <param name="propertyName">The property that caused the failure, if applicable.</param>
    /// <param name="innerException">The underlying serialization exception.</param>
    /// <returns>A configured DynamoDbMappingException with detailed context.</returns>
    public static DynamoDbMappingException HandleEntitySerializationError<T>(
        T entity,
        string? propertyName = null,
        Exception? innerException = null)
    {
        return DynamoDbMappingException.EntitySerializationFailed(
            typeof(T),
            entity!,
            propertyName,
            innerException);
    }

    /// <summary>
    /// Handles key generation errors with detailed context information.
    /// </summary>
    /// <typeparam name="T">The entity type for which key generation failed.</typeparam>
    /// <param name="keyType">The type of key (partition or sort).</param>
    /// <param name="keyValue">The key value that caused the failure.</param>
    /// <param name="innerException">The underlying key generation exception.</param>
    /// <returns>A configured DynamoDbMappingException with detailed context.</returns>
    public static DynamoDbMappingException HandleKeyGenerationError<T>(
        string keyType,
        object? keyValue,
        Exception innerException)
    {
        return DynamoDbMappingException.KeyGenerationFailed(
            typeof(T),
            keyType,
            keyValue,
            innerException);
    }

    /// <summary>
    /// Validates that a DynamoDB item contains required attributes for entity mapping.
    /// </summary>
    /// <param name="item">The DynamoDB item to validate.</param>
    /// <param name="requiredAttributes">The list of required attribute names.</param>
    /// <param name="entityType">The target entity type.</param>
    /// <exception cref="DynamoDbMappingException">Thrown when required attributes are missing.</exception>
    public static void ValidateRequiredAttributes(
        Dictionary<string, AttributeValue> item,
        string[] requiredAttributes,
        Type entityType)
    {
        var missingAttributes = requiredAttributes.Where(attr => !item.ContainsKey(attr)).ToArray();
        
        if (missingAttributes.Length > 0)
        {
            var message = $"DynamoDB item is missing required attributes for {entityType.Name}: {string.Join(", ", missingAttributes)}";
            
            throw new DynamoDbMappingException(
                message,
                entityType,
                MappingOperation.FromDynamoDb,
                item)
                .WithContext("MissingAttributes", missingAttributes)
                .WithContext("AvailableAttributes", item.Keys.ToArray());
        }
    }

    /// <summary>
    /// Validates that an entity has required properties set before serialization.
    /// </summary>
    /// <typeparam name="T">The entity type.</typeparam>
    /// <param name="entity">The entity instance to validate.</param>
    /// <param name="requiredProperties">The list of required property names.</param>
    /// <exception cref="DynamoDbMappingException">Thrown when required properties are null or empty.</exception>
    public static void ValidateRequiredProperties<T>(T entity, string[] requiredProperties)
    {
        if (entity == null)
        {
            throw new ArgumentNullException(nameof(entity));
        }

        var entityType = typeof(T);
        var missingProperties = new List<string>();

        foreach (var propertyName in requiredProperties)
        {
            var property = entityType.GetProperty(propertyName);
            if (property == null)
            {
                missingProperties.Add($"{propertyName} (property not found)");
                continue;
            }

            var value = property.GetValue(entity);
            if (value == null || (value is string str && string.IsNullOrEmpty(str)))
            {
                missingProperties.Add(propertyName);
            }
        }

        if (missingProperties.Count > 0)
        {
            var message = $"Entity {entityType.Name} has null or empty required properties: {string.Join(", ", missingProperties)}";
            
            throw new DynamoDbMappingException(
                message,
                entityType,
                MappingOperation.ToDynamoDb)
                .WithContext("MissingProperties", missingProperties.ToArray())
                .WithContext("EntityInstance", entity.ToString() ?? "[null]");
        }
    }

    /// <summary>
    /// Creates a detailed error message for debugging mapping failures.
    /// </summary>
    /// <param name="operation">The mapping operation that failed.</param>
    /// <param name="entityType">The entity type involved in the operation.</param>
    /// <param name="additionalContext">Additional context information.</param>
    /// <returns>A formatted error message with context details.</returns>
    public static string CreateDetailedErrorMessage(
        MappingOperation operation,
        Type entityType,
        Dictionary<string, object>? additionalContext = null)
    {
        var message = $"DynamoDB mapping operation failed:\n";
        message += $"  Operation: {operation}\n";
        message += $"  Entity Type: {entityType.FullName}\n";
        message += $"  Timestamp: {DateTimeOffset.UtcNow:yyyy-MM-dd HH:mm:ss.fff UTC}\n";

        if (additionalContext?.Count > 0)
        {
            message += "  Additional Context:\n";
            foreach (var kvp in additionalContext)
            {
                message += $"    {kvp.Key}: {kvp.Value}\n";
            }
        }

        return message;
    }

    /// <summary>
    /// Wraps an operation with comprehensive error handling.
    /// </summary>
    /// <typeparam name="T">The return type of the operation.</typeparam>
    /// <param name="operation">The operation to execute.</param>
    /// <param name="operationType">The type of mapping operation being performed.</param>
    /// <param name="entityType">The entity type involved in the operation.</param>
    /// <param name="context">Additional context information.</param>
    /// <returns>The result of the operation.</returns>
    /// <exception cref="DynamoDbMappingException">Thrown when the operation fails.</exception>
    public static T ExecuteWithErrorHandling<T>(
        Func<T> operation,
        MappingOperation operationType,
        Type entityType,
        Dictionary<string, object>? context = null)
    {
        try
        {
            return operation();
        }
        catch (DynamoDbMappingException)
        {
            // Re-throw mapping exceptions as-is
            throw;
        }
        catch (Exception ex)
        {
            var message = CreateDetailedErrorMessage(operationType, entityType, context);
            
            var mappingException = new DynamoDbMappingException(
                message,
                entityType,
                operationType,
                innerException: ex);

            if (context != null)
            {
                foreach (var kvp in context)
                {
                    mappingException.WithContext(kvp.Key, kvp.Value);
                }
            }

            throw mappingException;
        }
    }

    /// <summary>
    /// Wraps an async operation with comprehensive error handling.
    /// </summary>
    /// <typeparam name="T">The return type of the operation.</typeparam>
    /// <param name="operation">The async operation to execute.</param>
    /// <param name="operationType">The type of mapping operation being performed.</param>
    /// <param name="entityType">The entity type involved in the operation.</param>
    /// <param name="context">Additional context information.</param>
    /// <returns>The result of the operation.</returns>
    /// <exception cref="DynamoDbMappingException">Thrown when the operation fails.</exception>
    public static async Task<T> ExecuteWithErrorHandlingAsync<T>(
        Func<Task<T>> operation,
        MappingOperation operationType,
        Type entityType,
        Dictionary<string, object>? context = null)
    {
        try
        {
            return await operation();
        }
        catch (DynamoDbMappingException)
        {
            // Re-throw mapping exceptions as-is
            throw;
        }
        catch (Exception ex)
        {
            var message = CreateDetailedErrorMessage(operationType, entityType, context);
            
            var mappingException = new DynamoDbMappingException(
                message,
                entityType,
                operationType,
                innerException: ex);

            if (context != null)
            {
                foreach (var kvp in context)
                {
                    mappingException.WithContext(kvp.Key, kvp.Value);
                }
            }

            throw mappingException;
        }
    }
}