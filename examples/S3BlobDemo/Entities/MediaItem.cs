using Oproto.FluentDynamoDb.Attributes;
using Oproto.FluentDynamoDb.Providers.BlobStorage;

namespace S3BlobDemo.Entities;

/// <summary>
/// Represents a media item with binary data stored in S3.
/// 
/// This entity demonstrates the [BlobReference] attribute for storing large data
/// externally in S3 while keeping only a reference key in DynamoDB.
/// </summary>
/// <remarks>
/// <para>
/// <strong>Attribute Usage:</strong>
/// </para>
/// <list type="bullet">
/// <item>
/// <description>
/// <see cref="DynamoDbTableAttribute"/> - Specifies the DynamoDB table name.
/// </description>
/// </item>
/// <item>
/// <description>
/// <see cref="ScannableAttribute"/> - Enables Scan() operations for listing all media items.
/// </description>
/// </item>
/// <item>
/// <description>
/// <see cref="BlobReferenceAttribute"/> - Marks the DataReference property for S3 storage.
/// The actual binary data is stored in S3, and only the S3 key is stored in DynamoDB.
/// </description>
/// </item>
/// </list>
/// </remarks>
[DynamoDbTable("s3-blob-demo", IsDefault = true)]
[Scannable]
[GenerateEntityProperty(Name = "MediaItems")]
public partial class MediaItem
{
    /// <summary>
    /// Gets or sets the unique identifier for the media item.
    /// This serves as the partition key for the DynamoDB table.
    /// </summary>
    [PartitionKey]
    [DynamoDbAttribute("pk")]
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the display name of the media item.
    /// </summary>
    [DynamoDbAttribute("name")]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the MIME content type of the media (e.g., "image/png", "application/pdf").
    /// </summary>
    [DynamoDbAttribute("contentType")]
    public string ContentType { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the S3 reference key for the blob data.
    /// The actual binary data is stored in S3, and only this reference key is stored in DynamoDB.
    /// </summary>
    /// <remarks>
    /// This property stores the S3 object key that points to the actual binary data.
    /// The S3BlobProvider handles upload/download operations using this key.
    /// </remarks>
    [DynamoDbAttribute("dataRef")]
    public string DataReference { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the size of the blob data in bytes.
    /// </summary>
    [DynamoDbAttribute("sizeBytes")]
    public long SizeBytes { get; set; }

    /// <summary>
    /// Gets or sets the timestamp when the media item was uploaded.
    /// </summary>
    [DynamoDbAttribute("uploadedAt")]
    public DateTime UploadedAt { get; set; }

    /// <summary>
    /// Gets or sets an optional description of the media item.
    /// </summary>
    [DynamoDbAttribute("description")]
    public string? Description { get; set; }

    // Named provider "images" — routes to the images bucket/provider registered via
    // FluentDynamoDbOptions.WithBlobStorage("images", provider). Omitting the Provider
    // parameter on [BlobStorage] uses the default provider instead.
    /// <summary>
    /// Gets or sets the thumbnail image stored via the "images" named blob provider.
    /// </summary>
    [BlobStorage(Provider = "images")]
    [DynamoDbAttribute("thumbnailRef")]
    public BlobData<byte[]>? Thumbnail { get; set; }

    // Named provider "documents" — routes to the documents bucket/provider registered via
    // FluentDynamoDbOptions.WithBlobStorage("documents", provider). Specifying Provider = "name"
    // routes to the named provider rather than the default.
    /// <summary>
    /// Gets or sets the attachment document stored via the "documents" named blob provider.
    /// </summary>
    [BlobStorage(Provider = "documents")]
    [DynamoDbAttribute("attachmentRef")]
    public BlobData<byte[]>? Attachment { get; set; }
}
