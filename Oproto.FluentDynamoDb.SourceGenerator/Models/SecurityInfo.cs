namespace Oproto.FluentDynamoDb.SourceGenerator.Models;

/// <summary>
/// Represents security information for a property.
/// </summary>
internal class SecurityInfo
{
    /// <summary>
    /// Gets or sets a value indicating whether the property is marked as sensitive.
    /// </summary>
    public bool IsSensitive { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the property is encrypted.
    /// </summary>
    public bool IsEncrypted { get; set; }

    /// <summary>
    /// Gets or sets the encryption configuration if the property is encrypted.
    /// </summary>
    public EncryptionConfig? EncryptionConfig { get; set; }
}
