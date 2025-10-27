using Amazon.DynamoDBv2.Model;
using Amazon.Runtime;

namespace Oproto.FluentDynamoDb.Storage;

/// <summary>
/// Contains metadata and response details from a DynamoDB operation.
/// This class is populated automatically by Primary API methods and accessible via DynamoDbOperationContext.Current.
/// </summary>
public class OperationContextData
{
    // === Operation Metadata ===
    
    /// <summary>
    /// The type of operation that was executed (Query, GetItem, UpdateItem, etc.).
    /// </summary>
    public string? OperationType { get; set; }

    /// <summary>
    /// The table name the operation was executed against.
    /// </summary>
    public string? TableName { get; set; }

    /// <summary>
    /// The index name if the operation used an index.
    /// </summary>
    public string? IndexName { get; set; }

    // === Capacity & Performance ===
    
    /// <summary>
    /// Consumed capacity information from the operation.
    /// Available when ReturnConsumedCapacity is configured on the request.
    /// </summary>
    public ConsumedCapacity? ConsumedCapacity { get; set; }

    /// <summary>
    /// Number of items returned by the operation (for Query/Scan).
    /// </summary>
    public int? ItemCount { get; set; }

    /// <summary>
    /// Number of items evaluated before applying filter expression (for Query/Scan).
    /// </summary>
    public int? ScannedCount { get; set; }

    /// <summary>
    /// Item collection metrics (for operations that modify indexed attributes).
    /// Available when ReturnItemCollectionMetrics is configured on the request.
    /// </summary>
    public ItemCollectionMetrics? ItemCollectionMetrics { get; set; }

    // === Pagination ===
    
    /// <summary>
    /// The last evaluated key for pagination (for Query/Scan).
    /// Null if there are no more pages.
    /// </summary>
    public Dictionary<string, AttributeValue>? LastEvaluatedKey { get; set; }

    // === Raw Response Data ===
    
    /// <summary>
    /// Raw items from Query/Scan operations before deserialization.
    /// This is a reference to the response object's Items collection.
    /// </summary>
    public List<Dictionary<string, AttributeValue>>? RawItems { get; set; }

    /// <summary>
    /// Raw item from GetItem operation before deserialization.
    /// This is a reference to the response object's Item dictionary.
    /// </summary>
    public Dictionary<string, AttributeValue>? RawItem { get; set; }

    // === Pre/Post Operation Values ===
    
    /// <summary>
    /// Attribute values before the operation (from ReturnValues = ALL_OLD or UPDATED_OLD).
    /// Available for UpdateItem, DeleteItem, and PutItem operations when ReturnValues is configured.
    /// </summary>
    public Dictionary<string, AttributeValue>? PreOperationValues { get; set; }

    /// <summary>
    /// Attribute values after the operation (from ReturnValues = ALL_NEW or UPDATED_NEW).
    /// Available for UpdateItem and PutItem operations when ReturnValues is configured.
    /// </summary>
    public Dictionary<string, AttributeValue>? PostOperationValues { get; set; }

    // === Encryption Context (Migrated) ===
    
    /// <summary>
    /// Encryption context identifier (e.g., tenant ID, customer ID).
    /// This replaces the standalone EncryptionContext.Current property.
    /// </summary>
    public string? EncryptionContextId { get; set; }

    // === Response Metadata ===
    
    /// <summary>
    /// AWS response metadata (request ID, etc.).
    /// </summary>
    public ResponseMetadata? ResponseMetadata { get; set; }

    // === Deserialization Helpers ===
    
    /// <summary>
    /// Deserializes the RawItem to a strongly-typed entity.
    /// Returns null if RawItem is null or doesn't match the entity type.
    /// </summary>
    /// <typeparam name="T">The entity type to deserialize to. Must implement IDynamoDbEntity.</typeparam>
    /// <returns>The deserialized entity, or null if RawItem is null or doesn't match.</returns>
    /// <exception cref="DynamoDbMappingException">Thrown if deserialization fails.</exception>
    public T? DeserializeRawItem<T>() where T : class, IDynamoDbEntity
    {
        try
        {
            if (RawItem == null || !T.MatchesEntity(RawItem))
                return null;
            
            return T.FromDynamoDb<T>(RawItem);
        }
        catch (Exception ex)
        {
            throw new DynamoDbMappingException(
                $"Failed to deserialize raw item to {typeof(T).Name}. Error: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Deserializes the RawItems collection to strongly-typed entities.
    /// Filters out items that don't match the entity type.
    /// </summary>
    /// <typeparam name="T">The entity type to deserialize to. Must implement IDynamoDbEntity.</typeparam>
    /// <returns>A list of deserialized entities. Returns empty list if RawItems is null.</returns>
    /// <exception cref="DynamoDbMappingException">Thrown if deserialization fails for any item.</exception>
    public List<T> DeserializeRawItems<T>() where T : class, IDynamoDbEntity
    {
        try
        {
            if (RawItems == null)
                return new List<T>();
            
            return RawItems
                .Where(T.MatchesEntity)
                .Select(item => T.FromDynamoDb<T>(item))
                .ToList();
        }
        catch (Exception ex)
        {
            throw new DynamoDbMappingException(
                $"Failed to deserialize raw items to {typeof(T).Name}. Error: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Deserializes the PreOperationValues to a strongly-typed entity.
    /// Returns null if PreOperationValues is null or doesn't match the entity type.
    /// </summary>
    /// <typeparam name="T">The entity type to deserialize to. Must implement IDynamoDbEntity.</typeparam>
    /// <returns>The deserialized entity, or null if PreOperationValues is null or doesn't match.</returns>
    /// <exception cref="DynamoDbMappingException">Thrown if deserialization fails.</exception>
    public T? DeserializePreOperationValue<T>() where T : class, IDynamoDbEntity
    {
        try
        {
            if (PreOperationValues == null || !T.MatchesEntity(PreOperationValues))
                return null;
            
            return T.FromDynamoDb<T>(PreOperationValues);
        }
        catch (Exception ex)
        {
            throw new DynamoDbMappingException(
                $"Failed to deserialize pre-operation values to {typeof(T).Name}. Error: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Deserializes the PostOperationValues to a strongly-typed entity.
    /// Returns null if PostOperationValues is null or doesn't match the entity type.
    /// </summary>
    /// <typeparam name="T">The entity type to deserialize to. Must implement IDynamoDbEntity.</typeparam>
    /// <returns>The deserialized entity, or null if PostOperationValues is null or doesn't match.</returns>
    /// <exception cref="DynamoDbMappingException">Thrown if deserialization fails.</exception>
    public T? DeserializePostOperationValue<T>() where T : class, IDynamoDbEntity
    {
        try
        {
            if (PostOperationValues == null || !T.MatchesEntity(PostOperationValues))
                return null;
            
            return T.FromDynamoDb<T>(PostOperationValues);
        }
        catch (Exception ex)
        {
            throw new DynamoDbMappingException(
                $"Failed to deserialize post-operation values to {typeof(T).Name}. Error: {ex.Message}", ex);
        }
    }
}
