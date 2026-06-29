namespace Oproto.FluentDynamoDb.Encryption.Kms;

/// <summary>
/// Exception thrown when field-level encryption or decryption operations fail.
/// </summary>
/// <remarks>
/// <para>
/// This exception provides detailed context about encryption failures, including:
/// </para>
/// <list type="bullet">
/// <item>The field name that failed to encrypt/decrypt</item>
/// <item>The context identifier (e.g., tenant ID) if applicable</item>
/// <item>The KMS key ID that was used or attempted</item>
/// <item>The key alias used for per-property key selection</item>
/// <item>The underlying error from AWS KMS or the Encryption SDK</item>
/// </list>
/// <para>
/// Common scenarios that trigger this exception:
/// </para>
/// <list type="bullet">
/// <item>KMS key access denied (insufficient IAM permissions)</item>
/// <item>KMS key not found or disabled</item>
/// <item>Data key generation failure</item>
/// <item>Corrupted ciphertext during decryption</item>
/// <item>Encryption context validation failure</item>
/// <item>Network errors communicating with KMS</item>
/// <item>Key resolver returned an invalid (null/empty) key identifier</item>
/// </list>
/// </remarks>
/// <example>
/// <strong>Example: Handling encryption exceptions</strong>
/// <code>
/// try
/// {
///     await table.PutItem(entity)
///         .WithEncryptionContext("tenant-123")
///         .ExecuteAsync();
/// }
/// catch (FieldEncryptionException ex)
/// {
///     logger.LogError(ex,
///         "Failed to encrypt field {FieldName} for context {ContextId} using key {KeyId} (alias: {KeyAlias})",
///         ex.FieldName, ex.ContextId, ex.KeyId, ex.KeyAlias);
///     
///     // Check for specific error types
///     if (ex.InnerException is AccessDeniedException)
///     {
///         // Handle KMS access denied - check IAM permissions
///     }
///     else if (ex.InnerException is NotFoundException)
///     {
///         // Handle KMS key not found - verify key ARN
///     }
/// }
/// </code>
/// </example>
public sealed class FieldEncryptionException : Exception
{
    /// <summary>
    /// Gets the name of the field that failed to encrypt or decrypt.
    /// </summary>
    public string FieldName { get; }

    /// <summary>
    /// Gets the context identifier (e.g., tenant ID, customer ID) associated with the operation,
    /// or null if no context was provided.
    /// </summary>
    public string? ContextId { get; }

    /// <summary>
    /// Gets the KMS key ARN or alias that was used or attempted for the operation,
    /// or null if the key could not be resolved.
    /// </summary>
    public string? KeyId { get; }

