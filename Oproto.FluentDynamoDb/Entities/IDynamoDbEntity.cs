using Amazon.DynamoDBv2.Model;
using Oproto.FluentDynamoDb.Metadata;
using Oproto.FluentDynamoDb.Providers.BlobStorage;
using Oproto.FluentDynamoDb.Providers.Encryption;

namespace Oproto.FluentDynamoDb.Entities;

/// <summary>
/// Interface that DynamoDB entities must implement to support automatic mapping.
/// Inherits from <see cref="IReadOnlyEntity"/> to provide read operations and adds write operations.
/// Uses static abstract interface methods for compile-time type safety and AOT compatibility.
/// </summary>
/// <remarks>
/// This interface extends <see cref="IReadOnlyEntity"/> with write-specific operations like
/// <see cref="ToDynamoDb{TSelf}"/> and <see cref="MatchesEntity"/>. Full entities implement
/// this interface, while projections (read-only views) implement only <see cref="IReadOnlyEntity"/>.
/// </remarks>
public interface IDynamoDbEntity : IReadOnlyEntity
{
    /// <summary>
    /// Converts an entity instance to a DynamoDB AttributeValue dictionary.
    /// </summary>
    /// <typeparam name="TSelf">The entity type implementing this interface.</typeparam>
    /// <param name="entity">The entity instance to convert.</param>
    /// <param name="options">Optional configuration options including logger, JSON serializer, etc. If null, default behavior is used.</param>
    /// <returns>A dictionary of attribute names to AttributeValue objects.</returns>
    static abstract Dictionary<string, AttributeValue> ToDynamoDb<TSelf>(TSelf entity, FluentDynamoDbOptions? options = null)
        where TSelf : IDynamoDbEntity;

    /// <summary>
    /// Converts an entity instance to a DynamoDB AttributeValue dictionary with KeyInputMode for prefix application.
    /// </summary>
    /// <typeparam name="TSelf">The entity type implementing this interface.</typeparam>
    /// <param name="entity">The entity instance to convert.</param>
    /// <param name="options">Optional configuration options including logger, JSON serializer, etc. If null, default behavior is used.</param>
    /// <param name="keyInputMode">The KeyInputMode controlling how key prefixes are applied during serialization.</param>
    /// <returns>A dictionary of attribute names to AttributeValue objects.</returns>
    static abstract Dictionary<string, AttributeValue> ToDynamoDb<TSelf>(TSelf entity, FluentDynamoDbOptions? options, KeyInputMode keyInputMode)
        where TSelf : IDynamoDbEntity;

    /// <summary>
    /// Creates an entity instance from multiple DynamoDB items.
    /// Used for multi-item entities where a single logical entity spans multiple DynamoDB items.
    /// </summary>
    /// <typeparam name="TSelf">The entity type implementing this interface.</typeparam>
    /// <param name="items">The collection of DynamoDB items that belong to the same entity.</param>
    /// <param name="options">Optional configuration options including logger, JSON serializer, etc. If null, default behavior is used.</param>
    /// <returns>The mapped entity instance.</returns>
    static abstract TSelf FromDynamoDb<TSelf>(IList<Dictionary<string, AttributeValue>> items, FluentDynamoDbOptions? options = null)
        where TSelf : IDynamoDbEntity;

    /// <summary>
    /// Determines whether a DynamoDB item matches this entity type.
    /// Used for entity discrimination in multi-type tables.
    /// </summary>
    /// <param name="item">The DynamoDB item to check.</param>
    /// <returns>True if the item matches this entity type, false otherwise.</returns>
    static abstract bool MatchesEntity(Dictionary<string, AttributeValue> item);

    /// <summary>
    /// Gets whether this entity type requires write operations within a transaction.
    /// Source-generated based on the <see cref="Attributes.RequireWriteTransactionAttribute"/> attribute.
    /// When true, Put, Update, Delete, and BatchWrite operations will throw
    /// <see cref="InvalidOperationException"/> unless performed within a TransactWrite operation.
    /// </summary>
    static abstract bool RequiresWriteTransaction { get; }

    // FromDynamoDb(single item) and GetPartitionKey are inherited from IReadOnlyEntity
    // GetEntityMetadata() is inherited from IEntityMetadataProvider

    /// <summary>
    /// Asynchronously creates an entity instance from multiple DynamoDB items.
    /// Used for composite entity assembly where a single logical entity spans multiple DynamoDB items,
    /// with support for encrypted fields and blob storage properties on both parent and child entities.
    /// </summary>
    /// <typeparam name="TSelf">The entity type implementing this interface.</typeparam>
    /// <param name="items">The collection of DynamoDB items that belong to the same entity.</param>
    /// <param name="blobProvider">Optional blob storage provider for resolving blob references.</param>
    /// <param name="fieldEncryptor">Optional field encryptor for decrypting encrypted properties.</param>
    /// <param name="options">Optional configuration options including logger, JSON serializer, etc.</param>
    /// <param name="cancellationToken">Cancellation token for async operations.</param>
    /// <returns>A task that resolves to the mapped entity instance with populated related collections.</returns>
    static abstract Task<TSelf> FromDynamoDbAsync<TSelf>(
        IList<Dictionary<string, AttributeValue>> items,
        IBlobStorageProvider? blobProvider,
        IFieldEncryptor? fieldEncryptor,
        FluentDynamoDbOptions? options,
        CancellationToken cancellationToken) where TSelf : IDynamoDbEntity;
}
