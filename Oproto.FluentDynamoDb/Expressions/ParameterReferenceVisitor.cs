using System.Linq.Expressions;

namespace Oproto.FluentDynamoDb.Expressions;

/// <summary>
/// An expression visitor that checks if a specific parameter expression is referenced
/// within an expression tree. Used to validate that index expressions in list operations
/// do not reference the entity parameter.
/// </summary>
/// <remarks>
/// <para>
/// This visitor walks the expression tree and tracks whether the target parameter
/// is referenced anywhere in the tree. It is used to enforce the constraint that
/// list indices cannot depend on entity properties (which are not known at translation time).
/// </para>
/// <para>
/// <strong>Example Usage:</strong>
/// </para>
/// <code>
/// // Check if an index expression references the entity parameter
/// var visitor = new ParameterReferenceVisitor(entityParameter);
/// visitor.Visit(indexExpression);
/// if (visitor.ReferencesParameter)
/// {
///     throw new UnsupportedExpressionException("List index cannot reference the entity parameter.");
/// }
/// </code>
/// </remarks>
internal sealed class ParameterReferenceVisitor : ExpressionVisitor
{
    private readonly ParameterExpression _targetParameter;

    /// <summary>
    /// Gets a value indicating whether the target parameter was found in the expression tree.
    /// </summary>
    public bool ReferencesParameter { get; private set; }

    /// <summary>
    /// Initializes a new instance of the <see cref="ParameterReferenceVisitor"/> class.
    /// </summary>
    /// <param name="targetParameter">The parameter expression to search for.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="targetParameter"/> is null.</exception>
    public ParameterReferenceVisitor(ParameterExpression targetParameter)
    {
        _targetParameter = targetParameter ?? throw new ArgumentNullException(nameof(targetParameter));
    }

    /// <summary>
    /// Visits a parameter expression and checks if it matches the target parameter.
    /// </summary>
    /// <param name="node">The parameter expression to visit.</param>
    /// <returns>The visited expression.</returns>
    protected override Expression VisitParameter(ParameterExpression node)
    {
        if (node == _targetParameter)
        {
            ReferencesParameter = true;
        }
        return base.VisitParameter(node);
    }

    /// <summary>
    /// Checks if the specified expression references the target parameter.
    /// </summary>
    /// <param name="expression">The expression to check.</param>
    /// <param name="targetParameter">The parameter to search for.</param>
    /// <returns>True if the expression references the target parameter; otherwise, false.</returns>
    public static bool ContainsReference(Expression expression, ParameterExpression targetParameter)
    {
        var visitor = new ParameterReferenceVisitor(targetParameter);
        visitor.Visit(expression);
        return visitor.ReferencesParameter;
    }
}
