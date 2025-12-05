namespace Oproto.FluentDynamoDb.Providers.Encryption;

/// <summary>
/// Provides context information for field encryption and decryption operations.
/// This context is used to determine encryption keys, configure caching, and manage external blob storage.
/// </summary>
public class FieldEncryptionContext
{
    /// <summary>
    /// Gets or initializes the runtime context identifier (e.g., tenant ID, customer ID, region).
    /// This identifier is passed to the key resolver to determine the appropriate encryption key.
    /// </summary>
    public string? ContextId { get; init; }

    /// <summary>
    /// Gets or initializes the cache TTL for data keys in seconds.
    /// This value is typically set from the EncryptedAttribute.CacheTtlSeconds property.
    /// Default is 300 seconds (5 minutes).
    /// </summary>
    public int CacheTtlSeconds { get; init; } = 300;

    /// <summary>
    /// Gets or initializes a value indicating whether the encrypted data should be stored as an external blob.
    /// When true, the encrypted data is stored in external storage (e.g., S3) and a reference is stored in DynamoDB.
    /// This is useful for large encrypted fields that would exceed DynamoDB item size limits.
    /// </summary>
    public bool IsExternalBlob { get; init; }

    /// <summary>
    /// Gets or initializes the entity identifier used for constructing external blob storage paths.
    /// This is typically the primary key or unique identifier of the entity being encrypted.
    /// </summary>
    public string? EntityId { get; init; }
}
