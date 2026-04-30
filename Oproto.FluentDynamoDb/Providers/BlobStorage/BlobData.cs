namespace Oproto.FluentDynamoDb.Providers.BlobStorage;

/// <summary>
/// A wrapper type that encapsulates blob storage behavior including lazy loading,
/// reference key access, and data retrieval.
/// </summary>
/// <typeparam name="T">The type of data stored in the blob.</typeparam>
/// <remarks>
/// <para>
/// <c>BlobData&lt;T&gt;</c> provides control over when blob data is loaded from external storage.
/// Use <see cref="Create"/> to create instances with data to be stored, and
/// <see cref="LoadAsync"/> to retrieve data from storage.
/// </para>
/// <para>
/// This type is designed to be AOT-compatible and does not use reflection.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// // Creating new blob data
/// var document = new Document
/// {
///     Content = BlobData&lt;byte[]&gt;.Create(fileBytes)
/// };
/// 
/// // Accessing loaded data
/// if (document.Content.IsLoaded)
/// {
///     var bytes = document.Content.Value;
/// }
/// 
/// // Lazy loading
/// await document.Content.LoadAsync();
/// var bytes = document.Content.Value;
/// </code>
/// </example>
public sealed class BlobData<T>
{
    private T? _value;
    private IBlobStorageProvider? _provider;
    private Func<Stream, CancellationToken, Task<T>>? _deserializer;
    private bool _isLoaded;
    private readonly object _loadLock = new();
    private Task? _loadTask;

    /// <summary>
    /// Gets the loaded data value.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the data has not been loaded. Call <see cref="LoadAsync"/> first
    /// or configure eager loading with <c>[BlobStorage(LazyLoad = false)]</c>.
    /// </exception>
    public T Value
    {
        get
        {
            if (!_isLoaded)
            {
                throw new InvalidOperationException(
                    "Blob data has not been loaded. Call LoadAsync() first or configure eager loading.");
            }
            return _value!;
        }
    }


    /// <summary>
    /// Gets the reference key for the stored blob, or <c>null</c> if not yet stored.
    /// </summary>
    /// <remarks>
    /// The reference key is the identifier used by the blob storage provider to locate the data.
    /// It is set after the blob is stored or when the instance is created from a reference key
    /// during deserialization.
    /// </remarks>
    public string? ReferenceKey { get; private set; }

    /// <summary>
    /// Gets whether the blob data has been loaded from storage.
    /// </summary>
    /// <value>
    /// <c>true</c> if the data has been loaded and <see cref="Value"/> can be accessed;
    /// <c>false</c> if <see cref="LoadAsync"/> must be called first.
    /// </value>
    public bool IsLoaded => _isLoaded;

    /// <summary>
    /// Gets whether this instance has data to be stored.
    /// </summary>
    /// <value>
    /// <c>true</c> if this instance was created via <see cref="Create"/> and contains
    /// data that needs to be uploaded to blob storage;
    /// <c>false</c> if this instance was created from a reference key or has no data.
    /// </value>
    public bool HasPendingData { get; private set; }

    /// <summary>
    /// Creates a new <see cref="BlobData{T}"/> instance with data to be stored.
    /// </summary>
    /// <param name="value">The data value to store.</param>
    /// <returns>A new <see cref="BlobData{T}"/> instance with the data loaded and marked for storage.</returns>
    /// <remarks>
    /// The returned instance has <see cref="IsLoaded"/> set to <c>true</c> and
    /// <see cref="HasPendingData"/> set to <c>true</c>. The <see cref="ReferenceKey"/>
    /// will be <c>null</c> until the data is stored by the blob storage strategy.
    /// </remarks>
    public static BlobData<T> Create(T value)
    {
        return new BlobData<T>
        {
            _value = value,
            _isLoaded = true,
            HasPendingData = true
        };
    }

