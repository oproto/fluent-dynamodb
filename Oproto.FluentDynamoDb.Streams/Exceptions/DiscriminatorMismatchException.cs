namespace Oproto.FluentDynamoDb.Streams.Exceptions;

/// <summary>
/// Exception thrown when a discriminator value does not match the expected value for an entity type.
/// </summary>
/// <remarks>
/// <para>
/// This exception is thrown during entity deserialization when discriminator validation is enabled
/// and the actual discriminator value in the stream record doesn't match the expected value
/// configured for the entity type.
/// </para>
/// <para>
/// Discriminator validation ensures that entities are correctly typed in single-table designs.
/// A mismatch typically indicates:
/// - Data corruption or inconsistency in the table
/// - Incorrect discriminator configuration in entity attributes
/// - Migration issues when changing discriminator values
/// - Routing errors in multi-entity processing
/// </para>
/// <para>
/// The exception provides the field name, expected value, and actual value to help diagnose
/// the mismatch.
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
/// catch (DiscriminatorMismatchException ex)
/// {
///     _logger.LogError(ex,
///         "Discriminator mismatch on field {FieldName}: expected {Expected}, got {Actual}",
///         ex.FieldName,
///         ex.ExpectedValue,
///         ex.ActualValue);
/// }
/// </code>
/// </example>
public class DiscriminatorMismatchException : StreamProcessingException
{
    /// <summary>
    /// Gets the expected discriminator value.
    /// </summary>
    public string? ExpectedValue { get; }

    /// <summary>
    /// Gets the actual discriminator value found in the stream record.
    /// </summary>
    public string? ActualValue { get; }

    /// <summary>
    /// Gets the name of the discriminator field.
    /// </summary>
    public string? FieldName { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="DiscriminatorMismatchException"/> class.
    /// </summary>
    public DiscriminatorMismatchException()
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="DiscriminatorMismatchException"/> class with a specified error message.
    /// </summary>
    /// <param name="message">The message that describes the error.</param>
    public DiscriminatorMismatchException(string message) : base(message)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="DiscriminatorMismatchException"/> class with a specified error message
    /// and a reference to the inner exception that is the cause of this exception.
    /// </summary>
    /// <param name="message">The error message that explains the reason for the exception.</param>
    /// <param name="innerException">The exception that is the cause of the current exception.</param>
    public DiscriminatorMismatchException(string message, Exception innerException) : base(message, innerException)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="DiscriminatorMismatchException"/> class with discriminator context.
    /// </summary>
    /// <param name="message">The error message that explains the reason for the exception.</param>
    /// <param name="fieldName">The name of the discriminator field.</param>
    /// <param name="expectedValue">The expected discriminator value.</param>
    /// <param name="actualValue">The actual discriminator value found.</param>
    public DiscriminatorMismatchException(string message, string fieldName, string expectedValue, string actualValue)
        : base(message)
    {
        FieldName = fieldName;
        ExpectedValue = expectedValue;
        ActualValue = actualValue;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="DiscriminatorMismatchException"/> class with full context and inner exception.
    /// </summary>
    /// <param name="message">The error message that explains the reason for the exception.</param>
    /// <param name="fieldName">The name of the discriminator field.</param>
    /// <param name="expectedValue">The expected discriminator value.</param>
    /// <param name="actualValue">The actual discriminator value found.</param>
    /// <param name="innerException">The exception that is the cause of the current exception.</param>
    public DiscriminatorMismatchException(string message, string fieldName, string expectedValue, string actualValue, Exception innerException)
        : base(message, innerException)
    {
        FieldName = fieldName;
        ExpectedValue = expectedValue;
        ActualValue = actualValue;
    }
}
