using System.Collections.Immutable;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using Oproto.FluentDynamoDb.Hydration;
using Oproto.FluentDynamoDb.Logging;
using Oproto.FluentDynamoDb.Providers.BlobStorage;
using Oproto.FluentDynamoDb.Providers.Encryption;

namespace Oproto.FluentDynamoDb;

/// <summary>
/// Configuration options for FluentDynamoDb.
/// Passed to table constructors to configure optional features.
/// Thread-safe and immutable after construction.
/// </summary>
public sealed class FluentDynamoDbOptions
{
    /// <summary>
    /// Gets the logger for DynamoDB operations.
    /// </summary>
    public IDynamoDbLogger Logger { get; private init; } = NoOpLogger.Instance;
    
    /// <summary>
    /// Gets the geospatial provider for spatial queries.
    /// Null if geospatial features are not configured.
    /// </summary>
    public IGeospatialProvider? GeospatialProvider { get; private init; }
    
    /// <summary>
    /// Gets the blob storage provider for large object storage.
    /// Null if blob storage is not configured.
    /// </summary>
    public IBlobStorageProvider? BlobStorageProvider { get; private init; }
    
    /// <summary>
    /// Gets the registry of named blob storage providers.
    /// </summary>
    internal ImmutableDictionary<string, IBlobStorageProvider> NamedBlobProviders { get; private init; }
        = ImmutableDictionary<string, IBlobStorageProvider>.Empty;
    
    /// <summary>
    /// Gets the blob storage strategy for coordinating blob and DynamoDB operations.
    /// When a blob storage provider is configured, defaults to <see cref="BestEffortCleanupStrategy"/>.
    /// Null if blob storage is not configured.
    /// </summary>
    public IBlobStorageStrategy? BlobStorageStrategy { get; private init; }
    
    /// <summary>
    /// Gets the field encryptor for sensitive data.
    /// Null if encryption is not configured.
    /// </summary>
    public IFieldEncryptor? FieldEncryptor { get; private init; }
    
    /// <summary>
    /// Gets the entity hydrator registry for async entity loading.
    /// </summary>
    internal IEntityHydratorRegistry HydratorRegistry { get; private init; } 
        = DefaultEntityHydratorRegistry.Instance;

    /// <summary>
    /// Gets the JSON serializer for [JsonBlob] properties.
    /// Null if JSON blob serialization is not configured.
    /// Configure using .WithSystemTextJson() or .WithNewtonsoftJson() extension methods.
    /// </summary>
    public IJsonBlobSerializer? JsonSerializer { get; private init; }

    /// <summary>
    /// Gets the default setting for consistent reads.
    /// When set to true, all Get and Query request builders will default to consistent reads.
    /// Null means no default is applied (DynamoDB's default eventually consistent reads will be used).
    /// </summary>
    public bool? DefaultConsistentRead { get; private init; }

    /// <summary>
    /// Gets the default setting for return consumed capacity.
    /// When set, all request builders will default to returning consumed capacity at the specified level.
    /// Null means no default is applied.
    /// </summary>
    public ReturnConsumedCapacity? DefaultReturnConsumedCapacity { get; private init; }

    /// <summary>
    /// Gets the default setting for return item collection metrics.
    /// When set, write request builders (Put, Update, Delete) will default to returning item collection metrics.
    /// Null means no default is applied.
    /// </summary>
    public ReturnItemCollectionMetrics? DefaultReturnItemCollectionMetrics { get; private init; }

    /// <summary>
    /// Gets the default setting for return values on write operations.
    /// When set, Update and Delete request builders will default to the specified return values.
    /// Null means no default is applied.
    /// </summary>
    public ReturnValue? DefaultReturnValues { get; private init; }

    /// <summary>
    /// Gets the default key input mode used when operations specify KeyInputMode.Default.
    /// Default value: KeyInputMode.Auto
    /// </summary>
    public KeyInputMode DefaultKeyInputMode { get; private init; } = KeyInputMode.Auto;

    /// <summary>
    /// Creates a new options instance with the specified logger.
    /// </summary>
    /// <param name="logger">The logger to use. If null, uses NoOpLogger.Instance.</param>
    /// <returns>A new FluentDynamoDbOptions instance with the specified logger.</returns>
    public FluentDynamoDbOptions WithLogger(IDynamoDbLogger? logger)
        => CloneWith(logger: logger ?? NoOpLogger.Instance);
    
