namespace Oproto.FluentDynamoDb.Providers.BlobStorage;

/// <summary>
/// A blob storage strategy that performs no cleanup of orphaned blobs.
/// </summary>
/// <remarks>
/// <para>
/// This strategy uploads blobs before DynamoDB writes but does not attempt
/// to clean up orphaned blobs if the DynamoDB write fails, nor does it delete
/// blobs when entities are deleted.
/// </para>
/// <para>
/// Use this strategy when:
/// <list type="bullet">
/// <item>Orphaned blobs are acceptable (e.g., non-critical data)</item>
/// <item>You have a separate cleanup process (e.g., S3 lifecycle rules)</item>
/// <item>You want the simplest possible implementation</item>
/// </list>
/// </para>
/// <para>
/// Lifecycle behavior:
/// <list type="bullet">
/// <item><see cref="OnBeforeDynamoDbWriteAsync"/>: Uploads all blob data to storage</item>
/// <item><see cref="OnAfterDynamoDbWriteSuccessAsync"/>: No-op</item>
/// <item><see cref="OnAfterDynamoDbWriteFailureAsync"/>: No-op (no cleanup)</item>
/// <item><see cref="OnBeforeDynamoDbDeleteAsync"/>: No-op</item>
/// <item><see cref="OnAfterDynamoDbDeleteSuccessAsync"/>: No-op (no cleanup)</item>
/// </list>
/// </para>
/// </remarks>
/// <example>
/// <code>
/// var options = new FluentDynamoDbOptions()
///     .WithBlobStorage(new S3BlobProvider(s3Client, "my-bucket"))
///     .WithBlobStorageStrategy(new NoCleanupStrategy(provider));
/// </code>
/// </example>
public sealed class NoCleanupStrategy : IBlobStorageStrategy
{
    private readonly IBlobStorageProvider _provider;

    /// <summary>
    /// Initializes a new instance of the <see cref="NoCleanupStrategy"/> class.
    /// </summary>
    /// <param name="provider">The blob storage provider to use for operations.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="provider"/> is null.</exception>
    public NoCleanupStrategy(IBlobStorageProvider provider)
    {
        _provider = provider ?? throw new ArgumentNullException(nameof(provider));
    }

    /// <inheritdoc />
    /// <remarks>
    /// Uploads all blob data to storage and returns the assigned reference keys.
    /// </remarks>
    public async Task<BlobWriteResult> OnBeforeDynamoDbWriteAsync(
        BlobWriteContext context,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        var referenceKeys = new Dictionary<string, string>();

        foreach (var prop in context.BlobProperties)
        {
            var options = new BlobStoreOptions { ContentType = prop.ContentType };
            var key = await _provider.StoreAsync(
                prop.Data,
                options,
                prop.ExistingReferenceKey,
                cancellationToken).ConfigureAwait(false);

            referenceKeys[prop.PropertyName] = key;
        }

        return new BlobWriteResult { ReferenceKeys = referenceKeys };
    }

    /// <inheritdoc />
    /// <remarks>
    /// No action needed on success.
    /// </remarks>
    public Task OnAfterDynamoDbWriteSuccessAsync(
        BlobWriteContext context,
        CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    /// <remarks>
    /// No cleanup is performed - orphaned blobs are left in storage.
    /// </remarks>
    public Task OnAfterDynamoDbWriteFailureAsync(
        BlobWriteContext context,
        Exception exception,
        CancellationToken cancellationToken = default)
    {
        // No cleanup - orphaned blobs are acceptable with this strategy
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    /// <remarks>
    /// No preparation needed since no cleanup will be performed.
    /// </remarks>
    public Task<BlobDeleteContext> OnBeforeDynamoDbDeleteAsync(
        BlobDeleteContext context,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(context);
    }

    /// <inheritdoc />
    /// <remarks>
    /// No cleanup is performed - blobs are left in storage after entity deletion.
    /// </remarks>
    public Task OnAfterDynamoDbDeleteSuccessAsync(
        BlobDeleteContext context,
        CancellationToken cancellationToken = default)
    {
        // No cleanup - blobs are left in storage
        return Task.CompletedTask;
    }
}
