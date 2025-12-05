namespace Oproto.FluentDynamoDb.Requests;

/// <summary>
/// Indicates the source of an update expression in UpdateItemRequestBuilder.
/// Used to detect and prevent mixing of string-based and expression-based Set() methods.
/// </summary>
public enum UpdateExpressionSource
{
    /// <summary>
    /// Update expression was set using string-based Set() methods.
    /// </summary>
    StringBased,

    /// <summary>
    /// Update expression was set using expression-based Set() methods with lambda expressions.
    /// </summary>
    ExpressionBased
}