    /// <summary>
    /// Creates a new options instance with the specified blob storage provider.
    /// When a provider is configured and no strategy is set, <see cref="BestEffortCleanupStrategy"/> is used as the default.
    /// </summary>
    /// <param name="provider">The blob storage provider to use.</param>
    /// <returns>A new FluentDynamoDbOptions instance with the specified blob storage provider.</returns>
    /// <example>
    /// <code>
    /// var options = new FluentDynamoDbOptions()
    ///     .WithBlobStorage(new S3BlobProvider(s3Client, "my-bucket"));
    /// 
    /// // BestEffortCleanupStrategy is automatically configured as the default strategy
    /// </code>
    /// </example>
    public FluentDynamoDbOptions WithBlobStorage(IBlobStorageProvider? provider)
    {
        if (provider == null)
        {
            return CloneWith(
                blobStorageProvider: null, 
                blobStorageStrategy: null, 
                setBlobStorageProvider: true,
                setBlobStorageStrategy: true);
        }
        
        // Set default strategy if not already configured
        var strategy = BlobStorageStrategy ?? new BestEffortCleanupStrategy(provider, Logger);
        return CloneWith(blobStorageProvider: provider, blobStorageStrategy: strategy);
    }

    /// <summary>
    /// Creates a new options instance with the specified named blob storage provider registered.
    /// </summary>
    /// <param name="name">The provider name. Must not be null, empty, or whitespace.</param>
    /// <param name="provider">The blob storage provider instance. Must not be null.</param>
    /// <returns>A new FluentDynamoDbOptions instance with the named provider registered.</returns>
    /// <exception cref="ArgumentException">Thrown when name is null, empty, or whitespace.</exception>
    /// <exception cref="ArgumentNullException">Thrown when provider is null.</exception>
    public FluentDynamoDbOptions WithBlobStorage(string name, IBlobStorageProvider provider)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(provider);

