using Amazon.DynamoDBv2.Model;

namespace Oproto.FluentDynamoDb.Requests;

public class BatchWriteItemBuilder
{
    private readonly List<WriteRequest> _writeRequests = new List<WriteRequest>();
    private readonly string _tableName;

    public BatchWriteItemBuilder(string tableName)
    {
        _tableName = tableName;
    }

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

    public BatchWriteItemBuilder PutItem<T>(T item, Func<T, Dictionary<string, AttributeValue>> mapper)
    {
        var mappedItem = mapper(item);
        return PutItem(mappedItem);
    }

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

    public BatchWriteItemBuilder DeleteItem(string keyName, string keyValue)
    {
        var key = new Dictionary<string, AttributeValue>
        {
            { keyName, new AttributeValue { S = keyValue } }
        };
        return DeleteItem(key);
    }

    public BatchWriteItemBuilder DeleteItem(string primaryKeyName, string primaryKeyValue, string sortKeyName, string sortKeyValue)
    {
        var key = new Dictionary<string, AttributeValue>
        {
            { primaryKeyName, new AttributeValue { S = primaryKeyValue } },
            { sortKeyName, new AttributeValue { S = sortKeyValue } }
        };
        return DeleteItem(key);
    }

    public List<WriteRequest> ToWriteRequests()
    {
        return new List<WriteRequest>(_writeRequests);
    }
}