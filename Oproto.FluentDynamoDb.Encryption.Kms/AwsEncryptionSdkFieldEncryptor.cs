using System.Collections.Concurrent;
using AWS.Cryptography.EncryptionSDK;
using AWS.Cryptography.MaterialProviders;
using Oproto.FluentDynamoDb.Providers.Encryption;

namespace Oproto.FluentDynamoDb.Encryption.Kms;

/// <summary>
/// Implements field-level encryption using AWS Encryption SDK with KMS keyring support.
/// </summary>
/// <remarks>
/// <para>
/// This implementation uses the AWS Encryption SDK to provide industry-standard encryption
/// with the following features:
/// </para>
/// <list type="bullet">
/// <item>KMS-based key management with automatic data key generation</item>
/// <item>Configurable data key caching to reduce KMS API calls</item>
/// <item>Encryption context for audit trails in CloudTrail</item>
/// <item>Key commitment to prevent key substitution attacks</item>
/// <item>Algorithm agility for future-proofing</item>
/// </list>
/// </remarks>
public sealed class AwsEncryptionSdkFieldEncryptor : IFieldEncryptor
{
    private readonly IKmsKeyResolver _keyResolver;
    private readonly AwsEncryptionSdkOptions _options;
    
    // New SDK clients
    private readonly MaterialProviders _materialProviders;
    private readonly ESDK _esdk;
    
    // Cache keyrings by key ARN (and optionally context ID) to avoid recreating them
    // Note: The AWS Encryption SDK for .NET does not support data key caching like other language implementations.
    // Instead, we cache keyrings to reduce object creation overhead. For true data key caching,
    // consider using the AWS KMS Hierarchical keyring (requires additional DynamoDB table setup).
    private readonly ConcurrentDictionary<string, IKeyring> _keyringCache = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="AwsEncryptionSdkFieldEncryptor"/> class.
    /// </summary>
    /// <param name="keyResolver">The key resolver for determining KMS key ARNs based on context.</param>
    /// <param name="options">Optional configuration options. If null, default options are used.</param>
    /// <remarks>
    /// <para>
    /// Note: The AWS Encryption SDK for .NET does not support data key caching like other language
    /// implementations. The <see cref="AwsEncryptionSdkOptions.EnableCaching"/> option controls
    /// keyring caching (to reduce object creation overhead) but does not cache data keys.
    /// Each encryption operation will call KMS to generate a new data key.
    /// </para>
    /// <para>
    /// For true data key caching, consider using the AWS KMS Hierarchical keyring, which requires
    /// additional setup including a DynamoDB table for branch key storage.
    /// </para>
    /// <para>
    /// Tenant isolation is achieved through encryption context - different context IDs result in
    /// different encryption contexts that are cryptographically bound to the ciphertext.
    /// </para>
    /// </remarks>
    public AwsEncryptionSdkFieldEncryptor(
        IKmsKeyResolver keyResolver,
        AwsEncryptionSdkOptions? options = null)
    {
        _keyResolver = keyResolver ?? throw new ArgumentNullException(nameof(keyResolver));
        _options = options ?? new AwsEncryptionSdkOptions();
        
        // Initialize SDK clients
        _materialProviders = new MaterialProviders(new MaterialProvidersConfig());
        _esdk = new ESDK(new AwsEncryptionSdkConfig());
    }

    /// <summary>
    /// Gets a value indicating whether keyring caching is enabled.
    /// </summary>
    /// <remarks>
    /// Note: This controls keyring object caching only. The AWS Encryption SDK for .NET
    /// does not support data key caching natively. Each encryption operation calls KMS.
    /// </remarks>
    internal bool IsCachingEnabled => _options.EnableCaching;

