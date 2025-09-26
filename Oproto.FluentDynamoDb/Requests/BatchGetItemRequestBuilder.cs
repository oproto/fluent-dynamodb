using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;

namespace Oproto.FluentDynamoDb.Requests;

public class BatchGetItemRequestBuilder
{
    private readonly IAmazonDynamoDB _dynamoDbClient;
    private readonly BatchGetItemRequest _req = new();

    public BatchGetItemRequestBuilder(IAmazonDynamoDB dynamoDbClient)
    {
        _dynamoDbClient = dynamoDbClient;
        _req.RequestItems = new Dictionary<string, KeysAndAttributes>();
    }

    public BatchGetItemRequestBuilder GetFromTable(string tableName, Action<BatchGetItemBuilder> builderAction)
    {
        var builder = new BatchGetItemBuilder(tableName);
        builderAction(builder);
        _req.RequestItems[tableName] = builder.ToKeysAndAttributes();
        return this;
    }

    public BatchGetItemRequestBuilder ReturnConsumedCapacity(ReturnConsumedCapacity consumedCapacity)
    {
        _req.ReturnConsumedCapacity = consumedCapacity;
        return this;
    }

    public BatchGetItemRequest ToBatchGetItemRequest()
    {
        return _req;
    }

    public async Task<BatchGetItemResponse> ExecuteAsync(CancellationToken cancellationToken = default)
    {
        return await _dynamoDbClient.BatchGetItemAsync(ToBatchGetItemRequest(), cancellationToken);
    }
}