        return CloneWith(namedBlobProviders: NamedBlobProviders.SetItem(name, provider));
    }
    
    /// <summary>
    /// Gets the blob storage provider for the given name.
    /// </summary>
    /// <param name="name">
    /// The provider name, or <c>null</c>/<c>""</c> to get the default provider
    /// registered via <see cref="WithBlobStorage(IBlobStorageProvider?)"/>.
    /// </param>
    /// <returns>The resolved <see cref="IBlobStorageProvider"/>.</returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the requested provider is not registered. The exception message includes
    /// the requested name and, when other providers are registered, lists all available provider names.
    /// </exception>
    public IBlobStorageProvider GetBlobProvider(string? name)
    {
        if (string.IsNullOrEmpty(name))
        {
            return BlobStorageProvider
                ?? throw new InvalidOperationException(
                    "No default blob storage provider has been configured. " +
                    "Call .WithBlobStorage(provider) on FluentDynamoDbOptions to register one.");
        }

        if (NamedBlobProviders.TryGetValue(name, out var provider))
        {
            return provider;
        }

        var message = NamedBlobProviders.IsEmpty
            ? $"Named blob storage provider '{name}' is not registered and no named providers have been configured. " +
              $"Call .WithBlobStorage(\"{name}\", provider) on FluentDynamoDbOptions to register it."
            : $"Named blob storage provider '{name}' is not registered. " +
              $"Available providers: {string.Join(", ", NamedBlobProviders.Keys.OrderBy(k => k))}. " +
              $"Call .WithBlobStorage(\"{name}\", provider) on FluentDynamoDbOptions to register it.";

        throw new InvalidOperationException(message);
    }

    /// <summary>
    /// Creates a new options instance with the specified blob storage strategy.
    /// </summary>
    /// <param name="strategy">The blob storage strategy to use for coordinating blob and DynamoDB operations.</param>
    /// <returns>A new FluentDynamoDbOptions instance with the specified blob storage strategy.</returns>
    /// <remarks>
    /// Use this method to override the default <see cref="BestEffortCleanupStrategy"/> with a custom strategy
    /// such as <see cref="NoCleanupStrategy"/> or your own implementation.
    /// </remarks>
    /// <example>
    /// <code>
    /// var options = new FluentDynamoDbOptions()
    ///     .WithBlobStorage(new S3BlobProvider(s3Client, "my-bucket"))
    ///     .WithBlobStorageStrategy(new NoCleanupStrategy(provider));
    /// </code>
    /// </example>
    public FluentDynamoDbOptions WithBlobStorageStrategy(IBlobStorageStrategy? strategy)
        => CloneWith(blobStorageStrategy: strategy, setBlobStorageStrategy: true);
    
    /// <summary>
    /// Creates a new options instance with the specified field encryptor.
    /// </summary>
    /// <param name="encryptor">The field encryptor to use.</param>
    /// <returns>A new FluentDynamoDbOptions instance with the specified field encryptor.</returns>
    public FluentDynamoDbOptions WithEncryption(IFieldEncryptor? encryptor)
        => CloneWith(fieldEncryptor: encryptor);
    
    /// <summary>
    /// Creates a new options instance with the specified geospatial provider.
    /// This method is internal and used by the Geospatial package extension methods.
    /// </summary>
    /// <param name="provider">The geospatial provider to use.</param>
    /// <returns>A new FluentDynamoDbOptions instance with the specified geospatial provider.</returns>
    internal FluentDynamoDbOptions WithGeospatialProvider(IGeospatialProvider? provider)
        => CloneWith(geospatialProvider: provider);
    
    /// <summary>
    /// Creates a new options instance with the specified hydrator registry.
    /// This method is internal and used for testing and advanced scenarios.
    /// </summary>
    /// <param name="registry">The hydrator registry to use.</param>
    /// <returns>A new FluentDynamoDbOptions instance with the specified hydrator registry.</returns>
    internal FluentDynamoDbOptions WithHydratorRegistry(IEntityHydratorRegistry registry)
        => CloneWith(hydratorRegistry: registry);

    /// <summary>
    /// Creates a new options instance with the specified JSON serializer.
    /// </summary>
    /// <param name="serializer">The JSON serializer to use for [JsonBlob] properties.</param>
    /// <returns>A new FluentDynamoDbOptions instance with the specified JSON serializer.</returns>
    public FluentDynamoDbOptions WithJsonSerializer(IJsonBlobSerializer? serializer)
        => CloneWith(jsonSerializer: serializer, setJsonSerializer: true);

    /// <summary>
    /// Creates a new options instance with consistent reads enabled or disabled by default.
    /// When enabled, all Get and Query request builders will default to consistent reads.
    /// </summary>
    /// <param name="value">True to enable consistent reads by default, false to disable.</param>
    /// <returns>A new FluentDynamoDbOptions instance with the specified consistent read setting.</returns>
    /// <example>
    /// <code>
    /// var options = new FluentDynamoDbOptions()
    ///     .UseConsistentRead(true);
    /// 
    /// // All Get and Query operations will now use consistent reads by default
    /// var item = await table.Users.Get(userId).GetItemAsync();
    /// </code>
    /// </example>
    public FluentDynamoDbOptions UseConsistentRead(bool value = true)
        => CloneWith(defaultConsistentRead: value);

    /// <summary>
    /// Creates a new options instance with the specified default return consumed capacity setting.
    /// When set, all request builders will default to returning consumed capacity at the specified level.
    /// </summary>
    /// <param name="value">The level of consumed capacity information to return by default.</param>
    /// <returns>A new FluentDynamoDbOptions instance with the specified return consumed capacity setting.</returns>
    /// <example>
    /// <code>
    /// var options = new FluentDynamoDbOptions()
    ///     .ReturnConsumedCapacity(ReturnConsumedCapacity.TOTAL);
    /// 
    /// // All operations will now return consumed capacity by default
    /// var response = await table.Users.Query()
    ///     .Where(x => x.Pk == tenantId)
    ///     .ToDynamoDbResponseAsync();
    /// // response.ConsumedCapacity will be populated
    /// </code>
    /// </example>
    public FluentDynamoDbOptions ReturnConsumedCapacity(ReturnConsumedCapacity value)
        => CloneWith(defaultReturnConsumedCapacity: value);

    /// <summary>
    /// Creates a new options instance with the specified default return item collection metrics setting.
    /// When set, write request builders (Put, Update, Delete) will default to returning item collection metrics.
    /// </summary>
    /// <param name="value">The level of item collection metrics to return by default.</param>
    /// <returns>A new FluentDynamoDbOptions instance with the specified return item collection metrics setting.</returns>
    /// <example>
    /// <code>
    /// var options = new FluentDynamoDbOptions()
    ///     .ReturnItemCollectionMetrics(ReturnItemCollectionMetrics.SIZE);
    /// 
    /// // All write operations will now return item collection metrics by default
    /// var response = await table.Users.Put()
    ///     .WithItem(user)
    ///     .ToDynamoDbResponseAsync();
    /// // response.ItemCollectionMetrics will be populated
    /// </code>
    /// </example>
    public FluentDynamoDbOptions ReturnItemCollectionMetrics(ReturnItemCollectionMetrics value)
        => CloneWith(defaultReturnItemCollectionMetrics: value);

    /// <summary>
    /// Creates a new options instance with the specified default return values setting.
    /// When set, Put, Update, and Delete request builders will default to the specified return values.
    /// </summary>
    /// <param name="value">The return value option to use by default.</param>
    /// <returns>A new FluentDynamoDbOptions instance with the specified return values setting.</returns>
    /// <example>
    /// <code>
    /// var options = new FluentDynamoDbOptions()
    ///     .ReturnValues(ReturnValue.ALL_NEW);
    /// 
    /// // All update operations will now return the new values by default
    /// var response = await table.Users.Update()
    ///     .WithKey(userId)
    ///     .Set("SET #name = :name")
    ///     .WithAttribute("#name", "name")
    ///     .WithValue(":name", "John")
    ///     .ToDynamoDbResponseAsync();
    /// // response.Attributes will contain the updated item
    /// </code>
    /// </example>
    public FluentDynamoDbOptions ReturnValues(ReturnValue value)
        => CloneWith(defaultReturnValues: value);

    /// <summary>
    /// Creates a new options instance with the specified default key input mode.
    /// </summary>
    /// <param name="mode">The key input mode to use as the default. Cannot be KeyInputMode.Default.</param>
    /// <returns>A new FluentDynamoDbOptions instance with the specified key input mode.</returns>
    /// <exception cref="ArgumentException">Thrown when KeyInputMode.Default is specified.</exception>
    public FluentDynamoDbOptions UseKeyInputMode(KeyInputMode mode)
    {
        if (mode == KeyInputMode.Default)
            throw new ArgumentException(
                "KeyInputMode.Default is only valid as a per-call parameter value. " +
                "Specify Auto, Value, or Raw for the global default.",
                nameof(mode));
        return CloneWith(defaultKeyInputMode: mode);
    }

    /// <summary>
    /// Creates a clone of this options instance with the specified overrides.
    /// </summary>
    private FluentDynamoDbOptions CloneWith(
        IDynamoDbLogger? logger = null,
        IGeospatialProvider? geospatialProvider = null,
        IBlobStorageProvider? blobStorageProvider = null,
        IBlobStorageStrategy? blobStorageStrategy = null,
        IFieldEncryptor? fieldEncryptor = null,
        IEntityHydratorRegistry? hydratorRegistry = null,
        IJsonBlobSerializer? jsonSerializer = null,
        bool? defaultConsistentRead = null,
        ReturnConsumedCapacity? defaultReturnConsumedCapacity = null,
        ReturnItemCollectionMetrics? defaultReturnItemCollectionMetrics = null,
        ReturnValue? defaultReturnValues = null,
        KeyInputMode? defaultKeyInputMode = null,
        ImmutableDictionary<string, IBlobStorageProvider>? namedBlobProviders = null,
        bool setJsonSerializer = false,
        bool setBlobStorageProvider = false,
        bool setBlobStorageStrategy = false)
    {
        return new FluentDynamoDbOptions
        {
            Logger = logger ?? Logger,
            GeospatialProvider = geospatialProvider ?? GeospatialProvider,
            BlobStorageProvider = setBlobStorageProvider ? blobStorageProvider : (blobStorageProvider ?? BlobStorageProvider),
            BlobStorageStrategy = setBlobStorageStrategy ? blobStorageStrategy : (blobStorageStrategy ?? BlobStorageStrategy),
            FieldEncryptor = fieldEncryptor ?? FieldEncryptor,
            HydratorRegistry = hydratorRegistry ?? HydratorRegistry,
            JsonSerializer = setJsonSerializer ? jsonSerializer : (jsonSerializer ?? JsonSerializer),
            DefaultConsistentRead = defaultConsistentRead ?? DefaultConsistentRead,
            DefaultReturnConsumedCapacity = defaultReturnConsumedCapacity ?? DefaultReturnConsumedCapacity,
            DefaultReturnItemCollectionMetrics = defaultReturnItemCollectionMetrics ?? DefaultReturnItemCollectionMetrics,
            DefaultReturnValues = defaultReturnValues ?? DefaultReturnValues,
            DefaultKeyInputMode = defaultKeyInputMode ?? DefaultKeyInputMode,
            NamedBlobProviders = namedBlobProviders ?? NamedBlobProviders
        };
    }
}
