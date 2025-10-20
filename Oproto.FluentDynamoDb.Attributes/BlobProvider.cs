namespace Oproto.FluentDynamoDb.Attributes;

/// <summary>
/// Specifies the blob storage provider to use for external blob storage.
/// </summary>
public enum BlobProvider
{
    /// <summary>
    /// Amazon S3 blob storage provider.
    /// Requires Oproto.FluentDynamoDb.BlobStorage.S3 package.
    /// </summary>
    S3,
    
    /// <summary>
    /// Custom blob storage provider.
    /// Use ProviderType property to specify the custom implementation.
    /// </summary>
    Custom
}
