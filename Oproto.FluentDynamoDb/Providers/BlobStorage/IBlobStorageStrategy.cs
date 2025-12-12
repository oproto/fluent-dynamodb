namespace Oproto.FluentDynamoDb.Providers.BlobStorage;

/// <summary>
/// Interface for coordinating blob storage operations with DynamoDB operations.
/// </summary>
/// <remarks>
/// <para>
/// Implementations of this interface define how blob data is uploaded, cleaned up,
/// and deleted in coordination with DynamoDB write and delete operations.
/// </para>
/// <para>
/// The strategy lifecycle for write operations is:
/// <list type="number">
/// <item><see cref="OnBeforeDynamoDbWriteAsync"/> - Upload blobs before DynamoDB write</item>
/// <item>DynamoDB write operation executes</item>
/// <item><see cref="OnAfterDynamoDbWriteSuccessAsync"/> or <see cref="OnAfterDynamoDbWriteFailureAsync"/></item>
/// </list>
/// </para>
/// <para>
/// The strategy lifecycle for delete operations is:
/// <list type="number">
/// <item><see cref="OnBeforeDynamoDbDeleteAsync"/> - Prepare for blob cleanup</item>
/// <item>DynamoDB delete operation executes</item>
/// <item><see cref="OnAfterDynamoDbDeleteSuccessAsync"/> - Clean up blobs after successful delete</item>
/// </list>
/// </para>
/// </remarks>
public interface IBlobStorageStrategy
{
    /// <summary>
    /// Called before DynamoDB write to upload blob data.
    /// </summary>
    /// <param name="context">The context containing blob properties to upload.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The result containing reference keys for the uploaded blobs.</returns>
    /// <remarks>
    /// This method should upload all blob data and return the reference keys
    /// that will be stored in DynamoDB. If this method throws, the DynamoDB
    /// write will not be attempted.
    /// </remarks>
    Task<BlobWriteResult> OnBeforeDynamoDbWriteAsync(
        BlobWriteContext context,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Called after successful DynamoDB write.
    /// </summary>
    /// <param name="context">The context containing information about the write operation.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    /// <remarks>
    /// This method can be used to finalize or commit blob operations.
    /// For most strategies, this is a no-op since blobs are already stored.
    /// </remarks>
    Task OnAfterDynamoDbWriteSuccessAsync(
        BlobWriteContext context,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Called after failed DynamoDB write.
    /// </summary>
    /// <param name="context">The context containing information about the write operation.</param>
    /// <param name="exception">The exception that caused the DynamoDB write to fail.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    /// <remarks>
    /// This method can be used to clean up uploaded blobs when the DynamoDB
    /// write fails. The implementation determines whether cleanup failures
    /// are propagated or swallowed.
    /// </remarks>
    Task OnAfterDynamoDbWriteFailureAsync(
        BlobWriteContext context,
        Exception exception,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Called before DynamoDB delete to prepare for blob cleanup.
    /// </summary>
    /// <param name="context">The context containing reference keys to be deleted.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The context, potentially enriched with additional information.</returns>
    /// <remarks>
    /// This method is called before the DynamoDB delete to capture the reference
    /// keys that need to be cleaned up after the delete succeeds.
    /// </remarks>
    Task<BlobDeleteContext> OnBeforeDynamoDbDeleteAsync(
        BlobDeleteContext context,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Called after successful DynamoDB delete to clean up blobs.
    /// </summary>
    /// <param name="context">The context containing reference keys to delete.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    /// <remarks>
    /// This method should delete the blobs associated with the deleted entity.
    /// The implementation determines whether cleanup failures are propagated
    /// or swallowed.
    /// </remarks>
    Task OnAfterDynamoDbDeleteSuccessAsync(
        BlobDeleteContext context,
        CancellationToken cancellationToken = default);
}
