namespace Oproto.FluentDynamoDb.Providers.BlobStorage;

/// <summary>
/// Result of a blob storage write operation.
/// </summary>
/// <remarks>
/// This class contains the reference keys assigned to uploaded blobs,
/// which are then stored in DynamoDB.
/// </remarks>
public sealed class BlobWriteResult
{
    /// <summary>
    /// Gets the reference keys assigned to uploaded blobs.
    /// </summary>
    /// <remarks>
    /// The dictionary maps property names to their assigned reference keys.
    /// These keys are stored in DynamoDB and used to retrieve the blobs later.
    /// </remarks>
    public required IReadOnlyDictionary<string, string> ReferenceKeys { get; init; }
}
