using System.Linq.Expressions;

namespace Oproto.FluentDynamoDb.Expressions;

/// <summary>
/// Thrown when an expression references an unmapped property.
/// </summary>
public class UnmappedPropertyException : ExpressionTranslationException
{
    /// <summary>
    /// Gets the name of the property that is not mapped.
    /// </summary>
    public string PropertyName { get; }

    /// <summary>
    /// Gets the entity type that contains the unmapped property.
    /// </summary>
    public Type EntityType { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="UnmappedPropertyException"/> class.
    /// </summary>
    /// <param name="propertyName">The name of the unmapped property.</param>
    /// <param name="entityType">The entity type.</param>
    /// <param name="expression">The original expression.</param>
    public UnmappedPropertyException(string propertyName, Type entityType, Expression? expression = null)
        : base(
            $"Property '{propertyName}' on type '{entityType.Name}' does not map to a DynamoDB attribute. " +
            $"Ensure the property has a [DynamoDbAttribute] or is included in entity configuration.",
            expression)
    {
        PropertyName = propertyName;
        EntityType = entityType;
    }
}
