using Oproto.FluentDynamoDb.Attributes;

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
    /// Gets or sets the binary data for the media item.
    /// This property is automatically stored in S3 and only a reference key is kept in DynamoDB.
    /// </summary>
    /// <remarks>
    /// This property is marked with [BlobReference(BlobProvider.S3)] to indicate that
    /// the data should be stored in S3. The S3BlobProvider handles the upload/download
    /// operations automatically when using the async methods (ToDynamoDbAsync/FromDynamoDbAsync).
    /// </remarks>
    [BlobReference(BlobProvider.S3)]
    [DynamoDbAttribute("blobData")]
    public byte[]? BlobData { get; set; }

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
}
