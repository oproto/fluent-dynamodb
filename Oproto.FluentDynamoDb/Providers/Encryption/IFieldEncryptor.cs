namespace Oproto.FluentDynamoDb.Providers.Encryption;

/// <summary>
/// Provides field-level encryption and decryption capabilities for DynamoDB entity properties.
/// Implementations should use industry-standard encryption libraries and key management services.
/// </summary>
public interface IFieldEncryptor
{
    /// <summary>
    /// Encrypts plaintext data for a specific field.
    /// </summary>
    /// <param name="plaintext">The plaintext data to encrypt.</param>
    /// <param name="fieldName">The name of the field being encrypted (used for encryption context).</param>
    /// <param name="context">Encryption context containing metadata for the operation.</param>
    /// <param name="cancellationToken">Cancellation token for the async operation.</param>
    /// <returns>The encrypted data as a byte array.</returns>
    Task<byte[]> EncryptAsync(
        byte[] plaintext,
        string fieldName,
        FieldEncryptionContext context,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Decrypts ciphertext data for a specific field.
    /// </summary>
    /// <param name="ciphertext">The encrypted data to decrypt.</param>
    /// <param name="fieldName">The name of the field being decrypted (used for encryption context validation).</param>
    /// <param name="context">Encryption context containing metadata for the operation.</param>
    /// <param name="cancellationToken">Cancellation token for the async operation.</param>
    /// <returns>The decrypted plaintext data as a byte array.</returns>
    Task<byte[]> DecryptAsync(
        byte[] ciphertext,
        string fieldName,
        FieldEncryptionContext context,
        CancellationToken cancellationToken = default);
}
