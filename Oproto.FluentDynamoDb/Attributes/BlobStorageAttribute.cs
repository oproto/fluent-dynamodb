namespace Oproto.FluentDynamoDb.Attributes;

/// <summary>
/// Marks a property to be stored externally in blob storage (e.g., S3, Azure Blob Storage)
/// with only a reference key persisted in DynamoDB.
/// This is useful for large data that exceeds DynamoDB's 400KB item size limit.
/// </summary>
/// <remarks>
/// <para>
/// Properties marked with this attribute must be of type <c>BlobData&lt;T&gt;</c> where T is the
/// data type to be stored. The blob storage provider must be configured via
/// <c>FluentDynamoDbOptions.WithBlobStorage()</c>.
/// </para>
/// <para>
/// Can be combined with:
/// <list type="bullet">
/// <item><description><c>[JsonBlob]</c> - Serialize objects to JSON before storing</description></item>
/// <item><description><c>[Encrypted]</c> - Encrypt data before storing</description></item>
/// <item><description><c>[Sensitive]</c> - Redact reference keys and values in logs</description></item>
/// </list>
/// </para>
/// </remarks>
/// <example>
/// <code>
/// [DynamoDbTable("Documents")]
/// public partial class Document
/// {
///     [BlobStorage]
///     [DynamoDbAttribute("content")]
///     public BlobData&lt;byte[]&gt; Content { get; set; }
///     
///     [BlobStorage(LazyLoad = true)]
///     [JsonBlob]
///     [DynamoDbAttribute("metadata")]
///     public BlobData&lt;DocumentMetadata&gt; Metadata { get; set; }
/// }
/// </code>
/// </example>
[AttributeUsage(AttributeTargets.Property)]
public sealed class BlobStorageAttribute : Attribute
{
    /// <summary>
    /// Gets or sets whether to defer blob loading until explicitly requested via <c>LoadAsync()</c>.
    /// </summary>
    /// <value>
    /// <c>true</c> to defer loading until <c>LoadAsync()</c> is called (lazy loading);
    /// <c>false</c> to automatically load blob data during entity deserialization (eager loading).
    /// Default is <c>false</c> (eager loading).
    /// </value>
    /// <remarks>
    /// When <c>LazyLoad</c> is <c>false</c> (default), blob data is automatically downloaded
    /// when the entity is retrieved from DynamoDB using <c>FromDynamoDbAsync()</c>.
    /// When <c>LazyLoad</c> is <c>true</c>, the <c>BlobData&lt;T&gt;.Value</c> property will throw
    /// <see cref="InvalidOperationException"/> until <c>LoadAsync()</c> is explicitly called.
    /// </remarks>
    public bool LazyLoad { get; set; } = false;

    /// <summary>
    /// Gets or sets the name of the blob storage provider to use for this property.
    /// </summary>
    /// <value>
    /// The provider name, or <c>null</c> (default) to use the default provider.
    /// </value>
    /// <remarks>
    /// <para>
    /// When <c>null</c> (default), the default provider registered via
    /// <c>FluentDynamoDbOptions.WithBlobStorage(provider)</c> is used.
    /// </para>
    /// <para>
    /// When set to a non-empty value, the named provider registered via
    /// <c>FluentDynamoDbOptions.WithBlobStorage(name, provider)</c> is resolved at runtime.
    /// If no provider is registered for the specified name, an <see cref="InvalidOperationException"/>
    /// is thrown during hydration or serialization.
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// [BlobStorage(Provider = "documents")]
    /// [DynamoDbAttribute("contract")]
    /// public BlobData&lt;byte[]&gt; ContractPdf { get; set; }
    /// </code>
    /// </example>
    public string? Provider { get; set; }
}
