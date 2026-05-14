namespace Oproto.FluentDynamoDb.Encryption.Kms;

/// <summary>
/// Configuration options for AWS Encryption SDK field-level encryption.
/// </summary>
/// <remarks>
/// <para>
/// These options control the behavior of the <see cref="AwsEncryptionSdkFieldEncryptor"/>,
/// including key resolution, caching policies, and algorithm selection.
/// </para>
/// <para>
/// <strong>Data Key Caching:</strong> The AWS Encryption SDK for .NET does not support
/// data key caching natively. Each encryption operation calls KMS to generate a new data key.
/// For applications requiring reduced KMS API calls, consider using the AWS KMS Hierarchical Keyring.
/// </para>
/// <para>
/// <strong>Security Best Practices:</strong>
/// </para>
/// <list type="bullet">
/// <item>Load KMS key ARNs from secure configuration (AWS Secrets Manager, Parameter Store, etc.)</item>
/// <item>Never hardcode KMS key ARNs in source code</item>
/// <item>Use key commitment algorithms (default) to prevent key substitution attacks</item>
/// <item>Use encryption context for audit trails in CloudTrail</item>
/// <item>Consider the AWS KMS Hierarchical Keyring for data key caching scenarios</item>
/// </list>
/// </remarks>
/// <example>
/// <strong>Example: Basic configuration</strong>
/// <code>
/// var options = new AwsEncryptionSdkOptions
/// {
///     DefaultKeyId = configuration["Kms:DefaultKeyArn"],
///     EnableCaching = true
/// };
/// </code>
/// </example>
/// <example>
/// <strong>Example: Multi-tenant configuration</strong>
/// <code>
/// var options = new AwsEncryptionSdkOptions
/// {
///     DefaultKeyId = configuration["Kms:DefaultKeyArn"],
///     ContextKeyMap = new Dictionary&lt;string, string&gt;
///     {
///         ["tenant-a"] = configuration["Kms:TenantA:KeyArn"],
///         ["tenant-b"] = configuration["Kms:TenantB:KeyArn"]
///     },
///     EnableCaching = true
/// };
/// </code>
/// </example>
public sealed class AwsEncryptionSdkOptions
{
    /// <summary>
    /// Gets or sets the default KMS key ARN or alias used when no context is provided
    /// or when the context doesn't match any mapped keys.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This should be a valid KMS key ARN (e.g., "arn:aws:kms:us-east-1:123456789012:key/12345678-1234-1234-1234-123456789012")
    /// or a KMS key alias (e.g., "alias/my-encryption-key").
    /// </para>
    /// <para>
    /// <strong>Security Note:</strong> Load this value from secure configuration, not hardcoded in source code.
    /// </para>
    /// </remarks>
    public string DefaultKeyId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the optional mapping of context identifiers to KMS key ARNs or aliases.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This allows different contexts (e.g., tenants, customers, regions) to use different KMS keys
    /// for data isolation and security. The context identifier is passed at runtime via
    /// <c>WithEncryptionContext()</c> or <c>EncryptionContext.Current</c>.
    /// </para>
    /// <para>
    /// Example:
    /// <code>
    /// {
    ///     ["tenant-a"] = "arn:aws:kms:us-east-1:123456789012:key/tenant-a-key-id",
    ///     ["tenant-b"] = "arn:aws:kms:us-east-1:123456789012:key/tenant-b-key-id"
    /// }
    /// </code>
    /// </para>
    /// </remarks>
    public Dictionary<string, string>? ContextKeyMap { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether keyring caching is enabled.
    /// Default is <c>true</c>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <strong>Important:</strong> The AWS Encryption SDK for .NET does not support data key caching.
    /// This option controls keyring object caching only, which reduces object creation overhead
    /// but does not cache data keys. Each encryption operation will call KMS.
    /// </para>
    /// <para>
    /// When enabled, keyrings are cached by a composite key of KMS key ARN and context ID.
    /// This provides tenant isolation - different contexts get different keyring instances.
    /// </para>
    /// <para>
    /// When disabled, a new keyring is created for each encryption/decryption operation.
    /// </para>
    /// </remarks>
    public bool EnableCaching { get; set; } = true;

    /// <summary>
    /// Gets or sets the algorithm suite identifier to use for encryption.
    /// Default is "AES_256_GCM_HKDF_SHA512_COMMIT_KEY_ECDSA_P384".
    /// </summary>
    /// <remarks>
    /// <para>
    /// AWS Encryption SDK 3.x uses key commitment by default to prevent key substitution attacks.
    /// The default algorithm provides:
    /// </para>
    /// <list type="bullet">
    /// <item>AES-256-GCM encryption</item>
    /// <item>HKDF-SHA512 key derivation</item>
    /// <item>Key commitment (prevents key substitution attacks)</item>
    /// <item>ECDSA P-384 signature for non-repudiation</item>
    /// </list>
    /// <para>
    /// Valid values include:
    /// </para>
    /// <list type="bullet">
    /// <item>AES_256_GCM_HKDF_SHA512_COMMIT_KEY_ECDSA_P384 (recommended, default)</item>
    /// <item>AES_256_GCM_HKDF_SHA512_COMMIT_KEY</item>
    /// <item>AES_192_GCM_HKDF_SHA384_ECDSA_P384</item>
    /// </list>
    /// <para>
    /// <strong>Security Note:</strong> Always use algorithms with key commitment (COMMIT_KEY) to prevent
    /// key substitution attacks.
    /// </para>
    /// </remarks>
    public string Algorithm { get; set; } = "AES_256_GCM_HKDF_SHA512_COMMIT_KEY_ECDSA_P384";

}
