using System.Linq.Expressions;

namespace Oproto.FluentDynamoDb.Expressions;

/// <summary>
/// Thrown when an expression uses an unsupported operator or method.
/// </summary>
public class UnsupportedExpressionException : ExpressionTranslationException
{
    /// <summary>
    /// Gets the expression type that is not supported, if applicable.
    /// </summary>
    public ExpressionType? ExpressionType { get; }

    /// <summary>
    /// Gets the method name that is not supported, if applicable.
    /// </summary>
    public string? MethodName { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="UnsupportedExpressionException"/> class.
    /// </summary>
    /// <param name="message">The error message.</param>
    /// <param name="expression">The original expression.</param>
    public UnsupportedExpressionException(string message, Expression? expression = null)
        : base(message, expression)
    {
        if (expression != null)
        {
            ExpressionType = expression.NodeType;
            
            if (expression is MethodCallExpression methodCall)
            {
                MethodName = methodCall.Method.Name;
            }
        }
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="UnsupportedExpressionException"/> class.
    /// </summary>
    /// <param name="message">The error message.</param>
    /// <param name="expressionType">The unsupported expression type.</param>
    /// <param name="expression">The original expression.</param>
    public UnsupportedExpressionException(string message, ExpressionType expressionType, Expression? expression = null)
        : base(message, expression)
    {
        ExpressionType = expressionType;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="UnsupportedExpressionException"/> class.
    /// </summary>
    /// <param name="message">The error message.</param>
    /// <param name="methodName">The unsupported method name.</param>
    /// <param name="expression">The original expression.</param>
    public UnsupportedExpressionException(string message, string methodName, Expression? expression = null)
        : base(message, expression)
    {
        MethodName = methodName;
    }
}
