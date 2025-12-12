namespace Oproto.FluentDynamoDb.Providers.BlobStorage;

/// <summary>
/// Context for blob storage write operations.
/// </summary>
/// <remarks>
/// This class contains information about the blob properties being written
/// and tracks the reference keys assigned during upload.
/// </remarks>
public sealed class BlobWriteContext
{
    /// <summary>
    /// Gets the type name of the entity being written.
    /// </summary>
    public required string EntityType { get; init; }

    /// <summary>
    /// Gets the blob properties that need to be uploaded.
    /// </summary>
    public required IReadOnlyList<BlobPropertyContext> BlobProperties { get; init; }

    /// <summary>
    /// Gets or sets the reference keys assigned to uploaded blobs.
    /// </summary>
    /// <remarks>
    /// This is populated by <see cref="IBlobStorageStrategy.OnBeforeDynamoDbWriteAsync"/>
    /// and used by <see cref="IBlobStorageStrategy.OnAfterDynamoDbWriteFailureAsync"/>
    /// for cleanup.
    /// </remarks>
    public IReadOnlyDictionary<string, string>? UploadedReferenceKeys { get; set; }
}
