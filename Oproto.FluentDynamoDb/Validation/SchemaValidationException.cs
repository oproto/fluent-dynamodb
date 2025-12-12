namespace Oproto.FluentDynamoDb.Validation;

/// <summary>
/// Exception thrown when schema validation fails and ThrowOnError() is called.
/// </summary>
public class SchemaValidationException : Exception
{
    /// <summary>
    /// Gets the validation result containing all errors and warnings.
    /// </summary>
    public SchemaValidationResult ValidationResult { get; }
    
    /// <summary>
    /// Initializes a new instance of the <see cref="SchemaValidationException"/> class.
    /// </summary>
    /// <param name="result">The validation result containing errors and warnings.</param>
    public SchemaValidationException(SchemaValidationResult result)
        : base($"Schema validation failed with {result.Errors.Count} error(s)")
    {
        ValidationResult = result;
    }
    
    /// <summary>
    /// Initializes a new instance of the <see cref="SchemaValidationException"/> class
    /// with a custom message.
    /// </summary>
    /// <param name="message">The error message.</param>
    /// <param name="result">The validation result containing errors and warnings.</param>
    public SchemaValidationException(string message, SchemaValidationResult result)
        : base(message)
    {
        ValidationResult = result;
    }
    
    /// <summary>
    /// Initializes a new instance of the <see cref="SchemaValidationException"/> class
    /// with a custom message and inner exception.
    /// </summary>
    /// <param name="message">The error message.</param>
    /// <param name="result">The validation result containing errors and warnings.</param>
    /// <param name="innerException">The inner exception.</param>
    public SchemaValidationException(string message, SchemaValidationResult result, Exception innerException)
        : base(message, innerException)
    {
        ValidationResult = result;
    }
}
