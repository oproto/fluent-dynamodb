using Amazon.DynamoDBv2.Model;
using Oproto.FluentDynamoDb.Storage;

namespace Oproto.FluentDynamoDb.Requests;

/// <summary>
/// Response wrapper for batch get operations providing type-safe deserialization.
/// Provides indexed access to items with explicit type parameters for AOT-safe deserialization.
/// Maintains item order across multiple tables and exposes unprocessed keys for retry logic.
/// </summary>
/// <example>
/// <code>
/// var response = await DynamoDbBatch.Get
///     .Add(userTable.Get(userId1))
///     .Add(userTable.Get(userId2))
///     .Add(orderTable.Get(orderId))
///     .ExecuteAsync();
/// 
/// var users = response.GetItemsRange&lt;User&gt;(0, 1);
/// var order = response.GetItem&lt;Order&gt;(2);
/// 
/// if (response.HasUnprocessedKeys)
/// {
///     // Implement retry logic
/// }
/// </code>
/// </example>
public class BatchGetResponse
{
    private readonly BatchGetItemResponse _response;
    private readonly List<Dictionary<string, AttributeValue>?> _items;
    private readonly Dictionary<string, KeysAndAttributes> _unprocessedKeys;
    private readonly FluentDynamoDbOptions? _options;

    /// <summary>
    /// Initializes a new instance of the BatchGetResponse class.
    /// </summary>
    /// <param name="response">The underlying AWS SDK response.</param>
    /// <param name="tableOrder">The order of tables as items were added to the batch.</param>
    /// <param name="requestedKeys">The keys that were requested, in order.</param>
    /// <param name="options">Optional configuration options for entity deserialization (JSON, logging, etc.).</param>
    internal BatchGetResponse(
        BatchGetItemResponse response, 
        List<string> tableOrder,
        List<Dictionary<string, AttributeValue>> requestedKeys,
        FluentDynamoDbOptions? options = null)
    {
        _response = response ?? throw new ArgumentNullException(nameof(response));
        _options = options;
        _unprocessedKeys = response.UnprocessedKeys ?? new Dictionary<string, KeysAndAttributes>();
        
        // Flatten items in the order they were requested by matching keys
        _items = new List<Dictionary<string, AttributeValue>?>();
        
        for (int i = 0; i < tableOrder.Count; i++)
        {
            var tableName = tableOrder[i];
            var requestedKey = requestedKeys[i];
            
            if (response.Responses.TryGetValue(tableName, out var tableItems))
            {
                // Find the item that matches the requested key
                var matchingItem = tableItems.FirstOrDefault(item => KeysMatch(item, requestedKey));
                _items.Add(matchingItem);
            }
            else
            {
                // Table not in response (item not found)
                _items.Add(null);
            }
        }
    }
    
    /// <summary>
    /// Checks if two DynamoDB keys match by comparing their key attributes.
    /// </summary>
    private static bool KeysMatch(Dictionary<string, AttributeValue>? item, Dictionary<string, AttributeValue> key)
    {
        if (item == null)
        {
            return false;
        }
        
        foreach (var kvp in key)
        {
            if (!item.TryGetValue(kvp.Key, out var itemValue))
            {
                return false;
            }
            
            // Compare attribute values
            if (kvp.Value.S != itemValue.S ||
                kvp.Value.N != itemValue.N ||
                kvp.Value.B != itemValue.B)
            {
                return false;
            }
        }
        
        return true;
    }

    /// <summary>
    /// Gets the underlying AWS SDK response.
    /// Provides access to consumed capacity and other metadata.
    /// </summary>
    public BatchGetItemResponse RawResponse => _response;

    /// <summary>
    /// Gets unprocessed keys that need to be retried.
    /// These keys were not processed due to provisioned throughput limits or other transient issues.
    /// </summary>
    /// <example>
    /// <code>
    /// if (response.HasUnprocessedKeys)
    /// {
    ///     var unprocessedKeys = response.UnprocessedKeys;
    ///     // Implement exponential backoff retry logic
    /// }
    /// </code>
    /// </example>
    public Dictionary<string, KeysAndAttributes> UnprocessedKeys => _unprocessedKeys;

    /// <summary>
    /// Indicates whether there are unprocessed keys that need to be retried.
    /// </summary>
    public bool HasUnprocessedKeys => _unprocessedKeys.Count > 0;