    /// <summary>
    /// Gets or creates a keyring for the specified KMS key ARN and optional context ID.
    /// </summary>
    /// <param name="keyArn">The KMS key ARN to create a keyring for.</param>
    /// <param name="contextId">Optional context ID for cache key partitioning (tenant isolation).</param>
    /// <returns>A keyring configured for the specified KMS key.</returns>
    /// <remarks>
    /// <para>
    /// When caching is enabled, keyrings are cached by a composite key of key ARN and context ID.
    /// This ensures tenant isolation - different contexts get different keyring instances.
    /// </para>
    /// <para>
    /// When caching is disabled, a new keyring is created for each operation.
    /// </para>
    /// </remarks>
    private IKeyring GetOrCreateKeyring(string keyArn, string? contextId)
    {
        if (!_options.EnableCaching)
        {
            // When caching is disabled, create a new keyring for each operation
            return _materialProviders.CreateAwsKmsKeyring(new CreateAwsKmsKeyringInput
            {
                KmsKeyId = keyArn
            });
        }

        // Create a composite cache key that includes context ID for tenant isolation (Requirement 4.6)
        // This ensures different contexts use different cache entries
        var cacheKey = string.IsNullOrWhiteSpace(contextId) 
            ? keyArn 
            : $"{keyArn}|{contextId}";

        return _keyringCache.GetOrAdd(cacheKey, _ =>
            _materialProviders.CreateAwsKmsKeyring(new CreateAwsKmsKeyringInput
            {
                KmsKeyId = keyArn
            }));
    }

    /// <inheritdoc />
    public async Task<byte[]> EncryptAsync(
        byte[] plaintext,
        string fieldName,
        FieldEncryptionContext context,
        CancellationToken cancellationToken = default)
    {
        if (plaintext == null)
            throw new ArgumentNullException(nameof(plaintext));
        if (string.IsNullOrWhiteSpace(fieldName))
            throw new ArgumentException("Field name cannot be null or empty.", nameof(fieldName));

        string? keyArn = null;
        
        try
        {
            // 1. Resolve KMS key ARN via IKmsKeyResolver.ResolveKeyId
            keyArn = _keyResolver.ResolveKeyId(context.ContextId);
            
            if (string.IsNullOrWhiteSpace(keyArn))
            {
                throw new FieldEncryptionException(
                    "Key resolver returned null or empty key ARN.",
                    fieldName,
                    context.ContextId,
                    null);
            }

            // 2. Build encryption context dictionary (field name, context ID, entity type)
            var encryptionContext = BuildEncryptionContext(fieldName, context.ContextId);

            // 3. Get or create KMS keyring with resolved key ARN
            // Context ID is included in cache key for tenant isolation (Requirement 4.6)
            var keyring = GetOrCreateKeyring(keyArn, context.ContextId);

            // 4. Create EncryptInput with plaintext, keyring, and encryption context
            // Note: The .NET SDK doesn't support data key caching, so we use the keyring directly.
            // Tenant isolation is achieved through:
            // - Separate keyring cache entries per context ID
            // - Encryption context bound to ciphertext (validated during decryption)
            var encryptInput = new EncryptInput
            {
                Plaintext = new MemoryStream(plaintext),
                Keyring = keyring,
                EncryptionContext = encryptionContext,
                // Use algorithm suite with key commitment to prevent key substitution attacks (Requirement 2.5)
                AlgorithmSuiteId = ESDKAlgorithmSuiteId.ALG_AES_256_GCM_HKDF_SHA512_COMMIT_KEY_ECDSA_P384
            };

            // 5. Call ESDK.Encrypt and return ciphertext bytes
            var encryptOutput = _esdk.Encrypt(encryptInput);
            
            // Read the ciphertext from the output stream
            using var ciphertextStream = encryptOutput.Ciphertext;
            using var memoryStream = new MemoryStream();
            await ciphertextStream.CopyToAsync(memoryStream, cancellationToken).ConfigureAwait(false);
            return memoryStream.ToArray();
        }
        catch (FieldEncryptionException)
        {
            // Re-throw our own exceptions
            throw;
        }
        catch (Exception ex)
        {
            // Handle errors with FieldEncryptionException (Requirements 2.4, 7.2, 7.4)
            throw new FieldEncryptionException(
                $"Failed to encrypt field '{fieldName}': {ex.Message}",
                fieldName,
                context.ContextId,
                keyArn,
                ex);
        }
    }

