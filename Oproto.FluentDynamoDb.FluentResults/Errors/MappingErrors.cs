namespace Oproto.FluentDynamoDb.FluentResults;

/// <summary>
/// Error indicating a DynamoDB entity mapping failure.
/// </summary>
public class MappingError : DynamoDbError
{
    /// <inheritdoc />
    public override string ErrorCode => "MAPPING_ERROR";

    /// <summary>
    /// Gets the entity type that was being mapped when the error occurred.
    /// </summary>
    public string? EntityType { get; }

    /// <summary>
    /// Gets the field name that caused the mapping failure, if applicable.
    /// </summary>
    public string? FieldName { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="MappingError"/> class.
    /// </summary>
    /// <param name="message">The error message.</param>
    /// <param name="entityType">The entity type that was being mapped.</param>
    /// <param name="fieldName">The field name that caused the failure.</param>
    /// <param name="innerException">The inner exception that caused this error.</param>
    public MappingError(string message, string? entityType = null, string? fieldName = null, Exception? innerException = null)
        : base(message, innerException)
    {
        EntityType = entityType;
        FieldName = fieldName;
    }
}

/// <summary>
/// Error indicating a discriminator value mismatch during entity mapping.
/// </summary>
public class DiscriminatorMismatchError : MappingError
{
    /// <inheritdoc />
    public override string ErrorCode => "DISCRIMINATOR_MISMATCH";

    /// <summary>
    /// Gets the expected discriminator value.
    /// </summary>
    public string? ExpectedDiscriminator { get; }

    /// <summary>
    /// Gets the actual discriminator value found.
    /// </summary>
    public string? ActualDiscriminator { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="DiscriminatorMismatchError"/> class.
    /// </summary>
    /// <param name="message">The error message.</param>
    /// <param name="expectedDiscriminator">The expected discriminator value.</param>
    /// <param name="actualDiscriminator">The actual discriminator value found.</param>
    /// <param name="projectionType">The projection type being hydrated.</param>
    /// <param name="innerException">The inner exception that caused this error.</param>
    public DiscriminatorMismatchError(
        string message,
        string? expectedDiscriminator = null,
        string? actualDiscriminator = null,
        string? projectionType = null,
        Exception? innerException = null)
        : base(message, projectionType, fieldName: null, innerException)
    {
        ExpectedDiscriminator = expectedDiscriminator;
        ActualDiscriminator = actualDiscriminator;
    }
}

/// <summary>
/// Error indicating a GSI projection constraint validation failure.
/// </summary>
public class ProjectionValidationError : MappingError
{
    /// <inheritdoc />
    public override string ErrorCode => "PROJECTION_VALIDATION_FAILED";

    /// <summary>
    /// Gets the GSI name that has the projection constraint.
    /// </summary>
    public string? IndexName { get; }

    /// <summary>
    /// Gets the expected projection type for the GSI.
    /// </summary>
    public string? ExpectedType { get; }

    /// <summary>
    /// Gets the actual type that was used in the query.
    /// </summary>
    public string? ActualType { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="ProjectionValidationError"/> class.
    /// </summary>
    /// <param name="message">The error message.</param>
    /// <param name="indexName">The GSI name that has the projection constraint.</param>
    /// <param name="expectedType">The expected projection type for the GSI.</param>
    /// <param name="actualType">The actual type that was used in the query.</param>
    /// <param name="innerException">The inner exception that caused this error.</param>
    public ProjectionValidationError(
        string message,
        string? indexName = null,
        string? expectedType = null,
        string? actualType = null,
        Exception? innerException = null)
        : base(message, entityType: actualType, fieldName: null, innerException)
    {
        IndexName = indexName;
        ExpectedType = expectedType;
        ActualType = actualType;
    }
}

/// <summary>
/// Error indicating an expression translation failure.
/// </summary>
public class ExpressionTranslationError : MappingError
{
    /// <inheritdoc />
    public override string ErrorCode => "EXPRESSION_TRANSLATION_FAILED";

    /// <summary>
    /// Gets the original expression that caused the error, if available.
    /// </summary>
    public string? OriginalExpression { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="ExpressionTranslationError"/> class.
    /// </summary>
    /// <param name="message">The error message.</param>
    /// <param name="originalExpression">The original expression that caused the error.</param>
    /// <param name="innerException">The inner exception that caused this error.</param>
    public ExpressionTranslationError(
        string message,
        string? originalExpression = null,
        Exception? innerException = null)
        : base(message, entityType: null, fieldName: null, innerException)
    {
        OriginalExpression = originalExpression;
    }
}
