using System.Linq.Expressions;

namespace Oproto.FluentDynamoDb.Expressions;

/// <summary>
/// Base exception for expression translation errors.
/// </summary>
public class ExpressionTranslationException : Exception
{
    /// <summary>
    /// Gets the original expression that caused the error, if available.
    /// </summary>
    public Expression? OriginalExpression { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="ExpressionTranslationException"/> class.
    /// </summary>
    /// <param name="message">The error message.</param>
    /// <param name="expression">The original expression that caused the error.</param>
    public ExpressionTranslationException(string message, Expression? expression = null)
        : base(message)
    {
        OriginalExpression = expression;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ExpressionTranslationException"/> class.
    /// </summary>
    /// <param name="message">The error message.</param>
    /// <param name="innerException">The inner exception.</param>
    /// <param name="expression">The original expression that caused the error.</param>
    public ExpressionTranslationException(string message, Exception innerException, Expression? expression = null)
        : base(message, innerException)
    {
        OriginalExpression = expression;
    }
}
