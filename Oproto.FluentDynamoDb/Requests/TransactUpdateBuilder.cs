using Amazon.DynamoDBv2.Model;
using Oproto.FluentDynamoDb.Requests.Interfaces;

namespace Oproto.FluentDynamoDb.Requests;

public class TransactUpdateBuilder : 
    IWithKey<TransactUpdateBuilder>, IWithConditionExpression<TransactUpdateBuilder>, IWithAttributeNames<TransactUpdateBuilder>, IWithAttributeValues<TransactUpdateBuilder>
{
    private readonly TransactWriteItem _req = new TransactWriteItem();
    
    public TransactUpdateBuilder(string tableName)
    {
        _req.Update = new Update();
        _req.Update.TableName = tableName;
    }

    
    public TransactUpdateBuilder WithKey(string primaryKeyName, AttributeValue primaryKeyValue, string? sortKeyName=null, AttributeValue? sortKeyValue = null)
    {
        _req.Update.Key = new() { {primaryKeyName, primaryKeyValue } };
        if (sortKeyName!= null && sortKeyValue != null)
        {
            _req.Update.Key.Add(sortKeyName, sortKeyValue);
        }
        return this;
    }

    public TransactUpdateBuilder WithKey(string keyName, string keyValue)
    {
        if (_req.Update.Key == null) _req.Update.Key = new();
        _req.Update.Key.Add(keyName, new AttributeValue { S = keyValue });
        return this;
    }
    
    public TransactUpdateBuilder WithKey(string primaryKeyName, string primaryKeyValue, string sortKeyName, string sortKeyValue)
    {
        if (_req.Update.Key == null) _req.Update.Key = new();
        _req.Update.Key.Add(primaryKeyName, new AttributeValue { S = primaryKeyValue });
        _req.Update.Key.Add(sortKeyName, new AttributeValue { S = sortKeyValue });
        return this;
    }
    
    public TransactUpdateBuilder Where(string conditionExpression)
    {
        _req.Update.ConditionExpression = conditionExpression;
        return this;
    }
    
    public TransactUpdateBuilder Set(string updateExpression)
    {
        _req.Update.UpdateExpression = updateExpression;
        return this;
    }

    
    public TransactUpdateBuilder UsingExpressionAttributeNames(Dictionary<string,string> attributeNames)
    {
        _req.Update.ExpressionAttributeNames = attributeNames;
        return this;
    }
    
    public TransactUpdateBuilder UsingExpressionAttributeNames(Action<Dictionary<string,string>> attributeNameFunc)
    {
        var attributeNames = new Dictionary<string, string>();
        attributeNameFunc(attributeNames);
        _req.Update.ExpressionAttributeNames = attributeNames;
        return this;
    }

    public TransactUpdateBuilder WithValues(
        Dictionary<string, AttributeValue> attributeValues)
    {
        _req.Update.ExpressionAttributeValues = attributeValues;
        return this;
    }
    
    public TransactUpdateBuilder WithValues(
        Action<Dictionary<string, AttributeValue>> attributeValueFunc)
    {
        var attributeValues = new Dictionary<string, AttributeValue>();
        attributeValueFunc(attributeValues);
        _req.Update.ExpressionAttributeValues = attributeValues;
        return this;
    }
    
    public TransactUpdateBuilder WithValue(
        string attributeName, string? attributeValue)
    {
        _req.Update.ExpressionAttributeValues ??= new();
        if (attributeValue != null)
        {
            _req.Update.ExpressionAttributeValues.Add(attributeName, new AttributeValue() { S = attributeValue });
        }

        return this;
    }
    
    public TransactUpdateBuilder WithValue(
        string attributeName, bool attributeValue)
    {
        _req.Update.ExpressionAttributeValues ??= new();
        _req.Update.ExpressionAttributeValues.Add(attributeName, new AttributeValue() { BOOL = attributeValue });
        return this;
    }
    
    public TransactUpdateBuilder ReturnOldValuesOnConditionCheckFailure()
    {
        _req.Update.ReturnValuesOnConditionCheckFailure = Amazon.DynamoDBv2.ReturnValuesOnConditionCheckFailure.ALL_OLD;
        return this;
    }

    public TransactWriteItem ToWriteItem()
    {
        return _req;
    }
}