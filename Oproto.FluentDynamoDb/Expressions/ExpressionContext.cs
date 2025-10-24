using Oproto.FluentDynamoDb.Requests;
using Oproto.FluentDynamoDb.Storage;

namespace Oproto.FluentDynamoDb.Expressions;

/// <summary>
/// Context for expression translation, tracking parameters and validation state.
/// </summary>
public class ExpressionContext
{
    /// <summary>
    /// The attribute value helper for parameter generation.
    /// </summary>
    public AttributeValueInternal AttributeValues { get; }
    
    /// <summary>
    /// The attribute name helper for reserved word handling.
    /// </summary>
    public AttributeNameInternal AttributeNames { get; }
    
    /// <summary>
    /// Entity metadata for property validation.
    /// </summary>
    public EntityMetadata? EntityMetadata { get; }
    
    /// <summary>
    /// Validation mode for the expression context.
    /// </summary>
    public ExpressionValidationMode ValidationMode { get; }
    
    /// <summary>
    /// Parameter generator for unique parameter names.
    /// </summary>
    public ParameterGenerator ParameterGenerator { get; }
    
    /// <summary>
    /// Initializes a new instance of the <see cref="ExpressionContext"/> class.
    /// </summary>
    /// <param name="attributeValues">The attribute value helper for parameter generation.</param>
    /// <param name="attributeNames">The attribute name helper for reserved word handling.</param>
    /// <param name="entityMetadata">Entity metadata for property validation.</param>
    /// <param name="validationMode">Validation mode for the expression context.</param>
    public ExpressionContext(
        AttributeValueInternal attributeValues,
        AttributeNameInternal attributeNames,
        EntityMetadata? entityMetadata,
        ExpressionValidationMode validationMode)
    {
        AttributeValues = attributeValues ?? throw new ArgumentNullException(nameof(attributeValues));
        AttributeNames = attributeNames ?? throw new ArgumentNullException(nameof(attributeNames));
        EntityMetadata = entityMetadata;
        ValidationMode = validationMode;
        ParameterGenerator = new ParameterGenerator();
    }
}