    /// <inheritdoc />
    public async Task<byte[]> DecryptAsync(
        byte[] ciphertext,
        string fieldName,
        FieldEncryptionContext context,
        CancellationToken cancellationToken = default)
    {
        if (ciphertext == null)
            throw new ArgumentNullException(nameof(ciphertext));
        if (string.IsNullOrWhiteSpace(fieldName))
            throw new ArgumentException("Field name cannot be null or empty.", nameof(fieldName));

        string? keyArn = null;
        
        try
        {
            // 1. Resolve KMS key ARN via key resolver (Requirement 3.1)
            keyArn = _keyResolver.ResolveKeyId(context.ContextId);
            
            if (string.IsNullOrWhiteSpace(keyArn))
            {
                throw new FieldEncryptionException(
                    "Key resolver returned null or empty key ARN.",
                    fieldName,
                    context.ContextId,
                    null);
            }

            // 2. Build expected encryption context for validation (Requirements 3.2, 3.3)
            var expectedContext = BuildEncryptionContext(fieldName, context.ContextId);

            // 3. Get or create KMS keyring with resolved key ARN
            // Context ID is included in cache key for tenant isolation (Requirement 4.6)
            var keyring = GetOrCreateKeyring(keyArn, context.ContextId);

            // 4. Create DecryptInput with ciphertext and keyring
            // Note: The .NET SDK doesn't support data key caching, so we use the keyring directly.
            var decryptInput = new DecryptInput
            {
                Ciphertext = new MemoryStream(ciphertext),
                Keyring = keyring
            };

            // 5. Call ESDK.Decrypt and get plaintext bytes
            var decryptOutput = _esdk.Decrypt(decryptInput);
            
            // 6. Validate encryption context from decrypted message (Requirements 3.2, 3.3)
            ValidateEncryptionContext(decryptOutput.EncryptionContext, expectedContext, fieldName, context.ContextId);
            
            // 7. Read the plaintext from the output stream
            using var plaintextStream = decryptOutput.Plaintext;
            using var memoryStream = new MemoryStream();
            await plaintextStream.CopyToAsync(memoryStream, cancellationToken).ConfigureAwait(false);
            return memoryStream.ToArray();
        }
        catch (FieldEncryptionException)
        {
            // Re-throw our own exceptions
            throw;
        }
        catch (Exception ex)
        {
            // Handle errors with FieldEncryptionException (Requirements 3.4, 3.5, 7.3, 7.5)
            var message = BuildDecryptionErrorMessage(ex, fieldName, keyArn);
            throw new FieldEncryptionException(
                message,
                fieldName,
                context.ContextId,
                keyArn,
                ex);
        }
    }

    /// <summary>
    /// Validates that the encryption context from the decrypted message matches the expected values.
    /// </summary>
    /// <param name="actualContext">The encryption context from the decrypted message.</param>
    /// <param name="expectedContext">The expected encryption context values.</param>
    /// <param name="fieldName">The field name for error reporting.</param>
    /// <param name="contextId">The context ID for error reporting.</param>
    /// <exception cref="FieldEncryptionException">Thrown when the encryption context validation fails.</exception>
    /// <remarks>
    /// <para>
    /// This validation ensures that the ciphertext was encrypted for the expected field and context.
    /// It prevents accidental decryption of data intended for a different field or tenant.
    /// </para>
    /// </remarks>
    private static void ValidateEncryptionContext(
        Dictionary<string, string>? actualContext,
        Dictionary<string, string> expectedContext,
        string fieldName,
        string? contextId)
    {
        if (actualContext == null)
        {
            throw new FieldEncryptionException(
                $"Encryption context validation failed for field '{fieldName}': No encryption context found in ciphertext.",
                fieldName,
                contextId,
                null);
        }

        // Validate field name is present and matches
        if (!actualContext.TryGetValue("field", out var actualFieldName) || actualFieldName != expectedContext["field"])
        {
            var actualFieldDisplay = actualFieldName ?? "(missing)";
            throw new FieldEncryptionException(
                $"Encryption context validation failed for field '{fieldName}': Expected field '{expectedContext["field"]}', found '{actualFieldDisplay}'.",
                fieldName,
                contextId,
                null);
        }

        // Validate context ID if expected (Requirements 3.2, 3.3)
        if (expectedContext.TryGetValue("context", out var expectedContextId))
        {
            if (!actualContext.TryGetValue("context", out var actualContextId) || actualContextId != expectedContextId)
            {
                var actualContextDisplay = actualContextId ?? "(missing)";
                throw new FieldEncryptionException(
                    $"Encryption context validation failed for field '{fieldName}': Expected context '{expectedContextId}', found '{actualContextDisplay}'.",
                    fieldName,
                    contextId,
                    null);
            }
        }
    }

