namespace Oproto.FluentDynamoDb.SourceGenerator.Models;

/// <summary>
/// Configuration for blob storage provider.
/// </summary>
internal class BlobProviderConfig
{
    /// <summary>
    /// Gets or sets the provider type (S3, Custom).
    /// </summary>
    public string ProviderType { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the S3 bucket name (for S3 provider).
    /// </summary>
    public string? BucketName { get; set; }

    /// <summary>
    /// Gets or sets the key prefix for blob storage (for S3 provider).
    /// </summary>
    public string? KeyPrefix { get; set; }

    /// <summary>
    /// Gets or sets the custom provider type name (for Custom provider).
    /// </summary>
    public string? CustomProviderTypeName { get; set; }
}
