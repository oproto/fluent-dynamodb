namespace Oproto.FluentDynamoDb.FluentResults;

/// <summary>
/// Base class for validation-related DynamoDB errors.
/// </summary>
public abstract class ValidationError : DynamoDbError
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ValidationError"/> class.
    /// </summary>
    /// <param name="message">The error message.</param>
    protected ValidationError(string message) : base(message)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ValidationError"/> class with an inner exception.
    /// </summary>
    /// <param name="message">The error message.</param>
    /// <param name="innerException">The inner exception that caused this error.</param>
    protected ValidationError(string message, Exception? innerException) : base(message, innerException)
    {
    }
}

/// <summary>
/// Error indicating schema validation failure.
/// </summary>
public class SchemaValidationError : ValidationError
{
    /// <inheritdoc />
    public override string ErrorCode => "SCHEMA_VALIDATION_FAILED";

    /// <summary>
    /// Gets the list of validation errors.
    /// </summary>
    public IReadOnlyList<string> ValidationErrors { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="SchemaValidationError"/> class.
    /// </summary>
    /// <param name="validationErrors">The list of validation errors.</param>
    /// <param name="innerException">The inner exception that caused this error.</param>
    public SchemaValidationError(IEnumerable<string> validationErrors, Exception? innerException = null)
        : base($"Schema validation failed with {validationErrors.Count()} error(s)", innerException)
    {
        ValidationErrors = validationErrors.ToList().AsReadOnly();
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="SchemaValidationError"/> class with a single error.
    /// </summary>
    /// <param name="validationError">The validation error.</param>
    /// <param name="innerException">The inner exception that caused this error.</param>
    public SchemaValidationError(string validationError, Exception? innerException = null)
        : this(new[] { validationError }, innerException)
    {
    }
}

/// <summary>
/// Error indicating an empty collection was provided where a non-empty collection was expected.
/// </summary>
public class EmptyCollectionError : ValidationError
{
    /// <inheritdoc />
    public override string ErrorCode => "EMPTY_COLLECTION";

    /// <summary>
    /// Gets the parameter name that was empty.
    /// </summary>
    public string? ParameterName { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="EmptyCollectionError"/> class.
    /// </summary>
    /// <param name="parameterName">The parameter name that was empty.</param>
    /// <param name="innerException">The inner exception that caused this error.</param>
    public EmptyCollectionError(string? parameterName = null, Exception? innerException = null)
        : base(parameterName != null
            ? $"Collection '{parameterName}' cannot be empty"
            : "Collection cannot be empty", innerException)
    {
        ParameterName = parameterName;
    }
}

/// <summary>
/// Error indicating an invalid format string was provided.
/// </summary>
public class FormatStringError : ValidationError
{
    /// <inheritdoc />
    public override string ErrorCode => "FORMAT_STRING_ERROR";

    /// <summary>
    /// Gets the format details that caused the error.
    /// </summary>
    public string? FormatDetails { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="FormatStringError"/> class.
    /// </summary>
    /// <param name="message">The error message.</param>
    /// <param name="formatDetails">The format details that caused the error.</param>
    /// <param name="innerException">The inner exception that caused this error.</param>
    public FormatStringError(string message, string? formatDetails = null, Exception? innerException = null)
        : base(message, innerException)
    {
        FormatDetails = formatDetails;
    }
}
