namespace Oproto.FluentDynamoDb.Providers.BlobStorage;

/// <summary>
/// Exception thrown when blob storage operations fail.
/// </summary>
/// <remarks>
/// <para>
/// This exception wraps underlying provider-specific exceptions to provide a consistent
/// error handling experience across different blob storage providers.
/// </para>
/// <para>
/// The <see cref="ReferenceKey"/> property contains the key of the blob involved in the
/// failed operation, when available. This can be useful for logging and debugging.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// try
/// {
///     await document.Content.LoadAsync();
/// }
/// catch (BlobStorageException ex)
/// {
///     logger.LogError(ex, "Failed to load blob {ReferenceKey}", ex.ReferenceKey);
/// }
/// </code>
/// </example>
public class BlobStorageException : Exception
{
    /// <summary>
    /// Gets the reference key of the blob involved in the failed operation.
    /// </summary>
    /// <value>
    /// The reference key if available; otherwise, <c>null</c>.
    /// </value>
    public string? ReferenceKey { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="BlobStorageException"/> class.
    /// </summary>
    public BlobStorageException()
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="BlobStorageException"/> class
    /// with a specified error message.
    /// </summary>
    /// <param name="message">The message that describes the error.</param>
    public BlobStorageException(string message)
        : base(message)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="BlobStorageException"/> class
    /// with a specified error message and a reference to the inner exception.
    /// </summary>
    /// <param name="message">The message that describes the error.</param>
    /// <param name="innerException">The exception that is the cause of the current exception.</param>
    public BlobStorageException(string message, Exception innerException)
        : base(message, innerException)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="BlobStorageException"/> class
    /// with a specified error message, reference key, and inner exception.
    /// </summary>
    /// <param name="message">The message that describes the error.</param>
    /// <param name="referenceKey">The reference key of the blob involved in the failed operation.</param>
    /// <param name="innerException">The exception that is the cause of the current exception.</param>
    public BlobStorageException(string message, string? referenceKey, Exception innerException)
        : base(message, innerException)
    {
        ReferenceKey = referenceKey;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="BlobStorageException"/> class
    /// with a specified error message and reference key.
    /// </summary>
    /// <param name="message">The message that describes the error.</param>
    /// <param name="referenceKey">The reference key of the blob involved in the failed operation.</param>
    public BlobStorageException(string message, string? referenceKey)
        : base(message)
    {
        ReferenceKey = referenceKey;
    }
}
