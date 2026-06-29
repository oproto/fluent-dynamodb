namespace Oproto.FluentDynamoDb.Encryption.Kms;

/// <summary>
/// Default implementation of <see cref="IKmsKeyResolver"/> that uses dictionary-based lookup
/// with a three-tier resolution priority: alias map → context map → default key.
/// </summary>
/// <remarks>
/// <para>
/// This resolver is suitable for scenarios where you have a fixed set of key mappings
/// that can be configured at application startup. For dynamic key resolution (e.g., loading from
/// a database or vault), implement a custom <see cref="IKmsKeyResolver"/>.
/// </para>
/// <para>
/// Resolution priority:
/// <list type="number">
///   <item>If <c>keyAlias</c> is non-null and exists in the alias-to-key map, return the alias mapping.</item>
///   <item>If <c>contextId</c> is non-null and exists in the context-to-key map, return the context mapping.</item>
///   <item>Otherwise, return the default key ID.</item>
/// </list>
/// </para>
/// <para>
/// All lookups are case-sensitive. All code paths return completed tasks via <see cref="Task.FromResult{TResult}"/>
/// since no asynchronous work is performed internally.
/// </para>
/// </remarks>
/// <example>
/// <strong>Example: Multi-tenant configuration with per-property key aliases</strong>
/// <code>
/// var contextKeyMap = new Dictionary&lt;string, string&gt;
/// {
///     ["tenant-a"] = "arn:aws:kms:us-east-1:123456789012:key/tenant-a-key-id",
///     ["tenant-b"] = "arn:aws:kms:us-east-1:123456789012:key/tenant-b-key-id"
/// };
/// 
/// var aliasKeyMap = new Dictionary&lt;string, string&gt;
/// {
///     ["pii"] = "arn:aws:kms:us-east-1:123456789012:key/pii-key-id",
///     ["financial"] = "arn:aws:kms:us-east-1:123456789012:key/financial-key-id"
/// };
/// 
/// var resolver = new DefaultKmsKeyResolver(
///     defaultKeyId: "arn:aws:kms:us-east-1:123456789012:key/default-key-id",
///     contextKeyMap: contextKeyMap,
///     aliasKeyMap: aliasKeyMap
/// );
/// 
/// // Returns PII key (alias takes priority)
/// var keyPii = await resolver.ResolveKeyIdAsync("tenant-a", "pii");
/// 
/// // Returns tenant-a's key (alias not provided, context matches)
/// var keyTenant = await resolver.ResolveKeyIdAsync("tenant-a");
/// 
/// // Returns default key (no alias, context not in map)
/// var keyDefault = await resolver.ResolveKeyIdAsync("unknown-tenant");
/// </code>
/// </example>
public sealed class DefaultKmsKeyResolver : IKmsKeyResolver
{
    private readonly string _defaultKeyId;
    private readonly IReadOnlyDictionary<string, string>? _contextKeyMap;
    private readonly IReadOnlyDictionary<string, string>? _aliasKeyMap;

    /// <summary>
    /// Initializes a new instance of the <see cref="DefaultKmsKeyResolver"/> class.
    /// </summary>
    /// <param name="defaultKeyId">
    /// The default KMS key ARN or alias to use when neither the alias nor context resolves to a key.
    /// Must not be null, empty, or whitespace.
    /// </param>
    /// <param name="contextKeyMap">
    /// Optional dictionary mapping context identifiers to KMS key ARNs or aliases.
    /// If null, context-based lookups will always fall through to the default key.
    /// Lookups are case-sensitive.
    /// </param>
    /// <param name="aliasKeyMap">
    /// Optional dictionary mapping key aliases (data classification identifiers) to KMS key ARNs or aliases.
    /// If null, alias-based lookups will always fall through to context evaluation.
    /// Lookups are case-sensitive.
    /// </param>
    /// <exception cref="ArgumentException">
    /// Thrown when <paramref name="defaultKeyId"/> is null, empty, or whitespace.
    /// </exception>
    public DefaultKmsKeyResolver(
        string defaultKeyId,
        IReadOnlyDictionary<string, string>? contextKeyMap = null,
        IReadOnlyDictionary<string, string>? aliasKeyMap = null)
    {
        if (string.IsNullOrWhiteSpace(defaultKeyId))
            throw new ArgumentException("Default key ID cannot be null, empty, or whitespace.", nameof(defaultKeyId));

        _defaultKeyId = defaultKeyId;
        _contextKeyMap = contextKeyMap;
        _aliasKeyMap = aliasKeyMap;
    }

    /// <inheritdoc />
    /// <remarks>
    /// <para>
    /// This implementation performs case-sensitive lookups in the following priority order:
    /// <list type="number">
    ///   <item>If <paramref name="keyAlias"/> is non-null and found in the alias-to-key map, return that value.</item>
    ///   <item>If <paramref name="contextId"/> is non-null and found in the context-to-key map, return that value.</item>
    ///   <item>Otherwise, return the default key ID.</item>
    /// </list>
    /// </para>
    /// <para>
    /// This method checks the cancellation token at entry and returns a completed task for all code paths.
    /// It is thread-safe and can be called concurrently from multiple threads.
    /// </para>
    /// </remarks>
    public Task<string> ResolveKeyIdAsync(
        string? contextId,
        string? keyAlias = null,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        // Priority 1: Alias-to-key map lookup
        if (keyAlias != null && _aliasKeyMap?.TryGetValue(keyAlias, out var aliasKey) == true)
        {
            return Task.FromResult(aliasKey);
        }

        // Priority 2: Context-to-key map lookup
        if (contextId != null && _contextKeyMap?.TryGetValue(contextId, out var contextKey) == true)
        {
            return Task.FromResult(contextKey);
        }

        // Priority 3: Default key
        return Task.FromResult(_defaultKeyId);
    }
}
