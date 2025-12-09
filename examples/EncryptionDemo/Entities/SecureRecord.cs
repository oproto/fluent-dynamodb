using Oproto.FluentDynamoDb.Attributes;

namespace EncryptionDemo.Entities;

/// <summary>
/// Represents a secure record with encrypted and sensitive properties stored in DynamoDB.
/// 
/// This entity demonstrates the [Encrypted] and [Sensitive] attributes for protecting
/// sensitive data. The [Encrypted] attribute encrypts data at rest using KMS, while
/// the [Sensitive] attribute redacts values in log output.
/// </summary>
/// <remarks>
/// <para>
/// <strong>Attribute Usage:</strong>
/// </para>
/// <list type="bullet">
/// <item>
/// <description>
/// <see cref="DynamoDbTableAttribute"/> - Specifies the DynamoDB table name.
/// </description>
/// </item>
/// <item>
/// <description>
/// <see cref="ScannableAttribute"/> - Enables Scan() operations for listing all records.
/// </description>
/// </item>
/// <item>
/// <description>
/// <see cref="SensitiveAttribute"/> - Marks properties for redaction in log output.
/// The value is replaced with "[REDACTED]" in logs but stored normally in DynamoDB.
/// </description>
/// </item>
/// <item>
/// <description>
/// <see cref="EncryptedAttribute"/> - Marks properties for encryption at rest using KMS.
/// The value is encrypted before storing and decrypted after retrieval.
/// </description>
/// </item>
/// </list>
/// </remarks>
[DynamoDbTable("encryption-demo", IsDefault = true)]
[Scannable]
[GenerateEntityProperty(Name = "SecureRecords")]
public partial class SecureRecord
{
    /// <summary>
    /// Gets or sets the unique identifier for the secure record.
    /// This serves as the partition key for the DynamoDB table.
    /// </summary>
    [PartitionKey]
    [DynamoDbAttribute("pk")]
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets a descriptive label for the record.
    /// This is a non-sensitive field that appears normally in logs.
    /// </summary>
    [DynamoDbAttribute("label")]
    public string Label { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the email address.
    /// This property is marked as [Sensitive] so it will be redacted in log output
    /// but stored as plain text in DynamoDB.
    /// </summary>
    [Sensitive]
    [DynamoDbAttribute("email")]
    public string Email { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the social security number.
    /// This property is marked as [Encrypted] so it will be encrypted at rest
    /// using KMS before storing in DynamoDB.
    /// </summary>
    /// <remarks>
    /// Note: The AWS Encryption SDK integration is pending completion.
    /// This demonstrates the intended API pattern.
    /// </remarks>
    [Encrypted]
    [DynamoDbAttribute("ssn")]
    public string SocialSecurityNumber { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the credit card number.
    /// This property is marked with both [Encrypted] and [Sensitive] attributes,
    /// meaning it will be encrypted at rest AND redacted in log output.
    /// </summary>
    /// <remarks>
    /// Note: The AWS Encryption SDK integration is pending completion.
    /// This demonstrates the intended API pattern.
    /// </remarks>
    [Encrypted]
    [Sensitive]
    [DynamoDbAttribute("creditCard")]
    public string CreditCardNumber { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the timestamp when the record was created.
    /// </summary>
    [DynamoDbAttribute("createdAt")]
    public DateTime CreatedAt { get; set; }
}
