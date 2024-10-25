using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using Oproto.FluentDynamoDb.Storage;

namespace Oproto.FluentDynamoDb.Requests;

public class TransactWriteItemsRequestBuilder
{
    private readonly IAmazonDynamoDB _dynamoDbClient;
    private readonly TransactWriteItemsRequest _req = new();

    public TransactWriteItemsRequestBuilder(IAmazonDynamoDB dynamoDbClient)
    {
        _dynamoDbClient = dynamoDbClient;
    }

    public TransactWriteItemsRequestBuilder WithClientRequestToken(string token)
    {
        _req.ClientRequestToken = token;
        return this;
    }

    public TransactWriteItemsRequestBuilder ReturnTotalConsumedCapacity()
    {
        _req.ReturnConsumedCapacity = Amazon.DynamoDBv2.ReturnConsumedCapacity.TOTAL;
        return this;
    }

    public TransactWriteItemsRequestBuilder ReturnConsumedCapacity(ReturnConsumedCapacity consumedCapacity)
    {
        _req.ReturnConsumedCapacity = consumedCapacity;
        return this;
    }

    public TransactWriteItemsRequestBuilder ReturnItemCollectionMetrics()
    {
        _req.ReturnItemCollectionMetrics = Amazon.DynamoDBv2.ReturnItemCollectionMetrics.SIZE;
        return this;
    }

    public TransactWriteItemsRequestBuilder CheckCondition(DynamoDbTableBase table,
        Action<TransactConditionCheckBuilder> builderExpression)
    {
        TransactConditionCheckBuilder builder = new(table.Name);
        builderExpression(builder);
        _req.TransactItems.Add(builder.ToWriteItem());
        return this;
    }

    public TransactWriteItemsRequestBuilder Delete(DynamoDbTableBase table,
        Action<TransactDeleteBuilder> builderExpression)
    {
        TransactDeleteBuilder builder = new(table.Name);
        builderExpression(builder);
        _req.TransactItems.Add(builder.ToWriteItem());
        return this;
    }

    public TransactWriteItemsRequestBuilder Put(DynamoDbTableBase table, Action<TransactPutBuilder> builderExpression)
    {
        TransactPutBuilder builder = new(table.Name);
        builderExpression(builder);
        _req.TransactItems.Add(builder.ToWriteItem());
        return this;
    }

    public TransactWriteItemsRequestBuilder Update(DynamoDbTableBase table,
        Action<TransactUpdateBuilder> builderExpression)
    {
        TransactUpdateBuilder builder = new TransactUpdateBuilder(table.Name);
        builderExpression(builder);
        _req.TransactItems.Add(builder.ToWriteItem());
        return this;
    }

    public TransactWriteItemsRequestBuilder AddTransactItem(TransactWriteItem item)
    {
        _req.TransactItems.Add(item);
        return this;
    }

    public TransactWriteItemsRequest ToTransactWriteItemsRequest()
    {
        return _req;
    }

    public async Task<TransactWriteItemsResponse> ExecuteAsync(CancellationToken cancellationToken = default)
    {
        return await _dynamoDbClient.TransactWriteItemsAsync(this.ToTransactWriteItemsRequest(), cancellationToken);
    }
}