    /// <summary>
    /// Creates a <see cref="BlobData{T}"/> instance from a reference key for deserialization.
    /// </summary>
    /// <param name="referenceKey">The reference key pointing to the stored blob.</param>
    /// <param name="provider">The blob storage provider to use for loading.</param>
    /// <param name="deserializer">The function to deserialize the blob data from a stream.</param>
    /// <returns>A new <see cref="BlobData{T}"/> instance that can load data from storage.</returns>
    /// <remarks>
    /// This method is intended for internal use by the source generator during entity deserialization.
    /// The returned instance has <see cref="IsLoaded"/> set to <c>false</c> until
    /// <see cref="LoadAsync"/> is called.
    /// </remarks>
    internal static BlobData<T> FromReferenceKey(
        string referenceKey,
        IBlobStorageProvider? provider,
        Func<Stream, CancellationToken, Task<T>>? deserializer)
    {
        return new BlobData<T>
        {
            ReferenceKey = referenceKey,
            _provider = provider,
            _deserializer = deserializer,
            _isLoaded = false,
            HasPendingData = false
        };
    }

    /// <summary>
    /// Loads the blob data from storage asynchronously.
    /// </summary>
    /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
    /// <returns>A task representing the asynchronous load operation.</returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown when no blob storage provider is configured or when no reference key is available.
    /// </exception>
    /// <exception cref="BlobStorageException">
    /// Thrown when the blob storage provider fails to retrieve the data.
    /// </exception>
    /// <remarks>
    /// This method is idempotent - calling it multiple times will only fetch the data once.
    /// Subsequent calls return immediately without re-fetching.
    /// </remarks>
    public Task LoadAsync(CancellationToken cancellationToken = default)
    {
        // Fast path: already loaded
        if (_isLoaded)
        {
            return Task.CompletedTask;
        }

        // Thread-safe loading with idempotence
        lock (_loadLock)
        {
            // Double-check after acquiring lock
            if (_isLoaded)
            {
                return Task.CompletedTask;
            }

            // If a load is already in progress, return the existing task
            if (_loadTask != null)
            {
                return _loadTask;
            }

            // Start the load operation
            _loadTask = LoadInternalAsync(cancellationToken);
            return _loadTask;
        }
    }

    private async Task LoadInternalAsync(CancellationToken cancellationToken)
    {
        if (_provider == null)
        {
            throw new InvalidOperationException(
                "Cannot load blob data: no blob storage provider configured. " +
                "Call FluentDynamoDbOptions.WithBlobStorage() to configure a provider.");
        }

        if (string.IsNullOrEmpty(ReferenceKey))
        {
            throw new InvalidOperationException(
                "Cannot load blob data: no reference key available.");
        }

        if (_deserializer == null)
        {
            throw new InvalidOperationException(
                "Cannot load blob data: no deserializer configured.");
        }

        try
        {
            await using var stream = await _provider.RetrieveAsync(ReferenceKey, cancellationToken).ConfigureAwait(false);
            _value = await _deserializer(stream, cancellationToken).ConfigureAwait(false);
            _isLoaded = true;
        }
        catch (Exception ex) when (ex is not InvalidOperationException and not BlobStorageException)
        {
            throw new BlobStorageException(
                $"Failed to load blob data from storage. ReferenceKey: {ReferenceKey}",
                ReferenceKey,
                ex);
        }
    }

    /// <summary>
    /// Sets the reference key after the blob has been stored.
    /// </summary>
    /// <param name="referenceKey">The reference key assigned by the blob storage provider.</param>
    /// <remarks>
    /// This method is intended for internal use by the blob storage strategy after
    /// successfully storing the blob data.
    /// </remarks>
    internal void SetReferenceKey(string referenceKey)
    {
        ReferenceKey = referenceKey;
        HasPendingData = false;
    }

    /// <summary>
    /// Sets the value directly for eager loading scenarios.
    /// </summary>
    /// <param name="value">The loaded value.</param>
    /// <remarks>
    /// This method is intended for internal use by the source generator during
    /// eager loading in <c>FromDynamoDbAsync()</c>.
    /// </remarks>
    internal void SetLoadedValue(T value)
    {
        _value = value;
        _isLoaded = true;
    }

    /// <summary>
    /// Gets the pending data value for serialization.
    /// </summary>
    /// <returns>The data value if pending, or default if not.</returns>
    /// <remarks>
    /// This method is intended for internal use by the blob storage strategy
    /// when uploading pending data.
    /// </remarks>
    internal T? GetPendingValue()
    {
        return HasPendingData ? _value : default;
    }

    private BlobData()
    {
    }
}
