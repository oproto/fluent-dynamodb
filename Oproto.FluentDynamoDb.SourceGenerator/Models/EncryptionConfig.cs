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
}
