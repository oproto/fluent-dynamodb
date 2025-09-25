using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using Oproto.FluentDynamoDb.Storage;

namespace Oproto.FluentDynamoDb.Requests;

public class TransactGetItemsRequestBuilder
{
    public TransactGetItemsRequestBuilder(IAmazonDynamoDB dynamoDbClient)
    {
        _dynamoDbClient = dynamoDbClient;
    }
    
    private readonly IAmazonDynamoDB _dynamoDbClient;
    private readonly TransactGetItemsRequest _req = new();
    
    
    public TransactGetItemsRequestBuilder ReturnConsumedCapacity(ReturnConsumedCapacity consumedCapacity)
    {
        _req.ReturnConsumedCapacity = consumedCapacity;
        return this;
    }
    
    public TransactGetItemsRequestBuilder Get(DynamoDbTableBase table,
        Action<TransactGetItemBuilder> builderExpression)
    {
        TransactGetItemBuilder builder = new(table.Name);
        builderExpression(builder);
        _req.TransactItems.Add(builder.ToGetItem());
        return this;
    }
    
    public TransactGetItemsRequestBuilder AddTransactItem(TransactGetItem item)
    {
        _req.TransactItems.Add(item);
        return this;
    }

    public TransactGetItemsRequest ToTransactGetItemsRequest()
    {
        return _req;
    }

    public async Task<TransactGetItemsResponse> ExecuteAsync(CancellationToken cancellationToken = default)
    {
        return await _dynamoDbClient.TransactGetItemsAsync(this.ToTransactGetItemsRequest(), cancellationToken);
    }
}