namespace Oproto.FluentDynamoDb.Streams.Exceptions;

/// <summary>
/// Exception thrown when a filter predicate evaluation fails during stream processing.
/// </summary>
/// <remarks>
/// <para>
/// This exception is thrown when a Where or WhereKey predicate throws an exception during evaluation.
/// Common causes include:
/// - Null reference exceptions in predicate logic
/// - Invalid property access on null objects
/// - Type conversion errors in filter expressions
/// - Exceptions thrown by custom filter logic
/// </para>
/// <para>
/// The exception wraps the original exception and provides context about which filter
/// (Where or WhereKey) caused the failure.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// try
/// {
///     await record.Process&lt;User&gt;()
///         .Where(u => u.Email.Contains("@"))  // May throw if Email is null
///         .OnInsert(async (_, user) => await ProcessUser(user))
///         .ProcessAsync();
/// }
/// catch (StreamFilterException ex)
/// {
///     _logger.LogError(ex, 
///         "Filter evaluation failed: {FilterExpression}", 
///         ex.FilterExpression);
/// }
/// </code>
/// </example>
public class StreamFilterException : StreamProcessingException
{
    /// <summary>
    /// Gets the filter expression that caused the failure, if available.
    /// </summary>
    public string? FilterExpression { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="StreamFilterException"/> class.
    /// </summary>
    public StreamFilterException()
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="StreamFilterException"/> class with a specified error message.
    /// </summary>
    /// <param name="message">The message that describes the error.</param>
    public StreamFilterException(string message) : base(message)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="StreamFilterException"/> class with a specified error message
    /// and a reference to the inner exception that is the cause of this exception.
    /// </summary>
    /// <param name="message">The error message that explains the reason for the exception.</param>
    /// <param name="innerException">The exception that is the cause of the current exception.</param>
    public StreamFilterException(string message, Exception innerException) : base(message, innerException)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="StreamFilterException"/> class with filter context.
    /// </summary>
    /// <param name="message">The error message that explains the reason for the exception.</param>
    /// <param name="filterExpression">The filter expression that caused the failure.</param>
    /// <param name="innerException">The exception that is the cause of the current exception.</param>
    public StreamFilterException(string message, string filterExpression, Exception innerException)
        : base(message, innerException)
    {
        FilterExpression = filterExpression;
    }
}
