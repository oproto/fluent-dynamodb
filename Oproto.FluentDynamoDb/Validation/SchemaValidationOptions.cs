namespace Oproto.FluentDynamoDb.Validation;

/// <summary>
/// Options for schema validation behavior.
/// </summary>
public class SchemaValidationOptions
{
    /// <summary>
    /// Gets or sets the validation strictness level. Default is Relaxed.
    /// </summary>
    public ValidationStrictness Strictness { get; set; } = ValidationStrictness.Relaxed;
}

/// <summary>
/// Validation strictness levels.
/// </summary>
public enum ValidationStrictness
{
    /// <summary>
    /// Missing projection models for non-ALL indexes are warnings.
    /// </summary>
    Relaxed,
    
    /// <summary>
    /// Missing projection models for non-ALL indexes are errors.
    /// </summary>
    Strict
}
