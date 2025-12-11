namespace Oproto.FluentDynamoDb.Providers.BlobStorage;

/// <summary>
/// Interface for blob storage providers that handle external storage of large data.
/// Implementations can store data in services like S3, Azure Blob Storage, etc.
/// </summary>
/// <remarks>
/// <para>
/// This interface is cloud-agnostic and does not expose any provider-specific types.
/// Provider-specific configuration (bucket names, container names, connection strings, etc.)
/// should be handled by the implementation's constructor or configuration.
/// </para>
/// <para>
/// The reference key format is provider-defined, allowing each provider to use its
/// native key format (S3 keys, Azure blob names, GCS object names, etc.).
/// </para>
/// </remarks>
public interface IBlobStorageProvider
{
    /// <summary>
    /// Stores blob data and returns a reference key that can be used to retrieve it later.
    /// </summary>
    /// <param name="data">The data stream to store.</param>
    /// <param name="suggestedKey">Optional suggested key for the blob. If null, provider generates a unique key.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A reference key that can be used to retrieve the blob.</returns>
    Task<string> StoreAsync(
        Stream data,
        string? suggestedKey = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Stores blob data with additional options and returns a reference key.
    /// </summary>
    /// <param name="data">The data stream to store.</param>
    /// <param name="options">Options for the store operation including content type, metadata, and tags.</param>
    /// <param name="suggestedKey">Optional suggested key for the blob. If null, provider generates a unique key.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A reference key that can be used to retrieve the blob.</returns>
    /// <remarks>
    /// Not all providers support all options. Unsupported options are silently ignored.
    /// </remarks>
    Task<string> StoreAsync(
        Stream data,
        BlobStoreOptions options,
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
