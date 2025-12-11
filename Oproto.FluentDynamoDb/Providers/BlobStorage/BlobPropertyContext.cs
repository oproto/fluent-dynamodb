namespace Oproto.FluentDynamoDb.Providers.BlobStorage;

/// <summary>
/// Context for a single blob property being written.
/// </summary>
/// <remarks>
/// This class contains information about a single blob property including
/// the data to upload and any existing reference key for updates.
/// </remarks>
public sealed class BlobPropertyContext
{
    /// <summary>
    /// Gets the name of the property on the entity.
    /// </summary>
    public required string PropertyName { get; init; }

    /// <summary>
    /// Gets the DynamoDB attribute name for this property.
    /// </summary>
    public required string AttributeName { get; init; }

    /// <summary>
    /// Gets the data stream to upload.
    /// </summary>
    public required Stream Data { get; init; }

    /// <summary>
    /// Gets the content type (MIME type) for the blob.
    /// </summary>
    /// <value>
    /// The content type if specified; otherwise, <c>null</c>.
    /// </value>
    public string? ContentType { get; init; }

    /// <summary>
    /// Gets the existing reference key if this is an update operation.
    /// </summary>
    /// <value>
    /// The existing reference key if updating an existing blob;
    /// <c>null</c> for new blobs.
    /// </value>
    /// <remarks>
    /// When updating an existing blob, the strategy may choose to overwrite
    /// the existing blob or create a new one and delete the old one.
    /// </remarks>
    public string? ExistingReferenceKey { get; init; }
}
