namespace Oproto.FluentDynamoDb.Expressions;

/// <summary>
/// Validation mode for expression translation.
/// </summary>
public enum ExpressionValidationMode
{
    /// <summary>
    /// No validation - any property can be referenced (for filter/condition expressions).
    /// </summary>
    None,
    
    /// <summary>
    /// Key-only validation - only partition key and sort key properties allowed (for Query().Where()).
    /// </summary>
    KeysOnly
}
