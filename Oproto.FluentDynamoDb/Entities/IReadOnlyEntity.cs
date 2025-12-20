using Amazon.DynamoDBv2.Model;
using Oproto.FluentDynamoDb.Metadata;

namespace Oproto.FluentDynamoDb.Entities;

/// <summary>
/// Interface for read-only entity types that support querying and reading operations.
/// Implemented by both projections and full entities to provide consistent API access.
/// Uses static abstract interface methods for compile-time type safety and AOT compatibility.
/// </summary>
/// <remarks>
/// This interface defines the minimal contract for types that can be read from DynamoDB.
/// It is the base interface for <see cref="IDynamoDbEntity"/> which adds write operations.
/// Projections implement this interface directly since they only support read operations.
/// </remarks>
public interface IReadOnlyEntity : IEntityMetadataProvider
{
    /// <summary>
    /// Creates an entity instance from a single DynamoDB item.
    /// Used for single-item entities and projections.
    /// </summary>
    /// <typeparam name="TSelf">The entity type implementing this interface.</typeparam>
    /// <param name="item">The DynamoDB item as an AttributeValue dictionary.</param>
    /// <param name="options">Optional configuration options including logger, JSON serializer, etc. If null, default behavior is used.</param>
    /// <returns>The mapped entity instance.</returns>
    static abstract TSelf FromDynamoDb<TSelf>(Dictionary<string, AttributeValue> item, FluentDynamoDbOptions? options = null)
        where TSelf : IReadOnlyEntity;

    /// <summary>
    /// Extracts the partition key value from a DynamoDB item.
    /// Used for grouping items that belong to the same entity.
    /// </summary>
    /// <param name="item">The DynamoDB item.</param>
    /// <returns>The partition key value.</returns>
    static abstract string GetPartitionKey(Dictionary<string, AttributeValue> item);
}
