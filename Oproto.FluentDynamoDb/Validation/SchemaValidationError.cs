namespace Oproto.FluentDynamoDb.Validation;

/// <summary>
/// Represents a critical schema validation error.
/// </summary>
public class SchemaValidationError
{
    /// <summary>
    /// Gets the error code for programmatic handling.
    /// </summary>
    public SchemaValidationErrorCode Code { get; }
    
    /// <summary>
    /// Gets the element that has the mismatch (table name, index name, attribute name).
    /// </summary>
    public string Element { get; }
    
    /// <summary>
    /// Gets the expected value from entity metadata.
    /// </summary>
    public string Expected { get; }
    
    /// <summary>
    /// Gets the actual value from DynamoDB table.
    /// </summary>
    public string Actual { get; }
    
    /// <summary>
    /// Gets the human-readable error message.
    /// </summary>
    public string Message { get; }
    
    /// <summary>
    /// Initializes a new instance of the <see cref="SchemaValidationError"/> class.
    /// </summary>
    /// <param name="code">The error code.</param>
    /// <param name="element">The element that has the mismatch.</param>
    /// <param name="expected">The expected value from entity metadata.</param>
    /// <param name="actual">The actual value from DynamoDB table.</param>
    /// <param name="message">The human-readable error message.</param>
    public SchemaValidationError(
        SchemaValidationErrorCode code,
        string element,
        string expected,
        string actual,
        string message)
    {
        Code = code;
        Element = element;
        Expected = expected;
        Actual = actual;
        Message = message;
    }
    
    /// <summary>
    /// Returns a string representation of the error.
    /// </summary>
    public override string ToString() => 
        $"[{Code}] {Element}: {Message} (Expected: {Expected}, Actual: {Actual})";
}
