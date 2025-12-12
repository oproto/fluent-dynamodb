using Oproto.FluentDynamoDb.Logging;

namespace Oproto.FluentDynamoDb.Providers.BlobStorage;

/// <summary>
/// A blob storage strategy that attempts best-effort cleanup of orphaned blobs.
/// </summary>
/// <remarks>
/// <para>
/// This strategy uploads blobs before DynamoDB writes and attempts to clean up
/// orphaned blobs if the DynamoDB write fails. Cleanup failures are logged but
/// do not throw exceptions, hence "best effort".
/// </para>
/// <para>
/// This is the default strategy when a blob storage provider is configured.
/// </para>
/// <para>
/// Lifecycle behavior:
/// <list type="bullet">
/// <item><see cref="OnBeforeDynamoDbWriteAsync"/>: Uploads all blob data to storage</item>
/// <item><see cref="OnAfterDynamoDbWriteSuccessAsync"/>: No-op (blobs already stored)</item>
/// <item><see cref="OnAfterDynamoDbWriteFailureAsync"/>: Attempts to delete uploaded blobs</item>
/// <item><see cref="OnBeforeDynamoDbDeleteAsync"/>: Captures reference keys for cleanup</item>
/// <item><see cref="OnAfterDynamoDbDeleteSuccessAsync"/>: Attempts to delete associated blobs</item>
/// </list>
/// </para>
/// </remarks>
/// <example>
/// <code>
/// var options = new FluentDynamoDbOptions()
///     .WithBlobStorage(new S3BlobProvider(s3Client, "my-bucket"))
///     .WithBlobStorageStrategy(new BestEffortCleanupStrategy(provider, logger));
/// </code>
/// </example>
public sealed class BestEffortCleanupStrategy : IBlobStorageStrategy
{
    private readonly IBlobStorageProvider _provider;
    private readonly IDynamoDbLogger? _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="BestEffortCleanupStrategy"/> class.
    /// </summary>
    /// <param name="provider">The blob storage provider to use for operations.</param>
    /// <param name="logger">Optional logger for diagnostic output.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="provider"/> is null.</exception>
    public BestEffortCleanupStrategy(IBlobStorageProvider provider, IDynamoDbLogger? logger = null)
    {
        _provider = provider ?? throw new ArgumentNullException(nameof(provider));
        _logger = logger;
    }

    /// <inheritdoc />
    /// <remarks>
    /// Uploads all blob data to storage and returns the assigned reference keys.
    /// The reference keys are stored in the context for potential cleanup if
    /// the DynamoDB write fails.
    /// </remarks>
    public async Task<BlobWriteResult> OnBeforeDynamoDbWriteAsync(
        BlobWriteContext context,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        var referenceKeys = new Dictionary<string, string>();

        foreach (var prop in context.BlobProperties)
        {
            try
            {
                var options = new BlobStoreOptions { ContentType = prop.ContentType };
                var key = await _provider.StoreAsync(
                    prop.Data,
                    options,
                    prop.ExistingReferenceKey,
                    cancellationToken);

                referenceKeys[prop.PropertyName] = key;

                _logger?.LogDebug(
                    LogEventIds.BlobUploadSuccess,
                    "Uploaded blob for property {PropertyName}. Key: {ReferenceKey}",
                    prop.PropertyName,
                    key);
            }
            catch (Exception ex)
            {
                _logger?.LogError(
                    LogEventIds.BlobUploadFailed,
                    ex,
                    "Failed to upload blob for property {PropertyName}",
                    prop.PropertyName);

                // Clean up any blobs we already uploaded before re-throwing
                await CleanupUploadedBlobsAsync(referenceKeys, cancellationToken);

                throw new BlobStorageException(
                    $"Failed to upload blob for property '{prop.PropertyName}'",
                    prop.ExistingReferenceKey,
                    ex);
            }
        }

        context.UploadedReferenceKeys = referenceKeys;
        return new BlobWriteResult { ReferenceKeys = referenceKeys };
    }

    /// <inheritdoc />
    /// <remarks>
    /// No action needed on success - blobs are already stored.
    /// </remarks>
    public Task OnAfterDynamoDbWriteSuccessAsync(
        BlobWriteContext context,
        CancellationToken cancellationToken = default)
    {
        // Nothing to do on success - blobs are already stored
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    /// <remarks>
    /// Attempts to delete all blobs that were uploaded before the DynamoDB write failed.
    /// Cleanup failures are logged but do not throw exceptions.
    /// </remarks>
    public async Task OnAfterDynamoDbWriteFailureAsync(
        BlobWriteContext context,
        Exception exception,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (context.UploadedReferenceKeys == null || context.UploadedReferenceKeys.Count == 0)
        {
            return;
        }

        _logger?.LogDebug(
            LogEventIds.BlobCleanupSuccess,
            "Attempting to clean up {Count} orphaned blob(s) after DynamoDB write failure",
            context.UploadedReferenceKeys.Count);

        await CleanupUploadedBlobsAsync(context.UploadedReferenceKeys, cancellationToken);
    }

    /// <inheritdoc />
    /// <remarks>
    /// Captures the reference keys for cleanup after the delete succeeds.
    /// </remarks>
    public Task<BlobDeleteContext> OnBeforeDynamoDbDeleteAsync(
        BlobDeleteContext context,
        CancellationToken cancellationToken = default)
    {
        // Store reference keys for cleanup after successful delete
        return Task.FromResult(context);
    }

    /// <inheritdoc />
    /// <remarks>
    /// Attempts to delete all blobs associated with the deleted entity.
    /// Cleanup failures are logged but do not throw exceptions.
    /// </remarks>
    public async Task OnAfterDynamoDbDeleteSuccessAsync(
        BlobDeleteContext context,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        foreach (var referenceKey in context.ReferenceKeys)
        {
            try
            {
                await _provider.DeleteAsync(referenceKey, cancellationToken);

                _logger?.LogDebug(
                    LogEventIds.BlobDeleteSuccess,
                    "Deleted blob after entity deletion. Key: {ReferenceKey}",
                    referenceKey);
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(
                    LogEventIds.BlobDeleteFailed,
                    "Failed to delete blob after entity deletion. Key: {ReferenceKey}, Error: {Error}",
                    referenceKey,
                    ex.Message);
                // Continue without throwing - best effort cleanup
            }
        }
    }

    private async Task CleanupUploadedBlobsAsync(
        IReadOnlyDictionary<string, string> referenceKeys,
        CancellationToken cancellationToken)
    {
        foreach (var (propertyName, referenceKey) in referenceKeys)
        {
            try
            {
                await _provider.DeleteAsync(referenceKey, cancellationToken);

                _logger?.LogDebug(
                    LogEventIds.BlobCleanupSuccess,
                    "Cleaned up orphaned blob after DynamoDB write failure. Property: {PropertyName}, Key: {ReferenceKey}",
                    propertyName,
                    referenceKey);
            }
            catch (Exception cleanupEx)
            {
                _logger?.LogWarning(
                    LogEventIds.BlobCleanupFailed,
                    "Failed to clean up orphaned blob after DynamoDB write failure. Property: {PropertyName}, Key: {ReferenceKey}, Error: {Error}",
                    propertyName,
                    referenceKey,
                    cleanupEx.Message);
                // Continue without throwing - best effort cleanup
            }
        }
    }
}
