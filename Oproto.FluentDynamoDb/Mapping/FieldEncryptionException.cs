namespace Oproto.FluentDynamoDb.Mapping;

/// <summary>
/// Exception thrown when field-level encryption or decryption operations fail.
/// </summary>
/// <remarks>
/// This exception is thrown when:
/// <list type="bullet">
/// <item>Encryption fails during update expression parameter encryption</item>
/// <item>Decryption fails during entity deserialization</item>
/// <item>IFieldEncryptor is not configured but encryption is required</item>
/// <item>Encryption context is invalid or missing</item>
/// </list>
/// </remarks>
public class FieldEncryptionException : Exception
{
    /// <summary>
    /// Initializes a new instance of the <see cref="FieldEncryptionException"/> class.
    /// </summary>
    /// <param name="message">The error message that explains the reason for the exception.</param>
    public FieldEncryptionException(string message)
        : base(message)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="FieldEncryptionException"/> class
    /// with a specified error message and a reference to the inner exception that is the cause of this exception.
    /// </summary>
    /// <param name="message">The error message that explains the reason for the exception.</param>
    /// <param name="innerException">The exception that is the cause of the current exception.</param>
    public FieldEncryptionException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
