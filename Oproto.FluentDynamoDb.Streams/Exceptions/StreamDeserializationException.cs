namespace Oproto.FluentDynamoDb.Streams.Exceptions;

/// <summary>
/// Exception thrown when deserialization of a stream record to an entity fails.
/// </summary>
/// <remarks>
/// <para>
/// This exception is thrown when the FromDynamoDbStream or FromStreamImage method fails to
/// deserialize a Lambda AttributeValue dictionary to a strongly-typed entity. Common causes include:
/// - Type conversion errors (e.g., string to number conversion failure)
/// - Missing required properties in the stream record
/// - Invalid data format in AttributeValue
/// - Encryption/decryption failures for encrypted fields
/// </para>
/// <para>
/// The exception provides context about which entity type and property (if applicable) caused
/// the failure, along with the underlying exception that triggered the error.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// try
/// {
///     await record.Process&lt;User&gt;()
///         .OnInsert(async (_, user) => await ProcessUser(user))
///         .ProcessAsync();
/// }
/// catch (StreamDeserializationException ex)
/// {
///     _logger.LogError(ex, 
///         "Failed to deserialize {EntityType}, property: {PropertyName}", 
///         ex.EntityType?.Name, 
///         ex.PropertyName);
/// }
/// </code>
/// </example>
public class StreamDeserializationException : StreamProcessingException
{
    /// <summary>
    /// Gets the type of entity that failed to deserialize.
    /// </summary>
    public Type? EntityType { get; }

    /// <summary>
    /// Gets the name of the property that caused the deserialization failure, if applicable.
    /// </summary>
    public string? PropertyName { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="StreamDeserializationException"/> class.
    /// </summary>
    public StreamDeserializationException()
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="StreamDeserializationException"/> class with a specified error message.
    /// </summary>
    /// <param name="message">The message that describes the error.</param>
    public StreamDeserializationException(string message) : base(message)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="StreamDeserializationException"/> class with a specified error message
    /// and a reference to the inner exception that is the cause of this exception.
    /// </summary>
    /// <param name="message">The error message that explains the reason for the exception.</param>
    /// <param name="innerException">The exception that is the cause of the current exception.</param>
    public StreamDeserializationException(string message, Exception innerException) : base(message, innerException)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="StreamDeserializationException"/> class with entity type context.
    /// </summary>
    /// <param name="message">The error message that explains the reason for the exception.</param>
    /// <param name="entityType">The type of entity that failed to deserialize.</param>
    /// <param name="innerException">The exception that is the cause of the current exception.</param>
    public StreamDeserializationException(string message, Type entityType, Exception innerException)
        : base(message, innerException)
    {
        EntityType = entityType;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="StreamDeserializationException"/> class with full context information.
    /// </summary>
    /// <param name="message">The error message that explains the reason for the exception.</param>
    /// <param name="entityType">The type of entity that failed to deserialize.</param>
    /// <param name="propertyName">The name of the property that caused the failure.</param>
    /// <param name="innerException">The exception that is the cause of the current exception.</param>
    public StreamDeserializationException(string message, Type entityType, string propertyName, Exception innerException)
        : base(message, innerException)
    {
        EntityType = entityType;
        PropertyName = propertyName;
    }
}
