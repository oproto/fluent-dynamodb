using Amazon.DynamoDBv2.Model;

namespace Oproto.FluentDynamoDb.Expressions;

/// <summary>
/// Metadata about a parameter in an update expression that tracks encryption requirements.
/// </summary>
/// <remarks>
/// <para>
/// This class is used to track parameters that require encryption during update expression translation.
/// The UpdateExpressionTranslator marks parameters that need encryption, and the request builder
/// (e.g., UpdateItemRequestBuilder) encrypts them before sending the request to DynamoDB.
/// </para>
/// 
/// <para><strong>Encryption Flow:</strong></para>
/// <list type="number">
/// <item><description>UpdateExpressionTranslator detects encrypted property from PropertyMetadata</description></item>
/// <item><description>Translator creates ParameterMetadata entry with RequiresEncryption = true</description></item>
/// <item><description>Translator adds parameter to ExpressionContext.ParameterMetadata list</description></item>
/// <item><description>Request builder checks for parameters requiring encryption</description></item>
/// <item><description>Request builder calls IFieldEncryptor.EncryptAsync for each marked parameter</description></item>
/// <item><description>Request builder replaces parameter value with encrypted value</description></item>
/// <item><description>Request is sent to DynamoDB with encrypted values</description></item>
/// </list>
/// 
/// <para><strong>Design Rationale:</strong></para>
/// <para>
/// This deferred encryption approach allows the UpdateExpressionTranslator to remain synchronous
/// while encryption (which is async) happens at the request builder layer where async operations
/// are natural. This avoids blocking async calls and maintains clean separation of concerns.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// // Translator marks encrypted parameter
/// var metadata = new ParameterMetadata
/// {
///     ParameterName = ":p0",
///     Value = new AttributeValue { S = "sensitive-data" },
///     RequiresEncryption = true,
///     PropertyName = "SocialSecurityNumber",
///     AttributeName = "ssn"
/// };
/// context.ParameterMetadata.Add(metadata);
/// 
/// // Request builder encrypts marked parameters
/// foreach (var param in context.ParameterMetadata.Where(p => p.RequiresEncryption))
/// {
///     var encrypted = await fieldEncryptor.EncryptAsync(...);
///     request.ExpressionAttributeValues[param.ParameterName] = new AttributeValue { S = encrypted };
/// }
/// </code>
/// </example>
public class ParameterMetadata
{
    /// <summary>
    /// Gets or sets the parameter name in the update expression (e.g., ":p0", ":p1").
    /// </summary>
    /// <remarks>
    /// This is the placeholder name used in the DynamoDB update expression string.
    /// It corresponds to a key in the ExpressionAttributeValues dictionary.
    /// </remarks>
    public string ParameterName { get; set; } = string.Empty;
    
    /// <summary>
    /// Gets or sets the AttributeValue for this parameter.
    /// </summary>
    /// <remarks>
    /// This is the original (unencrypted) value that was captured during expression translation.
    /// If RequiresEncryption is true, this value will be replaced with an encrypted version
    /// before the request is sent to DynamoDB.
    /// </remarks>
    public AttributeValue Value { get; set; } = new AttributeValue();
    
    /// <summary>
    /// Gets or sets a value indicating whether this parameter requires encryption.
    /// </summary>
    /// <remarks>
    /// When true, the request builder will encrypt this parameter's value using the configured
    /// IFieldEncryptor before sending the request to DynamoDB. If no encryptor is configured,
    /// an exception will be thrown.
    /// </remarks>
    public bool RequiresEncryption { get; set; }
    
    /// <summary>
    /// Gets or sets the C# property name that this parameter corresponds to.
    /// </summary>
    /// <remarks>
    /// This is the name of the property in the entity class (e.g., "SocialSecurityNumber").
    /// Used for error messages and encryption context.
    /// </remarks>
    public string? PropertyName { get; set; }
    
    /// <summary>
    /// Gets or sets the DynamoDB attribute name that this parameter corresponds to.
    /// </summary>
    /// <remarks>
    /// This is the name of the attribute in DynamoDB (e.g., "ssn").
    /// Used for error messages and encryption context.
    /// </remarks>
    public string? AttributeName { get; set; }
}
