using Amazon.DynamoDBv2.Model;

namespace Oproto.FluentDynamoDb.Storage;

/// <summary>
/// Interface for async entity hydration and serialization with blob storage support.
/// Implemented by source-generated code for entities with blob references.
/// </summary>
/// <typeparam name="TEntity">The entity type to hydrate/serialize.</typeparam>
public interface IAsyncEntityHydrator<TEntity> where TEntity : class
{
    /// <summary>
    /// Hydrates an entity from DynamoDB attributes, loading blob references.
    /// </summary>
    /// <param name="item">The DynamoDB item attributes.</param>
    /// <param name="blobProvider">The blob storage provider for loading blob references.</param>
    /// <param name="options">Optional configuration options including logger, JSON serializer, etc.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The hydrated entity.</returns>
    Task<TEntity> HydrateAsync(
        Dictionary<string, AttributeValue> item,
        IBlobStorageProvider blobProvider,
        FluentDynamoDbOptions? options = null,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Hydrates an entity from multiple DynamoDB items (composite entities).
    /// </summary>
    /// <param name="items">The list of DynamoDB item attributes.</param>
    /// <param name="blobProvider">The blob storage provider for loading blob references.</param>
    /// <param name="options">Optional configuration options including logger, JSON serializer, etc.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The hydrated entity.</returns>
    Task<TEntity> HydrateAsync(
        IList<Dictionary<string, AttributeValue>> items,
        IBlobStorageProvider blobProvider,
        FluentDynamoDbOptions? options = null,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Serializes an entity to DynamoDB attributes, storing blob references.
    /// </summary>
    /// <param name="entity">The entity to serialize.</param>
    /// <param name="blobProvider">The blob storage provider for storing blob references.</param>
    /// <param name="options">Optional configuration options including logger, JSON serializer, etc.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The DynamoDB attributes.</returns>
    Task<Dictionary<string, AttributeValue>> SerializeAsync(
        TEntity entity,
        IBlobStorageProvider blobProvider,
        FluentDynamoDbOptions? options = null,
        CancellationToken cancellationToken = default);
}
