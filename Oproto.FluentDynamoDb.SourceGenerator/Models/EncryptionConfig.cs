namespace Oproto.FluentDynamoDb.SourceGenerator.Models;

/// <summary>
/// Represents encryption configuration extracted from EncryptedAttribute.
/// </summary>
internal class EncryptionConfig
{
    /// <summary>
    /// Gets or sets the cache TTL in seconds for data keys.
    /// Default is 300 seconds (5 minutes).
    /// </summary>
    public int CacheTtlSeconds { get; set; } = 300;

    /// <summary>
    /// Gets or sets the key alias for per-property KMS key selection.
    /// When non-null and non-whitespace, emitted as the KeyAlias property in FieldEncryptionContext.
    /// </summary>
    public string? KeyAlias { get; set; }
}
