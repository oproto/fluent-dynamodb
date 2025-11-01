namespace Oproto.FluentDynamoDb.Attributes;

/// <summary>
/// Marks a property to be stored externally (e.g., in S3) with only a reference key in DynamoDB.
/// This is useful for large data that exceeds DynamoDB's 400KB item size limit.
/// Can be combined with JsonBlobAttribute to serialize objects before storing externally.
/// </summary>
[AttributeUsage(AttributeTargets.Property)]
public class BlobReferenceAttribute : Attribute
{
    /// <summary>
    /// Gets the blob storage provider to use.
    /// </summary>
    public BlobProvider Provider { get; }
    
    /// <summary>
    /// Gets or sets the S3 bucket name (for S3 provider).
    /// </summary>
    public string? BucketName { get; set; }
    
    /// <summary>
    /// Gets or sets the key prefix for blob storage (for S3 provider).
    /// </summary>
    public string? KeyPrefix { get; set; }
    
    /// <summary>
    /// Gets or sets the custom provider type (for Custom provider).
    /// Must implement IBlobStorageProvider interface.
    /// </summary>
    public Type? ProviderType { get; set; }
    
    /// <summary>
    /// Initializes a new instance of the BlobReferenceAttribute class.
    /// </summary>
    /// <param name="provider">The blob storage provider to use.</param>
    public BlobReferenceAttribute(BlobProvider provider)
    {
        Provider = provider;
    }
}
