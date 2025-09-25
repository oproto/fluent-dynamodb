using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using Oproto.FluentDynamoDb.Requests.Interfaces;

namespace Oproto.FluentDynamoDb.Requests;

public class GetItemRequestBuilder : IWithKey<GetItemRequestBuilder>, IWithAttributeNames<GetItemRequestBuilder>
{
    public GetItemRequestBuilder(IAmazonDynamoDB dynamoDbClient)
    {
        _dynamoDbClient = dynamoDbClient;
    }
    
    private GetItemRequest _req = new GetItemRequest();
    private readonly IAmazonDynamoDB _dynamoDbClient;
    private readonly AttributeNameInternal _attrN = new AttributeNameInternal();
    
    public GetItemRequestBuilder ForTable(string tableName)
    {
        _req.TableName = tableName;
        return this;
    }

    public GetItemRequestBuilder WithKey(string primaryKeyName, AttributeValue primaryKeyValue, string? sortKeyName=null, AttributeValue? sortKeyValue = null)
    {
        _req.Key = new() { {primaryKeyName, primaryKeyValue } };
        if (sortKeyName!= null && sortKeyValue != null)
        {
            _req.Key.Add(sortKeyName, sortKeyValue);
        }
        return this;
    }

    public GetItemRequestBuilder WithKey(string keyName, string keyValue)
    {
        if (_req.Key == null) _req.Key = new();
        _req.Key.Add(keyName, new AttributeValue { S = keyValue });
        return this;
    }
    
    public GetItemRequestBuilder WithKey(string primaryKeyName, string primaryKeyValue, string sortKeyName, string sortKeyValue)
    {
        if (_req.Key == null) _req.Key = new();
        _req.Key.Add(primaryKeyName, new AttributeValue { S = primaryKeyValue });
        _req.Key.Add(sortKeyName, new AttributeValue { S = sortKeyValue });
        return this;
    }
    
    public GetItemRequestBuilder WithAttributes(Dictionary<string,string> attributeNames)
    {
        _attrN.WithAttributes(attributeNames);
        return this;
    }
    
    public GetItemRequestBuilder WithAttributes(Action<Dictionary<string,string>> attributeNameFunc)
    {
        _attrN.WithAttributes(attributeNameFunc);
        return this;
    }

    public GetItemRequestBuilder WithAttribute(string parameterName, string attributeName)
    {
        _attrN.WithAttribute(parameterName, attributeName);
        return this;
    }

    public GetItemRequestBuilder UsingConsistentRead()
    {
        _req.ConsistentRead = true;
        return this;
    }

    public GetItemRequestBuilder WithProjection(string projectionExpression)
    {
        _req.ProjectionExpression = projectionExpression;
        return this;
    }

    public GetItemRequestBuilder ReturnTotalConsumedCapacity()
    {
        _req.ReturnConsumedCapacity = Amazon.DynamoDBv2.ReturnConsumedCapacity.TOTAL;
        return this;
    }
    
    public GetItemRequestBuilder ReturnConsumedCapacity(ReturnConsumedCapacity consumedCapacity)
    {
        _req.ReturnConsumedCapacity = consumedCapacity;
        return this;
    }

    public GetItemRequest ToGetItemRequest()
    {
        _req.ExpressionAttributeNames = _attrN.AttributeNames;
        return _req;
    }
    
    public async Task<GetItemResponse> ExecuteAsync(CancellationToken cancellationToken = default)
    {
        return await _dynamoDbClient.GetItemAsync(this.ToGetItemRequest(), cancellationToken);
    }
}