namespace Oproto.FluentDynamoDb.Streams.Exceptions;

/// <summary>
/// Base exception class for all stream processing errors.
/// </summary>
/// <remarks>
/// <para>
/// This exception serves as the base class for all exceptions thrown during DynamoDB stream processing.
/// Specific error scenarios are represented by derived exception types that provide additional context.
/// </para>
/// <para>
/// Derived exception types include:
/// - <see cref="StreamDeserializationException"/>: Thrown when entity deserialization fails
/// - <see cref="StreamFilterException"/>: Thrown when filter predicate evaluation fails
/// - <see cref="DiscriminatorMismatchException"/>: Thrown when discriminator validation fails
/// </para>
/// <para>
/// Handler exceptions (exceptions thrown by user-provided event handlers) are not wrapped in
/// StreamProcessingException and are propagated directly to the caller.
/// </para>
/// </remarks>
public class StreamProcessingException : Exception
{
    /// <summary>
    /// Initializes a new instance of the <see cref="StreamProcessingException"/> class.
    /// </summary>
    public StreamProcessingException()
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="StreamProcessingException"/> class with a specified error message.
    /// </summary>
    /// <param name="message">The message that describes the error.</param>
    public StreamProcessingException(string message) : base(message)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="StreamProcessingException"/> class with a specified error message
    /// and a reference to the inner exception that is the cause of this exception.
    /// </summary>
    /// <param name="message">The error message that explains the reason for the exception.</param>
    /// <param name="innerException">The exception that is the cause of the current exception.</param>
    public StreamProcessingException(string message, Exception innerException) : base(message, innerException)
    {
    }
}
