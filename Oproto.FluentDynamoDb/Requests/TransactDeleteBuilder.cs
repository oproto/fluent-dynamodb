using Amazon.DynamoDBv2.Model;
using Oproto.FluentDynamoDb.Requests.Interfaces;

namespace Oproto.FluentDynamoDb.Requests;

public class TransactDeleteBuilder : 
    IWithKey<TransactDeleteBuilder>, IWithConditionExpression<TransactDeleteBuilder>, IWithAttributeNames<TransactDeleteBuilder>, IWithAttributeValues<TransactDeleteBuilder>
{
    private readonly TransactWriteItem _req = new TransactWriteItem();
    
    public TransactDeleteBuilder(string tableName)
    {
        _req.Delete = new();
        _req.Delete.TableName = tableName;
    }

    
    public TransactDeleteBuilder WithKey(string primaryKeyName, AttributeValue primaryKeyValue, string? sortKeyName=null, AttributeValue? sortKeyValue = null)
    {
        _req.Delete.Key = new() { {primaryKeyName, primaryKeyValue } };
        if (sortKeyName!= null && sortKeyValue != null)
        {
            _req.Delete.Key.Add(sortKeyName, sortKeyValue);
        }
        return this;
    }

    public TransactDeleteBuilder WithKey(string keyName, string keyValue)
    {
        if (_req.Delete.Key == null) _req.Delete.Key = new();
        _req.Delete.Key.Add(keyName, new AttributeValue { S = keyValue });
        return this;
    }
    
    public TransactDeleteBuilder WithKey(string primaryKeyName, string primaryKeyValue, string sortKeyName, string sortKeyValue)
    {
        if (_req.Delete.Key == null) _req.Delete.Key = new();
        _req.Delete.Key.Add(primaryKeyName, new AttributeValue { S = primaryKeyValue });
        _req.Delete.Key.Add(sortKeyName, new AttributeValue { S = sortKeyValue });
        return this;
    }
    
    public TransactDeleteBuilder Where(string conditionExpression)
    {
        _req.Delete.ConditionExpression = conditionExpression;
        return this;
    }
    
    public TransactDeleteBuilder UsingExpressionAttributeNames(Dictionary<string,string> attributeNames)
    {
        _req.Delete.ExpressionAttributeNames = attributeNames;
        return this;
    }
    
    public TransactDeleteBuilder UsingExpressionAttributeNames(Action<Dictionary<string,string>> attributeNameFunc)
    {
        var attributeNames = new Dictionary<string, string>();
        attributeNameFunc(attributeNames);
        _req.Delete.ExpressionAttributeNames = attributeNames;
        return this;
    }

    public TransactDeleteBuilder WithValues(
        Dictionary<string, AttributeValue> attributeValues)
    {
        _req.Delete.ExpressionAttributeValues = attributeValues;
        return this;
    }
    
    public TransactDeleteBuilder WithValues(
        Action<Dictionary<string, AttributeValue>> attributeValueFunc)
    {
        var attributeValues = new Dictionary<string, AttributeValue>();
        attributeValueFunc(attributeValues);
        _req.Delete.ExpressionAttributeValues = attributeValues;
        return this;
    }
    
    public TransactDeleteBuilder WithValue(
        string attributeName, string? attributeValue)
    {
        _req.Delete.ExpressionAttributeValues ??= new();
        if (attributeValue != null)
        {
            _req.Delete.ExpressionAttributeValues.Add(attributeName, new AttributeValue() { S = attributeValue });
        }

        return this;
    }
    
    public TransactDeleteBuilder WithValue(
        string attributeName, bool attributeValue)
    {
        _req.Delete.ExpressionAttributeValues ??= new();
        _req.Delete.ExpressionAttributeValues.Add(attributeName, new AttributeValue() { BOOL = attributeValue });
        return this;
    }
    
    public TransactDeleteBuilder ReturnOldValuesOnConditionCheckFailure()
    {
        _req.Delete.ReturnValuesOnConditionCheckFailure = Amazon.DynamoDBv2.ReturnValuesOnConditionCheckFailure.ALL_OLD;
        return this;
    }

    public TransactWriteItem ToWriteItem()
    {
        return _req;
    }
}