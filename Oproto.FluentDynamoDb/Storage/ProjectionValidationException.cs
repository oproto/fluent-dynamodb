namespace Oproto.FluentDynamoDb.Storage;

/// <summary>
/// Exception thrown when GSI projection constraints are violated.
/// This occurs when a query attempts to use a type that doesn't match the required projection type for a GSI.
/// </summary>
public class ProjectionValidationException : Exception
{
    /// <summary>
    /// Gets the GSI name that has the projection constraint.
    /// </summary>
    public string? IndexName { get; }

    /// <summary>
    /// Gets the expected projection type for the GSI.
    /// </summary>
    public Type? ExpectedType { get; }

    /// <summary>
    /// Gets the actual type that was used in the query.
    /// </summary>
    public Type? ActualType { get; }

    /// <summary>
    /// Initializes a new instance of the ProjectionValidationException class.
    /// </summary>
    public ProjectionValidationException() : this("GSI projection constraint validation failed.")
    {
    }

    /// <summary>
    /// Initializes a new instance of the ProjectionValidationException class with a specified error message.
    /// </summary>
    /// <param name="message">The message that describes the error.</param>
    public ProjectionValidationException(string message) : base(message)
    {
    }

    /// <summary>
    /// Initializes a new instance of the ProjectionValidationException class with a specified error message and inner exception.
    /// </summary>
    /// <param name="message">The message that describes the error.</param>
    /// <param name="innerException">The exception that is the cause of the current exception.</param>
    public ProjectionValidationException(string message, Exception innerException) : base(message, innerException)
    {
    }

    /// <summary>
    /// Initializes a new instance of the ProjectionValidationException class with detailed context information.
    /// </summary>
    /// <param name="message">The message that describes the error.</param>
    /// <param name="indexName">The GSI name that has the projection constraint.</param>
    /// <param name="expectedType">The expected projection type for the GSI.</param>
    /// <param name="actualType">The actual type that was used in the query.</param>
    public ProjectionValidationException(
        string message,
        string? indexName = null,
        Type? expectedType = null,
        Type? actualType = null) : base(message)
    {
        IndexName = indexName;
        ExpectedType = expectedType;
        ActualType = actualType;
    }

    /// <summary>
    /// Creates a projection validation exception for GSI projection constraint violations.
    /// </summary>
    /// <param name="indexName">The GSI name that has the projection constraint.</param>
    /// <param name="expectedType">The expected projection type for the GSI.</param>
    /// <param name="actualType">The actual type that was used in the query.</param>
    /// <returns>A configured ProjectionValidationException.</returns>
    public static ProjectionValidationException GsiProjectionMismatch(
        string indexName,
        Type expectedType,
        Type actualType)
    {
        var message = $"GSI '{indexName}' requires projection type '{expectedType.Name}' but query uses '{actualType.Name}'. " +
                     $"Either use the correct projection type or remove the [UseProjection] constraint from the GSI definition.";

        return new ProjectionValidationException(
            message,
            indexName,
            expectedType,
            actualType);
    }

    /// <summary>
    /// Creates a detailed error message with context information for debugging.
    /// </summary>
    /// <returns>A formatted error message with context details.</returns>
    public override string ToString()
    {
        var details = new List<string> { base.ToString() };

        if (!string.IsNullOrEmpty(IndexName))
        {
            details.Add($"Index Name: {IndexName}");
        }

        if (ExpectedType != null)
        {
            details.Add($"Expected Type: {ExpectedType.FullName ?? ExpectedType.Name}");
        }

        if (ActualType != null)
        {
            details.Add($"Actual Type: {ActualType.FullName ?? ActualType.Name}");
        }

        return string.Join("\n", details);
    }
}
