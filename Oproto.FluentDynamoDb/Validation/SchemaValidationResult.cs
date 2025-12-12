using Oproto.FluentDynamoDb.Logging;

namespace Oproto.FluentDynamoDb.Validation;

/// <summary>
/// Result of schema validation containing errors and warnings.
/// </summary>
public class SchemaValidationResult
{
    private readonly List<SchemaValidationError> _errors;
    private readonly List<SchemaValidationWarning> _warnings;
    
    /// <summary>
    /// Gets whether the validation passed (no errors).
    /// </summary>
    public bool IsValid => _errors.Count == 0;
    
    /// <summary>
    /// Gets the collection of validation errors (critical mismatches).
    /// </summary>
    public IReadOnlyList<SchemaValidationError> Errors => _errors;
    
    /// <summary>
    /// Gets the collection of validation warnings (non-critical differences).
    /// </summary>
    public IReadOnlyList<SchemaValidationWarning> Warnings => _warnings;
    
    /// <summary>
    /// Initializes a new instance of the <see cref="SchemaValidationResult"/> class.
    /// </summary>
    public SchemaValidationResult()
    {
        _errors = new List<SchemaValidationError>();
        _warnings = new List<SchemaValidationWarning>();
    }
    
    /// <summary>
    /// Initializes a new instance of the <see cref="SchemaValidationResult"/> class
    /// with the specified errors and warnings.
    /// </summary>
    /// <param name="errors">The validation errors.</param>
    /// <param name="warnings">The validation warnings.</param>
    public SchemaValidationResult(
        IEnumerable<SchemaValidationError> errors,
        IEnumerable<SchemaValidationWarning> warnings)
    {
        _errors = errors.ToList();
        _warnings = warnings.ToList();
    }

    /// <summary>
    /// Adds an error to the validation result.
    /// </summary>
    /// <param name="error">The error to add.</param>
    internal void AddError(SchemaValidationError error)
    {
        _errors.Add(error);
    }
    
    /// <summary>
    /// Adds a warning to the validation result.
    /// </summary>
    /// <param name="warning">The warning to add.</param>
    internal void AddWarning(SchemaValidationWarning warning)
    {
        _warnings.Add(warning);
    }
    
    /// <summary>
    /// Throws <see cref="SchemaValidationException"/> if there are any errors.
    /// </summary>
    /// <exception cref="SchemaValidationException">Thrown when validation has errors.</exception>
    public void ThrowOnError()
    {
        if (!IsValid)
        {
            throw new SchemaValidationException(this);
        }
    }
    
    /// <summary>
    /// Logs all errors and warnings using the provided logger.
    /// Errors are logged at Error level, warnings at Warning level.
    /// </summary>
    /// <param name="logger">The logger to use for output.</param>
    public void LogResults(IDynamoDbLogger logger)
    {
        foreach (var error in _errors)
        {
            logger.LogError(
                LogEventIds.SchemaValidationError,
                "Schema validation error [{Code}] {Element}: {Message} (Expected: {Expected}, Actual: {Actual})",
                error.Code,
                error.Element,
                error.Message,
                error.Expected,
                error.Actual);
        }
        
        foreach (var warning in _warnings)
        {
            logger.LogWarning(
                LogEventIds.SchemaValidationWarning,
                "Schema validation warning [{Code}] {Element}: {Message}",
                warning.Code,
                warning.Element,
                warning.Message);
        }
    }
}
