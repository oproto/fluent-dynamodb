using System.Linq.Expressions;

namespace Oproto.FluentDynamoDb.Expressions;

/// <summary>
/// Thrown when a Query().Where() expression references non-key attributes.
/// </summary>
public class InvalidKeyExpressionException : ExpressionTranslationException
{
    /// <summary>
    /// Gets the name of the property that is not a key attribute.
    /// </summary>
    public string PropertyName { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="InvalidKeyExpressionException"/> class.
    /// </summary>
    /// <param name="propertyName">The name of the non-key property.</param>
    /// <param name="expression">The original expression.</param>
    public InvalidKeyExpressionException(string propertyName, Expression? expression = null)
        : base(
            $"Property '{propertyName}' is not a key attribute and cannot be used in Query().Where(). " +
            $"Use WithFilter() to filter on non-key attributes.",
            expression)
    {
        PropertyName = propertyName;
    }
}
