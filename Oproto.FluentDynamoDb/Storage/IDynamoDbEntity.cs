using Amazon.DynamoDBv2.Model;

namespace Oproto.FluentDynamoDb.Storage;

/// <summary>
/// Interface that DynamoDB entities must implement to support automatic mapping.
/// Uses static abstract interface methods for compile-time type safety and AOT compatibility.
/// </summary>
public interface IDynamoDbEntity
{
    /// <summary>
    /// Converts an entity instance to a DynamoDB AttributeValue dictionary.
    /// For multi-item entities, returns the first item from the multi-item conversion.
    /// </summary>
    /// <typeparam name="TSelf">The entity type implementing this interface.</typeparam>
    /// <param name="entity">The entity instance to convert.</param>
    /// <returns>A dictionary of attribute names to AttributeValue objects.</returns>
    static abstract Dictionary<string, AttributeValue> ToDynamoDb<TSelf>(TSelf entity) 
        where TSelf : IDynamoDbEntity;

    /// <summary>
    /// Converts a multi-item entity instance to multiple DynamoDB AttributeValue dictionaries.
    /// Each collection item becomes a separate DynamoDB item with the same partition key.
    /// For single-item entities, returns a list with one item.
    /// </summary>
    /// <typeparam name="TSelf">The entity type implementing this interface.</typeparam>
    /// <param name="entity">The entity instance to convert.</param>
    /// <returns>A list of dictionaries representing multiple DynamoDB items.</returns>
    static abstract List<Dictionary<string, AttributeValue>> ToDynamoDbMultiple<TSelf>(TSelf entity) 
        where TSelf : IDynamoDbEntity;
    
    /// <summary>
    /// Creates an entity instance from a single DynamoDB item.
    /// Used for single-item entities.
    /// </summary>
    /// <typeparam name="TSelf">The entity type implementing this interface.</typeparam>
    /// <param name="item">The DynamoDB item as an AttributeValue dictionary.</param>
    /// <returns>The mapped entity instance.</returns>
    static abstract TSelf FromDynamoDb<TSelf>(Dictionary<string, AttributeValue> item) 
        where TSelf : IDynamoDbEntity;
    
    /// <summary>
    /// Creates an entity instance from multiple DynamoDB items.
    /// Used for multi-item entities where a single logical entity spans multiple DynamoDB items.
    /// </summary>
    /// <typeparam name="TSelf">The entity type implementing this interface.</typeparam>
    /// <param name="items">The collection of DynamoDB items that belong to the same entity.</param>
    /// <returns>The mapped entity instance.</returns>
    static abstract TSelf FromDynamoDb<TSelf>(IList<Dictionary<string, AttributeValue>> items) 
        where TSelf : IDynamoDbEntity;
    
    /// <summary>
    /// Extracts the partition key value from a DynamoDB item.
    /// Used for grouping items that belong to the same entity.
    /// </summary>
    /// <param name="item">The DynamoDB item.</param>
    /// <returns>The partition key value.</returns>
    static abstract string GetPartitionKey(Dictionary<string, AttributeValue> item);
    
    /// <summary>
    /// Determines whether a DynamoDB item matches this entity type.
    /// Used for entity discrimination in multi-type tables.
    /// </summary>
    /// <param name="item">The DynamoDB item to check.</param>
    /// <returns>True if the item matches this entity type, false otherwise.</returns>
    static abstract bool MatchesEntity(Dictionary<string, AttributeValue> item);
    
    /// <summary>
    /// Gets metadata about the entity structure for future LINQ support.
    /// </summary>
    /// <returns>Comprehensive metadata about the entity.</returns>
    static abstract EntityMetadata GetEntityMetadata();
}