    /// <summary>
    /// Gets the total number of items in the response.
    /// </summary>
    public int Count => _items.Count;

    /// <summary>
    /// Deserializes a single item at the specified index.
    /// Uses the source-generated FromDynamoDb method for AOT-safe deserialization.
    /// </summary>
    /// <typeparam name="TEntity">The entity type to deserialize to. Must have a FromDynamoDb method.</typeparam>
    /// <param name="index">The zero-based index of the item.</param>
    /// <returns>The deserialized entity, or null if the item is missing or empty.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when index is out of range.</exception>
    /// <exception cref="DynamoDbMappingException">Thrown when deserialization fails.</exception>
    /// <example>
    /// <code>
    /// var user = response.GetItem&lt;User&gt;(0);
    /// if (user != null)
    /// {
    ///     Console.WriteLine($"User: {user.Name}");
    /// }
    /// </code>
    /// </example>
    public TEntity? GetItem<TEntity>(int index) where TEntity : class, IDynamoDbEntity
    {
        if (index < 0 || index >= _items.Count)
        {
            throw new ArgumentOutOfRangeException(
                nameof(index), 
                $"Index {index} is out of range. Response contains {_items.Count} items.");
        }

        var item = _items[index];
        if (item == null || item.Count == 0)
        {
            return null;
        }

        try
        {
            // Call the static abstract FromDynamoDb method directly (AOT-safe)
            return TEntity.FromDynamoDb<TEntity>(item, _options);
        }
        catch (DynamoDbMappingException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new DynamoDbMappingException(
                $"Failed to deserialize item at index {index} to type {typeof(TEntity).Name}.",
                typeof(TEntity),
                MappingOperation.FromDynamoDb,
                item,
                innerException: ex)
                .WithContext("Index", index);
        }
    }

    /// <summary>
    /// Deserializes multiple items of the same type at the specified indices.
    /// </summary>
    /// <typeparam name="TEntity">The entity type to deserialize to.</typeparam>
    /// <param name="indices">The zero-based indices of the items to retrieve.</param>
    /// <returns>A list of deserialized entities (nulls for missing items).</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when any index is out of range.</exception>
    /// <exception cref="DynamoDbMappingException">Thrown when deserialization fails for any item.</exception>
    /// <example>
    /// <code>
    /// var users = response.GetItems&lt;User&gt;(0, 2, 4);
    /// foreach (var user in users.Where(u => u != null))
    /// {
    ///     Console.WriteLine($"User: {user.Name}");
    /// }
    /// </code>
    /// </example>
    public List<TEntity?> GetItems<TEntity>(params int[] indices) where TEntity : class, IDynamoDbEntity
    {
        if (indices == null || indices.Length == 0)
        {
            return new List<TEntity?>();
        }

        return indices.Select(i => GetItem<TEntity>(i)).ToList();
    }

    /// <summary>
    /// Deserializes a contiguous range of items of the same type.
    /// </summary>
    /// <typeparam name="TEntity">The entity type to deserialize to.</typeparam>
    /// <param name="startIndex">The zero-based start index (inclusive).</param>
    /// <param name="endIndex">The zero-based end index (inclusive).</param>
    /// <returns>A list of deserialized entities (nulls for missing items).</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when indices are out of range or invalid.</exception>
    /// <exception cref="DynamoDbMappingException">Thrown when deserialization fails for any item.</exception>
    /// <example>
    /// <code>
    /// // Get items 0, 1, and 2
    /// var users = response.GetItemsRange&lt;User&gt;(0, 2);
    /// </code>
    /// </example>
    public List<TEntity?> GetItemsRange<TEntity>(int startIndex, int endIndex) where TEntity : class, IDynamoDbEntity
    {
        if (startIndex < 0 || startIndex >= _items.Count)
        {
            throw new ArgumentOutOfRangeException(
                nameof(startIndex),
                $"Start index {startIndex} is out of range. Response contains {_items.Count} items.");
        }
        
        if (endIndex < startIndex || endIndex >= _items.Count)
        {
            throw new ArgumentOutOfRangeException(
                nameof(endIndex),
                $"End index {endIndex} is invalid. Must be >= {startIndex} and < {_items.Count}.");
        }

        var result = new List<TEntity?>();
        for (int i = startIndex; i <= endIndex; i++)
        {
            result.Add(GetItem<TEntity>(i));
        }
        return result;
    }
}