    /// <summary>
    /// Gets the key alias that was passed to the key resolver for per-property key selection,
    /// or null if no key alias was specified.
    /// </summary>
    public string? KeyAlias { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="FieldEncryptionException"/> class.
    /// </summary>
    /// <param name="message">The error message that explains the reason for the exception.</param>
    /// <param name="fieldName">The name of the field that failed to encrypt or decrypt.</param>
    /// <param name="contextId">
    /// Optional context identifier (e.g., tenant ID) associated with the operation.
    /// </param>
    /// <param name="keyId">
    /// Optional KMS key ARN or alias that was used or attempted for the operation.
    /// </param>
    public FieldEncryptionException(
        string message,
        string fieldName,
        string? contextId = null,
        string? keyId = null)
        : base(message)
    {
        FieldName = fieldName ?? throw new ArgumentNullException(nameof(fieldName));
        ContextId = contextId;
        KeyId = keyId;
        KeyAlias = null;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="FieldEncryptionException"/> class
    /// with a specified error message and a reference to the inner exception that is the cause of this exception.
    /// </summary>
    /// <param name="message">The error message that explains the reason for the exception.</param>
    /// <param name="fieldName">The name of the field that failed to encrypt or decrypt.</param>
    /// <param name="innerException">
    /// The exception that is the cause of the current exception, typically from AWS KMS or the Encryption SDK.
    /// </param>
    public FieldEncryptionException(
        string message,
        string fieldName,
        Exception innerException)
        : base(message, innerException)
    {
        FieldName = fieldName ?? throw new ArgumentNullException(nameof(fieldName));
        ContextId = null;
        KeyId = null;
        KeyAlias = null;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="FieldEncryptionException"/> class
    /// with a specified error message, field context, and a reference to the inner exception.
    /// </summary>
    /// <param name="message">The error message that explains the reason for the exception.</param>
    /// <param name="fieldName">The name of the field that failed to encrypt or decrypt.</param>
    /// <param name="contextId">
    /// Optional context identifier (e.g., tenant ID) associated with the operation.
    /// </param>
    /// <param name="keyId">
    /// Optional KMS key ARN or alias that was used or attempted for the operation.
    /// </param>
    /// <param name="innerException">
    /// The exception that is the cause of the current exception, typically from AWS KMS or the Encryption SDK.
    /// </param>
    public FieldEncryptionException(
        string message,
        string fieldName,
        string? contextId,
        string? keyId,
        Exception? innerException)
        : base(message, innerException)
    {
        FieldName = fieldName ?? throw new ArgumentNullException(nameof(fieldName));
        ContextId = contextId;
        KeyId = keyId;
        KeyAlias = null;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="FieldEncryptionException"/> class
    /// with a specified error message, field context, key alias, and no inner exception.
    /// </summary>
    /// <param name="message">The error message that explains the reason for the exception.</param>
    /// <param name="fieldName">The name of the field that failed to encrypt or decrypt.</param>
    /// <param name="contextId">
    /// Optional context identifier (e.g., tenant ID) associated with the operation.
    /// </param>
    /// <param name="keyId">
    /// Optional KMS key ARN or alias that was used or attempted for the operation.
    /// </param>
    /// <param name="keyAlias">
    /// Optional key alias that was passed to the key resolver for per-property key selection.
    /// </param>
    public FieldEncryptionException(
        string message,
        string fieldName,
        string? contextId,
        string? keyId,
        string? keyAlias)
        : base(message)
    {
        FieldName = fieldName ?? throw new ArgumentNullException(nameof(fieldName));
        ContextId = contextId;
        KeyId = keyId;
        KeyAlias = keyAlias;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="FieldEncryptionException"/> class
    /// with a specified error message, field context, key alias, and a reference to the inner exception.
    /// </summary>
    /// <param name="message">The error message that explains the reason for the exception.</param>
    /// <param name="fieldName">The name of the field that failed to encrypt or decrypt.</param>
    /// <param name="contextId">
    /// Optional context identifier (e.g., tenant ID) associated with the operation.
    /// </param>
    /// <param name="keyId">
    /// Optional KMS key ARN or alias that was used or attempted for the operation.
    /// </param>
    /// <param name="keyAlias">
    /// Optional key alias that was passed to the key resolver for per-property key selection.
    /// </param>
    /// <param name="innerException">
    /// The exception that is the cause of the current exception, typically from AWS KMS or the Encryption SDK.
    /// </param>
    public FieldEncryptionException(
        string message,
        string fieldName,
        string? contextId,
        string? keyId,
        string? keyAlias,
        Exception? innerException)
        : base(message, innerException)
    {
        FieldName = fieldName ?? throw new ArgumentNullException(nameof(fieldName));
        ContextId = contextId;
        KeyId = keyId;
        KeyAlias = keyAlias;
    }

    /// <summary>
    /// Returns a string representation of the exception including field context.
    /// </summary>
    /// <returns>A string that represents the current exception.</returns>
    public override string ToString()
    {
        var details = $"FieldName: {FieldName}";
        
        if (ContextId != null)
        {
            details += $", ContextId: {ContextId}";
        }
        
        if (KeyId != null)
        {
            details += $", KeyId: {KeyId}";
        }

        if (KeyAlias != null)
        {
            details += $", KeyAlias: {KeyAlias}";
        }
        
        return $"{base.ToString()}\n{details}";
    }
}
