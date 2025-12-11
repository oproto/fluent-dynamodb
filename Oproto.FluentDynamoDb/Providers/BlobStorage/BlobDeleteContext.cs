namespace Oproto.FluentDynamoDb.Providers.BlobStorage;

/// <summary>
/// Context for blob storage delete operations.
/// </summary>
/// <remarks>
/// This class contains information about the blobs to be deleted
/// when an entity is deleted from DynamoDB.
/// </remarks>
public sealed class BlobDeleteContext
{
    /// <summary>
    /// Gets the type name of the entity being deleted.
    /// </summary>
    public required string EntityType { get; init; }

    /// <summary>
    /// Gets the reference keys of blobs to be deleted.
    /// </summary>
    /// <remarks>
    /// These are the reference keys stored in DynamoDB that point to
    /// blobs in external storage. After the DynamoDB delete succeeds,
    /// these blobs should be cleaned up.
    /// </remarks>
    public required IReadOnlyList<string> ReferenceKeys { get; init; }
}
