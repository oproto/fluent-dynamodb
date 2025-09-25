using Amazon.DynamoDBv2.Model;
using Oproto.FluentDynamoDb.Requests.Interfaces;

namespace Oproto.FluentDynamoDb.Requests;

public class TransactDeleteBuilder : 
    IWithKey<TransactDeleteBuilder>, IWithConditionExpression<TransactDeleteBuilder>, IWithAttributeNames<TransactDeleteBuilder>, IWithAttributeValues<TransactDeleteBuilder>
{
    private readonly TransactWriteItem _req = new TransactWriteItem();
    private readonly AttributeValueInternal _attrV = new AttributeValueInternal();
    private readonly AttributeNameInternal _attrN = new AttributeNameInternal();
    
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
    
    public TransactDeleteBuilder WithAttributes(Dictionary<string,string> attributeNames)
    {
        _attrN.WithAttributes(attributeNames);
        return this;
    }
    
    public TransactDeleteBuilder WithAttributes(Action<Dictionary<string,string>> attributeNameFunc)
    {
        _attrN.WithAttributes(attributeNameFunc);
        return this;
    }

    public TransactDeleteBuilder WithAttribute(string parameterName, string attributeName)
    {
        _attrN.WithAttribute(parameterName, attributeName);
        return this;
    }

    public TransactDeleteBuilder WithValues(
        Dictionary<string, AttributeValue> attributeValues)
    {
        _attrV.WithValues(attributeValues);
        return this;
    }
    
    public TransactDeleteBuilder WithValues(
        Action<Dictionary<string, AttributeValue>> attributeValueFunc)
    {
        _attrV.WithValues(attributeValueFunc);
        return this;
    }
    
    public TransactDeleteBuilder WithValue(
        string attributeName, string? attributeValue, bool conditionalUse = true)
    {
        _attrV.WithValue(attributeName, attributeValue, conditionalUse);
        return this;
    }
    
    public TransactDeleteBuilder WithValue(
        string attributeName, bool? attributeValue, bool conditionalUse = true)
    {
        _attrV.WithValue(attributeName, attributeValue, conditionalUse);
        return this;
    }
    
    public TransactDeleteBuilder WithValue(
        string attributeName, decimal? attributeValue, bool conditionalUse = true)
    {
        _attrV.WithValue(attributeName, attributeValue, conditionalUse);
        return this;
    }
    
    public TransactDeleteBuilder WithValue(string attributeName, Dictionary<string, string> attributeValue,
        bool conditionalUse = true)
    {
        _attrV.WithValue(attributeName, attributeValue, conditionalUse);
        return this;
    }
    
    public TransactDeleteBuilder WithValue(string attributeName, Dictionary<string, AttributeValue> attributeValue, bool conditionalUse = true)
    {
        _attrV.WithValue(attributeName, attributeValue, conditionalUse);
        return this;
    }
    
    
    
    public TransactDeleteBuilder ReturnOldValuesOnConditionCheckFailure()
    {
        _req.Delete.ReturnValuesOnConditionCheckFailure = Amazon.DynamoDBv2.ReturnValuesOnConditionCheckFailure.ALL_OLD;
        return this;
    }

    public TransactWriteItem ToWriteItem()
    {
        _req.Delete.ExpressionAttributeNames = _attrN.AttributeNames;
        _req.Delete.ExpressionAttributeValues = _attrV.AttributeValues;
        return _req;
    }
}