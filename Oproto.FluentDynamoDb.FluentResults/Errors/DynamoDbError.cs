using FluentResults;

namespace Oproto.FluentDynamoDb.FluentResults;

/// <summary>
/// Base class for all DynamoDB-related errors in FluentResults.
/// Provides a consistent error structure with error codes and inner exception support.
/// </summary>
public abstract class DynamoDbError : Error
{
    /// <summary>
    /// Gets the error code for programmatic error handling.
    /// </summary>
    public abstract string ErrorCode { get; }

    /// <summary>
    /// Gets the inner exception that caused this error, if any.
    /// </summary>
    public Exception? InnerException { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="DynamoDbError"/> class.
    /// </summary>
    /// <param name="message">The error message.</param>
    protected DynamoDbError(string message) : base(message)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="DynamoDbError"/> class with an inner exception.
    /// </summary>
    /// <param name="message">The error message.</param>
    /// <param name="innerException">The inner exception that caused this error.</param>
    protected DynamoDbError(string message, Exception? innerException) : base(message)
    {
        InnerException = innerException;
        if (innerException != null)
        {
            CausedBy(innerException);
        }
    }
}
