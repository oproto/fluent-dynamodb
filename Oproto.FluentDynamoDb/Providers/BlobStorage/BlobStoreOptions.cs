namespace Oproto.FluentDynamoDb.Providers.BlobStorage;

/// <summary>
/// Configuration options for blob storage operations.
/// </summary>
/// <remarks>
/// <para>
/// This class provides optional metadata that can be passed to blob storage providers
/// when storing data. Not all providers support all options - unsupported options
/// are silently ignored.
/// </para>
/// <para>
/// This type is cloud-agnostic and does not contain any provider-specific configuration.
/// Provider-specific settings (bucket names, container names, etc.) should be configured
/// on the provider implementation itself.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// var options = new BlobStoreOptions
/// {
///     ContentType = "application/json",
///     Metadata = new Dictionary&lt;string, string&gt;
///     {
///         ["created-by"] = "my-application",
///         ["version"] = "1.0"
///     },
///     Tags = new Dictionary&lt;string, string&gt;
///     {
///         ["environment"] = "production",
///         ["cost-center"] = "engineering"
///     }
/// };
/// </code>
/// </example>
public sealed class BlobStoreOptions
{
    /// <summary>
    /// Gets or sets the content type (MIME type) of the blob.
    /// </summary>
    /// <value>
    /// The MIME type of the content (e.g., "application/json", "application/octet-stream").
    /// <c>null</c> to let the provider determine the content type.
    /// </value>
    /// <remarks>
    /// When combined with <c>[JsonBlob]</c>, this is typically set to "application/json" automatically.
    /// </remarks>
    public string? ContentType { get; set; }

    /// <summary>
    /// Gets or sets custom metadata to store with the blob.
    /// </summary>
    /// <value>
    /// A dictionary of key-value pairs for custom metadata.
    /// <c>null</c> if no custom metadata is needed.
    /// </value>
    /// <remarks>
    /// <para>
    /// Metadata is stored alongside the blob and can be retrieved without downloading
    /// the blob content. This is useful for storing information about the blob
    /// such as creation time, source application, or version information.
    /// </para>
    /// <para>
    /// Provider-specific limitations may apply to metadata key names and values.
    /// For S3, keys must be lowercase and values are limited to ASCII characters.
    /// </para>
    /// </remarks>
    public IDictionary<string, string>? Metadata { get; set; }

    /// <summary>
    /// Gets or sets tags to apply to the blob.
    /// </summary>
    /// <value>
    /// A dictionary of key-value pairs for object tags.
    /// <c>null</c> if no tags are needed.
    /// </value>
    /// <remarks>
    /// <para>
    /// Tags are used for categorization and cost allocation. Unlike metadata,
    /// tags can be used for querying and filtering blobs in some providers.
    /// </para>
    /// <para>
    /// Not all providers support tags. For providers that don't support tags,
    /// this property is silently ignored.
    /// </para>
    /// </remarks>
    public IDictionary<string, string>? Tags { get; set; }
}
