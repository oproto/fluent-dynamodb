namespace Oproto.FluentDynamoDb.Providers.Encryption;

/// <summary>
/// Controls how the library handles decryption failures for [Encrypted] fields
/// during entity deserialization (FromDynamoDbAsync).
/// Write operations (ToDynamoDbAsync) always throw on failure regardless of this setting.
/// </summary>
public enum DecryptionFailureMode
{
    /// <summary>
    /// Default. Any decryption failure throws an exception, halting entity deserialization.
    /// This is the safest mode and preserves backward compatibility.
    /// </summary>
    Throw = 0,

    /// <summary>
    /// Recoverable decryption failures (no encryptor configured, KMS access denied) leave
    /// the encrypted property at its CLR default value and log a warning.
    /// Integrity failures (wrong key, corrupted ciphertext) always throw regardless.
    /// </summary>
    SkipFields = 1
}