    /// <summary>
    /// Builds a descriptive error message for decryption failures.
    /// </summary>
    /// <param name="ex">The exception that caused the failure.</param>
    /// <param name="fieldName">The field name being decrypted.</param>
    /// <param name="keyArn">The KMS key ARN that was used.</param>
    /// <returns>A descriptive error message.</returns>
    private static string BuildDecryptionErrorMessage(Exception ex, string fieldName, string? keyArn)
    {
        // Check for common KMS-related errors and provide clear messages (Requirements 3.4, 3.5)
        var exceptionMessage = ex.Message;
        var innerMessage = ex.InnerException?.Message ?? string.Empty;
        var combinedMessage = $"{exceptionMessage} {innerMessage}".ToLowerInvariant();

        if (combinedMessage.Contains("access denied") || combinedMessage.Contains("accessdenied"))
        {
            return $"KMS access denied for key '{keyArn}'. Verify IAM permissions for kms:Decrypt.";
        }

        if (combinedMessage.Contains("not found") || combinedMessage.Contains("notfound") || 
            combinedMessage.Contains("does not exist"))
        {
            return $"KMS key '{keyArn}' not found or disabled. Verify the key ARN is correct and the key is enabled.";
        }

        if (combinedMessage.Contains("invalid ciphertext") || combinedMessage.Contains("invalidciphertext") ||
            combinedMessage.Contains("cannot decrypt"))
        {
            return $"Failed to decrypt field '{fieldName}': The ciphertext may have been encrypted with a different key or is corrupted.";
        }

        // Default error message
        return $"Failed to decrypt field '{fieldName}': {ex.Message}";
    }

    /// <summary>
    /// Builds the encryption context dictionary for AWS Encryption SDK operations.
    /// </summary>
    /// <param name="fieldName">The name of the field being encrypted/decrypted.</param>
    /// <param name="contextId">Optional context identifier (e.g., tenant ID).</param>
    /// <param name="entityType">Optional entity type name for additional context.</param>
    /// <returns>A dictionary containing the encryption context key-value pairs.</returns>
    /// <remarks>
    /// <para>
    /// The encryption context is additional authenticated data (AAD) that is cryptographically
    /// bound to the encrypted data. It provides:
    /// </para>
    /// <list type="bullet">
    /// <item>Audit trail in AWS CloudTrail logs</item>
    /// <item>Additional security through context validation during decryption</item>
    /// <item>Metadata about what was encrypted and for which context</item>
    /// </list>
    /// <para>
    /// The encryption context always includes the field name. Context ID and entity type
    /// are included if provided.
    /// </para>
    /// </remarks>
    private static Dictionary<string, string> BuildEncryptionContext(
        string fieldName,
        string? contextId,
        string? entityType = null)
    {
        var encryptionContext = new Dictionary<string, string>
        {
            ["field"] = fieldName
        };

        if (!string.IsNullOrWhiteSpace(contextId))
        {
            encryptionContext["context"] = contextId;
        }

        if (!string.IsNullOrWhiteSpace(entityType))
        {
            encryptionContext["entity"] = entityType;
        }

        return encryptionContext;
    }
}
