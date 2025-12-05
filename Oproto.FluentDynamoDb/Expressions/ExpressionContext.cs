using Oproto.FluentDynamoDb.Metadata;
using Oproto.FluentDynamoDb.Requests;

namespace Oproto.FluentDynamoDb.Expressions;

/// <summary>
/// Context for expression translation, tracking parameters and validation state.
/// </summary>
/// <remarks>
/// <para>
/// The ExpressionContext maintains state during expression translation, including:
/// </para>
/// <list type="bullet">
/// <item><description>Attribute value parameters (:p0, :p1, etc.)</description></item>
/// <item><description>Attribute name placeholders (#attr0, #attr1, etc.)</description></item>
/// <item><description>Entity metadata for property validation</description></item>
/// <item><description>Validation mode (KeysOnly for Query().Where(), None for filters)</description></item>
/// </list>
/// 
/// <para><strong>Validation Modes:</strong></para>
/// <list type="table">
/// <listheader><term>Mode</term><description>Usage</description><description>Restrictions</description></listheader>
/// <item><term>KeysOnly</term><description>Query().Where()</description><description>Only partition key and sort key properties allowed</description></item>
/// <item><term>None</term><description>WithFilter(), WithCondition()</description><description>Any property can be referenced</description></item>
/// </list>
/// 
/// <para><strong>Thread Safety:</strong></para>
/// <para>
/// ExpressionContext instances are not thread-safe and should not be shared across concurrent
/// expression translations. Each translation should use its own context instance.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// // Create context for Query().Where() with key-only validation
/// var context = new ExpressionContext(
///     attributeValues,
///     attributeNames,
///     entityMetadata,
///     ExpressionValidationMode.KeysOnly);
/// 
/// // Create context for WithFilter() with no restrictions
/// var filterContext = new ExpressionContext(
///     attributeValues,
///     attributeNames,
///     entityMetadata,
///     ExpressionValidationMode.None);
/// 
/// // Create context without metadata (validation skipped)
/// var noValidationContext = new ExpressionContext(
///     attributeValues,
///     attributeNames,
///     null,
///     ExpressionValidationMode.None);
/// </code>
/// </example>
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
    /// The name of the Global Secondary Index being queried, if any.
    /// When set, key validation uses the GSI's key schema instead of the main table's keys.
    /// </summary>
    public string? IndexName { get; }
    
    /// <summary>
    /// Parameter generator for unique parameter names.
    /// Shared with AttributeValues to ensure uniqueness across all parameter generation methods.
    /// </summary>
    public ParameterGenerator ParameterGenerator => AttributeValues.ParameterGenerator;
    
    /// <summary>
    /// Gets the list of parameter metadata for tracking encryption requirements.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This list tracks parameters that require encryption during update expression translation.
    /// The UpdateExpressionTranslator adds entries when it encounters encrypted properties,
    /// and the request builder (e.g., UpdateItemRequestBuilder) uses this list to encrypt
    /// parameters before sending the request to DynamoDB.
    /// </para>
    /// 
    /// <para><strong>Usage Pattern:</strong></para>
    /// <list type="number">
    /// <item><description>Translator detects encrypted property from PropertyMetadata.IsEncrypted</description></item>
    /// <item><description>Translator creates ParameterMetadata with RequiresEncryption = true</description></item>
    /// <item><description>Translator adds to this list</description></item>
    /// <item><description>Request builder iterates this list and encrypts marked parameters</description></item>
    /// </list>
    /// </remarks>
    public List<ParameterMetadata> ParameterMetadata { get; } = new List<ParameterMetadata>();
    
    /// <summary>
    /// Initializes a new instance of the <see cref="ExpressionContext"/> class.
    /// </summary>
    /// <param name="attributeValues">The attribute value helper for parameter generation.</param>
    /// <param name="attributeNames">The attribute name helper for reserved word handling.</param>
    /// <param name="entityMetadata">Entity metadata for property validation.</param>
    /// <param name="validationMode">Validation mode for the expression context.</param>
    /// <param name="indexName">Optional GSI name for GSI key validation.</param>
    public ExpressionContext(
        AttributeValueInternal attributeValues,
        AttributeNameInternal attributeNames,
        EntityMetadata? entityMetadata,
        ExpressionValidationMode validationMode,
        string? indexName = null)
    {
        AttributeValues = attributeValues ?? throw new ArgumentNullException(nameof(attributeValues));
        AttributeNames = attributeNames ?? throw new ArgumentNullException(nameof(attributeNames));
        EntityMetadata = entityMetadata;
        ValidationMode = validationMode;
        IndexName = indexName;
    }
}
