namespace Oproto.FluentDynamoDb.Storage;

/// <summary>
/// Interface for blob storage providers that handle external storage of large data.
/// Implementations can store data in services like S3, Azure Blob Storage, etc.
/// </summary>
public interface IBlobStorageProvider
{
    /// <summary>
    /// Stores blob data and returns a reference key that can be used to retrieve it later.
    /// </summary>
    /// <param name="data">The data stream to store</param>
    /// <param name="suggestedKey">Optional suggested key for the blob. If null, provider generates a unique key.</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>A reference key that can be used to retrieve the blob</returns>
    Task<string> StoreAsync(
        Stream data,
        string? suggestedKey = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves blob data by reference key.
    /// </summary>
    /// <param name="referenceKey">The reference key returned by StoreAsync</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>A stream containing the blob data</returns>
    /// <exception cref="KeyNotFoundException">Thrown when the blob does not exist</exception>
    Task<Stream> RetrieveAsync(
        string referenceKey,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes blob data by reference key.
    /// </summary>
    /// <param name="referenceKey">The reference key of the blob to delete</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task DeleteAsync(
        string referenceKey,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if a blob exists.
    /// </summary>
    /// <param name="referenceKey">The reference key to check</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if the blob exists, false otherwise</returns>
    Task<bool> ExistsAsync(
        string referenceKey,
        CancellationToken cancellationToken = default);
}
