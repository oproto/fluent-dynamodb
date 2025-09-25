using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using Oproto.FluentDynamoDb.Requests.Interfaces;

namespace Oproto.FluentDynamoDb.Requests;

public class UpdateItemRequestBuilder : 
    IWithKey<UpdateItemRequestBuilder>, IWithConditionExpression<UpdateItemRequestBuilder>, IWithAttributeNames<UpdateItemRequestBuilder>, IWithAttributeValues<UpdateItemRequestBuilder>
{
    public UpdateItemRequestBuilder(IAmazonDynamoDB dynamoDbClient)
    {
        _dynamoDbClient = dynamoDbClient;
    }
    
    private UpdateItemRequest _req = new();
    private readonly IAmazonDynamoDB _dynamoDbClient;
    private readonly AttributeValueInternal _attrV = new AttributeValueInternal();
    private readonly AttributeNameInternal _attrN = new AttributeNameInternal();
    
    public UpdateItemRequestBuilder ForTable(string tableName)
    {
        _req.TableName = tableName;
        return this;
    }
    
    public UpdateItemRequestBuilder WithKey(string primaryKeyName, AttributeValue primaryKeyValue, string? sortKeyName=null, AttributeValue? sortKeyValue = null)
    {
        _req.Key = new() { {primaryKeyName, primaryKeyValue } };
        if (sortKeyName!= null && sortKeyValue != null)
        {
            _req.Key.Add(sortKeyName, sortKeyValue);
        }
        return this;
    }

    public UpdateItemRequestBuilder WithKey(string keyName, string keyValue)
    {
        if (_req.Key == null) _req.Key = new();
        _req.Key.Add(keyName, new AttributeValue { S = keyValue });
        return this;
    }
    
    public UpdateItemRequestBuilder WithKey(string primaryKeyName, string primaryKeyValue, string sortKeyName, string sortKeyValue)
    {
        if (_req.Key == null) _req.Key = new();
        _req.Key.Add(primaryKeyName, new AttributeValue { S = primaryKeyValue });
        _req.Key.Add(sortKeyName, new AttributeValue { S = sortKeyValue });
        return this;
    }
    
    public UpdateItemRequestBuilder Where(string conditionExpression)
    {
        _req.ConditionExpression = conditionExpression;
        return this;
    }
    
    public UpdateItemRequestBuilder Set(string updateExpression)
    {
        _req.UpdateExpression = updateExpression;
        return this;
    }
    
    
    public UpdateItemRequestBuilder WithAttributes(Dictionary<string,string> attributeNames)
    {
        _attrN.WithAttributes(attributeNames);
        return this;
    }
    
    public UpdateItemRequestBuilder WithAttributes(Action<Dictionary<string,string>> attributeNameFunc)
    {
        _attrN.WithAttributes(attributeNameFunc);
        return this;
    }

    public UpdateItemRequestBuilder WithAttribute(string parameterName, string attributeName)
    {
        _attrN.WithAttribute(parameterName, attributeName);
        return this;
    }

    public UpdateItemRequestBuilder WithValues(
        Dictionary<string, AttributeValue> attributeValues)
    {
        _attrV.WithValues(attributeValues);
        return this;
    }
    
    public UpdateItemRequestBuilder WithValues(
        Action<Dictionary<string, AttributeValue>> attributeValueFunc)
    {
        _attrV.WithValues(attributeValueFunc);
        return this;
    }
    
    public UpdateItemRequestBuilder WithValue(
        string attributeName, string? attributeValue, bool conditionalUse = true)
    {
        _attrV.WithValue(attributeName, attributeValue, conditionalUse);
        return this;
    }
    
    public UpdateItemRequestBuilder WithValue(
        string attributeName, bool? attributeValue, bool conditionalUse = true)
    {
        _attrV.WithValue(attributeName, attributeValue, conditionalUse);
        return this;
    }
    
    public UpdateItemRequestBuilder WithValue(
        string attributeName, decimal? attributeValue, bool conditionalUse = true)
    {
        _attrV.WithValue(attributeName, attributeValue, conditionalUse);
        return this;
    }
    
    public UpdateItemRequestBuilder WithValue(string attributeName, Dictionary<string, string> attributeValue,
        bool conditionalUse = true)
    {
        _attrV.WithValue(attributeName, attributeValue, conditionalUse);
        return this;
    }
    
    public UpdateItemRequestBuilder WithValue(string attributeName, Dictionary<string, AttributeValue> attributeValue, bool conditionalUse = true)
    {
        _attrV.WithValue(attributeName, attributeValue, conditionalUse);
        return this;
    }

    public UpdateItemRequestBuilder ReturnUpdatedNewValues()
    {
        _req.ReturnValues = ReturnValue.UPDATED_NEW;
        return this;
    }
    
    public UpdateItemRequestBuilder ReturnUpdatedOldValues()
    {
        _req.ReturnValues = ReturnValue.UPDATED_OLD;
        return this;
    }
    
    public UpdateItemRequestBuilder ReturnAllNewValues()
    {
        _req.ReturnValues = ReturnValue.ALL_NEW;
        return this;
    }
    
    public UpdateItemRequestBuilder ReturnAllOldValues()
    {
        _req.ReturnValues = ReturnValue.ALL_OLD;
        return this;
    }
    
    public UpdateItemRequestBuilder ReturnNone()
    {
        _req.ReturnValues = ReturnValue.NONE;
        return this;
    }
    
    public UpdateItemRequestBuilder ReturnTotalConsumedCapacity()
    {
        _req.ReturnConsumedCapacity = Amazon.DynamoDBv2.ReturnConsumedCapacity.TOTAL;
        return this;
    }
    
    public UpdateItemRequestBuilder ReturnConsumedCapacity(ReturnConsumedCapacity consumedCapacity)
    {
        _req.ReturnConsumedCapacity = consumedCapacity;
        return this;
    }

    public UpdateItemRequestBuilder ReturnItemCollectionMetrics()
    {
        _req.ReturnItemCollectionMetrics = Amazon.DynamoDBv2.ReturnItemCollectionMetrics.SIZE;
        return this;
    }

    public UpdateItemRequestBuilder ReturnOldValuesOnConditionCheckFailure()
    {
        _req.ReturnValuesOnConditionCheckFailure = Amazon.DynamoDBv2.ReturnValuesOnConditionCheckFailure.ALL_OLD;
        return this;
    }
    
    public UpdateItemRequest ToUpdateItemRequest()
    {
        _req.ExpressionAttributeNames = _attrN.AttributeNames;
        _req.ExpressionAttributeValues = _attrV.AttributeValues;
        return _req;
    }

    public async Task<UpdateItemResponse> ExecuteAsync(CancellationToken cancellationToken = default)
    {
        return await _dynamoDbClient.UpdateItemAsync(this.ToUpdateItemRequest(), cancellationToken);
    }
}