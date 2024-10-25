using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using Oproto.FluentDynamoDb.Requests.Interfaces;

namespace Oproto.FluentDynamoDb.Requests;

public class PutItemRequestBuilder : IWithAttributeNames<PutItemRequestBuilder>, IWithAttributeValues<PutItemRequestBuilder>,
    IWithConditionExpression<PutItemRequestBuilder>
{
    public PutItemRequestBuilder(IAmazonDynamoDB dynamoDbClient)
    {
        _dynamoDbClient = dynamoDbClient;
    }
    
    private PutItemRequest _req = new PutItemRequest();
    private readonly IAmazonDynamoDB _dynamoDbClient;
    
    public PutItemRequestBuilder ForTable(string tableName)
    {
        _req.TableName = tableName;
        return this;
    }
    
    public PutItemRequestBuilder Where(string conditionExpression)
    {
        _req.ConditionExpression = conditionExpression;
        return this;
    }

    public PutItemRequestBuilder UsingExpressionAttributeNames(Dictionary<string,string> attributeNames)
    {
        _req.ExpressionAttributeNames = attributeNames;
        return this;
    }
    
    public PutItemRequestBuilder UsingExpressionAttributeNames(Action<Dictionary<string,string>> attributeNameFunc)
    {
        var attributeNames = new Dictionary<string, string>();
        attributeNameFunc(attributeNames);
        _req.ExpressionAttributeNames = attributeNames;
        return this;
    }

    public PutItemRequestBuilder WithValues(
        Dictionary<string, AttributeValue> attributeValues)
    {
        _req.ExpressionAttributeValues = attributeValues;
        return this;
    }
    
    public PutItemRequestBuilder WithValues(
        Action<Dictionary<string, AttributeValue>> attributeValueFunc)
    {
        var attributeValues = new Dictionary<string, AttributeValue>();
        attributeValueFunc(attributeValues);
        _req.ExpressionAttributeValues = attributeValues;
        return this;
    }
    
    public PutItemRequestBuilder WithValue(
        string attributeName, string? attributeValue)
    {
        _req.ExpressionAttributeValues ??= new();
        if (attributeValue != null)
        {
            _req.ExpressionAttributeValues.Add(attributeName, new AttributeValue() { S = attributeValue });
        }

        return this;
    }
    
    public PutItemRequestBuilder WithValue(
        string attributeName, bool attributeValue)
    {
        _req.ExpressionAttributeValues ??= new();
        _req.ExpressionAttributeValues.Add(attributeName, new AttributeValue() { BOOL = attributeValue });
        return this;
    }
    
    public PutItemRequestBuilder ReturnUpdatedNewValues()
    {
        _req.ReturnValues = ReturnValue.UPDATED_NEW;
        return this;
    }
    
    public PutItemRequestBuilder ReturnUpdatedOldValues()
    {
        _req.ReturnValues = ReturnValue.UPDATED_OLD;
        return this;
    }
    
    public PutItemRequestBuilder ReturnAllNewValues()
    {
        _req.ReturnValues = ReturnValue.ALL_NEW;
        return this;
    }
    
    public PutItemRequestBuilder ReturnAllOldValues()
    {
        _req.ReturnValues = ReturnValue.ALL_OLD;
        return this;
    }
    
    public PutItemRequestBuilder ReturnNone()
    {
        _req.ReturnValues = ReturnValue.NONE;
        return this;
    }
    
    public PutItemRequestBuilder ReturnTotalConsumedCapacity()
    {
        _req.ReturnConsumedCapacity = Amazon.DynamoDBv2.ReturnConsumedCapacity.TOTAL;
        return this;
    }
    
    public PutItemRequestBuilder ReturnConsumedCapacity(ReturnConsumedCapacity consumedCapacity)
    {
        _req.ReturnConsumedCapacity = consumedCapacity;
        return this;
    }

    public PutItemRequestBuilder ReturnItemCollectionMetrics()
    {
        _req.ReturnItemCollectionMetrics = Amazon.DynamoDBv2.ReturnItemCollectionMetrics.SIZE;
        return this;
    }

    public PutItemRequestBuilder ReturnOldValuesOnConditionCheckFailure()
    {
        _req.ReturnValuesOnConditionCheckFailure = Amazon.DynamoDBv2.ReturnValuesOnConditionCheckFailure.ALL_OLD;
        return this;
    }

    public PutItemRequestBuilder WithItem(Dictionary<string, AttributeValue> item)
    {
        _req.Item = item;
        return this;
    }

    public PutItemRequestBuilder WithItem<TItemType>(TItemType item, Func<TItemType,Dictionary<string, AttributeValue>> modelMapper)
    {
        _req.Item = modelMapper(item);
        return this;
    }
    
    public PutItemRequest ToPutItemRequest()
    {
        return _req;
    }

    public async Task<PutItemResponse> ExecuteAsync(CancellationToken cancellationToken = default)
    {
        return await _dynamoDbClient.PutItemAsync(ToPutItemRequest(), cancellationToken);
    }
}