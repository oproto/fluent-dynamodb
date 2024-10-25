using Amazon.DynamoDBv2.Model;
using Oproto.FluentDynamoDb.Requests.Interfaces;

namespace Oproto.FluentDynamoDb.Requests;

public class TransactConditionCheckBuilder: 
    IWithKey<TransactConditionCheckBuilder>, IWithConditionExpression<TransactConditionCheckBuilder>, IWithAttributeNames<TransactConditionCheckBuilder>, IWithAttributeValues<TransactConditionCheckBuilder>
{
    private readonly TransactWriteItem _req = new TransactWriteItem();
    
    public TransactConditionCheckBuilder(string tableName)
    {
        _req.ConditionCheck = new ConditionCheck();
        _req.ConditionCheck.TableName = tableName;
    }

    
    public TransactConditionCheckBuilder WithKey(string primaryKeyName, AttributeValue primaryKeyValue, string? sortKeyName=null, AttributeValue? sortKeyValue = null)
    {
        _req.ConditionCheck.Key = new() { {primaryKeyName, primaryKeyValue } };
        if (sortKeyName!= null && sortKeyValue != null)
        {
            _req.Delete.Key.Add(sortKeyName, sortKeyValue);
        }
        return this;
    }

    public TransactConditionCheckBuilder WithKey(string keyName, string keyValue)
    {
        if (_req.ConditionCheck.Key == null) _req.ConditionCheck.Key = new();
        _req.ConditionCheck.Key.Add(keyName, new AttributeValue { S = keyValue });
        return this;
    }
    
    public TransactConditionCheckBuilder WithKey(string primaryKeyName, string primaryKeyValue, string sortKeyName, string sortKeyValue)
    {
        if (_req.ConditionCheck.Key == null) _req.ConditionCheck.Key = new();
        _req.ConditionCheck.Key.Add(primaryKeyName, new AttributeValue { S = primaryKeyValue });
        _req.ConditionCheck.Key.Add(sortKeyName, new AttributeValue { S = sortKeyValue });
        return this;
    }
    
    public TransactConditionCheckBuilder Where(string conditionExpression)
    {
        _req.ConditionCheck.ConditionExpression = conditionExpression;
        return this;
    }
    
    public TransactConditionCheckBuilder UsingExpressionAttributeNames(Dictionary<string,string> attributeNames)
    {
        _req.ConditionCheck.ExpressionAttributeNames = attributeNames;
        return this;
    }
    
    public TransactConditionCheckBuilder UsingExpressionAttributeNames(Action<Dictionary<string,string>> attributeNameFunc)
    {
        var attributeNames = new Dictionary<string, string>();
        attributeNameFunc(attributeNames);
        _req.ConditionCheck.ExpressionAttributeNames = attributeNames;
        return this;
    }

    public TransactConditionCheckBuilder WithValues(
        Dictionary<string, AttributeValue> attributeValues)
    {
        _req.ConditionCheck.ExpressionAttributeValues = attributeValues;
        return this;
    }
    
    public TransactConditionCheckBuilder WithValues(
        Action<Dictionary<string, AttributeValue>> attributeValueFunc)
    {
        var attributeValues = new Dictionary<string, AttributeValue>();
        attributeValueFunc(attributeValues);
        _req.ConditionCheck.ExpressionAttributeValues = attributeValues;
        return this;
    }
    
    public TransactConditionCheckBuilder WithValue(
        string attributeName, string? attributeValue)
    {
        _req.ConditionCheck.ExpressionAttributeValues ??= new();
        if (attributeValue != null)
        {
            _req.ConditionCheck.ExpressionAttributeValues.Add(attributeName,
                new AttributeValue() { S = attributeValue });
        }

        return this;
    }
    
    public TransactConditionCheckBuilder WithValue(
        string attributeName, bool attributeValue)
    {
        _req.ConditionCheck.ExpressionAttributeValues ??= new();
        _req.ConditionCheck.ExpressionAttributeValues.Add(attributeName, new AttributeValue() { BOOL = attributeValue });
        return this;
    }
    
    public TransactConditionCheckBuilder ReturnOldValuesOnConditionCheckFailure()
    {
        _req.ConditionCheck.ReturnValuesOnConditionCheckFailure = Amazon.DynamoDBv2.ReturnValuesOnConditionCheckFailure.ALL_OLD;
        return this;
    }

    public TransactWriteItem ToWriteItem()
    {
        return _req;
    }
}