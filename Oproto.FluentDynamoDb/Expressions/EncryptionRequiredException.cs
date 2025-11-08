using System.Linq.Expressions;

namespace Oproto.FluentDynamoDb.Expressions;

/// <summary>
/// Thrown when an update expression attempts to update an encrypted property without a configured field encryptor.
/// This occurs when a property marked with [Encrypted] is included in an update operation but no IFieldEncryptor is available.
/// </summary>
/// <remarks>
/// <para><strong>Common Causes:</strong></para>
/// <list type="bullet">
/// <item><description>Property is marked with [Encrypted] attribute</description></item>
/// <item><description>No IFieldEncryptor configured in the operation context</description></item>
/// <item><description>Field encryptor not registered in dependency injection</description></item>
/// </list>
/// 
/// <para><strong>Resolution:</strong></para>
/// <list type="number">
/// <item><description>Configure an IFieldEncryptor in the DynamoDB operation context</description></item>
/// <item><description>Register a field encryptor implementation in your DI container</description></item>
/// <item><description>Remove the [Encrypted] attribute if encryption is not needed</description></item>
/// <item><description>Use string-based update expressions with pre-encrypted values</description></item>
/// </list>
/// 
/// <para><strong>Property Information:</strong></para>
/// <para>
/// The exception provides <see cref="PropertyName"/> and <see cref="AttributeName"/> properties
/// to help identify which property requires encryption.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// // Entity with encrypted property
/// public class User
/// {
///     [PartitionKey]
///     public string UserId { get; set; }
///     
///     [Encrypted]
///     public string SocialSecurityNumber { get; set; }
///     
///     public string Name { get; set; }
/// }
/// 
/// try
/// {
///     // ✗ Invalid: No field encryptor configured
///     table.Users.Update(userId)
///         .Set(x => new UserUpdateModel 
///         {
///             SocialSecurityNumber = "123-45-6789"  // Requires encryption!
///         })
///         .UpdateAsync();
/// }
/// catch (EncryptionRequiredException ex)
/// {
///     Console.WriteLine($"Property: {ex.PropertyName}"); // "SocialSecurityNumber"
///     Console.WriteLine($"Attribute: {ex.AttributeName}"); // "ssn"
///     Console.WriteLine(ex.Message);
///     // "Property 'SocialSecurityNumber' is marked as encrypted but no IFieldEncryptor is configured..."
/// }
/// 
/// // ✓ Valid: Configure field encryptor
/// var encryptor = new AwsEncryptionSdkFieldEncryptor(options);
/// var context = new DynamoDbOperationContext
/// {
///     FieldEncryptor = encryptor
/// };
/// 
/// table.Users.Update(userId)
///     .WithOperationContext(context)
///     .Set(x => new UserUpdateModel 
///     {
///         SocialSecurityNumber = "123-45-6789"  // Will be encrypted automatically
///     })
///     .UpdateAsync();
/// 
/// // Alternative: Use string-based expression with pre-encrypted value
/// var encryptedValue = await encryptor.EncryptAsync("123-45-6789", ...);
/// table.Users.Update(userId)
///     .Set("ssn = :ssn")
///     .WithValue(":ssn", encryptedValue)
///     .UpdateAsync();
/// </code>
/// </example>
public class EncryptionRequiredException : ExpressionTranslationException
{
    /// <summary>
    /// Gets the name of the property that requires encryption.
    /// </summary>
    public string PropertyName { get; }

    /// <summary>
    /// Gets the DynamoDB attribute name of the property that requires encryption.
    /// </summary>
    public string AttributeName { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="EncryptionRequiredException"/> class.
    /// </summary>
    /// <param name="message">The error message.</param>
    /// <param name="propertyName">The name of the property.</param>
    /// <param name="attributeName">The DynamoDB attribute name.</param>
    /// <param name="expression">The original expression.</param>
    public EncryptionRequiredException(
        string message, 
        string propertyName, 
        string attributeName, 
        Expression? expression = null)
        : base(message, expression)
    {
        PropertyName = propertyName;
        AttributeName = attributeName;
    }
}
