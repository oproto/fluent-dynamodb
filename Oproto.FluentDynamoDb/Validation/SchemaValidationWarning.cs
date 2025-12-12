namespace Oproto.FluentDynamoDb.Validation;

/// <summary>
/// Represents a non-critical schema validation warning.
/// </summary>
public class SchemaValidationWarning
{
    /// <summary>
    /// Gets the warning code for programmatic handling.
    /// </summary>
    public SchemaValidationWarningCode Code { get; }
    
    /// <summary>
    /// Gets the element that has the difference.
    /// </summary>
    public string Element { get; }
    
    /// <summary>
    /// Gets the human-readable warning message explaining why this may be acceptable.
    /// </summary>
    public string Message { get; }
    
    /// <summary>
    /// Initializes a new instance of the <see cref="SchemaValidationWarning"/> class.
    /// </summary>
    /// <param name="code">The warning code.</param>
    /// <param name="element">The element that has the difference.</param>
    /// <param name="message">The human-readable warning message.</param>
    public SchemaValidationWarning(
        SchemaValidationWarningCode code,
        string element,
        string message)
    {
        Code = code;
        Element = element;
        Message = message;
    }
    
    /// <summary>
    /// Returns a string representation of the warning.
    /// </summary>
    public override string ToString() => $"[{Code}] {Element}: {Message}";
}
