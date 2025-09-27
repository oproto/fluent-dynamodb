using Amazon.DynamoDBv2.Model;

namespace Oproto.FluentDynamoDb.Requests;

/// <summary>
/// Builder for configuring write operations (put and delete) for a single table in a BatchWriteItem operation.
/// This builder is used within BatchWriteItemRequestBuilder to specify which items to put or delete
/// in each table. You can mix put and delete operations for the same table.
/// </summary>
public class BatchWriteItemBuilder
{
    private readonly List<WriteRequest> _writeRequests = new List<WriteRequest>();
    private readonly string _tableName;

    /// <summary>
    /// Initializes a new instance of the BatchWriteItemBuilder for a specific table.
    /// </summary>
    /// <param name="tableName">The name of the table to write to.</param>
    public BatchWriteItemBuilder(string tableName)
    {
        _tableName = tableName;
    }

    /// <summary>
    /// Adds a put operation to write an item to the table.
    /// The item will be created if it doesn't exist, or completely replaced if it does exist.
    /// </summary>
    /// <param name="item">The item to put, represented as a dictionary of attribute names to AttributeValue objects.</param>
    /// <returns>The builder instance for method chaining.</returns>
    public BatchWriteItemBuilder PutItem(Dictionary<string, AttributeValue> item)
    {
        var writeRequest = new WriteRequest
        {
            PutRequest = new PutRequest
            {
                Item = item
            }
        };
        _writeRequests.Add(writeRequest);
        return this;
    }

    /// <summary>
    /// Adds a put operation to write an item to the table using a custom mapper function.
    /// This is useful when you have strongly-typed objects that need to be converted to DynamoDB format.
    /// </summary>
    /// <typeparam name="T">The type of the item to put.</typeparam>
    /// <param name="item">The item to put.</param>
    /// <param name="mapper">A function that converts the item to a dictionary of AttributeValue objects.</param>
    /// <returns>The builder instance for method chaining.</returns>
    /// <example>
    /// <code>
    /// .PutItem(user, u => new Dictionary&lt;string, AttributeValue&gt;
    /// {
    ///     ["id"] = new AttributeValue(u.Id),
    ///     ["name"] = new AttributeValue(u.Name)
    /// })
    /// </code>
    /// </example>
    public BatchWriteItemBuilder PutItem<T>(T item, Func<T, Dictionary<string, AttributeValue>> mapper)
    {
        var mappedItem = mapper(item);
        return PutItem(mappedItem);
    }

    /// <summary>
    /// Adds a delete operation to remove an item from the table.
    /// </summary>
    /// <param name="key">The primary key of the item to delete, represented as a dictionary of attribute names to AttributeValue objects.</param>
    /// <returns>The builder instance for method chaining.</returns>
    public BatchWriteItemBuilder DeleteItem(Dictionary<string, AttributeValue> key)
    {
        var writeRequest = new WriteRequest
        {
            DeleteRequest = new DeleteRequest
            {
                Key = key
            }
        };
        _writeRequests.Add(writeRequest);
        return this;
    }

    /// <summary>
    /// Adds a delete operation to remove an item with a single key attribute.
    /// </summary>
    /// <param name="keyName">The name of the key attribute.</param>
    /// <param name="keyValue">The string value of the key attribute.</param>
    /// <returns>The builder instance for method chaining.</returns>
    public BatchWriteItemBuilder DeleteItem(string keyName, string keyValue)
    {
        var key = new Dictionary<string, AttributeValue>
        {
            { keyName, new AttributeValue { S = keyValue } }
        };
        return DeleteItem(key);
    }

    /// <summary>
    /// Adds a delete operation to remove an item with a composite primary key.
    /// </summary>
    /// <param name="primaryKeyName">The name of the primary key attribute.</param>
    /// <param name="primaryKeyValue">The string value of the primary key attribute.</param>
    /// <param name="sortKeyName">The name of the sort key attribute.</param>
    /// <param name="sortKeyValue">The string value of the sort key attribute.</param>
    /// <returns>The builder instance for method chaining.</returns>
    public BatchWriteItemBuilder DeleteItem(string primaryKeyName, string primaryKeyValue, string sortKeyName, string sortKeyValue)
    {
        var key = new Dictionary<string, AttributeValue>
        {
            { primaryKeyName, new AttributeValue { S = primaryKeyValue } },
            { sortKeyName, new AttributeValue { S = sortKeyValue } }
        };
        return DeleteItem(key);
    }

    /// <summary>
    /// Builds and returns the list of write requests for this table.
    /// This method is used internally by BatchWriteItemRequestBuilder.
    /// </summary>
    /// <returns>A list of WriteRequest objects ready for use in a BatchWriteItem request.</returns>
    public List<WriteRequest> ToWriteRequests()
    {
        return new List<WriteRequest>(_writeRequests);
    }
}