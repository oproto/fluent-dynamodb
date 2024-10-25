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
    
    
    public UpdateItemRequestBuilder UsingExpressionAttributeNames(Dictionary<string,string> attributeNames)
    {
        _req.ExpressionAttributeNames = attributeNames;
        return this;
    }
    
    public UpdateItemRequestBuilder UsingExpressionAttributeNames(Action<Dictionary<string,string>> attributeNameFunc)
    {
        var attributeNames = new Dictionary<string, string>();
        attributeNameFunc(attributeNames);
        _req.ExpressionAttributeNames = attributeNames;
        return this;
    }

    public UpdateItemRequestBuilder WithValues(
        Dictionary<string, AttributeValue> attributeValues)
    {
        _req.ExpressionAttributeValues = attributeValues;
        return this;
    }
    
    public UpdateItemRequestBuilder WithValues(
        Action<Dictionary<string, AttributeValue>> attributeValueFunc)
    {
        var attributeValues = new Dictionary<string, AttributeValue>();
        attributeValueFunc(attributeValues);
        _req.ExpressionAttributeValues = attributeValues;
        return this;
    }
    
    public UpdateItemRequestBuilder WithValue(
        string attributeName, string? attributeValue)
    {
        _req.ExpressionAttributeValues ??= new();
        if (attributeValue != null)
        {
            _req.ExpressionAttributeValues.Add(attributeName, new AttributeValue() { S = attributeValue });
        }

        return this;
    }
    
    public UpdateItemRequestBuilder WithValue(
        string attributeName, bool attributeValue)
    {
        _req.ExpressionAttributeValues ??= new();
        _req.ExpressionAttributeValues.Add(attributeName, new AttributeValue() { BOOL = attributeValue });
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
        return _req;
    }

    public async Task<UpdateItemResponse> ExecuteAsync(CancellationToken cancellationToken = default)
    {
        return await _dynamoDbClient.UpdateItemAsync(this.ToUpdateItemRequest(), cancellationToken);
    }
}