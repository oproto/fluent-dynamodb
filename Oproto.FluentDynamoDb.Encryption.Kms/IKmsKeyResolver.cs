namespace Oproto.FluentDynamoDb.Encryption.Kms;

/// <summary>
/// Resolves encryption context identifiers and key aliases to AWS KMS key ARNs or aliases.
/// Implement this interface to provide custom key resolution logic based on your application's requirements.
/// </summary>
/// <remarks>
/// <para>
/// The context identifier is a runtime value (e.g., tenant ID, customer ID, region) that determines
/// which KMS key should be used for encryption operations. The key alias is an optional data classification
/// identifier (e.g., "pii", "financial") that enables per-property key selection. Together, these allow
/// different data contexts and classifications to use different encryption keys for isolation and security.
/// </para>
/// <para>
/// <strong>Usage Examples:</strong>
/// </para>
/// <example>
/// <strong>Example 1: Simple default key resolver</strong>
/// <code>
/// public class SimpleKeyResolver : IKmsKeyResolver
/// {
///     private readonly string _keyArn;
///     
///     public SimpleKeyResolver(string keyArn)
///     {
///         _keyArn = keyArn;
///     }
///     
///     public Task&lt;string&gt; ResolveKeyIdAsync(string? contextId, string? keyAlias = null, CancellationToken cancellationToken = default)
///     {
///         return Task.FromResult(_keyArn);
///     }
/// }
/// </code>
/// </example>
/// <example>
/// <strong>Example 2: Multi-tenant key resolver with alias support</strong>
/// <code>
/// public class TenantKeyResolver : IKmsKeyResolver
/// {
///     private readonly IKeyRepository _keyRepo;
///     private readonly string _defaultKey;
///     
///     public TenantKeyResolver(IKeyRepository keyRepo, string defaultKey)
///     {
///         _keyRepo = keyRepo;
///         _defaultKey = defaultKey;
///     }
///     
///     public async Task&lt;string&gt; ResolveKeyIdAsync(string? contextId, string? keyAlias = null, CancellationToken cancellationToken = default)
///     {
///         cancellationToken.ThrowIfCancellationRequested();
///         
///         if (contextId == null)
///             return _defaultKey;
///             
///         // Load tenant-specific key from database or configuration
///         return await _keyRepo.GetKmsKeyForTenantAsync(contextId, cancellationToken) ?? _defaultKey;
///     }
/// }
/// </code>
/// </example>
/// <example>
/// <strong>Example 3: Region-based key resolver with per-property aliases</strong>
/// <code>
/// public class RegionKeyResolver : IKmsKeyResolver
/// {
///     private readonly Dictionary&lt;string, string&gt; _regionKeys;
///     private readonly Dictionary&lt;string, string&gt; _aliasKeys;
///     private readonly string _defaultKey;
///     
///     public RegionKeyResolver(Dictionary&lt;string, string&gt; regionKeys, Dictionary&lt;string, string&gt; aliasKeys, string defaultKey)
///     {
///         _regionKeys = regionKeys;
///         _aliasKeys = aliasKeys;
///         _defaultKey = defaultKey;
///     }
///     
///     public Task&lt;string&gt; ResolveKeyIdAsync(string? contextId, string? keyAlias = null, CancellationToken cancellationToken = default)
///     {
///         cancellationToken.ThrowIfCancellationRequested();
///         
///         if (keyAlias != null &amp;&amp; _aliasKeys.TryGetValue(keyAlias, out var aliasKey))
///             return Task.FromResult(aliasKey);
///         if (contextId != null &amp;&amp; _regionKeys.TryGetValue(contextId, out var keyArn))
///             return Task.FromResult(keyArn);
///         return Task.FromResult(_defaultKey);
///     }
/// }
/// </code>
/// </example>
/// </remarks>
public interface IKmsKeyResolver
{
    /// <summary>
    /// Resolves a context identifier and optional key alias to an AWS KMS key ARN or alias.
    /// </summary>
    /// <param name="contextId">
    /// Optional context identifier (e.g., tenant ID, customer ID, region) that determines
    /// which KMS key to use. If null, the resolver should fall through to a default key.
    /// </param>
    /// <param name="keyAlias">
    /// Optional data classification alias (e.g., "pii", "financial") for per-property key selection.
    /// When specified, takes priority over contextId for key resolution. Defaults to null.
    /// </param>
    /// <param name="cancellationToken">
    /// A cancellation token that can be used to cancel the resolution operation. Defaults to <see cref="CancellationToken.None"/>.
    /// </param>
    /// <returns>
    /// A task that resolves to an AWS KMS key ARN (e.g., "arn:aws:kms:us-east-1:123456789012:key/12345678-1234-1234-1234-123456789012")
    /// or KMS key alias (e.g., "alias/my-encryption-key"). The returned string must be non-null and non-empty.
    /// </returns>
    /// <remarks>
    /// <para>
    /// This method is called during encryption and decryption operations to determine which KMS key
    /// to use. The implementation should be thread-safe as it may be called concurrently.
    /// </para>
    /// <para>
    /// The returned key ARN or alias must have appropriate permissions for the calling principal
    /// to perform kms:GenerateDataKey (for encryption) and kms:Decrypt (for decryption) operations.
    /// </para>
    /// <para>
    /// Implementations should respect the cancellation token and throw <see cref="OperationCanceledException"/>
    /// if cancellation is requested before resolution completes.
    /// </para>
    /// </remarks>
    Task<string> ResolveKeyIdAsync(string? contextId, string? keyAlias = null, CancellationToken cancellationToken = default);
}
