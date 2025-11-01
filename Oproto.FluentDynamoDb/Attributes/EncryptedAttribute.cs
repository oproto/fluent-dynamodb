namespace Oproto.FluentDynamoDb.Attributes;

/// <summary>
/// Marks a property to be encrypted at rest using AWS KMS before storing in DynamoDB.
/// Requires the Oproto.FluentDynamoDb.Encryption.Kms package and proper KMS configuration.
/// Encrypted data is stored as binary (B) attribute type using AWS Encryption SDK message format.
/// Can be combined with SensitiveAttribute to also exclude the value from logging.
/// For large encrypted fields, combine with BlobReferenceAttribute to store externally (e.g., in S3).
/// </summary>
[AttributeUsage(AttributeTargets.Property, AllowMultiple = false)]
public sealed class EncryptedAttribute : Attribute
{
    /// <summary>
    /// Gets or sets the cache TTL (in seconds) for data keys used to encrypt this field.
    /// Higher values reduce KMS API calls but increase the window for key reuse.
    /// Default is 300 seconds (5 minutes).
    /// </summary>
    public int CacheTtlSeconds { get; set; } = 300;
}
