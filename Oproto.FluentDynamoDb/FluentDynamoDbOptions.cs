using Oproto.FluentDynamoDb.Logging;
using Oproto.FluentDynamoDb.Storage;

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
    /// Creates a new options instance with the specified logger.
    /// </summary>
    /// <param name="logger">The logger to use. If null, uses NoOpLogger.Instance.</param>
    /// <returns>A new FluentDynamoDbOptions instance with the specified logger.</returns>
    public FluentDynamoDbOptions WithLogger(IDynamoDbLogger? logger)
        => new() 
        { 
            Logger = logger ?? NoOpLogger.Instance,
            GeospatialProvider = GeospatialProvider,
            BlobStorageProvider = BlobStorageProvider,
            FieldEncryptor = FieldEncryptor,
            HydratorRegistry = HydratorRegistry,
            JsonSerializer = JsonSerializer
        };
    
    /// <summary>
    /// Creates a new options instance with the specified blob storage provider.
    /// </summary>
    /// <param name="provider">The blob storage provider to use.</param>
    /// <returns>A new FluentDynamoDbOptions instance with the specified blob storage provider.</returns>
    public FluentDynamoDbOptions WithBlobStorage(IBlobStorageProvider? provider)
        => new() 
        { 
            Logger = Logger,
            GeospatialProvider = GeospatialProvider,
            BlobStorageProvider = provider,
            FieldEncryptor = FieldEncryptor,
            HydratorRegistry = HydratorRegistry,
            JsonSerializer = JsonSerializer
        };
    
    /// <summary>
    /// Creates a new options instance with the specified field encryptor.
    /// </summary>
    /// <param name="encryptor">The field encryptor to use.</param>
    /// <returns>A new FluentDynamoDbOptions instance with the specified field encryptor.</returns>
    public FluentDynamoDbOptions WithEncryption(IFieldEncryptor? encryptor)
        => new() 
        { 
            Logger = Logger,
            GeospatialProvider = GeospatialProvider,
            BlobStorageProvider = BlobStorageProvider,
            FieldEncryptor = encryptor,
            HydratorRegistry = HydratorRegistry,
            JsonSerializer = JsonSerializer
        };
    
    /// <summary>
    /// Creates a new options instance with the specified geospatial provider.
    /// This method is internal and used by the Geospatial package extension methods.
    /// </summary>
    /// <param name="provider">The geospatial provider to use.</param>
    /// <returns>A new FluentDynamoDbOptions instance with the specified geospatial provider.</returns>
    internal FluentDynamoDbOptions WithGeospatialProvider(IGeospatialProvider? provider)
        => new() 
        { 
            Logger = Logger,
            GeospatialProvider = provider,
            BlobStorageProvider = BlobStorageProvider,
            FieldEncryptor = FieldEncryptor,
            HydratorRegistry = HydratorRegistry,
            JsonSerializer = JsonSerializer
        };
    
    /// <summary>
    /// Creates a new options instance with the specified hydrator registry.
    /// This method is internal and used for testing and advanced scenarios.
    /// </summary>
    /// <param name="registry">The hydrator registry to use.</param>
    /// <returns>A new FluentDynamoDbOptions instance with the specified hydrator registry.</returns>
    internal FluentDynamoDbOptions WithHydratorRegistry(IEntityHydratorRegistry registry)
        => new() 
        { 
            Logger = Logger,
            GeospatialProvider = GeospatialProvider,
            BlobStorageProvider = BlobStorageProvider,
            FieldEncryptor = FieldEncryptor,
            HydratorRegistry = registry,
            JsonSerializer = JsonSerializer
        };

    /// <summary>
    /// Creates a new options instance with the specified JSON serializer.
    /// </summary>
    /// <param name="serializer">The JSON serializer to use for [JsonBlob] properties.</param>
    /// <returns>A new FluentDynamoDbOptions instance with the specified JSON serializer.</returns>
    public FluentDynamoDbOptions WithJsonSerializer(IJsonBlobSerializer? serializer)
        => new() 
        { 
            Logger = Logger,
            GeospatialProvider = GeospatialProvider,
            BlobStorageProvider = BlobStorageProvider,
            FieldEncryptor = FieldEncryptor,
            HydratorRegistry = HydratorRegistry,
            JsonSerializer = serializer
        };
}
