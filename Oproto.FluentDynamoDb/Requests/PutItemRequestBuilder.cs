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
    private readonly AttributeValueInternal _attrV = new AttributeValueInternal();
    private readonly AttributeNameInternal _attrN = new AttributeNameInternal();
    
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

    public PutItemRequestBuilder WithAttributes(Dictionary<string,string> attributeNames)
    {
        _attrN.WithAttributes(attributeNames);
        return this;
    }
    
    public PutItemRequestBuilder WithAttributes(Action<Dictionary<string,string>> attributeNameFunc)
    {
        _attrN.WithAttributes(attributeNameFunc);
        return this;
    }

    public PutItemRequestBuilder WithAttribute(string parameterName, string attributeName)
    {
        _attrN.WithAttribute(parameterName, attributeName);
        return this;
    }

    public PutItemRequestBuilder WithValues(
        Dictionary<string, AttributeValue> attributeValues)
    {
        _attrV.WithValues(attributeValues);
        return this;
    }
    
    public PutItemRequestBuilder WithValues(
        Action<Dictionary<string, AttributeValue>> attributeValueFunc)
    {
        _attrV.WithValues(attributeValueFunc);
        return this;
    }
    
    public PutItemRequestBuilder WithValue(
        string attributeName, string? attributeValue, bool conditionalUse = true)
    {
        _attrV.WithValue(attributeName, attributeValue, conditionalUse);
        return this;
    }
    
    public PutItemRequestBuilder WithValue(
        string attributeName, bool? attributeValue, bool conditionalUse = true)
    {
        _attrV.WithValue(attributeName, attributeValue, conditionalUse);
        return this;
    }
    
    public PutItemRequestBuilder WithValue(
        string attributeName, decimal? attributeValue, bool conditionalUse = true)
    {
        _attrV.WithValue(attributeName, attributeValue, conditionalUse);
        return this;
    }

    public PutItemRequestBuilder WithValue(string attributeName, Dictionary<string, string> attributeValue,
        bool conditionalUse = true)
    {
        _attrV.WithValue(attributeName, attributeValue, conditionalUse);
        return this;
    }
    
    public PutItemRequestBuilder WithValue(string attributeName, Dictionary<string, AttributeValue> attributeValue, bool conditionalUse = true)
    {
        _attrV.WithValue(attributeName, attributeValue, conditionalUse);
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
        _req.ExpressionAttributeNames = _attrN.AttributeNames;
        _req.ExpressionAttributeValues = _attrV.AttributeValues;
        return _req;
    }

    public async Task<PutItemResponse> ExecuteAsync(CancellationToken cancellationToken = default)
    {
        return await _dynamoDbClient.PutItemAsync(ToPutItemRequest(), cancellationToken);
    }
}